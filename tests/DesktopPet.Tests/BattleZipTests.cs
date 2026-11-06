using DesktopPet;

// 1.5 M17, B1 the Zip Slime (PetBattle.Zip.cs), redone 2026-11-05 and 2026-11-06, on the shipped numbers. Its size
// and stages. The press riddle: the slides, a lift dodging them, a drop after it stops pinning it, two pins opening
// it (stun, more damage), missing it opening it with no reward. The roped phase: its helpers come tied to it, no
// damage while a rope is left, the Scissors and the cat (held or thrown, fast enough) cut ropes, a slow drag and a
// paused battle do not, two cuts together stun it longer. The bomb: the clock, pulls by the cat and the Scissors,
// the flight and reward, the burst sparing a cat lifted over it and arming again. The data limits. Its blows and
// debuffs: BattleZipMoveTests.
internal static class BattleZipTests
{
    const double dt = 1.0 / 60;

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static readonly EnemyType ShippedZip = Shipped.Enemy("zip");
    private static readonly ZipSpec Spec = new()
    {
        Grow = ShippedZip.Zip!.Grow, RopeComboWindow = ShippedZip.Zip.RopeComboWindow, RopeComboStun = ShippedZip.Zip.RopeComboStun, Stages = ShippedZip.Zip.Stages,
        Press = ShippedZip.Zip.Press, Bomb = ShippedZip.Zip.Bomb, Moves = ShippedZip.Zip.Moves, Debuff = null
    };

    private static EnemyType Plain(string id = "slime") => new() { Id = id, MaxHp = 300, Speed = 40, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3 };
    private static readonly FormationSpec Duo = new()
    {
        Id = "duo", NameVi = "Đôi", NameEn = "Duo",
        Parts = new[] { new WaveGroup { Squad = new[] { new WaveGroup { Enemy = "slime", Count = 2 } } } }
    };
    private static BossPhase[] AllPhases => new[]
    {
        new BossPhase { At = 0.66, Shield = true, Press = true },
        new BossPhase { At = 0.33, Shield = true, Roped = true, Formation = "duo" },
        new BossPhase { At = 0.2, Shield = true, Bomb = true }
    };
    private static EnemyType Zip(double damage = 20) => new()
    {
        Id = "zip", MaxHp = 1000, Speed = 20, Damage = damage, Range = 110, Windup = 1.2, AttackCooldown = 2.6, Recover = 0.7, Scale = 2,
        Boss = true, Slam = true, PushFactor = 0.35, StunFactor = 0.4, SummonCap = 10, ShieldBreakStun = 2,
        RopeCutSpeed = ShippedZip.RopeCutSpeed, RopeCutStun = ShippedZip.RopeCutStun, Roll = ShippedZip.Roll, Zip = Spec, Phases = AllPhases
    };

    private static BattleSettings Settings(EnemyType boss) => new()
    {
        Weapons = new[] { Shipped.Weapon("sword") with { Range = 60, StunChance = 0, Damage = 0 } }, FirstWaveDelay = 0, SpawnInterval = 0.05,
        CatMaxHp = 1000, MaxAlive = 8, RageHitGain = 0, ScissorsCooldown = 0.1,
        Enemies = new[] { boss, Plain(), Shipped.Enemy("zip_packet") }, Formations = new[] { Duo },
        Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "zip", Count = 1 } } } }
    };

    private static readonly BattleSkill[] Tools = { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Bomb, BattleSkill.Scissors, BattleSkill.Shield };

    // The boss in, standing at bossX, the cat at catX.
    private static (BattleSim Sim, Slime Boss) Field(EnemyType boss, double bossX = 1200, double catX = 100)
    {
        var sim = new BattleSim(Settings(boss), 0, 1600, seed: 4, skills: Tools, startHp: 900);
        for (int i = 0; i < 600 && !sim.Slimes.Any(s => s.IsBoss && s.Targetable); i++) sim.Step(dt, catX, false);
        var zip = sim.Slimes.Single(s => s.IsBoss);
        zip.X = bossX;
        sim.ClearEvents();
        return (sim, zip);
    }

    // Steps `seconds` with the cat at catX (held: in the hand past the drag threshold, high up), until an
    // event of `kind` (then true).
    private static bool Run(BattleSim sim, double seconds, double catX = 100, BattleEventKind? kind = null, bool held = false, HashSet<BattleEventKind>? seen = null)
    {
        for (int i = 0; i < Math.Round(seconds / dt); i++)
        {
            sim.Step(dt, catX, held, held, held ? new CatPose(catX, 200, 0, 0, true, false) : null);
            foreach (var e in sim.Events) seen?.Add(e.Kind);
            bool hit = kind is BattleEventKind k && sim.Events.Any(e => e.Kind == k);
            sim.ClearEvents();
            if (hit) return true;
            if (sim.Over) break;
        }
        return false;
    }

    // The cat lifted a moment, then let go at x.
    private static void Drop(BattleSim sim, double x, HashSet<BattleEventKind>? seen = null)
    {
        Run(sim, 0.1, x, held: true, seen: seen);
        Run(sim, dt, x, seen: seen);
    }

    public static void Run(Action<bool, string> check)
    {
        StageChecks(check);
        PuzzleChecks(check);
        RepulseChecks(check);
        CanopyChecks(check);
        PhaseTwoRegression(check);
        BeltContactChecks(check);
        DataChecks(check);
    }

    private static void StageChecks(Action<bool, string> check)
    {
        var stages = Spec.Stages;
        // It comes in small, at its first stage's size and reach.
        var (sim, zip) = Field(Zip());
        check(zip.ZipStage == 0 && Math.Abs(zip.Width - sim.Settings.SlimeWidth * stages[0].Scale) < 1e-6 && zip.Range == stages[0].Range, "It comes in small, at its first stage's size");
        check(stages[0].Scale < stages[1].Scale && stages[1].Scale < stages[2].Scale, "Each stage is bigger than the last");
        check(stages[0].Speed > stages[1].Speed && stages[1].Speed > stages[2].Speed, "And slower");
        check(Spec.Moves.Select(m => m.Damage).Max() > stages[0].Damage, "Its blows carry their own numbers");
    }

    private static void PuzzleChecks(Action<bool, string> check)
    {
        var (sim, boss) = Field(Zip(), bossX: 800, catX: 400);
        sim.Damage(boss, 100000, DamageSource.App);
        Run(sim, 2, 400);
        var belts = sim.Slimes.Where(s => s.ZipProp == ZipProp.Belt).OrderBy(s => s.X).ToArray();
        check(boss.Shielded && boss.Hp == boss.MaxHp * 0.66 && belts.Length == 2, "Oversized hit starts first puzzle and creates two edge belts");
        check(Math.Abs(boss.X - 800) < 1 && belts[0].X == 65 && belts[1].X == 1535, "Boss leaps to centre; both locks leave the cat room at map edges");
        sim.Damage(belts[1], 100000, DamageSource.Weapon);
        sim.Damage(belts[0], 100000, DamageSource.Splash);
        check(boss.Pins == 0, "Inactive lock and splash cannot advance the puzzle");
        for (int i = 0; i < 300 && !sim.ZipBlows.Any(b => !b.Done); i++) Run(sim, dt, 400);
        check(sim.ZipBlows.Any(b => b.Top == 65 && b.Stun == 0.5), "Belt sweep warns along the active half of the floor");
        sim.Paused = true;
        double timer = boss.ZipPuzzleClock;
        Run(sim, 1, 400);
        check(boss.ZipPuzzleClock == timer, "Pause freezes puzzle and sweep clocks");
        sim.Paused = false;
        for (int i = 0; i < 12; i++)
        {
            var active = belts[(i / 3) % 2];
            check(sim.ZipPropActive(active), $"Correct belt active for hit {i + 1}");
            sim.Damage(active, 1, DamageSource.Weapon);
            check(boss.Pins == i + 1, "Each direct weapon strike counts once");
        }
        check(!boss.Shielded && boss.ZipStage == 1 && belts.All(s => !s.Targetable), "Twelve strikes remove belts and open stage two");
        boss.MechBonusClock = 0;
        sim.Damage(boss, 100000, DamageSource.Poison);
        Run(sim, 0.1, 400);
        var files = sim.Slimes.Where(s => s.ZipProp == ZipProp.File && s.Targetable).ToArray();
        check(files.Length == 3 && boss.Shielded, "Second HP gate creates three matching files");
        check(files.All(s => s.X >= 120 && s.X <= 1480 && sim.ZipFileGoal(s) >= 120 && sim.ZipFileGoal(s) <= 1480), "Files and folders leave room to attack from either side");
        Run(sim, 3.5, 400);
        check(sim.ZipBlows.Any(b => !b.Done && b.Kind == ZipBlowKind.Branch && b.Stun == 0.6), "Folder puzzle plants a warned damaging tree at the cat");
        foreach (var file in files)
        {
            Run(sim, dt, file.X + (sim.ZipFileGoal(file) > file.X ? -70 : 70), held: true);
            // The cat stands to the left of these initial positions.
            for (int i = 0; i < 10 && file.Targetable; i++) sim.Damage(file, 1, DamageSource.Weapon);
        }
        check(!boss.Shielded && boss.ZipStage == 2, "Matching all three files opens stage three");
        check(boss.ZipRepulseClock == 5, "New stage resets its first independent repulse to five eligible seconds");
        boss.Hp = 2;
        sim.Damage(boss, 100000, DamageSource.Poison);
        check(boss.Hp == 1 && boss.Shielded && boss.BombClock > 0, "Lethal poison is clamped to exactly one HP and immediately starts final puzzle");
        var tabs = sim.Slimes.Where(s => s.ZipProp == ZipProp.Tab && s.Targetable).ToArray();
        check(tabs.Length == 6 && tabs.Count(s => s.Airborne) == 3, "Final puzzle has three floor and three air pulls");
        sim.Damage(boss, 100000, DamageSource.Reflect);
        check(boss.Hp == 1, "Damage cannot bypass the final puzzle shield");
        Run(sim, boss.Type.Zip!.Bomb.Charge + 0.2, 400, held: true);
        check(boss.Shielded && boss.BombClock > 0 && tabs.All(s => s.Targetable), "Timeout keeps remaining pulls and never exposes a killable boss");
        foreach (var tab in tabs) sim.Damage(tab, 1, DamageSource.Weapon);
        check(tabs.All(s => !s.Targetable) && boss.Deflating > 0, "Every pull disappears after one direct strike");
        check(!sim.ZipBlows.Any(b => b.OwnerId == boss.Id), "Solving six pulls clears every final tree and pending hit immediately");
        Run(sim, 2, 400);
        check(boss.State == SlimeState.Defeated && sim.BossDefeated, "Six pulls deflate and defeat the one HP boss");
    }

    private static void BeltContactChecks(Action<bool, string> check)
    {
        foreach (int direction in new[] { -1, 1 })
        {
            double from = 800, to = direction < 0 ? 80 : 1520, cat = direction < 0 ? 200 : 1400;
            var (sim, boss) = Field(Zip(), bossX: from, catX: cat);
            boss.Cooldown = 100;
            var belt = new ZipBlow { OwnerId = boss.Id, Kind = ZipBlowKind.Belt, FromX = from, ToX = to, Travel = 0.35, X = (from + to) / 2, Radius = 360, Bottom = 0, Top = 65, Warn = 0.8, Left = 0.8, Life = 0.55, Damage = 8, Stun = 0.5 };
            ((ICollection<ZipBlow>)sim.ZipBlows).Add(belt);
            double hp = sim.CatHp;
            Run(sim, 0.82, cat);
            check(belt.Done && sim.CatHp == hp, "Finishing the belt warning does not instantly damage the whole lane");
            Run(sim, 0.2, cat);
            check(sim.CatHp == hp && Math.Abs(belt.Front - from) > 0, "The visible buckle travels from the boss before reaching a far cat");
            sim.Paused = true;
            double elapsed = belt.Elapsed;
            Run(sim, 1, cat);
            check(belt.Elapsed == elapsed && sim.CatHp == hp, "Pause freezes the travelling belt and its contact");
            double front = belt.Front;
            sim.SetArea(-800, 0);
            cat = -800 + cat * 0.5;
            check(Math.Abs(belt.Front - (-800 + front * 0.5)) < 0.01 && belt.Elapsed == elapsed, "Changing monitor-relative area maps the belt path without restarting or sweeping stale coordinates");
            sim.Paused = false;
            Run(sim, 0.1, cat);
            check(sim.CatHp < hp && belt.Hit && sim.CatStunned, "Belt damage and stun occur when the moving front actually crosses the cat");
            hp = sim.CatHp;
            Run(sim, 0.2, cat);
            check(sim.CatHp == hp, "The trailing and returning strap cannot hit a second time");
        }
    }

    private static void PhaseTwoRegression(Action<bool, string> check)
    {
        foreach (double width in new[] { 480.0, 640, 1600 })
        foreach (int seed in Enumerable.Range(1, 12))
        {
            var sim = new BattleSim(Settings(Zip()), 0, width, seed: seed, skills: Tools, startHp: 900);
            for (int i = 0; i < 100 && sim.Boss is not { Targetable: true }; i++) sim.Step(dt, 50, false);
            var boss = sim.Boss!;
            boss.X = width * (0.2 + seed % 4 * 0.2);
            boss.ZipStage = 1; boss.PhasesDone = 1; boss.Hp = boss.MaxHp * 0.5;
            var bomb = sim.AddSlime(width / 2, Shipped.Enemy("zip_packet"));
            bomb.SummonedBy = boss.Id; bomb.PacketFuse = true; bomb.PacketLeft = 0.05; bomb.Set(SlimeState.Moving);
            sim.Damage(boss, 100000, DamageSource.App);
            Run(sim, 2, 40);
            var files = sim.Slimes.Where(s => s.ZipProp == ZipProp.File && s.Targetable).ToArray();
            check(files.Length == 3 && bomb.State == SlimeState.Defeated, $"Phase two clears old bombs and creates three files: width {width}, seed {seed}");
            check(files.All(f => Math.Abs(f.X - boss.X) > boss.Width / 2 + 32 && Math.Abs(sim.ZipFileGoal(f) - boss.X) > boss.Width / 2 + 32), "Files and matching goals never overlap the boss");
            check(files.All(f => (f.ZipLaneRight < boss.X - boss.Width / 2 || f.ZipLaneLeft > boss.X + boss.Width / 2) && f.ZipLaneLeft >= 0 && f.ZipLaneRight <= width), "Every file push lane stays on one side of the boss and inside the map");
            // Even a late bomb spawned after entry must not delete puzzle props.
            var late = sim.AddSlime(40, Shipped.Enemy("zip_packet"));
            late.SummonedBy = boss.Id; late.PacketFuse = true; late.PacketLeft = 0.05; late.Set(SlimeState.Moving);
            Run(sim, 0.2, width - 40);
            check(files.All(f => f.Targetable) && boss.Shielded, "Exploding or clearing faulty packets cannot remove puzzle files");
            foreach (var file in files)
            {
                for (int hit = 0; hit < 20 && file.Targetable; hit++)
                {
                    Run(sim, dt, file.X + (sim.ZipFileGoal(file) > file.X ? -60 : 60), held: true);
                    sim.Damage(file, 1, DamageSource.Weapon);
                }
            }
            check(!boss.Shielded && boss.ZipStage == 2, "All seeded file layouts can be solved without crossing the boss");
        }
    }

    private static void RepulseChecks(Action<bool, string> check)
    {
        var (sim, boss) = Field(Zip(), bossX: 800, catX: 400);
        boss.ZipStage = 1; boss.ZipRepulseClock = 0; boss.Cooldown = 0;
        double hp = sim.CatHp;
        Run(sim, 0.5, 400);
        check(boss.ZipRepulseWarn > 0 && sim.ShoveLeft == 0, "Independent repulse warns before displacing the cat");
        sim.Paused = true;
        double warning = boss.ZipRepulseWarn;
        Run(sim, 1, 400);
        check(boss.ZipRepulseWarn == warning, "Pause freezes repulse warning");
        sim.Paused = false;
        Run(sim, 0.5, 400);
        check(sim.ShoveLeft > 0 && !sim.CatStunned && sim.CatHp == hp, "Repulse pushes without damage or early stun");
        Run(sim, 0.9, 400);
        check(sim.ShoveLeft == 0 && sim.CatStunned && sim.CatHp == hp && boss.ZipRepulseClock > 9, "Repulse stuns only after displacement, with separate cooldown and no damage");
        (sim, boss) = Field(Zip(), bossX: 800, catX: 400);
        boss.ZipStage = 2; boss.ZipRepulseClock = 0; boss.Cooldown = 0;
        Run(sim, 1.5, 400, held: true);
        check(sim.ShoveLeft == 0 && !sim.CatStunned, "Repulse never drops or stuns a held cat");
        check(boss.ZipRepulseWarn == 0 && boss.ZipRepulseClock <= 0, "A held cat does not consume a ready repulse");
        boss.ZipAct = ZipAct.None; boss.Set(SlimeState.Moving); boss.Cooldown = 0;
        Run(sim, dt, 400);
        check(boss.ZipRepulseWarn == 0 && sim.Grace > 0, "Repulse respects landing grace after a held cat is released");
        for (int i = 0; i < 180 && boss.ZipRepulseWarn <= 0; i++) Run(sim, dt, 400);
        check(boss.ZipRepulseWarn > 0 && boss.ZipAct == ZipAct.None, "Ready repulse takes priority over the next normal move on the floor");
        Run(sim, 1, 400, held: true);
        check(sim.ShoveLeft == 0 && boss.ZipRepulseClock > 11 && boss.ZipRepulseWarn == 0,
            "Lifting during the warning dodges repulse and consumes its normal cooldown");
    }

    private static void CanopyChecks(Action<bool, string> check)
    {
        for (int state = 0; state < 3; state++)
        {
            var (sim, boss) = Field(Zip(), bossX: 1200, catX: 400);
            boss.ZipStage = 2; boss.Shielded = true; boss.ShieldTag = 3;
            boss.BombClock = 14; boss.ZipPuzzleClock = 2;
            void Step(int frames, double x)
            {
                for (int i = 0; i < frames; i++) sim.Step(dt, x, state != 0, state == 1,
                    state == 0 ? null : new CatPose(x, 400, 0, 200, state == 1, state == 2));
            }
            Step(119, 400);
            check(!sim.ZipBlows.Any(b => b.Kind == ZipBlowKind.Canopy), "Final tree waits two seconds before first warning");
            Step(3, 400);
            var tree = sim.ZipBlows.Single(b => b.Kind == ZipBlowKind.Canopy);
            double hp = sim.CatHp, left = tree.Left;
            sim.Paused = true; Step(60, 400); sim.Paused = false;
            check(tree.Left == left && sim.CatHp == hp, "Pause freezes full-height tree warning");
            Step(62, 400);
            check(tree.Top == sim.SkyHeight && tree.Radius == 50 && tree.Done && tree.Hit && sim.CatHp < hp,
                $"Full-height tree hits cat pose {state} only after warning");
            double after = sim.CatHp;
            Step(12, 400);
            check(sim.CatHp == after && (state == 0 || !sim.CatStunned), "Tree hits once and never stuns held/flying cat");
            Step(40, 400);
            check(!sim.ZipBlows.Any(b => b.Kind == ZipBlowKind.Canopy), "Tree disappears after its active window");
            Step(160, 700);
            check(sim.ZipBlows.Any(b => b.Kind == ZipBlowKind.Canopy && Math.Abs(b.X - 700) < 1), "Next tree locks new cat position on independent cadence");
        }
    }

    private static void DataChecks(Action<bool, string> check)
    {
        const string slime = "{\"id\":\"slime\",\"nameVi\":\"S\",\"nameEn\":\"S\"}";
        static string Enemy(string id, string extra) => $"{{\"id\":\"{id}\",\"nameVi\":\"T\",\"nameEn\":\"T\",{extra}}}";
        const string formation = ",\"formations\":[{\"id\":\"duo\",\"nameVi\":\"D\",\"nameEn\":\"D\",\"parts\":[{\"enemy\":\"slime\",\"count\":2}]}]";
        bool Rejects(string enemies, string more = formation)
        {
            try { BattleData.Parse("{\"enemies\":[" + slime + "," + enemies + "]" + more + ",\"waves\":[{\"groups\":[{\"enemy\":\"a\",\"count\":1}]}]}"); return false; }
            catch (InvalidDataException) { return true; }
        }
        const string boss = "\"boss\":true,", zip1 = "\"zip\":{\"stages\":[{}]},";
        check(!Rejects(Enemy("a", boss + "\"roll\":{}," + zip1 + "\"phases\":[{\"at\":0.6,\"shield\":true,\"press\":true},{\"at\":0.4,\"shield\":true,\"roped\":true,\"formation\":\"duo\"},{\"at\":0.2,\"shield\":true,\"bomb\":true}]")),
            "A boss with stages, a press, a roped formation and a bomb phase is accepted");
        check(Rejects(Enemy("a", "\"roll\":{}")) && Rejects(Enemy("a", boss + "\"roll\":{\"minRange\":600,\"range\":500}")) && Rejects(Enemy("a", boss + "\"roll\":{\"speed\":0}")),
            "A roll on a plain enemy, or with numbers out of range, is refused");
        check(Rejects(Enemy("a", boss + "\"phases\":[{\"at\":0.5,\"shield\":true,\"formation\":\"nobody\"}]")) && Rejects(Enemy("a", boss + "\"phases\":[{\"at\":0.5,\"roped\":true,\"summon\":\"slime\",\"count\":1}]"))
              && Rejects(Enemy("a", boss + zip1 + "\"phases\":[{\"at\":0.5,\"shield\":true,\"roped\":true,\"press\":true,\"summon\":\"slime\",\"count\":1}]")),
            "An unknown formation, a roped phase without a shield, two riddles in one phase are refused");
        check(Rejects(Enemy("a", boss + "\"phases\":[{\"at\":0.5,\"shield\":true,\"press\":true}]")) && Rejects(Enemy("a", boss + zip1 + "\"phases\":[{\"at\":0.5,\"press\":true}]"))
              && Rejects(Enemy("a", boss + zip1 + "\"phases\":[{\"at\":0.5,\"shield\":true,\"bomb\":true,\"summon\":\"slime\",\"count\":1}]")),
            "A riddle needs a zip block and a shield, and calls no helpers");
        check(Rejects(Enemy("a", zip1 + "\"damage\":1")) && Rejects(Enemy("a", boss + "\"zip\":{\"stages\":[]}")) && Rejects(Enemy("a", boss + "\"zip\":{\"stages\":[{\"scale\":9}]}"))
              && Rejects(Enemy("a", boss + "\"zip\":{\"stages\":[{\"moves\":[\"nobody\"]}]}")) && Rejects(Enemy("a", boss + "\"zip\":{\"stages\":[{}],\"press\":{\"rolls\":1,\"pins\":2}}")) && Rejects(Enemy("a", boss + "\"zip\":{\"stages\":[{}],\"bomb\":{\"charge\":1}}")),
            "Stages on a plain enemy, or out of range, or a riddle's numbers out of range, are refused");
        check(Rejects(Enemy("a", boss + "\"still\":true")) && Rejects(Enemy("a", "\"still\":true,\"dash\":true")) && Rejects(Enemy("a", boss + "\"ropeCutSpeed\":10")),
            "Still on a boss or an attacker, a rope cut speed out of range, are refused");

        var s = Shipped;
        var zip = s.Enemy("zip");
        var p = zip.Phases ?? Array.Empty<BossPhase>();
        check(zip is { Boss: true, Roll: not null, BinSpit: false, Zip.Stages.Length: 3 } && p.Length == 3 && p[0] is { Press: true } && p[1] is { Roped: true } && p[2] is { Bomb: true },
            "The shipped Zip Slime: three stages, the press, its packets on streams, then the bomb");
        check(p.All(ph => s.PhaseCalls(ph).Count() <= zip.SummonCap), "Each of its phases fits its summon cap");
        check(s.Enemies.All(e => e.Id != "zipshard"), "No more file shards");
        check(Spec.Stages.All(st => s.SlimeHeight * st.Scale * 1.15 <= 130), "Even swollen it fits under the field's top");
        bool Uses(WaveGroup g) => g.Enemy == "zip" || g.Pick?.Keys.Contains("zip") == true || g.Squad?.Any(m => m.Enemy == "zip") == true;
        var all = s.WavePlan.Concat(s.Campaign?.Chapters.Take(1).SelectMany(ch => ch.Stages).SelectMany(st => st.Waves) ?? Enumerable.Empty<WaveSpec>());
        check(!all.SelectMany(w => w.Groups).Any(Uses) && s.Endless?.Boss != "zip", "No built-in battle uses it outside chapter 2 and later");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m17.json"))) dir = dir.Parent;
        var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", "battle-m17.json"));
        check(load is { Custom: true, Error: null } && load.Settings.WavePlan.SelectMany(w => w.Groups).Any(g => g.Enemy == "zip"),
            $"docs/examples/battle-m17.json loads as custom data with the Zip Slime ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
    }
}
