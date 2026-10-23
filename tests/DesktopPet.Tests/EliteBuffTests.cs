using DesktopPet;

// 1.2.0-beta enemy and elite buff: elite stat multipliers (applied once, on top of difficulty and
// threat), a shorter wait between attacks that leaves the windup alone, a heal cooldown and budget
// that do not touch the amount per heal or HealPower, and the Push immunity of the elite rock.
internal static class EliteBuffTests
{
    const double dt = 1.0 / 60;

    private static BattleSettings Settings(params EnemyType[] kinds) => new()
    {
        FirstWaveDelay = 0, SpawnInterval = 0.2, CatDamage = 0, ComboKnock = 0, CatMaxHp = 1_000_000, MaxAlive = 3, MaxElites = 1, Enemies = kinds
    };

    private static StageSpec Stage(double elite, params string[] enemies) => new()
    {
        Id = "t", EliteChance = elite, Waves = new[] { new WaveSpec { Groups = enemies.Select(e => new WaveGroup { Enemy = e, Count = 1 }).ToArray() } }
    };

    private static void Run(BattleSim sim, double seconds, Func<double>? catX = null, Action? each = null)
    {
        for (double t = 0; t < seconds && !sim.Over; t += dt) { sim.Step(dt, catX?.Invoke() ?? 800, false); each?.Invoke(); sim.ClearEvents(); }
    }

    private static EnemyType Fighter(EliteSpec? elite) => new()
    {
        Id = "slime", MaxHp = 50, Speed = 0, Damage = 10, Range = 64, Windup = 0.8, AttackCooldown = 1.8, Recover = 0.35, Elite = elite
    };

    public static void Run(Action<bool, string> check)
    {
        StatChecks(check);
        CooldownChecks(check);
        HealChecks(check);
        PushChecks(check);
        DataChecks(check);
        ShippedChecks(check);
    }

    private static void StatChecks(Action<bool, string> check)
    {
        var buff = new EliteSpec { Hp = 1.2, Damage = 1.2, DescVi = "a", DescEn = "a" };
        Slime First(BattleDifficulty level, double chance)
        {
            var sim = new BattleSim(Settings(Fighter(buff)), 0, 1600, difficulty: level, stage: Stage(chance, "slime"), seed: 1);
            Run(sim, 1, () => 100);
            return sim.Slimes[0];
        }
        var plain = First(BattleDifficulty.Normal, 0);
        var elite = First(BattleDifficulty.Normal, 1);
        check(plain.Elite == null && plain.MaxHp == 50 && plain.Damage == 10 && elite.Elite != null && elite.MaxHp == 60 && elite.Hp == 60 && elite.Damage == 12,
            $"An elite's HP and damage multiply once on its kind (50/10 → {elite.MaxHp}/{elite.Damage})");
        var hard = First(BattleDifficulty.Hard, 1);
        check(hard.MaxHp == 78 && hard.Damage == 17, $"…on top of difficulty: Hard 65 HP × 1.2 = 78, 14 damage × 1.2 = 17 ({hard.MaxHp}/{hard.Damage})");
        var sim = new BattleSim(Settings(Fighter(buff)), 0, 1600, stage: Stage(1, "slime"), seed: 1);
        Run(sim, 6, () => 100);
        check(sim.Slimes[0].MaxHp == 60 && sim.Slimes[0].Damage == 12, "The buff is never applied again while the battle goes on");
    }

    private static void CooldownChecks(Action<bool, string> check)
    {
        double Wait(EliteSpec? elite, double chance, out double windup)
        {
            var sim = new BattleSim(Settings(Fighter(elite)), 0, 1600, stage: Stage(chance, "slime"), seed: 1);
            double first = double.NaN, second = double.NaN;
            Run(sim, 1);
            double x = sim.Slimes[0].X;
            Run(sim, 8, () => x, () =>
            {
                if (!sim.Events.Any(e => e.Kind == BattleEventKind.CatHurt)) return;
                if (double.IsNaN(first)) first = sim.Time; else if (double.IsNaN(second)) second = sim.Time;
            });
            windup = sim.Windup(sim.Slimes[0]);
            return second - first;
        }
        var quick = new EliteSpec { AttackCooldown = 0.65, DescVi = "a", DescEn = "a" };
        double plain = Wait(quick, 0, out double plainWindup), elite = Wait(quick, 1, out double eliteWindup);
        // From one hit to the next: the wait (1.8 s or 1.17 s) and the 0.8 s windup.
        check(Math.Abs(plain - 2.6) < 0.05 && Math.Abs(elite - (1.8 * 0.65 + 0.8)) < 0.05,
            $"An elite's wait between attacks × 0.65, the windup stays (hit to hit: {plain:0.00} s → {elite:0.00} s)");
        check(plainWindup == 0.8 && eliteWindup == 0.8, "A shorter attack cooldown leaves the windup (the warning) as it is");
    }

    private static void HealChecks(Action<bool, string> check)
    {
        EnemyType Healer(EliteSpec? elite) => new()
        {
            Id = "mender", MaxHp = 40, Speed = 0, Damage = 0, Range = 56, Windup = 0.8, AttackCooldown = 2, Support = true, KeepBack = 0,
            HealAmount = 10, HealRange = 2000, HealCooldown = 1, HealWindup = 0.2, HealBudget = 100, Elite = elite
        };
        var tuned = new EliteSpec { HealCooldown = 0.85, HealBudget = 1.3, DescVi = "a", DescEn = "a" };
        (Slime Healer, List<double> Heals, List<double> Gaps) Play(BattleDifficulty level, double chance)
        {
            var heals = new List<double>();
            var times = new List<double>();
            var sim = new BattleSim(Settings(new EnemyType { Id = "wall", MaxHp = 5000, Speed = 0, Damage = 0, Range = 64, Windup = 9, AttackCooldown = 9 }, Healer(tuned)), 0, 1600,
                difficulty: level, stage: Stage(chance, "wall", "mender"), seed: 1);
            Run(sim, 0.3);
            var wall = sim.Slimes.First(s => s.Type.Id == "wall");
            wall.Hp = 100;
            Run(sim, 5, each: () =>
            {
                foreach (var e in sim.Events.Where(e => e.Kind == BattleEventKind.EnemyHealed)) { heals.Add(e.Value); times.Add(sim.Time); }
                wall.Hp = Math.Min(wall.Hp, 100);
            });
            return (sim.Slimes.First(s => s.Type.Support), heals, times.Zip(times.Skip(1), (a, b) => b - a).ToList());
        }
        var plain = Play(BattleDifficulty.Normal, 0);
        var elite = Play(BattleDifficulty.Normal, 1);
        // Heal to heal: the cooldown (1 s or 0.85 s) and the 0.2 s windup.
        check(plain.Healer.Elite == null && elite.Healer.Elite != null && plain.Gaps.Count > 1 && elite.Gaps.Count > 1
            && plain.Gaps.All(g => Math.Abs(g - 1.2) < 0.05) && elite.Gaps.All(g => Math.Abs(g - 1.05) < 0.05),
            $"An elite herb slime heals sooner (heal to heal {plain.Gaps.Average():0.00} s → {elite.Gaps.Average():0.00} s)");
        check(elite.Heals.All(h => h == 10) && elite.Healer.HealPower == 1,
            "…each heal stays as strong (10), HealPower is not touched");
        double spent = elite.Heals.Sum();
        check(Math.Abs(elite.Healer.HealLeft + spent - 130) < 1e-9, $"…and its heal budget is × 1.3 (100 → {elite.Healer.HealLeft + spent})");
        var hard = Play(BattleDifficulty.Hard, 1);
        double hardSpent = hard.Heals.Sum();
        check(hard.Healer.HealPower == 1.3 && Math.Abs(hard.Healer.HealLeft + hardSpent - 169) < 1e-9 && hard.Heals.All(h => h == 13),
            $"On Hard the budget grows with HP (130) and then × 1.3 once (169), each heal 13 as before ({hard.Healer.HealLeft + hardSpent})");
    }

    private static void PushChecks(Action<bool, string> check)
    {
        EnemyType Rock(bool immune) => new()
        {
            Id = "rock", MaxHp = 150, Speed = 0, Damage = 0, Range = 72, Windup = 1.5, AttackCooldown = 2.4, Recover = 0.5, Scale = 1.35,
            Elite = new EliteSpec { Hp = 1.3, PushImmune = immune, DescVi = "a", DescEn = "a" }
        };
        // An elite rock winding up: the Push is spent, but it neither moves, is stunned nor stops.
        var sim = new BattleSim(Settings(Rock(true)), 0, 1600, stage: Stage(1, "rock"), seed: 1);
        Run(sim, 1, () => 100);
        var rock = sim.Slimes[0];
        double x = rock.X;
        Run(sim, 3, () => x + 40, () => { });
        for (int i = 0; i < 600 && rock.State != SlimeState.Windup; i++) { sim.Step(dt, x + 40, false); sim.ClearEvents(); }
        bool windingUp = rock.State == SlimeState.Windup;
        bool used = sim.UsePush(rock.Id, x + 40);
        bool resisted = sim.Events.Any(e => e.Kind == BattleEventKind.PushResisted && e.Value == rock.Id) && !sim.Events.Any(e => e.Kind == BattleEventKind.Pushed);
        check(windingUp && used && rock.State == SlimeState.Windup && rock.StunRemaining == 0 && rock.KnockVelocity == 0 && sim.PushCooldown == sim.Settings.PushCooldown && resisted,
            "An elite rock ignores Push: no knock back, no stun, its windup goes on; the skill is spent and the battle says so");
        Run(sim, 0.3, () => x + 40);
        check(rock.X == x, "…and it has not moved");

        // The same kind without the immunity, and the immune kind when it is not elite, are pushed as usual.
        var plain = new BattleSim(Settings(Rock(true)), 0, 1600, stage: Stage(0, "rock"), seed: 1);
        Run(plain, 1, () => 100);
        var mortal = new BattleSim(Settings(Rock(false)), 0, 1600, stage: Stage(1, "rock"), seed: 1);
        Run(mortal, 1, () => 100);
        check(plain.UsePush(plain.Slimes[0].Id, 100) && plain.Slimes[0].State == SlimeState.Stunned && mortal.UsePush(mortal.Slimes[0].Id, 100) && mortal.Slimes[0].State == SlimeState.Stunned,
            "Only an elite with pushImmune ignores Push (a plain rock and an elite without it are pushed and stunned)");

        // The cat's combos (and a heavy weapon's knock) move it neither; it still takes the damage.
        (double Moved, double Lost, int Combos) Swings(bool immune)
        {
            var settings = new BattleSettings
            {
                FirstWaveDelay = 0, SpawnInterval = 0.2, CatDamage = 1, ComboKnock = 300, CatMaxHp = 1_000_000, MaxAlive = 3, MaxElites = 1, Enemies = new[] { Rock(immune) }
            };
            var fight = new BattleSim(settings, 0, 1600, stage: Stage(1, "rock"), seed: 1);
            Run(fight, 1, () => 100);
            var stone = fight.Slimes[0];
            double start = stone.X, hp = stone.Hp, most = 0;
            Run(fight, 6, () => stone.X - stone.Facing * -50, () => most = Math.Max(most, Math.Abs(stone.X - start)));
            return (most, hp - stone.Hp, fight.CombosLanded);
        }
        var firm = Swings(true);
        var loose = Swings(false);
        check(firm.Combos > 0 && firm.Moved == 0 && firm.Lost > 0 && loose.Combos > 0 && loose.Moved > 5,
            $"An elite rock that ignores Push is not knocked back by combos either, yet takes the damage (moved {firm.Moved:0} vs {loose.Moved:0} DIPs without the immunity)");
    }

    private static string Embedded()
    {
        using var stream = typeof(BattleData).Assembly.GetManifestResourceStream("DesktopPet.battle.json")!;
        return new StreamReader(stream).ReadToEnd();
    }

    private static void DataChecks(Action<bool, string> check)
    {
        string builtIn = Embedded();
        string Bad(string elite, string id = "slime")
        {
            try { BattleData.ParseWithOverride(builtIn, "{ \"schemaVersion\": 1, \"enemies\": [ { \"id\": \"" + id + "\", \"elite\": { " + elite + "\"descVi\": \"a\", \"descEn\": \"a\" } } ] }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        check(Bad("\"pushImmune\": true, ") == "" && Bad("\"hp\": 1.5, ") == "" && Bad("\"attackCooldown\": 0.5, ") == "", "An elite affix may be only stats or only pushImmune");
        check(Bad("\"hp\": 0.8, ").Contains("elite.hp") && Bad("\"damage\": 4, ").Contains("elite.damage") && Bad("\"attackCooldown\": 1.2, ").Contains("elite.attackCooldown"),
            "Elite stats out of range are rejected (hp/damage 1–3, attackCooldown 0.2–1)");
        check(Bad("\"healBudget\": 1.3, ").Contains("heals") && Bad("\"healCooldown\": 0.8, ").Contains("heals") && Bad("\"healBudget\": 1.3, ", "mender") == "",
            "healCooldown/healBudget are only for an enemy that heals");
        check(Bad("\"hp\": 1, \"damage\": 1, \"attackCooldown\": 1, ").Contains("changes nothing"), "An elite affix of only ×1 stats changes nothing and is rejected");
    }

    // The shipped numbers (2026-10-21): the base buff and the elite affixes as agreed.
    private static void ShippedChecks(Action<bool, string> check)
    {
        var data = BattleData.Parse(Embedded());
        EnemyType E(string id) => data.Enemies!.First(e => e.Id == id);
        check(E("slime") is { MaxHp: 58, Damage: 12 } && E("runner") is { MaxHp: 28, Damage: 7 } && E("rock") is { MaxHp: 188, Damage: 18 }
            && E("mender") is { MaxHp: 46, HealCooldown: 3.15 } && E("king_c1") is { MaxHp: 720, Damage: 30 } && E("king") is { MaxHp: 480, Damage: 30 },
            "Shipped base buff: slime 58/12, quick 28/7, rock 188/18, herb 46 HP and 3.15 s heals, Kings 720 and 480 HP with 30 damage");
        check(E("slime").Elite is { Hp: 1.2, AttackCooldown: 0.65, Damage: 1 } && E("runner").Elite is { Damage: 1.2, Hp: 1, WindupAdd: 0.3, DashSpeed: 1.4 }
            && E("rock").Elite is { Hp: 1.3, PushImmune: true, SureBlocks: 1 } && E("mender").Elite is { HealCooldown: 0.85, HealBudget: 1.3, FirstHeal: 2, Hp: 1 },
            "Shipped elites: slime +20 % HP and −35 % wait, quick +20 % damage, rock +30 % HP and no Push, herb −15 % heal cooldown and +30 % budget; old traits kept");
        check(E("rock").Elite!.Tag(false) == "MIỄN ĐẨY" && E("slime").Elite!.Tag(true) == "QUICK HITS"
            && E("runner").Elite!.Tag(false) == "ĐÒN MẠNH" && E("mender").Elite!.Tag(false) == "HỒI MẠNH",
            "Each shipped elite carries a label of its main trait over its head (rock: no Push; slime: quick hits; quick: hard hits; herb: big heals)");
        check(data.Enemies!.Count(e => e.Elite?.PushImmune == true) == 1 && data.MaxElites == 1, "Only the rock elite ignores Push; still one elite at a time");
        var stages = data.Campaign!.Chapters[0].Stages;
        check(stages.Select(s => s.RulesVersion).Zip(new[] { 5, 11, 13, 14, 14 }).All(p => p.First >= p.Second) && data.Expedition!.RulesVersion >= 5,
            "The buff raised every Adventure stage's rules version and the Expedition's (old records stay apart; later changes only raise them)");
    }
}
