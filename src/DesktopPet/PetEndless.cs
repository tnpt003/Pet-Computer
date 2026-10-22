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
    public double FirstCount { get; init; } = 3;
    public double CountPerWave { get; init; } = 0.35;
    public int MaxCount { get; init; } = 14;
    public double ThreatPerWave { get; init; } = 0.06;
    // Enemies on the floor at once (unset: the battle's maxAlive) and the elite chance (StageSpec).
    public int? MaxAlive { get; init; }
    public double EliteChance { get; init; }
    public EndlessKind[] Kinds { get; init; } = Array.Empty<EndlessKind>();

    public int Count(int wave) => Math.Clamp((int)Math.Floor(FirstCount + CountPerWave * (wave - 1)), 1, MaxCount);
    public double Threat(int wave) => 1 + ThreatPerWave * (wave - 1);
    public bool IsBossWave(int wave) => BossEvery > 0 && wave % BossEvery == 0;

    public WaveSpec Wave(int wave)
    {
        if (IsBossWave(wave)) return new() { Threat = Threat(wave), Groups = new[] { new WaveGroup { Enemy = Boss, Count = 1 } } };
        var pick = Kinds.Where(k => k.From <= wave && k.Weight > 0).ToDictionary(k => k.Enemy, k => k.Weight);
        return new() { Threat = Threat(wave), Groups = new[] { new WaveGroup { Pick = pick, Count = Count(wave) } } };
    }

    // The battle's stage: endless, cards every UpgradeEvery waves, the waves above.
    public StageSpec Stage() => new()
    {
        Id = Id, NameVi = "Vô tận", NameEn = "Infinity", Objective = StageSpec.EndlessObjective, Upgrades = UpgradeEvery > 0,
        UpgradeEvery = UpgradeEvery, RulesVersion = RulesVersion, MaxAlive = MaxAlive, EliteChance = EliteChance,
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
    }
}

// One kind in Infinity's draw: its weight, from wave From on.
public sealed class EndlessKind
{
    public string Enemy { get; init; } = "slime";
    public double Weight { get; init; } = 1;
    public int From { get; init; } = 1;
}
