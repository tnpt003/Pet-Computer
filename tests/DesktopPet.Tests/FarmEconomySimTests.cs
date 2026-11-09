using DesktopPet;

// The farm's economy in simulation, to set the stall's decoration prices (docs/FARM-GAME.md,
// finishing milestone B). NOT real play: it uses the real prices, XP, growth clock, cat's choice of
// work and stall visits, but one sitting a day, a fixed walk, no weather and no birds.
// The player looks in every CheckMinutes, sells everything, sows the best crop it can on any free
// tile, and notices NoticeChance of the stall's visits. Tiles come first, then the scarecrow, then
// decorations. "Decor first" spends on decorations before tiles, to see what that delays.
internal static class FarmEconomySimTests
{
    private const int Seeds = 40, MaxDays = 90;
    private const double CheckMinutes = 30, NoticeChance = 0.6, CatFree = 0.8, TripSeconds = 8;
    private static readonly DateTime Start = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly record struct Result(int? FirstDecor, int? AllDecor, int? AllTiles);

    public static void Run(Action<bool, string> check)
    {
        Console.WriteLine("Farm economy (median day, 10th-90th percentile; one sitting a day):");
        foreach (double hours in new[] { 1.0, 3.0, 8.0 })
        {
            var usual = Enumerable.Range(1, Seeds).Select(seed => Play(seed, hours, decorFirst: false)).ToList();
            var eager = Enumerable.Range(1, Seeds).Select(seed => Play(seed, hours, decorFirst: true)).ToList();
            Console.WriteLine($"  {hours}h/day: first decoration {Line(usual, r => r.FirstDecor)} | all six {Line(usual, r => r.AllDecor)} | six tiles {Line(usual, r => r.AllTiles)} | six tiles if decor first {Line(eager, r => r.AllTiles)}");
            if (hours == 3)
            {
                check(Median(usual, r => r.FirstDecor) <= 3, "At 3 hours a day the first decoration comes within 3 days");
                check(Median(usual, r => r.AllDecor) is >= 7 and <= 14, "At 3 hours a day all six take 7 to 14 days");
            }
        }
        check(Play(5, 3, false) == Play(5, 3, false), "The same seed plays the same");
    }

    private static int Median(List<Result> results, Func<Result, int?> pick) => Percentile(results, pick, 0.5);

    // A run that never got there counts as one past the last day.
    private static int Percentile(List<Result> results, Func<Result, int?> pick, double share)
    {
        var days = results.Select(result => pick(result) ?? MaxDays + 1).OrderBy(day => day).ToList();
        return days[(int)Math.Clamp(Math.Round(share * (days.Count - 1)), 0, days.Count - 1)];
    }

    private static string Line(List<Result> results, Func<Result, int?> pick) => $"{Median(results, pick)} ({Percentile(results, pick, 0.1)}-{Percentile(results, pick, 0.9)})";

    private static Result Play(int seed, double hours, bool decorFirst)
    {
        var random = new Random(seed);
        var farm = new PetFarm();
        int? firstDecor = null, allDecor = null, allTiles = null;
        int count = Enum.GetValues<FarmDecor>().Count(decor => !PetFarm.IsGiftDecor(decor));

        for (int day = 1; day <= MaxDays && (allDecor == null || allTiles == null); day++)
        {
            var open = Start.AddDays(day - 1);
            var chores = new FarmChores();
            var stall = new FarmStall();
            double t = 0, end = hours * 3600, nextCheck = 0, nextLook = FarmChores.FirstVisitSeconds, nextFish = FarmChores.FishMinSeconds, nextCook = FarmChores.CookMinSeconds, nextStall = FarmStall.FirstSeconds;
            while (true)
            {
                t = new[] { nextCheck, nextLook, nextFish, nextCook, nextStall }.Min();
                if (t >= end) break;
                var now = open.AddSeconds(t);
                farm.Advance(now);
                if (t == nextCheck)
                {
                    Tend(farm, now);
                    Spend(farm, random, null, decorFirst);
                    nextCheck += CheckMinutes * 60;
                }
                else if (t == nextLook)
                {
                    bool went = random.NextDouble() < CatFree && CatTrip(farm, now);
                    chores.ScheduleAfterLook(went, random);
                    nextLook = t + (went ? TripSeconds : 0) + chores.VisitDue;
                }
                else if (t == nextFish)
                {
                    if (random.NextDouble() < CatFree) farm.Catch(farm.RollFish(random));
                    nextFish = t + FarmChores.NextFish(random);
                }
                else if (t == nextCook)
                {
                    if (random.NextDouble() < CatFree && farm.AutoDish() is FarmDish dish && farm.Pour(dish)) farm.TakePot();
                    nextCook = t + FarmChores.NextCook(random);
                }
                else
                {
                    var offers = farm.NewOffers(random);
                    var decors = farm.NewDecors(random);
                    if (random.NextDouble() < NoticeChance)
                    {
                        foreach (var offer in offers) farm.Deliver(offer);
                        Spend(farm, random, decors, decorFirst);
                    }
                    nextStall = t + FarmStall.StaySeconds + FarmStall.NextDelay(random);
                }
                firstDecor ??= farm.Decors.Count > 0 ? day : null;
                allDecor ??= farm.Decors.Count == count ? day : null;
                allTiles ??= farm.NextLockedTile < 0 ? day : null;
            }
        }
        return new Result(firstDecor, allDecor, allTiles);
    }

    // One tile a trip, chosen by the real rule. False when there was nothing to do.
    private static bool CatTrip(PetFarm farm, DateTime now)
    {
        if (FarmChores.Pick(farm, tile => tile) is not (FarmChoreKind kind, int index)) return false;
        switch (kind)
        {
            case FarmChoreKind.Harvest: farm.Harvest(index); break;
            case FarmChoreKind.Water: farm.Water(index, now); break;
            case FarmChoreKind.Till: farm.Till(index, keepLastCrop: true); break;
            default: if (farm.ResowCrop(index) is FarmCrop crop) farm.Plant(index, crop); break;
        }
        return true;
    }

    // The best crop the coins at hand can sow, free radishes at worst.
    private static FarmCrop Best(PetFarm farm) => Enum.GetValues<FarmCrop>().Where(crop => PetFarm.CropSeason(crop) == null && farm.Unlocked(crop) && farm.Coins >= PetFarm.SeedPrice(crop)).Max();

    private static void Tend(PetFarm farm, DateTime now)
    {
        farm.SellAll();
        for (int i = 0; i < PetFarm.TileCount; i++)
        {
            if (!farm.IsOpen(i)) continue;
            var tile = farm.Tiles[i];
            // Picking a ripe crop by hand is how a tile moves on to something better.
            if (farm.Next(i) == FarmStep.Harvest && tile.Crop < Best(farm)) farm.Harvest(i);
            if (farm.Next(i) == FarmStep.Till) farm.Till(i);
            if (farm.Next(i) == FarmStep.Plant) farm.Plant(i, Best(farm));
            if (farm.Next(i) == FarmStep.Water) farm.Water(i, now);
        }
    }

    // On sale: what the stall has brought this visit, or null when it is not here.
    private static void Spend(PetFarm farm, Random random, List<FarmDecor>? onSale, bool decorFirst)
    {
        farm.SellAll();
        void Decorate()
        {
            foreach (var decor in onSale ?? new()) farm.BuyDecor(decor);
        }
        if (decorFirst) Decorate();
        while (farm.OpenNextTile()) { }
        // A careful player waits for the next tile instead of spending what it is saving.
        if (!decorFirst && farm.NextLockedTile >= 0) return;
        farm.BuyScarecrow();
        if (!decorFirst) Decorate();
    }
}
