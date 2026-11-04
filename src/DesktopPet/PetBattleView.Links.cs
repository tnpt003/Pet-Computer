using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// Links between enemies (PetBattle.Links.cs), drawn so each one is seen before it matters: a slime
// rope from a tether's holder to its ally, an aura's ring on the floor, a rhythm's beat dots over its
// head (red on the beat before the strike), a merge's countdown bar, sweat drops on a panicking slime.
// Outlines only (no fills over empty floor), so clicks still pass through between the slimes. 1.5 M10:
// the drummer holds a little drum at its front and taps it on every beat; the chain slime wears two
// steel links on its head.
public sealed partial class BattleFieldView
{
    private static readonly Pen RopePen = FrozenPen(Frozen(235, 0x8E, 0xD8, 0x6E), 3.2);
    private static readonly Pen RopeInk = FrozenPen(Frozen(200, 0x2A, 0x1F, 0x16), 5.4);
    private static readonly Pen AuraPen = FrozenPen(Frozen(200, 0xFF, 0xC8, 0x2E), 2.5, new DashStyle(new double[] { 3, 2 }, 0));
    private static readonly Pen RhythmArea = FrozenPen(Frozen(150, 0xFF, 0x9F, 0x4A), 2, new DashStyle(new double[] { 1, 2.5 }, 0));
    private static readonly Brush BeatOn = Frozen(255, 0xFF, 0x9F, 0x4A);
    private static readonly Brush BeatOff = Frozen(170, 0xFF, 0xF4, 0xD6);
    private static readonly Brush MergeFill = Frozen(255, 0x9B, 0x5C, 0xE0);
    private static readonly Brush SweatBlue = Frozen(255, 0x7F, 0xC8, 0xFF);
    private static readonly Brush DrumShell = Frozen(255, 0xD6, 0x45, 0x45);
    private static readonly Brush DrumHead = Frozen(255, 0xFF, 0xF4, 0xD6);
    private static readonly Pen StickPen = FrozenPen(PetUi.ArtInk, 2.2);
    private static readonly Pen ChainInk = FrozenPen(PetUi.ArtInk, 4.2);
    private static readonly Pen ChainSteel = FrozenPen(Frozen(255, 0xB8, 0xB2, 0xA8), 2.4);
    private const int RopeSegments = 10;

    private static double Top(Slime slime, double floor) => floor - slime.LeapHeight - slime.RideHeight - slime.Lift - slime.Height;

    // Under the slimes: ropes and the rings on the floor.
    private void DrawLinksBehind(DrawingContext dc, double floor, double time)
    {
        if (sim.LinkKinds.Count == 0) return;
        for (int i = 0; i < sim.Slimes.Count; i++)
        {
            var slime = sim.Slimes[i];
            if (slime.Link is not LinkSpec link || !slime.Targetable) continue;
            double x = slime.X - OriginX;
            switch (link.Kind)
            {
                case EnemyLinks.Tether when sim.LinkPartner(slime) is Slime ally:
                    DrawChain(dc, slime, ally, floor, time);
                    break;
                case EnemyLinks.Aura:
                    double pulse = 1 + 0.03 * Math.Sin(time * 4);
                    dc.DrawEllipse(null, Halo, new Point(x, floor - 3), link.Range * pulse, 9);
                    dc.DrawEllipse(null, AuraPen, new Point(x, floor - 3), link.Range * pulse, 9);
                    break;
                case EnemyLinks.Rhythm:
                    dc.DrawEllipse(null, RhythmArea, new Point(x, floor - 3), link.Range, 8);
                    break;
            }
        }
    }

    // A sagging rope of goo between the two, swaying a little (plain lines: no geometry each frame); a long
    // one rests on the floor line instead of sinking under it (as the Scissors and the cat cut it).
    private void DrawRope(DrawingContext dc, Slime holder, Slime ally, double floor, double time)
    {
        var a = new Point(holder.X - OriginX, floor - holder.LeapHeight - holder.RideHeight - holder.Lift - holder.Height * 0.45);
        var b = new Point(ally.X - OriginX, floor - ally.LeapHeight - ally.RideHeight - ally.Lift - ally.Height * 0.45);
        double sag = Math.Min(28, Math.Abs(b.X - a.X) * 0.18) + 3 * Math.Sin(time * 3 + holder.Id);
        for (int pass = 0; pass < 2; pass++)
        {
            var pen = pass == 0 ? RopeInk : RopePen;
            var last = a;
            for (int k = 1; k <= RopeSegments; k++)
            {
                double t = (double)k / RopeSegments;
                var p = new Point(a.X + (b.X - a.X) * t, Math.Min(floor - 2, a.Y + (b.Y - a.Y) * t + sag * 4 * t * (1 - t)));
                dc.DrawLine(pen, last, p);
                last = p;
            }
        }
    }

    // 2026-10-31: a chain slime's tether as a sagging chain of steel links (hollow, every other one turned
    // edge-on, so they read as linked), swaying a little; when its holder takes a share of a hit a gold flash
    // runs down the chain from the ally to the holder, lighting the links it passes. A long one rests on the
    // floor line like the rope.
    private static readonly Pen LinkInk = FrozenPen(PetUi.ArtInk, 3.6);
    private static readonly Pen LinkSteel = FrozenPen(Frozen(255, 0xC9, 0xC3, 0xB8), 1.8);
    private static readonly Pen LinkLit = FrozenPen(Frozen(255, 0xFF, 0xD2, 0x3F), 2.2);
    private static readonly Brush FlashCore = Frozen(255, 0xFF, 0xF6, 0xC8);
    private static readonly Brush FlashGlow = Frozen(110, 0xFF, 0xD2, 0x3F);
    private static readonly Brush BadgeBack = Frozen(150, 0x2A, 0x1F, 0x16);

    private void DrawChain(DrawingContext dc, Slime holder, Slime ally, double floor, double time)
    {
        var a = new Point(holder.X - OriginX, floor - holder.LeapHeight - holder.RideHeight - holder.Lift - holder.Height * 0.45);
        var b = new Point(ally.X - OriginX, floor - ally.LeapHeight - ally.RideHeight - ally.Lift - ally.Height * 0.45);
        double length = Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y);
        double sag = Math.Min(28, Math.Abs(b.X - a.X) * 0.18) + 3 * Math.Sin(time * 3 + holder.Id);
        Point At(double t) => new(a.X + (b.X - a.X) * t, Math.Min(floor - 2, a.Y + (b.Y - a.Y) * t + sag * 4 * t * (1 - t)));
        int links = Math.Clamp((int)(length / 9), 4, 60);
        // The flash: from the ally (t = 1) to the holder (t = 0) over LinkPulseSeconds.
        double flash = holder.LinkPulse > 0 ? holder.LinkPulse / BattleSim.LinkPulseSeconds : -1;
        for (int i = 0; i <= links; i++)
        {
            double t = (double)i / links;
            var p = At(t);
            var next = At(Math.Min(1, t + 0.02));
            var prev = At(Math.Max(0, t - 0.02));
            double angle = Math.Atan2(next.Y - prev.Y, next.X - prev.X) * 180 / Math.PI;
            bool edge = i % 2 == 1;
            bool lit = flash >= 0 && Math.Abs(t - flash) < 0.12;
            dc.PushTransform(new RotateTransform(angle, p.X, p.Y));
            dc.DrawEllipse(null, LinkInk, p, 5, edge ? 1.4 : 3.2);
            dc.DrawEllipse(null, lit ? LinkLit : LinkSteel, p, 5, edge ? 1.4 : 3.2);
            dc.Pop();
        }
        if (flash < 0) return;
        var bead = At(flash);
        dc.DrawEllipse(FlashGlow, null, bead, 9, 9);
        dc.DrawEllipse(FlashCore, null, bead, 4, 4);
    }

    // Over a chained ally (body-free, screen coordinates): a small chain-link badge, so it is clear the chain
    // slime takes part of its hits.
    private static void DrawChainBadge(DrawingContext dc, Point c)
    {
        dc.DrawEllipse(BadgeBack, null, c, 9, 9);
        foreach (var pen in new[] { ChainInk, ChainSteel })
        {
            dc.DrawEllipse(null, pen, new Point(c.X - 3, c.Y), 4, 2.6);
            dc.DrawEllipse(null, pen, new Point(c.X + 3, c.Y), 2.6, 4);
        }
    }

    // At the drummer's front (body coordinates): a drum, and a stick that comes down on every beat.
    private static void DrawDrum(DrawingContext dc, Slime slime)
    {
        dc.PushTransform(new ScaleTransform(slime.Facing, 1));
        var rect = new Rect(9, -16, 15, 11);
        dc.DrawRoundedRectangle(null, Halo, rect, 2, 2);
        dc.DrawRoundedRectangle(DrumShell, Outline, rect, 2, 2);
        dc.DrawEllipse(DrumHead, Outline, new Point(16.5, -16), 7.5, 2.6);
        // Down just after each beat, up again before the next one.
        double beat = slime.Link?.Beat ?? 0.8, k = Math.Clamp(slime.BeatClock / beat, 0, 1);
        double lift = slime.State == SlimeState.Stunned ? 0.6 : k < 0.2 ? k / 0.2 * 0.3 : 0.3 + 0.7 * (k - 0.2) / 0.8;
        double angle = -0.2 - 0.9 * lift;
        var hand = new Point(10, -22);
        dc.DrawLine(StickPen, hand, new Point(hand.X + 12 * Math.Cos(angle), hand.Y - 12 * Math.Sin(angle)));
        dc.DrawEllipse(DrumHead, StickPen, new Point(hand.X + 12 * Math.Cos(angle), hand.Y - 12 * Math.Sin(angle)), 1.6, 1.6);
        dc.Pop();
    }

    // On the chain slime's head (body coordinates): two steel links, one through the other.
    private static void DrawChainLinks(DrawingContext dc)
    {
        foreach (var pen in new[] { ChainInk, ChainSteel })
        {
            dc.DrawEllipse(null, pen, new Point(-4, -30), 5, 3.4);
            dc.DrawEllipse(null, pen, new Point(4, -35), 3.4, 5);
        }
    }

    // Over the slimes: beat dots, merge bars, panic drops.
    private void DrawLinkMarks(DrawingContext dc, double floor, double time)
    {
        if (sim.LinkKinds.Count == 0) return;
        for (int i = 0; i < sim.Slimes.Count; i++)
        {
            var slime = sim.Slimes[i];
            if (!slime.Targetable) continue;
            double x = slime.X - OriginX, top = Top(slime, floor);
            DrawAuraTrail(dc, slime, x, top);
            if (slime.PanicRemaining > 0)
                for (int k = -1; k <= 1; k += 2)
                {
                    double bob = 2 * Math.Sin(time * 10 + k);
                    dc.DrawEllipse(SweatBlue, Halo, new Point(x + k * (slime.Width / 2 - 2), top + 4 + bob), 2.6, 3.6);
                    dc.DrawEllipse(SweatBlue, null, new Point(x + k * (slime.Width / 2 - 2), top + 4 + bob), 2.6, 3.6);
                }
            // A chained ally: the badge by its head (2026-10-31).
            if (sim.TetherHolder(slime) != null) DrawChainBadge(dc, new Point(x - slime.Width / 2 + 2, top + 2));
            if (slime.Link is not LinkSpec link) continue;
            if (link.Kind == EnemyLinks.Rhythm)
            {
                // Beats dots; the last one before the strike glows red.
                int beats = BattleSim.Beats(slime);
                bool warn = slime.BeatCount == beats - 1;
                double y = top - 30, gap = 9, start = x - gap * (beats - 1) / 2;
                for (int k = 0; k < beats; k++)
                {
                    var fill = k < slime.BeatCount ? (warn ? Warning : BeatOn) : BeatOff;
                    double r = warn && k == beats - 1 ? 3.6 + 0.8 * Math.Sin(time * 18) : 3;
                    dc.DrawEllipse(fill, Ink, new Point(start + k * gap, y), r, r);
                }
            }
            else if (link.Kind == EnemyLinks.Merge && slime.MergeClock > 0)
            {
                double k = Math.Clamp(slime.MergeClock / link.MergeSeconds, 0, 1), w = 30, y = top - 28;
                dc.DrawRoundedRectangle(BarBack, null, new Rect(x - w / 2, y, w, 5), 2, 2);
                dc.DrawRoundedRectangle(MergeFill, null, new Rect(x - w / 2, y, w * k, 5), 2, 2);
            }
        }
    }
}
