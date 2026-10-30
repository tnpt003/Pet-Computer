using DesktopPet;

// 1.5 M29a (docs/UPDATE-1.5.md F1): the Custom Fight: a preset built into a battle, the Monster Book's
// locks, the link and cursor limits, the chosen apps in the simulation, the presets file and the book's
// apps. Simulation and data only, no WPF.
internal static class CustomFightTests
{
    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        const double dt = 1.0 / 60;
        MonsterBook Book(params string[] met)
        {
            var book = new MonsterBook();
            book.Add(data, met, new Dictionary<string, int>(), Array.Empty<string>(), out _);
            return book;
        }
        var all = Book(data.EnemyTypes.Select(e => e.Id).ToArray());
        all.AddApps(CustomFight.IconOrder.Select(BattleSim.AppId).Concat(MalwareCue.Kinds), out _);

        // The pickable kinds: every kind with a page; the King's page spawns the chapter 1 King (shield phases).
        var kinds = CustomFights.Kinds(data).Select(e => e.Id).ToList();
        var bosses = CustomFights.Bosses(data).Select(e => e.Id).ToList();
        check(kinds.Count == 24 && !kinds.Contains("splitling") && !kinds.Contains("zipshard") && !kinds.Contains("king_c1"),
            $"Slimes to choose: the kinds with a page ({kinds.Count}: {string.Join(",", kinds)})");
        check(bosses.OrderBy(b => b).SequenceEqual(new[] { "king", "mother", "mutant_king", "zip" }), $"Bosses to choose: the four boss pages ({string.Join(",", bosses)})");
        check(CustomFights.SpawnOf(data, "king") == "king_c1" && CustomFights.SpawnOf(data, "slime") == "slime", "The King's page fights as the chapter 1 King");

        // The starter (a copy of Default): the old free battle's slimes and King, built into waves.
        var fight = CustomFight.Starter("a1", "Set 1");
        var stage = CustomFights.Build(data, fight, all)!;
        check(stage is { Custom: true, Id: "custom:a1", Upgrades: false, EliteChance: 0, MaxAlive: 3, CustomApps: null, Malware: false } && stage.Threat == 1,
            "A built fight is a Custom stage: no cards, no elites, 3 on the floor, no apps");
        check(stage.Waves.Length == 4 && stage.Waves.Take(3).Select(w => w.Groups.Single().Count).SequenceEqual(new[] { 2, 3, 4 })
              && stage.Waves[0].Groups[0].Pick!.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}").SequenceEqual(new[] { "rock:1", "runner:2", "slime:2" })
              && stage.Waves[3].Groups.Single().Enemy == "king_c1",
            $"Waves: 2, 3, 4 drawn from the chosen weights, then the King alone ({string.Join(" | ", stage.Waves.Select(w => string.Join("+", w.Groups.Select(g => g.Pick != null ? "pick×" + g.Count : g.Enemy))))})");
        fight.Cards = true; fight.Elites = 25; fight.Threat = 1.4; fight.MaxAlive = 6; fight.Waves = 2; fight.PerWave = 5; fight.Boss = null;
        stage = CustomFights.Build(data, fight, all)!;
        check(stage.Upgrades && stage.EliteChance == 0.25 && Math.Abs(stage.Threat - 1.4) < 1e-9 && stage.MaxAlive == 6
              && stage.Waves.Select(w => w.Groups.Single().Count).SequenceEqual(new[] { 5, 6 }), "Cards, elites, strength, floor and waves go into the stage");
        check(CustomFights.Build(data, new CustomFight { Id = "e", Name = "E" }, all) == null, "Nothing chosen: no battle");
        var bossOnly = CustomFights.Build(data, new CustomFight { Id = "b", Name = "B", Boss = "zip" }, all)!;
        check(bossOnly.Waves.Length == 1 && bossOnly.Waves[0].Groups[0].Enemy == "zip", "A boss alone: one wave with it");

        // Numbers stay within their limits.
        var wild = new CustomFight { Id = "w", Name = "  ", Waves = 99, PerWave = 0, MaxAlive = 20, Elites = 33, Threat = 9, Enemies = new() { ["slime"] = 7, ["runner"] = 0 },
            Icons = new() { "tabs", "tabs", "nope" }, Malware = new() { ["ads"] = 5, ["boom"] = 2 } };
        wild.Normalize();
        check(wild is { Waves: 10, PerWave: 2, MaxAlive: 8, Elites: 25, Name: "?" } && wild.Threat == 2 && wild.Enemies.Count == 1 && wild.Enemies["slime"] == 3
              && wild.Icons.SequenceEqual(new[] { "tabs" }) && wild.Malware.Count == 1 && wild.Malware["ads"] == 3, "Normalize clamps every number and drops unknown ids");

        // Only what the book has met: locked kinds, bosses and apps are left out.
        var few = Book("slime", "runner");
        check(CustomFights.CanAdd(data, few, fight, "rock") == CustomFightBlock.Locked && CustomFights.CanAdd(data, few, fight, "runner") == CustomFightBlock.None,
            "A kind not met cannot be added");
        var locked = CustomFights.Build(data, new CustomFight { Id = "l", Name = "L", Enemies = new() { ["slime"] = 1, ["rock"] = 3 }, Boss = "king", Icons = new() { "tabs" }, Malware = new() { ["ads"] = 2 } }, few)!;
        check(locked.Waves.Length == 3 && locked.Waves[0].Groups[0].Pick!.Keys.SequenceEqual(new[] { "slime" }) && locked.CustomApps == null,
            "Building leaves out the rock, the King and the apps the book has not met");
        check(CustomFights.Build(data, new CustomFight { Id = "x", Name = "X", Enemies = new() { ["rock"] = 1 } }, few) == null, "Only locked kinds: no battle");

        // At most two kinds of link, two cursor tricks (bosses count with what they bring).
        var linked = new CustomFight { Id = "k", Name = "K", Enemies = new() { ["chain"] = 1, ["drummer"] = 1 } };
        check(CustomFights.CanAdd(data, all, linked, "fuser") == CustomFightBlock.Links && CustomFights.CanAdd(data, all, linked, "slime") == CustomFightBlock.None,
            "Chains and drums: a third kind of link (merging) is refused, a plain slime is not");
        check(CustomFights.CanAdd(data, all, new CustomFight { Id = "m", Name = "M", Enemies = new() { ["fuser"] = 1 } }, "mutant_king", boss: true) == CustomFightBlock.Links,
            "The Mutant King's chains and drums with merging slimes: refused");
        var tricks = new CustomFight { Id = "t", Name = "T", Enemies = new() { ["phantom"] = 1, ["bubble"] = 1 } };
        check(CustomFights.CanAdd(data, all, tricks, "mirage") == CustomFightBlock.Mouse, "Two cursor tricks: a third is refused");
        tricks.Enemies["mirage"] = 1;
        check(CustomFights.Build(data, tricks, all) == null, "A preset past a limit (hand-edited) builds no battle");

        // The chosen apps in the battle: the icons it was built with, malware at any threat, as chosen.
        AppsSpec Rules() => new() { StartCharge = 0.2, MalwareEveryMin = 3, MalwareEveryMax = 3, InfectChance = 0, List = AppsSpec.BuiltIn };
        BattleSettings Data() => new() { FirstWaveDelay = 0, Waves = new[] { 3, 3, 3 }, Boss = false, SlimeMaxHp = 1000, SlimeDamage = 0, SlimeSpeed = 40, CatDamage = 0, CatMaxHp = 1000, Apps = Rules() };
        int[] Levels(params (string Kind, int Level)[] chosen) => MalwareCue.Kinds.Select(k => chosen.FirstOrDefault(c => c.Kind == k).Level).ToArray();
        StageSpec Custom(BattleAppKind[] icons, int[] malware) => new() { Id = "custom:t", Custom = true, Malware = malware.Any(l => l > 0), CustomApps = new() { Icons = icons, Malware = malware } };
        BattleSim Fight(StageSpec s, double seconds)
        {
            var sim = new BattleSim(Data(), 0, 1600, stage: s, seed: 4, skills: BattleSkills.Default);
            sim.SetAppGrid(12, 3);
            for (double t = 0; t < seconds; t += dt) sim.Step(dt, 1500, false);
            return sim;
        }
        var two = Fight(Custom(new[] { BattleAppKind.Tabs, BattleAppKind.Screenshot }, Levels()), 12);
        check(two.Desktop.Select(a => a.Kind).OrderBy(k => k).SequenceEqual(new[] { BattleAppKind.Tabs, BattleAppKind.Screenshot }) && !two.NewApps,
            $"Only the chosen icons, the Screenshot among them ({string.Join(",", two.Desktop.Select(a => a.Kind))})");
        check(two.MetApps.OrderBy(a => a).SequenceEqual(new[] { "screenshot", "tabs" }), $"No malware chosen: none comes ({string.Join(",", two.MetApps)})");
        var loading = Fight(Custom(new[] { BattleAppKind.Tabs, BattleAppKind.Ping }, Levels(("loading", 3))), 25);
        check(loading.Threat < data.Apps!.MalwareMinThreat && loading.MetApps.Contains("loading") && !loading.MetApps.Contains("ads") && !loading.MetApps.Contains("update"),
            $"Only the chosen malware comes, at threat 1 too ({string.Join(",", loading.MetApps)})");
        var infected = Fight(Custom(new[] { BattleAppKind.Tabs, BattleAppKind.Ping }, Levels(("infect", 3))), 15);
        check(infected.MetApps.Contains("infect") && !infected.MetApps.Contains("loading"), "Icon viruses (Many): a slime shoots one in the first wave, at threat 1");
        check(new CustomAppRules { Malware = Levels(("ads", 1)) }.WaitScale == 1.5 && new CustomAppRules { Malware = Levels(("ads", 3), ("update", 1)) }.WaitScale == 0.6
              && new CustomAppRules { Malware = Levels(("infect", 2)) }.InfectChance == 0.75, "Few waits longer, Many shorter; Some infects 3 waves in 4");
        // Elsewhere nothing changed: a stage without CustomApps still keeps malware below its threat.
        var plain = new BattleSim(Data(), 0, 1600, stage: new StageSpec { Id = "s" }, seed: 4, skills: BattleSkills.Default);
        plain.SetAppGrid(12, 3);
        for (double t = 0; t < 25; t += dt) plain.Step(dt, 1500, false);
        check(plain.Desktop.Count == 5 && plain.MetApps.All(a => !MalwareCue.Kinds.Contains(a)), "An Adventure-like stage: five icons, no malware under its threat");

        // The Monster Book's apps: added once, seeded once from stages already won.
        var book = new MonsterBook();
        book.AddApps(new[] { "tabs", "ads", "tabs" }, out bool changed);
        check(changed && book.Apps.Count == 2 && book.KnowsApp("ads"), "A battle's apps go in the book once");
        book.AddApps(new[] { "tabs" }, out changed);
        check(!changed, "Nothing new: no save");
        var progress = new AdventureProgress();
        progress.Completed.AddRange(data.Campaign!.Chapters[0].Stages.Select(s => s.Id));
        var seeded = new MonsterBook();
        check(seeded.SeedApps(data.Campaign, data, progress) && new[] { "tabs", "ping", "endtask", "undo", "trash" }.All(seeded.KnowsApp) && !seeded.KnowsApp("screenshot")
              && !seeded.SeedApps(data.Campaign, data, progress), $"Chapter 1 won: the five first icons and its malware, once ({string.Join(",", seeded.Apps)})");
        progress.Completed.AddRange(data.Campaign.Chapters[1].Stages.Select(s => s.Id));
        var both = new MonsterBook();
        both.SeedApps(data.Campaign, data, progress);
        check(new[] { "screenshot", "captcha", "battery", "cursor" }.All(both.KnowsApp), "Chapter 2 won: the Screenshot and the three newer malware");
        var fresh = new MonsterBook();
        check(fresh.SeedApps(data.Campaign, data, new AdventureProgress()) && fresh.Apps.Count == 0 && fresh.AppsSeeded, "Nothing won: nothing seeded (and not tried again)");

        // The presets file: saved atomically, read back, a broken file from its .bak, a newer one never overwritten.
        string dir = Path.Combine(Path.GetTempPath(), "pet-custom-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "custom-fights.json");
            var store = new CustomFightBook();
            var a = store.Add(null, n => "Set " + n)!;
            var b = store.Add(a, n => "Set " + n)!;
            check(store.Presets.Count == 2 && b.Name == "Set 2" && store.Selected == b.Id && b.Enemies.SequenceEqual(a.Enemies) && b.Id != a.Id, "+ New set copies the chosen one and is chosen");
            check(store.Save(path) && store.Save(path) && File.Exists(path + ".bak"), "Saved with a .bak of the last good file");
            var back = CustomFightBook.Load(path);
            check(back.Presets.Select(p => p.Id).SequenceEqual(new[] { a.Id, b.Id }) && back.Selected == b.Id && back.Chosen!.Boss == "king", "Read back as saved");
            File.WriteAllText(path, "{ broken");
            check(CustomFightBook.Load(path).Presets.Count == 2, "A broken file is read from its .bak");
            File.WriteAllText(path, "{ \"Version\": 99, \"Selected\": \"zz\", \"Presets\": [ { \"Id\": \"p\", \"Name\": \"P\", \"Waves\": 50 } ] }");
            var newer = CustomFightBook.Load(path);
            check(newer.ReadOnly && !newer.Save(path) && File.ReadAllText(path).Contains("\"Version\": 99") && newer.Selected == null && newer.Presets[0].Waves == 10,
                "A newer file: read (clamped), never overwritten; an unknown choice falls back to Default");
            File.WriteAllText(path, "{ \"Presets\": [ { \"Id\": \"default\", \"Name\": \"D\" }, { \"Id\": \"q\", \"Name\": \"Q\" }, { \"Id\": \"q\", \"Name\": \"Q2\" } ] }");
            check(CustomFightBook.Load(path).Presets.Select(p => p.Name).SequenceEqual(new[] { "Q" }), "Default's id and a repeated id are not presets");
            for (int i = 0; i < 10; i++) store.Add(null, n => "Set " + n);
            check(store.Presets.Count == CustomFightBook.MaxPresets && store.Full && store.Add(null, n => "x") == null, $"At most {CustomFightBook.MaxPresets} presets");
            string middle = store.Presets[2].Id, next = store.Presets[3].Id;
            store.Selected = middle;
            check(store.Delete(middle) && store.Selected == next && !store.Delete("none"), "Deleting the chosen preset chooses the next one");
            while (store.Presets.Count > 0) store.Delete(store.Presets[0].Id);
            check(store.Selected == null, "Deleting every preset leaves Default chosen");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }

        // The data check still finds three kinds of link in one battle (shared Reach).
        var (links, _) = BattleData.Reach(data, new[] { "chain", "drummer", "fuser" });
        check(links.SetEquals(new[] { "tether", "rhythm", "merge" }), $"Reach: the links three kinds bring ({string.Join(",", links)})");
    }
}
