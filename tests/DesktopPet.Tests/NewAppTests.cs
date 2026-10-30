using DesktopPet;

// 1.5 M26 (docs/UPDATE-1.5.md A1–A4): the Screenshot icon and the captcha, low battery and frozen cursor,
// only where a stage has newAppsFrom. Simulation only (BattleSim), no WPF.
internal static class NewAppTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        void Run(BattleSim sim, double seconds, double catX = 800, bool held = false, bool lifted = false)
        {
            for (double t = 0; t < seconds; t += dt) sim.Step(dt, catX, held, lifted);
        }
        int Until(BattleSim sim, Func<bool> done, double seconds, BattleEventKind kind = BattleEventKind.Won, double catX = 800)
        {
            int n = 0;
            for (double t = 0; t < seconds && !done(); t += dt) { sim.Step(dt, catX, false); n += sim.Events.Count(e => e.Kind == kind); sim.ClearEvents(); }
            return n;
        }
        // Quick charges (1 s) so the Screenshot is ready at once; no random malware unless asked.
        AppsSpec Rules(double captcha = 0, double battery = 0, double cursor = 0) => new()
        {
            StartCharge = 1, KillCharge = 0, InfectChance = 0, MalwareMinThreat = 0, MalwareEveryMin = 3, MalwareEveryMax = 3, MaxMalware = 4,
            AdWeight = 0, LoadingWeight = 0, UpdateWeight = 0, VirusExeWeight = 0, CaptchaWeight = captcha, BatteryWeight = battery, CursorWeight = cursor,
            List = AppsSpec.BuiltIn.Select(a => a with { Charge = a.Id == "popup" ? 0 : 1 }).ToArray()
        };
        BattleSettings Data(AppsSpec apps, int count = 1, double speed = 60) => new()
        {
            FirstWaveDelay = 0, Waves = new[] { count, count }, Boss = false, SlimeMaxHp = 1000, SlimeDamage = 0, SlimeSpeed = speed, CatDamage = 0, CatMaxHp = 1000, Apps = apps
        };
        StageSpec Stage(int from, params MalwareCue[] plan) => new() { Id = "new_apps", NewAppsFrom = from, MalwarePlan = plan.Length > 0 ? plan : null };
        BattleSim Out(BattleSettings data, StageSpec stage, int seed = 5)
        {
            var sim = new BattleSim(data, 0, 1600, stage: stage, seed: seed, skills: BattleSkills.Default);
            Run(sim, 0.8, 1500);
            for (double t = 0; t < 4 && !sim.Slimes.Any(s => s.Targetable); t += dt) sim.Step(dt, 1500, false);
            sim.ClearEvents();
            return sim;
        }

        // Where the newer apps are off, the desktop and the draw are as before.
        var off = Out(Data(Rules(captcha: 1, battery: 1, cursor: 1)), Stage(0));
        var older = Out(Data(Rules(captcha: 0, battery: 0, cursor: 0)), Stage(0));
        check(off.Desktop.Count == 5 && off.Desktop.All(a => a.Kind != BattleAppKind.Screenshot)
              && off.Desktop.Select(a => (a.Col, a.Row)).SequenceEqual(older.Desktop.Select(a => (a.Col, a.Row))),
            "Without newAppsFrom: five icons, on the same cells as without the new weights (no Screenshot)");
        Run(off, 20, 1500);
        check(off.Captcha == null && !off.BatteryLow && !off.CursorFrozen, "Without newAppsFrom the newer malware never comes, whatever its weight");

        // The Screenshot: one of five icons; its flash freezes every enemy ShotFreeze s.
        var sim = Out(Data(Rules()), Stage(1));
        var shot = sim.Desktop.SingleOrDefault(a => a.Kind == BattleAppKind.Screenshot);
        check(sim.NewApps && sim.Desktop.Count == 5 && shot != null && sim.Desktop.Select(a => (a.Col, a.Row)).Distinct().Count() == 5,
            "With newAppsFrom 1: five icons on distinct cells, the Screenshot among them");
        Run(sim, 1, 1500);
        var slime = sim.Slimes.First(s => s.Targetable);
        check(sim.UseApp(shot!.Id, 1500) && sim.Launches.Any(l => l.Kind == LaunchKind.Flash) && shot.Charge == 0, "A charged Screenshot sends its flash");
        Run(sim, BattleSim.LaunchSeconds + dt, 1500);
        double x = slime.X, state = slime.StateTime;
        check(sim.EnemiesFrozen && sim.FrozenRemaining > 1.9 && sim.FlashAt >= 0, $"The flash lands: every enemy frozen 2 s ({sim.FrozenRemaining:0.00})");
        Run(sim, 1.5, 1500);
        check(slime.X == x && slime.StateTime == state && sim.EnemiesFrozen, "A frozen slime does not move or act");
        Run(sim, 1, 1500);
        check(!sim.EnemiesFrozen && slime.X != x, "Once the freeze wears off it moves again");
        var empty = new BattleSim(Data(Rules()), 0, 1600, stage: Stage(1), seed: 2);
        empty.Step(dt, 800, false);
        check(!empty.UseApp(empty.Desktop.Single(a => a.Kind == BattleAppKind.Screenshot).Id, 800), "With no enemy to freeze the Screenshot keeps its charge");
        // A frozen slime still takes hits and can be defeated.
        sim = Out(Data(Rules()), Stage(1));
        Run(sim, 1, 1500);
        sim.UseApp(sim.Desktop.Single(a => a.Kind == BattleAppKind.Screenshot).Id, 1500);
        Run(sim, BattleSim.LaunchSeconds + dt, 1500);
        slime = sim.Slimes.First(s => s.Targetable);
        slime.Hp = 1;
        sim.Use(BattleSkill.Bomb, slime.Id, 1500);
        Run(sim, 0.6, 1500);
        check(sim.EnemiesFrozen && slime.State == SlimeState.Defeated, "A frozen slime is still hit (a bomb defeats it)");
        // Infinity: the Screenshot joins at its wave.
        var late = Out(Data(Rules()), Stage(2));
        check(late.Desktop.Count == 5 && !late.NewApps, "newAppsFrom 2: wave 1 still has five icons");
        foreach (var s in late.Slimes) s.Hp = 1;
        foreach (var s in late.Slimes.Where(s => s.Targetable).ToList()) late.Use(BattleSkill.Bomb, s.Id, s.X);
        Until(late, () => late.Wave == 2, 12);
        Run(late, 0.1);
        check(late.Wave == 2 && late.NewApps && late.Desktop.Count == 5, "...and the Screenshot joins once wave 2 starts");

        // An infected Screenshot dazzles the cat: its swings miss.
        AppsSpec Virus() => Rules() with { InfectMinThreat = 0, InfectChance = 1, MaxInfected = 3, DazzleEvery = 1.5, CrawlEvery = 60 };
        BattleSim? dazzled = null;
        for (int seed = 1; seed < 200 && dazzled == null; seed++)
        {
            var s = Out(Data(Virus(), count: 3, speed: 0), Stage(1), seed);
            Until(s, () => s.Desktop.Count(a => a.Infected) >= 1, 12, catX: 1500);
            if (s.Desktop.Any(a => a.Infected && a.Kind == BattleAppKind.Screenshot)) dazzled = s;
        }
        check(dazzled != null, "Some seed infects the Screenshot");
        if (dazzled != null)
        {
            Until(dazzled, () => dazzled.CatDazzled, 4, catX: 1500);
            check(dazzled.CatDazzled && dazzled.DazzleRemaining <= 1.5, $"Infected, the Screenshot's flash dazzles the cat 1.5 s ({dazzled.DazzleRemaining:0.00})");
        }
        var settings = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, Boss = false, SlimeMaxHp = 1000, SlimeDamage = 0, SlimeSpeed = 0, CatDamage = 25, Apps = Rules() };
        var swinging = new BattleSim(settings, 0, 1600, stage: Stage(1), seed: 3);
        Run(swinging, 1.2);
        var target = swinging.Slimes.First(s => s.Targetable);
        swinging.DazzleRemaining = 5;
        double hp = target.Hp;
        Run(swinging, 2, target.X - 30);
        check(target.Hp == hp && swinging.Effects.Any(e => e.Kind == BattleEffectKind.Miss), "A dazzled cat's swings miss (a MISS, no damage)");
        swinging.DazzleRemaining = 0;
        Run(swinging, 2, target.X - 30);
        check(target.Hp < hp, "Undazzled, the swings land again");

        // The captcha: around the cat, a third of the floor; cat tiles picked, a wrong one shuffles; the last cat closes it.
        sim = Out(Data(Rules()), Stage(1, new MalwareCue { At = 0, Kind = "captcha" }));
        Until(sim, () => sim.Captcha != null, 3, catX: 1500);
        var captcha = sim.Captcha;
        int cats = captcha?.Tiles.Count(t => t == BattleCaptcha.Cat) ?? 0;
        check(captcha != null && Math.Abs(captcha.Width - Math.Min(BattleSim.CaptchaMaxWidth, 1600 / 3.0)) < 1 && captcha.X + captcha.Width / 2 <= 1600 && cats is >= 2 and <= 4,
            $"A captcha opens near the cat ({captcha?.X:0}, {captcha?.Width:0} wide), {cats} of its nine tiles show a cat");
        if (captcha != null)
        {
            int wrong = Array.FindIndex(captcha.Tiles, t => t != BattleCaptcha.Cat), right = Array.IndexOf(captcha.Tiles, BattleCaptcha.Cat);
            check(sim.ClickCaptcha(right) && captcha.Picked[right] && sim.Captcha == captcha, "A cat tile is picked");
            check(sim.ClickCaptcha(wrong) && captcha.ShuffledAt >= 0 && !captcha.Picked.Any(p => p), "A wrong tile shuffles it (nothing picked any more)");
            sim.SetCatState(true, true);
            for (int i = 0; i < 9; i++) if (captcha.Tiles[i] == BattleCaptcha.Cat) sim.ClickCaptcha(i);
            check(sim.Captcha == null && sim.Events.Any(e => e.Kind == BattleEventKind.CaptchaClosed && e.Value == 1), "Every cat picked (even with the cat in the user's hands): it closes");
        }
        sim = Out(Data(Rules()), Stage(1, new MalwareCue { At = 0, Kind = "captcha" }));
        Until(sim, () => sim.Captcha != null, 3, catX: 1500);
        Run(sim, 5, 800, held: true, lifted: true);
        check(sim.Captcha is { Age: < 1 }, "The captcha's clock waits while the cat is in the user's hands");
        Until(sim, () => sim.Captcha == null, 16, catX: 1500);
        check(sim.Captcha == null, "Left alone it closes after 15 s");
        // Never while a flyer is out (the shipped flyer).
        var flyers = BattleData.Settings;
        var sky = new StageSpec { Id = "captcha_sky", NewAppsFrom = 1, MalwarePlan = new[] { new MalwareCue { At = 0, Kind = "captcha" } },
            Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "flyer", Count = 1 }, new WaveGroup { Enemy = "slime", Count = 1 } } } } };
        var withFlyer = new BattleSim(flyers, 0, 1600, stage: sky, seed: 4);
        Run(withFlyer, 9, 800);
        check(withFlyer.Slimes.Any(s => s.Type.Fly != null && s.State != SlimeState.Defeated) && withFlyer.Captcha == null, "No captcha while a flying slime is out (the plan waits)");

        // Low battery: the skills recover at 40 % until plugged in.
        sim = Out(Data(Rules()), Stage(1, new MalwareCue { At = 0, Kind = "battery" }));
        Until(sim, () => sim.BatteryLow, 3, catX: 1500);
        sim.Use(BattleSkill.Cheer, 1500);
        double cooldown = sim.Cooldown(BattleSkill.Cheer);
        Run(sim, 2, 1500);
        check(sim.BatteryLow && Math.Abs(cooldown - sim.Cooldown(BattleSkill.Cheer) - 0.8) < 0.05, $"Low battery: 2 s bring a skill 0.8 s closer ({cooldown:0.0} → {sim.Cooldown(BattleSkill.Cheer):0.0})");
        check(sim.PlugIn() && !sim.BatteryLow && !sim.PlugIn(), "Plug in: the battery is fine again (a second click does nothing)");
        cooldown = sim.Cooldown(BattleSkill.Cheer);
        Run(sim, 1, 1500);
        check(Math.Abs(cooldown - sim.Cooldown(BattleSkill.Cheer) - 1) < 0.05, "...and the skills recover at their usual speed");

        // Frozen cursor: no skill for 5 s or until Continue; a chosen skill is put away; apps still work.
        sim = Out(Data(Rules()), Stage(1, new MalwareCue { At = 0.5, Kind = "cursor" }));
        sim.Arm(BattleSkill.Bomb);
        Until(sim, () => sim.CursorFrozen, 3, catX: 1500);
        check(sim.CursorFrozen && sim.Armed == null && !sim.Ready(BattleSkill.Heal) && !sim.Use(BattleSkill.Heal, 1500) && !sim.Arm(BattleSkill.Bomb),
            "Frozen cursor: the chosen skill is put away, no skill can be used or chosen");
        var tabs = sim.Desktop.Single(a => a.Kind == BattleAppKind.Tabs);
        check(sim.UseApp(tabs.Id, 1500), "...an app still can");
        check(sim.ResumeCursor() && !sim.CursorFrozen && sim.Ready(BattleSkill.Heal), "Continue: the skills work again");
        sim = Out(Data(Rules()), Stage(1, new MalwareCue { At = 0, Kind = "cursor" }));
        Until(sim, () => sim.CursorFrozen, 3, catX: 1500);
        Run(sim, 5.05, 1500);
        check(!sim.CursorFrozen, "...or it runs out after 5 s");

        // Random draws, and a cleared wave takes them all away.
        sim = Out(Data(Rules(captcha: 1, battery: 1, cursor: 1)), Stage(1));
        Until(sim, () => sim.Captcha != null && sim.BatteryLow && sim.CursorFrozen, 30, catX: 1500);
        check(sim.Captcha != null && sim.BatteryLow && sim.CursorFrozen, "With their weights the three come at random times");
        foreach (var s in sim.Slimes) s.Hp = 1;
        sim.ResumeCursor();
        foreach (var s in sim.Slimes.Where(s => s.Targetable).ToList()) sim.Use(BattleSkill.Bomb, s.Id, s.X);
        Until(sim, () => sim.Phase == BattlePhase.WaveBreak, 6, catX: 1500);
        check(sim.Phase == BattlePhase.WaveBreak && sim.Captcha == null && !sim.BatteryLow && !sim.CursorFrozen, "A cleared wave takes the captcha, the low battery and the frozen cursor away");

        // The shipped data: on in chapter 2, the Dump Trail and Infinity from wave 15; never in chapter 1.
        var data = BattleData.Settings;
        var chapters = data.Campaign!.Chapters;
        check(chapters[0].Stages.All(s => s.NewAppsFrom == 0) && chapters[1].Stages.All(s => s.NewAppsFrom == 1)
              && data.Expedition!.Fights.All(f => f.NewAppsFrom == 0) && data.Expedition.Route("dump_trail")!.Fights.All(f => f.NewAppsFrom == 1)
              && data.Endless!.Stage().NewAppsFrom == 15 && data.Apps is { CaptchaWeight: > 0, BatteryWeight: > 0, CursorWeight: > 0 },
            "Shipped: the newer apps in chapter 2, the Dump Trail and Infinity from wave 15; not in chapter 1 or the first route");
    }
}
