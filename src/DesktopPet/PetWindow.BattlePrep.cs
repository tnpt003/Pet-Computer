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

// Battle Mode's setup panel: an Adventure stage (from its map) or a new Expedition run (its map's
// Prepare) opens it; Ready starts the fight (or the run), Map goes back to that map; the free battle's
// Close goes back to the pet's normal life. While it is open nothing fights yet (no slime, timer, damage or
// record) and it owns the pet's free time: no random actions, sleep, away nap, AI commands,
// games or chatter (behavior runs as in Quiet mode). Sleep, waking, a drag or a flight already
// going on are not cut; Ready just waits until the cat is free, checked again when pressed.
// The panel never takes focus and is used with the mouse only, so it gets no keys from other apps.
// Layout: a preview of this cat holding the chosen weapon (its own model, never the real cat;
// Try it swings once), one card per weapon in the battle data (art, name, hint, four stats on
// fixed scales), the difficulty (with one to three stars), then How to play, Close and the main button, Ready.
public sealed partial class PetWindow
{
    private bool battlePreparing;
    // The panel prepares a new Expedition run (Ready starts the run, not a battle).
    private bool prepForExpedition;
    public bool BattlePrepForExpedition => battlePreparing && prepForExpedition;
    // The setup of an Infinity run (from the hub): Ready starts it, the cancel button goes back to the hub.
    private bool prepForEndless;
    public bool BattlePrepForEndless => battlePreparing && prepForEndless;
    private Window? battlePrepWindow;
    private string prepWeapon = BattleWeapon.Sword;
    private BattleDifficulty prepDifficulty = BattleDifficulty.Normal;
    // The app icons above the battlefield (not for a free battle, which has none).
    private bool prepApps = true;
    private Border? prepAppsChip;
    private Path? prepAppsCheck;
    private TextBlock? prepAppsLabel, prepAppsText, prepAppsHint;
    // The Adventure Guide for this stage (PetWindow.BattleGuide.cs): preferences.AdventureGuide, kept on Ready.
    private bool prepGuide;
    private Border? prepGuideChip;
    private Path? prepGuideCheck;
    private TextBlock? prepGuideLabel, prepGuideHint;
    // The five skills (1.5): ten chips, the five first in the top row; a skill not open yet shows a lock.
    // Hidden for a chapter 1 stage (always the five first skills); Ready needs exactly five.
    private readonly HashSet<BattleSkill> prepSkills = new();
    private BattleSkill? prepSkillHover;
    private readonly List<(BattleSkill Skill, Border Chip, UIElement Lock)> prepSkillChips = new();
    private TextBlock? prepSkillsLabel, prepSkillsHint;
    private FrameworkElement? prepSkillsGrid;
    private double prepRefresh;
    private readonly List<(string Id, Border Card, Path Check, TextBlock Name, TextBlock Hint, TextBlock[] StatNames, TextBlock[] StatValues)> prepCards = new();
    private readonly List<(BattleDifficulty Level, Border Chip, Path Check, TextBlock Text)> prepLevelChips = new();
    private TextBlock? prepTitle, prepWeaponLabel, prepLevelLabel, prepData, prepStatus, prepReadyText, prepGuideText, prepCancelText, prepTryText, prepTipText, prepBuild;
    private Border? prepReady;
    // The preview: its own model and renderer, animated by its own timer only while Try it runs.
    private PetModel? prepPreview;
    private PetRenderer? prepPreviewView;
    private PreviewHitMark? prepHitMark;
    private DispatcherTimer? prepPreviewTimer;
    private double prepPreviewTime;
    private static readonly Brush PrepChosen = PetUi.Game.Chosen;
    private static readonly Brush PrepCardBack = PetUi.Game.CardBack;
    private static readonly Brush PrepCardRim = PetUi.Game.CardRim;
    private static readonly Brush PrepWarning = PetUi.Game.Warning;
    private static readonly Brush PrepBarBack = PetUi.Game.BarBack;
    private static readonly Brush PrepBarFill = PetUi.Game.Gold;
    private static readonly Brush PrepStage = PetUi.Game.Stage;
    private static readonly Geometry CheckMark = PetUi.FrozenGeometry("M 1,6.5 L 5,10.5 L 13,2");
    private static readonly Geometry DifficultyStar = PetUi.FrozenGeometry("M 5,0 L 6.2,3.4 L 9.8,3.5 L 7,5.7 L 8,9.2 L 5,7.1 L 2,9.2 L 3,5.7 L 0.2,3.5 L 3.8,3.4 Z");
    public const double BattlePrepRefreshSeconds = 0.2;
    // Fixed scales for the card bars (the same for every weapon, so bars compare honestly; a
    // custom weapon past the end shows a full bar and its real number).
    public static readonly (string Vi, string En, double Max)[] WeaponStatScales =
        { ("Sát thương", "Damage", 60), ("Tốc độ", "Speed", 2), ("Tầm", "Reach", 120), ("Đẩy lùi", "Knock-back", 300) };

    public bool BattlePreparing => battlePreparing;
    // The battle or its setup owns the pet (PetWindow.Battle.cs has the battle's own rules).
    public bool BattleOwnsPet => battle != null || battleHubOpen || battlePreparing || adventureMapOpen || expeditionMapOpen;
    public string BattlePrepWeapon => prepWeapon;
    public BattleDifficulty BattlePrepDifficulty => prepDifficulty;
    public bool BattlePrepApps => prepApps;
    public bool BattlePrepGuide => prepGuide;
    public bool BattlePrepHasGuide => StageHasGuide(prepStage);
    public bool BattlePrepHasSkillChoice => battlePreparing && (prepForExpedition || prepForEndless || StageSkillChoice(prepStage));
    public IReadOnlyList<BattleSkill> BattlePrepSkills => BattleSkills.All.Select(info => info.Skill).Where(prepSkills.Contains).ToArray();
    public string BattlePrepSkillsHint => prepSkillsHint?.Text ?? "";
    private bool PrepHasApps => prepStage != null || prepForExpedition || prepForEndless;
    private bool PrepFree => prepStage == null && !prepForExpedition && !prepForEndless;
    public bool BattlePrepVisible => battlePrepWindow?.IsVisible == true;
    public bool BattlePrepReadyEnabled => prepReady?.IsEnabled == true;
    public int BattlePrepCards => prepCards.Count;
    // For checks: the preview's own model, and whether Try it is running.
    public PetModel? BattlePrepPreview => prepPreview;
    public bool BattlePrepPreviewPlaying => prepPreviewTimer != null;
    public string BattlePrepText => string.Join("\n", new[] { prepTitle, prepBuild, prepData, prepStatus, prepTryText, prepGuideText, prepTipText, prepCancelText, prepReadyText }
        .Where(t => t is { Visibility: Visibility.Visible }).Select(t => t!.Text)
        .Concat(prepCards.SelectMany(c => new[] { c.Name.Text, c.Hint.Text }.Concat(c.StatValues.Select(v => v.Text))))
        .Concat(prepLevelChips.Select(c => c.Text.Text))
        .Concat(new[] { prepSkillsLabel, prepSkillsHint }.Where(t => t is { Visibility: Visibility.Visible }).Select(t => t!.Text)));

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
    // forExpedition: the loadout of a new Expedition run (from its map's Prepare): Ready starts the
    // run instead of a battle and goes back to the map, as does the Map button.
    public string? OpenBattlePrep(string? weapon = null, BattleDifficulty? level = null, StageSpec? stage = null, bool forExpedition = false, bool forEndless = false)
    {
        if (BattlePrepRefusal() is string reason) return reason;
        if (forExpedition || forEndless || stage?.Endless == true) { forEndless |= stage?.Endless == true; stage = null; }
        if (battlePreparing && prepStage?.Id == stage?.Id && prepForExpedition == forExpedition && prepForEndless == forEndless) { PlaceBattlePrep(); return null; }
        if (battlePreparing) { CloseBattlePrep(); CloseBattleTutorial(); }
        ReloadBattleData();
        if (stage != null)
        {
            if (Adventure?.Stage(stage.Id) is not StageSpec fresh || !Adventure!.IsUnlocked(fresh.Id))
                return L("Màn này không còn trong dữ liệu hoặc chưa mở.", "This stage is no longer in the data or is locked.");
            stage = fresh;
        }
        if (forExpedition && Expedition == null) return L("Dữ liệu trận không có viễn chinh.", "The battle data has no Expedition.");
        if (forEndless && EndlessRefusal() is string closed) return closed;
        CloseBattleHub(); CloseAdventureMap(); CloseExpeditionMap();
        prepStage = stage;
        prepForExpedition = forExpedition;
        prepForEndless = forEndless;
        prepWeapon = BattleTuning.Weapon(weapon ?? preferences.BattleWeapon).Id;
        prepDifficulty = level is BattleDifficulty chosen && Enum.IsDefined(chosen) ? chosen : BattleDifficultyChoice;
        prepApps = preferences.BattleApps;
        prepGuide = preferences.AdventureGuide;
        prepSkills.Clear();
        prepSkills.UnionWith(LoadoutChoice(PrepFree));
        prepSkillHover = null;
        battlePreparing = true;
        CloseBattleSummary(); CloseBattleNotice();
        queuedCommand = null;
        revision++; // An AI reply chosen before the setup must not move the cat now.
        pet.Speech.Hide(); pet.Thought.Clear();
        ApplyQuiet();
        ShowBattlePrepWindow();
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

    public bool BattlePrepChooseGuide(bool on)
    {
        if (!battlePreparing || !StageHasGuide(prepStage)) return false;
        prepGuide = on;
        UpdateBattlePrep();
        return true;
    }

    public bool BattlePrepChooseApps(bool on)
    {
        if (!battlePreparing || !PrepHasApps) return false;
        prepApps = on;
        UpdateBattlePrep();
        return true;
    }

    // A skill chip: a chosen skill is put back; an open one is added while fewer than five are chosen.
    // A locked skill (or a sixth) changes nothing; the hint under the chips says why.
    public bool BattlePrepToggleSkill(BattleSkill skill)
    {
        if (!BattlePrepHasSkillChoice) return false;
        prepSkillHover = skill;
        bool changed = prepSkills.Remove(skill) || (BattleSkillOpen(skill, PrepFree) && prepSkills.Count < BattleSkills.Carry && prepSkills.Add(skill));
        UpdateBattlePrep();
        return changed;
    }

    // Null when the skill choice lets Ready go on.
    private string? PrepSkillsRefusal() => BattlePrepHasSkillChoice && prepSkills.Count != BattleSkills.Carry
        ? LF($"chọn đủ {BattleSkills.Carry} kỹ năng (đang có {prepSkills.Count}).", $"choose {BattleSkills.Carry} skills ({prepSkills.Count} chosen).") : null;

    // Ready: checks the cat again now, remembers the choice and starts the fight. Null on success.
    // A second press (double click) finds no panel and does nothing.
    public string? BattlePrepReady()
    {
        if (!battlePreparing || battle != null) return L("Không có bảng chuẩn bị.", "No battle setup is open.");
        // A run's loadout starts no battle yet: only what keeps the panel open is checked.
        if ((prepForExpedition ? BattlePrepRefusal() : BattleRefusal()) is string reason) { UpdateBattlePrep(); return reason; }
        if (PrepSkillsRefusal() is string skills) { UpdateBattlePrep(); return skills; }
        SetBattleWeapon(prepWeapon);
        if (BattlePrepHasSkillChoice) SetBattleLoadout(prepSkills.ToArray(), PrepFree);
        SetBattleDifficulty(prepDifficulty);
        if (PrepHasApps && preferences.BattleApps != prepApps) { preferences.BattleApps = prepApps; SavePreferences(); }
        if (BattleTutorialOpen) CloseBattleTutorial(finished: true);
        if (prepForExpedition) { string weapon = prepWeapon; var level = prepDifficulty; CloseBattlePrep(); return ExpeditionSetupReady(weapon, level); }
        if (prepForEndless) return EndlessRefusal() ?? StartBattle(stage: BattleTuning.Endless!.Stage());
        if (StageHasGuide(prepStage))
        {
            guideChoice = prepGuide;
            if (preferences.AdventureGuide != prepGuide) { preferences.AdventureGuide = prepGuide; SavePreferences(); }
        }
        return StartBattle(stage: prepStage);
    }

    // Close: no battle, no win or loss, no record; the pet goes back to its own life. For an
    // Adventure stage or an Expedition run the button says Map and goes back to that map instead.
    public void CancelBattlePrep()
    {
        if (!battlePreparing) return;
        var stage = prepStage;
        bool expedition = prepForExpedition, endless = prepForEndless;
        CloseBattlePrep();
        CloseBattleTutorial();
        if (stage != null && OpenAdventureMap(stage.Id) == null) return;
        if (expedition && OpenExpeditionMap() == null) return;
        if (endless && OpenBattleHub() == null) return;
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
        prepCards.Clear(); prepLevelChips.Clear(); prepSkillChips.Clear();
        prepSkillsLabel = prepSkillsHint = null; prepSkillsGrid = null;
        prepAppsChip = null; prepAppsCheck = null; prepAppsLabel = prepAppsText = prepAppsHint = null;
        prepGuideChip = null; prepGuideCheck = null; prepGuideLabel = prepGuideText = prepGuideHint = null;
        prepStage = null;
        prepForExpedition = prepForEndless = false;
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
        static TextBlock Line(double size, bool bold = false, double maxWidth = 330) => PanelLine(size, bold, maxWidth);
        Path Check() => new() { Data = CheckMark, Stroke = Brushes.White, StrokeThickness = 2.4, StrokeEndLineCap = PenLineCap.Round,
            StrokeStartLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Width = 15, Height = 13, Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center };
        prepTitle = Line(PetUi.HeadingSize, true);
        prepWeaponLabel = Line(PetUi.SmallSize); prepWeaponLabel.Opacity = 0.75; prepWeaponLabel.Margin = new Thickness(0, 0, 0, 4);
        prepLevelLabel = Line(PetUi.SmallSize); prepLevelLabel.Opacity = 0.75; prepLevelLabel.Margin = new Thickness(0, 8, 0, 4);
        prepData = Line(PetUi.SmallSize, maxWidth: 470); prepData.Foreground = PrepWarning; prepData.Margin = new Thickness(0, 8, 0, 0);
        prepBuild = Line(PetUi.SmallSize, maxWidth: 470); prepBuild.Margin = new Thickness(0, 8, 0, 0);
        prepStatus = Line(PetUi.SmallSize, true, 470); prepStatus.Foreground = PrepWarning; prepStatus.Margin = new Thickness(0, 8, 0, 0);

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
            var name = Line(PetUi.TitleSize, true, 200);
            var hint = Line(PetUi.SmallSize, maxWidth: 230); hint.Opacity = 0.85;
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
                statNames[i] = new TextBlock { Foreground = Brushes.White, Opacity = 0.8, FontSize = PetUi.CaptionSize, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
                double fraction = Math.Clamp(numbers[i] / WeaponStatScales[i].Max, 0, 1);
                var bar = new Grid { Height = 5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 2),
                    Children = { new Border { Background = PrepBarBack, CornerRadius = new CornerRadius(2.5) },
                                 new Border { Background = PrepBarFill, CornerRadius = new CornerRadius(2.5), HorizontalAlignment = HorizontalAlignment.Left, Width = 64 * fraction } } };
                statValues[i] = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.CaptionSize, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                    Text = i == 1 ? numbers[i].ToString("0.0", CultureInfo.InvariantCulture) + "/s" : numbers[i].ToString("0", CultureInfo.InvariantCulture) };
                Grid.SetRow(statNames[i], i); Grid.SetRow(bar, i); Grid.SetRow(statValues[i], i);
                Grid.SetColumn(bar, 1); Grid.SetColumn(statValues[i], 2);
                stats.Children.Add(statNames[i]); stats.Children.Add(bar); stats.Children.Add(statValues[i]);
            }
            var icon = new WeaponIcon { Style = weapon.Style, Width = 46, Height = 52, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top };
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
            var text = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            // One to three small gold stars after the name: how hard, at a glance.
            var stars = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i <= (int)level; i++)
                stars.Children.Add(new Path { Data = DifficultyStar, Fill = PrepBarFill, Width = 9, Height = 9, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 1, 0) });
            var chip = new Border
            {
                CornerRadius = new CornerRadius(7), Padding = new Thickness(9, 4, 11, 4), Margin = new Thickness(0, 0, 6, 0), BorderThickness = new Thickness(1.5),
                Cursor = System.Windows.Input.Cursors.Hand, Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { check, text, stars } }
            };
            chip.MouseLeftButtonDown += (_, e) => { BattlePrepChooseDifficulty(level); e.Handled = true; };
            levels.Children.Add(chip);
            prepLevelChips.Add((level, chip, check, text));
        }

        // The app icons: one chip that turns them on and off, with what that means under it.
        prepAppsLabel = Line(PetUi.SmallSize); prepAppsLabel.Opacity = 0.75; prepAppsLabel.Margin = new Thickness(0, 8, 0, 4);
        prepAppsCheck = Check();
        prepAppsText = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        prepAppsChip = new Border
        {
            CornerRadius = new CornerRadius(7), Padding = new Thickness(9, 4, 11, 4), BorderThickness = new Thickness(1.5), HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = System.Windows.Input.Cursors.Hand, Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { prepAppsCheck, prepAppsText } }
        };
        prepAppsChip.MouseLeftButtonDown += (_, e) => { BattlePrepChooseApps(!prepApps); e.Handled = true; };
        prepAppsHint = Line(PetUi.CaptionSize, maxWidth: 300); prepAppsHint.Opacity = 0.8; prepAppsHint.Margin = new Thickness(0, 4, 0, 0);

        // The skills: two rows of five chips (the five first, then the 1.5 ones), a hint under them.
        prepSkillsLabel = Line(PetUi.SmallSize); prepSkillsLabel.Opacity = 0.75; prepSkillsLabel.Margin = new Thickness(0, 8, 0, 4);
        var skillRows = new WrapPanel { Width = BattleSkills.Carry * 42, HorizontalAlignment = HorizontalAlignment.Left };
        prepSkillChips.Clear();
        foreach (var info in BattleSkills.All)
        {
            var skill = info.Skill;
            var mark = new HudGlyph(HudGlyphKind.Lock) { Width = 15, Height = 15, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -4, -4) };
            var chip = new Border
            {
                Width = 36, Height = 36, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1.5), Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(5),
                Cursor = System.Windows.Input.Cursors.Hand, Child = new Grid { Children = { new HudGlyph(HudGlyph.For(skill)), mark } }
            };
            chip.MouseLeftButtonDown += (_, e) => { BattlePrepToggleSkill(skill); e.Handled = true; };
            chip.MouseEnter += (_, _) => { prepSkillHover = skill; UpdateBattlePrep(); };
            chip.MouseLeave += (_, _) => { if (prepSkillHover == skill) { prepSkillHover = null; UpdateBattlePrep(); } };
            skillRows.Children.Add(chip);
            prepSkillChips.Add((skill, chip, mark));
        }
        prepSkillsGrid = skillRows;
        prepSkillsHint = Line(PetUi.CaptionSize, maxWidth: 300); prepSkillsHint.Opacity = 0.85;

        // The Adventure Guide (a chapter 1 stage): one chip, on until the stage is won.
        prepGuideLabel = Line(PetUi.SmallSize); prepGuideLabel.Opacity = 0.75; prepGuideLabel.Margin = new Thickness(0, 8, 0, 4);
        prepGuideCheck = Check();
        prepGuideText = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        prepGuideChip = new Border
        {
            CornerRadius = new CornerRadius(7), Padding = new Thickness(9, 4, 11, 4), BorderThickness = new Thickness(1.5), HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = System.Windows.Input.Cursors.Hand, Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { prepGuideCheck, prepGuideText } }
        };
        prepGuideChip.MouseLeftButtonDown += (_, e) => { BattlePrepChooseGuide(!prepGuide); e.Handled = true; };
        prepGuideHint = Line(PetUi.CaptionSize, maxWidth: 300); prepGuideHint.Opacity = 0.8; prepGuideHint.Margin = new Thickness(0, 4, 0, 0);

        // Actions: a quiet Stage tip link on the left; Close, then the one main button, Ready.
        prepTipText = new TextBlock { Foreground = Brushes.White, Opacity = 0.85, FontSize = PetUi.SmallSize, TextDecorations = TextDecorations.Underline,
            Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent };
        prepTipText.MouseLeftButtonDown += (_, e) => { if (prepStage != null) ShowStageTip(prepStage); e.Handled = true; };
        var (cancel, cancelText, _) = HudButton(CancelBattlePrep, plain: true);
        var (ready, readyText) = PrimaryButton(() => BattlePrepReady());
        prepReady = ready; prepReadyText = readyText; prepCancelText = cancelText;
        var actions = PanelActions(new UIElement[] { prepTipText }, new UIElement[] { ready, cancel });

        var choices = new StackPanel { Width = 300, Children = { prepWeaponLabel, cards, prepLevelLabel, levels, prepSkillsLabel, skillRows, prepSkillsHint, prepAppsLabel, prepAppsChip, prepAppsHint, prepGuideLabel, prepGuideChip, prepGuideHint } };
        var title = PanelTitle(prepTitle);
        var window = CreateGamePanel(new StackPanel { Children = { title, new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), Children = { stage, choices } },
            prepBuild, prepData, prepStatus } }, actions, "Pet setup");
        window.Closed += (_, _) => { if (battlePrepWindow == window) { StopPreviewSwing(); battlePrepWindow = null; } };
        battlePrepWindow = window;
        UpdateBattlePrep();
        window.Show();
        PlaceBattlePrep();
    }

    private void PlaceBattlePrep() => PlaceGamePanel(battlePrepWindow);

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
        prepTitle.Text = prepStage is StageSpec stage ? StageTitle(stage) + (stage.Goal(preferences.Language) is { Length: > 0 } goal ? "\n" + goal : "")
            : prepForExpedition && Expedition is Expedition x ? L("Viễn chinh — chuẩn bị chuyến", "Expedition — prepare the run") + "\n"
                + LF($"{x.Spec.StepCount} bậc, bậc cuối là trùm. Vũ khí, độ khó, HP và build giữ suốt chuyến.", $"{x.Spec.StepCount} steps, the last one a boss. Weapon, difficulty, HP and build carry through the run.")
            : prepForEndless && BattleTuning.Endless is EndlessSpec infinity ? L("Vô tận — chuẩn bị", "Infinity — get ready") + "\n" + EndlessAbout(infinity) + EndlessRecordLine(infinity, prepWeapon, prepDifficulty)
            : L("Chuẩn bị trận", "Get ready to battle");
        prepWeaponLabel!.Text = prepForExpedition ? L("Chọn cách đánh cho cả chuyến", "Choose how to fight for the whole run") : L("Chọn cách đánh", "Choose how to fight");
        prepLevelLabel!.Text = L("Độ khó", "Difficulty");
        foreach (var (id, card, check, name, hint, statNames, _) in prepCards)
        {
            var weapon = BattleTuning.Weapon(id);
            name.Text = weapon.Name(preferences.Language);
            hint.Text = weapon.Hint(preferences.Language);
            for (int i = 0; i < statNames.Length; i++) statNames[i].Text = L(WeaponStatScales[i].Vi, WeaponStatScales[i].En);
            ShowChosen(card, id == prepWeapon, check, rim: (2.5, 1), card: true);
        }
        foreach (var (level, chip, check, text) in prepLevelChips)
        {
            text.Text = DifficultyName(level);
            ShowChosen(chip, level == prepDifficulty, check);
        }
        if (prepAppsChip != null)
        {
            var shown = PrepHasApps ? Visibility.Visible : Visibility.Collapsed;
            prepAppsLabel!.Visibility = prepAppsChip.Visibility = prepAppsHint!.Visibility = shown;
            prepAppsLabel.Text = prepForExpedition ? L("Trên màn hình (cả chuyến)", "On the screen (whole run)") : L("Trên màn hình", "On the screen");
            prepAppsText!.Text = L("App trong trận", "Battle apps");
            prepAppsHint.Text = prepApps ? L("5 icon app giúp mèo; slime có thể nhiễm virus vào icon.", "Five app icons help the cat; slimes may infect them with viruses.")
                : L("Không có icon app hay virus. Kỷ lục lưu riêng.", "No app icons or viruses. Records are kept apart.");
            ShowChosen(prepAppsChip, prepApps, prepAppsCheck!);
        }
        if (prepSkillsGrid != null)
        {
            var shown = BattlePrepHasSkillChoice ? Visibility.Visible : Visibility.Collapsed;
            prepSkillsLabel!.Visibility = prepSkillsGrid.Visibility = prepSkillsHint!.Visibility = shown;
            prepSkillsLabel.Text = LF($"Kỹ năng mang theo ({prepSkills.Count}/{BattleSkills.Carry})", $"Skills to bring ({prepSkills.Count}/{BattleSkills.Carry})");
            foreach (var (skill, chip, mark) in prepSkillChips)
            {
                bool open = BattleSkillOpen(skill, PrepFree), carried = prepSkills.Contains(skill);
                ShowChosen(chip, carried);
                mark.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
                chip.Opacity = open || carried ? 1 : 0.5;
            }
            prepSkillsHint.Text = prepSkillHover is BattleSkill hover
                ? BattleSkills.Get(hover).Describe(BattleTuning, preferences.Language)
                    + (BattleSkillOpen(hover, PrepFree) || prepSkills.Contains(hover) ? ""
                        : " " + (BattleSkills.Get(hover).Working ? L("(Chưa mở.)", "(Not open yet.)") : L("(Chưa mở: có trong bản cập nhật sau.)", "(Not open yet: coming in a later update.)")))
                : prepSkills.Count < BattleSkills.Carry ? LF($"Chọn thêm {BattleSkills.Carry - prepSkills.Count} kỹ năng. Rê chuột lên ô để xem tác dụng.", $"Choose {BattleSkills.Carry - prepSkills.Count} more. Point at a skill to see what it does.")
                : prepForExpedition ? L("Bộ kỹ năng giữ suốt chuyến. Bấm một ô đã chọn để bỏ, rồi chọn ô khác.", "The skills stay for the whole run. Click a chosen skill to drop it, then choose another.")
                : L("Bấm một ô đã chọn để bỏ, rồi chọn ô khác. Lựa chọn được nhớ cho lần sau.", "Click a chosen skill to drop it, then choose another. The choice is kept.");
        }
        if (prepGuideChip != null)
        {
            var shown = StageHasGuide(prepStage) ? Visibility.Visible : Visibility.Collapsed;
            prepGuideLabel!.Visibility = prepGuideChip.Visibility = prepGuideHint!.Visibility = shown;
            prepGuideLabel.Text = L("Hướng dẫn phiêu lưu", "Adventure Guide");
            prepGuideText!.Text = L("Hướng dẫn trong trận", "Guide in the battle");
            prepGuideHint.Text = prepGuide ? L("Trận đứng hình đúng lúc và chỉ chỗ cần làm. Bỏ tick khi đã quen; lựa chọn được nhớ cho lần sau.", "The battle stands still at the right moments and shows what to do. Untick it once you know the game; the choice is kept.")
                : L("Không có hướng dẫn trong trận này.", "No guide in this battle.");
            ShowChosen(prepGuideChip, prepGuide, prepGuideCheck!);
        }
        if (prepPreview != null && (prepPreview.WeaponStyle != chosen.Style || prepPreview.SwordHitAt != chosen.HitFraction))
        {
            prepPreview.WeaponStyle = chosen.Style;
            prepPreview.SwordHitAt = chosen.HitFraction;
            prepPreviewView?.InvalidateVisual();
        }
        Show(prepBuild, prepStage is StageSpec buildStage && (buildStage.Upgrades || Adventure?.StartBuild(buildStage).Count > 0) ? StageBuildText(buildStage) : null);
        Show(prepData, BattleDataError != null
            ? L("File battle.json của bạn có lỗi nên dùng dữ liệu gốc: ", "Your battle.json has a mistake, so the built-in battle is used: ") + BattleDataError
            : BattleDataCustom ? L("Đang dùng dữ liệu trận tùy chỉnh (battle.json).", "Using your custom battle data (battle.json).") : null);
        string? waiting = (prepForExpedition ? BattlePrepRefusal() : BattleRefusal()) ?? PrepSkillsRefusal();
        Show(prepStatus, waiting != null ? L("Chưa thể bắt đầu: ", "Not yet: ") + waiting : null);
        prepReadyText!.Text = prepForExpedition ? L("Lên đường", "Set out") : L("Sẵn sàng", "Ready");
        prepCancelText!.Text = prepStage != null || prepForExpedition ? L("‹ Bản đồ", "‹ Map") : prepForEndless ? L("‹ Sảnh", "‹ Hub") : L("Đóng", "Close");
        prepTryText!.Text = L("Xem thử", "Try it");
        Show(prepTipText, prepStage?.Tip(preferences.Language) is { Length: > 0 } ? L("Màn này dạy gì", "What you learn here") : null);
        bool can = waiting == null;
        if (prepReady.IsEnabled != can) { prepReady.IsEnabled = can; prepReady.Opacity = can ? 1 : 0.5; }
    }

    // The build a stage starts with: "Build brought in: Defense 2 · Power 1 · ★ Thorns".
    private string StageBuildText(StageSpec stage)
    {
        var parts = BuildParts(Adventure?.StartBuild(stage) ?? new Dictionary<string, int>());
        return parts.Count == 0 ? L("Build mang vào màn: chưa có thẻ (thắng màn để giữ thẻ đã chọn sang màn sau)", "Build brought in: no cards yet (win a stage to keep its cards for the next)")
            : L("Build mang vào màn: ", "Build brought in: ") + string.Join(" · ", parts);
    }

    // "Defense 2", "Power 1", "★ Thorns", "◆ Turtle shell": a build's group points, synergies and rare cards.
    private List<string> BuildParts(IReadOnlyDictionary<string, int> build)
    {
        var cards = BattleTuning.UpgradeList;
        var parts = new List<string>();
        foreach (var group in BuildGroups.All)
        {
            int points = build.Where(card => cards.FirstOrDefault(u => u.Id == card.Key)?.Group == group).Sum(card => card.Value);
            if (points > 0) parts.Add($"{BuildGroups.Name(group, preferences.Language)} {points}");
            foreach (var y in BattleTuning.SynergyList) if (y.Group == group && !y.EndlessOnly && points >= y.At) parts.Add("★ " + y.Name(preferences.Language));
        }
        foreach (var u in cards) if (u.Rare && build.TryGetValue(u.Id, out int n) && n > 0) parts.Add("◆ " + u.Name(preferences.Language));
        return parts;
    }
}

// A weapon's art for a setup card (the same drawing as in the cat's paw).
public sealed class WeaponIcon : FrameworkElement
{
    public string Style { get; init; } = "sword";
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new TranslateTransform(ActualWidth / 2 - 10, ActualHeight / 2 + 17));
        PetRenderer.DrawWeaponIcon(dc, Style);
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
    private static readonly Pen Ink = PetUi.FrozenPen(PetUi.ArtInk, 1.4);
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
