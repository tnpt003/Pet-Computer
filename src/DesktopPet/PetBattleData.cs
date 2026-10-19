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
    // while moving) or "cracks" (a stony body).
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
    public string Name(bool english) => english ? NameEn : NameVi;
}

// One wave: groups spawn in order (at most MaxAlive at once).
public sealed class WaveGroup
{
    public string Enemy { get; init; } = "slime";
    public int Count { get; init; } = 1;
}

public sealed class WaveSpec
{
    public WaveGroup[] Groups { get; init; } = Array.Empty<WaveGroup>();
    public IEnumerable<string> Spawns() => Groups.SelectMany(g => Enumerable.Repeat(g.Enemy, Math.Max(0, g.Count)));
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
    public int RulesVersion { get; init; } = 1;
    // Enemies on the floor at once (unset: the battle's maxAlive).
    public int? MaxAlive { get; init; }
    public WaveSpec[] Waves { get; init; } = Array.Empty<WaveSpec>();
    public string Name(bool english) => english ? NameEn : NameVi;
    public string Goal(bool english) => english ? GoalEn : GoalVi;
}

// An upgrade offered between the waves of an Adventure stage: it lasts for that run only (a new
// run, stage or battle starts without it). Effect is one the code knows (UpgradeEffects); Amount
// is a fraction for damage/moveSpeed/attackSpeed/armor (0.2 = 20 %), DIPs/s for knock, HP for heal.
// MaxStacks: how often one run may take it (0 = no limit). Definitions are shared and never changed.
public sealed class UpgradeSpec
{
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string Effect { get; init; } = "damage";
    public double Amount { get; init; }
    public int MaxStacks { get; init; }
    public string Name(bool english) => english ? NameEn : NameVi;
    public string Describe(bool english) => english ? DescEn : DescVi;
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
        Merge(root, user, "");
        return Parse(root.ToJsonString());
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
                key.Equals("upgrades", StringComparison.OrdinalIgnoreCase);
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
    public static readonly string[] EnemyMarks = { "none", "speed", "cracks" };

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
        if (s.RescueGraceCooldownSeconds > 60) throw new InvalidDataException("rescueGraceCooldownSeconds must be at most 60");
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
            if (e.EnrageAt > 0) { Within(e.EnragedSpeed, 0.1, 400, at + ".enragedSpeed"); Within(e.EnragedWindup, 0.1, 10, at + ".enragedWindup"); }
        }
        foreach (var e in s.EnemyTypes)
            if (e.SummonAt > 0 && (!ids.Contains(e.SummonType) || s.Enemy(e.SummonType).SummonAt > 0))
                throw new InvalidDataException($"enemies[{e.Id}] summons \"{e.SummonType}\": an unknown enemy (or one that summons too)");
        void Waves(IReadOnlyList<WaveSpec>? waves, string at)
        {
            if (waves == null || waves.Count == 0) throw new InvalidDataException($"{at} needs at least one wave");
            if (waves.Count > 30) throw new InvalidDataException($"{at} may have at most 30 waves");
            for (int i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                if (wave?.Groups == null || wave.Groups.Length == 0) throw new InvalidDataException($"{at}[{i}] needs at least one group");
                foreach (var g in wave.Groups)
                {
                    if (g == null || !ids.Contains(g.Enemy)) throw new InvalidDataException($"{at}[{i}] names an unknown enemy \"{g?.Enemy}\"");
                    if (g.Count is < 1 or > 50) throw new InvalidDataException($"{at}[{i}] {g.Enemy}: count must be 1–50");
                }
            }
        }
        Waves(s.WavePlan, "waves");
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
        }
        if (s.Campaign is CampaignSpec campaign) ValidateCampaign(campaign, Waves, Id, Text);
    }

    public static readonly string[] StageObjectives = { "clear", "survive" };
    // What an upgrade can do, and its amount's range (fractions, DIPs/s or HP).
    public static readonly Dictionary<string, (double Min, double Max)> UpgradeLimits = new()
    {
        ["damage"] = (0.01, 2), ["moveSpeed"] = (0.01, 1), ["attackSpeed"] = (0.01, 0.5),
        ["knock"] = (1, 500), ["heal"] = (1, 1000), ["armor"] = (0.01, 0.5)
    };

    private static void ValidateCampaign(CampaignSpec campaign, Action<IReadOnlyList<WaveSpec>?, string> waves, Action<string?, string> id, Action<string?, string> text)
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
                if (stage.Survival && stage.SurviveSeconds is not (>= 10 and <= 600)) throw new InvalidDataException($"{where}.surviveSeconds must be 10–600 for a survive stage");
                if (!stage.Survival && stage.SurviveSeconds != null) throw new InvalidDataException($"{where}.surviveSeconds is only for a survive stage");
                waves(stage.Waves, where + ".waves");
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
