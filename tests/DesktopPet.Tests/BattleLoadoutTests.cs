using DesktopPet;

// 1.5 M02: the five skills a battle carries, out of ten. Without a loadout a battle has the five
// first skills (chapter 1, old saves, the balance runs); a loadout is read in HUD order and anything
// that does not fit falls back to the default; a skill not carried is never ready; a card or relic
// about a skill is only offered when that skill is carried; the five 1.5 skills have no effect yet
// and are only opened by a won stage that names them; an Expedition run keeps its loadout.
internal static class BattleLoadoutTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        var old = new[] { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Bomb, BattleSkill.Shield };

        // The ten skills and their data.
        check(BattleSkills.All.Length == 10 && BattleSkills.Carry == 5 && BattleSkills.Default.SequenceEqual(old),
            "Ten skills, five carried; the default is the five first ones");
        check(BattleSkills.All.Count(info => info.Working) == 5 && BattleSkills.All.Where(info => info.Working).Select(info => info.Skill).SequenceEqual(old),
            "Only the five first skills work so far (the 1.5 ones come with M05/M06)");
        var data = BattleData.Settings;
        check(BattleSkills.All.All(info => data.Skill(info.Skill).Name("en").Length > 0 && data.Skill(info.Skill).Name("vi").Length > 0
                && data.Skill(info.Skill).Name("ja") != data.Skill(info.Skill).Name("en")),
            "Every skill has a name in vi, en and ja (shipped battle.json)");
        check(BattleSkills.BuiltIn.Length == 10 && BattleSkills.All.All(info => BattleSkills.BuiltIn.Any(k => k.Id == BattleSkills.Id(info.Skill))),
            "The built-in texts cover every skill");
        string oldFive = string.Join(",", BattleSkills.BuiltIn.Take(5).Select(k => System.Text.Json.JsonSerializer.Serialize(k)));
        var oldFile = BattleData.ParseWithOverride(ExpeditionFixture.Embedded(), "{ \"schemaVersion\": 1, \"skills\": [" + oldFive + "] }");
        check(oldFile.SkillList.Count == 10 && oldFile.Skill(BattleSkill.Fan).NameEn == "Fan",
            "A user battle.json from before 1.5 (five skills) still loads; the new skills keep their built-in texts");

        // Reading a loadout.
        check(BattleSkills.Loadout(null).SequenceEqual(old) && BattleSkills.Save(old) == null, "No saved choice is the default, and the default saves as nothing");
        check(BattleSkills.Loadout(new[] { "shield", "PUSH", "bomb", "heal", "cheer" }).SequenceEqual(old), "Saved ids are read in HUD order, case ignored");
        var withLaser = BattleSkills.Loadout(new[] { "push", "heal", "cheer", "laser", "shield" });
        check(withLaser.SequenceEqual(new[] { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Shield, BattleSkill.Laser }),
            "A loadout with a new skill keeps it after the old ones (HUD order)");
        check(BattleSkills.Save(withLaser)!.SequenceEqual(new[] { "push", "heal", "cheer", "shield", "laser" }), "A loadout saves as its ids");
        foreach (var (ids, why) in new (string[], string)[]
        {
            (new[] { "push", "heal", "cheer", "bomb" }, "four skills"),
            (new[] { "push", "heal", "cheer", "bomb", "shield", "fan" }, "six skills"),
            (new[] { "push", "heal", "cheer", "bomb", "bomb" }, "a skill twice"),
            (new[] { "push", "heal", "cheer", "bomb", "teleport" }, "an unknown id"),
            (new[] { "push", "heal", "cheer", "bomb", "7" }, "a number"),
        })
            check(BattleSkills.Loadout(ids).SequenceEqual(old), $"A saved choice with {why} reads as the default");

        // A battle's loadout.
        var plain = new BattleSim(new BattleSettings(), 0, 1600, seed: 1);
        check(plain.Skills.SequenceEqual(old) && old.All(plain.Carries), "A battle without a loadout carries the five first skills");
        check(new BattleSim(new BattleSettings(), 0, 1600, seed: 1, skills: new[] { BattleSkill.Push }).Skills.SequenceEqual(old),
            "A loadout that is not five skills gives the default");
        var noBomb = new BattleSim(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000 }, 0, 1600, seed: 1,
            skills: new[] { BattleSkill.Shield, BattleSkill.Push, BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Laser });
        check(noBomb.Skills.SequenceEqual(new[] { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Shield, BattleSkill.Laser }),
            "A battle's skills are in HUD order");
        for (int i = 0; i < 30; i++) noBomb.Step(dt, 800, false);
        int slime = noBomb.Slimes[0].Id;
        check(!noBomb.Carries(BattleSkill.Bomb) && !noBomb.Ready(BattleSkill.Bomb) && !noBomb.Arm(BattleSkill.Bomb) && !noBomb.Use(BattleSkill.Bomb, slime, 800)
              && noBomb.Bombs.Count == 0, "A skill left at home is never ready, armed or used");
        check(noBomb.Carries(BattleSkill.Laser) && !noBomb.Ready(BattleSkill.Laser) && !noBomb.Use(BattleSkill.Laser, 800) && noBomb.Cooldown(BattleSkill.Laser) == 0,
            "A new skill that has no effect yet is never ready, even when carried");
        check(noBomb.Ready(BattleSkill.Push) && noBomb.Ready(BattleSkill.Shield) && noBomb.Use(BattleSkill.Shield, 800) && noBomb.ShieldRemaining > 0,
            "The carried old skills work as before");

        // Cards and relics about a skill.
        check(data.UpgradeList.Single(u => u.Id == "big_bomb").Skill == "bomb" && data.UpgradeList.Single(u => u.Id == "thick_bubble").Skill == "shield"
              && data.Expedition!.Relic("counter_bell")?.Skill == "shield" && data.Expedition.Relic("relief_pack")?.Skill == "heal",
            "Big bomb, Thick bubble, Counter bell and Relief pack name their skill");
        var cards = new[]
        {
            new UpgradeSpec { Id = "bombish", Effect = "bomb", Amount = 0.2, MaxStacks = 9, Skill = "bomb", Group = "power" },
            new UpgradeSpec { Id = "plain", Effect = "damage", Amount = 0.1, MaxStacks = 9, Group = "power" },
        };
        bool Offered(IReadOnlyList<BattleSkill>? skills, string id)
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = new BattleSim(new BattleSettings { Upgrades = cards }, 0, 1600, seed: seed, skills: skills);
                if (sim.OfferCards()?.Any(u => u.Id == id) == true) return true;
            }
            return false;
        }
        check(Offered(null, "bombish") && Offered(null, "plain"), "With the Yarn bomb carried its card is offered");
        check(!Offered(noBomb.Skills, "bombish") && Offered(noBomb.Skills, "plain"), "Without the Yarn bomb its card is never offered; other cards are");

        // Opening a skill: a won stage that names it (and only one that works).
        var campaign = new CampaignSpec
        {
            Chapters = new[]
            {
                new ChapterSpec { Id = "c1", Stages = new[] { new StageSpec { Id = "a" } } },
                new ChapterSpec { Id = "c2", SkillChoice = true, Stages = new[] { new StageSpec { Id = "b", UnlocksSkill = "laser" }, new StageSpec { Id = "c", UnlocksSkill = "bomb" } } },
            }
        };
        var progress = new AdventureProgress();
        var adventure = new Adventure(campaign, progress);
        check(old.All(adventure.SkillOpen) && !adventure.SkillOpen(BattleSkill.Laser) && !adventure.SkillOpen(BattleSkill.Fan), "The five first skills are always open; the new ones are not");
        progress.Completed.Add("b");
        check(!adventure.SkillOpen(BattleSkill.Laser), "Winning the stage that opens a skill does not open one with no effect yet");
        check(!adventure.SkillChoice(campaign.Chapters[0].Stages[0]) && adventure.SkillChoice(campaign.Chapters[1].Stages[0]),
            "Only a chapter with skillChoice lets the user choose (chapter 1 keeps the five first skills)");
        check(!data.Campaign!.Chapters[0].SkillChoice && data.Campaign.Chapters.SelectMany(c => c.Stages).All(s => s.UnlocksSkill == null),
            "The shipped chapter 1 has no skill choice and no stage opens a skill yet");

        // An Expedition run keeps the loadout it set out with.
        var expedition = new Expedition(data, new ExpeditionProgress());
        var run = expedition.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 42, "loadout-a");
        check(run.Skills == null && run.Loadout.SequenceEqual(old), "A run set out with the default saves no skills and reads the default");
        var other = new Expedition(data, new ExpeditionProgress()).Start(BattleWeapon.Sword, BattleDifficulty.Normal, 42, "loadout-b", skills: noBomb.Skills);
        check(other.Skills!.SequenceEqual(new[] { "push", "heal", "cheer", "shield", "laser" }) && other.Loadout.SequenceEqual(noBomb.Skills),
            "A run keeps the loadout it set out with");
        var json = System.Text.Json.JsonSerializer.Serialize(other);
        check(System.Text.Json.JsonSerializer.Deserialize<ExpeditionRun>(json)!.Loadout.SequenceEqual(noBomb.Skills) && !json.Contains("\"Loadout\""),
            "The run's skills survive saving (only the ids are written)");
        check(System.Text.Json.JsonSerializer.Deserialize<ExpeditionRun>("{\"RunId\":\"old\"}")!.Loadout.SequenceEqual(old), "A run saved before 1.5 reads as the default");
    }
}
