using DesktopPet;

internal static class KingFormationVisualChecks
{
    public static void Run(Action<bool, string> check)
    {
        var sim = new BattleSim(BattleData.Settings, 0, 1000, seed: 1);
        var boss = sim.AddSlime(500, BattleData.Settings.Enemy("king"));
        var first = sim.AddSlime(250, BattleData.Settings.Enemy("rock"));
        var second = sim.AddSlime(750, BattleData.Settings.Enemy("runner"));
        foreach (var s in new[] { boss, first, second }) s.Set(SlimeState.Moving);
        boss.Shielded = true; boss.ShieldTag = 1; boss.RallyCharge = 0.4;
        boss.RallyTargetId = first.Id;
        var view = new BattleFieldView(sim);
        var one = SkyVisualChecks.Render(view, 1000, (int)BattleFieldView.FieldHeight, "king-rally-one");
        boss.RallyTargetId2 = second.Id;
        var two = SkyVisualChecks.Render(view, 1000, (int)BattleFieldView.FieldHeight, "king-rally-two");
        check(sim.RallyTargets(boss).Count() == 2 && !one.SequenceEqual(two), "Both distinct rally targets receive visible crown beams and ground rings");
        boss.RallyCharge = 0;
        var stopped = SkyVisualChecks.Render(view, 1000, (int)BattleFieldView.FieldHeight, "king-rally-cancelled");
        check(!two.SequenceEqual(stopped), "Interrupting the crown removes both target warnings");
    }
}
