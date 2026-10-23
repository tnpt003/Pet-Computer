using DesktopPet;

// The battle's desktop apps (2026-10-23): five made-up app icons on a desktop grid that charge and
// send their help when clicked, slimes infecting them with a virus, the infected icons working
// against the cat; Adventure/Expedition/Infinity battles only. Simulation only (BattleSim), no WPF.
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
        // Steps until `done` (at most `seconds`), counting one kind of event on the way.
        int Until(BattleSim sim, Func<bool> done, double seconds, BattleEventKind kind = BattleEventKind.Won)
        {
            int n = 0;
            for (double t = 0; t < seconds && !done(); t += dt) { sim.Step(dt, 800, false); n += Count(sim, kind); sim.ClearEvents(); }
            return n;
        }
        BattleSettings Data(AppsSpec apps, int[] waves, double slimeHp = 1000, double slimeDamage = 0) => new()
        {
            FirstWaveDelay = 0, Waves = waves, Boss = false, SlimeMaxHp = slimeHp, SlimeDamage = slimeDamage, CatDamage = 0, Apps = apps
        };
        var stage = new StageSpec { Id = "apps_test" };
        DesktopApp App(BattleSim sim, BattleAppKind kind) => sim.Desktop.Single(a => a.Kind == kind);
        // A battle whose first wave is out and settled (the slimes Targetable).
        BattleSim Out(AppsSpec rules, int[] waves, double slimeHp = 1000, double slimeDamage = 0, int seed = 5)
        {
            var sim = new BattleSim(Data(rules, waves, slimeHp, slimeDamage), 0, 1600, stage: stage, seed: seed);
            Run(sim, 0.8);
            for (double t = 0; t < 4 && sim.Slimes.Count(s => s.Targetable) < waves[0]; t += dt) sim.Step(dt, 800, false);
            sim.ClearEvents();
            return sim;
        }

        // The desktop: five icons on distinct cells, partly charged; none in a free battle.
        var free = new BattleSim(Data(new AppsSpec(), new[] { 1 }), 0, 1600, seed: 1);
        Run(free, 1);
        check(free.AppRules == null && free.Desktop.Count == 0, "Free battles have no desktop apps");
        var sim = new BattleSim(Data(new AppsSpec(), new[] { 1 }), 0, 1600, stage: stage, seed: 1);
        sim.SetAppGrid(6, 2, new[] { (0, 1), (1, 1) });
        check(sim.Desktop.Count == 5 && sim.Desktop.Select(a => a.Kind).Distinct().Count() == 5 && sim.Desktop.Select(a => (a.Col, a.Row)).Distinct().Count() == 5
              && sim.Desktop.All(a => a.Col is >= 0 and < 6 && a.Row is 0 or 1 && (a.Col, a.Row) is not (0, 1) and not (1, 1) && a.Charge == 0.2),
            "Five app icons on distinct free cells of the grid (never under the HUD), 20 % charged");
        sim.SetAppGrid(3, 2, new[] { (2, 1) });
        check(sim.Desktop.Select(a => (a.Col, a.Row)).Distinct().Count() == 5 && sim.Desktop.All(a => a.Col < 3 && (a.Col, a.Row) != (2, 1)),
            "A smaller grid (another monitor, the HUD moved) puts every icon back on a free cell");
        var a1 = new BattleSim(Data(new AppsSpec(), new[] { 1 }), 0, 1600, stage: stage, seed: 3);
        var a2 = new BattleSim(Data(new AppsSpec(), new[] { 1 }), 0, 1600, stage: stage, seed: 3);
        a1.Step(dt, 800, false); a2.Step(dt, 800, false);
        check(a1.Desktop.Select(a => (a.Col, a.Row)).SequenceEqual(a2.Desktop.Select(a => (a.Col, a.Row))), "The same seed puts the icons on the same cells");

        // Charging: over time (not while the cat is held), faster with each defeated enemy; Ready once.
        var charging = new AppsSpec { StartCharge = 0, KillCharge = 0.25, List = AppsSpec.BuiltIn.Select(a => a with { Charge = a.Id == "popup" ? 0 : 10 }).ToArray() };
        sim = Out(charging, new[] { 1, 1 }, slimeHp: 20);
        double before = App(sim, BattleAppKind.Tabs).Charge;
        Run(sim, 2, held: true);
        check(App(sim, BattleAppKind.Tabs).Charge == before, "Icons do not charge while the cat is in the user's hands");
        Run(sim, 2);
        double after = App(sim, BattleAppKind.Tabs).Charge;
        check(Math.Abs(after - before - 0.2) < 0.01, $"An icon charges over battle time (10 s here: +20 % in 2 s, {before:P0} → {after:P0})");
        sim.Use(BattleSkill.Bomb, sim.Slimes.First(s => s.Targetable).Id, 800);
        Run(sim, 0.6);
        check(App(sim, BattleAppKind.Tabs).Charge >= after + 0.25, "A defeated enemy charges every clean icon");
        int readies = Until(sim, () => App(sim, BattleAppKind.Tabs).Ready, 10, BattleEventKind.AppReady);
        check(App(sim, BattleAppKind.Tabs).Ready && readies >= 1, "A full icon is ready (an AppReady event)");

        var full = new AppsSpec { StartCharge = 1 };
        // Tab tornado: flies down first, then hits the slime near the cat every pulse, not the far one.
        sim = Out(full, new[] { 2 });
        var near = sim.Slimes.Where(s => s.Targetable).ElementAt(0);
        var far = sim.Slimes.Where(s => s.Targetable).ElementAt(1);
        near.X = 830; far.X = 1400;
        sim.SetCatState(true, false);
        check(!sim.UseApp(App(sim, BattleAppKind.Tabs).Id, 800), "A helper cannot be used while the cat is in the user's hands");
        sim.SetCatState(false, false);
        check(sim.UseApp(App(sim, BattleAppKind.Tabs).Id, 800) && App(sim, BattleAppKind.Tabs).Charge == 0 && sim.Launches.Single().Kind == LaunchKind.Tabs && sim.TabsRemaining == 0,
            "Tab tornado: the click spends the charge and sends the tabs flying (nothing yet)");
        Run(sim, BattleSim.LaunchSeconds + 0.05);
        check(sim.TabsRemaining > 0 && sim.Launches.Count == 0 && near.Hp < near.MaxHp, "When the tabs land the tornado starts at once");
        double nearHp = near.Hp;
        Run(sim, 1.0);
        check(near.Hp == nearHp - 28 && far.Hp == far.MaxHp, $"Tabs hit the slime near the cat every 0.5 s (14 each), not the far one ({nearHp} → {near.Hp})");
        Run(sim, 1, held: true, lifted: true);
        check(near.Hp == nearHp - 28, "The tabs do not hit while the cat is lifted");

        // Ping storm: bells land on every slime: damage, and a stun except on an elite that ignores Push.
        sim = Out(full, new[] { 2 });
        var plain = sim.Slimes.Where(s => s.Targetable).ElementAt(0);
        var firm = sim.Slimes.Where(s => s.Targetable).ElementAt(1);
        firm.Elite = new EliteSpec { PushImmune = true };
        sim.UseApp(App(sim, BattleAppKind.Ping).Id, 800);
        check(plain.Hp == plain.MaxHp, "The bells do nothing before they land");
        Run(sim, BattleSim.LaunchSeconds - 0.02);
        foreach (var s in new[] { plain, firm }) s.Set(SlimeState.Moving);
        Run(sim, 0.06);
        check(plain.Hp == plain.MaxHp - 14 && firm.Hp == firm.MaxHp - 14 && plain.State == SlimeState.Stunned && firm.State != SlimeState.Stunned,
            "Ping storm: 14 damage on every slime, a stun except on one that ignores Push");

        // End task: the slime with the most HP; an elite loses a quarter.
        sim = Out(full, new[] { 2 });
        var big = sim.Slimes.Where(s => s.Targetable).ElementAt(0);
        var small = sim.Slimes.Where(s => s.Targetable).ElementAt(1);
        big.Hp = big.MaxHp = 1500; small.Hp = small.MaxHp = 100;
        sim.UseApp(App(sim, BattleAppKind.EndTask).Id, 800);
        Run(sim, BattleSim.LaunchSeconds + 0.02);
        check(big.State == SlimeState.Defeated && small.Hp == 100, "End task ends the slime with the most HP when it lands, and leaves the other");
        sim = Out(full, new[] { 1 });
        var elite = sim.Slimes.First(s => s.Targetable);
        elite.Hp = elite.MaxHp = 400; elite.Elite = new EliteSpec();
        sim.UseApp(App(sim, BattleAppKind.EndTask).Id, 800);
        Run(sim, BattleSim.LaunchSeconds + 0.02);
        check(elite.Hp == 300, $"End task on an elite takes 25 % of its max HP ({elite.Hp})");

        // Ctrl+Z: nothing lost, nothing happens (it keeps its charge); after a hit the HP comes back.
        sim = Out(full, new[] { 1 }, slimeDamage: 10);
        var undo = App(sim, BattleAppKind.Undo);
        check(!sim.UseApp(undo.Id, 800) && undo.Ready, "Ctrl+Z with nothing to undo keeps its charge");
        foreach (var s in sim.Slimes.Where(s => s.Targetable)) s.X = 840;
        Until(sim, () => sim.CatHp < sim.CatMaxHp, 6);
        double hurt = sim.CatHp;
        check(hurt < sim.CatMaxHp && sim.UseApp(undo.Id, 800), "Ctrl+Z is sent after a hit");
        foreach (var s in sim.Slimes.Where(s => s.Targetable)) s.X = 1500; // No new hit while it flies.
        Run(sim, BattleSim.LaunchSeconds + 0.02);
        check(sim.CatHp == sim.CatMaxHp, $"Ctrl+Z gives back the HP just lost when it lands ({hurt} → {sim.CatHp})");

        // The bin: the two plain slimes nearest the cat go at once (no boss, no elite), flying into it.
        sim = Out(full, new[] { 3 });
        var line = sim.Slimes.Where(s => s.Targetable).ToList();
        line[0].X = 850; line[1].X = 900; line[2].X = 860; line[2].Elite = new EliteSpec();
        sim.UseApp(App(sim, BattleAppKind.Trash).Id, 800);
        check(line[0].State == SlimeState.Defeated && line[1].State == SlimeState.Defeated && line[2].State != SlimeState.Defeated
              && sim.Launches.Single() is { Kind: LaunchKind.Trash, ToIcon: true, Targets.Length: 2 }, "The bin takes the 2 nearest plain slimes (not an elite); they fly into it");

        // A new wave moves one icon to another cell.
        sim = Out(full, new[] { 1, 1 }, slimeHp: 20);
        sim.Use(BattleSkill.Bomb, sim.Slimes.First(s => s.Targetable).Id, 800);
        int moves = Until(sim, () => sim.Wave == 2, 8, BattleEventKind.AppMoved);
        var mover = sim.Desktop.SingleOrDefault(a => a.MovedAt >= 0);
        check(moves == 1 && mover is { Crawled: false } && (mover.Col, mover.Row) != (mover.FromCol, mover.FromRow), "The second wave moves one icon to another cell (a hop)");

        // Viruses: a slime shoots one at an icon; it stops charging, crawls, and acts against the cat
        // until scrubbed or its slime is defeated.
        AppsSpec Virus(double every = 2) => new()
        {
            StartCharge = 0.5, InfectMinThreat = 0, InfectChance = 1, CrawlEvery = 2,
            PopupEvery = every, BellEvery = every, FreezeEvery = every, MendEvery = every, SpitEvery = every, PopupSeconds = 3
        };
        BattleSim Infected(int seed, int[]? waves = null)
        {
            var s = Out(Virus(), waves ?? new[] { 2 }, seed: seed);
            Until(s, () => s.Desktop.Any(a => a.Infected), 12);
            return s;
        }
        var guarded = Out(new AppsSpec { InfectChance = 1, InfectMinThreat = 1.5 }, new[] { 1 });
        check(Until(guarded, () => false, 10, BattleEventKind.GooShot) == 0 && !guarded.Desktop.Any(a => a.Infected), "No virus below the rules' threat");
        sim = Out(Virus(), new[] { 2 });
        int shots = Until(sim, () => sim.Launches.Any(l => l.Kind == LaunchKind.Goo), 9, BattleEventKind.GooShot);
        var goo = sim.Launches.FirstOrDefault(l => l.Kind == LaunchKind.Goo);
        check(shots == 1 && goo is { ToIcon: true } && sim.IsInfector(goo.SlimeId) && !sim.FindApp(goo.AppId)!.Infected, "A slime shoots its virus at an icon (it flies up; the slime wears the mark)");
        Run(sim, BattleSim.GooSeconds);
        var sick = sim.FindApp(goo!.AppId)!;
        check(sick.Infected && sick.InfectedBy == goo.SlimeId && sick.ScrubsLeft == 3 && !sick.Ready, "It lands: the icon is infected (3 clicks to scrub)");
        double charge = sick.Charge;
        int crawls = Until(sim, () => false, 2.2, BattleEventKind.AppMoved);
        check(sick.Charge == charge && crawls >= 1 && sick.Crawled, "An infected icon stops charging and crawls to another cell");
        sim.SetCatState(true, true);
        check(sim.UseApp(sick.Id, 800) && sim.UseApp(sick.Id, 800) && sick.Infected && sick.ScrubsLeft == 1, "Each click scrubs once (even with the cat in the user's hands)");
        check(sim.UseApp(sick.Id, 800) && !sick.Infected && sick.CleanedAt >= 0, "The last click cleans it");
        sim = Infected(6);
        sick = sim.Desktop.First(a => a.Infected);
        var shooter = sim.Find(sick.InfectedBy)!;
        sim.ClearEvents();
        shooter.Hp = 1;
        sim.Use(BattleSkill.Bomb, shooter.Id, 800);
        Run(sim, 0.6);
        check(!sick.Infected && !sim.IsInfector(shooter.Id), "Defeating the slime that shot it cleans the icon");

        // What each infected icon does (every 2 s here).
        BattleSim Sick(BattleAppKind kind)
        {
            for (int seed = 1; seed < 80; seed++)
            {
                var s = Infected(seed);
                if (s.Desktop.Any(a => a.Infected && a.Kind == kind)) return s;
            }
            throw new InvalidOperationException("no seed infects " + kind);
        }
        sim = Sick(BattleAppKind.Tabs);
        Until(sim, () => sim.Popups.Count > 0, 4);
        var ad = sim.Popups.FirstOrDefault();
        check(ad != null && sim.Slimes.Any(s => s.Targetable && Math.Abs(s.X - ad.X) < BattleSim.PopupWidth), "Infected Tabs opens an ad over a slime");
        sim.SetCatState(true, true);
        check(ad != null && sim.ClosePopup(ad.Id) && sim.Popups.Count == 0 && sim.PopupsClosed == 1, "The ad's × closes it, even with the cat in the user's hands");
        sim.SetCatState(false, false);
        Until(sim, () => sim.Popups.Count > 0, 4);
        int alive = sim.Alive;
        int spawned = Until(sim, () => sim.Popups.Count == 0, 4, BattleEventKind.PopupSpawned);
        check(spawned == 1 && sim.Alive == alive + 1, $"Left alone, the ad lets a slime out ({alive} → {sim.Alive})");

        sim = Sick(BattleAppKind.Ping);
        foreach (var s in sim.Slimes) s.X = 1500; // Only the bells can hurt the cat here.
        double hp = sim.CatHp;
        Until(sim, () => sim.CatHp < hp, 4);
        check(hp - sim.CatHp == 6 && sim.Effects.Any(e => e.Kind == BattleEffectKind.Bell), $"Infected Ping drops a bell on the cat's head (6 damage, {hp} → {sim.CatHp})");
        sim.Use(BattleSkill.Shield, 800);
        hp = sim.CatHp;
        int blocked = Until(sim, () => false, 2.5, BattleEventKind.Blocked);
        check(sim.CatHp == hp && blocked >= 1, "The bubble shield stops the bell");

        sim = Sick(BattleAppKind.EndTask);
        foreach (var s in sim.Slimes) s.X = 1500;
        Until(sim, () => sim.CatStunned, 4);
        check(sim.CatStunned && sim.CatStunRemaining <= 1.2 && sim.Effects.Any(e => e.Kind == BattleEffectKind.EndTask), "Infected End task leaves the cat \"Not responding\" (stunned 1.2 s)");

        sim = Sick(BattleAppKind.Undo);
        foreach (var s in sim.Slimes.Where(s => s.Targetable)) s.Hp = s.MaxHp;
        var weak = sim.Slimes.First(s => s.Targetable);
        weak.Hp = weak.MaxHp / 2;
        double weakHp = weak.Hp;
        Until(sim, () => weak.Hp > weakHp, 4);
        check(weak.Hp == weakHp + weak.MaxHp * 0.15, $"Infected Ctrl+Z heals the most hurt slime 15 % ({weakHp} → {weak.Hp})");

        sim = Sick(BattleAppKind.Trash);
        alive = sim.Alive;
        Until(sim, () => sim.Alive > alive, 4);
        check(sim.Alive == alive + 1, "The infected bin spits a slime onto the floor");

        // The end: nothing flies, viruses and ads are gone, the icons stay for the outro.
        sim = Infected(8, new[] { 1 });
        foreach (var s in sim.Slimes.Where(s => s.Targetable).ToList()) { s.Hp = 1; s.X = 820; }
        if (sim.Slimes.FirstOrDefault(s => s.Targetable) is Slime last) sim.Use(BattleSkill.Bomb, last.Id, 800);
        Until(sim, () => sim.Over, 3);
        check(sim.Over && sim.Desktop.Count == 5 && !sim.Desktop.Any(a => a.Infected) && sim.Launches.Count == 0 && sim.Popups.Count == 0,
            "When the battle ends the icons stay (for the outro); no virus, ad or flight is left");

        // Paused: no app is used.
        sim = Out(full, new[] { 1 });
        sim.Paused = true;
        check(!sim.UseApp(App(sim, BattleAppKind.Tabs).Id, 800), "Paused: an app cannot be used");

        // The shipped data: every app named and described (helper and infected) in both languages.
        var shipped = BattleData.Settings.Apps;
        check(shipped != null && shipped.AppList.Count == 6 && shipped.AppList.All(a => a.NameVi.Length > 0 && a.NameEn.Length > 0 && a.DescVi.Length > 0 && a.DescEn.Length > 0)
              && shipped.AppList.Where(a => a.Id != "popup").All(a => a.BadVi.Length > 0 && a.BadEn.Length > 0 && a.Charge > 0),
            "battle.json \"apps\": all six apps named and described in both languages; the five icons say what they do infected and how long they charge");
        var shippedSim = new BattleSim(BattleData.Settings, 0, 1600, stage: stage, seed: 2);
        shippedSim.Step(dt, 800, false);
        double ChargeOf(BattleAppKind kind) => shippedSim.Desktop.Single(a => a.Kind == kind).Charge;
        check(Math.Abs(ChargeOf(BattleAppKind.Tabs) - 0.6) < 0.01 && Math.Abs(ChargeOf(BattleAppKind.Ping) - 0.4) < 0.01 &&
              new[] { BattleAppKind.EndTask, BattleAppKind.Undo, BattleAppKind.Trash }.All(k => Math.Abs(ChargeOf(k) - 0.2) < 0.01) &&
              shipped!.App(BattleAppKind.Tabs).Charge * 0.4 <= 20.5,
            "Staggered start: Tabs 60 %, Ping 40 %, the others 20 % — the first app is ready about 20 s in"); 
        string dir = Path.Combine(Path.GetTempPath(), "pet-apps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "battle.json");
            string? Error(string apps) { File.WriteAllText(file, "{ \"schemaVersion\": 1, \"apps\": " + apps + " }"); return BattleData.Load(file).Error; }
            check(Error("{ \"scrubClicks\": 4 }") == null, "A user file can change one app number");
            check(Error("{ \"popupSpawn\": \"dragon\" }")?.Contains("popupSpawn") == true, "An unknown popupSpawn enemy is refused");
            check(Error("{ \"list\": [ { \"id\": \"tabs\", \"nameVi\": \"a\", \"nameEn\": \"a\", \"descVi\": \"\", \"descEn\": \"\", \"charge\": 5 } ] }")?.Contains("every app") == true,
                "A list missing apps is refused");
            check(Error("{ \"infectChance\": 2 }")?.Contains("infectChance") == true, "infectChance must be 0–1");
            check(Error("{ \"list\": [ " + string.Join(", ", Enum.GetNames<BattleAppKind>().Select(k => "{ \"id\": \"" + k.ToLowerInvariant() + "\", \"nameVi\": \"a\", \"nameEn\": \"a\", \"descVi\": \"\", \"descEn\": \"\", \"charge\": " + (k == "Popup" ? "0" : "5") + (k == "Tabs" ? ", \"start\": 1.5" : "") + " }")) + " ] }")?.Contains("start") == true,
                "An app's start charge must be 0–1");
        }
        finally { Directory.Delete(dir, true); }

        // Malware (2026-10-24): the wave's virus thrown onto the field as a loading ring.
        var ringRules = new AppsSpec { InfectMinThreat = 0, InfectChance = 1, InfectWeight = 0, LoadingWeight = 1, LoadingSeconds = 6, LoadingRadius = 60, LoadingSlow = 0.6 };
        var ring = Out(ringRules, new[] { 1 });
        Until(ring, () => ring.Hazards.Count > 0, 10);
        var loading = ring.Hazards.FirstOrDefault();
        check(loading is { Kind: HazardKind.Loading } && Math.Abs(loading.X - 800) < 1 && ring.Desktop.All(a => !a.Infected),
            "With only malware weighted, the wave's virus becomes a loading ring where the cat stands (no icon infected)");
        check(ring.LagFactor == 0.6, "Inside the ring the cat runs and swings at 60 % speed");
        ring.Step(dt, 900, false);
        check(ring.LagFactor == 1, "Out of the ring the cat is at full speed");
        ring.Step(dt, 800, true, true);
        double heldAge = loading!.Age;
        Run(ring, 2, held: true, lifted: true);
        check(loading.Age == heldAge && ring.LagFactor == 1, "While the cat is lifted the ring waits and does not slow it");
        Run(ring, 6.5);
        check(ring.Hazards.Count == 0, "The ring wears off after its time");
        var closing = Out(ringRules, new[] { 1 });
        Until(closing, () => closing.Hazards.Count > 0, 10);
        int ringId = closing.Hazards[0].Id;
        check(closing.CloseHazard(ringId) && closing.Hazards.Count == 0 && closing.Events.Any(e => e.Kind == BattleEventKind.HazardClosed) && !closing.CloseHazard(ringId),
            "Clicking the spinner closes the ring at once (once)");
        var mixed = new AppsSpec { InfectMinThreat = 0, InfectChance = 1, LoadingWeight = 1 };
        int rings = 0, infections = 0;
        for (int seed = 1; seed <= 24; seed++)
        {
            var roll = Out(mixed, new[] { 1 }, seed: seed);
            Until(roll, () => roll.Hazards.Count > 0 || roll.Desktop.Any(a => a.Infected), 12);
            if (roll.Hazards.Count > 0) rings++;
            if (roll.Desktop.Any(a => a.Infected)) infections++;
        }
        check(rings > 3 && infections > 3, $"With both weighted, waves get a ring or an infection ({rings} rings, {infections} infections in 24 battles)");
        check(BattleData.Settings.Apps!.LoadingWeight > 0 && BattleData.Settings.Apps.InfectWeight > 0, "The shipped data throws loading rings as well as viruses");

        // Turned off in the setup panel (2026-10-24): no icons, no viruses; records kept apart, progress still counts.
        var off = new BattleSim(BattleData.Settings, 0, 1600, stage: stage, seed: 2, apps: false);
        Run(off, 20);
        check(!off.AppsOn && off.AppRules == null && off.Desktop.Count == 0 && off.Launches.Count == 0, "Apps turned off: no icons and nothing flies, the stage plays on");
        check(new BattleSim(BattleData.Settings, 0, 1600, seed: 2).AppRules == null, "A free battle never has apps");
        check(AdventureProgress.RecordKey(stage, "Sword", BattleDifficulty.Normal, noApps: true) != AdventureProgress.RecordKey(stage, "Sword", BattleDifficulty.Normal)
              && AdventureProgress.RecordKey(stage, "Sword", BattleDifficulty.Normal, noApps: false) == $"apps_test|r{stage.RulesVersion}|Sword|Normal",
            "Adventure records without apps get their own key; the usual key is unchanged");
        var infinity = BattleData.Settings.Endless!;
        var records = new BattleRecords();
        records.AddEndless(infinity, "Sword", BattleDifficulty.Normal, false, wave: 25, defeated: 90, cleared: 24, noApps: true);
        records.AddEndless(infinity, "Sword", BattleDifficulty.Normal, false, wave: 6, defeated: 20, cleared: 5);
        records.AddEndless(infinity, "Sword", BattleDifficulty.Normal, true, wave: 40, defeated: 99, cleared: 39, noApps: true);
        check(records.ForEndless(infinity, "Sword", BattleDifficulty.Normal, noApps: true).MostWaves == 25 && records.ForEndless(infinity, "Sword", BattleDifficulty.Normal).MostWaves == 6,
            "Infinity without apps keeps its own record beside the usual one");
        check(records.EndlessBest(infinity) == 6 && records.EndlessCleared(infinity) == 24,
            "The hub's furthest wave counts runs with apps only; milestone rewards count runs without apps too (never custom data)");
        var trip = new Expedition(BattleData.Settings, new ExpeditionProgress());
        var tripRun = trip.Start("Sword", BattleDifficulty.Normal, 7, noApps: true);
        check(tripRun.NoApps && Expedition.RecordKey(trip.Spec, "Sword", BattleDifficulty.Normal, true).EndsWith("|noapps") && !trip.Start("Sword", BattleDifficulty.Normal, 7).NoApps,
            "An Expedition run remembers it set out without apps, for its battles and its records");
    }
}
