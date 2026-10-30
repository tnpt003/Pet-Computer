using DesktopPet;

internal static class BattleFeedbackFixTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        var data = BattleData.Settings;
        var sim = new BattleSim(data, 0, 1600, seed: 19);
        var giant = sim.AddSlime(700, data.Enemy("giant"));
        giant.Set(SlimeState.Moving);
        double hp = giant.Hp;
        for (int i = 0; i < 3; i++) sim.Damage(giant, 10, DamageSource.Weapon);
        check(giant.Hp == hp && giant.GiantShields == 0, "Giant: its first three direct hits spend shields without HP loss");
        sim.Damage(giant, 10, DamageSource.Weapon);
        check(giant.Hp == hp - 10, "Giant: the fourth direct hit hurts it");
        sim.Paused = true;
        sim.Step(1, 100, true, true);
        check(giant.GiantShieldClock == 10, "Giant: Pause freezes shield recovery");
        sim.Paused = false;
        for (int i = 0; i < 601; i++) sim.Step(dt, 100, true, true);
        check(giant.GiantShields == 3, "Giant: three shields return after ten battle seconds");
        sim.Damage(giant, 2, DamageSource.Poison);
        check(giant.GiantShields == 3 && giant.Hp == hp - 12, "Giant: poison hurts without spending shields");

        sim = new BattleSim(data, 0, 1600, seed: 21);
        var spit = sim.AddSlime(1400, data.Enemy("spitter"));
        spit.Set(SlimeState.Moving); spit.SpitCooldown = 0;
        sim.Step(dt, 100, false);
        check(spit.Spitting, "Spitter aims across the map beyond its former range");
        check(BattleSim.SpitFlight(spit.Type.Spit!, 1200) > BattleSim.SpitFlight(spit.Type.Spit!, 300) * 3.9,
            "Spitter flight grows with distance instead of using the same arrival time");

        var stage = new StageSpec { Id = "loadout", NewAppsFrom = 1 };
        var selected = new[] { BattleAppKind.Ping, BattleAppKind.Screenshot };
        sim = new BattleSim(data, 0, 1600, stage: stage, appLoadout: selected);
        sim.SetAppGrid(12, 3);
        check(sim.Desktop.Select(a => a.Kind).SequenceEqual(selected), "Battle apps use exactly the chosen icons");
        sim = new BattleSim(data, 0, 1600, stage: stage, appLoadout: Array.Empty<BattleAppKind>());
        sim.SetAppGrid(12, 3);
        check(sim.Desktop.Count == 0, "An empty app selection stays empty");
        sim = new BattleSim(data, 0, 1600, stage: stage, appLoadout: BattleSim.DefaultApps.Concat(new[] { BattleAppKind.Screenshot }));
        sim.SetAppGrid(12, 3);
        check(sim.Desktop.Count == 5, "Even an oversized supplied app loadout is capped at five");

        var anchorType = new EnemyType { Id = "anchor", Speed = 0, Damage = 0, MaxHp = 1000, Range = 1 };
        var chainType = data.Enemy("chain");
        var retreatData = new BattleSettings { FirstWaveDelay = 1000, Enemies = new[] { anchorType, chainType } };
        sim = new BattleSim(retreatData, 0, 1600, seed: 1);
        var anchor = sim.AddSlime(500, anchorType); anchor.Set(SlimeState.Moving);
        var chain = sim.AddSlime(490, chainType); chain.Set(SlimeState.Moving);
        for (int i = 0; i < 240; i++) sim.Step(dt, 100, true, true);
        check(chain.X > anchor.X && Math.Abs(anchor.X - 500) < 0.01,
            "A chain slime retreats through its ally instead of pushing that ally backward");
    }
}
