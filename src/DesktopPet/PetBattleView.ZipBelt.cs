using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

public sealed partial class BattleFieldView
{
    private static readonly Pen BeltEdge = FrozenPen(PetUi.ArtInk, 10);
    private static readonly Pen BeltRibbon = FrozenPen(PetUi.Game.Gold, 6);

    private static void DrawMovingBelt(DrawingContext dc, ZipBlow b, double from, double to, double floor)
    {
        double side = Math.Sign(to - from), rootY = floor - 12;
        if (!b.Done)
        {
            // Low floor markings, not a filled wall across the cat and both locks.
            dc.DrawLine(Halo, new Point(from, floor - 4), new Point(to, floor - 4));
            dc.DrawLine(TacticEdge, new Point(from, floor - 4), new Point(to, floor - 4));
            double charge = Math.Clamp(1 - b.Left / b.Warn, 0, 1);
            double coil = from + side * 30;
            dc.DrawLine(BeltEdge, new Point(from, rootY), new Point(coil, rootY));
            dc.DrawLine(BeltRibbon, new Point(from, rootY), new Point(coil, rootY));
            for (int i = 0; i < 3; i++)
            {
                double yy = rootY - 5 + i * 6, length = 10 + 14 * charge;
                dc.DrawLine(BeltEdge, new Point(coil, yy), new Point(coil + side * length, yy));
                dc.DrawLine(BeltRibbon, new Point(coil, yy), new Point(coil + side * length, yy));
            }
            dc.DrawRoundedRectangle(ZipTab, Outline, new Rect(coil + side * (12 + 14 * charge) - 7, rootY - 8, 14, 18), 3, 3);
            return;
        }
        double tip = from + (to - from) * Math.Clamp(b.Elapsed / b.Travel, 0, 1);
        if (b.Elapsed > b.Travel)
        {
            tip = to + (from - to) * Math.Clamp((b.Elapsed - b.Travel) / 0.2, 0, 1);
            dc.PushOpacity(0.55);
        }
        dc.DrawLine(Halo, new Point(from, rootY), new Point(tip, rootY));
        dc.DrawLine(BeltEdge, new Point(from, rootY), new Point(tip, rootY));
        dc.DrawLine(BeltRibbon, new Point(from, rootY), new Point(tip, rootY));
        // This leading buckle is the same ±12 DIP sweep used for collision.
        dc.DrawRoundedRectangle(FlapFill, Halo, new Rect(tip - 10, floor - b.Top, 20, b.Top), 4, 4);
        dc.DrawRoundedRectangle(null, Outline, new Rect(tip - 10, floor - b.Top, 20, b.Top), 4, 4);
        dc.DrawRoundedRectangle(ZipInside, Outline, new Rect(tip - 5, rootY - 12, 10, 20), 3, 3);
        if (b.Elapsed > b.Travel) dc.Pop();
    }
}
