using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// One board on the farm sign: is that land out, how much waits there, or is it not bought yet.
public readonly record struct FarmDockSlotView(bool Open, int Work, bool Locked);

public sealed partial class PetFarmArt
{
    public sealed partial class Land
    {
        public const int DockSlotCount = 4, DockCartTarget = 4, DockVisitorTarget = 5;
        private const double DockSlotWidth = 32, DockSlotHeight = 28, DockSlotStep = 35, DockSlotTop = 80, DockGuestY = 62, DockGuestRadius = 12, DockGuestGap = 22;

        public FarmDockSlotView[] DockSlots { get; } = new FarmDockSlotView[DockSlotCount];
        public int DockHover { get; set; } = -1;
        public bool DockCart { get; set; }
        public bool DockVisitor { get; set; }

        private double DockMid => Side + LandWidth / 2;
        private double DockBoardLeft => DockMid - (DockSlotStep * DockSlotCount + 8) / 2;
        private Rect DockSlotRect(int slot) => new(DockBoardLeft + 5 + slot * DockSlotStep, DockSlotTop, DockSlotWidth, DockSlotHeight);
        private Point DockGuestAt(int target) => new(DockMid + (target == DockCartTarget ? -DockGuestGap : DockGuestGap), DockGuestY);

        // Which board or guest is under this point of the window; -1 for none.
        public int DockTargetAt(Point point)
        {
            for (int slot = 0; slot < DockSlotCount; slot++)
                if (DockSlotRect(slot).Contains(point)) return slot;
            if (DockCart && Within(point, DockCartTarget)) return DockCartTarget;
            if (DockVisitor && Within(point, DockVisitorTarget)) return DockVisitorTarget;
            return -1;
        }

        private bool Within(Point point, int target) => (point - DockGuestAt(target)).Length <= DockGuestRadius + 2;

        private static readonly Brush
            SignWood = PetUi.Hex("#B4815E"), SignWoodDark = PetUi.Hex("#6D4E40"), SignOpen = PetUi.Hex("#FFF1D8"),
            SignPot = PetUi.Hex("#4A3A33"), SignFlame = PetUi.Hex("#F0A63A"), SignLeaf = PetUi.Hex("#6FAE55"),
            SignFish = PetUi.Hex("#6DB6E0"), SignCart = PetUi.Hex("#E2503F"), SignCat = PetUi.Hex("#BFB3A6"), SignLock = PetUi.Hex("#F1E4D6");
        private static readonly Pen SignHover = PetUi.FrozenPen(PetUi.Game.Gold, 2.4), SignOn = PetUi.FrozenPen(PetUi.Hex("#C2561F"), 3), SignStem = PetUi.FrozenPen(PetUi.Hex("#4F8A3E"), 2.4), SignShine = PetUi.FrozenPen(PetUi.Hex("#D9A67F"), 1.6), SignAwningStripe = PetUi.FrozenPen(SignOpen, 1.8);
        private static readonly Geometry CatHead = Parse("M-8,-3 L-9,-11 L-3.5,-7.5 C-1.5,-8.3 1.5,-8.3 3.5,-7.5 L9,-11 L8,-3 C9,4 4,7 0,7 C-4,7 -9,4 -8,-3 Z");
        private static readonly Geometry CartAwning = Parse("M-8,-9 L8,-9 L11,-2 L-11,-2 Z");

        // A wooden sign on its own bit of ground: one board for each piece of land, guests hung above it.
        private void DrawDock(DrawingContext dc)
        {
            double left = DockBoardLeft, width = DockSlotStep * DockSlotCount + 8, top = DockSlotTop - 4, height = DockSlotHeight + 8;
            foreach (double post in new[] { DockMid - width / 2 + 16, DockMid + width / 2 - 16 })
                dc.DrawRectangle(SignWoodDark, InkThin, new Rect(post - 4, top + height - 2, 8, SoilTop - top - height + 6));
            dc.DrawRoundedRectangle(SignWood, Ink, new Rect(left, top, width, height), 6, 6);
            dc.DrawLine(SignShine, new Point(left + 7, top + 2.5), new Point(left + width - 7, top + 2.5));
            for (int slot = 0; slot < DockSlotCount; slot++) DrawDockSlot(dc, slot);
            if (DockCart) DrawDockGuest(dc, DockCartTarget);
            if (DockVisitor) DrawDockGuest(dc, DockVisitorTarget);
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

        private void DrawDockGuest(DrawingContext dc, int target)
        {
            var at = DockGuestAt(target);
            dc.DrawEllipse(SignOpen, DockHover == target ? SignHover : Ink, at, DockGuestRadius, DockGuestRadius);
            if (target == DockCartTarget)
            {
                dc.PushTransform(new TranslateTransform(at.X, at.Y));
                dc.DrawRectangle(SignWood, InkThin, new Rect(-7, -2, 14, 6));
                dc.DrawGeometry(SignCart, InkThin, CartAwning);
                dc.DrawLine(SignAwningStripe, new Point(-3, -7), new Point(-4, -2));
                dc.DrawLine(SignAwningStripe, new Point(3, -7), new Point(4, -2));
                dc.DrawEllipse(SignPot, null, new Point(-4.5, 6), 2.6, 2.6);
                dc.DrawEllipse(SignPot, null, new Point(4.5, 6), 2.6, 2.6);
                dc.Pop();
                return;
            }
            dc.PushTransform(new TranslateTransform(at.X, at.Y + 1));
            dc.DrawGeometry(SignCat, InkThin, CatHead);
            dc.DrawEllipse(PetUi.ArtInk, null, new Point(-3, -1), 1.1, 1.1);
            dc.DrawEllipse(PetUi.ArtInk, null, new Point(3, -1), 1.1, 1.1);
            dc.Pop();
        }
    }
}
