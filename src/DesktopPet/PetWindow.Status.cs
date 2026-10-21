using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace DesktopPet;

// The pet's status card (menu "<Name>'s status"): what the cat is doing and feeling now, plus
// the energy and needs the Python AI last reported. Display only: opening, refreshing or
// closing it never wakes, moves or interrupts the cat and is not an interaction.
public sealed partial class PetWindow
{
    public const double StatusCardWidth = 260;
    private Window? statusWindow;
    private bool statusClosing; // Closing deactivates the card, which must not close it again.
    private DispatcherTimer? statusTimer;
    private TextBlock? statusTitle, statusDoing, statusFeeling, statusHint;
    private StatusBar[]? statusBars;

    private sealed record StatusBar(Grid Row, TextBlock Label, TextBlock Value, Border Fill);

    public void ShowStatusCard()
    {
        if (statusWindow != null) { statusWindow.Activate(); return; }
        statusTitle = new TextBlock { FontSize = PetUi.HeadingSize, FontWeight = FontWeights.Bold, Foreground = PetUi.Ink, VerticalAlignment = VerticalAlignment.Center };
        var close = new Button { Content = PetUi.Icon("", PetUi.Muted), Width = 26, Height = 26, Cursor = Cursors.Hand, Template = StatusCloseTemplate,
            Background = Brushes.Transparent, ToolTip = L("Đóng", "Close") };
        close.Click += (_, _) => CloseStatusCard();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close); head.Children.Add(statusTitle);
        statusDoing = new TextBlock { Foreground = PetUi.Ink, TextWrapping = TextWrapping.Wrap };
        statusFeeling = new TextBlock { Foreground = PetUi.Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
        statusHint = new TextBlock { Foreground = PetUi.Muted, FontSize = PetUi.SmallSize, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
        var panel = new StackPanel { Children = { head, statusDoing, statusFeeling } };
        statusBars = new StatusBar[5];
        for (int i = 0; i < statusBars.Length; i++)
        {
            var label = new TextBlock { Foreground = PetUi.Ink, FontSize = PetUi.SmallSize };
            var value = new TextBlock { Foreground = PetUi.Muted, FontSize = PetUi.SmallSize, HorizontalAlignment = HorizontalAlignment.Right };
            var fill = new Border { Background = PetUi.AccentBright, CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            var track = new Border { Background = PetUi.AccentSoft, CornerRadius = new CornerRadius(4), Height = 8, Margin = new Thickness(0, 3, 0, 0), Child = fill };
            var row = new Grid { Margin = new Thickness(0, i == 0 ? 12 : 8, 0, 0) };
            row.RowDefinitions.Add(new RowDefinition()); row.RowDefinitions.Add(new RowDefinition());
            Grid.SetRow(track, 1);
            row.Children.Add(label); row.Children.Add(value); row.Children.Add(track);
            panel.Children.Add(row);
            statusBars[i] = new StatusBar(row, label, value, fill);
        }
        panel.Children.Add(statusHint);
        var card = new Border
        {
            Child = panel, Width = StatusCardWidth, Background = PetUi.CardBack, BorderBrush = PetUi.Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(PetUi.PillRadius), Padding = new Thickness(16, 10, 10, 14), Margin = new Thickness(14),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x4A, 0x36, 0x28), BlurRadius = 16, ShadowDepth = 2, Opacity = 0.22 }
        };
        statusWindow = new Window
        {
            Title = L("Trạng thái", "Status") + " — " + preferences.Name, Content = card, WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize, SizeToContent = SizeToContent.WidthAndHeight,
            Topmost = true, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual
        };
        PetUi.ApplyWindowFont(statusWindow);
        // A glance card: clicking anywhere else, or Esc, puts it away.
        statusWindow.Deactivated += (_, _) => CloseStatusCard();
        statusWindow.KeyDown += (_, e) => { if (e.Key == Key.Escape) CloseStatusCard(); };
        statusWindow.Closing += (_, _) => statusClosing = true;
        statusWindow.Closed += (_, _) => { statusClosing = false; statusTimer?.Stop(); statusTimer = null; statusWindow = null; statusBars = null; };
        RefreshStatusCard();
        statusWindow.Left = -10000; statusWindow.Top = -10000; // Measured first, then placed next to the cat.
        statusWindow.Show();
        statusWindow.UpdateLayout();
        PlaceStatusCard();
        statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
        statusTimer.Tick += (_, _) => RefreshStatusCard();
        statusTimer.Start();
        statusWindow.Activate();
    }

    public void CloseStatusCard() { if (!statusClosing) statusWindow?.Close(); }

    // Above the cat (like the battle notice); below it when the cat sits near the top.
    private void PlaceStatusCard()
    {
        if (statusWindow == null) return;
        double width = statusWindow.ActualWidth, height = statusWindow.ActualHeight;
        double left = Math.Clamp(pet.X + settings.Width / 2 - width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - width));
        double top = pet.Y + 10 - height;
        if (top < bounds.Top) top = Math.Min(pet.Y + settings.Height - 10, Math.Max(bounds.Top, bounds.Bottom - height));
        statusWindow.Left = left; statusWindow.Top = top;
    }

    private void RefreshStatusCard()
    {
        if (statusWindow == null || statusBars == null) return;
        statusTitle!.Text = preferences.Name;
        string doing = battle != null ? L("Đang chiến đấu", "In a battle") : paused ? L("Đang tạm dừng", "Paused") : ActionText(pet.Action);
        if (battle != null) doing += $" · HP {Math.Ceiling(battle.CatHp)}";
        statusDoing!.Text = L("Đang làm: ", "Doing: ") + doing;
        statusFeeling!.Text = L("Cảm xúc: ", "Feeling: ") + EmotionText(pet.Emotions.Current);
        // Needs are kept so that a full bar is always good: fed, entertained, loved.
        var needs = aiNeeds;
        (string Label, double? Value)[] rows =
        {
            (L("Năng lượng", "Energy"), aiEnergy),
            (L("No bụng", "Fed"), needs == null ? null : 100 - needs.Hunger),
            (L("Vui chơi", "Entertained"), needs == null ? null : 100 - needs.Boredom),
            (L("Được cưng", "Loved"), needs?.Affection),
            (L("Thân thiết", "Bond"), needs?.Bond),
        };
        for (int i = 0; i < statusBars.Length; i++)
        {
            var bar = statusBars[i];
            bar.Row.Visibility = rows[i].Value.HasValue ? Visibility.Visible : Visibility.Collapsed;
            if (rows[i].Value is not double value) continue;
            value = Math.Clamp(value, 0, 100);
            bar.Label.Text = rows[i].Label;
            bar.Value.Text = $"{Math.Round(value)}/100";
            bar.Fill.Width = (StatusCardWidth - 26 - 2) * value / 100; // Card width minus padding and border.
        }
        statusHint!.Text = needs == null
            ? L("Bật \"Mèo thông minh (AI)\" ở biểu tượng khay để xem năng lượng và nhu cầu của mèo.",
                "Turn on \"Smart pet (AI)\" in the tray icon to see the pet's energy and needs.")
            : L("Số liệu từ lần AI báo gần nhất.", "From the AI's latest report.");
    }

    private string ActionText(PetAction action) => action switch
    {
        PetAction.Idle => L("Đứng chơi", "Hanging out"),
        PetAction.Walking => L("Đi dạo", "Walking"),
        PetAction.Jumping => L("Nhảy", "Jumping"),
        PetAction.Sitting => L("Ngồi", "Sitting"),
        PetAction.Sleeping => L("Ngủ", "Sleeping"),
        PetAction.Looking => L("Nhìn quanh", "Looking around"),
        PetAction.Dragging => L("Được bế", "Being carried"),
        PetAction.Thrown => L("Đang bay", "Flying"),
        PetAction.Waking => L("Vừa thức dậy", "Waking up"),
        PetAction.SeekingAttention => L("Vẫy chân gọi bạn", "Asking for attention"),
        PetAction.Grooming => L("Liếm lông", "Grooming"),
        PetAction.Stretching => L("Duỗi người", "Stretching"),
        PetAction.PreparingJump => L("Lấy đà nhảy", "Getting ready to jump"),
        PetAction.Kneading => L("Nhồi bột", "Kneading"),
        PetAction.RollingOver => L("Lăn ngửa", "Rolling over"),
        PetAction.ChasingTail => L("Đuổi đuôi", "Chasing its tail"),
        PetAction.Scratching => L("Cào móng", "Scratching"),
        PetAction.Sneezing => L("Hắt xì", "Sneezing"),
        PetAction.Climbing => L("Trèo", "Climbing"),
        _ => action.ToString()
    };

    private string EmotionText(PetEmotion emotion) => emotion switch
    {
        PetEmotion.Happy => L("Vui", "Happy"),
        PetEmotion.Angry => L("Giận", "Angry"),
        PetEmotion.Sad => L("Buồn", "Sad"),
        PetEmotion.Surprised => L("Giật mình", "Surprised"),
        PetEmotion.Sleepy => L("Buồn ngủ", "Sleepy"),
        PetEmotion.Dizzy => L("Chóng mặt", "Dizzy"),
        _ => L("Bình thường", "Normal")
    };

    // A round, borderless close button that turns soft orange on hover.
    private static readonly ControlTemplate StatusCloseTemplate = CreateStatusCloseTemplate();
    private static ControlTemplate CreateStatusCloseTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var chrome = new FrameworkElementFactory(typeof(Border), "Chrome");
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
        chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        chrome.AppendChild(presenter);
        template.VisualTree = chrome;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, PetUi.AccentSoft, "Chrome"));
        template.Triggers.Add(hover);
        template.Seal();
        return template;
    }
}
