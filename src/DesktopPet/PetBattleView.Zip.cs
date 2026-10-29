using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M17 (PetBattle.Zip.cs). The Zip Slime: a folder-yellow boss with a zipper down its front (teeth and a
// pull tab), turning over as it rolls; packed, it is drawn small with two straps round it. Its roped
// helpers hang on goo ropes to it, drawn as a tether's. A file shard: a small pale slime holding a page with
// a folded corner. Drawing only.
public sealed partial class BattleFieldView
{
    private static readonly Pen ZipTeeth = FrozenPen(Frozen(255, 0x8C, 0x6A, 0x2E), 1.6);
    private static readonly Pen ZipTrack = FrozenPen(Frozen(255, 0x6E, 0x52, 0x22), 1.2);
    private static readonly Brush ZipTab = Frozen(255, 0xC9, 0xC2, 0xB6);
    private static readonly Pen StrapPen = FrozenPen(Frozen(255, 0x8C, 0x6A, 0x2E), 2.6);
    private static readonly Brush PageFill = Frozen(255, 0xFF, 0xFB, 0xF2);
    private static readonly Pen PageLine = FrozenPen(Frozen(200, 0x9A, 0x8E, 0x7E), 1);
    private static readonly Geometry Page = Parse("M -6,-15 L 3,-15 L 6,-12 L 6,-2 L -6,-2 Z");
    private static readonly Geometry PageEar = Parse("M 3,-15 L 3,-12 L 6,-12 Z");

    // Under the slimes: every rope from a roped helper to its boss.
    private void DrawBossRopes(DrawingContext dc, double floor, double time)
    {
        for (int i = 0; i < sim.Slimes.Count; i++)
            if (sim.RopeBoss(sim.Slimes[i]) is Slime boss) DrawRope(dc, sim.Slimes[i], boss, floor, time);
    }

    // A rolling boss turns about its middle: lifted just enough that no part of its body goes under the
    // floor (sampled along its outline), so it tumbles along the floor rather than sinking into it.
    private static readonly (double X, double Y)[] RollOutline =
        { (-0.5, 0), (-0.47, -0.35), (-0.3, -0.85), (0, -1), (0.3, -0.85), (0.47, -0.35), (0.5, 0) };
    private static double RollLift(Slime slime, double degrees)
    {
        if (degrees == 0) return 0;
        double a = degrees * Math.PI / 180, sin = Math.Sin(a), cos = Math.Cos(a), w = slime.Width, h = slime.Height, low = 0;
        foreach (var (px, py) in RollOutline)
        {
            double dx = px * w, dy = py * h + h / 2; // From its middle (y down).
            low = Math.Max(low, dx * sin + dy * cos);
        }
        return Math.Max(0, low - h / 2);
    }

    // Marks in body coordinates (from DrawSlime; the origin at the middle of its bottom, up is negative).
    private static void DrawZipMarks(DrawingContext dc, Slime slime, double bw, double bh)
    {
        switch (slime.Type.Marks)
        {
            case "zip": DrawZipper(dc, slime, bw, bh); break;
            case "shard":
                dc.DrawGeometry(null, Halo, Page);
                dc.DrawGeometry(PageFill, Outline, Page);
                dc.DrawGeometry(ZipTab, Outline, PageEar);
                dc.DrawLine(PageLine, new Point(-4, -11), new Point(2, -11));
                dc.DrawLine(PageLine, new Point(-4, -8), new Point(4, -8));
                dc.DrawLine(PageLine, new Point(-4, -5), new Point(3, -5));
                break;
        }
    }

    // Down its front under the eyes: the track, teeth left and right in turn and the pull tab at the top;
    // packed, two straps round its body.
    private static void DrawZipper(DrawingContext dc, Slime slime, double bw, double bh)
    {
        double top = -bh * 0.42, bottom = -bh * 0.06;
        dc.DrawLine(ZipTrack, new Point(0, top), new Point(0, bottom));
        int side = 1;
        for (double y = top + 2; y < bottom; y += 2.6, side = -side)
            dc.DrawLine(ZipTeeth, new Point(0, y), new Point(side * 2.4, y));
        var tab = new Rect(-2.2, top - 1, 4.4, 6.5);
        dc.DrawRoundedRectangle(ZipTab, Outline, tab, 1.2, 1.2);
        if (!slime.Packed) return;
        foreach (double k in new[] { 0.32, 0.66 })
        {
            double y = -bh * k, half = bw * 0.5 * (1 - 0.55 * k * k) - 1;
            dc.DrawLine(Halo, new Point(-half, y), new Point(half, y));
            dc.DrawLine(StrapPen, new Point(-half, y), new Point(half, y));
        }
    }
}
