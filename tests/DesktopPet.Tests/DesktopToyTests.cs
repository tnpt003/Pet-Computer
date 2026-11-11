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

        // M3: fetch.
        check(FetchStreak.IsThrow(450, 0) && FetchStreak.IsThrow(300, -300) && !FetchStreak.IsThrow(390, 0) && !FetchStreak.IsThrow(double.NaN, 900),
            "A throw is a let-go faster than about 400 DIPs/s");
        check(FetchStreak.NextInvite(0) == 480 && FetchStreak.NextInvite(0.999) < 900 && FetchStreak.NextInvite(5) == 900, "The cat brings the ball every 8–15 minutes");
        var streak = new FetchStreak();
        int fetched = 0;
        for (int i = 0; i < 5; i++) if (streak.TryFetch()) fetched++;
        check(fetched == 5 && streak.Resting && streak.RestLeft == 120, "Five fetches in a row, then two minutes of rest");
        check(!streak.TryFetch() && streak.Resting, "...when it only watches");
        streak.Step(119);
        check(!streak.TryFetch(), "...for all two minutes");
        streak.Step(1.01);
        check(!streak.Resting && streak.TryFetch() && streak.InARow == 1, "...then it fetches again");
        streak.Break();
        check(streak.InARow == 0, "No throw while it waited: a new streak");
        streak.Step(double.NaN);
        check(!streak.Resting, "A broken frame changes nothing");

        // M4: the feather wand and the wind-up mouse.
        check(DesktopToyRules.All.Take(4).SequenceEqual(new[] { "ball", "box", "wand", "mouse" }) && DesktopToyRules.HalfWidthOf("wand") == 45 && DesktopToyRules.HalfWidthOf("mouse") == 18,
            "The basket has four toys: the wand's handle is about 90 DIPs long");
        check(DesktopToyRules.Normalize(new[] { new SavedToy { Id = "wand" }, new SavedToy { Id = "mouse" } }, true)!.Count == 2, "A saved wand and mouse are kept");
        var chase = new ToyChaseStreak();
        for (int i = 0; i < 89 * 60; i++) chase.Wand(1 / 60.0);
        check(!chase.Resting && chase.WandPlayed > 88, "The cat chases the wand for up to about 90 seconds");
        for (int i = 0; i < 70; i++) chase.Wand(1 / 60.0);
        check(chase.Resting && chase.RestLeft == 120 && !chase.TryMouse(), "...then rests two minutes, the mouse included");
        chase.Step(120.01, false);
        int mice = 0;
        for (int i = 0; i < 6; i++) if (chase.TryMouse()) mice++;
        check(mice == 5 && chase.Resting, "Five mice in a row, then the rest");
        chase.Step(121, false);
        chase.TryMouse(); chase.Wand(10);
        chase.Step(19, false);
        check(chase.MiceInARow == 1 && chase.WandPlayed == 10, "A short break keeps the count");
        chase.Step(1.01, false);
        check(chase.MiceInARow == 0 && chase.WandPlayed == 0, "Twenty seconds without play: it starts afresh");

        // M5: passing the ball, its shared rest and the reactions' rules.
        check(ToyPassRules.BatSpeed(160) == 200 && ToyPassRules.BatSpeed(10) == ToyPassRules.MinBat && ToyPassRules.BatSpeed(-2000) == ToyPassRules.MaxBat && ToyPassRules.BatSpeed(double.NaN) == ToyPassRules.MinBat,
            "The bat is as hard as it takes to stop near the cursor, within limits");
        check(Math.Abs(ToyPassRules.StopsAt(100, 200) - 260) < 1e-9, "A rolling ball stops about v / 1.25 DIPs on");
        var rolled = new ToyBall { X = 100, VX = 200 };
        for (int i = 0; i < 1200; i++) rolled.Step(1 / 120.0, 0, 5000);
        check(Math.Abs(rolled.X - ToyPassRules.StopsAt(100, 200)) < 12, "...as the ball's own physics does");
        check(ToyPassRules.BatDirection(900, 500, -1) == 1 && ToyPassRules.BatDirection(100, 500, 1) == -1 && ToyPassRules.BatDirection(530, 500, -1) == -1 && ToyPassRules.BatDirection(470, 500, 1) == 1,
            "Batted toward the cursor; with the cursor overhead, back the way it came");
        var passes = new ToyPassCount(3);
        check(!passes.Bat() && passes.Count == 0, "A bat with no touch from the user is not a pass");
        passes.Touch(); passes.Touch();
        check(passes.Bat() && !passes.Bat() && passes.Count == 1, "One touch, one pass");
        for (int i = 0; i < 3; i++) { passes.Touch(); passes.Bat(); }
        check(passes.Count == 4 && passes.Best == 4 && !passes.End() && passes.Count == 0 && passes.Best == 4, "A record under five is kept quietly");
        for (int i = 0; i < 6; i++) { passes.Touch(); passes.Bat(); }
        check(passes.Best == 6 && passes.End() && !passes.End(), "Five or more beats the record: said once");
        var shared = new ToyChaseStreak();
        for (int i = 0; i < 19; i++) shared.Passed();
        check(!shared.Resting && shared.PassesMade == 19, "Up to twenty passes in a row");
        shared.Passed();
        check(shared.Resting && !shared.TryMouse(), "...then the rest, shared with the wand and the mouse");
        shared.Step(121, false);
        shared.Pass(60); shared.Wand(29);
        check(!shared.Resting, "Passing and the wand count apart");
        shared.Pass(31);
        check(shared.Resting, "About 90 seconds of passing rests too");
        check(ToyReactRules.Normalize(new List<string> { "ball", "ghost", "ball", "mouse" })!.SequenceEqual(new[] { "ball", "mouse" }) && ToyReactRules.Normalize(new List<string>()) == null,
            "Sniffed toys are known ones, each once");
        check(ToyReactRules.NextToSniff(new[] { "ball", "wand" }, new[] { "ball" }) == "wand" && ToyReactRules.NextToSniff(new[] { "ball" }, new[] { "ball" }) == null && ToyReactRules.NextToSniff(new[] { "box" }, null) == "box",
            "The next toy to sniff is the first one out it never met");

        // Đợt 2 M1 (docs/LIVING-CORNER.md): the scratching post, a living-corner thing with its own limit.
        check(DesktopToyRules.All.Contains(DesktopToyRules.Post) && DesktopToyRules.IsLiving(DesktopToyRules.Post) && !DesktopToyRules.IsLiving(DesktopToyRules.Box),
            "The scratching post is in the basket, in the living-corner group");
        check(DesktopToyRules.CanTake(DesktopToyRules.Post, new[] { "ball", "box", "wand" }) && !DesktopToyRules.CanTake(DesktopToyRules.Mouse, new[] { "ball", "box", "wand", "post" }),
            "Three toys out leave room for the post; the post does not make room for a fourth toy");
        check(!DesktopToyRules.CanTake(DesktopToyRules.Post, new[] { "ball", "post", "post" }) && DesktopToyRules.CanTake(DesktopToyRules.Post, new[] { "ball", "post" }),
            "Two living-corner things out: no more of them");
        var mixed = DesktopToyRules.Normalize(new[] { "ball", "box", "wand", "post", "mouse" }.Select(id => new SavedToy { Id = id }), true)!;
        check(mixed.Select(t => t.Id).SequenceEqual(new[] { "ball", "box", "wand", "post" }), "A saved list keeps three toys and the post");
        check(DesktopToyRules.HalfWidthOf(DesktopToyRules.Post) == 30 && DesktopToyRules.HalfHeightOf(DesktopToyRules.Post) == 55, "The post is about 60 DIPs wide, 110 tall");
        check(PostScratchRules.NextVisit(0) == 360 && PostScratchRules.NextVisit(0.999999) < 600.01 && PostScratchRules.NextVisit(double.NaN) == 360,
            "The cat's own visits are 6–10 minutes apart");
        check(PostScratchRules.StandAt(500, 300, 0, 1000) == 500 - PostScratchRules.StandOff && PostScratchRules.StandAt(500, 700, 0, 1000) == 500 + PostScratchRules.StandOff,
            "The cat scratches from the side it comes from");
        check(PostScratchRules.StandAt(20, 10, 0, 1000) == 20 + PostScratchRules.StandOff && PostScratchRules.StandAt(20, 10, 100, 110) == null,
            "No room on that side: the other side; no room at all: nowhere");

        var feather = new FeatherString();
        for (int i = 0; i < 600; i++) feather.Step(1 / 60.0, 500, 100, 1000);
        check(Math.Abs(feather.X - 500) < 1 && Math.Abs(feather.Y - (100 + FeatherString.Length)) < 1 && feather.Speed < 5, "Held still, the feather hangs on its string under the tip");
        for (int i = 0; i < 10; i++) feather.Step(1 / 60.0, 500 + (i + 1) * 12, 100, 1000);
        check(feather.X < 500 + 120 - 20 && feather.Speed > 200, "The tip swept across: the feather lags behind and swings");
        double far = Math.Sqrt((feather.X - 620) * (feather.X - 620) + (feather.Y - 100) * (feather.Y - 100));
        check(far <= FeatherString.Length + 0.01, "...never farther than the string");
        for (int i = 0; i < 120; i++) feather.Step(1 / 60.0, 620, 950, 1000);
        check(feather.Y <= 1000, "Low down it lies along the floor");
        feather.Pin(640, 1000);
        feather.Step(1 / 60.0, 620, 950, 1000);
        check(feather.Pinned && feather.X == 640, "In the cat's paws it stays");
        feather.Step(1 / 60.0, 620, 800, 1000);
        check(!feather.Pinned, "...until the string is pulled tight");

        var toy = new ToyBall { X = 500 };
        var mouse = new WindUpMouse { Direction = 1 };
        check(mouse.Wind(toy) && mouse.Running && mouse.Winds == 1, "A click winds the mouse up");
        check(!mouse.Wind(toy) && mouse.Winds == 1, "...a click while it runs does nothing");
        double ran = 0, top = 0;
        for (int i = 0; i < 60; i++) { mouse.Step(toy, 1 / 60.0, 30, 2000, null, 18); top = Math.Max(top, Math.Abs(toy.VX)); }
        ran = toy.X - 500;
        check(Math.Abs(top - WindUpMouse.Speed) < 1 && ran > 170 && ran < 190, "It runs along the floor at about 180 DIPs/s");
        for (int i = 0; i < 6 * 60; i++) mouse.Step(toy, 1 / 60.0, 30, 2000, null, 18);
        check(!mouse.Running && toy.AtRest && toy.X - 500 < 5 * 180 && toy.X - 500 > 4 * 180 - 1, "About 5 seconds, slowing down at the end, then it stops");
        var edge = new ToyBall { X = 1990 };
        var turner = new WindUpMouse { Direction = 1 };
        turner.Wind(edge);
        for (int i = 0; i < 30; i++) turner.Step(edge, 1 / 60.0, 30, 2000, null, 18);
        check(turner.Direction == -1 && edge.X < 1990, "At the screen's edge it turns round");
        var landed = new ToyBall { X = 800 };
        var lander = new WindUpMouse { Direction = -1 };
        lander.Wind(landed);
        var lands = new List<(double, double)> { (700, 760) };
        for (int i = 0; i < 60; i++) lander.Step(landed, 1 / 60.0, 30, 2000, lands, 18);
        check(lander.Direction == 1 && landed.X - 18 >= 760, "At a farm land it turns round too");
        var thrown = new ToyBall { X = 500, Height = 300, VX = 200 };
        var tumbler = new WindUpMouse();
        bool flipped = false;
        for (int i = 0; i < 180; i++) { tumbler.Step(thrown, 1 / 60.0, 30, 2000, null, 18); flipped |= tumbler.Flipped; }
        check(flipped && !tumbler.Flipped && !tumbler.Running, "A hard landing turns it over for about a second, then it rights itself");
        check(!tumbler.Wind(new ToyBall { Height = 20 }), "It cannot be wound in the air");

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
            new PetPreferences { ToyPassBest = -4, ToysSniffed = new() { "wand", "wand", "spoon" } }.Save(path);
            var m5 = PetPreferences.Load(path);
            check(m5.ToyPassBest == 0 && m5.ToysSniffed!.SequenceEqual(new[] { "wand" }), "Preferences keep the pass record and the sniffed toys, cleaned up");
        }
        finally { Directory.Delete(folder, true); }
    }
}
