using DesktopPet;

// The Expedition's step gacha and capsule machine (2026-10-22) over the built-in data: each step's
// node kinds drawn when the run gets there (by weight, with the rules that always hold), drawn
// once from the run's seed and kept (a second spin, a save and reload or another time never draws
// again); after a won fight node the capsule machine (both capsules drawn at the win, hidden, a
// free one and a gold one for HP). Simulated draws only, not real play. Temporary files only.
internal static class ExpeditionGachaTests
{
    // Moves the run past its current step (spin, the first node or a given one, a win, every choice left).
    private static void Pass(Expedition x, int lane = 0)
    {
        var run = x.Run!;
        if (x.NeedsRoll) x.Roll();
        lane = Math.Min(lane, run.Map[run.Step].Length - 1);
        if (x.Node(lane) is not null) { x.Enter(lane); x.Finish(true, run.Hp, run.Build, 10, 1); }
        else { x.Visit(lane); Resolve(x); }
        while (x.Run?.Pending != null) Resolve(x);
    }

    private static void Resolve(Expedition x)
    {
        var options = x.Options();
        x.Choose(options.Contains(Expedition.Leave) ? Expedition.Leave : options.Contains(Expedition.Heal) ? Expedition.Heal : Expedition.Safe);
    }

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var spec = data.Expedition!;
        check(spec.KindGacha is { Steps: 5, MinLanes: 2, MaxLanes: 3 } && spec.Steps.Length == 0 && spec.StepCount == 6 && spec.Capsules != null && spec.RulesVersion == 5,
            "Built-in Expedition: 5 steps drawn by the step gacha (2–3 nodes), then the boss; the capsule machine; rules version 5");

        // A new run: only the boss is known; nothing can be fought or visited before the spin.
        var x = new Expedition(data, new ExpeditionProgress());
        var run = x.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 42, "gacha-a");
        check(run.Map.Count == 6 && run.Map.Take(5).All(row => row.Length == 0) && run.Map[^1].Single() == spec.Boss!.Id && run.Kinds is { Count: 5 } && run.Kinds.All(k => k.Length == 0),
            "A new run knows only the boss: every step before it is still to be drawn");
        check(x.NeedsRoll && x.Kind(0, 0) == "" && !x.Rolled(0) && x.Rolled(5) && x.Node(0) == null && !x.Visit(0), "Step 1 waits for its spin; nothing can be fought or visited yet");
        check(x.Roll() && !x.Roll() && !x.NeedsRoll && x.Rolled(0) && !x.Rolled(1), "The spin draws step 1 once; a second spin draws nothing");
        var first = run.Kinds[0];
        check(first.Length is 2 or 3 && run.Map[0].Length == first.Length && first.All(k => k is ExpeditionSpec.Fight or ExpeditionSpec.Event) && run.Map[0].Distinct().Count() == first.Length,
            $"Step 1 has 2–3 different nodes, fights and events only ({string.Join(",", first)})");
        var again = new Expedition(data, new ExpeditionProgress());
        again.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 42, "gacha-b");
        again.Roll();
        check(again.Run!.Kinds![0].SequenceEqual(first) && again.Run.Map[0].SequenceEqual(run.Map[0]), "The same seed draws the same step");

        // Saved and read back: the drawn step stays; the next one is drawn the same as without the file.
        string dir = Path.Combine(Path.GetTempPath(), "pet-expedition-gacha-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "expedition.json");
            Pass(x);
            check(x.Progress.Save(file), "(setup) saved after step 1");
            var back = new Expedition(data, ExpeditionProgress.Load(file));
            check(back.Mismatch(back.Run!) == null && back.Run!.Kinds![0].SequenceEqual(first) && back.NeedsRoll, "Read back: step 1 as drawn, step 2 still to spin");
            back.Roll(); x.Roll();
            check(back.Run.Kinds[1].SequenceEqual(x.Run!.Kinds![1]) && back.Run.Map[1].SequenceEqual(x.Run.Map[1]), "Step 2 draws the same after a save and reload");
            check(new Expedition(ExpeditionFixture.Fixed, ExpeditionProgress.Load(file)).Mismatch(ExpeditionProgress.Load(file).Active!) != null,
                "A drawn run does not go on with data whose map is fixed (it ends with a note)");
            var tampered = ExpeditionProgress.Load(file);
            tampered.Active!.Kinds![0] = new[] { ExpeditionSpec.Elite, ExpeditionSpec.Elite };
            check(new Expedition(data, tampered).Mismatch(tampered.Active) != null, "A step whose nodes do not fit their kinds is refused");
            var unrolled = ExpeditionProgress.Load(file);
            unrolled.Active!.Fighting = 0;
            check(new Expedition(data, unrolled).Mismatch(unrolled.Active) != null, "A battle under way at a step not drawn is refused");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        // The rules over many runs (each step passed by its first node).
        bool firstOk = true, oneRest = true, notOnlyElites = true, restBeforeBoss = true, lanesOk = true, distinct = true, fits = true;
        var counts = new Dictionary<string, int>();
        for (int seed = 1; seed <= 150; seed++)
        {
            var g = new Expedition(data, new ExpeditionProgress());
            var r = g.Start(BattleWeapon.Hammer, BattleDifficulty.Easy, seed);
            for (int step = 0; step < 5 && g.Run != null; step++)
            {
                Pass(g, seed % 3);
                var kinds = r.Kinds![step];
                foreach (var k in kinds) counts[k] = counts.GetValueOrDefault(k) + 1;
                firstOk &= step != 0 || kinds.All(k => k is ExpeditionSpec.Fight or ExpeditionSpec.Event);
                oneRest &= kinds.Count(k => k == ExpeditionSpec.Rest) <= 1;
                notOnlyElites &= kinds.Any(k => k != ExpeditionSpec.Elite);
                restBeforeBoss &= step != 4 || kinds.Contains(ExpeditionSpec.Rest);
                lanesOk &= kinds.Length is 2 or 3;
                distinct &= r.Map[step].Where(id => id != ExpeditionSpec.Rest).Distinct().Count() == r.Map[step].Count(id => id != ExpeditionSpec.Rest);
                fits &= g.Mismatch(r) == null;
            }
        }
        check(firstOk && oneRest && notOnlyElites && restBeforeBoss && lanesOk && distinct && fits,
            $"150 runs: step 1 without elite or rest, at most one rest a step, never only elites, a rest before the boss, 2–3 nodes, no node twice in a step ({firstOk} {oneRest} {notOnlyElites} {restBeforeBoss} {lanesOk} {distinct} {fits})");
        int total = counts.Values.Sum();
        check(ExpeditionSpec.Kinds.All(counts.ContainsKey) && counts[ExpeditionSpec.Fight] == counts.Values.Max(),
            "Every kind comes up; fights most often (" + string.Join(", ", counts.OrderByDescending(c => c.Value).Select(c => $"{c.Key} {100.0 * c.Value / total:0}%")) + ")");
        Console.WriteLine("Expedition step gacha, 150 runs: " + string.Join(", ", counts.OrderByDescending(c => c.Value).Select(c => $"{c.Key} {100.0 * c.Value / total:0.0}%")));

        Capsules(check, data);
        DataChecks(check);
    }

    private static void Capsules(Action<bool, string> check, BattleSettings data)
    {
        var spec = data.Expedition!;
        var c = spec.Capsules!;
        // A run whose step 1 has a fight: its win leaves the capsule machine.
        Expedition? x = null; int lane = -1;
        for (int seed = 1; lane < 0; seed++)
        {
            x = new Expedition(data, new ExpeditionProgress());
            x.Start(BattleWeapon.Sword, BattleDifficulty.Normal, seed);
            x.Roll();
            lane = Array.IndexOf(x.Run!.Kinds![0], ExpeditionSpec.Fight);
        }
        var run = x!.Run!;
        x.Enter(lane);
        var won = x.Finish(true, run.Hp * 0.5, run.Build, 20, 2);
        check(won.Capsule && !won.Reward && run.Pending is { Kind: ExpeditionChoice.CapsuleKind } open && open.Free != null && open.Gold != null && run.Path.Count == 1,
            "A won fight leaves the capsule machine (both capsules drawn now); the step is passed");
        check(x.Options().SequenceEqual(new[] { Expedition.Free, Expedition.Gold, Expedition.Leave }) && Math.Abs(x.Cost(Expedition.Gold) - c.GoldCost * x.MaxHp(run)) < 1e-9 && x.Cost(Expedition.Free) == 0,
            "Free capsule, gold capsule (GoldCost of the max HP) or skip");
        check(!x.NeedsRoll && x.Node(0) == null, "Nothing goes on (no spin, no battle) until the capsule is chosen");
        double hp = run.Hp, max = x.MaxHp(run);
        string gold = run.Pending!.Gold!;
        int cards = run.Build.Values.Sum(), relics = run.Relics.Count;
        var got = x.Choose(Expedition.Gold);
        var (kind, id) = Expedition.Prize(gold);
        bool applied = kind switch
        {
            Expedition.PrizeRelic => run.Relics.Count == relics + 1 && run.Relics.Contains(id!),
            Expedition.PrizeRare or Expedition.PrizeCard => run.Build.Values.Sum() == cards + 1 && run.Build.ContainsKey(id!),
            _ => true
        };
        double heal = kind == Expedition.PrizeCard ? c.GoldHeal * max : 0;
        bool hpOk = id == "big_heart" || Math.Abs(run.Hp - Math.Min(max, hp - c.GoldCost * max + heal)) < 1e-6;
        check(got.Done && got.Prize == gold && applied && hpOk && run.Pending == null && run.Path.Count == 1 && x.NeedsRoll,
            $"The gold capsule costs its HP and gives its prize ({gold}); then step 2 waits for its spin");
        check(!x.Choose(Expedition.Gold).Done && !x.Choose(Expedition.Free).Done, "A second pick (double click) does nothing");

        // Many wins: what the capsules hold, the same for the same seed, never more than the run may take.
        var free = new Dictionary<string, int>(); var golds = new Dictionary<string, int>();
        var drawn = new Dictionary<int, (string?, string?)>();
        bool same = true, fullNoRelic = true, rareOk = true, weakRefused = true;
        for (int seed = 1; seed <= 120; seed++)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var g = new Expedition(data, new ExpeditionProgress());
                var r = g.Start(BattleWeapon.Hammer, BattleDifficulty.Hard, seed);
                g.Roll();
                int fight = Array.IndexOf(r.Kinds![0], ExpeditionSpec.Fight);
                if (fight < 0) break;
                // Every second seed: a run already full of relics and holding a rare card.
                if (seed % 2 == 0) { r.Relics.AddRange(spec.Relics.Take(spec.MaxRelics).Select(rel => rel.Id)); r.Build["turtle_shell"] = 1; }
                g.Enter(fight);
                g.Finish(true, seed % 5 == 0 ? 2 : r.Hp, r.Build, 10, 1);
                var p = r.Pending!;
                if (pass == 1) { same &= drawn[seed] == (p.Free, p.Gold); continue; }
                drawn[seed] = (p.Free, p.Gold);
                free[Expedition.Prize(p.Free).Kind] = free.GetValueOrDefault(Expedition.Prize(p.Free).Kind) + 1;
                golds[Expedition.Prize(p.Gold).Kind] = golds.GetValueOrDefault(Expedition.Prize(p.Gold).Kind) + 1;
                if (seed % 2 == 0) { fullNoRelic &= Expedition.Prize(p.Gold).Kind != Expedition.PrizeRelic; rareOk &= p.Gold != "rare:turtle_shell"; }
                if (seed % 5 == 0) weakRefused &= g.Refusal(Expedition.Gold) == "too weak" && g.Refusal(Expedition.Free) == null;
            }
        }
        check(same, "The same seed's win holds the same capsules");
        check(free.GetValueOrDefault(Expedition.Heal) > 0 && free.GetValueOrDefault(Expedition.PrizeCard) > 0 && free.Keys.All(k => !k.StartsWith("rare") && !k.StartsWith("relic")),
            $"The free capsule holds HP or a common card, never a rare card or a relic (heal {free.GetValueOrDefault(Expedition.Heal)}, card {free.GetValueOrDefault(Expedition.PrizeCard)})");
        Console.WriteLine($"Expedition capsules, 120 seeds (half of them full of relics): free heal {free.GetValueOrDefault(Expedition.Heal)} card {free.GetValueOrDefault(Expedition.PrizeCard)}; gold relic {golds.GetValueOrDefault(Expedition.PrizeRelic)} rare {golds.GetValueOrDefault(Expedition.PrizeRare)} card+HP {golds.GetValueOrDefault(Expedition.PrizeCard)}");
        check(golds.GetValueOrDefault(Expedition.PrizeRelic) > 0 && golds.GetValueOrDefault(Expedition.PrizeRare) > 0 && golds.GetValueOrDefault(Expedition.PrizeCard) > 0,
            $"The gold capsule holds relics, rare cards and cards with HP (relic {golds.GetValueOrDefault(Expedition.PrizeRelic)}, rare {golds.GetValueOrDefault(Expedition.PrizeRare)}, card {golds.GetValueOrDefault(Expedition.PrizeCard)})");
        check(fullNoRelic && rareOk, "A run full of relics gets no relic from it; a rare card held as often as it may be is not offered again");
        check(weakRefused, "A cat too weak to pay cannot take the gold capsule; the free one stays open");

        // The capsule after an elite or the boss: none (an elite leaves relics, the boss ends the run).
        var e = new Expedition(data, new ExpeditionProgress());
        var er = e.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 3);
        int elite = -1;
        for (int guard = 0; guard < 6 && e.Run != null && er.Step < 5 && elite < 0; guard++)
        {
            if (e.NeedsRoll) e.Roll();
            elite = Array.IndexOf(er.Kinds![er.Step], ExpeditionSpec.Elite);
            if (elite < 0) Pass(e);
        }
        if (elite >= 0)
        {
            e.Enter(elite);
            var won2 = e.Finish(true, er.Hp, er.Build, 10, 1);
            check(!won2.Capsule && er.Pending?.Kind != ExpeditionChoice.CapsuleKind, "A won elite leaves no capsule machine");
        }
    }

    private static void DataChecks(Action<bool, string> check)
    {
        string builtIn = ExpeditionFixture.Embedded();
        string Error(string expedition)
        {
            try { BattleData.ParseWithOverride(builtIn, "{ \"schemaVersion\": 1, \"expedition\": " + expedition + " }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        check(ExpeditionFixture.Fixed.Expedition is { KindGacha: null, Steps.Length: 5, Capsules: null }, "A user battle.json giving \"steps\" replaces the step gacha (and can turn the capsules off)");
        check(Error("{ \"kindGacha\": { \"steps\": 3, \"weights\": { \"fight\": 1 } } }") == "", "A user battle.json can set its own step gacha");
        check(Error("{ \"kindGacha\": { \"weights\": { \"fight\": 0, \"rest\": 5 } } }").Contains("fight"), "A step gacha without fights is rejected");
        check(Error("{ \"kindGacha\": { \"minLanes\": 3, \"maxLanes\": 2 } }").Contains("lanes"), "minLanes above maxLanes is rejected");
        check(Error("{ \"kindGacha\": { \"weights\": { \"shop\": 3, \"fight\": 1 } } }").Contains("kinds"), "An unknown node kind in the weights is rejected");
        check(Error("{ \"capsules\": { \"goldCost\": 1 } }").Contains("goldCost"), "A gold capsule costing all the HP is rejected");
        check(Error("{ \"capsules\": { \"relic\": 0, \"rare\": 0, \"cardHeal\": 0 } }").Contains("gold weight"), "A gold capsule with nothing in it is rejected");
    }
}
