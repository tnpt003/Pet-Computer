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
            if (b.Bottom <= 0 && b.Kind != ZipBlowKind.Canopy) DrawZipBlow(dc, b, x => new Point(x - OriginX, floor), time);
        foreach (var s in sim.Slimes)
        {
            if (s.Type.Zip != null && s.ZipRepulseWarn > 0)
                DrawRepulseWarning(dc, s.X - OriginX, floor, sim.CatXForZip < s.X ? -1 : 1, 1 - s.ZipRepulseWarn / 0.9);
            if (s.PacketFuse && s.PacketRevealed && s.Targetable && s.PacketLeft > 0 && sim.Find(s.SummonedBy) is Slime owner && BattleSim.MoveSpec(owner, ZipAct.Faulty) is ZipMoveSpec faulty)
            {
                DrawZipTarget(dc, ZipVisual.Faulty, s.X - OriginX, floor, faulty.Radius, faulty.Height, 1 - s.PacketLeft / faulty.Life);
                DrawSizeTag(dc, $"{Math.Ceiling(s.PacketLeft)} s", s.X - OriginX, floor - s.Height - 20);
            }
            if (!s.Targetable || s.Type.Zip is not ZipSpec zip || zip.Move(s.ZipAct) is not ZipMoveSpec m) continue;
            double progress = s.ZipSeconds > 0 ? 1 - s.ZipLeft / s.ZipSeconds : 1;
            switch (s.ZipAct)
            {
                case ZipAct.Clamp: DrawClamp(dc, s, m, floor, progress); break;
                case ZipAct.Tear: if (s.ZipBeat != ZipBeat.Recover) DrawTearWarning(dc, s, m, floor, progress); break;
                case ZipAct.Tumble: DrawZipTape(dc, s, floor); break;
            }
        }
    }

    // Two flaps of the bale opening over the marked spot, then shut on it.
    private void DrawClamp(DrawingContext dc, Slime s, ZipMoveSpec m, double floor, double progress)
    {
        if (s.ZipBeat == ZipBeat.Recover) return;
        double x = s.ZipAimX - OriginX, open = s.ZipBeat == ZipBeat.Warn ? Math.Min(1, progress * 1.6) : 0;
        if (s.ZipBeat == ZipBeat.Warn) DrawZipTarget(dc, ZipVisual.Clamp, x, floor, m.Radius, m.Height, progress);
        foreach (int side in new[] { -1, 1 })
        {
            double gap = m.Radius * open;
            var flap = new Rect(x + (side < 0 ? -gap - 22 : gap), floor - m.Height, 22, m.Height);
            dc.DrawRoundedRectangle(FlapFill, FlapEdge, flap, 4, 4);
            dc.DrawLine(StrapPen, new Point(flap.Left + 3, floor - m.Height * 0.55), new Point(flap.Right - 3, floor - m.Height * 0.55));
        }
    }

    private void DrawTearWarning(DrawingContext dc, Slime s, ZipMoveSpec m, double floor, double progress)
    {
        double x = s.X - OriginX, y = floor - 4, grow = s.ZipBeat == ZipBeat.Warn ? progress : 1;
        foreach (int side in new[] { -1, 1 })
        {
            var end = new Point(x + side * (m.Spacing * grow + m.Radius), y);
            dc.DrawLine(Halo, new Point(x, y), end);
            dc.DrawLine(WarningEdge, new Point(x, y), end);
            DrawZipTeeth(dc, new Point(x, y), end);
            dc.DrawLine(WarningPen, end, new Point(end.X - side * 8, y - 6));
            dc.DrawLine(WarningPen, end, new Point(end.X - side * 8, y + 6));
        }
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
        if (b.Kind == ZipBlowKind.Canopy)
        {
            if (!b.Done)
                DrawZipTarget(dc, ZipVisual.Tree, p.X, p.Y, b.Radius, b.Top, Math.Clamp(1 - b.Left / b.Warn, 0, 1));
            else
            {
                dc.PushOpacity(Math.Clamp(b.Life / 0.15, 0, 1));
                DrawFolderTree(dc, b.Variant, b.Radius, p.X, p.Y, b.Top);
                for (double levelY = 85; levelY < b.Top - 30; levelY += 85)
                {
                    double nx = p.X + ((int)(levelY / 85) % 2 == 0 ? -1 : 1) * (b.Radius - 13);
                    dc.DrawLine(ZipSeam, new Point(p.X, p.Y - levelY), new Point(nx, p.Y - levelY));
                    DrawFolderNode(dc, nx, p.Y - levelY, 22);
                }
                dc.Pop();
            }
            return;
        }
        if (b.Kind == ZipBlowKind.Belt)
        {
            DrawMovingBelt(dc, b, at(b.FromX).X, at(b.ToX).X, p.Y);
            return;
        }
        double y = p.Y - b.Bottom, k = b.Warn > 0 ? Math.Clamp(1 - b.Left / b.Warn, 0, 1) : 1, high = b.Top - b.Bottom;
        if (b.Done)
        {
            if (b.Kind == ZipBlowKind.Branch)
            {
                DrawFolderTree(dc, b, p.X, y, high);
            }
            return;
        }
        DrawZipTarget(dc, b.Kind == ZipBlowKind.Branch ? ZipVisual.Tree : b.Kind == ZipBlowKind.Belt ? ZipVisual.Belt : ZipVisual.Overwrite, p.X, y, b.Radius, high, k);
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
        foreach (var s in sim.Slimes) if (s.Type.Zip != null && (s.ZipBurstLeft > 0 || s.Shielded && s.BombClock > 0 && s.BombClock <= 2)) return true;
        foreach (var b in sim.ZipBlows) if (b.Bottom > 0 || b.Kind == ZipBlowKind.Canopy) return true;
        foreach (var s in sim.Slimes) if (s.ZipAct is ZipAct.Bolt or ZipAct.Overflow) return true;
        return false;
    }

    public static void DrawZipAir(DrawingContext dc, BattleSim sim, Func<double, double, Point> toScreen, Point cat, double time)
    {
        foreach (var boss in sim.Slimes)
            if (boss.ZipAct == ZipAct.Overflow && BattleSim.MoveSpec(boss, ZipAct.Overflow) is ZipMoveSpec overflow)
                DrawZipOverflow(dc, sim, boss, overflow, toScreen, time);
        foreach (var boss in sim.Slimes)
            if (boss.Type.Zip != null && (boss.ZipBurstLeft > 0 || boss.Shielded && boss.BombClock > 0 && boss.BombClock <= 2))
                DrawArchiveBurst(dc, sim, boss, toScreen, time);
        foreach (var b in sim.ZipBlows)
            if (b.Bottom > 0 || b.Kind == ZipBlowKind.Canopy) DrawZipBlow(dc, b, x => toScreen(x, 0), time);
        foreach (var s in sim.Slimes)
        {
            if (s.ZipAct != ZipAct.Bolt || s.ZipBeat == ZipBeat.Recover || !s.Targetable) continue;
            var from = toScreen(s.X, s.Height * 0.7);
            var aim = toScreen(s.ZipAimX, s.ZipAimHeight);
            if (s.ZipBeat is ZipBeat.Warn or ZipBeat.Lock)
            {
                dc.DrawLine(Halo, from, aim);
                dc.DrawLine(s.ZipBeat == ZipBeat.Lock ? LockedLine : AimLine, from, aim);
                DrawZipTeeth(dc, from, aim);
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
                var targetBox = new Rect(cat.X - ring, cat.Y - 40, ring * 2, 80);
                dc.DrawRoundedRectangle(null, Halo, targetBox, 4, 4);
                dc.DrawRoundedRectangle(null, LockedLine, targetBox, 4, 4);
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
        double shake = s.PacketFuse && s.PacketRevealed ? 1.8 * Math.Sin(time * 34) : 0;
        dc.PushTransform(new TranslateTransform(shake, 0));
        dc.DrawRoundedRectangle(s.PacketFuse && s.PacketRevealed ? BombRed : PageFill, Outline, new Rect(-16, -26, 32, 26), 3, 3);
        dc.DrawLine(PageLine, new Point(-11, -19), new Point(11, -19));
        dc.DrawLine(PageLine, new Point(-11, -13), new Point(11, -13));
        dc.DrawLine(StrapPen, new Point(0, -26), new Point(0, 0));
        dc.DrawRoundedRectangle(ZipTab, Outline, new Rect(-3, -30, 6, 8), 1.4, 1.4);
        dc.Pop();
    }
}
