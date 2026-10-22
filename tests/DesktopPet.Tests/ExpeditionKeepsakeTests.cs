using DesktopPet;

// Expedition milestone 4 (docs/EXPEDITION-DIRECTION.md): keepsakes. Given once each, by a won run
// at their difficulty, in the same save that counts the run; none for a lost or given-up run or the
// user's own battle data; the saved file (version 3). Temporary files only.
internal static class ExpeditionKeepsakeTests
{
    // Plays a whole run as won: a won elite's relics are left, the first battle node of each step is
    // fought (rests and events are passed by).
    private static ExpeditionStepOutcome WinRun(Expedition x, BattleDifficulty level, int seed)
    {
        var run = x.Start(BattleWeapon.Sword, level, seed, $"keep-{level}-{seed}");
        ExpeditionStepOutcome last = default;
        while (x.Run != null)
        {
            if (run.Pending != null) { x.Choose(Expedition.Leave); continue; }
            int lane = Enumerable.Range(0, run.Map[run.Step].Length).First(l => x.Node(l) != null);
            x.Enter(lane);
            last = x.Finish(true, 60, new Dictionary<string, int>(), 30, 3);
        }
        return last;
    }

    public static void Run(Action<bool, string> check)
    {
        var data = ExpeditionFixture.Fixed;
        var spec = data.Expedition!;
        var keepsakes = spec.Keepsakes;
        check(keepsakes.Length == 5 && keepsakes.Count(k => k.Kind == KeepsakeSpec.Cup) == 3 && keepsakes.Where(k => k.Kind == KeepsakeSpec.Cup).All(k => k.Exact)
              && keepsakes.Single(k => k.Kind == KeepsakeSpec.Cushion).Difficulty == "Normal",
            "battle.json: three cups (one per difficulty), a slime cushion from Normal, a scarf");
        var scarf = keepsakes.Single(k => k.Kind == KeepsakeSpec.AccessoryKind);
        check(PetAccessories.Find(scarf.Accessory) is { Reward: "slime_trail", Layer: AccessoryLayer.Neck } && PetPreferences.Accessories.Contains(scarf.Accessory!),
            "The Expedition scarf is a neck accessory earned by the Expedition");
        check(ExpeditionProgress.CurrentVersion == 4, "Milestone 4 raised expedition.json to version 3, the step gacha to 4");

        var x = new Expedition(data, new ExpeditionProgress());
        var normal = WinRun(x, BattleDifficulty.Normal, 1);
        check(normal.RunWon && normal.Keepsakes!.OrderBy(k => k).SequenceEqual(new[] { "cup_silver", "expedition_scarf", "slime_cushion" }) && x.Progress.Keepsakes.Count == 3,
            $"A won Normal run gives the silver cup, the scarf and the cushion ({string.Join(", ", normal.Keepsakes ?? Array.Empty<string>())})");
        var again = WinRun(x, BattleDifficulty.Normal, 2);
        check(again.RunWon && again.Keepsakes!.Count == 0 && x.Progress.Keepsakes.Count == 3, "Winning at Normal again gives nothing new");
        var hard = WinRun(x, BattleDifficulty.Hard, 3);
        check(hard.Keepsakes!.SequenceEqual(new[] { "cup_gold" }), "A won Hard run gives the gold cup (the cushion and scarf were given already)");
        var easy = WinRun(x, BattleDifficulty.Easy, 4);
        check(easy.Keepsakes!.SequenceEqual(new[] { "cup_bronze" }) && x.Progress.Keepsakes.Count == 5, "A won Easy run gives the bronze cup only (the cups are per difficulty)");
        check(x.Finish(true, 60, new Dictionary<string, int>(), 1, 1) == default && x.Progress.Keepsakes.Count == 5, "Nothing is given twice");

        var first = new Expedition(data, new ExpeditionProgress());
        var easyFirst = WinRun(first, BattleDifficulty.Easy, 5);
        check(easyFirst.Keepsakes!.OrderBy(k => k).SequenceEqual(new[] { "cup_bronze", "expedition_scarf" }), "A first win on Easy gives the bronze cup and the scarf, not the cushion");

        var losing = new Expedition(data, new ExpeditionProgress());
        losing.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 6, "lost-run");
        losing.Enter(0);
        var lost = losing.Finish(false, 0, new Dictionary<string, int>(), 10, 0);
        losing.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 7, "given-up");
        var gaveUp = losing.Abandon();
        check(lost.RunOver && (lost.Keepsakes?.Count ?? 0) == 0 && (gaveUp.Keepsakes?.Count ?? 0) == 0 && losing.Progress.Keepsakes.Count == 0, "A lost or given-up run gives no keepsake");
        var custom = new Expedition(data, new ExpeditionProgress()) { GrantKeepsakes = false };
        check(WinRun(custom, BattleDifficulty.Normal, 8) is { RunWon: true } won && won.Keepsakes!.Count == 0 && custom.Progress.Keepsakes.Count == 0,
            "With the user's own battle data a won run gives no keepsake");

        // The file.
        string dir = Path.Combine(Path.GetTempPath(), "pet-keepsakes-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(dir, "expedition.json");
        try
        {
            check(x.Progress.Save(file) && ExpeditionProgress.Load(file).Keepsakes.Count == 5 && File.ReadAllText(file).Contains("\"Version\": 4"), "Keepsakes are saved and read back (version 4)");
            File.WriteAllText(file, "{ \"Version\": 2, \"Records\": {} }");
            var older = ExpeditionProgress.Load(file);
            check(!older.ReadOnly && older.Keepsakes.Count == 0 && older.Version == 4, "A milestone 3 file is read with no keepsakes");
            File.WriteAllText(file, "{ \"Version\": 2, \"Keepsakes\": [ \"cup_gold\", \"cup_gold\", \"\" ] }");
            check(ExpeditionProgress.Load(file).Keepsakes.SequenceEqual(new[] { "cup_gold" }), "Repeated or empty keepsake ids are cleaned when read");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        // Validation.
        string Error(string keepsake)
        {
            string enc(string id, string enemy = "slime") => $"{{ \"id\": \"{id}\", \"nameVi\": \"E\", \"nameEn\": \"E\", \"goalVi\": \"g\", \"goalEn\": \"g\", \"waves\": [ {{ \"groups\": [ {{ \"enemy\": \"{enemy}\", \"count\": 1 }} ] }} ] }}";
            string json = $"{{ \"schemaVersion\": 1, \"expedition\": {{ \"id\": \"trail\", \"nameVi\": \"T\", \"nameEn\": \"T\", \"keepsakes\": [ {keepsake} ], \"steps\": [ [ \"fight\" ] ], \"fights\": [ {enc("f1")} ], \"elites\": [], \"boss\": {enc("b", "king")} }} }}";
            try { BattleData.Parse(json); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        const string cup = "{ \"id\": \"c1\", \"kind\": \"cup\", \"tier\": \"gold\", \"difficulty\": \"Hard\", \"nameVi\": \"C\", \"nameEn\": \"C\" }";
        check(Error(cup) == "", "A valid keepsake is accepted");
        check(Error(cup.Replace("\"cup\"", "\"statue\"")).Contains("kind"), "An unknown keepsake kind is rejected");
        check(Error(cup.Replace("gold", "platinum")).Contains("tier"), "An unknown cup tier is rejected");
        check(Error(cup.Replace("Hard", "Insane")).Contains("difficulty"), "An unknown difficulty is rejected");
        check(Error(cup + ", " + cup).Contains("used twice"), "A keepsake id used twice is rejected");
        check(Error("{ \"id\": \"s\", \"kind\": \"accessory\", \"accessory\": \"Bell\", \"nameVi\": \"S\", \"nameEn\": \"S\" }").Contains("accessory"), "An accessory keepsake must be a reward accessory");
    }
}
