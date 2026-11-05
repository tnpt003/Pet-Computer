using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 2026-11-06, what shows of the Zip Slime's blows (PetBattle.ZipMoves.cs): all in field coordinates, never in its
// body's transform. Floor warnings draw in the strip; what can be high up (the zipper head, the marks, the files'
// lock on the cat) draws in the click-through canvas over the screen (SkyShotView). Same shapes as the hits.
public sealed partial class BattleFieldView
{
    private static readonly Pen ZipSeam = FrozenPen(Frozen(255, 0x6E, 0x52, 0x22), 3);
    private static readonly Pen LockedLine = FrozenPen(Game.Warning, 3);
    private static readonly Pen AimLine = FrozenPen(Frozen(150, 0xE5, 0x3E, 0x3E), 1.6);
    private static readonly Brush TideFill = Frozen(150, 0xFF, 0xFB, 0xF2);
    private static readonly Pen TideEdge = FrozenPen(Game.Warning, 2.4);
    private static readonly Brush MarkFill = Frozen(70, 0xE5, 0x3E, 0x3E);
    private static readonly Pen FlapEdge = FrozenPen(Frozen(255, 0x8C, 0x6A, 0x2E), 2.4);
    private static readonly Brush FlapFill = Frozen(235, 0xE8, 0xB8, 0x4A);
    private static readonly Geometry PaperShot = Parse("M -8,-10 L 4,-10 L 8,-6 L 8,10 L -8,10 Z");

    // Over the slimes, in the strip.
    private void DrawZipActs(DrawingContext dc, double floor, double time)
    {
        foreach (var b in sim.ZipBlows)
            if (b.Bottom <= 0) DrawZipBlow(dc, b, x => new Point(x - OriginX, floor), time);
        foreach (var s in sim.Slimes)
        {
            if (s.PacketFuse && s.Targetable && s.PacketLeft > 0 && sim.Find(s.SummonedBy) is Slime owner && BattleSim.MoveSpec(owner, ZipAct.Faulty) is ZipMoveSpec faulty)
                TacticRing(dc, s.X, floor, faulty.Radius, 1 - s.PacketLeft / faulty.Life);
            if (!s.Targetable || s.Type.Zip is not ZipSpec zip || zip.Move(s.ZipAct) is not ZipMoveSpec m) continue;
            double progress = s.ZipSeconds > 0 ? 1 - s.ZipLeft / s.ZipSeconds : 1;
            switch (s.ZipAct)
            {
                case ZipAct.Clamp: DrawClamp(dc, s, m, floor, progress); break;
                case ZipAct.Overflow: DrawOverflow(dc, s, m, floor, time); break;
                case ZipAct.Tear: if (s.ZipBeat != ZipBeat.Recover) DrawTearWarning(dc, s, m, floor, progress); break;
                case ZipAct.Tumble: DrawTumbleBack(dc, s, floor); break;
            }
        }
    }

    // Two flaps of the bale opening over the marked spot, then shut on it.
    private void DrawClamp(DrawingContext dc, Slime s, ZipMoveSpec m, double floor, double progress)
    {
        if (s.ZipBeat == ZipBeat.Recover) return;
        double x = s.ZipAimX - OriginX, open = s.ZipBeat == ZipBeat.Warn ? Math.Min(1, progress * 1.6) : 0;
        if (s.ZipBeat == ZipBeat.Warn) TacticRing(dc, s.ZipAimX, floor, m.Radius, progress);
        foreach (int side in new[] { -1, 1 })
        {
            double gap = m.Radius * open;
            var flap = new Rect(x + (side < 0 ? -gap - 22 : gap), floor - m.Height, 22, m.Height);
            dc.DrawRoundedRectangle(FlapFill, FlapEdge, flap, 4, 4);
            dc.DrawLine(StrapPen, new Point(flap.Left + 3, floor - m.Height * 0.55), new Point(flap.Right - 3, floor - m.Height * 0.55));
        }
    }

    // The tide of paper from both edges and, in gold, the gap it leaves.
    private void DrawOverflow(DrawingContext dc, Slime s, ZipMoveSpec m, double floor, double time)
    {
        if (s.ZipBeat == ZipBeat.Recover) return;
        double from = s.ZipBeat == ZipBeat.Strike ? s.ZipTear : 0.02;
        var (left, right) = sim.OverflowEdges(s, m, from);
        double top = floor - m.Height - 4 * Math.Sin(time * 9);
        var l = new Rect(0, top, Math.Max(0, left - OriginX), floor - top);
        var r = new Rect(Math.Max(0, right - OriginX), top, Math.Max(0, ActualWidth - (right - OriginX)), floor - top);
        dc.DrawRectangle(TideFill, TideEdge, l);
        dc.DrawRectangle(TideFill, TideEdge, r);
        for (double y = top + 12; y < floor - 4; y += 16)
        {
            dc.DrawLine(PageLine, new Point(l.Right - 26, y), new Point(l.Right - 6, y));
            dc.DrawLine(PageLine, new Point(r.Left + 6, y), new Point(r.Left + 26, y));
        }
        double gapL = s.ZipAimX - m.Radius - OriginX, gapR = s.ZipAimX + m.Radius - OriginX;
        foreach (double x in new[] { gapL, gapR })
        {
            dc.DrawLine(Halo, new Point(x, floor), new Point(x, floor - 58));
            dc.DrawLine(SpotPen, new Point(x, floor), new Point(x, floor - 58));
        }
        dc.DrawEllipse(null, Halo, new Point(s.ZipAimX - OriginX, floor - 4), m.Radius, 9);
        dc.DrawEllipse(null, SpotPen, new Point(s.ZipAimX - OriginX, floor - 4), m.Radius, 9);
    }

    private void DrawTearWarning(DrawingContext dc, Slime s, ZipMoveSpec m, double floor, double progress)
    {
        double x = s.X - OriginX, y = floor - 4, grow = s.ZipBeat == ZipBeat.Warn ? progress : 1;
        foreach (int side in new[] { -1, 1 })
        {
            var end = new Point(x + side * (m.Spacing * grow + m.Radius), y);
            dc.DrawLine(Halo, new Point(x, y), end);
            dc.DrawLine(WarningEdge, new Point(x, y), end);
            dc.DrawLine(WarningPen, end, new Point(end.X - side * 8, y - 6));
            dc.DrawLine(WarningPen, end, new Point(end.X - side * 8, y + 6));
        }
    }

    // The tumble's lane shows its second way too: an arrow back from its far end.
    private void DrawTumbleBack(DrawingContext dc, Slime s, double floor)
    {
        if (!s.RollBack || s.State != SlimeState.Windup) return;
        double far = s.RollGoal - OriginX, near = s.X - OriginX, way = Math.Sign(near - far), y = floor - 16;
        dc.DrawLine(Halo, new Point(far, y), new Point(near, y));
        dc.DrawLine(WarningEdge, new Point(far, y), new Point(near, y));
        dc.DrawLine(WarningPen, new Point(near, y), new Point(near - way * 8, y - 6));
        dc.DrawLine(WarningPen, new Point(near, y), new Point(near - way * 8, y + 6));
    }

    // Instead of its body: the two halves out on their way, the zipper still holding them.
    private void DrawTornBody(DrawingContext dc, Slime s, ZipMoveSpec m, double x, double floor, BattleSettings settings)
    {
        double size = s.Width / settings.SlimeWidth, reach = s.ZipTear * m.Spacing, y = floor - s.Height * 0.55;
        dc.DrawLine(Halo, new Point(x - reach, y), new Point(x + reach, y));
        dc.DrawLine(ZipSeam, new Point(x - reach, y), new Point(x + reach, y));
        var body = ZipBody(s);
        Brush fill = KindBrush(s.Enraged ? s.Type.AngryColor : s.Type.Color);
        foreach (int side in new[] { -1, 1 })
        {
            dc.PushTransform(new MatrixTransform(size, 0, 0, size, x + side * reach, floor));
            dc.PushClip(new RectangleGeometry(new Rect(side < 0 ? -40 : 0, -60, 40, 62)));
            dc.DrawGeometry(fill, Outline, body);
            dc.Pop();
            dc.DrawLine(ZipTeeth, new Point(0, -32), new Point(0, -1));
            dc.DrawEllipse(Brushes.White, Outline, new Point(side * 9, -18), 4, 4.4);
            dc.DrawEllipse(Ink.Brush, null, new Point(side * 9 + s.Facing, -18), 2, 2.4);
            dc.Pop();
        }
    }

    // A marked spot: its ring and turn number, then the blow itself.
    private static void DrawZipBlow(DrawingContext dc, ZipBlow b, Func<double, Point> at, double time)
    {
        var p = at(b.X);
        double y = p.Y - b.Bottom, k = b.Warn > 0 ? Math.Clamp(1 - b.Left / b.Warn, 0, 1) : 1, high = b.Top - b.Bottom;
        if (b.Done)
        {
            if (b.Kind == ZipBlowKind.Branch)
            {
                dc.DrawLine(Halo, new Point(p.X, y), new Point(p.X, y - high));
                dc.DrawLine(ZipSeam, new Point(p.X, y), new Point(p.X, y - high));
                foreach (int side in new[] { -1, 1 })
                    dc.DrawRoundedRectangle(FlapFill, FlapEdge, new Rect(p.X + (side < 0 ? -b.Radius : b.Radius - 22), y - high * 0.7, 22, 16), 3, 3);
                dc.DrawLine(ZipSeam, new Point(p.X - b.Radius + 11, y - high * 0.62), new Point(p.X + b.Radius - 11, y - high * 0.62));
            }
            else dc.DrawRoundedRectangle(MarkFill, TacticEdge, new Rect(p.X - b.Radius, y - high, b.Radius * 2, high), 4, 4);
            return;
        }
        if (b.Bottom <= 0)
        {
            dc.DrawEllipse(TacticZone, Halo, new Point(p.X, y - 7), b.Radius, 8);
            dc.DrawEllipse(null, TacticEdge, new Point(p.X, y - 7), b.Radius, 8);
            dc.DrawEllipse(Game.Warning, null, new Point(p.X, y - 7), Math.Max(2, b.Radius * k), 3);
        }
        else
        {
            dc.DrawRectangle(MarkFill, Halo, new Rect(p.X - b.Radius, y - high, b.Radius * 2, high));
            dc.DrawRectangle(null, TacticEdge, new Rect(p.X - b.Radius, y - high, b.Radius * 2, high));
        }
        var (shape, width, height) = NumberShape(b.Order.ToString(System.Globalization.CultureInfo.InvariantCulture), 13);
        double top = b.Bottom <= 0 ? y - 44 - 3 * Math.Sin(time * 6 + b.Order) : y - high / 2 - height / 2 - 2;
        dc.DrawEllipse(PageFill, Outline, new Point(p.X, top + height / 2), 11, 11);
        dc.PushTransform(new TranslateTransform(p.X - width / 2, top));
        dc.DrawGeometry(TagInk, null, shape);
        dc.Pop();
    }

    // The zipper head's line, the marks high up and the ring on the cat as the files lock on (SkyShotView).
    public static bool ZipAirBusy(BattleSim sim)
    {
        foreach (var b in sim.ZipBlows) if (b.Bottom > 0) return true;
        foreach (var s in sim.Slimes) if (s.ZipAct == ZipAct.Bolt) return true;
        return false;
    }

    public static void DrawZipAir(DrawingContext dc, BattleSim sim, Func<double, double, Point> toScreen, Point cat, double time)
    {
        foreach (var b in sim.ZipBlows)
            if (b.Bottom > 0) DrawZipBlow(dc, b, x => toScreen(x, 0), time);
        foreach (var s in sim.Slimes)
        {
            if (s.ZipAct != ZipAct.Bolt || s.ZipBeat == ZipBeat.Recover || !s.Targetable) continue;
            var from = toScreen(s.X, s.Height * 0.7);
            var aim = toScreen(s.ZipAimX, s.ZipAimHeight);
            if (s.ZipBeat is ZipBeat.Warn or ZipBeat.Lock)
            {
                dc.DrawLine(Halo, from, aim);
                dc.DrawLine(s.ZipBeat == ZipBeat.Lock ? LockedLine : AimLine, from, aim);
                double ring = s.ZipBeat == ZipBeat.Lock ? 15 : 22;
                dc.DrawEllipse(null, s.ZipBeat == ZipBeat.Lock ? LockedLine : AimLine, aim, ring, ring);
                continue;
            }
            var head = toScreen(s.ZipHeadX, s.ZipHeadHeight);
            dc.DrawLine(Halo, from, head);
            dc.DrawLine(ZipSeam, from, head);
            dc.DrawLine(LockedLine, aim, from);
            dc.PushTransform(new TranslateTransform(head.X, head.Y));
            dc.DrawRoundedRectangle(ZipTab, Outline, new Rect(-7, -12, 14, 24), 3, 3);
            dc.DrawEllipse(ZipInside, null, new Point(0, -6), 2.6, 2.6);
            dc.Pop();
        }
        foreach (var shot in sim.Shots)
            if (shot.Paper && shot.LockIn is > 0 and < 0.45)
            {
                double ring = 16 + 26 * shot.LockIn;
                dc.DrawEllipse(null, Halo, cat, ring, ring);
                dc.DrawEllipse(null, LockedLine, cat, ring, ring);
            }
    }

    // A file in flight, turned along its way.
    public static void DrawPaperShot(DrawingContext dc, BattleShot shot, Point at)
    {
        dc.PushTransform(new TranslateTransform(at.X, at.Y));
        dc.PushTransform(new RotateTransform(Math.Atan2(shot.VX, shot.VY) * 180 / Math.PI));
        dc.DrawGeometry(null, Halo, PaperShot);
        dc.DrawGeometry(PageFill, Outline, PaperShot);
        dc.DrawLine(PageLine, new Point(-5, -2), new Point(5, -2));
        dc.DrawLine(PageLine, new Point(-5, 3), new Point(5, 3));
        dc.Pop();
        dc.Pop();
    }

    // Over a data packet's plain body: a tied bundle of pages; the live one shakes (drawn in DrawSlime's body space).
    private static void DrawPacket(DrawingContext dc, Slime s, double time)
    {
        double shake = s.PacketFuse ? 1.8 * Math.Sin(time * 34) : 0;
        dc.PushTransform(new TranslateTransform(shake, 0));
        dc.DrawRoundedRectangle(s.PacketFuse ? BombRed : PageFill, Outline, new Rect(-16, -26, 32, 26), 3, 3);
        dc.DrawLine(PageLine, new Point(-11, -19), new Point(11, -19));
        dc.DrawLine(PageLine, new Point(-11, -13), new Point(11, -13));
        dc.DrawLine(StrapPen, new Point(0, -26), new Point(0, 0));
        dc.DrawRoundedRectangle(ZipTab, Outline, new Rect(-3, -30, 6, 8), 1.4, 1.4);
        dc.Pop();
    }
}
