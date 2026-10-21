using DesktopPet;

// After Enemy Director milestone 3 (user request): the chapter boss leaps to the edge opposite its
// half of the floor (the other one if the cat is in that half), announces its helpers with rings on
// the floor, lines them up by role, shows which helper its rally call is for; support heals grow
// with the same factor as enemy HP.
internal static class BossLeapTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id, double scale = 1) => new()
    {
        Id = id, MaxHp = 50, Speed = 0, Damage = 0, Range = 64, Windup = 0.8, AttackCooldown = 1.8, Recover = 0.35, Scale = scale
    };

    private static readonly EnemyType[] Helpers =
    {
        Kind("slime"), new EnemyType { Id = "runner", MaxHp = 25, Speed = 0, Damage = 0, Range = 54, Windup = 0.7, AttackCooldown = 2.6, Scale = 0.75, Dash = true, DashRange = 10 },
        new EnemyType { Id = "mender", MaxHp = 40, Speed = 0, Damage = 0, Range = 56, Windup = 0.8, AttackCooldown = 2, Scale = 0.9, Support = true, KeepBack = 0, HealAmount = 10, HealBudget = 100 },
        new EnemyType { Id = "rock", MaxHp = 150, Speed = 0, Damage = 0, Range = 72, Windup = 1, AttackCooldown = 2.4, Scale = 1.35, Blocker = true }
    };

    private static EnemyType King(double warning = 0.6, bool rally = false) => new()
    {
        Id = "king", MaxHp = 400, Speed = 0, Damage = 0, Range = 110, Windup = 1.2, AttackCooldown = 2.6, Recover = 0.7, Scale = 2, Boss = true, Slam = true,
        PushFactor = 0.35, StunFactor = 0.4, SummonCap = 4, RetreatLeap = true, LeapCrouch = 0.35, LeapSeconds = 0.9, SummonWarning = warning,
        RallyEvery = rally ? 1 : 0, RallyCharge = 0.5,
        Phases = new[] { new BossPhase { At = 0.5, Shield = true, Summons = new[] {
            new WaveGroup { Enemy = "runner", Count = 1 }, new WaveGroup { Enemy = "slime", Count = 1 }, new WaveGroup { Enemy = "mender", Count = 1 }, new WaveGroup { Enemy = "rock", Count = 1 } } } }
    };

    // The boss at bossX, the cat at catX, then a big bomb: the shield phase starts.
    private static (BattleSim Sim, Slime Boss) Shielded(double bossX, double catX, EnemyType? king = null)
    {
        var settings = new BattleSettings { FirstWaveDelay = 0, CatDamage = 0, CatMaxHp = 1_000_000, MaxAlive = 8, BombDamage = 1000, BombDelay = 0.01, BombCooldown = 0.1,
            Enemies = Helpers.Append(king ?? King()).ToArray(), Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "king" } } } } };
        var sim = new BattleSim(settings, 0, 1600, seed: 1);
        for (int i = 0; i < 300 && sim.Boss is not { State: SlimeState.Moving }; i++) sim.Step(dt, catX, false);
        var boss = sim.Boss!;
        boss.X = bossX;
        sim.Use(BattleSkill.Bomb, boss.Id, catX);
        for (int i = 0; i < 10 && !boss.Shielded; i++) sim.Step(dt, catX, false);
        return (sim, boss);
    }

    public static void Run(Action<bool, string> check)
    {
        EdgeChecks(check);
        LeapChecks(check);
        SummonChecks(check);
        RallyChecks(check);
        HealChecks(check);
        DataChecks(check);
    }

    private static void EdgeChecks(Action<bool, string> check)
    {
        double Lands(double bossX, double catX)
        {
            var (sim, boss) = Shielded(bossX, catX);
            for (int i = 0; i < 180 && boss.Retreating; i++) sim.Step(dt, catX, false);
            return boss.X;
        }
        const double left = 46, right = 1554; // The floor's edges for a boss 92 DIPs wide.
        check(Math.Abs(Lands(1200, 1400) - left) < 1 && Math.Abs(Lands(1000, 1500) - left) < 1, "Boss and cat in the right half: it leaps to the left edge");
        check(Math.Abs(Lands(300, 200) - right) < 1 && Math.Abs(Lands(500, 100) - right) < 1, "Boss and cat in the left half: it leaps to the right edge");
        check(Math.Abs(Lands(900, 700) - right) < 1, "Boss in the right half but the cat in the left one: it goes right, never to the cat's side");
        check(Math.Abs(Lands(700, 900) - left) < 1, "Boss in the left half but the cat in the right one: it goes left");
    }

    private static void LeapChecks(Action<bool, string> check)
    {
        var (sim, boss) = Shielded(1200, 1400);
        double startX = boss.X, peak = 0, hp = boss.Hp;
        bool stillWhileCrouching = true, rose = false, crouched = false;
        int took = 0, landed = 0;
        double time = 0, landedAt = 0;
        sim.ClearEvents();
        for (int i = 0; i < 180 && boss.Retreating; i++)
        {
            sim.Step(dt, 1400, false);
            time += dt;
            if (time < 0.33) { stillWhileCrouching &= Math.Abs(boss.X - startX) < 0.01 && boss.LeapHeight == 0; crouched |= boss.Crouch > 0.5; }
            rose |= boss.LeapHeight > 20;
            peak = Math.Max(peak, boss.LeapHeight);
            took += sim.Events.Count(e => e.Kind == BattleEventKind.BossLeaping);
            if (sim.Events.Any(e => e.Kind == BattleEventKind.BossLanded)) { landed++; landedAt = time; }
            if (time > 0.6 && time < 0.7) sim.Use(BattleSkill.Bomb, boss.Id, 1400); // Mid-air: nothing gets through.
            sim.ClearEvents();
        }
        check(crouched && stillWhileCrouching, "It crouches where it stands first (0.35 s)");
        check(rose && peak <= 80 && took == 1 && landed == 1 && Math.Abs(landedAt - 1.25) < 0.05, $"…then flies (up to {peak:0} DIPs) and lands once, about 1.25 s after the shield went up ({landedAt:0.00} s)");
        check(boss.Shielded && boss.Hp == hp && boss.LeapHeight == 0 && sim.Slimes.All(s => s.SummonedBy == 0), "Shielded all the way (a bomb mid-air does nothing); no helper before it lands");

        // Pause freezes the leap; a push during the crouch makes it wait, then it still goes.
        var (paused, king) = Shielded(1200, 1400);
        for (int i = 0; i < 40; i++) paused.Step(dt, 1400, false);
        double x = king.X, height = king.LeapHeight;
        paused.Paused = true;
        for (int i = 0; i < 60; i++) paused.Step(dt, 1400, false);
        check(king.X == x && king.LeapHeight == height && height > 0, "Pause freezes the boss in mid-air");
        var (pushed, crouching) = Shielded(1200, 1400);
        pushed.Step(dt, 1400, false);
        pushed.UsePush(crouching.Id, 1400);
        for (int i = 0; i < 180 && crouching.Retreating; i++) pushed.Step(dt, 1400, false);
        check(!crouching.Retreating && Math.Abs(crouching.X - 46) < 1, "A push during the crouch only delays the leap");
    }

    private static void SummonChecks(Action<bool, string> check)
    {
        var (sim, boss) = Shielded(1200, 1400);
        int warned = 0, summoned = 0;
        List<(string Kind, double X)>? spots = null;
        bool heldWhileWarning = true;
        for (int i = 0; i < 300 && summoned == 0; i++)
        {
            sim.Step(dt, 1400, false);
            warned += sim.Events.Count(e => e.Kind == BattleEventKind.BossSummoning);
            summoned += sim.Events.Count(e => e.Kind == BattleEventKind.BossSummoned);
            if (boss.CallSpots != null) { spots ??= boss.CallSpots.ToList(); heldWhileWarning &= boss.Shielded && sim.Slimes.All(s => s.SummonedBy == 0); }
            sim.ClearEvents();
        }
        var helpers = sim.Slimes.Where(s => s.SummonedBy == boss.Id).ToList();
        check(warned == 1 && spots?.Count == 4 && heldWhileWarning && summoned == 1, "After landing, rings show where 4 helpers will appear; the shield holds while they are announced");
        check(spots != null && helpers.Count == 4 && helpers.All(h => spots.Any(p => p.Kind == h.Type.Id && Math.Abs(p.X - h.X) < 1)), "…and the helpers appear on those rings");
        var order = helpers.OrderBy(h => Math.Abs(h.X - boss.X)).Select(h => h.Type.Id).ToArray();
        check(order.SequenceEqual(new[] { "mender", "rock", "slime", "runner" }) && helpers.All(h => h.X > boss.X),
            $"Helpers line up by role from the boss toward the cat: herb slime, rock, slime, quick slime ({string.Join(", ", order)})");
        // The rock guards the herb slime it came with.
        for (int i = 0; i < 60; i++) sim.Step(dt, 1400, false); // They finish appearing (0.5 s), then guards are chosen.
        check(helpers.Single(h => h.Type.Id == "rock").GuardOf == helpers.Single(h => h.Type.Id == "mender").Id, "A summoned rock guards the summoned herb slime");

        // Without a warning (data 0) they come at once, as before.
        var (direct, quick) = Shielded(1200, 1400, King(0));
        for (int i = 0; i < 180 && quick.Retreating; i++) direct.Step(dt, 1400, false);
        check(quick.CallSpots == null && direct.Slimes.Count(s => s.SummonedBy == quick.Id) == 4, "summonWarning 0: the helpers come as soon as it lands");
    }

    private static void RallyChecks(Action<bool, string> check)
    {
        var (sim, boss) = Shielded(1200, 1400, King(0.1, rally: true));
        Slime? shown = null;
        bool same = false;
        for (int i = 0; i < 600 && !same; i++)
        {
            sim.Step(dt, 600, false);
            if (sim.RallyTarget(boss) is Slime target) shown = target;
            if (shown != null && sim.Events.Any(e => e.Kind == BattleEventKind.BossRallied)) same = shown.Rallied && shown.Elite != null && !shown.Type.Support;
            sim.ClearEvents();
        }
        check(same, "While the crown glows the battle tells which helper the call is for, and that one turns elite");
    }

    private static void HealChecks(Action<bool, string> check)
    {
        var mender = Helpers[2];
        var settings = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, CatDamage = 0, CatMaxHp = 1_000_000, Enemies = Helpers };
        BattleSim Battle(double threat, BattleDifficulty level) => new(settings, 0, 1600, difficulty: level, seed: 1,
            stage: new StageSpec { Id = "h", Threat = threat, Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "mender" } } } } });
        Slime Healer(BattleSim sim) { for (int i = 0; i < 60 && sim.Slimes.Count == 0; i++) sim.Step(dt, 800, false); return sim.Slimes[0]; }
        var plain = Healer(Battle(1, BattleDifficulty.Normal));
        var strong = Healer(Battle(1.8, BattleDifficulty.Normal));
        var easy = Healer(Battle(1, BattleDifficulty.Easy));
        check(plain.HealPower == 1 && plain.HealLeft == 100 && strong.HealPower == 1.8 && strong.HealLeft == 180 && easy.HealPower == settings.Easy.EnemyHp,
            "A support's heals (and their budget) grow like enemy HP: ×1.8 at threat 1.8, Easy's HP factor on Easy");
        // A heal at threat 2: twice the amount.
        var sim = new BattleSim(new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, CatDamage = 0, CatMaxHp = 1_000_000, Enemies = Helpers.Select(h => h.Id == "mender" ? new EnemyType
            { Id = "mender", MaxHp = 40, Speed = 0, Damage = 0, Range = 56, Windup = 0.8, AttackCooldown = 2, Support = true, KeepBack = 0, HealAmount = 10, HealRange = 2000, HealCooldown = 1, HealWindup = 0.2, HealBudget = 100 } : h).ToArray() },
            0, 1600, seed: 1, stage: new StageSpec { Id = "h", Threat = 2, Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "rock" }, new WaveGroup { Enemy = "mender" } } } } });
        for (int i = 0; i < 60; i++) sim.Step(dt, 800, false);
        var rock = sim.Slimes.First(s => s.Type.Id == "rock");
        rock.Hp = 100;
        double healed = 0;
        for (int i = 0; i < 180 && healed == 0; i++) { sim.Step(dt, 800, false); healed = sim.Events.Where(e => e.Kind == BattleEventKind.EnemyHealed).Sum(e => e.Value); sim.ClearEvents(); }
        check(healed == 20, $"At threat 2 a heal of 10 gives 20 ({healed})");
        _ = mender;
    }

    private static void DataChecks(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var king = data.Enemy("king_c1");
        var herb = data.Enemy("mender");
        check(king is { RetreatLeap: true, LeapCrouch: 0.35, LeapSeconds: 0.9, SummonWarning: 0.6 } && !data.Enemy("king").RetreatLeap,
            "The chapter boss leaps (0.35 s crouch, 0.9 s flight) and announces its helpers 0.6 s; the free battle's king is unchanged");
        check(herb is { HealAmount: 35, HealCooldown: 3.15, HealWindup: 0.9, HealBudget: 210 }, "Herb slime: heals 35 every 3.15 s (0.9 s windup), 210 in all, before difficulty and threat");
        string Bad(string json)
        {
            using var stream = typeof(BattleData).Assembly.GetManifestResourceStream("DesktopPet.battle.json")!;
            try { BattleData.ParseWithOverride(new StreamReader(stream).ReadToEnd(), "{ \"schemaVersion\": 1, " + json + " }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        check(Bad("\"enemies\": [ { \"id\": \"king\", \"retreatLeap\": true } ]").Contains("shield phase") && Bad("\"enemies\": [ { \"id\": \"king_c1\", \"leapSeconds\": 9 } ]").Contains("leapSeconds"),
            "A leap without a shield phase, or a flight out of range, is rejected");
    }
}
