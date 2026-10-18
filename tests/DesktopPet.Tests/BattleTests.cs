using DesktopPet;

// Battle Mode's fight (BattleSim) without WPF or Python.
internal static class BattleTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        // A fixed seed and no waits make every scenario repeatable.
        BattleSim New(BattleSettings? settings = null) => new(settings ?? new BattleSettings { FirstWaveDelay = 0 }, 0, 1600, new Random(7));
        void Run(BattleSim sim, double seconds, Func<double> catX, bool held = false, bool lifted = false)
        {
            for (double t = 0; t < seconds; t += dt) sim.Step(dt, catX(), held, lifted);
        }

        // Waves and spawning.
        var sim = New(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 5 }, MaxAlive = 2, SpawnInterval = 0.1 });
        int maxAlive = 0;
        for (int i = 0; i < 120; i++) { sim.Step(dt, 800, false); maxAlive = Math.Max(maxAlive, sim.Alive); }
        check(sim.Wave == 1 && maxAlive == 2 && sim.ToSpawn == 3, "Slimes spawn in waves, never more than the limit at once");
        check(sim.Slimes.All(s => Math.Abs(s.X - 800) >= sim.Settings.SpawnMinDistance && s.X >= sim.Settings.SlimeWidth / 2 && s.X <= 1600 - sim.Settings.SlimeWidth / 2),
            "New slimes appear inside the floor and away from the cat");
        var narrow = new BattleSim(new BattleSettings { FirstWaveDelay = 0 }, 0, 300, new Random(1));
        narrow.Step(dt, 150, false);
        check(narrow.Slimes.Count == 1 && narrow.Slimes[0].X >= 23 && narrow.Slimes[0].X <= 277, "A narrow floor still spawns inside its edges");

        sim = New(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1, 1 }, Boss = false, WaveBreakSeconds = 3 });
        sim.Step(dt, 800, false);
        var first = sim.Slimes[0];
        first.Hp = 1;
        double cat = first.X + 60; // Within reach.
        Run(sim, 1, () => cat);
        check(sim.Phase == BattlePhase.WaveBreak && sim.Wave == 1 && sim.Alive == 0, "Clearing a wave starts a short break");
        Run(sim, 1.9, () => cat);
        check(sim.Phase == BattlePhase.WaveBreak && sim.Slimes.Count == 0, "No slime arrives during the break");
        Run(sim, 1.0, () => cat);
        check(sim.Phase == BattlePhase.Fighting && sim.Wave == 2 && sim.Slimes.Count == 1, "The next wave starts after the break");
        sim.Slimes[0].Hp = 1;
        cat = sim.Slimes[0].X + 60;
        Run(sim, 1, () => cat);
        check(sim.Phase == BattlePhase.Won && sim.Over && !sim.Finished, "Clearing the last wave wins the battle (with a short outro)");
        Run(sim, sim.Settings.OutroSeconds + 0.1, () => cat);
        check(sim.Finished && sim.Slimes.Count == 0, "After the outro the battle is finished");

        // Targeting.
        sim = New(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 3 }, MaxAlive = 3, SpawnInterval = 0 });
        for (int i = 0; i < 3; i++) sim.Step(dt, 800, false);
        Run(sim, 0.6, () => 800, held: true); // Spawned and settled, but nobody moves while the cat is held.
        var slimes = sim.Slimes.ToList();
        slimes[0].X = 300; slimes[1].X = 1000; slimes[2].X = 1500;
        sim.Step(dt, 800, false);
        check(sim.TargetId == slimes[1].Id, "The cat goes for the nearest slime by itself");
        var order = sim.Step(dt, 800, false);
        check(order.Command == CatCommand.Move && order.X > 800 && order.Facing == 1, "It walks toward its target (no teleport)");
        check(sim.Select(slimes[2].Id) && sim.SelectedId == slimes[2].Id, "Clicking a slime marks it as the target");
        order = sim.Step(dt, 800, false);
        check(sim.TargetId == slimes[2].Id && order.X > 1000, "The cat prefers the marked slime over a closer one");
        check(slimes.All(s => s.Hp == s.MaxHp), "Marking a target deals no damage");
        slimes[2].Hp = 1;
        Run(sim, 0.4, () => slimes[2].X - 60);
        check(sim.SelectedId == null && sim.Slimes.All(s => s.Id != slimes[2].Id || s.State == SlimeState.Defeated), "A defeated target loses its marker");
        sim.Step(dt, 800, false);
        check(sim.TargetId is int next && next != slimes[2].Id, "Then the cat picks another slime by itself");
        check(!sim.Select(slimes[2].Id), "A defeated slime cannot be marked");

        // Reach, cooldown and the single hit of a swing.
        var s1 = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0 };
        sim = New(s1);
        sim.Step(dt, 800, false);
        var dummy = sim.Slimes[0];
        Run(sim, 0.6, () => 800, held: true);
        dummy.X = 800 + s1.CatAttackRange + 40;
        order = sim.Step(dt, 800, false);
        check(order.Command == CatCommand.Move && sim.SwingTime < 0, "Out of reach: the cat moves instead of swinging");
        dummy.X = 800 + s1.CatAttackRange - 10;
        sim.Step(dt, 800, false);
        check(sim.SwingTime >= 0, "In reach: the cat swings");
        int hits = 0;
        double hp = dummy.Hp;
        for (int i = 0; i < 60; i++) { dummy.X = 800 + s1.CatAttackRange - 10; sim.Step(dt, 800, false); hits += sim.Events.Count(e => e.Kind == BattleEventKind.SlimeHit); sim.ClearEvents(); }
        // 1 s with a 0.75 s cooldown: the first swing and one more.
        check(hits == 2 && hp - dummy.Hp == 2 * s1.CatDamage, "Each swing deals damage exactly once, paced by the cooldown");
        var fine = New(s1); fine.Step(dt, 800, false); Run(fine, 0.6, () => 800, held: true);
        fine.Slimes[0].X = 800 + s1.CatAttackRange - 10;
        for (int i = 0; i < 600; i++) fine.Step(1.0 / 600, 800, false);
        check(fine.Slimes[0].MaxHp - fine.Slimes[0].Hp == 2 * s1.CatDamage, "The same damage at 600 FPS: hits follow simulation time, not render frames");
        var dodge = New(s1); dodge.Step(dt, 800, false); Run(dodge, 0.6, () => 800, held: true);
        dodge.Slimes[0].X = 800 + s1.CatAttackRange - 10;
        dodge.Step(dt, 800, false); // Swing starts.
        dodge.Slimes[0].X = 800 + s1.CatAttackRange + 60; // Knocked away before the hit moment.
        Run(dodge, s1.CatSwingDuration, () => 800);
        check(dodge.Slimes[0].Hp == dodge.Slimes[0].MaxHp, "A slime out of reach at the hit moment takes no damage");

        // Slime windup, damage and the cat's HP.
        var s2 = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, CatDamage = 0 };
        sim = New(s2);
        sim.Step(dt, 800, false);
        Run(sim, 0.6, () => 800, held: true);
        var slime = sim.Slimes[0];
        slime.X = 800 + 40;
        sim.Step(dt, 800, false);
        check(slime.State == SlimeState.Windup && sim.CatHp == s2.CatMaxHp, "A slime in reach first winds up (the warning), no damage yet");
        Run(sim, s2.SlimeWindup - 0.1, () => 800);
        check(sim.CatHp == s2.CatMaxHp, "No damage before the windup ends");
        Run(sim, 0.15, () => 800);
        check(sim.CatHp == s2.CatMaxHp - s2.SlimeDamage && slime.State == SlimeState.Recover, "The windup ends in one hit");
        Run(sim, s2.SlimeAttackCooldown - 0.2, () => 800);
        check(sim.CatHp == s2.CatMaxHp - s2.SlimeDamage, "The slime waits for its cooldown before the next attack");

        // Push skill.
        sim = New(s2);
        sim.Step(dt, 800, false);
        Run(sim, 0.6, () => 800, held: true);
        slime = sim.Slimes[0];
        slime.X = 840;
        sim.Step(dt, 800, false); // Winding up.
        check(sim.ArmPush(true) && sim.PushArmed, "The push skill can be chosen");
        check(sim.UsePush(slime.Id, 800) && slime.State == SlimeState.Stunned && !sim.PushArmed && sim.PushCooldown == s2.PushCooldown,
            "Using it on a slime stuns it, cancels its attack and starts the cooldown");
        check(sim.SelectedId == null && slime.Hp == slime.MaxHp, "Pushing changes no target and deals no damage");
        Run(sim, 0.8, () => 800);
        check(slime.X > 880 && slime.State == SlimeState.Stunned && sim.CatHp == s2.CatMaxHp, "The slime is knocked away from the cat and stays stunned briefly");
        check(!sim.ArmPush(true) && !sim.UsePush(slime.Id, 800), "The skill cannot be used again during its cooldown");
        Run(sim, s2.PushCooldown, () => 800);
        check(sim.PushCooldown == 0 && sim.ArmPush(true) && sim.ArmPush(false) && !sim.PushArmed, "After the cooldown it is ready again, and choosing it can be cancelled");
        check(slime.State != SlimeState.Stunned, "The stun wears off");
        var edge = New(s2);
        edge.Step(dt, 800, false);
        Run(edge, 0.6, () => 800, held: true);
        edge.SetCatState(false, false);
        var corner = edge.Slimes[0];
        corner.X = corner.Width / 2 + 5;
        check(edge.UsePush(corner.Id, 800, -1), "(setup) a slime at the left edge is pushed further left");
        Run(edge, 0.8, () => 800);
        check(corner.X >= corner.Width / 2 - 0.001 && corner.X <= corner.Width / 2 + 5 && corner.State == SlimeState.Stunned,
            $"A push into the floor's edge stops at the edge, still stunned ({corner.X:F1})");

        // Pause freezes everything; resuming brings no burst.
        sim = New(s2);
        sim.Step(dt, 800, false);
        Run(sim, 0.6, () => 800, held: true);
        slime = sim.Slimes[0]; slime.X = 840;
        sim.Step(dt, 800, false);
        double windupAt = slime.StateTime, timeAt = sim.Time;
        sim.Paused = true;
        Run(sim, 30, () => 800);
        check(slime.StateTime == windupAt && sim.Time == timeAt && sim.CatHp == s2.CatMaxHp, "Pause freezes windups, timers and damage");
        sim.Paused = false;
        sim.Step(1.0, 800, false); // A long first frame after resuming is capped.
        check(sim.CatHp == s2.CatMaxHp && slime.State == SlimeState.Windup, "Resuming does not release a burst of stored time");
        check(sim.Step(dt, double.NaN, false).Command == CatCommand.Stand && sim.Step(double.NaN, 800, false).Command == CatCommand.Stand,
            "Invalid input is ignored");

        // Holding (dragging) the cat.
        // (No combo knock-back here: the slime has to stay next to the cat.)
        sim = New(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 4 }, MaxAlive = 2, SpawnInterval = 0, SlimeMaxHp = 1000, ComboHits = 1000 });
        sim.Step(dt, 800, false);
        Run(sim, 0.6, () => 800, held: true);
        slime = sim.Slimes[0]; slime.X = 840;
        sim.Step(dt, 800, false);
        check(slime.State == SlimeState.Windup, "(setup) a slime winds up next to the cat");
        int spawned = sim.Slimes.Count, toSpawn = sim.ToSpawn;
        var follower = sim.Slimes.Last(s => s != slime);
        double x0 = follower.X, heldAt = slime.StateTime;
        order = sim.Step(dt, 800, true, true);
        check(order.Command == CatCommand.Hold && slime.State == SlimeState.Windup && slime.StateTime == heldAt && sim.SwingTime < 0,
            "Lifting the cat holds the slime's attack where it is (the warning stays) and stops the cat's own swing");
        bool woundUp = false;
        for (double t = 0; t < 5; t += dt) { sim.Step(dt, 1200, true, true); woundUp |= sim.Slimes.Any(s => s != slime && s.State == SlimeState.Windup); }
        check(sim.CatHp == sim.Settings.CatMaxHp && sim.Slimes.Count == spawned && sim.ToSpawn == toSpawn && !woundUp,
            "While carried: no damage, no new slimes, no wave progress, no other slime winds up");
        check(slime.State == SlimeState.Windup && slime.StateTime == heldAt && slime.X == 840, "The held attack neither progresses nor moves while the cat is carried");
        check(Math.Abs(follower.X - 1200) < Math.Abs(x0 - 1200) - 100 || Math.Abs(follower.X - 1200) <= follower.Range,
            $"The other slimes follow the carried cat along the floor ({x0:F0} → {follower.X:F0})");
        sim.Step(dt, 800, false);
        check(sim.Grace > 0, "Putting the cat down starts the protection time");
        Run(sim, sim.Settings.LandingGraceSeconds - 0.2, () => 800);
        check(sim.CatHp == sim.Settings.CatMaxHp && slime.State != SlimeState.Windup,
            "Put back on the same spot with landing protection, the held attack finishes and misses; no new attack starts");
        // The missed hit costs the slime its full attack cooldown (1.8 s), then a new windup (0.8 s).
        Run(sim, 3, () => 800);
        check(sim.CatHp < sim.Settings.CatMaxHp, "After it, slimes attack again");
        var click = New(new BattleSettings { FirstWaveDelay = 0 });
        click.Step(dt, 800, true, false); click.Step(dt, 800, false);
        check(click.Grace == 0, "A plain click (press without lifting) grants no protection");

        // Losing.
        sim = New(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, CatMaxHp = 10, SlimeDamage = 10, CatDamage = 0 });
        sim.Step(dt, 800, false);
        Run(sim, 0.6, () => 800, held: true);
        sim.Slimes[0].X = 840;
        Run(sim, 1, () => 800);
        check(sim.Phase == BattlePhase.Lost && sim.CatHp == 0 && sim.Over, "At 0 HP the battle is lost");
        check(sim.Step(dt, 800, false).Command == CatCommand.Stand && !sim.Select(sim.Slimes[0].Id) && !sim.ArmPush(true), "After the end nothing more happens");

        // The floor moves (work area change, other monitor).
        sim = New(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 } });
        sim.Step(dt, 800, false);
        sim.Slimes[0].X = 1200;
        sim.SetArea(-1920, -320);
        check(sim.Left == -1920 && sim.Right == -320 && Math.Abs(sim.Slimes[0].X - (-1920 + 1200)) < 0.01, "A new floor keeps the slimes at the same relative spot");
        sim.SetArea(0, 100);
        check(sim.Slimes[0].X >= 23 && sim.Slimes[0].X <= 77, "A smaller floor clamps the slimes inside it");

        // Effects stay bounded.
        sim = New(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, MaxEffects = 3, PushCooldown = 0 });
        sim.Step(dt, 800, false);
        Run(sim, 0.6, () => 800, held: true);
        sim.SetCatState(false, false);
        for (int i = 0; i < 10; i++) sim.UsePush(sim.Slimes[0].Id, 800);
        check(sim.Effects.Count == 3, "Effects are capped");
        Run(sim, 1, () => 800, held: true);
        check(sim.Effects.Count == 0, "Effects disappear by themselves");

        // HP back between waves (not after the last), and the battle's length for the result.
        var heal = New(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1, 1 }, Boss = false, SlimeMaxHp = 60, SlimeDamage = 50, WaveBreakSeconds = 5 });
        heal.Step(dt, 800, false);
        heal.Slimes[0].X = 840;
        Run(heal, 3, () => 800);
        check(heal.Phase == BattlePhase.WaveBreak && heal.LastHeal == 15 && heal.CatHp == 65, $"Clearing a wave gives back {heal.Settings.WaveHeal} HP (HP {heal.CatHp})");
        for (int i = 0; i < 600 && !(heal.Phase == BattlePhase.Fighting && heal.Slimes.Count > 0); i++) heal.Step(dt, 800, false);
        heal.Slimes[0].X = 840;
        Run(heal, 3, () => 800);
        check(heal.Phase == BattlePhase.Won && heal.CatHp == 15, $"The last wave gives nothing back (HP {heal.CatHp})");
        double fight = heal.FightSeconds;
        Run(heal, 1, () => 800);
        check(fight > 5 && heal.FightSeconds == fight && heal.Time > fight, "The battle's length stops at the end (the outro is not counted)");
        var full = New(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1, 1 }, SlimeMaxHp = 1, SlimeDamage = 0 });
        full.Step(dt, 800, false);
        full.Slimes[0].X = 840;
        Run(full, 1.5, () => 800);
        check(full.Phase == BattlePhase.WaveBreak && full.CatHp == full.Settings.CatMaxHp && full.LastHeal == 0, "Healing never goes above full HP");

        // Battle foley: short, quiet, synthesized, distinct and safe to scale.
        string[] foley = { "swoosh", "squish", "pop" };
        var waves = foley.Select(kind => PetSounds.CreateWave(kind, 3)).ToArray();
        bool valid = true;
        foreach (var w in waves)
        {
            int peak = 0;
            for (int i = 44; i + 1 < w.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(w, i)));
            double seconds = (w.Length - 44) / 44100.0;
            valid &= w[0] == 'R' && BitConverter.ToInt32(w, 40) == w.Length - 44 && seconds is > 0.05 and < 0.25 && peak > 2000 && peak < 32767 * 0.5;
        }
        check(valid, "Battle effects are valid short WAVs with a soft peak");
        check(waves.Select(Convert.ToBase64String).Distinct().Count() == 3 && !waves[0].SequenceEqual(PetSounds.CreateWave("meow", 3)),
            "Swoosh, squish and pop sound different from each other and from the cat");
        check(PetSounds.CreateWave("pop", 3).SequenceEqual(waves[2]) && !PetSounds.CreateWave("pop", 4).SequenceEqual(waves[2]), "The same seed gives the same effect; another seed varies it");
        byte[] shared = PetSounds.CreateWave("squish"); Array.Clear(shared);
        check(PetSounds.CreateWave("squish")[0] == (byte)'R' && foley.All(PetSounds.IsBattleEffect) && !PetSounds.IsBattleEffect("meow"),
            "Volume scaling never touches the cached effects");
    }
}
