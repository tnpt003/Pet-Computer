using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// Links between enemies (PetBattle.Links.cs), drawn so each one is seen before it matters: a slime
// rope from a tether's holder to its ally, an aura's ring on the floor, a rhythm's beat dots over its
// head (red on the beat before the strike), a merge's countdown bar, sweat drops on a panicking slime.
// Outlines only (no fills over empty floor), so clicks still pass through between the slimes.
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
                    DrawRope(dc, slime, ally, floor, time);
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

    // A sagging rope of goo between the two, swaying a little (plain lines: no geometry each frame).
    private void DrawRope(DrawingContext dc, Slime holder, Slime ally, double floor, double time)
    {
        var a = new Point(holder.X - OriginX, floor - holder.LeapHeight - holder.Lift - holder.Height * 0.45);
        var b = new Point(ally.X - OriginX, floor - ally.LeapHeight - ally.Lift - ally.Height * 0.45);
        double sag = Math.Min(28, Math.Abs(b.X - a.X) * 0.18) + 3 * Math.Sin(time * 3 + holder.Id);
        for (int pass = 0; pass < 2; pass++)
        {
            var pen = pass == 0 ? RopeInk : RopePen;
            var last = a;
            for (int k = 1; k <= RopeSegments; k++)
            {
                double t = (double)k / RopeSegments;
                var p = new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t + sag * 4 * t * (1 - t));
                dc.DrawLine(pen, last, p);
                last = p;
            }
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
            if (slime.PanicRemaining > 0)
                for (int k = -1; k <= 1; k += 2)
                {
                    double bob = 2 * Math.Sin(time * 10 + k);
                    dc.DrawEllipse(SweatBlue, Halo, new Point(x + k * (slime.Width / 2 - 2), top + 4 + bob), 2.6, 3.6);
                    dc.DrawEllipse(SweatBlue, null, new Point(x + k * (slime.Width / 2 - 2), top + 4 + bob), 2.6, 3.6);
                }
            if (slime.Link is not LinkSpec link) continue;
            if (link.Kind == EnemyLinks.Rhythm)
            {
                // Beats dots; the last one before the strike glows red.
                bool warn = slime.BeatCount == link.Beats - 1;
                double y = top - 30, gap = 9, start = x - gap * (link.Beats - 1) / 2;
                for (int k = 0; k < link.Beats; k++)
                {
                    var fill = k < slime.BeatCount ? (warn ? Warning : BeatOn) : BeatOff;
                    double r = warn && k == link.Beats - 1 ? 3.6 + 0.8 * Math.Sin(time * 18) : 3;
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
