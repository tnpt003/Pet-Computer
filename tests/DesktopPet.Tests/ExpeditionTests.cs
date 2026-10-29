using DesktopPet;

// Expedition milestones 2–3 (docs/EXPEDITION-DIRECTION.md): the data, a run's map drawn once from its
// seed, HP and build carried from step to step, leaving a battle (RetreatCost), a run counted once,
// the saved file (atomic, backup, newer versions, a run that no longer fits the data) and BattleSim's
// start HP and step threat. Then the whole run in simulation (NOT real play). Temporary files only.
internal static class ExpeditionTests
{
    public static void Run(Action<bool, string> check)
    {
        var data = ExpeditionFixture.Fixed;
        var spec = data.Expedition;
        check(spec != null && spec.Steps.Length == 5 && spec.UnlockAfter == "c1_slime_king" && spec.Boss?.Id == "x_king",
            "Built-in battle.json has an Expedition: 5 steps and the boss, opened by winning the Slime King");
        var adventureIds = data.Campaign!.Chapters.SelectMany(c => c.Stages).Select(s => s.Id).ToHashSet();
        var encounters = spec!.Fights.Concat(spec.Elites).Append(spec.Boss!).ToList();
        check(encounters.Select(s => s.Id).Distinct().Count() == encounters.Count && encounters.All(s => !adventureIds.Contains(s.Id) && s.Upgrades && !s.Survival),
            "Encounters have their own ids (none an Adventure stage's), all clear stages with upgrade cards");
        check(spec.Steps.Select(step => step.Count(k => k == ExpeditionSpec.Elite)).Sum() is > 0 and < 5 && spec.Steps.All(step => step.Length is 2 or 3 && step.Any(ExpeditionSpec.IsBattle)),
            "Every step offers two or three nodes, at least one a battle, some of them elite");

        // Unlocking: the Adventure's last stage.
        var expedition = new Expedition(data, new ExpeditionProgress());
        var adventure = new AdventureProgress();
        check(!expedition.IsUnlocked(adventure), "The Expedition is locked before the Slime King is won");
        adventure.Completed.Add("c1_slime_king");
        check(expedition.IsUnlocked(adventure), "Winning the Slime King opens the Expedition");

        // The map: drawn once from the seed, the same for the same seed, each step's nodes different.
        var run = expedition.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 42, "run-a");
        var again = new Expedition(data, new ExpeditionProgress()).Start(BattleWeapon.Sword, BattleDifficulty.Normal, 42, "run-b");
        check(run.Map.Count == 6 && run.Map[^1].Single() == "x_king" && run.Map.Take(5).Select((row, i) => row.Length == spec.Steps[i].Length && row.Distinct().Count() == row.Length).All(ok => ok),
            "A run's map: 5 steps of different nodes, then the boss");
        check(run.Map.SelectMany(r => r).SequenceEqual(again.Map.SelectMany(r => r)), "The same seed draws the same map");
        bool differs = Enumerable.Range(1, 20).Any(seed => !new Expedition(data, new ExpeditionProgress()).Start(BattleWeapon.Sword, BattleDifficulty.Normal, seed).Map.SelectMany(r => r).SequenceEqual(run.Map.SelectMany(r => r)));
        check(differs, "Other seeds draw other maps");
        bool kindsFit = Enumerable.Range(0, 5).All(step => Enumerable.Range(0, spec.Steps[step].Length).All(lane => spec.Steps[step][lane] switch
        {
            ExpeditionSpec.Rest => run.Map[step][lane] == ExpeditionSpec.Rest,
            ExpeditionSpec.Event => spec.FindEvent(run.Map[step][lane]) != null,
            var kind => spec.Pool(kind).Any(s => s.Id == run.Map[step][lane])
        }));
        check(kindsFit, "Each node's encounter (or event) comes from its kind's pool");
        check(run.Hp == data.CatMaxHp && run.Path.Count == 0 && run.Fighting == null && expedition.Run == run, "A new run starts at full HP, at step 1, not fighting");

        // A node: its battle has a fixed seed and the step's threat.
        var node = expedition.Node(1)!.Value;
        check(node.Seed == expedition.Node(1)!.Value.Seed && node.Seed != expedition.Node(0)!.Value.Seed && node.Threat == 1 && expedition.Node(2) == null,
            "A node's battle seed is fixed (each node its own), step 1 has no extra threat, there is no third lane");
        var sim = new BattleSim(data, 0, 1600, difficulty: BattleDifficulty.Normal, stage: node.Stage, seed: node.Seed, startHp: 40, threat: 1.3);
        check(sim.CatHp == 40 && sim.Seed == node.Seed && Math.Abs(sim.Threat - node.Stage.Threat * 1.3) < 1e-9,
            "BattleSim starts with the given HP and multiplies the stage's threat by the step's");
        check(new BattleSim(data, 0, 1600, startHp: 500).CatHp == data.CatMaxHp && new BattleSim(data, 0, 1600, startHp: -3).CatHp == 1
            && new BattleSim(data, 0, 1600, threat: 2).Threat == 1 && new BattleSim(data, 0, 1600, stage: node.Stage).Threat == node.Stage.Threat,
            "Start HP stays within 1 and the max; a free battle has no threat; a stage without a step threat is as before");

        // Winning a step: the build and HP go on, with WinHeal back.
        check(expedition.Enter(1) && !expedition.Enter(0) && run.Fighting == 1, "Entering a node marks its battle as under way (once)");
        var build = new Dictionary<string, int> { ["big_heart"] = 1, ["sharp"] = 2 };
        var won = expedition.Finish(true, 70, build, 50, 5);
        check(won.Counted && !won.RunOver && run.Step == 1 && run.Path[0] == 1 && run.Fighting == null && run.Build["sharp"] == 2,
            "A won step moves on with its build");
        check(Math.Abs(won.MaxHp - (data.CatMaxHp + 20)) < 1e-9 && Math.Abs(run.Hp - (70 + spec.WinHeal * won.MaxHp)) < 1e-9 && Math.Abs(won.Healed - spec.WinHeal * won.MaxHp) < 1e-9,
            $"After a won step the cat keeps its HP plus {spec.WinHeal:P0} of the build's max HP ({run.Hp:0}/{won.MaxHp:0})");
        check(expedition.Finish(true, 70, build, 50, 5) == default && run.Step == 1, "A step result given twice changes nothing");
        check(expedition.Node(0)!.Value.Threat == spec.Threat(1) && spec.Threat(1) > 1, "Step 2's enemies are stronger");

        // Leaving a battle: the step stays, for RetreatCost of the max HP.
        double hp = run.Hp;
        expedition.Enter(0);
        var left = expedition.Retreat();
        check(left.Counted && !left.RunOver && run.Step == 1 && run.Fighting == null && run.Retreats == 1 && Math.Abs(run.Hp - (hp - spec.RetreatCost * won.MaxHp)) < 1e-9,
            $"Leaving a step's battle costs {spec.RetreatCost:P0} of the max HP and the step can be fought again");
        check(expedition.Retreat() == default, "Leaving when no battle is under way does nothing");
        check(expedition.Node(0)!.Value.Seed == expedition.Node(0)!.Value.Seed, "The step fought again has the same battle");

        // A loss ends the run, counted once.
        expedition.Enter(0);
        var lost = expedition.Finish(false, 0, build, 30, 2);
        var record = expedition.Record(BattleWeapon.Sword, BattleDifficulty.Normal);
        check(lost.Counted && lost.RunOver && !lost.RunWon && expedition.Run == null && record.Runs == 1 && record.Wins == 0 && record.BestSteps == 1,
            "A lost battle ends the run: one run counted, its steps won kept as the best");
        check(expedition.Progress.Runs.Contains("run-a") && expedition.Finish(false, 0, build, 1, 1) == default, "The ended run is counted once");

        // The shipped data (2026-10-23): leaving costs nothing, so even a weak cat keeps its run.
        check(spec.RetreatCost == 0, "Leaving a step's battle costs no HP in the shipped data");
        var weak = expedition.Start(BattleWeapon.Hammer, BattleDifficulty.Hard, 7, "run-weak");
        weak.Hp = 10;
        expedition.Enter(0);
        var kept = expedition.Retreat();
        check(kept.Counted && !kept.RunOver && expedition.Run == weak && weak.Hp == 10 && weak.Fighting == null,
            "Leaving a battle with little HP keeps the run, the HP and the step");
        expedition.Abandon();
        check(expedition.Record(BattleWeapon.Hammer, BattleDifficulty.Hard).Runs == 1, "Giving the run up still counts it");

        // A whole run won: the boss ends it; the record keeps the time.
        var full = expedition.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 9, "run-full");
        ExpeditionStepOutcome last = default;
        for (int step = 0; step < full.Map.Count; step++)
        {
            // A won elite's relics are left; the first battle node of each step is fought.
            if (full.Pending != null) expedition.Choose(Expedition.Leave);
            expedition.Enter(Enumerable.Range(0, full.Map[step].Length).First(lane => expedition.Node(lane) != null));
            last = expedition.Finish(true, 60, new Dictionary<string, int>(), 40, 3);
        }
        record = expedition.Record(BattleWeapon.Sword, BattleDifficulty.Normal);
        check(last.RunOver && last.RunWon && last.Fastest && last.BestSteps && expedition.Run == null && record.Wins == 1 && record.Runs == 2 && record.BestSteps == 6 && record.BestSeconds == 240,
            "Winning the boss ends the run as won: a win, 6 steps, the fastest time (fight time only)");
        expedition.Abandon();
        check(expedition.Record(BattleWeapon.Sword, BattleDifficulty.Normal).Runs == 2, "Giving up without a run does nothing");
        var given = expedition.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 10, "run-given");
        var gaveUp = expedition.Abandon();
        check(gaveUp.RunOver && !gaveUp.RunWon && expedition.Run == null && expedition.Record(BattleWeapon.Sword, BattleDifficulty.Normal).Runs == 3, "Giving a run up ends it, counted");
        check(!expedition.Progress.Records.Keys.Any(k => adventureIds.Any(id => k.StartsWith(id + "|"))), "Expedition records are kept by the Expedition's id, never an Adventure stage's");

        // The file: atomic save, the run read back as it was, broken files, newer versions.
        string dir = Path.Combine(Path.GetTempPath(), "pet-expedition-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(dir, "expedition.json");
        try
        {
            var progress = new ExpeditionProgress();
            var saved = new Expedition(data, progress);
            var keep = saved.Start(BattleWeapon.Hammer, BattleDifficulty.Easy, 1234, "run-saved");
            saved.Enter(1); saved.Finish(true, 55, new Dictionary<string, int> { ["thick_fur"] = 1 }, 33, 4);
            saved.Enter(0);
            check(progress.Save(file) && File.Exists(file) && !File.Exists(file + ".tmp"), "Expedition progress is saved (no temp file left)");
            var read = ExpeditionProgress.Load(file);
            var back = read.Active!;
            check(back.RunId == "run-saved" && back.Seed == 1234 && back.Weapon == BattleWeapon.Hammer && back.Level == BattleDifficulty.Easy && back.Path.SequenceEqual(new[] { 1 })
                && back.Build["thick_fur"] == 1 && back.Fighting == 0 && back.Map.SelectMany(r => r).SequenceEqual(keep.Map.SelectMany(r => r)) && Math.Abs(back.Hp - keep.Hp) < 1e-9,
                "The run is read back as it was: map, path, HP, build, the battle under way");
            var resumed = new Expedition(data, read);
            check(resumed.Mismatch(back) == null && resumed.Node(0)!.Value.Seed == saved.Node(0)!.Value.Seed, "A resumed run fits the data and its node battles are the same");
            // Closed or crashed during the battle: the same rule as leaving it.
            var crashed = resumed.Retreat();
            check(crashed.Counted && back.Fighting == null && back.Step == 1, "A battle under way when the file was saved is left (RetreatCost) when the run is resumed");
            read.Save(file);
            check(File.Exists(file + ".bak"), "Saving again keeps the previous file as .bak");
            File.WriteAllText(file, "{ broken");
            check(ExpeditionProgress.Load(file).Active?.RunId == "run-saved", "A broken file is read from its backup");
            File.WriteAllText(file + ".bak", "also broken");
            check(ExpeditionProgress.Load(file).Active == null, "With both broken the Expedition starts over (no crash)");
            File.WriteAllText(file, "{ \"Version\": 99, \"Active\": null, \"Records\": { \"slime_trail|r1|sword|Normal\": { \"Runs\": 4, \"Wins\": 1 } } }");
            var newer = ExpeditionProgress.Load(file);
            check(newer.ReadOnly && newer.Records.Count == 1 && !newer.Save(file) && File.ReadAllText(file).Contains("\"Version\": 99"),
                "A newer version's file is read as far as it is understood and never overwritten");

            // A saved run that no longer fits the data.
            var other = new BattleSettings { Campaign = data.Campaign, Expedition = new ExpeditionSpec
            {
                Id = spec.Id, NameVi = "x", NameEn = "x", RulesVersion = spec.RulesVersion + 1, Steps = spec.Steps, Fights = spec.Fights, Elites = spec.Elites, Boss = spec.Boss
            } };
            check(new Expedition(other, read).Mismatch(back) is string why && why.Contains("rules"), "A run saved under other rules does not go on");
            var shorter = new BattleSettings { Campaign = data.Campaign, Expedition = new ExpeditionSpec
            {
                Id = spec.Id, NameVi = "x", NameEn = "x", RulesVersion = spec.RulesVersion, Steps = spec.Steps.Take(3).ToArray(), Fights = spec.Fights, Elites = spec.Elites, Boss = spec.Boss
            } };
            check(new Expedition(shorter, read).Mismatch(back) is string changed && changed.Contains("map"), "A run whose map no longer fits does not go on");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        // Validation: a broken expedition rejects the whole file.
        string Error(string expeditionJson)
        {
            string campaign = "\"campaign\": { \"chapters\": [ { \"id\": \"ch\", \"nameVi\": \"C\", \"nameEn\": \"C\", \"stages\": [ "
                + "{ \"id\": \"a\", \"nameVi\": \"A\", \"nameEn\": \"A\", \"goalVi\": \"g\", \"goalEn\": \"g\", \"waves\": [ { \"groups\": [ { \"enemy\": \"slime\", \"count\": 1 } ] } ] } ] } ] }";
            try { BattleData.Parse("{ \"schemaVersion\": 1, " + campaign + ", \"expedition\": " + expeditionJson + " }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        string Enc(string id, string extra = "", string enemy = "slime") =>
            $"{{ \"id\": \"{id}\", \"nameVi\": \"E\", \"nameEn\": \"E\", \"goalVi\": \"g\", \"goalEn\": \"g\"{extra}, \"waves\": [ {{ \"groups\": [ {{ \"enemy\": \"{enemy}\", \"count\": 1 }} ] }} ] }}";
        string Exp(string steps = "[ [ \"fight\" ] ]", string fights = null!, string elites = "[]", string? boss = null, string extra = "") =>
            $"{{ \"id\": \"trail\", \"nameVi\": \"T\", \"nameEn\": \"T\"{extra}, \"steps\": {steps}, \"fights\": {fights ?? "[ " + Enc("f1") + " ]"}, \"elites\": {elites}, \"boss\": {boss ?? Enc("b", enemy: "king")} }}";
        check(Error(Exp()) == "", "A small valid expedition is accepted");
        check(Error(Exp(steps: "[ [ \"market\" ] ]")).Contains("kinds"), "An unknown node kind is rejected");
        check(Error(Exp(steps: "[ [ \"fight\", \"fight\" ] ]")).Contains("more fight nodes"), "A step with more nodes of a kind than its pool has is rejected");
        check(Error(Exp(fights: "[ " + Enc("a") + " ]")).Contains("used twice"), "An encounter with an Adventure stage's id is rejected");
        check(Error(Exp(boss: Enc("b"))).Contains("boss needs"), "A boss encounter without a boss is rejected");
        check(Error(Exp(fights: "[ " + Enc("f1", ", \"objective\": \"survive\", \"surviveSeconds\": 30") + " ]")).Contains("clear stage"), "A survive encounter is rejected");
        check(Error(Exp(extra: ", \"unlockAfter\": \"Bad Id\"")).Contains("unlockAfter"), "unlockAfter that is not an id is rejected");
        var open = BattleData.Parse("{ \"schemaVersion\": 1, \"expedition\": " + Exp(extra: ", \"unlockAfter\": \"nowhere\"") + " }");
        check(new Expedition(open, new ExpeditionProgress()).IsUnlocked(new AdventureProgress()), "unlockAfter naming a stage the data does not have leaves the Expedition open (a custom campaign)");
        check(Error(Exp(extra: ", \"retreatCost\": 2")).Contains("retreatCost"), "retreatCost above 1 is rejected");
        check(Error(Exp(steps: "[]")).Contains("1–10 steps"), "An expedition without steps is rejected");
        check(Error(Exp(extra: ", \"shop\": 1")) != "", "A misspelt expedition key is rejected");

        Balance(check, BattleData.Settings); // The real data: the step gacha and capsules.
        Balance(check, BattleData.Settings, "dump_trail"); // 1.5 M23: the second route.
    }

    // The whole run in simulation (NOT real play): each weapon × difficulty × player (simple: the
    // best case, casual: slow and missing some, AdventureBalanceTests) × route policy, 2 × AdventureBalanceTests.Seeds seeds.
    // Safe: rests when hurt, avoids elites, takes no risks at events. Greedy: elites and events
    // first, risky chests and shrines, cards over rests unless low. A won elite's first relic is
    // always taken. The capsule machine (2026-10-22): safe takes the free capsule, greedy the gold one
    // while at 70% HP or more. Measured, not a target yet (the user plays before the numbers are agreed);
    // checked only that it can be won and is not endless.
    public enum Policy { Safe, Greedy }
    private static readonly string[] CardOrder = { "sharp", "fast_paws", "thick_fur", "big_heart", "quick_feet", "heavy_push", "turtle_shell", "opening_strike", "running_start" };
    private static int Rank(string id) => Array.IndexOf(CardOrder, id) is int i && i >= 0 ? i : 99;

    // Which node of the step to go to.
    public static int Route(Expedition x, ExpeditionRun run, Policy policy)
    {
        double share = run.Hp / x.MaxHp(run);
        string[] order = policy == Policy.Safe
            ? share < 0.5 ? new[] { ExpeditionSpec.Rest, ExpeditionSpec.Event, ExpeditionSpec.Fight, ExpeditionSpec.Elite } : new[] { ExpeditionSpec.Fight, ExpeditionSpec.Rest, ExpeditionSpec.Event, ExpeditionSpec.Elite }
            : share < 0.35 ? new[] { ExpeditionSpec.Rest, ExpeditionSpec.Fight, ExpeditionSpec.Event, ExpeditionSpec.Elite } : new[] { ExpeditionSpec.Elite, ExpeditionSpec.Event, ExpeditionSpec.Fight, ExpeditionSpec.Rest };
        int lanes = run.Map[run.Step].Length;
        foreach (var kind in order)
            for (int lane = 0; lane < lanes; lane++)
                if (x.Kind(run.Step, lane) == kind) return lane;
        return 0;
    }

    // The open choice, resolved the policy's way.
    public static void Resolve(Expedition x, ExpeditionRun run, Policy policy)
    {
        var open = run.Pending!;
        double share = run.Hp / x.MaxHp(run);
        string? best = open.Cards.OrderBy(Rank).FirstOrDefault();
        bool Try(string option, string? card = null, string? drop = null, string? relic = null) => x.Choose(option, card, drop, relic).Done;
        if (open.Kind == ExpeditionChoice.RewardKind) { if (!Try(Expedition.Take, relic: open.Relics.FirstOrDefault())) Try(Expedition.Leave); return; }
        // The capsule machine: safe takes the free one; greedy pays for the gold one while healthy.
        if (open.Kind == ExpeditionChoice.CapsuleKind) { if (policy == Policy.Safe || share < 0.7 || !Try(Expedition.Gold)) Try(Expedition.Free); return; }
        if (open.Kind == ExpeditionChoice.RestKind)
        {
            bool heal = policy == Policy.Safe ? share < 0.7 : share < 0.35;
            if (heal || !Try(Expedition.Card, best)) Try(Expedition.Heal);
            return;
        }
        switch (x.Spec.FindEvent(open.Event)!.Kind)
        {
            case ExpeditionEventSpec.Swap:
                string? worst = run.Build.Keys.OrderByDescending(Rank).FirstOrDefault();
                if (policy == Policy.Safe || worst == null || best == null || Rank(best) >= Rank(worst) || !Try(Expedition.SwapOption, best, worst)) Try(Expedition.Leave);
                break;
            case ExpeditionEventSpec.Chest:
                if (policy == Policy.Safe || share < 0.5 || !Try(Expedition.Risk)) Try(Expedition.Safe);
                break;
            // 1.5 M23b: the forge with coins whenever it can (greedy also with HP while healthy); the curse's
            // rare card and the lost cat only for greedy while healthy.
            case ExpeditionEventSpec.Forge:
                if (!Try(Expedition.PayCoins) && (policy == Policy.Safe || share < 0.6 || !Try(Expedition.PayHp))) Try(Expedition.Leave);
                break;
            case ExpeditionEventSpec.Curse:
                if (policy == Policy.Safe || share < 0.6 || !Try(Expedition.Card, open.Cards.FirstOrDefault())) Try(Expedition.Leave);
                break;
            case ExpeditionEventSpec.Lost:
                if (policy == Policy.Safe || share < 0.5 || !Try(Expedition.Card, best)) Try(Expedition.Leave);
                break;
            default:
                if (policy == Policy.Safe || share < 0.5 || !Try(Expedition.Take, relic: open.Relics.FirstOrDefault())) Try(Expedition.Leave);
                break;
        }
    }

    private static void Balance(Action<bool, string> check, BattleSettings data, string? route = null)
    {
        var levels = new[] { BattleDifficulty.Easy, BattleDifficulty.Normal, BattleDifficulty.Hard };
        var weapons = new[] { BattleWeapon.Sword, BattleWeapon.Hammer };
        var players = new[] { AdventureBalanceTests.Player.Simple, AdventureBalanceTests.Player.Casual };
        // A run is a chain of fights, so it varies more than one stage: twice the Adventure's seeds.
        const int seeds = AdventureBalanceTests.Seeds * 2;
        Console.WriteLine($"Expedition balance{(route == null ? "" : " · " + route)} (simulation, not real play): weapon | level | player | route | runs won/{seeds} | avg steps passed | avg fight s (won runs) | battles/run | elites/run | relics at the end | HP % before the boss | HP % left (won runs)");
        var results = new Dictionary<(string, BattleDifficulty, AdventureBalanceTests.Player, Policy), (int Wins, double Steps, double Seconds)>();
        foreach (var weapon in weapons)
            foreach (var level in levels)
                foreach (var player in players)
                    foreach (var policy in new[] { Policy.Safe, Policy.Greedy })
                    {
                        int wins = 0, reachedBoss = 0; double steps = 0, seconds = 0, battles = 0, elites = 0, relics = 0, beforeBoss = 0, left = 0;
                        for (int seed = 1; seed <= seeds; seed++)
                        {
                            var expedition = new Expedition(data, new ExpeditionProgress(), route);
                            var run = expedition.Start(weapon, level, seed);
                            double fought = 0;
                            while (expedition.Run != null)
                            {
                                if (expedition.NeedsRoll) { expedition.Roll(); continue; }
                                if (run.Pending != null) { Resolve(expedition, run, policy); continue; }
                                int lane = Route(expedition, run, policy);
                                if (expedition.Node(lane) is not ExpeditionNode node)
                                {
                                    expedition.Visit(lane);
                                    continue;
                                }
                                battles++;
                                if (node.Kind == ExpeditionSpec.Elite) elites++;
                                if (node.Kind == Expedition.BossKind) { reachedBoss++; beforeBoss += run.Hp / expedition.MaxHp(run); }
                                expedition.Enter(lane);
                                var outcome = AdventureBalanceTests.Play(data, node.Stage, weapon, level, seed * 100 + run.Step, player, run.Build, run.Hp, node.Threat, node.Seed, run.Relics,
                                    weaponBonus: node.WeaponBonus, eliteBonus: node.EliteBonus);
                                fought += outcome.Seconds;
                                int held = run.Relics.Count;
                                var step = expedition.Finish(outcome.Won, outcome.Hp, outcome.Build, outcome.Seconds, 0);
                                if (step.RunOver)
                                {
                                    relics += held;
                                    steps += step.Step + (step.RunWon ? 1 : 0);
                                    if (step.RunWon) { wins++; seconds += fought; left += outcome.HpShare; }
                                }
                            }
                        }
                        results[(weapon, level, player, policy)] = (wins, steps / seeds, wins > 0 ? seconds / wins : 0);
                        Console.WriteLine($"  {weapon,-6} | {level,-6} | {player.ToString().ToLowerInvariant(),-6} | {policy.ToString().ToLowerInvariant(),-6} | {wins,2}/{seeds} | {steps / seeds,4:0.0} | {(wins > 0 ? seconds / wins : 0),5:0} | {battles / seeds,3:0.0} | {elites / seeds,3:0.0} | {relics / seeds,3:0.0} | {(reachedBoss > 0 ? beforeBoss / reachedBoss : 0),4:P0} | {(wins > 0 ? left / wins : 0),4:P0}");
                    }
        foreach (var weapon in weapons)
            foreach (var policy in new[] { Policy.Safe, Policy.Greedy })
            {
                // The 1.2.0-beta enemy buff (user, 2026-10-21: keep the buff, lower the targets): the greedy
                // route still wins half its runs; the safe one (fewer relics) is only required to stay winnable.
                int won = results[(weapon, BattleDifficulty.Normal, AdventureBalanceTests.Player.Simple, policy)].Wins;
                int least = policy == Policy.Greedy ? seeds / 2 : seeds / 10;
                check(won >= least, $"Expedition balance{(route == null ? "" : " (" + route + ")")}: {weapon}, Normal, the simple player with the {policy.ToString().ToLowerInvariant()} route wins at least {least}/{seeds} ({won}/{seeds})");
            }
        double longest = results.Values.Where(r => r.Wins > 0).Max(r => r.Seconds);
        check(longest <= 15 * 60, $"Expedition balance{(route == null ? "" : " (" + route + ")")}: a won run's fights take at most 15 minutes in simulation (longest {longest / 60:0.0} min)");
    }
}
