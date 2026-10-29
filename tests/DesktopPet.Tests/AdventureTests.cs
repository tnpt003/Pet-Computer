using DesktopPet;

// Adventure (milestone 2): the chapter data, stage battles on the existing engine, the unlock
// rules, results counted once, and the saved progress (atomic, backup, newer versions, broken
// files). Temporary files only; the user's own progress is never touched.
internal static class AdventureTests
{
    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var campaign = data.Campaign;
        check(campaign is { Chapters.Length: 2 } && campaign.Chapters[0].Stages.Length == 5, "Built-in battle.json has Adventure chapter 1 with 5 stages (and chapter 2 since 1.5 M21)");
        var stages = campaign!.Chapters[0].Stages;
        check(stages.Select(s => s.Id).Distinct().Count() == 5 && stages.All(s => (s.Objective == "clear" || s.Survival) && s.Waves.Length > 0 && s.GoalVi.Length > 0 && s.GoalEn.Length > 0),
            "Every stage has its own id, a goal in both languages and waves");
        check(stages[^1].Waves.Any(w => w.Groups.Any(g => data.Enemy(g.Enemy).Boss)), "The last stage is the Slime King's");

        // The engine: a stage brings its own waves and enemy cap; a free battle is as before.
        var free = new BattleSim(data, 0, 1600, new Random(1));
        check(free.Stage == null && free.WaveCount == data.WavePlan.Count && free.WaveCount == 4, "A free battle still uses the battle's own 4 waves");
        var first = new BattleSim(data, 0, 1600, new Random(1), stage: stages[0]);
        check(first.Stage == stages[0] && first.WaveCount == 2 && !first.IsBossWave(2), "A stage battle uses the stage's waves");
        check(new BattleSim(data, 0, 1600, new Random(1), stage: stages[4]) is var last && last.IsBossWave(last.WaveCount), "The boss stage's last wave is a boss wave");
        // Stage 1 allows 2 slimes at once although its second wave has 3.
        int most = 0;
        double cat = 800;
        for (double t = 0; t < 240 && !first.Over; t += 1.0 / 60)
        {
            var order = first.Step(1.0 / 60, cat, false);
            most = Math.Max(most, first.Alive);
            if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -72 * 1.8 / 60, 72 * 1.8 / 60); // Walking speed × CatRunScale.
        }
        check(first.Phase == BattlePhase.Won && first.Defeated == 5 && most <= 2, $"Stage 1 is won in the simulation with at most 2 slimes at once (won {first.Phase}, {first.Defeated} defeated, max {most})");

        // Validation: a broken campaign rejects the whole file.
        string Error(string campaignJson)
        {
            try { BattleData.Parse("{ \"schemaVersion\": 1, \"campaign\": " + campaignJson + " }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        string Stage(string id, string extra = "", string enemy = "slime") =>
            $"{{ \"id\": \"{id}\", \"nameVi\": \"A\", \"nameEn\": \"A\", \"goalVi\": \"g\", \"goalEn\": \"g\"{extra}, \"waves\": [ {{ \"groups\": [ {{ \"enemy\": \"{enemy}\", \"count\": 1 }} ] }} ] }}";
        string Chapter(params string[] list) => $"{{ \"chapters\": [ {{ \"id\": \"ch\", \"nameVi\": \"C\", \"nameEn\": \"C\", \"stages\": [ {string.Join(",", list)} ] }} ] }}";
        check(Error(Chapter(Stage("a"), Stage("b"))) == "", "A small valid campaign is accepted");
        check(Error(Chapter(Stage("a"), Stage("a"))).Contains("used twice"), "A stage id used twice is rejected");
        check(Error(Chapter(Stage("a", enemy: "ghost"))).Contains("unknown enemy"), "A stage wave naming an unknown enemy is rejected");
        check(Error(Chapter(Stage("a", ", \"objective\": \"escort\""))).Contains("objective"), "An unknown objective is rejected");
        check(Error(Chapter(Stage("a", ", \"maxAlive\": 0"))).Contains("maxAlive"), "maxAlive outside 1–10 is rejected");
        check(Error(Chapter(Stage("Bad Id"))).Contains(".id"), "A stage id with spaces or capitals is rejected");
        check(Error("{ \"chapters\": [] }").Contains("at least one chapter"), "A campaign without chapters is rejected");
        check(Error(Chapter(Stage("a", ", \"waves2\": 1"))) != "", "A misspelt stage key is rejected");
        // A user's battle.json without "campaign" keeps the built-in chapter.
        string userFile = Path.Combine(Path.GetTempPath(), "pet-battle-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(userFile, "{ \"schemaVersion\": 1, \"catMaxHp\": 120 }");
        var merged = BattleData.Load(userFile);
        File.Delete(userFile);
        check(merged.Custom && merged.Settings.Campaign?.Chapters[0].Stages.Length == 5 && merged.Settings.CatMaxHp == 120, "A custom battle.json without a campaign keeps the built-in chapter");

        // The rules: stages open in order, a run counts once, a loss takes nothing back.
        var progress = new AdventureProgress();
        var adventure = new Adventure(campaign, progress);
        check(adventure.IsUnlocked(stages[0].Id) && !adventure.IsUnlocked(stages[1].Id) && !adventure.IsUnlocked(stages[4].Id), "Only stage 1 is open at first");
        var lost = adventure.Apply("run1", stages[0], ranked: true, won: false, "sword", BattleDifficulty.Normal, 2, 40);
        check(lost.Counted && !lost.FirstClear && lost.Unlocked == null && !adventure.IsUnlocked(stages[1].Id), "A loss is counted but opens nothing");
        var won = adventure.Apply("run2", stages[0], true, true, "sword", BattleDifficulty.Normal, 5, 50);
        check(won.Counted && won.FirstClear && won.Unlocked == stages[1] && adventure.IsUnlocked(stages[1].Id), "Winning stage 1 opens stage 2");
        var again = adventure.Apply("run2", stages[0], true, true, "sword", BattleDifficulty.Normal, 5, 30);
        check(!again.Counted && adventure.Record(stages[0], "sword", BattleDifficulty.Normal).Wins == 1 && progress.Runs.Count(r => r == "run2") == 1,
            "The same run counted twice changes nothing (no second win, no new record)");
        var replay = adventure.Apply("run3", stages[0], true, true, "sword", BattleDifficulty.Normal, 5, 45);
        check(replay.Counted && !replay.FirstClear && replay.Unlocked == null && replay.News.Fastest, "Playing a won stage again is allowed and can set a record");
        adventure.Apply("run4", stages[0], true, false, "sword", BattleDifficulty.Normal, 1, 10);
        check(adventure.IsCompleted(stages[0].Id) && adventure.IsUnlocked(stages[1].Id), "Losing a won stage later keeps it won and the next stage open");
        check(adventure.Record(stages[0], "hammer", BattleDifficulty.Normal).Empty && adventure.Record(stages[0], "sword", BattleDifficulty.Hard).Empty,
            "Stage records are kept per weapon and difficulty");
        var custom = adventure.Apply("run5", stages[1], ranked: false, won: true, "sword", BattleDifficulty.Normal, 9, 20);
        check(!custom.Counted && !adventure.IsCompleted(stages[1].Id) && adventure.Record(stages[1], "sword", BattleDifficulty.Normal).Empty,
            "A run with custom battle data saves no progress and no record");
        check(adventure.Next(stages[4].Id) == null && adventure.Next(stages[0].Id) == stages[1], "Next stage follows the chapter order and ends after the boss");
        var rules2 = new StageSpec { Id = stages[0].Id, RulesVersion = stages[0].RulesVersion + 1 };
        check(AdventureProgress.RecordKey(rules2, "sword", BattleDifficulty.Normal) != AdventureProgress.RecordKey(stages[0], "sword", BattleDifficulty.Normal),
            "A stage's new rules version keeps its records apart from the old ones");
        // Clearing every stage in order: the last one opens nothing and completes the chapter.
        var all = new Adventure(campaign, new AdventureProgress());
        StageSpec? opened = null;
        for (int i = 0; i < stages.Length; i++) opened = all.Apply("s" + i, stages[i], true, true, "hammer", BattleDifficulty.Easy, 3, 60).Unlocked;
        check(opened == campaign.Chapters[1].Stages[0] && stages.All(s => all.IsCompleted(s.Id)), "Winning all 5 stages in order completes the chapter (the last one opens chapter 2, 1.5 M21)");
        for (int i = 0; i < 60; i++) all.Apply("extra" + i, stages[0], true, false, "sword", BattleDifficulty.Normal, 0, 1);
        check(all.Progress.Runs.Count == AdventureProgress.RunsKept, "Only the last 50 run ids are kept");

        // The file: atomic save with a backup, broken files, newer versions. Temporary folder only.
        string dir = Path.Combine(Path.GetTempPath(), "pet-adventure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "adventure-progress.json");
            check(AdventureProgress.Load(path).Completed.Count == 0, "No file: empty progress");
            check(progress.Save(path) && !File.Exists(path + ".tmp"), "Progress saves without leaving a .tmp file");
            var read = AdventureProgress.Load(path);
            check(read.Version == AdventureProgress.CurrentVersion && read.Completed.SequenceEqual(new[] { stages[0].Id }) && read.Runs.Contains("run2") &&
                  read.Records[AdventureProgress.RecordKey(stages[0], "sword", BattleDifficulty.Normal)].Wins == 2,
                "Saved progress reads back the same (won stages, run ids, records)");
            read.Completed.Add(stages[1].Id);
            check(read.Save(path) && File.Exists(path + ".bak") && AdventureProgress.Load(path + ".bak").Completed.Count == 1, "Saving again keeps the previous file as .bak");
            File.WriteAllText(path, "{ broken");
            var fromBackup = AdventureProgress.Load(path);
            check(fromBackup.Completed.SequenceEqual(new[] { stages[0].Id }), "A broken file is read from its backup");
            File.WriteAllText(path + ".bak", "nope");
            check(AdventureProgress.Load(path).Completed.Count == 0, "Broken file and backup: progress starts over, no crash");
            File.WriteAllText(path, "{ \"Version\": 7, \"Completed\": [\"c1_first_steps\", \"future_stage\", \"\"], \"Future\": true }");
            var newer = AdventureProgress.Load(path);
            check(newer.ReadOnly && newer.Completed.Count == 2 && !newer.Save(path) && File.ReadAllText(path).Contains("\"Version\": 7"),
                "A newer version's file is read but never overwritten (unknown stage ids kept)");
            File.WriteAllText(path, "{ \"Completed\": null, \"Records\": { \"x\": { \"Wins\": -3, \"BestSeconds\": -1 } } }");
            var odd = AdventureProgress.Load(path);
            check(!odd.ReadOnly && odd.Completed.Count == 0 && odd.Records["x"].Wins == 0 && odd.Records["x"].BestSeconds == null, "Missing lists and impossible numbers are cleaned");
        }
        finally { Directory.Delete(dir, true); }
    }
}
