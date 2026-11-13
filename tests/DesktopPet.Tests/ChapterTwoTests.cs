using DesktopPet;

// 1.5 M21: chapter 2 "The Download Dump" (battle.json campaign, PetAdventure.cs). It opens once the
// chapter 1 boss is won (unlockAfter); each stage lends the skill it opens (Adventure.Lent: carried in
// that stage, taking the last slot when not chosen) until it is won; its slimes drop fish and potions
// (chapter "drops"); a stage may allow more malware kinds at once (maxMalware, chapter 2: 4). The data
// limits of the new keys. Chapter 2's balance is in AdventureBalanceTests.RunChapter2.
internal static class ChapterTwoTests
{
    private static readonly BattleSettings Shipped = BattleData.Settings;

    public static void Run(Action<bool, string> check)
    {
        ShippedChecks(check);
        GateChecks(check);
        LendChecks(check);
        MalwareChecks(check);
        DataChecks(check);
        RosterChecks(check);
    }

    // Chapter 2 review E (2026-11-14): the chapter also shows the sulky slime (stage 1) and a timer
    // (stage 4) whose calls are formations the chapter has shown by then; it shares the timer's page.
    private static void RosterChecks(Action<bool, string> check)
    {
        var two = Shipped.Campaign!.Chapters[1];
        IEnumerable<string> Kinds(StageSpec stage) => stage.Waves.SelectMany(w => w.Groups ?? Array.Empty<WaveGroup>()).Select(g => g.Enemy).OfType<string>();
        check(Kinds(two.Stages[0]).Contains("sulky") && two.Stages[0].RulesVersion == 5, "Chapter 2 stage 1 brings a sulky slime (rules 5)");
        check(Kinds(two.Stages[3]).Contains("timer_dump") && two.Stages[3].RulesVersion == 8, "Chapter 2 stage 4 brings chapter 2's timer (rules 8)");
        var dump = Shipped.Enemy("timer_dump");
        var timer = Shipped.Enemy("timer");
        check(dump is { Page: "timer", TipVi: null, Clock.Calls: 2 } && dump.Clock!.CallFormations!.Keys.OrderBy(k => k).SequenceEqual(new[] { "fortress", "trampoline" })
              && dump.MaxHp == timer.MaxHp && dump.Damage == timer.Damage && dump.Clock.Seconds == timer.Clock!.Seconds,
            "Chapter 2's timer is the timer (same page, numbers and clock) calling only the Trampoline or the Fortress, twice at most");
        check(MonsterBook.PageOf(Shipped, "timer_dump") == "timer" && CustomFights.SpawnOf(Shipped, "timer") == "timer" && CustomFights.SpawnOf(Shipped, "king") == "king_c1",
            "Its defeats count on the timer's page; a Custom Fight still spawns the timer itself (and the chapter 1 King for the King's page)");
        var (_, mouse) = BattleData.Reach(Shipped, Kinds(two.Stages[0]));
        check(mouse.Count <= BattleData.MaxMouseKinds, $"Stage 1 keeps within the cursor kinds limit ({string.Join(",", mouse)})");
    }

    private static void ShippedChecks(Action<bool, string> check)
    {
        var campaign = Shipped.Campaign!;
        var two = campaign.Chapters.Length > 1 ? campaign.Chapters[1] : null;
        check(two is { Id: "download_dump", SkillChoice: true, Drops: true, UnlockAfter: "c1_slime_king" } && two.Stages.Length == 5,
            "The built-in data has chapter 2: five stages, opened by the Slime King, with the skill choice and drops");
        check(two != null && two.Stages.Select(s => s.UnlocksSkill).SequenceEqual(new[] { "fan", "laser", "grabber", "scissors", "spray" }),
            "Chapter 2's stages open the Fan, the Laser, the Grabber, the Spray and the Scissors in that order");
        check(two != null && two.Stages[^1].Waves[^1].Groups!.Any(g => g.Enemy == "zip") && !two.Stages[3].Waves.Any(w => w.Groups?.Any(g => g.Enemy == "mother") == true),
            "The Zip Slime ends chapter 2 and stage 4 focuses on the Scissors exam");
        check(two != null && two.Stages.Count(s => s.Basket != null) == 1 && two.Stages.All(s => s.MaxMalware == 4 && s.SuggestSkills?.Length == 3),
            "Chapter 2 has one fish basket stage; every stage allows four malware kinds and suggests three skills");
        var one = campaign.Chapters[0];
        check(one.UnlockAfter == null && !one.Drops && one.Stages.All(s => s.MaxMalware == null && s.UnlocksSkill == null && s.SuggestSkills == null),
            "Chapter 1 keeps its rules (no drops, lent skills, suggestions or malware change)");
    }

    private static void GateChecks(Action<bool, string> check)
    {
        var campaign = Shipped.Campaign!;
        var adventure = new Adventure(campaign, new AdventureProgress());
        var one = campaign.Chapters[0]; var two = campaign.Chapters[1];
        check(adventure.IsUnlocked(one.Stages[0].Id) && !adventure.IsUnlocked(two.Stages[0].Id) && !adventure.ChapterOpen(two) && adventure.ChapterOpen(one),
            "A new progress: chapter 1's first stage is open, chapter 2's is locked");
        foreach (var stage in one.Stages.Take(4)) adventure.Progress.Completed.Add(stage.Id);
        check(!adventure.IsUnlocked(two.Stages[0].Id), "Chapter 2 stays locked until the Slime King is won");
        var lost = adventure.Apply("lose", one.Stages[4], true, false, BattleWeapon.Sword, BattleDifficulty.Normal, 3, 60);
        check(lost.Unlocked == null && !adventure.IsUnlocked(two.Stages[0].Id), "Losing to the Slime King opens nothing");
        var won = adventure.Apply("win", one.Stages[4], true, true, BattleWeapon.Sword, BattleDifficulty.Normal, 9, 80);
        check(won.Unlocked == two.Stages[0] && adventure.IsUnlocked(two.Stages[0].Id) && !adventure.IsUnlocked(two.Stages[1].Id) && adventure.ChapterAfter(one.Stages[4].Id) == two,
            "Beating the Slime King opens chapter 2's first stage (named as unlocked in the result); its second waits");
        var again = adventure.Apply("again", one.Stages[4], true, true, BattleWeapon.Sword, BattleDifficulty.Normal, 9, 80);
        check(again.Unlocked == null, "Beating the Slime King again opens nothing new");
        check(adventure.StartBuild(two.Stages[0]).Count == 0 && adventure.Drops(two.Stages[0]) && !adventure.Drops(one.Stages[0]),
            "Chapter 2 starts without chapter 1's build; its stages drop items, chapter 1's do not");
        // A won stage stays open even if the opener were somehow lost from the progress.
        adventure.Progress.Completed.Add(two.Stages[0].Id);
        adventure.Progress.Completed.Remove(one.Stages[4].Id);
        check(adventure.IsUnlocked(two.Stages[0].Id) && adventure.IsUnlocked(two.Stages[1].Id), "A won chapter 2 stage stays open");
    }

    private static void LendChecks(Action<bool, string> check)
    {
        var campaign = Shipped.Campaign!;
        var adventure = new Adventure(campaign, new AdventureProgress());
        var two = campaign.Chapters[1];
        check(adventure.Lent(campaign.Chapters[0].Stages[1]) == null, "A chapter 1 stage lends nothing");
        check(adventure.Lent(two.Stages[0]) == BattleSkill.Fan && !adventure.SkillOpen(BattleSkill.Fan), "Chapter 2 stage 1 lends the Fan (still not open for other battles)");
        var lent = Adventure.WithLent(BattleSkills.Default, BattleSkill.Fan);
        check(lent.SequenceEqual(new[] { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Bomb, BattleSkill.Fan }),
            "A lent skill takes the last slot of a loadout without it (the Shield of the first five)");
        var chosen = new[] { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Bomb, BattleSkill.Shield, BattleSkill.Fan };
        check(Adventure.WithLent(chosen, BattleSkill.Fan).SequenceEqual(chosen) && Adventure.WithLent(chosen, null).SequenceEqual(chosen),
            "A loadout that already has the lent skill (or no lent skill) stays as chosen");
        // Feedback 13/11: a chapter 2 stage's suggested skills are required, every time.
        check(adventure.Required(campaign.Chapters[0].Stages[1]).Count == 0, "A chapter 1 stage requires nothing");
        var required = adventure.Required(two.Stages[0]);
        check(required.ToHashSet().SetEquals(new[] { BattleSkill.Fan, BattleSkill.Push, BattleSkill.Bomb }), "Chapter 2 stage 1 requires its three suggested skills");
        var withRequired = Adventure.WithRequired(BattleSkills.Default, required);
        check(withRequired.SequenceEqual(new[] { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Bomb, BattleSkill.Fan }),
            "The required ones take their slots; the user's other skills fill the rest in their order");
        var other = new[] { BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Shield, BattleSkill.Laser, BattleSkill.Grabber };
        var forced = Adventure.WithRequired(other, required);
        check(forced.Count == BattleSkills.Carry && required.All(forced.Contains) && forced.Contains(BattleSkill.Heal) && forced.Contains(BattleSkill.Cheer),
            "With none of them chosen, three of the user's five make room (the first two stay)");
        adventure.Progress.Completed.Add(two.Stages[0].Id);
        check(adventure.Required(two.Stages[0]).ToHashSet().SetEquals(required), "Once won, they are still required when the stage is played again");
        check(adventure.Lent(two.Stages[0]) == null && adventure.SkillOpen(BattleSkill.Fan) && adventure.Lent(two.Stages[1]) == BattleSkill.Laser,
            "Once stage 1 is won the Fan is open for good and lent no more; stage 2 lends the Laser");
        // The lent skill really works in the stage's battle.
        var sim = new BattleSim(Shipped, 0, 1600, seed: 4, stage: two.Stages[1], skills: Adventure.WithLent(BattleSkills.Default, adventure.Lent(two.Stages[1])), loot: BattleLoot.Items);
        check(sim.Carries(BattleSkill.Laser) && !sim.Carries(BattleSkill.Shield) && sim.Loot == BattleLoot.Items, "The stage's battle carries the lent Laser and drops items");
    }

    private static void MalwareChecks(Action<bool, string> check)
    {
        // Malware every 3 s, every kind as likely, a cat that cannot lose: how many kinds are out at once.
        int MostOut(int? cap)
        {
            var plain = new EnemyType { Id = "slime", MaxHp = 100000, Speed = 0, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3 };
            var apps = Shipped.Apps! with { MalwareMinThreat = 0, MalwareEveryMin = 3, MalwareEveryMax = 3, AdWeight = 1, LoadingWeight = 1, UpdateWeight = 1, VirusExeWeight = 1, InfectChance = 0 };
            var settings = new BattleSettings { Weapons = new[] { Shipped.Weapon("sword") with { Damage = 0 } }, FirstWaveDelay = 0, CatMaxHp = 100000, MaxAlive = 4, Enemies = new[] { plain }, Apps = apps, RageHitGain = 0 };
            var stage = new StageSpec { Id = "m", NameVi = "M", NameEn = "M", MaxMalware = cap, Objective = "survive", SurviveSeconds = 120, Threat = 2,
                Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 2 } } } } };
            var sim = new BattleSim(settings, 0, 1600, seed: 9, stage: stage);
            int most = 0;
            for (int i = 0; i < 60 * 90 && !sim.Over; i++)
            {
                sim.Step(1.0 / 60, 1400, false);
                sim.ClearEvents();
                int kinds = (sim.Popups.Count > 0 ? 1 : 0) + (sim.Hazards.Any(h => h.Kind == HazardKind.Loading) ? 1 : 0)
                    + (sim.Hazards.Any(h => h.Kind == HazardKind.Update) ? 1 : 0) + (sim.VirusFiles.Count > 0 ? 1 : 0);
                most = Math.Max(most, kinds);
            }
            return most;
        }
        int two = MostOut(null), four = MostOut(4);
        check(two <= 2 && four > 2, $"A stage's maxMalware lets more malware kinds out at once (data's 2: at most {two}; the stage's 4: up to {four})");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        string Parse(string chapters)
        {
            try { BattleData.Parse("{ \"schemaVersion\": 1, \"campaign\": { \"chapters\": [ " + chapters + " ] } }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        string Chapter(string id, string extra, string stage, string stageExtra = "") =>
            $"{{ \"id\": \"{id}\", \"nameVi\": \"C\", \"nameEn\": \"C\", {extra} \"stages\": [ {{ \"id\": \"{stage}\", \"nameVi\": \"S\", \"nameEn\": \"S\", {stageExtra} \"waves\": [ {{ \"groups\": [ {{ \"enemy\": \"slime\", \"count\": 1 }} ] }} ] }} ] }}";
        check(Parse(Chapter("a", "", "s1") + ", " + Chapter("b", "\"unlockAfter\": \"s1\", \"drops\": true,", "s2")) == "",
            "A chapter may open after a stage of an earlier chapter and drop items");
        check(Parse(Chapter("a", "\"unlockAfter\": \"s2\",", "s1") + ", " + Chapter("b", "", "s2")).Contains("unlockAfter")
            && Parse(Chapter("a", "\"unlockAfter\": \"nope\",", "s1")).Contains("unlockAfter"),
            "unlockAfter must name a stage of an earlier chapter (not a later or unknown one)");
        check(Parse(Chapter("a", "", "s1", "\"maxMalware\": 4,")) == "" && Parse(Chapter("a", "", "s1", "\"maxMalware\": 5,")).Contains("maxMalware")
            && Parse(Chapter("a", "", "s1", "\"maxMalware\": 3, \"malware\": false,")).Contains("maxMalware"),
            "maxMalware is 1–4, in a stage with malware");
        check(Parse(Chapter("a", "", "s1", "\"suggestSkills\": [ \"fan\", \"push\" ],")) == "" && Parse(Chapter("a", "", "s1", "\"suggestSkills\": [ \"nope\" ],")).Contains("suggestSkills")
            && Parse(Chapter("a", "", "s1", "\"suggestSkills\": [ \"fan\", \"fan\" ],")).Contains("suggestSkills") && Parse(Chapter("a", "", "s1", "\"suggestSkills\": [],")).Contains("suggestSkills"),
            "suggestSkills lists 1–5 different skill ids");
        check(Parse(Chapter("a", "", "s1", "\"newAppsFrom\":1,\"lesson\":\"battery\",\"malwareKinds\":[\"battery\"],")) == ""
            && Parse(Chapter("a", "", "s1", "\"lesson\":\"battery\"," )).Contains("lesson")
            && Parse(Chapter("a", "", "s1", "\"malwareKinds\":[\"battery\",\"battery\"],")).Contains("malwareKinds")
            && Parse(Chapter("a", "", "s1", "\"malwareKinds\":[\"unknown\"],")).Contains("malwareKinds"),
            "Lessons require new apps and malware pools reject duplicates or unknown kinds");
    }
}
