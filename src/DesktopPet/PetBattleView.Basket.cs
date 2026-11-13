using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M20, defence 2026-11-13 (PetBattle.Basket.cs): the fish basket on the floor (a wicker basket, its fish
// peeking over the rim) with its HP bar over it; it flashes when hit, cracks show as it weakens and its fish
// thin out. A slime the cat hit (turned on the cat for a while) wears a small red "!". Drawing only.
public sealed partial class BattleFieldView
{
    private static readonly Brush WickerFill = Frozen(255, 0xD9, 0xA4, 0x62);
    private static readonly Brush WickerHurt = Frozen(255, 0xF0, 0x8A, 0x6E);
    private static readonly Brush WickerRim = Frozen(255, 0xB8, 0x80, 0x44);
    private static readonly Pen WickerWeave = FrozenPen(Frozen(255, 0xA8, 0x74, 0x3C), 1.4);
    private static readonly Pen BasketInk = FrozenPen(PetUi.ArtInk, 2);
    private static readonly Pen BasketCrack = FrozenPen(PetUi.ArtInk, 1.6);
    private static readonly Brush BasketBarLow = Frozen(255, 0xFF, 0x8A, 0x5C);
    private static readonly Brush AggroFill = Frozen(255, 0xFF, 0x6B, 0x4A);
    private static readonly Pen AggroInk = FrozenPen(Brushes.White, 2);
    private static readonly Geometry BasketBody = PetUi.FrozenGeometry("M -26,-22 L 26,-22 L 21,0 L -21,0 Z");
    private static readonly Geometry BasketCracks = PetUi.FrozenGeometry("M -14,-21 L -10,-14 L -15,-8 M 9,-20 L 12,-12 L 8,-6 L 11,-1");

    // 1.5 M21 (the guide): the basket (this view's DIPs).
    public Rect? BasketBounds() => sim.Basket != null && double.IsFinite(sim.BasketX) ? new Rect(sim.BasketX - OriginX - 30, Floor - 44, 60, 46) : null;

    // Behind the slimes: the basket and its HP bar.
    private void DrawBasket(DrawingContext dc, double floor, double time)
    {
        if (sim.Basket == null || !double.IsFinite(sim.BasketX)) return;
        double x = sim.BasketX - OriginX, bottom = floor - 1;
        double left = sim.BasketMaxHp > 0 ? Math.Clamp(sim.BasketHp / sim.BasketMaxHp, 0, 1) : 0;
        // A hit shakes it a little.
        double shake = sim.BasketFlash > 0 ? Math.Sin(time * 60) * 1.5 : 0;
        x += shake;
        // Its fish stand heads up over the rim (fewer as it weakens), behind the front of the basket.
        int shown = left <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(left * 5));
        for (int i = 0; i < shown; i++)
        {
            double fx = x + (i - (shown - 1) / 2.0) * 10, fy = bottom - 31 - (i % 2) * 4;
            dc.PushTransform(new RotateTransform(i % 2 == 0 ? 80 : 100, fx, fy));
            DrawFishDrop(dc, fx, fy);
            dc.Pop();
        }
        dc.PushTransform(new TranslateTransform(x, bottom));
        dc.DrawGeometry(null, Halo, BasketBody);
        dc.DrawGeometry(sim.BasketFlash > 0 ? WickerHurt : WickerFill, BasketInk, BasketBody);
        for (int row = 1; row <= 2; row++) dc.DrawLine(WickerWeave, new Point(-25 + row * 1.6, -22 + row * 7.3), new Point(25 - row * 1.6, -22 + row * 7.3));
        for (int col = -2; col <= 2; col++) dc.DrawLine(WickerWeave, new Point(col * 9, -21), new Point(col * 7.5, -1));
        if (left < 0.5) dc.DrawGeometry(null, BasketCrack, BasketCracks);
        dc.DrawRoundedRectangle(WickerRim, BasketInk, new Rect(-28, -26, 56, 6), 3, 3);
        dc.Pop();
        // Its HP bar over the fish (like a slime's, warm red once low).
        double barW = 46, barY = bottom - 48;
        dc.DrawRoundedRectangle(BarBack, null, new Rect(x - barW / 2 - 1, barY - 1, barW + 2, 7), 3, 3);
        if (left > 0) dc.DrawRoundedRectangle(left < 0.3 ? BasketBarLow : BarFill, null, new Rect(x - barW / 2, barY, barW * left, 5), 2.5, 2.5);
    }

    // Over the slimes: a red "!" over each slime the cat hit lately (it goes for the cat, not the basket).
    private void DrawAggroMarks(DrawingContext dc, double floor, double time)
    {
        if (sim.Basket == null) return;
        for (int i = 0; i < sim.Slimes.Count; i++)
        {
            var s = sim.Slimes[i];
            if (s.AggroLeft <= 0 || !s.Targetable || !BattleSim.AttacksBasket(s.Type)) continue;
            double x = s.X - OriginX + s.Width * 0.32, y = floor - s.Lift - s.RideHeight - s.Altitude - s.Height - 4 - Math.Abs(Math.Sin(time * 8 + s.Id)) * 1.5;
            dc.DrawEllipse(AggroFill, BasketInk, new Point(x, y), 6, 6);
            dc.DrawLine(AggroInk, new Point(x, y - 3.2), new Point(x, y + 0.6));
            dc.DrawEllipse(Brushes.White, null, new Point(x, y + 2.8), 1.1, 1.1);
        }
    }
}
