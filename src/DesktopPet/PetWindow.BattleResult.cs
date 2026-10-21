using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopPet;

// Battle Mode, around the fight: the result panel after a won or lost battle (Play again /
// Change gear / Back to desktop) and the short note that says why a battle cannot start. Both never take focus.
public sealed partial class PetWindow
{
    private static readonly Brush HudButtonBack = PetUi.Game.ButtonBack;

    // The cat's 192-DIP frame cropped to the cat and its weapon (the frame's margin is for
    // poses like being carried), for the setup preview and the result picture.
    private static FrameworkElement CatPicture(UIElement cat) => new Viewbox
    {
        Child = new Canvas { Width = 136, Height = 136, ClipToBounds = true, Children = { cat } }
    }.WithOffset(cat, -30, -26);

    private static (Border Button, TextBlock Text, Border Progress) HudButton(Action click, bool plain = false)
    {
        var text = new TextBlock { Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        var progress = new Border { Height = 3, Background = PetUi.Game.Progress, HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 3, 0, 0) };
        if (plain) progress.Visibility = Visibility.Collapsed; // No cooldown bar: the text sits in the middle.
        var button = new Border
        {
            Background = HudButtonBack, CornerRadius = new CornerRadius(PetUi.Game.ButtonRadius), Padding = new Thickness(10, 4, 10, 3),
            Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, MinWidth = 70, Child = new StackPanel { Children = { text, progress } }
        };
        button.MouseLeftButtonDown += (_, e) => { if (button.IsEnabled) click(); e.Handled = true; };
        button.MouseEnter += (_, _) => button.Opacity = button.IsEnabled ? 0.85 : button.Opacity;
        button.MouseLeave += (_, _) => button.Opacity = button.IsEnabled ? 1 : 0.5;
        return (button, text, progress);
    }

    // A short click-through note above the cat (why a battle cannot start); never takes focus.
    private Window? battleNoticeWindow;
    private System.Windows.Threading.DispatcherTimer? battleNoticeTimer;
    public const double BattleNoticeSeconds = 2.5;

    private void ShowBattleNotice(string text)
    {
        CloseBattleNotice();
        var label = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 240, FontFamily = PetUi.TextFont };
        var panel = new Border { Background = HudBack, BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5, 10, 6), Margin = new Thickness(4), Child = label };
        var notice = CreateBattleWindow(panel, "Pet notice", clickThrough: true);
        notice.SizeToContent = SizeToContent.WidthAndHeight;
        notice.IsHitTestVisible = false;
        notice.Closed += (_, _) => { if (battleNoticeWindow == notice) battleNoticeWindow = null; };
        battleNoticeWindow = notice;
        notice.Show();
        notice.UpdateLayout();
        double left = Math.Clamp(pet.X + settings.Width / 2 - notice.ActualWidth / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - notice.ActualWidth));
        MoveWindow(notice, left, Math.Max(bounds.Top, pet.Y + 6 - notice.ActualHeight));
        battleNoticeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(BattleNoticeSeconds) };
        battleNoticeTimer.Tick += (_, _) => CloseBattleNotice();
        battleNoticeTimer.Start();
    }

    private void CloseBattleNotice()
    {
        battleNoticeTimer?.Stop();
        battleNoticeWindow?.Close();
        battleNoticeWindow = null;
    }

    // The result after a won or lost battle: this cat cheering or seeing stars, the loadout
    // (weapon · difficulty), time, slimes defeated and the loadout's records (HP left and the
    // records from before weapons were told apart are smaller). Play again starts the same
    // loadout at once (after the usual safety check; otherwise the setup panel opens with the
    // reason), Change gear opens the setup panel with it, Back to desktop just closes. It stays
    // until the user chooses (a fullscreen app, the pet being brought back or Exit still close it).
    public readonly record struct BattleSummary(bool Won, int Defeated, double Seconds, double Hp, double MaxHp, bool Boss = false, string? BossName = null,
        BattleDifficulty Difficulty = BattleDifficulty.Normal, BattleRecordNews News = default, string Weapon = BattleWeapon.Sword, string WeaponLabel = "",
        bool Custom = false, string WeaponStyle = "sword", StageSpec? Stage = null, AdventureOutcome Outcome = default, bool Ranked = true, string Upgrades = "",
        ExpeditionStepOutcome? Expedition = null);
    private static readonly Brush RecordGold = PetUi.Game.Gold;
    private static readonly Brush ResultMain = PetUi.Game.Chosen;
    private static string Clock(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Round(seconds));
        return $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }
    public BattleSummary? LastBattleSummary { get; private set; }
    private Window? battleSummaryWindow;
    public bool BattleSummaryOpen => battleSummaryWindow != null;

    private string RecordLine(BattleRecord best)
    {
        string fastest = best.BestSeconds is double s ? L($"nhanh nhất {Clock(s)} · ", $"fastest {Clock(s)} · ") : "";
        return L($"{fastest}nhiều nhất {best.MostDefeated} slime · thắng {best.Wins} lần",
                 $"{fastest}most {best.MostDefeated} slime{(best.MostDefeated == 1 ? "" : "s")} · {best.Wins} win{(best.Wins == 1 ? "" : "s")}");
    }

    private void ShowBattleSummary(BattleSummary result)
    {
        CloseBattleSummary();
        LastBattleSummary = result;
        if (closed || context.Fullscreen) return;
        TextBlock Line(double size, Brush? brush = null, bool bold = false, double opacity = 1) => new()
        {
            Foreground = brush ?? Brushes.White, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Opacity = opacity,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 300, Margin = new Thickness(0, 2, 0, 0)
        };
        var title = Line(15.5, bold: true);
        title.Text = result.Won ? (result.Boss ? L($"Chiến thắng! Đã hạ {result.BossName ?? "boss"}", $"Victory! The {result.BossName ?? "boss"} is down") : L("Chiến thắng!", "Victory!"))
            : L("Thua rồi… mèo choáng váng", "Defeated… the cat sees stars");
        string level = DifficultyName(result.Difficulty), arms = result.WeaponLabel.Length > 0 ? result.WeaponLabel : result.Weapon;
        string place = result.Stage is not StageSpec played ? ""
            : result.Expedition is ExpeditionStepOutcome at ? ExpeditionStageTitle(at.Step, Expedition?.Spec.StepCount ?? at.Step + 1, played) + " · " : StageTitle(played) + " · ";
        var loadout = Line(13, bold: true); loadout.Text = place + $"{arms} · {level}";
        var main = Line(12.5);
        main.Text = L($"Hạ {result.Defeated} slime · {Clock(result.Seconds)}", $"{result.Defeated} slime{(result.Defeated == 1 ? "" : "s")} defeated · {Clock(result.Seconds)}");
        var hp = Line(11.5, opacity: 0.75); hp.Text = L($"HP còn {Math.Ceiling(result.Hp)}/{result.MaxHp}", $"HP {Math.Ceiling(result.Hp)}/{result.MaxHp} left");
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, loadout, main, hp } };
        Window? window = null;
        // Each choice acts once: the panel is gone before anything else happens (no double start).
        void Choose(Action action) { if (window == null || battleSummaryWindow != window) return; CloseBattleSummary(); action(); }
        UIElement actions;
        if (result.Expedition is ExpeditionStepOutcome step)
        {
            var (lines, stepActions) = ExpeditionResult(result, step, Choose);
            info.Children.Add(lines);
            actions = stepActions;
        }
        else if (result.Stage is StageSpec stage)
        {
            var (lines, stageActions) = StageResult(result, stage, Choose);
            info.Children.Add(lines);
            actions = stageActions;
        }
        else actions = FreeBattleResult(result, info, arms, level, Choose);
        // This cat, as the battle ended: cheering with its weapon, or sitting dazed.
        var model = new PetModel { Action = PetAction.Sitting, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1, SwordOut = true,
            Hammer = result.WeaponStyle == "hammer", BattleCheer = result.Won, BattleDazed = !result.Won };
        var portrait = new PetRenderer(model) { Coat = renderer.Coat, Accessory = renderer.Accessory, Width = 192, Height = 192 };
        var picture = new Border { Width = 96, Height = 96, Margin = new Thickness(0, 0, 12, 0), CornerRadius = new CornerRadius(48), Background = PrepStage,
            VerticalAlignment = VerticalAlignment.Top, Child = CatPicture(portrait) };
        var panel = new Border
        {
            Background = HudBack, BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(6),
            Child = new StackPanel { Children = { new StackPanel { Orientation = Orientation.Horizontal, Children = { picture, info } }, actions } },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 }
        };
        panel.SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
        window = CreateBattleWindow(panel, "Pet result");
        window.SizeToContent = SizeToContent.WidthAndHeight;
        var shown = window;
        window.Closed += (_, _) => { if (battleSummaryWindow == shown) battleSummaryWindow = null; };
        battleSummaryWindow = window;
        window.Show();
        window.UpdateLayout();
        MoveWindow(window, (bounds.Left + bounds.Right - window.ActualWidth) / 2, bounds.Top + 12);
    }

    // A free battle's records lines and its Play again / Change gear / Back to desktop.
    private UIElement FreeBattleResult(BattleSummary result, StackPanel info, string arms, string level, Action<Action> Choose)
    {
        TextBlock Line(double size, Brush? brush = null, bool bold = false, double opacity = 1) => new()
        {
            Foreground = brush ?? Brushes.White, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Opacity = opacity,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 300, Margin = new Thickness(0, 2, 0, 0)
        };
        // Records of this loadout (weapon id + difficulty; custom data kept apart), and what this battle beat.
        if (result.News.Any)
        {
            var news = Line(12.5, RecordGold, true);
            news.Text = result.News.Fastest && result.News.MostDefeated ? L("Kỷ lục mới: thắng nhanh nhất, hạ nhiều slime nhất!", "New record: fastest win, most slimes!")
                : result.News.Fastest ? L("Kỷ lục mới: thắng nhanh nhất!", "New record: fastest win!") : L("Kỷ lục mới: hạ nhiều slime nhất!", "New record: most slimes!");
            info.Children.Add(news);
        }
        var best = BattleRecords.For(result.Weapon, result.Difficulty, result.Custom);
        if (!best.Empty)
        {
            var records = Line(11.5, opacity: 0.85);
            records.Text = L($"Kỷ lục ({arms} · {level}{(result.Custom ? ", dữ liệu tùy chỉnh" : "")}): ", $"Records ({arms} · {level}{(result.Custom ? ", custom data" : "")}): ") + RecordLine(best);
            info.Children.Add(records);
        }
        var legacy = BattleRecords.Legacy(result.Difficulty);
        if (!legacy.Empty)
        {
            var old = Line(11, opacity: 0.6);
            old.Text = L($"Trước phân loại trang bị ({level}): ", $"Before weapons were told apart ({level}): ") + RecordLine(legacy);
            info.Children.Add(old);
        }
        var (again, againText, _) = HudButton(() => Choose(() => { if (BattleReplay(result) is string reason) ShowBattleNotice(reason); }), plain: true);
        again.Background = ResultMain; again.Padding = new Thickness(16, 6, 16, 5); again.Margin = new Thickness(0);
        againText.FontSize = 13.5; againText.FontWeight = FontWeights.Bold; againText.Text = L("Chơi lại", "Play again");
        var (gear, gearText, _) = HudButton(() => Choose(() => { if (OpenBattlePrep(result.Weapon, result.Difficulty) is string reason) ShowBattleNotice(reason); }), plain: true);
        gearText.Text = L("Đổi trang bị", "Change gear");
        var (home, homeText, _) = HudButton(() => Choose(() => { }), plain: true);
        homeText.Text = L("Về desktop", "Back to desktop");
        var actions = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
        DockPanel.SetDock(home, Dock.Left); DockPanel.SetDock(again, Dock.Right); DockPanel.SetDock(gear, Dock.Right);
        actions.Children.Add(home); actions.Children.Add(again); actions.Children.Add(gear);
        return actions;
    }

    // Play again: the same weapon and difficulty (and Adventure stage) as the battle that just
    // ended, at once, if the cat is free; otherwise the setup panel opens with that choice and says
    // why (nothing is forced). A stage no longer in the data sends the user to the map.
    public string? BattleReplay(BattleSummary last)
    {
        if (battle != null) return L("Trận đang diễn ra.", "A battle is already running.");
        if (battlePreparing) return null;
        if (BattleRefusal() is string reason)
        {
            // The panel shows the reason itself; paused or fullscreen keep even the panel away.
            return OpenBattlePrep(last.Weapon, last.Difficulty, last.Stage) is string refused ? refused : null;
        }
        ReloadBattleData();
        if (last.Stage == null) return StartBattle(last.Weapon, last.Difficulty);
        if (Adventure?.Stage(last.Stage.Id) is not StageSpec stage || !Adventure!.IsUnlocked(stage.Id)) return OpenAdventureMap();
        return StartBattle(last.Weapon, last.Difficulty, stage);
    }

    private void CloseBattleSummary()
    {
        battleSummaryWindow?.Close();
        battleSummaryWindow = null;
    }

    private static readonly Brush HudBack = PetUi.Game.Panel;
}

internal static class PictureExtensions
{
    // Places a child of the picture's canvas (a negative offset crops the frame's margin).
    public static T WithOffset<T>(this T picture, UIElement child, double left, double top) where T : FrameworkElement
    {
        Canvas.SetLeft(child, left);
        Canvas.SetTop(child, top);
        return picture;
    }
}
