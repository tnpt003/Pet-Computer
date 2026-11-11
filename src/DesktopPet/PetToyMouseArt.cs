using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The wind-up mouse (docs/TOY-PLAY.md, M4): a grey tin mouse on two little wheels with a brass key on its
// back that turns while it runs, in the cat's vector style. Facing is where its nose points; turned over
// after a hard landing it lies on its back with its wheels spinning. Only what is drawn takes clicks.
public sealed class ToyMouseArt : FrameworkElement
{
    public const double ArtWidth = 56, ArtHeight = 36;
    private const double Cx = ArtWidth / 2, Cy = ArtHeight - 11;

    private static Brush B(byte r, byte g, byte b) => PetUi.Frozen(Color.FromRgb(r, g, b));
    private static Pen P(Brush brush, double width) { var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
    private static readonly Brush Body = B(0xA8, 0xA4, 0xB4), Belly = B(0xC9, 0xC6, 0xD2), Ear = B(0xF2, 0xB8, 0xC6), Nose = B(0xE8, 0x7A, 0xB8),
        Brass = B(0xE0, 0xB4, 0x4C), Wheel = B(0x5A, 0x50, 0x4C), Hit = PetUi.Frozen(Color.FromArgb(1, 0, 0, 0));
    private static readonly Pen Outline = P(PetUi.ArtInk, 1.4), Tail = P(B(0xE8, 0x9A, 0xB0), 1.6), Spoke = P(B(0xC8, 0xC0, 0xB8), 1);

    public int Facing = 1;
    public bool Flipped;
    // The key's and the wheels' turn, in degrees.
    public double Turn;

    public ToyMouseArt() { Width = ArtWidth; Height = ArtHeight; }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawEllipse(Hit, null, new Point(Cx, Cy - 2), 24, 14);
        dc.PushTransform(new ScaleTransform(Facing < 0 ? -1 : 1, Flipped ? -1 : 1, Cx, Cy - 2));
        // The tail, curling up behind (to the left: the nose points right before the flip).
        var tail = new StreamGeometry();
        using (var g = tail.Open())
        {
            g.BeginFigure(new Point(Cx - 15, Cy), false, false);
            g.BezierTo(new Point(Cx - 24, Cy + 2), new Point(Cx - 26, Cy - 10), new Point(Cx - 20, Cy - 12), true, true);
        }
        tail.Freeze();
        dc.DrawGeometry(null, Tail, tail);
        // Wheels under the body.
        foreach (double wx in new[] { Cx - 8, Cx + 8 })
        {
            var hub = new Point(wx, Cy + 7);
            dc.DrawEllipse(Wheel, Outline, hub, 3.4, 3.4);
            double a = Turn * Math.PI / 180;
            dc.DrawLine(Spoke, hub + new Vector(Math.Cos(a) * 2.4, Math.Sin(a) * 2.4), hub - new Vector(Math.Cos(a) * 2.4, Math.Sin(a) * 2.4));
        }
        // The body: an egg, pointed at the nose.
        var body = new StreamGeometry();
        using (var g = body.Open())
        {
            g.BeginFigure(new Point(Cx + 19, Cy + 2), true, true);
            g.BezierTo(new Point(Cx + 10, Cy - 12), new Point(Cx - 14, Cy - 13), new Point(Cx - 16, Cy + 1), true, true);
            g.BezierTo(new Point(Cx - 15, Cy + 7), new Point(Cx + 8, Cy + 8), new Point(Cx + 19, Cy + 2), true, true);
        }
        body.Freeze();
        dc.DrawGeometry(Body, Outline, body);
        dc.PushClip(body);
        dc.DrawEllipse(Belly, null, new Point(Cx, Cy + 7), 16, 4.5);
        dc.Pop();
        dc.DrawEllipse(Ear, Outline, new Point(Cx + 7, Cy - 8), 4.6, 4.6);
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(Cx + 12, Cy - 3), 1.4, 1.4);
        dc.DrawEllipse(Nose, Outline, new Point(Cx + 19.5, Cy + 1.5), 2, 2);
        // The wind-up key on its back, turning (seen edge-on, so it narrows as it turns).
        if (!Flipped)
        {
            var stem = new Point(Cx - 4, Cy - 11);
            dc.DrawLine(Outline, stem, stem + new Vector(0, -4));
            double wide = 2 + 4.5 * Math.Abs(Math.Cos(Turn * Math.PI / 180));
            dc.DrawEllipse(Brass, Outline, stem + new Vector(-wide / 2 - 1, -6), wide / 2 + 0.6, 3.2);
            dc.DrawEllipse(Brass, Outline, stem + new Vector(wide / 2 + 1, -6), wide / 2 + 0.6, 3.2);
        }
        dc.Pop();
    }
}
