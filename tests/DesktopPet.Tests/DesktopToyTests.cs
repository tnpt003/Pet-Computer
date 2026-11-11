using System.IO;
using DesktopPet;

// The rules of the toys left out on the desktop (docs/TOY-PLAY.md, M1).
internal static class DesktopToyTests
{
    private static readonly Area Floor = new(100, 0, 1100, 800);

    public static void Run(Action<bool, string> check)
    {
        check(DesktopToyRules.CanTake(0) && DesktopToyRules.CanTake(2) && !DesktopToyRules.CanTake(3), "Three toys at most are out");

        var known = new[] { "a", "b", "c", "d" };
        var many = known.Concat(new[] { "a", "nobody" }).Select(id => new SavedToy { Id = id, FractionX = 2 }).ToList();
        var kept = DesktopToyRules.Normalize(many, true, known)!;
        check(kept.Select(toy => toy.Id).SequenceEqual(new[] { "a", "b", "c" }) && kept.All(toy => toy.FractionX == 1), "A saved list keeps known toys, once each, three at most, on the screen");
        check(DesktopToyRules.Normalize(many, false, known) == null, "No basket: nothing is out");
        check(DesktopToyRules.Normalize(new[] { new SavedToy { Id = "ball", FractionX = double.NaN } }, true)![0].FractionX == 0.5, "A broken place falls back to the middle");

        double x = DesktopToyRules.FromFraction(0.3, 12, Floor);
        check(Math.Abs(DesktopToyRules.Fraction(x, 12, Floor) - 0.3) < 1e-9, "A place is remembered as a fraction of its monitor");
        check(DesktopToyRules.FromFraction(0, 12, Floor) == 112 && DesktopToyRules.FromFraction(1, 12, Floor) == 1088, "...and stays on the floor");

        check(DesktopToyRules.OverBasket(500, 30, 500) && DesktopToyRules.OverBasket(535, 0, 500), "Let go over the basket: it goes in");
        check(!DesktopToyRules.OverBasket(560, 0, 500) && !DesktopToyRules.OverBasket(500, 200, 500), "Beside or high above it: it does not");

        var land = new List<(double, double)> { (400, 700) };
        check(DesktopToyRules.FreeCentre(200, 12, land, Floor) == null, "Off the land it stays where it is");
        check(DesktopToyRules.FreeCentre(420, 12, land, Floor) is double left && Math.Abs(left - (400 - DesktopToyRules.Gap - 12)) < 0.01, "On the land it goes to the nearest free side");
        check(DesktopToyRules.FreeCentre(690, 12, land, Floor) is double right && Math.Abs(right - (700 + DesktopToyRules.Gap + 12)) < 0.01, "...either side");
        check(DesktopToyRules.FreeCentre(500, 12, new List<(double, double)> { (0, 2000) }, Floor) == null, "Nowhere free: it stays");

        var ball = new ToyBall { X = 500 };
        ball.Nudge(10);
        check(ball.VX < 0 && ball.VY > 0 && ball.Feedback > 0, "A click rolls the ball away from the click");
        var push = new ToyBall { X = 500 };
        push.Push(10);
        check(Math.Abs(ball.VX) < Math.Abs(push.VX) && ball.VY < push.VY, "...more gently than a push in the game");
        ball.Nudge(0);
        check(ball.VX != 0, "A click on its middle still rolls it");
        for (int i = 0; i < 1200 && !ball.AtRest; i++) ball.Step(1 / 60.0, 112, 1088);
        check(ball.AtRest && ball.Height == 0, "It comes to rest on the floor");
        double rested = ball.X;
        ball.Step(1 / 60.0, 112, 1088);
        check(ball.AtRest && ball.X == rested, "...and stays at rest");

        // M2: the cardboard box.
        check(DesktopToyRules.All.Contains(DesktopToyRules.Box) && DesktopToyRules.HalfWidthOf(DesktopToyRules.Box) == 50 && DesktopToyRules.HalfWidthOf(DesktopToyRules.Ball) == 12, "The basket has the box too, about 100 DIPs wide");
        check(DesktopToyRules.Normalize(new[] { new SavedToy { Id = "box" }, new SavedToy { Id = "ball" } }, true)!.Count == 2, "A saved box is kept");
        var box = new ToyBall { X = 500 };
        box.Wobble();
        for (int i = 0; i < 30; i++) box.StepBox(1 / 60.0, 150, 1050);
        check(box.AtRest && box.X == 500 && box.Rotation == 0 && box.Feedback == 0, "A click only shakes the box where it stands");
        var thrownBall = new ToyBall { X = 500, Height = 200, VX = 600 };
        var thrownBox = new ToyBall { X = 500, Height = 200, VX = 600 };
        double ballPeak = 0, boxPeak = 0;
        bool boxLanded = false, ballLanded = false;
        for (int i = 0; i < 1200; i++)
        {
            thrownBall.Step(1 / 60.0, 112, 9000); thrownBox.StepBox(1 / 60.0, 150, 9000);
            if (thrownBall.Height == 0) ballLanded = true; else if (ballLanded) ballPeak = Math.Max(ballPeak, thrownBall.Height);
            if (thrownBox.Height == 0) boxLanded = true; else if (boxLanded) boxPeak = Math.Max(boxPeak, thrownBox.Height);
        }
        check(thrownBox.AtRest && thrownBox.Rotation == 0, "A thrown box comes to rest without turning");
        check(boxPeak < ballPeak * 0.4, "...bounces low");
        check(thrownBox.X - 500 < (thrownBall.X - 500) * 0.6, "...and slides a shorter way than the ball rolls");

        string folder = Path.Combine(Path.GetTempPath(), "pet-toys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "preferences.json");
            check(new PetPreferences().ToyBasketShown == false, "The basket stays put away by default");
            new PetPreferences { ToyBasketShown = true, ToyBasketFractionX = 3, ToysOut = new() { new SavedToy { Id = "ball", Monitor = "M", FractionX = 0.4 }, new SavedToy { Id = "ghost" } } }.Save(path);
            var loaded = PetPreferences.Load(path);
            check(loaded.ToyBasketShown && loaded.ToyBasketFractionX == 1 && loaded.ToysOut is { Count: 1 } && loaded.ToysOut[0].Monitor == "M" && loaded.ToysOut[0].FractionX == 0.4, "Preferences keep the basket and the toys, cleaned up");
            new PetPreferences { ToyBasketShown = false, ToysOut = new() { new SavedToy { Id = "ball" } } }.Save(path);
            check(PetPreferences.Load(path).ToysOut == null, "A hidden basket has nothing out");
        }
        finally { Directory.Delete(folder, true); }
    }
}
