using DesktopPet;

// Mini-games M5 (docs/MINI-GAMES.md): the treasure hunt's pure rules — the 60–120 minute timer and 2 minute
// retry, at most 3 maps a day (a new day starts again), the map's 10 minutes, where the 3 prints go (apart,
// inside the work area, off the cat and what stands on the floor), how bright they are, the hints at 20/40
// seconds, no losing after 3 minutes, the chest beside the cat on its ground, stories without repeats, old files.
internal static class TreasureTests
{
    public static void Run(Action<bool, string> check)
    {
        var random = new Random(5);
        bool delays = Enumerable.Range(0, 300).Select(_ => TreasureRules.NextDelay(random)).All(d => d >= 60 * 60 && d <= 120 * 60);
        check(delays && TreasureRules.RetrySeconds == 120, "A map is found every 60–120 minutes; blocked, it tries again 2 minutes later");

        // At most 3 maps a day, by the computer's date; the next day starts again.
        var prefs = new PetPreferences();
        var day = new DateTime(2026, 11, 12, 9, 0, 0);
        check(prefs.Treasure && TreasureRules.MayFind(prefs, day) && TreasureRules.MapsToday(prefs, day) == 0, "On by default, no map yet today");
        for (int i = 0; i < 3; i++) TreasureRules.CountFind(prefs, day.AddHours(i));
        check(TreasureRules.MapsToday(prefs, day.AddHours(5)) == 3 && !TreasureRules.MayFind(prefs, day.AddHours(5)), "Three maps found: no more today");
        check(TreasureRules.MapsToday(prefs, day.AddDays(1)) == 0 && TreasureRules.MayFind(prefs, day.AddDays(1).Date.AddMinutes(1)), "The next day: maps again");
        TreasureRules.CountFind(prefs, day.AddDays(1));
        check(prefs.TreasureMapsToday == 1 && prefs.TreasureMapDay == day.AddDays(1).Date, "...counted from 1 on the new day");
        prefs.Treasure = false;
        check(!TreasureRules.MayFind(prefs, day.AddDays(1)), "Switched off: no map");

        // The map: 10 minutes, still in Pause (dt 0), fading over its last seconds.
        var map = new TreasureMap();
        map.Give();
        map.Step(0);
        check(map.Held && map.Left == 600 && map.Fade == 1, "A map lasts 10 minutes; Pause (dt 0) keeps its clock");
        for (int i = 0; i < 590 * 10; i++) map.Step(0.1);
        check(map.Held && map.Fade < 1 && map.Fade > 0, "...fading out at the end");
        for (int i = 0; i < 200; i++) map.Step(0.1);
        check(!map.Held && map.Fade == 0, "...then gone");
        map.Give(); map.Take();
        check(!map.Held, "Opening the chest (or the switch) takes it away");

        // The prints: 3, ±400 across and up to 300 above the ground, ≥ 80 apart, in the work area, off the cat and what is taken.
        var work = new Area(0, 0, 1600, 860);
        var taken = new List<Area> { new(900, 660, 1150, 860), new(400, 690, 470, 860) };
        bool placed = true, spread = true, apart = true, inside = true, offCat = true, offTaken = true;
        for (int seed = 0; seed < 200; seed++)
        {
            var r = new Random(seed);
            double cat = 200 + seed * 6;
            var spots = TreasureRules.Place(r, cat, 860, work, taken);
            if (spots == null) { placed = false; continue; }
            placed &= spots.Length == 3;
            foreach (var s in spots)
            {
                spread &= Math.Abs(s.X - cat) <= 400 && s.Y >= 860 - 300 && s.Y < 860;
                inside &= s.X >= work.Left + 16 && s.X <= work.Right - 16 && s.Y >= work.Top + 16;
                offCat &= !(Math.Abs(s.X - cat) < TreasureRules.CatHalfWidth + TreasureRules.PrintRadius && s.Y > 860 - TreasureRules.CatHeight - TreasureRules.PrintRadius);
                offTaken &= taken.All(t => !(s.X + 16 > t.Left && s.X - 16 < t.Right && s.Y + 16 > t.Top));
            }
            for (int a = 0; a < spots.Length; a++)
                for (int b = a + 1; b < spots.Length; b++) apart &= TreasureRules.Distance(spots[a].X, spots[a].Y, spots[b].X, spots[b].Y) >= 80;
        }
        check(placed, "Three prints are always placed on a roomy screen");
        check(spread && inside, "...within ±400 DIPs across and 300 above the cat's ground, inside the work area");
        check(apart, "...at least 80 DIPs apart");
        check(offCat && offTaken, "...never on the cat's body, the farm, toys or the corner things");
        var high = TreasureRules.Place(new Random(1), 800, 140, work, new List<Area>());
        check(high != null && high.All(s => s.Y >= 16 && s.Y < 140), "On a window edge near the top: the prints stay on the screen");
        check(TreasureRules.Place(new Random(1), 100, 860, new Area(0, 0, 120, 860), new List<Area>()) == null, "No room: no prints");

        // Brightness: faint, brighter within 50 DIPs of the cursor, a little brighter as the hint, full once found.
        check(TreasureRules.Glow(false, false, 500) == TreasureRules.Faint && Math.Abs(TreasureRules.Faint - 0.2) < 1e-9, "A print is faint (about 20 %)");
        check(TreasureRules.Glow(false, false, 25) > TreasureRules.Glow(false, false, 45) && TreasureRules.Glow(false, false, 45) > TreasureRules.Faint
            && TreasureRules.Glow(false, false, 50) == TreasureRules.Faint, "...brighter as the cursor comes within 50 DIPs");
        check(TreasureRules.Glow(false, true, 500) > TreasureRules.Faint && TreasureRules.Glow(false, true, 500) < 1, "...a little brighter as the 40 second hint");
        check(TreasureRules.Glow(true, false, 500) == 1, "...full once found");

        // The hunt: finds in any order, hints at 20 and 40 seconds without a new one, no losing at 3 minutes.
        var hunt = new TreasureHunt();
        var three = new[] { new TreasureSpot(300, 700), new TreasureSpot(500, 650), new TreasureSpot(700, 760) };
        hunt.Reset(three);
        check(hunt.At(500, 655) == 1 && hunt.At(420, 655) == -1, "A click on a print finds it; empty space does not");
        check(hunt.Find(2) && !hunt.Find(2) && hunt.Find(0) && hunt.FoundCount == 2 && hunt.Last == 0, "Prints are found in any order, each once");
        for (int i = 0; i < 19 * 10; i++) hunt.Step(0.1);
        check(hunt.Hint == 0, "No hint before 20 seconds");
        for (int i = 0; i < 2 * 10; i++) hunt.Step(0.1);
        check(hunt.Hint == 1, "20 seconds without a new print: the cat turns its head");
        for (int i = 0; i < 20 * 10; i++) hunt.Step(0.1);
        check(hunt.Hint == 2 && hunt.Nearest(320) == 1, "40 seconds: it walks near the print left");
        hunt.Find(1);
        check(hunt.AllFound && hunt.Hint == 0 && !hunt.TimedOut, "All three found");
        hunt.Reset(three);
        hunt.Step(0);
        bool early = false;
        for (int i = 0; i < 1799; i++) early |= hunt.Step(0.1);
        check(!early && !hunt.AllFound, "Before 3 minutes nothing lights up by itself");
        bool lit = false;
        for (int i = 0; i < 5 && !lit; i++) lit = hunt.Step(0.1);
        check(lit && hunt.AllFound && hunt.TimedOut && hunt.Last >= 0, "At 3 minutes the prints left light up: no losing");
        hunt.Reset(three); hunt.Find(1);
        for (int i = 0; i < 2000; i++) hunt.Step(0.1);
        check(hunt.AllFound && hunt.Last == 2, "...the chest comes near the last one lit");

        // Where the cat stands under a print: on its own ground.
        check(TreasureRules.WalkLimit(100, 72) == TreasureRules.WalkSeconds && TreasureRules.WalkLimit(-800, 72) * 72 >= 800 + 72
              && TreasureRules.WalkLimit(2 * TreasureRules.Spread, 72) * 72 > 2 * TreasureRules.Spread && TreasureRules.WalkLimit(800, 0) == TreasureRules.WalkSeconds
              && TreasureRules.WalkLimit(double.NaN, 72) == TreasureRules.WalkSeconds,
            "Walk time to a print: at least 10 s, enough for two prints 2 × Spread apart, safe on a zero speed");
        check(TreasureRules.Under(30, 0, 1600) == 46 && TreasureRules.Under(800, 600, 900) == 800 && TreasureRules.Under(1000, 600, 900) == 854, "Under a print, on the floor or the window edge it stands on");

        // The chest: on the ground near the last print, off the cat, off what is taken when it can be; the cat beside it.
        double? chest = TreasureRules.ChestSpot(520, 500, 0, 1600, new List<(double, double)>());
        check(chest is double c1 && Math.Abs(c1 - 500) >= 46 + 22 + 10 && Math.Abs(c1 - 520) <= 120, "The chest stands near the last print, never on the cat");
        chest = TreasureRules.ChestSpot(1000, 400, 0, 1600, new List<(double, double)> { (900, 1150) });
        check(chest is double c2 && (c2 + 22 <= 900 || c2 - 22 >= 1150), "...off the farm and the toys");
        double? stand = TreasureRules.StandSpot(1000, 400, 0, 1600);
        check(stand is double s1 && Math.Abs(s1 - 1000) == 46 + 22 + 10 && s1 < 1000, "The cat stands beside it, on its side");
        stand = TreasureRules.StandSpot(60, 400, 0, 1600);
        check(stand is double s2 && s2 > 60, "...or on the other side at a screen's edge");
        check(TreasureRules.ChestSpot(500, 500, 450, 560, new List<(double, double)>()) == null, "A window edge too short: no chest spot");

        // Stories: none repeats until all have been opened, then round again in the same order.
        var story = new PetPreferences();
        var seen = new List<int>();
        for (int i = 0; i < 12; i++) { int id = TreasureRules.PickStory(story, random); seen.Add(id); TreasureRules.OpenChest(story, id); }
        check(seen.Distinct().Count() == 12 && seen.All(id => id is >= 1 and <= 12), "Twelve chests: twelve different stories");
        var round = new List<int>();
        for (int i = 0; i < 12; i++) { int id = TreasureRules.PickStory(story, random); round.Add(id); TreasureRules.OpenChest(story, id); }
        check(round.SequenceEqual(seen) && story.TreasureChests == 24 && story.TreasureStories!.Count == 12, "...then they come round again, none twice in a row");

        // Old or edited files read fine.
        var json = "{\"TreasureStories\":[3,3,99,0,5],\"TreasureMapsToday\":17,\"TreasureChests\":-4}";
        string path = Path.Combine(Path.GetTempPath(), $"pet-treasure-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        var loaded = PetPreferences.Load(path);
        File.Delete(path);
        check(loaded.Treasure && loaded.TreasureStories!.SequenceEqual(new[] { 3, 5 }) && loaded.TreasureMapsToday == 3 && loaded.TreasureChests == 0,
            "An edited file is cleaned up: known stories once, at most 3 maps, no negative count");
        File.WriteAllText(path, "{\"Name\":\"Mochi\"}");
        loaded = PetPreferences.Load(path);
        File.Delete(path);
        check(loaded.Treasure && loaded.TreasureStories == null && loaded.TreasureChests == 0 && loaded.TreasureMapDay == null, "An older file without the hunt: switched on, nothing opened yet");
    }
}
