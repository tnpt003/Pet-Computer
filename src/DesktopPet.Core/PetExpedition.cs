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
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
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
    // The node kinds of each step before the boss, in lane order (fixed for every run). Either this
    // or KindGacha: then each step's kinds are drawn when the run reaches it.
    public string[][] Steps { get; init; } = Array.Empty<string[]>();
    public KindGachaSpec? KindGacha { get; init; }
    // The capsule machine after each won fight node (null: none).
    public CapsuleSpec? Capsules { get; init; }
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
    // Keepsakes (milestone 4): given once each, for a won run at their difficulty.
    public KeepsakeSpec[] Keepsakes { get; init; } = Array.Empty<KeepsakeSpec>();

    public IReadOnlyList<StageSpec> Pool(string kind) => kind == Elite ? Elites : Fights;
    public StageSpec? Encounter(string id) => Fights.Concat(Elites).Append(Boss).FirstOrDefault(s => s?.Id == id);
    public ExpeditionEventSpec? FindEvent(string? id) => Events.FirstOrDefault(e => e.Id == id);
    public RelicSpec? Relic(string? id) => Relics.FirstOrDefault(r => r.Id == id);
    public KeepsakeSpec? Keepsake(string? id) => Keepsakes.FirstOrDefault(k => k.Id == id);
    // Steps before the boss, and including the boss's.
    [JsonIgnore] public int PathSteps => KindGacha?.Steps ?? Steps.Length;
    [JsonIgnore] public int StepCount => PathSteps + 1;
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
            if (r.Skill != null && BattleSkills.Parse(r.Skill) == null) throw new InvalidDataException($"{where}.skill must be a skill id: {BattleData.KnownSkills}");
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
        if (x.Keepsakes == null || x.Keepsakes.Length > 12) throw new InvalidDataException(at + ".keepsakes must list at most 12 keepsakes");
        var keepIds = new HashSet<string>();
        foreach (var k in x.Keepsakes)
        {
            string where = $"{at}.keepsakes[{k?.Id}]";
            if (k == null) throw new InvalidDataException(at + ".keepsakes has an empty entry");
            id(k.Id, where + ".id");
            if (!keepIds.Add(k.Id)) throw new InvalidDataException($"keepsake id \"{k.Id}\" is used twice");
            text(k.NameVi, where + ".nameVi"); text(k.NameEn, where + ".nameEn");
            if (Array.IndexOf(KeepsakeSpec.Kinds, k.Kind) < 0) throw new InvalidDataException($"{where}.kind must be one of: {string.Join(", ", KeepsakeSpec.Kinds)}");
            if (k.Kind == KeepsakeSpec.Cup && Array.IndexOf(KeepsakeSpec.Tiers, k.Tier) < 0) throw new InvalidDataException($"{where}.tier must be one of: {string.Join(", ", KeepsakeSpec.Tiers)}");
            if (!Enum.TryParse<BattleDifficulty>(k.Difficulty, out var level) || !Enum.IsDefined(level)) throw new InvalidDataException(where + ".difficulty must be Easy, Normal or Hard");
            if (k.Kind == KeepsakeSpec.AccessoryKind && PetAccessories.Find(k.Accessory) is not { Reward: not null })
                throw new InvalidDataException(where + ".accessory must name a reward accessory of accessories.json");
        }
        if (x.Capsules is CapsuleSpec c)
        {
            foreach (var (value, name) in new[] { (c.FreeHeal, "freeHeal"), (c.FreeCard, "freeCard"), (c.GoldHeal, "goldHeal") })
                if (!double.IsFinite(value) || value is < 0 or > 1) throw new InvalidDataException($"{at}.capsules.{name} must be 0–1");
            if (!double.IsFinite(c.GoldCost) || c.GoldCost is < 0 or > 0.9) throw new InvalidDataException(at + ".capsules.goldCost must be 0–0.9");
            foreach (var (value, name) in new[] { (c.Relic, "relic"), (c.Rare, "rare"), (c.CardHeal, "cardHeal") })
                if (!double.IsFinite(value) || value is < 0 or > 1000) throw new InvalidDataException($"{at}.capsules.{name} must be 0–1000");
            if (c.Relic + c.Rare + c.CardHeal <= 0) throw new InvalidDataException(at + ".capsules needs a gold weight above 0");
        }
        if (x.KindGacha is KindGachaSpec g)
        {
            if (x.Steps is { Length: > 0 }) throw new InvalidDataException(at + " has both steps and kindGacha: use one");
            if (g.Steps is < 1 or > 10) throw new InvalidDataException(at + ".kindGacha.steps must be 1–10");
            if (g.MinLanes is < 1 or > 3 || g.MaxLanes is < 1 or > 3 || g.MinLanes > g.MaxLanes) throw new InvalidDataException(at + ".kindGacha lanes must be 1–3, minLanes ≤ maxLanes");
            if (g.Weights == null || g.Weights.Count == 0) throw new InvalidDataException(at + ".kindGacha.weights must list node kinds");
            if (!double.IsFinite(g.RerollCost) || g.RerollCost is < 0 or > 0.5) throw new InvalidDataException(at + ".kindGacha.rerollCost must be 0–0.5");
            foreach (var (kind, weight) in g.Weights)
            {
                if (Array.IndexOf(Kinds, kind) < 0) throw new InvalidDataException($"{at}.kindGacha.weights kinds must be one of: {string.Join(", ", Kinds)}");
                if (!double.IsFinite(weight) || weight is < 0 or > 1000) throw new InvalidDataException($"{at}.kindGacha.weights.{kind} must be 0–1000");
            }
            if (g.Weights.GetValueOrDefault(Fight) <= 0) throw new InvalidDataException(at + ".kindGacha.weights.fight must be above 0");
            if (g.Weights.GetValueOrDefault(Event) > 0 && x.Events.Length == 0) throw new InvalidDataException(at + ".kindGacha draws events but there are none");
            if (g.Weights.GetValueOrDefault(Elite) > 0 && x.Elites.Length == 0) throw new InvalidDataException(at + ".kindGacha draws elites but there are none");
            return;
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

// The node-kind gacha: a run's map shows only the boss at first; each step's nodes (MinLanes to
// MaxLanes of them) are drawn when the run reaches that step (the map's Spin), by Weights, and kept.
// The draw always follows these rules: step 1 has no elite or rest; a step has at most one rest and
// never only elites; the step before the boss has a rest; never more nodes of a kind than its pool.
// RerollCost: spinning a drawn step again (once a step, before going to one of its nodes) costs
// this share of the max HP; 0: no second spin.
public sealed class KindGachaSpec
{
    public int Steps { get; init; } = 5;
    public int MinLanes { get; init; } = 2;
    public int MaxLanes { get; init; } = 3;
    public Dictionary<string, double> Weights { get; init; } = new();
    public double RerollCost { get; init; }
}

// The capsule machine after a won fight node (not an elite: it leaves relics; not the boss). Both
// capsules are drawn when the battle is won and kept, shown only once opened; the user picks before
// knowing. Free: a common card from the battle's offer (FreeCard of the time) or FreeHeal of the max
// HP. Gold, for GoldCost of the max HP: by weight, a relic (while the run has room), a rare card it
// can still take, or a card with GoldHeal of the max HP.
public sealed class CapsuleSpec
{
    public double FreeHeal { get; init; } = 0.1;
    public double FreeCard { get; init; } = 0.5;
    public double GoldCost { get; init; } = 0.12;
    public double GoldHeal { get; init; } = 0.1;
    public double Relic { get; init; } = 40;
    public double Rare { get; init; } = 30;
    public double CardHeal { get; init; } = 30;
}

// A keepsake (Expedition milestone 4): given once, the first time a run is won at Difficulty or
// harder (only at it when Exact). cup: a trophy (Tier bronze, silver, gold) on the display corner;
// cushion: a slime cushion there; accessory: a reward accessory (accessories.json) the cat can wear;
// hourglass, orb: Infinity's displayed keepsakes (EndlessSpec.Milestones, given for Wave instead).
public sealed class KeepsakeSpec
{
    public const string Cup = "cup", Cushion = "cushion", AccessoryKind = "accessory", Hourglass = "hourglass", Orb = "orb";
    public static readonly string[] Kinds = { Cup, Cushion, AccessoryKind, Hourglass, Orb };
    // Infinity's milestones only: the wave to clear (0 in the Expedition).
    public int Wave { get; init; }
    public static readonly string[] Tiers = { "bronze", "silver", "gold" };
    public string Id { get; init; } = "";
    public string Kind { get; init; } = Cup;
    public string? Tier { get; init; }
    public string Difficulty { get; init; } = nameof(BattleDifficulty.Easy);
    public bool Exact { get; init; }
    public string? Accessory { get; init; }
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    [JsonIgnore] public bool Shown => Kind is Cup or Cushion or Hourglass or Orb;
    public bool EarnedAt(BattleDifficulty level) =>
        Enum.TryParse<BattleDifficulty>(Difficulty, out var at) && (Exact ? level == at : level >= at);
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
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    public string Describe(string language) => PetText.Data(language, DescVi, DescEn);
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
    // A skill id ("shield", "heal"…): only offered to a run that carries that skill.
    public string? Skill { get; init; }
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    public string Describe(string language) => PetText.Data(language, DescVi, DescEn);
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
    // The app icons were turned off when the run set out: none in its battles, its records kept apart.
    public bool NoApps { get; set; }
    // The five skills the run set out with (null: BattleSkills.Default; read through BattleSkills.Loadout).
    public List<string>? Skills { get; set; }
    [JsonIgnore] public BattleSkill[] Loadout => BattleSkills.Loadout(Skills);
    // Encounter ids per step, in lane order (the boss's step last). With the kind gacha a step not
    // drawn yet is empty, and Kinds holds each drawn step's node kinds (null: the data's fixed steps).
    public List<string[]> Map { get; set; } = new();
    public List<string[]>? Kinds { get; set; }
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
    // What happened so far (won battles, left battles, choices made), for the Bag's run log. Only
    // shown: the rules never read it, and a file without it reads as an empty log.
    public List<ExpeditionLogEntry> Log { get; set; } = new();
    public const int LogKept = 60;
    // Steps spun a second time (each at most once).
    public List<int> Rerolled { get; set; } = new();
    // 1.5 M19: the coins the run's won battles picked up (BattleSim.Coins), for its shop; they end with
    // the run (never kept past it). A file without it reads as 0.
    public int Coins { get; set; }

    [JsonIgnore] public int Step => Path.Count;
    [JsonIgnore] public BattleDifficulty Level => Enum.TryParse<BattleDifficulty>(Difficulty, out var level) && Enum.IsDefined(level) ? level : BattleDifficulty.Normal;
}

// An open choice. Kind: "rest", "event" (Event: its id) at node Lane of the step to go now, or
// "reward" after a won elite / "capsule" after a won fight (the step is already passed). Cards and
// Relics: what is offered. Free and Gold: a capsule's hidden prizes ("heal", "card:<id>",
// "rare:<id>", "relic:<id>").
public sealed class ExpeditionChoice
{
    public const string RestKind = "rest", EventKind = "event", RewardKind = "reward", CapsuleKind = "capsule";
    public string Kind { get; set; } = RestKind;
    public int Lane { get; set; }
    public string? Event { get; set; }
    public string[] Cards { get; set; } = Array.Empty<string>();
    public string[] Relics { get; set; } = Array.Empty<string>();
    public string? Free { get; set; }
    public string? Gold { get; set; }
}

// One line of a run's log. Kind: a won battle's node kind (fight, elite, boss), "retreat" (a battle
// left), or the choice's kind (rest, event, reward, capsule) with the option taken and what it gave.
// Hp: what the run's HP changed by.
public sealed class ExpeditionLogEntry
{
    public const string Retreat = "retreat", Reroll = "reroll";
    public int Step { get; set; }
    public string Kind { get; set; } = "";
    public string? Option { get; set; }
    public string? Event { get; set; }
    public string? Card { get; set; }
    public string? Dropped { get; set; }
    public string? Relic { get; set; }
    public string? Prize { get; set; }
    public double Hp { get; set; }
}

// What resolving a choice did: HP gained (or lost), the card taken and dropped, the relic gained,
// and for a capsule the prize it held.
public readonly record struct ExpeditionChoiceOutcome(bool Done, string Option, double Hp = 0, string? Card = null, string? Dropped = null, string? Relic = null, string? Prize = null);

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
    // 2: relics and open choices (milestone 3). 3: keepsakes (milestone 4). 4: the kind gacha and
    // capsules (run Kinds, choice Free/Gold). An older file is read as is (an older run ends when the
    // map opens: the rules changed); an older app reads this one without overwriting it.
    public const int CurrentVersion = 4;
    public int Version { get; set; } = CurrentVersion;
    public ExpeditionRun? Active { get; set; }
    public Dictionary<string, ExpeditionRecord> Records { get; set; } = new();
    // The last finished runs already counted, so one is never counted twice.
    public List<string> Runs { get; set; } = new();
    // Keepsake ids given (each once, never taken back).
    public List<string> Keepsakes { get; set; } = new();
    public const int RunsKept = 50;
    [JsonIgnore] public bool ReadOnly { get; private set; }

    public static string DefaultPath => Path.Combine(PetPreferences.DataDirectory, "expedition.json");

    public static ExpeditionProgress Load(string path)
    {
        var progress = JsonStore.ReadOrBackup<ExpeditionProgress>(path, "Expedition progress");
        if (progress == null) return new();
        progress.ReadOnly = progress.Version > CurrentVersion;
        if (!progress.ReadOnly) progress.Version = CurrentVersion;
        progress.Records = (progress.Records ?? new()).Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null).ToDictionary(pair => pair.Key, pair => pair.Value);
        progress.Runs = (progress.Runs ?? new()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().TakeLast(RunsKept).ToList();
        progress.Keepsakes = (progress.Keepsakes ?? new()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (progress.Active is ExpeditionRun run)
        {
            run.Map ??= new(); run.Path ??= new(); run.Build ??= new(); run.Relics ??= new(); run.Rerolled ??= new();
            run.Log = (run.Log ?? new()).Where(e => e != null).TakeLast(ExpeditionRun.LogKept).ToList();
            if (run.Pending != null) { run.Pending.Cards ??= Array.Empty<string>(); run.Pending.Relics ??= Array.Empty<string>(); }
        }
        return progress;
    }

    public bool Save(string path)
    {
        if (ReadOnly)
        {
            Trace.WriteLine("Expedition progress comes from a newer version of the pet; it is not overwritten.");
            return false;
        }
        return JsonStore.Write(path, this, "Expedition progress", indented: true, backup: true);
    }
}

// What one step's battle (or leaving it) changed. RunOver: the run ended (Won: at the boss).
// Reward: a won elite left relics to choose from on the map.
// Keepsakes: the ones this won run gave (each only ever once).
// Capsule: a won fight left the capsule machine on the map.
public readonly record struct ExpeditionStepOutcome(bool Counted, int Step, bool Won, bool RunOver, bool RunWon, double Hp, double MaxHp, double Healed = 0, bool BestSteps = false, bool Fastest = false, bool Reward = false,
    IReadOnlyList<string>? Keepsakes = null, bool Capsule = false, int Coins = 0, int CoinsGained = 0);

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
    // False with the user's own battle data: its runs give no keepsakes.
    public bool GrantKeepsakes { get; init; } = true;
    public bool IsUnlocked(AdventureProgress adventure) => Spec.UnlockAfter is not string stage || adventure.Completed.Contains(stage)
        || Settings.Campaign?.Chapters.Any(c => c.Stages.Any(s => s.Id == stage)) != true;
    public static string RecordKey(ExpeditionSpec spec, string weapon, BattleDifficulty difficulty, bool noApps = false) =>
        $"{spec.Id}|r{spec.RulesVersion}|{weapon}|{difficulty}{(noApps ? "|noapps" : "")}";
    public ExpeditionRecord Record(string weapon, BattleDifficulty difficulty, bool noApps = false) =>
        Progress.Records.TryGetValue(RecordKey(Spec, weapon, difficulty, noApps), out var record) ? record : new();

    // The cat's max HP with a build (cards like Big heart raise it) and relics, as the battle would have it.
    public double MaxHp(IReadOnlyDictionary<string, int> build, IReadOnlyList<string>? relics = null) => new BattleSim(Settings, 0, 1000, build: build, seed: 0, relics: relics).CatMaxHp;
    public double MaxHp(ExpeditionRun run) => MaxHp(run.Build, run.Relics);

    // A new run (replacing none: the caller ends the old one first). The map is drawn from the
    // seed once and kept, so closing and opening the map, or resuming, never draws it again.
    // With the kind gacha only the boss is drawn now; each step is drawn by Roll when the run gets there.
    public ExpeditionRun Start(string weapon, BattleDifficulty difficulty, int seed, string? runId = null, bool noApps = false, IReadOnlyList<BattleSkill>? skills = null)
    {
        var random = BattleRandom.Stream(seed, 50);
        var map = new List<string[]>();
        List<string[]>? kinds = null;
        if (Spec.KindGacha != null)
        {
            kinds = Enumerable.Range(0, Spec.PathSteps).Select(_ => Array.Empty<string>()).ToList();
            map.AddRange(kinds.Select(_ => Array.Empty<string>()));
        }
        else foreach (var step in Spec.Steps) map.Add(DrawEncounters(step, map, random));
        map.Add(new[] { Spec.Boss!.Id });
        var run = new ExpeditionRun
        {
            RunId = runId ?? Guid.NewGuid().ToString("N"), Expedition = Spec.Id, RulesVersion = Spec.RulesVersion, Seed = seed,
            Weapon = Settings.Weapon(weapon).Id, Difficulty = difficulty.ToString(), NoApps = noApps, Map = map, Kinds = kinds,
            Skills = BattleSkills.Save(BattleSkills.Order(skills ?? BattleSkills.Default) ?? BattleSkills.Default)
        };
        run.Hp = MaxHp(run);
        Progress.Active = run;
        return run;
    }

    // A step's content for its node kinds: an encounter or event each (never one the step already
    // has; among the rest, one the run has used least so far), "rest" for a rest.
    private string[] DrawEncounters(IReadOnlyList<string> kinds, IEnumerable<string[]> before, Random random)
    {
        var uses = new Dictionary<string, int>();
        foreach (var id in before.SelectMany(row => row)) uses[id] = uses.GetValueOrDefault(id) + 1;
        var row = new string[kinds.Count];
        for (int lane = 0; lane < kinds.Count; lane++)
        {
            if (kinds[lane] == ExpeditionSpec.Rest) { row[lane] = ExpeditionSpec.Rest; continue; }
            var options = (kinds[lane] == ExpeditionSpec.Event ? Spec.Events.Select(e => e.Id) : Spec.Pool(kinds[lane]).Select(s => s.Id)).Where(id => !row.Contains(id)).ToList();
            int least = options.Min(id => uses.GetValueOrDefault(id));
            options = options.Where(id => uses.GetValueOrDefault(id) == least).ToList();
            var pick = options[random.Next(options.Count)];
            row[lane] = pick;
            uses[pick] = least + 1;
        }
        return row;
    }

    // The step to go now still has to be drawn (kind gacha: the map's Spin).
    public bool NeedsRoll => Run is ExpeditionRun run && run.Kinds != null && run.Pending == null && run.Fighting == null
        && run.Step < Spec.PathSteps && run.Step < run.Kinds.Count && run.Kinds[run.Step].Length == 0;

    // Whether a step's nodes are known (always, without the kind gacha).
    public bool Rolled(int step) => step >= Spec.PathSteps || Run?.Kinds is not List<string[]> kinds || step < kinds.Count && kinds[step].Length > 0;

    // Spin: the step to go now gets its node kinds and encounters, from the run's seed (the same
    // whenever it is drawn) and kept. False when there is nothing to draw (a double click).
    public bool Roll()
    {
        if (!NeedsRoll || Spec.KindGacha is null) return false;
        Draw(Run!, Run!.Step, BattleRandom.Stream(Run.Seed, 6000 + Run.Step));
        return true;
    }

    // Spin again: the price in HP (RerollCost of the max HP), and whether the step to go now can be
    // spun again — drawn, not spun again yet, no node entered or visited, and the cat survives it.
    public double RerollPrice(ExpeditionRun run) => Math.Round(MaxHp(run) * (Spec.KindGacha?.RerollCost ?? 0));
    public bool CanReroll => Run is ExpeditionRun run && Spec.KindGacha is { RerollCost: > 0 } && run.Kinds != null && run.Pending == null
        && run.Fighting == null && run.Step < Spec.PathSteps && run.Step < run.Kinds.Count && run.Kinds[run.Step].Length > 0
        && !run.Rerolled.Contains(run.Step) && run.Hp > RerollPrice(run);

    // The step's nodes drawn again from the run's seed (another stream than the first spin, so the
    // same whenever it happens; a draw equal to the old one is skipped), paid in HP and logged.
    public bool Reroll()
    {
        if (!CanReroll) return false;
        var run = Run!;
        int step = run.Step;
        double price = RerollPrice(run);
        var oldKinds = run.Kinds![step];
        var oldMap = run.Map[step];
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Draw(run, step, BattleRandom.Stream(run.Seed, 7000 + step * 8 + attempt));
            if (!run.Kinds[step].SequenceEqual(oldKinds) || !run.Map[step].SequenceEqual(oldMap)) break;
        }
        run.Hp -= price;
        run.Rerolled.Add(step);
        AddLog(run, new() { Step = step, Kind = ExpeditionLogEntry.Reroll, Hp = -price });
        return true;
    }

    // A step's node kinds and encounters, drawn by the kind gacha's rules and kept in the run.
    private void Draw(ExpeditionRun run, int step, Random random)
    {
        var g = Spec.KindGacha!;
        bool first = step == 0, beforeBoss = step == Spec.PathSteps - 1 && !first;
        int lanes = random.Next(g.MinLanes, g.MaxLanes + 1);
        var kinds = new List<string>();
        if (beforeBoss) kinds.Add(ExpeditionSpec.Rest);
        while (kinds.Count < lanes)
        {
            var allowed = ExpeditionSpec.Kinds.Where(kind => g.Weights.GetValueOrDefault(kind) > 0 && !(first && kind is ExpeditionSpec.Elite or ExpeditionSpec.Rest)
                && kinds.Count(k => k == kind) < (kind == ExpeditionSpec.Rest ? 1 : kind == ExpeditionSpec.Event ? Spec.Events.Length : Spec.Pool(kind).Count)).ToList();
            if (allowed.Count == 0) break;
            double roll = random.NextDouble() * allowed.Sum(kind => g.Weights[kind]);
            string pick = allowed[^1];
            foreach (var kind in allowed) { roll -= g.Weights[kind]; if (roll < 0) { pick = kind; break; } }
            kinds.Add(pick);
        }
        // Never only elites: the last one becomes a fight (the fights' pool always has one left then).
        if (kinds.All(k => k == ExpeditionSpec.Elite)) kinds[^1] = ExpeditionSpec.Fight;
        // Shuffled, so the rest before the boss is not always on top.
        for (int i = kinds.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (kinds[i], kinds[j]) = (kinds[j], kinds[i]); }
        run.Map[step] = DrawEncounters(kinds, run.Map.Take(step), random);
        run.Kinds![step] = kinds.ToArray();
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
        // A run drawn with the kind gacha needs data with it, and the other way round.
        if ((run.Kinds != null) != (Spec.KindGacha != null) || run.Kinds != null && run.Kinds.Count != Spec.PathSteps) return "the map changed";
        for (int i = 0; i < run.Map.Count; i++)
        {
            int lanes = i >= Spec.PathSteps ? 1 : run.Kinds?[i]?.Length ?? (Spec.KindGacha != null ? -1 : Spec.Steps[i].Length);
            if (run.Map[i] == null || lanes is < 0 or > 3 || run.Map[i].Length != lanes) return "the map changed";
            // Steps passed (and one being fought) were drawn.
            if (lanes == 0 && i <= run.Step && (i < run.Step || run.Fighting != null || run.Pending is { Kind: ExpeditionChoice.RestKind or ExpeditionChoice.EventKind })) return "the map changed";
            for (int lane = 0; lane < lanes; lane++) if (!Fits(KindOf(run, i, lane), run.Map[i][lane])) return "the map changed";
        }
        if (run.Path.Where((lane, i) => lane < 0 || lane >= run.Map[i].Length).Any()) return "the map changed";
        if (run.Fighting is int f && (f < 0 || f >= run.Map[run.Step].Length || !ExpeditionSpec.IsBattle(KindOf(run, run.Step, f)) && KindOf(run, run.Step, f) != BossKind)) return "the map changed";
        if (run.Relics.Any(id => Spec.Relic(id) == null)) return "the relics changed";
        if (run.Pending is ExpeditionChoice open)
        {
            bool node = open.Kind is ExpeditionChoice.RestKind or ExpeditionChoice.EventKind;
            bool PrizeFits(string? prize) => Prize(prize) switch
            {
                (Heal, _) => true,
                (PrizeCard or PrizeRare, string id) => Settings.UpgradeList.Any(u => u.Id == id),
                (PrizeRelic, string id) => Spec.Relic(id) != null,
                _ => false
            };
            if (run.Fighting != null || open.Kind is not (ExpeditionChoice.RestKind or ExpeditionChoice.EventKind or ExpeditionChoice.RewardKind or ExpeditionChoice.CapsuleKind)
                || node && (open.Lane < 0 || open.Lane >= run.Map[run.Step].Length || KindOf(run, run.Step, open.Lane) != open.Kind || open.Kind == ExpeditionChoice.EventKind && run.Map[run.Step][open.Lane] != open.Event)
                || open.Kind == ExpeditionChoice.CapsuleKind && (!PrizeFits(open.Free) || !PrizeFits(open.Gold))
                || open.Relics.Any(id => Spec.Relic(id) == null) || open.Cards.Any(id => Settings.UpgradeList.All(u => u.Id != id)))
                return "the map changed";
        }
        return null;
    }

    // A node's kind: "fight", "elite", "rest", "event", "boss", or "" for a node not drawn yet.
    public string Kind(int step, int lane) => KindOf(Run, step, lane);
    private string KindOf(ExpeditionRun? run, int step, int lane)
    {
        if (step >= Spec.PathSteps) return BossKind;
        if (Spec.KindGacha == null) return Spec.Steps[step][lane];
        return run?.Kinds is { } kinds && step < kinds.Count && lane >= 0 && lane < kinds[step].Length ? kinds[step][lane] : "";
    }

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
    // given back, its coins added); a won boss or any loss ends the run and counts it once.
    public ExpeditionStepOutcome Finish(bool won, double hp, IReadOnlyDictionary<string, int> build, double seconds, int defeated, int coins = 0)
    {
        if (Run is not ExpeditionRun run || run.Fighting is not int lane) return default;
        run.Fighting = null;
        if (won) run.Coins += Math.Max(0, coins);
        run.Seconds += double.IsFinite(seconds) && seconds > 0 ? seconds : 0;
        run.Defeated += Math.Max(0, defeated);
        int step = run.Step;
        if (!won) return End(run, false, step, 0, MaxHp(run));
        run.Path.Add(lane);
        run.Build = build.Where(card => card.Value > 0).ToDictionary(card => card.Key, card => card.Value);
        double max = MaxHp(run);
        if (run.Step >= run.Map.Count) return End(run, true, step, Math.Clamp(hp, 0, max), max);
        double before = Math.Clamp(hp, 1, max), started = run.Hp;
        run.Hp = Math.Min(max, before + Spec.WinHeal * max);
        AddLog(run, new() { Step = step, Kind = Kind(step, lane), Hp = run.Hp - started });
        // A won elite: relics to choose from (drawn now, kept with the run).
        bool reward = false, capsule = false;
        if (Kind(step, lane) == ExpeditionSpec.Elite && DrawRelics(run, Spec.EliteRelics, BattleRandom.Stream(run.Seed, 4000 + step * 8 + lane)) is { Length: > 0 } relics)
        {
            run.Pending = new() { Kind = ExpeditionChoice.RewardKind, Lane = lane, Relics = relics };
            reward = true;
        }
        // A won fight: the capsule machine (both capsules drawn now, kept hidden).
        else if (Kind(step, lane) == ExpeditionSpec.Fight && Spec.Capsules is CapsuleSpec c)
        {
            run.Pending = DrawCapsules(run, c, BattleRandom.Stream(run.Seed, 5000 + step * 8 + lane), lane);
            capsule = true;
        }
        return new(true, step, true, false, false, run.Hp, max, run.Hp - before, Reward: reward, Capsule: capsule, Coins: run.Coins, CoinsGained: Math.Max(0, coins));
    }

    public const string PrizeCard = "card", PrizeRare = "rare", PrizeRelic = "relic";
    // A capsule prize split into its kind and id: ("heal", null), ("card", id), ("rare", id), ("relic", id).
    public static (string Kind, string? Id) Prize(string? prize) =>
        prize == null ? ("", null) : prize.IndexOf(':') is int at and > 0 ? (prize[..at], prize[(at + 1)..]) : (prize, null);

    private ExpeditionChoice DrawCapsules(ExpeditionRun run, CapsuleSpec c, Random random, int lane)
    {
        bool Rare(string id) => Settings.UpgradeList.FirstOrDefault(u => u.Id == id)?.Rare == true;
        string free = Heal;
        var commons = DrawCards(run, random.Next()).Where(id => !Rare(id)).ToArray();
        if (random.NextDouble() < c.FreeCard && commons.Length > 0) free = PrizeCard + ":" + commons[random.Next(commons.Length)];
        var gold = new List<(string Prize, double Weight)>();
        if (DrawRelics(run, 1, random) is { Length: 1 } relic && c.Relic > 0) gold.Add((PrizeRelic + ":" + relic[0], c.Relic));
        var rares = Settings.UpgradeList.Where(u => u.Rare && (u.MaxStacks == 0 || run.Build.GetValueOrDefault(u.Id) < u.MaxStacks)).ToList();
        if (rares.Count > 0 && c.Rare > 0) gold.Add((PrizeRare + ":" + rares[random.Next(rares.Count)].Id, c.Rare));
        var cards = DrawCards(run, random.Next());
        if (cards.Length > 0 && c.CardHeal > 0) gold.Add((PrizeCard + ":" + cards[random.Next(cards.Length)], c.CardHeal));
        string goldPrize = Heal;
        double roll = random.NextDouble() * gold.Sum(g => g.Weight);
        foreach (var (prize, weight) in gold) { goldPrize = prize; roll -= weight; if (roll < 0) break; }
        return new() { Kind = ExpeditionChoice.CapsuleKind, Lane = lane, Free = free, Gold = goldPrize };
    }

    // Up to count relics the run does not hold, in a drawn order; none once it holds MaxRelics.
    private string[] DrawRelics(ExpeditionRun run, int count, Random random)
    {
        if (run.Relics.Count >= Spec.MaxRelics) return Array.Empty<string>();
        var carried = run.Loadout;
        var left = Spec.Relics.Where(r => r.Skill == null || Array.IndexOf(carried, BattleSkills.Parse(r.Skill)) >= 0)
            .Select(r => r.Id).Where(id => !run.Relics.Contains(id)).ToList();
        var drawn = new List<string>();
        while (drawn.Count < count && left.Count > 0) { int i = random.Next(left.Count); drawn.Add(left[i]); left.RemoveAt(i); }
        return drawn.ToArray();
    }

    // Cards offered outside a battle (a rest, a swap): the battle's own offer for this build, from a
    // battle seeded by the node, at full HP (no instant heal: resting is the heal).
    private string[] DrawCards(ExpeditionRun run, int seed)
    {
        var sim = new BattleSim(Settings, 0, 1000, build: run.Build, seed: seed, relics: run.Relics, skills: run.Loadout);
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

    // A rest open (1.5 M06): the run's five skills may change for free, as often as wanted; the heal or
    // card is still to choose. A card on offer for a skill no longer carried is taken out (no new draw).
    // open: whether a skill may be carried (the setup panel's rule). False: no rest open, not five
    // different skills, or one not open; the same five again changes nothing.
    public bool ChangeSkills(IReadOnlyList<BattleSkill> skills, Func<BattleSkill, bool> open)
    {
        if (Run is not ExpeditionRun run || run.Pending is not { Kind: ExpeditionChoice.RestKind } rest || run.Fighting != null
            || BattleSkills.Order(skills) is not BattleSkill[] ordered || !ordered.All(open)) return false;
        run.Skills = BattleSkills.Save(ordered);
        rest.Cards = rest.Cards.Where(id => Settings.UpgradeList.FirstOrDefault(u => u.Id == id) is not { Skill: string need }
            || BattleSkills.Parse(need) is BattleSkill skill && Array.IndexOf(ordered, skill) >= 0).ToArray();
        return true;
    }

    public const string Heal = "heal", Card = "card", Safe = "safe", Risk = "risk", SwapOption = "swap", Take = "take", Leave = "leave", Free = "free", Gold = "gold";

    // The options the open choice has: rest (heal, card), swap (swap, leave), chest (safe, risk),
    // shrine and a won elite (take, leave), the capsule machine (free, gold, leave).
    public string[] Options()
    {
        if (Run?.Pending is not ExpeditionChoice open) return Array.Empty<string>();
        if (open.Kind == ExpeditionChoice.RestKind) return new[] { Heal, Card };
        if (open.Kind == ExpeditionChoice.RewardKind) return new[] { Take, Leave };
        if (open.Kind == ExpeditionChoice.CapsuleKind) return new[] { Free, Gold, Leave };
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
        if (Run is not ExpeditionRun run || run.Pending is not ExpeditionChoice open) return 0;
        if (open.Kind == ExpeditionChoice.CapsuleKind) return option == Gold ? (Spec.Capsules?.GoldCost ?? 0) * MaxHp(run) : 0;
        if (open.Kind != ExpeditionChoice.EventKind) return 0;
        var e = Spec.FindEvent(open.Event);
        return e != null && (option == Risk && e.Kind == ExpeditionEventSpec.Chest || option == Take && e.Kind == ExpeditionEventSpec.Shrine) ? e.Cost * MaxHp(run) : 0;
    }

    // HP an option gives back now.
    public double Gain(string option)
    {
        if (Run is not ExpeditionRun run || run.Pending is not ExpeditionChoice open) return 0;
        double max = MaxHp(run), share = option == Heal && open.Kind == ExpeditionChoice.RestKind ? Spec.RestHeal
            : option == Safe ? Spec.FindEvent(open.Event)?.Heal ?? 0
            // A capsule's heal (its prize stays hidden: only Choose uses this for it).
            : open.Kind == ExpeditionChoice.CapsuleKind && Spec.Capsules is CapsuleSpec c && option is Free or Gold
                ? Prize(option == Free ? open.Free : open.Gold).Kind switch { Heal => option == Free ? c.FreeHeal : c.GoldHeal, PrizeCard when option == Gold => c.GoldHeal, _ => 0 } : 0;
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
            case Gold:
                return run.Hp - Cost(option) < 1 ? "too weak" : null;
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
        // A capsule: its prize (a heal came with Gain above; a gold card also brings GoldHeal).
        string? prize = open.Kind == ExpeditionChoice.CapsuleKind && option is Free or Gold ? (option == Free ? open.Free : open.Gold) : null;
        switch (Prize(prize))
        {
            case (PrizeCard or PrizeRare, string id):
                taken = id;
                run.Build[id] = run.Build.GetValueOrDefault(id) + 1;
                if (Settings.UpgradeList.FirstOrDefault(u => u.Id == id) is { Effect: "maxHp" } bigHeart) run.Hp += bigHeart.Amount;
                break;
            case (PrizeRelic, string id) when run.Relics.Count < Spec.MaxRelics && !run.Relics.Contains(id):
                gained = id;
                run.Relics.Add(id);
                break;
        }
        run.Hp = Math.Min(run.Hp, MaxHp(run));
        // The step the choice belongs to: a node's is the step to go now, a won battle's the one passed.
        int at = open.Kind is ExpeditionChoice.RewardKind or ExpeditionChoice.CapsuleKind ? run.Step - 1 : run.Step;
        AddLog(run, new() { Step = Math.Max(0, at), Kind = open.Kind, Option = option, Event = open.Event, Card = taken, Dropped = dropped, Relic = gained, Prize = prize, Hp = run.Hp - before });
        if (open.Kind is not (ExpeditionChoice.RewardKind or ExpeditionChoice.CapsuleKind)) run.Path.Add(open.Lane);
        run.Pending = null;
        return new(true, option, run.Hp - before, taken, dropped, gained, prize);
    }

    private static void AddLog(ExpeditionRun run, ExpeditionLogEntry entry)
    {
        run.Log.Add(entry);
        if (run.Log.Count > ExpeditionRun.LogKept) run.Log.RemoveAt(0);
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
        AddLog(run, new() { Step = run.Step, Kind = ExpeditionLogEntry.Retreat, Hp = -Spec.RetreatCost * max });
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
        string key = RecordKey(Spec, run.Weapon, run.Level, run.NoApps);
        if (!Progress.Records.TryGetValue(key, out var record)) Progress.Records[key] = record = new();
        record.Runs++;
        bool best = run.Step > record.BestSteps, fastest = false;
        if (best) record.BestSteps = run.Step;
        if (won)
        {
            record.Wins++;
            if (run.Seconds > 0 && (record.BestSeconds is not double s || run.Seconds < s - 0.05)) { record.BestSeconds = run.Seconds; fastest = true; }
        }
        // Keepsakes: given in the same save as the run is counted, so never twice.
        var given = new List<string>();
        if (won && GrantKeepsakes)
            foreach (var k in Spec.Keepsakes)
                if (k.EarnedAt(run.Level) && !Progress.Keepsakes.Contains(k.Id)) { Progress.Keepsakes.Add(k.Id); given.Add(k.Id); }
        return new(true, step, won, true, won, hp, max, 0, best, fastest, Keepsakes: given);
    }
}
