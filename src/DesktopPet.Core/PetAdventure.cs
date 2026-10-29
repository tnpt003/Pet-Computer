using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopPet;

// The user's Adventure progress (adventure-progress.json next to preferences.json): the stages
// won, rewards received, stage tips seen and the best results per stage loadout. Only what
// lasts is kept; a battle in progress is never saved (closing the app drops it, nothing is won).
// Saving is atomic (a .tmp file moved over the old one, which is kept as .bak); a broken file is
// read from its .bak, or starts over, and never stops the pet. A file from a newer version is
// read as far as it is understood and never overwritten. Battle records (battle-records.json)
// stay apart: an Adventure stage is not the free battle and is not compared with it.
public sealed class AdventureProgress
{
    // 2 (Combat Depth): Builds. An older pet reads version 2 as newer and never overwrites it, so
    // it cannot drop the builds; a version 1 file is read as having none.
    public const int CurrentVersion = 2;
    public int Version { get; set; } = CurrentVersion;
    public List<string> Completed { get; set; } = new();
    // Reward ids already given (Adventure Update milestone 4), so none is given twice.
    public List<string> Rewards { get; set; } = new();
    // Stage tips the user has seen or skipped.
    public List<string> Seen { get; set; } = new();
    // Rewards (Adventure accessories and Expedition keepsakes) the hub's Rewards page has shown:
    // the others it marks as new. A file without it shows every reward held as new once.
    public List<string> RewardsSeen { get; set; } = new();
    // Best results per "stage|r<rulesVersion>|weapon|difficulty".
    public Dictionary<string, BattleRecord> Records { get; set; } = new();
    // The build (card id → times taken) each stage was last won with; the next stage starts with it.
    public Dictionary<string, Dictionary<string, int>> Builds { get; set; } = new();
    // The last finished runs already counted, so a result counted twice (double click, a panel
    // shown again) changes nothing.
    public List<string> Runs { get; set; } = new();
    public const int RunsKept = 50;
    [JsonIgnore] public bool ReadOnly { get; private set; }

    public static string DefaultPath => Path.Combine(PetPreferences.DataDirectory, "adventure-progress.json");

    // Runs with the app icons turned off keep their records apart ("|noapps").
    public static string RecordKey(StageSpec stage, string weapon, BattleDifficulty difficulty, bool noApps = false) =>
        $"{stage.Id}|r{stage.RulesVersion}|{weapon}|{difficulty}{(noApps ? "|noapps" : "")}";

    public static AdventureProgress Load(string path)
    {
        var progress = JsonStore.ReadOrBackup<AdventureProgress>(path, "Adventure progress");
        if (progress == null) return new();
        progress.ReadOnly = progress.Version > CurrentVersion;
        if (!progress.ReadOnly) progress.Version = CurrentVersion; // An older file is saved back in this layout.
        progress.Normalize();
        return progress;
    }

    private void Normalize()
    {
        static List<string> Clean(List<string>? list) => (list ?? new()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        Completed = Clean(Completed); Rewards = Clean(Rewards); Seen = Clean(Seen); Runs = Clean(Runs); RewardsSeen = Clean(RewardsSeen);
        if (Runs.Count > RunsKept) Runs = Runs.Skip(Runs.Count - RunsKept).ToList();
        Records = (Records ?? new()).Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
            .ToDictionary(pair => pair.Key, pair => { pair.Value.Normalize(); return pair.Value; });
        Builds = (Builds ?? new()).Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Where(card => !string.IsNullOrWhiteSpace(card.Key) && card.Value is > 0 and <= 100)
                .ToDictionary(card => card.Key, card => card.Value));
    }

    public bool Save(string path)
    {
        if (ReadOnly)
        {
            Trace.WriteLine("Adventure progress comes from a newer version of the pet; it is not overwritten.");
            return false;
        }
        return JsonStore.Write(path, this, "Adventure progress", indented: true, backup: true);
    }
}

// What one finished stage run changed. Counted: false when the run was already counted, or was
// played with the user's own battle data (then nothing is saved).
// Rewards: accessory ids received with this run (each only ever once).
public readonly record struct AdventureOutcome(bool Counted, bool FirstClear, StageSpec? Unlocked, BattleRecordNews News, IReadOnlyList<string>? Rewards = null);

// The Adventure's rules over the stage data and the progress: which stages are open, and what a
// finished run changes. Stages open in order inside a chapter: the first is always open, each
// other one once the stage before it is won (a won stage stays open even if the data changes).
public sealed class Adventure
{
    public CampaignSpec Campaign { get; }
    public AdventureProgress Progress { get; }

    public Adventure(CampaignSpec campaign, AdventureProgress progress)
    {
        Campaign = campaign;
        Progress = progress;
    }

    public ChapterSpec? Chapter(string? id = null) => id == null ? Campaign.Chapters.FirstOrDefault() : Campaign.Chapters.FirstOrDefault(c => c.Id == id);

    public (ChapterSpec Chapter, int Index)? Locate(string stageId)
    {
        foreach (var chapter in Campaign.Chapters)
            for (int i = 0; i < chapter.Stages.Length; i++)
                if (chapter.Stages[i].Id == stageId) return (chapter, i);
        return null;
    }

    public StageSpec? Stage(string? stageId) => stageId != null && Locate(stageId) is { } at ? at.Chapter.Stages[at.Index] : null;
    public bool IsCompleted(string stageId) => Progress.Completed.Contains(stageId);

    public bool IsUnlocked(string stageId)
    {
        if (Locate(stageId) is not { } at) return false;
        return at.Index == 0 || IsCompleted(stageId) || IsCompleted(at.Chapter.Stages[at.Index - 1].Id);
    }

    // The setup panel's skill choice for this stage: only in a chapter that allows it (chapter 1 keeps
    // the five first skills, BattleSkills.Default).
    public bool SkillChoice(StageSpec stage) => Locate(stage.Id) is { } at && at.Chapter.SkillChoice;

    // A skill the user may carry: one that works, and is one of the five first or opened by a won
    // stage (StageSpec.UnlocksSkill).
    public bool SkillOpen(BattleSkill skill) => BattleSkills.Get(skill).Working && (Array.IndexOf(BattleSkills.Default, skill) >= 0
        || Campaign.Chapters.SelectMany(c => c.Stages).Any(s => BattleSkills.Parse(s.UnlocksSkill) == skill && IsCompleted(s.Id)));

    // The stage after this one in its chapter (null for the last).
    public StageSpec? Next(string stageId) =>
        Locate(stageId) is { } at && at.Index + 1 < at.Chapter.Stages.Length ? at.Chapter.Stages[at.Index + 1] : null;

    public BattleRecord Record(StageSpec stage, string weapon, BattleDifficulty difficulty, bool noApps = false) =>
        Progress.Records.TryGetValue(AdventureProgress.RecordKey(stage, weapon, difficulty, noApps), out var record) ? record : new();

    // The build a stage starts with: the one the stage before it in its chapter was last won with
    // (empty for the first stage, or before that stage is won).
    public IReadOnlyDictionary<string, int> StartBuild(StageSpec stage)
    {
        if (Locate(stage.Id) is not { } at || at.Index == 0) return new Dictionary<string, int>();
        return Progress.Builds.TryGetValue(at.Chapter.Stages[at.Index - 1].Id, out var build) ? build : new Dictionary<string, int>();
    }

    // A run that ended in a win or a loss (never one the user ended). Each run id counts once;
    // a loss never takes back what was opened before. A win keeps its build for the next stage
    // (replacing the one this stage was won with before); a loss changes no build. fishKept: a basket
    // stage's fish at the end of a win (-1: no basket); a loss (every fish gone too) never counts it.
    public AdventureOutcome Apply(string runId, StageSpec stage, bool ranked, bool won, string weapon, BattleDifficulty difficulty, int defeated, double seconds,
        IReadOnlyDictionary<string, int>? build = null, bool noApps = false, int fishKept = -1)
    {
        if (!ranked || string.IsNullOrEmpty(runId) || Progress.Runs.Contains(runId) || Locate(stage.Id) == null) return default;
        Progress.Runs.Add(runId);
        if (Progress.Runs.Count > AdventureProgress.RunsKept) Progress.Runs.RemoveAt(0);
        string key = AdventureProgress.RecordKey(stage, weapon, difficulty, noApps);
        if (!Progress.Records.TryGetValue(key, out var record)) Progress.Records[key] = record = new();
        bool fastest = false, most = false, fish = false;
        if (won)
        {
            record.Wins++;
            if (stage.Basket != null && fishKept > record.MostFish) { record.MostFish = fishKept; fish = true; }
            // A survive stage always lasts its time: no "fastest win" there.
            if (!stage.Survival && double.IsFinite(seconds) && seconds > 0 && (record.BestSeconds is not double best || seconds < best - 0.05)) { record.BestSeconds = seconds; fastest = true; }
        }
        if (defeated > record.MostDefeated) { record.MostDefeated = defeated; most = true; }
        if (won) Progress.Builds[stage.Id] = (build ?? new Dictionary<string, int>()).Where(card => card.Value > 0).ToDictionary(card => card.Key, card => card.Value);
        bool first = false;
        StageSpec? unlocked = null;
        if (won && !IsCompleted(stage.Id))
        {
            var next = Next(stage.Id);
            bool wasOpen = next != null && IsUnlocked(next.Id);
            Progress.Completed.Add(stage.Id);
            first = true;
            if (next != null && !wasOpen) unlocked = next;
        }
        return new(true, first, unlocked, new(fastest, most, MostFish: fish), won ? GrantRewards() : null);
    }

    // Gives every reward of a won stage not given yet (also one added to a stage won before it
    // existed). Returns what was new; a reward already given is never given again.
    public IReadOnlyList<string> GrantRewards()
    {
        var given = new List<string>();
        foreach (var chapter in Campaign.Chapters)
            foreach (var stage in chapter.Stages)
                if (stage.Reward is string reward && IsCompleted(stage.Id) && !Progress.Rewards.Contains(reward))
                {
                    Progress.Rewards.Add(reward);
                    given.Add(reward);
                }
        return given;
    }

    public bool HasReward(string accessory) => Progress.Rewards.Contains(accessory);
}
