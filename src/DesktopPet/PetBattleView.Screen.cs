using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 2026-11-04 (docs/GOPY-C-2026-11-04.md D4, D10): what the Scissors, the Fan and the Screenshot draw over
// the whole battle screen. The strip draws the part on it; the sky canvas (PetWindow.BattleSky.cs) the
// part above it. Drawing only.
public sealed partial class BattleFieldView
{
    private static readonly Brush ColumnAir = Frozen(46, 0xD8, 0xF0, 0xFF);
    private static readonly Pen ColumnEdge = FrozenPen(Frozen(120, 0xF4, 0xFB, 0xFF), 2, new DashStyle(new double[] { 4, 4 }, 0));
    private static readonly Brush ColumnLeaf = Frozen(230, 0x6F, 0xC9, 0x8A);
    private static readonly Brush FanBody = Frozen(255, 0x8A, 0x6A, 0x4A);
    private static readonly Pen CutDash = FrozenPen(Frozen(230, 0xFF, 0xF4, 0xF0), 2.4, new DashStyle(new double[] { 3, 3 }, 0));
    private static readonly Pen CutDone = FrozenPen(Frozen(255, 0xFF, 0x6A, 0x5A), 3);
    private static readonly Pen CutHalo = FrozenPen(Frozen(150, 0x2A, 0x1F, 0x16), 6);
    // The running scissors: about 100 DIPs from tip to rings, ScissorsReach above and below the line.
    public const double ScissorsScale = 3.2, ScissorsReach = BattleSim.CutFloorHeight;
    private static readonly Geometry ScissorBlade = FrozenGeometry("M -1,-2.2 L 18,-0.2 L 18,0.6 L -1,2.2 Z");
    private static readonly Geometry ScissorShank = FrozenGeometry("M 0,-1.3 L -8,-1 L -8,1 L 0,1.3 Z");
    private static readonly Brush ScissorHandle = Frozen(255, 0xE8, 0x5D, 0x5D);
    private static readonly Pen ScissorInk = FrozenPen(PetUi.ArtInk, 0.7);
    private static readonly Pen ScissorRing = FrozenPen(ScissorHandle, 2);
    private static readonly Pen ScissorRingInk = FrozenPen(PetUi.ArtInk, 3);
    private static readonly Brush ScreenFlashWhite = Frozen(255, 0xFF, 0xFF, 0xFF);
    private static readonly Pen FinderCorner = FrozenPen(Frozen(255, 0x3E, 0x7F, 0xB0), 6);

    // The Fan's column: pale air between dashed edges, streaks of wind and leaves falling fast, from `top`
    // down to `bottom` (this view's y), x its middle. Fades out in its last half second.
    public static void DrawColumn(DrawingContext dc, double x, double width, double top, double bottom, double time, double left)
    {
        if (bottom <= top) return;
        dc.PushOpacity(Math.Clamp(left / 0.5, 0, 1));
        var r = new Rect(x - width / 2, top, width, bottom - top);
        dc.DrawRectangle(ColumnAir, null, r);
        dc.DrawLine(ColumnEdge, new Point(r.Left, top), new Point(r.Left, bottom));
        dc.DrawLine(ColumnEdge, new Point(r.Right, top), new Point(r.Right, bottom));
        double span = bottom - top;
        for (int k = 0; k < 7; k++)
        {
            double sx = r.Left + width * (k + 0.5) / 7, fall = (time * 520 + k * 97) % (span + 60) - 30;
            double y = top + fall;
            if (y < top || y > bottom) continue;
            dc.DrawLine(WindInk, new Point(sx, y), new Point(sx, Math.Min(bottom, y + 26)));
            dc.DrawLine(WindPen, new Point(sx, y), new Point(sx, Math.Min(bottom, y + 26)));
        }
        for (int k = 0; k < 4; k++)
        {
            double ly = top + (time * 300 + k * 151) % span, lx = x + Math.Sin(time * 5 + k * 2) * width * 0.35;
            dc.PushTransform(new RotateTransform(time * 300 + k * 90, lx, ly));
            dc.DrawEllipse(ColumnLeaf, FanInk, new Point(lx, ly), 5, 2.6);
            dc.Pop();
        }
        dc.Pop();
    }

    // The ceiling fan at the top of the screen over the column: a box and three spinning blades.
    public static void DrawCeilingFan(DrawingContext dc, double x, double y, double time)
    {
        dc.DrawRoundedRectangle(FanBody, FanInk, new Rect(x - 10, y - 22, 20, 12), 3, 3);
        dc.PushTransform(new ScaleTransform(2.6, 1.1, x, y));
        DrawFan(dc, new Point(x, y), time * 1.6);
        dc.Pop();
    }

    // The Scissors' cut: dashed all along at first; then cut red behind the running scissors.
    public static void DrawCut(DrawingContext dc, BattleSim.ScissorCut cut, Func<double, double, Point> at, BattleSettings s)
    {
        var from = at(cut.From, cut.Y); var to = at(cut.To, cut.Y);
        dc.DrawLine(CutHalo, from, to);
        dc.DrawLine(CutDash, from, to);
        if (cut.Age < s.ScissorsPreview) return;
        double t = Math.Clamp((cut.Age - s.ScissorsPreview) / s.ScissorsSweep, 0, 1);
        var blade = new Point(from.X + (to.X - from.X) * t, from.Y);
        dc.DrawLine(CutDone, from, blade);
        // The big scissors themselves, centred on the line, snipping as they go, facing the way they run.
        double open = 6 + 22 * Math.Abs(Math.Sin(cut.Age * 16)), facing = to.X >= from.X ? 0 : 180;
        dc.PushTransform(new TranslateTransform(blade.X, blade.Y));
        dc.PushTransform(new ScaleTransform(ScissorsScale, ScissorsScale));
        dc.PushTransform(new RotateTransform(facing));
        foreach (int side in new[] { -1, 1 })
        {
            dc.PushTransform(new RotateTransform(side * open));
            var ring = new Point(-11.5, -side * 1.5);
            dc.DrawGeometry(BladeFill, ScissorInk, ScissorBlade);
            dc.DrawGeometry(ScissorHandle, ScissorInk, ScissorShank);
            dc.DrawEllipse(null, ScissorRingInk, ring, 4.2, 3);
            dc.DrawEllipse(null, ScissorRing, ring, 4.2, 3);
            dc.Pop();
        }
        dc.DrawEllipse(FanHub, ScissorInk, new Point(0, 0), 1.5, 1.5);
        dc.Pop(); dc.Pop(); dc.Pop();
    }

    // The Screenshot's flash over the whole screen: white fading out, a camera's four corner marks.
    public static void DrawFlash(DrawingContext dc, Rect area, double left)
    {
        double k = Math.Clamp(left / BattleSim.FlashSeconds, 0, 1);
        dc.PushOpacity(0.85 * k);
        dc.DrawRectangle(ScreenFlashWhite, null, area);
        dc.Pop();
        dc.PushOpacity(k);
        double m = 40, len = 70;
        foreach (var (cx, cy, sx, sy) in new[] { (area.Left + m, area.Top + m, 1, 1), (area.Right - m, area.Top + m, -1, 1), (area.Left + m, area.Bottom - m, 1, -1), (area.Right - m, area.Bottom - m, -1, -1) })
        {
            dc.DrawLine(FinderCorner, new Point(cx, cy), new Point(cx + sx * len, cy));
            dc.DrawLine(FinderCorner, new Point(cx, cy), new Point(cx, cy + sy * len));
        }
        dc.Pop();
    }

    // The strip's part of them (from DrawTools).
    private void DrawScreenTools(DrawingContext dc, double floor, double time)
    {
        if (sim.ColumnX is double column) DrawColumn(dc, column - OriginX, sim.ColumnWidth, 0, floor, time, sim.ColumnRemaining);
        if (sim.Cut is { } cut && cut.Y <= FieldHeight + ScissorsReach) DrawCut(dc, cut, (x, h) => new Point(x - OriginX, floor - h), sim.Settings);
    }
}
