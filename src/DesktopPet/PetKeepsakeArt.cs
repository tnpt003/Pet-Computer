using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The display corner's art (Expedition milestone 4): the keepsakes the run gave that are shown
// (a slime cushion, then the cups bronze → gold) standing on a low wooden keepsake. Vector only,
// drawn again only when the list or the pose (squash, tilt, the puff and dust effects) changes.
public sealed class KeepsakeArt : FrameworkElement
{
    public const double StandHeight = 6, Pad = 6, Gap = 4, CupWidth = 26, CupHeight = 36, CushionWidth = 46, CushionHeight = 20;
    public static readonly double ArtHeight = CupHeight + StandHeight + 4;

    private static Brush B(byte r, byte g, byte b) => PetUi.Frozen(Color.FromRgb(r, g, b));
    private static readonly Pen Outline = Freeze(new Pen(B(0x3A, 0x2E, 0x4A), 1.4) { LineJoin = PenLineJoin.Round });
    private static readonly Brush Wood = B(0xA8, 0x72, 0x45), WoodDark = B(0x7A, 0x4E, 0x2C), Shine = PetUi.Frozen(Color.FromArgb(0xB0, 255, 255, 255));
    private static readonly Brush Slime = B(0x7C, 0xD3, 0x6A), SlimeDark = B(0x4F, 0x9E, 0x45), Eye = B(0x30, 0x2B, 0x48), Plinth = B(0x4A, 0x3C, 0x5C);
    private static Pen Freeze(Pen pen) { pen.Freeze(); return pen; }
    private static (Brush Fill, Brush Dark) CupColors(string? tier) => tier switch
    {
        "gold" => (B(0xF5, 0xC5, 0x42), B(0xB8, 0x86, 0x2A)),
        "silver" => (B(0xD6, 0xDC, 0xE4), B(0x8C, 0x96, 0xA3)),
        _ => (B(0xD0, 0x8A, 0x45), B(0x8E, 0x55, 0x23))
    };

    private IReadOnlyList<KeepsakeSpec> items = Array.Empty<KeepsakeSpec>();
    public IReadOnlyList<KeepsakeSpec> Items
    {
        get => items;
        set { items = value; Width = WidthFor(value) * Scale + Side * 2; Height = ArtHeight * Scale + TopRoom + BottomRoom; InvalidateVisual(); }
    }

    // On the desktop: transparent room around the art (sides, above, and below for the tilt while
    // held) for the squash, the tilt and the effects. The Rewards cell has none.
    public double Side { get; init; }
    public double TopRoom { get; init; }
    public double BottomRoom { get; init; }
    // Drawn this many times its natural size (the desktop's one keepsake is bigger; the Rewards cell 1).
    public double Scale { get; init; } = 1;
    private static readonly Brush Puff = PetUi.Frozen(Color.FromArgb(0xE8, 255, 255, 255)), Spark = B(0xFF, 0xD8, 0x4A);
    private static readonly Pen SparkEdge = Freeze(new Pen(B(0xE0, 0x9A, 0x1E), 0.8) { LineJoin = PenLineJoin.Round });
    private static readonly Pen PuffEdge = Freeze(new Pen(PetUi.Frozen(Color.FromArgb(0x90, 0x9C, 0x92, 0xB0)), 1.2));

    // The pose (KeepsakeMotion's springs), around the bottom center; Pop: the teleport's grow/shrink;
    // Pivot: the held point (unscaled art coordinates) the tilt swings around.
    private double scaleX = 1, scaleY = 1, tilt, pop = 1, shake;
    private Point? pivot;
    // Effects: the landing's dust (shared with the cat, PetImpact.cs), the teleport's puff (progress
    // 0..1, negative: none).
    private Landing? landing;
    private double dust = -1, puff = -1;
    public void SetPose(double scaleX, double scaleY, double tilt, Point? pivot, double pop, double shake, Landing? landing, double puff)
    {
        double dust = landing?.Dust ?? -1;
        if (this.scaleX == scaleX && this.scaleY == scaleY && this.tilt == tilt && this.pivot == pivot && this.pop == pop && this.shake == shake && this.dust == dust && this.puff == puff) return;
        (this.scaleX, this.scaleY, this.tilt, this.pivot, this.pop, this.shake, this.landing, this.dust, this.puff) = (scaleX, scaleY, tilt, pivot, pop, shake, landing, dust, puff);
        InvalidateVisual();
    }

    public static double WidthFor(IReadOnlyList<KeepsakeSpec> shown)
    {
        double width = Pad * 2;
        for (int i = 0; i < shown.Count; i++) width += (shown[i].Kind == KeepsakeSpec.Cushion ? CushionWidth : CupWidth) + (i > 0 ? Gap : 0);
        return width;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (items.Count == 0) return;
        double w = WidthFor(items);
        dc.PushTransform(new TranslateTransform(Side, TopRoom));
        dc.PushTransform(new ScaleTransform(Scale, Scale));
        if (landing != null) LandingDust.Draw(dc, landing, w / 2, ArtHeight, w / 2);
        if (pop > 0.01)
        {
            dc.PushTransform(new TranslateTransform(shake, 0));
            if (tilt != 0) dc.PushTransform(new RotateTransform(tilt, pivot?.X ?? w / 2, pivot?.Y ?? ArtHeight));
            dc.PushTransform(new ScaleTransform(scaleX * pop, scaleY * pop, w / 2, ArtHeight));
            DrawArt(dc, w);
            dc.Pop();
            if (tilt != 0) dc.Pop();
            dc.Pop();
        }
        if (puff is >= 0 and < 1) DrawPuff(dc, w);
        dc.Pop();
        dc.Pop();
    }

    // The teleport's "poof": a ring of cloud puffs swelling out and fading, with gold sparkles.
    private void DrawPuff(DrawingContext dc, double w)
    {
        double t = puff, cx = w / 2, cy = ArtHeight * 0.55;
        dc.PushOpacity(t < 0.6 ? 1 : 1 - (t - 0.6) / 0.4);
        double ring = 10 + t * (w * 0.45 + 18);
        for (int i = 0; i < 9; i++)
        {
            double a = i * Math.PI * 2 / 9 + t * 0.8, r = (13 - t * 6) * (i % 2 == 0 ? 1 : 0.8);
            dc.DrawEllipse(Puff, PuffEdge, new Point(cx + Math.Cos(a) * ring, cy + Math.Sin(a) * ring * 0.55), r, r * 0.85);
        }
        dc.DrawEllipse(Puff, null, new Point(cx, cy), 16 * (1 - t), 12 * (1 - t));
        for (int i = 0; i < 5; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI * 2 / 5 + 0.3, d = 8 + t * (w * 0.5 + 26);
            SparkleAt(dc, new Point(cx + Math.Cos(a) * d, cy + Math.Sin(a) * d * 0.7 - t * 10), 7 * (1 - t * 0.4));
        }
        dc.Pop();
    }

    private static void SparkleAt(DrawingContext dc, Point c, double r)
    {
        var g = new StreamGeometry();
        using (var s = g.Open())
        {
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4, rr = i % 2 == 0 ? r : r * 0.3;
                var p = new Point(c.X + Math.Cos(a) * rr, c.Y + Math.Sin(a) * rr);
                if (i == 0) s.BeginFigure(p, true, true); else s.LineTo(p, true, true);
            }
        }
        g.Freeze();
        dc.DrawGeometry(Spark, SparkEdge, g);
    }

    private void DrawArt(DrawingContext dc, double w)
    {
        double floor = ArtHeight - StandHeight;
        // The keepsake: a plank with a darker front edge.
        dc.DrawRoundedRectangle(Wood, Outline, new Rect(1, floor, w - 2, StandHeight - 1), 2.5, 2.5);
        dc.DrawLine(new Pen(WoodDark, 1.2), new Point(4, floor + StandHeight - 2.5), new Point(w - 4, floor + StandHeight - 2.5));
        double x = Pad;
        foreach (var item in items)
        {
            if (item.Kind == KeepsakeSpec.Cushion) { Cushion(dc, x, floor); x += CushionWidth + Gap; continue; }
            if (item.Kind == KeepsakeSpec.Hourglass) Hourglass(dc, x, floor);
            else if (item.Kind == KeepsakeSpec.Orb) Orb(dc, x, floor);
            else Cup(dc, x, floor, item.Tier);
            x += CupWidth + Gap;
        }
    }

    private static void Cushion(DrawingContext dc, double left, double floor)
    {
        double cx = left + CushionWidth / 2, top = floor - CushionHeight;
        var body = new StreamGeometry();
        using (var g = body.Open())
        {
            g.BeginFigure(new Point(left + 2, floor), true, true);
            g.BezierTo(new Point(left, top + 4), new Point(cx - 10, top - 2), new Point(cx, top + 1), true, true);
            g.BezierTo(new Point(cx + 10, top - 2), new Point(left + CushionWidth, top + 4), new Point(left + CushionWidth - 2, floor), true, true);
        }
        body.Freeze();
        dc.DrawGeometry(Slime, Outline, body);
        dc.DrawLine(new Pen(SlimeDark, 1.6), new Point(left + 6, floor - 3), new Point(left + CushionWidth - 6, floor - 3));
        dc.DrawEllipse(Shine, null, new Point(cx - 11, top + 6), 4, 2.2);
        dc.DrawEllipse(Eye, null, new Point(cx - 4.5, top + 10), 1.6, 2.2);
        dc.DrawEllipse(Eye, null, new Point(cx + 4.5, top + 10), 1.6, 2.2);
    }

    // Infinity's keepsakes (2026-10-22), in a cup's room (CupWidth × CupHeight).
    private static readonly Brush Glass = PetUi.Frozen(Color.FromArgb(0x70, 0xDC, 0xEE, 0xFF)), Sand = B(0xB9, 0x8C, 0xF0), SandDark = B(0x7A, 0x5A, 0xC8),
        Gold = B(0xF5, 0xC5, 0x42), GoldDark = B(0xB8, 0x86, 0x2A), Star = B(0xFF, 0xF6, 0xD8);
    private static readonly Brush Nebula = Freeze(new RadialGradientBrush(
        new GradientStopCollection { new(Color.FromRgb(0xF2, 0x9C, 0xE8), 0), new(Color.FromRgb(0x8A, 0x5C, 0xE0), 0.45), new(Color.FromRgb(0x2C, 0x2A, 0x6E), 1) })
        { GradientOrigin = new Point(0.38, 0.35), Center = new Point(0.45, 0.42), RadiusX = 0.65, RadiusY = 0.65 });
    private static Brush Freeze(Brush brush) { brush.Freeze(); return brush; }

    // An hourglass of violet "star sand": wooden caps, two glass bulbs, sand in both and a thin
    // falling stream, a ∞ on the top cap.
    private static void Hourglass(DrawingContext dc, double left, double floor)
    {
        double cx = left + CupWidth / 2, top = floor - CupHeight, neck = floor - CupHeight / 2;
        var bulbs = new StreamGeometry();
        using (var g = bulbs.Open())
        {
            g.BeginFigure(new Point(cx - 8.5, top + 4), true, true);
            g.LineTo(new Point(cx + 8.5, top + 4), true, true);
            g.BezierTo(new Point(cx + 9, neck - 6), new Point(cx + 1.5, neck - 2), new Point(cx + 1.5, neck), true, true);
            g.BezierTo(new Point(cx + 1.5, neck + 2), new Point(cx + 9, neck + 6), new Point(cx + 8.5, floor - 4), true, true);
            g.LineTo(new Point(cx - 8.5, floor - 4), true, true);
            g.BezierTo(new Point(cx - 9, neck + 6), new Point(cx - 1.5, neck + 2), new Point(cx - 1.5, neck), true, true);
            g.BezierTo(new Point(cx - 1.5, neck - 2), new Point(cx - 9, neck - 6), new Point(cx - 8.5, top + 4), true, true);
        }
        bulbs.Freeze();
        dc.DrawGeometry(Glass, null, bulbs);
        // Sand: a little left above, a heap below, the stream between.
        var upper = new StreamGeometry();
        using (var g = upper.Open())
        {
            g.BeginFigure(new Point(cx - 5.5, neck - 6), true, true);
            g.QuadraticBezierTo(new Point(cx, neck - 8), new Point(cx + 5.5, neck - 6), true, true);
            g.QuadraticBezierTo(new Point(cx + 2, neck - 2.5), new Point(cx, neck - 0.5), true, true);
            g.QuadraticBezierTo(new Point(cx - 2, neck - 2.5), new Point(cx - 5.5, neck - 6), true, true);
        }
        upper.Freeze();
        dc.DrawGeometry(Sand, null, upper);
        var lower = new StreamGeometry();
        using (var g = lower.Open())
        {
            g.BeginFigure(new Point(cx - 8, floor - 4.5), true, true);
            g.QuadraticBezierTo(new Point(cx - 4, floor - 11), new Point(cx, floor - 12), true, true);
            g.QuadraticBezierTo(new Point(cx + 4, floor - 11), new Point(cx + 8, floor - 4.5), true, true);
        }
        lower.Freeze();
        dc.DrawGeometry(Sand, null, lower);
        dc.DrawLine(new Pen(SandDark, 1), new Point(cx, neck), new Point(cx, floor - 12));
        dc.DrawEllipse(Star, null, new Point(cx - 2.5, floor - 7.5), 0.9, 0.9);
        dc.DrawEllipse(Star, null, new Point(cx + 3, floor - 6.5), 0.7, 0.7);
        dc.DrawGeometry(null, Outline, bulbs);
        dc.DrawEllipse(Shine, null, new Point(cx - 5, top + 9), 1.3, 2.6);
        // Caps and posts.
        dc.DrawRoundedRectangle(Wood, Outline, new Rect(cx - 11, top, 22, 4.5), 1.5, 1.5);
        dc.DrawRoundedRectangle(Wood, Outline, new Rect(cx - 11, floor - 4.5, 22, 4.5), 1.5, 1.5);
        dc.DrawLine(new Pen(WoodDark, 1.6), new Point(cx - 10, top + 4.5), new Point(cx - 10, floor - 4.5));
        dc.DrawLine(new Pen(WoodDark, 1.6), new Point(cx + 10, top + 4.5), new Point(cx + 10, floor - 4.5));
        Infinity(dc, cx, top + 2.25, 2.1, new Pen(Gold, 1.1));
    }

    // A nebula in a glass ball on a little gold claw stand, with a few stars inside.
    private static void Orb(DrawingContext dc, double left, double floor)
    {
        double cx = left + CupWidth / 2, r = 11, cy = floor - 8 - r;
        var stand = new StreamGeometry();
        using (var g = stand.Open())
        {
            g.BeginFigure(new Point(cx - 9, floor), true, true);
            g.LineTo(new Point(cx - 6, floor - 9), true, true);
            g.LineTo(new Point(cx + 6, floor - 9), true, true);
            g.LineTo(new Point(cx + 9, floor), true, true);
        }
        stand.Freeze();
        dc.DrawGeometry(Gold, Outline, stand);
        dc.DrawLine(new Pen(GoldDark, 1.2), new Point(cx - 7.5, floor - 3), new Point(cx + 7.5, floor - 3));
        dc.DrawEllipse(Nebula, Outline, new Point(cx, cy), r, r);
        // The swirl and the stars.
        var swirl = new StreamGeometry();
        using (var g = swirl.Open())
        {
            g.BeginFigure(new Point(cx - 7, cy + 2), false, false);
            g.BezierTo(new Point(cx - 4, cy - 6), new Point(cx + 6, cy - 4), new Point(cx + 3, cy + 2), true, true);
            g.BezierTo(new Point(cx + 1, cy + 5), new Point(cx - 3, cy + 2), new Point(cx - 1, cy), true, true);
        }
        swirl.Freeze();
        dc.DrawGeometry(null, new Pen(PetUi.Frozen(Color.FromArgb(0xB0, 0xFF, 0xE6, 0xFA)), 1.3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, swirl);
        SparkleAt(dc, new Point(cx + 5, cy - 5), 2.4);
        dc.DrawEllipse(Star, null, new Point(cx - 5, cy + 5), 0.9, 0.9);
        dc.DrawEllipse(Star, null, new Point(cx + 6, cy + 4.5), 0.7, 0.7);
        dc.DrawEllipse(Shine, null, new Point(cx - 5.5, cy - 5.5), 2.2, 3.2);
        // Claws holding the ball.
        dc.DrawLine(new Pen(Gold, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, new Point(cx - 6, floor - 9), new Point(cx - 8.5, cy + 6));
        dc.DrawLine(new Pen(Gold, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, new Point(cx + 6, floor - 9), new Point(cx + 8.5, cy + 6));
    }

    // ∞: two loops side by side around (cx, cy), each r wide.
    private static void Infinity(DrawingContext dc, double cx, double cy, double r, Pen pen)
    {
        var g = new StreamGeometry();
        using (var s = g.Open())
        {
            s.BeginFigure(new Point(cx, cy), false, true);
            s.BezierTo(new Point(cx + r * 0.6, cy - r * 0.9), new Point(cx + r * 2, cy - r * 0.9), new Point(cx + r * 2, cy), true, true);
            s.BezierTo(new Point(cx + r * 2, cy + r * 0.9), new Point(cx + r * 0.6, cy + r * 0.9), new Point(cx, cy), true, true);
            s.BezierTo(new Point(cx - r * 0.6, cy - r * 0.9), new Point(cx - r * 2, cy - r * 0.9), new Point(cx - r * 2, cy), true, true);
            s.BezierTo(new Point(cx - r * 2, cy + r * 0.9), new Point(cx - r * 0.6, cy + r * 0.9), new Point(cx, cy), true, true);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }

    private static void Cup(DrawingContext dc, double left, double floor, string? tier)
    {
        var (fill, dark) = CupColors(tier);
        double cx = left + CupWidth / 2;
        // Plinth, stem, bowl, handles, a star on the bowl and a shine.
        dc.DrawRoundedRectangle(Plinth, Outline, new Rect(cx - 9, floor - 6, 18, 6), 1.5, 1.5);
        dc.DrawRectangle(dark, Outline, new Rect(cx - 2.5, floor - 14, 5, 8));
        dc.DrawRoundedRectangle(fill, Outline, new Rect(cx - 6.5, floor - 16, 13, 3), 1, 1);
        var handle = new Pen(dark, 2.2);
        dc.DrawEllipse(null, handle, new Point(cx - 9.5, floor - 27), 3.5, 4.5);
        dc.DrawEllipse(null, handle, new Point(cx + 9.5, floor - 27), 3.5, 4.5);
        var bowl = new StreamGeometry();
        using (var g = bowl.Open())
        {
            g.BeginFigure(new Point(cx - 10, floor - CupHeight + 2), true, true);
            g.LineTo(new Point(cx + 10, floor - CupHeight + 2), true, true);
            g.BezierTo(new Point(cx + 10, floor - 20), new Point(cx + 5, floor - 16), new Point(cx, floor - 16), true, true);
            g.BezierTo(new Point(cx - 5, floor - 16), new Point(cx - 10, floor - 20), new Point(cx - 10, floor - CupHeight + 2), true, true);
        }
        bowl.Freeze();
        dc.DrawGeometry(fill, Outline, bowl);
        var star = new StreamGeometry();
        using (var g = star.Open())
        {
            double sy = floor - 25.5, r = 3.6, inner = 1.5;
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 == 0 ? r : inner;
                var p = new Point(cx + Math.Cos(a) * rr, sy + Math.Sin(a) * rr);
                if (i == 0) g.BeginFigure(p, true, true); else g.LineTo(p, true, true);
            }
        }
        star.Freeze();
        dc.DrawGeometry(dark, null, star);
        dc.DrawEllipse(Shine, null, new Point(cx - 5.5, floor - CupHeight + 7), 1.6, 3.2);
    }
}
