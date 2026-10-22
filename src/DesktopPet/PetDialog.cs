using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopPet;

// A small question or note in the app's warm look, in place of the grey Windows MessageBox:
// a round icon, the message, and pill buttons (the main one on the right).
public static class PetDialog
{
    public const string InfoGlyph = "", QuestionGlyph = "", WarningGlyph = "";

    // True when the main button was pressed (Esc, Cancel or closing the window: false).
    public static bool Show(Window? owner, string title, string message, string ok, string? cancel = null, string glyph = InfoGlyph) =>
        Create(owner, title, message, ok, cancel, glyph).ShowDialog() == true;

    // extra (optional, e.g. a checkbox) goes under the message.
    public static Window Create(Window? owner, string title, string message, string ok, string? cancel = null, string glyph = InfoGlyph, UIElement? extra = null)
    {
        var icon = new Grid { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 14, 0) };
        icon.Children.Add(new Ellipse { Fill = PetUi.AccentSoft });
        var mark = PetUi.Icon(glyph, PetUi.Accent);
        mark.FontSize = PetUi.HeadingSize + 2;
        icon.Children.Add(mark);
        var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = PetUi.Ink, VerticalAlignment = VerticalAlignment.Center, LineHeight = 19 };
        var body = new DockPanel { Margin = new Thickness(20, 20, 20, 14) };
        DockPanel.SetDock(icon, Dock.Left);
        body.Children.Add(icon);
        if (extra == null) body.Children.Add(text);
        else
        {
            if (extra is FrameworkElement element) element.Margin = new Thickness(0, 12, 0, 0);
            var column = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            column.Children.Add(text); column.Children.Add(extra);
            body.Children.Add(column);
        }

        var okButton = PetUi.PrimaryButton(ok);
        Button[] buttons;
        if (cancel != null)
        {
            var cancelButton = PetUi.SecondaryButton(cancel);
            cancelButton.IsCancel = true;
            buttons = new[] { cancelButton, okButton };
        }
        else { okButton.IsCancel = true; buttons = new[] { okButton }; }
        var bar = new Border { Child = PetUi.ButtonBar(null, buttons), BorderBrush = PetUi.Line, BorderThickness = new Thickness(0, 1, 0, 0) };
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Bottom);
        root.Children.Add(bar); root.Children.Add(body);

        var window = new Window
        {
            Title = title, Icon = AppIcon.WindowImage(), Content = root, Width = 400, SizeToContent = SizeToContent.Height,
            Background = PetUi.Surface, ResizeMode = ResizeMode.NoResize, Topmost = true, ShowInTaskbar = false,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
        };
        if (owner != null) window.Owner = owner;
        PetUi.ApplyWindowFont(window);
        PetControlStyle.Apply(window);
        okButton.Click += (_, _) => window.DialogResult = true;
        return window;
    }
}
