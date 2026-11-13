using DesktopPet;

// 1.5 M20, defence since 2026-11-13 (PetBattle.Basket.cs): a stage with a fish basket that has HP. Slimes
// (not bosses, supports, rocks or the ones with a job of their own) go for the basket with their own blows:
// a strike, a dash, goo, a bomb, a burrower, a flyer's drops; the cat lifted does not save it. A slime the
// cat's own blow hits turns on the cat for `aggro` s (each blow starts that again), then goes back for the
// basket; a bomb or an app does not turn it. The basket broken loses the battle with the cat's HP full. The
// stage records keep the most basket HP left in a win. A battle without a basket plays as before. Data
// limits, old keys still read, the example file.
internal static class BattleBasketTests
{
    const double dt = 1.0 / 60;

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static EnemyType Plain(double hp = 1e6) => new() { Id = "slime", MaxHp = hp, Speed = 120, Damage = 10, Range = 50, Windup = 0.4, AttackCooldown = 1, Recover = 0.2 };
    private static EnemyType Herb() => new() { Id = "herb", MaxHp = 1e6, Speed = 120, Damage = 10, Range = 50, Windup = 0.4, AttackCooldown = 1, Recover = 0.2, Support = true };
    private static EnemyType Rock() => new() { Id = "rock", MaxHp = 1e6, Speed = 120, Damage = 10, Range = 50, Windup = 0.4, AttackCooldown = 1, Recover = 0.2, Blocker = true };
    private static readonly BattleSkill[] Tools = { BattleSkill.Bomb, BattleSkill.Shield, BattleSkill.Push, BattleSkill.Cheer, BattleSkill.Heal };

    // A 600-wide field (the basket at 300), the cat at 40. Waves: the kinds of each (in spawn order).
    private static BattleSim Field(BasketSpec? basket, string[][] waves, bool survive = false, EnemyType[]? extra = null)
    {
        var arms = Shipped.Weapon("sword") with { Range = 60, StunChance = 0, Damage = 1 };
        var settings = new BattleSettings
        {
            Weapons = new[] { arms }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 100, MaxAlive = 8, RageHitGain = 0,
            Enemies = new[] { Plain(), Herb(), Rock() }.Concat(extra ?? Array.Empty<EnemyType>()).ToArray()
        };
        var stage = new StageSpec
        {
            Id = "basket", NameVi = "Giỏ", NameEn = "Basket", Basket = basket, Objective = survive ? "survive" : "clear", SurviveSeconds = survive ? 60 : null,
            Waves = waves.Select(w => new WaveSpec { Groups = w.Select(kind => new WaveGroup { Enemy = kind, Count = 1 }).ToArray() }).ToArray()
        };
        return new BattleSim(settings, 0, 600, seed: 5, skills: Tools, stage: stage);
    }

    // Steps (the cat at `catX`, held or not) until `done` (true) or `seconds` passed (false).
    private static bool Until(BattleSim sim, Func<bool> done, double seconds = 20, double catX = 40, bool held = false)
    {
        for (int i = 0; i < Math.Round(seconds / dt); i++)
        {
            sim.Step(dt, catX, held);
            sim.ClearEvents();
            if (done()) return true;
            if (sim.Over) break;
        }
        return false;
    }

    private static void Steps(BattleSim sim, double seconds, double catX = 40) { for (int i = 0; i < Math.Round(seconds / dt); i++) { sim.Step(dt, catX, false); sim.ClearEvents(); } }

    public static void Run(Action<bool, string> check)
    {
        BasicChecks(check);
        AggroChecks(check);
        BlowChecks(check);
        RecordChecks(check);
        DataChecks(check);
    }

    private static void BasicChecks(Action<bool, string> check)
    {
        var sim = Field(new BasketSpec { Hp = 100 }, new[] { new[] { "slime" } });
        check(sim.BasketHp == 100 && sim.BasketPercent == 100 && Math.Abs(sim.BasketX - 300) < 1e-9, "The basket stands in the middle of the strip with its HP");
        foreach (var id in new[] { "slime", "runner", "bomber", "flyer", "burrower", "spitter", "giant", "splitter" })
            check(BattleSim.AttacksBasket(Shipped.Enemy(id)), $"{id} goes for the basket");
        foreach (var id in new[] { "mender", "rock", "thief", "phantom", "springer", "clinger", "timer", "drummer" })
            check(!BattleSim.AttacksBasket(Shipped.Enemy(id)), $"{id} does not go for the basket (its own job, or a support/rock)");
        check(!BattleSim.AttacksBasket(Shipped.EnemyTypes.First(e => e.Boss)) && !BattleSim.AttacksBasket(Herb()) && !BattleSim.AttacksBasket(Rock()), "Bosses never go for the basket");

        // A plain slime walks past the far cat to the basket and hits it there; the cat is never hurt.
        bool hit = Until(sim, () => sim.BasketHits > 0);
        var s = sim.Slimes.First(o => o.Type.Id == "slime");
        check(hit && s.OnBasket && Math.Abs(s.X - 300) <= 50 + 12 && Math.Abs(sim.BasketHp - 90) < 1e-6 && sim.CatHp == sim.CatMaxHp,
            $"A slime hits the basket with its own strike ({sim.BasketHp:0}/100 HP), the cat untouched");
        // The cat lifted does not save the basket.
        int before = sim.BasketHits;
        Until(sim, () => sim.BasketHits > before, 3, held: true);
        check(sim.BasketHits > before, "With the cat held up the slimes still hit the basket");
        bool broken = Until(sim, () => sim.Over, 30);
        check(broken && sim.Phase == BattlePhase.Lost && sim.BasketBroken && sim.BasketHp == 0 && sim.BasketPercent == 0 && sim.CatHp == sim.CatMaxHp,
            "The basket broken loses the battle, the cat's HP full");

        // A support and a rock leave the basket alone (they go for the cat as before).
        var herd = Field(new BasketSpec(), new[] { new[] { "herb", "rock" } });
        Steps(herd, 6);
        check(herd.BasketHits == 0 && herd.Slimes.All(o => !o.OnBasket), "A herb slime and a rock slime never go for the basket");

        // Without a basket nothing changes.
        var none = Field(null, new[] { new[] { "slime" } });
        Until(none, () => none.CatHp < none.CatMaxHp, 10);
        check(double.IsNaN(none.BasketX) && none.CatHp < none.CatMaxHp && none.Slimes.All(o => !o.OnBasket) && none.BasketPercent == 0, "Without a basket the slimes go for the cat as before");

        // The window moves: the basket keeps its place across the strip.
        sim = Field(new BasketSpec { At = 0.25 }, new[] { new[] { "slime" } });
        sim.SetArea(100, 1300);
        check(Math.Abs(sim.BasketX - 400) < 1e-6, "A new work area keeps the basket at the same relative spot");
    }

    private static void AggroChecks(Action<bool, string> check)
    {
        // A slow slime: it never reaches the cat at 40 in these 4 s (so the cat never hits it back).
        var slow = new EnemyType { Id = "slow", MaxHp = 1e6, Speed = 30, Damage = 10, Range = 50, Windup = 0.4, AttackCooldown = 1, Recover = 0.2 };
        var sim = Field(new BasketSpec { Aggro = 4 }, new[] { new[] { "slow" } }, extra: new[] { slow });
        Until(sim, () => sim.BasketHits > 0, 30);
        var s = sim.Slimes.First();
        // A bomb (not the cat's own blow) does not turn it.
        sim.Damage(s, 1, DamageSource.Bomb);
        check(s.AggroLeft == 0, "A hit that is not the cat's own blow (a bomb) leaves it on the basket");
        sim.Damage(s, 1, DamageSource.Weapon);
        check(s.AggroLeft == 4 && sim.Aggroed == 1, "Hit by the cat it turns on the cat for 4 s");
        double at = s.X;
        Steps(sim, 1.5);
        check(!s.OnBasket && s.X < at - 20, $"It leaves the basket for the cat ({at:0} → {s.X:0})");
        // Each blow starts the time again.
        Steps(sim, 1.5);
        sim.Damage(s, 1, DamageSource.Combo);
        check(Math.Abs(s.AggroLeft - 4) < 1e-9 && sim.Aggroed == 1, "Another blow while it is angry starts its 4 s again (counted once)");
        Steps(sim, 3.5);
        check(!s.OnBasket && s.AggroLeft > 0, "3.5 s after that blow it still goes for the cat");
        bool back = Until(sim, () => s.OnBasket, 1);
        check(back && s.AggroLeft == 0, "4 s after the last blow it goes back for the basket");
        int hits = sim.BasketHits;
        Until(sim, () => sim.BasketHits > hits, 10);
        check(sim.BasketHits > hits, "Back at the basket it hits it again");
        // The cat thrown into it counts as the cat's blow.
        sim.Damage(s, 1, DamageSource.Throw);
        check(s.AggroLeft == 4, "The cat thrown into it turns it too");
    }

    private static void BlowChecks(Action<bool, string> check)
    {
        // Each kind's own blow on the basket; the cat stands far off and is never hurt.
        foreach (var id in new[] { "runner", "spitter", "bomber", "burrower", "flyer" })
        {
            var kind = Shipped.Enemy(id);
            var sim = Field(new BasketSpec { Hp = 1000 }, new[] { new[] { id } }, extra: new[] { kind });
            bool hit = Until(sim, () => sim.BasketHits > 0, 40, catX: 30);
            check(hit && sim.CatHp == sim.CatMaxHp, $"{id}: its own blow lands on the basket ({sim.BasketHits} hit(s), {sim.BasketHp:0}/1000), the cat untouched");
        }
        // A thief keeps going for the cat.
        var thief = Shipped.Enemy("thief");
        var t = Field(new BasketSpec(), new[] { new[] { "thief" } }, extra: new[] { thief });
        Steps(t, 5);
        check(t.BasketHits == 0 && t.Slimes.All(o => !o.OnBasket), "A thief slime never goes for the basket");
    }

    private static void RecordChecks(Action<bool, string> check)
    {
        var basket = new StageSpec { Id = "b1", NameVi = "B", NameEn = "B", Basket = new BasketSpec(), Waves = new[] { new WaveSpec() } };
        var plain = new StageSpec { Id = "b2", NameVi = "P", NameEn = "P", Waves = new[] { new WaveSpec() } };
        var campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "c", NameVi = "C", NameEn = "C", Stages = new[] { basket, plain } } } };
        var adventure = new Adventure(campaign, new AdventureProgress());
        var lost = adventure.Apply("r1", basket, true, false, "sword", BattleDifficulty.Normal, 4, 30, basketKept: 0);
        var record = adventure.Record(basket, "sword", BattleDifficulty.Normal);
        check(lost.Counted && !lost.News.BestBasket && record.Wins == 0 && record.BestBasket == 0 && !adventure.IsCompleted("b1"),
            "The basket broken: a loss (no win, no basket record, the stage not won)");
        var first = adventure.Apply("r2", basket, true, true, "sword", BattleDifficulty.Normal, 6, 50, basketKept: 40);
        check(first.News.BestBasket && first.News.Any && adventure.Record(basket, "sword", BattleDifficulty.Normal).BestBasket == 40 && adventure.IsCompleted("b1"),
            "A win with the basket at 40%: the stage won, a new basket record (40%)");
        var lower = adventure.Apply("r3", basket, true, true, "sword", BattleDifficulty.Normal, 6, 60, basketKept: 30);
        var higher = adventure.Apply("r4", basket, true, true, "sword", BattleDifficulty.Normal, 6, 60, basketKept: 85);
        record = adventure.Record(basket, "sword", BattleDifficulty.Normal);
        check(!lower.News.BestBasket && higher.News.BestBasket && record.BestBasket == 85 && record.Wins == 3, "A lower basket is no record; a higher one is (85%)");
        var other = adventure.Apply("r5", plain, true, true, "sword", BattleDifficulty.Normal, 6, 60, basketKept: 90);
        check(!other.News.BestBasket && adventure.Record(plain, "sword", BattleDifficulty.Normal).BestBasket == 0, "A stage without a basket keeps no basket record");
        check(new BattleRecord { BestBasket = 1 }.Empty == false && new BattleRecord().Empty, "A basket record alone is a record");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        var all = Shipped.Campaign?.Chapters.Take(1).SelectMany(c => c.Stages) ?? Enumerable.Empty<StageSpec>();
        check(!all.Any(s => s.Basket != null) && (Shipped.Expedition is not ExpeditionSpec x || x.Fights.Concat(x.Elites).Append(x.Boss).All(e => e?.Basket == null)),
            "No built-in stage outside chapter 2 has a basket (chapter 2 brings it, 1.5 M21)");
        var stage = Shipped.Campaign?.Chapters.SelectMany(c => c.Stages).FirstOrDefault(s => s.Id == "c2_fish_basket");
        check(stage is { Basket: { Hp: 150, At: 0.5 }, RulesVersion: 4 }, $"Keep the fish basket: a 150-HP basket in the middle, rules 4 ({stage?.Basket?.Hp}, {stage?.Basket?.At}, r{stage?.RulesVersion})");
        var d = new BasketSpec();
        check(d is { Hp: 150, At: 0.5, Damage: 1, Aggro: 4 }, "Basket defaults: 150 HP, middle of the strip, ×1 damage, 4 s aggro");
        string Stage(string basket) => "{ \"schemaVersion\": 1, \"campaign\": { \"chapters\": [ { \"id\": \"t\", \"nameVi\": \"T\", \"nameEn\": \"T\", \"stages\": [ { \"id\": \"t1\", \"nameVi\": \"T\", \"nameEn\": \"T\", "
            + "\"goalVi\": \"\", \"goalEn\": \"\", \"basket\": " + basket + ", \"waves\": [ { \"groups\": [ { \"enemy\": \"slime\", \"count\": 1 } ] } ] } ] } ] } }";
        bool Rejects(string basket)
        {
            try { BattleData.ParseWithOverride(ExpeditionFixture.Embedded(), Stage(basket)); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(Rejects("{ \"hp\": 5 }") && Rejects("{ \"hp\": 3000 }") && Rejects("{ \"at\": 0.95 }") && Rejects("{ \"damage\": 0 }") && Rejects("{ \"aggro\": 0.1 }") && Rejects("{ \"aggro\": 31 }"),
            "Basket data out of range is refused");
        check(!Rejects("{ \"hp\": 80, \"aggro\": 2 }"), "A valid basket stage is read");
        check(!Rejects("{ \"fish\": 3, \"every\": 1, \"grab\": 1, \"carry\": 0.8, \"back\": 5 }"), "An older custom file with the fish keys still loads (they are ignored)");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m20.json"))) dir = dir.Parent;
        var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", "battle-m20.json"));
        var stages = load?.Settings.Campaign?.Chapters.SelectMany(c => c.Stages).ToList();
        check(load is { Custom: true, Error: null } && stages is { Count: 2 } && stages[0].Basket is { Hp: 150 } && stages[1] is { Survival: true, Basket: { Hp: 120, Aggro: 3 } },
            $"docs/examples/battle-m20.json loads as custom data with two basket stages ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
    }
}
