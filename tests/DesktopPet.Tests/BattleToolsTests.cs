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
        WaitChecks(check);
        DataChecks(check);
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
        var sim = Sim(Kind("a", speed: 0), Kind("king", speed: 0, boss: true), Kind("glued", speed: 0), Kind("rock", speed: 0));
        Slime a = sim.Slimes[0], king = sim.Slimes[1], glued = sim.Slimes[2], rock = sim.Slimes[3];
        a.X = 620; king.X = 400; glued.X = 560; rock.X = 1300;
        sim.ApplyStatus(glued, SlimeStatus.Stick, 5);
        check(!sim.StartFan(500), "The Fan blows only once chosen");
        check(sim.Arm(BattleSkill.Fan) && sim.StartFan(500) && sim.FanBlowing, "Chosen, a press on the field starts it");
        double kingAt = king.X, gluedAt = glued.X;
        Run(sim, 0.5);
        check(a.X > 640 && king.X == kingAt && glued.X == gluedAt, $"It blows a slime away from the cursor, never a boss or a stuck one ({a.X:0})");
        check(sim.FanSteam < 1 && sim.FanSteam > 0.7 && sim.Armed == BattleSkill.Fan, $"Blowing uses its air ({sim.FanSteam:0.00})");
        sim.StopFan();
        double steam = sim.FanSteam;
        Run(sim, 0.5);
        check(!sim.FanBlowing && sim.FanSteam > steam && sim.Armed == BattleSkill.Fan, "Let go, it stops, its air comes back and it stays chosen");
        sim.StartFan(500);
        var seen = new List<BattleEvent>();
        for (int i = 0; i < 300 && sim.FanBlowing; i++) { sim.Step(dt, 100, false); seen.AddRange(sim.Events); sim.ClearEvents(); }
        check(!sim.FanBlowing && sim.Armed == null && sim.FanSteam == 0 && !sim.Ready(BattleSkill.Fan) && seen.Any(e => e.Kind == BattleEventKind.FanSpent),
            "Out of air it stops and is put away");
        check(Math.Abs(sim.Cooldown(BattleSkill.Fan) - sim.Settings.FanRefillSeconds) < 0.01 && !sim.Arm(BattleSkill.Fan), "Spent, it waits its refill");
        Run(sim, sim.Settings.FanRefillSeconds + 0.1);
        check(sim.Ready(BattleSkill.Fan) && sim.FanSteam > 0.999, $"Refilled, it is ready again ({sim.FanSteam}, {sim.Cooldown(BattleSkill.Fan)})");

        // Bumps: into a slime that stands, and into the edge; each slime at most once a second.
        sim = Sim(Kind("a", speed: 0), Kind("b", speed: 0), Kind("c", speed: 0));
        Slime first = sim.Slimes[0], wall = sim.Slimes[1], edge = sim.Slimes[2];
        first.X = 540; wall.X = 640; edge.X = 1560;
        sim.ApplyStatus(wall, SlimeStatus.Stick, 5);
        double hp = first.Hp, wallHp = wall.Hp;
        sim.Arm(BattleSkill.Fan); sim.StartFan(500);
        Run(sim, 0.9);
        check(first.Hp == hp - sim.Settings.FanBumpDamage && wall.Hp == wallHp - sim.Settings.FanBumpDamage && sim.FanBumps >= 1,
            $"Blown into another slime, both lose {sim.Settings.FanBumpDamage} once ({hp - first.Hp}, {wallHp - wall.Hp})");
        sim.StopFan();
        sim.MoveFan(0);
        double edgeHp = edge.Hp;
        sim.StartFan(1450);
        Run(sim, 0.5);
        check(edge.Hp == edgeHp - sim.Settings.FanBumpDamage, $"Blown into the edge it loses {sim.Settings.FanBumpDamage} ({edgeHp - edge.Hp})");
        sim.StopFan();

        // An elite that ignores Push: the wind barely moves it.
        sim = Sim(Kind("plain", speed: 0), Kind("stubborn", speed: 0));
        Slime plain = sim.Slimes[0], stubborn = sim.Slimes[1];
        plain.X = 560; stubborn.X = 1060;
        stubborn.Elite = new EliteSpec { PushImmune = true };
        sim.Arm(BattleSkill.Fan); sim.StartFan(500);
        Run(sim, 0.2);
        sim.StopFan(); sim.StartFan(1000);
        Run(sim, 0.2);
        check(plain.X - 560 > 3 * (stubborn.X - 1060) && stubborn.X > 1060, $"An elite that ignores Push moves far less ({plain.X - 560:0} vs {stubborn.X - 1060:0})");
    }

    private static void WaitChecks(Action<bool, string> check)
    {
        var sim = Sim(Kind("a", speed: 0));
        sim.Slimes[0].X = 600;
        sim.Arm(BattleSkill.Fan); sim.StartFan(500);
        sim.UseAt(BattleSkill.Laser, 800); // The Fan is chosen: no Laser now.
        check(sim.LaserX == null, "One skill chosen at a time");
        Run(sim, 0.2, held: true);
        double steam = sim.FanSteam;
        check(!sim.FanBlowing && sim.Armed == null, "Lifting the cat stops the Fan and puts it away");
        Run(sim, 1, held: true);
        check(sim.FanSteam == steam, "While the cat is held its air does not come back");
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
        check(Rejects("{\"laserSeconds\":0}") && Rejects("{\"fanSteamSeconds\":0}") && Rejects("{\"fanRange\":5000}") && Rejects("{\"laserDps\":-1}"),
            "Out-of-range Laser and Fan numbers are refused");
        check(!Rejects("{\"laserSeconds\":6,\"fanBumpDamage\":0}"), "In-range ones are accepted");
        var d = new BattleSettings();
        var s = BattleData.Settings;
        check(s.LaserCooldown == d.LaserCooldown && s.LaserSeconds == d.LaserSeconds && s.LaserLureRange == d.LaserLureRange && s.LaserDps == d.LaserDps
              && s.FanSteamSeconds == d.FanSteamSeconds && s.FanRefillSeconds == d.FanRefillSeconds && s.FanRange == d.FanRange && s.FanSpeed == d.FanSpeed
              && s.FanBumpDamage == d.FanBumpDamage, "The shipped battle.json has the built-in Laser and Fan numbers");
        check(BattleSkills.Get(BattleSkill.Laser).Aim == SkillAim.Point && BattleSkills.Get(BattleSkill.Fan).Aim == SkillAim.Hold
              && BattleSkills.Get(BattleSkill.Laser).Working && BattleSkills.Get(BattleSkill.Fan).Working, "The Laser is used on a spot, the Fan by holding");
        check(s.Skill(BattleSkill.Laser).Desc("en").Contains("{laserDps}") && BattleSkills.Get(BattleSkill.Fan).Describe(s, "en").Contains("4 HP"),
            "The descriptions show the battle's numbers");
    }
}
