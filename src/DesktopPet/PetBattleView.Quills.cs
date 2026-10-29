using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M16 (PetBattle.Quills.cs). The hedgehog: brown, a fan of quills over its back that lie flat and
// short, rise (with a shiver) and stand out long with pale tips; wet, they droop. The timer: mint, a
// little clock over its head whose ring empties as its time runs out (red near the end, pale blue while
// it is dazed and the clock crawls). The thief: grey-violet with a dark eye mask; carrying its loot, a
// sack on its back (a gold bolt for an app's charge, a blue star for a skill) and, waiting at the edge,
// a ring that fills until it gets away. Drawing only.
public sealed partial class BattleFieldView
{
    private static readonly Brush QuillFill = Frozen(255, 0x6E, 0x4B, 0x34);
    private static readonly Brush QuillTip = Frozen(255, 0xF6, 0xEB, 0xD9);
    private static readonly Brush ClockFace = Frozen(255, 0xFF, 0xFB, 0xF2);
    private static readonly Pen ClockRing = FrozenPen(Frozen(255, 0x3E, 0x9C, 0x74), 3);
    private static readonly Pen ClockLate = FrozenPen(Frozen(255, 0xE5, 0x3E, 0x3E), 3);
    private static readonly Pen ClockSlow = FrozenPen(Frozen(255, 0x7F, 0xB8, 0xE8), 3);
    private static readonly Pen ClockHand = FrozenPen(PetUi.ArtInk, 1.4);
    private static readonly Brush MaskFill = Frozen(255, 0x2E, 0x2A, 0x3E);
    private static readonly Brush SackFill = Frozen(255, 0xC9, 0x9A, 0x5E);
    private static readonly Brush LootGold = Frozen(255, 0xFF, 0xD2, 0x3F);
    private static readonly Brush LootBlue = Frozen(255, 0x5B, 0x9B, 0xE8);
    private static readonly Pen LingerPen = FrozenPen(Frozen(255, 0xE5, 0x3E, 0x3E), 2.4);
    private static readonly Geometry Bolt = Parse("M 1,-5 L -3,1 L 0,1 L -1,5 L 3,-1 L 0,-1 Z");
    private static readonly Geometry Star = Parse("M 0,-5 L 1.5,-1.5 L 5,-1.5 L 2.2,0.8 L 3.2,4.5 L 0,2.4 L -3.2,4.5 L -2.2,0.8 L -5,-1.5 L -1.5,-1.5 Z");

    // Marks in body coordinates (from DrawSlime; the origin at the middle of its bottom, up is negative).
    private static void DrawQuillMarks(DrawingContext dc, Slime slime, double bw, double bh, double time)
    {
        switch (slime.Type.Marks)
        {
            case "quills": DrawQuills(dc, slime, bw, bh, time); break;
            case "clock": DrawClock(dc, slime, bh); break;
            case "thief": DrawThief(dc, slime, bw, bh, time); break;
        }
    }

    private static void DrawQuills(DrawingContext dc, Slime slime, double bw, double bh, double time)
    {
        // How far up they stand (0 flat, 1 up), and how long they are.
        double rise = slime.Quill switch
        {
            QuillPhase.Up => 1,
            QuillPhase.Rising => BattleSim.QuillRise(slime),
            _ => 0
        };
        double length = 5 + 8 * rise, lay = (1 - rise) * 0.9 * -slime.Facing;
        if (slime.Wet) { length = 4; lay = 0; }
        double shiver = slime.Quill == QuillPhase.Rising ? Math.Sin(time * 50) * 0.06 : 0;
        for (int i = -3; i <= 3; i++)
        {
            double a = i * 0.38, ex = Math.Sin(a) * bw * 0.48, ey = -bh * 0.5 - Math.Cos(a) * bh * 0.5;
            double dir = a + lay + shiver;
            if (slime.Wet) dir = Math.Sign(a) * Math.PI * 0.6; // Drooping down its sides.
            double dx = Math.Sin(dir), dy = -Math.Cos(dir);
            var tip = new Point(ex + dx * length, ey + dy * length);
            var side = new Vector(-dy * 2.4, dx * 2.4);
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(ex, ey) + side - new Vector(dx, dy) * 2, true, true);
                ctx.LineTo(tip, true, false);
                ctx.LineTo(new Point(ex, ey) - side - new Vector(dx, dy) * 2, true, false);
            }
            g.Freeze();
            dc.DrawGeometry(QuillFill, ThinInk, g);
            if (rise > 0.5 && !slime.Wet) dc.DrawEllipse(QuillTip, null, tip - new Vector(dx, dy) * 1.5, 1.3, 1.3);
        }
    }

    private static void DrawClock(DrawingContext dc, Slime slime, double bh)
    {
        if (slime.Type.Clock is not ClockSpec c || slime.ClockCalls >= c.Calls && slime.ClockLeft <= 0 || slime.State == SlimeState.Defeated) return;
        var at = new Point(0, -bh - 13);
        double total = slime.ClockTotal > 0 ? slime.ClockTotal : slime.Elite != null ? c.EliteSeconds : c.Seconds;
        double left = slime.ClockTotal > 0 ? slime.ClockLeft : total, k = Math.Clamp(left / total, 0, 1);
        dc.DrawEllipse(null, Halo, at, 9, 9);
        dc.DrawEllipse(ClockFace, Outline, at, 8, 8);
        var ring = slime.State == SlimeState.Stunned ? ClockSlow : left <= 5 ? ClockLate : ClockRing;
        DrawArc(dc, ring, at, 5.2, k);
        double hand = (1 - k) * 2 * Math.PI;
        dc.DrawLine(ClockHand, at, new Point(at.X + Math.Sin(hand) * 4.5, at.Y - Math.Cos(hand) * 4.5));
        // The two knobs of an alarm clock.
        foreach (int side in new[] { -1, 1 }) dc.DrawEllipse(ClockFace, ThinInk, new Point(at.X + side * 6, at.Y - 7), 2.4, 2);
    }

    private static void DrawThief(DrawingContext dc, Slime slime, double bw, double bh, double time)
    {
        // A dark mask across its eyes, the eyes peeking through.
        double look = slime.Facing * 3, y = -bh * 0.55;
        dc.DrawRoundedRectangle(MaskFill, ThinInk, new Rect(-15 + look, y - 5, 30, 10), 5, 5);
        foreach (int side in new[] { -1, 1 })
        {
            var eye = new Point(side * 7 + look, y);
            dc.DrawEllipse(Brushes.White, null, eye, 3.6, 3.6);
            if (slime.State is SlimeState.Stunned or SlimeState.Defeated)
            {
                dc.DrawLine(ThinInk, eye + new Vector(-2, -2), eye + new Vector(2, 2));
                dc.DrawLine(ThinInk, eye + new Vector(-2, 2), eye + new Vector(2, -2));
            }
            else dc.DrawEllipse(Eye, null, eye + new Vector(slime.Facing, 0), 1.8, 2.2);
        }
        if (slime.Loot == StolenKind.None) return;
        // Its loot in a sack on its back, bouncing as it runs.
        double bob = slime.State == SlimeState.Moving ? Math.Abs(Math.Sin(time * 12 + slime.Id)) * 2 : 0;
        var sack = new Point(-slime.Facing * bw * 0.42, -bh * 0.62 - bob);
        dc.DrawEllipse(null, Halo, sack, 9, 8);
        dc.DrawEllipse(SackFill, Outline, sack, 9, 8);
        dc.DrawLine(ThinInk, sack + new Vector(-3, -7), sack + new Vector(3, -7));
        dc.PushTransform(new TranslateTransform(sack.X, sack.Y + 1));
        if (slime.Loot == StolenKind.Skill) dc.DrawGeometry(LootBlue, ThinInk, Star);
        else dc.DrawGeometry(LootGold, ThinInk, Bolt);
        dc.Pop();
        // Waiting at the edge to get away: a red ring fills over the sack.
        if (slime.LingerClock > 0 && slime.Type.Steal is StealSpec steal)
            DrawArc(dc, LingerPen, sack, 12, Math.Clamp(slime.LingerClock / steal.Linger, 0, 1));
    }
}
