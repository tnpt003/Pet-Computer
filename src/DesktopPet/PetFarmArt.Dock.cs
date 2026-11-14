using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// One board on the farm sign: is that land out, how much waits there, or is it not bought yet;
// Star marks a level up the user has not seen on the folded field yet.
public readonly record struct FarmDockSlotView(bool Open, int Work, bool Locked, bool Star = false);

public sealed partial class PetFarmArt
{
    public sealed partial class Land
    {
        public const int DockSlotCount = 4;
        private const double DockSlotWidth = 32, DockSlotHeight = 28, DockSlotStep = 35, DockSlotTop = 80;

        public FarmDockSlotView[] DockSlots { get; } = new FarmDockSlotView[DockSlotCount];
        public int DockHover { get; set; } = -1;

        private double DockMid => Side + LandWidth / 2;
        private double DockBoardLeft => DockMid - (DockSlotStep * DockSlotCount + 8) / 2;
        private Rect DockSlotRect(int slot) => new(DockBoardLeft + 5 + slot * DockSlotStep, DockSlotTop, DockSlotWidth, DockSlotHeight);

        // Which board is under this point of the window; -1 for none.
        public int DockTargetAt(Point point)
        {
            for (int slot = 0; slot < DockSlotCount; slot++)
                if (DockSlotRect(slot).Contains(point)) return slot;
            return -1;
        }

        private static readonly Brush
            SignWood = PetUi.Hex("#B4815E"), SignWoodDark = PetUi.Hex("#6D4E40"), SignOpen = PetUi.Hex("#FFF1D8"),
            SignPot = PetUi.Hex("#4A3A33"), SignFlame = PetUi.Hex("#F0A63A"), SignLeaf = PetUi.Hex("#6FAE55"),
            SignFish = PetUi.Hex("#6DB6E0"), SignLock = PetUi.Hex("#F1E4D6");
        private static readonly Pen SignHover = PetUi.FrozenPen(PetUi.Game.Gold, 2.4), SignOn = PetUi.FrozenPen(PetUi.Hex("#C2561F"), 3), SignStem = PetUi.FrozenPen(PetUi.Hex("#4F8A3E"), 2.4), SignShine = PetUi.FrozenPen(PetUi.Hex("#D9A67F"), 1.6);

        // A wooden sign on its own bit of ground: one board for each piece of land.
        private void DrawDock(DrawingContext dc)
        {
            double left = DockBoardLeft, width = DockSlotStep * DockSlotCount + 8, top = DockSlotTop - 4, height = DockSlotHeight + 8;
            foreach (double post in new[] { DockMid - width / 2 + 16, DockMid + width / 2 - 16 })
                dc.DrawRectangle(SignWoodDark, InkThin, new Rect(post - 4, top + height - 2, 8, SoilTop - top - height + 6));
            dc.DrawRoundedRectangle(SignWood, Ink, new Rect(left, top, width, height), 6, 6);
            dc.DrawLine(SignShine, new Point(left + 7, top + 2.5), new Point(left + width - 7, top + 2.5));
            for (int slot = 0; slot < DockSlotCount; slot++) DrawDockSlot(dc, slot);
        }

        private void DrawDockSlot(DrawingContext dc, int slot)
        {
            var view = DockSlots[slot];
            var rect = DockSlotRect(slot);
            bool hovered = DockHover == slot;
            dc.DrawRoundedRectangle(view.Open ? SignOpen : SignWoodDark, hovered ? SignHover : InkThin, rect, 4, 4);
            Point centre = new(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2 - (view.Open ? 1.5 : 0));
            if (view.Locked) DrawDockLock(dc, centre);
            else
            {
                if (!view.Open) dc.PushOpacity(0.72);
                DrawDockIcon(dc, slot, centre);
                if (!view.Open) dc.Pop();
            }
            // The tab under a board that is out: its state is not told by colour alone.
            if (view.Open) dc.DrawLine(SignOn, new Point(rect.Left + 8, rect.Bottom - 3.5), new Point(rect.Right - 8, rect.Bottom - 3.5));
            if (view.Work > 0 && !view.Locked)
            {
                dc.DrawEllipse(PetUi.Game.Gold, InkThin, new Point(rect.Right - 3, rect.Top + 3), 5, 5);
                dc.DrawLine(InkThin, new Point(rect.Right - 3, rect.Top + 0.6), new Point(rect.Right - 3, rect.Top + 3.4));
                dc.DrawEllipse(PetUi.ArtInk, null, new Point(rect.Right - 3, rect.Top + 5.5), 0.8, 0.8);
            }
            if (view.Star)
            {
                dc.PushTransform(new MatrixTransform(0.95, 0, 0, 0.95, rect.Left + 2, rect.Top + 2));
                dc.DrawGeometry(PetUi.Game.Gold, InkThin, Star);
                dc.Pop();
            }
        }

        private static void DrawDockIcon(DrawingContext dc, int slot, Point centre)
        {
            switch ((FarmLandSlot)slot)
            {
                case FarmLandSlot.Field:
                    dc.DrawLine(SignStem, new Point(centre.X, centre.Y + 10), new Point(centre.X, centre.Y - 2));
                    dc.PushTransform(new MatrixTransform(0.8, 0, 0, 0.8, centre.X, centre.Y + 8));
                    dc.DrawGeometry(SignLeaf, InkThin, SproutLeaves);
                    dc.Pop();
                    break;
                case FarmLandSlot.Hearth:
                    dc.PushTransform(new MatrixTransform(0.5, 0, 0, 0.5, centre.X, centre.Y + 12));
                    dc.DrawGeometry(SignFlame, Ink, FlameTongue);
                    dc.Pop();
                    dc.PushTransform(new MatrixTransform(0.36, 0, 0, 0.36, centre.X, centre.Y + 7));
                    dc.DrawGeometry(SignPot, Ink, PotBody);
                    dc.DrawRoundedRectangle(SignPot, Ink, new Rect(-30, -56, 60, 9), 4, 4);
                    dc.Pop();
                    break;
                case FarmLandSlot.Pond:
                    dc.PushTransform(new MatrixTransform(0.66, 0, 0, 0.66, centre.X + 2, centre.Y));
                    dc.DrawGeometry(SignFish, Ink, FishTail);
                    dc.DrawGeometry(SignFish, Ink, FishBody);
                    dc.DrawEllipse(PetUi.ArtInk, null, new Point(8, -1.5), 1.5, 1.5);
                    dc.Pop();
                    break;
                default:
                    dc.PushTransform(new MatrixTransform(0.72, 0, 0, 0.72, centre.X - 1, centre.Y + 9));
                    DrawHen(dc, 0, 0, 1, 0);
                    dc.Pop();
                    break;
            }
        }

        private static void DrawDockLock(DrawingContext dc, Point centre)
        {
            dc.DrawRoundedRectangle(SignLock, InkThin, new Rect(centre.X - 6, centre.Y - 2, 12, 9), 2, 2);
            dc.PushTransform(new TranslateTransform(centre.X, centre.Y + 13));
            dc.DrawGeometry(null, new Pen(SignLock, 2), LockShackle);
            dc.Pop();
        }
    }
}
