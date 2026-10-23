using DesktopPet;

// Desktop apps (2026-10-23): made-up helper apps dropping on the battle strip and the enemies' ad
// popup, in Adventure/Expedition/Infinity battles only. Simulation only (BattleSim), no WPF.
internal static class AppTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        void Run(BattleSim sim, double seconds, double catX = 800, bool held = false, bool lifted = false)
        {
            for (double t = 0; t < seconds; t += dt) sim.Step(dt, catX, held, lifted);
        }
        int Count(BattleSim sim, BattleEventKind kind) => sim.Events.Count(e => e.Kind == kind);
        // Rules where only `kind` drops (every app listed, the others at weight 0).
        AppsSpec Rules(BattleAppKind? kind = null, double dropChance = 0, double popupChance = 0, double popupMinThreat = 99) => new()
        {
            DropChance = dropChance, PopupChance = popupChance, PopupMinThreat = popupMinThreat,
            List = Enum.GetValues<BattleAppKind>().Select(k => new AppSpec { Id = k.ToString().ToLowerInvariant(), NameVi = k.ToString(), NameEn = k.ToString(), Weight = k == kind || (kind == null && k != BattleAppKind.Popup) ? 1 : 0 }).ToArray()
        };
        BattleSettings Data(AppsSpec apps, int[] waves, double slimeDamage = 0) => new()
        {
            FirstWaveDelay = 0, Waves = waves, Boss = false, SlimeMaxHp = 20, SlimeDamage = slimeDamage, CatDamage = 0, Apps = apps
        };
        var stage = new StageSpec { Id = "apps_test" };
        // Wave 1 (one slime) is cleared with the yarn bomb; wave 2 is out and settled.
        BattleSim Cleared(BattleSettings data, StageSpec? on, int seed = 5)
        {
            var sim = new BattleSim(data, 0, 1600, stage: on, seed: seed);
            Run(sim, 0.8);
            sim.Use(BattleSkill.Bomb, sim.Slimes[0].Id, 800);
            Run(sim, 1);
            return sim;
        }

        // A cleared wave drops one helper near the cat; free battles have none.
        var free = Cleared(Data(Rules(BattleAppKind.Tabs), new[] { 1, 1 }), null);
        check(free.WavesCleared == 1 && free.AppRules == null && free.Apps.Count == 0, "Free battles have no desktop apps");
        var sim = new BattleSim(Data(Rules(BattleAppKind.Tabs), new[] { 1, 1 }), 0, 1600, stage: stage, seed: 5);
        Run(sim, 0.8);
        sim.Use(BattleSkill.Bomb, sim.Slimes[0].Id, 800);
        int dropped = 0;
        for (double t = 0; t < 1; t += dt) { sim.Step(dt, 800, false); dropped += Count(sim, BattleEventKind.AppDropped); sim.ClearEvents(); }
        var app = sim.Apps.SingleOrDefault();
        check(sim.WavesCleared == 1 && dropped == 1 && app is { Kind: BattleAppKind.Tabs, Hostile: false } && Math.Abs(app.X - 800) is >= 90 and <= 160,
            $"A cleared wave drops one helper 90–160 DIPs from the cat ({app?.X})");
        double age = app!.Age;
        Run(sim, 30, held: true);
        check(sim.Apps.Count == 1 && app.Age == age, "An app waits while the cat is in the user's hands (its time stands still)");
        int expired = 0;
        for (double t = 0; t < app.Life + 0.5; t += dt) { sim.Step(dt, 800, false); expired += Count(sim, BattleEventKind.AppExpired); sim.ClearEvents(); }
        check(sim.Apps.Count == 0 && expired == 1, "Left alone, a helper fades after its life");
        var last = Cleared(Data(Rules(BattleAppKind.Tabs), new[] { 1 }), stage);
        check(last.Phase == BattlePhase.Won && last.Apps.Count == 0, "The last wave drops nothing (the battle is won)");
        var a1 = Cleared(Data(Rules(), new[] { 1, 1 }), stage, 11);
        var a2 = Cleared(Data(Rules(), new[] { 1, 1 }), stage, 11);
        check(a1.Apps.Single().Kind == a2.Apps.Single().Kind && a1.Apps[0].X == a2.Apps[0].X, "The same seed drops the same app at the same spot");

        // Tab tornado: hits enemies near the cat every pulse, not far ones; locked while the cat is held.
        sim = Cleared(Data(Rules(BattleAppKind.Tabs), new[] { 1, 2 }), stage);
        Run(sim, 6);
        var tabs = sim.Apps.Single();
        check(sim.Slimes.Count(s => s.Targetable) == 2, "Wave 2 is out");
        var near = sim.Slimes.Where(s => s.Targetable).ElementAt(0);
        var far = sim.Slimes.Where(s => s.Targetable).ElementAt(1);
        foreach (var s in new[] { near, far }) s.Hp = s.MaxHp = 1000;
        near.X = 830; far.X = 1400;
        sim.SetCatState(true, false);
        check(!sim.UseApp(tabs.Id, 800) && sim.Apps.Count == 1, "A helper cannot be used while the cat is in the user's hands (it stays)");
        sim.SetCatState(false, false);
        sim.ClearEvents();
        check(sim.UseApp(tabs.Id, 800) && sim.Apps.Count == 0 && sim.TabsRemaining > 0 && Count(sim, BattleEventKind.AppUsed) == 1, "Tab tornado starts at a click");
        Run(sim, 1.1);
        check(near.Hp == 970 && far.Hp == 1000, $"Tabs hit the slime near the cat 3 times in 1.1 s (10 each), not the far one ({near.Hp}, {far.Hp})");
        Run(sim, 1, held: true, lifted: true);
        check(near.Hp == 970, "The tabs do not hit while the cat is lifted");
        Run(sim, 6);
        check(sim.TabsRemaining == 0, "The tornado ends after its time");

        // Ping storm: every enemy takes damage; the stun skips an elite that ignores Push.
        sim = Cleared(Data(Rules(BattleAppKind.Ping), new[] { 1, 2 }), stage);
        Run(sim, 6);
        var plain = sim.Slimes.Where(s => s.Targetable).ElementAt(0);
        var firm = sim.Slimes.Where(s => s.Targetable).ElementAt(1);
        foreach (var s in new[] { plain, firm }) { s.Hp = s.MaxHp = 1000; s.Set(SlimeState.Moving); }
        firm.Elite = new EliteSpec { PushImmune = true };
        check(sim.UseApp(sim.Apps.Single().Id, 800) && plain.Hp == 990 && firm.Hp == 990 && plain.State == SlimeState.Stunned && firm.State != SlimeState.Stunned
              && sim.Effects.Count(e => e.Kind == BattleEffectKind.Bell) == 2,
            "Ping storm: a bell and 10 damage on every slime, a stun except on one that ignores Push");

        // End task: the enemy with the most HP is gone; an elite loses a quarter of its max HP.
        sim = Cleared(Data(Rules(BattleAppKind.EndTask), new[] { 1, 2 }), stage);
        Run(sim, 6);
        var big = sim.Slimes.Where(s => s.Targetable).ElementAt(0);
        var small = sim.Slimes.Where(s => s.Targetable).ElementAt(1);
        big.Hp = big.MaxHp = 300; small.Hp = small.MaxHp = 100;
        check(sim.UseApp(sim.Apps.Single().Id, 800) && big.State == SlimeState.Defeated && small.Hp == 100 && sim.Effects.Any(e => e.Kind == BattleEffectKind.EndTask),
            "End task ends the slime with the most HP and leaves the other");
        sim = Cleared(Data(Rules(BattleAppKind.EndTask), new[] { 1, 2 }), stage);
        Run(sim, 6);
        var elite = sim.Slimes.First(s => s.Targetable);
        foreach (var s in sim.Slimes.Where(s => s.Targetable)) s.Hp = s.MaxHp = 100;
        elite.Hp = elite.MaxHp = 400;
        elite.Elite = new EliteSpec();
        check(sim.UseApp(sim.Apps.Single().Id, 800) && elite.Hp == 300, $"End task on an elite takes 25 % of its max HP ({elite.Hp})");

        // Ctrl+Z: nothing lost, the icon stays; after a hit, the HP lost in the last 4 s comes back.
        sim = Cleared(Data(Rules(BattleAppKind.Undo), new[] { 1, 2 }, slimeDamage: 10), stage);
        var undo = sim.Apps.Single();
        check(sim.CatHp == sim.CatMaxHp && !sim.UseApp(undo.Id, 800) && sim.Apps.Count == 1, "Ctrl+Z with nothing to undo does nothing (the icon stays)");
        Run(sim, 4.3);
        foreach (var s in sim.Slimes.Where(s => s.Targetable)) s.X = 840;
        for (double t = 0; t < 4 && sim.CatHp == sim.CatMaxHp; t += dt) sim.Step(dt, 800, false);
        double hurt = sim.CatHp;
        check(hurt < sim.CatMaxHp && sim.UseApp(undo.Id, 800) && sim.CatHp == sim.CatMaxHp, $"Ctrl+Z gives back the HP just lost ({hurt} → {sim.CatHp})");

        // The ad popup: only from its threat; a click on × closes it (even with the cat in hand);
        // left alone it lets a slime out; a cleared wave closes it.
        BattleSim Popup(double threat, int[] waves)
        {
            var popupStage = new StageSpec { Id = "apps_popup", Threat = threat };
            var s = new BattleSim(Data(Rules(dropChance: 0, popupChance: 1, popupMinThreat: 1.5), waves), 0, 1600, stage: popupStage, seed: 9);
            for (double t = 0; t < 8 && !s.Apps.Any(a => a.Hostile); t += dt) s.Step(dt, 800, false);
            return s;
        }
        check(!Popup(1, new[] { 2 }).Apps.Any(), "No popup below the rules' threat");
        sim = Popup(2, new[] { 2 });
        var popup = sim.Apps.SingleOrDefault(a => a.Hostile);
        check(popup != null && sim.Slimes.Any(s => s.Targetable && Math.Abs(s.X - popup.X) < BattleSim.PopupWidth / 2), "From its threat a popup opens over one of the slimes");
        sim.SetCatState(true, true);
        sim.ClearEvents();
        check(popup != null && sim.UseApp(popup.Id, 800) && sim.Apps.Count == 0 && sim.PopupsClosed == 1 && Count(sim, BattleEventKind.PopupClosed) == 1,
            "A click on the popup's × closes it, even with the cat in the user's hands");
        sim = Popup(2, new[] { 2 });
        int alive = sim.Alive, spawned = 0;
        for (double t = 0; t < 9; t += dt) { sim.Step(dt, 800, false); spawned += Count(sim, BattleEventKind.PopupSpawned); sim.ClearEvents(); }
        check(spawned == 1 && sim.PopupsMissed == 1 && sim.Alive == alive + 1 && !sim.Apps.Any(a => a.Hostile), $"Left alone, the popup lets a slime out ({alive} → {sim.Alive})");
        sim = Popup(2, new[] { 1, 1 });
        var gathered = sim.Slimes.Where(s => s.Targetable).ToList();
        foreach (var s in gathered) { s.X = gathered[0].X; s.Hp = 1; }
        sim.Use(BattleSkill.Bomb, gathered[0].Id, 800);
        for (double t = 0; t < 1.5 && sim.WavesCleared == 0; t += dt) sim.Step(dt, 800, false);
        check(sim.WavesCleared == 1 && !sim.Apps.Any(a => a.Hostile), "A cleared wave closes an open popup");

        // Paused or over: no app is used.
        sim = Cleared(Data(Rules(BattleAppKind.Tabs), new[] { 1, 1 }), stage);
        sim.Paused = true;
        check(!sim.UseApp(sim.Apps.Single().Id, 800), "Paused: an app cannot be used");

        // The shipped data: every app named in both languages, numbers in range; a broken "apps" is refused.
        var shipped = BattleData.Settings.Apps;
        check(shipped != null && shipped.AppList.Count == 5 && shipped.AppList.All(a => a.NameVi.Length > 0 && a.NameEn.Length > 0 && a.DescVi.Length > 0 && a.DescEn.Length > 0)
              && shipped.AppList.Single(a => a.Id == "popup").Weight == 0,
            "battle.json \"apps\": all five apps with names and descriptions in both languages; the popup never drops");
        string dir = Path.Combine(Path.GetTempPath(), "pet-apps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "battle.json");
            string? Error(string apps) { File.WriteAllText(file, "{ \"schemaVersion\": 1, \"apps\": " + apps + " }"); return BattleData.Load(file).Error; }
            check(Error("{ \"life\": 12 }") == null, "A user file can change one app number");
            check(Error("{ \"popupSpawn\": \"dragon\" }")?.Contains("popupSpawn") == true, "An unknown popupSpawn enemy is refused");
            check(Error("{ \"list\": [ { \"id\": \"tabs\", \"nameVi\": \"a\", \"nameEn\": \"a\", \"descVi\": \"\", \"descEn\": \"\" } ] }")?.Contains("every app") == true,
                "A list missing apps is refused");
            check(Error("{ \"dropChance\": 2 }")?.Contains("dropChance") == true, "dropChance must be 0–1");
        }
        finally { Directory.Delete(dir, true); }
    }
}
