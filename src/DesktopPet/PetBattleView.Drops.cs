using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M19 (PetBattle.Drops.cs): the drops on the floor. A fish (salmon, a tail and an eye), a gold coin
// with a star on it, a round potion bottle (pink, a cork, a shine). Each pops up out of the slime, then
// sits bobbing a little with a soft shadow; in its last seconds it fades (blinking at the very end). An
// invisible halo around each makes it easy to click. Drawing and hit-testing only.
public sealed partial class BattleFieldView
{
    // A drop clicked (its id).
    public event Action<int>? DropClicked;

    private static readonly Brush FishFill = Frozen(255, 0xFF, 0x9E, 0x7A);
    private static readonly Brush FishBelly = Frozen(255, 0xFF, 0xD9, 0xC4);
    private static readonly Brush CoinFill = Frozen(255, 0xFF, 0xC8, 0x2E);
    private static readonly Brush CoinRim = Frozen(255, 0xE0, 0x9A, 0x1A);
    private static readonly Brush CoinStar = Frozen(255, 0xFF, 0xF1, 0xB0);
    private static readonly Brush PotionFill = Frozen(255, 0xF0, 0x6E, 0xB4);
    private static readonly Brush PotionGlass = Frozen(170, 0xFF, 0xFF, 0xFF);
    private static readonly Brush CorkFill = Frozen(255, 0xC9, 0x9A, 0x5E);
    private static readonly Brush LootShadow = Frozen(70, 0x2A, 0x1F, 0x16);
    private static readonly Pen DropInk = FrozenPen(PetUi.ArtInk, 1.8);
    private static readonly Geometry FishTail = PetUi.FrozenGeometry("M 0,0 L 7,-6 L 6,0 L 7,6 Z");
    private static readonly Geometry CoinMark = PetUi.FrozenGeometry("M 0,-4.5 L 1.3,-1.3 L 4.5,-1.3 L 1.9,0.7 L 2.8,4 L 0,2.1 L -2.8,4 L -1.9,0.7 L -4.5,-1.3 L -1.3,-1.3 Z");

    public const double PopSeconds = 0.35;

    // The drop under a point in this view, if any (the newest on top where they overlap).
    public int? DropAt(Point point)
    {
        for (int i = sim.Drops.Count - 1; i >= 0; i--)
        {
            var d = sim.Drops[i];
            double dx = Math.Abs(point.X - (d.X - OriginX)), dy = Floor - point.Y;
            if (dx <= BattleSim.DropHalfWidth + 4 && dy >= -4 && dy <= BattleSim.DropPickHeight) return d.Id;
        }
        return null;
    }

    private void DrawDrops(DrawingContext dc, double floor, double time)
    {
        double fade = sim.Settings.Drops?.Fade ?? 0;
        for (int i = 0; i < sim.Drops.Count; i++)
        {
            var d = sim.Drops[i];
            double x = d.X - OriginX;
            // Out of the slime in an arc, then a small bob.
            double pop = Math.Clamp(d.Age / PopSeconds, 0, 1);
            double lift = pop < 1 ? Math.Sin(pop * Math.PI) * 26 : 1.5 + Math.Sin(time * 4 + d.Id) * 1.5;
            double y = floor - 10 - lift;
            dc.PushOpacity(d.Opacity(fade));
            dc.DrawEllipse(HitHalo, null, new Point(x, floor - 12), BattleSim.DropHalfWidth + 4, 16);
            dc.DrawEllipse(LootShadow, null, new Point(x, floor - 2), 9 * (1 - lift / 60), 2.5);
            switch (d.Kind)
            {
                case BattleDropKind.Fish: DrawFishDrop(dc, x, y); break;
                case BattleDropKind.Coin: DrawCoinDrop(dc, x, y, time + d.Id); break;
                default: DrawPotionDrop(dc, x, y); break;
            }
            dc.Pop();
        }
    }

    private static void DrawFishDrop(DrawingContext dc, double x, double y)
    {
        dc.PushTransform(new TranslateTransform(x + 4, y));
        dc.DrawGeometry(FishFill, DropInk, FishTail);
        dc.Pop();
        dc.DrawEllipse(FishFill, DropInk, new Point(x - 2, y), 9, 5.5);
        dc.DrawEllipse(FishBelly, null, new Point(x - 3, y + 2), 5, 2);
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(x - 7, y - 1), 1.2, 1.2);
    }

    // The coin turns slowly (narrower and wider), its star on it.
    private static void DrawCoinDrop(DrawingContext dc, double x, double y, double time)
    {
        double turn = 0.55 + 0.45 * Math.Abs(Math.Cos(time * 2.2));
        dc.DrawEllipse(CoinRim, DropInk, new Point(x, y), 8 * turn, 8);
        dc.DrawEllipse(CoinFill, null, new Point(x, y), 6 * turn, 6);
        dc.PushTransform(new MatrixTransform(turn, 0, 0, 1, x, y));
        dc.DrawGeometry(CoinStar, null, CoinMark);
        dc.Pop();
    }

    private static void DrawPotionDrop(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(CorkFill, DropInk, new Rect(x - 2.5, y - 12, 5, 4));
        dc.DrawRectangle(PotionGlass, DropInk, new Rect(x - 3, y - 8.5, 6, 4));
        dc.DrawEllipse(PotionFill, DropInk, new Point(x, y), 7.5, 7);
        dc.DrawEllipse(PotionGlass, null, new Point(x - 3, y - 2.5), 2, 1.4);
    }
}
