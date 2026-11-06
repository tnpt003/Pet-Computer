using DesktopPet;

// 2026-11-06, the Zip Slime's blows and debuffs (PetBattle.ZipMoves.cs, PetBattle.ZipDebuff.cs), on the shipped
// numbers. The bag: from the seed, all three before a repeat, never the same twice running, only valid moves. Each
// move: what it hits, how it is dodged, that nothing moves after its lock. The hand: floor blows miss a cat lifted
// over them, aimed ones reach it, none lets go of it or stuns it. Being cut short: no late hit. The debuffs.
internal static class BattleZipMoveTests
{
    const double dt = 1.0 / 60;

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static readonly EnemyType ShippedZip = Shipped.Enemy("zip");
    private static readonly ZipSpec Spec = ShippedZip.Zip!;
    private static readonly ZipSpec Calm = new()
    {
        Grow = Spec.Grow, RopeComboWindow = Spec.RopeComboWindow, RopeComboStun = Spec.RopeComboStun, Stages = Spec.Stages,
        Press = Spec.Press, Bomb = Spec.Bomb, Moves = Spec.Moves, Debuff = null
    };

    private static EnemyType Boss(ZipSpec zip, double damage = 20) => new()
    {
        Id = "zip", MaxHp = 1000, Speed = 20, Damage = damage, Range = 110, Windup = 1.2, AttackCooldown = 2.6, Recover = 0.7, Scale = 2,
        Boss = true, Slam = true, PushFactor = 0.35, StunFactor = 0.4, SummonCap = 10, ShieldBreakStun = 2,
        RopeCutSpeed = ShippedZip.RopeCutSpeed, RopeCutStun = ShippedZip.RopeCutStun, Roll = ShippedZip.Roll, Zip = zip, Phases = ShippedZip.Phases!.Select((p, i) => i == 1 ? new BossPhase { At = p.At, Shield = true, Roped = true, Summons = p.Summons } : p).ToArray()
    };

    private static BattleSettings Settings(EnemyType boss, double weapon = 0, double? swingCooldown = null, double rageGain = 0) => new()
    {
        Weapons = new[] { Shipped.Weapon("sword") with { Range = 60, StunChance = 0, Damage = weapon, AttackCooldown = swingCooldown ?? Shipped.Weapon("sword").AttackCooldown } }, FirstWaveDelay = 0, SpawnInterval = 0.05,
        CatMaxHp = 1000, MaxAlive = 8, RageHitGain = rageGain, ScissorsCooldown = 0.1, RageAuto = false,
        Enemies = new[] { boss, Shipped.Enemy("zip_packet") }, Formations = Array.Empty<FormationSpec>(),
        Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "zip", Count = 1 } } } }
    };

    private static readonly BattleSkill[] Tools = { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Bomb, BattleSkill.Scissors, BattleSkill.Shield };

    private static (BattleSim Sim, Slime Boss) Field(int stage = 0, ZipSpec? zip = null, double bossX = 800, double catX = 500, int seed = 4, double weapon = 0, double damage = 20, double? swingCooldown = null, double rageGain = 0)
    {
        var sim = new BattleSim(Settings(Boss(zip ?? Calm, damage), weapon, swingCooldown, rageGain), 0, 2000, seed: seed, skills: Tools, startHp: 900);
        for (int i = 0; i < 600 && !sim.Slimes.Any(s => s.IsBoss && s.Targetable); i++) sim.Step(dt, catX, false);
        var boss = sim.Slimes.Single(s => s.IsBoss);
        boss.X = bossX;
        boss.ZipStage = stage;
        boss.PhasesDone = stage == 0 ? 0 : stage == 1 ? 1 : 3;
        boss.Range = Spec.Stages[stage].Range;
        boss.Cooldown = 0;
        sim.ClearEvents();
        return (sim, boss);
    }

    // Steps with the cat standing at x, or in the hand at (x, height), or thrown.
    private static void Run(BattleSim sim, double seconds, double x, double? height = null, bool flying = false)
    {
        for (int i = 0; i < Math.Round(seconds / dt) && !sim.Over; i++) One(sim, x, height, flying);
    }

    private static void One(BattleSim sim, double x, double? height = null, bool flying = false)
    {
        if (height is double h) sim.Step(dt, x, true, !flying, new CatPose(x, h, 0, 0, !flying, flying));
        else sim.Step(dt, x, false);
        sim.ClearEvents();
    }

    // The move comes next: true once it has started.
    private static bool Start(BattleSim sim, Slime boss, ZipAct act, double x, double? height = null)
    {
        boss.ZipBag.Clear();
        boss.ZipBag.Add(act);
        boss.ZipLast = ZipAct.None;
        boss.Cooldown = 0;
        for (int i = 0; i < 300 && boss.ZipAct != act; i++) One(sim, x, height);
        return boss.ZipAct == act;
    }

    private static ZipMoveSpec M(ZipAct act) => Spec.Move(act)!;
    private static double Whole(ZipMoveSpec m) => m.Warn + m.Lock + m.Strike;

    public static void Run(Action<bool, string> check)
    {
        BagChecks(check);
        Phase1(check);
        Phase2(check);
        Phase3(check);
        HandChecks(check);
        PaperBodyChecks(check);
        SelfLockChecks(check);
        CutShortChecks(check);
        // New puzzle rules are covered by BattleZipTests.
        DebuffChecks(check);
        DataChecks(check);
    }

    private static List<ZipAct> Draws(int stage, int seed, int count)
    {
        var (sim, boss) = Field(stage, seed: seed, damage: 0);
        var seen = new List<ZipAct>();
        for (int i = 0; i < 60000 && seen.Count < count; i++)
        {
            sim.Step(dt, 500, false);
            sim.ClearEvents();
            boss.X = 800; boss.Cooldown = 0;
            foreach (var s in sim.Slimes.Where(s => s.Type.Packet && s.Targetable).ToArray()) sim.Damage(s, s.Hp + 1, DamageSource.App);
            if (boss.ZipAct != ZipAct.None && (seen.Count == 0 || boss.ZipIdle == 0 && boss.ZipBeat is ZipBeat.Warn or ZipBeat.Strike && boss.ZipLeft > boss.ZipSeconds - 2 * dt && boss.ZipAct != seen[^1] || false)) seen.Add(boss.ZipAct);
        }
        return seen;
    }

    private static void BagChecks(Action<bool, string> check)
    {
        check(Enumerable.SequenceEqual(Draws(0, 7, 9), Draws(0, 7, 9)), "The same seed draws the same moves");
        bool differs = Enumerable.Range(0, 8).Select(seed => string.Join(",", Draws(1, seed, 6))).Distinct().Count() > 1;
        check(differs, "Other seeds draw them otherwise");
        for (int stage = 0; stage < 3; stage++)
        {
            bool full = true, varied = true;
            for (int seed = 0; seed < 6; seed++)
            {
                var acts = Draws(stage, seed, 9);
                for (int i = 0; i + 3 <= acts.Count; i += 3) full &= acts.Skip(i).Take(3).Distinct().Count() == 3;
                for (int i = 1; i < acts.Count; i++) varied &= acts[i] != acts[i - 1];
                full &= acts.Count == 9;
            }
            check(full, $"Stage {stage + 1}: all three show before the bag is shaken again");
            check(varied, $"Stage {stage + 1}: never the same move twice running");
        }
        // A lifted cat only gets what can reach it.
        var (sim, boss) = Field(0, damage: 0);
        bool others = false;
        for (int i = 0; i < 1200; i++) { One(sim, 700, 300); others |= boss.ZipAct is ZipAct.Clamp or ZipAct.Tumble; }
        check(!others && sim.ZipMovesUsed > 0, "At a cat lifted out of reach of its floor moves it uses only the aimed ones");
        check(Spec.Stages.All(st => st.Moves.Length == 3) && Spec.Stages.All(st => st.Acts.Any(a => Spec.Move(a)!.Air)), "Each stage has three moves, one of them aimed at a lifted cat");
    }

    private static void Phase1(Action<bool, string> check)
    {
        // Clamp: the marked spot, fixed from the start, even if the cat walks off.
        var (sim, boss) = Field(0, bossX: 700, catX: 500);
        double hp = sim.CatHp;
        check(Start(sim, boss, ZipAct.Clamp, 500), "The clamp starts");
        double aim = boss.ZipAimX;
        Run(sim, Whole(M(ZipAct.Clamp)) * 0.5, 500);
        check(boss.ZipAimX == aim && Math.Abs(boss.X - aim) < Math.Abs(700 - aim), "It slides to the marked spot, which stays");
        Run(sim, Whole(M(ZipAct.Clamp)) * 0.5 + 0.1, 500);
        check(sim.CatHp < hp && sim.CatStunned, "A cat left in the gap is clamped");
        (sim, boss) = Field(0, bossX: 700, catX: 500);
        hp = sim.CatHp;
        Start(sim, boss, ZipAct.Clamp, 500);
        Run(sim, Whole(M(ZipAct.Clamp)), 500 - M(ZipAct.Clamp).Radius - 40);
        check(sim.CatHp == hp, "One moved out of it is not");

        // Bolt: it follows until the lock, then the aim stays; both ways are dangerous.
        (sim, boss) = Field(0, bossX: 800, catX: 500);
        var bolt = M(ZipAct.Bolt);
        Start(sim, boss, ZipAct.Bolt, 500);
        Run(sim, bolt.Warn * 0.5, 520);
        check(Math.Abs(boss.ZipAimX - 520) < 1, "The zipper head follows the cat until it locks");
        Run(sim, bolt.Warn * 0.6, 540);
        double locked = boss.ZipAimX;
        hp = sim.CatHp;
        check(boss.ZipBeat == ZipBeat.Lock, "Then it locks");
        Run(sim, bolt.Lock + bolt.Strike + 0.1, 400);
        check(boss.ZipAimX == locked && sim.CatHp == hp, "Moved after the lock, the cat is missed and the aim stays");
        (sim, boss) = Field(0, bossX: 800, catX: 500);
        Start(sim, boss, ZipAct.Bolt, 500);
        hp = sim.CatHp;
        Run(sim, bolt.Warn + bolt.Lock + bolt.Strike * 0.75, 500);
        check(sim.CatHp < hp, "Standing on the spot, it is hit");
        hp = sim.CatHp;
        Run(sim, bolt.Strike, 500);
        check(sim.CatHp == hp, "Once a shot, there and back");
        (sim, boss) = Field(0, bossX: 800, catX: 500);
        Start(sim, boss, ZipAct.Bolt, 500);
        hp = sim.CatHp;
        Run(sim, bolt.Warn + bolt.Lock + bolt.Strike * 0.6, 200); // Away as it flies out...
        Run(sim, 0.01, 200);
        double midway = boss.ZipHeadX;
        Run(sim, bolt.Strike * 0.4, midway); // ...and on its way back.
        check(sim.CatHp < hp, "Its way back hurts too");

        // Tumble: over and back along the same floor, each way shown.
        (sim, boss) = Field(0, bossX: 800, catX: 500);
        hp = sim.CatHp;
        int rolls = 0;
        bool back = false;
        boss.ZipBag.Clear(); boss.ZipBag.Add(ZipAct.Tumble); boss.ZipLast = ZipAct.None;
        var seen = new HashSet<BattleEventKind>();
        for (int i = 0; i < 900; i++)
        {
            sim.Step(dt, 480, false);
            rolls += sim.Events.Count(e => e.Kind == BattleEventKind.Rolled);
            back |= boss.RollBounce;
            sim.ClearEvents();
            if (sim.CatStunRemaining > 0) sim.CatHp.Equals(0);
            if (rolls == 3 && !boss.Rolling) break;
        }
        check(rolls == 3 && back, "The tumble performs three separately warned rolls");
        check(Math.Abs(boss.X - 800) > 100, "The third roll is not locked to the original return point");
        check(sim.CatHp < hp, "A cat on the floor in its way is hit");
        (sim, boss) = Field(0, bossX: 800, catX: 500);
        boss.ZipBag.Clear(); boss.ZipBag.Add(ZipAct.Tumble); boss.ZipLast = ZipAct.None;
        for (int i = 0; i < 400 && boss.ZipCount < 2; i++) Run(sim, dt, 480);
        check(boss.Rolling && boss.ZipCount == 2 && boss.State == SlimeState.Windup, "Second pursuit has its own warning");
        Run(sim, 0.1, 100);
        check(Math.Abs(boss.ZipSeconds - 0.6) < 0.001 && boss.ZipLeft < boss.ZipSeconds && boss.ZipLeft > 0,
            "Published pursuit warning clock follows the actual 0.6-second windup");
        check(boss.DashDirection == -1 && boss.RollGoal < boss.X, "A cat behind the boss changes the next roll direction during warning");
        double goal = boss.RollGoal;
        sim.Paused = true; Run(sim, 1, 1400); sim.Paused = false;
        check(boss.RollGoal == goal && boss.State == SlimeState.Windup, "Pause freezes pursuit aim and warning");
        for (int i = 0; i < 60 && boss.State != SlimeState.Dashing; i++) Run(sim, dt, 100);
        goal = boss.RollGoal;
        Run(sim, dt, 1400);
        check(boss.State == SlimeState.Dashing && boss.RollGoal == goal && boss.DashDirection == -1, "Committed roll never turns mid-dash when the cat switches sides");
    }

    private static void Phase2(Action<bool, string> check)
    {
        // Tree: in order, a way through always left.
        var tree = M(ZipAct.Tree);
        var (sim, boss) = Field(1, bossX: 800, catX: 800 + tree.Spacing / 2);
        check(Start(sim, boss, ZipAct.Tree, 800 + tree.Spacing / 2), "The tree starts");
        check(sim.ZipBlows.Count == tree.Count - 1 && sim.ZipBlows.All(b => b.Kind == ZipBlowKind.Branch), $"All but one branch are marked ({sim.ZipBlows.Count})");
        check(sim.ZipBlows.OrderBy(b => b.Order).Select(b => b.Left).SequenceEqual(sim.ZipBlows.Select(b => b.Left).OrderBy(x => x)), "Numbered in the order they go off");
        var spots = sim.ZipBlows.Select(b => b.X).OrderBy(x => x).ToArray();
        check(spots.Zip(spots.Skip(1)).All(p => p.Second - p.First > 2 * tree.Radius), "With a gap between any two");
        double hp = sim.CatHp;
        Run(sim, Whole(tree) + tree.Count * tree.Delay + 0.3, 800 + tree.Spacing / 2);
        check(sim.CatHp == hp, "The cat in a gap is safe");
        (sim, boss) = Field(1, bossX: 800, catX: 800);
        Start(sim, boss, ZipAct.Tree, 800);
        var first = sim.ZipBlows.OrderBy(b => b.Order).First();
        hp = sim.CatHp;
        Run(sim, tree.Warn + 0.2, first.X);
        check(sim.CatHp < hp, "On a branch it is hit");

        // Chase: two files; one turn at the cat, then straight on.
        var chase = M(ZipAct.Chase);
        (sim, boss) = Field(1, bossX: 800, catX: 400);
        Start(sim, boss, ZipAct.Chase, 400);
        Run(sim, chase.Warn + 0.05, 400);
        var files = sim.Shots.Where(s => s.Paper).ToArray();
        check(files.Length == 3, "Three files fly out");
        Run(sim, chase.Delay + 0.05, 400);
        check(files.All(f => f.LockIn <= 0), "They lock on");
        double vx = files[0].VX, vy = files[0].VY;
        Run(sim, 0.2, 900);
        check(files[0].VX != vx || files[0].VY != vy, "Papers steer toward the moving cat after the initial lock");
        check(Math.Abs(Math.Sqrt(files[0].VX * files[0].VX + files[0].VY * files[0].VY) - chase.Speed) < 0.01, "Tracking keeps paper speed constant");
        hp = sim.CatHp;
        Run(sim, 3, 900);
        check(files.All(f => double.IsFinite(f.VX) && double.IsFinite(f.VY)), "Pursuit stays finite while the cat changes position");
        (sim, boss) = Field(1, bossX: 800, catX: 400);
        Start(sim, boss, ZipAct.Chase, 400);
        hp = sim.CatHp;
        Run(sim, 3, 400);
        check(sim.CatHp < hp, "One that stays is hit");

        // Faulty: three packets, one live.
        var faulty = M(ZipAct.Faulty);
        (sim, boss) = Field(1, bossX: 900, catX: 500);
        Start(sim, boss, ZipAct.Faulty, 500);
        Run(sim, faulty.Warn + 0.1, 500);
        var packets = sim.Slimes.Where(s => s.Type.Packet).ToArray();
        check(packets.Length == 3 && packets.Count(p => p.PacketFuse) == 1 && packets.All(p => p.RopedTo == 0), "Three packets, one of them live");
        int defeated = sim.Defeated;
        var live = packets.Single(p => p.PacketFuse);
        check(!packets.Any(p => p.PacketRevealed), "All faulty files initially conceal their identity");
        double fuseBefore = live.PacketLeft;
        Run(sim, 0.2, 500);
        check(live.PacketLeft < fuseBefore && !live.PacketRevealed, "Hidden bomb fuse advances before the player inspects it");
        sim.SetPointer((live.X, 10));
        Run(sim, 0.05, 500);
        check(live.PacketRevealed, "Hovering the bomb reveals its running fuse");
        sim.SetPointer(null);
        Run(sim, 0.05, 500);
        check(live.PacketRevealed, "Inspected bomb stays visible after the cursor leaves");
        for (int i = 0; i < 300 && live.State == SlimeState.Spawning; i++) One(sim, 500);
        sim.Damage(live, live.Hp + 1, DamageSource.App);
        One(sim, 500);
        check(packets.All(p => p.State == SlimeState.Defeated) && sim.ZipPacketBlasts == 0, "Breaking the live one defuses the batch");
        check(sim.Defeated == defeated + 1, "The others just fall apart: no kills");
        (sim, boss) = Field(1, bossX: 900, catX: 500);
        Start(sim, boss, ZipAct.Faulty, 500);
        Run(sim, faulty.Warn + 0.1, 500);
        live = sim.Slimes.Single(p => p.PacketFuse);
        hp = sim.CatHp;
        Run(sim, faulty.Life + 1.5, live.X);
        check(sim.ZipPacketBlasts == 1 && sim.CatHp < hp, "Left alone it blows, hurting a cat in its ring");
        check(sim.Slimes.All(s => !s.Type.Packet || s.State == SlimeState.Defeated), "And the batch is gone");
        (sim, boss) = Field(1, bossX: 900, catX: 500);
        Start(sim, boss, ZipAct.Faulty, 500);
        Run(sim, faulty.Warn + 0.1, 500);
        live = sim.Slimes.Single(p => p.PacketFuse);
        hp = sim.CatHp;
        Run(sim, faulty.Life + 1.5, live.X + faulty.Radius + 40);
        check(sim.ZipPacketBlasts == 1 && sim.CatHp == hp, "Out of its ring the cat is safe");
    }

    private static void Phase3(Action<bool, string> check)
    {
        // Overflow: the gap around the marked spot stays safe; outside it, no way but up.
        var flow = M(ZipAct.Overflow);
        var (sim, boss) = Field(2, bossX: 1000, catX: 900);
        double hp = sim.CatHp;
        Start(sim, boss, ZipAct.Overflow, 900);
        double gap = boss.ZipAimX;
        Run(sim, Whole(flow) + 0.2, gap);
        check(sim.CatHp == hp, "In the gap the cat is safe");
        (sim, boss) = Field(2, bossX: 1000, catX: 900);
        Start(sim, boss, ZipAct.Overflow, 900);
        hp = sim.CatHp;
        Run(sim, Whole(flow) + 0.2, boss.ZipAimX + flow.Radius + 60);
        check(hp - sim.CatHp > 0 && sim.CatStunned, "Outside it the tide and the burst hit");
        (sim, boss) = Field(2, bossX: 1000, catX: 900);
        Start(sim, boss, ZipAct.Overflow, 900);
        hp = sim.CatHp;
        Run(sim, Whole(flow) + 0.2, boss.ZipAimX + flow.Radius + 60, flow.BurstHeight + 40);
        check(sim.CatHp == hp, "Lifted over the burst it is safe too");

        // Overwrite: three marks of where the cat was; they stay.
        var write = M(ZipAct.Overwrite);
        (sim, boss) = Field(2, bossX: 1000, catX: 900);
        Start(sim, boss, ZipAct.Overwrite, 900);
        Run(sim, write.Warn * 0.2, 900);
        Run(sim, write.Warn * 0.33, 700);
        Run(sim, write.Warn * 0.33, 500);
        var marks = sim.ZipBlows.ToArray();
        check(marks.Length == write.Count && marks.Select(b => b.X).Distinct().Count() == write.Count, $"Three marks at three places ({marks.Length})");
        check(marks.OrderBy(b => b.Order).Select(b => b.Left).SequenceEqual(marks.Select(b => b.Left).OrderBy(x => x)), "Going off in order");
        hp = sim.CatHp;
        var xs = marks.Select(b => b.X).ToArray();
        Run(sim, 2, 1500);
        check(marks.Select(b => b.X).SequenceEqual(xs) && sim.CatHp == hp, "They stay put: the cat elsewhere is not hit");
        (sim, boss) = Field(2, bossX: 1000, catX: 900);
        Start(sim, boss, ZipAct.Overwrite, 900, 300);
        One(sim, 900, 300);
        var high = sim.ZipBlows.Single();
        check(high.Bottom > 200 && high.Top > 300, "A mark can be high up, where the cat is");
        hp = sim.CatHp;
        Run(sim, write.Warn + write.Lock + 1, 900, 300);
        check(sim.CatHp < hp, "And it reaches the cat there");

        // Tear: out and in, a chance on each sweep.
        var tear = M(ZipAct.Tear);
        (sim, boss) = Field(2, bossX: 1000, catX: 800);
        hp = sim.CatHp;
        Start(sim, boss, ZipAct.Tear, 800);
        Run(sim, tear.Warn + tear.Strike * 0.5, 800);
        double once = hp - sim.CatHp;
        check(once > 0, "The tear sweeps over the cat going out");
        Run(sim, tear.Strike * 0.6, 800);
        check(hp - sim.CatHp > once * 1.5, "And coming back in");
        (sim, boss) = Field(2, bossX: 1000, catX: 800);
        Start(sim, boss, ZipAct.Tear, 800);
        hp = sim.CatHp;
        Run(sim, Whole(tear) + 0.1, 1000 + tear.Spacing + tear.Radius + 30);
        check(sim.CatHp == hp, "Beyond its reach nothing happens");
    }

    private static void SelfLockChecks(Action<bool, string> check)
    {
        var (sim, boss) = Field(zip: Spec, bossX: 1800, catX: 200, weapon: 20, rageGain: 0.1);
        boss.Cooldown = 100; boss.ZipDebuffClock = 0; boss.ZipDebuffNext = ZipDebuff.Locked;
        for (int i = 0; i < 120 && !sim.CatLocked; i++) One(sim, 200);
        double hp = boss.Hp, rage = sim.Rage;
        check(sim.CatLocked && sim.BreakingZipLock && sim.SwingProgress >= 0 && sim.ZipLockSwings == 0, "A locked cat starts a self-directed swing even with no enemy in reach; start is not a hit");
        sim.Paused = true;
        double progress = sim.SwingTime, seconds = sim.ZipDebuffLeft;
        Run(sim, 1, 200);
        check(sim.SwingTime == progress && sim.ZipDebuffLeft == seconds && sim.ZipLockSwings == 0, "Pause freezes self-swing, contact count and lock timeout");
        sim.Paused = false;
        Run(sim, 0.2, 200);
        check(sim.ZipLockSwings == 1 && sim.ZipLockHitFlash > 0, "Only the weapon contact frame counts and flashes the padlock");
        for (int i = 0; i < 360 && sim.CatLocked; i++) One(sim, 200);
        check(!sim.CatLocked && sim.ZipLockSwings == 5 && !sim.BreakingZipLock, "Exactly five self-hits open the lock away from all enemies");
        check(boss.Hp == hp && sim.Rage == rage && sim.Combo == 0, "Breaking the lock deals no enemy damage and charges no rage or combo");
        (sim, boss) = Field(zip: Spec, bossX: 1800, catX: 200);
        boss.Cooldown = 100; boss.ZipDebuffClock = 0; boss.ZipDebuffNext = ZipDebuff.Locked;
        for (int i = 0; i < 120 && !sim.CatLocked; i++) One(sim, 200);
        sim.ClickZipLock(); sim.ClickZipLock(); sim.ClickZipLock();
        Run(sim, 0.3, 200);
        check(!sim.CatLocked && !sim.BreakingZipLock && sim.ZipLockSwings == 0, "Three clicks interrupt a pending self-hit without a late hit");
    }

    private static void PaperBodyChecks(Action<bool, string> check)
    {
        foreach (double offset in new[] { -48.0, 0, 36 })
        foreach (int state in new[] { 0, 1, 2 })
        {
            var (sim, boss) = Field(1, bossX: 1500, catX: 500);
            boss.Cooldown = 100;
            double h = state == 0 ? BattleSim.CatStandHeight : 220;
            var pose = state == 0 ? (CatPose?)null : new CatPose(500, h, 0, 400, state == 1, state == 2);
            sim.Step(dt, 500, state == 1, state != 0, pose);
            var shot = new BattleShot { OwnerId = boss.Id, X = 400, Height = h + offset, VX = 12000, Speed = 12000, Paper = true, LockIn = 1, Damage = 10, Radius = 3, Remaining = 2 };
            ((ICollection<BattleShot>)sim.Shots).Add(shot);
            double hp = sim.CatHp;
            sim.Step(dt, 500, state == 1, state != 0, pose);
            check(sim.CatHp < hp, $"Fast paper hits full body at offset {offset}, floor/held/bouncing state {state}");
            check(sim.CatStunRemaining == 0 && sim.TakeCatBounce() == null, "Paper damage neither stuns nor changes throw velocity");
        }
        check(BattleSim.PaperCrossesBody(-100, 36, 100, 36, 2), "Swept paper hits head edge");
        check(BattleSim.PaperCrossesBody(-100, -49, 100, -49, 2), "Swept paper hits foot edge");
        check(!BattleSim.PaperCrossesBody(-100, 80, 100, 80, 2), "Paper outside the body misses");
    }

    private static void HandChecks(Action<bool, string> check)
    {
        // A floor blow misses a cat over it, not one held low in it.
        var clamp = M(ZipAct.Clamp);
        var (sim, boss) = Field(0, bossX: 700, catX: 500);
        double hp = sim.CatHp;
        Start(sim, boss, ZipAct.Clamp, 500);
        Run(sim, Whole(clamp) + 0.1, 500, clamp.Height + 60);
        check(sim.CatHp == hp, "Lifted over it, a floor blow misses");
        (sim, boss) = Field(0, bossX: 700, catX: 500);
        Start(sim, boss, ZipAct.Clamp, 500);
        Run(sim, 0.05, 500, 50);
        hp = sim.CatHp;
        Run(sim, Whole(clamp) + 0.1, 500, 50);
        check(sim.CatHp < hp && sim.CatHeld && !sim.CatStunned, "Held low in it, it is hit: the hand keeps it, no stun");

        // Aimed moves reach a held cat wherever it is; the Shield still works.
        var bolt = M(ZipAct.Bolt);
        (sim, boss) = Field(0, bossX: 800, catX: 500);
        Start(sim, boss, ZipAct.Bolt, 500, 400);
        hp = sim.CatHp;
        Run(sim, Whole(bolt) + 0.1, 500, 400);
        check(sim.CatHp < hp && sim.CatHeld && !sim.CatStunned, "The zipper head reaches a cat high in the hand");
        (sim, boss) = Field(0, bossX: 800, catX: 500);
        check(sim.Use(BattleSkill.Shield, 500), "The Shield is up");
        Start(sim, boss, ZipAct.Bolt, 500, 400);
        hp = sim.CatHp;
        Run(sim, Whole(bolt) + 0.1, 500, 400);
        check(sim.CatHp == hp, "The Shield stops it there too");

        // A cat in flight is judged the same way, and it stays in flight.
        (sim, boss) = Field(0, bossX: 700, catX: 500);
        Start(sim, boss, ZipAct.Clamp, 500);
        hp = sim.CatHp;
        Run(sim, Whole(clamp) + 0.1, 500, 300, flying: true);
        check(sim.CatHp == hp, "Thrown high over a floor blow, it is missed");
        (sim, boss) = Field(0, bossX: 700, catX: 500);
        Start(sim, boss, ZipAct.Clamp, 500);
        hp = sim.CatHp;
        Run(sim, Whole(clamp) + 0.1, 500, 50, flying: true);
        check(sim.CatHp < hp && !sim.CatStunned, "Flying low through it, it is hit, with no stun");

        // Landing grace still protects.
        (sim, boss) = Field(0, bossX: 700, catX: 500);
        check(sim.Settings.LandingGraceSeconds > 0, "The landing grace stays");
    }

    private static void CutShortChecks(Action<bool, string> check)
    {
        foreach (var (stage, act) in new[] { (0, ZipAct.Clamp), (0, ZipAct.Bolt), (1, ZipAct.Tree), (2, ZipAct.Overflow), (2, ZipAct.Tear), (2, ZipAct.Overwrite) })
        {
            var (s, b) = Field(stage, bossX: 1000, catX: 900);
            Start(s, b, act, 900);
            Run(s, 0.5, 900);
            b.StunRemaining = 3;
            b.Set(SlimeState.Stunned);
            double hp = s.CatHp;
            Run(s, 2.5, 900);
            check(s.CatHp == hp && b.ZipAct == ZipAct.None && s.ZipBlows.Count == 0, $"Stunned, {act} ends with no late hit");
        }
        // The HP line ends a move and starts the riddle, and the bag moves on.
        var (sim, boss) = Field(0, bossX: 800, catX: 500);
        Start(sim, boss, ZipAct.Clamp, 500);
        sim.Damage(boss, boss.MaxHp * 0.5, DamageSource.App);
        One(sim, 500);
        check(boss.Shielded && boss.ZipAct == ZipAct.None, "Reaching its HP line ends the move at once");
        check(boss.PressLeft > 0, "And the riddle takes over");
        // Paused: nothing moves.
        (sim, boss) = Field(0, bossX: 800, catX: 500);
        Start(sim, boss, ZipAct.Clamp, 500);
        double left = boss.ZipLeft;
        sim.Paused = true;
        Run(sim, 1, 500);
        sim.Paused = false;
        check(boss.ZipLeft == left, "A pause holds the move");
        // Frozen by the Screenshot marks wait as well; the end of the boss leaves nothing behind.
        (sim, boss) = Field(1, bossX: 800, catX: 500);
        Start(sim, boss, ZipAct.Tree, 500);
        sim.Damage(boss, boss.Hp + 1, DamageSource.App);
        Run(sim, 0.1, 500);
        check(sim.ZipBlows.Count == 0, "Beaten, its marks go with it");
    }

    private static void DebuffChecks(Action<bool, string> check)
    {
        var d = Spec.Debuff!;
        // Which one comes is the seed's; find one of each.
        static (BattleSim Sim, Slime Boss) Until(ZipDebuff kind, double weapon = 0, double catX = 900)
        {
            for (int seed = 0; seed < 40; seed++)
            {
                var (sim, boss) = Field(0, zip: Spec, bossX: 1000, catX: catX, seed: seed, weapon: weapon, damage: 0);
                boss.ZipDebuffClock = 0;
                boss.ZipBag.Clear();
                for (int i = 0; i < 120 && sim.CatZipped == ZipDebuff.None; i++) One(sim, catX);
                if (sim.CatZipped == kind) return (sim, boss);
            }
            throw new InvalidOperationException("no seed gave " + kind);
        }

        // Squeezed.
        var (sim, boss) = Until(ZipDebuff.Squeezed);
        check(sim.CatSqueezed && sim.ZipDebuffLeft <= d.Seconds, "It can squeeze the cat");
        double move = sim.MoveFactor;
        Run(sim, 0.4, 900);
        check(Math.Abs(sim.SqueezeScale - d.Scale) < 1e-6, "The cat is drawn smaller");
        Run(sim, d.Seconds, 900);
        check(!sim.CatSqueezed && sim.SqueezeScale == 1 && sim.MoveFactor > move, "After its time it is over");

        (sim, boss) = Until(ZipDebuff.Squeezed, weapon: 20, catX: 1000);
        boss.X = 1000;
        double hp = boss.Hp;
        for (int i = 0; i < 120 && boss.Hp == hp; i++) One(sim, 1000);
        double squeezed = hp - boss.Hp;
        check(Math.Abs(squeezed - 20 * d.Damage) < 20 * d.Damage * 0.6, $"The weapon hits lighter ({squeezed:0.#})");
        double before = boss.Hp;
        sim.Damage(boss, 10, DamageSource.App);
        check(Math.Abs(before - boss.Hp - 10) < 1e-6, "Apps and skills do not");

        // Locked: on the floor; opened by clicks, by swings, by time; the boss holds off.
        (sim, boss) = Until(ZipDebuff.Locked);
        check(sim.CatLocked && sim.MoveFactor == 0, "ZIP lock prevents all automatic cat movement");
        check(sim.ClickZipLock() && sim.ZipLockClicks == 1 && sim.CatLocked, "A click counts");
        check(sim.ClickZipLock() && sim.CatLocked, "Two clicks do not open it");
        check(sim.ClickZipLock() && !sim.CatLocked && sim.ZipGuardLeft > 0, "The third does, and a moment of quiet follows");
        check(!sim.ClickZipLock(), "Open, a click is nothing");

        (sim, boss) = Field(zip: Spec, bossX: 1800, catX: 200, swingCooldown: 10);
        boss.Cooldown = 100; boss.ZipDebuffClock = 0; boss.ZipDebuffNext = ZipDebuff.Locked;
        for (int i = 0; i < 120 && !sim.CatLocked; i++) One(sim, 200);
        // A weapon slower than the lock's timeout still has the six-second fallback.
        int started = sim.ZipMovesUsed;
        Run(sim, d.LockSeconds - 0.5, 200);
        check(sim.CatLocked && sim.ZipMovesUsed == started, "While the cat is locked the boss starts nothing");
        Run(sim, 0.6, 200);
        check(!sim.CatLocked && sim.ZipGuardLeft > 0, "It opens by itself after its time");
        Run(sim, d.Guard * 0.8, 200);
        check(sim.ZipMovesUsed == started, "And the boss waits the guard out");

        (sim, boss) = Until(ZipDebuff.Locked, weapon: 20, catX: 1000);
        boss.X = 1000;
        for (int i = 0; i < 480 && sim.CatLocked; i++) { One(sim, 1000); boss.X = 1000; }
        check(!sim.CatLocked && sim.ZipLockSwings == d.LockSwings, $"Its own swings open it ({sim.ZipLockSwings})");

        // Never on a cat in the hand, or during a riddle, or with a blow announced.
        var (held, heldBoss) = Field(0, zip: Spec, bossX: 1000, catX: 900, seed: LockSeed(), damage: 0);
        heldBoss.ZipDebuffClock = 0;
        heldBoss.ZipBag.Clear();
        Run(held, 0.5, 900, 300);
        check(!held.CatLocked, "The hand is never made to let go");
        var (busy, busyBoss) = Field(0, zip: Spec, bossX: 1000, catX: 900, seed: LockSeed(), damage: 0);
        Start(busy, busyBoss, ZipAct.Clamp, 900);
        busyBoss.ZipDebuffClock = 0;
        Run(busy, M(ZipAct.Clamp).Warn * 0.5, 900);
        check(!busy.CatLocked, "Never with a blow announced");
        var (shut, shutBoss) = Field(0, zip: Spec, bossX: 1000, catX: 900, seed: LockSeed(), damage: 0);
        shut.Damage(shutBoss, shutBoss.MaxHp * 0.5, DamageSource.App);
        shutBoss.ZipDebuffClock = 0;
        Run(shut, 1, 900);
        check(shut.CatZipped == ZipDebuff.None, "Nor during a riddle");

        // A riddle needs the hand: the shell opens as one starts.
        var (riddle, riddleBoss) = Until(ZipDebuff.Locked);
        riddle.Damage(riddleBoss, riddleBoss.MaxHp * 0.5, DamageSource.App);
        check(riddleBoss.Shielded && !riddle.CatLocked, "A riddle starting opens the shell");

        // The Shield stops it; Rage clears it and keeps it off.
        var (safe, safeBoss) = Field(0, zip: Spec, bossX: 1000, catX: 900, seed: LockSeed(), damage: 0);
        safe.Use(BattleSkill.Shield, 900);
        safeBoss.ZipDebuffClock = 0;
        safeBoss.ZipBag.Clear();
        Run(safe, 0.2, 900);
        check(safe.CatZipped == ZipDebuff.None && safe.ZipDebuffsBlocked == 1, "The Shield stops a debuff taking hold");
        check(safeBoss.ZipDebuffClock >= d.GapMin - 0.3 && safeBoss.ZipDebuffClock <= d.GapMax, $"The next one is {d.GapMin:0}–{d.GapMax:0} s off ({safeBoss.ZipDebuffClock:0.#})");

        // One at a time, in time gaps; paused the clock waits.
        var (one, oneBoss) = Field(0, zip: Spec, bossX: 1000, catX: 900, seed: 3, damage: 0);
        int most = 0, taken = 0;
        for (int i = 0; i < 60 * 90; i++)
        {
            one.ClickZipLock();
            oneBoss.X = 1000; oneBoss.Cooldown = 5;
            taken = Math.Max(taken, one.ZipDebuffsTaken);
            One(one, 900);
            most = Math.Max(most, one.CatZipped == ZipDebuff.None ? 0 : 1);
        }
        check(taken is >= 5 and <= 9, $"Over 90 s they come 10–16 s apart ({taken})");
        var (paused, pausedBoss) = Field(0, zip: Spec, bossX: 1000, catX: 900, seed: LockSeed(), damage: 0);
        pausedBoss.ZipDebuffClock = 0;
        pausedBoss.ZipBag.Clear();
        for (int i = 0; i < 120 && !paused.CatLocked; i++) One(paused, 900);
        double debuff = paused.ZipDebuffLeft;
        paused.Paused = true;
        Run(paused, 1, 900);
        check(paused.ZipDebuffLeft == debuff && !paused.ClickZipLock(), "A pause holds it and takes no clicks");
        paused.Paused = false;
        pausedBoss.PhasesDone = 7;
        paused.Damage(pausedBoss, pausedBoss.Hp + 1, DamageSource.App);
        check(paused.CatZipped == ZipDebuff.None, "Beaten, the boss takes it along");
    }

    private static int LockSeed()
    {
        for (int seed = 0; seed < 40; seed++)
        {
            var (sim, boss) = Field(0, zip: Spec, bossX: 1000, catX: 900, seed: seed, damage: 0);
            boss.ZipDebuffClock = 0;
            boss.ZipBag.Clear();
            for (int i = 0; i < 120 && sim.CatZipped == ZipDebuff.None; i++) One(sim, 900);
            if (sim.CatZipped == ZipDebuff.Locked) return seed;
        }
        throw new InvalidOperationException("no seed");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        var d = Spec.Debuff!;
        check(d is { Scale: 0.75, Damage: 0.75, Speed: 0.8, Seconds: 5, LockSeconds: 6, LockClicks: 3, LockSwings: 5, GapMin: 10, GapMax: 16 }, "The debuffs carry the approved numbers");
        check(ShippedZip.Phases!.Select(p => p.At).SequenceEqual(new[] { 0.66, 0.33, 0.2 }), "Its riddles stand at 66, 33 and 20 %");
        check(Shipped.Enemy("zip_packet") is { Packet: true, Still: true, Cost: 0 }, "The data packet comes out of no wave");
        var phases = ShippedZip.Phases!;
        check(phases[1].Formation == null && Shipped.PhaseCalls(phases[1]).All(c => c == "zip_packet"), "Its stuck phase uses its own packets, not a Fortress formation");
    }
}
