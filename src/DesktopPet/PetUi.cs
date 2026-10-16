using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// Shared look for menus and windows: Windows' own icon font (no new assets or
// dependencies), one text font and a few colors.
public static class PetUi
{
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public static readonly FontFamily TextFont = new("Segoe UI");
    public static readonly Brush Accent = Frozen(Color.FromRgb(0x2A, 0x78, 0xD6));
    public static readonly Brush Muted = Frozen(Color.FromRgb(0x6E, 0x6C, 0x66));
    public static readonly Brush Surface = Frozen(Color.FromRgb(0xFC, 0xFC, 0xFB));
    public static readonly Brush Line = Frozen(Color.FromRgb(0xE4, 0xE2, 0xDD));

    // Segoe glyph codes used by the menus.
    public const string Treat = "", Ball = "", Laser = "", Play = "", Emotions = "",
        Home = "", Diary = "", Settings = "", More = "", Exit = "", Debug = "", About = "";

    public static Brush Frozen(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }

    public static TextBlock Icon(string glyph, Brush? color = null) => new()
    {
        Text = glyph, FontFamily = IconFont, FontSize = 14, Foreground = color ?? Brushes.Black,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };

    public static MenuItem MenuItem(string glyph, Brush? color = null) => new() { Icon = Icon(glyph, color) };

    // Small grey explanation under a setting.
    public static TextBlock Hint(string text) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 4, 0, 0)
    };

    // A text-only button that reads like a link (for rarely used, secondary actions).
    public static Button LinkButton(string text)
    {
        var label = new TextBlock { Text = text, Foreground = Accent, TextDecorations = TextDecorations.Underline };
        return new Button { Content = label, Cursor = System.Windows.Input.Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 0), Template = LinkTemplate };
    }

    private static readonly ControlTemplate LinkTemplate = CreateLinkTemplate();
    private static ControlTemplate CreateLinkTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        template.VisualTree = presenter;
        template.Seal();
        return template;
    }

    // The default button look turns light blue on hover, which hides white text: draw our own.
    public static Button PrimaryButton(string text) => new()
    {
        Content = text, MinWidth = 88, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(8, 0, 0, 0),
        Background = Accent, Foreground = Brushes.White, IsDefault = true, Cursor = System.Windows.Input.Cursors.Hand,
        Template = PrimaryTemplate
    };

    private static readonly Brush AccentHover = Frozen(Color.FromRgb(0x1F, 0x63, 0xB5));
    private static readonly ControlTemplate PrimaryTemplate = CreatePrimaryTemplate();
    private static ControlTemplate CreatePrimaryTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "Chrome");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, AccentHover, "Chrome"));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5, "Chrome"));
        template.Triggers.Add(hover); template.Triggers.Add(disabled);
        template.Seal();
        return template;
    }

    public static Button SecondaryButton(string text) => new()
    {
        Content = text, MinWidth = 88, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(8, 0, 0, 0)
    };

    public static readonly Brush Ink = Frozen(Color.FromRgb(0x2B, 0x2A, 0x28));

    // A white, softly bordered group of related content inside a Surface window.
    public static Border Card(UIElement child, Thickness? margin = null) => new()
    {
        Child = child, Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 12, 14, 12), Margin = margin ?? new Thickness(0, 0, 0, 10)
    };

    public static TextBlock SectionTitle(string text) => new()
    {
        Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Ink, Margin = new Thickness(0, 0, 0, 6)
    };

    // A bottom bar: an optional link-style action on the left, buttons on the right.
    public static DockPanel ButtonBar(Button? left, params Button[] right)
    {
        var bar = new DockPanel { Margin = new Thickness(16, 10, 16, 12), LastChildFill = false };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var button in right) buttons.Children.Add(button);
        DockPanel.SetDock(buttons, Dock.Right);
        bar.Children.Add(buttons);
        if (left != null) { left.Margin = new Thickness(0); left.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(left); }
        return bar;
    }

    public static void ApplyWindowFont(Window window)
    {
        window.FontFamily = TextFont;
        window.FontSize = 13;
    }
}
