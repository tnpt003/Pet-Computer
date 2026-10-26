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
    private const double ChartWidth = 312, ChartHeight = 64;

    public void ShowDiary()
    {
        if (diaryWindow != null) { diaryWindow.Activate(); return; }
        diaryPanel = new StackPanel { Margin = new Thickness(18, 14, 18, 18) };
        double areaHeight = Math.Max(300, bounds.Bottom - bounds.Top);
        double height = Math.Min(600, areaHeight - 24);
        diaryWindow = new Window
        {
            Title = LF($"Nhật ký của {preferences.Name}", $"{preferences.Name}'s diary"), Icon = AppIcon.WindowImage(),
            Width = 420, Height = height, Background = PetUi.Surface, ResizeMode = ResizeMode.NoResize, Topmost = true, ShowInTaskbar = false,
            Content = new ScrollViewer { Content = diaryPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = Math.Clamp(pet.X + settings.Width / 2 - 210, bounds.Left, Math.Max(bounds.Left, bounds.Right - 420)),
            Top = bounds.Top + Math.Max(8, (areaHeight - height) / 2)
        };
        PetUi.ApplyWindowFont(diaryWindow);
        PetControlStyle.Apply(diaryWindow);
        diaryWindow.Closed += (_, _) => { diaryWindow = null; diaryPanel = null; };
        RefreshDiary();
        diaryWindow.Show();
    }

    private void RefreshDiary()
    {
        if (diaryPanel == null) return;
        diaryPanel.Children.Clear();
        diaryPanel.Children.Add(new PetScene(PetSceneKind.Diary, 100));
        AddHeading(LF($"Nhật ký của {preferences.Name}", $"{preferences.Name}'s diary"), 18);
        if (aiDiary is not { } diary)
        {
            var session = pet.Interactions;
            AddLine(L("AI đang tắt hoặc chưa sẵn sàng, nên chỉ có số liệu của lần chạy này.",
                   "The AI is off or not ready yet, so only this session's numbers are shown."), PetUi.Muted);
            AddStats((session.Petted, L("Được vuốt", "Petted")), (session.Treats, L("Treat", "Treats")), (session.Toy + session.BallClicks, L("Chơi bóng", "Ball games")));
            return;
        }
        AddLine(LF($"Đã ở cùng sen {diary.Days} ngày (từ {diary.Since})", $"Together for {diary.Days} days (since {diary.Since})"), PetUi.Muted);
        AddStats((diary.Petted, L("Được vuốt", "Petted")), (diary.Treats, L("Treat", "Treats")), (diary.Play, L("Chơi bóng", "Ball games")), ((long)Math.Round(diary.Bond), L("Thân thiết", "Bond")));
        AddLine(LF($"Click {diary.Clicks} lần · bị ném mạnh {diary.RoughThrows} lần", $"Clicked {Times(diary.Clicks)} · thrown roughly {Times(diary.RoughThrows)}"), PetUi.Muted);
        if (diary.BattlesWon + diary.BattlesLost > 0)
            AddLine(LF($"Chiến đấu: thắng {diary.BattlesWon} · thua {diary.BattlesLost} · combo {diary.Combos}",
                   $"Battles: {diary.BattlesWon} won · {diary.BattlesLost} lost · {diary.Combos} combo{(diary.Combos == 1 ? "" : "s")}"), PetUi.Muted);

        var bond = AddCard(LF($"Độ thân thiết: {diary.Bond:F0}/100", $"Bond: {diary.Bond:F0}/100"));
        if (diary.BondHistory.Length >= 2) bond.Children.Add(BondChart(diary.BondHistory));
        bond.Children.Add(TextLine(diary.BondHistory.Length >= 2
            ? LF($"{diary.BondHistory.Length} ngày gần nhất: {string.Join(", ", diary.BondHistory.Select(v => v.ToString("F0")))}",
                $"Last {diary.BondHistory.Length} days: {string.Join(", ", diary.BondHistory.Select(v => v.ToString("F0")))}")
            : L("Biểu đồ sẽ hiện sau ngày thứ hai.", "The chart appears from the second day."), PetUi.Muted));

        var hours = AddCard(L("Giờ sen hay chơi", "When you usually play"));
        if (diary.Habits.Sum() < 10)
            hours.Children.Add(TextLine(L("Chưa đủ dữ liệu, chơi với mèo thêm vài hôm nhé.", "Not enough data yet; play a few more days."), PetUi.Muted));
        else
        {
            hours.Children.Add(HourChart(diary.Habits));
            var top = diary.Habits.Select((value, hour) => (value, hour)).Where(h => h.value > 0)
                .OrderByDescending(h => h.value).Take(3).Select(h => $"{h.hour}h");
            hours.Children.Add(TextLine(LF($"Nhiều nhất: {string.Join(", ", top)}", $"Most often: {string.Join(", ", top)}")));
        }

        var learned = AddCard(L("Những gì mèo đã học", "What the cat has learned"));
        learned.Children.Add(TextLine(L("Thích: ", "Likes: ") + (aiLearned is { Likes.Length: > 0 } ? string.Join("; ", aiLearned.Likes) : L("chưa có", "nothing yet"))));
        learned.Children.Add(TextLine(L("Không thích: ", "Dislikes: ") + (aiLearned is { Dislikes.Length: > 0 } ? string.Join("; ", aiLearned.Dislikes) : L("chưa có", "nothing yet"))));
    }

    private static string Times(long count) => count == 1 ? "once" : $"{count} times";

    private void AddHeading(string text, double size) => diaryPanel!.Children.Add(new TextBlock
    {
        Text = text, FontSize = size, FontWeight = FontWeights.SemiBold, Foreground = PetUi.Ink, Margin = new Thickness(0, 0, 0, 2)
    });

    private void AddLine(string text, Brush? brush = null) => diaryPanel!.Children.Add(TextLine(text, brush));

    private static TextBlock TextLine(string text, Brush? brush = null) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Foreground = brush ?? PetUi.Ink, Margin = new Thickness(0, 2, 0, 2)
    };

    // A titled card; returns the panel its content goes into.
    private StackPanel AddCard(string title)
    {
        var content = new StackPanel();
        content.Children.Add(PetIllustration.Beside(PetUi.SectionTitle(title), PetMotif.Book, 24));
        diaryPanel!.Children.Add(PetUi.Card(content));
        return content;
    }

    // Headline numbers as equal tiles: the value large, its label small underneath.
    private void AddStats(params (long Value, string Label)[] stats)
    {
        var row = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Margin = new Thickness(-4, 10, -4, 6) };
        foreach (var (value, label) in stats)
        {
            var tile = new StackPanel();
            tile.Children.Add(new PetIllustration(row.Children.Count == 0 ? PetMotif.Heart : row.Children.Count == 1 ? PetMotif.Leaf : row.Children.Count == 2 ? PetMotif.Cat : PetMotif.Heart, 30) { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0,0,0,6) });
            tile.Children.Add(new TextBlock { Text = value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), FontSize = PetUi.DisplaySize, FontWeight = FontWeights.SemiBold, Foreground = PetUi.AccentText });
            tile.Children.Add(new TextBlock { Text = label, FontSize = PetUi.SmallSize, Foreground = PetUi.Muted, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(PetUi.Card(tile, new Thickness(4, 0, 4, 0)));
        }
        diaryPanel!.Children.Add(row);
    }

    // Single series sparkline on a fixed 0-100 scale; the newest day is marked.
    private static FrameworkElement BondChart(double[] values)
    {
        var canvas = new Canvas { Width = ChartWidth, Height = ChartHeight + 6, Margin = new Thickness(0, 2, 0, 4) };
        foreach (double level in new[] { 0.0, 50, 100 })
        {
            double y = 3 + ChartHeight * (1 - level / 100);
            canvas.Children.Add(new Line { X1 = 0, X2 = ChartWidth, Y1 = y, Y2 = y, Stroke = PetUi.Line, StrokeThickness = 1 });
        }
        var line = new Polyline { Stroke = PetUi.AccentBright, StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };
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
        var marker = new Ellipse { Width = 8, Height = 8, Fill = PetUi.AccentBright, Stroke = PetUi.CardBack, StrokeThickness = 2, IsHitTestVisible = false };
        Canvas.SetLeft(marker, last.X - 4); Canvas.SetTop(marker, last.Y - 4);
        canvas.Children.Add(marker);
        return canvas;
    }

    // 24 columns from one baseline, 2 px gaps, rounded data ends; hours labelled every 6.
    private static FrameworkElement HourChart(double[] habits)
    {
        var canvas = new Canvas { Width = ChartWidth, Height = ChartHeight + 18, Margin = new Thickness(0, 2, 0, 4) };
        double max = Math.Max(1e-9, habits.Max()), slot = ChartWidth / 24;
        canvas.Children.Add(new Line { X1 = 0, X2 = ChartWidth, Y1 = ChartHeight, Y2 = ChartHeight, Stroke = PetUi.Line, StrokeThickness = 1 });
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
                    Width = slot - 2, Height = height, Background = PetUi.AccentBright, IsHitTestVisible = false,
                    CornerRadius = new CornerRadius(Math.Min(4, (slot - 2) / 2), Math.Min(4, (slot - 2) / 2), 0, 0)
                };
                Canvas.SetLeft(bar, hour * slot + 1); Canvas.SetTop(bar, ChartHeight - height);
                canvas.Children.Add(bar);
            }
            if (hour % 6 == 0)
            {
                var label = new TextBlock { Text = $"{hour}h", FontSize = PetUi.CaptionSize, Foreground = PetUi.Muted };
                Canvas.SetLeft(label, hour * slot); Canvas.SetTop(label, ChartHeight + 2);
                canvas.Children.Add(label);
            }
        }
        return canvas;
    }
}
