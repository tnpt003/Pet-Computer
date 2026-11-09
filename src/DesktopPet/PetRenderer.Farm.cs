using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The cat's farm work: what it holds and what flies off the job (docs/FARM-GAME.md). Drawn facing
// right in the cat's own space; the work is at about x = 114, one tile out from the cat's middle.
// Only the look: the timing comes from FarmChores and nothing here changes the field.
public sealed partial class PetRenderer
{
    private static readonly Brush HoeMetal = PetUi.Hex("#A8B0BA"), FarmSoil = PetUi.Hex("#8B5E3C"), SeedFill = PetUi.Hex("#E7C98B"), PouchFill = PetUi.Hex("#C9A16B"), BowlFill = PetUi.Hex("#D9B48A");
    private static readonly Geometry BowlShape = FrozenPath("M-13,-4 L13,-4 C13,7 6,10 0,10 C-6,10 -13,7 -13,-4 Z");
    private static readonly Geometry BiteMark = FrozenPath("M-2.6,-11 L2.6,-11 L1.5,1.5 L-1.5,1.5 Z M-2,5 A2,2 0 1 0 2,5 A2,2 0 1 0 -2,5 Z");

    private static double Ease(double k) => k * k * (3 - 2 * k);

    private void DrawFarmTool(DrawingContext dc, in Pose p)
    {
        dc.PushTransform(new ScaleTransform(p.FacingScale, 1, 56, 56));
        dc.PushOpacity(p.FarmToolBlend);
        switch (p.FarmTool)
        {
            case FarmToolPose.Till: DrawHoe(dc, p); break;
            case FarmToolPose.Sow: DrawSeedPouch(dc, p); break;
            case FarmToolPose.Harvest: DrawPickedCrop(dc, p); break;
            case FarmToolPose.Pour: DrawPourBowl(dc, p); break;
            case FarmToolPose.Pounce: DrawPaperScraps(dc, p); break;
        }
        dc.Pop(); dc.Pop();
    }

    // Brought up slowly, down fast; the soil jumps when it lands.
    private void DrawHoe(DrawingContext dc, in Pose p)
    {
        const double strike = 0.14;
        double t = p.FarmToolTime, hit = p.FarmToolHit;
        double raise = t < hit - strike ? Ease(Math.Clamp(t / (hit - strike), 0, 1)) : t < hit ? 1 - (t - (hit - strike)) / strike : 0;
        dc.PushTransform(new TranslateTransform(84, 76));
        dc.PushTransform(new RotateTransform(35 - 90 * raise));
        dc.DrawLine(StirInk, new Point(0, 0), new Point(38, 0));
        dc.DrawLine(StirWood, new Point(0, 0), new Point(38, 0));
        dc.DrawRoundedRectangle(HoeMetal, CanInk, new Rect(33, -1, 9, 14), 2, 2);
        dc.Pop(); dc.Pop();
        Oval(dc, Fur, 84, 76, 7, 6.5);
        double age = (t - hit) / 0.5;
        if (age is < 0 or >= 1) return;
        dc.PushOpacity(1 - age);
        for (int i = 0; i < 6; i++)
        {
            double spread = (i - 2.5) * 5;
            var at = new Point(116 + spread * (0.4 + age * 1.6), 104 - Math.Sin(age * Math.PI) * (12 + i % 3 * 6));
            dc.DrawEllipse(FarmSoil, CanInkThin, at, 2.8, 2.3);
        }
        dc.Pop();
    }

    // The little sack shakes and seeds drop onto the tile.
    private void DrawSeedPouch(DrawingContext dc, in Pose p)
    {
        double t = p.FarmToolTime, shake = Math.Sin(t * 22) * 2;
        var pouch = new Rect(94 + shake, 64, 17, 20);
        dc.DrawRoundedRectangle(PouchFill, CanInk, pouch, 6, 6);
        dc.DrawLine(CanInkThin, new Point(pouch.Left + 3, pouch.Top + 5), new Point(pouch.Right - 3, pouch.Top + 5));
        Oval(dc, Fur, 90 + shake, 74, 7, 6.5);
        for (int i = 0; i < 6; i++)
        {
            double fall = (t * 1.8 + i * 0.17) % 1;
            dc.DrawEllipse(SeedFill, CanInkThin, new Point(106 + i * 2.6 + fall * 5, 84 + fall * 24), 1.9, 2.6);
        }
    }

    // Down to the tile; once it comes free the crop rises in the paw, shining.
    private void DrawPickedCrop(DrawingContext dc, in Pose p)
    {
        double t = p.FarmToolTime, hit = p.FarmToolHit;
        if (t < hit)
        {
            double reach = Ease(Math.Clamp(t / hit, 0, 1));
            Oval(dc, Fur, 84 + 26 * reach, 84 + 16 * reach, 7, 6.5);
            return;
        }
        if (p.FarmToolCrop is not FarmCrop crop) return;
        double age = (t - hit) / Math.Max(0.01, p.FarmToolLength - hit), rise = Ease(Math.Clamp(age / 0.4, 0, 1));
        double x = 112 - 16 * rise, y = 106 - 44 * rise;
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new ScaleTransform(0.72, 0.72));
        PetFarmArt.DrawPlant(dc, FarmStage.Ripe, crop);
        dc.Pop(); dc.Pop();
        Oval(dc, Fur, x - 4, y + 2, 7, 6.5);
        if (age > 0.3) DrawFarmSparkles(dc, x, y - 26, (age - 0.3) / 0.7);
    }

    private void DrawPourBowl(DrawingContext dc, in Pose p)
    {
        double t = p.FarmToolTime, hit = p.FarmToolHit;
        double tip = t < hit ? Ease(Math.Clamp(t / hit, 0, 1)) : Ease(Math.Clamp(1 - (t - hit) / Math.Max(0.01, p.FarmToolLength - hit), 0, 1));
        dc.PushTransform(new TranslateTransform(100, 58));
        dc.PushTransform(new RotateTransform(55 * tip));
        dc.DrawGeometry(BowlFill, CanInk, BowlShape);
        dc.Pop(); dc.Pop();
        Oval(dc, Fur, 88, 64, 7, 6.5);
    }

    // The paper bird's feathers, when the cat comes down on it.
    private static void DrawPaperScraps(DrawingContext dc, in Pose p)
    {
        double age = (p.FarmToolTime - p.FarmToolHit) / Math.Max(0.01, p.FarmToolLength - p.FarmToolHit);
        if (age is < 0 or >= 1) return;
        dc.PushOpacity(1 - age * age);
        for (int i = 0; i < 6; i++)
        {
            double angle = -2.8 + i * 0.5, way = 8 + age * 26;
            dc.PushTransform(new TranslateTransform(112 + Math.Cos(angle) * way, 74 + Math.Sin(angle) * way + age * age * 16));
            dc.PushTransform(new RotateTransform(i * 50 + age * 240));
            dc.DrawRectangle(FloatWhite, CanInkThin, new Rect(-3.5, -2.2, 7, 4.4));
            dc.Pop(); dc.Pop();
        }
        dc.Pop();
    }

    // A drop running down the back of the head, now and then: this is work.
    private static void DrawWorkSweat(DrawingContext dc, in Pose p, double time, double blend)
    {
        double fall = time * 0.65 % 1;
        dc.PushTransform(new ScaleTransform(p.FacingScale, 1, 56, 56));
        dc.PushOpacity(blend * Math.Sin(fall * Math.PI));
        dc.PushTransform(new TranslateTransform(20, 26 + fall * 14));
        dc.DrawGeometry(CanDropFill, CanInkThin, CanDrop);
        dc.Pop(); dc.Pop(); dc.Pop();
    }

    // A "!" over the head when the float goes under; a paw in the air and sparkles once the fish is up.
    private void DrawFishingMoments(DrawingContext dc, in Pose p)
    {
        double since = p.FishTime - p.FishBiteAt;
        if (since < 0) return;
        dc.PushTransform(new ScaleTransform(p.FacingScale, 1, 56, 56));
        if (since < 0.5)
        {
            dc.PushOpacity(Math.Min(1, (0.5 - since) / 0.15));
            dc.PushTransform(new TranslateTransform(96, 18 - Math.Sin(Math.Min(1, since / 0.12) * Math.PI) * 4));
            DrawOutlinedEffect(dc, BiteMark, PetUi.Game.Gold);
            dc.Pop(); dc.Pop();
        }
        double shown = (since - FarmChores.ReelSeconds) / FarmChores.FishShowSeconds;
        if (shown >= 0)
        {
            double lift = Ease(Math.Clamp(shown / 0.3, 0, 1));
            Oval(dc, Fur, 30, 84 - 16 * lift + Math.Sin(p.FishTime * 18) * 2, 6.5, 9);
            DrawFarmSparkles(dc, 96, 26, Math.Min(1, shown));
        }
        dc.Pop();
    }

    private static void DrawFarmSparkles(DrawingContext dc, double x, double y, double age)
    {
        dc.PushOpacity(Math.Sin(Math.Clamp(age, 0, 1) * Math.PI));
        for (int i = 0; i < 3; i++)
        {
            double angle = -1.6 + (i - 1) * 1.15, way = 12 + age * 12, size = 0.6 + (i == 1 ? 0.25 : 0);
            dc.PushTransform(new TranslateTransform(x + Math.Cos(angle) * way, y + Math.Sin(angle) * way));
            dc.PushTransform(new ScaleTransform(size, size));
            DrawOutlinedEffect(dc, SpellStar, PetUi.Game.Gold);
            dc.Pop(); dc.Pop();
        }
        dc.Pop();
    }
}
