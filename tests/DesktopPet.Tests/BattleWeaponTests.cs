using DesktopPet;

// Battle Mode's two weapons: the sword (fast, the existing numbers) and the hammer (slow, heavy,
// knocks back on every hit). Measured in simulation only; how they feel needs real play.
internal static class BattleWeaponTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        var spec = new BattleSettings();
        check(spec.Weapon(BattleWeapon.Sword) is { Damage: 25, Range: 76, AttackCooldown: 0.75, SwingDuration: 0.36, HitTime: 0.16, Knock: 0 },
            "The sword is the existing Cat* numbers (no balance change for it)");
        var sword = new BattleSim(new BattleSettings(), 0, 1600);
        check(sword.Weapon.Id == BattleWeapon.Sword, "Without a choice a battle uses the sword");

        // One slime parked next to the cat, settled while the cat is held.
        (BattleSim Sim, Slime Slime) Duel(string weapon, BattleSettings settings, double offset)
        {
            var sim = new BattleSim(settings, 0, 1600, new Random(3), weapon: weapon);
            sim.Step(dt, 800, false);
            for (double t = 0; t < 0.6; t += dt) sim.Step(dt, 800, true);
            sim.SetCatState(false, false);
            var slime = sim.Slimes[0];
            slime.X = 800 + offset;
            return (sim, slime);
        }
        // Hits and damage per hit over 10 s against a slime kept in reach.
        (int Hits, double PerHit, bool KnockedEveryHit) Swings(string weapon)
        {
            var (sim, slime) = Duel(weapon, new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 100000, SlimeDamage = 0, ComboHits = 1000 }, 40);
            int hits = 0; bool knocked = true;
            for (double t = 0; t < 10; t += dt)
            {
                slime.X = 840; slime.KnockVelocity = 0;
                sim.Step(dt, 800, false);
                foreach (var e in sim.Events) if (e.Kind == BattleEventKind.SlimeHit) { hits++; knocked &= weapon == BattleWeapon.Sword ? slime.KnockVelocity == 0 : slime.KnockVelocity > 100; }
                sim.ClearEvents();
            }
            return (hits, (slime.MaxHp - slime.Hp) / Math.Max(1, hits), knocked);
        }
        var s = Swings(BattleWeapon.Sword);
        var h = Swings(BattleWeapon.Hammer);
        check(h.Hits < s.Hits && h.PerHit == 45 && s.PerHit == 25, $"The hammer swings less often but hits harder ({s.Hits} × {s.PerHit} vs {h.Hits} × {h.PerHit} in 10 s)");
        check(Math.Abs(h.Hits * h.PerHit - s.Hits * s.PerHit) <= 0.25 * s.Hits * s.PerHit,
            $"Their damage over time is close ({s.Hits * s.PerHit} vs {h.Hits * h.PerHit} in 10 s)");
        check(s.KnockedEveryHit && h.KnockedEveryHit, "Only the hammer knocks back on plain hits (away from the cat)");

        // A slime winding up at the edge of its reach: the hammer's knock-back makes its hit miss;
        // the sword's hit leaves it there and the hit lands.
        int HurtsFrom(string weapon)
        {
            var (sim, slime) = Duel(weapon, new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 100000, CatDamage = 25 }, 60);
            int hurts = 0;
            for (double t = 0; t < 1.2; t += dt) { sim.Step(dt, 800, false); foreach (var e in sim.Events) if (e.Kind == BattleEventKind.CatHurt) hurts++; sim.ClearEvents(); }
            return hurts;
        }
        check(HurtsFrom(BattleWeapon.Hammer) == 0 && HurtsFrom(BattleWeapon.Sword) == 1,
            "A hammer hit can knock a winding-up slime out of reach (its attack misses); a sword hit cannot");

        // Real fights (the cat walks where the battle tells it, as in the window): 30 s against one
        // enemy that never dies, and a wave of three ordinary slimes. Neither weapon is a free win.
        (double Taken, double Dealt) Fight(string weapon, string enemy, BattleDifficulty level)
        {
            var settings = new BattleSettings
            {
                FirstWaveDelay = 0, CatMaxHp = 100000,
                Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = enemy, Count = 1 } } } }
            };
            var sim = new BattleSim(settings, 0, 1600, new Random(2), level, weapon);
            double cat = 800;
            sim.Step(dt, cat, false);
            var foe = sim.Slimes[0];
            foe.Hp = foe.MaxHp = 1_000_000;
            for (double t = 0; t < 30; t += dt) Walk(sim, ref cat);
            return (settings.CatMaxHp - sim.CatHp, foe.MaxHp - foe.Hp);
        }
        (double Seconds, double Taken) Wave(string weapon)
        {
            var sim = new BattleSim(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 3 }, Boss = false, MaxAlive = 3, SpawnInterval = 0 }, 0, 1600, new Random(4), weapon: weapon);
            double cat = 800, t = 0;
            for (; t < 60 && !sim.Over; t += dt) Walk(sim, ref cat);
            return (sim.Over ? sim.FightSeconds : double.PositiveInfinity, sim.Settings.CatMaxHp - sim.CatHp);
        }
        static void Walk(BattleSim sim, ref double cat)
        {
            var order = sim.Step(dt, cat, false);
            const double run = 72 * 1.8; // Walking speed × CatRunScale.
            if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -run * dt, run * dt);
        }
        foreach (var level in new[] { BattleDifficulty.Easy, BattleDifficulty.Normal, BattleDifficulty.Hard })
            foreach (var enemy in new[] { "slime", "king" })
            {
                var a = Fight(BattleWeapon.Sword, enemy, level);
                var b = Fight(BattleWeapon.Hammer, enemy, level);
                string numbers = $"sword took {a.Taken} / dealt {a.Dealt}, hammer took {b.Taken} / dealt {b.Dealt}";
                Console.WriteLine($"  weapon balance {enemy} {level}: {numbers}");
                check(b.Taken > 0 && a.Taken > 0 && b.Dealt >= a.Dealt * 0.6 && b.Dealt <= a.Dealt * 1.4,
                    $"{enemy}, {level}: both weapons get hit and deal comparable damage over 30 s ({numbers})");
            }
        var ws = Wave(BattleWeapon.Sword);
        var wh = Wave(BattleWeapon.Hammer);
        Console.WriteLine($"  weapon balance wave of 3: sword {ws.Seconds:F1} s / -{ws.Taken} HP, hammer {wh.Seconds:F1} s / -{wh.Taken} HP");
        check(double.IsFinite(ws.Seconds) && double.IsFinite(wh.Seconds) && wh.Seconds <= ws.Seconds * 1.6,
            $"Both clear a wave of three slimes; the hammer is not much slower (sword {ws.Seconds:F1} s / -{ws.Taken} HP, hammer {wh.Seconds:F1} s / -{wh.Taken} HP)");

        // battle.json: the hammer's numbers are checked.
        bool Rejects(WeaponSpec hammer)
        {
            try { BattleData.Validate(new BattleSettings { Hammer = hammer }); return false; } catch (InvalidDataException) { return true; }
        }
        check(!Rejects(new WeaponSpec()) && Rejects(new WeaponSpec { HitTime = 0.7 }) && Rejects(new WeaponSpec { Range = 0 }) &&
              Rejects(new WeaponSpec { AttackCooldown = double.NaN }) && Rejects(new WeaponSpec { Knock = -1 }) && Rejects(new WeaponSpec { Knock = 5000 }),
            "Impossible hammer numbers in battle.json are rejected");
    }
}
