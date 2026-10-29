using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// The Laser and the Fan on the field (PetBattle.Tools.cs): the red dot with its glow and a ring
// counting its time down, a red glint over each slime chasing it; the fan's spinning blades where it
// blows from and streaks of wind running out both ways to its reach. 1.5 M06: the Grabber's claw on
// its cable from the top of the strip, the Scissors stroke being drawn (a dashed cut line with an open
// pair of blades at its end), the spray's nozzle with its fan of water and drops on soaked slimes.
// Outlines and small marks only, so clicks still reach the slimes.
public sealed partial class BattleFieldView
{
    private static readonly Brush LaserGlow = Frozen(80, 0xFF, 0x3B, 0x30);
    private static readonly Brush LaserCore = Frozen(255, 0xFF, 0x3B, 0x30);
    private static readonly Brush LaserHot = Frozen(255, 0xFF, 0xE8, 0xE0);
    private static readonly Pen LaserRing = FrozenPen(Frozen(220, 0xFF, 0x6A, 0x5A), 2);
    private static readonly Pen WindPen = FrozenPen(Frozen(190, 0xF4, 0xFB, 0xFF), 2.2);
    private static readonly Pen WindInk = FrozenPen(Frozen(90, 0x2A, 0x4A, 0x66), 3.6);
    private static readonly Brush FanBlade = Frozen(235, 0x8F, 0xD3, 0xF5);
    private static readonly Brush FanHub = Frozen(255, 0xFF, 0xD2, 0x3F);
    private static readonly Pen FanInk = FrozenPen(PetUi.ArtInk, 1.4);
    private static readonly Pen CablePen = FrozenPen(Frozen(230, 0x6B, 0x5A, 0x4A), 2);
    private static readonly Brush ClawMetal = Frozen(255, 0xC9, 0xD3, 0xDC);
    private static readonly Pen ClawPen = FrozenPen(Frozen(255, 0x8A, 0x96, 0xA3), 3);
    private static readonly Pen StrokeInk = FrozenPen(Frozen(150, 0x2A, 0x1F, 0x16), 4);
    private static readonly Pen StrokePen = FrozenPen(Frozen(240, 0xF4, 0xFB, 0xFF), 2, new DashStyle(new double[] { 3, 2 }, 0));
    private static readonly Brush BladeFill = Frozen(255, 0xE6, 0xEC, 0xF2);
    private static readonly Brush HandleFill = Frozen(255, 0xE8, 0x5D, 0x5D);
    private static readonly Brush WaterDrop = Frozen(220, 0x5C, 0xB8, 0xFF);
    private static readonly Brush WaterShine = Frozen(230, 0xE8, 0xF6, 0xFF);
    private static readonly Pen SprayArea = FrozenPen(Frozen(140, 0x5C, 0xB8, 0xFF), 1.6, new DashStyle(new double[] { 2, 3 }, 0));
    private static readonly Brush BottleFill = Frozen(240, 0x7F, 0xD0, 0xF0);
    private static readonly Brush NozzleFill = Frozen(255, 0xFF, 0xD2, 0x3F);

    private void DrawTools(DrawingContext dc, double floor, double time)
    {
        if (sim.LaserX is double dot)
        {
            double x = dot - OriginX, y = floor - 4, pulse = 1 + 0.15 * Math.Sin(time * 14);
            for (int i = 0; i < sim.Slimes.Count; i++)
                if (sim.Slimes[i] is { Lured: true } s) DrawLureGlint(dc, s.X - OriginX, Top(s, floor), time);
            dc.DrawEllipse(LaserGlow, null, new Point(x, y), 11 * pulse, 6 * pulse);
            dc.DrawEllipse(LaserCore, null, new Point(x, y), 4, 3);
            dc.DrawEllipse(LaserHot, null, new Point(x, y - 0.5), 1.6, 1.2);
            double left = Math.Clamp(sim.LaserRemaining / Math.Max(0.01, sim.LaserSeconds), 0, 1);
            DrawArc(dc, LaserRing, new Point(x, y), 14, left);
        }
        if (sim.FanX is double from)
        {
            double x = from - OriginX, range = sim.FanRange;
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 4; k++)
                {
                    double run = (time * 260 + k * 53 + (side > 0 ? 27 : 0)) % range;
                    double fade = 1 - run / range, len = 10 + 16 * fade, y = floor - 10 - k % 3 * 13 - (k == 3 ? 6 : 0);
                    var a = new Point(x + side * (14 + run), y);
                    var b = new Point(a.X + side * len, y - 2 * Math.Sin(time * 9 + k));
                    dc.DrawLine(WindInk, a, b);
                    dc.DrawLine(WindPen, a, b);
                }
            DrawFan(dc, new Point(x, floor - 26), time);
        }
        for (int i = 0; i < sim.Slimes.Count; i++)
        {
            var s = sim.Slimes[i];
            if (s.Wet && s.Targetable) DrawWetDrops(dc, s, s.X - OriginX, floor, time);
            if (s.Grabbed) DrawClaw(dc, s.X - OriginX, Top(s, floor));
        }
        if (sim.SprayX is double spray) DrawSpray(dc, new Point(spray - OriginX, floor - sim.SprayHeight), sim.Settings.SprayRange, time);
        if (strokeFrom is Point start) DrawStroke(dc, start, strokeTo);
    }

    // A cable from the top of the strip down to two prongs holding the slime's head.
    private static void DrawClaw(DrawingContext dc, double x, double top)
    {
        dc.DrawLine(CablePen, new Point(x, 0), new Point(x, top - 8));
        dc.DrawLine(ClawPen, new Point(x, top - 6), new Point(x - 9, top + 4));
        dc.DrawLine(ClawPen, new Point(x, top - 6), new Point(x + 9, top + 4));
        dc.DrawEllipse(ClawMetal, FanInk, new Point(x, top - 8), 5, 4);
    }

    // The stroke drawn so far, with an open pair of scissors at the cursor.
    private static void DrawStroke(DrawingContext dc, Point from, Point to)
    {
        dc.DrawLine(StrokeInk, from, to);
        dc.DrawLine(StrokePen, from, to);
        double angle = Math.Atan2(to.Y - from.Y, to.X - from.X) * 180 / Math.PI;
        dc.PushTransform(new TranslateTransform(to.X, to.Y));
        dc.PushTransform(new RotateTransform(angle));
        for (int side = -1; side <= 1; side += 2)
        {
            dc.PushTransform(new RotateTransform(side * 16));
            dc.DrawEllipse(BladeFill, FanInk, new Point(7, 0), 8, 2.2);
            dc.DrawEllipse(HandleFill, FanInk, new Point(-6, 0), 3.6, 3);
            dc.Pop();
        }
        dc.DrawEllipse(NozzleFill, FanInk, new Point(0, 0), 1.6, 1.6);
        dc.Pop();
        dc.Pop();
    }

    // The nozzle, its reach as a dotted ring and drops flying out all around (not a fill: clicks pass).
    private static void DrawSpray(DrawingContext dc, Point at, double range, double time)
    {
        dc.DrawEllipse(null, SprayArea, at, range, range * 0.55);
        for (int k = 0; k < 10; k++)
        {
            double phase = (time * 2.2 + k * 0.1) % 1, angle = k * 0.628 + Math.Sin(time * 3 + k) * 0.2;
            var p = new Point(at.X + Math.Cos(angle) * range * phase, at.Y + Math.Sin(angle) * range * 0.55 * phase);
            dc.DrawEllipse(WaterDrop, null, p, 2.4 * (1 - phase * 0.5), 2.4 * (1 - phase * 0.5));
        }
        dc.DrawRoundedRectangle(BottleFill, FanInk, new Rect(at.X - 5, at.Y - 4, 10, 14), 3, 3);
        dc.DrawRectangle(NozzleFill, FanInk, new Rect(at.X - 3, at.Y - 9, 6, 5));
    }

    // Two drops sliding down a soaked slime.
    private static void DrawWetDrops(DrawingContext dc, Slime s, double x, double floor, double time)
    {
        double top = Top(s, floor);
        for (int k = 0; k < 2; k++)
        {
            double slide = (time * 0.8 + k * 0.5 + s.Id * 0.13) % 1;
            var p = new Point(x - s.Width * 0.22 + k * s.Width * 0.4, top + s.Height * (0.2 + 0.5 * slide));
            dc.DrawEllipse(WaterDrop, FanInk, p, 2.6, 3.4);
            dc.DrawEllipse(WaterShine, null, new Point(p.X - 0.8, p.Y - 1.2), 0.8, 1);
        }
    }

    // A small red sparkle over a slime chasing the dot.
    private static void DrawLureGlint(DrawingContext dc, double x, double top, double time)
    {
        double bob = 2 * Math.Sin(time * 10 + x * 0.05);
        dc.DrawEllipse(LaserGlow, null, new Point(x, top - 8 + bob), 5, 5);
        dc.DrawEllipse(LaserCore, null, new Point(x, top - 8 + bob), 2.2, 2.2);
    }

    private static void DrawFan(DrawingContext dc, Point at, double time)
    {
        dc.PushTransform(new TranslateTransform(at.X, at.Y));
        dc.PushTransform(new RotateTransform(time * 720 % 360));
        for (int i = 0; i < 3; i++)
        {
            dc.PushTransform(new RotateTransform(i * 120));
            dc.DrawEllipse(FanBlade, FanInk, new Point(0, -7), 4, 7);
            dc.Pop();
        }
        dc.Pop();
        dc.DrawEllipse(FanHub, FanInk, new Point(0, 0), 2.6, 2.6);
        dc.Pop();
    }

    // A ring from 12 o'clock clockwise, this fraction of the way round.
    private static void DrawArc(DrawingContext dc, Pen pen, Point c, double r, double fraction)
    {
        if (fraction <= 0) return;
        if (fraction >= 0.999) { dc.DrawEllipse(null, pen, c, r, r); return; }
        double end = -Math.PI / 2 + fraction * 2 * Math.PI;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(c.X, c.Y - r), false, false);
            ctx.ArcTo(new Point(c.X + r * Math.Cos(end), c.Y + r * Math.Sin(end)), new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }
}
