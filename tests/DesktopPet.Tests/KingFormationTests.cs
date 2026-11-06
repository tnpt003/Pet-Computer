using DesktopPet;

internal static class KingFormationTests
{
    const double Dt = 1.0 / 60;
    private static (BattleSim Sim, Slime Boss) Field(string kind, int seed)
    {
        var data = BattleData.Settings;
        var settings = new BattleSettings
        {
            Weapons = new[] { data.Weapon("sword") with { Damage = 0, StunChance = 0 } },
            Enemies = data.EnemyTypes.ToArray(), Formations = data.FormationList.ToArray(),
            CatMaxHp = 10000, RageHitGain = 0, RageHurtGain = 0,
            FirstWaveDelay = 0, Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = kind, Count = 1 } } } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: seed);
        for (int i = 0; i < 180 && sim.Boss?.Targetable != true; i++) sim.Step(Dt, 100, false);
        var boss = sim.Boss!;
        boss.X = 1000;
        sim.Damage(boss, boss.MaxHp * 0.26, DamageSource.Weapon);
        return (sim, boss);
    }

    public static void Run(Action<bool, string> check)
    {
        var layouts = new HashSet<string>();
        foreach (string kind in new[] { "king", "mutant_king", "king_c1" })
        for (int seed = 1; seed <= 8; seed++)
        {
            var (sim, boss) = Field(kind, seed);
            int banners = 0, firstBatch = 0;
            for (int i = 0; i < 240; i++)
            {
                int before = sim.Slimes.Count(s => s.SummonedBy == boss.Id);
                sim.Step(Dt, 100, false);
                banners += sim.Events.Count(e => e.Kind == BattleEventKind.FormationArrived);
                sim.ClearEvents();
                int after = sim.Slimes.Count(s => s.SummonedBy == boss.Id);
                if (after > before) firstBatch = after - before;
            }
            var helpers = sim.Slimes.Where(s => s.SummonedBy == boss.Id && s.Targetable).ToArray();
            bool advanced = kind != "king_c1";
            check(advanced ? firstBatch is 9 or 10 && helpers.Length == firstBatch && banners == 3 : firstBatch == 3 && banners == 0,
                $"{kind} seed{seed}: complete announced formation bundle arrives in one batch; chapter one keeps its original three helpers");
            if (advanced) layouts.Add(string.Join(",", helpers.Select(s => s.Type.Id).Order()));
            foreach (var helper in helpers) { helper.StunRemaining = 99; helper.Set(SlimeState.Stunned); }
            boss.RallyCooldown = 0;
            sim.Step(Dt, 100, false);
            check(sim.RallyTargets(boss).Select(s => s.Id).Distinct().Count() == (advanced ? 2 : 1), "Rally warns exactly the distinct targets it will buff");
            double warning = boss.RallyCharge;
            sim.Paused = true; sim.Step(0.1, 100, false); sim.Paused = false;
            check(boss.RallyCharge == warning, "Pause freezes both rally targets and their charge");
            for (int i = 0; i < 55; i++) sim.Step(Dt, 100, false);
            check(helpers.Count(s => s.Rallied) == (advanced ? 2 : 1), "Rally promotes two later-king helpers and one chapter-one helper");
            check(helpers.Where(s => s.Rallied).All(s => s.Elite != null), "Every rally target receives the normal elite upgrade");
        }
        check(layouts.Count > 1, "Seeded later kings use varied compatible formation bundles");
        var (edgeSim, edgeBoss) = Field("mutant_king", 40);
        for (int i = 0; i < 240; i++) edgeSim.Step(Dt, 100, false);
        var edgeHelpers = edgeSim.Slimes.Where(s => s.SummonedBy == edgeBoss.Id && s.Targetable).ToArray();
        foreach (var helper in edgeHelpers) { helper.StunRemaining = 99; helper.Set(SlimeState.Stunned); }
        edgeBoss.RallyCooldown = 0;
        edgeSim.Step(Dt, 100, false);
        var doomed = edgeSim.RallyTargets(edgeBoss).First();
        edgeSim.Damage(doomed, doomed.MaxHp * 100, DamageSource.Poison);
        for (int i = 0; i < 55; i++) edgeSim.Step(Dt, 100, false);
        check(edgeHelpers.Count(s => s.Rallied) == 2 && !doomed.Rallied, "A lost warned helper is replaced without buffing the same survivor twice");
        edgeBoss.RallyCooldown = 0;
        edgeSim.Step(Dt, 100, false);
        edgeBoss.StunRemaining = 1; edgeBoss.Set(SlimeState.Stunned);
        edgeSim.Step(Dt, 100, false);
        check(edgeBoss.RallyCharge == 0 && edgeBoss.RallyTargetId == 0 && edgeBoss.RallyTargetId2 == 0,
            "Interrupting the King cancels both pending buffs");
    }
}
