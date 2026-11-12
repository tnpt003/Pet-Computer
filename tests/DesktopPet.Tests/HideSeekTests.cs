using DesktopPet;

// Mini-games M2 (docs/MINI-GAMES.md): hide-and-seek's pure rules — three places far enough apart, clear of
// farm land and toys, the box among them when it is out, nothing when they do not fit; how low the cat sinks
// behind each cover; and what a found game adds to the preferences.
internal static class HideSeekTests
{
    public static void Run(Action<bool, string> check)
    {
        var none = new List<(double, double)>();
        bool apart = true, inside = true;
        for (int seed = 0; seed < 50; seed++)
        {
            var spots = HideSeekRules.Place(800, 0, 1600, none, null, new Random(seed));
            apart &= spots is { Count: 3 } && spots.Zip(spots.Skip(1)).All(p => p.Second - p.First >= HideSeekRules.MinApart);
            inside &= spots != null && spots.All(x => x - HideSeekRules.BushHalfWidth >= 0 && x + HideSeekRules.BushHalfWidth <= 1600 && Math.Abs(x - 800) <= HideSeekRules.Reach);
        }
        check(apart, "Three hiding places, at least 150 DIPs apart, left to right");
        check(inside, "...all on the floor and within reach of the cat");

        var land = new List<(double, double)> { (500, 1100) };
        bool clear = true;
        for (int seed = 0; seed < 30; seed++)
        {
            var spots = HideSeekRules.Place(800, 0, 1600, land, null, new Random(seed));
            clear &= spots != null && spots.All(x => x + HideSeekRules.BushHalfWidth <= 500 - HideSeekRules.Gap || x - HideSeekRules.BushHalfWidth >= 1100 + HideSeekRules.Gap);
        }
        check(clear, "No bush on farm land or a toy");

        var withBox = HideSeekRules.Place(800, 0, 1600, none, 640, new Random(4));
        check(withBox is { Count: 3 } && withBox.Contains(640) && withBox.All(x => x == 640 || Math.Abs(x - 640) >= HideSeekRules.MinApart),
            "The box out on the floor is one of the places; bushes keep away from it");
        check(HideSeekRules.Place(150, 0, 300, none, null, new Random(1)) == null, "A floor too narrow for three places: no game");
        var crowded = new List<(double, double)> { (0, 700), (900, 1600) };
        check(HideSeekRules.Place(800, 0, 1600, crowded, null, new Random(1)) == null, "Too crowded: no game");

        check(HideSeekRules.Sink(HideSeekRules.BushHeight) == 112 - HideSeekRules.EarTip - HideSeekRules.BushHeight + 4 &&
              HideSeekRules.Sink(DesktopToyRules.BoxHeight) > HideSeekRules.Sink(HideSeekRules.BushHeight) && HideSeekRules.Sink(500) == 0,
            "The cat sinks until its ear tips are just under its cover's top (deeper behind the lower box)");

        var prefs = new PetPreferences();
        HideSeekRules.RecordFound(prefs, firstTry: true); HideSeekRules.RecordFound(prefs, firstTry: false);
        check(prefs.HideSeekFound == 2 && prefs.HideSeekFirstTry == 1, "Found games and first-try finds are counted");

        string folder = Path.Combine(Path.GetTempPath(), "DesktopPetSeekTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string file = Path.Combine(folder, "preferences.json");
            File.WriteAllText(file, "{\"HideSeekFound\":2,\"HideSeekFirstTry\":9}");
            var loaded = PetPreferences.Load(file);
            check(loaded.HideSeekFound == 2 && loaded.HideSeekFirstTry == 2, "First-try finds never exceed the finds when loaded");
            File.WriteAllText(file, "{}");
            check(PetPreferences.Load(file) is { HideSeekFound: 0, HideSeekFirstTry: 0 }, "Preferences from before hide-and-seek load with none");
        }
        finally { Directory.Delete(folder, true); }
    }
}
