using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 2026-11-04 (docs/GOPY-C-2026-11-04.md D5): what C4 adds to the strip. The planted flag (a post, no slime
// body), the big drum's rings of sound, the herb's leaf shield on a bomb slime (a green bubble with three
// leaves going round, a small leaf on the herb paired with it, a line of leaves as it is cast), a bomb's
// soot after a shielded blast and a quick slime's "!" as it waits to catch its bomb. Drawing only.
public sealed partial class BattleFieldView
{
    private static readonly Pen PostPole = FrozenPen(Frozen(255, 0x7A, 0x56, 0x3A), 4);
    private static readonly Brush PostCloth = Frozen(255, 0x5B, 0x8D, 0xEF), PostClothElite = Frozen(255, 0xFF, 0xD2, 0x3F), PostMound = Frozen(255, 0x8A, 0x6A, 0x4A);
    private static readonly Brush PostBarBack = Frozen(200, 0x2A, 0x1F, 0x16), PostBarFill = Frozen(255, 0x6F, 0xE0, 0x6A);
    private static readonly Pen DrumRing = FrozenPen(Frozen(240, 0xF0, 0x6A, 0x3A), 5.5), DrumRingSoft = FrozenPen(Frozen(190, 0xFF, 0xD2, 0x3F), 3.4);
    private static readonly Brush DrumBlast = Frozen(120, 0xF0, 0x6A, 0x3A);
    private static readonly Brush ShieldAir = Frozen(70, 0x6F, 0xE0, 0x8A), LeafFill = Frozen(255, 0x4F, 0xC9, 0x6A), Soot = Frozen(170, 0x1E, 0x18, 0x14);
    private static readonly Pen ShieldEdge = FrozenPen(Frozen(220, 0x3E, 0xB0, 0x6A), 2);
    private static readonly Pen LeafLine = FrozenPen(Frozen(220, 0x4F, 0xC9, 0x6A), 2.4, new DashStyle(new double[] { 2, 3 }, 0));

    // The planted flag: a mound, a pole and a cloth waving, a small HP bar over it.
    private static void DrawPost(DrawingContext dc, Slime post, double x, double floor, double time)
    {
        double top = floor - 62;
        dc.DrawEllipse(PostMound, Outline, new Point(x, floor - 3), 15, 6);
        dc.DrawLine(PostPole, new Point(x, floor - 4), new Point(x, top));
        var cloth = new StreamGeometry();
        using (var g = cloth.Open())
        {
            double wave = Math.Sin(time * 7) * 3;
            g.BeginFigure(new Point(x + 2, top), true, true);
            g.QuadraticBezierTo(new Point(x + 18, top + 3 + wave), new Point(x + 32, top + 2 - wave), true, false);
            g.LineTo(new Point(x + 26, top + 11), true, false);
            g.LineTo(new Point(x + 32, top + 20 - wave), true, false);
            g.QuadraticBezierTo(new Point(x + 18, top + 22 + wave), new Point(x + 2, top + 19), true, false);
        }
        cloth.Freeze();
        dc.DrawGeometry(post.Elite != null ? PostClothElite : PostCloth, ThinInk, cloth);
        double k = Math.Clamp(post.Hp / Math.Max(1, post.MaxHp), 0, 1);
        dc.DrawRectangle(PostBarBack, null, new Rect(x - 16, top - 9, 32, 5));
        dc.DrawRectangle(PostBarFill, null, new Rect(x - 15, top - 8, 30 * k, 3));
        if (!post.FlagGuard) return;
        // Its flag slime still lives: the same gold bubble as a shielded boss.
        double pulse = 1 + 0.03 * Math.Sin(time * 4);
        var centre = new Point(x + 8, floor - 38);
        dc.DrawEllipse(null, Halo, centre, 32 * pulse, 40);
        dc.DrawEllipse(null, BubblePen, centre, 32 * pulse, 40);
    }

    // Everything of the herb and the bomb (over the slimes).
    private void DrawLeaves(DrawingContext dc, double floor, double time)
    {
        for (int i = 0; i < sim.Slimes.Count; i++)
        {
            var s = sim.Slimes[i];
            if (!s.Targetable) continue;
            double x = s.X - OriginX, top = Top(s, floor);
            var middle = new Point(x, top + s.Height / 2);
            if (s.LeafShield)
            {
                double r = Math.Max(s.Width, s.Height) * 0.72;
                dc.DrawEllipse(ShieldAir, ShieldEdge, middle, r, r);
                for (int k = 0; k < 3; k++)
                {
                    double a = time * 2.4 + k * 2.09;
                    DrawLeaf(dc, new Point(middle.X + Math.Cos(a) * r, middle.Y + Math.Sin(a) * r * 0.8), a * 57);
                }
                if (sim.Find(s.LeafBy) is { Targetable: true } herb)
                {
                    DrawLeaf(dc, new Point(herb.X - OriginX + 12, Top(herb, floor) - 6), 20);
                    if (sim.Time - s.LeafCastAt < 0.6) dc.DrawLine(LeafLine, new Point(herb.X - OriginX, Top(herb, floor) + 8), middle);
                }
            }
            if (s.Charred)
            {
                dc.DrawEllipse(Soot, null, new Point(x - s.Width * 0.18, top + s.Height * 0.4), 7, 4);
                dc.DrawEllipse(Soot, null, new Point(x + s.Width * 0.2, top + s.Height * 0.62), 6, 3.5);
            }
            if (sim.AwaitingBomb(s))
            {
                var mark = new Point(x, top - 16 - Math.Abs(Math.Sin(time * 12)) * 3);
                dc.DrawLine(Halo, new Point(mark.X, mark.Y - 8), new Point(mark.X, mark.Y + 2));
                dc.DrawLine(WarningPen, new Point(mark.X, mark.Y - 8), new Point(mark.X, mark.Y + 2));
                dc.DrawEllipse(Warning, null, new Point(mark.X, mark.Y + 7), 2.4, 2.4);
            }
        }
    }

    private static void DrawLeaf(DrawingContext dc, Point at, double angle)
    {
        dc.PushTransform(new RotateTransform(angle, at.X, at.Y));
        dc.DrawEllipse(LeafFill, ThinInk, at, 5.5, 2.8);
        dc.Pop();
    }

    // The big drum: rings of sound running out to the edge of the band's ring.
    private void DrawDrum(DrawingContext dc, BattleEffect effect, double x, double floor)
    {
        double k = Math.Clamp(effect.Age / effect.Life, 0, 1);
        // A blast over the floor, and the word for it, as the rings go.
        double blast = Math.Clamp(k * 2, 0, 1);
        dc.DrawEllipse(DrumBlast, null, new Point(x, floor - 26), effect.Value * blast, (effect.Value * 0.3 + 12) * blast);
        DrawWord(dc, PetText.Pick(Language, "BÙM!", "BOOM!"), Warning, x, floor - 92 - 22 * k);
        for (int i = 0; i < 3; i++)
        {
            double ring = Math.Clamp(k * 1.3 - i * 0.15, 0, 1);
            if (ring <= 0) continue;
            dc.DrawEllipse(null, i == 0 ? DrumRing : DrumRingSoft, new Point(x, floor - 26), effect.Value * ring, (effect.Value * 0.3 + 12) * ring);
        }
    }
}
