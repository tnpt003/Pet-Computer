using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopPet;

// Battle Mode, around the fight: the result panel after a won or lost battle (Play again /
// Close) and the short note that says why a battle cannot start. Both never take focus.
public sealed partial class PetWindow
{
    private static readonly Brush HudButtonBack = PetUi.Frozen(Color.FromArgb(0x50, 255, 255, 255));

    private static (Border Button, TextBlock Text, Border Progress) HudButton(Action click)
    {
        var text = new TextBlock { Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        var progress = new Border { Height = 3, Background = new SolidColorBrush(Color.FromRgb(0x5C, 0xC8, 0xFF)), HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 3, 0, 0) };
        var button = new Border
        {
            Background = HudButtonBack, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 4, 10, 3),
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
        var panel = new Border { Background = HudBack, BorderBrush = Brushes.White, BorderThickness = new Thickness(1.5),
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

    // The result after a won or lost battle: stats, Play again and Close. It closes by itself.
    public readonly record struct BattleSummary(bool Won, int Defeated, double Seconds, double Hp, double MaxHp, bool Boss = false, string? BossName = null);
    public BattleSummary? LastBattleSummary { get; private set; }
    private Window? battleSummaryWindow;
    private System.Windows.Threading.DispatcherTimer? battleSummaryTimer;
    public const double BattleSummarySeconds = 12;

    private void ShowBattleSummary(BattleSummary result)
    {
        CloseBattleSummary();
        LastBattleSummary = result;
        if (closed || context.Fullscreen) return;
        var title = new TextBlock { Text = result.Won ? (result.Boss ? L($"Chiến thắng! Đã hạ {result.BossName ?? "boss"}", $"Victory! The {result.BossName ?? "boss"} is down") : L("Chiến thắng!", "Victory!"))
                : L("Thua rồi… mèo choáng váng", "Defeated… the cat sees stars"),
            Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold };
        var time = TimeSpan.FromSeconds(Math.Round(result.Seconds));
        string clock = $"{(int)time.TotalMinutes}:{time.Seconds:00}";
        var stats = new TextBlock { Foreground = Brushes.White, FontSize = 12.5, Margin = new Thickness(0, 4, 0, 0),
            Text = L($"Hạ {result.Defeated} slime · {clock} · HP còn {Math.Ceiling(result.Hp)}/{result.MaxHp}",
                     $"{result.Defeated} slime{(result.Defeated == 1 ? "" : "s")} defeated · {clock} · HP {Math.Ceiling(result.Hp)}/{result.MaxHp} left") };
        var (again, againText, _) = HudButton(() =>
        {
            CloseBattleSummary();
            if (StartBattle() is string reason) ShowBattleNotice(reason);
        });
        againText.Text = L("Chơi lại", "Play again");
        var (close, closeText, _) = HudButton(CloseBattleSummary);
        closeText.Text = L("Đóng", "Close");
        var panel = new Border
        {
            Background = HudBack, BorderBrush = Brushes.White, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(6),
            Child = new StackPanel { Children = { title, stats, new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), Children = { again, close } } } },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 }
        };
        panel.SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
        var window = CreateBattleWindow(panel, "Pet result");
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.Closed += (_, _) => { if (battleSummaryWindow == window) battleSummaryWindow = null; };
        battleSummaryWindow = window;
        window.Show();
        window.UpdateLayout();
        MoveWindow(window, (bounds.Left + bounds.Right - window.ActualWidth) / 2, bounds.Top + 12);
        battleSummaryTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(BattleSummarySeconds) };
        battleSummaryTimer.Tick += (_, _) => CloseBattleSummary();
        battleSummaryTimer.Start();
    }

    private void CloseBattleSummary()
    {
        battleSummaryTimer?.Stop();
        battleSummaryWindow?.Close();
        battleSummaryWindow = null;
    }

    private static readonly Brush HudBack = PetUi.Frozen(Color.FromArgb(0xE8, 0x26, 0x23, 0x38));
}
