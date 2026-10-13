using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopPet.AI;

namespace DesktopPet;

// The pet's diary: what the Python memory knows about life with the user. C# only
// displays the summary Python sends (decision.diary); with the AI off it shows this
// session's counts. Text follows the speech language setting.
public sealed partial class PetWindow
{
    private PetDiary? aiDiary;
    private Window? diaryWindow;
    private StackPanel? diaryPanel;
    private static readonly Brush DiaryAccent = Frozen(Color.FromRgb(0x2A, 0x78, 0xD6));
    private static readonly Brush DiaryInk = Frozen(Color.FromRgb(0x2B, 0x2A, 0x28));
    private static readonly Brush DiaryMuted = Frozen(Color.FromRgb(0x6E, 0x6C, 0x66));
    private static readonly Brush DiaryGrid = Frozen(Color.FromRgb(0xE4, 0xE2, 0xDD));
    private static readonly Brush DiarySurface = Frozen(Color.FromRgb(0xFC, 0xFC, 0xFB));
    private const double ChartWidth = 312, ChartHeight = 64;

    private static Brush Frozen(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }

    private string L(string vietnamese, string english) => preferences.Language == "en" ? english : vietnamese;

    public void ShowDiary()
    {
        if (diaryWindow != null) { diaryWindow.Activate(); return; }
        diaryPanel = new StackPanel { Margin = new Thickness(18, 14, 18, 18) };
        double areaHeight = Math.Max(300, bounds.Bottom - bounds.Top);
        double height = Math.Min(600, areaHeight - 24);
        diaryWindow = new Window
        {
            Title = L($"Nhật ký của {preferences.Name}", $"{preferences.Name}'s diary"), Icon = AppIcon.WindowImage(),
            Width = 380, Height = height, Background = DiarySurface, ResizeMode = ResizeMode.NoResize, Topmost = true, ShowInTaskbar = false,
            Content = new ScrollViewer { Content = diaryPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = Math.Clamp(pet.X + settings.Width / 2 - 190, bounds.Left, Math.Max(bounds.Left, bounds.Right - 380)),
            Top = bounds.Top + Math.Max(8, (areaHeight - height) / 2)
        };
        diaryWindow.Closed += (_, _) => { diaryWindow = null; diaryPanel = null; };
        RefreshDiary();
        diaryWindow.Show();
    }

    private void RefreshDiary()
    {
        if (diaryPanel == null) return;
        diaryPanel.Children.Clear();
        AddHeading(L($"Nhật ký của {preferences.Name}", $"{preferences.Name}'s diary"), 18);
        if (aiDiary is not { } diary)
        {
            var session = pet.Interactions;
            AddLine(L("AI đang tắt hoặc chưa sẵn sàng, nên chỉ có số liệu của lần chạy này.",
                   "The AI is off or not ready yet, so only this session's numbers are shown."), DiaryMuted);
            AddLine(L($"Được vuốt {session.Petted} lần · {session.Treats} treat · chơi bóng {session.Toy + session.BallClicks} lần",
                   $"Petted {Times(session.Petted)} · {session.Treats} treats · {session.Toy + session.BallClicks} ball games"));
            return;
        }
        AddLine(L($"Đã ở cùng sen {diary.Days} ngày (từ {diary.Since})", $"Together for {diary.Days} days (since {diary.Since})"));
        AddLine(L($"Được vuốt {diary.Petted} lần · {diary.Treats} treat · chơi bóng {diary.Play} lần",
               $"Petted {Times(diary.Petted)} · {diary.Treats} treats · {diary.Play} ball games"));
        AddLine(L($"Click {diary.Clicks} lần · bị ném mạnh {diary.RoughThrows} lần", $"Clicked {Times(diary.Clicks)} · thrown roughly {Times(diary.RoughThrows)}"), DiaryMuted);

        AddHeading(L($"Độ thân thiết: {diary.Bond:F0}/100", $"Bond: {diary.Bond:F0}/100"), 14);
        if (diary.BondHistory.Length >= 2) diaryPanel.Children.Add(BondChart(diary.BondHistory));
        AddLine(diary.BondHistory.Length >= 2
            ? L($"{diary.BondHistory.Length} ngày gần nhất: {string.Join(", ", diary.BondHistory.Select(v => v.ToString("F0")))}",
                $"Last {diary.BondHistory.Length} days: {string.Join(", ", diary.BondHistory.Select(v => v.ToString("F0")))}")
            : L("Biểu đồ sẽ hiện sau ngày thứ hai.", "The chart appears from the second day."), DiaryMuted);

        AddHeading(L("Giờ sen hay chơi", "When you usually play"), 14);
        if (diary.Habits.Sum() < 10)
            AddLine(L("Chưa đủ dữ liệu, chơi với mèo thêm vài hôm nhé.", "Not enough data yet; play a few more days."), DiaryMuted);
        else
        {
            diaryPanel.Children.Add(HourChart(diary.Habits));
            var top = diary.Habits.Select((value, hour) => (value, hour)).Where(h => h.value > 0)
                .OrderByDescending(h => h.value).Take(3).Select(h => $"{h.hour}h");
            AddLine(L($"Nhiều nhất: {string.Join(", ", top)}", $"Most often: {string.Join(", ", top)}"));
        }

        AddHeading(L("Những gì mèo đã học", "What the cat has learned"), 14);
        AddLine(L("Thích: ", "Likes: ") + (aiLearned is { Likes.Length: > 0 } ? string.Join("; ", aiLearned.Likes) : L("chưa có", "nothing yet")));
        AddLine(L("Không thích: ", "Dislikes: ") + (aiLearned is { Dislikes.Length: > 0 } ? string.Join("; ", aiLearned.Dislikes) : L("chưa có", "nothing yet")));
    }

    private static string Times(long count) => count == 1 ? "once" : $"{count} times";

    private void AddHeading(string text, double size) => diaryPanel!.Children.Add(new TextBlock
    {
        Text = text, FontSize = size, FontWeight = FontWeights.SemiBold, Foreground = DiaryInk, Margin = new Thickness(0, size > 16 ? 0 : 14, 0, 6)
    });

    private void AddLine(string text, Brush? brush = null) => diaryPanel!.Children.Add(new TextBlock
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Foreground = brush ?? DiaryInk, Margin = new Thickness(0, 2, 0, 2)
    });

    // Single series sparkline on a fixed 0-100 scale; the newest day is marked.
    private static FrameworkElement BondChart(double[] values)
    {
        var canvas = new Canvas { Width = ChartWidth, Height = ChartHeight + 6, Margin = new Thickness(0, 2, 0, 4) };
        foreach (double level in new[] { 0.0, 50, 100 })
        {
            double y = 3 + ChartHeight * (1 - level / 100);
            canvas.Children.Add(new Line { X1 = 0, X2 = ChartWidth, Y1 = y, Y2 = y, Stroke = DiaryGrid, StrokeThickness = 1 });
        }
        var line = new Polyline { Stroke = DiaryAccent, StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };
        double step = (ChartWidth - 10) / Math.Max(1, values.Length - 1);
        for (int i = 0; i < values.Length; i++)
        {
            var point = new Point(5 + i * step, 3 + ChartHeight * (1 - values[i] / 100));
            line.Points.Add(point);
            // Invisible, larger hit target for the tooltip.
            var hit = new Ellipse { Width = 14, Height = 14, Fill = Brushes.Transparent, ToolTip = $"{values[i]:F0}/100" };
            Canvas.SetLeft(hit, point.X - 7); Canvas.SetTop(hit, point.Y - 7);
            canvas.Children.Add(hit);
        }
        canvas.Children.Insert(3, line);
        var last = line.Points[^1];
        var marker = new Ellipse { Width = 8, Height = 8, Fill = DiaryAccent, Stroke = DiarySurface, StrokeThickness = 2, IsHitTestVisible = false };
        Canvas.SetLeft(marker, last.X - 4); Canvas.SetTop(marker, last.Y - 4);
        canvas.Children.Add(marker);
        return canvas;
    }

    // 24 columns from one baseline, 2 px gaps, rounded data ends; hours labelled every 6.
    private static FrameworkElement HourChart(double[] habits)
    {
        var canvas = new Canvas { Width = ChartWidth, Height = ChartHeight + 18, Margin = new Thickness(0, 2, 0, 4) };
        double max = Math.Max(1e-9, habits.Max()), slot = ChartWidth / 24;
        canvas.Children.Add(new Line { X1 = 0, X2 = ChartWidth, Y1 = ChartHeight, Y2 = ChartHeight, Stroke = DiaryGrid, StrokeThickness = 1 });
        for (int hour = 0; hour < 24; hour++)
        {
            double height = habits[hour] <= 0 ? 0 : Math.Max(2, ChartHeight * habits[hour] / max);
            var hit = new Border { Width = slot, Height = ChartHeight, Background = Brushes.Transparent, ToolTip = $"{hour}h: {habits[hour]:F1}" };
            Canvas.SetLeft(hit, hour * slot);
            canvas.Children.Add(hit);
            if (height > 0)
            {
                var bar = new Border
                {
                    Width = slot - 2, Height = height, Background = DiaryAccent, IsHitTestVisible = false,
                    CornerRadius = new CornerRadius(Math.Min(4, (slot - 2) / 2), Math.Min(4, (slot - 2) / 2), 0, 0)
                };
                Canvas.SetLeft(bar, hour * slot + 1); Canvas.SetTop(bar, ChartHeight - height);
                canvas.Children.Add(bar);
            }
            if (hour % 6 == 0)
            {
                var label = new TextBlock { Text = $"{hour}h", FontSize = 11, Foreground = DiaryMuted };
                Canvas.SetLeft(label, hour * slot); Canvas.SetTop(label, ChartHeight + 2);
                canvas.Children.Add(label);
            }
        }
        return canvas;
    }
}
