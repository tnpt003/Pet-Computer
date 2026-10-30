using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DesktopPet;

// 1.5 M28 (docs/UPDATE-1.5.md L2): one achievement from Assets/achievements.json. Stat names a number a
// battle gives (AchievementStats); Total adds it up over battles, otherwise the best single battle counts.
// Reward names a coat or accessory whose "reward" is "achievement": earned, it shows in Customize.
public sealed record AchievementSpec(string Id, string NameVi, string NameEn, string DescVi, string DescEn, string Stat, double Goal, bool Total, string? Reward = null)
{
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    public string Describe(string language) => PetText.Data(language, DescVi, DescEn);
}

public static class PetAchievements
{
    // What a coat's or an accessory's "reward" says when an achievement gives it.
    public const string RewardTag = "achievement";
    private static readonly Lazy<AchievementSpec[]> Loaded = new(Load);

    // Shown in this order.
    public static IReadOnlyList<AchievementSpec> All => Loaded.Value;
    public static AchievementSpec? Find(string? id) => All.FirstOrDefault(a => a.Id == id);
    // The achievement that gives a coat or an accessory, if any.
    public static AchievementSpec? Giving(string? item) => item == null ? null : All.FirstOrDefault(a => a.Reward == item);

    private static AchievementSpec[] Load()
    {
        try
        {
            using var stream = typeof(PetAchievements).Assembly.GetManifestResourceStream("DesktopPet.achievements.json")
                ?? throw new InvalidDataException("Missing embedded achievements.json");
            return Parse(stream);
        }
        // A broken file must never stop the pet: log it and offer no achievements.
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            Trace.WriteLine($"Achievements could not be loaded: {ex.Message}");
            return Array.Empty<AchievementSpec>();
        }
    }

    private static readonly Regex Id = new("^[a-z][a-z0-9_]{0,31}$");

    // Throws InvalidDataException/JsonException on any mistake, so a half-valid file is never used.
    public static AchievementSpec[] Parse(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("achievements", out var items) || items.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("achievements.json needs an \"achievements\" list");
        var list = new List<AchievementSpec>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Each achievement must be an object");
            string id = Text(item, "id") is string found && Id.IsMatch(found) ? found : throw new InvalidDataException("An achievement has a bad id");
            if (list.Any(a => a.Id == id)) throw new InvalidDataException("Duplicate achievement id " + id);
            var (nameVi, nameEn) = Names(item, "name", id);
            var (descVi, descEn) = Names(item, "desc", id);
            string stat = Text(item, "stat") is string s && AchievementStats.Names.Contains(s) ? s : throw new InvalidDataException(id + " has an unknown stat");
            double goal = item.TryGetProperty("goal", out var g) && g.ValueKind == JsonValueKind.Number ? g.GetDouble() : double.NaN;
            if (!(goal > 0 && goal <= 1_000_000)) throw new InvalidDataException(id + ": goal must be 1-1000000");
            bool total = Text(item, "scope") switch
            {
                "total" => true,
                "battle" => false,
                _ => throw new InvalidDataException(id + " needs scope total or battle")
            };
            string? reward = Text(item, "reward");
            if (reward != null)
            {
                bool coat = PetCoats.All.Any(c => c.Id == reward && c.Reward == RewardTag);
                bool accessory = PetAccessories.Find(reward) is { Reward: RewardTag };
                if (!coat && !accessory) throw new InvalidDataException($"{id}.reward \"{reward}\" is not a coat or accessory with reward \"{RewardTag}\"");
                if (list.Any(a => a.Reward == reward)) throw new InvalidDataException($"{id}.reward \"{reward}\" is already given by another achievement");
            }
            list.Add(new AchievementSpec(id, nameVi, nameEn, descVi, descEn, stat, goal, total, reward));
        }
        return list.ToArray();
    }

    private static (string Vi, string En) Names(JsonElement item, string name, string id)
    {
        if (!item.TryGetProperty(name, out var names) || names.ValueKind != JsonValueKind.Object
            || Text(names, "vi") is not { Length: > 0 } vi || Text(names, "en") is not { Length: > 0 } en)
            throw new InvalidDataException($"{id} needs {name} with vi and en");
        return (vi, en);
    }

    private static string? Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

// The numbers a finished (or left) battle gives the achievements. Only reads the battle.
public static class AchievementStats
{
    public static readonly string[] Names =
    {
        "wins", "defeated", "bosses", "combos", "flawless", "clutch", "hardWin", "quickWin", "heldHits", "throwHits",
        "ropesCut", "rages", "bubbles", "ghosts", "iceBreaks", "fullBasket", "coins", "endlessWaves", "popups", "quakeHits"
    };

    // A quick win: won within this many seconds of fighting.
    public const double QuickWinSeconds = 120;
    // A clutch win: won with at most this share of the cat's HP left.
    public const double ClutchShare = 0.15;

    public static Dictionary<string, double> From(BattleSim b)
    {
        bool won = b.Phase == BattlePhase.Won;
        return new()
        {
            ["wins"] = won ? 1 : 0,
            ["defeated"] = b.Defeated,
            ["bosses"] = b.BossDefeated ? 1 : 0,
            ["combos"] = b.CombosLanded,
            ["flawless"] = won && b.HurtTaken <= 0 ? 1 : 0,
            ["clutch"] = won && b.CatHp <= ClutchShare * b.CatMaxHp ? 1 : 0,
            ["hardWin"] = won && b.Difficulty == BattleDifficulty.Hard ? 1 : 0,
            ["quickWin"] = won && b.FightSeconds <= QuickWinSeconds ? 1 : 0,
            ["heldHits"] = b.HeldHits,
            ["throwHits"] = b.ThrowHits,
            ["ropesCut"] = b.CatRopeCuts + b.RopesCut + b.BossRopesCut,
            ["rages"] = b.Rages,
            ["bubbles"] = b.BubblesPopped,
            ["ghosts"] = b.GhostsRevealed,
            ["iceBreaks"] = b.IceBreaks,
            ["fullBasket"] = won && b.Basket != null && b.FishTotal > 0 && b.FishInBasket >= b.FishTotal ? 1 : 0,
            ["coins"] = b.Coins,
            ["endlessWaves"] = b.Endless ? b.WavesCleared : 0,
            ["popups"] = b.PopupsClosed,
            ["quakeHits"] = b.QuakeHits,
        };
    }
}

// The achievements the cat has (achievements.json next to preferences.json): the running totals, each
// stat's best battle and the achievements earned. Battles with the user's own battle.json add nothing
// (the window's business). Saving is atomic (.tmp moved over the old file, kept as .bak); a broken file
// is read from its .bak or starts over; a file from a newer version is read but never overwritten.
public sealed class AchievementBook
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public Dictionary<string, double> Totals { get; set; } = new();
    public Dictionary<string, double> Best { get; set; } = new();
    public List<string> Earned { get; set; } = new();
    [JsonIgnore] public bool ReadOnly { get; private set; }

    public static string DefaultPath => Path.Combine(PetPreferences.DataDirectory, "achievements.json");

    public bool Has(string id) => Earned.Contains(id);

    // How far an achievement is (capped at its goal).
    public double Progress(AchievementSpec a) =>
        Math.Min(a.Goal, (a.Total ? Totals : Best).GetValueOrDefault(a.Stat));

    // A coat or accessory an earned achievement gave.
    public bool HasReward(string? item, IEnumerable<AchievementSpec> all) =>
        item != null && all.Any(a => a.Reward == item && Has(a.Id));

    public static AchievementBook Load(string path)
    {
        var book = JsonStore.ReadOrBackup<AchievementBook>(path, "Achievements");
        if (book == null) return new();
        book.ReadOnly = book.Version > CurrentVersion;
        if (!book.ReadOnly) book.Version = CurrentVersion;
        book.Normalize();
        return book;
    }

    private void Normalize()
    {
        static Dictionary<string, double> Clean(Dictionary<string, double>? map) =>
            (map ?? new()).Where(p => !string.IsNullOrWhiteSpace(p.Key) && double.IsFinite(p.Value) && p.Value > 0).ToDictionary(p => p.Key, p => p.Value);
        Totals = Clean(Totals); Best = Clean(Best);
        Earned = (Earned ?? new()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
    }

    public bool Save(string path)
    {
        if (ReadOnly)
        {
            Trace.WriteLine("The achievements come from a newer version of the pet; they are not overwritten.");
            return false;
        }
        return JsonStore.Write(path, this, "Achievements", indented: true, backup: true);
    }

    // One battle's numbers. Returns the achievements it earned, in the list's order; changed: whether
    // anything at all was added.
    public List<AchievementSpec> Add(IEnumerable<AchievementSpec> all, IReadOnlyDictionary<string, double> stats, out bool changed)
    {
        changed = false;
        foreach (var (stat, value) in stats)
        {
            if (!double.IsFinite(value) || value <= 0) continue;
            Totals[stat] = Totals.GetValueOrDefault(stat) + value;
            if (value > Best.GetValueOrDefault(stat)) Best[stat] = value;
            changed = true;
        }
        var earned = new List<AchievementSpec>();
        foreach (var a in all)
            if (!Has(a.Id) && Progress(a) >= a.Goal)
            {
                Earned.Add(a.Id);
                earned.Add(a);
                changed = true;
            }
        return earned;
    }
}
