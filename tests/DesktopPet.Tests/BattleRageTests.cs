using DesktopPet;

// 1.5 M07 (PetBattle.Rage.cs), with stand-in enemies: the rage bar fills from the cat's landed swings
// and the HP it loses, never by itself starts the rage; the click works only full, on the floor, not
// stunned and not paused; the rage lasts its battle time (Pause freezes it), the bar stays empty
// meanwhile; a raging swing hits every enemy in reach (not one out of it) and knocks harder; no stun
// gets through while raging (the hit still hurts); the held cat's clock runs on; data limits and the
// shipped numbers.
internal static class BattleRageTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Dummy(string id) => new()
    {
        Id = id, MaxHp = 1000, Speed = 0, Damage = 0, Range = 64, Windup = 0.4, AttackCooldown = 1, Recover = 0.2
    };

    private static BattleSim Field(double hitGain, double hurtGain = 0, int count = 4)
    {
        var kinds = Enumerable.Range(0, count).Select(i => Dummy("d" + i)).ToArray();
        var settings = new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1000, MaxAlive = 8, Enemies = kinds, RageHitGain = hitGain, RageHurtGain = hurtGain,
            Weapons = new[] { new WeaponSpec { Id = "sword", Damage = 10, Knock = 0, StunChance = 0 } },
            Plan = new[] { new WaveSpec { Groups = kinds.Select(k => new WaveGroup { Enemy = k.Id, Count = 1 }).ToArray() } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 7);
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < count; i++) sim.Step(dt, 100, false);
        // Out of the way until a check places them.
        foreach (var s in sim.Slimes) s.X = 1500;
        sim.ClearEvents();
        return sim;
    }

    // pin: an enemy kept at 150 (a combo or a raging hit would knock it out of reach for good: it never walks).
    private static void Run(BattleSim sim, double seconds, double catX = 100, List<BattleEvent>? seen = null, bool held = false, Slime? pin = null)
    {
        for (double t = 0; t < seconds - 1e-9 && !sim.Over; t += dt)
        {
            if (pin != null) { pin.X = 150; pin.KnockVelocity = 0; }
            sim.Step(dt, catX, held, held);
            seen?.AddRange(sim.Events);
            sim.ClearEvents();
        }
    }

    public static void Run(Action<bool, string> check)
    {
        GaugeChecks(check);
        ClickChecks(check);
        SweepChecks(check);
        StunChecks(check);
        DataChecks(check);
    }

    private static void GaugeChecks(Action<bool, string> check)
    {
        var sim = Field(0.05, count: 1);
        var target = sim.Slimes[0];
        target.X = 150;
        check(sim.Rage == 0 && !sim.RageFull && !sim.RageReady, "A battle starts with an empty rage bar");
        var seen = new List<BattleEvent>();
        for (int i = 0; i < 600 && sim.Rage == 0; i++) { sim.Step(dt, 100, false); seen.AddRange(sim.Events); sim.ClearEvents(); }
        check(Math.Abs(sim.Rage - 0.05) < 1e-9 && seen.Count(e => e.Kind is BattleEventKind.SlimeHit or BattleEventKind.ComboHit) == 1, $"One landed swing fills 5 % ({sim.Rage:0.###})");
        seen.Clear();
        Run(sim, 30, seen: seen, pin: target);
        int hits = seen.Count(e => e.Kind is BattleEventKind.SlimeHit or BattleEventKind.ComboHit);
        check(sim.RageFull && sim.Rage == 1 && hits >= 19 && seen.Count(e => e.Kind == BattleEventKind.RageFull) == 1, $"Twenty landed swings fill it, once ({hits} more swings)");
        check(!sim.Raging && seen.All(e => e.Kind != BattleEventKind.Raged) && sim.Rages == 0, "Full, it waits for the user: the cat never rages by itself");

        // Hurt: the HP lost × rageHurtGain (the dummy hits for 10).
        var hitter = new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1000, CatDamage = 0, RageHitGain = 0, RageHurtGain = 0.012,
            Weapons = new[] { new WeaponSpec { Id = "sword", Damage = 0, Knock = 0, StunChance = 0 } },
            Enemies = new[] { new EnemyType { Id = "biter", MaxHp = 1000, Speed = 0, Damage = 10, Range = 64, Windup = 0.3, AttackCooldown = 1, Recover = 0.2 } },
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "biter", Count = 1 } } } }
        };
        var bitten = new BattleSim(hitter, 0, 1600, seed: 3);
        for (int i = 0; i < 600 && bitten.Slimes.Count == 0; i++) bitten.Step(dt, 100, false);
        bitten.Slimes[0].X = 150;
        Run(bitten, 3);
        double lost = 1000 - bitten.CatHp;
        check(lost >= 10 && Math.Abs(bitten.Rage - lost * 0.012) < 1e-9, $"Losing HP fills it ({lost:0} HP → {bitten.Rage:0.###})");
    }

    private static void ClickChecks(Action<bool, string> check)
    {
        var sim = Field(1, count: 1);
        var target = sim.Slimes[0];
        check(!sim.StartRage(), "An empty bar does nothing");
        target.X = 150;
        Run(sim, 1.5);
        check(sim.RageFull && sim.RageReady, "One landed swing fills a bar of gain 1");
        sim.Paused = true;
        check(!sim.RageReady && !sim.StartRage(), "Not while paused");
        sim.Paused = false;
        sim.SetCatState(true, true);
        check(!sim.StartRage() && sim.RageFull, "Not while the cat is in the user's hands (the bar stays full)");
        sim.SetCatState(false, false);
        var seen = new List<BattleEvent>();
        check(sim.StartRage() && sim.Raging && sim.Rage == 0 && sim.RageRemaining == sim.Settings.RageSeconds && sim.Rages == 1 && sim.Events.Any(e => e.Kind == BattleEventKind.Raged),
            "Full and on the floor: the click starts the rage and empties the bar");
        check(!sim.StartRage() && sim.Rages == 1, "A second click does nothing");
        sim.ClearEvents();
        check(sim.RageLook == 0, "It swells in from nothing");
        Run(sim, 0.5, seen: seen);
        check(sim.RageLook == 1 && sim.Rage == 0, "Half a second in it shows in full; landed swings do not fill the bar meanwhile");
        double left = sim.RageRemaining;
        sim.Paused = true;
        Run(sim, 2);
        check(sim.RageRemaining == left, "Pause freezes it");
        sim.Paused = false;
        // Held: the clock runs on, like Cheer's.
        Run(sim, 1, held: true);
        check(Math.Abs(sim.RageRemaining - (left - 1)) < 0.02, $"Held, it keeps running ({sim.RageRemaining:0.00} s left)");
        Run(sim, sim.RageRemaining + 0.05, seen: seen);
        check(!sim.Raging && sim.RageLook == 0 && seen.Count(e => e.Kind == BattleEventKind.RageEnded) == 1,
            $"It ends after {sim.Settings.RageSeconds:0} s of battle time");
        Run(sim, 0.5, pin: target);
        check(sim.Rage > 0, "Afterwards the bar fills again");
    }

    private static void SweepChecks(Action<bool, string> check)
    {
        // Calm: only the target takes the swing.
        var calm = Field(0, count: 4);
        Slime a = calm.Slimes[0], b = calm.Slimes[1], c = calm.Slimes[2], d = calm.Slimes[3];
        a.X = 150; b.X = 175; c.X = 300; d.X = 40;
        Swing(calm);
        check(a.Hp < 1000 && b.Hp == 1000 && c.Hp == 1000 && d.Hp == 1000, "Calm, a swing hits its target only");

        var sim = Field(1, count: 4);
        a = sim.Slimes[0]; b = sim.Slimes[1]; c = sim.Slimes[2]; d = sim.Slimes[3];
        a.X = 150; b.X = 175; c.X = 300; d.X = 40;
        Swing(sim);
        check(sim.RageFull && sim.StartRage(), "Full after its first swing, the cat rages");
        double hpA = a.Hp, hpB = b.Hp, hpC = c.Hp, hpD = d.Hp;
        sim.ClearEvents();
        Swing(sim);
        check(a.Hp < hpA && b.Hp < hpB && Math.Abs(hpB - b.Hp - 10) < 1e-6 && sim.RageHits >= 1, $"Raging, the swing hits every enemy in reach, each the full blow ({hpB - b.Hp:0})");
        check(c.Hp == hpC && d.Hp == hpD, "An enemy beyond the reach or behind the cat is untouched");
        check(Math.Abs(a.KnockVelocity) >= sim.Settings.RageKnock * a.Type.PushFactor - 1e-6 && Math.Abs(b.KnockVelocity) > 0 && Math.Sign(b.KnockVelocity) == 1,
            "A raging hit knocks back even with a weapon that never does, away from the cat");
    }

    // Steps until the cat's next swing has landed (its hit moment passed).
    private static void Swing(BattleSim sim)
    {
        for (int i = 0; i < 240; i++)
        {
            sim.Step(dt, 100, false);
            bool hit = sim.Events.Any(e => e.Kind is BattleEventKind.SlimeHit or BattleEventKind.ComboHit);
            sim.ClearEvents();
            if (hit) return;
        }
    }

    private static void StunChecks(Action<bool, string> check)
    {
        var dasher = new EnemyType
        {
            Id = "runner", MaxHp = 1000, Speed = 70, Damage = 6, Range = 54, Windup = 0.7, AttackCooldown = 2.6, Recover = 0.4, Scale = 0.75,
            Dash = true, DashRange = 170, DashDistance = 230, DashSpeed = 420
        };
        var settings = new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0, MaxAlive = 1, CatDamage = 0, StunDurationSeconds = 1.5, StunImmunitySeconds = 3,
            RageHitGain = 0, RageHurtGain = 1, RageSeconds = 30,
            Enemies = new[] { dasher }, Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "runner", Count = 1 } } } }
        };
        var sim = new BattleSim(settings, 0, 1600, new Random(3));
        for (double t = 0; t < 15 && !sim.CatStunned; t += dt) sim.Step(dt, 800, false);
        check(sim.CatStunned && sim.RageFull && !sim.RageReady && !sim.StartRage(), "Stunned, the full bar waits (a stun under way is not cut short)");
        for (double t = 0; t < 5 && (sim.CatStunned || sim.StunImmunity > 0); t += dt) sim.Step(dt, 800, false);
        check(sim.StartRage(), "Once the stun is over the cat rages");
        double hp = sim.CatHp;
        for (double t = 0; t < 15 && sim.CatHp == hp; t += dt) sim.Step(dt, 800, false);
        Run(sim, 0.2, 800);
        check(sim.CatHp < hp && !sim.CatStunned && sim.StunsTaken == 1, "Raging, a dash still hurts but stuns no more");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        bool Rejects(string json)
        {
            try { BattleData.Parse(json); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(Rejects("{\"rageSeconds\":0}") && Rejects("{\"rageScale\":1.6}") && Rejects("{\"rageHitGain\":-0.1}") && Rejects("{\"rageKnockFactor\":0.5}"),
            "Out-of-range rage numbers are refused");
        check(!Rejects("{\"rageSeconds\":8,\"rageHitGain\":0,\"rageHurtGain\":0,\"rageScale\":1}"), "In-range ones are accepted (both gains 0: the bar never fills)");
        var d = new BattleSettings();
        var s = BattleData.Settings;
        check(s.RageSeconds == 6 && s.RageScale == 1.3 && s.RageSeconds == d.RageSeconds && s.RageHitGain == d.RageHitGain && s.RageHurtGain == d.RageHurtGain
              && s.RageScale == d.RageScale && s.RageKnock == d.RageKnock && s.RageKnockFactor == d.RageKnockFactor, "The shipped battle.json has the built-in rage numbers (6 s, ×1.3)");
    }
}
