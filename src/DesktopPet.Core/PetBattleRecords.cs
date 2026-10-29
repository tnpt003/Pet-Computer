using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopPet;

// The user's best battles: the fastest win and the most slimes defeated in one battle (won or
// lost). Kept in battle-records.json next to preferences.json; a broken file is logged and
// starts over empty, it never stops a battle.
public sealed class BattleRecord
{
    public int Wins { get; set; }
    // Seconds of the fastest win (null: no win yet).
    public double? BestSeconds { get; set; }
    public int MostDefeated { get; set; }
    // Infinity: the furthest wave reached (0: none; only in "endless|…" records).
    public int MostWaves { get; set; }
    // Infinity: the most waves cleared in one run (records before 2026-10-22 have 0: MostWaves − 1 stands in).
    public int MostCleared { get; set; }
    // A stage with a fish basket (1.5 M20): the most fish kept in a win (0: none yet; only in its stage records).
    public int MostFish { get; set; }
    [JsonIgnore] public bool Empty => Wins == 0 && BestSeconds == null && MostDefeated == 0 && MostWaves == 0 && MostCleared == 0 && MostFish == 0;

    internal void Normalize()
    {
        Wins = Math.Max(0, Wins);
        MostDefeated = Math.Max(0, MostDefeated);
        MostWaves = Math.Max(0, MostWaves);
        MostCleared = Math.Clamp(MostCleared, 0, MostWaves);
        MostFish = Math.Max(0, MostFish);
        if (BestSeconds is double s && (!double.IsFinite(s) || s <= 0)) BestSeconds = null;
    }
}

// What a finished battle changed: a new fastest win and/or a new most-slimes count (a basket stage: the
// most fish kept in a win).
public readonly record struct BattleRecordNews(bool Fastest, bool MostDefeated, bool FurthestWave = false, bool MostFish = false)
{
    public bool Any => Fastest || MostDefeated || FurthestWave || MostFish;
}

// Version 2 (2026-10-19): records per loadout, keyed by the weapon's stable id and the
// difficulty ("hammer|Normal"), with "|custom" added for battles fought with the user's own
// battle.json (same ids, maybe other numbers: kept apart, not compared). Version 1 files had
// one record per difficulty for both weapons together; those stay as they were under
// Easy/Normal/Hard, shown as "before weapons were told apart", and are never given to a weapon.
// A file from a newer version is read as far as it is understood and never overwritten.
public sealed class BattleRecords
{
    public const int CurrentVersion = 2;
    public int Version { get; set; } = CurrentVersion;
    public Dictionary<string, BattleRecord> Loadouts { get; set; } = new();
    // Version 1 records (per difficulty, weapons not told apart). Never added to.
    public BattleRecord Easy { get; set; } = new();
    public BattleRecord Normal { get; set; } = new();
    public BattleRecord Hard { get; set; } = new();
    // Read from a newer version: saving would lose what this version does not know.
    [JsonIgnore] public bool ReadOnly { get; private set; }

    // noApps: an Infinity run with the app icons turned off (its records kept apart).
    public static string Key(string weapon, BattleDifficulty difficulty, bool custom = false, bool noApps = false) =>
        $"{weapon}|{difficulty}{(noApps ? "|noapps" : "")}{(custom ? "|custom" : "")}";

    public BattleRecord Legacy(BattleDifficulty difficulty) => difficulty switch
    {
        BattleDifficulty.Easy => Easy, BattleDifficulty.Hard => Hard, _ => Normal
    };

    // This loadout's records (an empty one if it has none yet; nothing is stored by reading).
    public BattleRecord For(string weapon, BattleDifficulty difficulty, bool custom = false) =>
        Loadouts.TryGetValue(Key(weapon, difficulty, custom), out var record) ? record : new();

    // Infinity's records live beside the free battle's, under "endless|r<rules>|<loadout key>".
    public static string EndlessKey(EndlessSpec spec, string weapon, BattleDifficulty difficulty, bool custom = false, bool noApps = false) =>
        $"{spec.Id}|r{spec.RulesVersion}|{Key(weapon, difficulty, custom, noApps)}";
    public BattleRecord ForEndless(EndlessSpec spec, string weapon, BattleDifficulty difficulty, bool custom = false, bool noApps = false) =>
        Loadouts.TryGetValue(EndlessKey(spec, weapon, difficulty, custom, noApps), out var record) ? record : new();
    // The furthest wave over every loadout (the hub's card), custom data and runs without apps left out.
    public int EndlessBest(EndlessSpec spec) => Loadouts.Where(pair => pair.Key.StartsWith($"{spec.Id}|r{spec.RulesVersion}|", StringComparison.Ordinal) && !pair.Key.EndsWith("|custom", StringComparison.Ordinal)
            && !pair.Key.Contains("|noapps", StringComparison.Ordinal))
        .Select(pair => pair.Value.MostWaves).DefaultIfEmpty(0).Max();

    // The most waves cleared in one run over every loadout and rules version (milestone rewards stay
    // earned when the rules change), runs without apps included, custom data left out.
    public int EndlessCleared(EndlessSpec spec) => Loadouts.Where(pair => pair.Key.StartsWith(spec.Id + "|r", StringComparison.Ordinal) && !pair.Key.EndsWith("|custom", StringComparison.Ordinal))
        .Select(pair => Math.Max(pair.Value.MostCleared, pair.Value.MostWaves - 1)).DefaultIfEmpty(0).Max();

    // One ended Infinity run: the wave it reached, the waves it cleared and the slimes it defeated.
    public BattleRecordNews AddEndless(EndlessSpec spec, string weapon, BattleDifficulty difficulty, bool custom, int wave, int defeated, int cleared = 0, bool noApps = false)
    {
        string key = EndlessKey(spec, weapon, difficulty, custom, noApps);
        if (!Loadouts.TryGetValue(key, out var record)) Loadouts[key] = record = new();
        bool furthest = wave > record.MostWaves, most = defeated > record.MostDefeated;
        if (furthest) record.MostWaves = wave;
        record.MostCleared = Math.Clamp(Math.Max(record.MostCleared, cleared), 0, record.MostWaves);
        if (most) record.MostDefeated = defeated;
        return new(false, most, furthest);
    }

    // Adds one finished battle (ended by a win or a loss, not by the user) to its loadout.
    public BattleRecordNews Add(string weapon, BattleDifficulty difficulty, bool custom, bool won, int defeated, double seconds)
    {
        string key = Key(weapon, difficulty, custom);
        if (!Loadouts.TryGetValue(key, out var record)) Loadouts[key] = record = new();
        bool fastest = false, most = false;
        if (won)
        {
            record.Wins++;
            if (double.IsFinite(seconds) && seconds > 0 && (record.BestSeconds is not double best || seconds < best - 0.05))
            {
                record.BestSeconds = seconds;
                fastest = true;
            }
        }
        if (defeated > record.MostDefeated)
        {
            record.MostDefeated = defeated;
            most = true;
        }
        return new(fastest, most);
    }

    public static string DefaultPath => Path.Combine(PetPreferences.DataDirectory, "battle-records.json");

    public static BattleRecords Load(string? path = null)
    {
        // A version 1 file has no "Version": its Easy/Normal/Hard are the legacy records.
        var records = JsonStore.Read<BattleRecords>(path ?? DefaultPath, "Battle records",
            (file, root) => { if (!root.TryGetProperty("Version", out _)) file.Version = 1; });
        if (records == null) return new();
        records.ReadOnly = records.Version > CurrentVersion;
        records.Easy ??= new(); records.Normal ??= new(); records.Hard ??= new();
        records.Easy.Normalize(); records.Normal.Normalize(); records.Hard.Normalize();
        records.Loadouts = (records.Loadouts ?? new())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
            .ToDictionary(pair => pair.Key, pair => { pair.Value.Normalize(); return pair.Value; });
        if (!records.ReadOnly) records.Version = CurrentVersion;
        return records;
    }

    public bool Save(string? path = null)
    {
        if (ReadOnly)
        {
            Trace.WriteLine("Battle records come from a newer version of the pet; they are not overwritten.");
            return false;
        }
        return JsonStore.Write(path ?? DefaultPath, this, "Battle records");
    }
}
