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
    // 1.5 M24: the bosses in turn, one per boss wave (unset: Boss every time).
    public string[]? Bosses { get; init; }
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
    // 1.5 M24: stretches of the run, each with newer kinds added to the draw. The first starts at
    // wave ErasFrom; the next ones right after each boss wave; past the last one they come round again.
    public int ErasFrom { get; init; } = 15;
    public EndlessEra[] Eras { get; init; } = Array.Empty<EndlessEra>();
    // 1.5 M24 "Mutations": after each cleared boss wave the user picks one of a few odd rules; it holds
    // until the next boss wave is cleared, then its reward comes.
    public MutationRules? Mutations { get; init; }
    public IEnumerable<KeepsakeSpec> EarnedBy(int cleared) => Milestones.Where(m => m.Wave <= cleared);
    public KeepsakeSpec? MilestoneAt(int wave) => Milestones.FirstOrDefault(m => m.Wave == wave);

    public int Count(int wave) => Math.Clamp((int)Math.Floor(FirstCount + CountPerWave * (wave - 1)), 1, MaxCount);
    public double Threat(int wave) => 1 + ThreatPerWave * (wave - 1);
    public bool IsBossWave(int wave) => BossEvery > 0 && wave % BossEvery == 0;
    // The boss of a boss wave: the rotation's turn (1.5 M24), else Boss.
    public string BossAt(int wave) => Bosses is { Length: > 0 } turn && BossEvery > 0 ? turn[(Math.Max(1, wave / BossEvery) - 1) % turn.Length] : Boss;
    // The stretch a wave belongs to: its index in Eras (-1: before ErasFrom, or no stretches).
    private int EraSpan => BossEvery > 0 ? BossEvery : 10;
    public int EraIndex(int wave)
    {
        if (Eras.Length == 0 || wave < ErasFrom) return -1;
        int first = (ErasFrom - 1) / EraSpan;
        return ((wave - 1) / EraSpan - first) % Eras.Length;
    }
    public EndlessEra? EraAt(int wave) => EraIndex(wave) is int i and >= 0 ? Eras[i] : null;
    // The first wave of a stretch (its banner).
    public bool EraStarts(int wave) => EraIndex(wave) >= 0 && (wave == ErasFrom || (wave - 1) % EraSpan == 0);

    public WaveSpec Wave(int wave)
    {
        if (IsBossWave(wave))
            return new()
            {
                Threat = Threat(wave),
                Groups = LateBoss != null && wave >= LateBossFrom
                    ? new[] { new WaveGroup { Enemy = BossAt(wave), Count = 1 }, new WaveGroup { Enemy = LateBoss, Count = 1 } }
                    : new[] { new WaveGroup { Enemy = BossAt(wave), Count = 1 } }
            };
        var pick = Kinds.Where(k => k.From <= wave && k.Weight > 0).ToDictionary(k => k.Enemy, k => k.Weight);
        // 1.5 M24: the stretch's newer kinds join the draw.
        foreach (var k in EraAt(wave)?.Kinds ?? Array.Empty<EndlessKind>())
            if (k.Weight > 0) pick[k.Enemy] = (pick.TryGetValue(k.Enemy, out double w) ? w : 0) + k.Weight;
        return new() { Threat = Threat(wave), Groups = new[] { new WaveGroup { Pick = pick, Count = Count(wave) } } };
    }

    // The battle's stage: endless, cards every UpgradeEvery waves, the waves above.
    public StageSpec Stage() => new()
    {
        Id = Id, NameVi = "Vô tận", NameEn = "Infinity", Objective = StageSpec.EndlessObjective, Upgrades = UpgradeEvery > 0,
        UpgradeEvery = UpgradeEvery, RulesVersion = RulesVersion, MaxAlive = MaxAlive, EliteChance = EliteChance,
        EliteChancePerWave = EliteChancePerWave, EliteChanceMax = EliteChanceMax, MoreElitesEvery = MoreElitesEvery, MoreElitesMax = MoreElitesMax,
        AllSynergiesAt = AllSynergiesAt, BossBonusPick = BossBonusPick, Infinity = this,
        Waves = Enumerable.Range(1, PlannedWaves).Select(Wave).ToArray()
    };

    // Open once its Adventure stage is won (or when the data has no such stage), as the Expedition.
    public bool IsUnlocked(AdventureProgress progress, BattleSettings settings) => UnlockAfter is not string stage || progress.Completed.Contains(stage)
        || settings.Campaign?.Chapters.Any(c => c.Stages.Any(s => s.Id == stage)) != true;

    // limits: the battle's check of link and cursor kinds (at most two each), run on each stretch and boss wave.
    public static void Validate(EndlessSpec e, BattleSettings s, Action<IEnumerable<WaveSpec>, string>? limits = null)
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
        // 1.5 M24: the boss rotation, the stretches and the mutations.
        if (e.Bosses != null)
        {
            if (e.Bosses.Length is 0 or > 6) throw new InvalidDataException(at + ".bosses must list 1–6 bosses");
            if (e.Bosses.Any(b => s.EnemyTypes.FirstOrDefault(t => t.Id == b) is not { Boss: true })) throw new InvalidDataException(at + ".bosses must name boss enemies");
        }
        if (e.ErasFrom is < 1 or > PlannedWaves) throw new InvalidDataException(at + ".erasFrom must be 1–" + PlannedWaves);
        if (e.Eras == null || e.Eras.Length > 12) throw new InvalidDataException(at + ".eras must list at most 12 stretches");
        for (int i = 0; i < e.Eras.Length; i++)
        {
            var era = e.Eras[i];
            string where = $"{at}.eras[{i}]";
            if (era == null || era.Kinds == null || era.Kinds.Length is 0 or > 8) throw new InvalidDataException(where + ".kinds must list 1–8 enemies");
            if (era.NameVi is not { Length: > 0 and <= 32 } || era.NameEn is not { Length: > 0 and <= 32 }) throw new InvalidDataException(where + " names must be 1–32 characters");
            foreach (var k in era.Kinds)
            {
                if (k == null || s.EnemyTypes.FirstOrDefault(t => t.Id == k.Enemy) is not EnemyType type || type.Boss) throw new InvalidDataException(where + ".kinds must name enemies that are not bosses");
                if (!double.IsFinite(k.Weight) || k.Weight is < 0 or > 1000) throw new InvalidDataException(where + ".kinds weight must be 0–1000");
                if (k.From != 1) throw new InvalidDataException(where + ".kinds take no \"from\" (the stretch says when)");
            }
        }
        if (limits != null)
        {
            // The plain draw, each stretch with every older kind, and each boss wave: at most two kinds of
            // link and two the cursor beats (the battle forgets its link kinds between waves, BattleSim.StepWaves).
            var all = e.Kinds.Select(k => k.Enemy).Distinct().ToDictionary(k => k, _ => 1.0);
            limits(new[] { new WaveSpec { Groups = new[] { new WaveGroup { Count = 1, Pick = all } } } }, at + ".kinds");
            for (int i = 0; i < e.Eras.Length; i++)
            {
                var pick = new Dictionary<string, double>(all);
                foreach (var k in e.Eras[i].Kinds) pick[k.Enemy] = 1;
                limits(new[] { new WaveSpec { Groups = new[] { new WaveGroup { Count = 1, Pick = pick } } } }, $"{at}.eras[{i}]");
            }
            if (e.BossEvery > 0)
                foreach (var boss in (e.Bosses ?? new[] { e.Boss }).Distinct())
                    limits(new[] { new WaveSpec { Groups = e.LateBoss != null ? new[] { new WaveGroup { Enemy = boss, Count = 1 }, new WaveGroup { Enemy = e.LateBoss, Count = 1 } }
                        : new[] { new WaveGroup { Enemy = boss, Count = 1 } } } }, $"{at} boss wave ({boss})");
        }
        if (e.Mutations is MutationRules mutations) MutationRules.Validate(mutations, e, s);
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

// 1.5 M24: a stretch of Infinity (EndlessSpec.Eras): its name (a banner when it starts) and the newer
// kinds added to the draw while it lasts.
public sealed class EndlessEra
{
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public EndlessKind[] Kinds { get; init; } = Array.Empty<EndlessKind>();
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
}

// 1.5 M24 "Mutations" (EndlessSpec.Mutations): after each cleared boss wave the user picks one of Offer
// rules (never the one just over); it holds until the next boss wave is cleared, then its reward comes.
// Ending the run or losing before that gives nothing.
public sealed class MutationRules
{
    public int Offer { get; init; } = 3;
    public MutationSpec[] List { get; init; } = Array.Empty<MutationSpec>();
    public MutationSpec? Find(string? id) => List.FirstOrDefault(m => m.Id == id);

    public static void Validate(MutationRules m, EndlessSpec e, BattleSettings s)
    {
        const string at = "endless.mutations";
        if (e.BossEvery <= 0) throw new InvalidDataException(at + " needs boss waves (bossEvery above 0)");
        if (m.Offer is < 1 or > 3) throw new InvalidDataException(at + ".offer must be 1–3");
        if (m.List == null || m.List.Length < m.Offer + 1 || m.List.Length > 12) throw new InvalidDataException(at + ".list must have offer + 1 to 12 rules");
        var ids = new HashSet<string>();
        foreach (var r in m.List)
        {
            string where = $"{at}.list[{r?.Id}]";
            if (r == null) throw new InvalidDataException(at + ".list has an empty entry");
            if (r.Id == null || !System.Text.RegularExpressions.Regex.IsMatch(r.Id, "^[a-z0-9_-]{1,32}$")) throw new InvalidDataException(where + ".id must be 1–32 lower-case letters, digits, _ or -");
            if (!ids.Add(r.Id) || s.UpgradeList.Any(u => u.Id == r.Id)) throw new InvalidDataException($"{where}: id \"{r.Id}\" is used twice (or by a card)");
            if (r.NameVi is not { Length: > 0 and <= 32 } || r.NameEn is not { Length: > 0 and <= 32 }) throw new InvalidDataException(where + " names must be 1–32 characters");
            if (r.DescVi is not { Length: > 0 and <= 80 } || r.DescEn is not { Length: > 0 and <= 80 } || r.RewardVi is not { Length: > 0 and <= 80 } || r.RewardEn is not { Length: > 0 and <= 80 })
                throw new InvalidDataException(where + " descriptions and rewards must be 1–80 characters");
            (double min, double max) = r.Effect switch
            {
                MutationSpec.Swift or MutationSpec.Elite or MutationSpec.Glass => (0.05, 1.0),
                MutationSpec.Crowd => (0, 1.0),
                MutationSpec.Lazy => (0.05, 2.0),
                MutationSpec.Hungry => (0, 0),
                _ => throw new InvalidDataException(where + ".effect must be swift, crowd, elite, glass, hungry or lazy")
            };
            if (!double.IsFinite(r.Amount) || r.Amount < min || r.Amount > max) throw new InvalidDataException($"{where}.amount must be {min}–{max}");
            if (r.Effect == MutationSpec.Crowd ? r.Extra is < 0 or > 4 || r.Amount == 0 && r.Extra == 0 : r.Extra != 0)
                throw new InvalidDataException(where + ".extra (more on the floor, 0–4) is only for crowd, which needs amount or extra");
            (double rMin, double rMax) = r.Reward switch
            {
                MutationSpec.DropsReward => (0.1, 3.0),
                MutationSpec.DamageReward => (0.05, 1.0),
                MutationSpec.MaxHpReward => (5, 60),
                MutationSpec.RareReward or MutationSpec.HealReward or MutationSpec.NoReward => (0, 0),
                _ => throw new InvalidDataException(where + ".reward must be drops, damage, maxHp, rare, heal or none")
            };
            if (!double.IsFinite(r.RewardAmount) || r.RewardAmount < rMin || r.RewardAmount > rMax) throw new InvalidDataException($"{where}.rewardAmount must be {rMin}–{rMax}");
            if (r.Cards is < 0 or > 3) throw new InvalidDataException(where + ".cards must be 0–3");
            if (r.Reward == MutationSpec.NoReward && r.Cards == 0) throw new InvalidDataException(where + " needs a reward or cards");
        }
    }
}

// One odd rule: its drawback (Effect, Amount, Extra) while it holds and its reward (Reward,
// RewardAmount; Cards more card picks). "drops" and "damage" pay while it holds; "maxHp", "rare",
// "heal" and the cards when the next boss wave is cleared.
public sealed class MutationSpec
{
    // swift: enemies Amount faster. crowd: Amount more enemies a wave, Extra more on the floor.
    // elite: Amount likelier elites. glass: the cat takes Amount more damage. hungry: no fish drops,
    // no heal between waves. lazy: the skills recharge Amount slower.
    public const string Swift = "swift", Crowd = "crowd", Elite = "elite", Glass = "glass", Hungry = "hungry", Lazy = "lazy";
    public const string DropsReward = "drops", DamageReward = "damage", MaxHpReward = "maxHp", RareReward = "rare", HealReward = "heal", NoReward = "none";
    // The offer shows a rule as a card of this effect (AsCard), chosen through BattleSim.ChooseUpgrade.
    public const string CardEffect = "mutation";
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string RewardVi { get; init; } = "";
    public string RewardEn { get; init; } = "";
    public string Effect { get; init; } = Swift;
    public double Amount { get; init; }
    public int Extra { get; init; }
    public string Reward { get; init; } = NoReward;
    public double RewardAmount { get; init; }
    public int Cards { get; init; }
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    public string Describe(string language) => PetText.Data(language, DescVi, DescEn);
    public string RewardText(string language) => PetText.Data(language, RewardVi, RewardEn);
    public UpgradeSpec AsCard() => new() { Id = Id, NameVi = NameVi, NameEn = NameEn, DescVi = DescVi, DescEn = DescEn, Effect = CardEffect };
}
