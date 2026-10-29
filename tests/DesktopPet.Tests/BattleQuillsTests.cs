using DesktopPet;

// 1.5 M16 (PetBattle.Quills.cs), with stand-ins that carry the shipped quills, clock and steal numbers. The
// hedgehog: its quills lie flat, rise and stand up in turn; up, the cat's weapon half hurts it and pricks
// the cat (the Shield stops it, a bomb goes through, the cat goes for others first), wet they lie flat. The
// timer: its clock runs (slower while it is dazed), run out it calls more slimes (the wave waits for them),
// a few times at most; defeated it calls no more. The thief: its windup steals a ready skill (or an app's
// charge), it runs to the far edge and gets away after a while there; defeated first it gives it back;
// nothing to steal, it only trails the cat; Push cuts its windup. The data limits, the shipped kinds (kept
// out of every battle for now) and the try-out file.
internal static class BattleQuillsTests
{
    const double dt = 1.0 / 60;

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static EnemyType Hedgehog(double damage = 0) => new()
    {
        Id = "hedgehog", MaxHp = 300, Speed = 40, Damage = damage, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3,
        Elite = new EliteSpec { Hp = 1.2 }, Quills = Shipped.Enemy("hedgehog").Quills
    };
    private static readonly ClockSpec ShippedClock = Shipped.Enemy("timer").Clock!;
    private static EnemyType Timer() => new()
    {
        Id = "timer", MaxHp = 300, Speed = 40, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3,
        Elite = new EliteSpec { Hp = 1.2 },
        Clock = new ClockSpec { Seconds = ShippedClock.Seconds, EliteSeconds = ShippedClock.EliteSeconds, Stunned = ShippedClock.Stunned, Calls = ShippedClock.Calls, Call = new[] { "slime", "slime" } }
    };
    private static EnemyType Thief(double hp = 300) => new()
    {
        Id = "thief", MaxHp = hp, Speed = 60, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3,
        Elite = new EliteSpec { Hp = 1.2 }, Steal = Shipped.Enemy("thief").Steal
    };
    private static EnemyType Plain() => new() { Id = "slime", MaxHp = 300, Speed = 40, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3 };

    private static readonly BattleSkill[] Tools = { BattleSkill.Bomb, BattleSkill.Shield, BattleSkill.Push, BattleSkill.Cheer, BattleSkill.Heal };

    // `count` of each kind on a 1600-wide field, the cat at 100 (weapon damage `hit`, reach 60); `extra`
    // kinds are known but in no wave. cooldown: every skill's. apps: a stage battle with the desktop.
    private static BattleSim Field(EnemyType[] kinds, double hit = 0, int count = 1, bool elite = false, EnemyType[]? extra = null, double cooldown = 0.1, bool apps = false)
    {
        var arms = Shipped.Weapon("sword") with { Range = 60, StunChance = 0, Damage = hit };
        var settings = new BattleSettings
        {
            Weapons = new[] { arms }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1000, MaxAlive = 8, MaxElites = 8,
            Enemies = kinds.Concat(extra ?? Array.Empty<EnemyType>()).ToArray(), RageHitGain = 0, BombDamage = 10, BombCooldown = cooldown / 2, BombDelay = 0.3, BombRadius = 40,
            PushCooldown = cooldown, ShieldCooldown = cooldown, CheerCooldown = cooldown, HealCooldown = cooldown, Apps = apps ? new AppsSpec() : null,
            Plan = new[] { new WaveSpec { Groups = kinds.Select(k => new WaveGroup { Enemy = k.Id, Count = count, Elite = elite }).ToArray() },
                new WaveSpec { Groups = new[] { new WaveGroup { Enemy = kinds[0].Id, Count = 1 } } } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 4, skills: Tools, startHp: 900, stage: apps ? new StageSpec { Id = "quills_test", Malware = false } : null);
        int all = kinds.Length * count;
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < all; i++) sim.Step(dt, 100, false);
        sim.ClearEvents();
        return sim;
    }
    private static BattleSim Field(EnemyType kind, double hit = 0, bool elite = false) => Field(new[] { kind }, hit, elite: elite);

    // Steps `seconds` with the cat at catX (or up to the step an event of `kind` came: then true).
    private static bool Run(BattleSim sim, double seconds, double catX = 100, BattleEventKind? kind = null, bool held = false, Action? each = null)
    {
        for (int i = 0; i < Math.Round(seconds / dt); i++)
        {
            each?.Invoke();
            sim.Step(dt, catX, held, held, held ? new CatPose(catX, 200, 0, 0, true, false) : null);
            bool seen = kind is BattleEventKind k && sim.Events.Any(e => e.Kind == k);
            sim.ClearEvents();
            if (seen) return true;
            if (sim.Over) break;
        }
        return false;
    }

    public static void Run(Action<bool, string> check)
    {
        QuillChecks(check);
        ClockChecks(check);
        ThiefChecks(check);
        DataChecks(check);
    }

    private static void QuillChecks(Action<bool, string> check)
    {
        var q = Shipped.Enemy("hedgehog").Quills!;
        // Its round: flat, rising, up, flat again.
        var sim = Field(Hedgehog());
        var s = sim.Slimes.Single();
        s.X = 1200;
        check(s.Quill == QuillPhase.Flat, "A hedgehog comes in with its quills flat");
        double t = 0;
        bool rising = Run(sim, q.Flat + 1, catX: 100, kind: BattleEventKind.QuillsRising, each: () => t += dt);
        check(rising && Math.Abs(t - q.Flat) < 0.6, $"Its quills rise after about {q.Flat} s flat ({t:0.0} s)");
        t = 0;
        bool up = Run(sim, q.Warn + 0.5, kind: BattleEventKind.QuillsUp, each: () => t += dt);
        check(up && s.Puffed && Math.Abs(t - q.Warn) < 0.1, $"They stand up {q.Warn} s after rising ({t:0.00} s)");
        t = 0;
        bool down = Run(sim, q.Puffed + 1, kind: BattleEventKind.QuillsDown, each: () => t += dt);
        check(down && !s.Puffed && Math.Abs(t - q.Puffed) < 0.1, $"They lie flat again after {q.Puffed} s ({t:0.00} s)");
        // An elite keeps them up longer.
        sim = Field(Hedgehog(), elite: true);
        s = sim.Slimes.Single();
        s.X = 1200;
        Run(sim, q.Flat + q.Warn + 1, kind: BattleEventKind.QuillsUp);
        t = 0;
        Run(sim, q.ElitePuffed + 1, kind: BattleEventKind.QuillsDown, each: () => t += dt);
        check(s.Elite != null && Math.Abs(t - q.ElitePuffed) < 0.1, $"An elite keeps its quills up {q.ElitePuffed} s ({t:0.00} s)");

        // Up: the cat's weapon half hurts it and pricks the cat.
        sim = Field(Hedgehog(), hit: 20);
        s = sim.Slimes.Single();
        s.X = 150;
        double hp = s.Hp, cat = sim.CatHp;
        Run(sim, 3, each: () => { s.Quill = QuillPhase.Up; s.QuillClock = 99; });
        int pricks = sim.QuillPricks;
        check(pricks > 0 && hp - s.Hp <= pricks * 20 * q.Armor * 2 + 1e-6 && hp - s.Hp < pricks * 20 * 0.8 && Math.Abs(cat - sim.CatHp - pricks * q.Prick) < 1e-6 && !sim.CatStunned,
            $"Quills up: each blow takes half and pricks the cat, no stun ({pricks} blows, slime lost {hp - s.Hp:0}, cat lost {cat - sim.CatHp:0})");
        // Flat: full blows, no prick.
        sim = Field(Hedgehog(), hit: 20);
        s = sim.Slimes.Single();
        s.X = 150;
        hp = s.Hp; cat = sim.CatHp;
        Run(sim, 2, each: () => { s.Quill = QuillPhase.Flat; s.QuillClock = 99; });
        check(sim.QuillPricks == 0 && hp - s.Hp >= 40 && sim.CatHp == cat, $"Quills flat: blows land in full, no prick (lost {hp - s.Hp:0})");
        // The Shield stops the prick; a bomb goes through without one.
        sim = Field(Hedgehog(), hit: 20);
        s = sim.Slimes.Single();
        s.X = 150;
        cat = sim.CatHp;
        sim.Use(BattleSkill.Shield, 100);
        Run(sim, 1.5, each: () => { s.Quill = QuillPhase.Up; s.QuillClock = 99; });
        check(sim.QuillPricks > 0 && sim.CatHp == cat, "The Shield stops the prick");
        sim = Field(Hedgehog());
        s = sim.Slimes.Single();
        s.X = 800; s.Quill = QuillPhase.Up; s.QuillClock = 99;
        hp = s.Hp; cat = sim.CatHp;
        sim.Use(BattleSkill.Bomb, s.Id, 100);
        Run(sim, 0.5, catX: 100);
        check(Math.Abs(hp - s.Hp - 10) < 1e-6 && sim.QuillPricks == 0 && sim.CatHp == cat, $"A bomb goes through the quills in full, no prick (lost {hp - s.Hp:0.#})");
        // Wet from the spray: flat until dry.
        s.WetRemaining = 2;
        bool soaked = Run(sim, 0.1, kind: BattleEventKind.QuillsSoaked);
        Run(sim, 1.5, each: () => s.WetRemaining = Math.Max(s.WetRemaining, 0.5));
        check(soaked && s.Quill == QuillPhase.Flat, "Wet from the spray, its quills lie flat and stay flat");
        // The cat goes for another slime first while its quills are up.
        sim = Field(new[] { Hedgehog(), Plain() });
        var hog = sim.Slimes.Single(x => x.Type.Id == "hedgehog");
        var plain = sim.Slimes.Single(x => x.Type.Id == "slime");
        hog.X = 200; plain.X = 400;
        Run(sim, 0.2, each: () => { hog.Quill = QuillPhase.Up; hog.QuillClock = 99; });
        check(sim.TargetId == plain.Id, "Quills up, the cat goes for another slime first");
        // Pause holds its round.
        hog.Quill = QuillPhase.Flat; hog.QuillClock = 0.5;
        sim.Paused = true;
        Run(sim, 2);
        check(hog.Quill == QuillPhase.Flat && hog.QuillClock == 0.5, "Pause holds its quills' round");
    }

    private static void ClockChecks(Action<bool, string> check)
    {
        var c = ShippedClock;
        var sim = Field(new[] { Timer() }, extra: new[] { Plain() });
        var s = sim.Slimes.Single();
        s.X = 1200;
        double t = 0;
        bool rang = Run(sim, c.Seconds + 2, catX: 100, kind: BattleEventKind.ClockRang, each: () => t += dt);
        check(rang && Math.Abs(t - c.Seconds) < 0.5 && s.ClockCalls == 1, $"Its clock runs out after about {c.Seconds} s ({t:0.0} s)");
        Run(sim, 3);
        var called = sim.Slimes.Where(x => x.Type.Id == "slime").ToList();
        check(called.Count == 2 && called.All(x => x.SummonedBy == BattleSim.CalledBy), $"It calls its slimes, announced at the edge ({called.Count} came)");
        // The wave waits for them: the timer defeated, the called slimes still hold it.
        s.Hp = 0.5;
        sim.Use(BattleSkill.Bomb, s.Id, 100);
        Run(sim, 0.6);
        bool waits = s.State == SlimeState.Defeated && sim.Phase == BattlePhase.Fighting && sim.Wave == 1;
        Run(sim, c.Seconds * 2 + 2);
        check(waits && sim.ClockRings == 1 && sim.Wave == 1, "Defeated, it calls no more, and its called slimes hold the wave");

        // Calls times at most.
        sim = Field(new[] { Timer() }, extra: new[] { Plain() });
        s = sim.Slimes.Single();
        s.X = 1200;
        Run(sim, c.Seconds * (c.Calls + 1) + 5, catX: 100);
        check(sim.ClockRings == c.Calls && s.ClockCalls == c.Calls && sim.Slimes.Count(x => x.Type.Id == "slime") == 2 * c.Calls, $"It calls {c.Calls} times at most ({sim.ClockRings})");

        // Dazed, the clock crawls.
        sim = Field(new[] { Timer() }, extra: new[] { Plain() });
        s = sim.Slimes.Single();
        s.X = 1200;
        Run(sim, 1);
        double left = s.ClockLeft;
        Run(sim, 2, each: () => { if (s.State != SlimeState.Stunned) s.Set(SlimeState.Stunned); s.StunRemaining = 5; });
        check(Math.Abs(left - s.ClockLeft - 2 * c.Stunned) < 0.05, $"Dazed, its clock runs at {c.Stunned:0.0}× ({left - s.ClockLeft:0.00} s in 2 s)");
        // An elite's clock is shorter.
        sim = Field(new[] { Timer() }, elite: true, extra: new[] { Plain() });
        s = sim.Slimes.Single();
        Run(sim, 0.1);
        check(s.Elite != null && s.ClockTotal == c.EliteSeconds, $"An elite's clock is {c.EliteSeconds} s");
        // Pause holds it.
        left = s.ClockLeft;
        sim.Paused = true;
        Run(sim, 2);
        check(s.ClockLeft == left, "Pause holds its clock");
    }

    private static void ThiefChecks(Action<bool, string> check)
    {
        var steal = Shipped.Enemy("thief").Steal!;
        // Its windup next to the cat steals the ready skill with the longest cooldown.
        var sim = Field(new[] { Thief() }, cooldown: 30);
        var s = sim.Slimes.Single();
        double hp = sim.CatHp;
        var expected = sim.Skills.OrderByDescending(sim.SkillCooldown).First();
        s.X = 300;
        bool stole = Run(sim, 20, kind: BattleEventKind.ThiefStole);
        check(stole && s.Loot == StolenKind.Skill && s.LootSkill == expected && sim.Cooldown(expected) > 0 && sim.CatHp == hp,
            $"Its windup steals the ready skill with the longest cooldown ({s.LootSkill}), never hurting the cat");
        // It runs to the edge away from the cat and gets away after a while there.
        double t = 0;
        int defeated = sim.Defeated;
        bool gone = Run(sim, 60, kind: BattleEventKind.ThiefEscaped, each: () => t += dt);
        check(gone && s.Escaped && s.State == SlimeState.Defeated && sim.Defeated == defeated && s.X > 1500 && t >= steal.Linger,
            $"It runs to the far edge and, {steal.Linger} s there, gets away (not a defeat of the cat's; {t:0.0} s)");

        // Defeated first, it gives the skill back.
        sim = Field(new[] { Thief() }, cooldown: 30);
        s = sim.Slimes.Single();
        s.X = 300;
        Run(sim, 20, kind: BattleEventKind.ThiefStole);
        var taken = s.LootSkill;
        s.Hp = 1;
        bool bombed = sim.Use(BattleSkill.Bomb, s.Id, 100);
        bool returned = Run(sim, 2, kind: BattleEventKind.ThiefReturned);
        check(bombed && taken != BattleSkill.Bomb && returned && s.State == SlimeState.Defeated && !s.Escaped && sim.Cooldown(taken) == 0 && sim.TheftsReturned == 1, $"Defeated before it gets away, it gives {taken} back");

        // The edge clock waits while it is dazed.
        sim = Field(new[] { Thief() }, cooldown: 30);
        s = sim.Slimes.Single();
        s.X = 300;
        Run(sim, 20, kind: BattleEventKind.ThiefStole);
        Run(sim, 30, each: () => { if (s.LingerClock > 1) { s.Set(SlimeState.Stunned); s.StunRemaining = 99; } });
        check(!s.Escaped && s.LingerClock is > 1 and < 1.2, $"Dazed at the edge, its time to get away waits ({s.LingerClock:0.00} s)");

        // Push cuts its windup: nothing stolen.
        sim = Field(new[] { Thief() }, cooldown: 30);
        s = sim.Slimes.Single();
        s.X = 300;
        Run(sim, 20, each: () => { if (s.State == SlimeState.Windup && s.StateTime > 0.2) sim.UsePush(s.Id, 100); }, kind: BattleEventKind.Pushed);
        check(s.State == SlimeState.Stunned && s.Loot == StolenKind.None && sim.Thefts == 0, "Push cuts its windup: nothing stolen");

        // Nothing to take: it trails the cat, never winding up.
        sim = Field(new[] { Thief() }, cooldown: 60);
        s = sim.Slimes.Single();
        sim.Use(BattleSkill.Shield, 100); sim.Use(BattleSkill.Cheer, 100); sim.Use(BattleSkill.Heal, 100);
        sim.Use(BattleSkill.Bomb, s.Id, 100); sim.Use(BattleSkill.Push, s.Id, 100);
        bool wound = false;
        Run(sim, 30, each: () => wound |= s.State == SlimeState.Windup);
        check(!wound && sim.Thefts == 0 && Math.Abs(s.X - 100) < 80, $"With nothing to take it only trails the cat (x {s.X:0})");

        // With apps: it drains the most charged clean icon; defeated, the charge goes back.
        sim = Field(new[] { Thief() }, apps: true, cooldown: 30);
        s = sim.Slimes.Single();
        check(sim.AppRules != null && sim.Desktop.Count == 5, "The apps test battle has its desktop");
        var full = sim.Desktop[2];
        foreach (var a in sim.Desktop) a.Charge = a == full ? 0.9 : 0.1;
        s.X = 300;
        Run(sim, 20, kind: BattleEventKind.ThiefStole);
        check(s.Loot == StolenKind.App && s.LootApp == full.Id && full.Charge < 0.1 && s.LootCharge >= 0.9, $"It drains the most charged icon ({s.LootCharge:0.00})");
        s.Hp = 1;
        sim.Use(BattleSkill.Bomb, s.Id, 100);
        Run(sim, 2, kind: BattleEventKind.ThiefReturned);
        check(s.State == SlimeState.Defeated && full.Charge >= 0.9, $"Defeated, the charge goes back to its icon ({full.Charge:0.00})");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        const string slime = "{\"id\":\"slime\",\"nameVi\":\"S\",\"nameEn\":\"S\"}";
        static string Enemy(string id, string extra) => $"{{\"id\":\"{id}\",\"nameVi\":\"T\",\"nameEn\":\"T\",{extra}}}";
        bool Rejects(string enemies)
        {
            try { BattleData.Parse("{\"enemies\":[" + slime + "," + enemies + "],\"waves\":[{\"groups\":[{\"enemy\":\"a\",\"count\":1}]}]}"); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(!Rejects(Enemy("a", "\"quills\":{}")) && !Rejects(Enemy("a", "\"clock\":{\"call\":[\"slime\"]}")) && !Rejects(Enemy("a", "\"steal\":{}")),
            "A hedgehog, a timer and a thief within their limits are accepted");
        check(Rejects(Enemy("a", "\"quills\":{},\"steal\":{}")) && Rejects(Enemy("a", "\"clock\":{\"call\":[\"slime\"]},\"fly\":{}")) && Rejects(Enemy("a", "\"boss\":true,\"steal\":{}")),
            "Two of them together, with flying, or on a boss are refused");
        check(Rejects(Enemy("a", "\"clock\":{\"call\":[\"nobody\"]}")) && Rejects(Enemy("a", "\"clock\":{\"call\":[\"a\"]}")) && Rejects(Enemy("a", "\"clock\":{\"call\":[]}")),
            "A timer calling an unknown enemy, another timer or nobody is refused");
        check(Rejects(Enemy("a", "\"quills\":{\"armor\":2}")) && Rejects(Enemy("a", "\"clock\":{\"calls\":9,\"call\":[\"slime\"]}")) && Rejects(Enemy("a", "\"steal\":{\"linger\":0}")),
            "Numbers out of range are refused");

        var s = Shipped;
        check(s.Enemy("hedgehog") is { Quills: not null, BinSpit: false, Elite: not null } && s.Enemy("timer") is { Clock: not null, BinSpit: false, Elite: not null }
              && s.Enemy("thief") is { Steal: not null, BinSpit: false, Elite: not null, Damage: 0.0 },
            "The three shipped kinds, each with an elite, never spat by a bin (the thief never hurts)");
        var kinds = new[] { "hedgehog", "timer", "thief" };
        bool Uses(WaveGroup g) => kinds.Contains(g.Enemy) || g.Pick?.Keys.Any(kinds.Contains) == true || g.Squad?.Any(m => kinds.Contains(m.Enemy)) == true;
        var all = s.WavePlan.Concat(s.Campaign?.Chapters.SelectMany(ch => ch.Stages).SelectMany(st => st.Waves) ?? Enumerable.Empty<WaveSpec>());
        check(!all.SelectMany(w => w.Groups).Any(Uses) && !s.FormationList.SelectMany(f => f.Members).Any(kinds.Contains), "No built-in battle uses them yet");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m16.json"))) dir = dir.Parent;
        var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", "battle-m16.json"));
        check(load is { Custom: true, Error: null } && kinds.All(k => load.Settings.WavePlan.SelectMany(w => w.Groups).Any(g => g.Enemy == k)),
            $"docs/examples/battle-m16.json loads as custom data with all three kinds ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
    }
}
