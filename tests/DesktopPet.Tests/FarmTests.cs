using System.IO;
using DesktopPet;

// The field's rules (1.5.1 M3): the order of the steps, growth by the clock, the save file, the lift.
internal static class FarmTests
{
    public static void Run(Action<bool, string> check)
    {
        var start = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        Steps(check, start);
        Clock(check, start);
        Saving(check, start);
        Lift(check);
        Chores(check, start);
        Economy(check, start);
        Links(check, start);
        Bulk(check, start);
        Levels(check);
        Stall(check);
        Rhythm(check, start);
        Hearth(check);
        Pond(check);
        Lands(check);
    }

    // Three pieces of land on one floor (docs/FARM-GAME.md).
    private static void Lands(Action<bool, string> check)
    {
        var field = new[] { (100.0, 200.0) };
        check(FarmLands.FreeSpot(400, 50, field, 0, 900, 6) == 400, "A free place is taken as it is");
        check(FarmLands.FreeSpot(120, 50, field, 0, 900, 6) == 44, "On other land it goes to the nearest side");
        check(FarmLands.FreeSpot(190, 50, field, 0, 900, 6) == 206, "...which may be the right");
        check(FarmLands.FreeSpot(40, 200, field, 0, 900, 6) == 206, "The other side when one has no room");
        check(FarmLands.FreeSpot(-30, 50, Array.Empty<(double, double)>(), 0, 900, 6) == 0, "It stays on the screen");
        check(FarmLands.FreeSpot(120, 500, field, 0, 150, 6) == 120, "With no room anywhere it stays where it is");
        check(FarmLands.FreeSpot(206, 50, field, 0, 900, 6) == 206, "Exactly at the gap counts as free");
        var two = new[] { (100.0, 200.0), (210.0, 300.0) };
        check(FarmLands.FreeSpot(205, 50, two, 0, 900, 6) == 306, "A gap too narrow is skipped");

        string folder = Path.Combine(Path.GetTempPath(), "pet-lands-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "farm.json");
        try
        {
            var saved = new PetFarm { HearthFractionX = 0.4, PondFractionX = 0.8 };
            saved.Save(path);
            var back = PetFarm.Load(path);
            check(back.HearthFractionX == 0.4 && back.PondFractionX == 0.8 && back.Version == 3, "Where the hearth and pond stand is saved");
            File.WriteAllText(path, "{\"Version\":3,\"HearthFractionX\":5,\"PondFractionX\":-2}");
            var odd = PetFarm.Load(path);
            check(odd.HearthFractionX == 1 && odd.PondFractionX == 0, "Places out of range are brought in");
            check(new PetFarm().HearthFractionX == null, "A field that never had them starts without");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    // The pond and its fish (docs/FARM-GAME.md).
    private static void Pond(Action<bool, string> check)
    {
        check(new PetPreferences().FarmAutoFish, "The cat fishes by itself by default");
        var random = new Random(5);
        var farm = new PetFarm();
        int Count(PetFarm field, FarmFish kind) { int n = 0; var r = new Random(5); for (int i = 0; i < 4000; i++) if (field.RollFish(r) == kind) n++; return n; }
        check(Count(farm, FarmFish.Goldfish) == 0, "No golden fish before its level");
        int carp = Count(farm, FarmFish.Carp);
        check(carp > 4000 * 0.26 && carp < 4000 * 0.34, $"Before it, a carp takes its place: about 30% carp ({carp})");
        var high = new PetFarm { Xp = 2000 };
        int gold = Count(high, FarmFish.Goldfish), carpHigh = Count(high, FarmFish.Carp);
        check(gold > 4000 * 0.03 && gold < 4000 * 0.07, $"At its level about 5% golden ({gold})");
        check(carpHigh > 4000 * 0.21 && carpHigh < 4000 * 0.29, $"...about 25% carp ({carpHigh})");

        farm.Catch(FarmFish.Minnow); farm.Catch(FarmFish.Carp);
        check(farm.FishOf(FarmFish.Minnow) == 1 && farm.FishOf(FarmFish.Carp) == 1 && farm.Xp == 11, "A catch goes to the store and gives XP");
        check(farm.StockValue == 20, "Fish count towards the store's worth");
        check(farm.Sell(FarmFish.Carp) == 15 && farm.FishOf(FarmFish.Carp) == 0 && farm.Sell(FarmFish.Carp) == 0, "A fish sells once, at its price");
        int coins = farm.Coins;
        check(farm.SellAll() == 5 && farm.Coins == coins + 5 && farm.Fish.Count == 0, "Selling all takes the fish too");

        check(!farm.CanCook(FarmDish.GrilledFish), "No fish, no grilled fish");
        farm.Catch(FarmFish.Minnow);
        check(farm.CanCook(FarmDish.GrilledFish) && farm.Pour(FarmDish.GrilledFish) && farm.FishOf(FarmFish.Minnow) == 0 && farm.PotDish == FarmDish.GrilledFish, "A dish uses its fish");
        check(PetFarm.Recipe(FarmDish.GrilledFish).Length == 0 && PetFarm.FishIn(FarmDish.GrilledFish) == FarmFish.Minnow, "Grilled fish takes one small fish and nothing else");
        check(PetFarm.Recipe(FarmDish.CarpTomato).Single() == (FarmCrop.Tomato, 1) && PetFarm.FishIn(FarmDish.CarpTomato) == FarmFish.Carp, "Carp with tomato");
        check(PetFarm.Recipe(FarmDish.GoldfishHotpot).Single() == (FarmCrop.Pumpkin, 1) && PetFarm.FishIn(FarmDish.GoldfishHotpot) == FarmFish.Goldfish, "Golden fish hotpot");
        check(PetFarm.DishLevel(FarmDish.GrilledFish) == 1 && PetFarm.DishLevel(FarmDish.CarpTomato) == 4 && PetFarm.DishLevel(FarmDish.GoldfishHotpot) == 7, "Their levels");
        check(PetFarm.FishIn(FarmDish.RadishSoup) == null && PetFarm.Recipe(FarmDish.PumpkinPie).Length == 2, "The other dishes are as they were");

        var store = new PetFarm { Xp = 2000 };
        store.Fish["Minnow"] = 4;
        check(store.AutoDish() == null, "The cat leaves four of a fish too");
        store.Fish["Minnow"] = 5;
        check(store.AutoDish() == FarmDish.GrilledFish, "With five it grills one");
        store.Fish["Goldfish"] = 50; store.Stock["Pumpkin"] = 50;
        check(store.AutoDish() == FarmDish.PumpkinPie || store.AutoDish() == FarmDish.GrilledFish, "(what is left)");
        store.Stock.Remove("Pumpkin");
        check(store.AutoDish() == FarmDish.GrilledFish, "It never spends a golden fish");
        store.Stock["Pumpkin"] = 50; store.Stock["Carrot"] = 0;
        check(store.CanCook(FarmDish.GoldfishHotpot), "The user can");

        string folder = Path.Combine(Path.GetTempPath(), "pet-fish-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "farm.json");
        try
        {
            var saved = new PetFarm();
            saved.Catch(FarmFish.Carp);
            saved.Save(path);
            check(PetFarm.Load(path).FishOf(FarmFish.Carp) == 1 && PetFarm.Load(path).Version == 3, "Fish are saved, the file stays at version 3");
            File.WriteAllText(path, "{\"Version\":3,\"Fish\":{\"Shark\":3,\"Carp\":-2,\"Minnow\":2}}");
            var odd = PetFarm.Load(path);
            check(odd.Fish.Count == 1 && odd.FishOf(FarmFish.Minnow) == 2, "Unknown or negative fish are dropped");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        var chores = new FarmChores();
        check(chores.FishDue == FarmChores.FishMinSeconds && chores.FishAgain == 0, "It does not fish at once");
        chores.ScheduleFishAgain(5);
        chores.CountDownFish(2);
        check(chores.FishDue == FarmChores.FishMinSeconds - 2 && chores.FishAgain == 3, "Both waits run down");
        for (int i = 0; i < 40; i++)
        {
            double wait = FarmChores.NextFish(random), bite = FarmChores.NextBite(random);
            if (wait < 480 || wait > 720 || bite < 4 || bite > 10) { check(false, $"A wait out of range ({wait:F0}, {bite:F1})"); break; }
        }
        chores.BeginFishing(50, -1, 5, FarmFish.Carp, byUser: true);
        check(chores.Kind == FarmChoreKind.Fishing && chores.ByUser && chores.Catch == FarmFish.Carp && chores.FishSide == -1 && chores.BiteAt == FarmChores.CastSeconds + 5, "Off to the pond");
        check(!chores.Step(FarmChores.WalkLimitSeconds + 1) && !chores.WalkedTooLong, "The pond may be further than a tile");

        // What the weather on show gives (milestone 5).
        int Rolled(PetFarm field, FarmFish kind, WeatherKind? sky) { int n = 0; var r = new Random(8); for (int i = 0; i < 4000; i++) if (field.RollFish(r, sky) == kind) n++; return n; }
        var skyFarm = new PetFarm { Xp = 2000 };
        int snowCarp = Rolled(skyFarm, FarmFish.Carp, WeatherKind.Snow), nightGold = Rolled(skyFarm, FarmFish.Goldfish, WeatherKind.Night);
        check(snowCarp > 4000 * 0.46 && snowCarp < 4000 * 0.54, $"Snow: about half are carp ({snowCarp})");
        check(nightGold > 4000 * 0.08 && nightGold < 4000 * 0.12, $"Night: about 10% golden ({nightGold})");
        check(Rolled(new PetFarm(), FarmFish.Goldfish, WeatherKind.Night) == 0, "...still not before its level");
        check(Rolled(skyFarm, FarmFish.Carp, WeatherKind.Sunny) == Rolled(skyFarm, FarmFish.Carp, null), "Other weather changes no catch");
        var nightRandom = new Random(2);
        bool nightBites = Enumerable.Range(0, 40).All(_ => FarmChores.NextBite(nightRandom, night: true) is >= 2 and <= 5);
        check(nightBites, "Night: bites come in 2 to 5 seconds");
        var gifted = new PetFarm();
        gifted.Tiles[0] = new FarmTile { Stage = FarmStage.Ripe, Crop = FarmCrop.Carrot };
        check(gifted.Harvest(0, extra: true) == FarmCrop.Carrot && gifted.StockOf(FarmCrop.Carrot) == 2 && gifted.HarvestedCount(FarmCrop.Carrot) == 1 && gifted.Xp == 5, "A sunset's extra goes to the store, with no extra XP");
        var pollenFarm = new PetFarm();
        pollenFarm.Tiles[0] = new FarmTile { Stage = FarmStage.Ripe, Crop = FarmCrop.Carrot };
        check(pollenFarm.Harvest(0, pollen: true) == FarmCrop.Carrot && pollenFarm.Xp == 8 && pollenFarm.StockOf(FarmCrop.Carrot) == 1, "Pollen: half as much XP again, rounded up");
        chores.Arrive();
        check(!chores.Step(chores.BiteAt + FarmChores.ReelSeconds - 0.1), "Nothing is caught until it is reeled in");
        check(chores.Step(0.2) && !chores.Finished, "...then the fish lands once");
        chores.Step(FarmChores.FishShowSeconds);
        check(chores.Finished, "...and the trip ends a moment later");
    }

    // The pot on the hearth (docs/FARM-GAME.md).
    private static void Hearth(Action<bool, string> check)
    {
        check(new PetPreferences().FarmAutoCook, "The cat cooks by itself by default");
        var farm = new PetFarm();
        check(!farm.Pour(FarmDish.RadishSoup) && farm.PotDish == null, "Nothing to pour from an empty store");
        farm.Stock["Radish"] = 3;
        check(farm.Pour(FarmDish.RadishSoup) && farm.PotDish == FarmDish.RadishSoup && farm.StockOf(FarmCrop.Radish) == 1, "Pouring uses the ingredients up");
        farm.Stock["Radish"] = 3;
        check(!farm.Pour(FarmDish.RadishSoup) && farm.StockOf(FarmCrop.Radish) == 3, "One dish in the pot at a time");
        check(farm.TakePot() == FarmDish.RadishSoup && farm.PotDish == null && farm.TakePot() == null, "Taken once");

        string folder = Path.Combine(Path.GetTempPath(), "pet-pot-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "farm.json");
        try
        {
            var saved = new PetFarm { PotDish = FarmDish.PumpkinPie };
            check(saved.Save(path) && PetFarm.Load(path).PotDish == FarmDish.PumpkinPie && File.ReadAllText(path).Contains("PumpkinPie"), "The pot is saved by name");
            check(saved.Version == 3, "The file stays at version 3");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        var store = new PetFarm();
        store.Stock["Radish"] = 5;
        check(store.AutoDish() == null, "The cat leaves the store its four");
        store.Stock["Radish"] = 6;
        check(store.AutoDish() == FarmDish.RadishSoup, "With enough to spare it cooks");
        store.Stock["Carrot"] = 6; store.Stock["Pumpkin"] = 5; store.Stock["Tomato"] = 5;
        check(store.AutoDish() == FarmDish.RadishSoup, "Only what its level has opened");
        store.Xp = 2000;
        check(store.AutoDish() == FarmDish.PumpkinPie, "The best dish it can spare");
        store.Stock["Radish"] = 0; store.Stock["Carrot"] = 4;
        check(store.AutoDish() == null, "No dish that would break into the four");

        var chores = new FarmChores();
        check(chores.CookDue == FarmChores.CookMinSeconds, "It does not cook at once");
        chores.CountDownCook(1);
        check(chores.CookDue == FarmChores.CookMinSeconds - 1, "The wait runs down");
        chores.CountDownCook(100000);
        check(chores.CookDue == 0, "...and stops at zero");
        var random = new Random(9);
        for (int i = 0; i < 40; i++)
        {
            double wait = FarmChores.NextCook(random);
            if (wait < 600 || wait > 900) { check(false, $"A cooking wait out of range ({wait:F0})"); break; }
        }
        chores.BeginCook(FarmChoreKind.Pour, FarmDish.CarrotStew, 100, byUser: true);
        check(chores.Active && chores.Cooking && chores.ByUser && chores.Dish == FarmDish.CarrotStew && chores.Tile == -1, "Off to the hearth");
        check(!chores.Step(FarmChores.WalkLimitSeconds + 1) && !chores.WalkedTooLong, "The hearth may be further than a tile");
        chores.Step(FarmChores.CookWalkLimitSeconds);
        check(chores.WalkedTooLong, "...but a walk still has a limit");
        chores.Arrive();
        check(!chores.Step(FarmChores.PourHit - 0.1) && chores.Step(0.2) && !chores.Step(0.1), "It pours once");
        chores.Step(FarmChores.PourSeconds);
        check(chores.Finished, "Pouring ends");
        chores.StartStirring();
        check(chores.Kind == FarmChoreKind.Stir && chores.Phase == FarmChorePhase.Work && chores.Time == 0 && chores.ByUser && !chores.Finished, "...and stirring begins, still asked for by the user");
        chores.Step(FarmChores.StirSeconds);
        check(chores.Finished, "Stirring ends");
        chores.Stop();
        check(!chores.Cooking && !chores.ByUser, "The trip is over");
        chores.BeginCook(FarmChoreKind.Stir, FarmDish.RadishSoup, 0, byUser: true);
        chores.Begin(FarmChoreKind.Water, 1, 0);
        check(!chores.ByUser, "A later trip is not the user's");
    }

    // The cat's working rhythm (docs/FARM-GAME.md): rests, the long rest, sowing again.
    private static void Rhythm(Action<bool, string> check, DateTime start)
    {
        var random = new Random(9);
        var chores = new FarmChores();
        check(chores.VisitDue == FarmChores.FirstVisitSeconds, "The first look for work comes soon");
        bool shortRests = true, longRests = true;
        for (int round = 0; round < 30; round++)
        {
            for (int trip = 1; trip < FarmChores.TripsBeforeLongRest; trip++)
            {
                chores.ScheduleAfterLook(true, random);
                shortRests &= chores.VisitDue >= 15 && chores.VisitDue <= 40;
            }
            chores.ScheduleAfterLook(true, random);
            longRests &= chores.VisitDue >= 120 && chores.VisitDue <= 180;
        }
        check(shortRests, "Between trips the cat rests 15 to 40 seconds");
        check(longRests, "After six trips in a row it rests 2 to 3 minutes");
        chores = new FarmChores();
        for (int trip = 1; trip < FarmChores.TripsBeforeLongRest; trip++) chores.ScheduleAfterLook(true, random);
        chores.ScheduleAfterLook(false, random);
        check(chores.VisitDue == FarmChores.RetrySeconds, "With nothing to do it looks again in 45 seconds");
        chores.ScheduleAfterLook(true, random);
        check(chores.VisitDue <= 40, "A pause in the work starts the six trips over");

        check(new PetPreferences().FarmResow, "Sowing again is on by default");
        double FromFirst(int i) => i;
        var farm = new PetFarm { Coins = 100 };
        farm.Tiles[0] = new FarmTile { Stage = FarmStage.Ripe, Crop = FarmCrop.Carrot };
        check(FarmChores.Pick(farm, FromFirst) == (FarmChoreKind.Harvest, 0) && farm.Harvest(0) == FarmCrop.Carrot && farm.Tiles[0].LastCrop == FarmCrop.Carrot, "A tile remembers what it grew");
        check(FarmChores.Pick(farm, FromFirst, resow: false) == null, "With sowing again off the cat leaves it");
        check(FarmChores.Pick(farm, FromFirst) == (FarmChoreKind.Till, 0), "On, the cat tills it");
        check(farm.ResowCrop(1) == null && FarmChores.Pick(farm, i => 9 - i) == (FarmChoreKind.Till, 0), "A tile that never grew anything is the user's");
        check(farm.Till(0, keepLastCrop: true) && FarmChores.Pick(farm, FromFirst) == (FarmChoreKind.Sow, 0), "Then sows it");
        int coins = farm.Coins;
        check(farm.Plant(0, farm.ResowCrop(0)!.Value) && farm.Tiles[0].Crop == FarmCrop.Carrot && farm.Coins == coins - 4, "The same crop, paying for the seed");
        check(FarmChores.Pick(farm, FromFirst) == (FarmChoreKind.Water, 0), "And then it needs water");

        farm = new PetFarm();
        farm.Tiles[0] = new FarmTile { LastCrop = FarmCrop.Radish };
        check(farm.Till(0) && farm.Tiles[0].LastCrop == null && FarmChores.Pick(farm, FromFirst) == null, "Tilled by hand, the tile is the user's");

        // Order: ripe, thirsty, then tilling, then sowing.
        farm = new PetFarm { Coins = 100, OpenTiles = 6 };
        farm.Tiles[0] = new FarmTile { Stage = FarmStage.Tilled, LastCrop = FarmCrop.Radish };
        farm.Tiles[1] = new FarmTile { LastCrop = FarmCrop.Radish };
        check(FarmChores.Pick(farm, FromFirst) == (FarmChoreKind.Till, 1), "Tilling comes before sowing");
        farm.Tiles[2] = new FarmTile { Stage = FarmStage.Seed, Crop = FarmCrop.Radish };
        check(FarmChores.Pick(farm, FromFirst) == (FarmChoreKind.Water, 2), "Watering comes before tilling");

        farm = new PetFarm { Coins = 3 };
        farm.Tiles[0] = new FarmTile { LastCrop = FarmCrop.Carrot };
        check(farm.ResowCrop(0) == null && FarmChores.Pick(farm, FromFirst) == null, "Too poor for the seed, the cat leaves the tile");
        farm.Coins = 100; farm.Tiles[0].LastCrop = FarmCrop.Pumpkin;
        check(farm.ResowCrop(0) == null, "A crop locked by level is not sown");
        farm.Tiles[0].LastCrop = FarmCrop.Radish; farm.Coins = 0;
        check(farm.ResowCrop(0) == FarmCrop.Radish, "A free seed is always there");

        var work = new FarmChores();
        work.Begin(FarmChoreKind.Till, 1, 0);
        work.Arrive();
        check(!work.Step(FarmChores.TillHit - 0.1) && work.Step(0.2) && !work.Finished, "Tilling lands once");
        work.Step(FarmChores.TillSeconds);
        check(work.Finished, "...and ends");
        work.Begin(FarmChoreKind.Sow, 1, 0);
        work.Arrive();
        check(!work.Step(FarmChores.SowHit - 0.1) && work.Step(0.2), "Sowing lands once");
        work.Step(FarmChores.SowSeconds);
        check(work.Finished, "...and ends");

        string folder = Path.Combine(Path.GetTempPath(), "pet-farm-rhythm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "farm.json");
            var saved = new PetFarm();
            saved.Tiles[1].LastCrop = FarmCrop.Tomato;
            saved.Save(path);
            check(PetFarm.Load(path).Tiles[1].LastCrop == FarmCrop.Tomato, "What a tile grew is saved");
        }
        finally { Directory.Delete(folder, true); }
    }

    // The traveling stall (docs/FARM-GAME.md, milestone 4).
    private static void Stall(Action<bool, string> check)
    {
        check(new PetFarm().OfferSlots == 1 && new PetFarm { Xp = 69 }.OfferSlots == 1 && new PetFarm { Xp = 70 }.OfferSlots == 2 && new PetFarm { Xp = 279 }.OfferSlots == 2 && new PetFarm { Xp = 280 }.OfferSlots == 3, "One offer at first, a second at level 3, a third at level 5");

        var random = new Random(11);
        bool sizes = true, known = true, kinds = true, capped = true, noGold = true, sawFish = false;
        foreach (int xp in new[] { 0, 25, 70, 150, 280, 5000 })
            for (int i = 0; i < 300; i++)
            {
                var farm = new PetFarm { Xp = xp };
                var offers = farm.NewOffers(random);
                sizes &= offers.Count == farm.OfferSlots;
                foreach (var offer in offers)
                {
                    int total = offer.Items.Values.Sum();
                    sizes &= total >= 2 && total <= Math.Min(6, farm.Level + 2);
                    known &= offer.Items.Keys.All(good => Enum.TryParse<FarmCrop>(good, out var crop) ? farm.Unlocked(crop) : Enum.TryParse<FarmFish>(good, out _));
                    kinds &= offer.Items.Count is 1 or 2;
                    capped &= offer.Items.GetValueOrDefault("Pumpkin") <= 2 && offer.Items.GetValueOrDefault("Tomato") <= 3 && offer.Items.GetValueOrDefault("Carp") <= 3;
                    noGold &= !offer.Items.ContainsKey("Goldfish");
                    sawFish |= offer.Items.ContainsKey("Minnow") || offer.Items.ContainsKey("Carp");
                }
            }
        check(sizes, "An offer asks for 2 to 6 goods, fewer at the start");
        check(known, "An offer only asks for what the farm can produce");
        check(kinds, "An offer is one good or two");
        check(capped, "Slow goods are asked for in small numbers");
        check(noGold, "The golden fish is never asked for");
        check(sawFish, "Fish are asked for too");

        var seller = new PetFarm { Coins = 0 };
        var both = new FarmOffer { Items = new() { ["Radish"] = 3, ["Carrot"] = 1 } };
        check(PetFarm.OfferCoins(both) == 33 && PetFarm.OfferXp(both) == 11, "3 radishes and a carrot pay 33 coins and 11 XP");
        seller.Stock["Radish"] = 3;
        check(!seller.CanDeliver(both) && seller.Deliver(both) == 0 && seller.StockOf(FarmCrop.Radish) == 3 && seller.Coins == 0, "Short of one good nothing is taken");
        seller.Stock["Carrot"] = 2; seller.Stock["Radish"] = 4;
        check(seller.CanDeliver(both) && seller.Deliver(both) == 33 && seller.Coins == 33 && seller.Xp == 11, "Delivering pays coins and XP");
        check(seller.StockOf(FarmCrop.Radish) == 1 && seller.StockOf(FarmCrop.Carrot) == 1 && both.Done, "...and takes exactly what it asked for");
        check(!seller.CanDeliver(both) && seller.Deliver(both) == 0, "An offer is delivered once");

        var fishy = new FarmOffer { Items = new() { ["Minnow"] = 2, ["Carp"] = 1 } };
        check(PetFarm.OfferCoins(fishy) == 38 && PetFarm.OfferXp(fishy) == 14, "Fish are paid like crops, at 1.5 times");
        var angler = new PetFarm { Coins = 0 };
        angler.Fish["Minnow"] = 3; angler.Fish["Carp"] = 1;
        check(angler.GoodsOf("Minnow") == 3 && angler.Deliver(fishy) == 38 && angler.FishOf(FarmFish.Minnow) == 1 && angler.FishOf(FarmFish.Carp) == 0, "Fish leave the store when delivered");

        var level = new PetFarm { Xp = 69 };
        level.Stock["Radish"] = 2;
        int coins = level.Coins;
        var levelling = new FarmOffer { Items = new() { ["Radish"] = 2 } };
        check(level.Deliver(levelling) == 9 && level.Xp == 73 && level.Level == 3 && level.Coins == coins + 9 + 60 && level.TakeLevelsGained() == 1, "An offer can reach a new level, which pays as well");

        string folder = Path.Combine(Path.GetTempPath(), "pet-farm-stall-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "farm.json");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(path, "{\"Version\":3,\"Coins\":7,\"Orders\":[{\"Items\":{\"Radish\":2}}]}");
            var old = PetFarm.Load(path);
            check(old.Coins == 7 && old.Save(path) && !File.ReadAllText(path).Contains("Orders") && PetFarm.Load(path).Version == 3, "Old orders are dropped, the rest of the file stays");
        }
        finally { Directory.Delete(folder, true); }

        var stall = new FarmStall();
        check(stall.Phase == FarmStallPhase.Away && !stall.Here && stall.Due == FarmStall.FirstSeconds && stall.Amount == 0, "It is away at first");
        check(!stall.CountDown(FarmStall.FirstSeconds - 1) && stall.CountDown(1.1), "The first visit is due after ten minutes");
        for (int i = 0; i < 40; i++)
        {
            double wait = FarmStall.NextDelay(random);
            if (wait < 1200 || wait > 2400) { check(false, $"A wait out of range ({wait:F0})"); break; }
        }
        stall.Arrive(new() { both });
        check(stall.Here && stall.Rolling && stall.Offers.Count == 1 && stall.Left == FarmStall.StaySeconds, "It comes with its offers");
        check(!stall.CountDown(100), "The wait stands still during a visit");
        stall.Step(FarmStall.RollSeconds / 2);
        check(stall.Amount > 0 && stall.Amount < 1, "It rolls in");
        stall.Step(FarmStall.RollSeconds);
        check(stall.Phase == FarmStallPhase.Open && stall.Amount == 1 && !stall.Rolling, "It stands");
        stall.Step(FarmStall.StaySeconds - 1);
        check(stall.Here && Math.Abs(stall.Left - 1) < 1e-6, "It stays five minutes");
        check(!stall.Step(1.1) && stall.Phase == FarmStallPhase.Leaving && !stall.Here, "Then it leaves");
        check(!stall.Step(FarmStall.RollSeconds / 2) && stall.Amount > 0, "...rolling away");
        check(stall.Step(FarmStall.RollSeconds) && stall.Phase == FarmStallPhase.Away && stall.Amount == 0, "...and is gone");
        check(stall.Offers.Count == 1, "Its offers stay for a panel still open");
        stall.Arrive(new());
        stall.Reset();
        check(stall.Phase == FarmStallPhase.Away, "A reset sends it away");
    }

    // XP, levels and what they open (docs/FARM-GAME.md, milestone B).
    private static void Levels(Action<bool, string> check)
    {
        var farm = new PetFarm();
        check(farm.Level == 1 && farm.Xp == 0 && farm.LevelProgress == 0 && farm.NextLevelXp == 25, "A new field is level 1");
        var reach = new (int Xp, int Level)[] { (24, 1), (25, 2), (69, 2), (70, 3), (150, 4), (280, 5), (450, 6), (700, 7), (1000, 8), (1400, 9), (1999, 9), (2000, 10), (9000, 10) };
        foreach (var (xp, level) in reach) { farm.Xp = xp; check(farm.Level == level, $"{xp} XP is level {level}"); }
        check(farm.LevelProgress == 1 && farm.NextLevelXp == 2000, "The top level is full");

        farm = new PetFarm();
        farm.Till(0); farm.Till(1); farm.Coins = 500;
        check(farm.Unlocked(FarmCrop.Radish) && farm.Unlocked(FarmCrop.Carrot) && !farm.Unlocked(FarmCrop.Tomato) && !farm.Unlocked(FarmCrop.Pumpkin), "Level 1 grows radish and carrot");
        check(!farm.Plant(0, FarmCrop.Tomato) && !farm.Plant(0, FarmCrop.Pumpkin) && farm.Coins == 500 && farm.CanSow(FarmCrop.Tomato) == 0, "A locked crop cannot be sown, however rich");
        farm.Xp = 25;
        check(farm.Plant(0, FarmCrop.Tomato) && !farm.Unlocked(FarmCrop.Pumpkin), "Level 2 opens the tomato, not yet the pumpkin");
        farm.Xp = 150;
        check(farm.Plant(1, FarmCrop.Pumpkin), "Level 4 opens the pumpkin");

        foreach (var (dish, level) in new[] { (FarmDish.RadishSoup, 1), (FarmDish.CarrotStew, 2), (FarmDish.StuffedTomato, 3), (FarmDish.PumpkinPie, 5) })
        {
            var cook = new PetFarm();
            foreach (var crop in Enum.GetValues<FarmCrop>()) cook.Stock[crop.ToString()] = 5;
            cook.Xp = level == 1 ? 0 : new[] { 25, 70, 150, 280 }[level - 2] - 1;
            check(level == 1 || (!cook.CanCook(dish) && !cook.Cook(dish) && cook.StockOf(FarmCrop.Radish) == 5), $"{dish} is closed below level {level}");
            cook.Xp++;
            check(cook.Cook(dish), $"{dish} opens at level {level}");
        }

        // A tile that grows on: a locked crop already in the ground still comes in.
        farm = new PetFarm();
        farm.Tiles[0] = new FarmTile { Stage = FarmStage.Ripe, Crop = FarmCrop.Pumpkin };
        int coins = farm.Coins;
        check(farm.Harvest(0) == FarmCrop.Pumpkin && farm.Xp == 30, "A locked crop already in the ground still comes in, with its XP");
        check(farm.Level == 2 && farm.Coins == coins + 40 && farm.TakeLevelsGained() == 1 && farm.TakeLevelsGained() == 0, "Level 2 pays 40 coins, and is told once");

        farm = new PetFarm();
        farm.Xp = 24;
        foreach (int tile in new[] { 0, 1 }) farm.Tiles[tile] = new FarmTile { Stage = FarmStage.Ripe, Crop = FarmCrop.Pumpkin };
        coins = farm.Coins;
        farm.Harvest(0); farm.Harvest(1);
        check(farm.Xp == 84 && farm.Level == 3 && farm.Coins == coins + 40 + 60 && farm.TakeLevelsGained() == 2, "Two levels pay both rewards");
        farm = new PetFarm { Xp = 69 };
        farm.Tiles[0] = new FarmTile { Stage = FarmStage.Ripe, Crop = FarmCrop.Radish };
        coins = farm.Coins;
        farm.Harvest(0);
        check(farm.Level == 3 && farm.Coins == coins + 60, "One XP over is enough");
        farm = new PetFarm { Xp = PetFarm.MaxXp, Coins = PetFarm.MaxCoins };
        farm.Tiles[0] = new FarmTile { Stage = FarmStage.Ripe, Crop = FarmCrop.Radish };
        farm.Harvest(0);
        check(farm.Xp == PetFarm.MaxXp && farm.Coins == PetFarm.MaxCoins, "XP and coins stop at their most");

        string folder = Path.Combine(Path.GetTempPath(), "pet-farm-levels-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "farm.json");
            farm = new PetFarm { Xp = 99 };
            farm.Save(path);
            var back = PetFarm.Load(path);
            check(back.Xp == 99 && back.Version == 3 && back.Level == 3, "XP is saved");
            check(!File.ReadAllText(path).Contains("\"Level\"") && !File.ReadAllText(path).Contains("LevelProgress"), "What follows from the XP is not saved");
            File.WriteAllText(path, """{ "Version": 2, "Coins": 50, "Harvested": { "Carrot": 3, "Pumpkin": 1, "Gold": 9 } }""");
            var old = PetFarm.Load(path);
            check(old.Xp == 45 && old.Level == 2 && old.Coins == 50 && old.TakeLevelsGained() == 0, "A field from before the levels gets XP for what it has grown, without the rewards");
            File.WriteAllText(path, """{ "Version": 3, "Xp": 12, "Harvested": { "Pumpkin": 90 } }""");
            check(PetFarm.Load(path).Xp == 12, "A field that has levels keeps its XP");
            File.WriteAllText(path, """{ "Version": 3, "Xp": -5 }""");
            check(PetFarm.Load(path).Xp == 0, "Odd XP is pulled back");
        }
        finally { Directory.Delete(folder, true); }
    }

    // One step on every tile ready for it.
    private static void Bulk(Action<bool, string> check, DateTime start)
    {
        var farm = new PetFarm();
        check(farm.TilesAt(FarmStep.Till).SequenceEqual(new[] { 0, 1, 2 }), "Locked tiles are no work");
        foreach (int tile in new[] { 0, 1, 2 }) farm.Till(tile);
        farm.Coins = 9;
        check(farm.CanSow(FarmCrop.Radish) == 3 && farm.CanSow(FarmCrop.Carrot) == 2 && farm.CanSow(FarmCrop.Pumpkin) == 0, "The coins say how many tiles a seed can cover");
        check(farm.SowAll(FarmCrop.Pumpkin).Count == 0 && farm.Coins == 9, "Too dear, nothing is sown");
        check(farm.SowAll(FarmCrop.Carrot).SequenceEqual(new[] { 0, 1 }) && farm.Coins == 1, "Sowing stops from the left when the coins run out");
        check(farm.Next(2) == FarmStep.Plant && farm.TilesAt(FarmStep.Water).Count == 2, "The tile left over still waits for a seed");
        check(farm.SowAll(FarmCrop.Radish).SequenceEqual(new[] { 2 }) && farm.Coins == 1, "A free seed always covers what is left");
        check(farm.SowAll(FarmCrop.Radish).Count == 0, "Nothing is left to sow");
    }

    // Cooking from the store, rain that waters, sun that hurries (1.5.1 M6).
    private static void Links(Action<bool, string> check, DateTime start)
    {
        var farm = new PetFarm();
        check(Enum.GetValues<FarmDish>().Where(d => PetFarm.FishIn(d) == null).All(d => PetFarm.Recipe(d).Sum(p => p.Count) == 2), "Every dish without fish takes two things from the store");
        check(!farm.Cook(FarmDish.RadishSoup) && farm.Stock.Count == 0, "An empty store cooks nothing");
        farm.Stock["Radish"] = 1;
        check(!farm.CanCook(FarmDish.RadishSoup) && !farm.Cook(FarmDish.RadishSoup) && farm.StockOf(FarmCrop.Radish) == 1, "One radish is not enough, and is kept");
        farm.Xp = 70;
        farm.Stock["Tomato"] = 1; farm.Stock["Radish"] = 3;
        check(farm.Cook(FarmDish.StuffedTomato) && farm.StockOf(FarmCrop.Tomato) == 0 && farm.StockOf(FarmCrop.Radish) == 2, "A dish uses up each ingredient");
        farm.Coins = 7; farm.Harvested["Radish"] = 4;
        check(farm.Cook(FarmDish.RadishSoup) && farm.Stock.Count == 0 && farm.Coins == 7 && farm.HarvestedCount(FarmCrop.Radish) == 4, "Cooking costs no coins and does not lower the harvest count");
        check(!farm.Cook((FarmDish)99), "A dish that does not exist is refused");

        farm = new PetFarm();
        farm.Till(0); farm.Plant(0, FarmCrop.Radish);
        farm.Till(1); farm.Plant(1, FarmCrop.Radish); farm.Water(1, start);
        farm.Till(2);
        var before = farm.Tiles[1].GrowsUntil;
        check(farm.RainWater(start).SequenceEqual(new[] { 0 }) && farm.Next(0) == FarmStep.Wait, "Rain waters what waits for water, and only that");
        check(farm.Tiles[1].GrowsUntil == before && farm.Tiles[2].Stage == FarmStage.Tilled, "A growing crop and bare soil are left as they are");
        check(farm.RainWater(start).Count == 0, "Rain has nothing more to do");

        double stage = PetFarm.StageSeconds(FarmCrop.Radish);
        check(farm.Sunshine(40), "Sun reaches what grows");
        check(Math.Abs(farm.Remaining(0, start).TotalSeconds - (stage - 10)) < 0.01, "A second of sun counts a quarter more");
        check(farm.Tiles[2].GrowsUntil == null, "Sun does not start anything");
        check(!new PetFarm().Sunshine(40), "With nothing growing the sun changes nothing");
        // Under sun all the way, each second of the clock counts 1.25: a fifth off the whole stage.
        farm = new PetFarm();
        farm.Till(0); farm.Plant(0, FarmCrop.Radish); farm.Water(0, start);
        farm.Sunshine(stage * 0.8);
        check(farm.Advance(start.AddSeconds(stage * 0.8 + 0.01)).Count == 1 && farm.Tiles[0].Stage == FarmStage.Sprout, "In full sun a stage takes four fifths of its time");

        var random = new Random(4);
        var weather = new PetWeather();
        check(!weather.Shining && !weather.Raining, "Hidden weather reaches nothing");
        weather.Step(PetWeather.FadeSeconds, true, random);
        check(weather.Shining && !weather.Raining, "A clear sky on show shines");
        weather.HideNow();
        weather.SetKind(WeatherKind.Rain);
        weather.Step(PetWeather.FadeSeconds, true, random);
        check(weather.Raining && !weather.Shining, "Rain on show rains");
        weather.HideNow();
        weather.Step(0.01, false, random);
        check(!weather.Raining, "Rain that is not on show waters nothing");
        foreach (var other in new[] { WeatherKind.Snow, WeatherKind.Sunset, WeatherKind.Pollen, WeatherKind.Night })
        {
            weather.HideNow();
            weather.SetKind(other);
            weather.Step(PetWeather.FadeSeconds, true, random);
            check(!weather.Raining && !weather.Shining, $"{other} does nothing for the field");
        }

        var pet = new PetModel();
        pet.Emotions.Cheer(6);
        pet.Emotions.Step(pet, 5, -200, 50);
        check(pet.Emotions.Target == PetEmotion.Happy, "A dish can keep the cat happy for longer");
    }

    // The store, farm coins, seeds, tiles and the scarecrow (1.5.1 M5).
    private static void Economy(Action<bool, string> check, DateTime start)
    {
        var farm = new PetFarm();
        check(farm.Coins == 20 && farm.OpenTiles == 3 && !farm.Scarecrow && farm.StockValue == 0, "A new field: 20 coins, three open tiles, no scarecrow");
        check(new[] { 0, 4, 10, 25 }.SequenceEqual(Enum.GetValues<FarmCrop>().Select(PetFarm.SeedPrice)), "Seeds cost 0, 4, 10 and 25");
        check(new[] { 3, 13, 30, 85 }.SequenceEqual(Enum.GetValues<FarmCrop>().Select(PetFarm.SellPrice)), "Crops sell for 3, 13, 30 and 85");
        check(Enum.GetValues<FarmCrop>().All(c => PetFarm.SellPrice(c) > PetFarm.SeedPrice(c)), "Every crop makes a profit");

        check(farm.Next(2) == FarmStep.Till && farm.Next(3) == FarmStep.Locked, "Locked tiles begin at the fourth");
        check(!farm.Till(3) && farm.Tiles[3].Stage == FarmStage.Wild, "A locked tile cannot be tilled");

        farm.Coins = 0;
        farm.Till(0); farm.Till(1);
        check(!farm.Plant(0, FarmCrop.Carrot) && farm.Tiles[0].Stage == FarmStage.Tilled && farm.Coins == 0, "Without coins a carrot cannot be sown");
        check(farm.Plant(1, FarmCrop.Radish) && farm.Coins == 0, "A radish is free, so nobody is ever stuck");
        farm.Coins = 5;
        check(farm.Plant(0, FarmCrop.Carrot) && farm.Coins == 1, "Sowing pays for the seed");

        foreach (int tile in new[] { 0, 1 }) { farm.Tiles[tile].Stage = FarmStage.Ripe; farm.Harvest(tile); }
        check(farm.StockOf(FarmCrop.Carrot) == 1 && farm.StockOf(FarmCrop.Radish) == 1 && farm.StockValue == 16, "The harvest goes to the store");
        check(farm.Sell(FarmCrop.Carrot) == 13 && farm.Coins == 14 && farm.StockOf(FarmCrop.Carrot) == 0, "Selling one pays its price");
        check(farm.Sell(FarmCrop.Carrot) == 0 && farm.Coins == 14, "There is nothing left to sell");
        check(farm.HarvestedCount(FarmCrop.Carrot) == 1, "Selling leaves the count of harvests alone");
        check(farm.SellAll() == 3 && farm.Coins == 17 && farm.Stock.Count == 0 && farm.SellAll() == 0, "Selling everything empties the store");

        farm.Coins = 39;
        check(!farm.OpenNextTile() && farm.OpenTiles == 3 && farm.Coins == 39, "A tile needs its price");
        farm.Coins = 1000;
        check(farm.OpenNextTile() && farm.OpenTiles == 4 && farm.Coins == 960, "The fourth tile costs 40");
        check(farm.Next(3) == FarmStep.Till && farm.Till(3), "An opened tile can be worked");
        check(farm.OpenNextTile() && farm.Coins == 840 && farm.OpenNextTile() && farm.Coins == 540, "The next two cost 120 and 300");
        check(farm.NextLockedTile == -1 && !farm.OpenNextTile() && farm.Coins == 540, "Nothing more to open");

        farm.Coins = 149;
        check(!farm.BuyScarecrow() && !farm.Scarecrow, "The scarecrow needs its price");
        farm.Coins = 400;
        check(farm.BuyScarecrow() && farm.Scarecrow && farm.Coins == 250 && !farm.BuyScarecrow() && farm.Coins == 250, "It is bought once, for 150");
        var random = new Random(9);
        bool rare = Enumerable.Range(0, 40).All(_ => FarmChores.NextBird(random, true) is >= 720 and <= 1440);
        check(rare, "With a scarecrow birds come every 12 to 24 minutes");

        farm.Coins = PetFarm.MaxCoins; farm.Stock["Pumpkin"] = 2;
        farm.Sell(FarmCrop.Pumpkin);
        check(farm.Coins == PetFarm.MaxCoins, "Coins stop at the most the panel can show");

        string folder = Path.Combine(Path.GetTempPath(), "pet-farm-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "farm.json");
        Directory.CreateDirectory(folder);
        try
        {
            var saved = new PetFarm { Coins = 77, OpenTiles = 5, Scarecrow = true };
            saved.Stock["Tomato"] = 4;
            saved.Save(path);
            var back = PetFarm.Load(path);
            check(back.Coins == 77 && back.OpenTiles == 5 && back.Scarecrow && back.StockOf(FarmCrop.Tomato) == 4 && back.Version == PetFarm.CurrentVersion, "The economy is saved");

            File.WriteAllText(path, """
                { "Version": 1, "Harvested": { "Carrot": 3, "Radish": 2 },
                  "Tiles": [ { }, { "Stage": "Tilled" }, { }, { }, { "Stage": "Seed", "Crop": "Radish" }, { } ] }
                """);
            var old = PetFarm.Load(path);
            check(old.Coins == 20 && old.StockOf(FarmCrop.Carrot) == 3 && old.StockOf(FarmCrop.Radish) == 2 && old.HarvestedCount(FarmCrop.Carrot) == 3, "A field from before the store gets coins and its harvests in store");
            check(old.OpenTiles == 5 && old.Next(4) == FarmStep.Water && old.Next(5) == FarmStep.Locked, "...and keeps every tile with something on it");

            File.WriteAllText(path, """{ "Version": 1, "Tiles": [ { }, { }, { }, { }, { }, { } ] }""");
            check(PetFarm.Load(path).OpenTiles == 3, "An old field of wild ground closes down to three");

            File.WriteAllText(path, """{ "Version": 2, "Coins": -9, "OpenTiles": 99, "Stock": { "Gold": 5, "Carrot": -1 } }""");
            var odd = PetFarm.Load(path);
            check(odd.Coins == 0 && odd.OpenTiles == 6 && odd.Stock.Count == 0, "Odd values are pulled back");

            File.WriteAllText(path, """{ "Version": 2, "Harvested": { "Carrot": 3 } }""");
            check(PetFarm.Load(path).StockOf(FarmCrop.Carrot) == 0, "Only a field from before the store is moved across");
        }
        finally { Directory.Delete(folder, true); }
    }

    // The cat's own trips (1.5.1 M4): which tile, in what order, and the bird's visit.
    private static void Chores(Action<bool, string> check, DateTime start)
    {
        check(new PetPreferences().FarmHelper, "The cat helps by default");
        foreach (var block in new[] { AutoBlock.Paused, AutoBlock.Held, AutoBlock.Unsettled, AutoBlock.Quiet, AutoBlock.Fullscreen, AutoBlock.Battle, AutoBlock.BattleScreens, AutoBlock.Feature, AutoBlock.Away })
            check(!PetAutonomy.MayAct(AutoKind.FarmChore, block), $"No trip with {block}");

        var farm = new PetFarm { OpenTiles = 6 };
        double FromFirst(int i) => i;
        check(FarmChores.Pick(farm, FromFirst) == null, "Wild ground gives the cat nothing to do");
        farm.Till(0);
        check(FarmChores.Pick(farm, FromFirst) == null, "It does not sow either");
        farm.Plant(0, FarmCrop.Radish); farm.Till(3); farm.Plant(3, FarmCrop.Radish);
        check(FarmChores.Pick(farm, FromFirst) == (FarmChoreKind.Water, 0), "It waters the nearest thirsty tile");
        check(FarmChores.Pick(farm, i => 9 - i) == (FarmChoreKind.Water, 3), "Nearest to where the cat is");
        farm.Water(0, start);
        check(FarmChores.Pick(farm, FromFirst) == (FarmChoreKind.Water, 3), "A tile already growing is left alone");
        farm.Tiles[5].Stage = FarmStage.Ripe; farm.Tiles[5].Crop = FarmCrop.Carrot;
        check(FarmChores.Pick(farm, FromFirst) == (FarmChoreKind.Harvest, 5), "A ripe tile comes first, even a far one");

        var random = new Random(4);
        for (int i = 0; i < 40; i++)
        {
            double bird = FarmChores.NextBird(random);
            if (bird < 360 || bird > 720) { check(false, $"A wait out of range ({bird:F0})"); break; }
        }
        check(FarmChores.BirdTile(new PetFarm(), random) == -1, "No bird on an empty field");
        check(Enumerable.Range(0, 30).All(_ => FarmChores.BirdTile(farm, random) is 0 or 3 or 5), "A bird only comes to a tile with a crop");

        var chores = new FarmChores();
        check(!chores.Active && !chores.CountDownVisit(1) && !chores.CountDownBird(1), "Nothing is due at first");
        chores.ScheduleVisit(1);
        check(chores.CountDownVisit(0.5) == false && chores.CountDownVisit(0.6), "The visit comes when its wait is over");
        chores.Begin(FarmChoreKind.Water, 2, 50);
        check(chores.Active && chores.Phase == FarmChorePhase.Walk && chores.Tile == 2 && chores.Spot == 50, "The cat sets off");
        check(!chores.Step(FarmChores.WalkLimitSeconds - 1) && !chores.WalkedTooLong, "Walking does no work");
        check(!chores.Step(2) && chores.WalkedTooLong, "A walk has a limit");
        chores.Arrive();
        check(chores.Phase == FarmChorePhase.Work && !chores.Step(FarmChores.WaterHit - 0.1), "It works a moment first");
        check(chores.Step(0.2) && !chores.Step(0.1) && !chores.Finished, "The water lands once");
        chores.Step(FarmChores.WaterSeconds);
        check(chores.Finished, "...and the work ends");
        chores.Stop();
        check(!chores.Active && chores.Tile == -1, "The trip is over");
        chores.Begin(FarmChoreKind.Bird, 1, 0);
        check(chores.Phase == FarmChorePhase.Notice && !chores.Step(FarmChores.NoticeSeconds + 0.01) && chores.Phase == FarmChorePhase.Walk, "For a bird the cat looks up first");

        var bird2 = new FarmBird();
        check(!bird2.Present && bird2.Air == 0, "No bird at first");
        bird2.Land(2, -1);
        check(bird2.Present && !bird2.Perched && bird2.Air == 1 && bird2.Side == -1, "It comes from the air");
        bird2.Step(FarmBird.LandSeconds + 0.01);
        check(bird2.Perched && bird2.Air == 0, "It lands");
        bird2.Step(FarmBird.StaySeconds - 1);
        check(bird2.Perched, "It stays a while");
        bird2.Step(1.1);
        check(bird2.Phase == FarmBirdPhase.Leaving, "It leaves by itself");
        bird2.Step(FarmBird.LeaveSeconds / 2);
        check(bird2.Air > 0 && bird2.Air < 1, "It flies off");
        bird2.Step(FarmBird.LeaveSeconds);
        check(!bird2.Present && bird2.Tile == -1, "It is gone");
        bird2.Land(1, 1);
        bird2.Scare();
        check(bird2.Phase == FarmBirdPhase.Leaving, "A pounce sends it away at once");
    }

    private static void Steps(Action<bool, string> check, DateTime start)
    {
        var farm = new PetFarm { OpenTiles = 6 };
        check(farm.Tiles.Count == 6 && farm.Tiles.All(t => t.Stage == FarmStage.Wild), "A new field has six wild tiles");
        check(new[] { 5, 15, 40, 120 }.SequenceEqual(Enum.GetValues<FarmCrop>().Select(c => (int)(PetFarm.GrowSeconds(c) / 60))), "Four crops of 5, 15, 40 and 120 minutes");

        check(farm.Next(0) == FarmStep.Till, "Wild ground is tilled first");
        check(!farm.Plant(0, FarmCrop.Carrot) && !farm.Water(0, start) && farm.Harvest(0) == null, "Nothing else can be done to wild ground");
        check(farm.Till(0) && farm.Next(0) == FarmStep.Plant && !farm.Till(0), "Tilled ground waits for a seed");
        check(!farm.Water(0, start), "Bare ground cannot be watered");
        check(farm.Plant(0, FarmCrop.Carrot) && farm.Next(0) == FarmStep.Water && farm.Tiles[0].Crop == FarmCrop.Carrot, "A seed waits for water");
        check(farm.Tiles[1].Stage == FarmStage.Wild, "The other tiles are left alone");
        check(!farm.Plant(-1, FarmCrop.Carrot) && !farm.Till(6) && farm.Harvest(9) == null, "Tiles outside the field are refused");

        var now = start;
        foreach (var stage in new[] { FarmStage.Sprout, FarmStage.Young, FarmStage.Ripe })
        {
            check(farm.Water(0, now) && farm.Next(0) == FarmStep.Wait && !farm.Water(0, now), $"Watered once, it grows towards {stage}");
            now = now.AddSeconds(PetFarm.StageSeconds(FarmCrop.Carrot));
            check(farm.Advance(now).SequenceEqual(new[] { 0 }) && farm.Tiles[0].Stage == stage, $"...and reaches {stage}");
        }
        check(now == start.AddMinutes(15), "Watered on time, a carrot takes its 15 minutes");
        check(farm.Next(0) == FarmStep.Harvest && farm.Progress(0, now) == 1, "Ripe, it waits for the harvest");
        check(farm.Harvest(0) == FarmCrop.Carrot && farm.HarvestedCount(FarmCrop.Carrot) == 1, "The harvest is counted");
        check(farm.Tiles[0].Stage == FarmStage.Wild && farm.Tiles[0].Crop == null && farm.Harvest(0) == null, "The tile is wild again and gives nothing twice");
    }

    private static void Clock(Action<bool, string> check, DateTime start)
    {
        var farm = new PetFarm();
        farm.Till(0); farm.Plant(0, FarmCrop.Radish);
        check(farm.Advance(start.AddDays(30)).Count == 0 && farm.Tiles[0].Stage == FarmStage.Seed, "Without water a seed waits for a month, not wilting");

        double stage = PetFarm.StageSeconds(FarmCrop.Radish);
        farm.Water(0, start);
        check(Math.Abs(farm.Progress(0, start.AddSeconds(stage / 2)) - 1.0 / 6) < 1e-9, "Half of the first stage is a sixth of the way");
        check(Math.Abs(farm.Remaining(0, start.AddSeconds(30)).TotalSeconds - (stage - 30)) < 1e-6, "The time left counts down");
        check(farm.Advance(start.AddSeconds(stage - 1)).Count == 0, "A second early it has not grown");

        farm.Advance(start.AddDays(10));
        check(farm.Tiles[0].Stage == FarmStage.Sprout && farm.Next(0) == FarmStep.Water, "Ten days away pass only the one stage that was watered");

        farm.Water(0, start);
        check(farm.Advance(start.AddDays(-5)).Count == 0, "A clock set backwards does not ripen anything");
        check(farm.Remaining(0, start.AddDays(-5)).TotalSeconds <= stage + 1e-6, "...and never makes the wait longer than one stage");

        var ripe = new PetFarm();
        ripe.Till(0); ripe.Plant(0, FarmCrop.Radish);
        for (int i = 0; i < 3; i++) { ripe.Water(0, start); ripe.Advance(start.AddSeconds(stage)); }
        check(ripe.Advance(start.AddYears(5)).Count == 0 && ripe.Tiles[0].Stage == FarmStage.Ripe, "Ripe crops wait for years");
    }

    private static void Saving(Action<bool, string> check, DateTime start)
    {
        string folder = Path.Combine(Path.GetTempPath(), "pet-farm-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "farm.json");
        try
        {
            check(PetFarm.Load(path).Tiles.Count == 6, "No file: a new field");

            var farm = new PetFarm { Monitor = @"\\.\DISPLAY2", FractionX = 0.4, Coins = 100, OpenTiles = 6 };
            farm.Xp = 150;
            farm.Till(1); farm.Plant(1, FarmCrop.Pumpkin); farm.Water(1, start);
            farm.Till(2);
            check(farm.Save(path), "The field is saved");
            var back = PetFarm.Load(path);
            check(back.Tiles[1].Stage == FarmStage.Seed && back.Tiles[1].Crop == FarmCrop.Pumpkin && back.Tiles[2].Stage == FarmStage.Tilled, "Its tiles come back");
            check(back.Tiles[1].GrowsUntil == start.AddSeconds(PetFarm.StageSeconds(FarmCrop.Pumpkin)) && back.Tiles[1].GrowsUntil!.Value.Kind == DateTimeKind.Utc, "The growing time comes back in UTC");
            check(back.Monitor == @"\\.\DISPLAY2" && back.FractionX == 0.4, "The plot's place comes back");
            check(File.ReadAllText(path).Contains("\"Seed\"") && File.ReadAllText(path).Contains("\"Pumpkin\""), "Stages and crops are written by name");

            farm.Till(3);
            farm.Save(path);
            File.WriteAllText(path, "{ not json");
            check(PetFarm.Load(path).Tiles[1].Crop == FarmCrop.Pumpkin, "A damaged file falls back to its backup");

            File.Delete(path + ".bak");
            File.WriteAllText(path, "{ not json");
            check(PetFarm.Load(path).Tiles.All(t => t.Stage == FarmStage.Wild), "With no backup either: a new field");

            File.WriteAllText(path, """
                { "Tiles": [ { "Stage": "Ripe" }, { "Stage": "Seed", "GrowsUntil": "2026-11-08T12:00:00Z" }, { "Stage": "Wild", "Crop": "Carrot", "GrowsUntil": "2026-11-08T12:00:00Z" } ],
                  "Harvested": { "Carrot": 3, "Gold": 99, "Tomato": -2 }, "FractionX": 7 }
                """);
            var odd = PetFarm.Load(path);
            check(odd.Tiles.Count == 6 && odd.Tiles[0].Stage == FarmStage.Wild && odd.Tiles[1].Stage == FarmStage.Wild, "A crop stage with no crop becomes wild ground");
            check(odd.Tiles[2].Crop == null && odd.Tiles[2].GrowsUntil == null, "Wild ground carries no crop or timer");
            check(odd.HarvestedCount(FarmCrop.Carrot) == 3 && odd.Harvested.Count == 1, "Unknown or negative harvest counts are dropped");
            check(odd.FractionX == 1, "A place off the screen is pulled back");

            File.WriteAllText(path, """{ "Version": 99, "FractionX": 0.2 }""");
            var newer = PetFarm.Load(path);
            check(newer.ReadOnly && !newer.Save(path) && File.ReadAllText(path).Contains("99"), "A file from a newer version is not overwritten");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private static void Lift(Action<bool, string> check)
    {
        var lift = new FarmLift();
        check(lift.Phase == FarmLiftPhase.Down && lift.Height == 0 && !lift.Busy, "The plot starts on the ground");
        lift.Drop();
        check(lift.Phase == FarmLiftPhase.Down, "What is on the ground cannot be dropped");

        lift.Begin();
        check(lift.Shed == 0, "Crumbs begin to come off");
        bool grounded = true, shook = false;
        for (int i = 0; i < (int)(FarmLift.GatherSeconds * 60) - 1; i++) { lift.Step(1.0 / 60); grounded &= lift.Height == 0; shook |= Math.Abs(lift.ShakeX) > 1; }
        check(grounded && shook, "It strains on the ground first, shaking");
        double last = 0, rocked = 0;
        bool noJump = true;
        for (double t = FarmLift.GatherSeconds - 2.0 / 60; t < FarmLift.RiseSeconds; t += 1.0 / 60) { lift.Step(1.0 / 60); noJump &= Math.Abs(lift.Height - last) < 5; rocked = Math.Max(rocked, Math.Abs(lift.Tilt)); last = lift.Height; }
        lift.Step(1.0 / 60);
        check(noJump && lift.Floating, "It is hauled up without a jump and floats");
        check(rocked > 0.5, $"...rocking on the way ({rocked:F1} degrees)");
        check(lift.ShakeX == 0 && lift.Tilt == 0, "In the air the shaking stops");

        double low = 999, high = 0;
        for (double t = 0; t < 4; t += 1.0 / 60) { lift.Step(1.0 / 60); low = Math.Min(low, lift.Height); high = Math.Max(high, lift.Height); }
        check(lift.Floating && Math.Abs(high - low - FarmLift.BobHeight * 2) < 0.2, $"It bobs gently in the air ({low:F1} to {high:F1})");

        lift.Drop();
        double from = lift.Height, previous = from, biggest = 0, lastStep = 0, frames = 0;
        bool landed = false;
        while (!landed && frames < 300)
        {
            landed = lift.Step(1.0 / 60);
            frames++;
            lastStep = previous - lift.Height;
            biggest = Math.Max(biggest, lastStep);
            previous = lift.Height;
        }
        check(landed && lift.Height == 0 && Math.Abs(frames / 60 - FarmLift.FallSeconds) < 0.05, $"It comes down in about {FarmLift.FallSeconds} s ({frames / 60:F2} s)");
        check(biggest > 3 && lastStep > biggest * 0.8, $"...faster and faster until it hits ({biggest:F2} DIPs a frame at most)");
        check(lift.Dust == 1 && lift.Busy && lift.Squash > 0.9, "It lands with a blow: dirt flies and the plot squashes");

        double bounced = 0, kicked = 0;
        for (int i = 0; i < (int)(FarmLift.BounceSeconds * 60); i++) { lift.Step(1.0 / 60); bounced = Math.Max(bounced, lift.Height); kicked = Math.Max(kicked, Math.Abs(lift.ShakeX)); }
        check(bounced > 2 && bounced < 7.01 && kicked > 1, $"...bounces a little and shakes ({bounced:F1} DIPs, {kicked:F1} sideways)");
        for (int i = 0; i < 40; i++) lift.Step(1.0 / 60);
        check(!lift.Busy && lift.Height == 0 && lift.ShakeX == 0 && lift.Squash == 0, "...and then all is still");

        lift.Begin(); lift.Step(0.1); lift.Drop();
        check(lift.Phase == FarmLiftPhase.Falling, "It can be put down while still rising");
        lift.Reset();
        check(lift.Phase == FarmLiftPhase.Down && lift.Height == 0 && !lift.Busy && lift.Shed < 0, "A reset puts it on the ground at once");

        lift.Begin();
        for (int i = 0; i < 100; i++) lift.Step(1.0 / 60);
        lift.Drop(0.4);
        double held = lift.Height;
        for (int i = 0; i < 12; i++) lift.Step(1.0 / 60);
        check(lift.FallAmount > 0.3 && lift.FallAmount < 0.9 && lift.Height == held, "Going back, it slides along in the air first");
        bool down = false;
        double seconds = 0.2;
        while (!down && seconds < 3) { down = lift.Step(1.0 / 60); seconds += 1.0 / 60; }
        check(down && Math.Abs(seconds - 0.4 - FarmLift.FallSeconds) < 0.05, $"...then falls ({seconds:F2} s in all)");
        check(lift.Busy && lift.Shed >= 0, "Crumbs still in the air finish falling after it lands");
        for (int i = 0; i < 180; i++) lift.Step(1.0 / 60);
        check(!lift.Busy, "...and stop");
    }
}
