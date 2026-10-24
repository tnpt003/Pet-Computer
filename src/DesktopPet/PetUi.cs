using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// Shared look for menus and windows: Windows' own icon font (no new assets or
// dependencies), one text font and a warm, cartoon-friendly palette. Colors live here as
// hex tokens so the XAML menu style and the C#-built windows use the same values.
public static class PetUi
{
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    // Segoe UI has no Japanese; Yu Gothic UI (Windows 10/11) draws it.
    public static readonly FontFamily TextFont = new("Segoe UI, Yu Gothic UI");

    // Palette. Accent passes 4.5:1 with white text; Muted passes 4.5:1 on Surface.
    public const string AccentHex = "#C2561F", AccentHoverHex = "#A84816", AccentBrightHex = "#E8834F",
        AccentSoftHex = "#FCE9DB", AccentSofterHex = "#F8D9C4", InkHex = "#4A3628", MutedHex = "#7D6656",
        SurfaceHex = "#FFF8F1", CardHex = "#FFFFFF", LineHex = "#EEDFD0", ControlLineHex = "#E2CDB9", StageHex = "#FBEFE3";

    public static readonly Brush Accent = Hex(AccentHex);
    public static readonly Brush AccentBright = Hex(AccentBrightHex); // Charts and decoration, not text.
    public static readonly Brush AccentSoft = Hex(AccentSoftHex);
    public static readonly Brush Ink = Hex(InkHex);
    public static readonly Brush Muted = Hex(MutedHex);
    public static readonly Brush Surface = Hex(SurfaceHex);
    public static readonly Brush CardBack = Hex(CardHex);
    public static readonly Brush Line = Hex(LineHex);
    public static readonly Brush Stage = Hex(StageHex); // Behind pet previews.
    private static readonly Brush AccentHover = Hex(AccentHoverHex);
    private static readonly Brush ControlLine = Hex(ControlLineHex);

    // Type scale and corner radii.
    public const double CaptionSize = 11, SmallSize = 12, BodySize = 13, TitleSize = 14, HeadingSize = 16, DisplaySize = 20;
    public const double SmallRadius = 6, CardRadius = 12, PillRadius = 16;

    // Segoe glyph codes used by the menus.
    public const string Treat = "", Ball = "", Laser = "", Play = "", Emotions = "",
        Home = "", Diary = "", Settings = "", More = "", Exit = "", Debug = "", About = "";

    public const string Battle = ""; // Game controller.
    public const string Status = ""; // Heart.
    public const string Customize = ""; // Personalize (brush).
    public const string Place = ""; // Map pin.

    // Frozen brushes, geometries and round pens: the one copy for every drawing in the app.
    public static Brush Frozen(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }
    public static Brush Frozen(byte a, byte r, byte g, byte b) => Frozen(Color.FromArgb(a, r, g, b));
    public static Brush Hex(string hex) => Frozen((Color)ColorConverter.ConvertFromString(hex));
    public static Geometry FrozenGeometry(string path) { var geometry = Geometry.Parse(path); geometry.Freeze(); return geometry; }
    public static Pen FrozenPen(Brush brush, double thickness, DashStyle? dash = null)
    {
        var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        if (dash != null) pen.DashStyle = dash;
        pen.Freeze();
        return pen;
    }

    public static TextBlock Icon(string glyph, Brush? color = null) => new()
    {
        Text = glyph, FontFamily = IconFont, FontSize = TitleSize, Foreground = color ?? Ink,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };

    // Semantic inks work on light surfaces; bright variants remain artwork only.
    public static readonly Brush Care = Hex("#327354"), Mystery = Hex("#7953A1"), Danger = Hex("#B94444"), Reward = Hex("#976412");
    public static Brush IconColor(string glyph) => glyph == Status || glyph == Emotions ? Care
        : glyph == Diary ? Mystery : glyph == Battle || glyph == Treat ? Reward
        : glyph == Exit ? Danger : Accent;
    public static MenuItem MenuItem(string glyph, Brush? color = null) => new()
    {
        Icon = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(7), Background = AccentSoft,
            IsHitTestVisible = false, Child = Icon(glyph, color ?? IconColor(glyph)) }
    };

    // Small grey explanation under a setting.
    public static TextBlock Hint(string text) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Muted, FontSize = SmallSize, Margin = new Thickness(0, 4, 0, 0)
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

    // The default button look turns light blue on hover, which hides white text: draw our own,
    // as soft pills that suit the cartoon pet.
    public static Button PrimaryButton(string text) => new()
    {
        Content = text, MinWidth = 88, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(8, 0, 0, 0),
        Background = Accent, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, IsDefault = true,
        Cursor = System.Windows.Input.Cursors.Hand, Template = PrimaryTemplate
    };

    public static Button SecondaryButton(string text) => new()
    {
        Content = text, MinWidth = 88, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(8, 0, 0, 0),
        Background = CardBack, BorderBrush = ControlLine, Foreground = Ink, Cursor = System.Windows.Input.Cursors.Hand,
        Template = SecondaryTemplate
    };

    private static readonly ControlTemplate PrimaryTemplate = CreatePillTemplate(AccentHover, null);
    private static readonly ControlTemplate SecondaryTemplate = CreatePillTemplate(AccentSoft, Accent);

    // A pill with a hover fill and, for outlined buttons, a hover border.
    private static ControlTemplate CreatePillTemplate(Brush hoverBack, Brush? hoverBorder)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "Chrome");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(PillRadius));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(hoverBorder == null ? 0 : 1));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, hoverBack, "Chrome"));
        if (hoverBorder != null) hover.Setters.Add(new Setter(Border.BorderBrushProperty, hoverBorder, "Chrome"));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5, "Chrome"));
        template.Triggers.Add(hover); template.Triggers.Add(disabled);
        template.Seal();
        return template;
    }

    // A white, softly bordered, well-rounded group of related content inside a Surface window.
    public static Border Card(UIElement child, Thickness? margin = null) => new()
    {
        Child = child, Background = CardBack, BorderBrush = Line, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(CardRadius), Padding = new Thickness(14, 12, 14, 12), Margin = margin ?? new Thickness(0, 0, 0, 10)
    };

    public static TextBlock SectionTitle(string text) => new()
    {
        Text = text, FontSize = TitleSize, FontWeight = FontWeights.SemiBold, Foreground = Ink, Margin = new Thickness(0, 0, 0, 6)
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
        window.FontSize = BodySize;
        window.Foreground = Ink;
    }

    // Battle Mode's floating panels and HUD: dark warm cocoa (readable over any wallpaper) with a
    // cream rim, the app's orange for the chosen thing, gold for rewards, records and rare cards.
    public static class Game
    {
        public static readonly Brush Panel = Hex("#E8302520");
        public static readonly Color HudTop = (Color)ColorConverter.ConvertFromString("#F0473529"),
            HudBottom = (Color)ColorConverter.ConvertFromString("#F02A1E19");
        public static readonly Brush Rim = Hex("#FFF3E2"), HudRim = Hex("#C8FFF3E2");
        public static readonly Brush Chosen = Accent, Armed = Hex("#C0C2561F"), Progress = AccentBright;
        public static readonly Brush CardBack = Hex("#33FFF0E0"), CardRim = Hex("#66FFE9D2"), RareBack = Hex("#2EFFC82E");
        public static readonly Brush ButtonBack = Hex("#50FFF0E0"), Stage = Hex("#26FFF0E0"), BarBack = Hex("#40FFF0E0");
        public static readonly Brush Locked = Hex("#38FFF0E0"), Ahead = Hex("#50FFF0E0");
        public static readonly Brush Gold = Hex("#FFD35C"), GoldInk = Hex("#3A2C08"), RareGold = Hex("#FFC82E"), Warning = Hex("#FFC86A");
        public static readonly Brush Soft = Hex("#F1E4D6"); // Secondary HUD text.
        public static Brush Shadow(byte alpha) => Frozen(Color.FromArgb(alpha, 0x14, 0x0D, 0x0A)); // Warm near-black wells.
        public const double ButtonRadius = 12;
    }
}
