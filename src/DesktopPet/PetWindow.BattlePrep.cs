using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DesktopPet;

// Battle Mode's setup panel: menu → Start battle opens it, Ready starts the fight, Close goes
// back to the pet's normal life. While it is open nothing fights yet (no slime, timer, damage or
// record) and it owns the pet's free time: no random actions, sleep, away nap, AI commands,
// games or chatter (behavior runs as in Quiet mode). Sleep, waking, a drag or a flight already
// going on are not cut; Ready just waits until the cat is free, checked again when pressed.
// The panel never takes focus and is used with the mouse only, so it gets no keys from other apps.
// Layout: a preview of this cat holding the chosen weapon (its own model, never the real cat;
// Try it swings once), one card per weapon in the battle data (art, name, hint, four stats on
// fixed scales), the difficulty, then How to play, Close and the main button, Ready.
public sealed partial class PetWindow
{
    private bool battlePreparing;
    private Window? battlePrepWindow;
    private string prepWeapon = BattleWeapon.Sword;
    private BattleDifficulty prepDifficulty = BattleDifficulty.Normal;
    private double prepRefresh;
    private readonly List<(string Id, Border Card, Path Check, TextBlock Name, TextBlock Hint, TextBlock[] StatNames, TextBlock[] StatValues)> prepCards = new();
    private readonly List<(BattleDifficulty Level, Border Chip, Path Check, TextBlock Text)> prepLevelChips = new();
    private TextBlock? prepTitle, prepWeaponLabel, prepLevelLabel, prepData, prepStatus, prepReadyText, prepGuideText, prepCancelText, prepTryText;
    private Border? prepReady;
    // The preview: its own model and renderer, animated by its own timer only while Try it runs.
    private PetModel? prepPreview;
    private PetRenderer? prepPreviewView;
    private PreviewHitMark? prepHitMark;
    private DispatcherTimer? prepPreviewTimer;
    private double prepPreviewTime;
    private static readonly Brush PrepChosen = PetUi.Frozen(Color.FromRgb(0x3E, 0x7B, 0xE0));
    private static readonly Brush PrepCardBack = PetUi.Frozen(Color.FromArgb(0x30, 255, 255, 255));
    private static readonly Brush PrepCardRim = PetUi.Frozen(Color.FromArgb(0x60, 255, 255, 255));
    private static readonly Brush PrepWarning = PetUi.Frozen(Color.FromRgb(0xFF, 0xC8, 0x6A));
    private static readonly Brush PrepBarBack = PetUi.Frozen(Color.FromArgb(0x40, 255, 255, 255));
    private static readonly Brush PrepBarFill = PetUi.Frozen(Color.FromRgb(0xFF, 0xD3, 0x5C));
    private static readonly Brush PrepStage = PetUi.Frozen(Color.FromArgb(0x26, 255, 255, 255));
    private static readonly Geometry CheckMark = PetUi.FrozenGeometry("M 1,6.5 L 5,10.5 L 13,2");
    public const double BattlePrepRefreshSeconds = 0.2;
    // Fixed scales for the card bars (the same for every weapon, so bars compare honestly; a
    // custom weapon past the end shows a full bar and its real number).
    public static readonly (string Vi, string En, double Max)[] WeaponStatScales =
        { ("Sát thương", "Damage", 60), ("Tốc độ", "Speed", 2), ("Tầm", "Reach", 120), ("Đẩy lùi", "Knock-back", 300) };

    public bool BattlePreparing => battlePreparing;
    // The battle or its setup owns the pet (PetWindow.Battle.cs has the battle's own rules).
    public bool BattleOwnsPet => battle != null || battlePreparing || adventureMapOpen;
    public string BattlePrepWeapon => prepWeapon;
    public BattleDifficulty BattlePrepDifficulty => prepDifficulty;
    public bool BattlePrepVisible => battlePrepWindow?.IsVisible == true;
    public bool BattlePrepReadyEnabled => prepReady?.IsEnabled == true;
    public int BattlePrepCards => prepCards.Count;
    // For checks: the preview's own model, and whether Try it is running.
    public PetModel? BattlePrepPreview => prepPreview;
    public bool BattlePrepPreviewPlaying => prepPreviewTimer != null;
    public string BattlePrepText => string.Join("\n", new[] { prepTitle, prepData, prepStatus, prepTryText, prepGuideText, prepCancelText, prepReadyText }
        .Where(t => t is { Visibility: Visibility.Visible }).Select(t => t!.Text)
        .Concat(prepCards.SelectMany(c => new[] { c.Name.Text, c.Hint.Text }.Concat(c.StatValues.Select(v => v.Text))))
        .Concat(prepLevelChips.Select(c => c.Text.Text)));

    // The numbers a card shows, from the weapon's own data (what the battle really uses).
    public static (double Damage, double Speed, double Reach, double Knock) WeaponStats(WeaponSpec w) =>
        (w.Damage, 1 / Math.Max(w.AttackCooldown, w.SwingDuration), w.Range, w.Knock);

    // Null when the panel may open now. Sleep, a drag or a game do not stop it (Ready waits for them).
    public string? BattlePrepRefusal()
    {
        if (battle != null) return L("Trận đang diễn ra.", "A battle is already running.");
        if (closed) return L("Mèo đã đóng.", "The pet is closed.");
        if (paused) return L("Mèo đang tạm dừng.", "The pet is paused.");
        if (context.Fullscreen) return L("Đang có ứng dụng toàn màn hình.", "A fullscreen app is open.");
        return null;
    }

    // Null when the panel is open (opening it twice keeps the one panel), otherwise why not.
    // Without a choice given it starts from the remembered one (the last that went into a battle).
    // stage: an Adventure stage (looked up again in the fresh data; gone from it or locked: refused).
    public string? OpenBattlePrep(string? weapon = null, BattleDifficulty? level = null, StageSpec? stage = null)
    {
        if (BattlePrepRefusal() is string reason) return reason;
        if (battlePreparing && prepStage?.Id == stage?.Id) { PlaceBattlePrep(); return null; }
        if (battlePreparing) { CloseBattlePrep(); CloseBattleTutorial(); }
        ReloadBattleData();
        if (stage != null)
        {
            if (Adventure?.Stage(stage.Id) is not StageSpec fresh || !Adventure!.IsUnlocked(fresh.Id))
                return L("Màn này không còn trong dữ liệu hoặc chưa mở.", "This stage is no longer in the data or is locked.");
            stage = fresh;
        }
        CloseAdventureMap();
        prepStage = stage;
        prepWeapon = BattleTuning.Weapon(weapon ?? preferences.BattleWeapon).Id;
        prepDifficulty = level is BattleDifficulty chosen && Enum.IsDefined(chosen) ? chosen : BattleDifficultyChoice;
        battlePreparing = true;
        CloseBattleSummary(); CloseBattleNotice();
        queuedCommand = null;
        revision++; // An AI reply chosen before the setup must not move the cat now.
        pet.Speech.Hide(); pet.Thought.Clear();
        ApplyQuiet();
        ShowBattlePrepWindow();
        // The guide opens by itself once, with the first setup (no battle is waiting behind it).
        if (!preferences.BattleTutorialSeen) ShowBattleTutorial();
        System.Diagnostics.Trace.WriteLine("Battle setup opened.");
        return null;
    }

    public bool BattlePrepChooseWeapon(string id)
    {
        if (!battlePreparing || BattleTuning.FindWeapon(id) is not WeaponSpec weapon) return false;
        prepWeapon = weapon.Id;
        StopPreviewSwing();
        UpdateBattlePrep();
        return true;
    }

    public bool BattlePrepChooseDifficulty(BattleDifficulty level)
    {
        if (!battlePreparing || !Enum.IsDefined(level)) return false;
        prepDifficulty = level;
        UpdateBattlePrep();
        return true;
    }

    // Ready: checks the cat again now, remembers the choice and starts the fight. Null on success.
    // A second press (double click) finds no panel and does nothing.
    public string? BattlePrepReady()
    {
        if (!battlePreparing || battle != null) return L("Không có bảng chuẩn bị.", "No battle setup is open.");
        if (BattleRefusal() is string reason) { UpdateBattlePrep(); return reason; }
        SetBattleWeapon(prepWeapon);
        SetBattleDifficulty(prepDifficulty);
        if (BattleTutorialOpen) CloseBattleTutorial(finished: true);
        return StartBattle(stage: prepStage);
    }

    // Close: no battle, no win or loss, no record; the pet goes back to its own life. For an
    // Adventure stage the button says Map and goes back to the map instead.
    public void CancelBattlePrep()
    {
        if (!battlePreparing) return;
        var stage = prepStage;
        CloseBattlePrep();
        CloseBattleTutorial();
        if (stage != null && OpenAdventureMap(stage.Id) == null) return;
        revision++;
        behavior.Reset(pet);
        System.Diagnostics.Trace.WriteLine("Battle setup cancelled.");
    }

    // Removes the panel (Ready, Close, reset, closing the pet). Leaves the pet's state alone.
    private void CloseBattlePrep()
    {
        bool was = battlePreparing;
        battlePreparing = false;
        StopPreviewSwing();
        battlePrepWindow?.Close();
        battlePrepWindow = null;
        prepReady = null;
        prepPreview = null; prepPreviewView = null; prepHitMark = null;
        prepCards.Clear(); prepLevelChips.Clear();
        prepStage = null;
        if (was) ApplyQuiet();
    }

    // Every frame, paused or not: a fullscreen app hides the panel (it comes back after), and
    // Ready's state follows the cat a few times a second.
    private void TickBattlePrep(double dt)
    {
        if (!battlePreparing || battlePrepWindow == null) return;
        if (context.Fullscreen) { StopPreviewSwing(); if (battlePrepWindow.IsVisible) battlePrepWindow.Hide(); return; }
        if (!battlePrepWindow.IsVisible) { battlePrepWindow.Show(); PlaceBattlePrep(); }
        prepRefresh += dt;
        if (prepRefresh < BattlePrepRefreshSeconds) return;
        prepRefresh = 0;
        UpdateBattlePrep();
    }

    // Try it: the preview cat swings the chosen weapon once, at the weapon's own pace and hit
    // moment, with its hit mark. Its own timer and model only; nothing in a battle or the real cat.
    public bool BattlePrepTryWeapon()
    {
        if (!battlePreparing || prepPreview == null || prepPreviewView == null) return false;
        StopPreviewSwing();
        var weapon = BattleTuning.Weapon(prepWeapon);
        prepPreviewTime = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        DateTime last = DateTime.UtcNow;
        timer.Tick += (_, _) =>
        {
            var now = DateTime.UtcNow;
            StepPreviewSwing(Math.Min(0.05, (now - last).TotalSeconds), weapon);
            last = now;
        };
        prepPreviewTimer = timer;
        StepPreviewSwing(0, weapon);
        timer.Start();
        PlayPetSound("swoosh"); // The usual swing cue (mute, volume and cooldown apply).
        return true;
    }

    // One preview frame (also driven directly by checks).
    public void StepPreviewSwing(double dt, WeaponSpec weapon)
    {
        if (prepPreview == null) return;
        prepPreviewTime += dt;
        double total = weapon.SwingDuration + 0.25; // A short pause after the swing, with the mark fading.
        prepPreview.SwordSwing = prepPreviewTime < weapon.SwingDuration ? prepPreviewTime / weapon.SwingDuration : -1;
        prepPreview.AnimationTime += dt; prepPreview.ActionTime += dt;
        prepPreviewView?.InvalidateVisual();
        prepHitMark?.Show(weapon.Effect, prepPreviewTime - weapon.HitTime);
        if (prepPreviewTime >= total) StopPreviewSwing();
    }

    private void StopPreviewSwing()
    {
        prepPreviewTimer?.Stop();
        prepPreviewTimer = null;
        if (prepPreview != null) { prepPreview.SwordSwing = -1; prepPreviewView?.InvalidateVisual(); }
        prepHitMark?.Show("", -1);
    }

    private void ShowBattlePrepWindow()
    {
        TextBlock Line(double size, bool bold = false, double maxWidth = 330) => new()
        {
            Foreground = Brushes.White, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap, MaxWidth = maxWidth
        };
        Path Check() => new() { Data = CheckMark, Stroke = Brushes.White, StrokeThickness = 2.4, StrokeEndLineCap = PenLineCap.Round,
            StrokeStartLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Width = 15, Height = 13, Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center };
        prepTitle = Line(16, true);
        prepWeaponLabel = Line(11.5); prepWeaponLabel.Opacity = 0.75; prepWeaponLabel.Margin = new Thickness(0, 0, 0, 4);
        prepLevelLabel = Line(11.5); prepLevelLabel.Opacity = 0.75; prepLevelLabel.Margin = new Thickness(0, 8, 0, 4);
        prepData = Line(11.5, maxWidth: 470); prepData.Foreground = PrepWarning; prepData.Margin = new Thickness(0, 8, 0, 0);
        prepStatus = Line(12, true, 470); prepStatus.Foreground = PrepWarning; prepStatus.Margin = new Thickness(0, 8, 0, 0);

        // The preview: this cat (coat, accessory) holding the chosen weapon, on a soft stage.
        prepPreview = new PetModel { Action = PetAction.Idle, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1, SwordOut = true };
        prepPreviewView = new PetRenderer(prepPreview) { Coat = renderer.Coat, Accessory = renderer.Accessory, Width = 192, Height = 192 };
        prepHitMark = new PreviewHitMark { Width = 192, Height = 192, IsHitTestVisible = false };
        var (tryButton, tryText, _) = HudButton(() => BattlePrepTryWeapon(), plain: true);
        tryButton.HorizontalAlignment = HorizontalAlignment.Center; tryButton.Margin = new Thickness(0, 4, 0, 0);
        prepTryText = tryText;
        var stage = new StackPanel
        {
            Width = 150, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                new Border { Background = PrepStage, CornerRadius = new CornerRadius(75), Width = 150, Height = 150,
                    Child = CatPicture(new Grid { Width = 192, Height = 192, Children = { prepPreviewView, prepHitMark } }) },
                tryButton
            }
        };

        // One card per weapon in the data (nothing hard-coded per id).
        var cards = new StackPanel();
        prepCards.Clear();
        foreach (var weapon in BattleTuning.WeaponList)
        {
            string id = weapon.Id;
            var check = Check();
            var name = Line(13.5, true, 200);
            var hint = Line(11.5, maxWidth: 230); hint.Opacity = 0.85;
            var stats = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            stats.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            stats.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var statNames = new TextBlock[WeaponStatScales.Length];
            var statValues = new TextBlock[WeaponStatScales.Length];
            var values = WeaponStats(weapon);
            double[] numbers = { values.Damage, values.Speed, values.Reach, values.Knock };
            for (int i = 0; i < WeaponStatScales.Length; i++)
            {
                stats.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                statNames[i] = new TextBlock { Foreground = Brushes.White, Opacity = 0.8, FontSize = 10.5, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
                double fraction = Math.Clamp(numbers[i] / WeaponStatScales[i].Max, 0, 1);
                var bar = new Grid { Height = 5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 2),
                    Children = { new Border { Background = PrepBarBack, CornerRadius = new CornerRadius(2.5) },
                                 new Border { Background = PrepBarFill, CornerRadius = new CornerRadius(2.5), HorizontalAlignment = HorizontalAlignment.Left, Width = 64 * fraction } } };
                statValues[i] = new TextBlock { Foreground = Brushes.White, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                    Text = i == 1 ? numbers[i].ToString("0.0", CultureInfo.InvariantCulture) + "/s" : numbers[i].ToString("0", CultureInfo.InvariantCulture) };
                Grid.SetRow(statNames[i], i); Grid.SetRow(bar, i); Grid.SetRow(statValues[i], i);
                Grid.SetColumn(bar, 1); Grid.SetColumn(statValues[i], 2);
                stats.Children.Add(statNames[i]); stats.Children.Add(bar); stats.Children.Add(statValues[i]);
            }
            var icon = new WeaponIcon { Hammer = weapon.Style == "hammer", Width = 46, Height = 52, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top };
            var card = new Border
            {
                CornerRadius = new CornerRadius(9), Padding = new Thickness(9, 7, 10, 7), Margin = new Thickness(0, 0, 0, 6), Cursor = System.Windows.Input.Cursors.Hand,
                Child = new DockPanel
                {
                    Children = { icon, new StackPanel { Children = { new StackPanel { Orientation = Orientation.Horizontal, Children = { check, name } }, hint, stats } } }
                }
            };
            DockPanel.SetDock(icon, Dock.Left);
            card.MouseLeftButtonDown += (_, e) => { BattlePrepChooseWeapon(id); e.Handled = true; };
            cards.Children.Add(card);
            prepCards.Add((id, card, check, name, hint, statNames, statValues));
        }

        var levels = new StackPanel { Orientation = Orientation.Horizontal };
        prepLevelChips.Clear();
        foreach (var level in new[] { BattleDifficulty.Easy, BattleDifficulty.Normal, BattleDifficulty.Hard })
        {
            var check = Check();
            var text = new TextBlock { Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            var chip = new Border
            {
                CornerRadius = new CornerRadius(7), Padding = new Thickness(9, 4, 11, 4), Margin = new Thickness(0, 0, 6, 0), BorderThickness = new Thickness(1.5),
                Cursor = System.Windows.Input.Cursors.Hand, Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { check, text } }
            };
            chip.MouseLeftButtonDown += (_, e) => { BattlePrepChooseDifficulty(level); e.Handled = true; };
            levels.Children.Add(chip);
            prepLevelChips.Add((level, chip, check, text));
        }

        // Actions: a quiet How to play link on the left; Close, then the one main button, Ready.
        prepGuideText = new TextBlock { Foreground = Brushes.White, Opacity = 0.85, FontSize = 12, TextDecorations = TextDecorations.Underline,
            Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent };
        prepGuideText.MouseLeftButtonDown += (_, e) => { ShowBattleTutorial(); e.Handled = true; };
        var (cancel, cancelText, _) = HudButton(CancelBattlePrep, plain: true);
        var (ready, readyText, _) = HudButton(() => BattlePrepReady(), plain: true);
        ready.Background = PrepChosen; ready.Padding = new Thickness(18, 6, 18, 5); ready.MinWidth = 110; ready.Margin = new Thickness(0);
        readyText.FontSize = 14; readyText.FontWeight = FontWeights.Bold;
        prepReady = ready; prepReadyText = readyText; prepCancelText = cancelText;
        var actions = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
        DockPanel.SetDock(prepGuideText, Dock.Left); DockPanel.SetDock(ready, Dock.Right); DockPanel.SetDock(cancel, Dock.Right);
        actions.Children.Add(prepGuideText); actions.Children.Add(ready); actions.Children.Add(cancel);

        var choices = new StackPanel { Width = 300, Children = { prepWeaponLabel, cards, prepLevelLabel, levels } };
        var panel = new Border
        {
            Background = HudBack, BorderBrush = Brushes.White, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(6),
            Child = new StackPanel { Children = { prepTitle, new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), Children = { stage, choices } },
                prepData, prepStatus, actions } },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 }
        };
        panel.SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
        var window = CreateBattleWindow(panel, "Pet setup");
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.Closed += (_, _) => { if (battlePrepWindow == window) { StopPreviewSwing(); battlePrepWindow = null; } };
        battlePrepWindow = window;
        UpdateBattlePrep();
        window.Show();
        PlaceBattlePrep();
    }

    // At the top of the cat's screen, centred (where the HUD and the result panel appear).
    private void PlaceBattlePrep()
    {
        if (battlePrepWindow == null) return;
        battlePrepWindow.UpdateLayout();
        double width = battlePrepWindow.ActualWidth;
        MoveWindow(battlePrepWindow, Math.Clamp((bounds.Left + bounds.Right - width) / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - width)), bounds.Top + 12);
    }

    private void UpdateBattlePrep()
    {
        if (!battlePreparing || prepTitle == null || prepReady == null) return;
        static void Show(TextBlock? text, string? value)
        {
            if (text == null) return;
            if (text.Text != (value ?? "")) text.Text = value ?? "";
            text.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
        var chosen = BattleTuning.Weapon(prepWeapon);
        prepTitle.Text = prepStage is StageSpec stage ? StageTitle(stage) + (stage.Goal(English) is { Length: > 0 } goal ? "\n" + goal : "") : L("Chuẩn bị trận", "Get ready to battle");
        prepWeaponLabel!.Text = L("Chọn cách đánh", "Choose how to fight");
        prepLevelLabel!.Text = L("Độ khó", "Difficulty");
        foreach (var (id, card, check, name, hint, statNames, _) in prepCards)
        {
            var weapon = BattleTuning.Weapon(id);
            bool selected = id == prepWeapon;
            name.Text = weapon.Name(English);
            hint.Text = weapon.Hint(English);
            for (int i = 0; i < statNames.Length; i++) statNames[i].Text = English ? WeaponStatScales[i].En : WeaponStatScales[i].Vi;
            // Chosen: a thick white rim and a tick, not only a colour.
            card.Background = selected ? PrepChosen : PrepCardBack;
            card.BorderBrush = selected ? Brushes.White : PrepCardRim;
            card.BorderThickness = new Thickness(selected ? 2.5 : 1);
            check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
        foreach (var (level, chip, check, text) in prepLevelChips)
        {
            bool selected = level == prepDifficulty;
            text.Text = DifficultyName(level);
            chip.Background = selected ? PrepChosen : PrepCardBack;
            chip.BorderBrush = selected ? Brushes.White : PrepCardRim;
            check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
        if (prepPreview != null && (prepPreview.Hammer != (chosen.Style == "hammer") || prepPreview.SwordHitAt != chosen.HitFraction))
        {
            prepPreview.Hammer = chosen.Style == "hammer";
            prepPreview.SwordHitAt = chosen.HitFraction;
            prepPreviewView?.InvalidateVisual();
        }
        Show(prepData, BattleDataError != null
            ? L("File battle.json của bạn có lỗi nên dùng dữ liệu gốc: ", "Your battle.json has a mistake, so the built-in battle is used: ") + BattleDataError
            : BattleDataCustom ? L("Đang dùng dữ liệu trận tùy chỉnh (battle.json).", "Using your custom battle data (battle.json).") : null);
        string? waiting = BattleRefusal();
        Show(prepStatus, waiting != null ? L("Chưa thể bắt đầu: ", "Not yet: ") + waiting : null);
        prepReadyText!.Text = L("Sẵn sàng", "Ready");
        prepGuideText!.Text = L("Cách chơi", "How to play");
        prepCancelText!.Text = prepStage != null ? L("Bản đồ", "Map") : L("Đóng", "Close");
        prepTryText!.Text = L("Xem thử", "Try it");
        bool can = waiting == null;
        if (prepReady.IsEnabled != can) { prepReady.IsEnabled = can; prepReady.Opacity = can ? 1 : 0.5; }
    }
}

// A weapon's art for a setup card (the same drawing as in the cat's paw).
public sealed class WeaponIcon : FrameworkElement
{
    public bool Hammer { get; init; }
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new TranslateTransform(ActualWidth / 2 - 10, ActualHeight / 2 + 17));
        PetRenderer.DrawWeaponIcon(dc, Hammer);
        dc.Pop();
    }
}

// The preview's hit mark, at the weapon's tip in front of the cat: a white burst ("spark") or
// floor dust and a ring ("thud"), for 0.25 s from the hit moment.
public sealed class PreviewHitMark : FrameworkElement
{
    private string effect = "";
    private double age = -1;
    private static readonly Pen Spark = PetUi.FrozenPen(Brushes.White, 3);
    private static readonly Pen Ink = PetUi.FrozenPen(PetUi.Frozen(Color.FromRgb(0x30, 0x2B, 0x48)), 1.4);
    public string Showing => age >= 0 && age <= 0.25 ? effect : "";

    public void Show(string kind, double sinceHit)
    {
        string before = Showing;
        effect = kind; age = sinceHit;
        if (before != Showing || Showing != "") InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Showing == "") return;
        double k = Math.Clamp(age / 0.25, 0, 1);
        dc.PushOpacity(1 - k);
        // In the 192-DIP pet frame: in front of the cat at paw height (sword) or on the floor (hammer).
        if (effect == "thud")
        {
            double x = 168, floor = 150, spread = 10 + 16 * k;
            dc.DrawEllipse(null, Spark, new Point(x, floor), spread, spread * 0.3);
            for (int i = 0; i < 4; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                dc.DrawEllipse(Brushes.White, Ink, new Point(x + side * (8 + 12 * k + 5 * (i / 2)), floor - 5 - 9 * k * (1 + i / 2)), 3.5 * (1 - k) + 1.5, 3.5 * (1 - k) + 1.5);
            }
        }
        else
        {
            double x = 168, y = 118;
            for (int i = 0; i < 6; i++)
            {
                double a = i * Math.PI / 3 + 0.3;
                dc.DrawLine(Spark, new Point(x + Math.Cos(a) * (5 + 8 * k), y + Math.Sin(a) * (5 + 8 * k)), new Point(x + Math.Cos(a) * (11 + 11 * k), y + Math.Sin(a) * (11 + 11 * k)));
            }
        }
        dc.Pop();
    }
}
