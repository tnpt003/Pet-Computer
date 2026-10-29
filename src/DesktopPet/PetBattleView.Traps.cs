using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M09 (PetBattle.Spit.cs, PetBattle.Spring.cs). The spitter: a short spout on its head that swells
// while it winds up, an orange ring on the floor where its goo will land (filling as it comes), the drop
// flying in an arc with its shadow growing under it, and the puddle it leaves: a glossy rim with arrows
// (dashes go faster through it), fading as it dries. The spring: a wide, flat mat with a coil and a pad on
// top, a gold ring on the floor all the time (red and filling while it squeezes, grey dashes while it rests
// or lies flattened). Outlines only on the floor, so clicks still go through to the desktop.
public sealed partial class BattleFieldView
{
    private static readonly Brush SpitOrange = Frozen(255, 0xFF, 0x9A, 0x3C);
    private static readonly Pen SpitEdge = FrozenPen(SpitOrange, 2.5, new DashStyle(new double[] { 3, 2 }, 0));
    private static readonly Pen SpitFill = FrozenPen(Frozen(210, 0xFF, 0x9A, 0x3C), 3.5);
    private static readonly Pen SpoutPen = FrozenPen(PetUi.ArtInk, 1.8);
    private static readonly Pen PuddleShine = FrozenPen(Frozen(220, 255, 255, 255), 1.6);
    private static readonly Pen DropShadow = FrozenPen(Frozen(110, 0x2A, 0x1F, 0x16), 2.5);
    private static readonly Pen SpringGold = FrozenPen(Frozen(255, 0xF2, 0xB8, 0x1E), 2.5, new DashStyle(new double[] { 4, 2 }, 0));
    private static readonly Pen SpringIdle = FrozenPen(Frozen(200, 0x9A, 0x92, 0x86), 2, new DashStyle(new double[] { 2, 3 }, 0));
    private static readonly Pen CoilPen = FrozenPen(Frozen(255, 0x8C, 0x85, 0x7A), 2.6);
    private static readonly Brush PadFill = Frozen(255, 0xE8, 0x5D, 0x5D);
    // The coil on the spring's back (body coordinates, before the spring's squash): a zigzag up to the pad.
    private static readonly Geometry Coil = Parse("M 0,-30 L -6,-33 L 6,-36 L -6,-39 L 6,-42 L 0,-45");
    private static readonly Geometry Chevron = Parse("M -4,-4 L 2,0 L -4,4");

    // A spring's shape: wide and low, squashing while it squeezes, stretched just after it springs,
    // very flat (and wider) once flattened.
    private static void SpringShape(Slime slime, ref double sx, ref double sy)
    {
        if (slime.Type.Spring is not SpringSpec spring) return;
        sx *= 1.2; sy *= 0.62;
        switch (slime.Spring)
        {
            case SpringState.Squeezing:
                double k = Math.Clamp(1 - slime.SpringClock / spring.Compress, 0, 1);
                sx *= 1 + 0.12 * k; sy *= 1 - 0.3 * k;
                break;
            case SpringState.Resting:
                double rest = slime.Elite != null ? spring.EliteRest : spring.Rest, since = rest - slime.SpringClock;
                if (since < 0.35) { double up = Math.Sin(since / 0.35 * Math.PI); sx *= 1 - 0.12 * up; sy *= 1 + 0.6 * up; }
                break;
            case SpringState.Flat:
                sx *= 1.15; sy *= 0.55;
                break;
        }
    }

    // The ring on the floor (screen coordinates), under the body.
    private void DrawSpringRing(DrawingContext dc, Slime slime, double x, double floor)
    {
        if (slime.Type.Spring is not SpringSpec spring || !slime.Targetable) return;
        double r = BattleSim.SpringRadius(slime);
        var at = new Point(x, floor - 3);
        dc.DrawEllipse(null, Halo, at, r, 8);
        if (slime.Spring == SpringState.Squeezing)
        {
            double k = Math.Clamp(1 - slime.SpringClock / spring.Compress, 0, 1);
            dc.DrawEllipse(null, WarningFill, at, Math.Max(1, r * k), Math.Max(1, 8 * k));
            dc.DrawEllipse(null, WarningEdge, at, r, 8);
        }
        else dc.DrawEllipse(null, slime.Spring == SpringState.Ready ? SpringGold : SpringIdle, at, r, 8);
    }

    // On the spring's back (body coordinates): the coil and its red pad (the coil hidden once flattened).
    private static void DrawSpringCoil(DrawingContext dc, Slime slime)
    {
        if (slime.Spring == SpringState.Flat) { dc.DrawEllipse(PadFill, Outline, new Point(0, -33), 7, 2.5); return; }
        dc.DrawGeometry(null, Halo, Coil);
        dc.DrawGeometry(null, CoilPen, Coil);
        dc.DrawEllipse(PadFill, Outline, new Point(0, -46), 8, 3);
    }

    // On the spitter's head (body coordinates): a short spout leaning forward, its mouth swelling while
    // it winds up a spit.
    private void DrawSpout(DrawingContext dc, Slime slime, Brush fill)
    {
        double swell = slime.Spitting ? Math.Clamp(slime.StateTime / sim.Windup(slime), 0, 1) : 0;
        dc.PushTransform(new ScaleTransform(slime.Facing, 1));
        var spout = new StreamGeometry();
        using (var c = spout.Open())
        {
            c.BeginFigure(new Point(-5, -31), true, true);
            c.LineTo(new Point(1, -44), true, false);
            c.LineTo(new Point(9, -42), true, false);
            c.LineTo(new Point(5, -30), true, false);
        }
        spout.Freeze();
        dc.DrawGeometry(null, Halo, spout);
        dc.DrawGeometry(fill, Outline, spout);
        dc.DrawEllipse(slime.Spitting ? SpitOrange : fill, Outline, new Point(5, -43.5), 4.5 + 2 * swell, 2.6 + 1.2 * swell);
        dc.Pop();
        // Puffed cheeks while it winds up.
        if (swell > 0) foreach (double cx in new[] { -13.0, 13.0 }) dc.DrawEllipse(fill, SpoutPen, new Point(cx, -14), 4 * swell + 1, 3.5 * swell + 1);
    }

    // Before the slimes: puddles on the floor, the rings where drops will land, and their shadows.
    private void DrawSpitBehind(DrawingContext dc, double floor, double time)
    {
        for (int i = 0; i < sim.Puddles.Count; i++)
        {
            var p = sim.Puddles[i];
            double x = p.X - OriginX, fade = Math.Clamp(p.Remaining / 1.0, 0.25, 1);
            dc.PushOpacity(fade);
            var at = new Point(x, floor - 3);
            dc.DrawEllipse(null, Halo, at, p.Radius, 7);
            dc.DrawEllipse(null, PuddleRim, at, p.Radius, 7);
            dc.DrawEllipse(null, PuddleShine, new Point(x - p.Radius * 0.35, floor - 5), p.Radius * 0.25, 1.8);
            // Arrows running along it: a dash through it goes faster.
            double run = time * 30 % 16;
            for (double ax = -p.Radius * 0.6 + run; ax < p.Radius * 0.6; ax += 16)
                foreach (int way in new[] { 1, -1 })
                {
                    dc.PushTransform(new MatrixTransform(way, 0, 0, 0.7, x + way * ax, floor - 3));
                    dc.DrawGeometry(null, PuddleRim, Chevron);
                    dc.Pop();
                }
            dc.Pop();
        }
        for (int i = 0; i < sim.Slimes.Count; i++)
        {
            var s = sim.Slimes[i];
            if (!s.Spitting) continue;
            double k = Math.Clamp(s.StateTime / sim.Windup(s), 0, 1), r = s.Type.Spit!.Radius;
            foreach (double target in sim.SpitTargets(s))
            {
                var at = new Point(target - OriginX, floor - 3);
                dc.DrawEllipse(null, Halo, at, r, 8);
                dc.DrawEllipse(null, SpitFill, at, Math.Max(1, r * k), Math.Max(1, 8 * k));
                dc.DrawEllipse(null, SpitEdge, at, r, 8);
            }
        }
        for (int i = 0; i < sim.Goos.Count; i++)
        {
            var g = sim.Goos[i];
            double k = g.Progress, r = g.Radius;
            var at = new Point(g.X - OriginX, floor - 3);
            dc.DrawEllipse(null, Halo, at, r, 8);
            dc.DrawEllipse(null, SpitEdge, at, r, 8);
            dc.DrawEllipse(null, DropShadow, at, 5 + 9 * k, 2 + 2 * k);
        }
        DrawSpikeAims(dc, floor);
    }

    // Over the slimes: the drops in flight, an arc from the spitter's head to where they land.
    private void DrawGoos(DrawingContext dc, double floor)
    {
        for (int i = 0; i < sim.Goos.Count; i++)
        {
            var g = sim.Goos[i];
            if (g.Spike) { DrawSpike(dc, g, floor); continue; } // A wall climber's (1.5 M15).
            if (g.Rain) { DrawRainDrop(dc, g, floor); continue; } // A boss's goo rain (1.5 M18).
            double k = g.Progress, x = g.FromX + (g.X - g.FromX) * k - OriginX;
            double y = floor - (40 * (1 - k) + 4 * k) - 4 * 70 * k * (1 - k);
            var fill = KindBrush(g.Tint ?? "#C38BE8");
            dc.DrawEllipse(null, Halo, new Point(x, y), 6.5, 7.5);
            dc.DrawEllipse(fill, Outline, new Point(x, y), 6, 7);
            dc.DrawEllipse(Shine, null, new Point(x - 2, y - 3), 1.8, 1.4);
        }
    }

    // A drop landing: a few droplets of its colour thrown out low over the floor.
    private static void DrawSplat(DrawingContext dc, BattleEffect effect, double x, double floor, double k)
    {
        var drop = KindBrush(effect.Tint ?? "#C38BE8");
        for (int i = 0; i < 5; i++)
        {
            double a = -Math.PI * (0.12 + 0.76 * i / 4), reach = 26 * (1 - (1 - k) * (1 - k));
            var c = new Point(x + Math.Cos(a) * reach * 1.4, floor - 6 + Math.Sin(a) * reach * 0.7 + 18 * k * k);
            dc.DrawEllipse(drop, Outline, c, 3.6 - 1.4 * k, 3.6 - 1.4 * k);
        }
    }

    private static readonly Pen PuddleRim = FrozenPen(Frozen(255, 0x9A, 0x5C, 0xD0), 2.4);
}
