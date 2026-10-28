using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M11 (PetBattle.Fuse.cs, PetBattle.Split.cs; the merge bar and the rider come from PetBattleView.Links.cs).
// The fusing slime: a small pink one with a bobbing feeler; the giant it merges into: three feelers and
// a heavy brow. The bomb slime: a steel cap with a wick on its head, a spark running down the wick
// while it burns, and a red ring on the floor the size of its blast (filling as the fuse burns). The
// splitting slime: a zigzag seam down its middle; its halves: a plaster on the head. Outlines only on the
// floor, so clicks still go through to the desktop.
public sealed partial class BattleFieldView
{
    private static readonly Brush FeelerTip = Frozen(255, 0xFF, 0xD3, 0xE6);
    private static readonly Pen FeelerPen = FrozenPen(PetUi.ArtInk, 1.6);
    private static readonly Pen ThinInk = FrozenPen(PetUi.ArtInk, 1);
    private static readonly Brush CapSteel = Frozen(255, 0x55, 0x5E, 0x70);
    private static readonly Pen WickPen = FrozenPen(Frozen(255, 0x6B, 0x4A, 0x2E), 2.2);
    private static readonly Brush SparkCore = Frozen(255, 0xFF, 0xF2, 0x9E);
    private static readonly Brush SparkGlow = Frozen(170, 0xFF, 0x8A, 0x2A);
    private static readonly Pen BlastIdle = FrozenPen(Frozen(150, 0xE0, 0x4A, 0x4A), 2, new DashStyle(new double[] { 2, 3 }, 0));
    private static readonly Pen SeamPen = FrozenPen(Frozen(255, 0x5A, 0x3E, 0x28), 1.8);
    private static readonly Brush PlasterFill = Frozen(255, 0xF7, 0xE8, 0xD2);
    private static readonly Geometry Seam = Parse("M 0,-34 L -3,-27 L 3,-20 L -3,-13 L 3,-6 L 0,0");
    private static readonly Geometry Wick = Parse("M 0,-36 C 2,-41 7,-41 8,-46");

    // A bomb's blast ring on the floor (screen coordinates), under the body: faint dashes when it is
    // not lit, filling red as the fuse burns.
    private void DrawBlastRing(DrawingContext dc, Slime slime, double x, double floor)
    {
        if (slime.Type.Fuse == null || !slime.Targetable || slime.Riding) return;
        double r = BattleSim.FuseRadius(slime);
        var at = new Point(x, floor - 3);
        dc.DrawEllipse(null, Halo, at, r, 8);
        if (!slime.Lit) { dc.DrawEllipse(null, BlastIdle, at, r, 8); return; }
        double k = slime.FuseTotal > 0 ? Math.Clamp(1 - slime.FuseLeft / slime.FuseTotal, 0, 1) : 1;
        dc.DrawEllipse(null, WarningFill, at, Math.Max(1, r * k), Math.Max(1, 8 * k));
        dc.DrawEllipse(null, WarningEdge, at, r, 8);
    }

    // The marks of the M11 kinds (body coordinates).
    private static void DrawSwarmMarks(DrawingContext dc, Slime slime, double time)
    {
        switch (slime.Type.Marks)
        {
            case "swarm":
                DrawFeeler(dc, 0, Math.Sin(time * 6 + slime.Id) * 2);
                break;
            case "giant":
                for (int i = -1; i <= 1; i++) DrawFeeler(dc, i * 12, Math.Sin(time * 4 + slime.Id + i) * 1.5);
                double look = slime.Facing * 3;
                dc.DrawLine(Ink, new Point(-12 + look, -bhBrow), new Point(-3 + look, -bhBrow + 3));
                dc.DrawLine(Ink, new Point(12 + look, -bhBrow), new Point(3 + look, -bhBrow + 3));
                break;
            case "bomb":
                DrawWick(dc, slime, time);
                break;
            case "split":
                dc.DrawGeometry(null, Halo, Seam);
                dc.DrawGeometry(null, SeamPen, Seam);
                break;
            case "half":
                dc.PushTransform(new RotateTransform(-20 * slime.Facing, 6 * slime.Facing, -27));
                dc.DrawRoundedRectangle(null, Halo, new Rect(6 * slime.Facing - 9, -31, 18, 8), 2.5, 2.5);
                dc.DrawRoundedRectangle(PlasterFill, ThinInk, new Rect(6 * slime.Facing - 9, -31, 18, 8), 2.5, 2.5);
                dc.DrawLine(SeamPen, new Point(6 * slime.Facing - 2, -28.5), new Point(6 * slime.Facing + 2, -25.5));
                dc.DrawLine(SeamPen, new Point(6 * slime.Facing - 2, -25.5), new Point(6 * slime.Facing + 2, -28.5));
                dc.Pop();
                break;
        }
    }

    // Where the giant's brows sit (body coordinates, over its eyes).
    private const double bhBrow = 25;

    private static void DrawFeeler(DrawingContext dc, double x, double sway)
    {
        var tip = new Point(x + sway, -42);
        dc.DrawLine(Halo, new Point(x, -33), tip);
        dc.DrawLine(FeelerPen, new Point(x, -33), tip);
        dc.DrawEllipse(null, Halo, tip, 4.2, 4.2);
        dc.DrawEllipse(FeelerTip, ThinInk, tip, 3.8, 3.8);
    }

    // The steel cap and the wick; a lit one's spark runs down the wick as the fuse burns.
    private static void DrawWick(DrawingContext dc, Slime slime, double time)
    {
        dc.DrawGeometry(null, Halo, Wick);
        dc.DrawGeometry(null, WickPen, Wick);
        dc.DrawRoundedRectangle(CapSteel, Outline, new Rect(-5, -38.5, 10, 5), 1.5, 1.5);
        if (!slime.Lit) return;
        double k = slime.FuseTotal > 0 ? Math.Clamp(slime.FuseLeft / slime.FuseTotal, 0, 1) : 0;
        // Along the wick's curve, from its end (k = 1) down to the cap (k = 0).
        double u = k, v = 1 - u;
        double sx = 3 * v * v * u * 2 + 3 * v * u * u * 7 + u * u * u * 8;
        double sy = v * v * v * -36 + 3 * v * v * u * -41 + 3 * v * u * u * -41 + u * u * u * -46;
        double flicker = 1 + 0.25 * Math.Sin(time * 40 + slime.Id);
        dc.DrawEllipse(SparkGlow, null, new Point(sx, sy), 6 * flicker, 6 * flicker);
        dc.DrawEllipse(SparkCore, null, new Point(sx, sy), 3 * flicker, 3 * flicker);
    }
}
