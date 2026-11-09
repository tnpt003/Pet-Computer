using DesktopPet;

internal static class FocusTests
{
    public static void Run(Action<bool, string> check)
    {
        var focus = new PetFocus();
        check(!focus.Running && focus.Step(5) == FocusEvent.None, "No session: the clock does nothing");
        check(!focus.Start(10) && !focus.Running, "Only 25 and 50 minutes can be started");
        check(focus.Start(25) && focus.Focusing && focus.Remaining == 1500, "A session starts with its whole length");
        check(!focus.Start(50) && focus.Minutes == 25, "A running session cannot be replaced");
        focus.Step(600);
        check(focus.Remaining == 1500, "A gap longer than the limit (the computer slept) is not counted");
        focus.Step(0); focus.Step(double.NaN); focus.Step(-3);
        check(focus.Remaining == 1500, "Pause (0) and broken values leave the clock alone");
        check(focus.Step(10) == FocusEvent.None && focus.Remaining == 1490, "Real time counts down");

        for (int i = 0; i < 148; i++) focus.Step(10);
        check(focus.Focusing && focus.LineWaiting == FocusEvent.None && !focus.ChimeWaiting, "Nothing is announced before the end");
        check(focus.Step(10) == FocusEvent.Done && focus.Phase == FocusPhase.Resting && focus.Remaining == 300, "The end of 25 minutes starts a rest of 5");
        check(focus.LineWaiting == FocusEvent.Done && focus.ChimeWaiting, "The chime and the line wait to be delivered");
        focus.ChimeStep(false, 2);
        check(focus.ChimeWaiting, "The chime keeps trying for a moment");
        focus.ChimeStep(false, PetFocus.ChimeGiveUp);
        check(!focus.ChimeWaiting, "It gives up rather than ring long after");
        focus.Stop(); focus.Start(25);
        for (int i = 0; i < 150; i++) focus.Step(10);
        focus.ChimeStep(true, 1);
        check(!focus.ChimeWaiting, "A chime that rang is done");

        for (int i = 0; i < 29; i++) focus.Step(10);
        check(focus.Phase == FocusPhase.Resting && focus.LineWaiting == FocusEvent.Done, "The news waits through the rest");
        check(focus.Step(10) == FocusEvent.RestOver && !focus.Running && focus.LineWaiting == FocusEvent.RestOver, "The rest ends by itself and its news replaces the older one");
        focus.LineSaid();
        check(focus.LineWaiting == FocusEvent.None, "A line said is done");
        check(!focus.Focusing && focus.Step(10) == FocusEvent.None, "No new session starts by itself");

        check(focus.Start(50) && PetFocus.RestMinutes(50) == 10 && PetFocus.RestMinutes(25) == 5, "A long session earns a longer rest");
        focus.Stop();
        check(!focus.Running && focus.Remaining == 0 && focus.LineWaiting == FocusEvent.None && !focus.ChimeWaiting, "Stopping leaves nothing behind");
        focus.Start(25);
        for (int i = 0; i < 150; i++) focus.Step(10);
        check(focus.Phase == FocusPhase.Resting && focus.Start(25) && focus.Focusing && focus.LineWaiting == FocusEvent.None, "A rest can be cut short by the next session, and its news goes with it");
        focus.Stop();

        foreach (var language in new[] { PetLanguage.Vietnamese, PetLanguage.English, PetLanguage.Japanese })
        foreach (var kind in new[] { FocusEvent.Done, FocusEvent.RestOver })
        {
            var (id, text) = PetFocus.LocalLine(kind, language, "Bông", new Random(1));
            check(PetFocus.IsFocusLine(id) && PetFocus.IsDoneLine(id) == (kind == FocusEvent.Done) && !PetBreaks.IsBreakLine(id) && !text.Contains("{name}") && text.Length > 5, $"The {kind} line in {language} has its own id and a filled name");
        }
        check(!PetFocus.IsFocusLine("break:local:vi:0") && !PetFocus.IsFocusLine(null), "Other lines are not focus lines");
        Bloom(check);
    }

    private static void Bloom(Action<bool, string> check)
    {
        const FarmCrop bloom = FarmCrop.FocusBloom;
        var now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        var farm = new PetFarm { Coins = 500 };
        foreach (int tile in new[] { 0, 1, 2 }) farm.Till(tile);

        // Seeds come only from sessions.
        check(!farm.InSeason(bloom) && farm.CanSow(bloom) == 0 && !farm.Plant(0, bloom, now) && farm.Coins == 500, "Without a seed it cannot be sown, whatever the coins");
        check(!farm.SowAll(bloom, now).Any(), "Sow all never takes it");
        check(farm.CompleteFocus(25).Count == 0 && farm.OffSeasonOf(bloom) == 1, "25 minutes give one seed");
        farm.CompleteFocus(50);
        check(farm.OffSeasonOf(bloom) == 3, "50 minutes give two");
        check(farm.Plant(0, bloom, now) && farm.OffSeasonOf(bloom) == 2 && farm.Coins == 500 && farm.Tiles[0].Crop == bloom, "A seed is used, no coins");
        check(farm.Plant(1, bloom, now) && farm.Plant(2, FarmCrop.Radish, now), "Beside another crop");

        // It waits for sessions, not water or the clock.
        check(farm.Next(0) == FarmStep.Wait && !farm.Water(0, now) && farm.TilesAt(FarmStep.Water).SequenceEqual(new[] { 2 }), "It never asks for water, so rain and the cat pass it by");
        check(farm.Advance(now.AddDays(30)).Count == 0 && farm.Tiles[0].Stage == FarmStage.Seed && farm.Next(0) == FarmStep.Wait, "A month does not grow it and it does not wilt");
        check(!farm.CanFertilize(0) && !farm.Fertilize(0, now), "It takes no fertilizer");
        check(farm.CompleteFocus(25).SequenceEqual(new[] { 0, 1 }) && farm.Tiles[0].Stage == FarmStage.Sprout && farm.Tiles[1].Stage == FarmStage.Sprout, "A session moves every bloom on a stage");
        check(farm.Tiles[2].Stage == FarmStage.Seed, "Other crops are not touched");
        check(farm.CompleteFocus(50).SequenceEqual(new[] { 0, 1 }) && farm.Tiles[0].Stage == FarmStage.Ripe && farm.Next(0) == FarmStep.Harvest, "50 minutes move it two stages, never past ripe");
        check(farm.CompleteFocus(50).Count == 0 && farm.Tiles[0].Stage == FarmStage.Ripe, "A ripe bloom stays as it is");

        // The harvest and the systems it stays out of.
        check(farm.Harvest(0, random: new Random(1)) == bloom && !farm.LastHarvestGold && farm.GoldOf(bloom) == 0 && farm.StockOf(bloom) == 1, "It is picked into the store and is never golden");
        check(farm.Xp == 12 && PetFarm.SellPrice(bloom) == 40 && farm.Sell(bloom) == 40 && farm.Coins == 540, "12 XP, sold for 40 coins");
        check(farm.ResowCrop(0) == null, "The cat does not sow it again");
        farm.Stock[bloom.ToString()] = 5;
        for (int seed = 0; seed < 40; seed++)
        {
            var random = new Random(seed);
            var only = new PetFarm { Stock = { [bloom.ToString()] = 5 } };
            check(only.NewVisitorRequest(random) == null, "A visitor does not ask for it");
        }
        for (int seed = 0; seed < 200; seed++)
        {
            var giver = new PetFarm { Stock = { ["Radish"] = 3 } };
            var gift = giver.GiveVisitor(new() { ["Radish"] = 1 }, new Random(seed));
            check(gift?.Crop != bloom, "No visitor gives its seed");
        }
        var stall = new PetFarm { Xp = 2000, Stock = { [bloom.ToString()] = 5 } };
        check(Enumerable.Range(0, 200).All(seed => stall.NewOffers(new Random(seed)).All(offer => !offer.Items.ContainsKey(bloom.ToString()))), "The stall does not ask for it");
        check(AchievementStats.From(new PetFarm { Harvested = { [bloom.ToString()] = 1 } })["farmCrops"] == 0, "It does not count toward the kinds of crop");

        // Saved, capped.
        string folder = Path.Combine(Path.GetTempPath(), "DesktopPetFocusTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "farm.json");
            check(farm.Save(path) && PetFarm.Load(path) is { Version: PetFarm.CurrentVersion } back && back.Tiles[1].Crop == bloom && back.OffSeasonOf(bloom) == farm.OffSeasonOf(bloom), "Seeds and blooms are saved");
            File.WriteAllText(path, "{\"Version\":5,\"Coins\":33,\"OffSeasonSeeds\":{\"Corn\":2}}");
            check(PetFarm.Load(path) is { Coins: 33, Version: PetFarm.CurrentVersion } old && old.OffSeasonOf(FarmCrop.Corn) == 2, "A version 5 field comes over whole");
        }
        finally { Directory.Delete(folder, true); }
        var full = new PetFarm();
        for (int i = 0; i < 60; i++) full.CompleteFocus(50);
        check(full.OffSeasonOf(bloom) == PetFarm.FocusSeedsMax, "Seeds stop at the cap");
    }
}
