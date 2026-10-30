using DesktopPet;

// 1.5 M15 (PetBattle.Wall.cs), with stand-ins that carry the shipped ice, cling and throwHit numbers. The
// ice slime: its shell turns the cat's weapon into scratches (the cat goes for others first) while bombs
// go through; the thrown cat breaks it fast enough, slower when the spray has it wet; it grows back. The
// wall climber: it goes to the nearest edge and climbs it (out of the floor's reach), aims at the cat's
// spot and drops spikes that hurt the cat on the floor only; the thrown cat, Push and the Grabber knock
// it off, it lies dazed, fights on the floor a while and climbs again. The data limits, the shipped kinds
// (kept out of every battle for now) and the try-out file.
internal static class BattleWallTests
{
    const double dt = 1.0 / 60;

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static EnemyType Icy(double damage = 10) => new()
    {
        Id = "icy", MaxHp = 300, Speed = 40, Damage = damage, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3,
        Elite = new EliteSpec { Hp = 1.2 }, Ice = Shipped.Enemy("icy").Ice, ThrowHit = Shipped.Enemy("icy").ThrowHit
    };
    private static EnemyType Clinger(double damage = 10) => new()
    {
        Id = "clinger", MaxHp = 300, Speed = 40, Damage = damage, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3,
        Elite = new EliteSpec { Hp = 1.2 }, Cling = Shipped.Enemy("clinger").Cling, ThrowHit = Shipped.Enemy("clinger").ThrowHit
    };
    private static EnemyType Plain() => new() { Id = "slime", MaxHp = 300, Speed = 40, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3 };

    private static readonly BattleSkill[] Tools = { BattleSkill.Bomb, BattleSkill.Shield, BattleSkill.Push, BattleSkill.Grabber, BattleSkill.Heal };

    // `count` enemies of each kind on a 1600-wide field, the cat at 100 (weapon damage `hit`, reach 60).
    private static BattleSim Field(EnemyType[] kinds, double hit = 0, int count = 1, bool elite = false)
    {
        var arms = Shipped.Weapon("sword") with { Range = 60, StunChance = 0, Damage = hit };
        var settings = new BattleSettings
        {
            Weapons = new[] { arms }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1000, MaxAlive = 8, MaxElites = 8,
            Enemies = kinds, RageHitGain = 0, BombDamage = 10, BombCooldown = 0.1, BombDelay = 0.3, BombRadius = 40,
            PushCooldown = 0.1, GrabberCooldown = 0.1, ShieldCooldown = 0.1,
            Plan = new[] { new WaveSpec { Groups = kinds.Select(k => new WaveGroup { Enemy = k.Id, Count = count, Elite = elite }).ToArray() },
                new WaveSpec { Groups = new[] { new WaveGroup { Enemy = kinds[0].Id, Count = 1 } } } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 4, skills: Tools);
        int all = kinds.Length * count;
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < all; i++) sim.Step(dt, 100, false);
        sim.ClearEvents();
        return sim;
    }
    private static BattleSim Field(EnemyType kind, double hit = 0, int count = 1, bool elite = false) => Field(new[] { kind }, hit, count, elite);

    // Steps `seconds` with the cat at catX (or up to the step an event of `kind` came: then true).
    private static bool Run(BattleSim sim, double seconds, double catX = 100, BattleEventKind? kind = null, bool held = false, CatPose? pose = null, Action? each = null)
    {
        for (int i = 0; i < Math.Round(seconds / dt); i++)
        {
            each?.Invoke();
            sim.Step(dt, catX, held, held, held ? pose : null);
            bool seen = kind is BattleEventKind k && sim.Events.Any(e => e.Kind == k);
            sim.ClearEvents();
            if (seen) return true;
            if (sim.Over) break;
        }
        return false;
    }

    // The thrown cat flying from one pose to the next, then landing at the last x.
    private static void Throw(BattleSim sim, CatPose from, CatPose to, HashSet<BattleEventKind>? seen = null)
    {
        foreach (var pose in new[] { from, to })
        {
            sim.Step(dt, pose.X, true, true, pose);
            if (seen != null) foreach (var e in sim.Events) seen.Add(e.Kind);
            sim.ClearEvents();
        }
        sim.Step(dt, to.X, false, false, null);
        if (seen != null) foreach (var e in sim.Events) seen.Add(e.Kind);
        sim.ClearEvents();
    }
    // Fast along the floor through x, left to right.
    private static void ThrowThrough(BattleSim sim, double x, double speed, HashSet<BattleEventKind>? seen = null, double height = 20) =>
        Throw(sim, new CatPose(x - 100, height, speed, 0, false, true), new CatPose(x + 100, height, speed, 0, false, true), seen);

    public static void Run(Action<bool, string> check)
    {
        IceChecks(check);
        ClingChecks(check);
        KnockChecks(check);
        DataChecks(check);
    }

    private static void IceChecks(Action<bool, string> check)
    {
        var ice = Shipped.Enemy("icy").Ice!;
        var rule = Shipped.Enemy("icy").ThrowHit!;
        // The cat's weapon only scratches the shell.
        var sim = Field(Icy(), hit: 20);
        var s = sim.Slimes.Single();
        check(s.Iced, "An ice slime comes in with its shell on");
        s.X = 160;
        double before = s.Hp;
        Run(sim, 4, each: () => s.ShotClock = 0);
        double lost = before - s.Hp;
        check(sim.IceScratches > 0 && lost > 0 && lost <= sim.IceScratches * 20 * ice.Armor * 2.5 + 1e-6,
            $"The cat's blows only scratch it ({sim.IceScratches} blows, lost {lost:0.#})");
        // A bomb goes through.
        before = s.Hp;
        sim.Use(BattleSkill.Bomb, s.Id, 100);
        Run(sim, 0.5, catX: 1500);
        check(Math.Abs(before - s.Hp - 10) < 1e-6, $"A yarn bomb goes through the shell (lost {before - s.Hp:0.#})");

        // A slow throw: nothing; a fast one breaks it (its throwHit too).
        sim = Field(Icy());
        s = sim.Slimes.Single();
        s.X = 800;
        before = s.Hp;
        ThrowThrough(sim, 800, rule.MinSpeed * 0.8);
        check(s.Iced && s.Hp == before && sim.IceBreaks == 0, "A throw below its minSpeed does nothing");
        var seen = new HashSet<BattleEventKind>();
        ThrowThrough(sim, 800, rule.MinSpeed * 1.2, seen);
        check(!s.Iced && sim.IceBreaks == 1 && seen.Contains(BattleEventKind.IceShattered) && before - s.Hp == rule.Damage && s.State == SlimeState.Stunned,
            $"A fast throw breaks the shell, hurts and dazes it (lost {before - s.Hp:0.#})");
        // Broken: the cat's weapon hits in full.
        check(!sim.Slimes.Any(x => x.Iced), "Broken, the shell is off");

        // Wet from the spray: a slower throw is enough.
        sim = Field(Icy());
        s = sim.Slimes.Single();
        s.X = 800;
        s.WetRemaining = 3;
        ThrowThrough(sim, 800, (ice.WetSpeed + rule.MinSpeed) / 2);
        check(!s.Iced && sim.IceBreaks == 1, "Wet from the spray, a slower throw breaks the shell");
        check(BattleSim.ThrowSpeed(s, rule) == rule.MinSpeed, "Without its shell the throw rule's own speed counts again");

        // It grows back (an elite's sooner).
        bool back = Run(sim, ice.Regrow + 0.5, catX: 1500, kind: BattleEventKind.IceRegrown);
        check(back && s.Iced, $"The shell grows back after {ice.Regrow} s");
        sim = Field(Icy(), elite: true);
        s = sim.Slimes.Single();
        s.X = 800;
        ThrowThrough(sim, 800, rule.MinSpeed * 1.2);
        double grew = 0;
        Run(sim, ice.Regrow, catX: 1500, kind: BattleEventKind.IceRegrown, each: () => grew += dt);
        check(s.Elite != null && s.Iced && grew <= ice.EliteRegrow + 0.2, $"An elite's shell grows back sooner ({grew:0.0} s)");

        // The cat goes for another slime first while the shell is on.
        sim = Field(new[] { Icy(), Plain() });
        var icy = sim.Slimes.Single(x => x.Type.Id == "icy");
        var plain = sim.Slimes.Single(x => x.Type.Id == "slime");
        icy.X = 200; plain.X = 400;
        Run(sim, 0.2);
        check(sim.TargetId == plain.Id, "With its shell on, the cat goes for another slime first");
        // Pause holds the regrowth.
        icy.Iced = false; icy.IceClock = 1;
        sim.Paused = true;
        Run(sim, 3);
        check(!icy.Iced, "Pause holds its regrowth");
    }

    private static void ClingChecks(Action<bool, string> check)
    {
        var c = Shipped.Enemy("clinger").Cling!;
        var sim = Field(Clinger());
        var s = sim.Slimes.Single();
        bool up = Run(sim, 30, catX: 100, kind: BattleEventKind.ClingClimbed);
        bool edge = Math.Abs(s.X - (s.WallSide < 0 ? sim.Left + s.Width * 0.35 : sim.Right - s.Width * 0.35)) < 1e-6;
        check(up && s.OnWall && s.Cling == ClingPhase.Clinging && edge && Math.Abs(s.Altitude - c.Height) < 1e-6,
            $"It goes to the nearest edge and climbs it (side {s.WallSide}, x {s.X:0}, height {s.Altitude:0})");
        check(s.Airborne && sim.TargetId != s.Id && !sim.Select(s.Id), "Up its edge the cat on the floor neither goes for it nor can choose it");

        // It aims at the cat's spot, then a spike hurts the cat on the floor (no stun).
        double hp = sim.CatHp;
        s.ShotClock = 0.05;
        bool aimed = false;
        Run(sim, 2, catX: 700, each: () => aimed |= s.Aiming && sim.SpikeTargets(s).Any(x => Math.Abs(x - 700) < 1));
        check(aimed && sim.CatHp < hp && sim.SpikeHits == 1 && !sim.CatStunned, $"It aims at the cat's spot and a spike hurts the cat on the floor, no stun ({hp - sim.CatHp:0}))");
        // Out of the ring: missed.
        hp = sim.CatHp;
        s.ShotClock = 0.05;
        Run(sim, c.Aim + 0.15, catX: 700);
        Run(sim, c.Flight + 0.2, catX: 700 + c.Radius + 80, each: () => s.ShotClock = 99);
        check(sim.CatHp == hp && sim.SpikeHits == 1, "Stepping out of the ring dodges the spike");
        // Lifted: never hit.
        hp = sim.CatHp;
        s.ShotClock = 0.05;
        Run(sim, c.Aim + c.Flight + 0.3, catX: 700, held: true, pose: new CatPose(700, 200, 0, 0, true, false), each: () => { if (!s.Aiming && sim.Goos.Count > 0) s.ShotClock = 99; });
        check(sim.CatHp == hp, "A lifted cat is never hit by a spike");

        // An elite drops two spikes round the cat's spot.
        sim = Field(Clinger(), elite: true);
        s = sim.Slimes.Single();
        Run(sim, 30, kind: BattleEventKind.ClingClimbed);
        s.ShotClock = 0.01;
        Run(sim, 0.1, catX: 700);
        var spots = sim.SpikeTargets(s).ToArray();
        check(s.Elite != null && spots.Length == c.EliteSpikes && Math.Abs(spots.Max() - spots.Min() - c.EliteSpread) < 1e-6, $"An elite drops {c.EliteSpikes} spikes {c.EliteSpread} apart");

        // A second one on the same edge climbs higher.
        sim = Field(Clinger(), count: 2);
        var pair = sim.Slimes.ToArray();
        foreach (var p in pair) p.X = 1200;
        Run(sim, 30, catX: 100, each: () => { });
        check(pair.All(p => p.OnWall && p.Cling == ClingPhase.Clinging) && Math.Abs(Math.Abs(pair[0].Altitude - pair[1].Altitude) - c.Spacing) < 1e-6,
            $"A second one on the same edge clings {c.Spacing} higher ({pair[0].Altitude:0}, {pair[1].Altitude:0})");

        // Pause and the frozen guide hold it still.
        sim = Field(Clinger());
        s = sim.Slimes.Single();
        Run(sim, 30, kind: BattleEventKind.ClingClimbed);
        s.ShotClock = 0.01;
        sim.Paused = true;
        Run(sim, 1);
        bool still = !s.Aiming;
        sim.Paused = false;
        for (int i = 0; i < 30; i++) sim.Step(0, 100, false);
        check(still && !s.Aiming, "Pause and the frozen guide hold it still");
    }

    // A clinger up its edge, ready to be knocked off.
    private static (BattleSim, Slime) OnEdge()
    {
        var sim = Field(Clinger());
        var s = sim.Slimes.Single();
        Run(sim, 30, kind: BattleEventKind.ClingClimbed);
        s.ShotClock = 99;
        return (sim, s);
    }

    private static void KnockChecks(Action<bool, string> check)
    {
        var c = Shipped.Enemy("clinger").Cling!;
        var rule = Shipped.Enemy("clinger").ThrowHit!;
        // The thrown cat flying into it knocks it off: it falls, lies dazed, then climbs again.
        var (sim, s) = OnEdge();
        double hp = s.Hp, h = s.Altitude + s.Height / 2;
        var seen = new HashSet<BattleEventKind>();
        double towards = s.WallSide;
        Throw(sim, new CatPose(s.X - towards * 160, h, towards * rule.MinSpeed * 1.5, 0, false, true), new CatPose(s.X - towards * 10, h, towards * rule.MinSpeed * 1.5, 0, false, true), seen);
        check(seen.Contains(BattleEventKind.ClingKnocked) && s.Cling is ClingPhase.Falling or ClingPhase.Ground && hp - s.Hp == rule.Damage,
            $"The thrown cat flying into it knocks it off its edge (lost {hp - s.Hp:0})");
        bool landed = Run(sim, 2, catX: 800, kind: BattleEventKind.ClingLanded);
        check(landed && !s.Airborne && s.State == SlimeState.Stunned && s.StunRemaining >= c.Downed - 0.05, "It lands on the floor dazed");
        Run(sim, 0.1, catX: 800);
        check(sim.TargetId == s.Id, "On the floor the cat goes for it");
        bool back = Run(sim, c.Downed + c.Ground + 30, catX: 800, kind: BattleEventKind.ClingClimbed);
        check(back && s.OnWall, "After a while on the floor it climbs again");

        // Too slow, or high over it: nothing.
        (sim, s) = OnEdge();
        towards = s.WallSide;
        h = s.Altitude + s.Height / 2;
        Throw(sim, new CatPose(s.X - towards * 160, h, towards * rule.MinSpeed * 0.6, 0, false, true), new CatPose(s.X - towards * 10, h, towards * rule.MinSpeed * 0.6, 0, false, true));
        Throw(sim, new CatPose(s.X - towards * 160, h + 150, towards * rule.MinSpeed * 2, 0, false, true), new CatPose(s.X - towards * 10, h + 150, towards * rule.MinSpeed * 2, 0, false, true));
        check(s.OnWall && sim.ClingKnocks == 0, "A slow throw, or one high over it, leaves it up its edge");

        // Push knocks it off.
        (sim, s) = OnEdge();
        check(sim.Use(BattleSkill.Push, s.Id, 100) && Run(sim, 2, kind: BattleEventKind.ClingLanded) && !s.Airborne, "Push knocks it off its edge");
        // The Grabber yanks it off at once and is spent.
        (sim, s) = OnEdge();
        check(sim.Arm(BattleSkill.Grabber) && sim.Grab(s.Id, s.X, s.Altitude) && !sim.Grabbing && sim.Cooldown(BattleSkill.Grabber) > 0 && s.Cling == ClingPhase.Falling,
            "The Grabber yanks it off at once (not held in the claw) and is spent");
        // The held cat swings at it (it is in the air).
        var shipped = Field(Clinger(), hit: 5);
        var t = shipped.Slimes.Single();
        Run(shipped, 30, kind: BattleEventKind.ClingClimbed);
        t.ShotClock = 99;
        hp = t.Hp;
        Run(shipped, 2, held: true, pose: new CatPose(t.X - t.WallSide * 45, t.Altitude + t.Height / 2, 0, 0, true, false), each: () => t.ShotClock = 99);
        check(t.Hp < hp && shipped.HeldHits > 0, "The cat in the user's hand swings at it up its edge");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        const string slime = "{\"id\":\"slime\",\"nameVi\":\"S\",\"nameEn\":\"S\"}";
        const string thrown = "\"throwHit\":{\"minSpeed\":600,\"damage\":5}";
        static string Enemy(string id, string extra) => $"{{\"id\":\"{id}\",\"nameVi\":\"T\",\"nameEn\":\"T\",{extra}}}";
        bool Rejects(string enemies)
        {
            try { BattleData.Parse("{\"enemies\":[" + slime + "," + enemies + "],\"waves\":[{\"groups\":[{\"enemy\":\"a\",\"count\":1}]}]}"); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(!Rejects(Enemy("a", "\"ice\":{}," + thrown)) && !Rejects(Enemy("a", "\"cling\":{}," + thrown)), "An ice slime and a wall climber within their limits are accepted");
        check(Rejects(Enemy("a", "\"ice\":{}")) && Rejects(Enemy("a", "\"cling\":{}")), "Without a throwHit they are refused");
        check(Rejects(Enemy("a", "\"ice\":{},\"cling\":{}," + thrown)) && Rejects(Enemy("a", "\"ice\":{},\"fly\":{}," + thrown)) && Rejects(Enemy("a", "\"boss\":true,\"cling\":{}," + thrown)),
            "Ice and cling together, with flying, or on a boss are refused");
        check(Rejects(Enemy("a", "\"ice\":{\"wetSpeed\":900}," + thrown)) && Rejects(Enemy("a", "\"ice\":{\"armor\":2}," + thrown)) && Rejects(Enemy("a", "\"cling\":{\"height\":300}," + thrown)),
            "Numbers out of range (a wet speed over the dry one, a climb over the strip) are refused");

        var s = Shipped;
        check(s.Enemy("icy") is { Ice: not null, ThrowHit: not null, BinSpit: false, Elite: not null } && s.Enemy("clinger") is { Cling: not null, ThrowHit: not null, BinSpit: false, Elite: not null },
            "The two shipped kinds, each with an elite and a throw rule, never spat by a bin");
        var kinds = new[] { "icy", "clinger" };
        bool Uses(WaveGroup g) => kinds.Contains(g.Enemy) || g.Pick?.Keys.Any(kinds.Contains) == true || g.Squad?.Any(m => kinds.Contains(m.Enemy)) == true;
        var all = s.WavePlan.Concat(s.Campaign?.Chapters.Take(1).SelectMany(ch => ch.Stages).SelectMany(st => st.Waves) ?? Enumerable.Empty<WaveSpec>());
        check(!all.SelectMany(w => w.Groups).Any(Uses) && !s.FormationList.SelectMany(f => f.Members).Any(kinds.Contains), "No built-in battle uses them yet outside chapter 2 (1.5 M21 brings it in)");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m15.json"))) dir = dir.Parent;
        var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", "battle-m15.json"));
        check(load is { Custom: true, Error: null } && load.Settings.WavePlan.SelectMany(w => w.Groups).Count(Uses) >= 4,
            $"docs/examples/battle-m15.json loads as custom data with both kinds ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
    }
}
