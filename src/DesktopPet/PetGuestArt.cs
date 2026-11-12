using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// Visitors (docs/MINI-GAMES.md → M4): the guest's own small window, moved with it, under the cat's. A paper
// butterfly (folded cream paper wings tinted Rose and Lilac, crease lines, an ink body and antennae) or a gentle
// slime (a soft Mint dome with a leaf sprout, blush and a calm face; rounder and leafier than the battle's
// slimes, no eyes of a fighter). Both outlined in ArtInk so they read on a light or dark desktop. A faint disc
// behind the guest takes the click; the rest of the window is fully transparent and clicks pass through.
public sealed class GuestArt : FrameworkElement
{
    public const double ButterflyWidth = 76, ButterflyHeight = 68, SlimeWidth = 72, SlimeHeight = 64, SlimeBase = 8;
    private static Brush B(byte a, byte r, byte g, byte b) => PetUi.Frozen(Color.FromArgb(a, r, g, b));
    private static readonly Brush Hit = B(0x02, 0xFF, 0xFF, 0xFF), UpperWing = B(0xFF, 0xFB, 0xD9, 0xD3), LowerWing = B(0xFF, 0xE4, 0xD6, 0xF2),
        Paper = B(0xFF, 0xFF, 0xF6, 0xE8), SlimeBody = B(0xFF, 0xC6, 0xEA, 0xD1), SlimeShine = B(0xC8, 0xFF, 0xFF, 0xFF),
        Blush = B(0xB0, 0xF3, 0xA1, 0xA0), Leaf = B(0xFF, 0x7F, 0xC0, 0x8E), Shadow = PetUi.Frozen(Color.FromArgb(0x38, PetUi.ShadeColor.R, PetUi.ShadeColor.G, PetUi.ShadeColor.B)),
        Spark = B(0xFF, 0xFF, 0xD8, 0x6A);
    private static readonly Pen Outline = PetUi.FrozenPen(PetUi.ArtInk, 1.8), Thin = PetUi.FrozenPen(PetUi.ArtInk, 1.2),
        Crease = PetUi.FrozenPen(B(0x70, 0x3A, 0x2C, 0x1F), 0.8), Face = Round(PetUi.FrozenPen(PetUi.ArtInk, 1.4)),
        SparkPen = PetUi.FrozenPen(B(0xFF, 0xFF, 0xC8, 0x50), 1.8);
    private static readonly Geometry UpperShape = Wing(new Point(0, -2), new Point(-25, -24), new Point(-29, -6), new Point(-4, 2)),
        LowerShape = Wing(new Point(-1, 1), new Point(-22, 4), new Point(-16, 19), new Point(-3, 8));

    private static Pen Round(Pen pen) { var p = pen.Clone(); p.StartLineCap = p.EndLineCap = PenLineCap.Round; p.Freeze(); return p; }

    // A folded paper wing on the left of the body (mirrored for the right): four corners, gently rounded.
    private static Geometry Wing(Point root, Point tip, Point outer, Point back)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(root, true, true);
            c.QuadraticBezierTo(new Point((root.X + tip.X) / 2 - 2, (root.Y + tip.Y) / 2 - 3), tip, true, true);
            c.QuadraticBezierTo(new Point(outer.X - 3, (tip.Y + outer.Y) / 2), outer, true, true);
            c.QuadraticBezierTo(new Point((outer.X + back.X) / 2, (outer.Y + back.Y) / 2 + 3), back, true, true);
        }
        g.Freeze();
        return g;
    }

    private readonly Guest guest;
    public GuestArt(Guest guest)
    {
        this.guest = guest;
        Width = guest.Kind == GuestKind.Butterfly ? ButterflyWidth : SlimeWidth;
        Height = guest.Kind == GuestKind.Butterfly ? ButterflyHeight : SlimeHeight;
    }

    private double zoomedAt = -1;
    public void Zoomed() => zoomedAt = guest.Time;

    protected override void OnRender(DrawingContext dc)
    {
        if (!guest.Active) return;
        dc.PushOpacity(guest.Fade);
        if (guest.Kind == GuestKind.Butterfly)
        {
            var at = new Point(ButterflyWidth / 2, ButterflyHeight / 2);
            if (!guest.Zooming) dc.DrawEllipse(Hit, null, at, 24, 22);
            if (zoomedAt >= 0) DrawSparks(dc, at, guest.Time - zoomedAt, guest.Direction);
            DrawButterfly(dc, at, guest.Direction, guest.Time * (guest.Zooming ? 26 : 13), guest.Zooming ? 14 : 4 * Math.Sin(guest.Time * 1.8));
        }
        else
        {
            var at = new Point(SlimeWidth / 2, SlimeHeight - SlimeBase);
            dc.DrawEllipse(Hit, null, new Point(at.X, at.Y - 16), 24, 22);
            double hop = 0, squash = 0;
            if (guest.Hop >= 0) { double f = guest.Hop / GuestRules.HopSeconds; hop = 14 * Math.Sin(Math.PI * f); squash = f > 0.85 ? 0.12 : -0.08 * Math.Sin(Math.PI * f); }
            else if (guest.Phase == GuestPhase.Greeting)
            {
                double f = guest.PhaseTime % 0.75 / 0.75;
                hop = 9 * Math.Sin(Math.PI * f);
                squash = f < 0.12 || f > 0.88 ? 0.1 : -0.05;
            }
            else squash = 0.05 * Math.Sin(guest.Time * 9); // Crawling: a gentle wobble.
            DrawSlime(dc, at, guest.Direction, hop, squash, guest.Phase == GuestPhase.Greeting || guest.Hop >= 0);
        }
        dc.Pop();
    }

    // The paper butterfly at its middle; flap: the wings' phase; tilt: degrees, leaning into the way it goes.
    public static void DrawButterfly(DrawingContext dc, Point at, int direction, double flap, double tilt)
    {
        dc.PushTransform(new TranslateTransform(at.X, at.Y));
        dc.PushTransform(new RotateTransform(direction * (8 + tilt)));
        double open = 0.3 + 0.7 * Math.Abs(Math.Cos(flap));
        foreach (int side in new[] { -1, 1 })
        {
            dc.PushTransform(new ScaleTransform(-side * open, 1));
            dc.DrawGeometry(LowerWing, Outline, LowerShape);
            dc.DrawGeometry(UpperWing, Outline, UpperShape);
            // The folds of the paper: a crease from the body to each wing's corner, a paler inner fold.
            dc.DrawLine(Crease, new Point(-1, -1), new Point(-27, -15));
            dc.DrawLine(Crease, new Point(-2, 3), new Point(-18, 12));
            dc.PushOpacity(0.55);
            dc.DrawGeometry(Paper, null, Wing(new Point(-3, -1), new Point(-15, -13), new Point(-18, -6), new Point(-5, 0)));
            dc.Pop();
            dc.Pop();
        }
        // Body, head and the two antennae with round tips.
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(0, 2), 2.6, 9);
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(0, -8), 3.2, 3.2);
        dc.DrawLine(Thin, new Point(-1, -10), new Point(-6, -19));
        dc.DrawLine(Thin, new Point(1, -10), new Point(6, -19));
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(-6, -19), 1.5, 1.5);
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(6, -19), 1.5, 1.5);
        dc.Pop(); dc.Pop();
    }

    // The gentle slime standing on (at.X, at.Y): hop lifts it, squash flattens (+) or stretches (−) it; happy
    // shows the closed smiling eyes of its hello.
    public static void DrawSlime(DrawingContext dc, Point at, int direction, double hop, double squash, bool happy)
    {
        double shadow = Math.Max(0.45, 1 - hop / 24);
        dc.DrawEllipse(Shadow, null, new Point(at.X, at.Y + 1), 17 * shadow, 3.5 * shadow);
        dc.PushTransform(new TranslateTransform(at.X, at.Y - hop));
        dc.PushTransform(new ScaleTransform(direction * (1 + squash), 1 - squash));
        var body = new StreamGeometry();
        using (var c = body.Open())
        {
            c.BeginFigure(new Point(-18, 0), true, true);
            c.BezierTo(new Point(-20, -16), new Point(-10, -29), new Point(1, -29), true, true);
            c.BezierTo(new Point(12, -29), new Point(20, -16), new Point(18, 0), true, true);
            c.QuadraticBezierTo(new Point(0, 3), new Point(-18, 0), true, true);
        }
        body.Freeze();
        dc.DrawGeometry(SlimeBody, Outline, body);
        dc.DrawEllipse(SlimeShine, null, new Point(-8, -20), 4, 2.6);
        // The leaf sprout on top, leaning back.
        dc.DrawLine(Thin, new Point(1, -29), new Point(-1, -34));
        var leaf = new StreamGeometry();
        using (var c = leaf.Open())
        {
            c.BeginFigure(new Point(-1, -34), true, true);
            c.QuadraticBezierTo(new Point(-9, -41), new Point(-13, -35), true, true);
            c.QuadraticBezierTo(new Point(-7, -31), new Point(-1, -34), true, true);
        }
        leaf.Freeze();
        dc.DrawGeometry(Leaf, Thin, leaf);
        // The face, looking the way it goes.
        if (happy)
        {
            dc.DrawLine(Face, new Point(1, -14), new Point(4, -16)); dc.DrawLine(Face, new Point(4, -16), new Point(7, -14));
            dc.DrawLine(Face, new Point(10, -14), new Point(12.5, -16)); dc.DrawLine(Face, new Point(12.5, -16), new Point(15, -14));
        }
        else
        {
            dc.DrawEllipse(PetUi.ArtInk, null, new Point(4, -14), 1.8, 2.3);
            dc.DrawEllipse(PetUi.ArtInk, null, new Point(12, -14), 1.8, 2.3);
        }
        dc.DrawEllipse(Blush, null, new Point(1, -9), 2.6, 1.6);
        dc.DrawEllipse(Blush, null, new Point(15, -9), 2.2, 1.5);
        dc.DrawLine(Face, new Point(7, -9.5), new Point(9, -8.5)); dc.DrawLine(Face, new Point(9, -8.5), new Point(10.5, -9.5));
        dc.Pop(); dc.Pop();
    }

    // A few sparks left behind as the clicked butterfly zooms off (0.6 s).
    private static void DrawSparks(DrawingContext dc, Point at, double age, int direction)
    {
        if (age >= 0.6) return;
        double f = age / 0.6;
        dc.PushOpacity(1 - f);
        for (int i = 0; i < 6; i++)
        {
            double a = i * Math.PI / 3 + 0.4, r = 8 + 18 * Math.Sqrt(f);
            var p = new Point(at.X - direction * (6 + 16 * f) + Math.Cos(a) * r * 0.6, at.Y + 6 * f + Math.Sin(a) * r * 0.6);
            dc.DrawLine(SparkPen, new Point(p.X - 2.5, p.Y), new Point(p.X + 2.5, p.Y));
            dc.DrawLine(SparkPen, new Point(p.X, p.Y - 2.5), new Point(p.X, p.Y + 2.5));
            if (i % 2 == 0) dc.DrawEllipse(Spark, null, p, 1.3, 1.3);
        }
        dc.Pop();
    }
}
