using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopPet;

// The pieces Battle Mode's floating panels share (the hub, the setup panel, the Adventure and
// Expedition maps), so they look and behave alike: one frame (cocoa panel, cream rim, the mode's banner
// and a title, content that scrolls on a low monitor, buttons docked below), one main
// button, one details card and one way to show the chosen item. Each panel keeps its own content.
public sealed partial class PetWindow
{
    private static TextBlock PanelLine(double size, bool bold = false, double maxWidth = 420) => new()
    {
        Foreground = Brushes.White, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap, MaxWidth = maxWidth
    };

    // A panel's one main button (Ready, Play stage, Fight): orange, larger bold text.
    private static (Border Button, TextBlock Text) PrimaryButton(Action click)
    {
        var (button, text, _) = HudButton(click, plain: true);
        button.Background = PrepChosen; button.Padding = new Thickness(18, 6, 18, 5); button.MinWidth = 110; button.Margin = new Thickness(0);
        text.FontSize = PetUi.TitleSize; text.FontWeight = FontWeights.Bold;
        return (button, text);
    }

    // The button row under a panel: left items in order, right items from the right edge in
    // order (the main button first, so it ends up on the far right).
    private static DockPanel PanelActions(IEnumerable<UIElement> left, IEnumerable<UIElement> right)
    {
        var actions = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
        foreach (var item in left) { DockPanel.SetDock(item, Dock.Left); actions.Children.Add(item); }
        foreach (var item in right) { DockPanel.SetDock(item, Dock.Right); actions.Children.Add(item); }
        return actions;
    }

    // A panel's title, flush left. The banner above it already shows the mode, so no badge.
    private static FrameworkElement PanelTitle(TextBlock title)
    {
        title.HorizontalAlignment = HorizontalAlignment.Left;
        title.Margin = new Thickness(0, 2, 0, 0);
        return title;
    }

    // The soft card that holds the chosen thing's details (a stage, a node).
    private static Border DetailCard(Thickness padding, params UIElement[] lines)
    {
        var stack = new StackPanel();
        foreach (var line in lines) stack.Children.Add(line);
        return new Border { Background = PrepCardBack, CornerRadius = new CornerRadius(9), Padding = padding, Margin = new Thickness(0, 10, 0, 0), Child = stack };
    }

    // Chosen: a tick, not only a colour. A chip or tab fills orange with a white rim; a big card
    // (card: true) gets a see-through orange and an orange rim so its text and stats stay calm.
    // rim: the rim's width when chosen and when not (null keeps the item's own).
    private static void ShowChosen(Border item, bool selected, Path? check = null, (double On, double Off)? rim = null, bool card = false)
    {
        item.Background = selected ? (card ? PetUi.Game.ChosenCard : PrepChosen) : PrepCardBack;
        item.BorderBrush = selected ? (card ? PetUi.Game.ChosenRim : Brushes.White) : PrepCardRim;
        if (rim is var (on, off)) item.BorderThickness = new Thickness(selected ? on : off);
        if (check != null) check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
    }

    private const string BannerKey = "PetBanner";
    internal const double BannerHeight = 104, ShortBannerHeight = 56, ShortScreen = 700, TinyScreen = 540;

    // The frame and window of a game panel. The content scrolls when it is taller than the cat's
    // monitor (PlaceGamePanel caps the height); the actions stay in view. Not shown yet.
    private Window CreateGamePanel(UIElement content, UIElement actions, string title, double minWidth = 0, PetSceneKind? scene = null)
    {
        var banner = new PetScene(scene ?? (title switch { "Pet adventure" => PetSceneKind.Adventure, "Pet expedition" => PetSceneKind.Expedition,
            "Pet setup" => PetSceneKind.Workshop, _ => PetSceneKind.Home }), BannerHeight);
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false,
            Content = new StackPanel { Children = { banner, content } }
        };
        scroll.Resources.MergedDictionaries.Add(PetControlStyle.Resources);
        var body = new DockPanel();
        DockPanel.SetDock(actions, Dock.Bottom);
        body.Children.Add(actions);
        var crest = new Border { Height = 3, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0,0,0,8),
            Background = PetUi.Game.Gold, Opacity = 0.65, IsHitTestVisible = false };
        DockPanel.SetDock(crest, Dock.Top); body.Children.Add(crest); body.Children.Add(scroll);
        var panel = new Border
        {
            Background = HudBack, BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(6), MinWidth = minWidth,
            Child = body,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 }
        };
        panel.SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
        var window = CreateOverlayWindow(panel, title);
        window.Resources[BannerKey] = banner;
        window.SizeToContent = SizeToContent.WidthAndHeight;
        return window;
    }

    // At the top of the cat's screen, centred (where the HUD and the result panel appear), at most
    // as tall as that screen.
    private void PlaceGamePanel(Window? window)
    {
        if (window == null) return;
        window.MaxHeight = Math.Max(240, bounds.Bottom - bounds.Top - 24);
        // A low screen keeps its room for the content: a shorter banner, none on a very low one.
        if (window.Resources[BannerKey] is PetScene banner)
        {
            double room = window.MaxHeight;
            banner.Height = room < ShortScreen ? ShortBannerHeight : BannerHeight;
            banner.Visibility = room < TinyScreen ? Visibility.Collapsed : Visibility.Visible;
        }
        window.UpdateLayout();
        double width = window.ActualWidth;
        MoveWindow(window, Math.Clamp((bounds.Left + bounds.Right - width) / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - width)), bounds.Top + 12);
    }
}

// A gacha capsule (procedural): a pill, its top half in Color, the bottom cream. Time −1: closed and
// still. From 0 it shakes harder and harder; at PopAt the top flies up and fades while sparkles
// burst out; at Length it is over (the owner then sets Time back to −1).
public sealed class CapsuleArt : FrameworkElement
{
    public const double PopAt = 0.55, Length = 1.0;
    public static readonly Brush PlainColor = PetUi.Hex("#5FB7A8"), GoldColor = PetUi.Game.Gold;
    private static readonly Brush Cream = PetUi.Hex("#FFF3E2"), Shine = PetUi.Hex("#AAFFFFFF"), Glow = PetUi.Hex("#66FFF4C9");
    private static readonly Pen Ink = PetUi.FrozenPen(PetUi.Hex("#3A2C1F"), 1.6);
    private static readonly Pen Spark = PetUi.FrozenPen(PetUi.Hex("#FFF4C9"), 2);

    private Brush color = PlainColor;
    public Brush Color { get => color; set { color = value; InvalidateVisual(); } }
    private double time = -1;
    public double Time { get => time; set { time = value; InvalidateVisual(); } }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double pw = Math.Min(w * 0.8, h * 0.6), ph = Math.Min(h * 0.92, pw * 1.5), cx = w / 2, cy = h / 2 + h * 0.03;
        var pill = new RectangleGeometry(new Rect(cx - pw / 2, cy - ph / 2, pw, ph), pw / 2, pw / 2);
        var top = Geometry.Combine(pill, new RectangleGeometry(new Rect(0, 0, w, cy)), GeometryCombineMode.Intersect, null);
        var bottom = Geometry.Combine(pill, new RectangleGeometry(new Rect(0, cy, w, h)), GeometryCombineMode.Intersect, null);
        bool open = time >= PopAt;
        double angle = time is >= 0 and < PopAt ? 13 * Math.Sin(time * 38) * Math.Min(1, time / 0.3) : 0;
        dc.PushTransform(new RotateTransform(angle, cx, cy + ph / 2));
        if (open)
        {
            // Light and sparkles from the open capsule.
            double k = Math.Clamp((time - PopAt) / (Length - PopAt), 0, 1);
            dc.DrawEllipse(Glow, null, new Point(cx, cy), pw * (0.5 + 0.5 * k), pw * (0.5 + 0.5 * k));
            dc.PushOpacity(1 - k * 0.6);
            for (int i = 0; i < 7; i++)
            {
                double a = -Math.PI / 2 + (i - 3) * 0.42, r0 = pw * (0.35 + 0.3 * k), r1 = r0 + pw * 0.25;
                dc.DrawLine(Spark, new Point(cx + Math.Cos(a) * r0, cy + Math.Sin(a) * r0), new Point(cx + Math.Cos(a) * r1, cy + Math.Sin(a) * r1));
            }
            dc.Pop();
            dc.DrawGeometry(Cream, Ink, bottom);
            dc.PushOpacity(1 - k);
            dc.PushTransform(new TranslateTransform(pw * 0.15 * k, -ph * 0.45 * k));
            dc.PushTransform(new RotateTransform(25 * k, cx, cy));
            dc.DrawGeometry(color, Ink, top);
            dc.Pop(); dc.Pop(); dc.Pop();
        }
        else
        {
            dc.DrawGeometry(Cream, Ink, bottom);
            dc.DrawGeometry(color, Ink, top);
            dc.DrawEllipse(Shine, null, new Point(cx - pw * 0.17, cy - ph * 0.27), pw * 0.1, ph * 0.08);
        }
        dc.Pop();
    }
}

// A mode's picture on top of a hub card: the mode's PetScene (Adventure, Expedition, Infinity).
public sealed class ModeArt : FrameworkElement
{
    public string Mode { get; init; } = "adventure";
    // 1.5 M22: the Adventure card shows the latest open chapter's scene.
    private PetSceneKind? scene;
    public PetSceneKind? Scene
    {
        get => scene;
        set { if (scene == value) return; scene = value; InvalidateVisual(); }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        PetScene.Draw(dc, Mode == "expedition" ? PetSceneKind.Expedition : Mode == "infinity" ? PetSceneKind.Infinity : scene ?? PetSceneKind.Adventure, ActualWidth, ActualHeight);
    }
}
