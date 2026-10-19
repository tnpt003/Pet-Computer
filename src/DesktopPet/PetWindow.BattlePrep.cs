using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// Battle Mode's setup panel: menu → Start battle opens it, Ready starts the fight, Cancel goes
// back to the pet's normal life. While it is open nothing fights yet (no slime, timer, damage or
// record) and it owns the pet's free time: no random actions, sleep, away nap, AI commands,
// games or chatter (behavior runs as in Quiet mode). Sleep, waking, a drag or a flight already
// going on are not cut; Ready just waits until the cat is free, checked again when pressed.
// The panel never takes focus and is used with the mouse only, so it gets no keys from other apps.
public sealed partial class PetWindow
{
    private bool battlePreparing;
    private Window? battlePrepWindow;
    private string prepWeapon = BattleWeapon.Sword;
    private BattleDifficulty prepDifficulty = BattleDifficulty.Normal;
    private double prepRefresh;
    private readonly List<(string Id, Border Chip, TextBlock Text)> prepWeaponChips = new();
    private readonly List<(BattleDifficulty Level, Border Chip, TextBlock Text)> prepLevelChips = new();
    private TextBlock? prepTitle, prepWeaponLabel, prepLevelLabel, prepHint, prepData, prepStatus, prepReadyText, prepGuideText, prepCancelText;
    private Border? prepReady;
    private static readonly Brush PrepChosen = PetUi.Frozen(Color.FromRgb(0x5C, 0x9E, 0xFF));
    private static readonly Brush PrepWarning = PetUi.Frozen(Color.FromRgb(0xFF, 0xC8, 0x6A));
    public const double BattlePrepRefreshSeconds = 0.2;

    public bool BattlePreparing => battlePreparing;
    // The battle or its setup owns the pet (PetWindow.Battle.cs has the battle's own rules).
    public bool BattleOwnsPet => battle != null || battlePreparing;
    public string BattlePrepWeapon => prepWeapon;
    public BattleDifficulty BattlePrepDifficulty => prepDifficulty;
    public bool BattlePrepVisible => battlePrepWindow?.IsVisible == true;
    public bool BattlePrepReadyEnabled => prepReady?.IsEnabled == true;
    public string BattlePrepText => string.Join("\n", new[] { prepTitle, prepHint, prepData, prepStatus }
        .Where(t => t is { Visibility: Visibility.Visible }).Select(t => t!.Text)
        .Concat(prepWeaponChips.Select(c => c.Text.Text)).Concat(prepLevelChips.Select(c => c.Text.Text)));

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
    public string? OpenBattlePrep()
    {
        if (BattlePrepRefusal() is string reason) return reason;
        if (battlePreparing) { PlaceBattlePrep(); return null; }
        ReloadBattleData();
        prepWeapon = BattleWeaponChoice;
        prepDifficulty = BattleDifficultyChoice;
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
        return StartBattle();
    }

    // Cancel/Close: no battle, no win or loss, no record; the pet goes back to its own life.
    public void CancelBattlePrep()
    {
        if (!battlePreparing) return;
        CloseBattlePrep();
        CloseBattleTutorial();
        revision++;
        behavior.Reset(pet);
        System.Diagnostics.Trace.WriteLine("Battle setup cancelled.");
    }

    // Removes the panel (Ready, Cancel, reset, closing the pet). Leaves the pet's state alone.
    private void CloseBattlePrep()
    {
        bool was = battlePreparing;
        battlePreparing = false;
        battlePrepWindow?.Close();
        battlePrepWindow = null;
        prepReady = null;
        prepWeaponChips.Clear(); prepLevelChips.Clear();
        if (was) ApplyQuiet();
    }

    // Every frame, paused or not: a fullscreen app hides the panel (it comes back after), and
    // Ready's state follows the cat a few times a second.
    private void TickBattlePrep(double dt)
    {
        if (!battlePreparing || battlePrepWindow == null) return;
        if (context.Fullscreen) { if (battlePrepWindow.IsVisible) battlePrepWindow.Hide(); return; }
        if (!battlePrepWindow.IsVisible) { battlePrepWindow.Show(); PlaceBattlePrep(); }
        prepRefresh += dt;
        if (prepRefresh < BattlePrepRefreshSeconds) return;
        prepRefresh = 0;
        UpdateBattlePrep();
    }

    private void ShowBattlePrepWindow()
    {
        TextBlock Line(double size, bool bold = false) => new()
        {
            Foreground = Brushes.White, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 330
        };
        prepTitle = Line(15, true);
        prepWeaponLabel = Line(11.5); prepWeaponLabel.Opacity = 0.8; prepWeaponLabel.Margin = new Thickness(0, 8, 0, 3);
        prepLevelLabel = Line(11.5); prepLevelLabel.Opacity = 0.8; prepLevelLabel.Margin = new Thickness(0, 8, 0, 3);
        prepHint = Line(12); prepHint.Opacity = 0.85; prepHint.Margin = new Thickness(0, 3, 0, 0);
        prepData = Line(11.5); prepData.Foreground = PrepWarning; prepData.Margin = new Thickness(0, 8, 0, 0);
        prepStatus = Line(12, true); prepStatus.Foreground = PrepWarning; prepStatus.Margin = new Thickness(0, 8, 0, 0);
        var weapons = new WrapPanel { MaxWidth = 340 };
        prepWeaponChips.Clear();
        foreach (var weapon in BattleTuning.WeaponList)
        {
            string id = weapon.Id;
            var (chip, text, _) = HudButton(() => BattlePrepChooseWeapon(id));
            chip.Margin = new Thickness(0, 0, 6, 4);
            weapons.Children.Add(chip);
            prepWeaponChips.Add((id, chip, text));
        }
        var levels = new WrapPanel { MaxWidth = 340 };
        prepLevelChips.Clear();
        foreach (var level in new[] { BattleDifficulty.Easy, BattleDifficulty.Normal, BattleDifficulty.Hard })
        {
            var (chip, text, _) = HudButton(() => BattlePrepChooseDifficulty(level));
            chip.Margin = new Thickness(0, 0, 6, 4);
            levels.Children.Add(chip);
            prepLevelChips.Add((level, chip, text));
        }
        var (ready, readyText, _) = HudButton(() => BattlePrepReady());
        var (guide, guideText, _) = HudButton(ShowBattleTutorial);
        var (cancel, cancelText, _) = HudButton(CancelBattlePrep);
        prepReady = ready; prepReadyText = readyText; prepGuideText = guideText; prepCancelText = cancelText;
        var panel = new Border
        {
            Background = HudBack, BorderBrush = Brushes.White, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(6),
            Child = new StackPanel { Children = { prepTitle, prepWeaponLabel, weapons, prepHint, prepLevelLabel, levels, prepData, prepStatus,
                new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), Children = { ready, guide, cancel } } } },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 }
        };
        panel.SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
        var window = CreateBattleWindow(panel, "Pet setup");
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.Closed += (_, _) => { if (battlePrepWindow == window) battlePrepWindow = null; };
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
        prepTitle.Text = L("Chuẩn bị trận", "Get ready to battle");
        prepWeaponLabel!.Text = L("Vũ khí", "Weapon");
        prepLevelLabel!.Text = L("Độ khó", "Difficulty");
        foreach (var (id, chip, text) in prepWeaponChips)
        {
            text.Text = BattleTuning.Weapon(id).Name(English);
            chip.Background = id == prepWeapon ? PrepChosen : HudButtonBack;
        }
        foreach (var (level, chip, text) in prepLevelChips)
        {
            text.Text = DifficultyName(level);
            chip.Background = level == prepDifficulty ? PrepChosen : HudButtonBack;
        }
        Show(prepHint, BattleTuning.Weapon(prepWeapon).Hint(English));
        Show(prepData, BattleDataError != null
            ? L("File battle.json của bạn có lỗi nên dùng dữ liệu gốc: ", "Your battle.json has a mistake, so the built-in battle is used: ") + BattleDataError
            : BattleDataCustom ? L("Đang dùng dữ liệu trận tùy chỉnh (battle.json).", "Using your custom battle data (battle.json).") : null);
        string? waiting = BattleRefusal();
        Show(prepStatus, waiting != null ? L("Chưa thể bắt đầu: ", "Not yet: ") + waiting : null);
        prepReadyText!.Text = L("Sẵn sàng", "Ready");
        prepGuideText!.Text = L("Hướng dẫn", "How to play");
        prepCancelText!.Text = L("Hủy", "Cancel");
        bool can = waiting == null;
        if (prepReady.IsEnabled != can) { prepReady.IsEnabled = can; prepReady.Opacity = can ? 1 : 0.5; }
    }
}
