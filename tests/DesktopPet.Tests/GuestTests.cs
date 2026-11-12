using DesktopPet;

// Mini-games M4 (docs/MINI-GAMES.md): visitors' pure rules — the 40–90 minute wait and the 2 minute retry,
// the butterfly's way across above the cat, the slime stopping about 80 DIPs from the cat, greeting and
// going (back, when the cat is on the floor), never into the cat's body; clicks; the cat's chase limits; the count.
internal static class GuestTests
{
    public static void Run(Action<bool, string> check)
    {
        var random = new Random(3);
        bool delays = Enumerable.Range(0, 200).Select(_ => GuestRules.NextDelay(random)).All(d => d >= 40 * 60 && d <= 90 * 60);
        check(delays && GuestRules.RetrySeconds == 120, "A visitor comes every 40–90 minutes; blocked, it tries again 2 minutes later");
        var kinds = Enumerable.Range(0, 200).Select(_ => GuestRules.Pick(random)).ToList();
        check(kinds.Contains(GuestKind.Butterfly) && kinds.Contains(GuestKind.Slime), "Each visit picks one of the two guests");

        const double left = 0, right = 1600, top = 0, floor = 860;
        GuestCat FloorCat(double x) => new(x, floor, true);
        // Run a visit to its end; whether it ever touched the cat, how long it took, what it did.
        (bool Touched, double Seconds, bool Greeted, double ClosestGap, int EndSide) Visit(Guest guest, GuestCat cat, Action<Guest>? each = null)
        {
            bool touched = false;
            double closest = double.MaxValue;
            int frames = 0;
            while (guest.Active && frames++ < 60 * 40)
            {
                guest.Step(1 / 60.0, cat);
                each?.Invoke(guest);
                if (!guest.Active) break;
                touched |= guest.Touches(guest.X, guest.Y, cat);
                closest = Math.Min(closest, Math.Abs(guest.X - cat.Center) - GuestRules.CatHalfWidth - GuestRules.SlimeHalf);
            }
            return (touched, frames / 60.0, guest.Greeted, closest, Math.Sign(guest.X - cat.Center));
        }

        // The butterfly: from one edge to the other, near the cat, always above its head, 8–15 seconds.
        bool butterflyStarts = true, butterflyClear = true, butterflyTime = true, crossed = true, near = true;
        for (int seed = 0; seed < 30; seed++)
        {
            var guest = new Guest();
            var cat = FloorCat(300 + seed * 40);
            butterflyStarts &= guest.Start(GuestKind.Butterfly, new Random(seed), left, right, top, floor, cat);
            int from = guest.Direction;
            double lowest = double.MaxValue;
            var run = Visit(guest, cat, g => { if (Math.Abs(g.X - cat.Center) < 60) lowest = Math.Min(lowest, cat.Top - g.Y); });
            butterflyClear &= !run.Touched;
            butterflyTime &= run.Seconds is >= 7.5 and <= 16;
            crossed &= run.EndSide == from;
            near &= lowest < 90;
        }
        check(butterflyStarts, "The butterfly starts wherever the cat is");
        check(butterflyClear, "The butterfly never flies into the cat's body");
        check(butterflyTime, "...crosses in about 8–15 seconds");
        check(crossed && near, "...from one edge to the other, passing just over the cat's head");

        // The slime on the floor: stops about 80 DIPs from a cat on the floor, greets 1.5 s, goes back.
        bool slimeStarts = true, slimeClear = true, stopped = true, greeted = true, back = true;
        for (int seed = 0; seed < 30; seed++)
        {
            var guest = new Guest();
            var cat = FloorCat(300 + seed * 33);
            slimeStarts &= guest.Start(GuestKind.Slime, new Random(seed), left, right, top, floor, cat);
            int from = guest.Direction;
            double greetAt = double.NaN, greetFor = 0;
            var run = Visit(guest, cat, g => { if (g.Phase == GuestPhase.Greeting) { greetAt = Math.Abs(g.X - cat.Center) - GuestRules.CatHalfWidth - GuestRules.SlimeHalf; greetFor += 1 / 60.0; } });
            slimeClear &= !run.Touched && guest.Y == floor;
            stopped &= Math.Abs(greetAt - GuestRules.SlimeStop) < 3;
            greeted &= run.Greeted && Math.Abs(greetFor - GuestRules.GreetSeconds) < 0.05;
            back &= run.EndSide == -from; // Out the edge it came from: it ended on its own side of the cat.
        }
        check(slimeStarts, "The slime starts wherever the cat is");
        check(slimeClear, "The slime crawls on the floor and never into the cat's body");
        check(stopped, "...stops about 80 DIPs from the cat");
        check(greeted, "...bounces hello for 1.5 seconds");
        check(back, "...and with the cat on the floor goes back the way it came");

        // The cat on a window edge: the slime passes under it and leaves by the other edge.
        {
            var guest = new Guest();
            var cat = new GuestCat(800, 500, false);
            check(guest.Start(GuestKind.Slime, new Random(1), left, right, top, floor, cat), "The slime starts with the cat on a window");
            int from = guest.Direction;
            var run = Visit(guest, cat);
            check(!run.Touched && run.Greeted && run.EndSide == from, "With the cat up on a window, the slime greets and crawls on to the other edge");
        }

        // No room: a cat hard against both sides of a narrow screen.
        check(!new Guest().Start(GuestKind.Slime, new Random(1), 0, 260, 0, 860, FloorCat(130)), "No room to stop before the cat: no slime");
        check(!new Guest().Start(GuestKind.Butterfly, new Random(1), 0, 150, 0, 860, FloorCat(75)), "A screen too narrow: no visitor");

        // Clicks: the butterfly zooms away (faster, up); the slime bounces once on the spot and goes on.
        {
            var guest = new Guest();
            var cat = FloorCat(800);
            guest.Start(GuestKind.Butterfly, new Random(2), left, right, top, floor, cat);
            for (int i = 0; i < 60; i++) guest.Step(1 / 60.0, cat);
            double x = guest.X, y = guest.Y;
            check(guest.Click() && guest.Zooming && !guest.Click(), "A click sends the butterfly off (once)");
            guest.Step(0.5, cat);
            check(Math.Abs(guest.X - x) > guest.Speed * 0.5 * 2 && guest.Y < y, "...faster and up");
            var zoom = Visit(guest, cat);
            check(!zoom.Touched, "...still never into the cat");
        }
        {
            var guest = new Guest();
            var cat = FloorCat(800);
            guest.Start(GuestKind.Slime, new Random(2), left, right, top, floor, cat);
            for (int i = 0; i < 60; i++) guest.Step(1 / 60.0, cat);
            double x = guest.X;
            check(guest.Click() && guest.Hop >= 0 && !guest.Click(), "A click makes the slime bounce (once at a time)");
            for (int i = 0; i < 20; i++) guest.Step(1 / 60.0, cat);
            check(guest.X == x, "...on the spot");
            for (int i = 0; i < 20; i++) guest.Step(1 / 60.0, cat);
            check(guest.Hop < 0 && guest.X != x, "...then it goes on");
        }

        // The cat moved in the way (dropped there): a slime turns away rather than crawl into it.
        {
            var guest = new Guest();
            var cat = FloorCat(1200);
            guest.Start(GuestKind.Slime, new Random(5), left, right, top, floor, cat);
            for (int i = 0; i < 60; i++) guest.Step(1 / 60.0, cat);
            var dropped = FloorCat(guest.X + guest.Direction * (GuestRules.CatHalfWidth + GuestRules.SlimeHalf + 14));
            var run = Visit(guest, dropped);
            check(!run.Touched, "A cat dropped just ahead of the slime is never crawled into");
        }

        // Leaving early fades out quickly; a stuck visit ends by itself.
        {
            var guest = new Guest();
            var cat = FloorCat(800);
            guest.Start(GuestKind.Butterfly, new Random(4), left, right, top, floor, cat);
            for (int i = 0; i < 30; i++) guest.Step(1 / 60.0, cat);
            guest.Leave();
            for (int i = 0; i < 19; i++) guest.Step(1 / 60.0, cat);
            check(!guest.Active, "Leaving early fades the guest out within ~0.3 s");
        }

        // The cat's chase: on its own ground, at most 150 DIPs, toward the guest, keeping a gap from a slime.
        check(GuestRules.ChaseTarget(500, 900, 0, 1600, 0) == 650, "The cat follows at most 150 DIPs");
        check(GuestRules.ChaseTarget(500, 300, 0, 1600, 0) == 350, "...either way");
        check(GuestRules.ChaseTarget(500, 900, 420, 600, 0) == 600 - GuestRules.CatHalfWidth, "...never off the window edge it stands on");
        check(GuestRules.ChaseTarget(500, 560, 0, 1600, 114) == 500, "...and never closer to a slime than the gap");
        var watcher = new GuestWatcher();
        watcher.Reset(new Random(1)); watcher.ForceFollow(true);
        check(!watcher.BeginChase(500, 900) && watcher.BeginChase(500, 700) && !watcher.BeginChase(500, 700), "The chase starts once, when the guest is near");
        bool ran = true;
        for (int i = 0; i < 60 * 2; i++) ran &= watcher.StepChase(1 / 60.0, 500 + i);
        check(ran && !watcher.StepChase(1 / 60.0, 500 + 151) && watcher.Chased, "...and ends at 150 DIPs");
        watcher.Reset(new Random(1)); watcher.ForceFollow(true); watcher.BeginChase(500, 700);
        int frames = 0;
        while (watcher.StepChase(1 / 60.0, 500) && frames < 1000) frames++;
        check(Math.Abs(frames / 60.0 - GuestRules.ChaseSeconds) < 0.05, "...or after 3 seconds");
        check(watcher.MayPounce(500, 540) && !watcher.MayPounce(500, 600), "A pounce when the butterfly is near");
        watcher.Pounce();
        check(!watcher.MayPounce(500, 520), "...only once a visit");
        watcher.Reset(new Random(1)); watcher.ForceFollow(false);
        check(!watcher.BeginChase(500, 520) && !watcher.MayPounce(500, 520), "Not following: no chase and no pounce");
        int follows = 0;
        var chance = new Random(9);
        for (int i = 0; i < 1000; i++) { watcher.Reset(chance); if (watcher.Follows) follows++; }
        check(follows is > 420 and < 580, "The cat follows about half the visits");

        var preferences = new PetPreferences();
        check(preferences.Guests && preferences.GuestVisits == 0, "Visitors are on by default");
        GuestRules.CountVisit(preferences); GuestRules.CountVisit(preferences);
        check(preferences.GuestVisits == 2, "Each visit is counted");
    }
}
