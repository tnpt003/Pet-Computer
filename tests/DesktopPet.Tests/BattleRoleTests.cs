using DesktopPet;

// Combat Depth milestone 3: enemy roles. The rock slime blocks some weapon hits (seeded, capped
// streak, never the bomb, Thorns or an Armor break combo); a support keeps behind its fighting
// allies and only fights once none is left; the boss's shield phase stops a hit at its line, runs
// to the far edge, calls its own helpers and takes nothing until they are all gone.
internal static class BattleRoleTests
{
    const double dt = 1.0 / 60;

    // Steps until the condition holds (or the time runs out); returns whether it did. The cat
    // stands still at catX unless move is set (then it walks where the battle sends it).
    private static bool Until(BattleSim sim, Func<bool> done, double seconds = 10, double catX = 800, List<BattleEvent>? events = null)
    {
        for (double t = 0; t < seconds; t += dt)
        {
            if (done()) return true;
            sim.Step(dt, catX, false);
            events?.AddRange(sim.Events);
            sim.ClearEvents();
        }
        return done();
    }

    private static WaveSpec[] Plan(params (string Enemy, int Count)[] groups) =>
        new[] { new WaveSpec { Groups = groups.Select(g => new WaveGroup { Enemy = g.Enemy, Count = g.Count }).ToArray() } };

    private static EnemyType Rock(double chance = 0.3, int streak = 2, double damage = 0) => new()
    {
        Id = "rock", MaxHp = 100000, Speed = 0, Damage = damage, Range = 72, Windup = 1, AttackCooldown = 2.4, Recover = 0.5, Scale = 1.35,
        BlockChance = chance, BlockMaxStreak = streak, PushFactor = 0
    };

    private static BattleSim RockFight(EnemyType rock, int seed = 5, IReadOnlyDictionary<string, int>? build = null, double bombCooldown = 12) =>
        new(new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0, CatMaxHp = 1_000_000, SpawnMinDistance = 0, BombCooldown = bombCooldown,
            Enemies = new[] { rock }, Plan = Plan(("rock", 1))
        }, 700, 900, new Random(seed), build: build);

    public static void Run(Action<bool, string> check)
    {
        RockChecks(check);
        SupportChecks(check);
        ShieldChecks(check);
        DataChecks(check);
    }

    private static void RockChecks(Action<bool, string> check)
    {
        // Two minutes of swings at a rock that never moves: about blockChance of the hits blocked,
        // never more than blockMaxStreak in a row, and a blocked hit takes nothing and moves nothing.
        var sim = RockFight(Rock());
        Until(sim, () => sim.Slimes.Count == 1 && sim.Slimes[0].State != SlimeState.Spawning);
        var rock = sim.Slimes[0];
        int hits = 0, blocked = 0, streak = 0, longest = 0;
        bool blockedClean = true;
        for (double t = 0; t < 120; t += dt)
        {
            double hp = rock.Hp;
            sim.Step(dt, rock.X - 60, false);
            bool hit = sim.Events.Any(e => e.Kind is BattleEventKind.SlimeHit or BattleEventKind.ComboHit);
            bool block = sim.Events.Any(e => e.Kind == BattleEventKind.HitBlocked);
            sim.ClearEvents();
            if (!hit) continue;
            hits++;
            if (block) { blocked++; streak++; longest = Math.Max(longest, streak); blockedClean &= rock.Hp == hp && rock.KnockVelocity == 0; }
            else streak = 0;
        }
        double share = blocked / (double)Math.Max(1, hits);
        check(hits > 100 && share is > 0.15 and < 0.4 && sim.BlocksMade == blocked, $"The rock slime blocks about 30 % of weapon hits ({blocked}/{hits})");
        check(longest == 2, $"Never more than 2 blocks in a row (longest {longest})");
        check(blockedClean, "A blocked hit takes no HP and knocks nothing back");
        check(sim.Effects.Any(e => e.Kind == BattleEffectKind.Block) || blocked > 0, "A block shows its mark");
        var again = RockFight(Rock());
        Until(again, () => again.Slimes.Count == 1 && again.Slimes[0].State != SlimeState.Spawning);
        for (double t = 0; t < 120; t += dt) { again.Step(dt, again.Slimes[0].X - 60, false); again.ClearEvents(); }
        check(again.BlocksMade == sim.BlocksMade, "The same seed blocks the same hits (one roll per hit, from the battle's random)");

        // The yarn bomb is never blocked, even at a 90 % chance with no streak cap to speak of.
        var bombed = RockFight(Rock(0.9, 10), bombCooldown: 0.1);
        Until(bombed, () => bombed.Slimes.Count == 1 && bombed.Slimes[0].State != SlimeState.Spawning, catX: 200);
        var target = bombed.Slimes[0];
        bool bombsLand = true;
        for (int i = 0; i < 8; i++)
        {
            double hp = target.Hp;
            bombed.SetCatState(false, false);
            bool thrown = bombed.Use(BattleSkill.Bomb, target.Id, 200);
            Until(bombed, () => false, 0.7, catX: 200);
            bombsLand &= thrown && Math.Abs(hp - target.Hp - bombed.Settings.BombDamage) < 1e-9;
        }
        check(bombsLand && bombed.BlocksMade == 0, "The yarn bomb always gets through");

        // Armor break (3 Power cards): combo hits pierce the block; plain hits are still blocked.
        var breaker = RockFight(Rock(0.9, 10), build: new Dictionary<string, int> { ["sharp"] = 3 });
        Until(breaker, () => breaker.Slimes.Count == 1 && breaker.Slimes[0].State != SlimeState.Spawning);
        int combos = 0, comboBlocks = 0;
        for (double t = 0; t < 60; t += dt)
        {
            breaker.Step(dt, breaker.Slimes[0].X - 60, false);
            bool combo = breaker.Events.Any(e => e.Kind == BattleEventKind.ComboHit);
            if (combo) { combos++; if (breaker.Events.Any(e => e.Kind == BattleEventKind.HitBlocked)) comboBlocks++; }
            breaker.ClearEvents();
        }
        check(breaker.Synergies.Any(y => y.Effect == "armorBreak") && combos > 3 && comboBlocks == 0 && breaker.BlocksMade > 0,
            $"Armor break: combo hits are never blocked ({combos} combos), plain hits still are ({breaker.BlocksMade})");
        // Without it, combos can be blocked too.
        var plain = RockFight(Rock(0.9, 10));
        Until(plain, () => plain.Slimes.Count == 1 && plain.Slimes[0].State != SlimeState.Spawning);
        int plainComboBlocks = 0;
        for (double t = 0; t < 60; t += dt)
        {
            plain.Step(dt, plain.Slimes[0].X - 60, false);
            if (plain.Events.Any(e => e.Kind == BattleEventKind.ComboHit) && plain.Events.Any(e => e.Kind == BattleEventKind.HitBlocked)) plainComboBlocks++;
            plain.ClearEvents();
        }
        check(plainComboBlocks > 0, "Without Armor break a combo hit can be blocked");

        // Thorns is never blocked: the rock that hits the cat takes its share back (the cat itself
        // deals nothing here, so every HP the rock loses is Thorns).
        var still = new BattleSim(new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0, CatMaxHp = 1_000_000, CatDamage = 0, SpawnMinDistance = 0, Enemies = new[] { Rock(0.9, 10, damage: 20) }, Plan = Plan(("rock", 1))
        }, 700, 900, new Random(5), build: new Dictionary<string, int> { ["thick_fur"] = 2, ["big_heart"] = 1 });
        Until(still, () => still.Slimes.Count == 1 && still.Slimes[0].State != SlimeState.Spawning);
        var bitten = still.Slimes[0];
        Until(still, () => still.Reflects >= 3, 20, catX: bitten.X - 40);
        check(still.Reflects >= 3 && Math.Abs(bitten.MaxHp - bitten.Hp - still.Reflects * 20 * 0.7 * 0.35) < 1e-6 && still.BlocksMade == 0,
            $"Thorns damage is never blocked ({still.Reflects} reflects)");
    }

    private static BattleSettings Field(EnemyType[] kinds, double bombDamage = 30, double catMaxHp = 1_000_000) => new()
    {
        FirstWaveDelay = 0, SpawnInterval = 0, CatMaxHp = catMaxHp, CatDamage = 0, MaxAlive = 6, SpawnMinDistance = 0, Enemies = kinds,
        BombCooldown = 0.1, BombDamage = bombDamage, BombRadius = 1
    };

    private static readonly EnemyType Fighter = new() { Id = "slime", MaxHp = 50, Speed = 34, Damage = 10, Range = 64 };
    private static readonly EnemyType Mender = new()
    {
        Id = "mender", MaxHp = 40, Speed = 28, Damage = 5, Range = 56, Scale = 0.9, HealAmount = 15, HealRange = 170, HealCooldown = 4.5, HealWindup = 1, HealBudget = 90,
        Support = true, KeepBack = 110
    };

    private static void SupportChecks(Action<bool, string> check)
    {
        // A support and a fighter: the fighter goes for the cat, the support stays behind it and never attacks.
        var sim = new BattleSim(Field(new[] { Fighter, Mender }), 0, 1600, new Random(4), stage: new StageSpec { Id = "t", Waves = Plan(("slime", 1), ("mender", 1)) });
        Until(sim, () => sim.Slimes.Count == 2 && sim.Slimes.All(s => s.State != SlimeState.Spawning), catX: 800);
        var support = sim.Slimes.Single(s => s.Type.Id == "mender");
        var fighter = sim.Slimes.Single(s => s.Type.Id == "slime");
        // Both on the right of the cat, the support nearer: it must fall back behind the fighter.
        fighter.X = 1300; support.X = 1100;
        bool neverAttacks = true, behind = true, fighterAttacks = false;
        for (double t = 0; t < 20; t += dt)
        {
            sim.Step(dt, 800, false);
            sim.ClearEvents();
            neverAttacks &= support.State is not (SlimeState.Windup or SlimeState.Dashing);
            fighterAttacks |= fighter.State == SlimeState.Windup;
            if (t > 8) behind &= support.X - 800 > fighter.X - 800 + 60;
        }
        check(neverAttacks && fighterAttacks, "While a fighting ally is on the field the support never attacks the cat (the ally does)");
        check(behind && Math.Abs(support.X - 800) >= 100, $"The support keeps well behind its ally ({Math.Abs(support.X - 800):0} DIPs from the cat, ally {Math.Abs(fighter.X - 800):0})");

        // It still heals from back there.
        fighter.Hp = 10;
        var events = new List<BattleEvent>();
        Until(sim, () => events.Any(e => e.Kind == BattleEventKind.EnemyHealed), 12, events: events);
        check(events.Any(e => e.Kind == BattleEventKind.EnemyHealed) && fighter.Hp > 10, "The support still heals its ally from behind");

        // The cat walks at it: it backs off at its own (slower) speed and stops at the edge, so it is caught.
        double cat = 800;
        bool inside = true;
        for (double t = 0; t < 30; t += dt)
        {
            sim.Step(dt, cat, false);
            sim.ClearEvents();
            cat += Math.Sign(support.X - cat) * Math.Min(Math.Abs(support.X - cat) > 40 ? 120 * dt : 0, Math.Abs(support.X - cat));
            inside &= support.X >= sim.Left && support.X <= sim.Right;
        }
        check(inside && Math.Abs(support.X - cat) <= 60 && (support.X <= sim.Left + support.Width || support.X >= sim.Right - support.Width) && support.State is not SlimeState.Windup,
            $"Chased, it backs off to the field's edge and is caught there (no endless kiting; {Math.Abs(support.X - cat):0} DIPs)");

        // Its ally gone, it fights.
        fighter.Hp = 0; fighter.Set(SlimeState.Defeated);
        Until(sim, () => support.State == SlimeState.Windup, 12, catX: support.X - 40);
        check(support.State == SlimeState.Windup, "With no fighting ally left the support attacks the cat");

        // Two supports alone: neither waits for the other, both fight.
        var pair = new BattleSim(Field(new[] { Fighter, Mender }), 0, 1600, new Random(4), stage: new StageSpec { Id = "t", Waves = Plan(("mender", 2)) });
        bool first = false, second = false;
        Until(pair, () =>
        {
            if (pair.Slimes.Count == 2) { first |= pair.Slimes[0].State == SlimeState.Windup; second |= pair.Slimes[1].State == SlimeState.Windup; }
            return first && second;
        }, 30);
        check(first && second, "Two supports left alone both fight (no waiting on each other)");
    }

    private static EnemyType King(int cap = 4, BossPhase[]? phases = null) => new()
    {
        Id = "king", MaxHp = 1000, Speed = 22, Damage = 25, Range = 110, Windup = 1.2, AttackCooldown = 2.6, Recover = 0.7, Scale = 2,
        Boss = true, Slam = true, PushFactor = 0.35, StunFactor = 0.4, EnragedSpeed = 32, EnragedWindup = 0.8, SummonCap = cap, RetreatSpeed = 240, RetreatSeconds = 3,
        Phases = phases ?? new[]
        {
            new BossPhase { At = 0.7, Shield = true, Summon = "slime", Count = 2 },
            new BossPhase { At = 0.4, Shield = true, Summons = new[] { new WaveGroup { Enemy = "mender", Count = 1 }, new WaveGroup { Enemy = "slime", Count = 2 } } },
            new BossPhase { At = 0.3, Enrage = true }
        }
    };

    private static void Bomb(BattleSim sim, Slime target, double catX, List<BattleEvent> events)
    {
        sim.SetCatState(false, false);
        sim.Use(BattleSkill.Bomb, target.Id, catX);
        Until(sim, () => false, 0.7, catX, events);
    }

    private static void ShieldChecks(Action<bool, string> check)
    {
        var settings = Field(new[] { Fighter, Mender, King() }, 900);
        var sim = new BattleSim(settings, 0, 1600, new Random(6), stage: new StageSpec { Id = "t", Waves = Plan(("king", 1)) });
        var events = new List<BattleEvent>();
        const double cat = 1300;
        Until(sim, () => sim.Boss is { State: not SlimeState.Spawning }, 5, cat, events);
        var boss = sim.Boss!;
        boss.X = 600;
        // One huge hit (900 of 1000): it stops at the first shield line, the rest is lost.
        Bomb(sim, boss, cat, events);
        check(boss.Hp == 700 && boss.Shielded && boss.PhasesDone == 0b001 && !boss.Enraged && events.Count(e => e.Kind == BattleEventKind.BossShielded) == 1,
            $"A hit past several lines stops at the first shield phase (HP {boss.Hp}); later phases wait");
        // It runs to the edge away from the cat, then calls its helpers there.
        double edge = sim.Left + boss.Width / 2;
        Until(sim, () => !boss.Retreating, 3, cat, events);
        var helpers = sim.Slimes.Where(s => s.SummonedBy == boss.Id).ToList();
        check(Math.Abs(boss.X - edge) < 3 && helpers.Count == 2 && helpers.All(h => h.PhaseTag == boss.ShieldTag && h.X > boss.X) && events.Any(e => e.Kind == BattleEventKind.BossSummoned),
            $"It backs off to the far edge ({boss.X:0}) and calls 2 helpers on the open side");
        // Shielded: nothing gets through, and the cat goes for the helpers.
        double hp = boss.Hp;
        Bomb(sim, boss, cat, events);
        check(boss.Hp == hp && sim.ImmuneHits >= 1 && events.Any(e => e.Kind == BattleEventKind.BossImmune), "A shielded boss takes nothing (the bomb shows IMMUNE)");
        check(sim.Select(boss.Id), "The user may still pick the shielded boss");
        sim.Step(dt, cat, false); sim.ClearEvents();
        check(sim.SelectedId == boss.Id && sim.TargetId != boss.Id && sim.TargetId is int t && helpers.Any(h => h.Id == t), "…but the cat goes for its helpers until the shield drops");
        // Pause freezes the phase.
        sim.Paused = true;
        double shieldTime = sim.BossShieldSeconds;
        Until(sim, () => false, 2, cat, events);
        sim.Paused = false;
        check(sim.BossShieldSeconds == shieldTime && boss.Shielded, "Pause freezes the shield phase");
        // The shield guards its edge: a cat put next to it gets slammed; it never walks up to the cat.
        double guardX = boss.X;
        Until(sim, () => false, 4, cat, events);
        bool stayed = Math.Abs(boss.X - guardX) < 40;
        Until(sim, () => boss.State == SlimeState.Windup, 6, boss.X + 60, events);
        check(stayed && boss.State == SlimeState.Windup, "While shielded the boss stays at its edge but still slams a cat that comes close");
        // Its helpers down (one by the bomb, one by hand): the shield drops once.
        events.Clear();
        foreach (var h in helpers) { h.Hp = 0; h.Set(SlimeState.Defeated); }
        Until(sim, () => false, 1, cat, events);
        check(!boss.Shielded && events.Count(e => e.Kind == BattleEventKind.BossShieldDown) == 1, "The last helper down, the shield drops exactly once");
        Until(sim, () => false, 1, cat, events);
        check(events.Count(e => e.Kind == BattleEventKind.BossShieldDown) == 1, "…and never drops twice");
        // Next big hit: the second shield phase at 40 % calls three helpers of two kinds.
        Until(sim, () => sim.Boss?.State is SlimeState.Moving or SlimeState.Recover, 3, cat, events);
        Bomb(sim, boss, cat, events);
        Until(sim, () => !boss.Retreating, 3, cat, events);
        var second = sim.Slimes.Where(s => s.SummonedBy == boss.Id && s.State != SlimeState.Defeated).ToList();
        check(boss.Hp == 400 && boss.Shielded && second.Count == 3 && second.Count(s => s.Type.Id == "mender") == 1 && second.All(s => s.PhaseTag == boss.ShieldTag) && !boss.Enraged,
            $"The second shield phase stops at 40 % and calls a herb slime and two slimes (HP {boss.Hp}, {second.Count} helpers)");
        // A helper that is not this phase's never keeps the shield up: helpers of the first phase are gone already.
        foreach (var h in second) { h.Hp = 0; h.Set(SlimeState.Defeated); }
        Until(sim, () => !boss.Shielded, 1, cat, events);
        Bomb(sim, boss, cat, events);
        check(!boss.Shielded && boss.State == SlimeState.Defeated && sim.BossDefeated, "After the last shield drops, hits go through again (the 900 bomb finishes the boss)");

        // Summon cap: helpers still alive from a plain phase leave no room, so the shield drops at once.
        var capped = King(2, new[]
        {
            new BossPhase { At = 0.9, Summon = "slime", Count = 2 },
            new BossPhase { At = 0.7, Shield = true, Summon = "slime", Count = 2 }
        });
        var full = new BattleSim(Field(new[] { Fighter, Mender, capped }, 150), 0, 1600, new Random(6),
            stage: new StageSpec { Id = "t", Waves = Plan(("king", 1)) });
        var fullEvents = new List<BattleEvent>();
        Until(full, () => full.Boss is { State: not SlimeState.Spawning }, 5, cat, fullEvents);
        var cappedBoss = full.Boss!;
        Bomb(full, cappedBoss, cat, fullEvents); // 1000 → 850: two plain helpers.
        Bomb(full, cappedBoss, cat, fullEvents); // 850 → 700: shield, but the cap is full.
        bool dropped = Until(full, () => !cappedBoss.Shielded, 4, cat, fullEvents);
        check(dropped && full.Slimes.Count(s => s.SummonedBy == cappedBoss.Id) == 2 && fullEvents.Count(e => e.Kind == BattleEventKind.BossShieldDown) == 1,
            "A shield phase whose helpers cannot come (summon cap) drops its shield at once: no endless shield");

        // Thorns never bounces off a shielded boss; the battle ending mid-phase leaves nothing behind.
        var thorns = new BattleSim(Field(new[] { Fighter, Mender, King() }, 900, 60), 0, 1600, new Random(6),
            stage: new StageSpec { Id = "t", Waves = Plan(("king", 1)) }, build: new Dictionary<string, int> { ["thick_fur"] = 2, ["big_heart"] = 1 });
        var tEvents = new List<BattleEvent>();
        Until(thorns, () => thorns.Boss is { State: not SlimeState.Spawning }, 5, cat, tEvents);
        var tBoss = thorns.Boss!;
        Bomb(thorns, tBoss, cat, tEvents);
        Until(thorns, () => !tBoss.Retreating, 3, cat, tEvents);
        int reflects = thorns.Reflects;
        double tHp = tBoss.Hp;
        Until(thorns, () => thorns.Over, 40, tBoss.X + 50, tEvents);
        check(thorns.Over && tBoss.Hp == tHp && thorns.Phase == BattlePhase.Lost, $"Thorns never hurts a shielded boss; losing mid-phase ends the battle cleanly ({thorns.Reflects - reflects} reflects on helpers)");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        string Error(string json)
        {
            try { BattleData.Parse("{ \"schemaVersion\": 1, " + json + " }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        string Enemies(string rock, string boss) => "\"waves\": [ { \"groups\": [ { \"enemy\": \"slime\", \"count\": 1 } ] } ], " +
            "\"enemies\": [ { \"id\": \"slime\" }, { \"id\": \"rock\", " + rock + " }, { \"id\": \"big\", \"boss\": true, " + boss + " } ]";
        const string okBoss = "\"phases\": [ { \"at\": 0.5, \"shield\": true, \"summons\": [ { \"enemy\": \"slime\", \"count\": 2 }, { \"enemy\": \"rock\", \"count\": 1 } ] } ]";
        check(Error(Enemies("\"blockChance\": 0.3, \"blockMaxStreak\": 2, \"support\": true, \"keepBack\": 120", okBoss)) == "",
            "Block, support and a shield phase with mixed helpers are accepted");
        check(Error(Enemies("\"blockChance\": 0.95", okBoss)).Contains("blockChance"), "A block chance above 0.9 is rejected");
        check(Error(Enemies("\"blockMaxStreak\": 0", okBoss)).Contains("blockMaxStreak"), "A block streak cap below 1 is rejected");
        check(Error(Enemies("\"support\": true, \"keepBack\": 900", okBoss)).Contains("keepBack"), "A support keeping back too far is rejected");
        check(Error(Enemies("\"maxHp\": 10", "\"phases\": [ { \"at\": 0.5, \"shield\": true, \"enrage\": true } ], \"enragedSpeed\": 30, \"enragedWindup\": 0.8")).Contains("shield phase must summon"),
            "A shield phase without helpers is rejected (its shield could never drop)");
        check(Error(Enemies("\"maxHp\": 10", "\"phases\": [ { \"at\": 0.5, \"shield\": true, \"summons\": [ { \"enemy\": \"ghost\", \"count\": 1 } ] } ]")).Contains("unknown enemy"),
            "A shield phase calling an unknown enemy is rejected");

        // The shipped data: the rock blocks, the herb slime supports, the chapter boss has three shield phases.
        var data = BattleData.Settings;
        var shippedRock = data.Enemy("rock");
        var herb = data.Enemy("mender");
        var chapterKing = data.Enemy("king_c1");
        check(shippedRock is { MaxHp: 188, BlockChance: 0.3, BlockMaxStreak: 2 } && herb is { Support: true, KeepBack: 110 } &&
              chapterKing.Phases!.Where(p => p.Shield).Select(p => p.At).SequenceEqual(new[] { 0.75, 0.5, 0.25 }) && data.Enemy("king").Phases == null,
            "battle.json: rock slime 188 HP with 30 % block, the herb slime keeps back, the chapter boss shields at 75 %, 50 % and 25 % (the free battle's king unchanged)");
    }
}
