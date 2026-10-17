using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DesktopPet;

// One kind of enemy. The slime and the Slime King are built from BattleSettings' numbers;
// Assets/battle.json lists every kind with its own numbers, so a new enemy is data only.
// Abilities are switches: Slam (area hit with a floor warning), summon helpers below an HP
// fraction, get angry below another. Plain data (no WPF) for the simulation and tests.
public sealed class EnemyType
{
    public string Id { get; init; } = "slime";
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

// Assets/battle.json (embedded): the battle's numbers, enemy kinds and waves. A broken file
// never stops the pet: it is logged and the built-in defaults (same numbers) are used.
public static class BattleData
{
    private static readonly Lazy<BattleSettings> Loaded = new(Load);
    public static BattleSettings Settings => Loaded.Value;

    private static BattleSettings Load()
    {
        try
        {
            using var stream = typeof(BattleData).Assembly.GetManifestResourceStream("DesktopPet.battle.json")
                ?? throw new InvalidDataException("Missing embedded battle.json");
            return Parse(stream);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or NotSupportedException)
        {
            Trace.WriteLine($"Battle data could not be loaded, using the built-in battle: {ex.Message}");
            return new BattleSettings();
        }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
        // A misspelt key is an error, not a silently ignored setting.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly Regex Hex = new("^#[0-9A-Fa-f]{6}$");

    // Throws on any mistake, so a half-valid file is never used.
    public static BattleSettings Parse(Stream stream)
    {
        var settings = JsonSerializer.Deserialize<BattleSettings>(stream, Options) ?? throw new InvalidDataException("battle.json is empty");
        Validate(settings);
        return settings;
    }

    public static void Validate(BattleSettings s)
    {
        void Positive(double value, string name) { if (!double.IsFinite(value) || value <= 0) throw new InvalidDataException($"{name} must be a positive number"); }
        void NotNegative(double value, string name) { if (!double.IsFinite(value) || value < 0) throw new InvalidDataException($"{name} must not be negative"); }
        Positive(s.CatMaxHp, "catMaxHp"); NotNegative(s.CatDamage, "catDamage"); Positive(s.CatAttackRange, "catAttackRange");
        Positive(s.CatAttackCooldown, "catAttackCooldown"); Positive(s.CatRunScale, "catRunScale");
        if (s.CatRunScale > 3) throw new InvalidDataException("catRunScale must be at most 3"); Positive(s.CatSwingDuration, "catSwingDuration");
        if (s.ComboHits is < 2 or > 6) throw new InvalidDataException("comboHits must be 2–6");
        Positive(s.ComboWindow, "comboWindow"); Positive(s.ComboDamage, "comboDamage"); NotNegative(s.ComboKnock, "comboKnock");
        if (!(s.CatHitTime > 0 && s.CatHitTime < s.CatSwingDuration)) throw new InvalidDataException("catHitTime must fall inside the swing");
        Positive(s.SlimeWidth, "slimeWidth"); Positive(s.SlimeHeight, "slimeHeight"); Positive(s.MaxAlive, "maxAlive");
        foreach (var skill in BattleSkills.All) Positive(skill.Cooldown(s), skill.Skill + " cooldown");
        Positive(s.HealAmount, "healAmount"); Positive(s.CheerSeconds, "cheerSeconds"); Positive(s.CheerSpeed, "cheerSpeed");
        Positive(s.BombRadius, "bombRadius"); NotNegative(s.BombDamage, "bombDamage"); Positive(s.ShieldSeconds, "shieldSeconds");
        foreach (var (level, name) in new[] { (s.Easy, "easy"), (s.Hard, "hard") })
            foreach (var (value, key) in new[] { (level.EnemyHp, "enemyHp"), (level.EnemyDamage, "enemyDamage"), (level.EnemySpeed, "enemySpeed") })
                if (!double.IsFinite(value) || value < 0.25 || value > 3) throw new InvalidDataException($"{name}.{key} must be 0.25–3");
        var ids = new HashSet<string>();
        foreach (var e in s.EnemyTypes)
        {
            if (string.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id)) throw new InvalidDataException("Each enemy needs its own id");
            Positive(e.MaxHp, e.Id + ".maxHp"); NotNegative(e.Speed, e.Id + ".speed"); NotNegative(e.Damage, e.Id + ".damage");
            Positive(e.Range, e.Id + ".range"); Positive(e.Windup, e.Id + ".windup"); Positive(e.AttackCooldown, e.Id + ".attackCooldown");
            NotNegative(e.Recover, e.Id + ".recover");
            // The battle strip is 170 DIPs tall: bigger enemies would be cut off.
            if (!(e.Scale >= 0.5 && e.Scale <= 2.3)) throw new InvalidDataException(e.Id + ".scale must be 0.5–2.3");
            if (!Hex.IsMatch(e.Color) || !Hex.IsMatch(e.AngryColor)) throw new InvalidDataException(e.Id + " colors must be #RRGGBB");
            if (e.Marks is not ("none" or "speed" or "cracks")) throw new InvalidDataException(e.Id + ".marks must be none, speed or cracks");
            if (e.SummonAt is < 0 or >= 1 || e.EnrageAt is < 0 or >= 1) throw new InvalidDataException(e.Id + " summonAt/enrageAt must be 0–1");
            if (e.EnrageAt > 0) { Positive(e.EnragedSpeed, e.Id + ".enragedSpeed"); Positive(e.EnragedWindup, e.Id + ".enragedWindup"); }
        }
        foreach (var e in s.EnemyTypes)
            if (e.SummonAt > 0 && (e.SummonCount <= 0 || !ids.Contains(e.SummonType) || s.Enemy(e.SummonType).SummonAt > 0))
                throw new InvalidDataException(e.Id + " summons an unknown enemy (or one that summons too)");
        if (s.WavePlan.Count == 0) throw new InvalidDataException("battle.json needs at least one wave");
        foreach (var wave in s.WavePlan)
        {
            if (wave.Groups.Length == 0 || wave.Groups.Any(g => g.Count <= 0 || !ids.Contains(g.Enemy)))
                throw new InvalidDataException("Each wave needs groups of known enemies with a positive count");
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
