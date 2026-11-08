using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The magic portal on the desktop (1.5.1 M1), drawn by code: a violet swirl in a lilac ring with
// gold sparks around it. Stands on the floor at the bottom center; PetWindow.Portal.cs sets the
// opening amount and the time and redraws it while it is on screen.
public sealed class PetPortalArt : FrameworkElement
{
    public const double ArtWidth = 132, ArtHeight = 172;
    private const double CenterX = 66, CenterY = 98, RadiusX = 46, RadiusY = 66, Floor = 168;
    private const int Sparks = 6, Motes = 7;

    private static readonly Brush Depth = Swirl();
    private static readonly Brush Hit = PetUi.Frozen(Color.FromArgb(1, 0, 0, 0));
    private static readonly Brush FloorGlow = PetUi.Frozen(Color.FromArgb(0x70, 0xC5, 0xAD, 0xE5));
    private static readonly Brush Core = PetUi.Frozen(Color.FromArgb(0xD0, 0xFF, 0xF6, 0xE0));
    private static readonly Brush Mote = PetUi.Frozen(Color.FromRgb(0xF4, 0xE8, 0xFF));
    private static readonly Pen Ink = PetUi.FrozenPen(PetUi.ArtInk, 7.5);
    private static readonly Pen Ring = PetUi.FrozenPen(PetUi.Lilac, 5);
    private static readonly Pen RingLight = PetUi.FrozenPen(PetUi.Frozen(Color.FromArgb(0xC0, 0xFF, 0xF3, 0xE2)), 1.4);
    private static readonly Pen Arm = PetUi.FrozenPen(PetUi.Frozen(Color.FromArgb(0xB0, 0xE9, 0xDC, 0xFF)), 7);
    private static readonly Pen SparkEdge = PetUi.FrozenPen(PetUi.ArtInk, 1.1);
    private static readonly Geometry Arc = Spiral();
    private static readonly Geometry Spark = Star();
    private static readonly Geometry Inside = Freeze(new EllipseGeometry(new Point(CenterX, CenterY), RadiusX, RadiusY));

    public double Amount { get; set; }
    public double Time { get; set; }

    public PetPortalArt() { Width = ArtWidth; Height = ArtHeight; IsHitTestVisible = true; }

    protected override void OnRender(DrawingContext dc)
    {
        double a = Math.Max(0, Amount);
        dc.DrawGeometry(Hit, null, Inside); // The whole oval takes the click; the rest stays click-through.
        if (a <= 0) return;
        dc.DrawEllipse(FloorGlow, null, new Point(CenterX, Floor - 2), 54 * Math.Min(1, a), 7 * Math.Min(1, a));
        dc.PushTransform(new ScaleTransform(a, a, CenterX, Floor));
        dc.DrawEllipse(Depth, null, new Point(CenterX, CenterY), RadiusX, RadiusY);
        dc.PushClip(Inside);
        for (int i = 0; i < 3; i++)
        {
            var turn = Matrix.Identity;
            turn.Rotate(Time * 70 + i * 120);
            turn.Scale(RadiusX / 70, RadiusY / 70);
            turn.Translate(CenterX, CenterY);
            dc.PushTransform(new MatrixTransform(turn));
            dc.DrawGeometry(null, Arm, Arc);
            dc.Pop();
        }
        double pulse = 1 + 0.12 * Math.Sin(Time * 4);
        dc.DrawEllipse(Core, null, new Point(CenterX, CenterY), 9 * pulse, 13 * pulse);
        for (int i = 0; i < Motes; i++)
        {
            double k = (Time * 0.35 + i / (double)Motes) % 1;
            double x = CenterX + Math.Sin(i * 2.4 + Time * 1.3) * (RadiusX - 12);
            dc.PushOpacity(Math.Sin(k * Math.PI));
            dc.DrawEllipse(Mote, null, new Point(x, CenterY + RadiusY - k * RadiusY * 2), 2.2, 2.2);
            dc.Pop();
        }
        dc.Pop();
        dc.DrawEllipse(null, Ink, new Point(CenterX, CenterY), RadiusX, RadiusY);
        dc.DrawEllipse(null, Ring, new Point(CenterX, CenterY), RadiusX, RadiusY);
        dc.DrawEllipse(null, RingLight, new Point(CenterX, CenterY), RadiusX - 1.4, RadiusY - 1.4);
        for (int i = 0; i < Sparks; i++)
        {
            double angle = Time * 0.8 + i * Math.PI * 2 / Sparks;
            double size = 1 + 0.18 * Math.Sin(Time * 5 + i * 1.7);
            dc.PushTransform(new TranslateTransform(CenterX + Math.Cos(angle) * (RadiusX + 1), CenterY + Math.Sin(angle) * (RadiusY + 1)));
            dc.PushTransform(new ScaleTransform(size, size));
            dc.DrawGeometry(PetUi.Game.Gold, SparkEdge, Spark);
            dc.Pop(); dc.Pop();
        }
        dc.Pop();
    }

    private static Brush Swirl()
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0xC9, 0xA6, 0xF2), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x7A, 0x52, 0xC8), 0.55));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x3C, 0x26, 0x78), 1));
        brush.Freeze();
        return brush;
    }

    // One arm of the swirl, from the middle out to the rim, on a circle of radius 70.
    private static Geometry Spiral()
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(4, 0), false, false);
            for (int i = 1; i <= 40; i++)
            {
                double k = i / 40.0, angle = k * Math.PI * 1.6, radius = 4 + k * 70;
                context.LineTo(new Point(Math.Cos(angle) * radius, Math.Sin(angle) * radius), true, true);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    private static Geometry Star() => Freeze(Geometry.Parse("M0,-7 L1.9,-1.9 L7,0 L1.9,1.9 L0,7 L-1.9,1.9 L-7,0 L-1.9,-1.9 Z"));

    private static Geometry Freeze(Geometry geometry) { geometry.Freeze(); return geometry; }
}
