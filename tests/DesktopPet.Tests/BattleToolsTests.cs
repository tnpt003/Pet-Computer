using DesktopPet;

// 1.5 M05 (PetBattle.Tools.cs), with stand-in enemies: the Laser's dot lures the slimes near it (not a
// far one, not a boss), a lured one never winds up, one on the dot loses LaserDps HP a second (a boss
// half), the dot ends on time and the skill cools down; the Fan blows slimes away from the cursor (not a
// boss or a stuck one, an elite that ignores Push barely), a blown one bumps into another or the edge
// for FanBumpDamage, its air runs out and refills; both wait while the cat is held and freeze with
// Pause; their data limits; the shipped numbers.
internal static class BattleToolsTests
{
    const double dt = 1.0 / 60;
    private static readonly BattleSkill[] Tools = { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Laser, BattleSkill.Fan };

    private static EnemyType Kind(string id, double speed = 40, double hp = 1000, bool boss = false) => new()
    {
        Id = id, MaxHp = hp, Speed = speed, Damage = 5, Range = 64, Windup = 0.4, AttackCooldown = 1, Recover = 0.2, Boss = boss
    };

    private static BattleSim Sim(params EnemyType[] kinds)
    {
        var settings = new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1000, MaxAlive = 8, Enemies = kinds,
            Weapons = new[] { new WeaponSpec { Id = "sword", Damage = 10, Knock = 0, StunChance = 0 } },
            Plan = new[] { new WaveSpec { Groups = kinds.Select(k => new WaveGroup { Enemy = k.Id, Count = 1 }).ToArray() } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 7, skills: Tools);
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < kinds.Length; i++) sim.Step(dt, 100, false);
        sim.ClearEvents();
        return sim;
    }

    private static void Run(BattleSim sim, double seconds, double catX = 100, List<BattleEvent>? seen = null, bool held = false)
    {
        for (double t = 0; t < seconds - 1e-9 && !sim.Over; t += dt)
        {
            sim.Step(dt, catX, held, held);
            seen?.AddRange(sim.Events);
            sim.ClearEvents();
        }
    }

    public static void Run(Action<bool, string> check)
    {
        LaserChecks(check);
        FanChecks(check);
        ColumnChecks(check);
        WaitChecks(check);
        DataChecks(check);
    }

    // 2026-11-04 (D4): a click anywhere puts the Fan's column there: floor slimes in it stand still and hit
    // the cat softer; a flyer in the air in it is blown down; then the cooldown.
    private static void ColumnChecks(Action<bool, string> check)
    {
        var flyer = new EnemyType { Id = "flyer", MaxHp = 1000, Speed = 40, Damage = 0, Range = 55, Windup = 0.8, AttackCooldown = 2, Recover = 0.4, Fly = BattleData.Settings.Enemy("flyer").Fly };
        var sim = Sim(Kind("walker", speed: 80), Kind("far", speed: 0), flyer);
        Slime walker = sim.Slimes[0], far = sim.Slimes[1], fly = sim.Slimes[2];
        walker.X = 700; far.X = 1400;
        check(!sim.UseOnScreen(BattleSkill.Fan, 700, 400), "The Fan works only once chosen");
        for (int i = 0; i < 600 && !(fly.Fly == FlyPhase.Aloft && fly.State != SlimeState.Spawning); i++) { sim.Step(dt, 100, false); sim.ClearEvents(); }
        fly.X = 645;
        walker.X = 755; // Apart: the flyer blown down must not shove it.
        check(sim.Arm(BattleSkill.Fan) && sim.UseOnScreen(BattleSkill.Fan, 700, 400) && sim.ColumnX == 700 && sim.Armed == null && sim.Cooldown(BattleSkill.Fan) > 0,
            "Chosen, a click anywhere puts the column there; the cooldown starts");
        var seen = new List<BattleEvent>();
        double before = walker.X;
        for (int i = 0; i < 60; i++) { sim.Step(dt, 100, false); seen.AddRange(sim.Events); sim.ClearEvents(); }
        check(Math.Abs(walker.X - before) < 2 && walker.Rooted && !far.Rooted, $"A slime on the floor in the column stands still ({before:0} → {walker.X:0}); one far away does not");
        check(fly.Fly != FlyPhase.Aloft && sim.ColumnKnocks >= 1, "A flyer in the air in the column is blown down");
        for (int i = 0; i < 300 && sim.ColumnX != null; i++) { sim.Step(dt, 100, false); sim.ClearEvents(); }
        check(sim.ColumnX == null && !walker.Rooted, $"After {sim.Settings.FanColumnSeconds} s the column is gone");
    }

    private static void LaserChecks(Action<bool, string> check)
    {
        var sim = Sim(Kind("near"), Kind("far"), Kind("king", speed: 0, boss: true));
        Slime near = sim.Slimes[0], far = sim.Slimes[1], king = sim.Slimes[2];
        near.X = 900; far.X = 1500; king.X = 1100;
        check(!sim.Use(BattleSkill.Laser, near.Id, 100) && !sim.UseAt(BattleSkill.Push, 1000), "The Laser is used on a spot, Push only on a slime");
        check(sim.Arm(BattleSkill.Laser) && sim.Armed == BattleSkill.Laser, "The Laser is chosen first");
        var seen = new List<BattleEvent>();
        check(sim.UseAt(BattleSkill.Laser, 1000) && sim.LaserX == 1000 && sim.Armed == null && sim.Cooldown(BattleSkill.Laser) == sim.Settings.LaserCooldown,
            "A click on a spot puts the dot there, ends the choice and starts the cooldown");
        double farBefore = far.X;
        Run(sim, 1, seen: seen);
        check(near.Lured && !far.Lured && !king.Lured, "Slimes within its reach chase the dot; a far one and a boss do not");
        check(near.X > 930 && near.X <= 1000 && far.X < farBefore, $"The lured one walks to the dot, a far one to the cat ({near.X:0}, {far.X:0})");
        near.X = 1000; king.X = 1000;
        double nearHp = near.Hp, kingHp = king.Hp;
        Run(sim, 2, seen: seen);
        check(Math.Abs(nearHp - near.Hp - 2 * sim.Settings.LaserDps) < 0.6, $"One on the dot loses {sim.Settings.LaserDps} HP a second ({nearHp - near.Hp:0.##} in 2 s)");
        check(Math.Abs(kingHp - king.Hp - sim.Settings.LaserDps) < 0.6, $"A boss on it takes half ({kingHp - king.Hp:0.##} in 2 s)");
        Run(sim, 1.2, seen: seen);
        check(sim.LaserX == null && !near.Lured && seen.Any(e => e.Kind == BattleEventKind.LaserOn) && seen.Any(e => e.Kind == BattleEventKind.LaserOff),
            "The dot goes after its time");

        // A lured slime right next to the cat starts no attack.
        sim = Sim(Kind("biter", speed: 0));
        var biter = sim.Slimes[0];
        biter.X = 140;
        sim.Arm(BattleSkill.Laser);
        sim.UseAt(BattleSkill.Laser, 400);
        bool woundUp = false;
        for (int i = 0; i < 180; i++) { sim.Step(dt, 100, false); sim.ClearEvents(); woundUp |= biter.State == SlimeState.Windup; }
        check(!woundUp && biter.Lured, "A lured slime in reach of the cat does not wind up");
        Run(sim, 2.5);
        biter.X = 140; // (it went some way to the dot)
        for (int i = 0; i < 120; i++) { sim.Step(dt, 100, false); sim.ClearEvents(); woundUp |= biter.State == SlimeState.Windup; }
        check(woundUp, "Once the dot is gone it attacks again");
    }

    private static void FanChecks(Action<bool, string> check)
    {
        var sim = Sim(Kind("a", speed: 80), Kind("king", speed: 22, boss: true));
        Slime a = sim.Slimes[0], king = sim.Slimes[1];
        a.X = 500; king.X = 540;
        sim.Arm(BattleSkill.Fan);
        check(sim.UseOnScreen(BattleSkill.Fan, 520, 300), "(setup) the column over a slime and a boss");
        Run(sim, 0.5);
        check(a.Rooted && !king.Rooted, "It holds a slime, never a boss");
        Run(sim, sim.Settings.FanColumnSeconds);
        check(sim.ColumnX == null && !sim.Ready(BattleSkill.Fan), "The column ends before its cooldown does");
        Run(sim, sim.Settings.FanRefillSeconds);
        check(sim.Ready(BattleSkill.Fan), "After its cooldown it is ready again");
        // Its blows land lighter in the column.
        var soft = Sim(Kind("hitter", speed: 0));
        var hitter = soft.Slimes[0];
        hitter.X = 130;
        soft.Arm(BattleSkill.Fan); soft.UseOnScreen(BattleSkill.Fan, 130, 300);
        double hp = soft.CatHp;
        for (int i = 0; i < 180 && soft.CatHp == hp; i++) { soft.Step(dt, 100, false); soft.ClearEvents(); }
        check(Math.Abs(hp - soft.CatHp - 5 * (1 - soft.Settings.FanColumnCut)) < 1e-6, $"A blow from inside the column lands {soft.Settings.FanColumnCut:P0} lighter ({hp - soft.CatHp:0.##} of 5)");
    }

    private static void WaitChecks(Action<bool, string> check)
    {
        var sim = Sim(Kind("a", speed: 0));
        sim.Slimes[0].X = 600;
        sim.Arm(BattleSkill.Fan);
        sim.UseAt(BattleSkill.Laser, 800); // The Fan is chosen: no Laser now.
        check(sim.LaserX == null, "One skill chosen at a time");
        Run(sim, 0.2, held: true);
        check(sim.Armed == null, "Lifting the cat puts the Fan away");
        Run(sim, 0.1);
        check(sim.Arm(BattleSkill.Laser) && sim.UseAt(BattleSkill.Laser, 800), "Put down, the Laser is used");
        double left = sim.LaserRemaining;
        sim.Paused = true;
        Run(sim, 2);
        check(sim.LaserRemaining == left && sim.LaserX == 800, "Pause freezes the dot");
        sim.Paused = false;
        Run(sim, 1, held: true);
        check(sim.LaserRemaining < left - 0.9, "A dot already down runs on while the cat is held (like Cheer)");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        bool Rejects(string json)
        {
            try { BattleData.Parse(json); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(Rejects("{\"laserSeconds\":0}") && Rejects("{\"fanColumnSeconds\":0}") && Rejects("{\"fanColumnWidth\":5000}") && Rejects("{\"laserDps\":-1}"),
            "Out-of-range Laser and Fan numbers are refused");
        check(!Rejects("{\"laserSeconds\":6,\"fanColumnCut\":0}"), "In-range ones are accepted");
        var d = new BattleSettings();
        var s = BattleData.Settings;
        check(s.LaserCooldown == d.LaserCooldown && s.LaserSeconds == d.LaserSeconds && s.LaserLureRange == d.LaserLureRange && s.LaserDps == d.LaserDps
              && s.FanRefillSeconds == d.FanRefillSeconds && s.FanColumnWidth == d.FanColumnWidth && s.FanColumnSeconds == d.FanColumnSeconds
              && s.FanColumnCut == d.FanColumnCut, "The shipped battle.json has the built-in Laser and Fan numbers");
        check(BattleSkills.Get(BattleSkill.Laser).Aim == SkillAim.Point && BattleSkills.Get(BattleSkill.Fan).Aim == SkillAim.Screen
              && BattleSkills.Get(BattleSkill.Laser).Working && BattleSkills.Get(BattleSkill.Fan).Working, "The Laser is used on a spot, the Fan anywhere on the screen (2026-11-04)");
        check(s.Skill(BattleSkill.Laser).Desc("en").Contains("{laserDps}") && BattleSkills.Get(BattleSkill.Fan).Describe(s, "en").Contains("150-DIP column for 4 s"),
            "The descriptions show the battle's numbers");
    }
}
