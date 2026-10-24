using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopPet;

// The pieces Battle Mode's floating panels share (the hub, the setup panel, the Adventure and
// Expedition maps), so they look and behave alike: one frame (cocoa panel, cream rim, a title with
// the mode's picture, content that scrolls on a low monitor, buttons docked below), one main
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

    // A title with the mode's picture in a small round badge on its left.
    private static FrameworkElement PanelTitle(string mode, TextBlock title)
    {
        var badge = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(17), BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1.5),
            Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top,
            Child = new ModeArt { Mode = mode, Round = true }
        };
        // The first line sits in the middle of the badge; a longer title (a stage's goal) runs on below.
        title.VerticalAlignment = VerticalAlignment.Top;
        title.Margin = new Thickness(0, 6, 0, 0);
        var row = new DockPanel { Children = { badge, title } };
        DockPanel.SetDock(badge, Dock.Left);
        return row;
    }

    // The soft card that holds the chosen thing's details (a stage, a node).
    private static Border DetailCard(Thickness padding, params UIElement[] lines)
    {
        var stack = new StackPanel();
        foreach (var line in lines) stack.Children.Add(line);
        return new Border { Background = PrepCardBack, CornerRadius = new CornerRadius(9), Padding = padding, Margin = new Thickness(0, 10, 0, 0), Child = stack };
    }

    // Chosen: the orange fill, a white rim and a tick (not only a colour). rim: the rim's width
    // when chosen and when not (null keeps the item's own).
    private static void ShowChosen(Border item, bool selected, Path? check = null, (double On, double Off)? rim = null)
    {
        item.Background = selected ? PrepChosen : PrepCardBack;
        item.BorderBrush = selected ? Brushes.White : PrepCardRim;
        if (rim is var (on, off)) item.BorderThickness = new Thickness(selected ? on : off);
        if (check != null) check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
    }

    // The frame and window of a game panel. The content scrolls when it is taller than the cat's
    // monitor (PlaceGamePanel caps the height); the actions stay in view. Not shown yet.
    private Window CreateGamePanel(UIElement content, UIElement actions, string title, double minWidth = 0)
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false,
            Content = content
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
        window.SizeToContent = SizeToContent.WidthAndHeight;
        return window;
    }

    // At the top of the cat's screen, centred (where the HUD and the result panel appear), at most
    // as tall as that screen.
    private void PlaceGamePanel(Window? window)
    {
        if (window == null) return;
        window.MaxHeight = Math.Max(240, bounds.Bottom - bounds.Top - 24);
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

// A small vector scene for each game mode (procedural, no image files): "adventure" a path over
// green hills to a flag, "expedition" a snowy peak at dusk with a flag on top, "infinity" a gold
// loop in a starry night. Drawn on a 120×72 design that covers the box; Round clips it to a circle
// (title badges), otherwise to a rounded rectangle (the hub's cards).
public sealed class ModeArt : FrameworkElement
{
    public string Mode { get; init; } = "adventure";
    public bool Round { get; init; }
    // Square corners at the bottom: the picture sits on top of a card.
    public bool CardTop { get; init; }

    private static Brush Sky(string top, string bottom)
    {
        var brush = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(top), (Color)ColorConverter.ConvertFromString(bottom), 90);
        brush.Freeze();
        return brush;
    }
    private static readonly Brush AdventureSky = Sky("#FFB36B", "#FFE6C2"), ExpeditionSky = Sky("#3E4C7A", "#A283B4"), InfinitySky = Sky("#231B3D", "#53306F");
    private static readonly Brush Sun = PetUi.Hex("#FFF4C9"), HillBack = PetUi.Hex("#86C46E"), HillFront = PetUi.Hex("#5EA352"), Pole = PetUi.Hex("#6B4A35");
    private static readonly Brush FarPeaks = PetUi.Hex("#6E6C9C"), Peak = PetUi.Hex("#4B4470"), Snow = PetUi.Hex("#F4EFFF"), SnowField = PetUi.Hex("#E6E0F2");
    private static readonly Pen Trail = MakeTrail();
    private static readonly Pen NodeRim = PetUi.FrozenPen(Brushes.White, 1.4);
    private static readonly Pen PolePen = PetUi.FrozenPen(Pole, 1.6), SnowPolePen = PetUi.FrozenPen(PetUi.Hex("#F1E4D6"), 1.4);
    private static readonly Pen LoopGlow = PetUi.FrozenPen(PetUi.Hex("#55FFD35C"), 11), LoopPen = PetUi.FrozenPen(PetUi.Game.Gold, 6), LoopShine = PetUi.FrozenPen(PetUi.Hex("#FFF4C9"), 1.8);
    private static readonly Geometry BackHill = PetUi.FrozenGeometry("M 0,50 Q 30,30 62,46 T 120,40 L 120,72 L 0,72 Z");
    private static readonly Geometry FrontHill = PetUi.FrozenGeometry("M 0,62 Q 40,46 80,60 T 120,56 L 120,72 L 0,72 Z");
    private static readonly Geometry TrailPath = PetUi.FrozenGeometry("M 8,70 C 26,60 34,54 52,56 S 80,50 93,41");
    private static readonly Geometry RedFlag = PetUi.FrozenGeometry("M 93,25 L 106,29.5 L 93,34 Z");
    private static readonly Geometry FarRange = PetUi.FrozenGeometry("M 0,60 L 22,38 L 38,52 L 58,32 L 80,54 L 100,38 L 120,52 L 120,72 L 0,72 Z");
    private static readonly Geometry MainPeak = PetUi.FrozenGeometry("M 16,72 L 62,18 L 108,72 Z");
    private static readonly Geometry SnowCap = PetUi.FrozenGeometry("M 62,18 L 73.5,31.5 L 68,29 L 64,34 L 59.5,29 L 51,31.5 Z");
    private static readonly Geometry SnowFront = PetUi.FrozenGeometry("M 0,66 Q 30,60 60,68 T 120,63 L 120,72 L 0,72 Z");
    private static readonly Geometry GoldFlag = PetUi.FrozenGeometry("M 62,6 L 73,9.5 L 62,13 Z");
    private static readonly Geometry Loop = PetUi.FrozenGeometry("M 60,37 C 70,22 93,22 93,37 C 93,52 70,52 60,37 C 50,22 27,22 27,37 C 27,52 50,52 60,37 Z");

    private static Pen MakeTrail()
    {
        var pen = new Pen(PetUi.Hex("#FFF3E2"), 2.6) { DashStyle = new DashStyle(new[] { 1.6, 1.6 }, 0), DashCap = PenLineCap.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }

    // A four-point twinkle.
    private static void Twinkle(DrawingContext dc, double x, double y, double r, Brush fill)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(x, y - r), true, true);
            c.QuadraticBezierTo(new Point(x, y), new Point(x + r, y), true, false);
            c.QuadraticBezierTo(new Point(x, y), new Point(x, y + r), true, false);
            c.QuadraticBezierTo(new Point(x, y), new Point(x - r, y), true, false);
            c.QuadraticBezierTo(new Point(x, y), new Point(x, y - r), true, false);
        }
        g.Freeze();
        dc.DrawGeometry(fill, null, g);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        Geometry clip = Round ? new EllipseGeometry(new Point(w / 2, h / 2), w / 2, h / 2)
            : CardTop ? Geometry.Combine(new RectangleGeometry(new Rect(0, 0, w, h), 9, 9), new RectangleGeometry(new Rect(0, h / 2, w, h / 2)), GeometryCombineMode.Union, null)
            : new RectangleGeometry(new Rect(0, 0, w, h), 9, 9);
        dc.PushClip(clip);
        double k = Math.Max(w / 120, h / 72);
        dc.PushTransform(new TranslateTransform((w - 120 * k) / 2, (h - 72 * k) / 2));
        dc.PushTransform(new ScaleTransform(k, k));
        switch (Mode)
        {
            case "expedition":
                dc.DrawRectangle(ExpeditionSky, null, new Rect(0, 0, 120, 72));
                Twinkle(dc, 18, 14, 2.6, Sun); Twinkle(dc, 98, 12, 2, Sun); Twinkle(dc, 108, 26, 1.6, Sun); Twinkle(dc, 34, 26, 1.4, Sun);
                dc.DrawGeometry(FarPeaks, null, FarRange);
                dc.DrawGeometry(Peak, null, MainPeak);
                dc.DrawGeometry(Snow, null, SnowCap);
                dc.DrawLine(SnowPolePen, new Point(62, 18), new Point(62, 6));
                dc.DrawGeometry(PetUi.Game.Gold, null, GoldFlag);
                dc.DrawGeometry(SnowField, null, SnowFront);
                break;
            case "infinity":
                dc.DrawRectangle(InfinitySky, null, new Rect(0, 0, 120, 72));
                Twinkle(dc, 16, 14, 3, Sun); Twinkle(dc, 104, 12, 2.4, Sun); Twinkle(dc, 108, 58, 2.8, Sun); Twinkle(dc, 12, 58, 2, Sun); Twinkle(dc, 60, 10, 1.6, Sun);
                dc.DrawGeometry(null, LoopGlow, Loop);
                dc.DrawGeometry(null, LoopPen, Loop);
                dc.DrawGeometry(null, LoopShine, Loop);
                break;
            default:
                dc.DrawRectangle(AdventureSky, null, new Rect(0, 0, 120, 72));
                dc.DrawEllipse(Sun, null, new Point(26, 18), 9, 9);
                dc.DrawGeometry(HillBack, null, BackHill);
                dc.DrawGeometry(HillFront, null, FrontHill);
                dc.DrawGeometry(null, Trail, TrailPath);
                dc.DrawEllipse(PetUi.Game.Gold, NodeRim, new Point(27, 60.5), 3.4, 3.4);
                dc.DrawEllipse(PetUi.Game.Gold, NodeRim, new Point(52, 56), 3.4, 3.4);
                dc.DrawEllipse(PetUi.Accent, NodeRim, new Point(76, 50), 3.4, 3.4);
                dc.DrawLine(PolePen, new Point(93, 42), new Point(93, 25));
                dc.DrawGeometry(PetUi.Accent, null, RedFlag);
                break;
        }
        dc.Pop(); dc.Pop(); dc.Pop();
    }
}
