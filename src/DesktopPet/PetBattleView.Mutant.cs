using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

public sealed partial class BattleFieldView
{
    private static readonly Brush MutationGreen = Frozen(235, 0x7F, 0xBF, 0x5A);
    private static readonly Brush MutationSoft = Frozen(75, 0x9B, 0xD9, 0x72);
    private static readonly Brush MutationCore = Frozen(255, 0xD8, 0xF4, 0xA4);
    private static readonly Brush MutationInk = Frozen(255, 0x18, 0x56, 0x55);
    private static readonly Pen MutationLine = FrozenPen(MutationInk, 2.4);
    private static readonly Pen MutationGlow = FrozenPen(MutationCore, 3);
    private static readonly Pen MutationWarn = FrozenPen(Game.Warning, 2, new DashStyle(new double[] { 4, 3 }, 0));
    private static readonly Geometry MutationBody = Parse("M -20,0 C -28,-3 -22,-14 -17,-19 C -17,-36 -3,-37 1,-25 C 10,-31 22,-28 20,-17 C 28,-8 25,1 17,0 Z");
    private static readonly Geometry MutationBody2 = Parse("M -20,0 C -28,-2 -21,-12 -19,-16 C -31,-12 -34,-20 -29,-23 C -25,-26 -22,-19 -19,-23 C -19,-37 -5,-39 0,-27 C 9,-35 21,-30 19,-20 C 26,-28 34,-25 31,-19 C 29,-15 24,-20 23,-13 C 29,-3 24,1 17,0 Z");
    private static readonly Geometry MutationBody3 = Parse("M -23,0 C -33,-3 -25,-10 -20,-13 C -29,-19 -25,-27 -18,-24 C -23,-39 -9,-41 -5,-29 C -1,-21 1,-37 10,-32 C 16,-29 16,-23 18,-22 C 20,-35 32,-34 28,-24 C 23,-17 36,-18 31,-10 C 28,-5 22,-8 26,-2 C 27,3 8,-2 0,0 Z");
    private static Geometry MutationBodyFor(Slime s) => (s.Mutant?.Phase ?? 1) switch { 2 => MutationBody2, 3 => MutationBody3, _ => MutationBody };

    private static void MutationSpiral(DrawingContext dc, Point center, double radius, double phase)
    {
        var path = new StreamGeometry();
        using (var g = path.Open())
        {
            g.BeginFigure(center, false, false);
            for (int i = 1; i <= 36; i++)
            {
                double a = i * Math.PI / 9 + phase, r = radius * i / 36;
                g.LineTo(new Point(center.X + Math.Cos(a) * r, center.Y + Math.Sin(a) * r), true, false);
            }
        }
        dc.DrawGeometry(null, MutationLine, path);
    }
    private static void DrawMutantFace(DrawingContext dc, Slime s, double bw, double bh, double time)
    {
        var m = s.Mutant;
        double phase = m?.Phase ?? 1;
        // Bulges move independently; the default face remains spiral-eyed and crooked.
        for (int i = 0; i < 3 + phase; i++)
        {
            double x = Math.Sin(i * 2.4 + time * 0.8) * bw * 0.32;
            double y = -bh * (0.3 + (i % 3) * 0.2);
            dc.DrawEllipse(MutationSoft, null, new Point(x, y), 3 + i % 3, 4 + Math.Sin(time + i));
        }
        double eyeY = -bh * (m?.Mode == MutantMode.Roar ? 0.7 : 0.58);
        MutationSpiral(dc, new Point(-8, eyeY), 5.6, time * 0.5);
        MutationSpiral(dc, new Point(8, eyeY - 2), 6.2, -time * 0.45);
        var mouth = new StreamGeometry();
        using (var p = mouth.Open())
        {
            p.BeginFigure(new Point(-12, -bh * 0.32), false, false);
            p.BezierTo(new Point(-4, -bh * 0.05), new Point(-2, -bh * 0.6), new Point(4, -bh * 0.3), true, false);
            p.BezierTo(new Point(10, -bh * 0.1), new Point(16, -bh * 0.38), new Point(12, -bh * 0.47), true, false);
        }
        dc.DrawGeometry(null, MutationLine, mouth);
        if (m?.Mode == MutantMode.Roar)
        {
            dc.DrawEllipse(MutationInk, MutationGlow, new Point(0, -bh * 0.3), 8, 7 + 3 * Math.Sin(time * 13));
            for (int i = 0; i < 3; i++) dc.DrawEllipse(null, MutationLine, new Point(0, -bh * 0.35), 17 + i * 6, 11 + i * 5);
        }
        if (m?.Mode == MutantMode.Core)
        {
            dc.DrawEllipse(MutationInk, MutationGlow, new Point(0, -bh * 0.4), 10, 12);
            dc.DrawEllipse(MutationCore, MutationLine, new Point(0, -bh * 0.4), 6, 8);
            for (int i = 0; i < m.CoreHits; i++) dc.DrawLine(MutationLine, new Point(-7 + i * 2.5, -bh * 0.5), new Point(-4 + i * 2.5, -bh * 0.3));
        }
        if (m?.Mode is MutantMode.Transition or MutantMode.Absorb)
            for (int side = -1; side <= 1; side += 2)
            {
                dc.DrawEllipse(MutationGreen, MutationLine, new Point(side * (bw * 0.46 + 4), -bh * 0.25), 8, 6);
                dc.DrawLine(MutationGlow, new Point(side * bw * 0.45, -bh * 0.25), new Point(side * bw * 0.65, 0));
            }
    }
    private void DrawMutantGround(DrawingContext dc, double floor, double time)
    {
        foreach (var s in sim.Slimes)
        {
            if (!s.Targetable || s.Mutant is not { } m) continue;
            if (m.Phase >= 2)
            {
                double fade = m.TerrainCleared ? Math.Clamp(1 - (time - m.FadeAt) / 2.5, 0, 1) : 1;
                dc.PushOpacity(fade);
                double growth = m.Mode == MutantMode.Transition ? Math.Clamp(1 - m.Clock / 3, 0, 1) : 1;
                double sideW = (sim.Right - sim.Left) * (m.Phase == 2 ? 0.125 : 0.25);
                if (m.Mode == MutantMode.Transition) sideW *= 0.5 + growth * 0.5;
                for (int i = 0; i < 4; i++)
                {
                    double x = (i < 2 ? sim.Left + sideW * (i + 0.5) / 2 : sim.Right - sideW * (i - 1.5) / 2) - OriginX;
                    double clock = BattleSim.MutantTerrainClock(m, i);
                    bool live = clock >= 5 && !m.TerrainCleared && m.Mode != MutantMode.Transition;
                    dc.DrawEllipse(live ? MutationGreen : MutationSoft, clock is >= 3.8 and < 5 ? MutationWarn : MutationLine, new Point(x, floor - 3), sideW / 4, live ? 10 : 4);
                    for (int k = 0; k < 4; k++)
                        dc.DrawEllipse(MutationCore, null, new Point(x + (k - 1.5) * sideW / 11, floor - 5 - (live ? 5 * Math.Abs(Math.Sin(time * 3 + k)) : 0)), 2.5, 2);
                    if (m.Mode == MutantMode.Transition || m.TerrainCleared)
                    {
                        Point origin = new(s.X - OriginX, floor - s.Height * 0.4);
                        double t = (time * 0.7 + i * 0.23) % 1;
                        if (m.TerrainCleared) t = 1 - t;
                        dc.DrawLine(MutationLine, origin, new Point(x, floor - 4));
                        dc.DrawEllipse(MutationCore, MutationLine, new Point(origin.X + (x - origin.X) * t, origin.Y + (floor - origin.Y) * t - Math.Sin(t * Math.PI) * 25), 5, 5);
                    }
                }
                dc.Pop();
            }
            foreach (var b in m.FailedCores)
            {
                double x = b.X - OriginX;
                dc.DrawEllipse(MutationInk, Outline, new Point(x, floor - 12), 24, 14);
                dc.DrawEllipse(MutationGreen, MutationLine, new Point(x, floor - 17), 17, 12);
                dc.DrawLine(MutationGlow, new Point(x - 9, floor - 19), new Point(x + 8, floor - 10));
                if (m.Mode == MutantMode.Absorb)
                {
                    Point end = new(s.X - OriginX, floor - s.Height * 0.5);
                    dc.DrawLine(MutationLine, new Point(x, floor - 15), end);
                    double t = (time * 0.8 + x * 0.01) % 1;
                    dc.DrawEllipse(MutationCore, null, new Point(x + (end.X - x) * t, floor - 15 + (end.Y - floor + 15) * t), 5, 5);
                }
            }
        }
    }
    private static void MutantCaption(DrawingContext dc, string text, Point p, double size = 16)
    {
        var t = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, MutationCore, 1);
        dc.DrawRoundedRectangle(MutationInk, null, new Rect(p.X - t.Width / 2 - 6, p.Y - 2, t.Width + 12, t.Height + 4), 5, 5);
        dc.DrawText(t, new Point(p.X - t.Width / 2, p.Y));
    }
    private static void MutantCatIcon(DrawingContext dc, Point p, bool sword)
    {
        dc.DrawEllipse(MutationCore, MutationLine, p, 9, 7);
        dc.DrawLine(MutationLine, new Point(p.X - 8, p.Y - 3), new Point(p.X - 6, p.Y - 12));
        dc.DrawLine(MutationLine, new Point(p.X - 6, p.Y - 12), new Point(p.X - 1, p.Y - 6));
        dc.DrawLine(MutationLine, new Point(p.X + 8, p.Y - 3), new Point(p.X + 6, p.Y - 12));
        dc.DrawLine(MutationLine, new Point(p.X + 6, p.Y - 12), new Point(p.X + 1, p.Y - 6));
        if (sword)
        {
            dc.DrawLine(MutationGlow, new Point(p.X + 13, p.Y + 5), new Point(p.X + 24, p.Y - 12));
            dc.DrawLine(MutationLine, new Point(p.X + 12, p.Y - 1), new Point(p.X + 23, p.Y + 5));
        }
    }
    public static void DrawMutantAir(DrawingContext dc, BattleSim sim, Func<double, double, Point> at, Point cat, double time)
    {
        foreach (var s in sim.Slimes)
        {
            if (!s.Targetable || s.Mutant is not { } m) continue;
            foreach (var z in m.Zones)
            {
                Point p = at(z.X, z.H + z.Height);
                double width = Math.Max(1, Math.Abs(at(z.X + z.W, 0).X - at(z.X, 0).X));
                double height = Math.Max(2, Math.Abs(at(0, z.H).Y - at(0, z.H + z.Height).Y));
                bool warning = z.Age < z.Warn;
                double fade = z.Age <= z.Warn + z.Life ? 1 : Math.Clamp(1 - (z.Age - z.Warn - z.Life) / 0.8, 0, 1);
                dc.PushOpacity(fade * (warning ? 0.65 : 0.9));
                var rect = new Rect(p.X - width / 2, p.Y, width, height);
                dc.DrawRoundedRectangle(warning ? MutationSoft : MutationGreen, warning ? MutationWarn : MutationGlow, rect, Math.Min(18, width / 4), Math.Min(18, height / 4));
                int dots = z.Shape is MutantShape.Column or MutantShape.Waterfall or MutantShape.Breath ? 12 : 4;
                for (int i = 0; i < dots; i++)
                {
                    double f = (i * 0.173 + time * (warning ? 0.12 : 0.7)) % 1;
                    double x = rect.Left + width * ((i * 0.37) % 1), y = rect.Top + height * f;
                    dc.DrawEllipse(warning ? MutationCore : MutationInk, null, new Point(x, y), 2 + i % 3, 3 + i % 4);
                }
                Point center = new(rect.Left + width / 2, rect.Top + height / 2);
                if (z.Shape == MutantShape.Eye) MutationSpiral(dc, center, Math.Min(width, height) * 0.3, time);
                if (z.Shape == MutantShape.Hand)
                    for (int i = 0; i < 4; i++) dc.DrawEllipse(MutationCore, MutationLine, new Point(rect.Left + width * (i + 1) / 5, rect.Top + 8), 7, 14);
                if (z.Shape == MutantShape.Fan)
                    for (int i = 0; i < 4; i++) dc.DrawLine(MutationLine, new Point(center.X, rect.Bottom), new Point(rect.Left + width * i / 3, rect.Top));
                if (warning) MutantCaption(dc, "!", new Point(center.X, rect.Top + 4));
                dc.Pop();
            }
            for (int i = 0; i < m.Bumps.Count; i++)
            {
                var b = m.Bumps[i]; Point p = at(b.X, b.H + (b.Side == 0 ? 15 : 0));
                bool active = i == m.NextBump && m.Mode == MutantMode.Bumps;
                dc.PushOpacity(b.Solved ? 0.4 : active ? 1 : 0.5);
                dc.PushTransform(new TranslateTransform(p.X, p.Y));
                if (b.Side != 0) dc.PushTransform(new RotateTransform(b.Side < 0 ? 90 : -90));
                dc.DrawEllipse(b.Solved ? MutationSoft : MutationGreen, active ? MutationGlow : MutationLine, new Point(0, 0), 30, b.Solved ? 5 : b.Side == 0 ? 15 : 23);
                dc.DrawEllipse(active ? MutationCore : MutationInk, MutationLine, new Point(0, -5), b.Side == 0 ? 13 : 9, b.Solved ? 2 : 7);
                if (b.Solved)
                {
                    dc.DrawLine(MutationGlow, new Point(-8, -7), new Point(-2, -2));
                    dc.DrawLine(MutationGlow, new Point(-2, -2), new Point(9, -14));
                }
                if (b.Side != 0) dc.Pop(); dc.Pop();
                MutantCaption(dc, (i + 1).ToString(CultureInfo.InvariantCulture), new Point(p.X + (b.Side < 0 ? 32 : b.Side > 0 ? -32 : 0), p.Y - 35), 13);
                if (active)
                {
                    double dx = b.Side == 0 ? 0 : -b.Side * 58;
                    Point icon = new(p.X + dx, p.Y - (b.Side == 0 ? 72 : 0));
                    MutantCatIcon(dc, icon, b.Side != 0);
                    Point end = b.Side == 0 ? new(p.X, p.Y - 22) : new(p.X - b.Side * 22, p.Y);
                    dc.DrawLine(MutationGlow, icon, end);
                    dc.DrawLine(MutationGlow, end, new Point(end.X - 5 - (b.Side == 0 ? 0 : b.Side * 3), end.Y - 7));
                    dc.DrawLine(MutationGlow, end, new Point(end.X + 5 - (b.Side == 0 ? 0 : b.Side * 3), end.Y - 7));
                }
                dc.Pop();
            }
            foreach (var bag in m.Bags)
            {
                double fade = m.TerrainCleared ? Math.Clamp(1 - (time - m.FadeAt) / 2.5, 0, 1) : 1;
                Point p = at(bag.X, bag.H);
                if (m.TerrainCleared)
                {
                    Point target = at(s.X, s.Height * 0.5);
                    double t = 1 - fade; p = new Point(p.X + (target.X - p.X) * t, p.Y + (target.Y - p.Y) * t);
                }
                dc.PushOpacity(fade);
                double pulse = bag.Warning ? 1.15 + Math.Sin(time * 12) * 0.05 : 1 + Math.Sin(time * 2 + bag.Lane) * 0.05;
                if (bag.Warning || bag.Spraying)
                {
                    double r = Math.Abs(at(bag.X + 85, bag.H).X - p.X);
                    dc.DrawEllipse(bag.Spraying && !m.TerrainCleared ? MutationSoft : null, bag.Warning ? MutationWarn : MutationGlow, p, r, r);
                    if (bag.Spraying)
                        for (int i = 0; i < 12; i++)
                        {
                            double a = i * Math.PI / 6, d = 30 + ((time * 80 + i * 17) % 55);
                            dc.DrawEllipse(MutationGreen, MutationLine, new Point(p.X + Math.Cos(a) * d, p.Y + Math.Sin(a) * d), 4, 6);
                        }
                }
                dc.DrawEllipse(MutationGreen, MutationLine, p, 28 * pulse, 35 / pulse);
                for (int i = 0; i < 3; i++) dc.DrawEllipse(MutationCore, null, new Point(p.X - 10 + i * 9, p.Y - 12 + i * 8), 4, 6);
                dc.Pop();
            }
            if (m.Projectile || m.ReturnLeft > 0)
            {
                Point p = at(m.CoreX, m.CoreH);
                if (m.Projectile && m.ProjectileAge < 0)
                {
                    Point end = at(m.AimX, 0), start = at(m.FromX, m.FromH);
                    dc.DrawLine(MutationWarn, start, end); dc.DrawEllipse(null, MutationGlow, end, 28, 8);
                    MutantCatIcon(dc, new Point(end.X, end.Y - 60), false);
                }
                dc.DrawEllipse(MutationSoft, MutationGlow, p, 28, 28);
                dc.DrawEllipse(MutationCore, MutationLine, p, 13, 17);
                MutationSpiral(dc, p, 8, time);
            }
            if (m.Mode == MutantMode.Absorb)
            {
                Point center = at(s.X, s.Height * 0.5);
                double k = Math.Clamp(1 - m.Clock / 3, 0, 1);
                dc.DrawEllipse(null, m.Misses == 6 ? MutationGlow : MutationWarn, center, 30 + k * (sim.Right - sim.Left), 20 + k * sim.SkyHeight);
                MutantCaption(dc, m.Phase == 1 ? "50%" : m.Misses == 6 ? "40%" : m.Misses >= 3 ? "20%" : "+", new Point(center.X, center.Y - 60), 22);
            }
            Point label = at(s.X, s.Height + 45);
            if (m.Mode == MutantMode.Bumps) MutantCaption(dc, $"{m.Round + 1}/3  ·  {Math.Max(0, m.Deadline - time):0}s", label);
            if (m.Mode == MutantMode.Ping) MutantCaption(dc, $"{m.Throws}/6", label);
            if (m.Final) MutantCaption(dc, $"{m.CoreHits}/6", label);
        }
        if (sim.MutantNoise)
        {
            for (int i = 0; i < 4; i++) dc.DrawEllipse(MutationSoft, MutationLine, new Point(cat.X + Math.Sin(time * 3 + i * 2) * 30, cat.Y + Math.Cos(time * 2 + i) * 30), 5, 5);
            dc.DrawRoundedRectangle(MutationInk, MutationGlow, new Rect(cat.X - 25, cat.Y - 60, 50, 7), 3, 3);
            dc.DrawRectangle(MutationCore, null, new Rect(cat.X - 24, cat.Y - 59, 48 * Math.Clamp(sim.MutantRubProgress / 2, 0, 1), 5));
            dc.DrawLine(MutationGlow, new Point(cat.X - 25, cat.Y - 70), new Point(cat.X + 25, cat.Y - 70));
        }
        if (sim.MutantRescuing) dc.DrawEllipse(null, MutationGlow, cat, 43, 55);
    }
}
