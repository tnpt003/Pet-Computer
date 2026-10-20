using DesktopPet;

// Battle Mode rules that lifting the cat must not get around: landing protection has its own
// cooldown, skills wait while the cat is safe in the user's hands, and pause freezes all of it.
internal static class BattleRulesTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        void Run(BattleSim sim, double seconds, bool held = false, bool lifted = false, double catX = 800)
        {
            for (double t = 0; t < seconds; t += dt) sim.Step(dt, catX, held, lifted);
        }
        void Lift(BattleSim sim, double seconds) => Run(sim, seconds, held: true, lifted: true);
        // A harmless slime is on the floor; the cat stands at 800.
        BattleSim Calm(double graceCooldown = 5, double minLift = 0.3)
        {
            var sim = new BattleSim(new BattleSettings
            {
                FirstWaveDelay = 0, Waves = new[] { 2 }, MaxAlive = 2, SpawnInterval = 0, SlimeMaxHp = 1000, SlimeDamage = 0, CatDamage = 0,
                RescueGraceCooldownSeconds = graceCooldown, RescueGraceMinLiftSeconds = minLift
            }, 0, 1600, new Random(5));
            sim.Step(dt, 800, false); sim.Step(dt, 800, false);
            Run(sim, 0.6, held: true); // Both slimes finish appearing (a press: no rescue).
            sim.SetCatState(false, false);
            return sim;
        }

        // Lifting and dropping over and over (0.2 s lifts): before the cooldown the cat was safe all
        // the time (measured without the minimum lift, which alone now gives such lifts nothing).
        (double Protected, double Ground) Spam(double graceCooldown, double minLift = 0)
        {
            var sim = Calm(graceCooldown, minLift);
            double safe = 0, ground = 0;
            for (int cycle = 0; cycle < 20; cycle++)
            {
                Lift(sim, 0.2);
                for (double t = 0; t < 0.3; t += dt) { sim.Step(dt, 800, false); ground += dt; if (sim.Grace > 0) safe += dt; }
            }
            return (safe, ground);
        }
        var old = Spam(0);
        var now = Spam(5);
        var spec = new BattleSettings();
        check(old.Protected > old.Ground * 0.95, $"(without the cooldown) lift-and-drop keeps the cat safe nearly all the time ({old.Protected:F1} of {old.Ground:F1} s)");
        check(now.Protected <= spec.LandingGraceSeconds * (1 + now.Ground / spec.RescueGraceCooldownSeconds) + 0.05,
            $"With the {spec.RescueGraceCooldownSeconds} s cooldown it cannot ({now.Protected:F1} of {now.Ground:F1} s on the ground protected)");
        var quick = Spam(5, spec.RescueGraceMinLiftSeconds);
        check(spec.RescueGraceMinLiftSeconds == 0.3 && quick.Protected == 0,
            $"Lifts shorter than {spec.RescueGraceMinLiftSeconds} s earn no landing protection at all ({quick.Protected:F1} s protected)");

        var sim = Calm();
        Lift(sim, 0.3);
        sim.Step(dt, 800, false);
        check(sim.Grace > spec.LandingGraceSeconds - 0.05 && sim.GraceCooldown > spec.RescueGraceCooldownSeconds - 0.05 && !sim.RescueGraceReady,
            "The first rescue protects the cat and starts the protection cooldown");
        Run(sim, 0.5);
        double left = sim.Grace;
        Lift(sim, 0.3);
        sim.Step(dt, 800, false);
        check(sim.Grace <= left && sim.Grace < spec.LandingGraceSeconds - 0.4, $"Dropping it again too soon neither renews nor extends it ({left:F2} → {sim.Grace:F2})");
        double cooling = sim.GraceCooldown;
        Lift(sim, 3);
        bool carriedSame = sim.GraceCooldown == cooling;
        sim.Paused = true; Run(sim, 3); sim.Paused = false;
        check(carriedSame && sim.GraceCooldown == cooling, "The protection cooldown does not run while the cat is carried or the battle is paused");
        Run(sim, cooling + 0.05);
        check(sim.RescueGraceReady && sim.Grace == 0, "On the ground in a running battle it runs out");
        sim.Step(dt, 800, true, false); sim.Step(dt, 800, false);
        check(sim.Grace == 0 && sim.RescueGraceReady, "A plain click is no rescue: no protection, nothing used up");
        Lift(sim, 0.2);
        sim.Step(dt, 800, false);
        bool tooShort = sim.Grace == 0 && sim.RescueGraceReady;
        Lift(sim, 0.35);
        sim.Step(dt, 800, false);
        check(tooShort && sim.Grace > spec.LandingGraceSeconds - 0.05, "After the cooldown a 0.2 s lift earns nothing (nor uses it up); the next real rescue protects again");

        // Skills wait while the cat is in the user's hands.
        var skills = Calm();
        var target = skills.Slimes[0];
        target.X = 1100;
        skills.Slimes[1].X = 1500;
        check(skills.Use(BattleSkill.Shield, 800) && skills.Arm(BattleSkill.Bomb) && skills.Use(BattleSkill.Bomb, target.Id, 800) && skills.Bombs.Count == 1,
            "(setup) shield up, a yarn bomb dropped");
        check(skills.Arm(BattleSkill.Push), "(setup) push chosen");
        skills.Step(dt, 800, true, false);
        check(skills.Armed == BattleSkill.Push && !skills.Use(BattleSkill.Push, target.Id, 800) && !skills.Use(BattleSkill.Cheer, 800),
            "A press on the cat keeps the chosen skill, but nothing can be used while the cat is held");
        skills.SetCatState(true, true);
        check(skills.Armed == null && skills.SkillsLocked, "A real lift drops the chosen skill at once");
        double bombCooldown = skills.Cooldown(BattleSkill.Bomb), shieldCooldown = skills.Cooldown(BattleSkill.Shield);
        Lift(skills, 3.2);
        check(skills.Bombs.Count == 0 && target.Hp == target.MaxHp - skills.Settings.BombDamage, "A bomb dropped before the lift still bursts and hurts");
        check(skills.Cooldown(BattleSkill.Bomb) == bombCooldown && skills.Cooldown(BattleSkill.Shield) == shieldCooldown,
            "Cooldowns do not recover while the cat is carried or flying");
        check(skills.ShieldRemaining == 0, "A running shield still wears off (it cannot be kept by holding the cat)");
        check(!skills.Use(BattleSkill.Cheer, 800) && !skills.Arm(BattleSkill.Push) && !skills.Use(BattleSkill.Bomb, target.Id, 800),
            "Calling the skills directly is refused while the cat is lifted");
        skills.Step(dt, 800, false);
        check(skills.Use(BattleSkill.Cheer, 800) && skills.Arm(BattleSkill.Push), "Back on the ground the skills work");
        double cheerCooldown = skills.Cooldown(BattleSkill.Cheer);
        Run(skills, 1);
        check(skills.Cooldown(BattleSkill.Cheer) < cheerCooldown - 0.9, "and recover normally");

        // Pause freezes bombs, buffs and cooldowns; resuming continues them, and no skill can be used meanwhile.
        var paused = Calm();
        var hit = paused.Slimes[0];
        hit.X = 1100;
        paused.Use(BattleSkill.Cheer, 800);
        paused.Arm(BattleSkill.Bomb); paused.Use(BattleSkill.Bomb, hit.Id, 800);
        double cheer = paused.CheerRemaining, bombLeft = paused.Bombs[0].Remaining, cooldown = paused.Cooldown(BattleSkill.Bomb);
        paused.Paused = true;
        Run(paused, 5);
        check(paused.Bombs.Count == 1 && paused.Bombs[0].Remaining == bombLeft && paused.CheerRemaining == cheer && paused.Cooldown(BattleSkill.Bomb) == cooldown,
            "Pause freezes a falling bomb, Cheer and the cooldowns");
        check(!paused.Use(BattleSkill.Shield, 800) && !paused.Arm(BattleSkill.Push), "No skill can be used during a pause, even directly");
        check(!paused.Select(paused.Slimes[1].Id) && paused.SelectedId == null, "No target can be marked during a pause either");
        paused.Paused = false;
        Run(paused, 0.6);
        check(paused.Bombs.Count == 0 && hit.Hp == hit.MaxHp - paused.Settings.BombDamage && paused.CheerRemaining < cheer, "After Resume the bomb bursts and Cheer runs on");

        // The battle's length (records) leaves the pause out.
        var timed = new BattleSim(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, Boss = false, SlimeMaxHp = 1, SlimeDamage = 0, OutroSeconds = 0.5 }, 0, 1600, new Random(5));
        timed.Step(dt, 800, false);
        var victim = timed.Slimes[0];
        for (double t = 0; t < 0.3; t += dt) timed.Step(dt, victim.X - 60, false);
        timed.Paused = true;
        for (double t = 0; t < 30; t += dt) timed.Step(dt, victim.X - 60, false);
        timed.Paused = false;
        for (double t = 0; t < 1.5 && !timed.Over; t += dt) timed.Step(dt, victim.X - 60, false);
        check(timed.Phase == BattlePhase.Won && timed.FightSeconds < 2, $"Paused time is not counted in the battle's length ({timed.FightSeconds:F2} s after a 30 s pause)");

        // Rescue balance, measured in real damage over 30 s against one enemy that never dies:
        // standing still, a timely rescue (lift when the "!" appears, put down out of reach) and
        // blind lift-and-drop on the spot (0.2 s up, 0.4 s down; it used to cancel every attack).
        (double HpLost, int Hits, int Strikes, double Dealt) Duel(string enemy, BattleDifficulty level, string mode)
        {
            var settings = new BattleSettings
            {
                FirstWaveDelay = 0, CatMaxHp = 10000, CatDamage = 25, SlimeMaxHp = 1_000_000,
                Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = enemy, Count = 1 } } } }
            };
            var duel = new BattleSim(settings, 0, 1600, new Random(1), level);
            const double step = 0.01;
            double cat = 800, lift = 0, landAt = 800, clock = 0;
            int hits = 0, strikes = 0;
            bool dodged = false; // This windup was already dodged (it is held, not gone, after the drop).
            duel.Step(step, cat, false);
            var foe = duel.Slimes[0];
            for (double t = 0; t < 1.5; t += step) duel.Step(step, cat, true); // Appears (bosses take longer).
            foe.Hp = foe.MaxHp = 1_000_000;
            foe.X = cat + 40;
            var last = foe.State;
            for (double t = 0; t < 30; t += step, clock += step)
            {
                bool held = mode switch
                {
                    "spam" => clock % 0.6 < 0.2,
                    "timely" => lift > 0,
                    _ => false
                };
                if (foe.State != SlimeState.Windup) dodged = false;
                if (mode == "timely" && lift <= 0 && !dodged && foe.State == SlimeState.Windup)
                {
                    // Out of reach on the far side from the slime, inside the floor.
                    landAt = foe.X < cat ? cat + 250 : cat - 250;
                    if (landAt is < 100 or > 1500) landAt = foe.X < cat ? foe.X - 250 : foe.X + 250;
                    lift = 0.3;
                    held = true;
                    dodged = true;
                }
                if (lift > 0) { lift -= step; if (lift <= 0) cat = landAt; }
                duel.Step(step, cat, held, held);
                foreach (var e in duel.Events) if (e.Kind == BattleEventKind.CatHurt) hits++;
                duel.ClearEvents();
                if (last == SlimeState.Windup && foe.State == SlimeState.Recover) strikes++;
                last = foe.State;
            }
            return (settings.CatMaxHp - duel.CatHp, hits, strikes, foe.MaxHp - foe.Hp);
        }
        foreach (var level in new[] { BattleDifficulty.Easy, BattleDifficulty.Normal, BattleDifficulty.Hard })
            foreach (var enemy in new[] { "slime", "king" })
            {
                var still = Duel(enemy, level, "still");
                var timely = Duel(enemy, level, "timely");
                var spam = Duel(enemy, level, "spam");
                string numbers = $"HP lost / hits / attacks finished / damage dealt — still {still.HpLost}/{still.Hits}/{still.Strikes}/{still.Dealt}, " +
                    $"timely {timely.HpLost}/{timely.Hits}/{timely.Strikes}/{timely.Dealt}, spam {spam.HpLost}/{spam.Hits}/{spam.Strikes}/{spam.Dealt}";
                Console.WriteLine($"  rescue balance {enemy} {level}: {numbers}");
                check(still.Hits > 0 && timely.Hits == 0 && timely.Strikes >= 3,
                    $"{enemy}, {level}: a timely rescue dodges every attack, which still finish (and miss) ({numbers})");
                check(spam.Hits >= still.Hits / 2 && spam.Strikes >= still.Strikes / 2,
                    $"{enemy}, {level}: lifting and dropping on the spot no longer cancels the attacks ({numbers})");
            }

        // battle.json: the protection numbers are checked.
        bool Rejects(BattleSettings settings)
        {
            try { BattleData.Validate(settings); return false; } catch (InvalidDataException) { return true; }
        }
        check(!Rejects(new BattleSettings()) && Rejects(new BattleSettings { RescueGraceCooldownSeconds = -1 }) && Rejects(new BattleSettings { RescueGraceCooldownSeconds = double.NaN }) &&
              Rejects(new BattleSettings { RescueGraceCooldownSeconds = 600 }) && Rejects(new BattleSettings { LandingGraceSeconds = -1 }),
            "rescueGraceCooldownSeconds and landingGraceSeconds must be sensible numbers");
    }
}
