using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The toy basket on the floor: woven wicker with a rolled rim, and the yarn ball peeking out
// while it is inside. Static art in the cat's vector style; only what is drawn takes clicks.
public sealed class ToyBasketArt : FrameworkElement
{
    public const double ArtWidth = 68, ArtHeight = 56;

    private static Brush B(byte r, byte g, byte b) => PetUi.Frozen(Color.FromRgb(r, g, b));
    private static Pen P(Brush brush, double width) { var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
    private static readonly Brush Wicker = B(0xD8, 0xA6, 0x68), Strand = B(0xB0, 0x7C, 0x44), Rim = B(0xEC, 0xC6, 0x90), Inside = B(0x7A, 0x52, 0x2E);
    private static readonly Pen Outline = P(PetUi.ArtInk, 1.4), Weave = P(Strand, 1.3);

    private bool hasBall;
    public bool HasBall
    {
        get => hasBall;
        set { if (hasBall == value) return; hasBall = value; InvalidateVisual(); }
    }

    public ToyBasketArt() { Width = ArtWidth; Height = ArtHeight; }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawEllipse(Inside, Outline, new Point(34, 22), 27, 7);
        if (hasBall) YarnBallArt.Draw(dc, new Point(32, 17), 11);

        var body = new StreamGeometry();
        using (var g = body.Open())
        {
            g.BeginFigure(new Point(8, 23), true, true);
            g.LineTo(new Point(60, 23), true, true);
            g.LineTo(new Point(55, 50), true, true);
            g.QuadraticBezierTo(new Point(54, 55), new Point(49, 55), true, true);
            g.LineTo(new Point(19, 55), true, true);
            g.QuadraticBezierTo(new Point(14, 55), new Point(13, 50), true, true);
        }
        body.Freeze();
        dc.DrawGeometry(Wicker, null, body);
        dc.PushClip(body);
        for (int row = 0; row < 4; row++)
        {
            double y = 31 + row * 7;
            dc.DrawLine(Weave, new Point(6, y), new Point(62, y));
            for (double x = 12 + row % 2 * 6; x < 60; x += 12)
                dc.DrawLine(Weave, new Point(x, y - 7), new Point(x, y));
        }
        dc.Pop();
        dc.DrawGeometry(null, Outline, body);
        dc.DrawRoundedRectangle(Rim, Outline, new Rect(5, 22, 58, 8), 4, 4);
    }
}
