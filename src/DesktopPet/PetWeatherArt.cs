using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The weather around the cat (1.5.1 M2), drawn by code. Every particle is a function of the time and
// its index and lives in screen coordinates, so a moving cat does not drag the rain along with it.
// Alpha comes in a few frozen steps (no per-particle opacity), which keeps a frame cheap.
public sealed class PetWeatherArt : FrameworkElement
{
    private const int Levels = 4, Raindrops = 90, Flakes = 60, Leaves = 28, Motes = 44, Gusts = 6, Clouds = 3, Stars = 26, Fireflies = 12;
    private const double RainSlant = 0.18, SoftEdge = 0.42, Tau = Math.PI * 2;

    private static readonly Pen[] RainPen = StrokeSteps(Color.FromRgb(0xC4, 0xDD, 0xF2), 0.9, 1.5);
    private static readonly Brush[] SnowFill = FillSteps(Colors.White, 0.95);
    private static readonly Pen[] SnowEdge = StrokeSteps(Color.FromRgb(0x86, 0xA8, 0xC8), 0.9, 0.9);
    private static readonly Brush[][] LeafFills =
    {
        FillSteps(Color.FromRgb(0xD8, 0x62, 0x2E), 1),
        FillSteps(Color.FromRgb(0xE2, 0x6E, 0x86), 1),
        FillSteps(Color.FromRgb(0xEE, 0xA8, 0x3A), 1),
    };
    private static readonly Pen[] LeafEdge = StrokeSteps(Color.FromRgb(0x6E, 0x3A, 0x1E), 0.9, 1);
    private static readonly Geometry LeafShape = Leaf();
    private static readonly Brush[] MoteFill = FillSteps(Color.FromRgb(0xF2, 0xE6, 0x7A), 0.95);
    private static readonly Pen[] MoteEdge = StrokeSteps(Color.FromRgb(0x7D, 0x8F, 0x3A), 0.8, 0.8);
    private static readonly Pen[] GustPen = StrokeSteps(Color.FromRgb(0xF4, 0xF8, 0xE4), 0.7, 1.6);
    private static readonly Pen[] StarPen = StrokeSteps(Color.FromRgb(0xF4, 0xEE, 0xD8), 0.95, 1.2);
    private static readonly Brush[] GlowFill = FillSteps(Color.FromRgb(0xE8, 0xD8, 0x6A), 0.3);
    private static readonly Brush[] FlyFill = FillSteps(Color.FromRgb(0xFF, 0xF4, 0xB0), 1);
    private static readonly Brush[] SunFill = FillSteps(Color.FromRgb(0xFF, 0xFB, 0xE6), 1);
    private static readonly Pen[] SunPen = StrokeSteps(Color.FromRgb(0xFF, 0xF0, 0xB8), 1, 1.4);
    private static readonly Brush[] CloudFill = FillSteps(Color.FromRgb(0xFF, 0xFB, 0xF4), 0.92);
    private static readonly Pen[] CloudEdge = StrokeSteps(Color.FromRgb(0x8F, 0xA6, 0xBC), 0.8, 1.3);
    private static readonly Geometry CloudShape = Cloud();

    public WeatherKind Kind { get; set; }
    public double Intensity { get; set; }
    public double Time { get; set; }
    // This window's top-left on the screen, in DIPs.
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    // The cat within this window: the weather thins out around it.
    public double CatX { get; set; }
    public double CatY { get; set; }

    public PetWeatherArt(double width, double height)
    {
        Width = width; Height = height;
        CatX = width / 2; CatY = height / 2;
        IsHitTestVisible = false;
    }

    // A little over three cats across, within what stays a region around the cat.
    public static Size RegionFor(double catWidth)
    {
        double width = Math.Clamp(catWidth * 3.2, 320, 520);
        return new Size(width, width * 0.85);
    }

    protected override void OnRender(DrawingContext dc)
    {
        switch (Kind)
        {
            case WeatherKind.Rain: DrawRain(dc); break;
            case WeatherKind.Snow: DrawSnow(dc); break;
            case WeatherKind.Sunset: DrawLeaves(dc); break;
            case WeatherKind.Pollen: DrawPollen(dc); break;
            case WeatherKind.Night: DrawNight(dc); break;
            default: DrawSunny(dc); break;
        }
    }

    private void DrawRain(DrawingContext dc)
    {
        double count = Raindrops * Intensity, w = Width, h = Height;
        for (int i = 0; i < Math.Ceiling(count); i++)
        {
            double a = SeedA(i), b = SeedB(i), c = SeedC(i), speed = 470 + c * 190, length = 12 + c * 12;
            double x = Wrap(a * w - speed * RainSlant * Time - OriginX, w), y = Wrap(b * h + speed * Time - OriginY, h);
            int level = Level(Mask(x, y) * Math.Min(1, count - i));
            if (level > 0) dc.DrawLine(RainPen[level], new Point(x, y), new Point(x - RainSlant * length, y + length));
        }
    }

    private void DrawSnow(DrawingContext dc)
    {
        double count = Flakes * Intensity, w = Width, h = Height;
        for (int i = 0; i < Math.Ceiling(count); i++)
        {
            double a = SeedA(i), b = SeedB(i), c = SeedC(i), speed = 38 + c * 34;
            double x = Wrap(a * w + Math.Sin(Time * (0.5 + c) + i) * 12 - OriginX, w), y = Wrap(b * h + speed * Time - OriginY, h), r = 1.7 + c * 2.1;
            int level = Level(Mask(x, y) * Math.Min(1, count - i));
            if (level > 0) dc.DrawEllipse(SnowFill[level], SnowEdge[level], new Point(x, y), r, r);
        }
    }

    private void DrawLeaves(DrawingContext dc)
    {
        double count = 3 + (Leaves - 3) * Intensity, w = Width, h = Height;
        for (int i = 0; i < Math.Ceiling(count); i++)
        {
            double a = SeedA(i), b = SeedB(i), c = SeedC(i), speed = 34 + c * 30, drift = 26 + a * 22;
            double x = Wrap(a * w + drift * Time + Math.Sin(Time * 1.1 + i) * 14 - OriginX, w), y = Wrap(b * h + speed * Time - OriginY, h);
            int level = Level(Mask(x, y) * Math.Min(1, count - i));
            if (level == 0) continue;
            // It turns as it falls, and tips over like a real leaf (its width narrows and comes back).
            double turn = a * Tau + Time * (0.5 + c * 0.7), size = 0.75 + b * 0.4, tip = 0.45 + 0.55 * Math.Abs(Math.Cos(Time * (1.2 + a) + i));
            double cos = Math.Cos(turn) * size, sin = Math.Sin(turn) * size;
            dc.PushTransform(new MatrixTransform(cos, sin, -sin * tip, cos * tip, x, y));
            dc.DrawGeometry(LeafFills[i % LeafFills.Length][level], LeafEdge[level], LeafShape);
            dc.Pop();
        }
    }

    private void DrawPollen(DrawingContext dc)
    {
        double count = 8 + (Motes - 8) * Intensity, w = Width, h = Height;
        for (int i = 0; i < Math.Ceiling(count); i++)
        {
            double a = SeedA(i), b = SeedB(i), c = SeedC(i), drift = 26 + c * 34;
            double x = Wrap(a * w + drift * Time - OriginX, w), y = Wrap(b * h + Math.Sin(Time * (0.6 + c) + i * 1.7) * 18 - OriginY, h), r = 1.4 + c * 1.3;
            int level = Level(Mask(x, y) * Math.Min(1, count - i));
            if (level > 0) dc.DrawEllipse(MoteFill[level], MoteEdge[level], new Point(x, y), r, r);
        }
        double gusts = Gusts * Intensity;
        for (int i = 0; i < Math.Ceiling(gusts); i++)
        {
            double a = SeedA(i + 40), b = SeedB(i + 40), c = SeedC(i + 40), speed = 170 + c * 90;
            double x = Wrap(a * w + speed * Time - OriginX, w), y = Wrap(b * h - OriginY, h);
            int level = Level(Mask(x, y) * Math.Min(1, gusts - i));
            if (level == 0) continue;
            var previous = new Point(x, y);
            for (int s = 1; s <= 4; s++)
            {
                var point = new Point(x + s * 13, y + Math.Sin(s * 0.9 + c * Tau) * 3.5);
                dc.DrawLine(GustPen[level], previous, point);
                previous = point;
            }
        }
    }

    private void DrawNight(DrawingContext dc)
    {
        double w = Width, h = Height;
        for (int i = 0; i < Stars; i++)
        {
            double a = SeedA(i), b = SeedB(i), c = SeedC(i);
            double x = Wrap(a * w - OriginX, w), y = Wrap(b * h - OriginY, h);
            double twinkle = 0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(Time * (0.8 + c * 1.6) + i * 2.1)), r = 2 + c * 2;
            int level = Level(Mask(x, y) * twinkle);
            if (level == 0) continue;
            dc.DrawLine(StarPen[level], new Point(x - r, y), new Point(x + r, y));
            dc.DrawLine(StarPen[level], new Point(x, y - r), new Point(x, y + r));
        }
        double flies = 3 + (Fireflies - 3) * Intensity;
        for (int i = 0; i < Math.Ceiling(flies); i++)
        {
            double a = SeedA(i + 60), b = SeedB(i + 60), c = SeedC(i + 60);
            double x = Wrap(a * w + Math.Sin(Time * (0.3 + c * 0.4) + i) * 34 - OriginX, w);
            double y = Wrap(b * h + Math.Cos(Time * (0.25 + a * 0.4) + i * 2) * 26 - OriginY, h);
            double blink = Math.Max(0, Math.Sin(Time * (0.9 + c) + i * 1.3));
            int level = Level(Mask(x, y) * blink * Math.Min(1, flies - i));
            if (level == 0) continue;
            dc.DrawEllipse(GlowFill[level], null, new Point(x, y), 8, 8);
            dc.DrawEllipse(FlyFill[level], null, new Point(x, y), 2, 2);
        }
    }

    // More cloud as the strength rises, more sun as it falls.
    private void DrawSunny(DrawingContext dc)
    {
        double w = Width, h = Height, glints = 8 + 14 * (1 - Intensity), clouds = Clouds * Intensity;
        for (int i = 0; i < Math.Ceiling(glints); i++)
        {
            double a = SeedA(i), b = SeedB(i), c = SeedC(i);
            double x = Wrap(a * w + Math.Sin(Time * 0.3 + i) * 10 - OriginX, w), y = Wrap(b * h - Time * (6 + c * 8) - OriginY, h);
            double twinkle = 0.3 + 0.7 * (0.5 + 0.5 * Math.Sin(Time * (1.1 + c) + i * 2.3)), r = 2.5 + c * 3.5;
            int level = Level(Mask(x, y) * twinkle * Math.Min(1, glints - i));
            if (level == 0) continue;
            dc.DrawLine(SunPen[level], new Point(x - r, y), new Point(x + r, y));
            dc.DrawLine(SunPen[level], new Point(x, y - r), new Point(x, y + r));
            dc.DrawEllipse(SunFill[level], null, new Point(x, y), 1.5, 1.5);
        }
        for (int i = 0; i < Math.Ceiling(clouds); i++)
        {
            double a = SeedA(i + 80), b = SeedB(i + 80), c = SeedC(i + 80), size = 0.8 + c * 0.45;
            double x = Wrap(a * w + (7 + c * 7) * Time - OriginX, w), y = Math.Max(32, CatY - h * (0.44 - b * 0.2)); // Over the cat, inside the soft edge.
            int level = Level(Mask(x, y) * Math.Min(1, clouds - i));
            if (level == 0) continue;
            dc.PushTransform(new MatrixTransform(size, 0, 0, size, x, y));
            dc.DrawGeometry(CloudFill[level], CloudEdge[level], CloudShape);
            dc.Pop();
        }
    }

    // 1 around the cat, thinning to 0 at the rim of the region, so it has no visible edge.
    private double Mask(double x, double y)
    {
        double dx = (x - CatX) / (Width / 2), dy = (y - CatY) / (Height / 2);
        return Math.Clamp((1 - Math.Sqrt(dx * dx + dy * dy)) / SoftEdge, 0, 1);
    }

    private static int Level(double amount) => (int)Math.Ceiling(Math.Clamp(amount, 0, 1) * Levels);
    private static double Wrap(double value, double size) => value - size * Math.Floor(value / size);
    // A and B are the R2 sequence, which fills a plane evenly without forming rows; C is the third axis.
    private static double SeedA(int i) => Frac((i + 1) * 0.7548776662);
    private static double SeedB(int i) => Frac((i + 1) * 0.5698402910);
    private static double SeedC(int i) => Frac((i + 1) * 0.6180339887);
    private static double Frac(double value) => value - Math.Floor(value);

    private static Brush[] FillSteps(Color color, double peak)
    {
        var steps = new Brush[Levels + 1];
        for (int i = 1; i <= Levels; i++) steps[i] = PetUi.Frozen(Color.FromArgb((byte)(255 * peak * i / Levels), color.R, color.G, color.B));
        return steps;
    }

    private static Pen[] StrokeSteps(Color color, double peak, double thickness)
    {
        var fills = FillSteps(color, peak);
        var steps = new Pen[Levels + 1];
        for (int i = 1; i <= Levels; i++)
        {
            var pen = new Pen(fills[i], thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            pen.Freeze();
            steps[i] = pen;
        }
        return steps;
    }

    // A pointed leaf with its stalk and middle vein, about 22 DIPs long, centered on its origin.
    private static Geometry Leaf()
    {
        var shape = Geometry.Parse("M-9,0 C-5,-7 4,-7 9,0 C4,7 -5,7 -9,0 Z M-13,0 L5,0");
        shape.Freeze();
        return shape;
    }

    // A puffy cartoon cloud about 84 DIPs wide, centered on its origin.
    private static Geometry Cloud()
    {
        Geometry shape = new RectangleGeometry(new Rect(-34, -2, 68, 14), 7, 7);
        foreach (var (x, y, r) in new[] { (-20.0, -3.0, 13.0), (0.0, -10.0, 18.0), (21.0, -2.0, 12.0) })
            shape = new CombinedGeometry(GeometryCombineMode.Union, shape, new EllipseGeometry(new Point(x, y), r, r));
        var flat = shape.GetFlattenedPathGeometry();
        flat.Freeze();
        return flat;
    }
}
