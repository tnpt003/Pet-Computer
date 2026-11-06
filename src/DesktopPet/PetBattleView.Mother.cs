using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

public sealed partial class BattleFieldView
{
    private static readonly Brush MotherGel = MotherGradient();
    private static readonly Brush MotherEgg = Frozen(255, 0xFF, 0xED, 0xD4);
    private static readonly Brush MotherInside = Frozen(110, 0xF8, 0xCE, 0xDB);
    private static readonly Brush MotherWarn = Frozen(45, 0xE8, 0x5A, 0x9A);
    private static readonly Brush MotherLive = Frozen(100, 0xE8, 0x5A, 0x9A);
    private static readonly Brush MotherRed = Frozen(255, 0xE5, 0x3E, 0x3E);
    private static readonly Pen MotherLine = FrozenPen(BowPink, 2);
    private static readonly Pen MotherDash = FrozenPen(BowPink, 2, new DashStyle(new double[] { 4, 3 }, 0));
    private static readonly Pen MotherCreamLine = FrozenPen(MotherEgg, 2);
    private static readonly Geometry MotherBell = Parse("M -24,-8 C -20,-12 -20,-24 -14,-30 C -8,-39 8,-39 14,-30 C 20,-24 20,-12 24,-8 C 24,-2 16,0 12,-4 C 9,0 3,0 0,-4 C -3,0 -9,0 -12,-4 C -16,0 -24,-2 -24,-8 Z");
    private static Brush MotherGradient()
    {
        var b = new RadialGradientBrush { Center = new Point(0.35, 0.3), GradientOrigin = new Point(0.28, 0.2), RadiusX = 0.75, RadiusY = 0.8 };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(220, 255, 221, 233), 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(230, 242, 163, 199), 0.65));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(245, 202, 103, 155), 1)); b.Freeze(); return b;
    }
    private static void DrawMotherBody(DrawingContext dc, Slime s, double bw, double bh, double time)
    {
        var m = s.Mother;
        int phase = m?.Phase ?? 1;
        double pulse = 1 + Math.Sin(time * 4) * 0.035;
        for (int lobe = 0; lobe < 4; lobe++)
        {
            double beat = s.State == SlimeState.Moving && m?.Protected != true ? Math.Sin(time * 7 + lobe * 1.4) : 0;
            dc.DrawEllipse(MotherGel, MotherLine, new Point((lobe - 1.5) * 11, -4 - Math.Abs(beat)), 6, 2.5 + Math.Abs(beat) * .6);
        }
        // Translucent belly: eggs share one heartbeat but bob at different phases.
        dc.DrawEllipse(MotherInside, MotherLine, new Point(0, -bh * 0.3), bw * 0.29, bh * 0.27);
        int eggs = phase == 3 ? 2 : 6;
        for (int i = 0; i < eggs; i++)
        {
            double ex = (i % 3 - 1) * bw * 0.16;
            double ey = -bh * (0.17 + i / 3 * 0.23) + Math.Sin(time * 2.5 + i) * 0.7;
            if (s.HitFlash > 0) ex += Math.Sin(time * 65 + i) * 1.7;
            dc.DrawEllipse(MotherEgg, MotherLine, new Point(ex, ey), 3.3 * pulse, 4.3 * pulse);
            dc.DrawEllipse(BellyHeart, null, new Point(ex - 1, ey - 1), 1.2, 1.8);
        }
        double calm = phase == 3 ? (m?.Soothe ?? 0) / 100 : 0;
        bool asleep = m?.Mode == MotherMode.Sleeping || s.State == SlimeState.Defeated;
        double face = -bh * 0.7;
        foreach (int side in new[] { -1, 1 })
        {
            double ex = side * 7 + s.Facing * 1.2;
            if (asleep || calm > 0.7)
                dc.DrawLine(Outline, new Point(ex - 2.5, face), new Point(ex + 2.5, face + 1));
            else
            {
                dc.DrawEllipse(MotherEgg, null, new Point(ex, face), 3.5, 4);
                dc.DrawEllipse(BowPink, Outline, new Point(ex + s.Facing * 0.5, face + 0.5), 1.8, 2.5);
                dc.DrawEllipse(Shine, null, new Point(ex - 0.4, face - 1), 0.7, 0.9);
                if (phase == 2) dc.DrawLine(Outline, new Point(ex - side * 3, face - 3), new Point(ex + side * 3, face - 5));
            }
            dc.DrawEllipse(BowPink, null, new Point(side * 11, face + 7), 3, 1.5);
        }
        dc.DrawEllipse(BellyHeart, Outline, new Point(s.Facing, face + 9), 2.8, asleep ? 0.7 : 1.7);
        double reaching = m is { Move: >= 0, Zones.Count: > 0 } ? Math.Sin(Math.Min(1, m.Zones[0].Age / Math.Max(0.1, m.Zones[0].Warn)) * Math.PI / 2) : 0;
        foreach (int side in new[] { -1, 1 })
        {
            double handX = side * (bw * 0.22 + reaching * 6);
            double handY = -bh * (0.34 + reaching * 0.16);
            dc.DrawEllipse(MotherGel, Outline, new Point(handX, handY), 6, 3.5);
            dc.DrawLine(MotherCreamLine, new Point(handX - 2, handY - 1), new Point(handX + 2, handY - 1));
        }
        dc.PushTransform(new TranslateTransform(-s.Facing * 5, -bh * 0.98));
        dc.PushTransform(new RotateTransform(s.State == SlimeState.Stunned ? Math.Sin(time * 13) * 35 : phase == 2 ? -12 * s.Facing : Math.Sin(time * 2) * 4));
        dc.PushTransform(new ScaleTransform(1.5, phase == 3 ? 1.1 : 1.5));
        dc.DrawGeometry(null, Halo, Bow); dc.DrawGeometry(BowPink, Outline, Bow);
        dc.DrawEllipse(MotherEgg, Outline, new Point(0, 0), 2.5, 2.5);
        if (phase == 3) dc.DrawLine(MotherCreamLine, new Point(4, -3), new Point(6, 3));
        dc.Pop(); dc.Pop(); dc.Pop();
        if (asleep)
            for (int i = 0; i < 4; i++) dc.DrawEllipse(null, MotherCreamLine, new Point((i - 1.5) * 11, -bh - 5 - ((time * 15 + i * 9) % 25)), 2 + i % 2, 2 + i % 2);
    }
    public static void DrawMotherAir(DrawingContext dc, BattleSim sim, Func<double, double, Point> at, Point cat, double time)
    {
        foreach (var s in sim.Slimes)
        {
            if (!s.Targetable || s.Mother is not { } m) continue;
            foreach (var z in m.Zones)
            {
                if (z.Age >= z.Warn + z.Life && z.Art != MotherArt.Heartbeat) continue;
                if (z.Thread)
                {
                    var from = at(z.FromX, z.FromH); var end = at(z.ToX, z.ToH);
                    dc.DrawLine(Halo, from, end); dc.DrawLine(z.Live ? MotherLine : MotherDash, from, end);
                    if (z.Live) { var tip = new Point(end.X * .9 + from.X * .1, end.Y * .9 + from.Y * .1); DrawMotherHeart(dc, tip, 1.4, false); }
                    continue;
                }
                Point a = at(z.X - z.Width / 2, z.H + z.Height), b = at(z.X + z.Width / 2, z.H);
                var r = new Rect(a, b);
                bool warning = z.Age < z.Warn;
                if (warning && z.Moving)
                {
                    var from = at(z.FromX, z.FromH + z.Height / 2); var to = at(z.ToX, z.ToH + z.Height / 2);
                    dc.DrawLine(Halo, from, to); dc.DrawLine(MotherDash, from, to);
                }
                if (z.Age >= z.Warn + z.Life)
                {
                    if (z.Art == MotherArt.Heartbeat && z.Age < z.Warn + z.Life + 1.8)
                    { var belly = at(s.X, s.Height * 0.4); dc.DrawEllipse(null, Halo, belly, 29, 24); dc.DrawEllipse(null, MotherLine, belly, 29, 24); DrawMotherHeart(dc, belly, 1.8, false); }
                    continue;
                }
                dc.DrawRoundedRectangle(warning ? MotherWarn : MotherLive, Halo, r, 8, 8);
                dc.DrawRoundedRectangle(null, warning ? MotherDash : MotherLine, r, 8, 8);
                if (z.Art is MotherArt.Egg or MotherArt.Shell)
                {
                    if (r.Height > 180)
                    {
                        double y = r.Top + ((time * 220) % Math.Max(1, r.Height - 20));
                        dc.DrawEllipse(MotherEgg, Outline, new Point(r.Left + r.Width / 2, y + 10), 11, 15);
                    }
                    else dc.DrawEllipse(MotherEgg, Outline, new Point(r.Left + r.Width / 2, r.Top + r.Height / 2), Math.Min(15, r.Width * 0.4), Math.Min(20, r.Height * 0.42));
                }
                else if (z.Art == MotherArt.Ribbon)
                {
                    for (double y = r.Top + 15; y < r.Bottom; y += 65)
                        dc.DrawLine(warning ? MotherDash : MotherLine, new Point(r.Left, y), new Point(r.Right, Math.Min(r.Bottom, y + 25)));
                }
                else if (z.Art == MotherArt.Hug)
                {
                    var p = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
                    foreach (int side in new[] { -1, 1 })
                    {
                        dc.PushTransform(new RotateTransform(side * (warning ? -30 : 25), p.X + side * 22, p.Y));
                        dc.DrawEllipse(MotherGel, Outline, new Point(p.X + side * 22, p.Y), 12, 28); dc.Pop();
                    }
                    DrawMotherHeart(dc, p, 1.3, false);
                }
                else if (z.Art == MotherArt.Child)
                {
                    if (z.ActorId == 0)
                    {
                        var p = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
                        dc.DrawEllipse(MotherGel, Outline, p, 22, 18);
                        dc.DrawEllipse(MotherEgg, null, new Point(p.X - 7, p.Y - 5), 4, 5);
                        dc.DrawEllipse(MotherEgg, null, new Point(p.X + 7, p.Y - 5), 4, 5);
                    }
                }
                else if (z.Art == MotherArt.Hand)
                {
                    var p = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
                    dc.DrawEllipse(MotherGel, Outline, p, Math.Min(28, r.Width * 0.3), Math.Min(22, r.Height * 0.3));
                    for (int finger = -1; finger <= 1; finger++) dc.DrawEllipse(MotherGel, Outline, new Point(p.X + finger * 11, p.Y - 17), 6, 10);
                }
                else if (z.Art == MotherArt.Flood)
                {
                    for (double x = r.Left + 10; x < r.Right; x += 17)
                        dc.DrawEllipse(MotherGel, MotherLine, new Point(x, r.Top + r.Height * .65 + Math.Sin(time * 6 + x * .1) * 5), 13, r.Height * .3);
                }
                else
                    for (double x = r.Left + 20; x < r.Right; x += 65) DrawMotherHeart(dc, new Point(x, r.Top + r.Height / 2), 1, false);
            }
            foreach (var t in m.Tokens)
            {
                if (t.Done) continue;
                var p = at(t.X, t.H);
                if (m.Mode == MotherMode.HangingEggs)
                {
                    if (!t.Falling) { dc.DrawLine(Halo, p, at(t.AnchorX, t.AnchorH)); dc.DrawLine(MotherLine, p, at(t.AnchorX, t.AnchorH)); }
                    dc.DrawEllipse(null, Halo, p, 15, 20); dc.DrawEllipse(MotherEgg, MotherLine, p, 15, 20);
                    if (t.Hits > 0) dc.DrawLine(MotherLine, new Point(p.X - 8, p.Y - 9), new Point(p.X + 4, p.Y + 10));
                }
                else if (m.Mode == MotherMode.Lullaby) DrawMotherHeart(dc, p, 1.6, t.Bad);
                else
                {
                    dc.DrawEllipse(MotherGel, Outline, p, 16, 13);
                    dc.DrawEllipse(MotherEgg, null, new Point(p.X - 4, p.Y - 3), 2, 3);
                    dc.DrawEllipse(MotherEgg, null, new Point(p.X + 4, p.Y - 3), 2, 3);
                }
            }
            if (m.Mode == MotherMode.Cradle)
            {
                double h = Math.Min(sim.SkyHeight - 90, 300);
                var p = at(m.RingX, h);
                dc.DrawLine(MotherLine, new Point(p.X - 50, p.Y), at(m.RingX - 50, h + 95));
                dc.DrawLine(MotherLine, new Point(p.X + 50, p.Y), at(m.RingX + 50, h + 95));
                dc.DrawRoundedRectangle(MotherInside, Halo, new Rect(p.X - 60, p.Y - 20, 120, 42), 18, 18);
                dc.DrawRoundedRectangle(null, MotherLine, new Rect(p.X - 60, p.Y - 20, 120, 42), 18, 18);
                DrawMotherHeart(dc, p, 1, false);
            }
            if (m.Mode == MotherMode.Stroke)
            {
                var p = at(m.RingX, 8); dc.DrawEllipse(MotherWarn, Halo, p, 65, 12); dc.DrawEllipse(null, MotherLine, p, 65, 12);
                var back = at(s.X, s.Height * 0.85);
                dc.DrawLine(Halo, new Point(back.X - 30, back.Y - 15), new Point(back.X + 30, back.Y - 15));
                dc.DrawLine(MotherLine, new Point(back.X - 30, back.Y - 15), new Point(back.X + 30, back.Y - 15));
                double tip = back.X + m.StrokeDirection * 30;
                dc.DrawLine(MotherLine, new Point(tip, back.Y - 15), new Point(tip - m.StrokeDirection * 10, back.Y - 22));
                dc.DrawLine(MotherLine, new Point(tip, back.Y - 15), new Point(tip - m.StrokeDirection * 10, back.Y - 8));
            }
        }
        if (sim.MotherWrapLeft > 0)
        {
            foreach (var s in sim.Slimes)
                if (s.Targetable && s.Mother is { Mode: MotherMode.Combat, Phase: >= 2 })
                { dc.DrawLine(Halo, cat, at(s.X, s.Height * .5)); dc.DrawLine(MotherDash, cat, at(s.X, s.Height * .5)); break; }
            dc.DrawRoundedRectangle(MotherInside, Halo, new Rect(cat.X - 33, cat.Y - 18, 66, 38), 9, 9);
            for (int i = 0; i < 3; i++) dc.DrawLine(MotherLine, new Point(cat.X - 30, cat.Y - 12 + i * 12), new Point(cat.X + 30, cat.Y - 6 + i * 12));
            for (int i = 0; i < 3; i++) dc.DrawEllipse(i < sim.MotherWrapClicks ? BowPink : MotherEgg, Outline, new Point(cat.X - 12 + i * 12, cat.Y + 28), 4, 4);
        }
    }
    private static void DrawMotherHeart(DrawingContext dc, Point p, double scale, bool bad)
    {
        dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, p.X, p.Y));
        dc.DrawGeometry(null, Halo, Heart); dc.DrawGeometry(bad ? MotherRed : BowPink, Outline, Heart);
        if (bad) { dc.DrawLine(MotherCreamLine, new Point(-3, -2), new Point(3, 4)); dc.DrawLine(MotherCreamLine, new Point(3, -2), new Point(-3, 4)); }
        else dc.DrawLine(MotherCreamLine, new Point(0, -2), new Point(0, 3));
        dc.Pop();
    }
}
