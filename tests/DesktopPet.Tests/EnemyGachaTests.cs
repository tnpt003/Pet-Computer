using DesktopPet;

// Enemy Director milestone 3: bounded randomness. Elite affixes (rolled once at spawn on their own
// stream, from a stage's eliteChance, never on Easy, at most maxElites alive, paid from a budget),
// wave and stream budgets, and one rare card per group in the upgrade offers.
internal static class EnemyGachaTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id, double hp = 50, double cost = 1, EliteSpec? elite = null) => new()
    {
        Id = id, MaxHp = hp, Speed = 0, Damage = 0, Range = 64, Windup = 0.8, AttackCooldown = 1.8, Recover = 0.35, Cost = cost, Elite = elite
    };

    private static readonly EliteSpec Quick = new() { WindupAdd = 0.3, DashSpeed = 1.4, DescVi = "a", DescEn = "a" };
    private static EnemyType Runner() => new()
    {
        Id = "runner", MaxHp = 25, Speed = 0, Damage = 0, Range = 54, Windup = 0.7, AttackCooldown = 2.6, Recover = 0.4, Scale = 0.75,
        Dash = true, DashRange = 2000, DashDistance = 230, DashSpeed = 420, Elite = Quick
    };

    private static BattleSettings Settings(EnemyType[] kinds, int maxAlive = 3, int maxElites = 1, double catDamage = 0, double rare = 0.15) => new()
    {
        FirstWaveDelay = 0, SpawnInterval = 0.2, CatDamage = catDamage, CatMaxHp = 1_000_000, MaxAlive = maxAlive, MaxElites = maxElites, Enemies = kinds, RareChance = rare
    };

    private static StageSpec Stage(double elite, params WaveSpec[] waves) => new() { Id = "t", EliteChance = elite, Waves = waves };
    private static WaveSpec Wave(params WaveGroup[] groups) => new() { Groups = groups };
    private static WaveGroup G(string enemy, int count = 1) => new() { Enemy = enemy, Count = count };

    private static void Run(BattleSim sim, double seconds, Func<double>? catX = null, Action? each = null)
    {
        for (double t = 0; t < seconds && !sim.Over; t += dt) { sim.Step(dt, catX?.Invoke() ?? 800, false); each?.Invoke(); sim.ClearEvents(); }
    }

    public static void Run(Action<bool, string> check)
    {
        EliteRollChecks(check);
        EliteEffectChecks(check);
        BudgetChecks(check);
        RareOfferChecks(check);
        RareEffectChecks(check);
        DataChecks(check);
        ShippedChecks(check);
    }

    private static void EliteRollChecks(Action<bool, string> check)
    {
        var kinds = new[] { Kind("slime", elite: new EliteSpec { SureBlocks = 0, FirstHeal = 1, WindupAdd = 0.1, DescVi = "a", DescEn = "a" }), Kind("plain") };
        var three = Stage(1, Wave(G("slime", 3)));
        var sim = new BattleSim(Settings(kinds), 0, 1600, stage: three, seed: 3);
        int most = 0, appeared = 0;
        Run(sim, 3, each: () => { most = Math.Max(most, sim.Slimes.Count(s => s.Elite != null && s.State != SlimeState.Defeated)); appeared += sim.Events.Count(e => e.Kind == BattleEventKind.EliteAppeared); });
        check(sim.Slimes.Count == 3 && most == 1 && sim.ElitesSpawned == 1 && appeared == 1,
            $"eliteChance 1: one elite among three, never more than maxElites (1) alive, announced once ({sim.ElitesSpawned})");
        var two = new BattleSim(Settings(kinds, maxElites: 2), 0, 1600, stage: three, seed: 3);
        Run(two, 3);
        check(two.ElitesSpawned == 2, "maxElites 2: two elites alive at once");
        var easy = new BattleSim(Settings(kinds), 0, 1600, difficulty: BattleDifficulty.Easy, stage: three, seed: 3);
        Run(easy, 3);
        var free = new BattleSim(Settings(kinds), 0, 1600, seed: 3);
        Run(free, 6);
        var none = new BattleSim(Settings(kinds), 0, 1600, stage: Stage(1, Wave(G("plain", 3))), seed: 3);
        Run(none, 3);
        check(easy.ElitesSpawned == 0 && free.ElitesSpawned == 0 && none.ElitesSpawned == 0,
            "No elites on Easy, in a free battle or for a kind without an affix");
        // The elite roll has its own stream: it never changes what spawns where.
        var pick = Stage(0, Wave(new WaveGroup { Pick = new() { ["slime"] = 1, ["plain"] = 1 }, Count = 3 }));
        string Spawns(double chance, int seed)
        {
            var b = new BattleSim(Settings(kinds), 0, 1600, stage: new StageSpec { Id = "t", EliteChance = chance, Waves = pick.Waves }, seed: seed);
            Run(b, 3);
            return string.Join(",", b.Slimes.Select(s => $"{s.Type.Id}@{s.X:0}"));
        }
        check(Enumerable.Range(1, 10).All(seed => Spawns(0, seed) == Spawns(0.5, seed) && Spawns(0.5, seed) == Spawns(1, seed)),
            "Elite rolls never change what spawns where (their own random stream)");
        string Elites(int seed)
        {
            var b = new BattleSim(Settings(kinds, maxElites: 3), 0, 1600, stage: new StageSpec { Id = "t", EliteChance = 0.5, Waves = new[] { Wave(G("slime", 3)) } }, seed: seed);
            Run(b, 3);
            return string.Join("", b.Slimes.Select(s => s.Elite != null ? "E" : "-"));
        }
        check(Elites(4) == Elites(4) && Enumerable.Range(1, 12).Select(Elites).Distinct().Count() > 2, "The same seed gives the same elites; seeds differ");

        // A boss's helpers are never elite.
        var boss = new EnemyType { Id = "boss", Boss = true, MaxHp = 100, Speed = 0, Damage = 0, Range = 64, Windup = 5, AttackCooldown = 9, SummonAt = 0.5, SummonCount = 2, SummonType = "slime" };
        var bossFight = new BattleSim(Settings(new[] { kinds[0], boss }, maxElites: 5, catDamage: 60), 0, 1600, stage: Stage(1, Wave(G("boss"))), seed: 2);
        Run(bossFight, 4, () => bossFight.Boss is Slime b ? b.X + (b.X < 800 ? 70 : -70) : 800);
        check(bossFight.Slimes.Count(s => s.SummonedBy != 0) == 2 && bossFight.Slimes.All(s => s.Elite == null), "A boss's helpers are never elite");
    }

    private static void EliteEffectChecks(Action<bool, string> check)
    {
        // Quick slime: a longer windup and a faster dash.
        var sim = new BattleSim(Settings(new[] { Runner() }), 0, 1600, stage: Stage(1, Wave(G("runner"))), seed: 1);
        Slime? runner = null;
        double before = double.NaN, moved = 0;
        bool dashing = false;
        // One frame of the dash (the frame after it began).
        Run(sim, 4, each: () =>
        {
            runner ??= sim.Slimes.FirstOrDefault();
            if (runner == null) return;
            if (runner.State == SlimeState.Dashing && dashing && moved == 0) moved = Math.Abs(runner.X - before);
            dashing = runner.State == SlimeState.Dashing;
            before = runner.X;
        });
        check(runner?.Elite != null && Math.Abs(sim.Windup(runner) - 1.0) < 1e-9, "An elite quick slime winds up 0.3 s longer (0.7 → 1.0 s)");
        check(Math.Abs(moved - 420 * 1.4 * dt) < 0.5, $"…and dashes ×1.4 as fast ({moved / dt:0} DIPs/s)");

        // Rock: its first front hit is surely blocked (no roll), the bomb still lands and leaves it.
        var rock = new EnemyType { Id = "rock", MaxHp = 500, Speed = 0, Damage = 0, Range = 72, Windup = 9, AttackCooldown = 9, Recover = 0.5, Scale = 1.35, BlockChance = 0.01, BlockFrontOnly = true,
            Elite = new EliteSpec { SureBlocks = 1, DescVi = "a", DescEn = "a" } };
        var fight = new BattleSim(Settings(new[] { rock }, catDamage: 25), 0, 1600, stage: Stage(1, Wave(G("rock"))), seed: 1);
        Run(fight, 1);
        var stone = fight.Slimes[0];
        double front = stone.X + stone.Facing * 70;
        fight.Use(BattleSkill.Bomb, stone.Id, front);
        Run(fight, 0.6, () => 1599);
        check(stone.Hp == 470 && stone.SureBlocks == 1, "The yarn bomb lands on an elite rock and leaves its sure block");
        double hpBefore = stone.Hp;
        Run(fight, 0.2, () => front);
        bool firstBlocked = fight.BlocksMade == 1 && stone.Hp == hpBefore && stone.SureBlocks == 0;
        Run(fight, 2.5, () => front);
        check(firstBlocked && stone.Hp < hpBefore, "An elite rock surely blocks its first front hit, then hits land as usual");

        // Herb slime: its first heal that lands is twice as strong (within its budget), then as usual.
        var healer = new EnemyType { Id = "mender", MaxHp = 40, Speed = 0, Damage = 0, Range = 56, Windup = 0.8, AttackCooldown = 2, Support = true, KeepBack = 0,
            HealAmount = 10, HealRange = 2000, HealCooldown = 1, HealWindup = 0.2, HealBudget = 100, Elite = new EliteSpec { FirstHeal = 2, DescVi = "a", DescEn = "a" } };
        var heals = new List<double>();
        var mend = new BattleSim(Settings(new[] { Kind("slime", 500), healer }), 0, 1600, stage: Stage(1, Wave(G("slime"), G("mender"))), seed: 1);
        Run(mend, 1);
        var ally = mend.Slimes.First(s => s.Type.Id == "slime");
        ally.Hp = 100;
        Run(mend, 3, each: () => heals.AddRange(mend.Events.Where(e => e.Kind == BattleEventKind.EnemyHealed).Select(e => e.Value)));
        check(mend.Slimes.First(s => s.Type.Support).Elite != null && heals.Count >= 2 && heals[0] == 20 && heals[1] == 10,
            $"An elite herb slime's first heal is ×2, the next ones as usual ({string.Join(", ", heals)})");
    }

    private static void BudgetChecks(Action<bool, string> check)
    {
        double Cost(string k) => k == "rock" ? 2 : 1;
        var pick = new WaveGroup { Pick = new() { ["rock"] = 1, ["slime"] = 1 }, Count = 4 };
        var bounded = new WaveSpec { Budget = 5, Groups = new[] { pick } };
        var draws = Enumerable.Range(0, 300).Select(i => SpawnDirector.Units(bounded, new Random(i), Cost).Select(u => u.Kinds[0]).ToList()).ToList();
        check(draws.All(d => d.Count == 4 && d.Sum(Cost) <= 5) && draws.Any(d => d.Contains("rock")),
            "A wave budget: drawn kinds that do not fit give way to the cheapest (4 draws, budget 5, never above)");
        var open = Wave(pick);
        check(Enumerable.Range(0, 100).All(i => SpawnDirector.Units(open, new Random(i)).Select(u => u.Kinds[0]).SequenceEqual(SpawnDirector.Units(open, new Random(i), Cost).Select(u => u.Kinds[0]))),
            "Without a budget the draw is what it always was");
        var fixedCost = new WaveSpec { Budget = 3, Groups = new[] { G("rock", 2), new WaveGroup { Pick = new() { ["slime"] = 1 }, Count = 2 } } };
        check(SpawnDirector.Units(fixedCost, new Random(1), Cost).Count == 4, "Fixed groups always come (the budget only limits drawn kinds)");

        // What a wave's budget leaves pays for elites.
        var kinds = new[] { Kind("slime", elite: new EliteSpec { WindupAdd = 0.1, Cost = 1, DescVi = "a", DescEn = "a" }) };
        int Elites(double budget)
        {
            var b = new BattleSim(Settings(kinds, maxElites: 3), 0, 1600, stage: Stage(1, new WaveSpec { Budget = budget, Groups = new[] { G("slime", 3) } }), seed: 1);
            Run(b, 3);
            return b.ElitesSpawned;
        }
        check(Elites(3) == 0 && Elites(4) == 1 && Elites(6) == 3, $"Elites are paid from what the wave's budget leaves (budget 3/4/6: {Elites(3)}/{Elites(4)}/{Elites(6)} elites)");

        // A stream's budget caps what is on the floor at once, elites included.
        var stony = new[] { Kind("rock", cost: 2, elite: new EliteSpec { WindupAdd = 0.1, Cost = 1, DescVi = "a", DescEn = "a" }), Kind("slime") };
        var stage = new StageSpec { Id = "s", Objective = "survive", SurviveSeconds = 60, EliteChance = 1, Waves = new[] { new WaveSpec { Stream = new StreamSpec { Weights = new() { ["rock"] = 3, ["slime"] = 1 }, Interval = 0.5, IntervalEnd = 0.5, Budget = 5 } } } };
        var flow = new BattleSim(Settings(stony, maxAlive: 5, maxElites: 2), 0, 1600, stage: stage, seed: 4);
        double worst = 0;
        Run(flow, 20, each: () => worst = Math.Max(worst, flow.Slimes.Where(s => s.State != SlimeState.Defeated).Sum(s => s.Type.Cost + (s.Elite?.Cost ?? 0))));
        check(worst <= 5 && flow.Alive >= 2, $"A stream's budget caps the cost on the floor, elites included (most {worst} of 5)");
    }

    private static readonly UpgradeSpec[] Cards =
    {
        new() { Id = "d1", Group = "defense", Effect = "armor", Amount = 0.1, MaxStacks = 3 }, new() { Id = "d2", Group = "defense", Effect = "maxHp", Amount = 10, MaxStacks = 3 },
        new() { Id = "p1", Group = "power", Effect = "damage", Amount = 0.1, MaxStacks = 3 }, new() { Id = "p2", Group = "power", Effect = "knock", Amount = 10, MaxStacks = 3 },
        new() { Id = "s1", Group = "speed", Effect = "moveSpeed", Amount = 0.1, MaxStacks = 3 }, new() { Id = "s2", Group = "speed", Effect = "attackSpeed", Amount = 0.1, MaxStacks = 3 },
        new() { Id = "shell", Group = "defense", Rare = true, Effect = "shell", Amount = 12, MaxStacks = 1 },
        new() { Id = "opener", Group = "power", Rare = true, Effect = "opener", Amount = 1, MaxStacks = 1 },
        new() { Id = "momentum", Group = "speed", Rare = true, Effect = "momentum", Amount = 0.8, MaxStacks = 1 },
    };

    // A stage that offers cards after its first wave (a slime the cat beats at once).
    private static BattleSim Offering(int seed, double rare, IReadOnlyDictionary<string, int>? build = null)
    {
        var settings = new BattleSettings { FirstWaveDelay = 0, CatDamage = 100, CatMaxHp = 1_000_000, Enemies = new[] { Kind("slime") }, Upgrades = Cards, RareChance = rare };
        var stage = new StageSpec { Id = "o", Upgrades = true, Waves = new[] { Wave(G("slime")), Wave(G("slime")) } };
        var sim = new BattleSim(settings, 0, 1600, stage: stage, build: build, seed: seed);
        for (double t = 0; t < 30 && !sim.ChoosingUpgrade && !sim.Over; t += dt)
        {
            double cat = sim.Slimes.FirstOrDefault(s => s.Targetable) is Slime s ? s.X + (s.X < 800 ? 50 : -50) : 800;
            sim.Step(dt, cat, false);
        }
        return sim;
    }

    private static void RareOfferChecks(Action<bool, string> check)
    {
        var offers = Enumerable.Range(1, 600).Select(seed => Offering(seed, 0.15).UpgradeOffer!).ToList();
        check(offers.All(o => o.Count == 3 && o.Select(c => c.Group).Distinct().Count() == 3), "Offers keep one card of each group, rare or not");
        foreach (var group in BuildGroups.All)
        {
            double share = offers.Count(o => o.Any(c => c.Rare && c.Group == group)) / (double)offers.Count;
            check(share is > 0.1 and < 0.2, $"The {group} rare card takes its group's place about 15 % of the time ({share:P0})");
        }
        // The rare roll has its own stream: the common cards are the ones they would have been.
        check(Enumerable.Range(1, 200).All(seed =>
        {
            var plain = Offering(seed, 0).UpgradeOffer!;
            var mixed = Offering(seed, 0.15).UpgradeOffer!;
            return plain.Zip(mixed).All(p => p.Second.Rare || p.First.Id == p.Second.Id);
        }), "Rare cards never change the common cards of an offer (their own random stream)");
        var always = Enumerable.Range(1, 30).Select(seed => Offering(seed, 1).UpgradeOffer!).ToList();
        check(always.All(o => o.All(c => c.Rare)), "rareChance 1: each group shows its rare card");
        var taken = Enumerable.Range(1, 30).Select(seed => Offering(seed, 1, new Dictionary<string, int> { ["shell"] = 1 }).UpgradeOffer!).ToList();
        check(taken.All(o => !o.Any(c => c.Id == "shell") && o.Count(c => c.Rare) == 2), "A rare card already in the build is never offered again (at most one)");
        var sim = Offering(3, 1);
        sim.ChooseUpgrade("opener", 800);
        check(sim.GroupPoints("power") == 1 && sim.Stacks("opener") == 1 && sim.Build().ContainsKey("opener"), "A rare card counts one point for its group and is carried in the build");
    }

    // Slimes of this HP standing still, the first one chosen as the cat's target.
    private static (BattleSim Sim, Slime Slime) Target(IReadOnlyDictionary<string, int> build, double hp = 1000, double damage = 0, double windup = 0.8, int count = 1)
    {
        // No combos (the chain lapses between swings), so each hit shows its own bonus.
        var settings = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0.1, CatDamage = 25, CatMaxHp = 1000, MaxAlive = 3, Upgrades = Cards, ComboWindow = 0.1,
            Enemies = new[] { new EnemyType { Id = "slime", MaxHp = hp, Speed = 0, Damage = damage, Range = 64, Windup = windup, AttackCooldown = 1, Recover = 0.1 } } };
        var sim = new BattleSim(settings, 0, 1600, stage: new StageSpec { Id = "r", Waves = new[] { Wave(G("slime", count)) } }, build: build, seed: 1);
        Run(sim, 0.8 + 0.1 * count, () => 800);
        sim.Select(sim.Slimes[0].Id);
        return (sim, sim.Slimes[0]);
    }

    private static void RareEffectChecks(Action<bool, string> check)
    {
        // Turtle shell: stops one whole hit, then grows back after 12 s of the cat on the ground.
        var (sim, slime) = Target(new Dictionary<string, int> { ["shell"] = 1 }, damage: 10);
        double near = slime.X + (slime.X < 800 ? 40 : -40);
        int shellStops = 0;
        Run(sim, 3.5, () => near, () => shellStops += sim.Events.Count(e => e.Kind == BattleEventKind.ShellBlocked));
        bool first = shellStops == 1 && sim.CatHp < 1000 && sim.CatHp >= 970 && !sim.ShellReady;
        for (double t = 0; t < 2; t += dt) sim.Step(dt, near, true); // Held: the shell does not grow back.
        double heldLeft = sim.ShellCooldown;
        Run(sim, 13, () => near);
        check(first && sim.ShellBlocks >= 2 && heldLeft > 8, $"Turtle shell: the first hit is stopped whole, later ones hurt, it grows back after 12 s on the ground (not in the user's hands) ({sim.ShellBlocks} stopped)");

        // Opening strike: the first landed hit on each enemy deals ×2.
        var hits = new List<double>();
        (sim, slime) = Target(new Dictionary<string, int> { ["opener"] = 1 }, count: 2);
        var other = sim.Slimes[1];
        near = slime.X + (slime.X < 800 ? 50 : -50);
        double last = slime.Hp;
        Run(sim, 2.4, () => near, () => { if (slime.Hp != last) { hits.Add(last - slime.Hp); last = slime.Hp; } });
        check(hits.Count >= 2 && hits[0] == 50 && hits.Skip(1).All(h => h == 25) && sim.OpenerHits == 1, $"Opening strike: the first hit on an enemy deals ×2, the next ones as usual ({string.Join(", ", hits)})");
        check(!other.Opened, "…and every other enemy still has its opening hit to take");

        // Running start: a swing after a run of 140 DIPs deals +80 %; standing swings as usual.
        hits.Clear();
        (sim, slime) = Target(new Dictionary<string, int> { ["momentum"] = 1 });
        int side = slime.X < 800 ? 1 : -1;
        double cat = slime.X + side * 400;
        last = slime.Hp;
        for (double t = 0; t < 6; t += dt)
        {
            var order = sim.Step(dt, cat, false);
            if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -130 * dt, 130 * dt);
            if (slime.Hp != last) { hits.Add(last - slime.Hp); last = slime.Hp; }
            sim.ClearEvents();
        }
        check(hits.Count >= 2 && hits[0] == 45 && hits.Skip(1).All(h => h == 25) && sim.ChargedHits == 1, $"Running start: the swing after the run deals +80 %, swings standing still do not ({string.Join(", ", hits)})");
        hits.Clear();
        (sim, slime) = Target(new Dictionary<string, int> { ["momentum"] = 1 });
        near = slime.X + (slime.X < 800 ? 50 : -50);
        for (double t = 0; t < 1; t += dt) sim.Step(dt, near + (t * 600 % 300), true); // Carried about: not a run.
        last = slime.Hp;
        Run(sim, 1, () => near, () => { if (slime.Hp != last) { hits.Add(last - slime.Hp); last = slime.Hp; } });
        check(hits.Count >= 1 && hits[0] == 25, "Being carried is not a run (no charged swing after it)");
    }

    private static string Embedded()
    {
        using var stream = typeof(BattleData).Assembly.GetManifestResourceStream("DesktopPet.battle.json")!;
        return new StreamReader(stream).ReadToEnd();
    }

    private static void DataChecks(Action<bool, string> check)
    {
        string builtIn = Embedded();
        string Bad(string json)
        {
            try { BattleData.ParseWithOverride(builtIn, "{ \"schemaVersion\": 1, " + json + " }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        check(Bad("\"enemies\": [ { \"id\": \"king_c1\", \"elite\": { \"windupAdd\": 0.2, \"descVi\": \"a\", \"descEn\": \"a\" } } ]").Contains("not for a boss"), "An elite affix on a boss is rejected");
        check(Bad("\"enemies\": [ { \"id\": \"slime\", \"elite\": { \"sureBlocks\": 1, \"descVi\": \"a\", \"descEn\": \"a\" } } ]").Contains("blocks"), "Sure blocks on an enemy that never blocks are rejected");
        check(Bad("\"enemies\": [ { \"id\": \"slime\", \"elite\": { \"windupAdd\": 0.2, \"descVi\": \"a\", \"descEn\": \"a\" } } ]").Contains("dash"), "A dash affix on an enemy that does not dash is rejected");
        check(Bad("\"enemies\": [ { \"id\": \"slime\", \"elite\": { \"hp\": 1, \"attackCooldown\": 1, \"virusEvery\": 0, \"descVi\": \"a\", \"descEn\": \"a\" } } ]").Contains("changes nothing"), "An elite affix that changes nothing is rejected");
        check(Bad("\"enemies\": [ { \"id\": \"slime\", \"cost\": 11 } ]").Contains("cost"), "An enemy cost above 10 is rejected");
        check(Bad("\"maxElites\": 6").Contains("maxElites") && Bad("\"rareChance\": 1.5").Contains("rareChance"), "maxElites and rareChance out of range are rejected");
        check(Bad("\"upgrades\": [ { \"id\": \"odd\", \"group\": \"power\", \"rare\": true, \"nameVi\": \"a\", \"nameEn\": \"a\", \"descVi\": \"a\", \"descEn\": \"a\", \"effect\": \"damage\", \"amount\": 0.1, \"maxStacks\": 1 } ]").Contains("one rare card per group"),
            "A second rare card in a group is rejected");
        check(Bad("\"upgrades\": [ { \"id\": \"opening_strike\", \"maxStacks\": 2 } ]").Contains("maxStacks 1"), "A rare card that stacks is rejected");
        check(Bad("\"waves\": [ { \"budget\": 101, \"groups\": [ { \"enemy\": \"slime\", \"count\": 1 } ] } ]").Contains("budget"), "A wave budget above 100 is rejected");
        string Stream(string extra) => Bad("\"campaign\": { \"chapters\": [ { \"id\": \"c\", \"nameVi\": \"C\", \"nameEn\": \"C\", \"stages\": [ { \"id\": \"s\", \"nameVi\": \"S\", \"nameEn\": \"S\", \"goalVi\": \"g\", \"goalEn\": \"g\", " +
            "\"objective\": \"survive\", \"surviveSeconds\": 30, " + extra + " } ] } ] }");
        check(Stream("\"waves\": [ { \"stream\": { \"weights\": { \"rock\": 1 }, \"budget\": 1 } } ]").Contains("cheapest"), "A stream budget below the cheapest kind it draws is rejected (it would never spawn)");
        check(Stream("\"eliteChance\": 2, \"waves\": [ { \"stream\": { \"weights\": { \"slime\": 1 } } } ]").Contains("eliteChance"), "eliteChance above 1 is rejected");
        check(Stream("\"eliteChance\": 0.5, \"waves\": [ { \"stream\": { \"weights\": { \"slime\": 1, \"rock\": 1 }, \"budget\": 4 } } ]") == "", "A stage with an elite chance and a stream budget is accepted");
    }

    private static void ShippedChecks(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var runner = data.Enemy("runner").Elite;
        var rock = data.Enemy("rock").Elite;
        var mender = data.Enemy("mender").Elite;
        check(runner is { WindupAdd: 0.3, DashSpeed: 1.4 } && rock is { SureBlocks: 1 } && mender is { FirstHeal: 2 } && data.Enemy("slime").Elite != null && data.MaxElites == 1,
            "Shipped elites: quick slime +0.3 s windup / ×1.4 dash, rock one sure block, herb slime first heal ×2 (traits kept by the 1.2.0-beta buff, which also gave the slime one); at most one alive");
        var rares = data.UpgradeList.Where(u => u.Rare).ToList();
        check(rares.Select(u => (u.Id, Group: u.Group ?? "", u.Effect)).OrderBy(r => r.Group).SequenceEqual(new[] { ("turtle_shell", "defense", "shell"), ("opening_strike", "power", "opener"), ("running_start", "speed", "momentum") }.OrderBy(r => r.Item2))
            && data.RareChance == 0.15, "Shipped rare cards: Turtle shell, Opening strike, Running start (one per group, 15 %)");
        var stages = data.Campaign!.Chapters[0].Stages;
        check(stages.Take(2).All(s => s.EliteChance == 0) && stages.Skip(2).All(s => s.EliteChance > 0), "Elites start at stage 3");
        // The code's own defaults match the shipped file (used when the file is broken and by tests).
        var defaults = new BattleSettings();
        check(defaults.UpgradeList.Where(u => u.Rare).Select(u => (u.Id, u.Effect, u.Amount)).SequenceEqual(rares.Select(u => (u.Id, u.Effect, u.Amount)))
            && defaults.Easy.EnemyHp == data.Easy.EnemyHp && defaults.Easy.EnemyDamage == data.Easy.EnemyDamage, "The built-in defaults match battle.json (rare cards, Easy)");
    }
}
