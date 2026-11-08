using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

public enum FarmPatchKind { Hearth, Pond }

public sealed partial class PetFarmArt
{
    // A patch of land of its own beside the field: the same bank of earth and the same lift, with the
    // hearth or the pond standing on it. Redrawn only when something changes.
    public sealed class Land : FrameworkElement
    {
        public FarmPatchKind Kind { get; }
        public double LandWidth { get; }
        public double Lift { get; set; }
        public double Dustiness { get; set; }
        public double ShakeX { get; set; }
        public double Tilt { get; set; }
        public double Squash { get; set; }
        public double Shed { get; set; } = -1;
        // The fire from 0, embers, to 1, ablaze; what the pot holds; whether the cat holds the stick;
        // what is being poured and how far along, 0 to 1. The clock also runs the pond's ripples.
        public double HearthFire { get; set; }
        public double HearthTime { get; set; }
        public FarmDish? PotDish { get; set; }
        public bool Stirring { get; set; }
        public FarmDish? PourDish { get; set; }
        public double PourAge { get; set; }
        // The cat is fishing from this side (-1 left), what jumps out and how far along, 0 to 1.
        public bool Fishing { get; set; }
        public int FishSide { get; set; } = 1;
        public FarmFish? CaughtFish { get; set; }
        public double CaughtAge { get; set; }

        public Land(FarmPatchKind kind)
        {
            Kind = kind;
            LandWidth = kind == FarmPatchKind.Hearth ? HearthLandWidth : PondLandWidth;
            Width = LandWidth + Side * 2;
            Height = ArtHeight;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double air = Math.Max(0, Lift), mid = Side + LandWidth / 2;
            if (air > 0.5)
                dc.DrawEllipse(Shadow, null, new Point(mid, Floor + 1), LandWidth / 2 * (1 - air / 400), 5 + air / 12);
            var land = Matrix.Identity;
            land.ScaleAt(1 + 0.04 * Squash, 1 - 0.12 * Squash, mid, Floor);
            land.RotateAt(Tilt, mid, Floor);
            land.Translate(ShakeX, -air);
            dc.PushTransform(new MatrixTransform(land));
            double above = Kind == FarmPatchKind.Hearth ? 58 : 8; // Only what stands on the land takes clicks.
            dc.DrawRectangle(Hit, null, new Rect(Side, SoilTop - above, LandWidth, SoilHeight + above));
            if (air > 0.5) DrawUnderside(dc, Side, LandWidth, air);
            DrawBank(dc, Side, LandWidth);
            if (Kind == FarmPatchKind.Hearth) DrawHearth(dc);
            else DrawPond(dc);
            dc.Pop();
            if (Shed >= 0) DrawShedding(dc, Side, LandWidth, Shed);
            if (Dustiness > 0) { DrawDust(dc, Side, LandWidth, Dustiness); DrawLandingDirt(dc, Side, LandWidth, Dustiness); }
        }

        // The pond seen from a little above, in a rim of earth, with reeds at the far end.
        private void DrawPond(DrawingContext dc)
        {
            dc.PushTransform(new TranslateTransform(Side + LandWidth / 2, Floor - 15));
            dc.DrawEllipse(Earth, Ink, new Point(0, -3), 64, 11);
            dc.DrawEllipse(Water, InkThin, new Point(0, -3), 57, 7.5);
            dc.DrawEllipse(PondShine, null, new Point(-20, -5.5), 15, 1.6);
            dc.DrawEllipse(PondShine, null, new Point(14, -0.5), 9, 1.2);
            dc.DrawEllipse(Leaf, InkThin, new Point(30, -5), 8, 2.6);
            dc.DrawEllipse(Stone, InkThin, new Point(-58, -1), 7, 4.5);
            dc.DrawEllipse(StoneDeep, InkThin, new Point(-47, 3), 5, 3);
            foreach (var (x, top, lean) in new[] { (50.0, -40.0, 2.0), (56.0, -33.0, 5.0), (44.0, -30.0, -3.0) })
            {
                dc.DrawLine(StemPen, new Point(x, -5), new Point(x + lean, top));
                dc.DrawRoundedRectangle(Cattail, InkThin, new Rect(x + lean - 2.4, top - 9, 4.8, 12), 2.4, 2.4);
            }
            if (Fishing)
            {
                double x = FishSide * FloatOffset;
                for (int i = 0; i < 2; i++)
                {
                    double k = (HearthTime * 0.7 + i * 0.5) % 1;
                    dc.PushOpacity(1 - k);
                    dc.DrawEllipse(null, RipplePen, new Point(x, -3), 4 + k * 13, 1.2 + k * 3);
                    dc.Pop();
                }
            }
            if (CaughtFish is FarmFish fish)
            {
                double age = Math.Clamp(CaughtAge, 0, 1);
                dc.PushOpacity(age < 0.75 ? 1 : (1 - age) / 0.25);
                dc.PushTransform(new TranslateTransform(FishSide * (FloatOffset - age * 12), -6 - Math.Sin(age * Math.PI) * 56));
                dc.PushTransform(new ScaleTransform(-FishSide, 1));
                dc.PushTransform(new RotateTransform((age - 0.5) * 120));
                DrawFish(dc, fish);
                dc.Pop(); dc.Pop(); dc.Pop();
                dc.Pop();
            }
            dc.Pop();
        }

        // Stones, logs, the fire under an iron pot; the stick rests in it while the cat is not stirring.
        private void DrawHearth(DrawingContext dc)
        {
            dc.PushTransform(new TranslateTransform(Side + LandWidth / 2, Floor - 16));
            double fire = Math.Clamp(HearthFire, 0, 1);
            dc.DrawRoundedRectangle(Stake, InkThin, new Rect(-18, -6, 36, 5), 2, 2);
            foreach (double x in new[] { -13.0, -3.0, 9.0 }) dc.DrawEllipse(Ember, null, new Point(x, -7), 3.2, 2);
            if (fire > 0) DrawFire(dc, fire);
            foreach (int side in new[] { -1, 1 })
            {
                dc.DrawRoundedRectangle(StoneDeep, InkThin, new Rect(side * 25 - 8, -22, 16, 22), 5, 5);
                dc.DrawEllipse(Stone, InkThin, new Point(side * 25, -6), 11, 6.5);
            }

            dc.DrawEllipse(PotDish is FarmDish inPot ? Broth(inPot) : PotDark, InkThin, new Point(0, PotRim), 24, 5.5);
            if (!Stirring)
            {
                var handle = new Point(32, -86);
                dc.DrawLine(SpoonInk, new Point(6, PotRim - 2), handle);
                dc.DrawLine(SpoonWood, new Point(6, PotRim - 2), handle);
            }
            foreach (int side in new[] { -1, 1 }) dc.DrawEllipse(null, InkThin, new Point(side * 28, -42), 4.5, 5.5);
            dc.DrawGeometry(Iron, Ink, PotBody);
            dc.DrawRoundedRectangle(IronHigh, InkThin, new Rect(-27, PotRim - 1, 54, 5), 2.5, 2.5);
            dc.DrawEllipse(IronHigh, null, new Point(-13, -35), 3, 7);

            if (fire > 0.3 && PotDish is FarmDish cooking)
            {
                DrawBubbles(dc, cooking);
                dc.PushOpacity(Math.Clamp((fire - 0.3) / 0.5, 0, 1));
                dc.PushTransform(new TranslateTransform(0, PotRim + 20));
                DrawSteam(dc, HearthTime);
                dc.Pop(); dc.Pop();
            }
            if (PourDish is FarmDish poured) DrawPour(dc, poured);
            dc.Pop();
        }

        private void DrawFire(DrawingContext dc, double fire)
        {
            for (int i = -1; i <= 1; i++)
            {
                double beat = 0.82 + 0.18 * Math.Sin(HearthTime * (11 + i * 2.5) + i * 1.7);
                dc.PushTransform(new TranslateTransform(i * 11, -3));
                dc.PushTransform(new ScaleTransform(fire * (i == 0 ? 1 : 0.8), fire * beat * (i == 0 ? 1 : 0.72)));
                dc.DrawGeometry(Flame, InkThin, FlameTongue);
                dc.PushTransform(new ScaleTransform(0.52, 0.55));
                dc.DrawGeometry(FlameCore, null, FlameTongue);
                dc.Pop(); dc.Pop(); dc.Pop();
            }
        }

        private void DrawBubbles(DrawingContext dc, FarmDish dish)
        {
            for (int i = 0; i < 4; i++)
            {
                double k = (HearthTime * 1.3 + i * 0.27) % 1, size = 1.4 + k * 2.4;
                dc.PushOpacity(1 - k * k);
                dc.DrawEllipse(Broth(dish), InkThin, new Point(-15 + i * 10, PotRim - 2 - k * 3), size, size);
                dc.Pop();
            }
        }

        // Pieces of the ingredients dropping in, then a splash when they land.
        private void DrawPour(DrawingContext dc, FarmDish dish)
        {
            double hit = FarmChores.PourHit / FarmChores.PourSeconds, age = Math.Clamp(PourAge, 0, 1);
            var colors = PetFarm.Recipe(dish).Select(part => CropColor(part.Crop)).ToList();
            if (PetFarm.FishIn(dish) is FarmFish inPot) colors.Add(FishColor(inPot));
            if (age < hit)
            {
                double k = age / hit;
                for (int i = 0; i < 3; i++)
                    dc.DrawEllipse(colors[i % colors.Count], InkThin, new Point(-10 + i * 10, PotRim - 34 + k * k * 32 - i % 2 * 5), 5, 5);
                return;
            }
            double splash = (age - hit) / (1 - hit);
            dc.PushOpacity(1 - splash);
            for (int i = 0; i < 6; i++)
            {
                double side = (i - 2.5) / 2.5;
                dc.DrawEllipse(Broth(dish), InkThin, new Point(side * (8 + splash * 18), PotRim - 3 - Math.Sin(splash * Math.PI) * (16 + (i % 3) * 5) + splash * 8), 2.4, 2.4);
            }
            dc.Pop();
        }
    }
}
