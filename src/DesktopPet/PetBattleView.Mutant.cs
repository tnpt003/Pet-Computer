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
    private static readonly Pen MutationEyeLine = FrozenPen(MutationInk, 1.25);
    private static readonly Pen MutationGlow = FrozenPen(MutationCore, 3);
    private static readonly Pen MutationWarn = FrozenPen(Game.Warning, 2, new DashStyle(new double[] { 4, 3 }, 0));
    private static readonly Geometry MutationBody = Parse("M -20,0 C -28,-3 -22,-14 -17,-19 C -17,-36 -3,-37 1,-25 C 10,-31 22,-28 20,-17 C 28,-8 25,1 17,0 Z");
    private static readonly Geometry MutationBody2 = Parse("M -20,0 C -28,-2 -21,-12 -19,-16 C -31,-12 -34,-20 -29,-23 C -25,-26 -22,-19 -19,-23 C -19,-37 -5,-39 0,-27 C 9,-35 21,-30 19,-20 C 26,-28 34,-25 31,-19 C 29,-15 24,-20 23,-13 C 29,-3 24,1 17,0 Z");
    private static readonly Geometry MutationBody3 = Parse("M -23,0 C -33,-3 -25,-10 -20,-13 C -29,-19 -25,-27 -18,-24 C -23,-39 -9,-41 -5,-29 C -1,-21 1,-37 10,-32 C 16,-29 16,-23 18,-22 C 20,-35 32,-34 28,-24 C 23,-17 36,-18 31,-10 C 28,-5 22,-8 26,-2 C 27,3 8,-2 0,0 Z");
    private static Geometry MutationBodyFor(Slime s) => MutantShellBody(s);

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
        dc.DrawGeometry(null, MutationEyeLine, path);
    }
    private static void DrawMutantFace(DrawingContext dc, Slime s, double bw, double bh, double time)
    {
        var m = s.Mutant;
        double open = MutantOpen(m);
        var action = MutantActionPose(s);
        if (s.State == SlimeState.Defeated)
        {
            double collapse = Math.Clamp(s.StateTime / 0.8, 0, 1);
            DrawLivingMutantCore(dc, new Point(0, -12), Math.Max(0.05, 1 - collapse), 0, 6);
            DrawGelBurst(dc, new Point(0, -12), 28, collapse, 2);
            return;
        }
        double drift = Math.Clamp(-(m?.Velocity ?? 0) / 90, -1, 1);
        dc.PushClip(MutantShellBody(s));
        for (int i = 0; i < 4 + (m?.Phase ?? 1); i++)
        {
            Point bubble = new(Math.Sin(i * 2.1 + time * 0.5) * bw * 0.32, -bh * (0.22 + (i % 4) * 0.17));
            dc.DrawEllipse(MutationSoft, GelVein, bubble, 2 + i % 2, 2.5 + i % 2);
        }
        // Internal fibres connect the body to the same seed that is later exposed.
        for (int side = -1; side <= 1; side += 2)
        {
            var fibre = new StreamGeometry();
            using (var g = fibre.Open()) { g.BeginFigure(new Point(side * bw * 0.35, -bh * 0.62), false, false); g.QuadraticBezierTo(new Point(side * 7, -bh * 0.7), new Point(side * 4, -bh * 0.24), true, false); }
            dc.DrawGeometry(null, GelVein, fibre);
        }
        dc.Pop();
        if (open > 0.01 && m != null) DrawExposedMutantCore(dc, s, time, open);
        double eyeY = -bh * 0.62 + open * bh * 0.2;
        double separation = 8 + open * 13;
        for (int side = -1; side <= 1; side += 2)
        {
            Point eye = new(side * separation + drift, eyeY + (side > 0 ? -1 : 0));
            if (action.Shape == MutantShape.Eye && action.Strike > 0.15)
                dc.DrawEllipse(MutationInk, GelVein, eye, 3.5, 2.5);
            else MutationSpiral(dc, eye, (4.4 + action.Gather * 1.1) * (1 - open * 0.15), side * time * 0.28);
            if (open > 0.5) dc.DrawLine(MutationLine, new Point(eye.X - 4, eye.Y - 1), new Point(eye.X + 3, eye.Y + 1));
        }
        if (open < 0.08)
        {
            double mouthX = s.Facing * (3 + action.Strike * 5), mouthY = -bh * 0.32;
            bool emitting = (action.Gather > 0 || action.Strike > 0) && action.Shape is MutantShape.Goo or MutantShape.Breath or MutantShape.Waterfall;
            bool ping = m is { Mode: MutantMode.Ping, Projectile: true };
            bool roar = m?.Mode == MutantMode.Roar;
            if (emitting || ping || roar)
            {
                double amount = roar ? 1 : ping ? Math.Clamp(1.2 + m!.ProjectileAge, 0.1, 1) : action.Gather * 0.5 + action.Strike;
                DrawGel(dc, new Point(mouthX, mouthY), 5 + amount * 4, 4 + amount * 3, time, 4);
                dc.DrawEllipse(MutationInk, GelVein, new Point(mouthX + s.Facing * amount * 2, mouthY), 3 + amount * 3, 2 + amount * (roar ? 7 : 3));
                if (roar)
                    for (int i = 0; i < 2; i++) dc.DrawGeometry(null, GelVein, GelMembrane(new Point(mouthX, mouthY), 10 + ((time * 7 + i * 5) % 12), 9 + ((time * 6 + i * 4) % 9), time, i));
            }
            else
            {
                var mouth = new StreamGeometry();
                using (var g = mouth.Open())
                {
                    g.BeginFigure(new Point(-11, mouthY - 1), false, false);
                    g.BezierTo(new Point(-6, mouthY + 9), new Point(-2, mouthY - 8), new Point(3, mouthY + 1), true, false);
                    g.BezierTo(new Point(8, mouthY + 5), new Point(13, mouthY - 4), new Point(11, mouthY - 6), true, false);
                }
                dc.DrawGeometry(null, MutationLine, mouth);
            }
        }
        if (m?.Mode is MutantMode.Transition or MutantMode.Absorb || action.Shape is MutantShape.Hand or MutantShape.Fan)
            for (int side = -1; side <= 1; side += 2)
            {
                Point root = new(side * bw * 0.32, -bh * 0.35);
                Point palm = new(side * (bw * 0.48 + action.Strike * 5), m?.Mode == MutantMode.Transition ? -2 : -bh * 0.42);
                DrawGelStream(dc, root, palm, time, side);
                DrawGel(dc, palm, 5 + action.Gather * 2, 4, time, side);
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
                    DrawGel(dc, new Point(x, floor - 3), sideW / 4, live ? 10 : 4, time, i, live);
                    if (clock is >= 3.8 and < 5) dc.DrawEllipse(null, MutationWarn, new Point(x, floor - 4), sideW / 4, 12);
                    for (int k = 0; k < 4; k++)
                        dc.DrawEllipse(MutationCore, null, new Point(x + (k - 1.5) * sideW / 11, floor - 5 - (live ? 5 * Math.Abs(Math.Sin(time * 3 + k)) : 0)), 2.5, 2);
                    if (m.Mode == MutantMode.Transition || m.TerrainCleared)
                    {
                        Point origin = m.TerrainCleared ? new(s.X - OriginX, floor - s.Height * 0.36)
                            : new(s.X - OriginX + (i < 2 ? -1 : 1) * s.Width * 0.48, floor - 4);
                        double t = (time * 0.7 + i * 0.23) % 1;
                        if (m.TerrainCleared) t = 1 - t;
                        DrawGelStream(dc, origin, new Point(x, floor - 4), time, i * 0.2, m.TerrainCleared);
                        dc.DrawEllipse(MutationCore, MutationLine, new Point(origin.X + (x - origin.X) * t, origin.Y + (floor - origin.Y) * t - Math.Sin(t * Math.PI) * 25), 5, 5);
                    }
                }
                dc.Pop();
            }
            foreach (var b in m.FailedCores)
            {
                double x = b.X - OriginX;
                double grow = Math.Clamp((time - b.BornAt) / 0.5, 0, 1);
                double shrink = m.Mode == MutantMode.Absorb ? Math.Clamp(m.Clock / 3, 0.08, 1) : 1;
                DrawGel(dc, new Point(x, floor - 14 * grow * shrink), 30 * grow * shrink, 22 * grow * shrink, time, b.X);
                DrawLivingMutantCore(dc, new Point(x, floor - 17 * shrink), 0.8 * shrink, time);
                for (int rib = 0; rib < 3; rib++) dc.DrawLine(GelVein, new Point(x - 17 + rib * 14, floor - 28 * shrink), new Point(x - 11 + rib * 12, floor - 4));
                if (time - b.BornAt < 0.7) DrawGelBurst(dc, new Point(x, floor - 5), 45, (time - b.BornAt) / 0.7);
                if (m.Mode == MutantMode.Absorb)
                {
                    Point end = new(s.X - OriginX, floor - s.Height * 0.5);
                    DrawGelStream(dc, new Point(x, floor - 15), end, time, x * 0.01);
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
    private static void MutantCatIcon(DrawingContext dc, Point p, bool sword, int facing = 1)
    {
        dc.DrawEllipse(MutationCore, MutationLine, p, 9, 7);
        dc.DrawLine(MutationLine, new Point(p.X - 8, p.Y - 3), new Point(p.X - 6, p.Y - 12));
        dc.DrawLine(MutationLine, new Point(p.X - 6, p.Y - 12), new Point(p.X - 1, p.Y - 6));
        dc.DrawLine(MutationLine, new Point(p.X + 8, p.Y - 3), new Point(p.X + 6, p.Y - 12));
        dc.DrawLine(MutationLine, new Point(p.X + 6, p.Y - 12), new Point(p.X + 1, p.Y - 6));
        if (sword)
        {
            dc.DrawLine(MutationGlow, new Point(p.X + facing * 13, p.Y + 5), new Point(p.X + facing * 24, p.Y - 12));
            dc.DrawLine(MutationLine, new Point(p.X + facing * 12, p.Y - 1), new Point(p.X + facing * 23, p.Y + 5));
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
                DrawMutantAttackArt(dc, z, rect, MutantEmission(s, z.Shape, at), time);
                Point center = new(rect.Left + width / 2, rect.Top + height / 2);
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
                double recoil = b.Solved ? Math.Clamp(1 - (time - b.SolvedAt) / 0.6, 0, 1) : 0;
                double sprout = Math.Clamp((time - b.BornAt) / 0.5, 0.1, 1);
                DrawGel(dc, new Point(0, 0), (30 + recoil * 8) * sprout, (b.Solved ? 5 + recoil * 9 : b.Side == 0 ? 16 : 24) * sprout, time, i, active);
                if (recoil > 0) DrawGelBurst(dc, new Point(0, -8), 48, 1 - recoil, i);
                if (b.Side == 0 && !b.Solved)
                    for (int ring = 0; ring < 2; ring++) dc.DrawEllipse(null, GelVein, new Point(0, -3 - ring * 3), 20 - ring * 6, 5);
                else if (!b.Solved)
                    for (int rib = -1; rib <= 1; rib++) dc.DrawLine(GelVein, new Point(rib * 13, 8), new Point(rib * 8, -17));
                dc.DrawGeometry(active ? MutationCore : MutationInk, GelVein, GelMembrane(new Point(0, -5), b.Side == 0 ? 13 : 9, b.Solved ? 2 : 7, time * 0.3, i));
                if (b.Solved)
                {
                    dc.DrawLine(MutationGlow, new Point(-8, -7), new Point(-2, -2));
                    dc.DrawLine(MutationGlow, new Point(-2, -2), new Point(9, -14));
                }
                if (b.Side != 0) dc.Pop(); dc.Pop();
                if (active)
                {
                    var guide = MutantGuide(p, b.Side, at(sim.Left, 0).X, at(sim.Right, 0).X, at(sim.Left, sim.SkyHeight).Y);
                    MutantCatIcon(dc, guide.Icon, b.Side != 0, b.Side == 0 ? 1 : b.Side);
                    dc.DrawLine(MutationGlow, guide.Start, guide.End);
                    Vector direction = guide.End - guide.Start; direction.Normalize();
                    Vector wing = new(-direction.Y * 5, direction.X * 5);
                    dc.DrawLine(MutationGlow, guide.End, guide.End - direction * 9 + wing);
                    dc.DrawLine(MutationGlow, guide.End, guide.End - direction * 9 - wing);
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
                    double r = Math.Abs(at(bag.X + 85, bag.H).X - at(bag.X, bag.H).X) * fade;
                    dc.DrawEllipse(bag.Spraying && !m.TerrainCleared ? MutationSoft : null, bag.Warning ? MutationWarn : MutationGlow, p, r, r);
                    if (bag.Spraying)
                        for (int i = 0; i < 12; i++)
                        {
                            double a = i * Math.PI / 6, d = 30 + ((time * 80 + i * 17) % 55);
                            dc.DrawEllipse(MutationGreen, MutationLine, new Point(p.X + Math.Cos(a) * d, p.Y + Math.Sin(a) * d), 4, 6);
                        }
                }
                DrawGel(dc, p, 43 * pulse * fade, 24 / pulse * fade, time, bag.Lane, bag.Spraying && !m.TerrainCleared);
                if (bag.Warning) DrawGelBurst(dc, p, 45, (time * 0.7) % 1, bag.Lane);
                dc.Pop();
            }
            if (m.Projectile || m.ReturnLeft > 0)
            {
                Point p = at(m.CoreX, m.CoreH);
                if (m.Projectile && m.ProjectileAge < 0)
                {
                    Point end = at(m.AimX, 0), start = at(m.FromX, m.FromH);
                    var arc = new StreamGeometry();
                    using (var g = arc.Open()) { g.BeginFigure(start, false, false); g.QuadraticBezierTo(new Point((start.X + end.X) / 2, Math.Min(start.Y, end.Y) - Math.Min(180, Math.Abs(end.X - start.X) * 0.4)), end, true, false); }
                    dc.DrawGeometry(null, MutationWarn, arc); dc.DrawEllipse(null, MutationGlow, end, 28, 8);
                    MutantCatIcon(dc, new Point(end.X, end.Y - 60), false);
                }
                DrawGel(dc, p, 28, 28, time, 6, m.ReturnLeft > 0);
                DrawGelBurst(dc, p, 40, (time * 2) % 1, 2);
                DrawLivingMutantCore(dc, p, 1.8, time);
            }
            if (m.Mode == MutantMode.Absorb)
            {
                Point center = at(s.X, s.Height * 0.5);
                double k = Math.Clamp(1 - m.Clock / 3, 0, 1);
                dc.DrawEllipse(null, m.Misses == 6 ? MutationGlow : MutationWarn, center, 30 + k * (sim.Right - sim.Left), 20 + k * sim.SkyHeight);
                double wave = (time * 0.45) % 1;
                dc.PushOpacity((1 - wave) * 0.6);
                dc.DrawGeometry(null, m.Misses == 6 ? MutationGlow : GelVein, GelMembrane(center, 30 + k * (sim.Right - sim.Left), 20 + k * sim.SkyHeight, time * 0.3, m.Misses));
                dc.Pop();

            }

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
