using DesktopPet;

// Expedition milestone 5 (docs/EXPEDITION-DIRECTION.md): whole runs end to end over the built-in data,
// through step spins (2026-10-22), capsules, rests, events, elite relics, a battle left after a crash and the boss. Each run is played
// three ways: without stopping, closing the app (saving, reading the file back) after every action,
// and clicking every action twice. All three must give the same map, offers, HP, build, relics,
// records and keepsakes. Simulated results only (NOT real play). Temporary files only.
internal static class ExpeditionReleaseTests
{
    private enum Mode { Straight, Reopen, DoubleClick }

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        string dir = Path.Combine(Path.GetTempPath(), "pet-expedition-release-" + Guid.NewGuid().ToString("N"));
        try
        {
            var seen = new HashSet<string>();
            bool same = true, doubled = true, ended = true;
            string firstDiff = "";
            // 1.5 M23: both routes.
            foreach (var route in data.Expedition!.All.Select(r => r.Id))
            foreach (var level in new[] { BattleDifficulty.Easy, BattleDifficulty.Normal, BattleDifficulty.Hard })
                for (int seed = 1; seed <= 6; seed++)
                {
                    var straight = Play(data, route, level, seed, Mode.Straight, Path.Combine(dir, "s.json"), seen, out _);
                    var reopened = Play(data, route, level, seed, Mode.Reopen, Path.Combine(dir, "r.json"), seen, out _);
                    var twice = Play(data, route, level, seed, Mode.DoubleClick, Path.Combine(dir, "d.json"), seen, out bool secondsRefused);
                    if (!straight.SequenceEqual(reopened) && firstDiff == "") firstDiff = $"{route} {level} seed {seed}: " + string.Join(" | ", straight.Zip(reopened).FirstOrDefault(p => p.First != p.Second));
                    same &= straight.SequenceEqual(reopened);
                    doubled &= straight.SequenceEqual(twice) && secondsRefused;
                    ended &= straight[^1].StartsWith("end won", StringComparison.Ordinal);
                }
            check(ended, "36 whole runs (2 routes × 3 difficulties × 6 seeds) end at the won boss");
            check(new[] { "spin", "rest heal", "rest card", "event swap", "event chest", "event shrine", "elite relic", "capsule free", "capsule gold", "retreat", "shop buy", "shop leave" }.All(seen.Contains),
                $"The runs pass spins, rests (heal and card), all three events, an elite's relic, both capsules, shops (bought from, left) and a battle left after a crash ({string.Join(", ", seen.OrderBy(s => s))})");
            check(same, "Closing the app after any action and reading the file back changes nothing: same map, offers, HP, build, relics, records, keepsakes" + (firstDiff == "" ? "" : $" ({firstDiff})"));
            check(doubled, "Every action clicked twice: the second click is refused and changes nothing");

            // Around the last save: the boss battle saved as under way, the app closed before its result.
            string file = Path.Combine(dir, "boss.json");
            var x = new Expedition(data, new ExpeditionProgress());
            var run = x.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 77, "boss-crash");
            while (run.Step < run.Map.Count - 1)
            {
                if (x.NeedsRoll) { x.Roll(); continue; }
                if (run.Pending != null) { var options = x.Options(); x.Choose(options.Contains(Expedition.Leave) ? Expedition.Leave : options.Contains(Expedition.Heal) ? Expedition.Heal : Expedition.Safe); continue; }
                int lane = Enumerable.Range(0, run.Map[run.Step].Length).FirstOrDefault(l => x.Node(l) != null, -1);
                if (lane < 0) { x.Visit(0); continue; }
                x.Enter(lane);
                x.Finish(true, run.Hp, run.Build, 20, 2);
            }
            if (run.Pending != null) x.Choose(Expedition.Leave);
            x.Enter(0);
            check(x.Progress.Save(file), "(setup) the boss battle is saved as under way");
            var back = new Expedition(data, ExpeditionProgress.Load(file));
            double hp = back.Run!.Hp, max = back.MaxHp(back.Run);
            var left = back.Retreat();
            check(left.Counted && !left.RunOver && back.Run!.Step == back.Run.Map.Count - 1 && Math.Abs(back.Run.Hp - (hp - back.Spec.RetreatCost * max)) < 1e-9,
                "Reopened after a crash in the boss battle: the battle is left (RetreatCost) and the boss stays to be fought");
            back.Enter(0);
            var won = back.Finish(true, 30, back.Run.Build, 40, 4);
            check(won.RunWon && back.Progress.Save(file) && won.Keepsakes!.Count == 3, "The boss fought again ends the run as won, with its keepsakes");
            var after = new Expedition(data, ExpeditionProgress.Load(file));
            check(after.Run == null && after.Abandon() == default && after.Finish(true, 30, new Dictionary<string, int>(), 1, 1) == default && after.Retreat() == default
                  && after.Record(BattleWeapon.Sword, BattleDifficulty.Normal) is { Runs: 1, Wins: 1 } && after.Progress.Keepsakes.Count == 3,
                "Read back after the win: nothing is left to finish, give up or leave; one run, one win, each keepsake once");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // Plays one run with a fixed policy and returns what it saw at each action.
    private static List<string> Play(BattleSettings data, string route, BattleDifficulty level, int seed, Mode mode, string file, HashSet<string> seen, out bool secondsRefused)
    {
        var trace = new List<string>();
        var x = new Expedition(data, new ExpeditionProgress(), route);
        x.Start(BattleWeapon.Sword, level, seed, $"release-{level}-{seed}");
        trace.Add("map " + string.Join("/", x.Run!.Map.Select(row => string.Join(",", row))));
        bool refused = true, crashed = false;
        // Run an action; in DoubleClick mode the same action again must do nothing; in Reopen mode
        // the progress goes through the file before the next action.
        T Act<T>(Func<Expedition, T> action, Func<T, bool> did)
        {
            var result = action(x);
            if (mode == Mode.DoubleClick)
            {
                string before = State(x);
                refused &= !did(action(x)) && State(x) == before;
            }
            if (mode == Mode.Reopen) Reopen();
            return result;
        }
        void Reopen()
        {
            if (!x.Progress.Save(file)) throw new InvalidOperationException("save failed");
            x = new Expedition(data, ExpeditionProgress.Load(file));
        }

        for (int guard = 0; guard < 40 && x.Run != null; guard++)
        {
            var run = x.Run;
            if (x.NeedsRoll)
            {
                int step = run.Step;
                Act(e => e.Roll(), ok => ok);
                seen.Add("spin");
                trace.Add($"spin {step} {string.Join(",", x.Run!.Kinds![step])} {string.Join(",", x.Run.Map[step])} | {State(x)}");
                continue;
            }
            if (run.Pending is ExpeditionChoice { Kind: ExpeditionChoice.ShopKind } shop)
            {
                // 1.5 M23: buy the first ware the run can pay for, then leave.
                trace.Add($"shop wares={string.Join(",", shop.Wares)} coins={run.Coins}");
                if (shop.Wares.FirstOrDefault(w => x.BuyRefusal(w) == null) is string ware)
                {
                    var bought = Act(e => e.Buy(ware), o => o.Done);
                    seen.Add("shop buy");
                    trace.Add($"bought {bought.Prize} hp{bought.Hp:+0.000;-0.000} | {State(x)}");
                    continue;
                }
                Act(e => e.Choose(Expedition.Leave), o => o.Done);
                seen.Add("shop leave");
                trace.Add($"left the shop | {State(x)}");
                continue;
            }
            if (run.Pending is ExpeditionChoice open)
            {
                string kind = open.Kind == ExpeditionChoice.EventKind ? x.Spec.FindEvent(open.Event)!.Kind : open.Kind;
                trace.Add($"offer {kind} cards={string.Join(",", open.Cards)} relics={string.Join(",", open.Relics)}");
                (string option, string? card, string? drop, string? relic, string label) pick = kind switch
                {
                    ExpeditionChoice.RestKind when (run.Step / 2 + seed) % 2 == 0 && open.Cards.Length > 0 => (Expedition.Card, open.Cards[0], null, null, "rest card"),
                    ExpeditionChoice.RestKind => (Expedition.Heal, null, null, null, "rest heal"),
                    ExpeditionChoice.CapsuleKind when (run.Step + seed) % 2 == 0 && x.Refusal(Expedition.Gold) == null => (Expedition.Gold, null, null, null, "capsule gold"),
                    ExpeditionChoice.CapsuleKind => (Expedition.Free, null, null, null, "capsule free"),
                    ExpeditionEventSpec.Swap when x.Refusal(Expedition.SwapOption, open.Cards.FirstOrDefault(), run.Build.Keys.OrderBy(k => k).FirstOrDefault()) == null =>
                        (Expedition.SwapOption, open.Cards[0], run.Build.Keys.OrderBy(k => k).First(), null, "event swap"),
                    ExpeditionEventSpec.Swap => (Expedition.Leave, null, null, null, "event leave"),
                    ExpeditionEventSpec.Chest when x.Refusal(Expedition.Risk) == null => (Expedition.Risk, null, null, null, "event chest"),
                    ExpeditionEventSpec.Chest => (Expedition.Safe, null, null, null, "event chest"),
                    _ when x.Refusal(Expedition.Take, relic: open.Relics.FirstOrDefault()) == null =>
                        (Expedition.Take, null, null, open.Relics[0], kind == ExpeditionChoice.RewardKind ? "elite relic" : "event shrine"),
                    _ => (Expedition.Leave, null, null, null, kind == ExpeditionChoice.RewardKind ? "elite leave" : "event leave")
                };
                seen.Add(pick.label);
                var chosen = Act(e => e.Choose(pick.option, pick.card, pick.drop, pick.relic), o => o.Done);
                trace.Add($"chose {chosen.Option} hp{chosen.Hp:+0.000;-0.000} card={chosen.Card} drop={chosen.Dropped} relic={chosen.Relic} prize={chosen.Prize} | {State(x)}");
                continue;
            }
            // Even seeds go to events, most seeds rest; otherwise prefer the elite, so the runs pass every kind.
            string[] order = seed % 2 == 0 ? new[] { ExpeditionSpec.Shop, ExpeditionSpec.Event, ExpeditionSpec.Rest, ExpeditionSpec.Elite, ExpeditionSpec.Fight }
                : seed % 3 != 0 ? new[] { ExpeditionSpec.Rest, ExpeditionSpec.Elite, ExpeditionSpec.Event, ExpeditionSpec.Fight }
                : new[] { ExpeditionSpec.Elite, ExpeditionSpec.Fight, ExpeditionSpec.Event, ExpeditionSpec.Rest };
            int lanes = run.Map[run.Step].Length;
            int lane = order.Select(kind => Enumerable.Range(0, lanes).FirstOrDefault(l => x.Kind(run.Step, l) == kind, -1)).FirstOrDefault(l => l >= 0, 0);
            if (x.Node(lane) == null)
            {
                Act(e => e.Visit(lane), ok => ok);
                trace.Add($"visit {run.Step}:{lane}");
                continue;
            }
            var node = x.Node(lane)!.Value;
            trace.Add($"fight {node.Step}:{lane} {node.Stage.Id} seed={node.Seed} threat={node.Threat:0.00}");
            Act(e => e.Enter(lane), ok => ok);
            if (!crashed && run.Step == 1)
            {
                // The app closed during this battle: reopened, the battle is left (the map does this).
                crashed = true;
                Reopen();
                var left = Act(e => e.Retreat(), o => o.Counted);
                seen.Add("retreat");
                trace.Add($"left hp={left.Hp:0.000} | {State(x)}");
                Act(e => e.Enter(lane), ok => ok);
            }
            // A deterministic battle result: 80% of the HP kept, one more card from the battle.
            var build = new Dictionary<string, int>(x.Run!.Build);
            string card = data.UpgradeList[(run.Step + seed) % data.UpgradeList.Count].Id;
            build[card] = build.GetValueOrDefault(card) + 1;
            double hp = x.Run.Hp * 0.8;
            var done = Act(e => e.Finish(true, hp, build, 30 + run.Step, 3, coins: 3), o => o.Counted);
            trace.Add(done.RunOver
                ? $"end {(done.RunWon ? "won" : "lost")} keepsakes={string.Join(",", done.Keepsakes ?? Array.Empty<string>())} best={done.BestSteps} fastest={done.Fastest} records={Records(x)}"
                : $"won hp={done.Hp:0.000} healed={done.Healed:0.000} reward={done.Reward} | {State(x)}");
        }
        secondsRefused = refused;
        if (x.Run != null) trace.Add("end unfinished");
        return trace;
    }

    private static string State(Expedition x) => x.Run is not ExpeditionRun run ? "no run" :
        $"step={run.Step} path={string.Join(",", run.Path)} hp={run.Hp:0.000} fighting={run.Fighting} retreats={run.Retreats} build={string.Join(",", run.Build.OrderBy(c => c.Key).Select(c => c.Key + "×" + c.Value))} "
        + $"relics={string.Join(",", run.Relics)} pending={run.Pending?.Kind}:{string.Join(",", run.Pending?.Cards ?? Array.Empty<string>())}:{string.Join(",", run.Pending?.Relics ?? Array.Empty<string>())}:{run.Pending?.Free}:{run.Pending?.Gold} "
        + $"kinds={string.Join("/", (run.Kinds ?? new()).Select(k => string.Join(",", k)))} coins={run.Coins} sold={string.Join(",", run.Pending?.Sold ?? new())}";

    private static string Records(Expedition x) =>
        string.Join(";", x.Progress.Records.OrderBy(r => r.Key).Select(r => $"{r.Key}={r.Value.Runs}/{r.Value.Wins}/{r.Value.BestSteps}/{r.Value.BestSeconds}")) + " kept=" + string.Join(",", x.Progress.Keepsakes);
}
