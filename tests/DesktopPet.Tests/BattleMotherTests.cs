using DesktopPet;

internal static class BattleMotherTests
{
    const double Dt = 1.0 / 60;
    static (BattleSim Sim, Slime Boss) Field(int seed = 17)
    {
        var data = BattleData.Settings;
        var settings = new BattleSettings { FirstWaveDelay = 0, RageAuto = false, RageHitGain = 0, RageHurtGain = 0, CatMaxHp = 10000,
            Weapons = new[] { data.Weapon("sword") with { Damage = 10, StunChance = 0 } }, Enemies = data.EnemyTypes.ToArray(),
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "mother", Count = 1 } } } } };
        var sim = new BattleSim(settings, 0, 1600, seed: seed); sim.SetSky(900);
        for (int i = 0; i < 200 && sim.Boss?.Targetable != true; i++) sim.Step(Dt, 100, false);
        var boss = sim.Boss!; sim.Step(Dt, 100, true, true, new CatPose(100, 800, 0, 0, true, false)); boss.X = 1200;
        return (sim, boss);
    }
    static void RunFor(BattleSim sim, double seconds, double x = 100, double h = 800, bool held = true)
    {
        for (int i = 0; i < Math.Ceiling(seconds / Dt) && !sim.Over; i++) sim.Step(Dt, x, held, held, held ? new CatPose(x, h, 0, 0, true, false) : null);
    }
    static void Phase3(BattleSim sim, Slime boss)
    {
        sim.Damage(boss, 100000, DamageSource.Weapon); RunFor(sim, 40.2);
        sim.Damage(boss, 100000, DamageSource.Bomb); RunFor(sim, 45.2);
    }
    public static void Run(Action<bool, string> check)
    {
        var (sim, boss) = Field();
        check(boss.Mother != null && boss.Type.Mother && boss.Type.Phases == null, "Mother uses dedicated state without legacy royal/enrage phases");
        sim.Damage(boss, 100000, DamageSource.App); var m = boss.Mother!;
        check(m.Mode == MotherMode.HangingEggs && m.Tokens.Count == 5 && Math.Abs(boss.Hp - boss.MaxHp * .66) < .001, "Overkill clamps at 66% and suspends five eggs at high/low positions");
        double hp = boss.Hp;
        foreach (var source in Enum.GetValues<DamageSource>()) sim.Damage(boss, 10000, source);
        check(boss.Hp == hp && !sim.ApplyStatus(boss, SlimeStatus.Poison, 4, 10), "Every damage source and poison are blocked by hanging-egg mech");
        double time = sim.Time, age = m.Age; sim.Paused = true; RunFor(sim, 1);
        check(sim.Time == time && m.Age == age, "Pause freezes eggs, deadlines and boss animation age"); sim.Paused = false;
        foreach (var t in m.Tokens.ToArray())
        {
            for (int i = 0; i < 600 && !t.Done; i++) sim.Step(Dt, t.X, true, true, new CatPose(t.X, t.H, 0, 0, true, false));
        }
        check(m.Mode == MotherMode.Combat && m.Phase == 2 && boss.Hp == hp, "Carry to strike then break all five falling eggs advances phase without healing");
        sim.Damage(boss, 100000, DamageSource.Bomb);
        check(m.Mode == MotherMode.Cradle && m.Tokens.Count == 4, "33% starts four-child rescue rather than ordinary damage");
        foreach (var child in m.Tokens.ToArray())
        {
            RunFor(sim, .1, child.X, 56); RunFor(sim, .1, m.RingX, 300);
        }
        check(m.Phase == 3 && m.Mode == MotherMode.Combat && m.Soothe == 0, "Four carried children delivered to hanging cradle start soothe phase");
        hp = boss.Hp;
        foreach (var source in new[] { DamageSource.App, DamageSource.Poison, DamageSource.Link, DamageSource.Reflect, DamageSource.Echo, DamageSource.Splash, DamageSource.Throw }) sim.Damage(boss, 100000, source);
        check(boss.Hp == hp && m.Soothe == 0, "Passive apps/DOT/reflect/echo/link/splash/throw cannot damage or soothe mother");
        sim.Damage(boss, boss.MaxHp * .33 * .34, DamageSource.Weapon);
        check(m.Mode == MotherMode.Lullaby && m.Tokens.Count(t => !t.Bad) == 5, "33% soothe starts exactly five good heart notes among red decoys");
        foreach (var note in m.Tokens.Where(t => !t.Bad).ToArray()) RunFor(sim, .05, note.X, note.H);
        check(m.Mode == MotherMode.Combat && m.Slower && m.Soothe >= 43, "Five good notes reward soothe and slow remaining attacks");
        sim.Damage(boss, boss.MaxHp * .33 * .24, DamageSource.Tool);
        check(m.Mode == MotherMode.Stroke, "66% soothe opens back-stroke puzzle");
        for (int stroke = 0; stroke < 3; stroke++)
        {
            double ring = m.RingX, direction = m.StrokeDirection;
            for (int k = 0; k < 20; k++)
            {
                sim.SetMotherPointer(boss.X + direction * (-boss.Width * .3 + k * boss.Width * .035), boss.Height * .8);
                sim.Step(Dt, ring, false);
            }
        }
        check(m.Mode == MotherMode.Combat && m.Progress == 0 && m.Soothe >= 77, "Three directed strokes with cat on relocated ring finish back rub");
        sim.Damage(boss, 100000, DamageSource.Bomb);
        check(m.Mode == MotherMode.LastEgg && m.Soothe == 100, "100% always enters mandatory last-egg puzzle");
        hp = boss.Hp; sim.Damage(boss, 100000, DamageSource.Weapon);
        check(boss.Hp == hp && m.Progress == 0, "Hitting closed body cannot skip final puzzle");
        for (int i = 0; i < 1750 && m.Mode == MotherMode.LastEgg; i++)
        {
            double x = m.BellyOpen ? boss.X : 100, h = m.BellyOpen ? boss.Height * .4 : 350;
            sim.Step(Dt, x, true, true, new CatPose(x, h, 0, 0, true, false));
        }
        check(m.Mode == MotherMode.Sleeping, "Lift over heartbeats and tap open belly five times puts mother to sleep");
        RunFor(sim, 3);
        check(sim.BossDefeated && boss.State == SlimeState.Defeated && sim.DefeatedBoss?.Id == "mother", "Sleeping dissolve counts as ordinary boss defeat with reward identity");

        (sim, boss) = Field(); Phase3(sim, boss); m = boss.Mother!;
        check(m.Phase == 3 && m.Mode == MotherMode.Combat, "Both first mechs advance after timeout; healing cannot re-trigger old phase");
        sim.Damage(boss, boss.MaxHp * .33 * .34, DamageSource.Tool); RunFor(sim, 18.2);
        check(m.Mode == MotherMode.Combat && Math.Abs(m.Soothe - 19) < .01 && m.Thresholds == 1, "Missed lullaby removes 15 points and marks threshold consumed");
        sim.Damage(boss, boss.MaxHp * .33 * .2, DamageSource.Tool);
        check(m.Mode == MotherMode.Combat && m.Soothe > 33, "Failed 33% threshold never repeats");
        sim.Damage(boss, 100000, DamageSource.Tool); RunFor(sim, 15.2);
        sim.Damage(boss, 100000, DamageSource.Tool); RunFor(sim, 30.2);
        check(m.Mode == MotherMode.Combat && m.Soothe == 85 && boss.Targetable, "Failed mandatory last egg drops to 85% with no boss kill");
        sim.Damage(boss, 100000, DamageSource.Tool);
        check(m.Mode == MotherMode.LastEgg, "100% re-triggers mandatory last egg after failure");

        (sim, boss) = Field(); m = boss.Mother!; m.Clock = 0;
        RunFor(sim, .05, 400, 300);
        var pending = m.Zones.First(); double before = sim.CatHp;
        RunFor(sim, pending.Warn + .05, pending.X, pending.H + pending.Height / 2);
        check(sim.CatHp < before && !sim.CatStunned && !sim.MutantForcedFall && !sim.ZipForcedFall, "Mother air hit really touching held cat damages without stun/drop");
        boss.Set(SlimeState.Stunned); boss.StunRemaining = 1; RunFor(sim, .02);
        check(m.Zones.Count == 0 && m.Move == -1, "Stun cancels every pending mother hazard without delayed hit");
        (sim, boss) = Field(); sim.Damage(boss, 100000, DamageSource.Bomb); RunFor(sim, 40.1); m = boss.Mother!;
        m.NextWrap = sim.Time; RunFor(sim, .02, 200, 56, false);
        check(sim.MotherWrapLeft > 0 && sim.ClickMotherWrap() && sim.ClickMotherWrap() && sim.ClickMotherWrap() && sim.MotherWrapLeft == 0, "Floor wrap is removed by three clicks");
        m.NextWrap = sim.Time; RunFor(sim, .02, 200, 500);
        check(sim.MotherWrapLeft == 0, "Wrap cannot be applied to held cat");
        sim.Damage(boss, 100000, DamageSource.Weapon);
        double oldRing = m.RingX; sim.SetArea(100, 900); sim.SetSky(500);
        check(Math.Abs(m.RingX - (100 + oldRing / 2)) < .01 && m.Tokens.All(t => t.X >= 100 && t.X <= 900), "Resize maps cradle and children together within new battlefield");
        (sim, boss) = Field(); m = boss.Mother!;
        for (int phase = 1; phase <= 3; phase++)
        {
            m.Phase = phase; m.Bag.Clear(); m.LastMove = -1;
            var seen = new HashSet<int>();
            for (int turn = 0; turn < 6; turn++)
            {
                int last = m.Move; m.Clock = 0; m.Zones.Clear();
                RunFor(sim, Dt);
                check(m.Move >= (phase - 1) * 3 && m.Move < phase * 3 && m.Zones.Count > 0 && (turn == 0 || m.Move != last), $"Mother phase {phase} turn {turn} uses only its three moves with no immediate repeat");
                seen.Add(m.Move);
            }
            check(seen.Count == 3, $"Mother phase {phase} seeded bag covers all three moves");
        }
        (sim, boss) = Field(); m = boss.Mother!; m.Clock = 10;
        sim.Step(Dt, 800, true, true, new CatPose(800, 470, 0, 0, true, false));
        m.Zones.Add(new MotherZone { Art = MotherArt.Egg, X = 100, H = 300, Width = 20, Height = 20,
            Moving = true, FromX = 100, ToX = 1500, FromH = 300, ToH = 300, Life = .1, Warn = 0 });
        double beforeSweep = sim.CatHp;
        sim.Step(.1, 800, true, true, new CatPose(800, 470, 0, 0, true, false));
        check(sim.CatHp < beforeSweep, "A fast arcing mother projectile sweeps through held cat even when one step crosses its expiry");
    }
}
