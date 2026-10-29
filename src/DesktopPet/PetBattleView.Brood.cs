using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M18 (PetBattle.Brood.cs, PetBattle.Mutant.cs). The Slime Mother: a pink boss with a bow beside her crown
// and a heart on her belly; her clutch is announced by the purple rings other helpers get; what it merged into
// wears a pink heart over its head on its way back to her, and hearts float from it to her while it feeds. The
// Mutant King: a green boss with goo dripping down its sides and its crown knocked crooked. Its rain drops fall
// straight down onto their rings. Drawing only.
public sealed partial class BattleFieldView
{
    private static readonly Brush BowPink = Frozen(255, 0xE8, 0x5A, 0x9A);
    private static readonly Brush BellyHeart = Frozen(235, 0xFF, 0xE4, 0xEF);
    private static readonly Pen NursePen = FrozenPen(Frozen(255, 0xE8, 0x5A, 0x9A), 2, new DashStyle(new double[] { 2, 2 }, 0));
    private static readonly Brush MutantGold = Frozen(255, 0xC9, 0xC2, 0x4A);
    private static readonly Brush DripGreen = Frozen(255, 0x5E, 0x9E, 0x3C);
    private static readonly Geometry Bow = Parse("M 0,0 L -7,-5 L -7,5 Z M 0,0 L 7,-5 L 7,5 Z");
    private static readonly Geometry Drip = Parse("M -2.4,0 L 2.4,0 L 2.4,5 A 2.4,2.4 0 1 1 -2.4,5 Z");

    // Marks in body coordinates (from DrawSlime; the origin at the middle of its bottom, up is negative).
    private static void DrawBroodMarks(DrawingContext dc, Slime slime, double bw, double bh)
    {
        switch (slime.Type.Marks)
        {
            case "mother":
                dc.PushTransform(new MatrixTransform(0.8, 0, 0, 0.8, 0, -bh * 0.13));
                dc.DrawGeometry(BellyHeart, Outline, Heart);
                dc.Pop();
                dc.PushTransform(new TranslateTransform(-slime.Facing * 13, -bh * 0.92));
                dc.DrawGeometry(null, Halo, Bow);
                dc.DrawGeometry(BowPink, Outline, Bow);
                dc.DrawEllipse(BowPink, Outline, new Point(0, 0), 2.6, 2.6);
                dc.Pop();
                break;
            case "mutant":
                // Over its outline, off the face: two down its upper sides, one down a lower side.
                foreach (var (kx, ky, len) in new[] { (-0.42, 0.62, 1.0), (0.4, 0.62, 0.8), (0.47, 0.3, 0.6) })
                {
                    double x = bw * kx, y = -bh * ky;
                    dc.PushTransform(new MatrixTransform(1, 0, 0, len, x, y));
                    dc.DrawGeometry(DripGreen, Outline, Drip);
                    dc.Pop();
                }
                break;
        }
    }

    // The crown on a boss's head (body coordinates): the Mutant King's knocked crooked, a paler gold, a drip on it.
    private static void DrawBossCrown(DrawingContext dc, Slime slime)
    {
        if (slime.Type.Marks != "mutant") { dc.DrawGeometry(Gold, Outline, Crown); return; }
        dc.PushTransform(new RotateTransform(-16 * slime.Facing, 0, -31));
        dc.DrawGeometry(MutantGold, Outline, Crown);
        dc.PushTransform(new TranslateTransform(6, -36));
        dc.DrawGeometry(DripGreen, Outline, Drip);
        dc.Pop();
        dc.Pop();
    }

    // Over the slimes: what a mother's clutch merged into, a heart over its head (with a dashed line to her
    // while it feeds, hearts floating along it).
    private void DrawNursing(DrawingContext dc, double floor, double time)
    {
        for (int i = 0; i < sim.Slimes.Count; i++)
        {
            var s = sim.Slimes[i];
            if (!s.Nursing || !s.Targetable || sim.Find(s.SummonedBy) is not { Targetable: true } mother) continue;
            double x = s.X - OriginX, y = floor - s.Height - 14 + 2 * Math.Sin(time * 6 + s.Id);
            dc.PushTransform(new TranslateTransform(x, y));
            dc.DrawGeometry(null, Halo, Heart);
            dc.DrawGeometry(BowPink, Outline, Heart);
            dc.Pop();
            if (!s.Feeding) continue;
            var from = new Point(x, floor - s.Height * 0.6);
            var to = new Point(mother.X - OriginX, floor - mother.Height * 0.6);
            dc.DrawLine(Halo, from, to);
            dc.DrawLine(NursePen, from, to);
            for (int h = 0; h < 2; h++)
            {
                double k = (time * 1.2 + h * 0.5) % 1;
                var at = new Point(from.X + (to.X - from.X) * k, from.Y + (to.Y - from.Y) * k - 10 * Math.Sin(Math.PI * k));
                dc.PushTransform(new MatrixTransform(0.7, 0, 0, 0.7, at.X, at.Y));
                dc.DrawGeometry(HeartRed, Outline, Heart);
                dc.Pop();
            }
        }
    }

    // A rain drop falling straight onto its ring, faster as it falls.
    private void DrawRainDrop(DrawingContext dc, BattleGoo g, double floor)
    {
        double k = g.Progress, x = g.X - OriginX, y = floor - 4 - g.FromHeight * (1 - k * k);
        var fill = KindBrush(g.Tint ?? "#7FBF5A");
        dc.PushTransform(new MatrixTransform(2.2, 0, 0, 2.2, x, y - 12));
        dc.DrawGeometry(null, Halo, Drip);
        dc.DrawGeometry(fill, Outline, Drip);
        dc.Pop();
    }
}
