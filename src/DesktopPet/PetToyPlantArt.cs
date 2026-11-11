using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The plant pot (docs/LIVING-CORNER.md → M2): a terracotta pot and a round-leafed plant in the cat's
// vector style, in four stages (sprout, small plant, leafy, in flower). Rustle sways the leaves (a click,
// the cat's paw), Sparkle is the short glitter when it reaches a new stage. Only what is drawn takes clicks.
public sealed class ToyPlantArt : FrameworkElement
{
    public const double ArtWidth = 84, ArtHeight = 112;
    private const double Cx = ArtWidth / 2, Bottom = ArtHeight;

    private static Brush B(byte r, byte g, byte b) => PetUi.Frozen(Color.FromRgb(r, g, b));
    private static Pen P(Brush brush, double width) { var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
    private static readonly Brush Clay = B(0xC8, 0x6E, 0x48), ClayRim = B(0xD9, 0x86, 0x5C), Soil = B(0x5E, 0x42, 0x30),
        Leaf = B(0x6F, 0xB0, 0x5C), LeafLight = B(0x93, 0xC9, 0x74), Petal = B(0xF2, 0xA6, 0xB8), Heart = B(0xF4, 0xD0, 0x5E),
        Shade = PetUi.Frozen(Color.FromArgb(0x30, 0x5A, 0x24, 0x10)), Glint = PetUi.Frozen(Color.FromArgb(0xE0, 0xFF, 0xF4, 0xB8));
    private static readonly Pen Outline = P(PetUi.ArtInk, 1.4), Stem = P(B(0x4E, 0x8A, 0x46), 2.2), Vein = P(B(0x4E, 0x8A, 0x46), 0.9);

    public int Stage;
    public double Rustle;
    // Seconds into the glitter of a new stage (negative: none).
    public double Sparkle = -1;
    public const double SparkleSeconds = 1.2;

    public ToyPlantArt() { Width = ArtWidth; Height = ArtHeight; }

    // How tall the plant stands above the pot's rim, per stage.
    public static double PlantHeight(int stage) => stage switch { 0 => 16, 1 => 34, 2 => 52, _ => 58 };

    protected override void OnRender(DrawingContext dc)
    {
        double rimTop = Bottom - DesktopToyRules.PlantHeight;
        int stage = Math.Clamp(Stage, 0, 3);
        double height = PlantHeight(stage);
        // The plant first, growing out of the soil, swaying from its base.
        dc.PushTransform(new RotateTransform(Rustle, Cx, rimTop + 4));
        var top = new Point(Cx, rimTop + 2 - height);
        var stem = new StreamGeometry();
        using (var g = stem.Open())
        {
            g.BeginFigure(new Point(Cx, rimTop + 4), false, false);
            g.BezierTo(new Point(Cx - 4, rimTop - height * 0.4), new Point(Cx + 4, rimTop - height * 0.7), top, true, true);
        }
        stem.Freeze();
        dc.DrawGeometry(null, Stem, stem);
        // Leaves in pairs up the stem; each stage adds a pair and makes them bigger.
        int pairs = stage switch { 0 => 1, 1 => 2, 2 => 3, _ => 3 };
        double size = stage switch { 0 => 6, 1 => 9, _ => 11 };
        for (int i = 0; i < pairs; i++)
        {
            double t = (i + 1.0) / (pairs + 0.6);
            double y = rimTop + 2 - height * t;
            double s = size * (1 - 0.15 * i);
            DrawLeaf(dc, new Point(Cx - 1, y), -1, s, i % 2 == 0 ? Leaf : LeafLight);
            DrawLeaf(dc, new Point(Cx + 1, y - 3), 1, s, i % 2 == 0 ? LeafLight : Leaf);
        }
        DrawLeaf(dc, top, 0, size * 0.8, Leaf); // The top bud.
        if (stage >= 3)
        {
            DrawFlower(dc, new Point(Cx, top.Y - 4));
            DrawFlower(dc, new Point(Cx - 16, rimTop + 2 - height * 0.62));
            DrawFlower(dc, new Point(Cx + 17, rimTop + 2 - height * 0.42));
        }
        dc.Pop();
        // The pot: soil, a tapering body, a rim.
        double half = DesktopToyRules.PlantHalfWidth;
        var body = new StreamGeometry();
        using (var g = body.Open())
        {
            g.BeginFigure(new Point(Cx - half + 3, rimTop + 8), true, true);
            g.LineTo(new Point(Cx + half - 3, rimTop + 8), true, true);
            g.LineTo(new Point(Cx + half - 9, Bottom - 0.7), true, true);
            g.LineTo(new Point(Cx - half + 9, Bottom - 0.7), true, true);
        }
        body.Freeze();
        dc.DrawGeometry(Clay, Outline, body);
        dc.DrawRectangle(Shade, null, new Rect(Cx + half - 16, rimTop + 9, 6, Bottom - rimTop - 10));
        var rim = new Rect(Cx - half, rimTop, 2 * half, 9);
        dc.DrawEllipse(Soil, null, new Point(Cx, rimTop + 2), half - 4, 3);
        dc.DrawRoundedRectangle(ClayRim, Outline, rim, 3, 3);
        if (Sparkle < 0) return;
        // A new stage: little stars around the leaves, fading out.
        double life = Math.Clamp(Sparkle / SparkleSeconds, 0, 1);
        dc.PushOpacity(1 - life);
        for (int i = 0; i < 4; i++)
        {
            double angle = i * Math.PI / 2 + life * 1.5;
            var c = new Point(Cx + Math.Cos(angle) * (18 + 8 * life), rimTop - height * 0.55 + Math.Sin(angle) * (14 + 6 * life));
            DrawStar(dc, c, 3.5 + 2 * Math.Sin(life * Math.PI));
        }
        dc.Pop();
    }

    // A round leaf from its base, pointing out to one side (0: straight up).
    private static void DrawLeaf(DrawingContext dc, Point at, int side, double size, Brush fill)
    {
        double angle = side == 0 ? -90 : side < 0 ? -160 : -20;
        dc.PushTransform(new RotateTransform(angle, at.X, at.Y));
        var leaf = new StreamGeometry();
        using (var g = leaf.Open())
        {
            g.BeginFigure(at, true, true);
            g.BezierTo(new Point(at.X + size * 0.5, at.Y - size * 0.7), new Point(at.X + size * 1.5, at.Y - size * 0.6), new Point(at.X + size * 1.8, at.Y), true, true);
            g.BezierTo(new Point(at.X + size * 1.5, at.Y + size * 0.6), new Point(at.X + size * 0.5, at.Y + size * 0.7), at, true, true);
        }
        leaf.Freeze();
        dc.DrawGeometry(fill, Outline, leaf);
        dc.DrawLine(Vein, at, new Point(at.X + size * 1.4, at.Y));
        dc.Pop();
    }

    private static void DrawFlower(DrawingContext dc, Point at)
    {
        for (int i = 0; i < 5; i++)
        {
            double a = i * 2 * Math.PI / 5 - Math.PI / 2;
            dc.DrawEllipse(Petal, Outline, new Point(at.X + Math.Cos(a) * 4, at.Y + Math.Sin(a) * 4), 3.3, 3.3);
        }
        dc.DrawEllipse(Heart, Outline, at, 2.6, 2.6);
    }

    private static void DrawStar(DrawingContext dc, Point c, double r)
    {
        var star = new StreamGeometry();
        using (var g = star.Open())
        {
            g.BeginFigure(new Point(c.X, c.Y - r), true, true);
            g.QuadraticBezierTo(c, new Point(c.X + r, c.Y), true, true);
            g.QuadraticBezierTo(c, new Point(c.X, c.Y + r), true, true);
            g.QuadraticBezierTo(c, new Point(c.X - r, c.Y), true, true);
            g.QuadraticBezierTo(c, new Point(c.X, c.Y - r), true, true);
        }
        star.Freeze();
        dc.DrawGeometry(Glint, null, star);
    }
}
