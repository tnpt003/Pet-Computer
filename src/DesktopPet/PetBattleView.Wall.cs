using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M15 (PetBattle.Wall.cs). The ice slime: pale blue, wrapped in a see-through ice shell with two
// glints (dripping and dashed while the spray has it soft); broken, only a few shards stick up on its
// head, growing back. The wall climber: olive with a row of spikes on its back and clawed paws; up the
// field's edge it leans into it, a shadow on the floor under it; aiming, a ring fills on the floor where
// its spikes will land; a spike is lobbed from it to there. Drawing only.
public sealed partial class BattleFieldView
{
    private static readonly Brush IceFill = Frozen(105, 0xE6, 0xF7, 0xFF);
    private static readonly Pen IceRim = FrozenPen(Frozen(255, 0x6F, 0xB8, 0xD8), 2.2);
    private static readonly Pen IceSoft = FrozenPen(Frozen(220, 0x6F, 0xB8, 0xD8), 2, new DashStyle(new double[] { 2, 2 }, 0));
    private static readonly Pen IceGlint = FrozenPen(Frozen(235, 255, 255, 255), 2);
    private static readonly Brush ShardFill = Frozen(230, 0xD8, 0xF2, 0xFF);
    private static readonly Brush DripFill = Frozen(220, 0x7F, 0xC8, 0xEA);
    private static readonly Brush SpikeFill = Frozen(255, 0x7A, 0x6A, 0x3A);
    private static readonly Brush SpikeTip = Frozen(255, 0xF4, 0xEC, 0xD8);
    private static readonly Geometry Shard = Parse("M -3,0 L 0,-9 L 3,0 Z");
    private static readonly Geometry BackSpike = Parse("M -4,0 L 0,-8 L 4,0 Z");
    // A falling spike (pointing down, its tip at the origin).
    private static readonly Geometry FallingSpike = Parse("M 0,0 L -4.5,-16 L 4.5,-16 Z");

    // How far up the wall climber is drawn (its bottom over the floor), and its lean into its edge.
    private static double WallLift(Slime slime) => slime.Type.Cling != null ? slime.Altitude : 0;
    private static double WallLean(Slime slime) => slime.OnWall ? slime.WallSide * 8 : 0;

    // Marks in body coordinates (from DrawSlime): the ice shell or its shards; the climber's spikes and paws.
    private static void DrawWallMarks(DrawingContext dc, Slime slime, double bw, double bh, double time)
    {
        if (slime.Type.Marks == "ice")
        {
            if (slime.Iced)
            {
                var shell = Body(bw * 1.16, bh * 1.14);
                dc.DrawGeometry(IceFill, slime.Wet ? IceSoft : IceRim, shell);
                dc.DrawLine(IceGlint, new Point(-bw * 0.36, -bh * 0.5), new Point(-bw * 0.26, -bh * 0.8));
                dc.DrawLine(IceGlint, new Point(bw * 0.3, -bh * 0.3), new Point(bw * 0.36, -bh * 0.45));
                // Soft from the spray: drips running off the shell.
                if (slime.Wet)
                    for (int i = 0; i < 3; i++)
                    {
                        double fall = (time * 1.6 + i * 0.33) % 1;
                        dc.DrawEllipse(DripFill, null, new Point((i - 1) * bw * 0.32, -bh * 0.15 + fall * 10), 1.8, 2.6);
                    }
                return;
            }
            // Broken: shards on its head, growing back.
            double regrow = BattleSim.IceRegrowTime(slime), k = regrow > 0 ? 0.4 + 0.6 * Math.Clamp(1 - slime.IceClock / regrow, 0, 1) : 0.4;
            for (int i = -1; i <= 1; i++)
            {
                dc.PushTransform(new MatrixTransform(k, 0, 0, k * (i == 0 ? 1.2 : 1), i * bw * 0.18, -bh * 0.92 + Math.Abs(i) * 3));
                dc.DrawGeometry(ShardFill, IceRim, Shard);
                dc.Pop();
            }
        }
        else if (slime.Type.Marks == "spikes")
        {
            // A row of spikes over its back, and clawed paws on the side it grips (its edge, or its feet).
            for (int i = -2; i <= 2; i++)
            {
                double a = i * 0.42, r = bh * 0.86;
                dc.PushTransform(new MatrixTransform(Math.Cos(a), Math.Sin(a), -Math.Sin(a), Math.Cos(a), Math.Sin(a) * bw * 0.48, -bh * 0.08 - Math.Cos(a) * r));
                dc.DrawGeometry(SpikeFill, ThinInk, BackSpike);
                dc.Pop();
            }
            int grip = slime.OnWall ? slime.WallSide : 0;
            foreach (int side in new[] { -1, 1 })
            {
                var paw = grip != 0 ? new Point(grip * bw * 0.5, -bh * (side < 0 ? 0.25 : 0.7)) : new Point(side * bw * 0.32, -2);
                dc.DrawEllipse(SpikeTip, ThinInk, paw, 4.5, 3.4);
                for (int c = -1; c <= 1; c++)
                    if (grip != 0) dc.DrawLine(ThinInk, new Point(paw.X + grip * 2, paw.Y + c * 2.2), new Point(paw.X + grip * 5, paw.Y + c * 2.6));
                    else dc.DrawLine(ThinInk, new Point(paw.X + c * 2.2, paw.Y + 2), new Point(paw.X + c * 2.6, paw.Y + 5));
            }
        }
    }

    // Under the slimes: the rings where climbers' spikes will land, filling up as they aim.
    private void DrawSpikeAims(DrawingContext dc, double floor)
    {
        for (int i = 0; i < sim.Slimes.Count; i++)
        {
            var s = sim.Slimes[i];
            if (!s.Aiming || s.Type.Cling is not ClingSpec cling || !s.OnWall) continue;
            double k = Math.Clamp(1 - s.AimLeft / cling.Aim, 0, 1), r = cling.Radius;
            foreach (double target in sim.SpikeTargets(s))
            {
                var at = new Point(target - OriginX, floor - 3);
                dc.DrawEllipse(null, Halo, at, r, 8);
                dc.DrawEllipse(null, WarningFill, at, Math.Max(1, r * k), Math.Max(1, 8 * k));
                dc.DrawEllipse(null, WarningEdge, at, r, 8);
            }
        }
    }

    // A spike in flight: lobbed in a low arc from the climber down to its spot (kept inside the strip),
    // pointing the way it goes (up off the wall, tipping down as it falls).
    private void DrawSpike(DrawingContext dc, BattleGoo g, double floor)
    {
        double k = g.Progress, fromY = floor - g.FromHeight, toY = floor - 4, arc = Math.Min(50, floor - g.FromHeight - 12);
        double x = g.FromX + (g.X - g.FromX) * k - OriginX, y = fromY + (toY - fromY) * k - 4 * arc * k * (1 - k);
        double angle = Math.Atan2(g.X - g.FromX, toY - fromY - 4 * arc * (1 - 2 * k)) * 180 / Math.PI;
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new RotateTransform(-angle));
        dc.DrawGeometry(null, Halo, FallingSpike);
        dc.DrawGeometry(SpikeFill, Outline, FallingSpike);
        dc.DrawLine(ThinInk, new Point(0, -2), new Point(0, -12));
        dc.Pop();
        dc.Pop();
    }
}
