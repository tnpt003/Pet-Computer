using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The cup game's things (docs/MINI-GAMES.md → M1), drawn in their own window next to the cat: three glazed
// clay cups upside down and a little dried fish. Only the drawn cups take clicks; the rest of the window
// is fully transparent, so clicks there go through to what is underneath.
public sealed class CupGameArt : FrameworkElement
{
    public const double Pad = 40, ArtHeight = 100, LiftSlide = 30, LiftHeight = 6;
    public static double ArtWidth => CupGameRules.RowWidth + 2 * Pad;

    private static Brush B(byte r, byte g, byte b) => PetUi.Frozen(Color.FromRgb(r, g, b));
    private static Pen P(Brush brush, double width) { var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
    private static readonly Brush Glaze = B(0xF4, 0xE6, 0xCC), GlazeShade = B(0xE2, 0xCC, 0xA8), Band = B(0xD9, 0x82, 0x5B),
        Shadow = PetUi.Frozen(Color.FromArgb(0x40, 0x2A, 0x1F, 0x16)), Shine = PetUi.Frozen(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF));
    private static readonly Pen Outline = P(PetUi.ArtInk, 1.6), ShinePen = P(Shine, 2.2), HoverPen = P(YarnBallArt.Halo, 5);
    private static readonly Geometry Body = Geometry.Parse("M-20,0 L-15,-28 Q-14.5,-31 -11.5,-31 L11.5,-31 Q14.5,-31 15,-28 L20,0 Z");
    private static readonly Geometry Foot = Geometry.Parse("M-9,-31 L-8,-35 L8,-35 L9,-31 Z");
    // The shade and the band cut to the body once, instead of a clip pushed for each cup: a pushed clip left
    // every cup after the first drawn one without hit testing, so only one cup took clicks (Issue #85).
    private static readonly Geometry BodyShade = Cut(new Rect(5, -32, 16, 34)), BodyBand = Cut(new Rect(-22, -19, 44, 6));
    private static Geometry Cut(Rect rect) { var cut = Geometry.Combine(Body, new RectangleGeometry(rect), GeometryCombineMode.Intersect, null); cut.Freeze(); return cut; }

    public readonly double[] X = new double[CupGameRules.Cups], Rise = new double[CupGameRules.Cups], Lift = new double[CupGameRules.Cups];
    public readonly bool[] Front = new bool[CupGameRules.Cups];
    public int Hover = -1;
    public int Side = 1; // Where the cat is: a lifted cup tips away from it.
    public double? FishX; // Centre across the row (from its left edge); null: the cat has it or it is not out.

    public CupGameArt()
    {
        Width = ArtWidth; Height = ArtHeight;
        for (int i = 0; i < X.Length; i++) X[i] = CupGameRules.SlotCentre(i);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double floor = ArtHeight - 2;
        for (int i = 0; i < X.Length; i++)
        {
            double up = Rise[i] + Lift[i] * LiftHeight;
            double w = 18 * Math.Max(0.45, 1 - up / 70);
            dc.DrawEllipse(Shadow, null, new Point(Pad + X[i] + Lift[i] * LiftSlide * Side, floor), w, 3.5);
        }
        if (FishX is double fish)
        {
            dc.PushTransform(new TranslateTransform(Pad + fish, floor - 6));
            dc.PushTransform(new ScaleTransform(0.8, 0.8));
            PetFarmArt.DrawFish(dc, FarmFish.Carp);
            dc.Pop(); dc.Pop();
        }
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < X.Length; i++)
                if (Front[i] == (pass == 1)) DrawCup(dc, i, floor);
    }

    private void DrawCup(DrawingContext dc, int i, double floor)
    {
        double up = Rise[i] + Lift[i] * LiftHeight;
        // Tipped by the cat's paw: it slides aside onto its far rim, so the fish (or nothing) shows where it stood.
        dc.PushTransform(new TranslateTransform(Pad + X[i] + Lift[i] * LiftSlide * Side, floor - up));
        if (Lift[i] > 0) dc.PushTransform(new RotateTransform(28 * Lift[i] * Side, Side > 0 ? 20 : -20, 0));
        if (i == Hover) dc.DrawGeometry(null, HoverPen, Body);
        dc.DrawGeometry(Glaze, null, Body);
        dc.DrawGeometry(GlazeShade, null, BodyShade);
        dc.DrawGeometry(Band, null, BodyBand);
        dc.DrawGeometry(Glaze, Outline, Foot);
        dc.DrawGeometry(null, Outline, Body);
        dc.DrawLine(ShinePen, new Point(-10, -26), new Point(-13, -9));
        if (Lift[i] > 0) dc.Pop();
        dc.Pop();
    }
}
