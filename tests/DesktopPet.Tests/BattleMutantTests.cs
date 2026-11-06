using DesktopPet;

internal static class BattleMutantTests
{
    const double Dt = 1.0 / 60;
    static (BattleSim Sim, Slime Boss) Field(int seed = 17)
    {
        var data = BattleData.Settings;
        var settings = new BattleSettings
        {
            FirstWaveDelay = 0, RageAuto = false, RageHitGain = 0, RageHurtGain = 0, CatMaxHp = 1000,
            Weapons = new[] { data.Weapon("sword") with { Damage = 10, StunChance = 0 } },
            Enemies = data.EnemyTypes.ToArray(),
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "mutant_king", Count = 1 } } } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: seed);
        sim.SetSky(900);
        for (int i = 0; i < 200 && sim.Boss?.Targetable != true; i++) sim.Step(Dt, 750, false);
        var boss = sim.Boss!;
        sim.Step(Dt, 750, true, true, new CatPose(750, 800, 0, 0, true, false));
        boss.X = 1000;
        return (sim, boss);
    }
    static void RunFor(BattleSim s, double seconds, double x = 750, double h = 800, bool flying = false)
    {
        for (int i = 0; i < (int)Math.Ceiling(seconds / Dt) && !s.Over; i++)
            s.Step(Dt, x, true, !flying, new CatPose(x, h, flying ? 600 : 0, 0, !flying, flying));
    }
    public static void Run(Action<bool, string> check)
    {
        var (sim, boss) = Field();
        double originalX = boss.X;
        RunFor(sim, 0.6, 100, 800);
        check(boss.X < originalX - 5 && boss.Mutant!.Velocity < 0, "Mutant advances toward distant cat between attacks instead of standing still");
        double stoppedX = boss.X;
        sim.Paused = true; RunFor(sim, 0.4, 100, 800); sim.Paused = false;
        check(boss.X == stoppedX, "Pause freezes mutant locomotion and its movement pose");
        check(boss.Type.Mutant && boss.Mutant != null && boss.Type.Phases == null, "Mutant owns its state machine without royal phases");
        sim.Damage(boss, 100000, DamageSource.Bomb);
        var m = boss.Mutant!;
        check(boss.Hp == 660 && m.Mode == MutantMode.Bumps && m.Bumps.Count == 6, "Overkill stops at 66% and starts six-bump mech");
        check(m.Bumps.Count(b => b.Side == 0) == 3 && m.Bumps.Count(b => b.Side != 0) == 3 && m.Bumps.Where(b => b.Side == 0).All(b => Math.Abs(b.X - boss.X) > boss.Width / 2 + 30), "Three floor and three wall bumps spawn outside the boss");
        check(!sim.ApplyStatus(boss, SlimeStatus.Poison, 4, 10) && !sim.ApplyStatus(boss, SlimeStatus.Chill, 4, 0.5), "Mech rejects poison and chill");
        double hp = boss.Hp, clock = m.Deadline - sim.Time;
        sim.Damage(boss, 100000, DamageSource.Weapon);
        check(boss.Hp == hp, "Bump mech body ignores weapon damage");
        sim.Paused = true; RunFor(sim, 1);
        check(Math.Abs(m.Deadline - sim.Time - clock) < 0.001, "Pause freezes mutant mech deadline");
        sim.Paused = false;
        var wrong = m.Bumps.Last();
        sim.Step(Dt, wrong.X, true, true, new CatPose(wrong.X, wrong.H + 60, 0, 400, false, true));
        check(m.NextBump == 0, "Wrong-order bump contact never advances the puzzle");
        for (int round = 0; round < 3; round++)
        {
            foreach (var b in m.Bumps.ToArray())
            {
                if (b.Side == 0)
                {
                    sim.Step(Dt, b.X, true, true, new CatPose(b.X, 150, 0, 400, false, true));
                    sim.Step(Dt, b.X, true, true, new CatPose(b.X, 65, 0, 400, false, true));
                }
                else RunFor(sim, 1.2, b.X, b.H);
            }
            check(m.Mode == MutantMode.Core, $"Bump round {round + 1} opens the core after all six contacts");
            sim.Damage(boss, 10, DamageSource.Weapon);
        }
        check(m.Phase == 2 && m.Mode == MutantMode.Transition, "Three core hits enter phase two transition");
        hp = boss.Hp; sim.Damage(boss, 100000, DamageSource.Bomb);
        check(boss.Hp == hp && !sim.ApplyStatus(boss, SlimeStatus.Stick, 3), "Transition blocks damage and debuffs");
        RunFor(sim, 3.2);
        sim.Damage(boss, 100000, DamageSource.Weapon);
        check(m.Mode == MutantMode.Ping && boss.Hp == 330, "Phase two clamps to 33% before ping pong");
        RunFor(sim, 45);
        check(m.Phase == 3 && m.Mode == MutantMode.Combat && boss.Hp == boss.MaxHp, "Six missed cores heal fully but still advance directly to phase three");
        check(m.Bags.Count == 4, "Phase three creates four airborne bags");
        sim.Damage(boss, 100000, DamageSource.Poison);
        check(boss.Hp == 1 && m.Mode == MutantMode.Roar && m.TerrainCleared, "At 1HP final roar immediately disables all permanent terrain");
        check(m.Zones.Count == 0 && m.FadeAt == sim.Time, "Final entry cancels pending attacks and starts terrain dissolve");
        hp = sim.CatHp;
        RunFor(sim, 1, 20, 56);
        check(sim.CatHp == hp, "Cleared floor cannot hurt a held cat during the roar");
        RunFor(sim, 4.1);
        check(m.Mode == MutantMode.Core && m.Bags.Count == 0, "Five-second roar consumes old bags and opens final core");
        sim.Damage(boss, 1000, DamageSource.Poison);
        check(m.CoreHits == 0, "Poison cannot consume final core hits");
        sim.Damage(boss, 1000, DamageSource.Weapon);
        sim.Damage(boss, 1000, DamageSource.Combo);
        check(m.CoreHits == 1 && m.Mode == MutantMode.AllIn && boss.Hp == 1, "Multi-hit sequence consumes only one final opening");

        // Persistent intersecting columns: a single hit, fall, landing stun, then time to pick up.
        m.Zones.Clear(); m.AllIn = 0; m.Clock = 100;
        RunFor(sim, 3, 100, 800);
        m.Zones.Add(new MutantZone { X = 750, H = 0, W = 75, Height = 900, Life = 30, Shape = MutantShape.Column, Falling = true });
        m.Zones.Add(new MutantZone { X = 800, H = 450, W = 1600, Height = 65, Life = 30, Shape = MutantShape.Column, Falling = true });
        hp = sim.CatHp;
        RunFor(sim, 0.2, 750, 480);
        check(Math.Abs(hp - sim.CatHp - sim.CatMaxHp * 0.05) < 0.01 && sim.MutantForcedFall, "Crossing two columns deals one 5% hit and forces held cat down");
        hp = sim.CatHp;
        RunFor(sim, 3, 750, 400, true);
        check(sim.CatHp == hp, "Long fall stays protected even after hit cooldown expires");
        sim.Step(Dt, 750, false);
        check(sim.CatStunned && !sim.MutantForcedFall, "Forced landing begins one-second stun");
        for (int i = 0; i < 100; i++) sim.Step(Dt, 750, false);
        check(!sim.CatStunned && sim.CatHp == hp && sim.MutantRescuing, "Cat can be picked up after stun without an immediate column combo");

        (sim, boss) = Field(19); m = boss.Mutant!;
        m.Phase = 2; m.Mode = MutantMode.Combat; m.Clock = 100; m.Age = 6;
        RunFor(sim, 0.2, 40, 56);
        check(sim.MutantNoise, "Active permanent floor applies Stutter to held cat on contact");
        for (int i = 0; i < 135; i++)
        {
            sim.SetMutantPointer(40 + 20 * Math.Sin(i * 0.25), 56);
            sim.Step(Dt, 40, true, true, new CatPose(40, 56, 0, 0, true, false));
        }
        check(!sim.MutantNoise, "Rubbing across cat for about two seconds clears Stutter");
        check(BattleSim.BossPushImmune(new Slime { Type = BattleData.Settings.Enemy("zip"), ZipStage = 1, PhasesDone = 1 }), "Zip phase two rejects external stun and push");

        // Six genuine thrown-cat interceptions, including the return flight, open the phase-two core.
        (sim, boss) = Field(31); m = boss.Mutant!;
        m.Phase = 2; m.Mode = MutantMode.Ping; m.Clock = 0; boss.Hp = 330;
        for (int i = 0; i < 4000 && m.Mode != MutantMode.Core && !sim.Over; i++)
        {
            if (m.Projectile && m.ProjectileAge >= 0)
                sim.Step(Dt, m.CoreX, true, false, new CatPose(m.CoreX, m.CoreH, 600, 0, false, true));
            else RunFor(sim, Dt);
        }
        check(m.Throws == 6 && m.Misses == 0 && m.Mode == MutantMode.Core && !sim.Over, "All six reflected cores visibly return, stagger boss and open its core");
        sim.Damage(boss, 10, DamageSource.Weapon);
        check(m.Phase == 3 && m.Mode == MutantMode.Transition, "Clean ping pong ends with one core strike and advances to phase three");

        (sim, boss) = Field(32); m = boss.Mutant!;
        sim.Damage(boss, 100000, DamageSource.Weapon);
        m.NextAttack = double.PositiveInfinity;
        double beforeFail = sim.CatHp;
        RunFor(sim, 62.5);
        check(m.Phase == 2 && Math.Abs(boss.Hp - boss.MaxHp) < 0.001 && Math.Abs(beforeFail - sim.CatHp - sim.CatMaxHp * 0.5) < 0.01,
            "60-second bump failure deals 50% and advances to phase two at full HP");

        // Ending an all-in deactivates hitboxes before its dissolve, for every selected move.
        for (int attack = 0; attack < 3; attack++)
        {
            (sim, boss) = Field(40 + attack); m = boss.Mutant!;
            m.Phase = 3; m.Final = m.TerrainCleared = true; m.Mode = MutantMode.AllIn; m.AllIn = attack; m.Clock = 0; boss.Hp = 1;
            for (int i = 0; i < 1700 && m.Mode != MutantMode.Core && !sim.Over; i++) RunFor(sim, Dt, 65, 800);
            check(m.Mode == MutantMode.Core && m.Zones.Count == 0 && !sim.Over, $"All-in {attack + 1} finishes and removes every hazard before next opening");
        }
    }
}
