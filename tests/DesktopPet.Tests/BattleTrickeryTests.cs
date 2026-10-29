using DesktopPet;

// 1.5 M25 (PetBattle.Trickery.cs, docs/UPDATE-1.5.md J1): the Trickery group. The shipped cards and
// synergies; its cards come by the utility place, a skill's only with the skill carried, a drop's only
// with drops; the skills' cards (Laser, Fan, Scissors), Ground quake (a real lift, floor enemies near the
// landing, its cooldown), Sleight of paw, Teamwork, Quick reload and Skill feast; an Expedition run of
// rules 7 goes on with its shop.
internal static class BattleTrickeryTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id = "slime", double hp = 1000) => new()
    {
        Id = id, MaxHp = hp, Speed = 0, Damage = 0, Range = 50, Windup = 0.8, AttackCooldown = 1.8, Recover = 0.35
    };

    private static UpgradeSpec Card(string id, string effect, double amount, bool rare = false) =>
        new() { Id = id, Group = BuildGroups.Trick, Rare = rare, Effect = effect, Amount = amount, MaxStacks = rare ? 1 : 2, NameVi = id, NameEn = id, DescVi = id, DescEn = id };
    private static SynergySpec Syn(string effect, double amount) =>
        new() { Id = effect.ToLowerInvariant(), Group = BuildGroups.Trick, At = 1, Effect = effect, Amount = amount, NameVi = "a", NameEn = "a", DescVi = "a", DescEn = "a" };

    // A field with `count` slimes that never move or hit, the cat at 800.
    private static BattleSim Field(UpgradeSpec[] cards, Dictionary<string, int> build, SynergySpec[]? synergies = null, int count = 1, double hp = 1000,
        IEnumerable<BattleSkill>? skills = null, BattleLoot loot = BattleLoot.None, DropSpec? drops = null)
    {
        var settings = new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0, SpawnMinDistance = 0, CatDamage = 0, CatMaxHp = 100, RageHitGain = 0, Enemies = new[] { Kind(hp: hp) },
            Upgrades = cards, SynergiesData = synergies ?? Array.Empty<SynergySpec>(), Drops = drops, MaxAlive = 8,
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = count } } }, new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime" } } } }
        };
        var stage = new StageSpec { Id = "trick_test", Waves = settings.Plan };
        var sim = new BattleSim(settings, 0, 1600, seed: 2, stage: stage, build: build, skills: skills, loot: loot);
        Steps(sim, 1.5);
        return sim;
    }

    private static void Steps(BattleSim sim, double seconds, bool held = false)
    {
        for (int i = 0; i < Math.Round(seconds / dt); i++) sim.Step(dt, 800, held, lifted: held);
    }

    // The user lifts the cat for `seconds`, then puts it down (one more frame on the floor); `before`
    // runs just before that frame.
    private static void Lift(BattleSim sim, double seconds, Action? before = null)
    {
        sim.SetCatState(true, true);
        Steps(sim, seconds, held: true);
        sim.SetCatState(false, false);
        before?.Invoke();
        sim.Step(dt, 800, false);
    }

    // A hit from outside the cat (the tests' own: defeats an enemy at once when it is worth more than its HP).
    private static void Hit(BattleSim sim, Slime slime, double amount) =>
        typeof(BattleSim).GetMethod("Damage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(sim, new object[] { slime, amount, DamageSource.Tool });

    public static void Run(Action<bool, string> check)
    {
        DataChecks(check);
        OfferChecks(check);
        CardChecks(check);
        QuakeChecks(check);
        SynergyChecks(check);
        ExpeditionChecks(check);
    }

    private static void DataChecks(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var trick = data.UpgradeList.Where(u => u.Group == BuildGroups.Trick).ToList();
        check(BuildGroups.All.SequenceEqual(new[] { "defense", "power", "speed", "trick" }) && BuildGroups.Offered.SequenceEqual(new[] { "defense", "power", "speed" })
              && BuildGroups.Name(BuildGroups.Trick, "vi") == "Mưu mẹo" && BuildGroups.Name(BuildGroups.Trick, "en") == "Trickery",
            "Trickery is the fourth group (Mưu mẹo); offers give the other three a place each");
        check(trick.Count == 14 && trick.Count(u => u.Rare) == 2 && data.UpgradeList.All(u => u.Group != null || u.Effect == "heal"),
            "battle.json: 14 Trickery cards (the 5 old utility cards among them, 2 rare); only the fish has no group");
        string?[] skills = { "laser", "fan", "grabber", "scissors", "spray" };
        check(skills.All(s => trick.Count(u => u.Skill == s) == 1) && trick.Where(u => u.NeedsDrops).Select(u => u.Id).OrderBy(i => i).SequenceEqual(new[] { "fat_fish", "keen_nose" }),
            "One card for each new skill (only with it carried), two for the drops (only with drops)");
        var quake = trick.Single(u => u.Id == "ground_quake");
        check(quake is { Rare: true, Effect: "landingQuake", Amount: 12 } && trick.Single(u => u.Id == "sleight") is { Rare: true, Effect: "skillRefund", Amount: 0.25 }
              && BattleSim.QuakeRadius == 110 && BattleSim.QuakeCooldown == 6,
            "Rare: Ground quake (12 damage, 110 DIPs, once per 6 s) and Sleight of paw (25 %)");
        var tiers = data.SynergyList.Where(y => y.Group == BuildGroups.Trick).OrderBy(y => y.At).ToList();
        check(tiers.Select(y => (y.Id, y.At, y.Effect, y.EndlessOnly)).SequenceEqual(new[] { ("teamwork", 3, "skillRush", false), ("quick_reload", 6, "killRecharge", true), ("skill_feast", 9, "skillFeast", true) }),
            "Synergies: Teamwork at 3 everywhere, Quick reload at 6 and Skill feast at 9 in Infinity");
        check(data.Endless!.RulesVersion >= 7 && data.Expedition!.RulesVersion == 8 && data.Expedition.Route("dump_trail")!.RulesVersion >= 2,
            "New rules versions: Infinity 7, Expedition 8, the Dump Trail 2 (or later: 1.5 M26 raised Infinity and the Dump Trail)");
        // An old saved build with the utility cards now counts toward Trickery.
        var old = new BattleSim(data, 0, 1600, seed: 1, stage: data.Campaign!.Chapters[0].Stages[1], build: new Dictionary<string, int> { ["quick_hands"] = 2, ["big_bomb"] = 1 });
        check(old.GroupPoints(BuildGroups.Trick) == 3 && old.Synergies.Any(y => y.Id == "teamwork"), "A build carried in with 3 of the old utility cards switches Teamwork on");
    }

    private static void OfferChecks(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var stage = data.Campaign!.Chapters[0].Stages[1];
        var tools = new[] { BattleSkill.Laser, BattleSkill.Grabber, BattleSkill.Scissors, BattleSkill.Fan, BattleSkill.Spray };
        string[] toolCards = { "long_laser", "turbo_fan", "firm_grip", "sharp_scissors", "big_tank" };
        bool toolsWithout = false, toolsWith = false, dropsWithout = false, dropsWith = false, rare = false, twoTricks = false;
        int trickOffers = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            var plain = new BattleSim(data, 0, 1600, seed: seed, stage: stage).OfferCards() ?? Array.Empty<UpgradeSpec>();
            toolsWithout |= plain.Any(u => toolCards.Contains(u.Id));
            dropsWithout |= plain.Any(u => u.NeedsDrops);
            if (plain.Any(u => u.Group == BuildGroups.Trick)) trickOffers++;
            twoTricks |= plain.Count(u => u.Group == BuildGroups.Trick) > 1;
            var carried = new BattleSim(data, 0, 1600, seed: seed, stage: stage, skills: tools, loot: BattleLoot.Items).OfferCards() ?? Array.Empty<UpgradeSpec>();
            toolsWith |= carried.Any(u => toolCards.Contains(u.Id));
            dropsWith |= carried.Any(u => u.NeedsDrops);
            rare |= carried.Concat(plain).Any(u => u.Rare && u.Group == BuildGroups.Trick);
        }
        check(!toolsWithout && toolsWith, "A new skill's card only turns up when the battle carries that skill");
        check(!dropsWithout && dropsWith, "Keen nose and Fat fish only turn up in battles with drops");
        check(rare, "Trickery's rare cards turn up in the utility place now and then");
        check(!twoTricks, "At most one Trickery card in a first offer (the utility place)");
        double share = trickOffers / 400.0;
        check(share is > 0.25 and < 0.5, $"A Trickery card is in about a third of the offers ({share:P0}; utilityChance 0.35, fillers too)");
    }

    private static void CardChecks(Action<bool, string> check)
    {
        var laser = Field(new[] { Card("l", "laserTime", 1.5) }, new() { ["l"] = 1 }, skills: new[] { BattleSkill.Laser, BattleSkill.Push, BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Shield });
        laser.Arm(BattleSkill.Laser);
        check(laser.UseAt(BattleSkill.Laser, 600) && Math.Abs(laser.LaserRemaining - (laser.Settings.LaserSeconds + 1.5)) < 1e-9 && laser.LaserSeconds == laser.Settings.LaserSeconds + 1.5,
            "Lasting laser: the dot stays 1.5 s longer");
        var fan = Field(new[] { Card("f", "fanPower", 0.25) }, new() { ["f"] = 1 });
        check(Math.Abs(fan.FanRange - fan.Settings.FanRange * 1.25) < 1e-9, "Turbo fan: 25 % more reach");
        var cut = Field(new[] { Card("s", "scissorsDamage", 0.4) }, new() { ["s"] = 1 }, skills: new[] { BattleSkill.Scissors, BattleSkill.Push, BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Shield });
        var slime = cut.Slimes.First(s => s.Targetable);
        double before = slime.Hp;
        cut.Arm(BattleSkill.Scissors);
        check(cut.Snip(slime.X - 40, 10, slime.X + 40, 10) && Math.Abs(before - slime.Hp - cut.Settings.ScissorsDamage * 1.4) < 1e-6, $"Sharp scissors: a cut deals 40 % more ({before - slime.Hp:0.#})");

        // Fat fish: a dropped fish heals 10 HP more.
        var drops = new DropSpec { Chance = 1, EliteChance = 1, Life = 8, Fade = 1.5, MaxOnFloor = 6, Weights = new() { ["fish"] = 1 }, FishHeal = 10 };
        var fishy = Field(new[] { Card("ff", "fishHeal", 10) }, new() { ["ff"] = 1 }, count: 2, loot: BattleLoot.Items, drops: drops);
        typeof(BattleSim).GetProperty("CatHp")!.SetValue(fishy, 50.0);
        Hit(fishy, fishy.Slimes.First(s => s.Targetable), 1e6);
        double hp = fishy.CatHp;
        check(fishy.Drops.Count == 1 && fishy.PickUp(fishy.Drops[0].Id) && Math.Abs(fishy.CatHp - hp - 20) < 1e-6, $"Fat fish: a fish heals 20 HP instead of 10 ({fishy.CatHp - hp:0})");

        // Sleight of paw at 100 %: every skill used costs half its cooldown.
        var magic = Field(new[] { Card("m", "skillRefund", 1, rare: true) }, new() { ["m"] = 1 });
        check(magic.Use(BattleSkill.Cheer, 800) && Math.Abs(magic.Cooldown(BattleSkill.Cheer) - magic.SkillCooldown(BattleSkill.Cheer) / 2) < 1e-9 && magic.Refunds == 1
              && magic.Events.Any(e => e.Kind == BattleEventKind.SkillRefunded),
            "Sleight of paw: the skill's cooldown is halved (here at 100 %)");
        var none = Field(new[] { Card("m", "skillRefund", 1, rare: true) }, new());
        check(none.Use(BattleSkill.Cheer, 800) && Math.Abs(none.Cooldown(BattleSkill.Cheer) - none.SkillCooldown(BattleSkill.Cheer)) < 1e-9 && none.Refunds == 0,
            "Without it the cooldown is whole");
    }

    private static void QuakeChecks(Action<bool, string> check)
    {
        var cards = new[] { Card("q", "landingQuake", 12, rare: true) };
        var sim = Field(cards, new() { ["q"] = 1 }, count: 3);
        var near = sim.Slimes.Where(s => s.Targetable).ToList();
        near[0].X = 830; near[1].X = 760; near[2].X = 1300;
        double[] before = near.Select(s => s.Hp).ToArray();
        Lift(sim, 0.1);
        check(sim.Quakes == 0 && near[0].Hp == before[0], "A short press-and-drop (under the lift time) shakes nothing");
        Lift(sim, 0.5);
        check(sim.Quakes == 1 && sim.QuakeHits == 2 && Math.Abs(before[0] - near[0].Hp - 12) < 1e-6 && Math.Abs(before[1] - near[1].Hp - 12) < 1e-6 && near[2].Hp == before[2]
              && sim.Events.Any(e => e.Kind == BattleEventKind.Quaked && e.Value == 2),
            "Put down after a real lift: 12 damage to the enemies near the landing, none to one far away");
        check(near[0].KnockVelocity > 0 && near[1].KnockVelocity < 0, "They are knocked a little away from the landing");
        double hp0 = near[0].Hp;
        Lift(sim, 0.5);
        check(sim.Quakes == 1 && near[0].Hp == hp0 && sim.QuakeRemaining > 0, "Within its 6 s cooldown a second landing shakes nothing");
        Steps(sim, 6);
        near[0].X = 830; near[0].KnockVelocity = 0;
        // An enemy in the air is not hit.
        near[1].X = 780;
        double air = near[1].Hp;
        Lift(sim, 0.5, () => near[1].Altitude = 40);
        check(sim.Quakes == 2 && near[0].Hp < hp0 && near[1].Hp == air, "Once ready again it shakes; an enemy in the air is not hit");
        var plain = Field(Array.Empty<UpgradeSpec>(), new(), count: 1);
        var s0 = plain.Slimes.First(s => s.Targetable);
        s0.X = 820;
        double h = s0.Hp;
        Lift(plain, 0.5);
        check(plain.Quakes == 0 && s0.Hp == h, "Without the card a landing shakes nothing");
    }

    private static void SynergyChecks(Action<bool, string> check)
    {
        var points = new[] { Card("p", "laserTime", 1) };
        var team = Field(points, new() { ["p"] = 1 }, new[] { Syn("skillRush", 0.2) });
        check(team.SkillRushRemaining == 0 && team.Use(BattleSkill.Cheer, 800) && Math.Abs(team.SkillRushRemaining - BattleSim.SkillRushSeconds) < 1e-9,
            "Teamwork: a skill used starts 3 s of more damage");
        Steps(team, 3.1);
        check(team.SkillRushRemaining == 0, "It wears off after 3 s");

        var reload = Field(points, new() { ["p"] = 1 }, new[] { Syn("killRecharge", 0.5) }, hp: 1);
        reload.Use(BattleSkill.Cheer, 800);
        double cd = reload.Cooldown(BattleSkill.Cheer);
        var target = reload.Slimes.First(s => s.Targetable);
        int defeated = reload.Defeated;
        Hit(reload, target, 1e6);
        check(reload.Defeated == defeated + 1 && Math.Abs(cd - reload.Cooldown(BattleSkill.Cheer) - 0.5) < 1e-9, "Quick reload: a defeat takes 0.5 s off every skill cooling down");

        var feast = Field(points, new() { ["p"] = 1 }, new[] { Syn("skillFeast", 10) });
        feast.Use(BattleSkill.Cheer, 800);
        // Its clock has run since the fight began (up to 1.5 s in Field): the feast comes 8.5–10 s after the skill.
        double waited = 0;
        while (feast.Feasts == 0 && waited < 12) { feast.Step(dt, 800, false); waited += dt; }
        check(feast.Feasts == 1 && waited is >= 8.4 and <= 10.05 && feast.Cooldown(BattleSkill.Cheer) == 0 && feast.Events.Any(e => e.Kind == BattleEventKind.SkillFeast),
            $"Skill feast: every 10 s of fighting the skill with the most cooldown left is ready ({waited:0.0} s after it was used)");
        Steps(feast, 5);
        check(feast.Feasts == 1, "Not again before its next 10 s");

        // The Infinity tiers stay off elsewhere (Allowed), Teamwork does not.
        var data = BattleData.Settings;
        var build = new Dictionary<string, int> { ["quick_hands"] = 3, ["big_bomb"] = 2, ["thick_bubble"] = 2, ["keen_nose"] = 2 };
        var adventure = new BattleSim(data, 0, 1600, seed: 1, stage: data.Campaign!.Chapters[0].Stages[1], build: build);
        var endless = new BattleSim(data, 0, 1600, seed: 1, stage: data.Endless!.Stage(), build: build);
        check(adventure.Synergies.Select(y => y.Id).SequenceEqual(new[] { "teamwork" }) && endless.Synergies.Select(y => y.Id).SequenceEqual(new[] { "teamwork", "quick_reload", "skill_feast" }),
            "9 Trickery points: Teamwork in the Adventure; all three tiers in Infinity");
    }

    private static void ExpeditionChecks(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        bool shop = false;
        for (int seed = 1; seed <= 40 && !shop; seed++)
        {
            var x = new Expedition(data, new ExpeditionProgress());
            var run = x.Start(BattleWeapon.Sword, BattleDifficulty.Normal, seed);
            run.RulesVersion = 7;
            check(x.Mismatch(run) == null || seed > 1, "A run of rules 7 still fits (keepRuns)");
            for (int step = 0; step < data.Expedition!.PathSteps && !shop; step++)
            {
                run.Path.Clear(); run.Path.AddRange(Enumerable.Repeat(0, step));
                x.Roll();
                shop |= run.Kinds![step].Contains(ExpeditionSpec.Shop);
            }
        }
        check(shop, "Its steps still draw the cat shop (shopFrom 7)");
    }
}
