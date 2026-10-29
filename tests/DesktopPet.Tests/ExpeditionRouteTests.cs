using DesktopPet;

// 1.5 M23 (docs/UPDATE-1.5.md X1, X3; docs/BATTLE-DATA.md → Tuyến và cửa hàng): the Expedition's second
// route (its zones, unlock, scene, own records), the cat shop (wares drawn once, coins, each ware once,
// never at step 1) and a run started under rules 6 going on to its end without shops. Simulated only.
internal static class ExpeditionRouteTests
{
    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var main = data.Expedition!;
        RouteChecks(check, data, main);
        ShopChecks(check, data, main);
        OlderRunChecks(check, data, main);
        DataChecks(check);
    }

    private static void RouteChecks(Action<bool, string> check, BattleSettings data, ExpeditionSpec main)
    {
        var dump = main.Route("dump_trail");
        check(main.All.Count == 2 && main.All[0] == main && dump != null && dump.UnlockAfter == "c2_zip_slime" && dump.Scene == ChapterSpec.Dump && main.Scene == null,
            "Built-in: two routes, the Slime Trail and the Dump Trail (opened by winning the Zip Slime, with chapter 2's backdrop)");
        check(dump!.Relics == main.Relics && dump.Events == main.Events && dump.Capsules == main.Capsules && dump.Store == main.Store && dump.RestHeal == main.RestHeal
              && dump.WinHeal == main.WinHeal && dump.MaxRelics == main.MaxRelics && dump.Keepsakes.Length == 0,
            "A route shares the Expedition's HP rules, events, relics, capsules and shop, and gives no keepsakes");
        check(dump.PathSteps == 6 && dump.StepCount == 7 && dump.Boss?.Id == "y_zip" && dump.Zones.Select(z => z.Id).SequenceEqual(new[] { "swamp", "fish_store", "folder_maze" })
              && dump.Zone(0)!.Id == "swamp" && dump.Zone(3)!.Id == "fish_store" && dump.Zone(5)!.Id == "folder_maze" && dump.Zone(6) == null,
            "The Dump Trail: 6 steps in three zones of two (swamp, fish store, folder maze), then the Zip Slime");
        check(dump.Fights.Concat(dump.Elites).Any(e => e.Basket != null) && dump.Fights.Concat(dump.Elites).Append(dump.Boss!).All(e => !e.Survival && e.MaxMalware == 4),
            "Its encounters are clear battles (the fish store's keep a basket) with up to 4 malware apps");

        var adventure = new AdventureProgress();
        adventure.Completed.Add("c1_slime_king");
        var x = new Expedition(data, new ExpeditionProgress(), "dump_trail");
        check(x.Spec == main.Route("dump_trail") && !x.IsUnlocked(adventure) && new Expedition(data, new ExpeditionProgress()).IsUnlocked(adventure),
            "Before the Zip Slime is won the Dump Trail is locked, the Slime Trail open");
        adventure.Completed.Add("c2_zip_slime");
        check(x.IsUnlocked(adventure) && new Expedition(data, new ExpeditionProgress(), "nowhere").Spec == main, "Winning it opens the Dump Trail; an unknown route is the first");

        // Every step draws its battles from its zone only, over many seeds.
        bool zoned = true, shopRules = true;
        int shops = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            var e = new Expedition(data, new ExpeditionProgress(), "dump_trail");
            var run = e.Start(BattleWeapon.Sword, BattleDifficulty.Normal, seed);
            check(run.Expedition == "dump_trail" || seed > 1, "A run started on a route is that route's");
            for (int step = 0; step < dump.PathSteps; step++)
            {
                run.Path.Clear(); run.Path.AddRange(Enumerable.Repeat(0, step));
                e.Roll();
                var zone = dump.Zone(step)!;
                for (int lane = 0; lane < run.Map[step].Length; lane++)
                {
                    string kind = run.Kinds![step][lane], id = run.Map[step][lane];
                    if (kind == ExpeditionSpec.Fight) zoned &= zone.Fights.Contains(id);
                    if (kind == ExpeditionSpec.Elite) zoned &= zone.Elites.Contains(id);
                }
                int stepShops = run.Kinds![step].Count(k => k == ExpeditionSpec.Shop);
                shops += stepShops;
                shopRules &= stepShops <= 1 && (step > 0 || stepShops == 0);
            }
        }
        check(zoned, "Each step's fights and elites come from its zone (60 seeds)");
        check(shopRules && shops > 0, $"Shops: never at step 1, one a step at most ({shops} drawn over 60 runs)");

        // Records are kept per route; reopening finds the run on its own route.
        var progress = new ExpeditionProgress();
        var onDump = new Expedition(data, progress, "dump_trail");
        onDump.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 3);
        var reopened = new Expedition(data, progress);
        check(reopened.Spec.Id == "dump_trail" && reopened.Mismatch(reopened.Run!) == null, "A saved run is read back on its own route, whatever route is asked for");
        reopened.Abandon();
        check(progress.Records.Keys.Single().StartsWith("dump_trail|r1|", StringComparison.Ordinal) && new Expedition(data, progress).Record(BattleWeapon.Sword, BattleDifficulty.Normal).Runs == 0
              && new Expedition(data, progress, "dump_trail").Record(BattleWeapon.Sword, BattleDifficulty.Normal).Runs == 1,
            "Each route keeps its own records");
        var lost = new ExpeditionProgress { Active = new ExpeditionRun { RunId = "gone", Expedition = "old_route", RulesVersion = 1 } };
        var gone = new Expedition(data, lost);
        check(gone.Spec == main && gone.Mismatch(lost.Active!) != null, "A run on a route the data no longer has ends when the map opens");
    }

    // A run at its first shop: the map of step 2 forced to a shop node and a fight.
    private static Expedition AtShop(BattleSettings data, int coins, out ExpeditionRun run)
    {
        var x = new Expedition(data, new ExpeditionProgress());
        run = x.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 11);
        x.Roll();
        run.Path.Add(0);
        run.Kinds![1] = new[] { ExpeditionSpec.Shop, ExpeditionSpec.Fight };
        run.Map[1] = new[] { ExpeditionSpec.Shop, data.Expedition!.Fights[0].Id };
        run.Coins = coins;
        return x;
    }

    private static void ShopChecks(Action<bool, string> check, BattleSettings data, ExpeditionSpec main)
    {
        var shop = main.Store!;
        check(shop is { Heal: 0.2, HealPrice: 2, CardPrice: 3, RarePrice: 5, RelicPrice: 6, Cards: 2, Rares: 1, Relics: 1 },
            "Built-in shop: +20% HP for 2 coins, 2 cards for 3, a rare card for 5, a relic for 6");
        var x = AtShop(data, 20, out var run);
        check(x.Mismatch(run) == null && x.Kind(1, 0) == ExpeditionSpec.Shop && x.Node(0) == null, "(setup) step 2 has a shop node; it is no battle");
        check(x.Visit(0) && run.Pending is { Kind: ExpeditionChoice.ShopKind } && x.Options().SequenceEqual(new[] { Expedition.Leave }), "Going to the shop opens it; its only option is to move on");
        var wares = run.Pending!.Wares;
        check(wares.Length == 5 && wares[0] == Expedition.Heal && wares.Count(w => w.StartsWith("card:")) == 2 && wares.Count(w => w.StartsWith("rare:")) == 1 && wares.Count(w => w.StartsWith("relic:")) == 1,
            $"The wares: a heal, two common cards, a rare card and a relic ({string.Join(", ", wares)})");
        check(wares.Where(w => w.StartsWith("card:")).All(w => data.UpgradeList.First(u => u.Id == w[5..]).Rare == false), "The shop's plain cards are common cards");
        var again = AtShop(data, 20, out var run2);
        again.Visit(0);
        check(run2.Pending!.Wares.SequenceEqual(wares), "The same run's shop draws the same wares (kept once drawn)");

        run.Hp = 40;
        double max = x.MaxHp(run);
        check(x.Price(Expedition.Heal) == 2 && x.BuyRefusal(Expedition.Heal) == null, "The heal costs 2 coins and can be bought");
        var heal = x.Buy(Expedition.Heal);
        check(heal.Done && Math.Abs(run.Hp - (40 + 0.2 * max)) < 1e-9 && run.Coins == 18 && run.Pending!.Sold.SequenceEqual(new[] { Expedition.Heal }) && run.Pending.Kind == ExpeditionChoice.ShopKind,
            "Buying the heal: +20% of the max HP, 2 coins paid, the shop stays open");
        check(x.BuyRefusal(Expedition.Heal) == "sold" && !x.Buy(Expedition.Heal).Done && run.Coins == 18, "Each ware sells once");
        string relic = wares.First(w => w.StartsWith("relic:"));
        var bought = x.Buy(relic);
        check(bought.Done && run.Relics.Contains(relic[6..]) && run.Coins == 12 && bought.Relic == relic[6..], "Buying the relic: the run holds it, 6 coins paid");
        string rare = wares.First(w => w.StartsWith("rare:"));
        var rareBought = x.Buy(rare);
        check(rareBought.Done && run.Build.GetValueOrDefault(rare[5..]) == 1 && run.Coins == 7, "Buying the rare card: it joins the build, 5 coins paid");
        string card = wares.First(w => w.StartsWith("card:"));
        x.Buy(card);
        string last = wares.Last(w => w.StartsWith("card:"));
        check(run.Coins == 4 && x.BuyRefusal(last) == null, "(setup) 4 coins left");
        run.Coins = 2;
        check(x.BuyRefusal(last) == "too poor" && !x.Buy(last).Done && run.Coins == 2, "Too few coins: the ware cannot be bought");
        check(run.Log.Count(e => e.Kind == ExpeditionChoice.ShopKind && e.Option == ExpeditionLogEntry.Buy) == 4 && run.Log.Last().Coins == 3 && run.Log.Last().Card == card[5..],
            "Each purchase is in the run log with its price");
        int step = run.Step;
        var left = x.Choose(Expedition.Leave);
        check(left.Done && run.Step == step + 1 && run.Pending == null && run.Path[^1] == 0 && run.Coins == 2, "Moving on passes the shop's step; the coins left stay with the run");

        // A full run: no relic for sale is bought; a full HP heal is refused.
        var full = AtShop(data, 50, out var fullRun);
        fullRun.Relics.AddRange(main.Relics.Take(main.MaxRelics).Select(r => r.Id));
        full.Visit(0);
        check(fullRun.Pending!.Wares.All(w => !w.StartsWith("relic:")) && full.BuyRefusal(Expedition.Heal) == "full",
            "A run already holding its relics is offered none; at full HP the heal is refused");

        // Saved and read back mid-shop: wares and sales kept; a broken shop ends the run.
        var progress = x.Progress;
        var y = AtShop(data, 9, out var saved);
        y.Visit(0);
        saved.Hp = 30;
        y.Buy(Expedition.Heal);
        string dir = Path.Combine(Path.GetTempPath(), "pet-shop-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(dir, "expedition.json");
        try
        {
            check(y.Progress.Save(file), "(setup) saved in the shop");
            var back = new Expedition(data, ExpeditionProgress.Load(file));
            check(back.Mismatch(back.Run!) == null && back.Run!.Pending!.Wares.SequenceEqual(saved.Pending!.Wares) && back.Run.Pending.Sold.SequenceEqual(new[] { Expedition.Heal }) && back.Run.Coins == 7,
                "Reopened in the shop: the same wares, the heal still sold, the coins kept");
            back.Run.Pending.Wares = new[] { "card:no_such_card" };
            check(back.Mismatch(back.Run) != null, "A shop selling what the data no longer has ends the run");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        _ = progress;
    }

    private static void OlderRunChecks(Action<bool, string> check, BattleSettings data, ExpeditionSpec main)
    {
        // A run started under rules 6 (before the shop): it goes on, its spins draw no shop, its record is r6's.
        bool noShop = true;
        for (int seed = 1; seed <= 40; seed++)
        {
            var x = new Expedition(data, new ExpeditionProgress());
            var run = x.Start(BattleWeapon.Sword, BattleDifficulty.Normal, seed);
            run.RulesVersion = 6;
            check(x.Mismatch(run) == null || seed > 1, "A run of rules 6 still fits (keepRuns)");
            for (int step = 0; step < main.PathSteps; step++)
            {
                run.Path.Clear(); run.Path.AddRange(Enumerable.Repeat(0, step));
                x.Roll();
                noShop &= !run.Kinds![step].Contains(ExpeditionSpec.Shop);
            }
        }
        check(noShop, "Its steps still to spin draw no shop (40 runs)");
        var older = new Expedition(data, new ExpeditionProgress());
        var r6 = older.Start(BattleWeapon.Hammer, BattleDifficulty.Hard, 5);
        r6.RulesVersion = 6;
        older.Abandon();
        check(older.Progress.Records.ContainsKey(Expedition.RecordKey(main, BattleWeapon.Hammer, BattleDifficulty.Hard, false, 6)) && older.Record(BattleWeapon.Hammer, BattleDifficulty.Hard).Runs == 0
              && older.Record(BattleWeapon.Hammer, BattleDifficulty.Hard, rulesVersion: 6).Runs == 1,
            "Its end is counted in the records of rules 6, apart from rules 7's");
        var r5 = new Expedition(data, new ExpeditionProgress()).Start(BattleWeapon.Sword, BattleDifficulty.Normal, 5);
        r5.RulesVersion = 5;
        check(new Expedition(data, new ExpeditionProgress { Active = r5 }).Mismatch(r5) != null, "A run of rules 5 still ends (not kept)");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        string builtIn = ExpeditionFixture.Embedded();
        string Error(string expedition)
        {
            try { BattleData.ParseWithOverride(builtIn, "{ \"schemaVersion\": 1, \"expedition\": " + expedition + " }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        string Enc(string id) => $"{{ \"id\": \"{id}\", \"nameVi\": \"E\", \"nameEn\": \"E\", \"goalVi\": \"g\", \"goalEn\": \"g\", \"waves\": [ {{ \"groups\": [ {{ \"enemy\": \"slime\", \"count\": 1 }} ] }} ] }}";
        string Boss(string id) => $"{{ \"id\": \"{id}\", \"nameVi\": \"E\", \"nameEn\": \"E\", \"goalVi\": \"g\", \"goalEn\": \"g\", \"waves\": [ {{ \"groups\": [ {{ \"enemy\": \"king\", \"count\": 1 }} ] }} ] }}";
        string Route(string id = "r2", string extra = "", string fights = "", string zones = "") =>
            $"{{ \"routes\": [ {{ \"id\": \"{id}\", \"nameVi\": \"R\", \"nameEn\": \"R\", \"steps\": [ [ \"fight\" ], [ \"fight\" ] ], \"fights\": [ {(fights == "" ? Enc("r_a") : fights)} ], \"elites\": [], \"boss\": {Boss("r_boss")}{zones}{extra} }} ] }}";
        check(Error(Route()) == "", "A small valid route is accepted");
        check(Error(Route(id: "slime_trail")).Contains("used twice"), "A route with the Expedition's id is rejected");
        check(Error(Route(fights: Enc("x_meadow"))).Contains("two routes"), "A route reusing another route's encounter id is rejected");
        check(Error(Route(extra: ", \"relics\": [ { \"id\": \"r\", \"nameVi\": \"R\", \"nameEn\": \"R\", \"descVi\": \"d\", \"descEn\": \"d\", \"effect\": \"light\" } ]")).Contains("takes its events"),
            "A route giving its own relics is rejected (they are the Expedition's)");
        check(Error(Route(zones: ", \"zones\": [ { \"id\": \"z\", \"nameVi\": \"Z\", \"nameEn\": \"Z\", \"steps\": 2, \"fights\": [ \"r_a\" ] } ]")) == "", "Zones covering every step are accepted");
        check(Error(Route(zones: ", \"zones\": [ { \"id\": \"z\", \"nameVi\": \"Z\", \"nameEn\": \"Z\", \"steps\": 1, \"fights\": [ \"r_a\" ] } ]")).Contains("cover every step"), "Zones not covering every step are rejected");
        check(Error(Route(zones: ", \"zones\": [ { \"id\": \"z\", \"nameVi\": \"Z\", \"nameEn\": \"Z\", \"steps\": 2, \"fights\": [ \"x_meadow\" ] } ]")).Contains("fight encounters of the route"),
            "A zone naming another route's encounter is rejected");
        check(Error(Route(extra: ", \"scene\": \"beach\"")).Contains("scene"), "An unknown scene is rejected");
        check(Error("{ \"keepRuns\": [ 7 ] }").Contains("keepRuns"), "keepRuns must be older than the rules version");
        check(Error("{ \"shop\": { \"cardPrice\": 0 } }").Contains("cardPrice"), "A free shop ware is rejected");
        check(Error("{ \"shop\": null, \"routes\": [] }").Contains("no shop"), "Shop nodes without a shop are rejected");
        check(Error("{ \"kindGacha\": { \"weights\": { \"fight\": 1, \"shop\": 0 } }, \"shop\": null, \"routes\": [] }") == "", "No shop nodes: no shop needed");
    }
}
