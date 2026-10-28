using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// The Laser and the Fan on the field (PetBattle.Tools.cs): the red dot with its glow and a ring
// counting its time down, a red glint over each slime chasing it; the fan's spinning blades where it
// blows from and streaks of wind running out both ways to its reach. Outlines and small marks only,
// so clicks still reach the slimes.
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
            double left = Math.Clamp(sim.LaserRemaining / Math.Max(0.01, sim.Settings.LaserSeconds), 0, 1);
            DrawArc(dc, LaserRing, new Point(x, y), 14, left);
        }
        if (sim.FanX is double from)
        {
            double x = from - OriginX, range = sim.Settings.FanRange;
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
