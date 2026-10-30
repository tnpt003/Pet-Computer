using System.Text;
using System.Text.Json;
using DesktopPet;

// 1.5 M28 (docs/UPDATE-1.5.md L2, L3): battle achievements and their coats and accessories: the data
// file, what a battle gives, the book and its file. Simulation and data only, no WPF.
internal static class AchievementTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        var all = PetAchievements.All;

        // The built-in list: 20 achievements; the six reward items each given by exactly one of them.
        check(all.Count == 20 && all.Select(a => a.Id).Distinct().Count() == 20, $"20 built-in achievements ({all.Count})");
        var coats = PetCoats.All.Where(c => c.Reward == PetAchievements.RewardTag).Select(c => c.Id).ToList();
        var items = PetAccessories.All.Where(a => a.Reward == PetAchievements.RewardTag).Select(a => a.Id).ToList();
        check(coats.SequenceEqual(new[] { "Mint", "Midnight", "Golden" }) && items.SequenceEqual(new[] { "Bandana", "HeroMedal", "FishCharm" }),
            $"Three achievement coats and three achievement accessories ({string.Join(",", coats.Concat(items))})");
        check(coats.Concat(items).All(id => all.Count(a => a.Reward == id) == 1) && all.Count(a => a.Reward != null) == 6,
            "Each of them is given by one achievement; the other achievements give the badge only");
        check(PetCoats.All.Where(c => c.Reward == null).Select(c => c.Id).SequenceEqual(new[] { "Orange", "Gray", "Black", "White", "Calico", "Tabby", "GingerTabby", "Tuxedo", "RussianBlue", "Siamese" }),
            "The ten original coats stay free");
        check(all.All(a => AchievementStats.Names.Contains(a.Stat) && a.NameEn.Length > 0 && a.NameVi.Length > 0 && a.DescEn.Length > 0 && a.DescVi.Length > 0),
            "Every achievement has a known stat and both languages");

        // A bad file is refused as a whole.
        AchievementSpec[]? Parse(string json)
        {
            try { return PetAchievements.Parse(new MemoryStream(Encoding.UTF8.GetBytes(json))); }
            catch (Exception e) when (e is InvalidDataException or JsonException) { return null; }
        }
        string One(string extra) => "{\"achievements\":[{\"id\":\"x\",\"name\":{\"vi\":\"X\",\"en\":\"X\"},\"desc\":{\"vi\":\"D\",\"en\":\"D\"}," + extra + "}]}";
        check(Parse(One("\"stat\":\"wins\",\"goal\":3,\"scope\":\"total\"")) is [{ Id: "x", Goal: 3, Total: true, Reward: null }], "A good achievement parses");
        check(Parse(One("\"stat\":\"wins\",\"goal\":1,\"scope\":\"battle\",\"reward\":\"Mint\"")) is [{ Reward: "Mint", Total: false }], "An achievement coat as its reward");
        check(Parse(One("\"stat\":\"teleports\",\"goal\":1,\"scope\":\"total\"")) == null, "An unknown stat is refused");
        check(Parse(One("\"stat\":\"wins\",\"goal\":0,\"scope\":\"total\"")) == null, "A goal of 0 is refused");
        check(Parse(One("\"stat\":\"wins\",\"goal\":1,\"scope\":\"ever\"")) == null, "An unknown scope is refused");
        check(Parse(One("\"stat\":\"wins\",\"goal\":1,\"scope\":\"total\",\"reward\":\"Bell\"")) == null
              && Parse(One("\"stat\":\"wins\",\"goal\":1,\"scope\":\"total\",\"reward\":\"SlimeHat\"")) == null
              && Parse(One("\"stat\":\"wins\",\"goal\":1,\"scope\":\"total\",\"reward\":\"Gray\"")) == null,
            "A reward must be an achievement coat or accessory (not a free one or another mode's reward)");
        check(Parse("{\"achievements\":[{\"id\":\"x\",\"name\":{\"vi\":\"X\",\"en\":\"X\"},\"stat\":\"wins\",\"goal\":1,\"scope\":\"total\"}]}") == null, "A description in both languages is needed");
        string twice = "{\"id\":\"%\",\"name\":{\"vi\":\"X\",\"en\":\"X\"},\"desc\":{\"vi\":\"D\",\"en\":\"D\"},\"stat\":\"wins\",\"goal\":1,\"scope\":\"total\",\"reward\":\"Mint\"}";
        check(Parse("{\"achievements\":[" + twice.Replace("%", "a") + "," + twice.Replace("%", "b") + "]}") == null, "One reward is given by one achievement only");
        check(Parse("{\"achievements\":[" + twice.Replace("%", "a") + "," + twice.Replace("%", "a").Replace(",\"reward\":\"Mint\"", "") + "]}") == null, "Ids are unique");
        check(Parse("{ broken") == null && Parse("{}") == null, "Broken JSON or no list is refused");
        bool firstCoatRefused = false;
        try { PetCoats.Parse(new MemoryStream(Encoding.UTF8.GetBytes("{\"coats\":[{\"id\":\"A\",\"fur\":\"#AAAAAA\",\"cream\":\"#FFFFFF\",\"pink\":\"#EE9999\",\"reward\":\"achievement\"}]}"))); }
        catch (InvalidDataException) { firstCoatRefused = true; }
        check(firstCoatRefused, "The default (first) coat cannot be a reward");

        // What a battle gives: a clean win counts a win, its slimes and Not a scratch.
        var easy = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 2 }, Boss = false, SlimeMaxHp = 1, SlimeDamage = 0, CatMaxHp = 1000 };
        var clean = new BattleSim(easy, 0, 1600, seed: 3);
        for (double t = 0; t < 60 && !clean.Over; t += dt) clean.Step(dt, 800, false);
        var stats = AchievementStats.From(clean);
        check(clean.Phase == BattlePhase.Won && clean.HurtTaken == 0 && stats["wins"] == 1 && stats["flawless"] == 1 && stats["defeated"] == 2
              && stats["clutch"] == 0 && stats["hardWin"] == 0 && stats["endlessWaves"] == 0 && stats["fullBasket"] == 0 && stats.Keys.OrderBy(k => k).SequenceEqual(AchievementStats.Names.OrderBy(k => k)),
            $"A clean win: a win, two slimes, flawless ({string.Join(", ", stats.Where(p => p.Value > 0).Select(p => p.Key + "=" + p.Value))})");
        check(stats["quickWin"] == (clean.FightSeconds <= AchievementStats.QuickWinSeconds ? 1 : 0), "A quick win is one within two minutes of fighting");

        // A hit on the cat counts as HP lost (heals do not take it back); a lost battle gives no win.
        var hard = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, Boss = false, SlimeMaxHp = 100000, SlimeDamage = 40, CatMaxHp = 100 };
        var hurt = new BattleSim(hard, 0, 1600, seed: 5);
        for (double t = 0; t < 120 && !hurt.Over; t += dt) hurt.Step(dt, 800, false);
        var lost = AchievementStats.From(hurt);
        check(hurt.Phase == BattlePhase.Lost && hurt.HurtTaken >= hurt.CatMaxHp && lost["wins"] == 0 && lost["flawless"] == 0 && lost["clutch"] == 0,
            $"A lost battle: HP lost counted ({hurt.HurtTaken:0}), no win, no flawless");

        // The book: totals add up, a battle's best counts for battle achievements, each is earned once.
        var specs = new[]
        {
            new AchievementSpec("ten", "", "", "", "", "defeated", 10, true),
            new AchievementSpec("combo", "", "", "", "", "combos", 5, false, "Mint"),
        };
        var book = new AchievementBook();
        var first = book.Add(specs, new Dictionary<string, double> { ["defeated"] = 6, ["combos"] = 3 }, out bool changed);
        check(first.Count == 0 && changed && book.Progress(specs[0]) == 6 && book.Progress(specs[1]) == 3, "Half way: progress, nothing earned");
        var second = book.Add(specs, new Dictionary<string, double> { ["defeated"] = 6, ["combos"] = 4 }, out _);
        check(second.Select(a => a.Id).SequenceEqual(new[] { "ten" }) && book.Progress(specs[0]) == 10 && book.Progress(specs[1]) == 4,
            "Totals add up over battles; a battle achievement needs it in one battle (3 then 4 is not 5)");
        var third = book.Add(specs, new Dictionary<string, double> { ["defeated"] = 1, ["combos"] = 5 }, out _);
        check(third.Select(a => a.Id).SequenceEqual(new[] { "combo" }) && book.HasReward("Mint", specs) && !book.HasReward("Golden", specs), "The best battle earns it and its coat");
        check(book.Add(specs, new Dictionary<string, double> { ["defeated"] = 50, ["combos"] = 9 }, out _).Count == 0 && book.Earned.Count == 2, "Each is earned once");
        book.Add(specs, new Dictionary<string, double> { ["defeated"] = 0 }, out bool nothing);
        check(!nothing, "A battle that did nothing changes nothing");

        // The file: saved and read back; a newer version is read but never overwritten; a broken one
        // is read from its backup, or starts over.
        string dir = Path.Combine(Path.GetTempPath(), "pet-achievements-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "achievements.json");
            check(book.Save(path) && !File.Exists(path + ".tmp"), "The book saves (no .tmp left)");
            var back = AchievementBook.Load(path);
            check(back.Has("ten") && back.Has("combo") && back.Totals["defeated"] == 63 && back.Best["combos"] == 9 && !back.ReadOnly, "...and reads back");
            back.Totals["defeated"] = 70;
            check(back.Save(path) && File.Exists(path + ".bak"), "A second save keeps the old file as .bak");
            File.WriteAllText(path, "{ broken");
            var fromBackup = AchievementBook.Load(path);
            check(fromBackup.Totals["defeated"] == 63 && fromBackup.Has("combo"), "A broken file is read from its backup");
            File.Delete(path + ".bak");
            check(AchievementBook.Load(path).Earned.Count == 0, "Broken with no backup: it starts over");
            File.WriteAllText(path, JsonSerializer.Serialize(new { Version = 99, Earned = new[] { "ten" }, Totals = new Dictionary<string, double> { ["defeated"] = -4, ["wins"] = 2 } }));
            var newer = AchievementBook.Load(path);
            check(newer.ReadOnly && newer.Has("ten") && !newer.Totals.ContainsKey("defeated") && newer.Totals["wins"] == 2 && !newer.Save(path) && File.ReadAllText(path).Contains("99"),
                "A newer file is read (bad numbers dropped) and never overwritten");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
