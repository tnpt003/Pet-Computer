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
        check(sim.Effects.Any(e => e.Kind == BattleEffectKind.Poof && e.Tint == big.Type.Color), "A defeated slime's poof carries its colour (the droplets it bursts into)");
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
            PopupEvery = every, BellEvery = every, FreezeEvery = every, MendEvery = every, SpitEvery = every, PopupSeconds = 3, SpitElite = false
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
        Until(sim, () => sim.Popups.Count == 3, 4);
        var xs = sim.Popups.Select(p => p.X).OrderBy(x => x).ToList();
        check(sim.Popups.Count == 3 && xs.Zip(xs.Skip(1)).All(p => p.Second - p.First >= BattleSim.PopupWidth * 0.5) && xs[^1] - xs[0] > 600,
            $"Infected Tabs opens three ads spread over the floor ({string.Join(", ", xs.Select(x => x.ToString("0")))})");
        var ad = sim.Popups[0];
        double adAge = ad.Age;
        Run(sim, 4);
        check(sim.PopupsLocked && !sim.ClosePopup(ad.Id) && sim.Popups.Count == 3 && sim.Events.Any(e => e.Kind == BattleEventKind.PopupLocked) && ad.Age == adAge,
            "While Tabs is infected its ads cannot be closed (a PopupLocked event) and do not run out");
        var tabsIcon = App(sim, BattleAppKind.Tabs);
        while (tabsIcon.Infected) sim.UseApp(tabsIcon.Id, 800);
        sim.SetCatState(true, true);
        check(!sim.PopupsLocked && sim.ClosePopup(ad.Id) && sim.Popups.Count == 2 && sim.PopupsClosed == 1, "Once Tabs is clean the ad's × closes it, even with the cat in the user's hands");
        sim.SetCatState(false, false);
        int alive = sim.Alive, room = Math.Min(2, sim.Settings.MaxAlive - alive);
        int spawned = Until(sim, () => sim.Popups.Count == 0, 4, BattleEventKind.PopupSpawned);
        check(room > 0 && spawned == room && sim.Alive == alive + room && sim.Popups.Count == 0, $"Left alone, each ad goes and lets a slime out while the floor has room ({alive} → {sim.Alive})");

        sim = Sick(BattleAppKind.Ping);
        foreach (var s in sim.Slimes) s.X = 1500; // Only the bells can hurt the cat here.
        double hp = sim.CatHp;
        Until(sim, () => sim.CatHp < hp, 4);
        check(hp - sim.CatHp == 6 && sim.Effects.Any(e => e.Kind == BattleEffectKind.Bell), $"Infected Ping drops a bell on the cat's head (6 damage, {hp} → {sim.CatHp})");
        check(sim.CatStunned && sim.CatStunRemaining <= 1 && sim.CatStunRemaining > 0.9, $"...and the bell stuns the cat 1 s ({sim.CatStunRemaining:0.00})");
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
        double guardedHp = weak.Hp;
        sim.Use(BattleSkill.Bomb, weak.Id, weak.X);
        Run(sim, 0.6);
        check(weak.WardUntil > sim.Time - 0.7 && weak.Hp == guardedHp && sim.WardedHits >= 1, $"...and guards it 2 s: a bomb on it does nothing ({weak.Hp})");

        sim = Sick(BattleAppKind.Trash);
        alive = sim.Alive;
        Until(sim, () => sim.Alive > alive, 4);
        check(sim.Alive == alive + 1 && sim.Slimes.Count(s => s.Spat) == 1, "The infected bin spits a slime onto the floor");
        Run(sim, 4);
        check(sim.Slimes.Count(s => s.Spat && s.State != SlimeState.Defeated) == 1, "Only one spat slime at a time");
        // With kinds that have an elite affix, what it spits is an elite (not counted as a rolled one).
        var eliteKinds = new BattleSettings
        {
            FirstWaveDelay = 0, CatDamage = 0, CatMaxHp = 1_000_000, Apps = Virus() with { SpitElite = true },
            Enemies = new[] { new EnemyType { Id = "slime", MaxHp = 1000, Speed = 0, Damage = 0, Elite = new EliteSpec { Hp = 1.2, DescVi = "a", DescEn = "a" } } },
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 2 } } } }
        };
        BattleSim? spitter = null;
        for (int seed = 1; seed < 80 && spitter == null; seed++)
        {
            var s = new BattleSim(eliteKinds, 0, 1600, stage: stage, seed: seed);
            Until(s, () => s.Desktop.Any(a => a.Infected), 12);
            if (s.Desktop.Any(a => a.Infected && a.Kind == BattleAppKind.Trash)) spitter = s;
        }
        int spatAlive = spitter?.Alive ?? 0;
        if (spitter != null) Until(spitter, () => spitter.Alive > spatAlive, 4);
        check(spitter?.Slimes.FirstOrDefault(s => s.Spat) is { Elite: not null, MaxHp: 1200 } && spitter.ElitesSpawned == 0, "With elite kinds, the bin spits an elite (its affix applied)");

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

        // Malware (2026-10-24): at random times while the fight goes on (every 3 s here, the first after 1.5 s).
        AppsSpec Malware(double loading = 0, double update = 0, double exe = 0, double ads = 0, int max = 2) => new()
        {
            InfectChance = 0, MalwareMinThreat = 0, MalwareEveryMin = 3, MalwareEveryMax = 3, MaxMalware = max,
            LoadingWeight = loading, UpdateWeight = update, VirusExeWeight = exe, AdWeight = ads
        };
        var ringRules = Malware(loading: 1) with { MalwareEveryMin = 6, MalwareEveryMax = 6, LoadingSeconds = 5 };
        var ring = Out(ringRules, new[] { 1 });
        Until(ring, () => ring.Hazards.Count > 0, 10);
        var loading = ring.Hazards.FirstOrDefault();
        check(loading is { Kind: HazardKind.Loading } && Math.Abs(loading.X - 800) < 1 && ring.Desktop.All(a => !a.Infected) && ring.Time < 3.5,
            $"Malware comes at a random time of the fight: a loading ring where the cat stands, no icon infected ({ring.Time:0.0} s)");
        check(ring.LagFactor == 0.6, "Inside the ring the cat runs and swings at 60 % speed");
        ring.Step(dt, 900, false);
        check(ring.LagFactor == 1, "Out of the ring the cat is at full speed");
        ring.Step(dt, 800, true, true);
        double heldAge = loading!.Age;
        Run(ring, 2, held: true, lifted: true);
        check(loading.Age == heldAge && ring.LagFactor == 1, "While the cat is lifted the ring waits and does not slow it");
        Run(ring, 5.3);
        check(ring.Hazards.Count == 0, "The ring wears off after its time");
        var closing = Out(ringRules, new[] { 1 });
        Until(closing, () => closing.Hazards.Count > 0, 10);
        int ringId = closing.Hazards[0].Id;
        check(closing.CloseHazard(ringId) && closing.Hazards.Count == 0 && closing.Events.Any(e => e.Kind == BattleEventKind.HazardClosed) && !closing.CloseHazard(ringId),
            "loadingHold 0: clicking the spinner closes the ring at once (once)");
        var holding = Out(new AppsSpec { InfectChance = 0, MalwareMinThreat = 0, MalwareEveryMin = 3, MalwareEveryMax = 3, LoadingWeight = 1, LoadingSeconds = 30, LoadingHold = 2 }, new[] { 1 });
        Until(holding, () => holding.Hazards.Count > 0, 10);
        var held = holding.Hazards[0];
        check(holding.CloseHazard(held.Id) && holding.HoldingHazardId == held.Id && holding.Hazards.Count == 1, "loadingHold 2: pressing the spinner starts holding it (the ring stays)");
        Run(holding, 1);
        holding.ReleaseHazard();
        check(holding.Hazards.Count == 1 && held.Held == 0 && holding.HoldingHazardId == 0, "Letting go after 1 s: the ring stays and its hold starts over");
        holding.CloseHazard(held.Id);
        Run(holding, 1.5, held: true, lifted: false);
        check(holding.Hazards.Count == 1, "(the hold counts even while the cat is pressed)");
        Run(holding, 0.6);
        check(holding.Hazards.Count == 0 && holding.HoldingHazardId == 0 && holding.Events.Count(e => e.Kind == BattleEventKind.HazardClosed) <= 1, "Held 2 s, the ring closes");
        // At most maxMalware kinds out at once, a kind never twice.
        var capped = Out(Malware(loading: 1, update: 1, max: 1) with { LoadingSeconds = 30, UpdateSeconds = 30 }, new[] { 1 });
        Run(capped, 12);
        check(capped.Hazards.Count == 1, $"maxMalware 1: one kind out at a time ({capped.Hazards.Count})");
        var both = Out(Malware(loading: 1, update: 1, max: 2) with { LoadingSeconds = 30, UpdateSeconds = 30 }, new[] { 1 }, slimeHp: 1000);
        foreach (var s in both.Slimes) s.X = 1400;
        Run(both, 12);
        check(both.Hazards.Select(h => h.Kind).Distinct().Count() == 2 && both.Hazards.Count == 2, $"maxMalware 2: a ring and an update together, each once ({both.Hazards.Count})");
        // The ads as malware: three of them, closable (no infected Tabs).
        var adWave = Out(Malware(ads: 1), new[] { 1 });
        Until(adWave, () => adWave.Popups.Count > 0, 10);
        check(adWave.Popups.Count == 3 && !adWave.PopupsLocked && adWave.ClosePopup(adWave.Popups[0].Id), "Ads come as malware too: three of them, each closed by its ×");
        var calm = Out(Malware(loading: 1) with { MalwareMinThreat = 1.5 }, new[] { 1 });
        Run(calm, 10);
        check(calm.Hazards.Count == 0, "No malware below malwareMinThreat");
        var shippedApps = BattleData.Settings.Apps!;
        check(shippedApps is { LoadingWeight: > 0, UpdateWeight: > 0, VirusExeWeight: > 0, AdWeight: > 0, MaxMalware: 2, MaxInfected: 3, ScrubClicks: 5, LoadingHold: 2, UpdateClicks: 4, LoadingSeconds: 12, AdCount: 3 },
            "The shipped data: every malware kind, at most two at once; up to 3 icons infected, 5 clicks to clean; hold 2 s, Later ×4, 12 s rings, 3 ads");
        check(BattleData.ParseWithOverride(ExpeditionFixture.Embedded(), "{ \"schemaVersion\": 1, \"apps\": { \"infectWeight\": 1 } }").Apps!.LoadingWeight > 0,
            "A user file that still sets apps.infectWeight keeps working (the key is dropped)");

        // Elites with a virus affix shoot at icons: soon after turning elite, at most eliteVirusPerWave a wave.
        var shots2 = Out(new AppsSpec { InfectChance = 0, MaxInfected = 5, EliteVirusPerWave = 2 }, new[] { 2 });
        foreach (var s in shots2.Slimes.Where(s => s.Targetable)) { s.Elite = new EliteSpec { VirusEvery = 5 }; s.VirusClock = 0.5; }
        int eliteGoo = Until(shots2, () => false, 20, BattleEventKind.GooShot);
        check(eliteGoo == 2, $"Elite slimes with virusEvery shoot at icons, at most 2 a wave ({eliteGoo} shots in 20 s)");

        // Malware: a forced update standing between the cat and the slimes as a wall.
        var wallRules = Malware(update: 1) with { UpdateSeconds = 20, UpdateHitSeconds = 1.2, UpdateClicks = 2 };
        var walled = Out(wallRules, new[] { 1 });
        Until(walled, () => walled.Hazards.Count > 0, 10);
        var update = walled.Hazards.FirstOrDefault();
        var walledSlime = walled.Slimes.FirstOrDefault(s => s.Targetable);
        check(update is { Kind: HazardKind.Update, ClicksLeft: 2 } && walledSlime != null && Math.Abs(Math.Abs(update.X - 800) - (BattleSim.WallHalfWidth + BattleSim.CatHalfWidth + 30)) < 1
              && Math.Sign(update.X - 800) == Math.Sign(walledSlime.X - 800),
            $"A forced update lands between the cat and the slime ({update?.X:0}, slime {walledSlime?.X:0})");
        bool woundUp = false;
        for (double t = 0; t < 3; t += dt) { walled.Step(dt, 800, false); woundUp |= walledSlime!.State == SlimeState.Windup; }
        check(Math.Abs(walledSlime!.X - update!.X) >= BattleSim.WallHalfWidth + walledSlime.Width / 2 - 0.5 && !woundUp,
            $"The slime cannot pass the wall nor attack across it ({walledSlime.X:0})");
        int toWall = Math.Sign(update.X - 800);
        double nextTo = update.X - toWall * (BattleSim.WallHalfWidth + 30);
        double ageBefore = update.Age;
        double back = 800 - toWall * 120;
        var order = walled.Step(dt, back, false);
        for (double t = 0; t < 1.5 && order.Command != CatCommand.Move; t += dt) order = walled.Step(dt, back, false); // A swing under way ends first.
        check(order.Command == CatCommand.Move && Math.Sign(order.X - back) == toWall && Math.Sign(update.X - order.X) == toWall && Math.Abs(order.X - update.X) > BattleSim.WallHalfWidth,
            $"From further away the cat walks up to the wall, not through it ({order.Command} to {order.X:0}, wall {update.X:0})");
        int wallHits = 0;
        for (double t = 0; t < 2; t += dt) { walled.Step(dt, nextTo, false); wallHits += Count(walled, BattleEventKind.WallHit); walled.ClearEvents(); }
        check(wallHits >= 1 && update.Age - ageBefore >= 1.2 + 2 - 0.1, $"Next to it, the cat hits the wall and each hit brings the update closer to done ({wallHits} hits)");
        check(walled.CloseHazard(update.Id) && walled.Hazards.Count == 1 && update.ClicksLeft == 1 && walled.CloseHazard(update.Id) && walled.Hazards.Count == 0,
            "Later twice closes the forced update");
        var expiring = Out(Malware(update: 1, max: 1) with { UpdateSeconds = 8, MalwareEveryMin = 20, MalwareEveryMax = 20 }, new[] { 1 });
        Until(expiring, () => expiring.Hazards.Count > 0, 10);
        Run(expiring, 8.5, catX: 800);
        check(expiring.Hazards.Count == 0, "Left alone, the update is done after its time");
        check(BattleData.Settings.Apps!.UpdateWeight > 0, "The shipped data throws forced updates too");

        // Malware: virus.exe on the desktop grid, copying itself until it infects an icon.
        var exeRules = Malware(exe: 1) with { VirusExeEvery = 2, VirusExeCopies = 3 };
        var outbreak = Out(exeRules, new[] { 1 });
        Until(outbreak, () => outbreak.VirusFiles.Count > 0, 10);
        var firstFile = outbreak.VirusFiles.FirstOrDefault();
        check(firstFile != null && outbreak.Desktop.All(a => (a.Col, a.Row) != (firstFile.Col, firstFile.Row) && !a.Infected) && outbreak.IsInfector(firstFile.SlimeId),
            "virus.exe lands on a free desktop cell; the slime that dropped it wears the virus mark");
        Run(outbreak, 2, held: true, lifted: true);
        check(outbreak.VirusFiles.Count == 1, "While the cat is lifted it does not copy itself");
        Run(outbreak, 2.1);
        var cells = outbreak.VirusFiles.Select(f => (f.Col, f.Row)).ToList();
        check(outbreak.VirusFiles.Count == 2 && cells.Distinct().Count() == 2 && outbreak.Desktop.All(a => !cells.Contains((a.Col, a.Row))),
            "Every few seconds it copies itself to another free cell");
        int copyId = outbreak.VirusFiles[^1].Id;
        check(outbreak.DeleteVirusFile(copyId) && outbreak.VirusFiles.Count == 1 && !outbreak.DeleteVirusFile(copyId), "A click deletes one copy (once)");
        Until(outbreak, () => outbreak.Desktop.Any(a => a.Infected), 10);
        check(outbreak.Desktop.Count(a => a.Infected) == 1 && outbreak.VirusFiles.Count == 0,
            "With enough copies they infect a clean icon and are gone");
        check(BattleData.Settings.Apps!.VirusExeWeight > 0 && BattleData.Settings.Campaign!.Chapters[0].Stages.Skip(1).All(s => s.RulesVersion >= 13) && BattleData.Settings.Endless!.RulesVersion >= 3,
            "The shipped data drops virus.exe too; Adventure stages 2–5 and Infinity have new rules versions for the malware");

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
