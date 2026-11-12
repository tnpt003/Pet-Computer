using DesktopPet;

// Mini-games M1 (docs/MINI-GAMES.md): the cup game's pure rules — swaps per round and their speed, the fish
// staying with its cup through every swap, cups moving smoothly (never a jump), where the row goes next to the
// cat, picking by position, and the best and game count kept in the preferences.
internal static class CupGameTests
{
    public static void Run(Action<bool, string> check)
    {
        check(CupGameRules.Swaps(1) == 3 && CupGameRules.Swaps(2) == 5 && CupGameRules.Swaps(3) == 7 && CupGameRules.Swaps(9) == 7,
            "Round 1 swaps 3 times, round 2 five, from round 3 on seven");
        check(CupGameRules.SwapSeconds(1) == 0.6 && CupGameRules.SwapSeconds(2) == 0.45 && CupGameRules.SwapSeconds(3) == 0.35 && CupGameRules.SwapSeconds(10) == 0.35,
            "Each swap gets quicker up to round 3, never faster than 0.35 s");

        // The fish follows its cup; the cups always fill the three slots.
        var shuffle = new CupShuffle();
        bool fishKept = true, slotsFull = true, smooth = true, timed = true;
        for (int seed = 0; seed < 40; seed++)
        {
            shuffle.Reset();
            var random = new Random(seed);
            for (int round = 1; round <= 4; round++)
            {
                shuffle.Start(round, random);
                fishKept &= shuffle.SlotOf(shuffle.FishCup) == 1 && shuffle.SwapCount == CupGameRules.Swaps(round);
                int fishCup = shuffle.FishCup;
                var last = Enumerable.Range(0, 3).Select(c => shuffle.Pose(c).X).ToArray();
                int frames = 0;
                while (shuffle.Shuffling && frames < 10000)
                {
                    shuffle.Step(1 / 60.0); frames++;
                    for (int c = 0; c < 3; c++)
                    {
                        double x = shuffle.Pose(c).X;
                        smooth &= Math.Abs(x - last[c]) <= 2 * CupGameRules.CupSpacing / (CupGameRules.SwapSeconds(round) * 60) * 1.6 + 0.5;
                        last[c] = x;
                    }
                }
                timed &= Math.Abs(frames / 60.0 - CupGameRules.Swaps(round) * CupGameRules.SwapSeconds(round)) < 0.05;
                fishKept &= shuffle.FishCup == fishCup;
                slotsFull &= Enumerable.Range(0, 3).Select(shuffle.SlotOf).OrderBy(s => s).SequenceEqual(new[] { 0, 1, 2 })
                    && Enumerable.Range(0, 3).All(s => shuffle.SlotOf(shuffle.CupIn(s)) == s);
            }
        }
        check(fishKept && slotsFull, "The fish stays under its cup through every swap, and the three cups always fill the three slots");
        check(smooth, "During a swap the cups glide, never jump");
        check(timed, "A round's shuffle lasts its swaps times the swap time");
        shuffle.Reset(); shuffle.Start(1, new Random(3));
        bool frontArcs = false;
        for (int i = 0; i < 20; i++) { shuffle.Step(0.02); for (int c = 0; c < 3; c++) frontArcs |= shuffle.Pose(c).Front && shuffle.Pose(c).Rise > 10; }
        check(frontArcs, "One cup of a swap hops over the other in front");

        // Where the cups go: next to the cat on the roomier side; around land; nowhere: null.
        double half = PetSettings.CenterX;
        var none = new List<(double, double)>();
        var place = CupGameRules.Place(300, half, 0, 1600, none, 300);
        check(place is { CatCentre: 300, Side: 1 } && CupGameRules.RowLeft(300, 1) > 300 + half,
            "Room right there: the cat stays and the cups go on its roomier side, clear of it");
        place = CupGameRules.Place(1500, half, 0, 1600, none, 300);
        check(place is { CatCentre: 1500, Side: -1 } && CupGameRules.RowLeft(1500, -1) + CupGameRules.RowWidth < 1500 - half,
            "Near the right edge they go to its left");
        var land = new List<(double, double)> { (340, 900) };
        place = CupGameRules.Place(300, half, 0, 1600, land, 300);
        check(place is { } p1 && p1.Side == -1 && CupGameRules.RowLeft(p1.CatCentre, -1) >= 4,
            "A farm field on the roomier side: the cups go on the other side");
        var crowded = new List<(double, double)> { (0, 250), (350, 1600) };
        check(CupGameRules.Place(300, half, 0, 1600, crowded, 300) == null, "No room within reach: no game");
        var far = new List<(double, double)> { (0, 520) };
        place = CupGameRules.Place(560, half, 0, 1600, far, 300);
        check(place is { } p2 && CupGameRules.RowLeft(p2.CatCentre, p2.Side) >= 520 + CupGameRules.Gap,
            "Hemmed in on one side: the row goes where it fits");
        check(CupGameRules.Place(300, half, 250, 300, none, 300) == null, "Ground narrower than the cat: no game");

        // Picking by position across the row.
        check(CupGameRules.SlotAt(CupGameRules.SlotCentre(0)) == 0 && CupGameRules.SlotAt(CupGameRules.SlotCentre(2) + 20) == 2 &&
              CupGameRules.SlotAt((CupGameRules.SlotCentre(0) + CupGameRules.SlotCentre(1)) / 2) == -1 && CupGameRules.SlotAt(-30) == -1,
            "A click picks the cup under it; between cups picks nothing");

        // The best goes up at once; "New best!" only from three rounds; games are counted.
        var prefs = new PetPreferences();
        check(CupGameRules.RaiseBest(prefs, 1) && prefs.CupGameBest == 1 && !CupGameRules.TellBest(true, 1), "A first right cup is a best, but not told");
        check(CupGameRules.RaiseBest(prefs, 3) && CupGameRules.TellBest(true, 3) && !CupGameRules.RaiseBest(prefs, 2) && prefs.CupGameBest == 3,
            "Three in a row is a new best worth telling; fewer never lowers it");
        check(!CupGameRules.RaiseBest(prefs, 99) || prefs.CupGameBest == CupGameRules.MaxRounds, "The best stops at ten rounds");
        CupGameRules.CountPlay(prefs); CupGameRules.CountPlay(prefs);
        check(prefs.CupGamePlays == 2, "Each game that ends the user's way is counted");

        string folder = Path.Combine(Path.GetTempPath(), "DesktopPetCupTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string file = Path.Combine(folder, "preferences.json");
            File.WriteAllText(file, "{\"Coat\":\"Calico\"}");
            var old = PetPreferences.Load(file);
            check(old.CupGameBest == 0 && old.CupGamePlays == 0 && old.Coat == "Calico", "Preferences from before the cup game load with no best");
            File.WriteAllText(file, "{\"CupGameBest\":55,\"CupGamePlays\":-4}");
            var bad = PetPreferences.Load(file);
            check(bad.CupGameBest == CupGameRules.MaxRounds && bad.CupGamePlays == 0, "A bad saved best or count is put back in range");
        }
        finally { Directory.Delete(folder, true); }
    }
}
