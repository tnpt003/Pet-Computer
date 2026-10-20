using DesktopPet;

// Enemy Director milestone 2: enemy roles and tactics. Rocks block the cat's way (and a cat put
// down inside one steps off it), block only from the front and turn slowly; attacks take turns;
// a rock guards its support; a support jumps to a badly hurt ally; waves can come from both sides
// with a warning; the boss rallies a helper during its shield and is dazed when the shield breaks.
internal static class EnemyTacticsTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id, double hp = 50, double speed = 0, double damage = 0, double range = 64) => new()
    {
        Id = id, MaxHp = hp, Speed = speed, Damage = damage, Range = range, Windup = 0.8, AttackCooldown = 1.8, Recover = 0.35
    };

    private static BattleSettings Settings(EnemyType[] kinds, WaveSpec[]? plan = null, double catDamage = 25, int maxAlive = 6, double bomb = 30) => new()
    {
        FirstWaveDelay = 0, SpawnInterval = 0.2, CatMaxHp = 1_000_000, MaxAlive = maxAlive, Enemies = kinds, Plan = plan, CatDamage = catDamage,
        BombDamage = bomb, BombCooldown = 0.1, BombDelay = 0.01
    };

    private static WaveSpec[] Plan(params WaveGroup[] groups) => new[] { new WaveSpec { Groups = groups } };
    private static WaveGroup G(string enemy, int count = 1, string? side = null) => new() { Enemy = enemy, Count = count, Side = side };

    // Steps with a cat that walks where the battle sends it (130 DIPs/s); returns the cat's place.
    private static double Walk(BattleSim sim, double cat, double seconds, Action<double>? each = null, bool move = true)
    {
        for (double t = 0; t < seconds && !sim.Over; t += dt)
        {
            var order = sim.Step(dt, cat, false);
            if (move && order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -130 * dt, 130 * dt);
            each?.Invoke(cat);
            sim.ClearEvents();
        }
        return cat;
    }

    private static void Ready(BattleSim sim, int count, double cat = 800)
    {
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < count; i++) sim.Step(dt, cat, false);
        sim.ClearEvents();
    }

    public static void Run(Action<bool, string> check)
    {
        BlockerChecks(check);
        FrontChecks(check);
        TurnChecks(check);
        GuardChecks(check);
        TeleportChecks(check);
        SideChecks(check);
        RallyChecks(check);
        DataChecks(check);
    }

    private static EnemyType RockKind(double hp = 200, double block = 0, double turn = 0, double speed = 0) => new()
    {
        Id = "rock", MaxHp = hp, Speed = speed, Damage = 0, Range = 72, Windup = 1, AttackCooldown = 2.4, Recover = 0.5, Scale = 1.35, HitScale = 1.5,
        Blocker = true, BlockFrontOnly = true, BlockChance = block, BlockMaxStreak = 10, TurnSeconds = turn, PushFactor = 0
    };

    private static void BlockerChecks(Action<bool, string> check)
    {
        // A rock between the cat and the slime it was told to hit: the rock first, never walked through.
        var sim = new BattleSim(Settings(new[] { RockKind(), Kind("slime") }, Plan(G("rock"), G("slime"))), 0, 1600, seed: 2);
        Ready(sim, 2);
        var rock = sim.Slimes.Single(s => s.Type.Id == "rock");
        var slime = sim.Slimes.Single(s => s.Type.Id == "slime");
        rock.X = 800; slime.X = 1000;
        sim.Select(slime.Id);
        bool held = true, marked = true, flagged = true;
        double cat = Walk(sim, 500, 30, c =>
        {
            if (rock.State == SlimeState.Defeated) return;
            held &= c < rock.X - rock.HitWidth / 2;
            marked &= sim.SelectedId == slime.Id;
            flagged &= sim.SwingTime >= 0 || sim.BlockedById == rock.Id || sim.CatCooldown > 0;
        });
        check(rock.State == SlimeState.Defeated || sim.Slimes.All(s => s != rock), "The cat breaks the rock in its way (it was hit, not passed)");
        check(held, "The cat never walks through a rock on the floor");
        check(marked && flagged, "The chosen slime keeps its marker while the rock blocks; the rock is flagged as the way blocker");
        check(slime.Hp < slime.MaxHp || slime.State == SlimeState.Defeated, "With the rock gone the cat reaches its chosen slime");

        // Lifted over the rock: from behind it, the way is clear.
        var over = new BattleSim(Settings(new[] { RockKind(), Kind("slime") }, Plan(G("rock"), G("slime"))), 0, 1600, seed: 2);
        Ready(over, 2);
        var r2 = over.Slimes.Single(s => s.Type.Id == "rock");
        var s2 = over.Slimes.Single(s => s.Type.Id == "slime");
        r2.X = 800; s2.X = 1000;
        over.Select(s2.Id);
        over.Step(dt, 900, false);
        check(over.BlockedById == 0 && over.TargetId == s2.Id, "Put down behind the rock, the cat goes straight for its target");

        // Put down inside a rock: a short walk to the nearest free side, never far.
        var inside = new BattleSim(Settings(new[] { RockKind() }, Plan(G("rock"))), 0, 1600, seed: 2);
        Ready(inside, 1);
        inside.Slimes[0].X = 800;
        var order = inside.Step(dt, 806, false);
        double edge = inside.Slimes[0].HitWidth / 2 + BattleSim.CatHalfWidth;
        check(order.Command == CatCommand.Move && order.X > 800 + edge && order.X < 800 + edge + 10, $"A cat inside a rock steps off it to its own side ({order.X - 800:0} DIPs from the centre)");
        // Two rocks side by side: it still ends free, close by.
        var pair = new BattleSim(Settings(new[] { RockKind() }, Plan(G("rock", 2))), 0, 1600, seed: 2);
        Ready(pair, 2);
        pair.Slimes[0].X = 800; pair.Slimes[1].X = 860;
        pair.Slimes[0].Set(SlimeState.Stunned); pair.Slimes[1].Set(SlimeState.Stunned);
        pair.Slimes[0].StunRemaining = pair.Slimes[1].StunRemaining = 100;
        double end = Walk(pair, 830, 3);
        bool free = pair.Slimes.All(s => Math.Abs(s.X - end) >= s.HitWidth / 2 + BattleSim.CatHalfWidth - 4);
        check(free && Math.Abs(end - 830) < 160, $"Between two rocks the cat walks clear of both, close by (ended {end - 830:0} DIPs away)");
    }

    private static void FrontChecks(Action<bool, string> check)
    {
        // A rock that never turns (turnSeconds 3, nearly always blocks from the front): hits from behind land.
        int Blocks(bool behind)
        {
            var sim = new BattleSim(Settings(new[] { RockKind(100000, 0.9, 3) }, Plan(G("rock"))), 0, 1600, seed: 4);
            Ready(sim, 1);
            var rock = sim.Slimes[0];
            rock.X = 800; rock.Facing = -1; // Faces left.
            int before = sim.BlocksMade;
            Walk(sim, behind ? 860 : 740, 2.5, move: false);
            return sim.BlocksMade - before;
        }
        int back = Blocks(true), front = Blocks(false);
        check(back == 0 && front > 0, $"A front-only rock blocks hits from the side it faces, never from behind (front {front}, behind {back})");
    }

    private static void TurnChecks(Action<bool, string> check)
    {
        // A cat behind a slow-turning rock: no windup until it has turned, then it does.
        var sim = new BattleSim(Settings(new[] { RockKind(100000, 0, 0.6) }, Plan(G("rock")), 0), 0, 1600, seed: 4);
        Ready(sim, 1);
        var rock = sim.Slimes[0];
        rock.X = 800; rock.Facing = -1;
        double firstWindup = -1, start = sim.Time;
        Walk(sim, 850, 3, _ => { if (firstWindup < 0 && rock.State == SlimeState.Windup) firstWindup = sim.Time - start; }, move: false);
        check(firstWindup >= 0.6 - 2 * dt && firstWindup < 1.2 && rock.Facing == 1, $"A rock needs its turn (0.6 s) before it winds up at a cat behind it ({firstWindup:0.00} s)");

        // Attack turns: four slimes in reach, never more than two winding up at once, starts 0.3 s apart.
        var crowd = new BattleSim(Settings(new[] { Kind("slime", 1000, 0, 0, 200) }, Plan(G("slime", 4)), 0), 0, 1600, seed: 6);
        Ready(crowd, 4);
        double[] spots = { 700, 760, 840, 900 };
        for (int i = 0; i < 4; i++) crowd.Slimes[i].X = spots[i];
        int most = 0;
        double lastStart = double.NegativeInfinity, closest = double.MaxValue;
        var attacked = new HashSet<int>();
        var was = crowd.Slimes.ToDictionary(s => s.Id, s => s.State);
        Walk(crowd, 800, 12, _ =>
        {
            most = Math.Max(most, crowd.Slimes.Count(s => s.State is SlimeState.Windup or SlimeState.Dashing));
            foreach (var s in crowd.Slimes)
            {
                if (s.State == SlimeState.Windup && was[s.Id] != SlimeState.Windup)
                {
                    closest = Math.Min(closest, crowd.Time - lastStart);
                    lastStart = crowd.Time;
                    attacked.Add(s.Id);
                }
                was[s.Id] = s.State;
            }
        }, move: false);
        check(most <= 2 && closest >= 0.3 - 1.5 * dt, $"Attacks take turns: at most 2 winding up at once ({most}), new windups ≥ 0.3 s apart ({closest:0.00} s)");
        check(attacked.Count == 4, "Every slime gets its turn (nobody waits for ever)");
    }

    private static void GuardChecks(Action<bool, string> check)
    {
        var mender = new EnemyType { Id = "mender", MaxHp = 40, Speed = 28, Damage = 0, Range = 56, Windup = 0.8, AttackCooldown = 2, Recover = 0.35, Scale = 0.9, Support = true, KeepBack = 110 };
        var settings = Settings(new[] { RockKind(100000, 0, 0, 40), mender }, Plan(G("rock"), G("mender")), 0);
        var sim = new BattleSim(settings, 0, 1600, seed: 3);
        Ready(sim, 2);
        var rock = sim.Slimes.Single(s => s.Type.Id == "rock");
        var support = sim.Slimes.Single(s => s.Type.Id == "mender");
        rock.X = 700; support.X = 800;
        bool Between(double cat) => (rock.X - cat) * (support.X - cat) > 0 && Math.Abs(rock.X - cat) < Math.Abs(support.X - cat);
        Walk(sim, 300, 6, move: false);
        check(rock.GuardOf == support.Id && Between(300), "A rock guards the support: it stands between the cat and it");
        // The cat thrown to the other side: after a short delay the guard takes the other side.
        bool sameFrame = false;
        int frames = 0;
        Walk(sim, 1400, 15, c => { if (frames++ == 0) sameFrame = Between(c); }, move: false);
        check(!sameFrame && Between(1400), "When the cat changes sides the guard follows (after a reaction delay, not in the same frame)");
        // The guard gone: its role is cleared and nothing waits on it.
        rock.Hp = 0; rock.Set(SlimeState.Defeated);
        Walk(sim, 1400, 1, move: false);
        check(sim.Slimes.All(s => s.GuardOf == 0 || s.State != SlimeState.Defeated), "A defeated guard guards nothing");
    }

    private static EnemyType Healer(string id = "mender", double heal = 10) => new()
    {
        Id = id, MaxHp = 40, Speed = 0, Damage = 0, Range = 56, Windup = 0.8, AttackCooldown = 2, Recover = 0.35, Scale = 0.9, Support = true, KeepBack = 110,
        HealAmount = heal, HealRange = 170, HealCooldown = 200, HealWindup = 1, HealBudget = 90, TeleportBelow = 0.35, TeleportWindup = 0.6, TeleportCooldown = 8
    };

    private static (BattleSim Sim, Slime Ally, Slime Support) TeleportFight(int seed = 1)
    {
        var sim = new BattleSim(Settings(new[] { Kind("slime", 100), Healer() }, Plan(G("slime"), G("mender")), 0), 0, 1600, seed: seed);
        Ready(sim, 2, 200);
        var ally = sim.Slimes.Single(s => s.Type.Id == "slime");
        var support = sim.Slimes.Single(s => s.Type.Id == "mender");
        ally.X = 600; support.X = 1200;
        return (sim, ally, support);
    }

    private static void TeleportChecks(Action<bool, string> check)
    {
        var (sim, ally, support) = TeleportFight();
        Walk(sim, 200, 0.2, move: false);
        check(support.State != SlimeState.Teleporting, "A healthy ally gets no teleport");
        ally.Hp = 30;
        Walk(sim, 200, dt, move: false);
        double spot = support.TeleportX;
        check(support.State == SlimeState.Teleporting && spot > ally.X && Math.Abs(spot - ally.X) < 60,
            $"An ally below 35 % out of heal reach: the support winds up a teleport to the spot behind it ({spot:0} for an ally at {ally.X:0})");
        Walk(sim, 200, 0.7, move: false);
        check(support.X == spot && ally.Hp == 30, "It appears there after its windup; the jump heals nothing by itself");
        support.X = 1300;
        bool again = false;
        Walk(sim, 200, 7, _ => again |= support.State == SlimeState.Teleporting, move: false);
        check(!again, "No second teleport inside its cooldown (8 s)");
        Walk(sim, 200, 1.5, _ => again |= support.State == SlimeState.Teleporting, move: false);
        check(again, "After the cooldown it may jump again");

        // A push during the windup cancels it (full cooldown); a gone ally means no jump.
        var (pushed, a2, s2) = TeleportFight(2);
        a2.Hp = 30;
        Walk(pushed, 200, dt, move: false);
        check(pushed.UsePush(s2.Id, 200) && s2.State == SlimeState.Stunned, "(setup) push the teleporting support");
        double at = s2.X;
        Walk(pushed, 200, 0.7, move: false);
        check(s2.X > 1000 && Math.Abs(s2.X - s2.TeleportX) > 300 && s2.TeleportCooldown > 7, $"A push cancels the teleport and costs its full cooldown (knocked from {at:0} to {s2.X:0}, no jump)");
        var (gone, a3, s3) = TeleportFight(3);
        a3.Hp = 30;
        Walk(gone, 200, dt, move: false);
        a3.Hp = 0; a3.Set(SlimeState.Defeated);
        Walk(gone, 200, 0.7, move: false);
        check(s3.X == 1200 && s3.TeleportCooldown <= 1, "Its ally gone before the jump: no jump, it looks again soon");

        // Never to another support.
        var twin = new BattleSim(Settings(new[] { Healer(), Healer("mender2") }, Plan(G("mender"), G("mender2")), 0), 0, 1600, seed: 1);
        Ready(twin, 2, 200);
        twin.Slimes[0].X = 600; twin.Slimes[1].X = 1200; twin.Slimes[0].Hp = 5;
        bool jumped = false;
        Walk(twin, 200, 2, _ => jumped |= twin.Slimes.Any(s => s.State == SlimeState.Teleporting), move: false);
        check(!jumped, "A support never jumps to another support");

        // A lone support appears next to a fighting ally, on its far side from the cat.
        bool joined = true;
        for (int seed = 1; seed <= 8; seed++)
        {
            var spawn = new BattleSim(Settings(new[] { Kind("slime"), Healer() }, Plan(G("slime"), G("mender")), 0), 0, 1600, seed: seed);
            Ready(spawn, 2, 800);
            var fighter = spawn.Slimes.Single(s => s.Type.Id == "slime");
            var healer = spawn.Slimes.Single(s => s.Type.Id == "mender");
            joined &= Math.Abs(healer.X - fighter.X) < 60 && Math.Abs(healer.X - 800) > Math.Abs(fighter.X - 800) - 1;
        }
        check(joined, "A support spawns beside a fighting ally, on the side away from the cat");
    }

    private static void SideChecks(Action<bool, string> check)
    {
        // Side a at the far edge, then side b at the near edge after the warning arrow.
        var sim = new BattleSim(Settings(new[] { Kind("slime") }, Plan(G("slime", 1, "a"), G("slime", 1, "b")), 0), 0, 1600, seed: 1);
        Ready(sim, 1, 400);
        check(sim.Slimes[0].X > 1500, "Side \"a\" comes from the edge far from the cat");
        for (int i = 0; i < 30 && !sim.Incoming; i++) sim.Step(dt, 400, false);
        double warnedAt = sim.Time;
        check(sim.Incoming && sim.IncomingX < 100 && sim.ToSpawn == 1 && sim.Slimes.Count == 1, "Side \"b\": an arrow at the other edge first, nothing spawns yet");
        double left = sim.IncomingRemaining;
        for (int i = 0; i < 60; i++) sim.Step(dt, 400, true, true);
        check(sim.IncomingRemaining == left, "The warning waits while the user holds the cat");
        for (int i = 0; i < 120 && sim.Incoming; i++) sim.Step(dt, 400, false);
        check(!sim.Incoming && sim.Slimes.Count == 2 && sim.Slimes[1].X < 100 && sim.Time - warnedAt >= 1 - dt, "After 1 s of warning it comes from that edge");

        // A cat standing at the "b" edge: the spawn goes to the other edge, unannounced.
        var near = new BattleSim(Settings(new[] { Kind("slime") }, Plan(G("slime", 1, "a"), G("slime", 1, "b")), 0), 0, 1600, seed: 1);
        bool warned = false;
        for (int i = 0; i < 120; i++) { near.Step(dt, 60, false); warned |= near.Incoming; }
        check(!warned && near.Slimes.Count == 2 && near.Slimes.All(s => s.X > 1400), "Never from the edge the cat stands at: then from the far edge, without an arrow");

        // A survive stream from both sides late in the stage: edges alternate, each announced.
        var stage = new StageSpec
        {
            Id = "t", Objective = "survive", SurviveSeconds = 60,
            Waves = new[] { new WaveSpec { Stream = new StreamSpec { Weights = new() { ["slime"] = 1 }, Interval = 1, IntervalEnd = 1, PincerAfter = 5 } } }
        };
        var stream = new BattleSim(Settings(new[] { Kind("slime") }, null, 0, 10), 0, 1600, stage: stage, seed: 2);
        var edges = new List<int>();
        bool early = false;
        for (double t = 0; t < 14; t += dt)
        {
            stream.Step(dt, 800, false);
            foreach (var e in stream.Events) if (e.Kind == BattleEventKind.Incoming) { edges.Add(stream.IncomingEdge); early |= 60 - stream.SurviveRemaining < 5; }
            stream.ClearEvents();
            foreach (var s in stream.Slimes) if (s.Targetable && stream.Alive > 6) { s.Hp = 0; s.Set(SlimeState.Defeated); }
        }
        check(!early && edges.Count >= 3 && edges.Zip(edges.Skip(1)).All(p => p.First == -p.Second), $"After pincerAfter the stream alternates edges, each announced ({edges.Count} arrows)");
    }

    private static EnemyType King(double rally = 2) => new()
    {
        Id = "king", MaxHp = 400, Speed = 0, Damage = 0, Range = 110, Windup = 1.2, AttackCooldown = 2.6, Recover = 0.7, Scale = 2, Boss = true, Slam = true,
        PushFactor = 0.35, StunFactor = 0.4, RetreatSpeed = 600, RetreatSeconds = 0.5, SummonCap = 4,
        Phases = new[] { new BossPhase { At = 0.5, Shield = true, Summon = "slime", Count = 2 } },
        RallyEvery = rally, RallyCharge = 0.5, RallySeconds = 3, RallySpeed = 2, ShieldBreakStun = 2
    };

    // The boss knocked to its shield line (a big bomb) with the cat at catX; steps until it has called.
    private static BattleSim ShieldUp(double catX, int seed = 1)
    {
        var sim = new BattleSim(Settings(new[] { King(), Kind("slime", 50, 30) }, Plan(G("king")), 0, 6, 1000),
            0, 1600, seed: seed);
        Ready(sim, 1, catX);
        var boss = sim.Slimes[0];
        sim.Use(BattleSkill.Bomb, boss.Id, catX);
        for (int i = 0; i < 240 && sim.Slimes.Count(s => s.SummonedBy == boss.Id) < 2; i++) sim.Step(dt, catX, false);
        sim.ClearEvents();
        return sim;
    }

    private static void RallyChecks(Action<bool, string> check)
    {
        var sim = ShieldUp(400);
        var boss = sim.Boss!;
        var helpers = sim.Slimes.Where(s => s.SummonedBy == boss.Id).ToList();
        check(boss.Shielded && helpers.Count == 2, "(setup) the boss shields up and calls two helpers");
        bool rallying = false, rallied = false;
        for (int i = 0; i < 300 && !rallied; i++)
        {
            sim.Step(dt, 400, false);
            rallying |= sim.Events.Any(e => e.Kind == BattleEventKind.BossRallying);
            rallied |= sim.Events.Any(e => e.Kind == BattleEventKind.BossRallied);
            sim.ClearEvents();
        }
        var fast = helpers.SingleOrDefault(h => h.RallyRemaining > 0);
        check(rallying && rallied && fast != null, "During the shield the boss's crown glows, then one helper is rallied");
        var slow = helpers.First(h => h != fast);
        double fx = fast?.X ?? 0, sx = slow.X;
        for (int i = 0; i < 20; i++) sim.Step(dt, 400, false);
        double fastStep = Math.Abs((fast?.X ?? 0) - fx), slowStep = Math.Abs(slow.X - sx);
        check(fast != null && slowStep > 0 && fastStep > slowStep * 1.7, $"The rallied helper walks faster ({fastStep:0.0} vs {slowStep:0.0} DIPs)");

        // A push on the boss while its crown glows cuts the call.
        var cut = ShieldUp(400, 2);
        var king = cut.Boss!;
        bool glowing = false, granted = false;
        for (int i = 0; i < 300 && !glowing; i++) { cut.Step(dt, 400, false); glowing = king.RallyCharge > 0; cut.ClearEvents(); }
        check(glowing && cut.UsePush(king.Id, 400), "(setup) push the boss while it rallies");
        for (int i = 0; i < 60; i++) { cut.Step(dt, 400, false); granted |= cut.Events.Any(e => e.Kind == BattleEventKind.BossRallied); cut.ClearEvents(); }
        check(!granted && king.RallyCharge == 0, "Pushing the boss during the glow cancels the rally");

        // The shield breaks: the boss is dazed (2 s) and takes damage meanwhile.
        foreach (var h in sim.Slimes.Where(s => s.SummonedBy == boss.Id)) { h.Hp = 0; h.Set(SlimeState.Defeated); }
        sim.Step(dt, 400, false);
        check(!boss.Shielded && boss.State == SlimeState.Stunned && boss.StunRemaining > 1.9, "A broken shield leaves the boss dazed for 2 s");
        double hp = boss.Hp;
        sim.Use(BattleSkill.Bomb, boss.Id, 400);
        for (int i = 0; i < 10; i++) sim.Step(dt, 400, false);
        check(boss.Hp < hp, "The dazed boss takes damage as usual (the bomb finishes it)");

        // Helpers never appear on top of the cat, even when it stands at the boss's edge.
        bool clear = true;
        for (int seed = 1; seed <= 4; seed++)
        {
            var close = ShieldUp(1480, seed);
            foreach (var h in close.Slimes.Where(s => s.SummonedBy != 0)) clear &= Math.Abs(h.X - 1480) >= h.Width / 2 + BattleSim.CatHalfWidth;
        }
        check(clear, "The boss's helpers never appear on top of the cat");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        bool Rejected(string json)
        {
            try { BattleData.Parse(json); return false; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return true; }
        }
        const string king = "{\"id\":\"king\",\"boss\":true}";
        check(Rejected("{\"enemies\":[{\"id\":\"slime\",\"teleportBelow\":0.3}," + king + "]}") &&
              !Rejected("{\"enemies\":[{\"id\":\"slime\",\"support\":true,\"healAmount\":5,\"teleportBelow\":0.3}," + king + "]}"),
            "teleportBelow is only for a support enemy");
        check(Rejected("{\"enemies\":[{\"id\":\"slime\"},{\"id\":\"king\",\"boss\":true,\"rallyEvery\":5}]}"), "rallyEvery needs a shield phase");
        check(Rejected("{\"waves\":[{\"groups\":[{\"enemy\":\"slime\",\"side\":\"c\"}]}]}") && Rejected("{\"maxAttackers\":11}"),
            "A side other than a/b, or maxAttackers above 10, is rejected");
        check(Rejected("{\"enemies\":[{\"id\":\"slime\",\"turnSeconds\":9}," + king + "]}") && Rejected("{\"incomingWarning\":9}"), "Turn and warning times out of range are rejected");

        var data = BattleData.Settings;
        var rock = data.Enemy("rock");
        var mender = data.Enemy("mender");
        var chapterKing = data.Enemy("king_c1");
        check(rock is { Blocker: true, BlockFrontOnly: true, TurnSeconds: 0.6 } && mender is { Marks: "leaf", TeleportBelow: 0.35, TeleportCooldown: 8 } &&
              chapterKing is { RallyEvery: 7, RallyCharge: 0.8, RallySpeed: 1.4, ShieldBreakStun: 2 } && data.MaxAttackers == 2,
            "battle.json: rocks block the way and only from the front; the herb slime teleports; the King rallies and is dazed when its shield breaks");
        var stages = data.Campaign!.Chapters[0].Stages;
        check(stages[4].Waves[0].Groups.Any(g => g.Side == "b") && stages[4].Waves[0].Groups.Any(g => g.Side == "a" && g.Enemy == "rock") &&
              stages[2].Waves[0].Stream?.PincerAfter == 45, "Stage 5 opens with a two-sided wave (rock in front, quick slimes behind); Hold on comes from both sides after 45 s");
    }
}
