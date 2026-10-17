using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DesktopPet;

// The user's best battles, per difficulty: the fastest win and the most slimes defeated in one
// battle (won or lost). Kept in battle-records.json next to preferences.json; a broken file is
// logged and starts over empty, it never stops a battle.
public sealed class BattleRecord
{
    public int Wins { get; set; }
    // Seconds of the fastest win (null: no win yet).
    public double? BestSeconds { get; set; }
    public int MostDefeated { get; set; }

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

public sealed class BattleRecords
{
    public BattleRecord Easy { get; set; } = new();
    public BattleRecord Normal { get; set; } = new();
    public BattleRecord Hard { get; set; } = new();

    public BattleRecord For(BattleDifficulty difficulty) => difficulty switch
    {
        BattleDifficulty.Easy => Easy, BattleDifficulty.Hard => Hard, _ => Normal
    };

    // Adds one finished battle (ended by a win or a loss, not by the user).
    public BattleRecordNews Add(BattleDifficulty difficulty, bool won, int defeated, double seconds)
    {
        var record = For(difficulty);
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
            var records = JsonSerializer.Deserialize<BattleRecords>(File.ReadAllText(path)) ?? new();
            records.Easy ??= new(); records.Normal ??= new(); records.Hard ??= new();
            records.Easy.Normalize(); records.Normal.Normalize(); records.Hard.Normalize();
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
