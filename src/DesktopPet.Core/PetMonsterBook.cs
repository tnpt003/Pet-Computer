using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace DesktopPet;

// 1.5 M27 (docs/UPDATE-1.5.md L1): the Monster Book (monster-book.json next to preferences.json): the
// enemy kinds and formations the cat has met and how many of each kind it defeated. A kind or a
// formation has a page once met; pages the hub has shown are Seen (the others carry "New"). Battles
// with the user's own battle.json add nothing (the window's business). Saving is atomic (.tmp moved
// over the old file, kept as .bak); a broken file is read from its .bak or starts over; a file from a
// newer version is read as far as it is understood and never overwritten. Seeded: a book made before
// this file existed was filled once from the Adventure stages already won.
public sealed class MonsterBook
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public List<string> Met { get; set; } = new();
    public List<string> Formations { get; set; } = new();
    public Dictionary<string, int> Defeated { get; set; } = new();
    // Pages shown: kind ids and FormationPage ids.
    public List<string> Seen { get; set; } = new();
    public bool Seeded { get; set; }
    // 1.5 M29a: the app icons and malware met (BattleSim.MetApps ids), for the Custom Fight's locks;
    // AppsSeeded: filled once from the Adventure stages already won.
    public List<string> Apps { get; set; } = new();
    public bool AppsSeeded { get; set; }
    [JsonIgnore] public bool ReadOnly { get; private set; }

    public static string DefaultPath => Path.Combine(PetPreferences.DataDirectory, "monster-book.json");
    public static string FormationPage(string id) => "f:" + id;

    public bool Knows(string kind) => Met.Contains(kind);
    public bool KnowsApp(string id) => Apps.Contains(id);
    public bool KnowsFormation(string id) => Formations.Contains(id);
    public int DefeatedOf(string kind) => Defeated.GetValueOrDefault(kind);

    public static MonsterBook Load(string path)
    {
        var book = JsonStore.ReadOrBackup<MonsterBook>(path, "Monster book");
        if (book == null) return new();
        book.ReadOnly = book.Version > CurrentVersion;
        if (!book.ReadOnly) book.Version = CurrentVersion;
        book.Normalize();
        return book;
    }

    private void Normalize()
    {
        static List<string> Clean(List<string>? list) => (list ?? new()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        Met = Clean(Met); Formations = Clean(Formations); Seen = Clean(Seen); Apps = Clean(Apps);
        Defeated = (Defeated ?? new()).Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public bool Save(string path)
    {
        if (ReadOnly)
        {
            Trace.WriteLine("The monster book comes from a newer version of the pet; it is not overwritten.");
            return false;
        }
        return JsonStore.Write(path, this, "Monster book", indented: true, backup: true);
    }

    // A battle's meetings and defeats. Returns the pages it opened (kinds with a page in the data, then
    // formations), in that order; changed: whether anything at all was added.
    public List<string> Add(BattleSettings data, IEnumerable<string> met, IReadOnlyDictionary<string, int> defeated, IEnumerable<string> formations, out bool changed)
    {
        var opened = new List<string>();
        var known = Met.ToHashSet();
        changed = false;
        foreach (var kind in met.Concat(defeated.Keys))
        {
            string page = PageOf(data, kind);
            if (!Met.Contains(kind)) { Met.Add(kind); changed = true; }
            if (!Met.Contains(page)) { Met.Add(page); changed = true; }
            if (HasPage(data, page) && !Seen.Contains(page) && !opened.Contains(page) && !known.Contains(page)) opened.Add(page);
        }
        // Defeats count on the page (the chapter 1 Slime King on the Slime King's).
        foreach (var (kind, count) in defeated)
            if (count > 0) { string page = PageOf(data, kind); Defeated[page] = DefeatedOf(page) + count; changed = true; }
        foreach (var id in formations)
            if (!Formations.Contains(id) && data.Formation(id) != null)
            {
                Formations.Add(id);
                changed = true;
                opened.Add(FormationPage(id));
            }
        return opened;
    }

    // 1.5 M29a: a battle's apps (changed: anything new).
    public void AddApps(IEnumerable<string> apps, out bool changed)
    {
        changed = false;
        foreach (var id in apps)
            if (!string.IsNullOrWhiteSpace(id) && !Apps.Contains(id)) { Apps.Add(id); changed = true; }
    }

    // Once: what the Adventure stages already won surely showed: the five first icons in a stage with
    // malware on, the malware its plan sends, the random malware and viruses its threat allows, and the
    // Screenshot with the three newer malware where a stage has newer apps (chapter 2).
    public bool SeedApps(CampaignSpec? campaign, BattleSettings data, AdventureProgress progress)
    {
        if (AppsSeeded) return false;
        AppsSeeded = true;
        var rules = data.Apps;
        foreach (var stage in campaign?.Chapters.SelectMany(c => c.Stages) ?? Enumerable.Empty<StageSpec>())
        {
            if (!progress.Completed.Contains(stage.Id) || !stage.Malware || rules == null) continue;
            AddApps(new[] { "tabs", "ping", "endtask", "undo", "trash" }, out _);
            AddApps(stage.MalwarePlan?.Select(c => c.Kind) ?? Enumerable.Empty<string>(), out _);
            double threat = stage.Threat * (stage.Waves.Length == 0 ? 1 : stage.Waves.Max(w => w.Threat));
            if (stage.MalwarePlan == null && threat >= rules.MalwareMinThreat) AddApps(new[] { "loading", "update", "virus", "ads" }, out _);
            if (stage.MalwarePlan == null && threat >= rules.InfectMinThreat) AddApps(new[] { "infect" }, out _);
            if (stage.NewAppsFrom > 0) AddApps(new[] { "screenshot", "captcha", "battery", "cursor" }, out _);
        }
        return true;
    }

    // The page a kind is shown on: its own, or the one it shares (EnemyType.Page).
    public static string PageOf(BattleSettings data, string kind) => data.EnemyTypes.FirstOrDefault(e => e.Id == kind)?.Page ?? kind;
    // A kind with a page of its own: one with a tip (a boss's file shards and a splitter's halves have none).
    public static bool HasPage(BattleSettings data, string kind) => data.EnemyTypes.FirstOrDefault(e => e.Id == kind) is { TipVi: not null };

    // Once: the kinds and formations of the Adventure stages already won count as met (none defeated).
    public bool Seed(CampaignSpec? campaign, BattleSettings data, AdventureProgress progress)
    {
        if (Seeded) return false;
        Seeded = true;
        foreach (var stage in campaign?.Chapters.SelectMany(c => c.Stages) ?? Enumerable.Empty<StageSpec>())
        {
            if (!progress.Completed.Contains(stage.Id)) continue;
            foreach (var wave in stage.Waves)
            {
                foreach (var kind in wave.Stream?.Weights.Keys ?? Enumerable.Empty<string>()) Meet(kind);
                foreach (var g in wave.Groups ?? Array.Empty<WaveGroup>())
                    if (g.Pick != null) foreach (var kind in g.Pick.Keys) Meet(kind);
                    else if (g.Formation != null) { foreach (var id in g.Formation.Keys) if (data.Formation(id) is FormationSpec f) { foreach (var kind in f.Members) Meet(kind); } }
                    else if (g.Squad != null) foreach (var m in g.Squad) Meet(m.Enemy);
                    else Meet(g.Enemy);
            }
        }
        // What is seeded is not news: those pages count as seen.
        foreach (var kind in Met) if (!Seen.Contains(kind)) Seen.Add(kind);
        return true;

        void Meet(string? kind)
        {
            if (string.IsNullOrEmpty(kind) || !data.EnemyTypes.Any(e => e.Id == kind)) return;
            foreach (var id in new[] { kind, PageOf(data, kind) }) if (!Met.Contains(id)) Met.Add(id);
        }
    }
}
