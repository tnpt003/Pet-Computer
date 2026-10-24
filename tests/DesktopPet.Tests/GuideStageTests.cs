using System;
using System.Collections.Generic;
using System.Linq;
using DesktopPet;

// The Adventure Guide's stages (2026-10-24): stage 2 has no malware, stage 3 sends each malware and
// an infection at set times (random ones only after that), stage 4 sets one elite of each kind
// (also on Easy), and a forced elite needs a kind with an affix.
internal static class GuideStageTests
{
    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var stages = data.Campaign!.Chapters[0].Stages;
        var breakThrough = stages.First(s => s.Id == "c1_break_through");
        var holdOn = stages.First(s => s.Id == "c1_hold_on");
        var cutSupport = stages.First(s => s.Id == "c1_cut_support");

        // Plays a stage with the cat standing still in the middle, never lifted; notes when each thing first shows.
        Dictionary<string, double> Play(StageSpec stage, double seconds, BattleDifficulty level, int seed, out BattleSim sim)
        {
            sim = new BattleSim(data, 0, 1600, difficulty: level, stage: stage, seed: seed, startHp: 100000);
            var seen = new Dictionary<string, double>();
            void Note(string what, bool now, BattleSim b) { if (now && !seen.ContainsKey(what)) seen[what] = b.Stage!.Survival ? b.Stage.SurviveSeconds!.Value - b.SurviveRemaining : b.Time; }
            for (double t = 0; t < seconds && !sim.Over; t += 1.0 / 30)
            {
                sim.Step(1.0 / 30, 800, false);
                if (sim.ChoosingUpgrade) sim.ChooseUpgrade(sim.UpgradeOffer![0].Id, 800);
                Note("ads", sim.Popups.Count > 0, sim);
                Note("loading", sim.Hazards.Any(h => h.Kind == HazardKind.Loading), sim);
                Note("update", sim.Hazards.Any(h => h.Kind == HazardKind.Update), sim);
                Note("virus", sim.VirusFiles.Count > 0, sim);
                Note("infect", sim.Desktop.Any(a => a.Infected), sim);
                Note("elite", sim.Slimes.Any(s => s.Elite != null), sim);
                sim.ClearEvents();
            }
            return seen;
        }

        bool quiet = true;
        for (int seed = 1; seed <= 5; seed++)
            quiet &= !Play(breakThrough, 120, BattleDifficulty.Hard, seed, out _).Keys.Any(k => k != "elite");
        check(quiet, "Stage 2: no malware and no infected icon (the HUD and the cards are what it teaches)");

        var plan = holdOn.MalwarePlan!.ToDictionary(c => c.Kind, c => c.At);
        bool onTime = true, allCame = true, noElite = true;
        string seenText = "", late = "";
        for (int seed = 1; seed <= 5; seed++)
        {
            var seen = Play(holdOn, 200, BattleDifficulty.Easy, seed, out var sim);
            noElite &= !seen.ContainsKey("elite");
            foreach (var (kind, at) in plan)
            {
                if (!seen.TryGetValue(kind, out double when)) { allCame = false; late += $" no {kind} (seed {seed}, {(sim.Over ? sim.Phase.ToString() : "running")})"; continue; }
                // Nothing of a kind before its cue; the cue comes soon after (it waits while no slime is on the floor).
                if (when < at - 0.05 || when > at + 8) { onTime = false; late += $" {kind}@{when:0.0} (seed {seed})"; }
            }
            if (seed == 1) seenText = string.Join(", ", seen.Where(p => p.Key != "elite").OrderBy(p => p.Value).Select(p => $"{p.Key} {p.Value:0.0}"));
        }
        check(allCame && onTime, $"Stage 3: ads, loading, update, virus.exe and an infection each come at their set time ({seenText}){late}");
        check(noElite && holdOn.EliteChance == 0, "Stage 3: no elites (they are stage 4's lesson)");

        // Stage 4's set elites come on Easy too (the balance run counts them on every difficulty).
        var easy = new BattleSim(data, 0, 1600, difficulty: BattleDifficulty.Easy, stage: cutSupport, seed: 3);
        for (double t = 0; t < 30 && easy.Slimes.Count(s => s.Targetable) < 3; t += 1.0 / 30) { easy.Step(1.0 / 30, 800, false); easy.ClearEvents(); }
        check(easy.Slimes.Count(s => s.Elite != null) == 2 && easy.Slimes.Where(s => s.Elite != null).Select(s => s.Type.Id).OrderBy(k => k).SequenceEqual(new[] { "runner", "slime" }),
            $"Stage 4 on Easy: the first wave's plain and quick slime come elite ({string.Join(", ", easy.Slimes.Where(s => s.Elite != null).Select(s => s.Type.Id))})");

        string Parse(string group)
        {
            try
            {
                BattleData.Parse("{ \"schemaVersion\": 1, \"campaign\": { \"chapters\": [ { \"id\": \"c\", \"nameVi\": \"C\", \"nameEn\": \"C\", \"stages\": [ { \"id\": \"s\", \"nameVi\": \"S\", \"nameEn\": \"S\", " +
                    "\"waves\": [ { \"groups\": [ " + group + " ] } ] } ] } ] } }");
                return "";
            }
            catch (Exception ex) when (ex is System.IO.InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        check(Parse("{ \"enemy\": \"king\", \"count\": 1, \"elite\": true }").Contains("no elite affix"),
            "A group set elite needs a kind with an elite affix (the shipped stage 4 has one of each)");
    }
}
