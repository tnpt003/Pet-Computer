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
// battle.json "expedition": the map's node kinds per step ("fight", "elite", "rest", "event"), the
// encounter pools the run draws from, the boss, the events and relics (milestone 3), and the run's
// rules. Ids are stable: a saved run names them.
public sealed class ExpeditionSpec
{
    public const string Fight = "fight", Elite = "elite", Rest = "rest", Event = "event";
    public static readonly string[] Kinds = { Fight, Elite, Rest, Event };
    public static bool IsBattle(string kind) => kind is Fight or Elite;

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
    // A rest node: RestHeal of the max HP back, or one card instead.
    public double RestHeal { get; init; } = 0.3;
    // Relics the run may hold at once, and how many a won elite offers to choose from.
    public int MaxRelics { get; init; } = 3;
    public int EliteRelics { get; init; } = 2;
    public ExpeditionEventSpec[] Events { get; init; } = Array.Empty<ExpeditionEventSpec>();
    public RelicSpec[] Relics { get; init; } = Array.Empty<RelicSpec>();

    public IReadOnlyList<StageSpec> Pool(string kind) => kind == Elite ? Elites : Fights;
    public StageSpec? Encounter(string id) => Fights.Concat(Elites).Append(Boss).FirstOrDefault(s => s?.Id == id);
    public ExpeditionEventSpec? FindEvent(string? id) => Events.FirstOrDefault(e => e.Id == id);
    public RelicSpec? Relic(string? id) => Relics.FirstOrDefault(r => r.Id == id);
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
        if (!double.IsFinite(x.RestHeal) || x.RestHeal is < 0 or > 1) throw new InvalidDataException(at + ".restHeal must be 0–1");
        if (x.MaxRelics is < 0 or > 6) throw new InvalidDataException(at + ".maxRelics must be 0–6");
        if (x.EliteRelics is < 0 or > 3) throw new InvalidDataException(at + ".eliteRelics must be 0–3");
        if (x.Relics == null || x.Relics.Length > 12) throw new InvalidDataException(at + ".relics must list at most 12 relics");
        static void Desc(string? vi, string? en, string where)
        {
            if (string.IsNullOrWhiteSpace(vi) || string.IsNullOrWhiteSpace(en) || vi.Length > 120 || en.Length > 120) throw new InvalidDataException(where + " descriptions must be 1–120 characters");
        }
        var relicIds = new HashSet<string>();
        foreach (var r in x.Relics)
        {
            string where = $"{at}.relics[{r?.Id}]";
            if (r == null) throw new InvalidDataException(at + ".relics has an empty entry");
            id(r.Id, where + ".id");
            if (!relicIds.Add(r.Id)) throw new InvalidDataException($"relic id \"{r.Id}\" is used twice");
            text(r.NameVi, where + ".nameVi"); text(r.NameEn, where + ".nameEn"); Desc(r.DescVi, r.DescEn, where);
            if (Array.IndexOf(RelicSpec.Effects, r.Effect) < 0) throw new InvalidDataException($"{where}.effect must be one of: {string.Join(", ", RelicSpec.Effects)}");
            foreach (var (value, name, max) in new[] { (r.Amount, "amount", 2.0), (r.Extra, "extra", 0.5), (r.Penalty, "penalty", r.Effect switch { RelicSpec.Light => 50.0, RelicSpec.Counter => 30.0, _ => 2.0 }), (r.Below, "below", 1.0) })
                if (!double.IsFinite(value) || value < 0 || value > max) throw new InvalidDataException($"{where}.{name} must be 0–{max}");
        }
        if (x.Events == null || x.Events.Length > 12) throw new InvalidDataException(at + ".events must list at most 12 events");
        var eventIds = new HashSet<string>();
        foreach (var e in x.Events)
        {
            string where = $"{at}.events[{e?.Id}]";
            if (e == null) throw new InvalidDataException(at + ".events has an empty entry");
            id(e.Id, where + ".id");
            if (!eventIds.Add(e.Id)) throw new InvalidDataException($"event id \"{e.Id}\" is used twice");
            text(e.NameVi, where + ".nameVi"); text(e.NameEn, where + ".nameEn"); Desc(e.DescVi, e.DescEn, where);
            if (Array.IndexOf(ExpeditionEventSpec.Kinds, e.Kind) < 0) throw new InvalidDataException($"{where}.kind must be one of: {string.Join(", ", ExpeditionEventSpec.Kinds)}");
            if (!double.IsFinite(e.Heal) || e.Heal is < 0 or > 1) throw new InvalidDataException(where + ".heal must be 0–1");
            if (!double.IsFinite(e.Cost) || e.Cost is < 0 or > 0.9) throw new InvalidDataException(where + ".cost must be 0–0.9");
            if (e.Kind != ExpeditionEventSpec.Swap && x.Relics.Length == 0) throw new InvalidDataException(where + ": a " + e.Kind + " event needs relics");
        }
        if (x.Steps == null || x.Steps.Length is < 1 or > 10) throw new InvalidDataException(at + ".steps must list 1–10 steps");
        for (int i = 0; i < x.Steps.Length; i++)
        {
            var step = x.Steps[i];
            if (step == null || step.Length is < 1 or > 3) throw new InvalidDataException($"{at}.steps[{i}] must list 1–3 nodes");
            foreach (var kind in step)
            {
                if (Array.IndexOf(Kinds, kind) < 0) throw new InvalidDataException($"{at}.steps[{i}] node kinds must be one of: {string.Join(", ", Kinds)}");
                // A step's nodes get different encounters (or events): its pool needs enough of them.
                int pool = kind == Event ? x.Events.Length : kind == Rest ? 1 : x.Pool(kind).Count;
                if (pool < step.Count(k => k == kind)) throw new InvalidDataException($"{at}.steps[{i}] has more {kind} nodes than {(kind == Rest ? "one" : kind + " encounters")}");
            }
        }
    }
}

// An event node (Expedition milestone 3): what it offers is said before choosing.
// swap: give up one card of the build for one of three new ones (or leave).
// chest: safe (Heal of the max HP back) or risky (Cost of the max HP for one relic drawn at random).
// shrine: one of two relics shown, for Cost of the max HP (or leave).
public sealed class ExpeditionEventSpec
{
    public const string Swap = "swap", Chest = "chest", Shrine = "shrine";
    public static readonly string[] Kinds = { Swap, Chest, Shrine };
    public string Id { get; init; } = "";
    public string Kind { get; init; } = Swap;
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public double Heal { get; init; }
    public double Cost { get; init; }
    public string Name(bool english) => english ? NameEn : NameVi;
    public string Describe(bool english) => english ? DescEn : DescVi;
}

// A relic: kept for the rest of the run only, a gain with a price (BattleSim applies it as its own
// modifier; the cards and skills are never changed).
// counter: after the Bubble shield or Turtle shell stops a hit, the next landed swing deals Amount
//   more; the Shield's cooldown is Penalty seconds longer.
// relief: the Healing fish heals Amount more; its cooldown is Penalty longer (a share).
// reckless: Amount more damage while HP is below Below of the max; Penalty more damage taken always.
// light: Amount more running speed and Extra less time between swings; Penalty less max HP.
public sealed class RelicSpec
{
    public const string Counter = "counter", Relief = "relief", Reckless = "reckless", Light = "light";
    public static readonly string[] Effects = { Counter, Relief, Reckless, Light };
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string Effect { get; init; } = Counter;
    public double Amount { get; init; }
    public double Extra { get; init; }
    public double Penalty { get; init; }
    public double Below { get; init; }
    public string Name(bool english) => english ? NameEn : NameVi;
    public string Describe(bool english) => english ? DescEn : DescVi;
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
    // The relics held (milestone 3), and the choice open now: a rest or event node visited, or the
    // relics a won elite offers. Its options were drawn once and are kept, so reopening the map,
    // closing the app or resuming never draws them again.
    public List<string> Relics { get; set; } = new();
    public ExpeditionChoice? Pending { get; set; }

    [JsonIgnore] public int Step => Path.Count;
    [JsonIgnore] public BattleDifficulty Level => Enum.TryParse<BattleDifficulty>(Difficulty, out var level) && Enum.IsDefined(level) ? level : BattleDifficulty.Normal;
}

// An open choice. Kind: "rest", "event" (Event: its id) at node Lane of the step to go now, or
// "reward" after a won elite (the step is already passed). Cards and Relics: what is offered.
public sealed class ExpeditionChoice
{
    public const string RestKind = "rest", EventKind = "event", RewardKind = "reward";
    public string Kind { get; set; } = RestKind;
    public int Lane { get; set; }
    public string? Event { get; set; }
    public string[] Cards { get; set; } = Array.Empty<string>();
    public string[] Relics { get; set; } = Array.Empty<string>();
}

// What resolving a choice did: HP gained (or lost), the card taken and dropped, the relic gained.
public readonly record struct ExpeditionChoiceOutcome(bool Done, string Option, double Hp = 0, string? Card = null, string? Dropped = null, string? Relic = null);

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
    // 2: relics and open choices (milestone 3). A version 1 file is read as is (its run ends when
    // the map opens: the rules changed); an older app reads this one without overwriting it.
    public const int CurrentVersion = 2;
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
            if (progress.Active is ExpeditionRun run)
            {
                run.Map ??= new(); run.Path ??= new(); run.Build ??= new(); run.Relics ??= new();
                if (run.Pending != null) { run.Pending.Cards ??= Array.Empty<string>(); run.Pending.Relics ??= Array.Empty<string>(); }
            }
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
// Reward: a won elite left relics to choose from on the map.
public readonly record struct ExpeditionStepOutcome(bool Counted, int Step, bool Won, bool RunOver, bool RunWon, double Hp, double MaxHp, double Healed = 0, bool BestSteps = false, bool Fastest = false, bool Reward = false);

// One battle node the user can fight now: its encounter, its kind, and the battle it starts.
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

    // The cat's max HP with a build (cards like Big heart raise it) and relics, as the battle would have it.
    public double MaxHp(IReadOnlyDictionary<string, int> build, IReadOnlyList<string>? relics = null) => new BattleSim(Settings, 0, 1000, build: build, seed: 0, relics: relics).CatMaxHp;
    public double MaxHp(ExpeditionRun run) => MaxHp(run.Build, run.Relics);

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
                if (step[lane] == ExpeditionSpec.Rest) { row[lane] = ExpeditionSpec.Rest; continue; }
                // Not one this step already has; among the rest, one used least so far.
                var options = (step[lane] == ExpeditionSpec.Event ? Spec.Events.Select(e => e.Id) : Spec.Pool(step[lane]).Select(s => s.Id)).Where(id => !row.Contains(id)).ToList();
                int least = options.Min(id => uses.GetValueOrDefault(id));
                options = options.Where(id => uses.GetValueOrDefault(id) == least).ToList();
                var pick = options[random.Next(options.Count)];
                row[lane] = pick;
                uses[pick] = least + 1;
            }
            map.Add(row);
        }
        map.Add(new[] { Spec.Boss!.Id });
        var run = new ExpeditionRun
        {
            RunId = runId ?? Guid.NewGuid().ToString("N"), Expedition = Spec.Id, RulesVersion = Spec.RulesVersion, Seed = seed,
            Weapon = Settings.Weapon(weapon).Id, Difficulty = difficulty.ToString(), Map = map
        };
        run.Hp = MaxHp(run);
        Progress.Active = run;
        return run;
    }

    // Whether a node's content (an encounter id, "rest", an event id) fits its kind in this data.
    private bool Fits(string kind, string? id) => kind switch
    {
        ExpeditionSpec.Rest => id == ExpeditionSpec.Rest,
        ExpeditionSpec.Event => Spec.FindEvent(id) != null,
        BossKind => id == Spec.Boss?.Id,
        _ => Spec.Pool(kind).Any(s => s.Id == id)
    };

    // Null when the saved run still fits this data; otherwise why it cannot go on.
    public string? Mismatch(ExpeditionRun run)
    {
        if (run.Expedition != Spec.Id || run.RulesVersion != Spec.RulesVersion) return "the expedition's rules changed";
        if (run.Map.Count != Spec.StepCount || run.Path.Count >= run.Map.Count) return "the map changed";
        for (int i = 0; i < run.Map.Count; i++)
        {
            int lanes = i < Spec.Steps.Length ? Spec.Steps[i].Length : 1;
            if (run.Map[i] == null || run.Map[i].Length != lanes) return "the map changed";
            for (int lane = 0; lane < lanes; lane++) if (!Fits(Kind(i, lane), run.Map[i][lane])) return "the map changed";
        }
        if (run.Path.Where((lane, i) => lane < 0 || lane >= run.Map[i].Length).Any()) return "the map changed";
        if (run.Fighting is int f && (f < 0 || f >= run.Map[run.Step].Length || !ExpeditionSpec.IsBattle(Kind(run.Step, f)) && Kind(run.Step, f) != BossKind)) return "the map changed";
        if (run.Relics.Any(id => Spec.Relic(id) == null)) return "the relics changed";
        if (run.Pending is ExpeditionChoice open)
        {
            bool node = open.Kind is ExpeditionChoice.RestKind or ExpeditionChoice.EventKind;
            if (run.Fighting != null || open.Kind is not (ExpeditionChoice.RestKind or ExpeditionChoice.EventKind or ExpeditionChoice.RewardKind)
                || node && (open.Lane < 0 || open.Lane >= run.Map[run.Step].Length || Kind(run.Step, open.Lane) != open.Kind || open.Kind == ExpeditionChoice.EventKind && run.Map[run.Step][open.Lane] != open.Event)
                || open.Relics.Any(id => Spec.Relic(id) == null) || open.Cards.Any(id => Settings.UpgradeList.All(u => u.Id != id)))
                return "the map changed";
        }
        return null;
    }

    public string Kind(int step, int lane) => step >= Spec.Steps.Length ? BossKind : Spec.Steps[step][lane];

    // A battle node of the step to go now (null: not one, or a choice is still open).
    public ExpeditionNode? Node(int lane)
    {
        if (Run is not ExpeditionRun run || run.Pending != null || run.Step >= run.Map.Count || lane < 0 || lane >= run.Map[run.Step].Length) return null;
        if (!ExpeditionSpec.IsBattle(Kind(run.Step, lane)) && Kind(run.Step, lane) != BossKind) return null;
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
        if (!won) return End(run, false, step, 0, MaxHp(run));
        run.Path.Add(lane);
        run.Build = build.Where(card => card.Value > 0).ToDictionary(card => card.Key, card => card.Value);
        double max = MaxHp(run);
        if (run.Step >= run.Map.Count) return End(run, true, step, Math.Clamp(hp, 0, max), max);
        double before = Math.Clamp(hp, 1, max);
        run.Hp = Math.Min(max, before + Spec.WinHeal * max);
        // A won elite: relics to choose from (drawn now, kept with the run).
        bool reward = false;
        if (Kind(step, lane) == ExpeditionSpec.Elite && DrawRelics(run, Spec.EliteRelics, BattleRandom.Stream(run.Seed, 4000 + step * 8 + lane)) is { Length: > 0 } relics)
        {
            run.Pending = new() { Kind = ExpeditionChoice.RewardKind, Lane = lane, Relics = relics };
            reward = true;
        }
        return new(true, step, true, false, false, run.Hp, max, run.Hp - before, Reward: reward);
    }

    // Up to count relics the run does not hold, in a drawn order; none once it holds MaxRelics.
    private string[] DrawRelics(ExpeditionRun run, int count, Random random)
    {
        if (run.Relics.Count >= Spec.MaxRelics) return Array.Empty<string>();
        var left = Spec.Relics.Select(r => r.Id).Where(id => !run.Relics.Contains(id)).ToList();
        var drawn = new List<string>();
        while (drawn.Count < count && left.Count > 0) { int i = random.Next(left.Count); drawn.Add(left[i]); left.RemoveAt(i); }
        return drawn.ToArray();
    }

    // Cards offered outside a battle (a rest, a swap): the battle's own offer for this build, from a
    // battle seeded by the node, at full HP (no instant heal: resting is the heal).
    private string[] DrawCards(ExpeditionRun run, int seed)
    {
        var sim = new BattleSim(Settings, 0, 1000, build: run.Build, seed: seed, relics: run.Relics);
        return sim.OfferCards()?.Select(u => u.Id).ToArray() ?? Array.Empty<string>();
    }

    // Go to a rest or event node of the step to go now: its choice opens (drawn once, kept).
    public bool Visit(int lane)
    {
        if (Run is not ExpeditionRun run || run.Pending != null || run.Fighting != null || run.Step >= run.Map.Count || lane < 0 || lane >= run.Map[run.Step].Length) return false;
        string kind = Kind(run.Step, lane);
        if (kind is not (ExpeditionSpec.Rest or ExpeditionSpec.Event)) return false;
        var random = BattleRandom.Stream(run.Seed, 3000 + run.Step * 8 + lane);
        var choice = new ExpeditionChoice { Kind = kind, Lane = lane };
        if (kind == ExpeditionSpec.Rest) choice.Cards = DrawCards(run, random.Next());
        else
        {
            var e = Spec.FindEvent(run.Map[run.Step][lane])!;
            choice.Event = e.Id;
            if (e.Kind == ExpeditionEventSpec.Swap) choice.Cards = DrawCards(run, random.Next());
            else choice.Relics = DrawRelics(run, e.Kind == ExpeditionEventSpec.Chest ? 1 : 2, random);
        }
        run.Pending = choice;
        return true;
    }

    public const string Heal = "heal", Card = "card", Safe = "safe", Risk = "risk", SwapOption = "swap", Take = "take", Leave = "leave";

    // The options the open choice has: rest (heal, card), swap (swap, leave), chest (safe, risk),
    // shrine and a won elite (take, leave).
    public string[] Options()
    {
        if (Run?.Pending is not ExpeditionChoice open) return Array.Empty<string>();
        if (open.Kind == ExpeditionChoice.RestKind) return new[] { Heal, Card };
        if (open.Kind == ExpeditionChoice.RewardKind) return new[] { Take, Leave };
        return Spec.FindEvent(open.Event)?.Kind switch
        {
            ExpeditionEventSpec.Swap => new[] { SwapOption, Leave },
            ExpeditionEventSpec.Chest => new[] { Safe, Risk },
            _ => new[] { Take, Leave }
        };
    }

    // HP an option costs (a share of the max HP; 0: free).
    public double Cost(string option)
    {
        if (Run is not ExpeditionRun run || run.Pending is not ExpeditionChoice open || open.Kind != ExpeditionChoice.EventKind) return 0;
        var e = Spec.FindEvent(open.Event);
        return e != null && (option == Risk && e.Kind == ExpeditionEventSpec.Chest || option == Take && e.Kind == ExpeditionEventSpec.Shrine) ? e.Cost * MaxHp(run) : 0;
    }

    // HP an option gives back now.
    public double Gain(string option)
    {
        if (Run is not ExpeditionRun run || run.Pending is not ExpeditionChoice open) return 0;
        double max = MaxHp(run), share = option == Heal && open.Kind == ExpeditionChoice.RestKind ? Spec.RestHeal
            : option == Safe ? Spec.FindEvent(open.Event)?.Heal ?? 0 : 0;
        return Math.Max(0, Math.Min(share * max, max - run.Hp));
    }

    // Null when the option can be taken now (with this card, card to drop, relic), otherwise why not.
    public string? Refusal(string option, string? card = null, string? drop = null, string? relic = null)
    {
        if (Run is not ExpeditionRun run || run.Pending is not ExpeditionChoice open || !Options().Contains(option)) return "no such choice";
        switch (option)
        {
            case Card:
                return open.Cards.Contains(card) ? null : open.Cards.Length == 0 ? "no cards" : "pick a card";
            case SwapOption:
                if (run.Build.Count == 0) return "no card to give up";
                if (drop == null || !run.Build.ContainsKey(drop)) return "pick a card to give up";
                return open.Cards.Contains(card) ? null : "pick a card";
            case Risk:
            case Take:
                if (open.Relics.Length == 0 || run.Relics.Count >= Spec.MaxRelics) return "no relic";
                if (Cost(option) > 0 && run.Hp - Cost(option) < 1) return "too weak";
                return option == Risk || open.Relics.Contains(relic) ? null : "pick a relic";
            default:
                return null;
        }
    }

    // Resolve the open choice once. A node's choice passes its step; a won elite's only closes.
    public ExpeditionChoiceOutcome Choose(string option, string? card = null, string? drop = null, string? relic = null)
    {
        if (Refusal(option, card, drop, relic) != null || Run is not ExpeditionRun run || run.Pending is not ExpeditionChoice open) return default;
        double before = run.Hp;
        run.Hp = Math.Max(1, run.Hp - Cost(option)) + Gain(option);
        string? taken = null, dropped = null, gained = null;
        if (option is Card or SwapOption)
        {
            if (option == SwapOption)
            {
                dropped = drop;
                if (--run.Build[drop!] <= 0) run.Build.Remove(drop!);
            }
            taken = card;
            run.Build[card!] = run.Build.GetValueOrDefault(card!) + 1;
            // More max HP comes with HP now, as it does in a battle.
            if (Settings.UpgradeList.FirstOrDefault(u => u.Id == card) is { Effect: "maxHp" } heart) run.Hp += heart.Amount;
        }
        if (option is Risk or Take)
        {
            gained = option == Risk ? open.Relics[0] : relic;
            run.Relics.Add(gained!);
        }
        run.Hp = Math.Min(run.Hp, MaxHp(run));
        if (open.Kind != ExpeditionChoice.RewardKind) run.Path.Add(open.Lane);
        run.Pending = null;
        return new(true, option, run.Hp - before, taken, dropped, gained);
    }

    // The step's battle was left (End, closing, a crash): the step stays to be fought again, for
    // RetreatCost of the max HP. HP gone ends the run as lost.
    public ExpeditionStepOutcome Retreat()
    {
        if (Run is not ExpeditionRun run || run.Fighting == null) return default;
        run.Fighting = null;
        run.Retreats++;
        double max = MaxHp(run);
        run.Hp -= Spec.RetreatCost * max;
        if (run.Hp <= 0.5) return End(run, false, run.Step, 0, max);
        return new(true, run.Step, false, false, false, run.Hp, max, -Spec.RetreatCost * max);
    }

    // The user gives the run up: counted as ended where it is.
    public ExpeditionStepOutcome Abandon()
    {
        if (Run is not ExpeditionRun run) return default;
        return End(run, false, run.Step, run.Hp, MaxHp(run));
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
