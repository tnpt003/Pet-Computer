using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DesktopPet;

// One kind of enemy (a definition: every slime of the kind shares it and nothing changes it in
// a battle; each Slime keeps its own HP, place and state). The slime and the Slime King are
// built from BattleSettings' numbers; Assets/battle.json lists every kind with its own numbers,
// so a new variant is data only. Abilities are switches the code supports: Slam (area hit with
// a floor warning), summon helpers below an HP fraction, get angry below another. A new kind
// of ability (flying, shooting) needs code first. Plain data (no WPF) for the simulation and tests.
public sealed class EnemyType
{
    public string Id { get; init; } = "slime";
    // What it is (all current kinds are slimes) and the drawing that shows it ("slime" only).
    public string Family { get; init; } = "slime";
    public string Renderer { get; init; } = "slime";
    public string NameVi { get; init; } = "Slime";
    public string NameEn { get; init; } = "Slime";
    public double MaxHp { get; init; } = 50;
    public double Speed { get; init; } = 34;
    public double Damage { get; init; } = 10;
    // Centre-to-centre reach of its attack.
    public double Range { get; init; } = 64;
    public double Windup { get; init; } = 0.8;
    public double AttackCooldown { get; init; } = 1.8;
    public double Recover { get; init; } = 0.35;
    // Size against the basic slime (1 = SlimeWidth × SlimeHeight).
    public double Scale { get; init; } = 1;
    // Combat size (weapon reach, bomb blast) against the basic slime; unset = Scale, as before.
    public double? HitScale { get; init; }
    [JsonIgnore] public double Hitbox => HitScale ?? Scale;
    public string Color { get; init; } = "#7CD36A";
    public string AngryColor { get; init; } = "#E86A6A";
    // Drawn marks that tell kinds apart at a glance: "none", "speed" (wind streaks behind it
    // while moving), "cracks" (a stony body: wide and low, armour on its front, more cracks when
    // weak), "leaf" (a herbal healer: a leaf cap and a herb pouch) or "cross" (an older "+" mark).
    public string Marks { get; init; } = "none";
    // A boss: crown, its own HP bar on the HUD, a "BOSS!" banner.
    public bool Boss { get; init; }
    // Area attack: a red zone on the floor while winding up, a shock wave when it lands.
    public bool Slam { get; init; }
    // Pushes move it and stun it this much (1 = like a slime).
    public double PushFactor { get; init; } = 1;
    public double StunFactor { get; init; } = 1;
    // Below SummonAt of its HP (0 = never) it calls SummonCount helpers of SummonType, once.
    public double SummonAt { get; init; }
    public int SummonCount { get; init; }
    public string SummonType { get; init; } = "slime";
    // Below EnrageAt of its HP (0 = never) it moves and winds up faster.
    public double EnrageAt { get; init; }
    public double EnragedSpeed { get; init; }
    public double EnragedWindup { get; init; }
    // Support: every HealCooldown s it winds up HealWindup s (a green "+" warns), then gives the most
    // hurt ally within HealRange up to HealAmount HP (never itself, never past max HP, never a
    // defeated one). HealBudget caps all it can ever heal, so healing can never outlast the user.
    // A push or stun during the windup cancels the heal. HealAmount 0: no healing.
    public double HealAmount { get; init; }
    public double HealRange { get; init; } = 160;
    public double HealCooldown { get; init; } = 4;
    public double HealWindup { get; init; } = 1;
    public double HealBudget { get; init; } = 100;
    // Dash: within DashRange of the cat it winds up (the lane is shown on the floor), then lunges
    // DashDistance DIPs at DashSpeed. Running into the cat hits it once (Damage) and stuns it
    // (BattleSettings.StunDurationSeconds). It replaces the kind's plain attack.
    public bool Dash { get; init; }
    public double DashRange { get; init; } = 170;
    public double DashDistance { get; init; } = 230;
    public double DashSpeed { get; init; } = 420;
    // Block: each hit from the cat's weapon (plain, combo or Afterimage) is blocked with this chance
    // (0 = never): no damage, no knock back. The yarn bomb and Thorns are never blocked, nor a combo
    // with Armor break. After BlockMaxStreak blocks in a row the next hit always lands.
    public double BlockChance { get; init; }
    public int BlockMaxStreak { get; init; } = 2;
    // Support: while any ally that fights (not a support) is on the field it never attacks the cat;
    // it keeps KeepBack DIPs behind the nearest such ally on its side (or that far from the cat),
    // backing off at its own speed. With no fighting ally left it fights like any slime.
    public bool Support { get; init; }
    public double KeepBack { get; init; } = 110;
    // Support far heal: with no hurt ally in HealRange, a fighting ally below FarHealBelow of its HP
    // (0 = never) anywhere on the field gets a heal like any other (same amount, budget, guard; a push
    // or stun cancels it) after a longer windup of FarHealWindup s, once per FarHealCooldown s.
    public double FarHealBelow { get; init; }
    public double FarHealWindup { get; init; } = 1.2;
    public double FarHealCooldown { get; init; } = 8;
    // Blocker: the cat walking on the floor cannot pass it; a target behind it means hitting it first
    // (lifting the cat over it still works). BlockFrontOnly: it blocks only hits from the side it
    // faces. TurnSeconds: how long it takes to face the other way (0: at once); it only winds up
    // facing the cat, so a cat behind it gets a moment of free hits.
    public bool Blocker { get; init; }
    public bool BlockFrontOnly { get; init; }
    public double TurnSeconds { get; init; }
    // Boss rally (during a shield phase, after its helpers came): every RallyEvery s (0 = never) its
    // crown glows RallyCharge s, then one fighting helper of the phase that is not elite becomes elite
    // for good: its kind's own affix (if any), RallyHp more max HP (and that much HP) and its hits ×
    // RallyDamage. Pushing the boss while its crown glows cancels the call.
    public double RallyEvery { get; init; }
    public double RallyCharge { get; init; } = 0.8;
    public double RallyHp { get; init; } = 0.5;
    public double RallyDamage { get; init; } = 1.25;
    // Boss: dazed this long when its shield breaks (0: not), taking damage as usual.
    public double ShieldBreakStun { get; init; }
    // Boss phases: each fires once when its HP fraction is reached (summon helpers and/or get
    // angry). At most SummonCap of its helpers are alive at once (more calls are skipped).
    public BossPhase[]? Phases { get; init; }
    public int SummonCap { get; init; } = 4;
    // A shield phase's run to the far edge: its speed, and the longest it may take before calling.
    public double RetreatSpeed { get; init; } = 240;
    public double RetreatSeconds { get; init; } = 3;
    // Instead of the run: a leap to that edge (a crouch of LeapCrouch s, then LeapSeconds in the air;
    // the landing hurts no one). The far edge: the one opposite the boss's half of the floor, unless
    // the cat is in that half too (then the other one), so it never lands next to the cat.
    public bool RetreatLeap { get; init; }
    public double LeapCrouch { get; init; } = 0.35;
    public double LeapSeconds { get; init; } = 0.9;
    // A shield phase's helpers are announced this long by rings on the floor where they will appear (0: at once).
    public double SummonWarning { get; init; }
    // Support tactics. HealGuard: the ally being healed takes no damage while the heal winds up (a push
    // or a stun on the healer ends it at once); never a boss, and the same ally only again
    // HealGuardRepeat s after. FleeSpeed: backing off from a cat nearer than 200 DIPs it walks up to
    // this × faster (at 60 DIPs). HideBehindGuard: it keeps behind its guard rather than the nearest ally.
    public bool HealGuard { get; init; }
    public double HealGuardRepeat { get; init; } = 2;
    public double FleeSpeed { get; init; } = 1;
    public bool HideBehindGuard { get; init; }
    // HitAndRun (dash enemies): after a dash it runs this many DIPs away from the cat before coming back.
    public double HitAndRun { get; init; }
    // What one of this kind weighs against a wave's or a stream's budget (WaveSpec.Budget).
    public double Cost { get; init; } = 1;
    // The one elite affix this kind may roll when it spawns (null: never elite).
    public EliteSpec? Elite { get; init; }
    public string Name(bool english) => english ? NameEn : NameVi;
}

// An elite affix (Enemy Director): rolled once when a wave or stream spawns the enemy, never for a
// boss's helpers (a boss's rally call makes them elite instead), never on Easy, at most
// BattleSettings.MaxElites alive at once. A gold rim and a
// star tell it apart. One small change per kind, not a stronger enemy in every way: a longer windup
// with a faster dash, sure blocks of its first front hits, a stronger first heal. Cost: what being
// elite adds to the budget.
public sealed class EliteSpec
{
    public double WindupAdd { get; init; }
    public double DashSpeed { get; init; } = 1;
    public int SureBlocks { get; init; }
    public double FirstHeal { get; init; } = 1;
    public double Cost { get; init; } = 1;
    // A short line for the banner the first time an elite of this kind appears in a battle.
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string Describe(bool english) => english ? DescEn : DescVi;
}

public sealed class BossPhase
{
    public double At { get; init; }
    public string? Summon { get; init; }
    public int Count { get; init; }
    // More helpers of other kinds in the same phase (after Summon × Count).
    public WaveGroup[]? Summons { get; init; }
    public bool Enrage { get; init; }
    // Shield phase: a hit never takes the boss below At (the rest is lost); it puts up a shield
    // (takes no damage), runs to the edge far from the cat, calls its helpers there and keeps the
    // shield until every helper of this phase is defeated. Later phases wait for the shield to drop.
    public bool Shield { get; init; }
    // Every helper this phase calls, in order.
    public IEnumerable<string> Calls()
    {
        if (Summon != null) for (int i = 0; i < Count; i++) yield return Summon;
        foreach (var g in Summons ?? Array.Empty<WaveGroup>()) for (int i = 0; i < g.Count; i++) yield return g.Enemy;
    }
}

// One wave: groups spawn in order (at most MaxAlive at once). A plain group is Count of Enemy.
// Pick: each of the Count spawns is a kind drawn from these weights when the wave starts (Enemy is
// then unused); AtLeast: that many of a kind among them whatever the draw. Squad: Count times these
// members together, side by side (fighters toward the cat, supports behind them), only once there is
// room on the floor for the whole squad (nothing after it spawns before it).
public sealed class WaveGroup
{
    public string Enemy { get; init; } = "slime";
    public int Count { get; init; } = 1;
    public Dictionary<string, double>? Pick { get; init; }
    public Dictionary<string, int>? AtLeast { get; init; }
    public WaveGroup[]? Squad { get; init; }
    // Two-sided waves: "a" spawns at the edge far from the cat (as the wave starts), "b" at the
    // other edge after a warning arrow there (BattleSettings.IncomingWarning); unset: either edge.
    public string? Side { get; init; }
    [JsonIgnore] public bool Fixed => Pick == null && Squad == null;
    [JsonIgnore] public int SquadSize => Squad?.Sum(m => Math.Max(0, m.Count)) ?? 1;
}

// Stream: a survive stage's last wave that never ends: one kind drawn from Weights whenever there is
// room, every Interval seconds at the start of the stage down to IntervalEnd at its end.
public sealed class StreamSpec
{
    public Dictionary<string, double> Weights { get; init; } = new();
    public double Interval { get; init; } = 2;
    public double IntervalEnd { get; init; } = 2;
    // From this many seconds of the clock on (0 = never), spawns come from alternate edges, each
    // after a warning arrow.
    public double PincerAfter { get; init; }
    // The most the enemies on the floor may cost at once (EnemyType.Cost, elites included; 0 = no
    // limit): a drawn kind that does not fit gives way to the cheapest one that does, or waits.
    public double Budget { get; init; }
}

public sealed class WaveSpec
{
    public WaveGroup[] Groups { get; init; } = Array.Empty<WaveGroup>();
    // Enemies of this wave are this much stronger (× the stage's threat; see BattleSettings.ThreatDamageShare).
    public double Threat { get; init; } = 1;
    // The most this wave's enemies may cost in all (EnemyType.Cost; 0 = no limit): a drawn kind that
    // does not fit gives way to the cheapest kind of its pick; what is left pays for elites.
    public double Budget { get; init; }
    public StreamSpec? Stream { get; init; }
    // The fixed kinds in spawn order (drawn kinds are not known before the wave starts).
    public IEnumerable<string> Spawns() => Groups.Where(g => g.Pick == null).SelectMany(g =>
        g.Squad == null ? Enumerable.Repeat(g.Enemy, Math.Max(0, g.Count))
            : Enumerable.Range(0, Math.Max(0, g.Count)).SelectMany(_ => g.Squad.SelectMany(m => Enumerable.Repeat(m.Enemy, Math.Max(0, m.Count)))));
}

// Adventure (battle.json "campaign"): chapters of stages played in order. A stage is a battle
// with its own waves (enemy kinds come from "enemies") and goal; winning it opens the next stage
// of its chapter. Ids are stable: progress (PetAdventure.cs) is saved by them. RulesVersion goes up
// when a stage changes enough that old records should not be compared with new ones.
public sealed class CampaignSpec
{
    public ChapterSpec[] Chapters { get; init; } = Array.Empty<ChapterSpec>();
}

public sealed class ChapterSpec
{
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public StageSpec[] Stages { get; init; } = Array.Empty<StageSpec>();
    public string Name(bool english) => english ? NameEn : NameVi;
}

public sealed class StageSpec
{
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    // One line about what the stage teaches or asks, shown on the map and the setup panel.
    public string GoalVi { get; init; } = "";
    public string GoalEn { get; init; } = "";
    // "clear": defeat every wave. "survive": stay alive SurviveSeconds (the last wave comes again
    // while time is left; the clock stops while the cat is in the user's hands).
    public string Objective { get; init; } = "clear";
    public double? SurviveSeconds { get; init; }
    [JsonIgnore] public bool Survival => Objective == "survive";
    // Between waves the user picks one of up to three upgrades (battle.json "upgrades") for this run.
    public bool Upgrades { get; init; }
    // At most this many picks in one run of the stage (unset: one after every cleared wave that has
    // another after it; a survive stage would otherwise offer one per repeated wave).
    public int? MaxUpgrades { get; init; }
    // Survive stages: a card offer at each of these points of the clock (seconds held out) instead of
    // after cleared waves.
    public double[]? UpgradeAt { get; init; }
    // How strong this stage's enemies are (1: their own numbers; × difficulty and the wave's threat).
    public double Threat { get; init; } = 1;
    // Survive stages: the threat grows by this much per minute held out.
    public double ThreatGrowth { get; init; }
    public int RulesVersion { get; init; } = 1;
    // Enemies on the floor at once (unset: the battle's maxAlive).
    public int? MaxAlive { get; init; }
    // The chance (0–1) that an enemy spawned by a wave or stream with an elite affix is elite
    // (never on Easy; BattleSettings.MaxElites caps how many are alive at once).
    public double EliteChance { get; init; }
    // An accessory id (Assets/accessories.json) given once, the first time the stage is won.
    public string? Reward { get; init; }
    // A short tip shown once when the stage first starts (and again from its setup panel).
    public string? TipVi { get; init; }
    public string? TipEn { get; init; }
    public string? Tip(bool english) => english ? TipEn : TipVi;
    public WaveSpec[] Waves { get; init; } = Array.Empty<WaveSpec>();
    public string Name(bool english) => english ? NameEn : NameVi;
    public string Goal(bool english) => english ? GoalEn : GoalVi;
}

// An upgrade offered between the waves of an Adventure stage. A run keeps what it took; in the
// Adventure a won stage passes its build on to the next stage (AdventureProgress.Builds), a free
// battle starts without any. Effect is one the code knows (UpgradeLimits); Amount is a fraction for
// damage/moveSpeed/attackSpeed/armor (0.2 = 20 %), DIPs/s for knock, HP for heal and maxHp. The rare
// effects: shell (the next hit on the cat is stopped whole, then it grows back in Amount s), opener
// (the first landed hit on each enemy deals Amount more), momentum (a swing after a run of
// BattleSim.MomentumRun DIPs deals Amount more).
// MaxStacks: how often one build may take it (0 = no limit). Group (defense, power, speed or none)
// counts toward that group's synergy. Definitions are shared and never changed.
public sealed class UpgradeSpec
{
    public string? Group { get; init; }
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string Effect { get; init; } = "damage";
    public double Amount { get; init; }
    public int MaxStacks { get; init; }
    // A rare card (one per group, at most once a build): it takes the place of its group's card in an
    // offer with BattleSettings.RareChance. It plays differently, not better in every way.
    public bool Rare { get; init; }
    public string Name(bool english) => english ? NameEn : NameVi;
    public string Describe(bool english) => english ? DescEn : DescVi;
}

// A build bonus: once a build holds At points of a group (one per card taken), the effect is on
// for the rest of that build. thorns: Amount of the damage the cat really takes from an enemy's
// hit goes back to that enemy. armorBreak: combo hits deal Amount more (and pierce a rock's block).
// echo: every Every-th landed swing adds a second hit of Amount × its damage. The bonus hits
// (reflected, echoed) never set off another bonus.
public sealed class SynergySpec
{
    public string Id { get; init; } = "";
    public string Group { get; init; } = "defense";
    public int At { get; init; } = 3;
    public string Effect { get; init; } = "thorns";
    public double Amount { get; init; }
    public int Every { get; init; } = 4;
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string Name(bool english) => english ? NameEn : NameVi;
    public string Describe(bool english) => english ? DescEn : DescVi;
}

// The upgrade groups, in the order cards, the HUD and the setup panel show them.
public static class BuildGroups
{
    public static readonly string[] All = { "defense", "power", "speed" };
    public static string Name(string group, bool english) => group switch
    {
        "defense" => english ? "Defense" : "Phòng thủ",
        "power" => english ? "Power" : "Sức mạnh",
        _ => english ? "Speed" : "Tốc độ"
    };
}

// Assets/battle.json (embedded) is the built-in battle: its numbers, weapons, enemy kinds and
// waves. The user may add battle.json to the app's data folder (%LOCALAPPDATA%\DesktopPet,
// docs/BATTLE-DATA.md) with only what it changes: it is merged over the built-in file
// ("enemies"/"weapons" entries by id, other objects key by key, anything else replaced) and the
// result is validated as a whole. A broken file never stops the pet and is never used in part:
// it is logged, the built-in battle is used and the battle setup panel says why.
public sealed record BattleDataLoad(BattleSettings Settings, bool Custom, string? Error);

public static class BattleData
{
    public const int SchemaVersion = 1;
    public static string UserFilePath => Path.Combine(PetPreferences.DataDirectory, "battle.json");
    private static readonly Lazy<BattleSettings> Loaded = new(LoadBuiltIn);
    // The built-in battle (the code's defaults if even the embedded file is broken).
    public static BattleSettings Settings => Loaded.Value;

    private static string EmbeddedText()
    {
        using var stream = typeof(BattleData).Assembly.GetManifestResourceStream("DesktopPet.battle.json")
            ?? throw new InvalidDataException("Missing embedded battle.json");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static bool IsDataError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or NotSupportedException or InvalidOperationException or FormatException;

    private static BattleSettings LoadBuiltIn()
    {
        try { return Parse(EmbeddedText()); }
        catch (Exception ex) when (IsDataError(ex))
        {
            Trace.WriteLine($"Battle data could not be loaded, using the built-in battle: {ex.Message}");
            return new BattleSettings();
        }
    }

    // The battle for the next fight: the built-in one with the user's file over it, if there is
    // one. Read again for each battle setup, so an edit applies to the next battle and never to a
    // running one (a battle keeps the settings it started with).
    public static BattleDataLoad Load(string? userFile)
    {
        if (string.IsNullOrEmpty(userFile) || !File.Exists(userFile)) return new(Settings, false, null);
        try
        {
            return new(ParseWithOverride(EmbeddedText(), File.ReadAllText(userFile)), true, null);
        }
        catch (Exception ex) when (IsDataError(ex))
        {
            Trace.WriteLine($"{userFile} was not used, the built-in battle is: {ex.Message}");
            return new(Settings, false, ex.Message);
        }
    }

    private static readonly JsonDocumentOptions NodeOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    // Throws on any mistake in either file or in the merged result.
    public static BattleSettings ParseWithOverride(string builtInJson, string userJson)
    {
        var root = JsonNode.Parse(builtInJson, null, NodeOptions) as JsonObject ?? throw new InvalidDataException("The built-in battle.json is not a JSON object");
        var user = JsonNode.Parse(userJson, null, NodeOptions) as JsonObject ?? throw new InvalidDataException("The file must be a JSON object: { ... }");
        var version = Key(user, "schemaVersion") is string key ? user[key] : null;
        if (version == null) throw new InvalidDataException($"schemaVersion is missing (write \"schemaVersion\": {SchemaVersion})");
        if (version is not JsonValue value || !value.TryGetValue<int>(out int number) || number != SchemaVersion)
            throw new InvalidDataException($"schemaVersion {version.ToJsonString()} is not supported (this version reads {SchemaVersion})");
        DropRetired(user);
        Merge(root, user, "");
        return Parse(root.ToJsonString());
    }

    // Settings of tactics taken out (2026-10-21): a user file that still sets them keeps working;
    // they are dropped with a log line instead of failing as misspelt keys.
    private static readonly string[] RetiredKeys = { "maxFlankers" };
    private static readonly string[] RetiredEnemyKeys = { "fallBackBelow", "flank", "flankSpeed", "teleportBelow", "teleportWindup", "teleportCooldown", "rallySeconds", "rallySpeed" };
    private static void DropRetired(JsonObject user)
    {
        var dropped = new List<string>();
        void Drop(JsonObject node, string[] keys, string at)
        {
            foreach (var key in keys) if (Key(node, key) is string found) { node.Remove(found); dropped.Add(at + key); }
        }
        Drop(user, RetiredKeys, "");
        if (Key(user, "enemies") is string list && user[list] is JsonArray enemies)
            foreach (var entry in enemies.OfType<JsonObject>()) Drop(entry, RetiredEnemyKeys, $"enemies[{IdOf(entry)}].");
        if (dropped.Count > 0) Trace.WriteLine($"battle.json: no longer used, ignored: {string.Join(", ", dropped)}");
    }

    // Keys match without case, as when the file is read.
    private static string? Key(JsonObject node, string key)
    {
        foreach (var pair in node) if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Key;
        return null;
    }

    private static void Merge(JsonObject target, JsonObject source, string path)
    {
        foreach (var (key, value) in source.ToList())
        {
            string? existing = Key(target, key);
            var current = existing != null ? target[existing] : null;
            if (value is JsonObject part && current is JsonObject whole) { Merge(whole, part, path + key + "."); continue; }
            bool byId = key.Equals("enemies", StringComparison.OrdinalIgnoreCase) || key.Equals("weapons", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("upgrades", StringComparison.OrdinalIgnoreCase) || key.Equals("synergies", StringComparison.OrdinalIgnoreCase);
            if (byId && value is JsonArray entries && current is JsonArray list) { MergeById(list, entries, path + key); continue; }
            if (existing != null) target.Remove(existing);
            target[key] = value?.DeepClone();
        }
    }

    // An entry with a known id changes that entry (only the keys it gives); a new id is added.
    private static void MergeById(JsonArray target, JsonArray source, string path)
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] is not JsonObject entry || IdOf(entry) is not string id) throw new InvalidDataException($"{path}[{i}] needs an \"id\"");
            if (!seen.Add(id)) throw new InvalidDataException($"{path}: id \"{id}\" is used twice");
            var match = target.OfType<JsonObject>().FirstOrDefault(e => IdOf(e) == id);
            if (match != null) Merge(match, entry, $"{path}[{id}].");
            else target.Add(entry.DeepClone());
        }
    }

    private static string? IdOf(JsonObject node) =>
        Key(node, "id") is string key && node[key] is JsonValue value && value.TryGetValue<string>(out var id) ? id : null;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
        // A misspelt key is an error, not a silently ignored setting.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly Regex Hex = new("^#[0-9A-Fa-f]{6}$");
    // Ids stay stable across versions: lower-case letters, digits, '_' and '-', at most 32.
    private static readonly Regex IdPattern = new("^[a-z0-9_-]{1,32}$");
    // What the code can draw and do; data can only choose among these.
    public static readonly string[] WeaponStyles = { "sword", "hammer" };
    public static readonly string[] WeaponHitEffects = { "spark", "thud" };
    public static readonly string[] EnemyRenderers = { "slime" };
    public static readonly string[] EnemyMarks = { "none", "speed", "cracks", "leaf", "cross" };
    public static readonly string[] GroupSides = { "a", "b" };

    // Throws on any mistake, so a half-valid file is never used.
    public static BattleSettings Parse(Stream stream)
    {
        var settings = JsonSerializer.Deserialize<BattleSettings>(stream, Options) ?? throw new InvalidDataException("battle.json is empty");
        Validate(settings);
        return settings;
    }

    public static BattleSettings Parse(string json)
    {
        var settings = JsonSerializer.Deserialize<BattleSettings>(json, Options) ?? throw new InvalidDataException("battle.json is empty");
        Validate(settings);
        return settings;
    }

    private static string N(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    public static void Validate(BattleSettings s)
    {
        void Positive(double value, string name) { if (!double.IsFinite(value) || value <= 0) throw new InvalidDataException($"{name} must be a positive number"); }
        void NotNegative(double value, string name) { if (!double.IsFinite(value) || value < 0) throw new InvalidDataException($"{name} must not be negative"); }
        void Within(double value, double min, double max, string name) { if (!double.IsFinite(value) || value < min || value > max) throw new InvalidDataException($"{name} must be {N(min)}–{N(max)}"); }
        void Id(string? id, string name) { if (id == null || !IdPattern.IsMatch(id)) throw new InvalidDataException($"{name} must be 1–32 lower-case letters, digits, _ or - (\"{id}\")"); }
        void Text(string? text, string name) { if (text == null || text.Length > 32) throw new InvalidDataException($"{name} must be at most 32 characters"); }
        if (s.SchemaVersion != SchemaVersion) throw new InvalidDataException($"schemaVersion {s.SchemaVersion} is not supported (this version reads {SchemaVersion})");
        Positive(s.CatMaxHp, "catMaxHp"); NotNegative(s.CatDamage, "catDamage"); Positive(s.CatAttackRange, "catAttackRange");
        Positive(s.CatAttackCooldown, "catAttackCooldown"); Positive(s.CatRunScale, "catRunScale");
        if (s.CatRunScale > 3) throw new InvalidDataException("catRunScale must be at most 3"); Positive(s.CatSwingDuration, "catSwingDuration");
        if (s.ComboHits is < 2 or > 6) throw new InvalidDataException("comboHits must be 2–6");
        Positive(s.ComboWindow, "comboWindow"); Positive(s.ComboDamage, "comboDamage"); NotNegative(s.ComboKnock, "comboKnock");
        if (!(s.CatHitTime > 0 && s.CatHitTime < s.CatSwingDuration)) throw new InvalidDataException("catHitTime must fall inside the swing");
        if (s.Hammer == null) throw new InvalidDataException("hammer is missing");
        if (s.Weapons is { Length: 0 }) throw new InvalidDataException("weapons must list at least one weapon");
        var weaponIds = new HashSet<string>();
        for (int i = 0; i < s.WeaponList.Count; i++)
        {
            var w = s.WeaponList[i] ?? throw new InvalidDataException($"weapons[{i}] is empty");
            string at = $"weapons[{(string.IsNullOrEmpty(w.Id) ? i.ToString() : w.Id)}]";
            Id(w.Id, at + ".id");
            if (!weaponIds.Add(w.Id)) throw new InvalidDataException($"weapon id \"{w.Id}\" is used twice");
            if (Array.IndexOf(WeaponStyles, w.Style) < 0) throw new InvalidDataException($"{at}.style must be one of: {string.Join(", ", WeaponStyles)}");
            if (w.HitEffect != null && Array.IndexOf(WeaponHitEffects, w.HitEffect) < 0) throw new InvalidDataException($"{at}.hitEffect must be one of: {string.Join(", ", WeaponHitEffects)}");
            Text(w.NameVi, at + ".nameVi"); Text(w.NameEn, at + ".nameEn");
            if (w.HintVi == null || w.HintEn == null || w.HintVi.Length > 80 || w.HintEn.Length > 80) throw new InvalidDataException($"{at} hints must be at most 80 characters");
            Within(w.Damage, 0, 1000, at + ".damage"); Within(w.Range, 10, 300, at + ".range");
            Within(w.AttackCooldown, 0.1, 10, at + ".attackCooldown"); Within(w.SwingDuration, 0.05, 5, at + ".swingDuration");
            if (!(w.HitTime > 0 && w.HitTime < w.SwingDuration)) throw new InvalidDataException(at + ".hitTime must fall inside the swing");
            Within(w.Knock, 0, 1000, at + ".knock");
        }
        NotNegative(s.LandingGraceSeconds, "landingGraceSeconds");
        if (s.LandingGraceSeconds > 5) throw new InvalidDataException("landingGraceSeconds must be at most 5");
        NotNegative(s.RescueGraceCooldownSeconds, "rescueGraceCooldownSeconds");
        Within(s.StunDurationSeconds, 0, 5, "stunDurationSeconds"); Within(s.StunImmunitySeconds, 0, 30, "stunImmunitySeconds");
        if (s.RescueGraceCooldownSeconds > 60) throw new InvalidDataException("rescueGraceCooldownSeconds must be at most 60");
        Within(s.RescueGraceMinLiftSeconds, 0, 2, "rescueGraceMinLiftSeconds");
        Positive(s.SlimeWidth, "slimeWidth"); Positive(s.SlimeHeight, "slimeHeight"); Positive(s.MaxAlive, "maxAlive");
        foreach (var skill in BattleSkills.All) Positive(skill.Cooldown(s), skill.Skill + " cooldown");
        Positive(s.HealAmount, "healAmount"); Positive(s.CheerSeconds, "cheerSeconds"); Positive(s.CheerSpeed, "cheerSpeed");
        Positive(s.BombRadius, "bombRadius"); NotNegative(s.BombDamage, "bombDamage"); Positive(s.ShieldSeconds, "shieldSeconds");
        foreach (var (level, name) in new[] { (s.Easy, "easy"), (s.Hard, "hard") })
            foreach (var (value, key) in new[] { (level.EnemyHp, "enemyHp"), (level.EnemyDamage, "enemyDamage"), (level.EnemySpeed, "enemySpeed") })
                if (!double.IsFinite(value) || value < 0.25 || value > 3) throw new InvalidDataException($"{name}.{key} must be 0.25–3");
        var ids = new HashSet<string>();
        if (s.Enemies is { Length: 0 }) throw new InvalidDataException("enemies must list at least one enemy");
        for (int i = 0; i < s.EnemyTypes.Count; i++)
        {
            var e = s.EnemyTypes[i] ?? throw new InvalidDataException($"enemies[{i}] is empty");
            string at = $"enemies[{(string.IsNullOrEmpty(e.Id) ? i.ToString() : e.Id)}]";
            Id(e.Id, at + ".id");
            if (!ids.Add(e.Id)) throw new InvalidDataException($"enemy id \"{e.Id}\" is used twice");
            Id(e.Family, at + ".family");
            if (Array.IndexOf(EnemyRenderers, e.Renderer) < 0) throw new InvalidDataException($"{at}.renderer must be one of: {string.Join(", ", EnemyRenderers)}");
            Text(e.NameVi, at + ".nameVi"); Text(e.NameEn, at + ".nameEn");
            Within(e.MaxHp, 1, 100000, at + ".maxHp"); Within(e.Speed, 0, 400, at + ".speed"); Within(e.Damage, 0, 1000, at + ".damage");
            Within(e.Range, 10, 400, at + ".range"); Within(e.Windup, 0.1, 10, at + ".windup"); Within(e.AttackCooldown, 0.1, 30, at + ".attackCooldown");
            Within(e.Recover, 0, 10, at + ".recover");
            // The battle strip is 170 DIPs tall: bigger enemies would be cut off.
            Within(e.Scale, 0.5, 2.3, at + ".scale");
            if (e.HitScale is double hit) Within(hit, 0.3, 2.3, at + ".hitScale");
            if (e.Color == null || e.AngryColor == null || !Hex.IsMatch(e.Color) || !Hex.IsMatch(e.AngryColor)) throw new InvalidDataException(at + " colors must be #RRGGBB");
            if (Array.IndexOf(EnemyMarks, e.Marks) < 0) throw new InvalidDataException($"{at}.marks must be one of: {string.Join(", ", EnemyMarks)}");
            Within(e.PushFactor, 0, 3, at + ".pushFactor"); Within(e.StunFactor, 0, 3, at + ".stunFactor");
            if (!(e.SummonAt >= 0 && e.SummonAt < 1) || !(e.EnrageAt >= 0 && e.EnrageAt < 1)) throw new InvalidDataException(at + " summonAt/enrageAt must be 0–1");
            if (e.SummonAt > 0 && e.SummonCount is < 1 or > 6) throw new InvalidDataException(at + ".summonCount must be 1–6");
            if (e.EnrageAt > 0 || e.Phases?.Any(p => p is { Enrage: true }) == true) { Within(e.EnragedSpeed, 0.1, 400, at + ".enragedSpeed"); Within(e.EnragedWindup, 0.1, 10, at + ".enragedWindup"); }
            Within(e.HealAmount, 0, 1000, at + ".healAmount");
            if (e.HealAmount > 0)
            {
                Within(e.HealRange, 10, 600, at + ".healRange"); Within(e.HealCooldown, 0.5, 60, at + ".healCooldown");
                Within(e.HealWindup, 0.2, 5, at + ".healWindup"); Within(e.HealBudget, 1, 100000, at + ".healBudget");
            }
            if (e.SummonCap is < 1 or > 10) throw new InvalidDataException(at + ".summonCap must be 1–10");
            Within(e.BlockChance, 0, 0.9, at + ".blockChance");
            if (e.BlockMaxStreak is < 1 or > 10) throw new InvalidDataException(at + ".blockMaxStreak must be 1–10");
            if (e.Support) Within(e.KeepBack, 0, 600, at + ".keepBack");
            Within(e.FarHealBelow, 0, 0.9, at + ".farHealBelow");
            if (e.FarHealBelow > 0)
            {
                if (!e.Support || e.HealAmount <= 0) throw new InvalidDataException(at + ".farHealBelow is for a support enemy that heals");
                Within(e.FarHealWindup, 0.2, 5, at + ".farHealWindup"); Within(e.FarHealCooldown, 1, 60, at + ".farHealCooldown");
            }
            Within(e.TurnSeconds, 0, 3, at + ".turnSeconds");
            Within(e.RallyEvery, 0, 60, at + ".rallyEvery");
            if (e.RallyEvery > 0)
            {
                if (e.Phases?.Any(p => p is { Shield: true }) != true) throw new InvalidDataException(at + ".rallyEvery needs a shield phase");
                Within(e.RallyCharge, 0.2, 5, at + ".rallyCharge"); Within(e.RallyHp, 0, 3, at + ".rallyHp"); Within(e.RallyDamage, 1, 3, at + ".rallyDamage");
            }
            Within(e.ShieldBreakStun, 0, 5, at + ".shieldBreakStun");
            if (e.Phases?.Any(p => p is { Shield: true }) == true)
            {
                Within(e.RetreatSpeed, 10, 600, at + ".retreatSpeed"); Within(e.RetreatSeconds, 0.2, 10, at + ".retreatSeconds");
                Within(e.LeapCrouch, 0.1, 2, at + ".leapCrouch"); Within(e.LeapSeconds, 0.3, 3, at + ".leapSeconds"); Within(e.SummonWarning, 0, 3, at + ".summonWarning");
            }
            else if (e.RetreatLeap || e.SummonWarning > 0) throw new InvalidDataException(at + ".retreatLeap/summonWarning need a shield phase");
            if (e.Dash)
            {
                Within(e.DashRange, 20, 600, at + ".dashRange"); Within(e.DashDistance, 20, 800, at + ".dashDistance");
                Within(e.DashSpeed, 50, 1500, at + ".dashSpeed");
            }
            Within(e.Cost, 0, 10, at + ".cost");
            if (e.HealGuard && e.HealAmount <= 0) throw new InvalidDataException(at + ".healGuard is for an enemy that heals");
            Within(e.HealGuardRepeat, 0, 10, at + ".healGuardRepeat"); Within(e.FleeSpeed, 1, 4, at + ".fleeSpeed");
            if ((e.FleeSpeed > 1 || e.HideBehindGuard) && !e.Support) throw new InvalidDataException(at + ".fleeSpeed/hideBehindGuard are for a support enemy");
            Within(e.HitAndRun, 0, 600, at + ".hitAndRun");
            if (e.HitAndRun > 0 && !e.Dash) throw new InvalidDataException(at + ".hitAndRun is for a dash enemy");
            if (e.Elite is EliteSpec elite)
            {
                if (e.Boss) throw new InvalidDataException(at + ".elite is not for a boss");
                Within(elite.WindupAdd, 0, 3, at + ".elite.windupAdd"); Within(elite.DashSpeed, 1, 3, at + ".elite.dashSpeed");
                if (elite.SureBlocks is < 0 or > 5) throw new InvalidDataException(at + ".elite.sureBlocks must be 0–5");
                Within(elite.FirstHeal, 1, 5, at + ".elite.firstHeal"); Within(elite.Cost, 0, 10, at + ".elite.cost");
                if ((elite.WindupAdd > 0 || elite.DashSpeed > 1) && !e.Dash) throw new InvalidDataException(at + ".elite: windupAdd/dashSpeed are for a dash enemy");
                if (elite.SureBlocks > 0 && e.BlockChance <= 0) throw new InvalidDataException(at + ".elite.sureBlocks is for an enemy that blocks");
                if (elite.FirstHeal > 1 && e.HealAmount <= 0) throw new InvalidDataException(at + ".elite.firstHeal is for an enemy that heals");
                if (elite.WindupAdd <= 0 && elite.DashSpeed <= 1 && elite.SureBlocks == 0 && elite.FirstHeal <= 1) throw new InvalidDataException(at + ".elite changes nothing");
                if (elite.DescVi == null || elite.DescEn == null || elite.DescVi.Length > 60 || elite.DescEn.Length > 60) throw new InvalidDataException(at + ".elite descriptions must be at most 60 characters");
            }
            if (e.Phases is { } phases)
            {
                if (phases.Length is 0 or > 6) throw new InvalidDataException(at + ".phases must list 1–6 phases");
                foreach (var p in phases)
                {
                    if (p == null || !(p.At > 0 && p.At < 1)) throw new InvalidDataException(at + ".phases: each \"at\" must be between 0 and 1");
                    if (p.Summon == null && p.Summons == null && !p.Enrage) throw new InvalidDataException(at + ".phases: a phase must summon or enrage");
                    if (p.Summon != null && p.Count is < 1 or > 6) throw new InvalidDataException(at + ".phases: count must be 1–6");
                    if (p.Summons != null && (p.Summons.Length is 0 or > 4 || p.Summons.Any(g => g == null || !g.Fixed || g.Count is < 1 or > 6)))
                        throw new InvalidDataException(at + ".phases: summons must list 1–4 plain groups of 1–6");
                    if (p.Calls().Count() > 8) throw new InvalidDataException(at + ".phases: a phase calls at most 8 helpers");
                    // A shield only drops when its helpers are gone: it needs some.
                    if (p.Shield && !p.Calls().Any()) throw new InvalidDataException(at + ".phases: a shield phase must summon");
                }
            }
        }
        // A summoned helper may not summon in turn (no chains of calls).
        static bool Summons(EnemyType e) => e.SummonAt > 0 || e.Phases?.Any(p => p.Calls().Any()) == true;
        foreach (var e in s.EnemyTypes)
            foreach (var p in e.Phases ?? Array.Empty<BossPhase>())
                foreach (var call in p.Calls().Distinct())
                    if (!ids.Contains(call) || Summons(s.Enemy(call)))
                        throw new InvalidDataException($"enemies[{e.Id}] phase summons \"{call}\": an unknown enemy (or one that summons too)");
        foreach (var e in s.EnemyTypes)
            if (e.SummonAt > 0 && (!ids.Contains(e.SummonType) || Summons(s.Enemy(e.SummonType))))
                throw new InvalidDataException($"enemies[{e.Id}] summons \"{e.SummonType}\": an unknown enemy (or one that summons too)");
        // Drawn kinds: known, never a boss (a boss only comes where a wave names it), some weight.
        void Weights(Dictionary<string, double>? weights, string at)
        {
            if (weights == null || weights.Count is 0 or > 8) throw new InvalidDataException($"{at} must list 1–8 enemies");
            foreach (var (kind, weight) in weights)
            {
                if (!ids.Contains(kind) || s.Enemy(kind).Boss) throw new InvalidDataException($"{at} names \"{kind}\": an unknown enemy or a boss");
                Within(weight, 0, 100, $"{at}.{kind}");
            }
            if (weights.Values.Sum() <= 0) throw new InvalidDataException($"{at} needs some weight above 0");
        }
        // maxAlive: what the battle allows on the floor (a squad must fit); survival: a survive stage.
        void Waves(IReadOnlyList<WaveSpec>? waves, string at, int maxAlive, bool survival)
        {
            if (waves == null || waves.Count == 0) throw new InvalidDataException($"{at} needs at least one wave");
            if (waves.Count > 30) throw new InvalidDataException($"{at} may have at most 30 waves");
            for (int i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                if (wave == null) throw new InvalidDataException($"{at}[{i}] is empty");
                Within(wave.Threat, 0.5, 3, $"{at}[{i}].threat");
                if (wave.Stream is StreamSpec stream)
                {
                    // It never ends, so only a survive stage's clock can finish it.
                    if (!survival || i != waves.Count - 1) throw new InvalidDataException($"{at}[{i}].stream is only for the last wave of a survive stage");
                    Weights(stream.Weights, $"{at}[{i}].stream.weights");
                    Within(stream.Interval, 0.3, 20, $"{at}[{i}].stream.interval"); Within(stream.IntervalEnd, 0.3, 20, $"{at}[{i}].stream.intervalEnd");
                    Within(stream.PincerAfter, 0, 600, $"{at}[{i}].stream.pincerAfter");
                    Within(stream.Budget, 0, 100, $"{at}[{i}].stream.budget");
                    // Some drawn kind must always fit an empty floor, or the stream would never spawn.
                    if (stream.Budget > 0 && stream.Weights.Where(w => w.Value > 0).Min(w => s.Enemy(w.Key).Cost) > stream.Budget)
                        throw new InvalidDataException($"{at}[{i}].stream.budget is below the cheapest kind it draws");
                    if (wave.Groups is { Length: > 0 }) throw new InvalidDataException($"{at}[{i}]: a stream wave has no groups");
                    continue;
                }
                if (wave.Groups == null || wave.Groups.Length == 0) throw new InvalidDataException($"{at}[{i}] needs at least one group");
                Within(wave.Budget, 0, 100, $"{at}[{i}].budget");
                foreach (var g in wave.Groups)
                {
                    if (g == null) throw new InvalidDataException($"{at}[{i}] has an empty group");
                    if (g.Count is < 1 or > 50) throw new InvalidDataException($"{at}[{i}] {g.Enemy}: count must be 1–50");
                    if (g.Side != null && Array.IndexOf(GroupSides, g.Side) < 0) throw new InvalidDataException($"{at}[{i}].side must be \"a\" or \"b\"");
                    if (g.Pick != null && g.Squad != null) throw new InvalidDataException($"{at}[{i}]: a group is a pick or a squad, not both");
                    if (g.Pick != null)
                    {
                        Weights(g.Pick, $"{at}[{i}].pick");
                        foreach (var (kind, n) in g.AtLeast ?? new())
                            if (!ids.Contains(kind) || s.Enemy(kind).Boss || n < 0) throw new InvalidDataException($"{at}[{i}].atLeast names \"{kind}\": an unknown enemy or a boss (or a negative count)");
                        if ((g.AtLeast?.Values.Sum() ?? 0) > g.Count) throw new InvalidDataException($"{at}[{i}].atLeast asks for more than count");
                        continue;
                    }
                    if (g.AtLeast != null) throw new InvalidDataException($"{at}[{i}].atLeast goes with pick");
                    if (g.Squad != null)
                    {
                        if (g.Squad.Length is 0 or > 4 || g.Squad.Any(m => m == null || !m.Fixed || !ids.Contains(m.Enemy) || s.Enemy(m.Enemy).Boss || m.Count is < 1 or > 6))
                            throw new InvalidDataException($"{at}[{i}].squad must list 1–4 known, non-boss enemies with counts 1–6");
                        // A squad that never fits would hold the wave for ever.
                        if (g.SquadSize is < 2 or > 8 || g.SquadSize > maxAlive) throw new InvalidDataException($"{at}[{i}].squad has {g.SquadSize} enemies: it needs 2–8 and at most maxAlive ({maxAlive})");
                        continue;
                    }
                    if (!ids.Contains(g.Enemy)) throw new InvalidDataException($"{at}[{i}] names an unknown enemy \"{g.Enemy}\"");
                }
            }
        }
        Waves(s.WavePlan, "waves", s.MaxAlive, false);
        if (s.Upgrades is { Length: 0 }) throw new InvalidDataException("upgrades must list at least one upgrade");
        var upgradeIds = new HashSet<string>();
        foreach (var u in s.UpgradeList)
        {
            if (u == null) throw new InvalidDataException("upgrades has an empty entry");
            string at = $"upgrades[{(string.IsNullOrEmpty(u.Id) ? "?" : u.Id)}]";
            Id(u.Id, at + ".id");
            if (!upgradeIds.Add(u.Id)) throw new InvalidDataException($"upgrade id \"{u.Id}\" is used twice");
            Text(u.NameVi, at + ".nameVi"); Text(u.NameEn, at + ".nameEn");
            if (u.DescVi == null || u.DescEn == null || u.DescVi.Length > 80 || u.DescEn.Length > 80) throw new InvalidDataException($"{at} descriptions must be at most 80 characters");
            if (!UpgradeLimits.TryGetValue(u.Effect ?? "", out var limit)) throw new InvalidDataException($"{at}.effect must be one of: {string.Join(", ", UpgradeLimits.Keys)}");
            Within(u.Amount, limit.Min, limit.Max, at + ".amount");
            if (u.MaxStacks is < 0 or > 10) throw new InvalidDataException($"{at}.maxStacks must be 0–10 (0: no limit)");
            if (u.Group != null && Array.IndexOf(BuildGroups.All, u.Group) < 0) throw new InvalidDataException($"{at}.group must be one of: {string.Join(", ", BuildGroups.All)} (or left out)");
            if (u.Group != null && u.Effect == "heal") throw new InvalidDataException($"{at}: a heal card cannot belong to a group");
            if (u.Rare && (u.Group == null || u.MaxStacks != 1)) throw new InvalidDataException($"{at}: a rare card needs a group and maxStacks 1");
        }
        foreach (var group in BuildGroups.All)
            if (s.UpgradeList.Count(u => u.Rare && u.Group == group) > 1) throw new InvalidDataException($"upgrades: at most one rare card per group ({group})");
        Within(s.RareChance, 0, 1, "rareChance");
        if (s.MaxElites is < 0 or > 5) throw new InvalidDataException("maxElites must be 0–5");
        var synergyIds = new HashSet<string>();
        foreach (var y in s.SynergyList)
        {
            if (y == null) throw new InvalidDataException("synergies has an empty entry");
            string at = $"synergies[{(string.IsNullOrEmpty(y.Id) ? "?" : y.Id)}]";
            Id(y.Id, at + ".id");
            if (!synergyIds.Add(y.Id)) throw new InvalidDataException($"synergy id \"{y.Id}\" is used twice");
            Text(y.NameVi, at + ".nameVi"); Text(y.NameEn, at + ".nameEn");
            if (y.DescVi == null || y.DescEn == null || y.DescVi.Length > 80 || y.DescEn.Length > 80) throw new InvalidDataException($"{at} descriptions must be at most 80 characters");
            if (Array.IndexOf(BuildGroups.All, y.Group) < 0) throw new InvalidDataException($"{at}.group must be one of: {string.Join(", ", BuildGroups.All)}");
            if (!SynergyLimits.TryGetValue(y.Effect ?? "", out var limit)) throw new InvalidDataException($"{at}.effect must be one of: {string.Join(", ", SynergyLimits.Keys)}");
            Within(y.Amount, limit.Min, limit.Max, at + ".amount");
            if (y.At is < 1 or > 10) throw new InvalidDataException($"{at}.at must be 1–10");
            if (y.Effect == "echo" && y.Every is < 2 or > 10) throw new InvalidDataException($"{at}.every must be 2–10");
        }
        Within(s.ThreatDamageShare, 0, 1, "threatDamageShare");
        if (s.MaxAttackers is < 0 or > 10) throw new InvalidDataException("maxAttackers must be 0–10 (0: no limit)");
        Within(s.AttackStagger, 0, 3, "attackStagger"); Within(s.IncomingWarning, 0, 5, "incomingWarning");
        if (s.Campaign is CampaignSpec campaign) ValidateCampaign(campaign, (waves, at, alive, survival) => Waves(waves, at, alive ?? s.MaxAlive, survival), Id, Text);
    }

    public static readonly string[] StageObjectives = { "clear", "survive" };
    // What an upgrade can do, and its amount's range (fractions, DIPs/s or HP).
    public static readonly Dictionary<string, (double Min, double Max)> UpgradeLimits = new()
    {
        ["damage"] = (0.01, 2), ["moveSpeed"] = (0.01, 1), ["attackSpeed"] = (0.01, 0.5),
        ["knock"] = (1, 500), ["heal"] = (1, 1000), ["armor"] = (0.01, 0.5), ["maxHp"] = (1, 500),
        // Rare cards' effects: shell (seconds to grow back), opener and momentum (fractions).
        ["shell"] = (3, 60), ["opener"] = (0.1, 3), ["momentum"] = (0.1, 3)
    };
    public static readonly Dictionary<string, (double Min, double Max)> SynergyLimits = new()
    {
        ["thorns"] = (0.05, 2), ["armorBreak"] = (0.05, 3), ["echo"] = (0.1, 2)
    };

    private static void ValidateCampaign(CampaignSpec campaign, Action<IReadOnlyList<WaveSpec>?, string, int?, bool> waves, Action<string?, string> id, Action<string?, string> text)
    {
        if (campaign.Chapters == null || campaign.Chapters.Length == 0) throw new InvalidDataException("campaign.chapters must list at least one chapter");
        var chapterIds = new HashSet<string>();
        // Stage ids are unique across the whole campaign: progress is saved by them alone.
        var stageIds = new HashSet<string>();
        for (int c = 0; c < campaign.Chapters.Length; c++)
        {
            var chapter = campaign.Chapters[c] ?? throw new InvalidDataException($"campaign.chapters[{c}] is empty");
            string at = $"campaign.chapters[{(string.IsNullOrEmpty(chapter.Id) ? c.ToString() : chapter.Id)}]";
            id(chapter.Id, at + ".id");
            if (!chapterIds.Add(chapter.Id)) throw new InvalidDataException($"chapter id \"{chapter.Id}\" is used twice");
            text(chapter.NameVi, at + ".nameVi"); text(chapter.NameEn, at + ".nameEn");
            if (chapter.Stages == null || chapter.Stages.Length is < 1 or > 12) throw new InvalidDataException($"{at}.stages must list 1–12 stages");
            for (int i = 0; i < chapter.Stages.Length; i++)
            {
                var stage = chapter.Stages[i] ?? throw new InvalidDataException($"{at}.stages[{i}] is empty");
                string where = $"{at}.stages[{(string.IsNullOrEmpty(stage.Id) ? i.ToString() : stage.Id)}]";
                id(stage.Id, where + ".id");
                if (!stageIds.Add(stage.Id)) throw new InvalidDataException($"stage id \"{stage.Id}\" is used twice");
                text(stage.NameVi, where + ".nameVi"); text(stage.NameEn, where + ".nameEn");
                if (stage.GoalVi == null || stage.GoalEn == null || stage.GoalVi.Length > 120 || stage.GoalEn.Length > 120)
                    throw new InvalidDataException($"{where} goals must be at most 120 characters");
                if (Array.IndexOf(StageObjectives, stage.Objective) < 0) throw new InvalidDataException($"{where}.objective must be one of: {string.Join(", ", StageObjectives)}");
                if (stage.RulesVersion is < 1 or > 1000) throw new InvalidDataException($"{where}.rulesVersion must be 1–1000");
                if (stage.MaxAlive is int alive && alive is < 1 or > 10) throw new InvalidDataException($"{where}.maxAlive must be 1–10");
                if (stage.MaxUpgrades is int picks && picks is < 1 or > 30) throw new InvalidDataException($"{where}.maxUpgrades must be 1–30");
                if (!double.IsFinite(stage.EliteChance) || stage.EliteChance is < 0 or > 1) throw new InvalidDataException($"{where}.eliteChance must be 0–1");
                if (stage.Survival && stage.SurviveSeconds is not (>= 10 and <= 600)) throw new InvalidDataException($"{where}.surviveSeconds must be 10–600 for a survive stage");
                if (!stage.Survival && stage.SurviveSeconds != null) throw new InvalidDataException($"{where}.surviveSeconds is only for a survive stage");
                waves(stage.Waves, where + ".waves", stage.MaxAlive, stage.Survival);
                if (!double.IsFinite(stage.Threat) || stage.Threat is < 0.5 or > 3) throw new InvalidDataException($"{where}.threat must be 0.5–3");
                if (!double.IsFinite(stage.ThreatGrowth) || stage.ThreatGrowth is < 0 or > 2 || (stage.ThreatGrowth > 0 && !stage.Survival))
                    throw new InvalidDataException($"{where}.threatGrowth must be 0–2 (per minute), and only for a survive stage");
                if (stage.UpgradeAt is { } offers)
                {
                    if (!stage.Survival || !stage.Upgrades) throw new InvalidDataException($"{where}.upgradeAt is only for a survive stage with upgrades");
                    if (offers.Length is 0 or > 10) throw new InvalidDataException($"{where}.upgradeAt must list 1–10 times");
                    for (int k = 0; k < offers.Length; k++)
                        if (!double.IsFinite(offers[k]) || offers[k] < 1 || offers[k] >= stage.SurviveSeconds || (k > 0 && offers[k] <= offers[k - 1]))
                            throw new InvalidDataException($"{where}.upgradeAt must rise and stay inside the survive time");
                }
                if ((stage.TipVi == null) != (stage.TipEn == null) || stage.TipVi?.Length > 220 || stage.TipEn?.Length > 220)
                    throw new InvalidDataException($"{where} tips need both languages, at most 220 characters each");
                if (stage.Reward != null && PetAccessories.Find(stage.Reward) is not { Reward: not null })
                    throw new InvalidDataException($"{where}.reward \"{stage.Reward}\" is not a reward accessory in accessories.json");
            }
        }
    }
}

// The user's skills, in HUD order: name, how it is used, cooldown and a description built
// from the current numbers (so battle.json changes show in the HUD). Adding a skill: a
// BattleSkill value, an entry here, its HudGlyph and its effect in BattleSim.Use.
public sealed record BattleSkillInfo(BattleSkill Skill, bool Aimed, Func<BattleSettings, double> Cooldown,
    string NameVi, string NameEn, Func<BattleSettings, string> DescribeVi, Func<BattleSettings, string> DescribeEn)
{
    public string Name(bool english) => english ? NameEn : NameVi;
    public string Describe(BattleSettings s, bool english) => english ? DescribeEn(s) : DescribeVi(s);
}

public static class BattleSkills
{
    private static string N(double value) => value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    public static readonly BattleSkillInfo[] All =
    {
        new(BattleSkill.Push, true, s => s.PushCooldown, "Đẩy quái", "Push",
            s => $"Đẩy quái — chọn rồi click một slime (nửa trái: đẩy sang phải, nửa phải: sang trái), choáng {N(s.PushStunSeconds)} giây. Hồi {N(s.PushCooldown)} giây.",
            s => $"Push — choose, then click a slime (left half: push right, right half: push left); stuns {N(s.PushStunSeconds)} s. {N(s.PushCooldown)} s cooldown."),
        new(BattleSkill.Heal, false, s => s.HealCooldown, "Cá hồi máu", "Healing fish",
            s => $"Cá hồi máu — mèo hồi {N(s.HealAmount)} HP. Hồi {N(s.HealCooldown)} giây.",
            s => $"Healing fish — the cat gets {N(s.HealAmount)} HP back. {N(s.HealCooldown)} s cooldown."),
        new(BattleSkill.Cheer, false, s => s.CheerCooldown, "Cổ vũ", "Cheer",
            s => $"Cổ vũ — {N(s.CheerSeconds)} giây mèo đánh nhanh ×{N(s.CheerSpeed)}, mạnh ×{N(s.CheerDamage)}. Hồi {N(s.CheerCooldown)} giây.",
            s => $"Cheer — for {N(s.CheerSeconds)} s the cat attacks ×{N(s.CheerSpeed)} as often, ×{N(s.CheerDamage)} as hard. {N(s.CheerCooldown)} s cooldown."),
        new(BattleSkill.Bomb, true, s => s.BombCooldown, "Bom len", "Yarn bomb",
            s => $"Bom len — chọn rồi click một slime: nổ sau {N(s.BombDelay)} giây, {N(s.BombDamage)} sát thương cho mọi quái quanh đó. Hồi {N(s.BombCooldown)} giây.",
            s => $"Yarn bomb — choose, then click a slime: bursts after {N(s.BombDelay)} s for {N(s.BombDamage)} damage to every slime nearby. {N(s.BombCooldown)} s cooldown."),
        new(BattleSkill.Shield, false, s => s.ShieldCooldown, "Khiên bong bóng", "Bubble shield",
            s => $"Khiên bong bóng — {N(s.ShieldSeconds)} giây mèo không mất máu. Hồi {N(s.ShieldCooldown)} giây.",
            s => $"Bubble shield — for {N(s.ShieldSeconds)} s the cat takes no damage. {N(s.ShieldCooldown)} s cooldown."),
    };

    public static BattleSkillInfo Get(BattleSkill skill) => All[Array.FindIndex(All, info => info.Skill == skill)];
}
