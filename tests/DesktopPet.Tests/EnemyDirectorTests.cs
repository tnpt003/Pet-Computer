using DesktopPet;

// Enemy Director milestone 1: a seeded battle split into streams, waves drawn from weights (with
// guarantees), squads that come together once there is room, threat on HP/damage worked out at
// spawn, and a survive stage's endless stream with timed card offers.
internal static class EnemyDirectorTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id, double hp = 50, double damage = 10, double scale = 1, bool support = false) => new()
    {
        Id = id, MaxHp = hp, Speed = 0, Damage = damage, Range = 64, Windup = 0.8, AttackCooldown = 1.8, Recover = 0.35, Scale = scale,
        Support = support, HealAmount = support ? 10 : 0
    };

    private static readonly EnemyType[] Kinds = { Kind("slime"), Kind("runner", 25, 6, 0.75), Kind("rock", 150, 16, 1.35), Kind("mender", 40, 5, 0.9, true) };

    // A battle the cat cannot hurt (catDamage 0) on a wide floor, with one stage of these waves.
    private static BattleSim Battle(StageSpec stage, int seed = 1, int maxAlive = 3, BattleDifficulty level = BattleDifficulty.Normal) =>
        new(new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0.2, CatDamage = 0, CatMaxHp = 1_000_000, MaxAlive = maxAlive, Enemies = Kinds },
            0, 1600, difficulty: level, stage: stage, seed: seed);

    private static StageSpec Stage(params WaveSpec[] waves) => new() { Id = "t", Waves = waves };
    private static WaveSpec Wave(params WaveGroup[] groups) => new() { Groups = groups };

    private static void Run(BattleSim sim, double seconds, double catX = 800, Action? each = null)
    {
        for (double t = 0; t < seconds && !sim.Over; t += dt) { sim.Step(dt, catX, false); each?.Invoke(); sim.ClearEvents(); }
    }

    public static void Run(Action<bool, string> check)
    {
        RandomChecks(check);
        PickChecks(check);
        SquadChecks(check);
        ThreatChecks(check);
        StreamChecks(check);
        DataChecks(check);
        ShippedChecks(check);
    }

    private static void RandomChecks(Action<bool, string> check)
    {
        check(Enumerable.Range(0, 5).Select(_ => BattleRandom.Stream(42, BattleRandom.Spawn).Next()).Distinct().Count() == 1 &&
              BattleRandom.Stream(42, BattleRandom.Spawn).Next() != BattleRandom.Stream(42, BattleRandom.Offer).Next() &&
              BattleRandom.Stream(42, BattleRandom.Spawn).Next() != BattleRandom.Stream(43, BattleRandom.Spawn).Next(),
            "A seed and stream always give the same numbers; streams and seeds differ from each other");
        var stage = Stage(Wave(new WaveGroup { Pick = new() { ["slime"] = 1, ["runner"] = 1, ["rock"] = 1 }, Count = 3 }));
        string Replay(int seed)
        {
            var sim = Battle(stage, seed);
            Run(sim, 3);
            return string.Join(",", sim.Slimes.Select(s => $"{s.Type.Id}@{s.X:0}"));
        }
        check(Replay(7) == Replay(7) && Battle(stage, 7).Seed == 7, "The same seed replays the same spawns at the same places (and Seed tells it)");
        check(Enumerable.Range(1, 12).Select(Replay).Distinct().Count() > 3, "Different seeds give different battles");
        var fromRandom = new BattleSim(new BattleSettings(), 0, 1600, new Random(5));
        check(fromRandom.Seed == new Random(5).Next(), "A battle given a Random takes its seed from it (old tests stay repeatable)");
    }

    private static void PickChecks(Action<bool, string> check)
    {
        // Weights: about their share over many draws; order in the file does not matter.
        var weights = new Dictionary<string, double> { ["slime"] = 1, ["runner"] = 3 };
        var reversed = new Dictionary<string, double> { ["runner"] = 3, ["slime"] = 1 };
        var random = new Random(9);
        int runners = Enumerable.Range(0, 4000).Count(_ => SpawnDirector.Pick(weights, random) == "runner");
        check(runners is > 2800 and < 3200, $"Weighted draws follow their weights (runner 3:1 → {runners / 40.0:0}%)");
        check(Enumerable.Range(0, 50).All(i => SpawnDirector.Pick(weights, new Random(i)) == SpawnDirector.Pick(reversed, new Random(i))),
            "A draw does not depend on the order the weights are listed in");
        check(SpawnDirector.Pick(new Dictionary<string, double> { ["slime"] = 0, ["rock"] = 2 }, new Random(1)) == "rock", "A kind with weight 0 is never drawn");

        // AtLeast: whatever the draw, the guaranteed kind is there.
        var group = new WaveGroup { Pick = new() { ["slime"] = 9, ["runner"] = 1 }, Count = 3, AtLeast = new() { ["runner"] = 1 } };
        var wave = Wave(group);
        var draws = Enumerable.Range(0, 300).Select(i => SpawnDirector.Units(wave, new Random(i)).Select(u => u.Kinds[0]).ToList()).ToList();
        check(draws.All(d => d.Count == 3 && d.Contains("runner")), "atLeast: the guaranteed kind is in every draw");
        check(draws.Select(d => d.IndexOf("runner")).Distinct().Count() == 3, "The guaranteed one is not always in the same place");
        var two = Wave(new WaveGroup { Pick = new() { ["slime"] = 1 }, Count = 2, AtLeast = new() { ["runner"] = 1, ["rock"] = 1 } });
        check(Enumerable.Range(0, 20).All(i => SpawnDirector.Units(two, new Random(i)).Select(u => u.Kinds[0]).OrderBy(k => k).SequenceEqual(new[] { "rock", "runner" })),
            "Several guarantees filling the whole count are all met");
    }

    private static void SquadChecks(Action<bool, string> check)
    {
        // A squad of a herb slime and three rocks comes in the same frame, the support at the back.
        var squad = new WaveGroup { Squad = new[] { new WaveGroup { Enemy = "mender", Count = 1 }, new WaveGroup { Enemy = "rock", Count = 3 } } };
        bool together = true, behind = true, apart = true;
        for (int seed = 1; seed <= 12; seed++)
            foreach (double cat in new[] { 800.0, 300, 1300, 60 })
            {
                var sim = Battle(Stage(Wave(squad)), seed, 5);
                int seen = 0;
                Run(sim, 1, cat, () => { if (sim.Slimes.Count != seen) { together &= seen == 0 && sim.Slimes.Count == 4; seen = sim.Slimes.Count; } });
                var mender = sim.Slimes.Single(s => s.Type.Id == "mender");
                behind &= sim.Slimes.Where(s => s != mender).All(r => Math.Abs(r.X - cat) < Math.Abs(mender.X - cat));
                apart &= sim.Slimes.All(s => Math.Abs(s.X - cat) >= 60);
            }
        check(together, "A squad appears all at once (4 enemies in one frame)");
        check(behind, "The squad's support stands behind its fighters, away from the cat");
        check(apart, "A squad never lands on the cat");

        // Reservation: with room for 5 and 2 slimes still up, the squad waits, and the slime after it waits too.
        var stage = Stage(Wave(new WaveGroup { Enemy = "slime", Count = 2 }, squad, new WaveGroup { Enemy = "runner", Count = 1 }));
        var waiting = Battle(stage, 3, 5);
        Run(waiting, 3);
        check(waiting.Alive == 2 && waiting.ToSpawn == 5 && waiting.Slimes.All(s => s.Type.Id == "slime"), "A squad waits for room for all of it; nothing behind it jumps the queue");
        // One slime down: room for 4, so the squad comes (and the runner still waits).
        var first = waiting.Slimes[0];
        first.Hp = 0; first.Set(SlimeState.Defeated);
        Run(waiting, 2);
        check(waiting.Alive == 5 && waiting.ToSpawn == 1 && waiting.Slimes.Count(s => s.Type.Id == "rock") == 3, "Once there is room the squad comes, then the rest");
    }

    private static void ThreatChecks(Action<bool, string> check)
    {
        var slime = Wave(new WaveGroup { Enemy = "slime", Count = 1 });
        Slime First(BattleSim sim) { Run(sim, 1); return sim.Slimes[0]; }
        var plain = First(Battle(Stage(slime)));
        check(plain.MaxHp == 50 && plain.Damage == 10, "Threat 1 on Normal leaves the numbers as they are");
        var strong = First(Battle(new StageSpec { Id = "t", Threat = 1.6, Waves = new[] { slime } }));
        check(strong.MaxHp == 80 && strong.Damage == 13, $"Threat 1.6: HP ×1.6, damage only ×1.3 (got {strong.MaxHp}/{strong.Damage})");
        var both = First(Battle(new StageSpec { Id = "t", Threat = 1.25, Waves = new[] { new WaveSpec { Threat = 1.28, Groups = slime.Groups } } }, level: BattleDifficulty.Hard));
        check(both.MaxHp == 104 && both.Damage == 18, $"Difficulty, stage threat and wave threat multiply (Hard × 1.25 × 1.28: {both.MaxHp}/{both.Damage})");
        var free = new BattleSim(new BattleSettings { FirstWaveDelay = 0, Enemies = Kinds, Plan = new[] { new WaveSpec { Threat = 2, Groups = slime.Groups } } }, 0, 1600, seed: 1);
        check(First(free).MaxHp == 50, "A free battle has no threat");
        check(strong.Type.Windup == 0.8 && BattleData.Settings.StunDurationSeconds == 1.5, "Threat changes no timings and no stun");
    }

    private static void StreamChecks(Action<bool, string> check)
    {
        var stage = new StageSpec
        {
            Id = "t", Objective = "survive", SurviveSeconds = 60, Upgrades = true, MaxUpgrades = 3, UpgradeAt = new[] { 15.0, 30, 45 }, ThreatGrowth = 1,
            Waves = new[] { new WaveSpec { Stream = new StreamSpec { Weights = new() { ["runner"] = 2, ["slime"] = 1 }, Interval = 3, IntervalEnd = 1 } } }
        };
        var sim = new BattleSim(new BattleSettings { FirstWaveDelay = 0, CatDamage = 0, CatMaxHp = 1_000_000, Enemies = Kinds,
            Upgrades = BattleData.Settings.UpgradeList.Where(u => u.Effect != "heal").ToArray() }, 0, 1600, stage: stage, seed: 4);
        int most = 0, offers = 0, spawns = 0;
        var spawnTimes = new List<double>();
        var hpAt = new List<(double Clock, double Hp, string Kind)>();
        bool clockStill = true;
        for (double t = 0; t < 200 && !sim.Over; t += dt)
        {
            if (sim.ChoosingUpgrade)
            {
                offers++;
                double clock = sim.SurviveRemaining;
                sim.Step(dt, 800, false);
                clockStill &= sim.SurviveRemaining == clock;
                sim.ChooseUpgrade(sim.UpgradeOffer![0].Id, 800);
            }
            // Clear the floor now and then, so the stream keeps drawing.
            if (sim.Alive >= 3 && (int)(t * 60) % 300 == 0) foreach (var s in sim.Slimes) if (s.Targetable) { s.Hp = 0; s.Set(SlimeState.Defeated); }
            sim.Step(dt, 800, false);
            foreach (var e in sim.Events.Where(e => e.Kind == BattleEventKind.SlimeSpawned))
            {
                spawns++;
                spawnTimes.Add(60 - sim.SurviveRemaining);
                var newest = sim.Slimes[^1];
                hpAt.Add((60 - sim.SurviveRemaining, newest.MaxHp, newest.Type.Id));
            }
            sim.ClearEvents();
            most = Math.Max(most, sim.Alive);
        }
        check(sim.Phase == BattlePhase.Won && sim.Wave == 1 && spawns > 20, $"A stream wave keeps sending enemies until the clock runs out ({spawns} came, one wave)");
        check(most <= sim.Settings.MaxAlive, $"An endless stream never puts more than maxAlive on the floor (most {most})");
        check(offers == 3 && sim.Picks == 3 && clockStill, "Card offers come at the stage's times (3), and the clock stands still while choosing");
        var early = hpAt.Where(h => h.Kind == "slime" && h.Clock < 15).ToList();
        var late = hpAt.Where(h => h.Kind == "slime" && h.Clock > 45).ToList();
        check(early.Count > 0 && late.Count > 0 && late.Min(h => h.Hp) > early.Max(h => h.Hp), "Threat grows with the clock: later slimes have more HP");
        check(hpAt.Any(h => h.Kind == "runner") && hpAt.Count(h => h.Kind == "runner") > hpAt.Count(h => h.Kind == "slime"), "The stream draws from its weights (more quick slimes)");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        bool Rejected(string json)
        {
            try { BattleData.Parse(json); return false; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return true; }
        }
        // The kinds named below (without them a file has only the built-in slime and king).
        const string kinds = "\"enemies\":[{\"id\":\"slime\"},{\"id\":\"runner\"},{\"id\":\"rock\"},{\"id\":\"king\",\"boss\":true}],";
        string Free(string wave) => "{" + kinds + "\"waves\":[" + wave + "]}";
        string Survive(string waves, string extra = "") => "{" + kinds + "\"campaign\":{\"chapters\":[{\"id\":\"c\",\"nameVi\":\"c\",\"nameEn\":\"c\",\"stages\":[{\"id\":\"s\",\"nameVi\":\"s\",\"nameEn\":\"s\",\"goalVi\":\"\",\"goalEn\":\"\"," +
            "\"objective\":\"survive\",\"surviveSeconds\":60,\"upgrades\":true" + extra + ",\"waves\":[" + waves + "]}]}]}}";
        string Clear(string waves, string extra = "") => Survive(waves, extra).Replace("\"objective\":\"survive\",\"surviveSeconds\":60,", "\"objective\":\"clear\",");
        const string stream = "{\"stream\":{\"weights\":{\"slime\":1},\"interval\":2,\"intervalEnd\":1}}";
        check(!Rejected(Free("{\"groups\":[{\"pick\":{\"slime\":1,\"runner\":1},\"count\":2,\"atLeast\":{\"runner\":1}}]}")) &&
              !Rejected(Free("{\"groups\":[{\"squad\":[{\"enemy\":\"rock\",\"count\":2}]}]}")) && !Rejected(Survive(stream, ",\"upgradeAt\":[20,40],\"threatGrowth\":0.3")),
            "Picks, squads and streams that fit are accepted");
        check(Rejected(Free("{\"groups\":[{\"squad\":[{\"enemy\":\"rock\",\"count\":4}]}]}")), "A squad bigger than maxAlive is rejected (it would never come)");
        check(Rejected(Free("{\"groups\":[{\"pick\":{\"king\":1},\"count\":1}]}")) && Rejected(Free("{\"groups\":[{\"pick\":{\"nobody\":1},\"count\":1}]}")) &&
              Rejected(Free("{\"groups\":[{\"pick\":{\"slime\":0},\"count\":1}]}")), "A pick of a boss, an unknown kind or no weight is rejected");
        check(Rejected(Free("{\"groups\":[{\"pick\":{\"slime\":1},\"count\":1,\"atLeast\":{\"runner\":2}}]}")) && Rejected(Free("{\"groups\":[{\"enemy\":\"slime\",\"atLeast\":{\"runner\":1}}]}")),
            "atLeast asking more than count, or without a pick, is rejected");
        check(Rejected(Free("{\"groups\":[{\"pick\":{\"slime\":1},\"squad\":[{\"enemy\":\"rock\",\"count\":2}]}]}")) &&
              Rejected(Free("{\"groups\":[{\"squad\":[{\"enemy\":\"king\",\"count\":2}]}]}")), "A pick-and-squad group or a boss squad is rejected");
        check(Rejected(Free(stream)) && Rejected(Clear(stream)) && Rejected(Survive(stream + ",{\"groups\":[{\"enemy\":\"slime\"}]}")),
            "A stream only goes last in a survive stage");
        check(Rejected(Survive(stream, ",\"upgradeAt\":[40,20]")) && Rejected(Survive(stream, ",\"upgradeAt\":[70]")) &&
              Rejected(Clear("{\"groups\":[{\"enemy\":\"slime\"}]}", ",\"upgradeAt\":[5]")) && Rejected(Clear("{\"groups\":[{\"enemy\":\"slime\"}]}", ",\"threatGrowth\":0.5")),
            "upgradeAt must rise inside the clock; upgradeAt and threatGrowth only for survive stages");
        check(Rejected(Free("{\"threat\":9,\"groups\":[{\"enemy\":\"slime\"}]}")) && Rejected(Clear("{\"groups\":[{\"enemy\":\"slime\"}]}", ",\"threat\":0.1")) &&
              Rejected("{\"threatDamageShare\":2}"), "Threat numbers out of range are rejected");
        const string bossKing = "{\"id\":\"king_c1\",\"boss\":true,\"phases\":[{\"at\":0.5,\"shield\":true,\"summons\":[{\"enemy\":\"slime\",\"count\":2}]}]}";
        check(!Rejected("{\"enemies\":[{\"id\":\"slime\"},{\"id\":\"king\",\"boss\":true}," + bossKing + "]}") &&
              Rejected("{\"enemies\":[{\"id\":\"slime\"},{\"id\":\"king\",\"boss\":true}," + bossKing.Replace("{\"enemy\":\"slime\",\"count\":2}", "{\"squad\":[{\"enemy\":\"slime\",\"count\":2}]}") + "]}"),
            "A boss phase calls plain groups only");
    }

    private static void ShippedChecks(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var stages = data.Campaign!.Chapters[0].Stages;
        string Kinds(WaveSpec wave, int seed) => string.Join(",", SpawnDirector.Units(wave, new Random(seed)).SelectMany(u => u.Kinds));
        // First steps: plain slimes first; the last wave always brings a quick slime to meet.
        check(stages[0].Waves[0].Groups.All(g => g.Fixed && g.Enemy == "slime") &&
              Enumerable.Range(0, 50).All(i => Kinds(stages[0].Waves[^1], i).Split(',').Contains("runner")), "Stage 1: plain slimes, then a last wave that always introduces a quick slime");
        // Break through: rocks the largest kind; plain and quick even in the rest.
        var counts = Enumerable.Range(0, 200).SelectMany(i => stages[1].Waves.SelectMany(w => Kinds(w, i).Split(','))).GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
        check(counts["rock"] > counts["slime"] && counts["rock"] > counts["runner"] && Math.Abs(counts["slime"] - counts["runner"]) < counts["rock"] / 4,
            $"Stage 2: rock slimes most, plain and quick about even ({counts["rock"]}/{counts["slime"]}/{counts["runner"]})");
        // Hold on: one endless stream, quick slimes most, rock and plain even; three timed picks.
        var stream = stages[2].Waves.Single().Stream!;
        check(stream.Weights["runner"] > stream.Weights["slime"] && stream.Weights["slime"] == stream.Weights["rock"] && stages[2].UpgradeAt?.Length == 3 && stages[2].MaxUpgrades == 3 && stages[2].ThreatGrowth > 0,
            "Stage 3: one endless stream (quick slimes most), threat growing, 3 timed card picks");
        // Cut the support: a herb slime with three rocks together, room for them.
        var squads = stages[3].Waves.SelectMany(w => w.Groups).Where(g => g.Squad != null).ToList();
        check(squads.Count >= 2 && squads.All(g => g.Squad!.Single(m => m.Enemy == "mender").Count == 1 && g.Squad!.Single(m => m.Enemy == "rock").Count == 3) && stages[3].MaxAlive >= 4,
            "Stage 4: herb slime + 3 rocks come as squads (room for all four)");
        check(stages[4].Waves[^1].Groups.Single() is { Fixed: true, Enemy: "king_c1", Count: 1 }, "Stage 5: the King's wave is the King alone (his helpers are his phases')");

        // Many seeds: every stage ends (won or lost) within ten minutes of battle; nothing deadlocks.
        bool ended = true;
        foreach (var stage in stages)
            for (int seed = 1; seed <= 8; seed++)
                foreach (var level in new[] { BattleDifficulty.Easy, BattleDifficulty.Hard })
                {
                    var sim = new BattleSim(data, 0, 1600, difficulty: level, stage: stage, seed: seed);
                    double cat = 800;
                    for (double t = 0; t < 600 && !sim.Over; t += 1.0 / 30)
                    {
                        if (sim.ChoosingUpgrade) sim.ChooseUpgrade(sim.UpgradeOffer![0].Id, cat);
                        var order = sim.Step(1.0 / 30, cat, false);
                        if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -4.3, 4.3);
                    }
                    ended &= sim.Over;
                }
        check(ended, "Every chapter stage ends on every seed tried (no formation waits for ever)");
    }
}
