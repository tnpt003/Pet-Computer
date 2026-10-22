using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The display corner's art (Expedition milestone 4): the keepsakes the run gave that are shown
// (a slime cushion, then the cups bronze → gold) standing on a low wooden shelf. Vector only,
// drawn again only when the list or the pose (squash, tilt, the puff and dust effects) changes.
public sealed class TrophyShelf : FrameworkElement
{
    public const double ShelfHeight = 6, Pad = 6, Gap = 4, CupWidth = 26, CupHeight = 36, CushionWidth = 46, CushionHeight = 20;
    public static readonly double ArtHeight = CupHeight + ShelfHeight + 4;

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
    private static readonly Brush Puff = PetUi.Frozen(Color.FromArgb(0xE8, 255, 255, 255)), Dust = PetUi.Frozen(Color.FromArgb(0xA0, 0xE8, 0xDC, 0xCC)), Spark = B(0xFF, 0xD8, 0x4A);
    private static readonly Pen SparkEdge = Freeze(new Pen(B(0xE0, 0x9A, 0x1E), 0.8) { LineJoin = PenLineJoin.Round });
    private static readonly Pen PuffEdge = Freeze(new Pen(PetUi.Frozen(Color.FromArgb(0x90, 0x9C, 0x92, 0xB0)), 1.2));

    // The pose (ShelfMotion's springs), around the bottom center; Pop: the teleport's grow/shrink;
    // Pivot: the held point (unscaled art coordinates) the tilt swings around.
    private double scaleX = 1, scaleY = 1, tilt, pop = 1, shake;
    private Point? pivot;
    // Effects, each a progress 0..1 (negative: none): dust from a landing, the teleport's puff.
    private double dust = -1, dustStrength, puff = -1;
    public void SetPose(double scaleX, double scaleY, double tilt, Point? pivot, double pop, double shake, double dust, double dustStrength, double puff)
    {
        if (this.scaleX == scaleX && this.scaleY == scaleY && this.tilt == tilt && this.pivot == pivot && this.pop == pop && this.shake == shake && this.dust == dust && this.puff == puff) return;
        (this.scaleX, this.scaleY, this.tilt, this.pivot, this.pop, this.shake, this.dust, this.dustStrength, this.puff) = (scaleX, scaleY, tilt, pivot, pop, shake, dust, dustStrength, puff);
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
        if (dust is >= 0 and < 1) DrawDust(dc, w);
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

    // Two little clouds kicked out sideways at the floor, fading.
    private void DrawDust(DrawingContext dc, double w)
    {
        double t = dust, size = 3 + 4 * Math.Clamp(dustStrength, 0, 1);
        dc.PushOpacity(1 - t);
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
            {
                double x = w / 2 + side * (w / 2 - 4 + t * (14 + i * 9)), y = ArtHeight - 2 - t * (3 + i * 3) - i;
                dc.DrawEllipse(Dust, null, new Point(x, y), size * (1 - t * 0.4) - i * 0.8, (size - i) * 0.7);
            }
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
        double floor = ArtHeight - ShelfHeight;
        // The shelf: a plank with a darker front edge.
        dc.DrawRoundedRectangle(Wood, Outline, new Rect(1, floor, w - 2, ShelfHeight - 1), 2.5, 2.5);
        dc.DrawLine(new Pen(WoodDark, 1.2), new Point(4, floor + ShelfHeight - 2.5), new Point(w - 4, floor + ShelfHeight - 2.5));
        double x = Pad;
        foreach (var item in items)
        {
            if (item.Kind == KeepsakeSpec.Cushion) { Cushion(dc, x, floor); x += CushionWidth + Gap; }
            else { Cup(dc, x, floor, item.Tier); x += CupWidth + Gap; }
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
