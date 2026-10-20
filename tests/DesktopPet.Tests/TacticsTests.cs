using DesktopPet;

// Enemy tactics after Enemy Director milestone 3 (user request), each behind its own data switch:
// a heal guards the ally it is on (healGuard), a support hurries away from a close cat (fleeSpeed)
// and keeps behind its guard (hideBehindGuard), a hurt slime falls back to its healer
// (fallBackBelow), a quick slime runs off after its dash (hitAndRun), a slime goes round the
// rocks to the cat's back (flank).
internal static class TacticsTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Fighter(double hp = 500, double speed = 0, double below = 0, bool flank = false) => new()
    {
        Id = "slime", MaxHp = hp, Speed = speed, Damage = 0, Range = 64, Windup = 0.8, AttackCooldown = 1.8, Recover = 0.35, FallBackBelow = below, Flank = flank, FlankSpeed = 2
    };

    private static EnemyType Mender(double heal = 10, double cooldown = 0.5, double flee = 1, bool hide = false, double speed = 0) => new()
    {
        Id = "mender", MaxHp = 40, Speed = speed, Damage = 0, Range = 56, Windup = 0.8, AttackCooldown = 2, Scale = 0.9, Support = true, KeepBack = 110,
        HealAmount = heal, HealRange = 2000, HealCooldown = cooldown, HealWindup = 1, HealBudget = 1000, HealGuard = true, HealGuardRepeat = 2, FleeSpeed = flee, HideBehindGuard = hide
    };

    private static readonly EnemyType Rock = new() { Id = "rock", MaxHp = 500, Speed = 0, Damage = 0, Range = 72, Windup = 1, AttackCooldown = 2.4, Scale = 1.35, Blocker = true };
    private static readonly EnemyType Boss = new() { Id = "boss", MaxHp = 1000, Speed = 0, Damage = 0, Range = 110, Windup = 9, AttackCooldown = 9, Scale = 2, Boss = true };

    // The kinds spawned in order (none of them moving unless given a speed), then put at these places.
    private static BattleSim Field(EnemyType[] kinds, params (string Kind, double X)[] place) => Field(0, 1, kinds, place);

    // catDamage: the cat's weapon (0: its hits do nothing); bombRadius: how far a blast reaches.
    private static BattleSim Field(double catDamage, double bombRadius, EnemyType[] kinds, params (string Kind, double X)[] place)
    {
        var settings = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, CatDamage = catDamage, CatMaxHp = 1_000_000, MaxAlive = 8, SpawnMinDistance = 0,
            BombDamage = 5, BombDelay = 0.01, BombCooldown = 0.1, BombRadius = bombRadius, Enemies = kinds,
            Plan = new[] { new WaveSpec { Groups = place.Select(p => new WaveGroup { Enemy = p.Kind }).ToArray() } } };
        var sim = new BattleSim(settings, 0, 1600, seed: 1);
        for (int i = 0; i < 120 && sim.Slimes.Count(s => s.Targetable) < place.Length; i++) sim.Step(dt, -5000, false);
        for (int i = 0; i < place.Length; i++) sim.Slimes[i].X = place[i].X;
        sim.ClearEvents();
        return sim;
    }

    private static void Run(BattleSim sim, double seconds, double cat, Func<bool>? until = null, Action? each = null)
    {
        for (double t = 0; t < seconds && until?.Invoke() != true; t += dt) { sim.Step(dt, cat, false); each?.Invoke(); sim.ClearEvents(); }
    }

    public static void Run(Action<bool, string> check)
    {
        GuardChecks(check);
        FleeChecks(check);
        FallBackChecks(check);
        HitAndRunChecks(check);
        FlankChecks(check);
        DataChecks(check);
    }

    private static void GuardChecks(Action<bool, string> check)
    {
        var sim = Field(new[] { Fighter(), Mender() }, ("slime", 600), ("mender", 900));
        var ally = sim.Slimes[0];
        var healer = sim.Slimes[1];
        ally.Hp = 300;
        int warded = 0;
        Run(sim, 2, 100, () => healer.State == SlimeState.Casting, () => warded += sim.Events.Count(e => e.Kind == BattleEventKind.EnemyWarded));
        bool guarded = ally.Mended && healer.MendingId == ally.Id;
        double hp = ally.Hp;
        sim.Use(BattleSkill.Bomb, ally.Id, 100);
        int hits = 0;
        Run(sim, 0.1, 100, each: () => hits += sim.Events.Count(e => e.Kind == BattleEventKind.HitWarded));
        check(guarded && ally.Hp == hp && hits == 1 && sim.WardedHits == 1, "While a heal winds up on it, the ally takes no damage (the bomb shows IMMUNE)");
        Run(sim, 2, 100, () => healer.State != SlimeState.Casting);
        sim.Step(dt, 100, false);
        check(!ally.Mended, "The guard ends with the heal");
        // The next heal comes within 2 s: no guard this time.
        Run(sim, 1.5, 100, () => healer.State == SlimeState.Casting);
        check(healer.State == SlimeState.Casting && !ally.Mended && healer.MendingId == 0, "The same ally is not guarded again within healGuardRepeat (2 s)");

        // A push on the healer ends the guard at once.
        var cut = Field(new[] { Fighter(), Mender() }, ("slime", 600), ("mender", 900));
        var friend = cut.Slimes[0];
        var mender = cut.Slimes[1];
        friend.Hp = 300;
        Run(cut, 2, 100, () => mender.State == SlimeState.Casting);
        cut.UsePush(mender.Id, 100);
        double before = friend.Hp;
        cut.Use(BattleSkill.Bomb, friend.Id, 100);
        Run(cut, 0.1, 100);
        check(!friend.Mended && friend.Hp < before, "Pushing the healer ends the guard at once (the bomb lands)");

        // Never a boss.
        var royal = Field(new[] { Boss, Mender() }, ("boss", 600), ("mender", 900));
        royal.Slimes[0].Hp = 500;
        Run(royal, 2, 100, () => royal.Slimes[1].State == SlimeState.Casting);
        check(royal.Slimes[1].State == SlimeState.Casting && !royal.Slimes[0].Mended, "A heal never guards a boss");

        // The cat leaves a guarded enemy alone while another one is there.
        var pick = Field(new[] { Fighter(), Mender() }, ("slime", 500), ("mender", 1400), ("slime", 700));
        pick.Slimes[0].Hp = 300;
        Run(pick, 2, 300, () => pick.Slimes[0].Mended);
        pick.Step(dt, 300, false);
        check(pick.Slimes[0].Mended && pick.TargetId == pick.Slimes[2].Id, "The cat goes for another enemy while its nearest one is guarded");

        // The guard ends on the very frame the heal lands, not a frame later: a bomb going off at
        // the start of the next frame (bombs come before the slimes) hits.
        var end = Field(new[] { Fighter(), Mender() }, ("slime", 600), ("mender", 900));
        var healed = end.Slimes[0];
        var ender = end.Slimes[1];
        healed.Hp = 300;
        Run(end, 2, 100, () => ender.State == SlimeState.Casting);
        Run(end, 2, 100, () => ender.State != SlimeState.Casting);
        bool endedNow = ender.State == SlimeState.Recover && !healed.Mended && healed.Hp > 300;
        end.Use(BattleSkill.Bomb, healed.Id, 100);
        double mendedHp = healed.Hp;
        end.Step(dt, 100, false);
        check(endedNow && healed.Hp < mendedHp && end.WardedHits == 0, "The guard ends on the frame the heal lands (the next frame's bomb lands)");

        // A healer defeated by the cat ends its guard at once (the cat's hit comes after the slimes).
        var fall = Field(1000, 1, new[] { Fighter(), Mender() }, ("slime", 1400), ("mender", 900));
        var spared = fall.Slimes[0];
        var victim = fall.Slimes[1];
        spared.Hp = 300;
        Run(fall, 2, 100, () => victim.State == SlimeState.Casting);
        bool wasGuarded = spared.Mended;
        Run(fall, 1, 880, () => victim.State == SlimeState.Defeated);
        check(wasGuarded && victim.State == SlimeState.Defeated && !spared.Mended, "A healer the cat defeats ends its guard on that frame");

        // One blast over a healer and the ally it guards: the guard holds for that blast whichever
        // comes first in the list, and ends right after it.
        foreach (bool healerFirst in new[] { false, true })
        {
            var both = healerFirst
                ? Field(0, 60, new[] { Fighter(), Mender() }, ("mender", 640), ("slime", 600))
                : Field(0, 60, new[] { Fighter(), Mender() }, ("slime", 600), ("mender", 640));
            var guardedOne = both.Slimes.First(x => x.Type.Id == "slime");
            var support = both.Slimes.First(x => x.Type.Id == "mender");
            guardedOne.Hp = 300;
            Run(both, 2, 100, () => support.State == SlimeState.Casting);
            support.Hp = 1;
            both.Use(BattleSkill.Bomb, guardedOne.Id, 100);
            both.Step(dt, 100, false);
            check(support.State == SlimeState.Defeated && guardedOne.Hp == 300 && both.WardedHits == 1 && !guardedOne.Mended,
                $"A blast over a healer and its ally: the ally is guarded from it, the guard ends after ({(healerFirst ? "healer" : "ally")} first in the list)");
        }
    }

    private static void FleeChecks(Action<bool, string> check)
    {
        var support = new Slime { Type = Mender(flee: 2.5) };
        check(Math.Abs(TacticsMath(support, 250) - 1) < 1e-9 && Math.Abs(TacticsMath(support, 60) - 2.5) < 1e-9 && Math.Abs(TacticsMath(support, 130) - 1.75) < 1e-9,
            "fleeSpeed: ×1 from 200 DIPs, ×2.5 at 60 DIPs and closer, in between on the way");
        double Moved(double flee)
        {
            var sim = Field(new[] { Fighter(), Mender(heal: 0.0001, cooldown: 60, flee: flee, speed: 28) }, ("slime", 100), ("mender", 950));
            var m = sim.Slimes[1];
            sim.Step(dt, 900, false);
            double x = m.X;
            sim.Step(dt, 900, false);
            return (m.X - x) / dt;
        }
        double slow = Moved(1), fast = Moved(2.5);
        check(Math.Abs(slow - 28) < 1 && Math.Abs(fast - 70) < 1 && fast < 130, $"A support backing off from a cat 50 DIPs away hurries (28 → {fast:0} DIPs/s), still slower than the cat runs");

        // It keeps behind its guard rock, not just the nearest ally.
        var sim = Field(new[] { Fighter(), Mender(heal: 0.0001, cooldown: 60, hide: true, speed: 60), Rock }, ("slime", 700), ("mender", 1100), ("rock", 1000));
        Run(sim, 4, 400);
        var (slime, healer, rock) = (sim.Slimes[0], sim.Slimes[1], sim.Slimes[2]);
        check(rock.GuardOf == healer.Id && Math.Abs(healer.X - rock.X - 110) < 8 && healer.X > rock.X && rock.X > slime.X,
            $"hideBehindGuard: the support keeps 110 DIPs behind its guard rock (rock {rock.X:0}, support {healer.X:0}), not behind the nearer slime");
    }

    private static double TacticsMath(Slime support, double distance) => BattleSim.FleeFactor(support, distance);

    private static void FallBackChecks(Action<bool, string> check)
    {
        var sim = Field(new[] { Fighter(50, 40, 0.3), Mender(heal: 35, cooldown: 1, speed: 0) }, ("slime", 400), ("mender", 1300));
        var hurt = sim.Slimes[0];
        var healer = sim.Slimes[1];
        hurt.Hp = 10;
        int announced = 0;
        bool windup = false;
        double start = hurt.X;
        Run(sim, 1, 380, each: () => { announced += sim.Events.Count(e => e.Kind == BattleEventKind.EnemyFallingBack); windup |= hurt.State == SlimeState.Windup; });
        check(hurt.FallingBack && hurt.X > start + 20 && !windup && announced == 1, "Below 30 % HP a slime falls back toward its healer (no attack on the way, with the cat in reach)");
        Run(sim, 12, 380, () => !hurt.FallingBack);
        check(!hurt.FallingBack && hurt.FellBack && hurt.Hp >= 0.6 * hurt.MaxHp, $"…until it is healed well past the line ({hurt.Hp:0} HP)");
        hurt.Hp = 5;
        Run(sim, 0.5, hurt.X - 40);
        check(!hurt.FallingBack, "Only once a battle");
        var alone = Field(new[] { Fighter(50, 40, 0.3) }, ("slime", 400));
        alone.Slimes[0].Hp = 5;
        Run(alone, 0.5, 380);
        check(!alone.Slimes[0].FallingBack, "No healer that can heal: it does not fall back");
    }

    private static void HitAndRunChecks(Action<bool, string> check)
    {
        var runner = new EnemyType { Id = "runner", MaxHp = 25, Speed = 70, Damage = 0, Range = 54, Windup = 0.7, AttackCooldown = 2.6, Recover = 0.4, Scale = 0.75,
            Dash = true, DashRange = 170, DashDistance = 230, DashSpeed = 420, HitAndRun = 150 };
        var sim = Field(new[] { runner }, ("runner", 600));
        var r = sim.Slimes[0];
        double cat = 700, lunged = double.NaN, farthest = 0;
        Run(sim, 6, cat, () => !double.IsNaN(lunged) && r.RunAwayLeft <= 0 && r.State == SlimeState.Moving && farthest > 0, () =>
        {
            if (double.IsNaN(lunged) && r.State == SlimeState.Recover) lunged = r.X;
            if (!double.IsNaN(lunged)) farthest = Math.Max(farthest, Math.Abs(r.X - lunged));
        });
        check(!double.IsNaN(lunged) && farthest >= 140 && farthest <= 160, $"hitAndRun: after its dash the quick slime runs ~150 DIPs off ({farthest:0}) before coming back");
    }

    private static void FlankChecks(Action<bool, string> check)
    {
        var sim = Field(new[] { Fighter(speed: 34, flank: true), Rock }, ("slime", 1300), ("rock", 1000), ("slime", 1350));
        var a = sim.Slimes[0];
        var b = sim.Slimes[2];
        int announced = 0, most = 0;
        Run(sim, 8, 800, () => a.Flanked || b.Flanked, () =>
        {
            announced += sim.Events.Count(e => e.Kind == BattleEventKind.EnemyFlanking);
            most = Math.Max(most, sim.Slimes.Count(s => s.Flanking));
        });
        var flanker = a.Flanked ? a : b;
        check(flanker.Flanked && flanker.X < 800 && Math.Abs(flanker.X - (800 - 64 * 0.8)) < 8 && most == 1 && announced >= 1,
            $"A slime behind a rock goes round to the cat's back ({flanker.X:0}, the cat at 800); one at a time");
        var open = Field(new[] { Fighter(speed: 34, flank: true) }, ("slime", 1300));
        Run(open, 2, 800);
        check(!open.Slimes[0].Flanking && !open.Slimes[0].Flanked, "No rock in the way: no flanking");
        var edge = Field(new[] { Fighter(speed: 34, flank: true), Rock }, ("slime", 600), ("rock", 300));
        Run(edge, 1, 40);
        check(!edge.Slimes[0].Flanking, "No room behind a cat at the floor's edge: no flanking");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        check(data.Enemy("mender") is { HealGuard: true, HealGuardRepeat: 2, FleeSpeed: 2.5, HideBehindGuard: true } &&
              data.Enemy("slime") is { FallBackBelow: 0.3, Flank: true, FlankSpeed: 2 } && data.Enemy("runner").HitAndRun == 150 && data.MaxFlankers == 1 &&
              data.Enemy("rock") is { Flank: false, FallBackBelow: 0 },
            "Shipped tactics: herb slime guards its heal target, hurries and hides behind its rock; slime falls back and flanks; quick slime hits and runs");
        string Bad(string json)
        {
            using var stream = typeof(BattleData).Assembly.GetManifestResourceStream("DesktopPet.battle.json")!;
            try { BattleData.ParseWithOverride(new StreamReader(stream).ReadToEnd(), "{ \"schemaVersion\": 1, " + json + " }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        check(Bad("\"enemies\": [ { \"id\": \"rock\", \"flank\": true } ]").Contains("blocker") && Bad("\"enemies\": [ { \"id\": \"slime\", \"hitAndRun\": 100 } ]").Contains("dash") &&
              Bad("\"enemies\": [ { \"id\": \"slime\", \"healGuard\": true } ]").Contains("heals") && Bad("\"enemies\": [ { \"id\": \"mender\", \"flank\": true } ]").Contains("support") &&
              Bad("\"maxFlankers\": 9").Contains("maxFlankers"), "Tactic switches on the wrong kind of enemy are rejected");
        // Each switch can be turned off by a user file without touching the others.
        var off = BattleData.ParseWithOverride(new StreamReader(typeof(BattleData).Assembly.GetManifestResourceStream("DesktopPet.battle.json")!).ReadToEnd(),
            "{ \"schemaVersion\": 1, \"enemies\": [ { \"id\": \"slime\", \"flank\": false } ] }");
        check(!off.Enemy("slime").Flank && off.Enemy("slime").FallBackBelow == 0.3, "A user battle.json can turn one tactic off and keep the rest");
    }
}
