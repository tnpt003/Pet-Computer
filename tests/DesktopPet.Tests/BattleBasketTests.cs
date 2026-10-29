using DesktopPet;

// 1.5 M20 (PetBattle.Basket.cs): a stage with a fish basket. Every n-th plain slime is a fish taker (no
// boss, support or rock); it walks to the basket, takes a fish after `grab` s and runs for the edge away
// from the cat, where it gets away (not a defeat of the cat's) and the fish is lost. Defeated, pushed or
// dazed on the way it drops the fish: a click puts it back, left alone it hops back after `back` s, and
// another taker picks it up faster than from the basket. The cat goes for a carrier first. Every fish lost
// loses the battle with the cat's HP full; a won battle keeps the fish still lying or carried. The stage
// records keep the most fish kept in a win. A battle without a basket plays as before. Data limits, the
// example file.
internal static class BattleBasketTests
{
    const double dt = 1.0 / 60;

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static EnemyType Plain(double hp = 10) => new() { Id = "slime", MaxHp = hp, Speed = 120, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3 };
    private static EnemyType Herb() => new() { Id = "herb", MaxHp = 10, Speed = 120, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3, Support = true };
    private static EnemyType Rock() => new() { Id = "rock", MaxHp = 10, Speed = 120, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3, Blocker = true };
    private static readonly BattleSkill[] Tools = { BattleSkill.Bomb, BattleSkill.Shield, BattleSkill.Push, BattleSkill.Cheer, BattleSkill.Heal };

    // A 600-wide field (the basket at 300), the cat at 100 with a weapon that defeats a slime in one hit.
    // Waves: the kinds of each (in spawn order).
    private static BattleSim Field(BasketSpec? basket, string[][] waves, bool survive = false, double hp = 10)
    {
        var arms = Shipped.Weapon("sword") with { Range = 60, StunChance = 0, Damage = 1000 };
        var settings = new BattleSettings
        {
            Weapons = new[] { arms }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 100, MaxAlive = 8, Enemies = new[] { Plain(hp), Herb(), Rock() }, RageHitGain = 0
        };
        var stage = new StageSpec
        {
            Id = "basket", NameVi = "Giỏ", NameEn = "Basket", Basket = basket, Objective = survive ? "survive" : "clear", SurviveSeconds = survive ? 30 : null,
            Waves = waves.Select(w => new WaveSpec { Groups = w.Select(kind => new WaveGroup { Enemy = kind, Count = 1 }).ToArray() }).ToArray()
        };
        return new BattleSim(settings, 0, 600, seed: 5, skills: Tools, stage: stage);
    }

    private static BasketSpec Spec(int fish = 2, int every = 1, double back = 5) => new() { Fish = fish, Every = every, Back = back, Grab = 1.2, Carry = 0.8 };

    // Steps (the cat standing at `catX`) until an event of `kind` (true) or `seconds` passed (false).
    private static bool Until(BattleSim sim, BattleEventKind kind, double seconds = 20, double catX = 100, Action? each = null)
    {
        for (int i = 0; i < Math.Round(seconds / dt); i++)
        {
            each?.Invoke();
            sim.Step(dt, catX, false);
            bool seen = sim.Events.Any(e => e.Kind == kind);
            sim.ClearEvents();
            if (seen) return true;
            if (sim.Over) break;
        }
        return false;
    }

    private static void Steps(BattleSim sim, double seconds, double catX = 100) { for (int i = 0; i < Math.Round(seconds / dt); i++) { sim.Step(dt, catX, false); sim.ClearEvents(); } }

    private static Slime Carrier(BattleSim sim) => sim.Slimes.First(s => s.CarriesFish);

    public static void Run(Action<bool, string> check)
    {
        TakerChecks(check);
        CarryChecks(check);
        DropChecks(check);
        TargetChecks(check);
        EndChecks(check);
        RecordChecks(check);
        DataChecks(check);
    }

    private static void TakerChecks(Action<bool, string> check)
    {
        var sim = Field(Spec(fish: 3, every: 2), new[] { new[] { "slime", "slime", "herb", "rock", "slime", "slime" } });
        Steps(sim, 0.6);
        var order = sim.Slimes.OrderBy(s => s.Id).ToList();
        check(sim.FishTotal == 3 && sim.FishInBasket == 3 && Math.Abs(sim.BasketX - 300) < 1e-9, "The basket stands at its spot (half the strip) with its fish");
        check(order.Count == 6 && order.Select(s => s.FishTaker).SequenceEqual(new[] { false, true, false, false, false, true }),
            "Every second plain slime is a fish taker (the herb slime and the rock never are)");
        check(!BattleSim.MayTakeFish(Shipped.Enemy("mender")) && !BattleSim.MayTakeFish(Shipped.Enemy("rock")) && !BattleSim.MayTakeFish(Shipped.EnemyTypes.First(e => e.Boss))
            && !BattleSim.MayTakeFish(Shipped.Enemy("thief")) && !BattleSim.MayTakeFish(Shipped.Enemy("flyer")) && BattleSim.MayTakeFish(Shipped.Enemy("slime"))
            && BattleSim.MayTakeFish(Shipped.Enemy("runner")), "Shipped kinds: plain and quick slimes take fish; healers, rocks, bosses, thieves, flyers never");

        var none = Field(null, new[] { new[] { "slime", "slime" } });
        Steps(none, 0.6);
        check(none.FishTotal == 0 && double.IsNaN(none.BasketX) && none.Slimes.All(s => !s.FishTaker), "Without a basket: no fish, no takers");
        Steps(none, 6);
        check(none.Defeated == 2 && none.FishLost == 0, "Without a basket the slimes go for the cat as before");
    }

    private static void CarryChecks(Action<bool, string> check)
    {
        var sim = Field(Spec(fish: 2), new[] { new[] { "slime" }, new[] { "slime" } });
        double took = -1;
        bool taken = Until(sim, BattleEventKind.FishTaken, each: () => { if (took < 0 && sim.Slimes.Any(s => s.FishClock > 0)) took = sim.Time; });
        var s = sim.Slimes.Single();
        check(taken && s.CarriesFish && sim.FishInBasket == 1 && Math.Abs(s.X - 300) <= 8 && sim.Time - took is > 1.1 and < 1.3,
            $"A taker walks to the basket and takes a fish after its grab time ({sim.Time - took:0.00} s)");
        check(s.FleeSide == 1, "It runs for the edge away from the cat");
        double x = s.X;
        Steps(sim, 0.5);
        check(Math.Abs(s.X - x - 120 * 0.8 * 0.5) < 3, $"Carrying a fish it runs at {s.X - x:0}/{120 * 0.8 * 0.5:0} DIPs in 0.5 s (× carry)");
        bool lost = Until(sim, BattleEventKind.FishLost);
        check(lost && sim.FishLost == 1 && sim.FishLeft == 1 && s.Escaped && s.State == SlimeState.Defeated && sim.Defeated == 0 && !sim.Over,
            "At the edge it gets away with the fish: one fish lost, not a defeat of the cat's, the battle goes on");
        bool gone = Until(sim, BattleEventKind.Lost, 30);
        check(gone && sim.Phase == BattlePhase.Lost && sim.FishGone && sim.FishLeft == 0 && sim.CatHp == sim.CatMaxHp,
            "The last fish carried off loses the battle, the cat's HP still full");

        // Held: the slimes go on taking fish (the user cannot just hold the cat up).
        var held = Field(Spec(fish: 1), new[] { new[] { "slime" } });
        Steps(held, 0.2);
        for (int i = 0; i < 600 && !held.Over; i++) { held.Step(dt, 100, true, true); held.ClearEvents(); }
        check(held.FishGone, "With the cat held up the slimes still carry the fish off");
    }

    private static void DropChecks(Action<bool, string> check)
    {
        var sim = Field(Spec(fish: 2, back: 5), new[] { new[] { "slime" }, new[] { "slime" } });
        Until(sim, BattleEventKind.FishTaken);
        var s = Carrier(sim);
        sim.Damage(s, 100, DamageSource.Bomb);
        check(s.State == SlimeState.Defeated && !s.CarriesFish && sim.StrayFish.Count == 1 && Math.Abs(sim.StrayFish[0].X - s.X) < 1 && sim.FishInBasket == 1 && sim.FishLeft == 2,
            "A carrier defeated drops its fish where it is (still the cat's)");
        bool back = Until(sim, BattleEventKind.FishReturned, 6, each: () => { foreach (var o in sim.Slimes) o.FishTaker = false; });
        check(back && sim.StrayFish.Count == 0 && sim.FishInBasket == 2, "Left alone, the fish hops back to the basket after its time");

        sim = Field(Spec(fish: 2), new[] { new[] { "slime" }, new[] { "slime" } });
        Until(sim, BattleEventKind.FishTaken);
        s = Carrier(sim);
        sim.Step(dt, 100, false);
        check(sim.UsePush(s.Id, 100), "Push on the carrier");
        sim.Step(dt, 100, false);
        check(!s.CarriesFish && sim.StrayFish.Count == 1, "Pushed (dazed), a carrier drops its fish");
        int id = sim.StrayFish[0].Id;
        sim.Paused = true;
        check(!sim.PickUpFish(id) && sim.StrayFish.Count == 1, "Paused: a dropped fish cannot be put back");
        sim.Paused = false;
        check(sim.PickUpFish(id) && sim.StrayFish.Count == 0 && sim.FishInBasket == 2 && !sim.PickUpFish(id), "A click puts a dropped fish back in the basket at once (once)");

        // Another taker goes for a fish lying on the floor first, and takes it faster.
        sim = Field(Spec(fish: 3, back: 30), new[] { new[] { "slime", "slime" } });
        Until(sim, BattleEventKind.FishTaken);
        s = Carrier(sim);
        sim.Damage(s, 100, DamageSource.Bomb);
        var other = sim.Slimes.First(o => o != s && o.State != SlimeState.Defeated);
        double from = sim.Time;
        bool retaken = Until(sim, BattleEventKind.FishTaken);
        check(retaken && other.CarriesFish && sim.StrayFish.Count == 0 && sim.FishInBasket == 2 && sim.Time - from < 1.2,
            $"Another taker picks the dropped fish up first, faster than from the basket ({sim.Time - from:0.00} s)");

        // The window moves: the basket and the fish on the floor keep their place across it.
        sim = Field(Spec(fish: 2), new[] { new[] { "slime" } });
        Until(sim, BattleEventKind.FishTaken);
        sim.Damage(Carrier(sim), 100, DamageSource.Bomb);
        double fishX = sim.StrayFish[0].X;
        sim.SetArea(100, 1300);
        check(Math.Abs(sim.BasketX - 700) < 1e-6 && Math.Abs(sim.StrayFish[0].X - (100 + fishX * 2)) < 1e-6, "A new work area keeps the basket and the fish at the same relative spot");
    }

    private static void TargetChecks(Action<bool, string> check)
    {
        // A plain slime next to the cat, a carrier far off: the cat goes for the carrier.
        var sim = Field(Spec(fish: 2, every: 2), new[] { new[] { "slime", "slime" } }, hp: 1e6);
        Until(sim, BattleEventKind.FishTaken, catX: 560);
        var carrier = Carrier(sim);
        var near = sim.Slimes.First(o => !o.FishTaker);
        near.X = 140;
        var order = sim.Step(dt, 100, false);
        check(sim.TargetId == carrier.Id && order.X > 100, "The cat goes for a slime carrying a fish before one closer to it");
        var plain = Field(null, new[] { new[] { "slime", "slime" } }, hp: 1e6);
        Steps(plain, 0.6);
        plain.Slimes[0].X = 140; plain.Slimes[1].X = 400;
        plain.Step(dt, 100, false);
        check(plain.TargetId == plain.Slimes[0].Id, "Without a carrier the cat goes for the nearest as before");
    }

    private static void EndChecks(Action<bool, string> check)
    {
        // Won with a fish on the floor: it is kept.
        var sim = Field(Spec(fish: 3), new[] { new[] { "slime" } });
        Until(sim, BattleEventKind.FishTaken);
        sim.Damage(Carrier(sim), 100, DamageSource.Bomb);
        Steps(sim, 0.1);
        check(sim.Phase == BattlePhase.Won && sim.FishInBasket == 3 && sim.StrayFish.Count == 0 && !sim.FishGone, "A win keeps a fish still lying on the floor (3/3 fish)");

        // A survive stage ends with a fish still carried: kept too.
        sim = Field(Spec(fish: 2), new[] { new[] { "slime" } }, survive: true, hp: 1e6);
        bool won = false;
        for (int i = 0; i < 31 * 60 && !sim.Over; i++)
        {
            sim.Step(dt, 100, false);
            sim.ClearEvents();
            // Keep the carrier from its edge until the clock is out.
            foreach (var s in sim.Slimes) if (s.CarriesFish) s.X = 320;
        }
        won = sim.Phase == BattlePhase.Won;
        check(won && sim.FishInBasket == 2 && sim.FishLost == 0, "A survive stage won with a fish still carried keeps it (2/2 fish)");

        // Lost by HP: the fish not lost are still counted as the cat's (FishLeft).
        sim = Field(Spec(fish: 2), new[] { new[] { "slime" }, new[] { "slime" } });
        Until(sim, BattleEventKind.FishLost);
        check(sim.FishLeft == 1 && sim.FishLost == 1, "One fish lost: the HUD shows 1/2");
    }

    private static void RecordChecks(Action<bool, string> check)
    {
        var basket = new StageSpec { Id = "b1", NameVi = "B", NameEn = "B", Basket = Spec(fish: 5), Waves = new[] { new WaveSpec() } };
        var plain = new StageSpec { Id = "b2", NameVi = "P", NameEn = "P", Waves = new[] { new WaveSpec() } };
        var campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "c", NameVi = "C", NameEn = "C", Stages = new[] { basket, plain } } } };
        var adventure = new Adventure(campaign, new AdventureProgress());
        var lost = adventure.Apply("r1", basket, true, false, "sword", BattleDifficulty.Normal, 4, 30, fishKept: 0);
        var record = adventure.Record(basket, "sword", BattleDifficulty.Normal);
        check(lost.Counted && !lost.News.MostFish && record.Wins == 0 && record.MostFish == 0 && record.BestSeconds == null && !adventure.IsCompleted("b1"),
            "Every fish lost: a loss (no win, no fastest time, no fish record, the stage not won)");
        var first = adventure.Apply("r2", basket, true, true, "sword", BattleDifficulty.Normal, 6, 50, fishKept: 3);
        check(first.News.MostFish && first.News.Any && adventure.Record(basket, "sword", BattleDifficulty.Normal).MostFish == 3 && adventure.IsCompleted("b1"),
            "A win keeping 3 fish: the stage won, a new most-fish record (3)");
        var fewer = adventure.Apply("r3", basket, true, true, "sword", BattleDifficulty.Normal, 6, 60, fishKept: 2);
        var more = adventure.Apply("r4", basket, true, true, "sword", BattleDifficulty.Normal, 6, 60, fishKept: 5);
        record = adventure.Record(basket, "sword", BattleDifficulty.Normal);
        check(!fewer.News.MostFish && more.News.MostFish && record.MostFish == 5 && record.Wins == 3, "Fewer fish is no record; more is (5)");
        var other = adventure.Apply("r5", plain, true, true, "sword", BattleDifficulty.Normal, 6, 60, fishKept: 5);
        check(!other.News.MostFish && adventure.Record(plain, "sword", BattleDifficulty.Normal).MostFish == 0, "A stage without a basket keeps no fish record");
        check(new BattleRecord { MostFish = 1 }.Empty == false && new BattleRecord().Empty, "A fish record alone is a record");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        var all = Shipped.Campaign?.Chapters.SelectMany(c => c.Stages) ?? Enumerable.Empty<StageSpec>();
        check(!all.Any(s => s.Basket != null) && (Shipped.Expedition is not ExpeditionSpec x || x.Fights.Concat(x.Elites).Append(x.Boss).All(e => e?.Basket == null)),
            "No built-in stage has a basket yet (chapter 2 brings it)");
        var d = new BasketSpec();
        check(d is { Fish: 5, At: 0.5, Every: 2, Grab: 1.2, Carry: 0.8, Back: 5 }, "Basket defaults: 5 fish, middle of the strip, every 2nd slime, 1.2 s, ×0.8, 5 s");
        string Stage(string basket) => "{ \"schemaVersion\": 1, \"campaign\": { \"chapters\": [ { \"id\": \"t\", \"nameVi\": \"T\", \"nameEn\": \"T\", \"stages\": [ { \"id\": \"t1\", \"nameVi\": \"T\", \"nameEn\": \"T\", "
            + "\"goalVi\": \"\", \"goalEn\": \"\", \"basket\": " + basket + ", \"waves\": [ { \"groups\": [ { \"enemy\": \"slime\", \"count\": 1 } ] } ] } ] } ] } }";
        bool Rejects(string basket)
        {
            try { BattleData.ParseWithOverride(ExpeditionFixture.Embedded(), Stage(basket)); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(Rejects("{ \"fish\": 0 }") && Rejects("{ \"fish\": 21 }") && Rejects("{ \"at\": 0.95 }") && Rejects("{ \"every\": 0 }") && Rejects("{ \"grab\": 9 }")
            && Rejects("{ \"carry\": 0.1 }") && Rejects("{ \"back\": 0 }"), "Basket data out of range is refused");
        check(!Rejects("{ \"fish\": 3, \"every\": 1 }"), "A valid basket stage is read");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m20.json"))) dir = dir.Parent;
        var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", "battle-m20.json"));
        var stages = load?.Settings.Campaign?.Chapters.SelectMany(c => c.Stages).ToList();
        check(load is { Custom: true, Error: null } && stages is { Count: 2 } && stages[0].Basket is { Fish: 5 } && stages[1] is { Survival: true, Basket.Every: 1 },
            $"docs/examples/battle-m20.json loads as custom data with two basket stages ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
    }
}
