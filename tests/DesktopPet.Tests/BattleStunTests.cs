using DesktopPet;

// Combat Depth milestone 1: the quick slime's dash and the cat's stun. A dash hits once and stuns
// for StunDurationSeconds of battle time; pause freezes it, lifting neither ends nor blocks it,
// a short immunity follows, and the Shield, landing grace and the user's hands keep it off.
internal static class BattleStunTests
{
    const double dt = 1.0 / 60;

    // One dashing slime (harmless to the cat's HP unless asked), a cat that cannot kill it.
    private static BattleSim Dasher(double damage = 6, double speed = 420, double stun = 1.5, double immunity = 3, int count = 1)
    {
        var dasher = new EnemyType
        {
            Id = "runner", MaxHp = 1000, Speed = 70, Damage = damage, Range = 54, Windup = 0.7, AttackCooldown = 2.6, Recover = 0.4, Scale = 0.75,
            Dash = true, DashRange = 170, DashDistance = 230, DashSpeed = speed
        };
        var settings = new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0, MaxAlive = count, CatDamage = 0, StunDurationSeconds = stun, StunImmunitySeconds = immunity,
            Enemies = new[] { dasher }, Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "runner", Count = count } } } }
        };
        return new BattleSim(settings, 0, 1600, new Random(3));
    }

    // Steps until the condition holds (or the time runs out); returns whether it did.
    private static bool Until(BattleSim sim, Func<bool> done, double seconds = 10, double catX = 800, bool held = false, bool lifted = false)
    {
        for (double t = 0; t < seconds; t += dt) { if (done()) return true; sim.Step(dt, catX, held, lifted); }
        return done();
    }

    public static void Run(Action<bool, string> check)
    {
        // The dash: a windup from further away than a plain attack, then one hit that stuns.
        var sim = Dasher();
        Until(sim, () => sim.Slimes.Count > 0 && sim.Slimes[0].State == SlimeState.Windup);
        var slime = sim.Slimes[0];
        double gap = Math.Abs(slime.X - 800);
        check(slime.State == SlimeState.Windup && gap > slime.Range && gap <= 170 && slime.DashDirection == Math.Sign(800 - slime.X),
            $"A dash slime winds up from beyond its plain reach, its lane set toward the cat ({gap:0} DIPs away)");
        Until(sim, () => sim.CatStunned, 3);
        check(sim.CatStunned && sim.CatHp == 100 - 6 && sim.StunsTaken == 1 && slime.DashHit, "Running into the cat hits it once (6 HP) and stuns it");
        double stunStart = sim.Time;
        bool still = true;
        for (int i = 0; i < 200 && sim.CatStunned; i++)
        {
            var order = sim.Step(dt, 800, false);
            // Only the frames that end still stunned (the last one hands the cat back to the battle).
            if (sim.CatStunned) still &= sim.SwingTime < 0 && order.Command == CatCommand.Stand;
        }
        double stunLength = sim.Time - stunStart;
        check(Math.Abs(stunLength - 1.5) <= dt * 1.5, $"The stun lasts 1.5 s of battle time ({stunLength:0.00} s)");
        check(still, "A stunned cat stands still and does not swing");
        check(sim.CatHp == 94 && sim.StunsTaken == 1, "Overlapping the dasher for many frames gives no second hit or stun");
        check(sim.StunImmunity > 2.9, "After the stun the cat is immune to a new one for a while");

        // Pause freezes the stun: no time passes for it.
        var paused = Dasher();
        Until(paused, () => paused.CatStunned);
        double left = paused.CatStunRemaining;
        paused.Paused = true;
        for (int i = 0; i < 120; i++) paused.Step(dt, 800, false);
        check(paused.CatStunRemaining == left, "Pause freezes the stun timer");
        paused.Paused = false;

        // Lifting does not end the stun and the timer keeps running in the user's hands; the battle
        // leaves a held cat to input and physics (Hold), even when the stun ends in the air.
        var lifted = Dasher();
        Until(lifted, () => lifted.CatStunned);
        var held = lifted.Step(dt, 800, true, true);
        check(held.Command == CatCommand.Hold && lifted.CatStunned, "A stunned cat can still be lifted (the battle lets go of it)");
        for (int i = 0; i < 120; i++) held = lifted.Step(dt, 800, true, true);
        check(!lifted.CatStunned && held.Command == CatCommand.Hold, "The stun runs out while carried and the cat is not forced down");

        // The user's hands, the Shield and landing grace keep the dash off.
        var carried = Dasher();
        Until(carried, () => carried.Slimes.Count > 0 && carried.Slimes[0].State == SlimeState.Windup);
        Until(carried, () => carried.Slimes[0].State == SlimeState.Dashing, 2);
        Until(carried, () => carried.Slimes[0].State != SlimeState.Dashing, 2, held: true, lifted: true);
        check(carried.CatHp == 100 && !carried.CatStunned, "A dash under a lifted cat misses it");
        var shielded = Dasher();
        Until(shielded, () => shielded.Slimes.Count > 0 && shielded.Slimes[0].State == SlimeState.Windup);
        shielded.Use(BattleSkill.Shield, 800);
        Until(shielded, () => shielded.Slimes[0].State == SlimeState.Recover, 3);
        check(shielded.CatHp == 100 && !shielded.CatStunned && shielded.Slimes[0].DashHit, "The Shield takes the dash: no damage, no stun");

        // Immunity: a second dash during it still hurts but does not stun again.
        var twice = Dasher(immunity: 30, count: 2);
        Until(twice, () => twice.StunsTaken == 1);
        Until(twice, () => !twice.CatStunned, 3);
        double hp = twice.CatHp;
        Until(twice, () => twice.CatHp < hp, 10);
        check(twice.CatHp < hp && twice.StunsTaken == 1 && !twice.CatStunned, "A dash during the immunity hurts but does not stun");

        // A very fast dash with long frames still meets the cat (the whole path is checked).
        var fast = Dasher(speed: 1500);
        for (double t = 0; t < 10 && !fast.CatStunned; t += 0.1) fast.Step(0.1, 800, false);
        check(fast.CatStunned && fast.CatHp == 94, "A 1500 DIPs/s dash at 10 fps does not pass through the cat");

        // The lane is fixed: a cat behind the dasher's back is not hit.
        var dodge = Dasher();
        Until(dodge, () => dodge.Slimes.Count > 0 && dodge.Slimes[0].State == SlimeState.Windup);
        double behind = dodge.Slimes[0].X - dodge.Slimes[0].DashDirection * 120;
        Until(dodge, () => dodge.Slimes[0].State == SlimeState.Recover, 3, catX: behind);
        check(dodge.CatHp == 100 && !dodge.CatStunned, "Stepping out of the lane dodges the dash");

        // A push during the windup cancels the dash.
        var pushed = Dasher();
        Until(pushed, () => pushed.Slimes.Count > 0 && pushed.Slimes[0].State == SlimeState.Windup);
        check(pushed.UsePush(pushed.Slimes[0].Id, 800) && pushed.Slimes[0].State == SlimeState.Stunned, "Push stops a dash slime winding up");
        Until(pushed, () => pushed.Slimes[0].State != SlimeState.Stunned, 3);
        check(!pushed.CatStunned && pushed.CatHp == 100, "The pushed dash never lands");

        // The end of a battle leaves no stun behind.
        var lost = Dasher(damage: 100);
        Until(lost, () => lost.Over);
        check(lost.Phase == BattlePhase.Lost && !lost.CatStunned && lost.StunImmunity == 0, "A lost battle ends without a stun left over (the last hit does not stun)");
        var ended = Dasher();
        Until(ended, () => ended.CatStunned);
        // Won while stunned: a bomb finishes the last slime.
        ended.Slimes[0].Hp = 1;
        ended.Slimes[0].KnockVelocity = 0;
        ended.Slimes[0].StunRemaining = 5;
        ended.Slimes[0].Set(SlimeState.Stunned); // Held in place so the bomb lands on it.
        check(ended.Use(BattleSkill.Bomb, ended.Slimes[0].Id, 800), "(setup) a bomb on the last slime");
        Until(ended, () => ended.Over, 2);
        check(ended.Phase == BattlePhase.Won && !ended.CatStunned && ended.StunImmunity == 0, "A battle won during a stun ends with no stun left over");

        // Data: the shipped quick slime dashes; numbers out of range are rejected.
        var shipped = BattleData.Settings;
        var runner = shipped.Enemy("runner");
        check(runner.Dash && shipped.StunDurationSeconds == 1.5 && shipped.StunImmunitySeconds > 0 && runner.DashDistance > runner.DashRange,
            "battle.json: the quick slime dashes; stun 1.5 s with immunity afterwards");
        bool Rejected(string json)
        {
            try { BattleData.Parse(json); return false; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return true; }
        }
        check(Rejected("{\"stunDurationSeconds\":9}") && Rejected("{\"stunImmunitySeconds\":-1}") && !Rejected("{\"stunDurationSeconds\":0}"),
            "Stun numbers out of range are rejected (0 turns stuns off)");
        check(Rejected("{\"enemies\":[{\"id\":\"slime\",\"dash\":true,\"dashSpeed\":5}],\"waves\":[{\"groups\":[{\"enemy\":\"slime\",\"count\":1}]}]}") &&
              !Rejected("{\"enemies\":[{\"id\":\"slime\",\"dash\":true}],\"waves\":[{\"groups\":[{\"enemy\":\"slime\",\"count\":1}]}]}"),
            "Dash numbers out of range are rejected; the defaults are valid");
        var off = Dasher(stun: 0);
        Until(off, () => off.CatHp < 100);
        check(off.CatHp == 94 && !off.CatStunned, "stunDurationSeconds 0: the dash only hurts");
    }
}
