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
    [JsonIgnore] public bool Empty => Wins == 0 && BestSeconds == null && MostDefeated == 0;

    internal void Normalize()
    {
        Wins = Math.Max(0, Wins);
        MostDefeated = Math.Max(0, MostDefeated);
        if (BestSeconds is double s && (!double.IsFinite(s) || s <= 0)) BestSeconds = null;
    }
}

// What a finished battle changed: a new fastest win and/or a new most-slimes count.
public readonly record struct BattleRecordNews(bool Fastest, bool MostDefeated)
{
    public bool Any => Fastest || MostDefeated;
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

    public static string Key(string weapon, BattleDifficulty difficulty, bool custom = false) =>
        $"{weapon}|{difficulty}{(custom ? "|custom" : "")}";

    public BattleRecord Legacy(BattleDifficulty difficulty) => difficulty switch
    {
        BattleDifficulty.Easy => Easy, BattleDifficulty.Hard => Hard, _ => Normal
    };

    // This loadout's records (an empty one if it has none yet; nothing is stored by reading).
    public BattleRecord For(string weapon, BattleDifficulty difficulty, bool custom = false) =>
        Loadouts.TryGetValue(Key(weapon, difficulty, custom), out var record) ? record : new();

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
        path ??= DefaultPath;
        if (!File.Exists(path)) return new();
        try
        {
            string text = File.ReadAllText(path);
            var records = JsonSerializer.Deserialize<BattleRecords>(text) ?? new();
            // A version 1 file has no "Version": its Easy/Normal/Hard are the legacy records.
            using (var document = JsonDocument.Parse(text))
                if (!document.RootElement.TryGetProperty("Version", out _)) records.Version = 1;
            records.ReadOnly = records.Version > CurrentVersion;
            records.Easy ??= new(); records.Normal ??= new(); records.Hard ??= new();
            records.Easy.Normalize(); records.Normal.Normalize(); records.Hard.Normalize();
            records.Loadouts = (records.Loadouts ?? new())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                .ToDictionary(pair => pair.Key, pair => { pair.Value.Normalize(); return pair.Value; });
            if (!records.ReadOnly) records.Version = CurrentVersion;
            return records;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.WriteLine($"Battle records could not be read, starting over: {ex.Message}");
            return new();
        }
    }

    public bool Save(string? path = null)
    {
        if (ReadOnly)
        {
            Trace.WriteLine("Battle records come from a newer version of the pet; they are not overwritten.");
            return false;
        }
        path ??= DefaultPath;
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, JsonSerializer.Serialize(this));
            File.Move(temp, path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"Battle records could not be saved: {ex.Message}");
            return false;
        }
    }
}
