using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// Hide-and-seek's bush (docs/MINI-GAMES.md → M2): a round leafy bush in the cat's vector style, there only
// during a game. Its own window for a bush the cat is not behind; the cat's window draws the same bush
// over the cat when it hides there (PetRenderer.HideCover), so every click on that one reaches the cat.
public sealed class BushArt : FrameworkElement
{
    public const double ArtWidth = 96, ArtHeight = 76;
    private static Brush B(byte r, byte g, byte b) => PetUi.Frozen(Color.FromRgb(r, g, b));
    private static readonly Brush Leaf = B(0x86, 0xBE, 0x7A), LeafDark = B(0x5F, 0x98, 0x5B), Shine = PetUi.Frozen(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF)),
        Berry = B(0xE8, 0x7A, 0x8A);
    private static readonly Pen Outline = PetUi.FrozenPen(PetUi.ArtInk, 1.6), Vein = PetUi.FrozenPen(B(0x4E, 0x82, 0x4B), 1.3);
    private static readonly Geometry Shape = BuildShape();

    // Seconds into a rustle (negative: still), how far it has grown in (0–1), and how faded (a wrong guess).
    public double Rustle = -1, Grow = 1;

    public BushArt() { Width = ArtWidth; Height = ArtHeight; }

    protected override void OnRender(DrawingContext dc) => Draw(dc, ArtWidth / 2, ArtHeight - 2, Rustle, Grow);

    // Five round clumps joined into one outline, flat on the floor; about 76 wide and 56 tall.
    private static Geometry BuildShape()
    {
        Geometry g = new EllipseGeometry(new Point(0, -26), 24, 24);
        foreach (var (x, y, r) in new[] { (-22.0, -18.0, 17.0), (22.0, -18.0, 17.0), (-12.0, -36.0, 17.0), (12.0, -38.0, 18.0) })
            g = new CombinedGeometry(GeometryCombineMode.Union, g, new EllipseGeometry(new Point(x, y), r, r));
        g = new CombinedGeometry(GeometryCombineMode.Union, g, new RectangleGeometry(new Rect(-34, -16, 68, 16), 8, 8));
        g = new CombinedGeometry(GeometryCombineMode.Intersect, g, new RectangleGeometry(new Rect(-50, -70, 100, 70)));
        var flat = g.GetFlattenedPathGeometry();
        flat.Freeze();
        return flat;
    }

    // Standing on (centreX, bottom).
    public static void Draw(DrawingContext dc, double centreX, double bottom, double rustle = -1, double grow = 1)
    {
        if (grow <= 0) return;
        double sway = rustle >= 0 && rustle < HideSeekRules.SignalSeconds ? Math.Sin(rustle * 32) * 4 * (1 - rustle / HideSeekRules.SignalSeconds) : 0;
        dc.PushTransform(new TranslateTransform(centreX, bottom));
        dc.PushTransform(new ScaleTransform(grow, grow));
        dc.PushTransform(new SkewTransform(sway, 0));
        dc.DrawGeometry(Leaf, Outline, Shape);
        dc.PushClip(Shape);
        dc.DrawEllipse(LeafDark, null, new Point(16, -6), 30, 14);
        dc.DrawEllipse(LeafDark, null, new Point(-26, -4), 14, 9);
        dc.Pop();
        dc.DrawGeometry(null, Outline, Shape);
        dc.DrawLine(Vein, new Point(-10, -30), new Point(-4, -22));
        dc.DrawLine(Vein, new Point(14, -34), new Point(9, -26));
        dc.DrawLine(Vein, new Point(-24, -16), new Point(-17, -12));
        dc.DrawEllipse(Shine, null, new Point(-14, -44), 5, 3);
        dc.DrawEllipse(Shine, null, new Point(6, -48), 4, 2.4);
        dc.DrawEllipse(Berry, Outline, new Point(20, -24), 3, 3);
        dc.DrawEllipse(Berry, Outline, new Point(-6, -14), 2.6, 2.6);
        dc.Pop(); dc.Pop(); dc.Pop();
    }
}
