using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The display corner's art (Expedition milestone 4): the keepsakes the run gave that are shown
// (a slime cushion, then the cups bronze → gold) standing on a low wooden shelf. Vector only,
// drawn again only when the list changes.
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
        set { items = value; Width = WidthFor(value); Height = ArtHeight; InvalidateVisual(); }
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
        double w = WidthFor(items), floor = ArtHeight - ShelfHeight;
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
