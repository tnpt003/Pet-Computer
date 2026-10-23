using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DesktopPet;

// Infinity (battle.json "endless", 2026-10-22): one battle that is never won. Waves come one after
// another, bigger (FirstCount + CountPerWave per wave, at most MaxCount) and stronger (threat
// 1 + ThreatPerWave per wave); a kind joins the draw from its From wave on. Every BossEvery-th wave
// is the Boss alone; after every UpgradeEvery-th cleared wave the user picks a card for this run.
// It ends when the cat falls or the user ends it (both show the result); closing the app ends it
// with nothing saved. Records: the furthest wave per weapon and difficulty (BattleRecords).
public sealed class EndlessSpec
{
    // Waves made for one run; past the last one it is played again (no one gets that far).
    public const int PlannedWaves = 500;
    public string Id { get; init; } = "endless";
    public int RulesVersion { get; init; } = 1;
    // The Adventure stage to win first (null: open from the start).
    public string? UnlockAfter { get; init; }
    public int UpgradeEvery { get; init; } = 3;
    public int BossEvery { get; init; } = 10;
    public string Boss { get; init; } = "king";
    // From wave LateBossFrom on, a boss wave brings LateBoss beside Boss (null: never).
    public string? LateBoss { get; init; }
    public int LateBossFrom { get; init; } = 30;
    // The elite chance grows by EliteChancePerWave a wave, at most EliteChanceMax (0: no cap); every
    // MoreElitesEvery waves one more elite may be alive at once (at most MoreElitesMax more).
    public double EliteChancePerWave { get; init; }
    public double EliteChanceMax { get; init; }
    public int MoreElitesEvery { get; init; }
    public int MoreElitesMax { get; init; } = 2;
    // From this wave on every synergy (the Infinity-only tiers too) is on (null: never).
    public int? AllSynergiesAt { get; init; }
    // A cleared boss wave offers a second card.
    public bool BossBonusPick { get; init; }
    public double FirstCount { get; init; } = 3;
    public double CountPerWave { get; init; } = 0.35;
    public int MaxCount { get; init; } = 14;
    public double ThreatPerWave { get; init; } = 0.06;
    // Enemies on the floor at once (unset: the battle's maxAlive) and the elite chance (StageSpec).
    public int? MaxAlive { get; init; }
    public double EliteChance { get; init; }
    public EndlessKind[] Kinds { get; init; } = Array.Empty<EndlessKind>();
    // Rewards for clearing a wave (2026-10-22), each given once: accessories and displayed keepsakes
    // (KeepsakeSpec with Wave). Earned from the records (BattleRecords.EndlessCleared), never with custom data.
    public KeepsakeSpec[] Milestones { get; init; } = Array.Empty<KeepsakeSpec>();
    public IEnumerable<KeepsakeSpec> EarnedBy(int cleared) => Milestones.Where(m => m.Wave <= cleared);
    public KeepsakeSpec? MilestoneAt(int wave) => Milestones.FirstOrDefault(m => m.Wave == wave);

    public int Count(int wave) => Math.Clamp((int)Math.Floor(FirstCount + CountPerWave * (wave - 1)), 1, MaxCount);
    public double Threat(int wave) => 1 + ThreatPerWave * (wave - 1);
    public bool IsBossWave(int wave) => BossEvery > 0 && wave % BossEvery == 0;

    public WaveSpec Wave(int wave)
    {
        if (IsBossWave(wave))
            return new()
            {
                Threat = Threat(wave),
                Groups = LateBoss != null && wave >= LateBossFrom
                    ? new[] { new WaveGroup { Enemy = Boss, Count = 1 }, new WaveGroup { Enemy = LateBoss, Count = 1 } }
                    : new[] { new WaveGroup { Enemy = Boss, Count = 1 } }
            };
        var pick = Kinds.Where(k => k.From <= wave && k.Weight > 0).ToDictionary(k => k.Enemy, k => k.Weight);
        return new() { Threat = Threat(wave), Groups = new[] { new WaveGroup { Pick = pick, Count = Count(wave) } } };
    }

    // The battle's stage: endless, cards every UpgradeEvery waves, the waves above.
    public StageSpec Stage() => new()
    {
        Id = Id, NameVi = "Vô tận", NameEn = "Infinity", Objective = StageSpec.EndlessObjective, Upgrades = UpgradeEvery > 0,
        UpgradeEvery = UpgradeEvery, RulesVersion = RulesVersion, MaxAlive = MaxAlive, EliteChance = EliteChance,
        EliteChancePerWave = EliteChancePerWave, EliteChanceMax = EliteChanceMax, MoreElitesEvery = MoreElitesEvery, MoreElitesMax = MoreElitesMax,
        AllSynergiesAt = AllSynergiesAt, BossBonusPick = BossBonusPick,
        Waves = Enumerable.Range(1, PlannedWaves).Select(Wave).ToArray()
    };

    // Open once its Adventure stage is won (or when the data has no such stage), as the Expedition.
    public bool IsUnlocked(AdventureProgress progress, BattleSettings settings) => UnlockAfter is not string stage || progress.Completed.Contains(stage)
        || settings.Campaign?.Chapters.Any(c => c.Stages.Any(s => s.Id == stage)) != true;

    public static void Validate(EndlessSpec e, BattleSettings s)
    {
        const string at = "endless";
        if (string.IsNullOrWhiteSpace(e.Id)) throw new InvalidDataException(at + ".id is needed");
        if (e.RulesVersion is < 1 or > 1000) throw new InvalidDataException(at + ".rulesVersion must be 1–1000");
        if (e.UpgradeEvery is < 0 or > 20) throw new InvalidDataException(at + ".upgradeEvery must be 0–20 (0: no cards)");
        if (e.BossEvery is < 0 or > 100) throw new InvalidDataException(at + ".bossEvery must be 0–100 (0: no boss)");
        if (e.BossEvery > 0 && s.EnemyTypes.FirstOrDefault(t => t.Id == e.Boss) is not { Boss: true }) throw new InvalidDataException(at + ".boss must be a boss enemy");
        if (e.LateBoss != null && s.EnemyTypes.FirstOrDefault(t => t.Id == e.LateBoss) is not { Boss: true }) throw new InvalidDataException(at + ".lateBoss must be a boss enemy");
        if (e.LateBossFrom is < 1 or > PlannedWaves) throw new InvalidDataException(at + ".lateBossFrom must be 1–" + PlannedWaves);
        if (!double.IsFinite(e.EliteChancePerWave) || e.EliteChancePerWave is < 0 or > 0.2) throw new InvalidDataException(at + ".eliteChancePerWave must be 0–0.2");
        if (!double.IsFinite(e.EliteChanceMax) || e.EliteChanceMax is < 0 or > 1) throw new InvalidDataException(at + ".eliteChanceMax must be 0–1");
        if (e.MoreElitesEvery is < 0 or > 200 || e.MoreElitesMax is < 0 or > 4) throw new InvalidDataException(at + ".moreElitesEvery must be 0–200 and moreElitesMax 0–4");
        if (e.AllSynergiesAt is < 1 or > PlannedWaves) throw new InvalidDataException(at + ".allSynergiesAt must be 1–" + PlannedWaves);
        if (!double.IsFinite(e.FirstCount) || e.FirstCount is < 1 or > 30) throw new InvalidDataException(at + ".firstCount must be 1–30");
        if (!double.IsFinite(e.CountPerWave) || e.CountPerWave is < 0 or > 5) throw new InvalidDataException(at + ".countPerWave must be 0–5");
        if (e.MaxCount is < 1 or > 60) throw new InvalidDataException(at + ".maxCount must be 1–60");
        if (!double.IsFinite(e.ThreatPerWave) || e.ThreatPerWave is < 0 or > 1) throw new InvalidDataException(at + ".threatPerWave must be 0–1");
        if (e.MaxAlive is < 1 or > 20) throw new InvalidDataException(at + ".maxAlive must be 1–20");
        if (!double.IsFinite(e.EliteChance) || e.EliteChance is < 0 or > 1) throw new InvalidDataException(at + ".eliteChance must be 0–1");
        if (e.Kinds == null || e.Kinds.Length == 0) throw new InvalidDataException(at + ".kinds must list enemies");
        foreach (var k in e.Kinds)
        {
            if (k == null || s.EnemyTypes.FirstOrDefault(t => t.Id == k.Enemy) is not EnemyType type || type.Boss) throw new InvalidDataException(at + ".kinds must name enemies that are not bosses");
            if (!double.IsFinite(k.Weight) || k.Weight is < 0 or > 1000) throw new InvalidDataException(at + ".kinds weight must be 0–1000");
            if (k.From is < 1 or > EndlessSpec.PlannedWaves) throw new InvalidDataException(at + ".kinds from must be 1–" + PlannedWaves);
        }
        if (!e.Kinds.Any(k => k.From == 1 && k.Weight > 0)) throw new InvalidDataException(at + ".kinds needs an enemy from wave 1");
        if (e.Milestones == null || e.Milestones.Length > 12) throw new InvalidDataException(at + ".milestones must list at most 12 rewards");
        var taken = (s.Expedition?.Keepsakes ?? Array.Empty<KeepsakeSpec>()).Select(k => k.Id).ToHashSet();
        int lastWave = 0;
        foreach (var m in e.Milestones)
        {
            string where = $"{at}.milestones[{m?.Id}]";
            if (m == null) throw new InvalidDataException(at + ".milestones has an empty entry");
            if (m.Id == null || !System.Text.RegularExpressions.Regex.IsMatch(m.Id, "^[a-z0-9_-]{1,32}$")) throw new InvalidDataException(where + ".id must be 1–32 lower-case letters, digits, _ or -");
            if (!taken.Add(m.Id)) throw new InvalidDataException($"{where}: id \"{m.Id}\" is used twice (keepsake ids are shared with the Expedition)");
            if (m.NameVi is not { Length: > 0 and <= 32 } || m.NameEn is not { Length: > 0 and <= 32 }) throw new InvalidDataException(where + " names must be 1–32 characters");
            if (m.Wave <= lastWave || m.Wave > PlannedWaves) throw new InvalidDataException(where + ".wave must grow from one reward to the next, at most " + PlannedWaves);
            lastWave = m.Wave;
            if (m.Kind is not (KeepsakeSpec.AccessoryKind or KeepsakeSpec.Hourglass or KeepsakeSpec.Orb or KeepsakeSpec.Cushion))
                throw new InvalidDataException(where + ".kind must be accessory, hourglass, orb or cushion");
            if (m.Kind == KeepsakeSpec.AccessoryKind && PetAccessories.Find(m.Accessory) is not { Reward: not null })
                throw new InvalidDataException(where + ".accessory must name a reward accessory of accessories.json");
        }
    }
}

// One kind in Infinity's draw: its weight, from wave From on.
public sealed class EndlessKind
{
    public string Enemy { get; init; } = "slime";
    public double Weight { get; init; } = 1;
    public int From { get; init; } = 1;
}
