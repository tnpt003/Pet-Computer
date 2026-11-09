using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The neighbour's cat, drawn by code: a grey tabby sitting side on, its tail going.
// Another cat's own colours, so nobody takes it for the player's cat.
public sealed class PetFarmVisitorArt : FrameworkElement
{
    public const double ArtWidth = 130, ArtHeight = 118, Floor = ArtHeight - PetFarmArt.BottomRoom;

    private static readonly Brush Coat = PetUi.Hex("#9AA5B1"), Belly = PetUi.Hex("#E5EAF0"), Stripe = PetUi.Hex("#6C7682"), Nose = PetUi.Hex("#E79AA6"), Shade = PetUi.Frozen(Color.FromArgb(0x58, 0x2A, 0x1F, 0x16));
    private static readonly Pen Ink = PetUi.FrozenPen(PetUi.ArtInk, 2.4), InkThin = PetUi.FrozenPen(PetUi.ArtInk, 1.4), TailInk = PetUi.FrozenPen(PetUi.ArtInk, 11), TailFur = PetUi.FrozenPen(Coat, 7), StripePen = PetUi.FrozenPen(Stripe, 2.6);
    private static readonly Geometry EarShape = Parse("M-11,0 L-7,-15 L3,-4 Z");

    private static Geometry Parse(string text) { var geometry = Geometry.Parse(text); geometry.Freeze(); return geometry; }

    // Seconds, for the tail.
    public double Time { get; set; }

    public PetFarmVisitorArt() { Width = ArtWidth; Height = ArtHeight; }

    protected override void OnRender(DrawingContext dc)
    {
        double y = Floor, mid = ArtWidth / 2;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(mid - 34, y - 72, 68, 72)); // The whole cat takes the click.
        dc.DrawEllipse(Shade, null, new Point(mid, y + 1), 30, 3.5);
        double wag = Math.Sin(Time * 3) * 7;
        var from = new Point(mid + 16, y - 10);
        var to = new Point(mid + 36 + wag * 0.4, y - 34 - wag * 0.2);
        dc.DrawLine(TailInk, from, to);
        dc.DrawLine(TailFur, from, to);
        dc.DrawEllipse(Coat, Ink, new Point(mid, y - 19), 21, 19);
        dc.DrawEllipse(Belly, null, new Point(mid - 8, y - 16), 10, 13);
        foreach (double offset in new[] { -3.0, 7.0, 16.0 }) dc.DrawLine(StripePen, new Point(mid + offset, y - 35), new Point(mid + offset + 2, y - 24));
        dc.DrawEllipse(Coat, Ink, new Point(mid - 11, y - 2), 7.5, 4);
        dc.DrawEllipse(Coat, Ink, new Point(mid + 7, y - 2), 7.5, 4);
        double head = y - 51;
        foreach (int side in new[] { -1, 1 })
        {
            dc.PushTransform(new TranslateTransform(mid + side * 13, head - 13));
            dc.PushTransform(new ScaleTransform(side, 1));
            dc.DrawGeometry(Coat, Ink, EarShape);
            dc.Pop(); dc.Pop();
        }
        dc.DrawEllipse(Coat, Ink, new Point(mid, head), 20, 17);
        dc.DrawLine(StripePen, new Point(mid - 5, head - 15), new Point(mid - 4, head - 9));
        dc.DrawLine(StripePen, new Point(mid, head - 16), new Point(mid, head - 9));
        dc.DrawLine(StripePen, new Point(mid + 5, head - 15), new Point(mid + 4, head - 9));
        dc.DrawEllipse(Belly, null, new Point(mid, head + 7), 9, 6);
        foreach (int side in new[] { -1, 1 }) dc.DrawEllipse(PetUi.ArtInk, null, new Point(mid + side * 8, head - 2), 2.4, 3);
        dc.DrawEllipse(Nose, InkThin, new Point(mid, head + 4), 2.2, 1.7);
        foreach (int side in new[] { -1, 1 })
        {
            dc.DrawLine(InkThin, new Point(mid + side * 12, head + 5), new Point(mid + side * 24, head + 2));
            dc.DrawLine(InkThin, new Point(mid + side * 12, head + 8), new Point(mid + side * 24, head + 9));
        }
    }
}
