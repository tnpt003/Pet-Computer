using DesktopPet;

// 1.5 M22: chapter 2's look and rewards (docs/UPDATE-1.5.md C2, C3). The chapter's scene ("dump": the
// map's banner and landmarks, the backdrop behind its battles; chapter 1 has none), the Folder hat
// (stage 3, a reward accessory like the Slime hat) and the Zip Slime plush (stage 5, a stage keepsake:
// given once with the stage's reward, saved with the rewards, shown on the display corner). The data
// limits of the new keys.
internal static class ChapterRewardTests
{
    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var campaign = data.Campaign!;
        var one = campaign.Chapters[0]; var two = campaign.Chapters[1];
        check(two.Scene == ChapterSpec.Dump && one.Scene == null, "Chapter 2 has the Download Dump's scene; chapter 1 keeps the meadow (no scene)");
        check(two.Stages[2].Reward == "FolderHat" && PetAccessories.Find("FolderHat") is { Reward: "download_dump", Layer: AccessoryLayer.Head },
            "Chapter 2's stage 3 gives the Folder hat (a head accessory earned by the chapter)");
        var plush = two.Stages[4].Keepsake;
        check(plush is { Id: "zip_plush", Kind: KeepsakeSpec.Plush, Shown: true } && two.Stages.Take(4).All(s => s.Keepsake == null) && one.Stages.All(s => s.Keepsake == null),
            "The Zip Slime stage gives the Zip Slime plush, a keepsake shown on the display corner (the only stage keepsake)");
        check(plush != null && plush.Name("vi") == "Thú bông Slime Nén" && plush.Name("en") == "Zip Slime plush", "The plush has its names");
        check(one.Stages.Select(s => s.Reward).Where(r => r != null).SequenceEqual(new[] { "SlimeHat", "SlimeKingCrown" }), "Chapter 1's rewards are unchanged");

        // Given once, with the stage's win; a stage won before the rewards existed gives them on the next look.
        var progress = new AdventureProgress();
        var adventure = new Adventure(campaign, progress);
        progress.Completed.Add(one.Stages[4].Id);
        adventure.GrantRewards(); // The Slime King's crown, won before.
        var lost = adventure.Apply("l", two.Stages[4], true, false, BattleWeapon.Sword, BattleDifficulty.Normal, 3, 60);
        check((lost.Rewards ?? Array.Empty<string>()).Count == 0 && !adventure.HasReward("zip_plush"), "Losing to the Zip Slime gives no plush");
        var won = adventure.Apply("w", two.Stages[4], true, true, BattleWeapon.Sword, BattleDifficulty.Normal, 9, 200);
        check(won.Rewards is ["zip_plush"] && adventure.HasReward("zip_plush") && adventure.Keepsake("zip_plush") == plush,
            "Beating the Zip Slime gives the plush once (saved with the rewards)");
        var again = adventure.Apply("w2", two.Stages[4], true, true, BattleWeapon.Sword, BattleDifficulty.Normal, 9, 200);
        check((again.Rewards ?? Array.Empty<string>()).Count == 0 && progress.Rewards.Count(r => r == "zip_plush") == 1, "Beating it again gives nothing more");
        var older = new AdventureProgress { Completed = { two.Stages[2].Id, two.Stages[4].Id } };
        var late = new Adventure(campaign, older);
        check(late.GrantRewards().OrderBy(r => r, StringComparer.Ordinal).SequenceEqual(new[] { "FolderHat", "zip_plush" }) && late.GrantRewards().Count == 0,
            "Stages won before the rewards existed give the hat and the plush once");
        check(adventure.SceneOf(two.Stages[0]) == ChapterSpec.Dump && adventure.SceneOf(one.Stages[0]) == null, "A stage's scene is its chapter's");

        // The saved file keeps the plush with the rewards.
        string dir = Path.Combine(Path.GetTempPath(), "pet-m22-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "adventure-progress.json");
            check(progress.Save(file) && AdventureProgress.Load(file).Rewards.Contains("zip_plush"), "The plush is saved with the Adventure progress");
        }
        finally { Directory.Delete(dir, true); }

        // Validation of the new keys.
        string Parse(string chapter)
        {
            try { BattleData.Parse("{ \"schemaVersion\": 1, \"campaign\": { \"chapters\": [ " + chapter + " ] } }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        string Stage(string id, string extra) =>
            $"{{ \"id\": \"{id}\", \"nameVi\": \"S\", \"nameEn\": \"S\", {extra} \"waves\": [ {{ \"groups\": [ {{ \"enemy\": \"slime\", \"count\": 1 }} ] }} ] }}";
        string Chapter(string extra, params string[] stages) => $"{{ \"id\": \"a\", \"nameVi\": \"C\", \"nameEn\": \"C\", {extra} \"stages\": [ {string.Join(", ", stages)} ] }}";
        const string Plush = "\"keepsake\": { \"id\": \"p\", \"kind\": \"plush\", \"nameVi\": \"P\", \"nameEn\": \"P\" },";
        check(Parse(Chapter("\"scene\": \"dump\",", Stage("s1", Plush))) == "", "A chapter may have the dump scene and a stage a plush keepsake");
        check(Parse(Chapter("\"scene\": \"beach\",", Stage("s1", ""))).Contains("scene"), "An unknown scene is rejected");
        check(Parse(Chapter("", Stage("s1", Plush.Replace("plush", "accessory")))).Contains("keepsake.kind"), "A stage keepsake must be one the display corner shows");
        check(Parse(Chapter("", Stage("s1", Plush.Replace("\"nameEn\": \"P\" ", "\"nameEn\": \"" + new string('P', 33) + "\" ")))).Contains("keepsake.nameEn"), "A stage keepsake's name is at most 32 characters");
        check(Parse(Chapter("", Stage("s1", Plush), Stage("s2", Plush))).Contains("used twice") && Parse(Chapter("", Stage("s1", Plush.Replace("\"p\"", "\"SlimeHat\"")))).Contains("keepsake.id"),
            "A keepsake id is used once and is a data id (never an accessory's, since both are saved as rewards)");
        check(Parse(Chapter("", Stage("s1", Plush.Replace("\"kind\": \"plush\"", "\"kind\": \"cup\"")))).Contains("keepsake.tier"), "A cup keepsake needs its tier");
    }
}
