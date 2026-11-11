using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The cardboard box on the floor (docs/TOY-PLAY.md, M2): a front panel with a strip of tape, the dark
// inside seen over its top edge, and the two side flaps folded open. Static art in the cat's vector
// style; only what is drawn takes clicks. While the cat sleeps inside, the cat's window draws the same
// front panel over the cat (DrawFront), so the cat looks inside and every click still reaches the cat.
public sealed class ToyBoxArt : FrameworkElement
{
    public const double ArtWidth = 136, ArtHeight = 72;

    private static Brush B(byte r, byte g, byte b) => PetUi.Frozen(Color.FromRgb(r, g, b));
    private static Pen P(Brush brush, double width) { var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
    private static readonly Brush Card = B(0xC9, 0x96, 0x5E), Flap = B(0xDB, 0xB0, 0x78), Inside = B(0x6A, 0x46, 0x28),
        InsideBack = B(0x83, 0x5A, 0x34), Tape = B(0xEC, 0xD8, 0xAE), Shade = PetUi.Frozen(Color.FromArgb(0x30, 0x5A, 0x34, 0x14));
    private static readonly Pen Outline = P(PetUi.ArtInk, 1.4), Fold = P(B(0xA8, 0x76, 0x44), 1.2);

    public ToyBoxArt() { Width = ArtWidth; Height = ArtHeight; }

    protected override void OnRender(DrawingContext dc)
    {
        const double c = ArtWidth / 2, top = ArtHeight - DesktopToyRules.BoxHeight, back = top - 8;
        double half = DesktopToyRules.BoxHalfWidth;
        // The flaps first, behind the opening, folded out to each side.
        Quad(dc, Flap, new Point(c - half, top), new Point(c - half + 6, back), new Point(c - half - 10, back - 14), new Point(c - half - 16, top - 12));
        Quad(dc, Flap, new Point(c + half, top), new Point(c + half - 6, back), new Point(c + half + 10, back - 14), new Point(c + half + 16, top - 12));
        // The opening: the far inside wall, then the floor of the box going away from us.
        Quad(dc, InsideBack, new Point(c - half, top), new Point(c + half, top), new Point(c + half - 6, back), new Point(c - half + 6, back));
        dc.DrawRectangle(Inside, null, new Rect(c - half + 3, back + 3, 2 * half - 6, top - back - 3));
        DrawFront(dc, c, ArtHeight);
    }

    private static void Quad(DrawingContext dc, Brush fill, Point a, Point b, Point c, Point d)
    {
        var shape = new StreamGeometry();
        using (var g = shape.Open())
        {
            g.BeginFigure(a, true, true);
            g.LineTo(b, true, true); g.LineTo(c, true, true); g.LineTo(d, true, true);
        }
        shape.Freeze();
        dc.DrawGeometry(fill, Outline, shape);
    }

    // The front panel, centred on centreX with its bottom on the floor (bottom).
    public static void DrawFront(DrawingContext dc, double centreX, double bottom)
    {
        double half = DesktopToyRules.BoxHalfWidth, height = DesktopToyRules.BoxHeight;
        var front = new Rect(centreX - half, bottom - height, 2 * half, height);
        dc.DrawRoundedRectangle(Card, null, front, 2.5, 2.5);
        dc.DrawRectangle(Shade, null, new Rect(front.Left, bottom - 9, front.Width, 9));
        dc.DrawLine(Fold, new Point(front.Left + 4, front.Top + 6), new Point(front.Right - 4, front.Top + 6));
        dc.DrawRectangle(Tape, null, new Rect(centreX - 7, front.Top, 14, 17));
        dc.DrawRoundedRectangle(null, Outline, front, 2.5, 2.5);
    }
}
