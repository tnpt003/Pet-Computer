using System.Text.Json;
using DesktopPet;

// 1.5 M27 (docs/UPDATE-1.5.md L1): the Monster Book: what a battle meets and defeats, the book's
// pages, the seed from stages already won, the file. Simulation and data only, no WPF.
internal static class MonsterBookTests
{
    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        const double dt = 1.0 / 60;

        // Every kind has a page except the ones that only come out of another (a splitter's halves, the Zip Slime's shards).
        var pageless = data.EnemyTypes.Where(e => e.TipVi == null && e.Page == null).Select(e => e.Id).OrderBy(id => id).ToList();
        check(pageless.SequenceEqual(new[] { "splitling", "zipshard" }), $"Every built-in kind has a page but the halves and the shards ({string.Join(", ", pageless)})");
        check(data.EnemyTypes.Where(e => e.TipVi != null).All(e => e.TipEn != null && (e.Counters ?? Array.Empty<string>()).All(k => BattleSkills.Parse(k) != null)),
            "Each page has its tip in both languages and real skills");
        check(data.EnemyTypes.Count(e => e.TipVi != null && e.Boss) == 4, "Four boss pages");
        string Bad(string extra)
        {
            try { BattleData.Parse("{ \"schemaVersion\": 1, \"enemies\": [ { \"id\": \"blob\", \"nameVi\": \"B\", \"nameEn\": \"B\", " + extra + " } ] }"); return ""; }
            catch (InvalidDataException e) { return e.Message; }
        }
        check(Bad("\"tipVi\": \"x\"").Contains("tipVi and tipEn"), "A tip in one language only is refused");
        check(Bad("\"tipVi\": \"x\", \"tipEn\": \"x\", \"counters\": [\"teleport\"]").Contains("counters"), "An unknown counter skill is refused");
        string good = Bad("\"tipVi\": \"x\", \"tipEn\": \"x\", \"counters\": [\"push\", \"fan\"]");
        check(!good.Contains("tip") && !good.Contains("counters"), $"A tip with real skills passes those checks ({good})");
        check(Bad("\"page\": \"nobody\"").Contains(".page"), "A shared page must be a known kind's");
        check(data.Enemy("king_c1").Page == "king" && MonsterBook.PageOf(data, "king_c1") == "king", "The chapter 1 Slime King shares the Slime King's page");
        var kings = new MonsterBook();
        var kingPages = kings.Add(data, new[] { "king_c1" }, new Dictionary<string, int> { ["king_c1"] = 1 }, Array.Empty<string>(), out _);
        kingPages.AddRange(kings.Add(data, new[] { "king" }, new Dictionary<string, int> { ["king"] = 1 }, Array.Empty<string>(), out _));
        check(kingPages.SequenceEqual(new[] { "king" }) && kings.DefeatedOf("king") == 2 && kings.Knows("king_c1"), $"Either King opens the one page and counts on it ({string.Join(",", kingPages)})");

        // A battle meets what stands on its floor (not while still spawning) and counts what the cat defeats.
        var settings = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 2 }, Boss = false, SlimeMaxHp = 1, SlimeDamage = 0, CatMaxHp = 1000 };
        var sim = new BattleSim(settings, 0, 1600, seed: 3);
        check(sim.MetKinds.Count == 0, "Nothing is met before the battle starts");
        for (double t = 0; t < 30 && !sim.Over; t += dt) sim.Step(dt, 800, false);
        check(sim.MetKinds.Contains("slime") && sim.DefeatedKinds.GetValueOrDefault("slime") == sim.Defeated && sim.Defeated == 2,
            $"Met and defeated by kind ({string.Join(",", sim.MetKinds)}; {sim.DefeatedKinds.GetValueOrDefault("slime")}/{sim.Defeated})");
        var watch = new BattleSim(settings, 0, 1600, seed: 3);
        var spawning = watch.AddSlime(800, data.Enemy("runner"));
        watch.Step(dt, 200, true);
        check(!watch.MetKinds.Contains("runner"), "A slime still spawning is not met yet");
        spawning.Set(SlimeState.Moving);
        var decoy = watch.AddSlime(900, data.Enemy("mirage"));
        decoy.Set(SlimeState.Moving); decoy.DecoyOf = spawning.Id;
        watch.Step(dt, 200, true);
        check(watch.MetKinds.Contains("runner") && !watch.MetKinds.Contains("mirage"), "On the floor it is met; a mirage's copy is not");

        // A formation arriving is met (Fortress, built-in).
        var squad = new BattleSettings
        {
            FirstWaveDelay = 0, Boss = false, SlimeDamage = 0, CatMaxHp = 1000, MaxAlive = 8,
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Formation = new() { ["fortress"] = 1 }, Count = 1 } } } },
            Enemies = data.EnemyTypes.ToArray(), Formations = data.FormationList.ToArray()
        };
        var formed = new BattleSim(squad, 0, 1600, seed: 2);
        for (double t = 0; t < 10 && formed.MetFormations.Count == 0; t += dt) formed.Step(dt, 800, false);
        check(formed.MetFormations.Contains("fortress"), $"An arriving formation is met ({string.Join(",", formed.MetFormations)})");

        // The book: pages open once; counts add up; a pageless kind is met but opens no page.
        var book = new MonsterBook();
        var opened = book.Add(data, new[] { "slime", "zipshard" }, new Dictionary<string, int> { ["slime"] = 3 }, new[] { "fortress" }, out bool changed);
        check(changed && opened.SequenceEqual(new[] { "slime", MonsterBook.FormationPage("fortress") }) && book.Knows("zipshard") && book.DefeatedOf("slime") == 3,
            $"First meeting: pages for the slime and the Fortress, the shard met without one ({string.Join(",", opened)})");
        opened = book.Add(data, new[] { "slime" }, new Dictionary<string, int> { ["slime"] = 2 }, new[] { "fortress" }, out changed);
        check(changed && opened.Count == 0 && book.DefeatedOf("slime") == 5, "Met again: no new page, the count goes on");
        book.Add(data, Array.Empty<string>(), new Dictionary<string, int>(), Array.Empty<string>(), out changed);
        check(!changed, "An empty battle changes nothing (no save)");
        book.Add(data, Array.Empty<string>(), new Dictionary<string, int>(), new[] { "no_such_formation" }, out changed);
        check(!changed && !book.KnowsFormation("no_such_formation"), "A formation the data does not have is not kept");

        // The seed: the stages already won count as met, once, and as seen.
        var progress = new AdventureProgress();
        var chapter1 = data.Campaign!.Chapters[0];
        progress.Completed.AddRange(chapter1.Stages.Select(s => s.Id));
        var seeded = new MonsterBook();
        check(seeded.Seed(data.Campaign, data, progress) && new[] { "slime", "runner", "rock", "mender", "king" }.All(seeded.Knows)
              && !seeded.Knows("spitter") && seeded.Met.All(seeded.Seen.Contains) && seeded.Defeated.Count == 0,
            $"Seeded from chapter 1: its enemies met and seen, none defeated ({string.Join(",", seeded.Met)})");
        check(!seeded.Seed(data.Campaign, data, new AdventureProgress { Completed = chapter1.Stages.Select(s => s.Id).Concat(data.Campaign.Chapters[1].Stages.Select(s => s.Id)).ToList() })
              && !seeded.Knows("spitter"), "The seed runs once");
        var fresh = new MonsterBook();
        check(fresh.Seed(data.Campaign, data, new AdventureProgress()) && fresh.Met.Count == 0 && fresh.Seeded, "Nothing won: nothing seeded");

        // The file: saved and read back; a newer version is read but never overwritten; a broken one starts over.
        string dir = Path.Combine(Path.GetTempPath(), "pet-book-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "monster-book.json");
            check(book.Save(path), "The book saves");
            var back = MonsterBook.Load(path);
            check(back.Knows("slime") && back.DefeatedOf("slime") == 5 && back.KnowsFormation("fortress") && !back.ReadOnly, "...and reads back");
            File.WriteAllText(path, JsonSerializer.Serialize(new { Version = 99, Met = new[] { "slime" } }));
            var newer = MonsterBook.Load(path);
            check(newer.ReadOnly && newer.Knows("slime") && !newer.Save(path) && File.ReadAllText(path).Contains("99"), "A newer file is read and never overwritten");
            File.WriteAllText(path, "{ broken");
            File.Delete(path + ".bak");
            var broken = MonsterBook.Load(path);
            check(broken.Met.Count == 0 && !broken.ReadOnly, "A broken file starts over");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
