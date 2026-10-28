using DesktopPet;

// 1.5 M08 (PetBattle.Weapons.cs), with stand-in enemies and the shipped yarn and glove: the yarn reaches
// further and every swing hits every enemy in reach in front (not behind, not beyond), and draws a
// support kind that keeps its distance in (not a plain one); the glove's chain is 5 hits, its combo
// tosses the enemy up, which lands dazed (never a boss); the data limits; the shipped numbers.
internal static class BattleNewWeaponTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Dummy(string id, bool support = false, bool boss = false) => new()
    {
        Id = id, MaxHp = 1000, Speed = 0, Damage = 0, Range = 64, Windup = 0.4, AttackCooldown = 1, Recover = 0.2, Support = support, Boss = boss
    };

    private static BattleSim Field(string weapon, params EnemyType[] kinds)
    {
        var shipped = BattleData.Settings;
        var settings = new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1000, MaxAlive = 8, Enemies = kinds, RageHitGain = 0,
            Weapons = new[] { shipped.Weapon(weapon) },
            Plan = new[] { new WaveSpec { Groups = kinds.Select(k => new WaveGroup { Enemy = k.Id, Count = 1 }).ToArray() } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 5, weapon: weapon);
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < kinds.Length; i++) sim.Step(dt, 100, false);
        foreach (var s in sim.Slimes) s.X = 1500;
        sim.ClearEvents();
        return sim;
    }

    // Steps until the cat's next swing has landed; returns whether it was a combo.
    private static bool? Swing(BattleSim sim, Action? each = null)
    {
        for (int i = 0; i < 240; i++)
        {
            each?.Invoke();
            sim.Step(dt, 100, false);
            var hit = sim.Events.FirstOrDefault(e => e.Kind is BattleEventKind.SlimeHit or BattleEventKind.ComboHit);
            sim.ClearEvents();
            if (hit.Kind is BattleEventKind.SlimeHit or BattleEventKind.ComboHit) return hit.Kind == BattleEventKind.ComboHit;
        }
        return null;
    }

    public static void Run(Action<bool, string> check)
    {
        YarnChecks(check);
        GloveChecks(check);
        DataChecks(check);
    }

    private static void YarnChecks(Action<bool, string> check)
    {
        var sim = Field("yarn", Dummy("a"), Dummy("b"), Dummy("c"), Dummy("d"), Dummy("e"));
        Slime a = sim.Slimes[0], b = sim.Slimes[1], c = sim.Slimes[2], d = sim.Slimes[3], e = sim.Slimes[4];
        a.X = 205; b.X = 230; c.X = 215; d.X = 300; e.X = 40;
        check(Swing(sim) == false, "The yarn reaches a slime 105 DIPs away without a step");
        double hit = sim.Weapon.Damage;
        check(sim.Slimes.Count(s => s.Hp < 1000) == 3 && new[] { a, b, c }.All(s => s.Hp == 1000 - hit), $"One lash hits every slime in reach in front, each the full {hit:0}");
        check(d.Hp == 1000 && e.Hp == 1000 && sim.SweepHits == 2, "Not one beyond its reach, nor one behind the cat");

        var pull = Field("yarn", Dummy("herb", support: true), Dummy("plain"));
        Slime herb = pull.Slimes[0], plain = pull.Slimes[1];
        herb.X = 205; plain.X = 210;
        Swing(pull);
        for (int i = 0; i < 90; i++) pull.Step(dt, 100, false);
        double gap = herb.X - 100;
        check(gap < 90 && gap > 40 && pull.Pulls >= 1, $"A support slime that keeps its distance is drawn in, stopping beside the cat ({gap:0} DIPs away)");
        check(plain.X == 210, "A plain slime is not pulled");
    }

    private static void GloveChecks(Action<bool, string> check)
    {
        var sim = Field("glove", Dummy("a"));
        var a = sim.Slimes[0];
        check(sim.ComboHits == 5 && Field("yarn", Dummy("x")).ComboHits == sim.Settings.ComboHits, "The glove's chain is 5 hits; the other weapons keep the battle's");
        void Pin() { if (!a.Handled) { a.X = 150; a.KnockVelocity = 0; } }
        var combos = new List<bool?>();
        for (int i = 0; i < 5; i++) combos.Add(Swing(sim, Pin));
        check(combos.Take(4).All(c => c == false) && combos[4] == true, $"Four plain punches, then the combo ({string.Join(",", combos)})");
        sim.Step(dt, 100, false); sim.ClearEvents();
        check(a.Tossed && a.Launched && a.Handled && a.Lift > 0 && sim.WeaponLaunches == 1 && sim.TargetId != a.Id, "The combo tosses the slime up; in the air it is out of the fight");
        double top = 0;
        for (int i = 0; i < 120 && a.Tossed; i++) { sim.Step(dt, 100, false); top = Math.Max(top, a.Lift); }
        check(!a.Tossed && !a.Launched && a.Lift == 0 && a.State == SlimeState.Stunned && top > 60, $"It lands dazed ({top:0} DIPs high)");
        check(!sim.GrabberBusy, "A launch is not the Grabber at work");

        var boss = Field("glove", Dummy("king", boss: true));
        var king = boss.Slimes[0];
        void PinKing() { king.X = 150; king.KnockVelocity = 0; }
        for (int i = 0; i < 5; i++) Swing(boss, PinKing);
        check(!king.Tossed && boss.WeaponLaunches == 0, "A boss is never launched");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        bool Rejects(string weapon)
        {
            try { BattleData.Parse("{\"weapons\":[{\"id\":\"w\",\"nameVi\":\"W\",\"nameEn\":\"W\",\"style\":\"sword\",\"damage\":10,\"range\":70,\"attackCooldown\":0.5,\"swingDuration\":0.3,\"hitTime\":0.1," + weapon + "}]}"); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(Rejects("\"pull\":-1") && Rejects("\"comboHits\":7") && Rejects("\"launch\":2000") && Rejects("\"launchStun\":4") && Rejects("\"style\":\"spear\""),
            "Out-of-range sweep, pull, combo and launch numbers (and an unknown drawing) are refused");
        check(!Rejects("\"sweep\":true,\"pull\":200,\"comboHits\":4,\"launch\":500,\"launchStun\":1,\"style\":\"glove\""), "In-range ones are accepted");
        var s = BattleData.Settings;
        check(s.FindWeapon("yarn") is { Style: "yarn", Sweep: true, Pull: > 0, StunChance: 0, Range: > 100 } yarn && yarn.Range >= s.Weapon("sword").Range * 1.4
              && s.FindWeapon("glove") is { Style: "glove", ComboHits: 5, Launch: > 0, LaunchStun: > 0 } glove && glove.AttackCooldown < s.Weapon("sword").AttackCooldown * 0.6,
            "The shipped yarn reaches ~1.5× the sword's range, sweeps, pulls, never stuns; the glove is much faster with a 5-hit combo that launches");
    }
}
