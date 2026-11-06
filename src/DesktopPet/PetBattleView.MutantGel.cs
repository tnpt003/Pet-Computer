using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

public sealed partial class BattleFieldView
{
    private static readonly Brush GelShadow = Frozen(170, 0x36, 0x78, 0x48);
    private static readonly Brush GelShine = Frozen(170, 0xEE, 0xFF, 0xC8);
    private static readonly Brush GelFill = MakeGelFill();
    private static readonly Pen GelVein = FrozenPen(Frozen(130, 0xCD, 0xF0, 0x97), 1.5);
    private static readonly Pen GelStream = FrozenPen(Frozen(200, 0x65, 0xA8, 0x4A), 9);
    private static Brush MakeGelFill()
    {
        var b = new LinearGradientBrush(Color.FromArgb(235, 0xB4, 0xE4, 0x81), Color.FromArgb(245, 0x4D, 0x91, 0x49), 90);
        b.Freeze(); return b;
    }
    // Smooth closed membrane, with independent broad lobes and little surface ripples.
    private static Geometry GelMembrane(Point c, double rx, double ry, double time, double seed)
    {
        const int n = 16;
        Span<Point> points = stackalloc Point[n];
        for (int i = 0; i < n; i++)
        {
            double a = i * Math.PI * 2 / n;
            double wave = 1 + 0.13 * Math.Sin(a * 3 + time * 1.7 + seed) + 0.065 * Math.Sin(a * 5 - time * 2.1 + seed);
            points[i] = new(c.X + Math.Cos(a) * rx * wave, c.Y + Math.Sin(a) * ry * wave);
        }
        var path = new StreamGeometry();
        using (var g = path.Open())
        {
            g.BeginFigure(points[0], true, true);
            for (int i = 0; i < n; i++)
            {
                Point a = points[(i + n - 1) % n], b = points[i], d = points[(i + 1) % n], e = points[(i + 2) % n];
                g.BezierTo(b + (d - a) / 6, d - (e - b) / 6, d, true, false);
            }
        }
        return path;
    }
    private static void DrawGel(DrawingContext dc, Point p, double rx, double ry, double time, double seed = 0, bool active = false)
    {
        rx = Math.Max(2, rx); ry = Math.Max(2, ry);
        var body = GelMembrane(p, rx, ry, time, seed);
        dc.DrawGeometry(GelFill, active ? MutationGlow : MutationLine, body);
        dc.PushClip(body);
        dc.DrawEllipse(GelShadow, null, new Point(p.X + rx * 0.2, p.Y + ry * 0.65), rx, ry * 0.5);
        dc.DrawEllipse(GelShine, null, new Point(p.X - rx * 0.32, p.Y - ry * 0.38), rx * 0.28, Math.Max(2, ry * 0.15));
        for (int i = 0; i < 4; i++)
        {
            double x = Math.Sin(seed + i * 2.3 + time * 0.55) * rx * 0.55;
            double y = Math.Cos(i * 1.7 + time * 0.8) * ry * 0.45;
            dc.DrawEllipse(MutationSoft, GelVein, new Point(p.X + x, p.Y + y), 2 + i % 2 * 2, 2 + i % 2 * 2);
        }
        dc.Pop();
        // Hanging filaments stretch slowly and shed a drop, giving the material weight.
        if (ry > 12)
            for (int i = 0; i < 3; i++)
            {
                double x = p.X + (i - 1) * rx * 0.52;
                double drop = (time * 0.5 + i * 0.31 + seed * 0.1) % 1;
                dc.DrawLine(GelVein, new Point(x, p.Y + ry * 0.6), new Point(x + Math.Sin(time + i) * 3, p.Y + ry + drop * 10));
                dc.DrawEllipse(MutationGreen, null, new Point(x + Math.Sin(time + i) * 3, p.Y + ry + drop * 10), 2.4, 3.5);
            }
    }
    private static void DrawGelStream(DrawingContext dc, Point from, Point to, double time, double seed = 0, bool sucking = false)
    {
        var g = new StreamGeometry();
        var delta = to - from;
        double length = Math.Max(1, delta.Length);
        Vector normal = new(-delta.Y / length, delta.X / length);
        double bend = Math.Min(18, length * 0.1);
        Point a = from + delta * 0.33 + normal * bend * Math.Sin(time * 3 + seed);
        Point b = from + delta * 0.7 - normal * bend * Math.Cos(time * 2.7 + seed);
        using (var p = g.Open()) { p.BeginFigure(from, false, false); p.BezierTo(a, b, to, true, false); }
        dc.DrawGeometry(null, GelStream, g); dc.DrawGeometry(null, GelVein, g);
        for (int i = 0; i < 4; i++)
        {
            double t = (time * 0.8 + i * 0.25 + seed) % 1; if (sucking) t = 1 - t;
            double u = 1 - t;
            Point p = new(u * u * u * from.X + 3 * u * u * t * a.X + 3 * u * t * t * b.X + t * t * t * to.X,
                u * u * u * from.Y + 3 * u * u * t * a.Y + 3 * u * t * t * b.Y + t * t * t * to.Y);
            dc.DrawEllipse(GelShine, null, p, 3, 3);
        }
    }
    private static void DrawGelBurst(DrawingContext dc, Point p, double radius, double k, double seed = 0)
    {
        k = Math.Clamp(k, 0, 1);
        dc.PushOpacity(1 - k);
        for (int i = 0; i < 9; i++)
        {
            double angle = i * Math.PI * 2 / 9 + seed, d = radius * (0.2 + k * 0.8);
            Point end = new(p.X + Math.Cos(angle) * d, p.Y + Math.Sin(angle) * d + k * k * 16);
            dc.DrawEllipse(MutationGreen, GelVein, end, 2 + (i % 3), 3 + (i % 3));
        }
        dc.Pop();
    }
    private static void DrawMutantAttackArt(DrawingContext dc, MutantZone z, Rect r, Point source, double time)
    {
        bool warning = z.Age < z.Warn;
        double progress = warning ? Math.Clamp(z.Age / Math.Max(0.01, z.Warn), 0, 1) : Math.Clamp((z.Age - z.Warn) / Math.Max(0.01, z.Life), 0, 1);
        Point center = new(r.Left + r.Width / 2, r.Top + r.Height / 2);
        // Keep an explicit boundary throughout the attack. Decorative droplets do not enlarge damage.
        dc.DrawRoundedRectangle(warning ? MutationSoft : null, warning ? MutationWarn : GelVein, r, 8, 8);
        if (z.Shape is MutantShape.Column or MutantShape.Waterfall or MutantShape.Breath)
        {
            dc.PushClip(new RectangleGeometry(r));
            if (!warning) dc.DrawGeometry(GelFill, null, GelMembrane(center, r.Width * 0.58, r.Height * 0.58, time, 7));
            int lanes = Math.Clamp((int)((z.Shape == MutantShape.Breath ? r.Height : r.Width) / 24), 2, 24);
            for (int i = 0; i < lanes; i++)
            {
                double f = (i + 0.5) / lanes;
                Point a = z.Shape == MutantShape.Breath ? new(r.Left, r.Top + r.Height * f) : new(r.Left + r.Width * f, r.Top);
                Point b = z.Shape == MutantShape.Breath ? new(r.Right, a.Y) : new(a.X, r.Bottom);
                if (warning) dc.DrawLine(MutationWarn, a, a + (b - a) * progress);
                else DrawGelStream(dc, a, b, time * (z.Shape == MutantShape.Waterfall ? 2 : 1), i * 0.13);
            }
            dc.Pop();
            if (!warning) DrawGelBurst(dc, z.Shape == MutantShape.Breath ? new(r.Right, center.Y) : new(center.X, r.Bottom), Math.Min(55, r.Width / 2), progress);
            return;
        }
        Point arriving = warning ? source + (center - source) * progress + new Vector(0, -Math.Sin(progress * Math.PI) * 60) : center;
        switch (z.Shape)
        {
            case MutantShape.Eye:
                DrawGel(dc, arriving, r.Width * 0.4, r.Height * 0.4, time, 3, !warning);
                MutationSpiral(dc, arriving, Math.Min(r.Width, r.Height) * 0.23, time * 2);
                break;
            case MutantShape.Hand:
                DrawGelStream(dc, source, arriving, time);
                DrawGel(dc, arriving, r.Width * 0.32, r.Height * 0.3, time, 4, !warning);
                for (int i = 0; i < 4; i++) DrawGel(dc, new Point(arriving.X + (i - 1.5) * r.Width * 0.14, arriving.Y - r.Height * 0.23), r.Width * 0.08, r.Height * 0.22, time, i);
                break;
            case MutantShape.Shell:
                DrawGel(dc, center, r.Width * (0.3 + progress * 0.15), r.Height * 0.4, time, 2, !warning);
                dc.DrawLine(MutationGlow, new Point(center.X - r.Width * 0.2, r.Top + 10), new Point(center.X + r.Width * 0.1, r.Bottom - 5));
                break;
            case MutantShape.Wave:
            case MutantShape.Fan:
                for (int i = 0; i < 5; i++)
                {
                    double f = (i + 0.5) / 5;
                    double high = z.Shape == MutantShape.Fan ? Math.Sin(f * Math.PI) : Math.Abs(Math.Sin(time * 4 + i));
                    DrawGel(dc, new Point(r.Left + r.Width * f, r.Bottom - r.Height * 0.3 * high), r.Width / 9, r.Height * (0.15 + high * 0.25), time, i, !warning);
                }
                break;
            default:
                DrawGel(dc, arriving, Math.Min(30, r.Width * 0.35), Math.Min(32, r.Height * 0.35), time, 1, !warning);
                break;
        }
        if (!warning) DrawGelBurst(dc, center, Math.Min(r.Width, r.Height) * 0.6, progress);
    }
}
