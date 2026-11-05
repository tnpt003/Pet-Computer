using DesktopPet;

internal static class BossTacticsTests
{
    const double Dt = 1.0 / 60;
    static readonly BattleSettings Data = BattleData.Settings;

    static (BattleSim Sim, Slime Boss) Field(bool mother)
    {
        var kind = Data.Enemy(mother ? "mother" : "mutant_king");
        var boss = new EnemyType { Id = kind.Id, Boss = true, MaxHp = 1000, Speed = 0, Damage = 25, Range = 100,
            Scale = 2, Brood = kind.Brood, Rain = kind.Rain, BossTactics = kind.BossTactics, SummonCap = 6 };
        var settings = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0.01, CatMaxHp = 10000,
            Weapons = new[] { Data.Weapon("sword") with { Damage = 0, StunChance = 0 } }, RageHitGain = 0,
            Enemies = new[] { boss, Data.Enemy("fuser"), Data.Enemy("giant") },
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = boss.Id, Count = 1 } } } } };
        var sim = new BattleSim(settings, 0, 1600, seed: 17);
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

        (sim, mother) = Field(true);
        var giant = sim.AddSlime(900, Data.Enemy("giant")); giant.Set(SlimeState.Moving);
        giant.SummonedBy = mother.Id; giant.Nursing = true; giant.Hp = giant.MaxHp * 0.4;
        double hp = giant.Hp;
        RunFor(sim, 0.5);
        check(giant.Hp == hp && sim.HpFed == 0, "A full-health advanced mother does not consume her nursing child's HP");
        check(mother.NurseCord == giant.Id, "Mother protects a wounded nursing giant with a cord");
        // A real carried-cat stroke crosses the sagged cord at body height.
        double middle = (mother.X + giant.X) / 2;
        sim.Step(Dt, middle, true, true, new CatPose(middle, 0, 0, 800, true, false));
        sim.Step(Dt, middle, true, true, new CatPose(middle, 130, 0, -800, true, false));
        check(sim.NurseCordsCut == 1 && mother.NurseCord == 0 && mother.State == SlimeState.Stunned && !giant.Nursing,
            "Dragging the cat through the rescue cord cuts it and staggers both ends");

        (sim, mother) = Field(true); mother.Hp = 190;
        RunFor(sim, 0.1); double spent = mother.Hp;
        RunFor(sim, 0.2);
        check(mother.LastBrood && spent == mother.Hp && spent == 130, "The last clutch costs health exactly once");

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
        check(kingSim.Puddles.Count > 0 && kingSim.Puddles.All(p => p.DrainCut), "Dragging the cat across golden streams interrupts their absorption");
        RunFor(kingSim, 2.5, 100, true);
        check(king.GooArmor == 0, "Cut absorption streams give the king no armor");

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
