using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace DesktopPet;

// 1.5 M29a (docs/UPDATE-1.5.md F1): the Custom Fight, the free battle the user builds. A preset says
// which slimes come and how often (0–3 each), how many waves (the first with PerWave enemies, one more
// each wave after), how many on the floor at once, a last boss (or none), how likely elites are, how
// strong (threat), cards between waves or not, which app icons and how much of each malware. Weapon,
// difficulty and skills come from the setup panel as before. Only what the Monster Book has met can be
// chosen. The built-in "Default" is no preset at all: the old free battle (no stage), with its records.
// A preset is built into a StageSpec marked Custom: no records, no achievements, no Adventure progress.
public sealed class CustomFight
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    // Kind (Monster Book page) → how often: 1 few, 2 some, 3 many.
    public Dictionary<string, int> Enemies { get; set; } = new();
    public int Waves { get; set; } = 3;
    public int PerWave { get; set; } = 3;
    public int MaxAlive { get; set; } = 3;
    // A boss page (king, zip, mother, mutant_king) for the last wave; null: none.
    public string? Boss { get; set; }
    // Percent: 0, 10, 25 or 50 (never on Easy, as everywhere).
    public int Elites { get; set; }
    public double Threat { get; set; } = 1;
    public bool Cards { get; set; }
    // App icon ids (BattleSim.AppId) and malware (MalwareCue.Kinds) → 1–3.
    public List<string> Icons { get; set; } = new();
    public Dictionary<string, int> Malware { get; set; } = new();

    public const string DefaultId = "default";
    public const int MinWaves = 1, MaxWaves = 10, MinPerWave = 2, MaxPerWave = 12, MinAlive = 2, MaxAliveCap = 8, MaxLevel = 3;
    public const double MinThreat = 0.6, MaxThreat = 2, ThreatStep = 0.2;
    public static readonly int[] EliteSteps = { 0, 10, 25, 50 };
    public static readonly BattleAppKind[] IconOrder = { BattleAppKind.Tabs, BattleAppKind.Ping, BattleAppKind.EndTask, BattleAppKind.Undo, BattleAppKind.Trash, BattleAppKind.Screenshot };

    public CustomFight Copy(string id, string name) => new()
    {
        Id = id, Name = name, Enemies = new(Enemies), Waves = Waves, PerWave = PerWave, MaxAlive = MaxAlive, Boss = Boss,
        Elites = Elites, Threat = Threat, Cards = Cards, Icons = new(Icons), Malware = new(Malware)
    };

    // Numbers back within their limits (a hand-edited or older file).
    public void Normalize()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[..Math.Min(Name.Trim().Length, 40)];
        Enemies = (Enemies ?? new()).Where(p => !string.IsNullOrWhiteSpace(p.Key) && p.Value > 0).ToDictionary(p => p.Key, p => Math.Min(p.Value, MaxLevel));
        Malware = (Malware ?? new()).Where(p => MalwareCue.Kinds.Contains(p.Key) && p.Value > 0).ToDictionary(p => p.Key, p => Math.Min(p.Value, MaxLevel));
        Icons = (Icons ?? new()).Where(id => IconOrder.Any(k => BattleSim.AppId(k) == id)).Distinct().ToList();
        Waves = Math.Clamp(Waves, MinWaves, MaxWaves);
        PerWave = Math.Clamp(PerWave, MinPerWave, MaxPerWave);
        MaxAlive = Math.Clamp(MaxAlive, MinAlive, MaxAliveCap);
        Elites = EliteSteps.OrderBy(step => Math.Abs(step - Elites)).First();
        Threat = double.IsFinite(Threat) ? Math.Round(Math.Clamp(Threat, MinThreat, MaxThreat) / ThreatStep) * ThreatStep : 1;
        if (string.IsNullOrWhiteSpace(Boss)) Boss = null;
    }

    // A first preset: what the old free battle brings (slime, quick slime, rock slime, the Slime King).
    public static CustomFight Starter(string id, string name) => new()
    {
        Id = id, Name = name, Enemies = new() { ["slime"] = 2, ["runner"] = 2, ["rock"] = 1 }, Waves = 3, PerWave = 2, MaxAlive = 3, Boss = "king"
    };
}

// Why a choice is refused.
public enum CustomFightBlock { None, Locked, Links, Mouse }

public static class CustomFights
{
    // The kinds a preset can bring: those with a Monster Book page; slimes, then bosses.
    public static List<EnemyType> Kinds(BattleSettings s) => s.EnemyTypes.Where(e => e.TipVi != null && !e.Boss).ToList();
    public static List<EnemyType> Bosses(BattleSettings s) => s.EnemyTypes.Where(e => e.TipVi != null && e.Boss).ToList();
    // The kind a page spawns: the one sharing it (the chapter 1 King, with its shield phases, for the King's page), else itself.
    public static string SpawnOf(BattleSettings s, string page) => s.EnemyTypes.FirstOrDefault(e => e.Page == page)?.Id ?? page;
    public static bool Open(MonsterBook book, string page) => book.Knows(page);
    public static bool AppOpen(MonsterBook book, string id) => book.KnowsApp(id);

    // The kinds (spawned) a preset brings, boss included.
    private static IEnumerable<string> Spawns(BattleSettings s, CustomFight fight) =>
        fight.Enemies.Where(p => p.Value > 0).Select(p => SpawnOf(s, p.Key)).Concat(fight.Boss is string boss ? new[] { SpawnOf(s, boss) } : Array.Empty<string>());

    // The kinds of link and of cursor tricks the preset brings (at most two of each).
    public static (HashSet<string> Links, HashSet<string> Mouse) Limits(BattleSettings s, CustomFight fight) => BattleData.Reach(s, Spawns(s, fight));

    // Whether a kind (or boss: boss true) may be added to the preset now.
    public static CustomFightBlock CanAdd(BattleSettings s, MonsterBook book, CustomFight fight, string page, bool boss = false)
    {
        if (!Open(book, page)) return CustomFightBlock.Locked;
        var trial = fight.Copy(fight.Id, fight.Name);
        if (boss) trial.Boss = page; else trial.Enemies[page] = 1;
        var (links, mouse) = Limits(s, trial);
        if (links.Count > EnemyLinks.MaxKinds) return CustomFightBlock.Links;
        if (mouse.Count > BattleData.MaxMouseKinds) return CustomFightBlock.Mouse;
        return CustomFightBlock.None;
    }

    // The preset as a battle (null: nothing to fight, or it breaks a limit). Kinds the book has not met
    // (or the data no longer has) are left out.
    public static StageSpec? Build(BattleSettings s, CustomFight fight, MonsterBook book)
    {
        var kinds = Kinds(s).Select(e => e.Id).ToHashSet();
        var weights = new Dictionary<string, double>();
        foreach (var (page, level) in fight.Enemies)
            if (level > 0 && kinds.Contains(page) && Open(book, page)) weights[SpawnOf(s, page)] = Math.Clamp(level, 1, CustomFight.MaxLevel);
        string? boss = fight.Boss is string b && Bosses(s).Any(e => e.Id == b) && Open(book, b) ? SpawnOf(s, b) : null;
        if (weights.Count == 0 && boss == null) return null;
        var clean = fight.Copy(fight.Id, fight.Name);
        clean.Enemies = clean.Enemies.Where(p => kinds.Contains(p.Key) && Open(book, p.Key)).ToDictionary(p => p.Key, p => p.Value);
        if (boss == null) clean.Boss = null;
        var (links, mouse) = Limits(s, clean);
        if (links.Count > EnemyLinks.MaxKinds || mouse.Count > BattleData.MaxMouseKinds) return null;
        var waves = new List<WaveSpec>();
        if (weights.Count > 0)
            for (int i = 0; i < fight.Waves; i++)
                waves.Add(new WaveSpec { Groups = new[] { new WaveGroup { Pick = weights, Count = fight.PerWave + i } } });
        if (boss != null) waves.Add(new WaveSpec { Groups = new[] { new WaveGroup { Enemy = boss, Count = 1 } } });
        var icons = CustomFight.IconOrder.Where(k => fight.Icons.Contains(BattleSim.AppId(k)) && AppOpen(book, BattleSim.AppId(k))).ToArray();
        var malware = MalwareCue.Kinds.Select(k => AppOpen(book, k) ? Math.Clamp(fight.Malware.GetValueOrDefault(k), 0, CustomFight.MaxLevel) : 0).ToArray();
        // Malware needs a desktop to come to; the ads and the virus need icons too (the ads, a Tabs to lock them).
        var apps = icons.Length > 0 ? new CustomAppRules { Icons = icons, Malware = malware } : null;
        return new StageSpec
        {
            Id = "custom:" + fight.Id, NameVi = fight.Name, NameEn = fight.Name, Custom = true,
            Upgrades = fight.Cards, Threat = fight.Threat, MaxAlive = fight.MaxAlive, EliteChance = fight.Elites / 100.0,
            Malware = apps?.AnyMalware == true, CustomApps = apps, Waves = waves.ToArray()
        };
    }
}

// custom-fights.json next to preferences.json: the user's presets (at most MaxPresets) and the one chosen
// (null: Default). Saved atomically (.tmp, .bak); a broken file is read from its .bak or starts empty; a
// file from a newer version is read as far as it is understood and never overwritten.
public sealed class CustomFightBook
{
    public const int CurrentVersion = 1, MaxPresets = 6;
    public int Version { get; set; } = CurrentVersion;
    public string? Selected { get; set; }
    public List<CustomFight> Presets { get; set; } = new();
    [JsonIgnore] public bool ReadOnly { get; private set; }

    public static string DefaultPath => Path.Combine(PetPreferences.DataDirectory, "custom-fights.json");

    public static CustomFightBook Load(string path)
    {
        var book = JsonStore.ReadOrBackup<CustomFightBook>(path, "Custom fights");
        if (book == null) return new();
        book.ReadOnly = book.Version > CurrentVersion;
        if (!book.ReadOnly) book.Version = CurrentVersion;
        book.Presets = (book.Presets ?? new()).Where(p => p != null && !string.IsNullOrWhiteSpace(p.Id) && p.Id != CustomFight.DefaultId)
            .GroupBy(p => p.Id).Select(g => g.First()).Take(MaxPresets).ToList();
        foreach (var p in book.Presets) p.Normalize();
        if (book.Selected != null && book.Find(book.Selected) == null) book.Selected = null;
        return book;
    }

    public bool Save(string path)
    {
        if (ReadOnly)
        {
            Trace.WriteLine("The custom fights come from a newer version of the pet; they are not overwritten.");
            return false;
        }
        return JsonStore.Write(path, this, "Custom fights", indented: true, backup: true);
    }

    public CustomFight? Find(string? id) => id == null ? null : Presets.FirstOrDefault(p => p.Id == id);
    public CustomFight? Chosen => Find(Selected);
    public bool Full => Presets.Count >= MaxPresets;

    // A new preset, a copy of the given one (null: the starter), chosen at once; null when full.
    public CustomFight? Add(CustomFight? from, Func<int, string> name)
    {
        if (Full) return null;
        int n = 1;
        while (Presets.Any(p => p.Name == name(n))) n++;
        string id;
        do id = Guid.NewGuid().ToString("N")[..8]; while (Find(id) != null || id == CustomFight.DefaultId);
        var fight = from?.Copy(id, name(n)) ?? CustomFight.Starter(id, name(n));
        fight.Normalize();
        Presets.Add(fight);
        Selected = id;
        return fight;
    }

    public bool Delete(string id)
    {
        var fight = Find(id);
        if (fight == null) return false;
        int at = Presets.IndexOf(fight);
        Presets.Remove(fight);
        if (Selected == id) Selected = Presets.Count == 0 ? null : Presets[Math.Min(at, Presets.Count - 1)].Id;
        return true;
    }
}
