using DesktopPet;

internal static class BossTacticsTests
{
    const double Dt = 1.0 / 60;
    static readonly BattleSettings Data = BattleData.Settings;

    static (BattleSim Sim, Slime Boss) Field(bool mother, BossTacticsSpec? tactics = null, IReadOnlyList<BattleSkill>? skills = null)
    {
        var kind = Data.Enemy(mother ? "mother" : "mutant_king");
        var boss = new EnemyType { Id = kind.Id, Boss = true, MaxHp = 1000, Speed = 0, Damage = 25, Range = 100,
            Scale = 2, Brood = kind.Brood, Rain = mother ? kind.Rain : new RainSpec { PuddleSeconds = 5 }, BossTactics = tactics ?? kind.BossTactics ?? new BossTacticsSpec(), SummonCap = 6 };
        var settings = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0.01, CatMaxHp = 10000,
            Weapons = new[] { Data.Weapon("sword") with { Damage = 0, StunChance = 0 } }, RageHitGain = 0,
            Enemies = new[] { boss, Data.Enemy("fuser"), Data.Enemy("giant") },
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = boss.Id, Count = 1 } } } } };
        var sim = new BattleSim(settings, 0, 1600, seed: 17, skills: skills);
        for (int i = 0; i < 200 && !sim.Slimes.Any(s => s.IsBoss && s.Targetable); i++) sim.Step(Dt, 100, false);
        var s = sim.Slimes.Single(s => s.IsBoss);
        s.X = 1000; s.BroodClock = 100; s.TacticClock = 100;
        return (sim, s);
    }

    static void RunFor(BattleSim sim, double seconds, double x = 100, bool held = false)
    {
        for (int i = 0; i < (int)Math.Ceiling(seconds / Dt) && !sim.Over; i++)
            sim.Step(Dt, x, held, held, held ? new CatPose(x, 250, 0, 0, true, false) : null);
    }

    static void Move(Slime boss, BossMove move, double target = 100)
    {
        boss.Special = move; boss.SpecialAt = target; boss.SpecialFrom = boss.X;
        boss.SpecialLeft = boss.SpecialDuration = 0.8; boss.Facing = target < boss.X ? -1 : 1;
    }

    public static void Run(Action<bool, string> check)
    {
        var (sim, mother) = Field(true);
        mother.BroodClock = 0;
        RunFor(sim, 1.8);
        var nest = sim.Slimes.FirstOrDefault(s => s.BroodNest && s.Targetable);
        check(nest != null && nest.Link == null && sim.Clutches == 1, "Advanced mother lays a damageable nest that cannot merge");
        if (nest != null)
        {
            nest.Set(SlimeState.Stunned); nest.StunRemaining = 0.25;
            double clock = nest.NestLeft;
            RunFor(sim, 0.15);
            check(nest.NestLeft == clock, "Stunning a nest delays its hatch");
            RunFor(sim, 0.3);
            check(nest.State != SlimeState.Stunned && nest.NestLeft < clock, "Nest stun expires without locking its timer forever");
            sim.Damage(nest, 10000, DamageSource.Tool);
            RunFor(sim, 4);
            check(sim.NestsBroken == 1 && !sim.Slimes.Any(s => s.SummonedBy == mother.Id && s.Targetable), "Breaking a nest prevents its clutch from spawning");
        }

        (sim, mother) = Field(true); mother.Hp = 550; mother.BroodClock = 0;
        RunFor(sim, 1);
        check(sim.Slimes.Count(s => s.BroodNest && s.State != SlimeState.Defeated) == 2, "Below sixty percent the mother lays two staggered nests");
        RunFor(sim, 6);
        check(sim.Slimes.Count(s => s.SummonedBy == mother.Id && s.State != SlimeState.Defeated) <= 6, "Hatching both nests respects the mother's summon cap");

        (sim, mother) = Field(true, skills: new[] { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Bomb, BattleSkill.Scissors, BattleSkill.Shield });
        var giant = sim.AddSlime(900, Data.Enemy("giant")); giant.Set(SlimeState.Moving);
        giant.SummonedBy = mother.Id; giant.Nursing = true; giant.Hp = giant.MaxHp * 0.4;
        double hp = giant.Hp;
        RunFor(sim, 0.5);
        check(giant.Hp == hp && sim.HpFed == 0, "A full-health advanced mother does not consume her nursing child's HP");
        check(mother.NurseCord == giant.Id, "Mother protects a wounded nursing giant with a cord");
        // A carried-cat stroke across the sagged cord at body height leaves it; only the Scissors cut.
        double middle = (mother.X + giant.X) / 2;
        sim.Step(Dt, middle, true, true, new CatPose(middle, 0, 0, 800, true, false));
        sim.Step(Dt, middle, true, true, new CatPose(middle, 130, 0, -800, true, false));
        check(sim.NurseCordsCut == 0 && mother.NurseCord == giant.Id, "Dragging the cat through the rescue cord no longer cuts it");
        sim.Step(Dt, 100, false); // Skills stay locked while the cat is lifted.
        check(sim.Arm(BattleSkill.Scissors) && sim.UseOnScreen(BattleSkill.Scissors, middle, 30), "The Scissors can stroke across the rescue cord");
        for (int i = 0; i < 120 && sim.Cut != null; i++) { sim.Step(Dt, 100, false); sim.ClearEvents(); }
        check(sim.NurseCordsCut == 1 && mother.NurseCord == 0 && mother.State == SlimeState.Stunned && !giant.Nursing,
            "The Scissors cut the rescue cord and stagger both ends");

        (sim, mother) = Field(true); mother.Hp = 190;
        RunFor(sim, 0.1); double spent = mother.Hp;
        RunFor(sim, 0.2);
        check(mother.LastBrood && spent == mother.Hp && spent == 130, "The last clutch costs health exactly once");

        (sim, mother) = Field(true); mother.Hp = 190;
        Slime? occupiedNest = null;
        for (int i = 0; i < 6; i++)
        {
            var child = sim.AddSlime(500 + i * 60, Data.Enemy("fuser"));
            child.Set(SlimeState.Moving); child.SummonedBy = mother.Id; child.Link = null;
            if (i < 2) { child.BroodNest = true; child.NestLeft = 100; occupiedNest = child; }
        }
        RunFor(sim, 0.1);
        check(!mother.LastBrood && mother.Hp == 190, "A full nursery defers the last clutch without spending the mother's HP");
        sim.Damage(occupiedNest!, 10000, DamageSource.Tool);
        RunFor(sim, 0.1);
        check(mother.LastBrood && mother.Hp == 130 && sim.Slimes.Count(s => s.BroodNest && s.State != SlimeState.Defeated) == 2,
            "The deferred last clutch spends HP only once room becomes available");

        (sim, mother) = Field(true); mother.MotherFury = 3; mother.TacticClock = 0;
        RunFor(sim, 0.1, 850);
        check(mother.Special == BossMove.Charge, "Losing three children announces the mother's charge");
        double from = mother.X;
        RunFor(sim, 1.2, 850);
        check(mother.X < from && mother.X > mother.SpecialAt, "The mother's charge moves over time instead of teleporting");
        double chargeHp = sim.CatHp;
        RunFor(sim, 0.6, 850, true);
        check(sim.CatHp == chargeHp, "Holding the cat avoids the mother's charge");

        var (kingSim, king) = Field(false);
        Move(king, BossMove.Rain, 500);
        RunFor(kingSim, 0.9, 500);
        var rain = kingSim.Goos.Where(g => g.Rain).ToArray();
        check(rain.Length == 8 && rain.Select(g => Math.Round(g.Remaining, 1)).Distinct().Count() == 2,
            "Advanced rain announces two staggered beats with fixed locations");
        double catHp = kingSim.CatHp;
        RunFor(kingSim, 3, 500, true);
        check(kingSim.CatHp == catHp, "Both rain beats miss a carried cat");
        check(kingSim.Puddles.Count > 0 && kingSim.Puddles.All(p => p.OwnerId == king.Id), "Advanced rain puddles retain their owner for absorption");
        Move(king, BossMove.Absorb); king.SpecialLeft = king.SpecialDuration = 2.4;
        RunFor(kingSim, 2.5, 100, true);
        check(king.GooArmor > 0 && king.GooArmor <= king.MaxHp * 0.1 && kingSim.Puddles.Count == 0,
            "A completed absorb consumes owned puddles and caps regenerated armor at ten percent HP");

        (kingSim, king) = Field(false); Move(king, BossMove.Rain, 500);
        RunFor(kingSim, 3.8, 100, true);
        Move(king, BossMove.Absorb); king.SpecialLeft = king.SpecialDuration = 2.4;
        kingSim.Step(Dt, 10, true, true, new CatPose(10, 4, 1200, 0, true, false));
        kingSim.Step(Dt, 1590, true, true, new CatPose(1590, 4, 1200, 0, true, false));
        check(kingSim.Puddles.Count > 0 && kingSim.Puddles.All(p => !p.DrainCut), "Dragging the cat across golden streams no longer interrupts them");
        RunFor(kingSim, 2.5, 100, true);
        check(king.GooArmor > 0, "Streams the cat dragged through still feed the king armor");

        (kingSim, king) = Field(false); Move(king, BossMove.Rain, 500);
        RunFor(kingSim, 0.9);
        double remaining = kingSim.Goos[0].Remaining;
        kingSim.Step(0, 500, false);
        check(kingSim.Goos[0].Remaining == remaining, "Zero simulation time does not advance announced boss projectiles");
        kingSim.Damage(king, 10000, DamageSource.Tool);
        check(kingSim.Goos.All(g => g.OwnerId != king.Id), "Defeating an advanced boss clears its pending special projectiles");

        (kingSim, king) = Field(false); Move(king, BossMove.Wave, 100);
        RunFor(kingSim, 0.9);
        check(kingSim.Goos.Count > 0 && kingSim.Goos.Select(g => g.X).Distinct().Count() == kingSim.Goos.Count,
            "The floor wave travels outward through distinct warned positions");

        (kingSim, king) = Field(false); Move(king, BossMove.Sweep, 800);
        king.Set(SlimeState.Stunned); king.StunRemaining = 0.2;
        RunFor(kingSim, 1, 875);
        check(king.Special == BossMove.None && kingSim.CatHp == kingSim.CatMaxHp, "Stunning a boss cancels its announced special without a delayed hit");

        (kingSim, king) = Field(false);
        king.GooArmor = 50;
        double before = king.Hp;
        kingSim.Damage(king, 70, DamageSource.Tool);
        check(king.GooArmor == 0 && king.Hp == before - 20, "Regenerated goo armor is finite and absorbs damage before HP");
        Move(king, BossMove.Rest); king.SpecialCount = 4;
        before = king.Hp;
        kingSim.Damage(king, 10, DamageSource.Tool);
        check(Math.Abs(king.Hp - (before - 14)) < 0.001, "Crown overload exposes the boss to forty percent extra damage");

        (kingSim, king) = Field(false); Move(king, BossMove.Burst, 100);
        kingSim.SetArea(-800, 800);
        check(king.SpecialAt == -700 && king.SpecialFrom == 200, "Boss warning anchors follow a changed monitor area");
        king.Hp = 240; Move(king, BossMove.Sweep, 800);
        RunFor(kingSim, 0.9, 0);
        check(king.Special == BossMove.Burst, "Final phase chains a second announced special");
        RunFor(kingSim, 1.2, 0);
        check(king.Special == BossMove.Rest, "Every final-phase pair ends in a compulsory opening");

        var custom = new BossTacticsSpec { SweepReach = 180, SweepRadius = 70, WaveSpacing = 95, WaveRadius = 52 };
        (kingSim, king) = Field(false);
        var normalA = kingSim.AddSlime(300, Data.Enemy("fuser"));
        var normalB = kingSim.AddSlime(350, Data.Enemy("fuser"));
        kingSim.AddPuddle(new BattleGoo { OwnerId = normalA.Id, X = 500, PuddleRadius = 40, PuddleSeconds = 3, PuddleDamage = 2 });
        kingSim.AddPuddle(new BattleGoo { OwnerId = normalB.Id, X = 500, PuddleRadius = 40, PuddleSeconds = 5, PuddleDamage = 2 });
        check(kingSim.Puddles.Count == 1 && kingSim.Puddles[0].Life == 5,
            "Ordinary spitters still refresh one shared puddle, regardless of owner");
        kingSim.AddPuddle(new BattleGoo { OwnerId = king.Id, X = 500, PuddleRadius = 40, PuddleSeconds = 5 });
        check(kingSim.Puddles.Count == 2 && kingSim.Puddles.Count(p => p.OwnerId == king.Id) == 1,
            "An advanced boss's puddle stays separate so its absorption cannot consume ordinary goo");
        (kingSim, king) = Field(false, custom); Move(king, BossMove.Sweep, 500);
        var edge = BattleSim.BossZone(king, BossMove.Sweep);
        check(edge.X == 820 && edge.Radius == 70 && kingSim.BossThreatens(king, 750) && !kingSim.BossThreatens(king, 749),
            "Custom boss geometry exposes the exact warning boundary to the balance player");
        RunFor(kingSim, 0.9, 750);
        check(kingSim.CatHp < kingSim.CatMaxHp, "A sweep hits at its configured warning boundary");
        (kingSim, king) = Field(false, custom); Move(king, BossMove.Sweep, 500);
        RunFor(kingSim, 0.9, 749);
        check(kingSim.CatHp == kingSim.CatMaxHp, "One DIP beyond the configured warning boundary is safe");
        (kingSim, king) = Field(false, custom); Move(king, BossMove.Wave, 500);
        RunFor(kingSim, 0.9);
        check(kingSim.Goos.Count == BattleSim.BossWaveSteps && kingSim.Goos.All(g => g.Radius == 52)
            && kingSim.Goos.Select(g => g.X).SequenceEqual(Enumerable.Range(1, BattleSim.BossWaveSteps).Select(i => BattleSim.BossWaveZone(king, i).X)),
            "Wave projectile positions and radii use the same configurable geometry as their warnings");

        foreach (var move in new[] { BossMove.Sweep, BossMove.Burst })
            foreach (var mode in new[] { "floor", "held", "flying", "shield", "rage", "pause", "snapshot" })
            {
                (kingSim, king) = Field(false); Move(king, move, 900);
                double x = move == BossMove.Sweep ? 875 : 1000;
                if (mode == "shield") kingSim.Use(BattleSkill.Shield, x);
                if (mode == "rage")
                {
                    typeof(BattleSim).GetProperty(nameof(BattleSim.Rage))!.SetValue(kingSim, 1.0);
                    check(kingSim.StartRage(), "Interaction matrix starts the actual rage stance");
                }
                if (mode == "pause") kingSim.Paused = true;
                if (mode == "snapshot") kingSim.FrozenRemaining = 2;
                for (int i = 0; i < 55; i++)
                {
                    bool lifted = mode is "held" or "flying";
                    // Step's catHeld means controlled by the window (carried OR thrown), not InHand.
                    kingSim.Step(Dt, x, lifted, lifted,
                        lifted ? new CatPose(x, 250, 0, 0, mode == "held", mode == "flying") : null);
                }
                check(mode is "floor" or "rage" ? kingSim.CatHp < kingSim.CatMaxHp : kingSim.CatHp == kingSim.CatMaxHp,
                    $"Boss interaction matrix: {move}, {mode} respects the approved damage rules");
                if (mode == "rage") check(kingSim.ShoveLeft == 0 && !kingSim.CatStunned, "Rage prevents boss knockback and stun while ordinary damage still applies");
                if (mode == "floor" && move == BossMove.Burst)
                {
                    check(kingSim.ShoveLeft > 0, "A landed burst starts the approved floor knockback");
                    typeof(BattleSim).GetProperty(nameof(BattleSim.Rage))!.SetValue(kingSim, 1.0);
                    check(kingSim.StartRage() && kingSim.ShoveLeft == 0 && kingSim.RageCleansed == 1,
                        "Starting rage also clears knockback that was already in progress");
                }
                if (mode is "pause" or "snapshot") check(king.SpecialLeft == 0.8, $"{mode} freezes the boss warning clock");
            }

        check(Data.Enemy("king_c1").BossTactics == null && Data.Enemy("zip").BossTactics == null,
            "Advanced mechanics opt in only for the two approved bosses");
        try
        {
            BattleData.Validate(new BattleSettings { Enemies = new[] { new EnemyType { Id = "mother", Boss = true, Brood = new(), BossTactics = new() { Warning = 0.1 } } } });
            check(false, "Invalid unreadable boss windup is rejected");
        }
        catch (System.IO.InvalidDataException) { check(true, "Invalid unreadable boss windup is rejected"); }
    }
}
