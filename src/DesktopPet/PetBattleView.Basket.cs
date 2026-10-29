using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M20 (PetBattle.Basket.cs): the fish basket on the floor (a wicker basket, its fish peeking over the
// rim), the fish a slime carries over its head, and the fish dropped on the floor (a white ring running
// down to when it hops back; an invisible halo makes it easy to click). A gold ring fills over the basket
// or a dropped fish while a slime is taking it. Drawing and hit-testing only.
public sealed partial class BattleFieldView
{
    // A dropped fish clicked (its id).
    public event Action<int>? FishClicked;

    private static readonly Brush WickerFill = Frozen(255, 0xD9, 0xA4, 0x62);
    private static readonly Brush WickerRim = Frozen(255, 0xB8, 0x80, 0x44);
    private static readonly Pen WickerWeave = FrozenPen(Frozen(255, 0xA8, 0x74, 0x3C), 1.4);
    private static readonly Pen BasketInk = FrozenPen(PetUi.ArtInk, 2);
    private static readonly Pen GrabRing = FrozenPen(Frozen(255, 0xFF, 0xD2, 0x3F), 3);
    private static readonly Pen BackRing = FrozenPen(Frozen(240, 255, 255, 255), 2.2);
    private static readonly Pen BackRingRim = FrozenPen(PetUi.ArtInk, 4.2);
    private static readonly Geometry BasketBody = PetUi.FrozenGeometry("M -26,-22 L 26,-22 L 21,0 L -21,0 Z");

    // The dropped fish under a point in this view, if any.
    public int? FishAt(Point point)
    {
        for (int i = sim.StrayFish.Count - 1; i >= 0; i--)
        {
            var f = sim.StrayFish[i];
            double dx = Math.Abs(point.X - (f.X - OriginX)), dy = Floor - point.Y;
            if (dx <= BattleSim.FishHalfWidth + 4 && dy >= -4 && dy <= BattleSim.FishPickHeight) return f.Id;
        }
        return null;
    }

    // 1.5 M21 (the guide): the basket, and a dropped fish (this view's DIPs).
    public Rect? BasketBounds() => sim.Basket != null && double.IsFinite(sim.BasketX) ? new Rect(sim.BasketX - OriginX - 30, Floor - 34, 60, 36) : null;
    public Rect? FishBounds(int id) => sim.FindFish(id) is StrayFish f ? new Rect(f.X - OriginX - BattleSim.FishHalfWidth - 4, Floor - BattleSim.FishPickHeight, BattleSim.FishHalfWidth * 2 + 8, BattleSim.FishPickHeight + 4) : null;

    // Behind the slimes: the basket and the fish lying on the floor.
    private void DrawBasket(DrawingContext dc, double floor, double time)
    {
        if (sim.Basket is not BasketSpec spec || !double.IsFinite(sim.BasketX)) return;
        double x = sim.BasketX - OriginX, bottom = floor - 1;
        // Its fish stand heads up over the rim (at most five drawn), behind the front of the basket.
        int shown = Math.Min(5, sim.FishInBasket);
        for (int i = 0; i < shown; i++)
        {
            double fx = x + (i - (shown - 1) / 2.0) * 10, fy = bottom - 31 - (i % 2) * 4;
            dc.PushTransform(new RotateTransform(i % 2 == 0 ? 80 : 100, fx, fy));
            DrawFishDrop(dc, fx, fy);
            dc.Pop();
        }
        dc.PushTransform(new TranslateTransform(x, bottom));
        dc.DrawGeometry(null, Halo, BasketBody);
        dc.DrawGeometry(WickerFill, BasketInk, BasketBody);
        for (int row = 1; row <= 2; row++) dc.DrawLine(WickerWeave, new Point(-25 + row * 1.6, -22 + row * 7.3), new Point(25 - row * 1.6, -22 + row * 7.3));
        for (int col = -2; col <= 2; col++) dc.DrawLine(WickerWeave, new Point(col * 9, -21), new Point(col * 7.5, -1));
        dc.DrawRoundedRectangle(WickerRim, BasketInk, new Rect(-28, -26, 56, 6), 3, 3);
        dc.Pop();
        // A slime taking a fish from it: a gold ring fills over the basket.
        foreach (var s in sim.Slimes)
            if (s.FishClock > 0 && !s.CarriesFish && Math.Abs(s.X - sim.BasketX) <= 10)
                DrawArc(dc, GrabRing, new Point(x, bottom - 44), 9, Math.Clamp(s.FishClock / spec.Grab, 0, 1));
        for (int i = 0; i < sim.StrayFish.Count; i++)
        {
            var f = sim.StrayFish[i];
            double fx = f.X - OriginX, y = floor - 10 + Math.Sin(time * 5 + f.Id) * 1.2;
            dc.DrawEllipse(HitHalo, null, new Point(fx, floor - 12), BattleSim.FishHalfWidth + 4, 15);
            dc.DrawEllipse(LootShadow, null, new Point(fx, floor - 2), 9, 2.5);
            DrawFishDrop(dc, fx, y);
            // The time until it hops back by itself.
            // (an ink rim under it: seen on light desktops too).
            double left = Math.Clamp(1 - f.Age / spec.Back, 0, 1);
            DrawArc(dc, BackRingRim, new Point(fx, y), 14, left);
            DrawArc(dc, BackRing, new Point(fx, y), 14, left);
            foreach (var s in sim.Slimes)
                if (s.FishClock > 0 && !s.CarriesFish && Math.Abs(s.X - f.X) <= 10)
                    DrawArc(dc, GrabRing, new Point(fx, y - 22), 7, Math.Clamp(s.FishClock / (spec.Grab / 2), 0, 1));
        }
    }

    // Over the slimes: a fish held high by each slime running off with one.
    private void DrawCarriedFish(DrawingContext dc, double floor, double time)
    {
        if (sim.Basket == null) return;
        for (int i = 0; i < sim.Slimes.Count; i++)
        {
            var s = sim.Slimes[i];
            if (!s.CarriesFish || s.State == SlimeState.Defeated) continue;
            double x = s.X - OriginX, bob = Math.Abs(Math.Sin(time * 12 + s.Id)) * 2;
            double y = floor - s.Lift - s.RideHeight - s.Height - 8 - bob;
            // Its head the way the slime runs.
            dc.PushTransform(new MatrixTransform(-s.Facing, 0, 0, 1, x * (1 + s.Facing), 0));
            dc.DrawEllipse(null, Halo, new Point(x - 2, y), 10, 6.5);
            DrawFishDrop(dc, x, y);
            dc.Pop();
        }
    }
}
