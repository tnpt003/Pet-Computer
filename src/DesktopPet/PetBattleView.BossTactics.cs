using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

public sealed partial class BattleFieldView
{
    private static readonly Brush NestShell = Frozen(245, 0xF8, 0xC8, 0xDA);
    private static readonly Brush TacticZone = Frozen(65, 0xFF, 0xB5, 0x59);
    private static readonly Pen TacticEdge = FrozenPen(Game.Warning, 2);

    private static void DrawBroodNest(DrawingContext dc, Slime s, double x, double floor, double time)
    {
        double breath = 1 + Math.Sin(time * 5 + s.Id) * 0.035;
        dc.DrawEllipse(NestShell, Outline, new Point(x, floor - 20), 28 * breath, 20 / breath);
        for (int i = -1; i <= 1; i++)
            dc.DrawEllipse(BowPink, Outline, new Point(x + i * 14, floor - 24 + Math.Abs(i) * 5), 9, 11);
        dc.DrawEllipse(BellyHeart, Outline, new Point(x, floor - 22), 3, 4);
        dc.DrawRoundedRectangle(Game.Panel, Outline, new Rect(x - 25, floor - 53, 50, 5), 2, 2);
        dc.DrawRoundedRectangle(BowPink, null, new Rect(x - 24, floor - 52, 48 * Math.Clamp(s.Hp / s.MaxHp, 0, 1), 3), 1, 1);
        // The hatching clock is distinct from the health bar: three eggs plus a numeric countdown.
        var number = NumberShape(Math.Ceiling(s.NestLeft).ToString(System.Globalization.CultureInfo.InvariantCulture), 12);
        dc.DrawRoundedRectangle(Game.Panel, Outline, new Rect(x - number.Width / 2 - 5, floor - 78, number.Width + 10, number.Height + 4), 4, 4);
        dc.PushTransform(new TranslateTransform(x - number.Width / 2, floor - 76));
        dc.DrawGeometry(Game.Rim, null, number.Shape);
        dc.Pop();
    }

    private void DrawBossTactics(DrawingContext dc, double floor, double time)
    {
        foreach (var s in sim.Slimes)
        {
            if (!s.Targetable) continue;
            if (s.BroodJumpLeft > 0)
                TacticRing(dc, s.SpecialAt, floor, s.BroodJumpRadius, s.BroodJumpLeft / BattleSim.BroodFlightSeconds);
            if (s.Type.BossTactics == null) continue;
            double x = s.X - OriginX, y = floor - s.Height * 0.45;
            if (s.NurseCord != 0 && sim.Find(s.NurseCord) is { Targetable: true } child)
            {
                // Same sagged shape as RopeCrossed: the drawn cord is the actual cutting target.
                double bx = child.X - OriginX, by = floor - child.Height * 0.45;
                double sag = Math.Min(28, Math.Abs(bx - x) * 0.18);
                var prev = new Point(x, y);
                for (int k = 1; k <= 10; k++)
                {
                    double t = k / 10.0;
                    var at = new Point(x + (bx - x) * t, Math.Min(floor - 2, y + (by - y) * t + sag * 4 * t * (1 - t)));
                    dc.DrawLine(Halo, prev, at); dc.DrawLine(NursePen, prev, at); prev = at;
                }
                dc.DrawEllipse(BellyHeart, Outline, new Point((x + bx) / 2, (y + by) / 2 + sag - 6), 7, 7);
            }
            if (s.GooArmor > 0)
            {
                dc.DrawRoundedRectangle(Game.Panel, Outline, new Rect(x - 35, floor - s.Height - 17, 70, 5), 2, 2);
                dc.DrawRoundedRectangle(DripGreen, null, new Rect(x - 34, floor - s.Height - 16, 68 * Math.Clamp(s.GooArmor / (s.MaxHp * 0.1), 0, 1), 3), 1, 1);
            }
            double progress = s.SpecialDuration > 0 ? 1 - s.SpecialLeft / s.SpecialDuration : 0;
            var zone = BattleSim.BossZone(s, s.Special);
            switch (s.Special)
            {
                case BossMove.Sweep:
                    TacticRing(dc, zone.X, floor, zone.Radius, progress);
                    // A translucent arm extends on the announced side; no change to the body hitbox.
                    dc.DrawEllipse(TacticZone, TacticEdge, new Point(zone.X - OriginX, floor - 24), zone.Radius, 13);
                    break;
                case BossMove.Burst:
                    TacticRing(dc, zone.X, floor, zone.Radius, progress);
                    dc.DrawEllipse(null, TacticEdge, new Point(x, floor - s.Height / 2), s.Width * (0.4 + progress * 0.2), s.Height * 0.5);
                    break;
                case BossMove.Charge:
                    double lo = zone.X - zone.Radius - OriginX;
                    dc.DrawRoundedRectangle(TacticZone, TacticEdge, new Rect(lo, floor - 14, zone.Radius * 2, 12), 6, 6);
                    TacticRing(dc, s.SpecialAt, floor, BattleSim.BossChargePadding, progress);
                    break;
                case BossMove.Wave:
                    for (int k = 1; k <= BattleSim.BossWaveSteps; k++)
                    {
                        var wave = BattleSim.BossWaveZone(s, k);
                        if (wave.X < sim.Left + 24 || wave.X > sim.Right - 24) break;
                        TacticRing(dc, wave.X, floor, wave.Radius, progress);
                    }
                    break;
                case BossMove.Rain:
                    for (int beat = 0; beat < 2; beat++)
                        for (int i = 0; i < s.Type.Rain!.Drops; i++)
                        {
                            var rain = BattleSim.BossRainZone(s, beat, i);
                            if (rain.X < sim.Left + rain.Radius || rain.X > sim.Right - rain.Radius) continue;
                            TacticRing(dc, rain.X, floor, rain.Radius, progress * (beat == 0 ? 1 : 0.65));
                        }
                    dc.PushTransform(new TranslateTransform(s.SpecialAt - OriginX, floor - 70));
                    dc.DrawGeometry(DripGreen, Outline, Drip);
                    dc.Pop();
                    break;
                case BossMove.Absorb:
                    foreach (var p in sim.Puddles)
                        if (p.OwnerId == s.Id && !p.DrainCut && Math.Abs(p.X - s.X) <= BattleSim.BossAbsorbRange)
                        {
                            var from = new Point(p.X - OriginX, floor - 4);
                            dc.DrawLine(Halo, from, new Point(x, y));
                            dc.DrawLine(TacticEdge, from, new Point(x, y));
                            double t = (time * 0.8) % 1;
                            dc.DrawEllipse(DripGreen, Outline, new Point(from.X + (x - from.X) * t, from.Y + (y - from.Y) * t), 5, 5);
                        }
                    break;
                case BossMove.BroodJump:
                    TacticRing(dc, zone.X, floor, zone.Radius, progress);
                    break;
                case BossMove.Recall:
                    dc.DrawEllipse(null, NursePen, new Point(x, y), s.Width * 0.65, s.Height * 0.65);
                    break;
                case BossMove.Rest:
                    if (s.SpecialCount % 4 == 0)
                    {
                        dc.DrawEllipse(Gold, Outline, new Point(x - 12, floor - s.Height - 10), 4, 4);
                        dc.DrawEllipse(Gold, Outline, new Point(x + 12, floor - s.Height - 10), 4, 4);
                    }
                    break;
            }
        }
    }

    private void TacticRing(DrawingContext dc, double at, double floor, double radius, double progress)
    {
        double x = at - OriginX;
        dc.DrawEllipse(TacticZone, Halo, new Point(x, floor - 7), radius, 8);
        dc.DrawEllipse(null, TacticEdge, new Point(x, floor - 7), radius, 8);
        dc.DrawEllipse(Game.Warning, null, new Point(x, floor - 7), Math.Max(2, radius * Math.Clamp(progress, 0, 1)), 3);
    }
}
