using DesktopPet;

// Combat Depth milestone 2: card groups, synergies (Thorns, Armor break, Afterimage), the offer
// rule (one card per group, the fish replaces the weakest group), max HP cards, the build carried
// from stage to stage and its save. BattleSim and AdventureProgress only, no window.
internal static class BuildSynergyTests
{
    const double dt = 1.0 / 60;

    private static UpgradeSpec Card(string id, string? group, string effect = "maxHp", double amount = 1, int max = 3) =>
        new() { Id = id, Group = group, NameVi = id, NameEn = id, DescVi = "", DescEn = "", Effect = effect, Amount = amount, MaxStacks = max };

    // Three groups of harmless cards (1 max HP each), plus the shipped synergies.
    private static readonly UpgradeSpec[] Plain = { Card("d", "defense", max: 5), Card("p", "power", "knock", 1), Card("s", "speed", "moveSpeed", 0.01), Card("fish", null, "heal", 30, 0) };

    private static StageSpec Stage(int waves = 3, int? maxUpgrades = null, string objective = "clear", double? survive = null) => new()
    {
        Id = "t", Upgrades = true, MaxUpgrades = maxUpgrades, Objective = objective, SurviveSeconds = survive,
        Waves = Enumerable.Range(0, waves).Select(_ => new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 } } }).ToArray()
    };

    private static BattleSettings Quick(UpgradeSpec[]? cards = null, double slimeHp = 1, double slimeDamage = 0, double catDamage = 25, double waveHeal = 15) => new()
    {
        FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = slimeHp, SlimeDamage = slimeDamage, CatDamage = catDamage, WaveBreakSeconds = 0.5, OutroSeconds = 0.5,
        WaveHeal = waveHeal, Upgrades = cards ?? Plain
    };

    private static double cat = 800;
    private static void Until(BattleSim sim, Func<bool> done, double seconds = 30)
    {
        for (double t = 0; t < seconds && !done(); t += dt)
        {
            var order = sim.Step(dt, cat, false);
            if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -72 * 1.8 * dt, 72 * 1.8 * dt);
        }
    }

    public static void Run(Action<bool, string> check)
    {
        // The shipped data: three groups, each able to reach its synergy at 3; the fish counts for none.
        var data = BattleData.Settings;
        foreach (var group in BuildGroups.All)
        {
            int room = data.UpgradeList.Where(u => u.Group == group).Sum(u => u.MaxStacks == 0 ? 99 : u.MaxStacks);
            var synergy = data.SynergyList.Single(y => y.Group == group);
            check(synergy.At == 3 && room >= 3, $"battle.json: {group} has room for {room} points and a synergy at 3 ({synergy.Name("en")})");
        }
        check(data.SynergyList.Select(y => y.Effect).OrderBy(e => e).SequenceEqual(new[] { "armorBreak", "echo", "thorns" }) &&
              data.UpgradeList.Single(u => u.Effect == "heal").Group == null && data.UpgradeList.Any(u => u is { Id: "big_heart", Effect: "maxHp", Group: "defense" }),
            "battle.json: Thorns, Armor break and Afterimage; Big heart joins Defense; the fish has no group");
        check(data.Campaign!.Chapters[0].Stages[2].MaxUpgrades == 3, "The survive stage allows at most 3 picks");

        // The offer: one card from each group at full HP.
        bool distinct = true;
        for (int seed = 1; seed <= 15; seed++)
        {
            cat = 800;
            var sim = new BattleSim(Quick(BattleData.Settings.UpgradeList.ToArray()), 0, 1600, new Random(seed), stage: Stage());
            Until(sim, () => sim.ChoosingUpgrade);
            var offer = sim.UpgradeOffer!;
            distinct &= offer.Count == 3 && offer.Select(u => u.Group).OrderBy(g => g).SequenceEqual(new[] { "defense", "power", "speed" });
        }
        check(distinct, "At full HP every offer has one Defense, one Power and one Speed card (15 seeds)");

        // Hurt: the fish takes the place of the group the build has fewest points in (never Defense
        // here, the build holds 2 of it).
        bool fishRule = true;
        for (int seed = 1; seed <= 15; seed++)
        {
            cat = 800;
            var hurt = new BattleSim(Quick(slimeHp: 80, slimeDamage: 5, waveHeal: 0), 0, 1600, new Random(seed), stage: Stage(),
                build: new Dictionary<string, int> { ["d"] = 2 });
            Until(hurt, () => hurt.ChoosingUpgrade || hurt.Over, 60);
            var offer = hurt.UpgradeOffer;
            fishRule &= hurt.CatHp < hurt.CatMaxHp && offer is { Count: 3 } && offer.Any(u => u.Id == "fish") && offer.Any(u => u.Group == "defense") &&
                offer.Select(u => u.Group).Distinct().Count() == 3;
        }
        check(fishRule, "Hurt: the fish replaces the card of a group the build has least of; the build's own group stays on offer (15 seeds)");

        // A group with nothing left gives its place to another card; never a heal at full HP.
        cat = 800;
        var full = new BattleSim(Quick(new[] { Card("d", "defense", max: 1), Card("p", "power"), Card("p2", "power"), Card("x", null, "knock"), Card("fish", null, "heal", 30, 0) }),
            0, 1600, new Random(2), stage: Stage(), build: new Dictionary<string, int> { ["d"] = 1 });
        Until(full, () => full.ChoosingUpgrade);
        check(full.UpgradeOffer is { Count: 3 } o && o.All(u => u.Id != "d" && u.Id != "fish") && o.Select(u => u.Id).Distinct().Count() == 3,
            "A group at its limit offers nothing; other cards fill its place (three different ones), no fish at full HP");

        // Synergies: 3 points switch one on, once, announced once; the card says so beforehand.
        cat = 800;
        var build = new BattleSim(Quick(), 0, 1600, new Random(4), stage: Stage(5));
        int unlocked = 0, synergyAtTwo = -1;
        for (int pick = 0; pick < 4; pick++)
        {
            Until(build, () => build.ChoosingUpgrade);
            var d = build.UpgradeOffer!.First(u => u.Group == "defense");
            bool predicted = build.Unlocks(d) != null;
            if (build.GroupPoints("defense") == 2) synergyAtTwo = build.Synergies.Count;
            build.ChooseUpgrade(d.Id, cat);
            unlocked += build.Events.Count(e => e.Kind == BattleEventKind.SynergyUnlocked);
            build.ClearEvents();
            check(predicted == (build.GroupPoints("defense") == 3), $"The card says it completes the synergy exactly when it does (pick {pick + 1})");
        }
        check(synergyAtTwo == 0 && build.GroupPoints("defense") == 4 && build.Synergies.Count == 1 && build.Synergies[0].Effect == "thorns" && unlocked == 1 && build.Picks == 4,
            "Defense 3 switches Thorns on once (not at 2, not again at 4), announced once");

        // Thorns: 35 % of what the cat really takes goes back; nothing through the Shield.
        cat = 800;
        var thorns = new BattleSim(Quick(slimeHp: 1000, slimeDamage: 10, catDamage: 0), 0, 1600, new Random(3), stage: Stage(1),
            build: new Dictionary<string, int> { ["d"] = 3 });
        Until(thorns, () => thorns.CatHp < thorns.CatMaxHp, 20);
        var biter = thorns.Slimes[0];
        check(thorns.Synergies.Any(y => y.Effect == "thorns") && thorns.Reflects == 1 && Math.Abs(biter.MaxHp - biter.Hp - 3.5) < 1e-9,
            $"Thorns: a 10-damage hit sends 3.5 back ({biter.MaxHp - biter.Hp})");
        double hpBefore = biter.Hp;
        Until(thorns, () => thorns.Slimes[0].State == SlimeState.Windup, 10);
        thorns.Use(BattleSkill.Shield, cat);
        Until(thorns, () => thorns.Slimes[0].State == SlimeState.Recover, 3);
        check(biter.Hp == hpBefore && thorns.Reflects == 1, "Thorns: a hit the Shield blocks sends nothing back");

        // Armor break: the combo hit deals 30 % more (25, 25, then 50 × 1.3 = 65).
        cat = 800;
        var breaker = new BattleSim(Quick(slimeHp: 1000, catDamage: 25), 0, 1600, new Random(3), stage: Stage(1), build: new Dictionary<string, int> { ["p"] = 3 });
        Until(breaker, () => breaker.CombosLanded == 1, 20);
        var dummy = breaker.Slimes[0];
        check(breaker.Synergies.Any(y => y.Effect == "armorBreak") && Math.Abs(dummy.MaxHp - dummy.Hp - 115) < 1e-9, $"Armor break: combo 65 instead of 50 (took {dummy.MaxHp - dummy.Hp})");

        // Afterimage: the 4th landed swing strikes again for half; the echo starts no count of its own.
        cat = 800;
        var echo = new BattleSim(Quick(slimeHp: 1000, catDamage: 25), 0, 1600, new Random(3), stage: Stage(1), build: new Dictionary<string, int> { ["s"] = 3 });
        int Swings() => echo.Events.Count(e => e.Kind is BattleEventKind.SlimeHit or BattleEventKind.ComboHit);
        Until(echo, () => Swings() == 4, 20);
        var target = echo.Slimes[0];
        check(echo.Echoes == 1 && Math.Abs(target.MaxHp - target.Hp - (25 + 25 + 50 + 25 + 12.5)) < 1e-9, $"Afterimage: 4th hit adds 12.5 (took {target.MaxHp - target.Hp})");
        Until(echo, () => Swings() == 8, 20);
        check(echo.Echoes == 2, "and again on the 8th, not more often");

        // Big heart: max HP and HP at once; the wave heal fills to the new maximum.
        cat = 800;
        // The first seed whose first offer has it (the Defense card is drawn from two).
        BattleSim heart = null!;
        bool hasHeart = false;
        for (int seed = 1; seed <= 20 && !hasHeart; seed++)
        {
            cat = 800;
            heart = new BattleSim(Quick(BattleData.Settings.UpgradeList.ToArray()), 0, 1600, new Random(seed), stage: Stage());
            Until(heart, () => heart.ChoosingUpgrade);
            hasHeart = heart.UpgradeOffer?.Any(u => u.Id == "big_heart") == true;
        }
        double maxBefore = heart.CatMaxHp, hpNow = heart.CatHp;
        if (hasHeart) heart.ChooseUpgrade("big_heart", cat);
        check(hasHeart && heart.CatMaxHp == maxBefore + 20 && heart.CatHp == hpNow + 20, "Big heart: +20 max HP and 20 HP at once");

        // The carried build: its cards as taken, synergy on from the start (no banner), full HP, no picks.
        var carried = new BattleSim(BattleData.Settings, 0, 1600, new Random(1), stage: data.Campaign.Chapters[0].Stages[1],
            build: new Dictionary<string, int> { ["thick_fur"] = 2, ["big_heart"] = 5, ["grilled_fish"] = 3, ["gone"] = 1 });
        check(carried.Stacks("thick_fur") == 2 && carried.Stacks("big_heart") == 2 && carried.Stacks("grilled_fish") == 0 && carried.Stacks("gone") == 0 &&
              carried.Synergies.Any(y => y.Effect == "thorns") && carried.CatMaxHp == 140 && carried.CatHp == 140 && carried.Picks == 0 &&
              !carried.Events.Any(e => e.Kind == BattleEventKind.SynergyUnlocked) && Math.Abs(carried.ArmorFactor - 0.7) < 1e-9,
            "A carried build: limits kept, heals and unknown cards dropped, synergy already on, full (bigger) HP");
        check(carried.Build().OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}").SequenceEqual(new[] { "big_heart=2", "thick_fur=2" }), "Build(): what goes on (no heals)");

        // maxUpgrades: a survive stage stops offering after its picks.
        cat = 800;
        var hold = new BattleSim(Quick(), 0, 1600, new Random(5), stage: Stage(1, maxUpgrades: 2, objective: "survive", survive: 40));
        int offers = 0;
        Until(hold, () => { if (hold.ChoosingUpgrade) { offers++; hold.ChooseUpgrade(hold.UpgradeOffer![0].Id, cat); } return hold.Over; }, 60);
        check(hold.Phase == BattlePhase.Won && offers == 2 && hold.Picks == 2 && hold.Wave > 3, $"maxUpgrades 2: two picks, then the waves go on without offers ({hold.Wave} waves)");

        // The Adventure keeps the build per stage: a win stores it, a loss does not, the next stage
        // starts with it, a new win of the stage replaces it.
        var stages = data.Campaign.Chapters[0].Stages;
        var adventure = new Adventure(data.Campaign, new AdventureProgress());
        check(adventure.StartBuild(stages[0]).Count == 0 && adventure.StartBuild(stages[2]).Count == 0, "Nothing won yet: every stage starts with no build");
        adventure.Apply("w1", stages[1], true, true, "sword", BattleDifficulty.Normal, 5, 50, new Dictionary<string, int> { ["sharp"] = 2 });
        adventure.Apply("l1", stages[1], true, false, "sword", BattleDifficulty.Normal, 5, 50, new Dictionary<string, int> { ["sharp"] = 3 });
        check(adventure.StartBuild(stages[2]).SequenceEqual(new Dictionary<string, int> { ["sharp"] = 2 }), "Stage 3 starts with stage 2's winning build; a loss changed nothing");
        adventure.Apply("w2", stages[1], true, true, "sword", BattleDifficulty.Normal, 5, 50, new Dictionary<string, int> { ["thick_fur"] = 1 });
        adventure.Apply("c", stages[1], false, true, "sword", BattleDifficulty.Normal, 5, 50, new Dictionary<string, int> { ["sharp"] = 9 });
        check(adventure.StartBuild(stages[2]).SequenceEqual(new Dictionary<string, int> { ["thick_fur"] = 1 }), "Winning again replaces it; a custom-data run saves nothing");

        string dir = Path.Combine(Path.GetTempPath(), "pet-builds-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "adventure-progress.json");
            check(adventure.Progress.Save(path) && AdventureProgress.Load(path).Builds[stages[1].Id]["thick_fur"] == 1 && File.ReadAllText(path).Contains("\"Version\": 2"),
                "Builds are saved and read back (file version 2)");
            File.WriteAllText(path, "{ \"Version\": 1, \"Completed\": [\"c1_first_steps\"] }");
            var old = AdventureProgress.Load(path);
            check(!old.ReadOnly && old.Builds.Count == 0 && old.Completed.Count == 1 && old.Save(path) && File.ReadAllText(path).Contains("\"Version\": 2"),
                "A version 1 file reads with no builds and is saved as version 2");
            File.WriteAllText(path, "{ \"Builds\": { \"c1_break_through\": { \"sharp\": -2, \"fast_paws\": 2, \"\": 1 }, \"\": { \"x\": 1 } } }");
            var odd = AdventureProgress.Load(path);
            check(odd.Builds.Count == 1 && odd.Builds["c1_break_through"].Count == 1 && odd.Builds["c1_break_through"]["fast_paws"] == 2, "Impossible build entries are cleaned");
        }
        finally { Directory.Delete(dir, true); }

        // Validation.
        bool Rejected(string json)
        {
            try { BattleData.Parse(json); return false; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return true; }
        }
        const string Up = "{\"id\":\"u\",\"nameVi\":\"u\",\"nameEn\":\"u\",\"descVi\":\"\",\"descEn\":\"\",";
        check(Rejected("{\"upgrades\":[" + Up + "\"effect\":\"damage\",\"amount\":0.1,\"group\":\"luck\"}]}") &&
              Rejected("{\"upgrades\":[" + Up + "\"effect\":\"heal\",\"amount\":10,\"group\":\"defense\"}]}") &&
              !Rejected("{\"upgrades\":[" + Up + "\"effect\":\"maxHp\",\"amount\":10,\"group\":\"defense\"}]}"),
            "An unknown group, or a heal in a group, is rejected; a max HP card in Defense is fine");
        const string Syn = "{\"id\":\"y\",\"nameVi\":\"y\",\"nameEn\":\"y\",\"descVi\":\"\",\"descEn\":\"\",\"group\":\"speed\",";
        check(Rejected("{\"synergies\":[" + Syn + "\"effect\":\"fly\",\"amount\":1}]}") && Rejected("{\"synergies\":[" + Syn + "\"effect\":\"echo\",\"amount\":0.5,\"every\":1}]}") &&
              !Rejected("{\"synergies\":[" + Syn + "\"effect\":\"echo\",\"amount\":0.5,\"every\":3}]}"), "Unknown synergy effects and impossible numbers are rejected");
        check(Rejected("{\"campaign\":{\"chapters\":[{\"id\":\"c\",\"nameVi\":\"c\",\"nameEn\":\"c\",\"stages\":[{\"id\":\"s\",\"nameVi\":\"s\",\"nameEn\":\"s\",\"maxUpgrades\":0,\"waves\":[{\"groups\":[{\"enemy\":\"slime\",\"count\":1}]}]}]}]}}"),
            "maxUpgrades must be at least 1");
    }
}
