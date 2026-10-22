using DesktopPet;

// The battle hub's Bag reads the cat's numbers from a BattleSim made with the build and relics
// (SwingDamage, SwingsPerSecond, Knock, HurtFactor, CatMaxHp, MoveFactor): the battle's own rules,
// with the built-in data.
internal static class InventoryStatTests
{
    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var sword = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Sword, seed: 0);
        var w = sword.Weapon;
        check(Near(sword.SwingDamage, w.Damage) && Near(sword.SwingsPerSecond, 1 / Math.Max(w.AttackCooldown, w.SwingDuration)) && Near(sword.Knock, w.Knock)
            && Near(sword.HurtFactor, 1) && Near(sword.MoveFactor, 1) && Near(sword.CatMaxHp, data.CatMaxHp),
            "Without a build the Bag's numbers are the weapon's own");
        var hammer = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Hammer, seed: 0);
        check(Near(hammer.SwingDamage, hammer.Weapon.Damage) && Near(hammer.Knock, hammer.Weapon.Knock) && hammer.SwingsPerSecond < sword.SwingsPerSecond,
            "The hammer's numbers are its own (harder, slower)");

        var build = new Dictionary<string, int> { ["sharp"] = 2, ["fast_paws"] = 1, ["heavy_push"] = 1, ["thick_fur"] = 1, ["big_heart"] = 1 };
        var built = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Sword, build: build, seed: 0);
        check(Near(built.SwingDamage, w.Damage * 1.4) && Near(built.SwingsPerSecond, 1 / Math.Max(w.AttackCooldown * 0.88, w.SwingDuration))
            && Near(built.Knock, w.Knock + 80) && Near(built.HurtFactor, 0.85) && Near(built.CatMaxHp, data.CatMaxHp + 20),
            $"A build changes the numbers as its cards say (damage {built.SwingDamage}, swings {built.SwingsPerSecond:0.###}/s, knock {built.Knock}, taken {built.HurtFactor})");

        var relics = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Sword, build: build, seed: 0, relics: new[] { "reckless_charm", "light_feather" });
        check(Near(relics.HurtFactor, 0.85 * 1.1) && Near(relics.MoveFactor, 1.15) && Near(relics.CatMaxHp, data.CatMaxHp + 20 - 15)
            && Near(relics.SwingsPerSecond, 1 / Math.Max(w.AttackCooldown * (1 - 0.12 - 0.1), w.SwingDuration)) && Near(relics.SwingDamage, built.SwingDamage),
            "Relics add their prices and gains (Reckless: more damage taken always, its bonus only in a fight; Light feather: faster, less max HP)");

        // The Expedition run's log (the Bag's Relics tab): one line per won battle, left battle and choice.
        var x = new Expedition(data, new ExpeditionProgress());
        var run = x.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 11, "log-run");
        if (x.NeedsRoll) x.Roll();
        int fight = Enumerable.Range(0, run.Map[0].Length).First(lane => x.Kind(0, lane) == ExpeditionSpec.Fight);
        double hp = run.Hp;
        x.Enter(fight);
        x.Retreat();
        check(run.Log.Count == 1 && run.Log[0].Kind == ExpeditionLogEntry.Retreat && run.Log[0].Step == 0 && Near(run.Log[0].Hp, run.Hp - hp),
            "A left battle is logged with its HP cost");
        hp = run.Hp;
        x.Enter(fight);
        x.Finish(true, hp - 10, new Dictionary<string, int>(), 20, 3);
        check(run.Log.Count == 2 && run.Log[1].Kind == ExpeditionSpec.Fight && run.Log[1].Step == 0 && Near(run.Log[1].Hp, run.Hp - hp), "A won battle is logged with what HP did");
        check(x.Choose(Expedition.Free) is { Done: true } done && run.Log.Count == 3 && run.Log[2] is { Kind: ExpeditionChoice.CapsuleKind, Option: Expedition.Free, Step: 0 } entry
            && entry.Prize == done.Prize && (done.Card == null || entry.Card == done.Card) && x.Choose(Expedition.Free) is { Done: false } && run.Log.Count == 3,
            "A capsule is logged once (a second click logs nothing) with its prize");
        string file = Path.Combine(Path.GetTempPath(), "pet-log-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            x.Progress.Save(file);
            var back = ExpeditionProgress.Load(file).Active!;
            check(back.Log.Count == 3 && back.Log[2].Prize == run.Log[2].Prize && back.Log[0].Kind == ExpeditionLogEntry.Retreat, "The log is saved and read back with the run");
            File.WriteAllText(file, File.ReadAllText(file).Replace("\"Log\"", "\"OldLog\""));
            check(ExpeditionProgress.Load(file).Active is { Log.Count: 0 }, "A file without a log reads as an empty log");
        }
        finally { File.Delete(file); File.Delete(file + ".bak"); }
        for (int i = 0; i < ExpeditionRun.LogKept + 5; i++) { x.Enter(Enumerable.Range(0, run.Map[run.Step].Length).FirstOrDefault(lane => x.Node(lane) != null)); if (x.Run == null) break; x.Retreat(); run.Hp = 100; }
        check(run.Log.Count <= ExpeditionRun.LogKept, $"The log keeps at most {ExpeditionRun.LogKept} lines ({run.Log.Count})");
    }
}
