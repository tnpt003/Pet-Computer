using DesktopPet;

// Battle Mode: the user's five skills and the Slime King, without WPF or Python.
internal static class BattleSkillTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        void Run(BattleSim sim, double seconds, double catX = 800, bool held = false)
        {
            for (double t = 0; t < seconds; t += dt) sim.Step(dt, catX, held);
        }
        // One slime parked next to the cat (spawned, settled while the cat is held).
        (BattleSim Sim, Slime Slime) Duel(BattleSettings settings, double offset = 40)
        {
            var sim = new BattleSim(settings, 0, 1600, new Random(3));
            sim.Step(dt, 800, false);
            Run(sim, 0.6, held: true);
            sim.SetCatState(false, false); // Let go (a plain press): skills work again, no landing protection.
            var slime = sim.Slimes[0];
            slime.X = 800 + offset;
            return (sim, slime);
        }
        int Count(BattleSim sim, BattleEventKind kind) => sim.Events.Count(e => e.Kind == kind);

        // Healing fish.
        var (heal, _) = Duel(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 40, CatDamage = 0 });
        check(!heal.Use(BattleSkill.Heal, 800) && heal.Cooldown(BattleSkill.Heal) == 0, "At full HP the fish is kept (no cooldown spent)");
        Run(heal, 1);
        double hurt = heal.CatHp;
        heal.ClearEvents();
        check(hurt == 60 && heal.Use(BattleSkill.Heal, 800) && heal.CatHp == 85 && Count(heal, BattleEventKind.Healed) == 1 &&
              heal.Cooldown(BattleSkill.Heal) == heal.Settings.HealCooldown, $"The fish gives back 25 HP and starts its cooldown ({hurt} → {heal.CatHp})");
        check(heal.Effects.Any(e => e.Kind == BattleEffectKind.Number && e.Value == -25), "A green +25 rises over the cat");
        check(!heal.Use(BattleSkill.Heal, 800), "It cannot be used again during its cooldown");
        check(!heal.Use(BattleSkill.Heal, heal.Slimes[0].Id, 800) && !heal.Arm(BattleSkill.Heal), "Instant skills are not aimed at a slime");

        // Cheer: faster and harder swings for a while.
        int Hits(bool cheer, out double perHit)
        {
            // No combos here: every hit is a plain one.
            var (sim, slime) = Duel(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0, ComboHits = 1000 });
            if (cheer) sim.Use(BattleSkill.Cheer, 800);
            int hits = 0;
            for (int i = 0; i < 120; i++) { slime.X = 840; sim.Step(dt, 800, false); hits += Count(sim, BattleEventKind.SlimeHit); sim.ClearEvents(); }
            perHit = (slime.MaxHp - slime.Hp) / Math.Max(1, hits);
            return hits;
        }
        int plain = Hits(false, out double plainHit), cheered = Hits(true, out double cheeredHit);
        check(cheered > plain && Math.Abs(cheeredHit - 35) < 0.01 && Math.Abs(plainHit - 25) < 0.01,
            $"Cheer makes the cat swing faster ({plain} → {cheered} hits in 2 s) and hit harder (25 → {cheeredHit})");
        // Combo: the third hit in a row hits twice as hard and knocks the slime back.
        var (combo, target) = Duel(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0 });
        var kinds = new List<BattleEventKind>();
        bool comboSwingSeen = false, plainSwingGold = false;
        double hpBefore = 0, comboDamage = 0, knock = 0;
        for (int i = 0; i < 300 && !kinds.Contains(BattleEventKind.ComboHit); i++)
        {
            target.X = 840;
            hpBefore = target.Hp;
            combo.Step(dt, 800, false);
            if (combo.SwingTime >= 0) { comboSwingSeen |= combo.ComboSwing; plainSwingGold |= combo.ComboSwing && combo.Combo < 2 && Count(combo, BattleEventKind.ComboHit) == 0; }
            foreach (var e in combo.Events) if (e.Kind is BattleEventKind.SlimeHit or BattleEventKind.ComboHit) kinds.Add(e.Kind);
            if (kinds.Contains(BattleEventKind.ComboHit)) { comboDamage = hpBefore - target.Hp; knock = target.KnockVelocity; }
            combo.ClearEvents();
        }
        check(string.Join(",", kinds) == "SlimeHit,SlimeHit,ComboHit" && Math.Abs(comboDamage - 50) < 0.01 && combo.CombosLanded == 1 && combo.Combo == 0,
            $"Two hits, then a combo hit for double damage ({string.Join(",", kinds)}; {comboDamage})");
        var comboNumbers = combo.Effects.Where(e => e.Kind == BattleEffectKind.Number && e.Value > 0).Select(e => e.Tone).ToList();
        check(comboNumbers.Count > 0 && comboNumbers[^1] == NumberTone.Combo && comboNumbers.Take(comboNumbers.Count - 1).All(t => t == NumberTone.Plain),
            $"Plain hits show plain numbers; the combo hit's number reads as a combo ({string.Join(",", comboNumbers)})");
        check(knock > 100 && target.State != SlimeState.Stunned && combo.Effects.Any(e => e.Kind == BattleEffectKind.Combo),
            $"The combo knocks the slime away from the cat (not stunned) with its own burst ({knock:0})");
        check(comboSwingSeen && !plainSwingGold, "Only the swing that can finish the chain is the gold one");
        // Lifting the cat, or a pause in the hits longer than the window, ends the chain.
        int HitsUntil(BattleSim sim, Slime slime, int count)
        {
            int seen = 0;
            for (int i = 0; i < 300 && seen < count; i++) { slime.X = 840; sim.Step(dt, 800, false); seen += Count(sim, BattleEventKind.SlimeHit); sim.ClearEvents(); }
            return seen;
        }
        var (lift, liftSlime) = Duel(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0 });
        HitsUntil(lift, liftSlime, 2);
        int chain = lift.Combo;
        lift.Step(dt, 800, true, true);
        check(chain == 2 && lift.Combo == 0, "Lifting the cat ends the chain");
        var (slow, slowSlime) = Duel(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0 });
        HitsUntil(slow, slowSlime, 2);
        slowSlime.X = 1500;
        Run(slow, slow.Settings.ComboWindow + 0.3, 800);
        slow.ClearEvents();
        HitsUntil(slow, slowSlime, 1);
        check(slow.CombosLanded == 0 && slow.Combo == 1, "A gap longer than the combo window starts a new chain");
        var (cheer, _) = Duel(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0 });
        cheer.Use(BattleSkill.Cheer, 800);
        Run(cheer, 5.1);
        check(cheer.CheerRemaining == 0 && cheer.Cooldown(BattleSkill.Cheer) > 0, "Cheer wears off after 5 s; its cooldown runs on");

        // Push direction: the user decides by where the slime is clicked.
        double Pushed(int? direction)
        {
            var (sim, slime) = Duel(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0, CatDamage = 0 }, 200);
            double start = slime.X;
            sim.UsePush(slime.Id, 800, direction);
            Run(sim, 0.5, held: true);
            return slime.X - start;
        }
        check(Pushed(1) > 50 && Pushed(-1) < -50 && Pushed(null) > 50, "Push goes right or left as chosen (toward the cat too); without a choice, away from the cat");
        check(BattleSim.PushDirection(-10) == 1 && BattleSim.PushDirection(10) == -1 && BattleSim.PushDirection(1) == null,
            "Clicking a slime's left half pushes it right, its right half left, its centre: default");

        // Bubble shield.
        var (shield, _) = Duel(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 20, CatDamage = 0 });
        check(shield.Use(BattleSkill.Shield, 800) && shield.ShieldRemaining > 0, "The shield goes up at once");
        int blocked = 0;
        for (double t = 0; t < 2.9; t += dt) { shield.Step(dt, 800, false); blocked += Count(shield, BattleEventKind.Blocked); shield.ClearEvents(); }
        check(shield.CatHp == shield.Settings.CatMaxHp && blocked >= 1, "While it lasts, slime hits are blocked");
        Run(shield, 3);
        check(shield.ShieldRemaining == 0 && shield.CatHp < shield.Settings.CatMaxHp, "After 3 s hits land again");

        // Yarn bomb: aimed, delayed, hits everything near the spot.
        var bombSettings = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 3 }, MaxAlive = 3, SpawnInterval = 0, SlimeMaxHp = 100, SlimeDamage = 0, CatDamage = 0 };
        var bomb = new BattleSim(bombSettings, 0, 1600, new Random(3));
        for (int i = 0; i < 3; i++) bomb.Step(dt, 800, false);
        Run(bomb, 0.6, held: true);
        bomb.SetCatState(false, false);
        var near = bomb.Slimes[0]; var side = bomb.Slimes[1]; var far = bomb.Slimes[2];
        near.X = 1100; side.X = 1150; far.X = 1400;
        check(!bomb.Use(BattleSkill.Bomb, 800) && bomb.Arm(BattleSkill.Bomb) && bomb.Armed == BattleSkill.Bomb, "The bomb is chosen first, then aimed");
        check(bomb.Use(BattleSkill.Bomb, near.Id, 800) && bomb.Armed == null && bomb.Bombs.Count == 1 && near.Hp == 100,
            "Clicking a slime drops the yarn ball; nothing is hit yet");
        Run(bomb, 0.55, held: true);
        check(bomb.Bombs.Count == 0 && near.Hp == 70 && side.Hp == 70 && far.Hp == 100, "It bursts after 0.5 s and hits only the slimes near the spot");
        check(!bomb.Arm(BattleSkill.Bomb) && bomb.Cooldown(BattleSkill.Bomb) > 11, "Then it cools down");
        check(bomb.Effects.Count(e => e.Kind == BattleEffectKind.Number) >= 2, "Each hit slime shows its damage number");

        // The Slime King.
        var bossSettings = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1, WaveBreakSeconds = 1, CatDamage = 25 };
        var boss = new BattleSim(bossSettings, 0, 1600, new Random(3));
        check(boss.WaveCount == 2 && boss.IsBossWave(2) && !boss.IsBossWave(1), "With the boss on, a boss wave follows the normal ones");
        boss.Step(dt, 800, false);
        boss.Slimes[0].X = 840;
        bool appeared = false;
        for (double t = 0; t < 5 && boss.Boss == null; t += dt) { boss.Step(dt, 800, false); appeared |= Count(boss, BattleEventKind.BossAppeared) > 0; boss.ClearEvents(); }
        var king = boss.Boss;
        check(appeared && king != null && king.IsBoss && king.MaxHp == 400 && king.Width == 92 && boss.ToSpawn == 0 && boss.Alive == 1,
            "After the last normal wave the Slime King appears alone (twice as big, 400 HP)");
        check(Math.Abs(king!.X - 800) >= bossSettings.SpawnMinDistance, "It appears away from the cat too");

        var (slam, bigSlime) = BossDuel(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1, CatDamage = 0, WaveBreakSeconds = 0 }, 100);
        Run(slam, 1.1);
        check(bigSlime.State == SlimeState.Windup && slam.CatHp == 100, "The slam winds up for 1.2 s (a long warning) and reaches 100 DIPs away");
        Run(slam, 0.2);
        check(slam.CatHp == 75 && slam.Effects.Any(e => e.Kind == BattleEffectKind.Slam), "Then it lands for 25 damage with a shock wave");

        var (heavy, heavyKing) = BossDuel(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1, CatDamage = 0, SlimeDamage = 0, WaveBreakSeconds = 0, BossDamage = 0 }, 60);
        double before = heavyKing.X;
        heavy.UsePush(heavyKing.Id, 800);
        Run(heavy, 1);
        check(heavyKing.X - before > 10 && heavyKing.X - before < 40 && heavyKing.State != SlimeState.Stunned,
            $"The boss is heavy: a push moves it only a little and stuns it briefly (moved {heavyKing.X - before:F0})");

        var phases = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1, CatDamage = 0, SlimeDamage = 0, BossDamage = 0, WaveBreakSeconds = 0, BombDamage = 150, BombCooldown = 0 };
        var (rage, rageKing) = BossDuel(phases, 300);
        void Bomb(BattleSim sim, Slime target) { sim.SetCatState(false, false); sim.Arm(BattleSkill.Bomb); sim.Use(BattleSkill.Bomb, target.Id, 800); Run(sim, 0.55, held: true); }
        Bomb(rage, rageKing);
        check(rageKing.Hp == 250 && rage.Alive == 1 && !rageKing.Enraged, "(setup) 250/400 HP: nothing special yet");
        bool summoned = false, enraged = false;
        rage.SetCatState(false, false); rage.Arm(BattleSkill.Bomb); rage.Use(BattleSkill.Bomb, rageKing.Id, 800);
        for (double t = 0; t < 0.55; t += dt) { rage.Step(dt, 800, true); summoned |= Count(rage, BattleEventKind.BossSummoned) > 0; enraged |= Count(rage, BattleEventKind.BossEnraged) > 0; rage.ClearEvents(); }
        check(summoned && rage.Alive == 3 && rage.Slimes.Count(s => !s.IsBoss) == 2, "Below half HP the King calls two helper slimes, once");
        check(enraged && rageKing.Enraged && rage.Windup(rageKing) == phases.BossEnragedWindup, "Below 30% it gets angry: shorter windup, faster steps");
        foreach (var helper in rage.Slimes.Where(s => !s.IsBoss)) helper.X = rageKing.X + 400 + helper.Id * 60;
        Bomb(rage, rageKing);
        check(rage.BossDefeated && rage.Boss == null && rage.Phase == BattlePhase.Fighting, "Defeating the King with helpers left does not end the battle yet");
        foreach (var helper in rage.Slimes.Where(s => s.Targetable).ToList()) { rage.Arm(BattleSkill.Bomb); rage.Use(BattleSkill.Bomb, helper.Id, 800); Run(rage, 0.55); }
        Run(rage, 1);
        check(rage.Phase == BattlePhase.Won, "With the King and its helpers gone the battle is won");
    }

    // A battle that skips straight to the Slime King, parked `offset` DIPs right of the cat.
    private static (BattleSim, Slime) BossDuel(BattleSettings settings, double offset)
    {
        const double dt = 1.0 / 60;
        var sim = new BattleSim(settings, 0, 1600, new Random(3));
        sim.Step(dt, 800, false);
        for (double t = 0; t < 0.6; t += dt) sim.Step(dt, 800, true);
        // Clear the single normal slime with a bomb, then the boss wave starts.
        sim.SetCatState(false, false);
        sim.Arm(BattleSkill.Bomb); sim.Use(BattleSkill.Bomb, sim.Slimes[0].Id, 800);
        for (double t = 0; t < 0.55; t += dt) sim.Step(dt, 800, true);
        for (double t = 0; t < 3 && sim.Boss == null; t += dt) sim.Step(dt, 800, false);
        for (double t = 0; t < 1.2; t += dt) sim.Step(dt, 800, true); // Let it rise while nobody moves.
        sim.SetCatState(false, false);
        var king = sim.Boss!;
        king.X = 800 + offset;
        return (sim, king);
    }
}
