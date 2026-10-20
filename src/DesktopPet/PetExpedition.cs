using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopPet;

// Expedition (docs/EXPEDITION-DIRECTION.md): one run over a small map of steps. Each step offers
// one to three nodes; the user picks one, fights it with the usual battle engine (an encounter is
// a StageSpec), and the cat's HP and build go on to the next step. The last step is the boss.
// battle.json "expedition": the map's node kinds per step ("fight", "elite"), the encounter pools
// the run draws from, the boss, and the run's rules. Ids are stable: a saved run names them.
public sealed class ExpeditionSpec
{
    public const string Fight = "fight", Elite = "elite";
    public static readonly string[] Kinds = { Fight, Elite };

    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string Name(bool english) => english ? NameEn : NameVi;
    // Raise it when the run changes enough that a saved run or old records should not go on.
    public int RulesVersion { get; init; } = 1;
    // The Adventure stage that must be won first (null, or a stage the data no longer has, as with a
    // user's battle.json that replaces the campaign: always open).
    public string? UnlockAfter { get; init; }
    // Leaving a step's battle (End, closing the app, a crash): the step can be fought again, for
    // this share of the cat's max HP.
    public double RetreatCost { get; init; } = 0.25;
    // HP given back after a won step, as a share of the cat's max HP.
    public double WinHeal { get; init; } = 0.1;
    // Each step makes its enemies this much stronger than the one before (step 1: × 1).
    public double StepThreat { get; init; }
    // The node kinds of each step before the boss, in lane order.
    public string[][] Steps { get; init; } = Array.Empty<string[]>();
    public StageSpec[] Fights { get; init; } = Array.Empty<StageSpec>();
    public StageSpec[] Elites { get; init; } = Array.Empty<StageSpec>();
    public StageSpec? Boss { get; init; }

    public IReadOnlyList<StageSpec> Pool(string kind) => kind == Elite ? Elites : Fights;
    public StageSpec? Encounter(string id) => Fights.Concat(Elites).Append(Boss).FirstOrDefault(s => s?.Id == id);
    // Steps including the boss's.
    [JsonIgnore] public int StepCount => Steps.Length + 1;
    public double Threat(int step) => 1 + StepThreat * step;

    internal static void Validate(ExpeditionSpec x, BattleSettings s, Action<IReadOnlyList<WaveSpec>?, string, int?, bool> waves, Action<string?, string> id, Action<string?, string> text)
    {
        const string at = "expedition";
        id(x.Id, at + ".id");
        text(x.NameVi, at + ".nameVi"); text(x.NameEn, at + ".nameEn");
        if (x.RulesVersion is < 1 or > 1000) throw new InvalidDataException(at + ".rulesVersion must be 1–1000");
        if (!double.IsFinite(x.RetreatCost) || x.RetreatCost is < 0 or > 1) throw new InvalidDataException(at + ".retreatCost must be 0–1");
        if (!double.IsFinite(x.WinHeal) || x.WinHeal is < 0 or > 1) throw new InvalidDataException(at + ".winHeal must be 0–1");
        if (!double.IsFinite(x.StepThreat) || x.StepThreat is < 0 or > 0.5) throw new InvalidDataException(at + ".stepThreat must be 0–0.5");
        var stageIds = new HashSet<string>(s.Campaign?.Chapters.SelectMany(c => c.Stages).Select(st => st.Id) ?? Enumerable.Empty<string>());
        if (x.UnlockAfter != null) id(x.UnlockAfter, at + ".unlockAfter");
        // Encounter ids are unique, and never an Adventure stage's (titles and records stay apart).
        var own = new HashSet<string>();
        void Encounter(StageSpec? stage, string where)
        {
            if (stage == null) throw new InvalidDataException(where + " is empty");
            id(stage.Id, where + ".id");
            if (stageIds.Contains(stage.Id) || !own.Add(stage.Id)) throw new InvalidDataException($"encounter id \"{stage.Id}\" is used twice (or is an Adventure stage's)");
            BattleData.ValidateStage(stage, where, waves, text);
            if (stage.Survival) throw new InvalidDataException(where + ": an encounter is a clear stage");
            if (stage.Reward != null || stage.TipVi != null) throw new InvalidDataException(where + ": an encounter has no reward or tip");
        }
        if (x.Fights == null || x.Fights.Length is < 1 or > 20) throw new InvalidDataException(at + ".fights must list 1–20 encounters");
        if (x.Elites == null || x.Elites.Length > 20) throw new InvalidDataException(at + ".elites must list at most 20 encounters");
        for (int i = 0; i < x.Fights.Length; i++) Encounter(x.Fights[i], $"{at}.fights[{x.Fights[i]?.Id ?? i.ToString()}]");
        for (int i = 0; i < x.Elites.Length; i++) Encounter(x.Elites[i], $"{at}.elites[{x.Elites[i]?.Id ?? i.ToString()}]");
        Encounter(x.Boss, at + ".boss");
        if (!x.Boss!.Waves.Any(w => w.Groups.Any(g => g.Fixed && s.Enemy(g.Enemy).Boss))) throw new InvalidDataException(at + ".boss needs a wave with a boss");
        if (x.Steps == null || x.Steps.Length is < 1 or > 10) throw new InvalidDataException(at + ".steps must list 1–10 steps");
        for (int i = 0; i < x.Steps.Length; i++)
        {
            var step = x.Steps[i];
            if (step == null || step.Length is < 1 or > 3) throw new InvalidDataException($"{at}.steps[{i}] must list 1–3 nodes");
            foreach (var kind in step)
            {
                if (Array.IndexOf(Kinds, kind) < 0) throw new InvalidDataException($"{at}.steps[{i}] node kinds must be one of: {string.Join(", ", Kinds)}");
                // A step's nodes get different encounters: its pool needs enough of them.
                if (x.Pool(kind).Count < step.Count(k => k == kind)) throw new InvalidDataException($"{at}.steps[{i}] has more {kind} nodes than {kind} encounters");
            }
        }
    }
}

// The run in progress: the drawn map, the steps won (the lane picked at each), the cat's HP and
// build. Fighting: the lane of the step's battle under way (saved when it starts); still set when
// the run is read again, the battle was left (closed, crashed) and costs RetreatCost.
public sealed class ExpeditionRun
{
    public string RunId { get; set; } = "";
    public string Expedition { get; set; } = "";
    public int RulesVersion { get; set; }
    public int Seed { get; set; }
    public string Weapon { get; set; } = BattleWeapon.Sword;
    public string Difficulty { get; set; } = nameof(BattleDifficulty.Normal);
    // Encounter ids per step, in lane order (the boss's step last).
    public List<string[]> Map { get; set; } = new();
    public List<int> Path { get; set; } = new();
    public double Hp { get; set; }
    public Dictionary<string, int> Build { get; set; } = new();
    public int? Fighting { get; set; }
    public int Retreats { get; set; }
    public double Seconds { get; set; }
    public int Defeated { get; set; }

    [JsonIgnore] public int Step => Path.Count;
    [JsonIgnore] public BattleDifficulty Level => Enum.TryParse<BattleDifficulty>(Difficulty, out var level) && Enum.IsDefined(level) ? level : BattleDifficulty.Normal;
}

// Best results per "expedition|r<rulesVersion>|weapon|difficulty": runs ended, runs won, the most
// steps won in one run, the fastest won run (fight time only).
public sealed class ExpeditionRecord
{
    public int Runs { get; set; }
    public int Wins { get; set; }
    public int BestSteps { get; set; }
    public double? BestSeconds { get; set; }
}

// expedition.json next to preferences.json, apart from the Adventure progress: the run in progress
// and what finished runs left. Saved like the Adventure progress: atomic (.tmp moved over the old
// file, kept as .bak), a broken file read from its .bak or started over, a newer version's file read
// as far as it is understood and never overwritten.
public sealed class ExpeditionProgress
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public ExpeditionRun? Active { get; set; }
    public Dictionary<string, ExpeditionRecord> Records { get; set; } = new();
    // The last finished runs already counted, so one is never counted twice.
    public List<string> Runs { get; set; } = new();
    public const int RunsKept = 50;
    [JsonIgnore] public bool ReadOnly { get; private set; }

    public static string DefaultPath => Path.Combine(PetPreferences.DataDirectory, "expedition.json");

    public static ExpeditionProgress Load(string path)
    {
        if (TryLoad(path, out var progress)) return progress!;
        if (TryLoad(path + ".bak", out progress))
        {
            Trace.WriteLine("Expedition progress was read from its backup.");
            return progress!;
        }
        return new();
    }

    private static bool TryLoad(string path, out ExpeditionProgress? progress)
    {
        progress = null;
        if (!File.Exists(path)) return false;
        try
        {
            progress = JsonSerializer.Deserialize<ExpeditionProgress>(File.ReadAllText(path)) ?? throw new JsonException("empty file");
            progress.ReadOnly = progress.Version > CurrentVersion;
            if (!progress.ReadOnly) progress.Version = CurrentVersion;
            progress.Records = (progress.Records ?? new()).Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null).ToDictionary(pair => pair.Key, pair => pair.Value);
            progress.Runs = (progress.Runs ?? new()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().TakeLast(RunsKept).ToList();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.WriteLine($"Expedition progress {path} could not be read: {ex.Message}");
            progress = null;
            return false;
        }
    }

    public bool Save(string path)
    {
        if (ReadOnly)
        {
            Trace.WriteLine("Expedition progress comes from a newer version of the pet; it is not overwritten.");
            return false;
        }
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true);
            else File.Move(temp, path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"Expedition progress could not be saved: {ex.Message}");
            return false;
        }
    }
}

// What one step's battle (or leaving it) changed. RunOver: the run ended (Won: at the boss).
public readonly record struct ExpeditionStepOutcome(bool Counted, int Step, bool Won, bool RunOver, bool RunWon, double Hp, double MaxHp, double Healed = 0, bool BestSteps = false, bool Fastest = false);

// One step the user can fight now: its encounter, its kind, and the battle it starts.
public readonly record struct ExpeditionNode(int Step, int Lane, string Kind, StageSpec Stage, int Seed, double Threat);

// The Expedition's rules over its data and the saved progress (no WPF, no files: the caller saves).
public sealed class Expedition
{
    public const string BossKind = "boss";
    public ExpeditionSpec Spec { get; }
    public BattleSettings Settings { get; }
    public ExpeditionProgress Progress { get; }

    public Expedition(BattleSettings settings, ExpeditionProgress progress)
    {
        Settings = settings;
        Spec = settings.Expedition ?? throw new ArgumentException("The battle data has no expedition.");
        Progress = progress;
    }

    public ExpeditionRun? Run => Progress.Active;
    public bool IsUnlocked(AdventureProgress adventure) => Spec.UnlockAfter is not string stage || adventure.Completed.Contains(stage)
        || Settings.Campaign?.Chapters.Any(c => c.Stages.Any(s => s.Id == stage)) != true;
    public static string RecordKey(ExpeditionSpec spec, string weapon, BattleDifficulty difficulty) => $"{spec.Id}|r{spec.RulesVersion}|{weapon}|{difficulty}";
    public ExpeditionRecord Record(string weapon, BattleDifficulty difficulty) =>
        Progress.Records.TryGetValue(RecordKey(Spec, weapon, difficulty), out var record) ? record : new();

    // The cat's max HP with a build (cards like Big heart raise it), as the battle would have it.
    public double MaxHp(IReadOnlyDictionary<string, int> build) => new BattleSim(Settings, 0, 1000, build: build, seed: 0).CatMaxHp;

    // A new run (replacing none: the caller ends the old one first). The map is drawn from the
    // seed once and kept, so closing and opening the map, or resuming, never draws it again.
    public ExpeditionRun Start(string weapon, BattleDifficulty difficulty, int seed, string? runId = null)
    {
        var random = BattleRandom.Stream(seed, 50);
        var uses = new Dictionary<string, int>();
        var map = new List<string[]>();
        foreach (var step in Spec.Steps)
        {
            var row = new string[step.Length];
            for (int lane = 0; lane < step.Length; lane++)
            {
                // Not one this step already has; among the rest, one used least so far.
                var options = Spec.Pool(step[lane]).Where(s => !row.Contains(s.Id)).ToList();
                int least = options.Min(s => uses.GetValueOrDefault(s.Id));
                options = options.Where(s => uses.GetValueOrDefault(s.Id) == least).ToList();
                var pick = options[random.Next(options.Count)];
                row[lane] = pick.Id;
                uses[pick.Id] = least + 1;
            }
            map.Add(row);
        }
        map.Add(new[] { Spec.Boss!.Id });
        var run = new ExpeditionRun
        {
            RunId = runId ?? Guid.NewGuid().ToString("N"), Expedition = Spec.Id, RulesVersion = Spec.RulesVersion, Seed = seed,
            Weapon = Settings.Weapon(weapon).Id, Difficulty = difficulty.ToString(), Map = map
        };
        run.Hp = MaxHp(run.Build);
        Progress.Active = run;
        return run;
    }

    // Null when the saved run still fits this data; otherwise why it cannot go on.
    public string? Mismatch(ExpeditionRun run)
    {
        if (run.Expedition != Spec.Id || run.RulesVersion != Spec.RulesVersion) return "the expedition's rules changed";
        if (run.Map.Count != Spec.StepCount || run.Path.Count >= run.Map.Count) return "the map changed";
        for (int i = 0; i < run.Map.Count; i++)
        {
            int lanes = i < Spec.Steps.Length ? Spec.Steps[i].Length : 1;
            if (run.Map[i] == null || run.Map[i].Length != lanes || run.Map[i].Any(id => Spec.Encounter(id) == null)) return "the map changed";
        }
        if (run.Path.Where((lane, i) => lane < 0 || lane >= run.Map[i].Length).Any()) return "the map changed";
        if (run.Fighting is int f && (f < 0 || f >= run.Map[run.Step].Length)) return "the map changed";
        return null;
    }

    public string Kind(int step, int lane) => step >= Spec.Steps.Length ? BossKind : Spec.Steps[step][lane];

    // A node of the step to fight now (null: not one).
    public ExpeditionNode? Node(int lane)
    {
        if (Run is not ExpeditionRun run || run.Step >= run.Map.Count || lane < 0 || lane >= run.Map[run.Step].Length) return null;
        if (Spec.Encounter(run.Map[run.Step][lane]) is not StageSpec stage) return null;
        // Each node's battle has its own seed: fighting it again (after leaving it) plays the same.
        int seed = BattleRandom.Stream(run.Seed, 1000 + run.Step * 8 + lane).Next();
        return new(run.Step, lane, Kind(run.Step, lane), stage, seed, Spec.Threat(run.Step));
    }

    // The node's battle has started: from now on leaving it costs RetreatCost.
    public bool Enter(int lane)
    {
        if (Run is not ExpeditionRun run || run.Fighting != null || Node(lane) == null) return false;
        run.Fighting = lane;
        return true;
    }

    // The step's battle ended in a win or a loss. A win moves the run on (HP and build kept, WinHeal
    // given back); a won boss or any loss ends the run and counts it once.
    public ExpeditionStepOutcome Finish(bool won, double hp, IReadOnlyDictionary<string, int> build, double seconds, int defeated)
    {
        if (Run is not ExpeditionRun run || run.Fighting is not int lane) return default;
        run.Fighting = null;
        run.Seconds += double.IsFinite(seconds) && seconds > 0 ? seconds : 0;
        run.Defeated += Math.Max(0, defeated);
        int step = run.Step;
        if (!won) return End(run, false, step, 0, MaxHp(run.Build));
        run.Path.Add(lane);
        run.Build = build.Where(card => card.Value > 0).ToDictionary(card => card.Key, card => card.Value);
        double max = MaxHp(run.Build);
        if (run.Step >= run.Map.Count) return End(run, true, step, Math.Clamp(hp, 0, max), max);
        double before = Math.Clamp(hp, 1, max);
        run.Hp = Math.Min(max, before + Spec.WinHeal * max);
        return new(true, step, true, false, false, run.Hp, max, run.Hp - before);
    }

    // The step's battle was left (End, closing, a crash): the step stays to be fought again, for
    // RetreatCost of the max HP. HP gone ends the run as lost.
    public ExpeditionStepOutcome Retreat()
    {
        if (Run is not ExpeditionRun run || run.Fighting == null) return default;
        run.Fighting = null;
        run.Retreats++;
        double max = MaxHp(run.Build);
        run.Hp -= Spec.RetreatCost * max;
        if (run.Hp <= 0.5) return End(run, false, run.Step, 0, max);
        return new(true, run.Step, false, false, false, run.Hp, max, -Spec.RetreatCost * max);
    }

    // The user gives the run up: counted as ended where it is.
    public ExpeditionStepOutcome Abandon()
    {
        if (Run is not ExpeditionRun run) return default;
        return End(run, false, run.Step, run.Hp, MaxHp(run.Build));
    }

    private ExpeditionStepOutcome End(ExpeditionRun run, bool won, int step, double hp, double max)
    {
        Progress.Active = null;
        if (Progress.Runs.Contains(run.RunId)) return new(false, step, won, true, won, hp, max);
        Progress.Runs.Add(run.RunId);
        if (Progress.Runs.Count > ExpeditionProgress.RunsKept) Progress.Runs.RemoveAt(0);
        string key = RecordKey(Spec, run.Weapon, run.Level);
        if (!Progress.Records.TryGetValue(key, out var record)) Progress.Records[key] = record = new();
        record.Runs++;
        bool best = run.Step > record.BestSteps, fastest = false;
        if (best) record.BestSteps = run.Step;
        if (won)
        {
            record.Wins++;
            if (run.Seconds > 0 && (record.BestSeconds is not double s || run.Seconds < s - 0.05)) { record.BestSeconds = run.Seconds; fastest = true; }
        }
        return new(true, step, won, true, won, hp, max, 0, best, fastest);
    }
}
