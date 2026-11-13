using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The feather wand (docs/TOY-PLAY.md, M4): a wooden handle with a grip, a string and a teal feather, in the
// cat's vector style. One window for both poses: the handle's centre is always at (CentreX, CentreY).
// Lying, the handle rests flat on the floor with the feather beside its tip; held, the handle tilts up
// and the feather hangs on its string where the window puts it (FeatherString). Only what is drawn takes clicks.
public sealed class ToyWandArt : FrameworkElement
{
    // Room for the feather swung anywhere on its string, plume and all: tip offset + string + plume ≈ 155 each way.
    public const double ArtWidth = 340, ArtHeight = 340, CentreX = 170, CentreY = 170;
    // From the handle's centre to its tip while held (times Facing across, then up).
    public const double HeldTipX = 30, HeldTipY = 28;

    private static Brush B(byte r, byte g, byte b) => PetUi.Frozen(Color.FromRgb(r, g, b));
    private static Pen P(Brush brush, double width) { var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
    private static readonly Brush Wood = B(0xC9, 0x8E, 0x55), Grip = B(0xD9, 0x5D, 0x5D), Plume = B(0x4F, 0xB3, 0xA9), PlumeTip = B(0xFF, 0xF4, 0xE0),
        Hit = PetUi.Frozen(Color.FromArgb(1, 0, 0, 0));
    private static readonly Pen Outline = P(PetUi.ArtInk, 1.4), WoodInk = P(PetUi.ArtInk, 7.4), WoodPen = P(Wood, 5), GripPen = P(Grip, 5.4),
        StringPen = P(B(0x6A, 0x4A, 0x3A), 1.3), Barb = P(B(0x2F, 0x7F, 0x78), 1), Quill = P(B(0xF4, 0xE6, 0xC8), 1.2);

    public bool Held;
    public int Facing = 1;
    // The feather and the floor, in this window's DIPs (while held).
    public Point Feather;
    public double Floor = CentreY + DesktopToyRules.WandHeight / 2;

    public ToyWandArt() { Width = ArtWidth; Height = ArtHeight; }

    public static Point HeldTip(int facing) => new(CentreX + facing * HeldTipX, CentreY - HeldTipY);

    protected override void OnRender(DrawingContext dc)
    {
        int f = Facing < 0 ? -1 : 1;
        Point back, tip;
        if (Held) { tip = HeldTip(f); back = new Point(CentreX - f * HeldTipX, CentreY + HeldTipY); }
        else { tip = new Point(CentreX + f * DesktopToyRules.WandHalfWidth, CentreY); back = new Point(CentreX - f * DesktopToyRules.WandHalfWidth, CentreY); }
        Point feather = Held ? Feather : new Point(tip.X + f * 8, Floor - 9);
        // The string first, sagging a little when it is slack.
        double span = (feather - tip).Length;
        double sag = Held ? Math.Max(0, FeatherString.Length - span) * 0.35 : 0;
        var line = new StreamGeometry();
        using (var g = line.Open())
        {
            g.BeginFigure(tip, false, false);
            g.QuadraticBezierTo(new Point((tip.X + feather.X) / 2, (tip.Y + feather.Y) / 2 + sag), feather, true, true);
        }
        line.Freeze();
        dc.DrawGeometry(null, StringPen, line);
        // The handle: ink, wood, and a red grip at the back end.
        dc.DrawLine(WoodInk, back, tip);
        dc.DrawLine(WoodPen, back, tip);
        var grip = back + (tip - back) * 0.28;
        dc.DrawLine(P(PetUi.ArtInk, 7.8), back, grip);
        dc.DrawLine(GripPen, back, grip);
        // A wider, invisible grip area so the thin handle is easy to pick up.
        dc.DrawLine(P(Hit, 18), back, tip);
        double angle = Held ? Math.Atan2(feather.Y - tip.Y, feather.X - tip.X) * 180 / Math.PI - 90 : (f > 0 ? -90 : 90);
        DrawFeather(dc, feather, angle);
    }

    // The quill at at, the plume pointing away along angle (0: straight down).
    private static void DrawFeather(DrawingContext dc, Point at, double angle)
    {
        dc.PushTransform(new TranslateTransform(at.X, at.Y));
        dc.PushTransform(new RotateTransform(angle));
        dc.DrawEllipse(Hit, null, new Point(0, 14), 12, 18);
        var vane = new StreamGeometry();
        using (var g = vane.Open())
        {
            g.BeginFigure(new Point(0, 3), true, true);
            g.BezierTo(new Point(9, 8), new Point(10, 22), new Point(0, 32), true, true);
            g.BezierTo(new Point(-10, 22), new Point(-9, 8), new Point(0, 3), true, true);
        }
        vane.Freeze();
        dc.DrawGeometry(Plume, Outline, vane);
        dc.PushClip(vane);
        dc.DrawEllipse(PlumeTip, null, new Point(0, 32), 8, 6);
        for (int i = 0; i < 4; i++)
        {
            double y = 9 + i * 5;
            dc.DrawLine(Barb, new Point(0, y), new Point(8, y + 4));
            dc.DrawLine(Barb, new Point(0, y), new Point(-8, y + 4));
        }
        dc.Pop();
        dc.DrawLine(Quill, new Point(0, 0), new Point(0, 30));
        dc.Pop(); dc.Pop();
    }
}
