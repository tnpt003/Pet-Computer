using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M17, redrawn 2026-11-05 (PetBattle.Zip.cs). The Zip Slime is an archive come alive, a folder-yellow file
// with a zipper through its whole body, no crown: small and squared, belted shut, the zipper closed (4 KB);
// then a fat folder, a belt burst and the zipper opening at the foot with a page poking out (2 MB); then an
// overstuffed bag, its zipper wide open like a toothy grin, pages spilling (9 GB). Its riddles draw here too:
// the padlock and the gold spot to drop the cat on, the bomb's bar and its zipper tab. Its roped helpers hang
// on goo ropes to it, drawn as a tether's. Drawing only.
public sealed partial class BattleFieldView
{
    private static readonly Pen ZipTeeth = FrozenPen(Frozen(255, 0x8C, 0x6A, 0x2E), 1.6);
    private static readonly Pen ZipTrack = FrozenPen(Frozen(255, 0x6E, 0x52, 0x22), 1.2);
    private static readonly Brush ZipTab = Frozen(255, 0xC9, 0xC2, 0xB6);
    private static readonly Pen StrapPen = FrozenPen(Frozen(255, 0x8C, 0x6A, 0x2E), 3);
    private static readonly Brush PageFill = Frozen(255, 0xFF, 0xFB, 0xF2);
    private static readonly Pen PageLine = FrozenPen(Frozen(200, 0x9A, 0x8E, 0x7E), 1);
    private static readonly Pen GapTooth = FrozenPen(Frozen(255, 0xC9, 0xC2, 0xB6), 1.5);
    private static readonly Brush ZipInside = Frozen(255, 0x4A, 0x36, 0x1C);
    private static readonly Brush BombRed = Frozen(255, 0xE5, 0x3E, 0x3E), BombTrack = Frozen(230, 0x33, 0x2A, 0x24), TagInk = Frozen(255, 0x4A, 0x36, 0x1C);
    private static readonly Pen SpotPen = FrozenPen(Frozen(255, 0xFF, 0xC8, 0x3A), 3), TabCord = FrozenPen(Frozen(255, 0x8C, 0x6A, 0x2E), 2.4), Shackle = FrozenPen(Frozen(255, 0x6E, 0x52, 0x22), 2);
    private static readonly Geometry Page = Parse("M -6,-15 L 3,-15 L 6,-12 L 6,-2 L -6,-2 Z");
    private static readonly Geometry PageEar = Parse("M 3,-15 L 3,-12 L 6,-12 Z");
    // Squared and tight, a file's tab on top; fat; overstuffed, bulging past its width.
    private static readonly Geometry[] ZipBodies =
    {
        Parse("M -19,0 L -19.5,-25 Q -19.5,-29 -15.5,-29 L -8,-29 L -5,-33 L 9,-33 L 11,-29 L 15.5,-29 Q 19.5,-29 19.5,-25 L 19,0 Z"),
        Parse("M -22,0 C -23,-8 -23,-20 -23,-26 Q -23,-30 -19,-30 L -9,-30 L -6,-34 L 11,-34 L 13,-30 L 19,-30 Q 23,-30 23,-26 C 23,-20 23,-8 22,0 Z"),
        Parse("M -22,0 C -29,-8 -28,-24 -19,-29 C -15,-31 -10,-31 -8,-31 L -6,-34 L 10,-34 L 12,-31 C 16,-31 19,-30 21,-28 C 28,-22 29,-8 22,0 Z")
    };
    private static readonly Geometry[] ZipGaps =
    {
        Parse("M 0,-15 C 3,-11 7,-6 8,0 L -8,0 C -7,-6 -3,-11 0,-15 Z"),
        Parse("M 0,-14 C 8,-13 15,-9 17,-1 L -17,-1 C -15,-9 -8,-13 0,-14 Z")
    };
    private static readonly (double X, double Y)[][] GapTeeth =
    {
        new[] { (1.6, -13.0), (3.6, -10.0), (5.4, -7.0), (6.8, -4.0), (7.6, -1.5) },
        new[] { (3.5, -13.4), (7.0, -12.3), (10.0, -10.4), (12.6, -8.0), (14.6, -5.2), (16.0, -2.4) }
    };
    private static readonly string[] ZipSizes = { "4 KB", "2 MB", "9 GB" };

    private static Geometry ZipBody(Slime slime) => ZipBodies[Math.Clamp(slime.ZipStage, 0, ZipBodies.Length - 1)];

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

    // The bomb swells it, faster towards the end; letting its air out it goes long and flat.
    private static void ZipShape(Slime slime, double time, ref double sx, ref double sy)
    {
        if (BattleSim.BombFill(slime) is double fill)
        {
            double throb = 1 + 0.1 * fill + 0.02 * Math.Sin(time * (8 + 12 * fill));
            sx *= throb; sy *= throb;
        }
        if (slime.Deflating > 0) { sx *= 1.2; sy *= 0.8; }
    }

    // Marks in body coordinates (from DrawSlime; the origin at the middle of its bottom, up is negative).
    private void DrawZipMarks(DrawingContext dc, Slime slime)
    {
        if (slime.Type.Marks == "packet") DrawPacket(dc, slime, slime.StateTime);
        if (slime.Type.Marks != "zip") return;
        switch (Math.Clamp(slime.ZipStage, 0, 2))
        {
            case 0:
                DrawZipperLine(dc, -30, -1);
                DrawZipTab(dc, -31);
                // One belt bursts with each pin of the press riddle.
                double[] belts = { -26.0, -9.0 };
                for (int i = Math.Min(slime.Pins, belts.Length); i < belts.Length; i++) DrawBelt(dc, belts[i], 19);
                break;
            case 1:
                DrawZipperLine(dc, -33, -15);
                DrawZipTab(dc, -34);
                DrawZipGap(dc, 0);
                DrawPage(dc, 3, -1, 16, 0.8); // Spilling out of the gap.
                // The burst belt: two ends hanging off the sides, under the eyes.
                foreach (int side in new[] { -1, 1 })
                {
                    var from = new Point(side * 23, -12);
                    var to = new Point(side * 14, -8);
                    dc.DrawLine(Halo, from, to);
                    dc.DrawLine(StrapPen, from, to);
                }
                break;
            default:
                DrawZipperLine(dc, -33, -14);
                if (BattleSim.BombTab(slime) == null) DrawZipTab(dc, -34);
                DrawZipGap(dc, 1);
                DrawPage(dc, -7, -1, -24, 0.72);
                DrawPage(dc, 7, -1, 22, 0.72);
                break;
        }
        if (sim.ShieldPhase(slime) is { Press: true }) DrawPadlock(dc);
    }

    private static void DrawZipperLine(DrawingContext dc, double top, double bottom)
    {
        dc.DrawLine(ZipTrack, new Point(0, top), new Point(0, bottom));
        int side = 1;
        for (double y = top + 2; y < bottom; y += 2.6, side = -side)
            dc.DrawLine(ZipTeeth, new Point(0, y), new Point(side * 2.6, y));
    }

    private static void DrawZipTab(DrawingContext dc, double y) =>
        dc.DrawRoundedRectangle(ZipTab, Outline, new Rect(-2.6, y, 5.2, 8), 1.4, 1.4);

    private static void DrawBelt(DrawingContext dc, double y, double half)
    {
        dc.DrawLine(Halo, new Point(-half, y), new Point(half, y));
        dc.DrawLine(StrapPen, new Point(-half, y), new Point(half, y));
        dc.DrawRoundedRectangle(ZipTab, Outline, new Rect(-3.6, y - 2.8, 7.2, 5.6), 1, 1);
    }

    private static void DrawPage(DrawingContext dc, double x, double y, double tilt, double size)
    {
        dc.PushTransform(new MatrixTransform(size, 0, 0, size, x, y));
        dc.PushTransform(new RotateTransform(tilt));
        dc.DrawGeometry(PageFill, Outline, Page);
        dc.DrawGeometry(ZipTab, Outline, PageEar);
        dc.DrawLine(PageLine, new Point(-4, -11), new Point(2, -11));
        dc.DrawLine(PageLine, new Point(-4, -8), new Point(4, -8));
        dc.Pop();
        dc.Pop();
    }

    // The zipper's open part: dark inside (over the pages' feet), teeth along both lips.
    private static void DrawZipGap(DrawingContext dc, int size)
    {
        dc.DrawGeometry(ZipInside, Outline, ZipGaps[size]);
        foreach (var (x, y) in GapTeeth[size])
            foreach (int side in new[] { -1, 1 })
                dc.DrawLine(GapTooth, new Point(side * x, y), new Point(side * (x - 1.8), y + 2));
    }

    private static void DrawPadlock(DrawingContext dc)
    {
        var arc = new Rect(-3.4, -47, 6.8, 9);
        dc.DrawRoundedRectangle(null, Shackle, arc, 3.4, 3.4);
        dc.DrawRoundedRectangle(Gold, Outline, new Rect(-5.5, -42.5, 11, 8.5), 1.6, 1.6);
        dc.DrawEllipse(ZipInside, null, new Point(0, -38.5), 1.3, 1.3);
    }

    // On the floor, under it: where to drop the cat on the press riddle's roll (gold: good news, not a warning).
    private void DrawZipFloor(DrawingContext dc, Slime slime, double ground, double time)
    {
        if (slime.Type.Zip is not ZipSpec zip || BattleSim.PressSpot(slime) is not double spot) return;
        double x = spot - OriginX, rx = slime.HitWidth / 2 + zip.Press.Reach, bob = 3 * Math.Sin(time * 7);
        dc.DrawEllipse(null, Halo, new Point(x, ground - 4), rx, 9);
        dc.DrawEllipse(null, SpotPen, new Point(x, ground - 4), rx, 9);
        var tip = new Point(x, ground - 16 + bob);
        foreach (var (from, to) in new[] { (new Point(x, ground - 46 + bob), tip), (new Point(x - 8, ground - 24 + bob), tip), (new Point(x + 8, ground - 24 + bob), tip) })
        {
            dc.DrawLine(Halo, from, to);
            dc.DrawLine(SpotPen, from, to);
        }
    }

    // Over it: its file size; the pins so far; the bomb's bar, the pulls so far and the zipper tab to go through.
    private void DrawZipOver(DrawingContext dc, Slime slime, double x, double ground, double top, double time)
    {
        if (slime.Type.Zip is not ZipSpec zip || slime.State == SlimeState.Defeated) return;
        // Kept inside the field however big it swells (the bar overlaps its head then).
        double y = Math.Max(12, top - 18);
        if (slime.State != SlimeState.Windup && BattleSim.BombFill(slime) == null) DrawSizeTag(dc, ZipSizes[Math.Clamp(slime.ZipStage, 0, ZipSizes.Length - 1)], x, y);
        if (sim.ShieldPhase(slime) is { Press: true }) DrawPips(dc, x, y - 13, slime.Pins, zip.Press.Pins);
        if (slime.Deflating > 0)
            for (int i = 0; i < 3; i++)
            {
                double back = -Math.Sign(slime.DeflateVelocity) * (slime.Width / 2 + 6 + 7 * i), ay = ground - slime.LeapHeight - slime.Height * (0.3 + 0.2 * i);
                dc.DrawLine(Streak, new Point(x + back, ay), new Point(x + back - Math.Sign(slime.DeflateVelocity) * 16, ay));
            }
        if (BattleSim.BombFill(slime) is not double fill) return;
        const double barWidth = 104, barHeight = 13;
        var bar = new Rect(x - barWidth / 2, y - 10, barWidth, barHeight);
        // Its clock, the time running out in its hand.
        var face = new Point(bar.Left - 14, bar.Y + barHeight / 2);
        dc.DrawEllipse(PageFill, Outline, face, 10, 10);
        double hand = fill * Math.PI * 2;
        dc.DrawLine(Shackle, face, new Point(face.X + Math.Sin(hand) * 7, face.Y - Math.Cos(hand) * 7));
        dc.DrawRoundedRectangle(BombTrack, Halo, bar, 4, 4);
        dc.DrawRoundedRectangle(BombRed, null, new Rect(bar.X + 2, bar.Y + 2, (barWidth - 4) * fill, barHeight - 4), 2.5, 2.5);
        var (shape, width, height) = NumberShape($"{(int)(fill * 100)}%", 10);
        dc.PushTransform(new TranslateTransform(x - width / 2, bar.Y + (barHeight - height) / 2));
        dc.DrawGeometry(Brushes.White, null, shape);
        dc.Pop();
        DrawPips(dc, x, bar.Y + barHeight + 7, slime.BombPulls, zip.Bomb.Pulls);
        if (BattleSim.BombTab(slime) is not (double tabX, double tabHeight)) return;
        var tab = new Point(tabX - OriginX, ground - tabHeight);
        var root = new Point(x + Math.Sign(tab.X - x) * slime.Width * 0.3, ground - slime.Height * 0.7);
        dc.DrawLine(Halo, root, tab);
        dc.DrawLine(TabCord, root, tab);
        double ring = zip.Bomb.TabRadius * 0.62 * (1 + 0.1 * Math.Sin(time * 9));
        dc.DrawEllipse(null, Halo, tab, ring, ring);
        dc.DrawEllipse(null, SpotPen, tab, ring, ring);
        dc.DrawRoundedRectangle(ZipTab, Outline, new Rect(tab.X - 6, tab.Y - 11, 12, 22), 3, 3);
        dc.DrawEllipse(ZipInside, null, new Point(tab.X, tab.Y - 5), 2.4, 2.4);
    }

    private static void DrawSizeTag(DrawingContext dc, string text, double x, double y)
    {
        var (shape, width, height) = NumberShape(text, 11);
        dc.DrawRoundedRectangle(PageFill, Outline, new Rect(x - width / 2 - 5, y - height / 2 - 1, width + 10, height + 2), 4, 4);
        dc.PushTransform(new TranslateTransform(x - width / 2, y - height / 2));
        dc.DrawGeometry(TagInk, null, shape);
        dc.Pop();
    }

    private static void DrawPips(DrawingContext dc, double x, double y, int done, int all)
    {
        for (int i = 0; i < all; i++)
            dc.DrawEllipse(i < done ? Gold : PageFill, Outline, new Point(x + (i - (all - 1) / 2.0) * 12, y), 4, 4);
    }
}
