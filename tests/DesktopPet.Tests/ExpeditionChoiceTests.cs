using DesktopPet;

// Expedition milestone 3 (docs/EXPEDITION-DIRECTION.md): rest and event nodes, relics. The choices
// drawn once from the run's seed and kept (no reroll on resume), each resolved once, their prices,
// a won elite's relics, the relic cap, the saved file, and each relic's modifier in BattleSim
// (none outside an Expedition run). Temporary files only.
internal static class ExpeditionChoiceTests
{
    const double dt = 1.0 / 60;
    private static readonly Dictionary<string, int> NoCards = new();

    private static int LaneOf(Expedition x, ExpeditionRun run, string kind) =>
        Enumerable.Range(0, run.Map[run.Step].Length).First(lane => x.Kind(run.Step, lane) == kind);

    private static void Win(Expedition x, int lane, double hp, Dictionary<string, int> build)
    {
        x.Enter(lane);
        x.Finish(true, hp, build, 30, 3);
    }

    // A seed whose step 2 event is of this kind (step 2: fight, elite, event).
    private static int SeedFor(BattleSettings data, string kind) => Enumerable.Range(1, 500).First(seed =>
        data.Expedition!.FindEvent(new Expedition(data, new ExpeditionProgress()).Start(BattleWeapon.Sword, BattleDifficulty.Normal, seed).Map[1][2])!.Kind == kind);

    // A run at step 2 (step 1 won with this HP and build), ready for its event.
    private static (Expedition X, ExpeditionRun Run) AtStep2(BattleSettings data, int seed, double hp, Dictionary<string, int> build)
    {
        var x = new Expedition(data, new ExpeditionProgress());
        var run = x.Start(BattleWeapon.Sword, BattleDifficulty.Normal, seed, "run-" + seed);
        Win(x, 0, hp, new Dictionary<string, int>(build));
        return (x, run);
    }

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var spec = data.Expedition!;
        check(spec.RulesVersion == 2 && ExpeditionProgress.CurrentVersion == 2, "Milestone 3 raises the Expedition's rules (2) and its file version (2)");
        check(spec.Events.Select(e => e.Kind).OrderBy(k => k).SequenceEqual(new[] { "chest", "shrine", "swap" }) && spec.Relics.Select(r => r.Effect).Distinct().Count() == 4 && spec.MaxRelics == 3,
            "battle.json: three events (swap, chest, shrine), four relics with different effects, at most 3 held");
        check(spec.Steps.Any(step => step.Contains(ExpeditionSpec.Rest) && step.Contains(ExpeditionSpec.Elite)) && spec.Steps.Count(step => step.Contains(ExpeditionSpec.Event)) >= 2 && spec.Steps[0].All(ExpeditionSpec.IsBattle),
            "The map has a step where resting competes with an elite, events on two steps, and a first step of battles only");

        // Rest: drawn once, kept; nothing else at that step until it is resolved.
        var (x, run) = AtStep2(data, 3, 50, new() { ["sharp"] = 1 });
        Win(x, 0, 40, new() { ["sharp"] = 1, ["thick_fur"] = 1 });
        int rest = LaneOf(x, run, ExpeditionSpec.Rest);
        int elite = LaneOf(x, run, ExpeditionSpec.Elite);
        check(x.Visit(rest) && run.Pending is { Kind: "rest" } open && open.Cards.Length is > 0 and <= 3 && open.Cards.Distinct().Count() == open.Cards.Length,
            "Visiting a rest opens its choice with up to three different cards");
        check(run.Pending!.Cards.All(id => data.UpgradeList.First(u => u.Id == id).Effect != "heal"), "A rest offers no instant heal card (resting is the heal)");
        check(x.Node(elite) == null && !x.Enter(elite) && !x.Visit(rest), "While a choice is open no other node can be fought or visited");
        var (twin, twinRun) = AtStep2(data, 3, 50, new() { ["sharp"] = 1 });
        Win(twin, 0, 40, new() { ["sharp"] = 1, ["thick_fur"] = 1 });
        twin.Visit(rest);
        check(twinRun.Pending!.Cards.SequenceEqual(run.Pending!.Cards), "The same run seed draws the same rest offer");
        check(x.Options().SequenceEqual(new[] { Expedition.Heal, Expedition.Card }) && x.Refusal(Expedition.Card) != null && !x.Choose(Expedition.Card).Done,
            "A rest offers heal or card; taking a card needs one picked");
        double max = x.MaxHp(run), before = run.Hp;
        var healed = x.Choose(Expedition.Heal);
        check(healed.Done && Math.Abs(run.Hp - Math.Min(max, before + spec.RestHeal * max)) < 1e-9 && run.Pending == null && run.Step == 3 && run.Path[2] == rest,
            $"Resting gives back {spec.RestHeal:P0} of the max HP and passes the step ({before:0} → {run.Hp:0})");
        check(!x.Choose(Expedition.Heal).Done && run.Step == 3, "A choice is resolved only once");
        string card = twinRun.Pending!.Cards[0];
        int had = twinRun.Build.GetValueOrDefault(card);
        var took = twin.Choose(Expedition.Card, card);
        check(took.Done && took.Card == card && twinRun.Build[card] == had + 1 && twinRun.Step == 3, "Taking a card at a rest adds it to the build and passes the step");

        // Swap: give one card up for one of three.
        int swapSeed = SeedFor(data, ExpeditionEventSpec.Swap);
        var (sx, sRun) = AtStep2(data, swapSeed, 60, new() { ["sharp"] = 2, ["thick_fur"] = 1 });
        check(sx.Visit(2) && sRun.Pending!.Event == "cat_trader" && sRun.Pending.Cards.Length > 0, "A swap event offers new cards");
        string pick = sRun.Pending.Cards.FirstOrDefault(c => c != "thick_fur") ?? sRun.Pending.Cards[0];
        check(sx.Refusal(Expedition.SwapOption, pick) != null && sx.Refusal(Expedition.SwapOption, pick, "big_heart") != null, "A swap needs a card of the build to give up");
        var swapped = sx.Choose(Expedition.SwapOption, pick, "thick_fur");
        check(swapped.Done && swapped.Dropped == "thick_fur" && !sRun.Build.ContainsKey("thick_fur") && sRun.Build.GetValueOrDefault(pick) >= 1 && sRun.Step == 2,
            $"Swapping drops one card ({swapped.Dropped}) and takes the picked one ({pick})");
        var (ex, eRun) = AtStep2(data, swapSeed, 60, NoCards);
        ex.Visit(2);
        check(ex.Refusal(Expedition.SwapOption, eRun.Pending!.Cards.FirstOrDefault()) != null && ex.Choose(Expedition.Leave).Done && eRun.Step == 2 && eRun.Build.Count == 0,
            "With no cards there is nothing to swap; leaving passes the step unchanged");

        // Chest: safe heal or a hidden relic for HP.
        int chestSeed = SeedFor(data, ExpeditionEventSpec.Chest);
        var (cx, cRun) = AtStep2(data, chestSeed, 60, NoCards);
        cx.Visit(2);
        check(cRun.Pending!.Relics.Length == 1 && cx.Options().SequenceEqual(new[] { Expedition.Safe, Expedition.Risk }), "A chest holds one relic; safe or risky");
        max = cx.MaxHp(cRun); before = cRun.Hp;
        double cost = cx.Cost(Expedition.Risk);
        string inside = cRun.Pending.Relics[0];
        var risked = cx.Choose(Expedition.Risk);
        check(risked.Done && risked.Relic == inside && cRun.Relics.SequenceEqual(new[] { inside }) && Math.Abs(cost - 0.15 * max) < 1e-9 && Math.Abs(cRun.Hp - Math.Min(cx.MaxHp(cRun), before - cost)) < 1e-9,
            $"The risky chest costs 15% of the max HP ({cost:0}) and gives its relic ({inside})");
        var (cx2, cRun2) = AtStep2(data, chestSeed, 60, NoCards);
        cx2.Visit(2);
        before = cRun2.Hp;
        check(cx2.Choose(Expedition.Safe).Done && Math.Abs(cRun2.Hp - (before + 0.1 * cx2.MaxHp(cRun2))) < 1e-9 && cRun2.Relics.Count == 0, "The safe chest gives 10% of the max HP back, no relic");
        var (cx3, cRun3) = AtStep2(data, chestSeed, 60, NoCards);
        cx3.Visit(2);
        cRun3.Hp = 10;
        check(cx3.Refusal(Expedition.Risk) == "too weak" && !cx3.Choose(Expedition.Risk).Done, "A price the cat cannot pay (it would drop under 1 HP) is refused");

        // Shrine: two relics shown, one taken for HP.
        int shrineSeed = SeedFor(data, ExpeditionEventSpec.Shrine);
        var (hx, hRun) = AtStep2(data, shrineSeed, 80, NoCards);
        hx.Visit(2);
        var shown = hRun.Pending!.Relics;
        check(shown.Length == 2 && shown.Distinct().Count() == 2 && hx.Refusal(Expedition.Take) != null && hx.Refusal(Expedition.Take, relic: "nope") != null,
            "A shrine shows two different relics; taking needs one of them picked");
        before = hRun.Hp;
        cost = hx.Cost(Expedition.Take);
        check(hx.Choose(Expedition.Take, relic: shown[1]).Done && hRun.Relics.SequenceEqual(new[] { shown[1] }) && Math.Abs(cost - 0.2 * hx.MaxHp(new ExpeditionRun())) < 1e-9
            && hRun.Hp <= before - cost + 1e-9, $"The shrine's relic costs 20% of the max HP ({cost:0})");

        // A won elite: relics to pick (or leave), before anything else.
        var (wx, wRun) = AtStep2(data, 5, 70, NoCards);
        wx.Enter(1);
        var won = wx.Finish(true, 60, NoCards, 40, 6);
        check(won.Reward && wRun.Pending is { Kind: "reward" } reward && reward.Relics.Length == 2 && wRun.Step == 2 && wx.Node(LaneOf(wx, wRun, ExpeditionSpec.Elite)) == null && !wx.Visit(LaneOf(wx, wRun, ExpeditionSpec.Rest)),
            "A won elite offers two relics; nothing goes on until the choice is made");
        string relicPicked = wRun.Pending!.Relics[0];
        check(wx.Choose(Expedition.Take, relic: relicPicked).Done && wRun.Relics.Contains(relicPicked) && wRun.Step == 2 && wRun.Pending == null && wx.Node(LaneOf(wx, wRun, ExpeditionSpec.Elite)) != null,
            "Taking the elite's relic keeps the step count (the battle passed it)");
        var (lx, lRun) = AtStep2(data, 5, 70, NoCards);
        lx.Enter(1); lx.Finish(true, 60, NoCards, 40, 6);
        check(lx.Choose(Expedition.Leave).Done && lRun.Relics.Count == 0 && lRun.Pending == null && lRun.Step == 2, "Leaving the elite's relics closes the choice");
        var (fx, fRun) = AtStep2(data, 5, 70, NoCards);
        fRun.Relics.AddRange(spec.Relics.Take(spec.MaxRelics).Select(r => r.Id));
        fx.Enter(1);
        check(!fx.Finish(true, 60, NoCards, 40, 6).Reward && fRun.Pending == null, $"With {spec.MaxRelics} relics held a won elite offers none");
        var (mx, mRun) = AtStep2(data, shrineSeed, 90, NoCards);
        mRun.Relics.AddRange(spec.Relics.Take(spec.MaxRelics).Select(r => r.Id));
        mx.Visit(2);
        check(mRun.Pending!.Relics.Length == 0 && mx.Refusal(Expedition.Take, relic: spec.Relics[3].Id) != null && mx.Choose(Expedition.Leave).Done, "A full run's shrine has nothing to take; it can be left");
        check(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }.All(seed =>
        {
            var (rx, rRun) = AtStep2(data, seed, 70, NoCards);
            rRun.Relics.Add(spec.Relics[0].Id);
            rx.Enter(1); rx.Finish(true, 60, NoCards, 40, 6);
            return !rRun.Pending!.Relics.Contains(spec.Relics[0].Id);
        }), "A relic already held is never offered again");

        // The file: an open choice and the relics come back as they were.
        string dir = Path.Combine(Path.GetTempPath(), "pet-expedition3-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(dir, "expedition.json");
        try
        {
            var progress = new ExpeditionProgress();
            var px = new Expedition(data, progress);
            var pRun = px.Start(BattleWeapon.Sword, BattleDifficulty.Normal, shrineSeed, "run-file");
            Win(px, 0, 70, new() { ["sharp"] = 1 });
            pRun.Relics.Add("relief_pack");
            px.Visit(2);
            var offer = pRun.Pending!.Relics.ToArray();
            check(progress.Save(file), "A run with relics and an open choice is saved");
            var read = ExpeditionProgress.Load(file);
            var back = read.Active!;
            var resumed = new Expedition(data, read);
            check(back.Relics.SequenceEqual(new[] { "relief_pack" }) && back.Pending is { Kind: "event", Lane: 2 } && back.Pending.Relics.SequenceEqual(offer) && resumed.Mismatch(back) == null,
                "Read back: the relics and the open choice with the same offer (no reroll)");
            check(resumed.Choose(Expedition.Take, relic: offer[0]).Done && back.Relics.Count == 2, "The resumed choice can be made");
            back.Relics.Add("gone_relic");
            check(resumed.Mismatch(back) is string why && why.Contains("relics"), "A run holding a relic the data no longer has does not go on");
            // A milestone 2 file: read (version 1 → 2), its run no longer fits the rules.
            File.WriteAllText(file, "{ \"Version\": 1, \"Active\": { \"RunId\": \"old\", \"Expedition\": \"slime_trail\", \"RulesVersion\": 1, \"Seed\": 1, \"Map\": [ [ \"x_meadow\", \"x_shortcut\" ] ], \"Path\": [], \"Hp\": 80, \"Build\": {} }, \"Records\": { \"slime_trail|r1|sword|Normal\": { \"Runs\": 2, \"Wins\": 1 } } }");
            var old = ExpeditionProgress.Load(file);
            check(!old.ReadOnly && old.Version == 2 && old.Active!.Relics.Count == 0 && new Expedition(data, old).Mismatch(old.Active) is string rules && rules.Contains("rules") && old.Records.Count == 1,
                "A milestone 2 file is read: its run ends (rules changed), its records stay");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        Relics(check, data);
        Validation(check);
    }

    // Each relic's modifier in a battle; none without relics.
    private static void Relics(Action<bool, string> check, BattleSettings data)
    {
        var expedition = data.Expedition!;
        RelicSpec R(string effect) => expedition.Relics.First(r => r.Effect == effect);
        BattleSettings Quick(double slimeDamage = 0, double catDamage = 10) => new()
        {
            FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 1000, SlimeDamage = slimeDamage, CatDamage = catDamage, ComboHits = 99,
            WaveBreakSeconds = 0.5, OutroSeconds = 0.5, Expedition = expedition
        };
        StageSpec stage = new() { Id = "t", Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 } } } } };
        double cat = 800;
        void Until(BattleSim sim, Func<bool> done, double seconds = 30)
        {
            for (double t = 0; t < seconds && !done(); t += dt)
            {
                var order = sim.Step(dt, cat, false);
                if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -72 * 1.8 * dt, 72 * 1.8 * dt);
            }
        }
        double FirstHit(BattleSim sim)
        {
            cat = 800;
            Until(sim, () => sim.Slimes.Count > 0 && sim.Slimes[0].Hp < sim.Slimes[0].MaxHp, 20);
            return sim.Slimes.Count > 0 ? sim.Slimes[0].MaxHp - sim.Slimes[0].Hp : 0;
        }

        var none = new BattleSim(Quick(), 0, 1600, new Random(1), stage: stage);
        var empty = new BattleSim(Quick(), 0, 1600, new Random(1), stage: stage, relics: Array.Empty<string>());
        var unknown = new BattleSim(Quick(), 0, 1600, new Random(1), stage: stage, relics: new[] { "nope" });
        check(none.Relics.Count == 0 && empty.Relics.Count == 0 && unknown.Relics.Count == 0 && none.HealAmount == data.HealAmount && none.SkillCooldown(BattleSkill.Shield) == data.SkillCooldown(BattleSkill.Shield),
            "Without relics (or with unknown ones) nothing changes: the fish and the skills' cooldowns are the settings'");
        var noRelicSettings = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0 };
        check(new BattleSim(noRelicSettings, 0, 1600, relics: new[] { "light_feather" }).CatMaxHp == noRelicSettings.CatMaxHp, "Battle data without an Expedition ignores relics");
        var adventureStage = data.Campaign!.Chapters[0].Stages[2];
        var a = AdventureBalanceTests.Play(data, adventureStage, BattleWeapon.Sword, BattleDifficulty.Normal, 7, AdventureBalanceTests.Player.Simple);
        var b = AdventureBalanceTests.Play(data, adventureStage, BattleWeapon.Sword, BattleDifficulty.Normal, 7, AdventureBalanceTests.Player.Simple, relics: Array.Empty<string>());
        check(a.Won == b.Won && a.Seconds == b.Seconds && a.Hp == b.Hp && a.Build.OrderBy(c => c.Key).SequenceEqual(b.Build.OrderBy(c => c.Key)),
            "An Adventure stage plays exactly the same with an empty relic list (same seed, same result)");

        // Light feather: faster, swings sooner, less max HP.
        var light = new BattleSim(Quick(), 0, 1600, stage: stage, relics: new[] { R("light").Id });
        check(Math.Abs(light.CatMaxHp - (data.CatMaxHp - R("light").Penalty)) < 1e-9 && light.CatHp == light.CatMaxHp && Math.Abs(light.MoveFactor - 1.15) < 1e-9 && Math.Abs(light.CooldownFactor - 0.9) < 1e-9,
            $"Light feather: max HP {light.CatMaxHp}, running ×{light.MoveFactor}, swing cooldown ×{light.CooldownFactor}");

        // Relief pack: the fish heals more, its cooldown is longer.
        var relief = new BattleSim(Quick(), 0, 1600, stage: stage, startHp: 40, relics: new[] { R("relief").Id });
        check(relief.Use(BattleSkill.Heal, 800) && Math.Abs(relief.CatHp - (40 + data.HealAmount * 1.5)) < 1e-9 && Math.Abs(relief.Cooldown(BattleSkill.Heal) - data.SkillCooldown(BattleSkill.Heal) * 1.5) < 1e-9
            && Math.Abs(relief.SkillCooldown(BattleSkill.Heal) - relief.Cooldown(BattleSkill.Heal)) < 1e-9,
            $"Relief pack: the fish heals {data.HealAmount * 1.5} and recovers in {relief.Cooldown(BattleSkill.Heal)} s (HUD total the same)");

        // Reckless charm: more damage only when low; always takes more.
        double plain = FirstHit(new BattleSim(Quick(), 0, 1600, stage: stage, startHp: 30, seed: 4));
        double low = FirstHit(new BattleSim(Quick(), 0, 1600, stage: stage, startHp: 30, seed: 4, relics: new[] { R("reckless").Id }));
        double high = FirstHit(new BattleSim(Quick(), 0, 1600, stage: stage, seed: 4, relics: new[] { R("reckless").Id }));
        check(Math.Abs(plain - 10) < 1e-9 && Math.Abs(low - 14) < 1e-9 && Math.Abs(high - 10) < 1e-9, $"Reckless charm: +40% damage below 40% HP ({low}), none above ({high})");
        cat = 800;
        var bitten = new BattleSim(Quick(slimeDamage: 10, catDamage: 0), 0, 1600, stage: stage, seed: 4, relics: new[] { R("reckless").Id });
        Until(bitten, () => bitten.CatHp < bitten.CatMaxHp, 20);
        check(Math.Abs(bitten.CatMaxHp - bitten.CatHp - 11) < 1e-9, $"Reckless charm: a 10-damage hit takes 11 ({bitten.CatMaxHp - bitten.CatHp})");

        // Counter bell: a hit the Shield stops readies a stronger next swing; the Shield recovers slower.
        cat = 800;
        var bell = new BattleSim(Quick(slimeDamage: 10, catDamage: 10), 0, 1600, stage: stage, seed: 4, relics: new[] { R("counter").Id });
        check(Math.Abs(bell.SkillCooldown(BattleSkill.Shield) - (data.SkillCooldown(BattleSkill.Shield) + 3)) < 1e-9, "Counter bell: the Shield's cooldown is 3 s longer");
        Until(bell, () => bell.Slimes.Count > 0 && bell.Slimes[0].State == SlimeState.Windup, 20);
        check(!bell.CounterReady, "Counter bell: nothing readied before a hit is stopped");
        bell.Use(BattleSkill.Shield, cat);
        check(Math.Abs(bell.Cooldown(BattleSkill.Shield) - bell.SkillCooldown(BattleSkill.Shield)) < 1e-9, "Counter bell: the Shield used now waits the longer cooldown");
        Until(bell, () => bell.CounterReady, 3);
        double hpBefore = bell.Slimes[0].Hp;
        check(bell.CounterReady && bell.CatHp == bell.CatMaxHp, "Counter bell: the Shield stopped the hit and readied the counter");
        Until(bell, () => bell.Slimes[0].Hp < hpBefore, 5);
        check(Math.Abs(hpBefore - bell.Slimes[0].Hp - 16) < 1e-9 && !bell.CounterReady && bell.CounterHits == 1, $"Counter bell: the next landed swing deals +60% ({hpBefore - bell.Slimes[0].Hp}), once");
        hpBefore = bell.Slimes[0].Hp;
        Until(bell, () => bell.Slimes[0].Hp < hpBefore, 5);
        check(Math.Abs(hpBefore - bell.Slimes[0].Hp - 10) < 1e-9, "Counter bell: the swing after is a plain one again");
    }

    private static void Validation(Action<bool, string> check)
    {
        string Error(string extra, string steps = "[ [ \"fight\" ] ]")
        {
            string enc(string id, string enemy = "slime") => $"{{ \"id\": \"{id}\", \"nameVi\": \"E\", \"nameEn\": \"E\", \"goalVi\": \"g\", \"goalEn\": \"g\", \"waves\": [ {{ \"groups\": [ {{ \"enemy\": \"{enemy}\", \"count\": 1 }} ] }} ] }}";
            string json = $"{{ \"schemaVersion\": 1, \"expedition\": {{ \"id\": \"trail\", \"nameVi\": \"T\", \"nameEn\": \"T\"{extra}, \"steps\": {steps}, \"fights\": [ {enc("f1")} ], \"elites\": [], \"boss\": {enc("b", "king")} }} }}";
            try { BattleData.Parse(json); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        const string relic = "{ \"id\": \"r1\", \"effect\": \"light\", \"amount\": 0.1, \"penalty\": 10, \"nameVi\": \"R\", \"nameEn\": \"R\", \"descVi\": \"d\", \"descEn\": \"d\" }";
        const string chest = "{ \"id\": \"e1\", \"kind\": \"chest\", \"heal\": 0.1, \"cost\": 0.1, \"nameVi\": \"E\", \"nameEn\": \"E\", \"descVi\": \"d\", \"descEn\": \"d\" }";
        check(Error($", \"relics\": [ {relic} ], \"events\": [ {chest} ]", "[ [ \"fight\", \"rest\", \"event\" ] ]") == "", "A small expedition with a rest, an event and a relic is accepted");
        check(Error($", \"events\": [ {chest} ]", "[ [ \"event\" ] ]").Contains("needs relics"), "A chest without relics in the data is rejected");
        check(Error("", "[ [ \"event\" ] ]").Contains("event"), "An event node without events is rejected");
        check(Error("", "[ [ \"rest\", \"rest\" ] ]").Contains("rest"), "Two rests on one step are rejected");
        check(Error($", \"relics\": [ {relic.Replace("light", "laser")} ]").Contains("effect"), "An unknown relic effect is rejected");
        check(Error($", \"relics\": [ {relic}, {relic} ]").Contains("used twice"), "A relic id used twice is rejected");
        check(Error($", \"relics\": [ {relic} ], \"events\": [ {chest.Replace("chest", "casino")} ]").Contains("kind"), "An unknown event kind is rejected");
        check(Error($", \"relics\": [ {relic} ], \"events\": [ {chest.Replace("0.1, \"nameVi", "0.95, \"nameVi")} ]").Contains("cost"), "An event cost above 0.9 is rejected");
        check(Error(", \"maxRelics\": 9").Contains("maxRelics"), "maxRelics above 6 is rejected");
        check(Error(", \"restHeal\": 1.5").Contains("restHeal"), "restHeal above 1 is rejected");
    }
}
