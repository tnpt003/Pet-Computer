using DesktopPet;

// Battle balance 2026-10-24, milestone 4: the new cards (crit, execute, combo window, Tailwind,
// Regrowth, Steady and the utility cards), Infinity's own card limits and 6/9-point synergy tiers,
// its second pick after a boss wave, every synergy from wave 48 and more elites as it goes on.
internal static class EndlessBuildTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id = "slime", double hp = 1000, double damage = 0, double speed = 60, EliteSpec? elite = null) => new()
    {
        Id = id, MaxHp = hp, Speed = speed, Damage = damage, Range = 64, Windup = 0.8, AttackCooldown = 1.8, Recover = 0.35, Elite = elite
    };

    private static BattleSettings Data(UpgradeSpec[]? cards = null, SynergySpec[]? synergies = null, EnemyType[]? kinds = null, WaveSpec[]? plan = null,
        double catDamage = 10, double catHp = 100) => new()
    {
        FirstWaveDelay = 0, SpawnInterval = 0, WaveBreakSeconds = 0.3, CatDamage = catDamage, CatMaxHp = catHp, SpawnMinDistance = 0, ComboSplash = 0,
        Upgrades = cards, SynergiesData = synergies, Enemies = kinds ?? new[] { Kind() },
        Plan = plan ?? new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime" } } } }
    };

    private static UpgradeSpec Card(string id, string effect, double amount, string? group = "power", int max = 5) =>
        new() { Id = id, Group = group, Effect = effect, Amount = amount, MaxStacks = max, NameVi = id, NameEn = id, DescVi = id, DescEn = id };

    private static StageSpec Endless(int waves = 3, int? all = null, bool bonus = false) => new()
    {
        Id = "endless_test", Objective = StageSpec.EndlessObjective, Upgrades = true, AllSynergiesAt = all, BossBonusPick = bonus,
        Waves = Enumerable.Range(0, waves).Select(_ => new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime" } } }).ToArray()
    };
    private static StageSpec Plain(int waves = 1) => new()
    {
        Id = "plain_test", Waves = Enumerable.Range(0, waves).Select(_ => new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime" } } }).ToArray()
    };

    // Steps with a cat that walks where the battle sends it.
    private static double Walk(BattleSim sim, double seconds, Func<bool>? done = null, double cat = 800)
    {
        for (double t = 0; t < seconds && !sim.Over && done?.Invoke() != true; t += dt)
        {
            var order = sim.Step(dt, cat, false);
            if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -130 * dt, 130 * dt);
        }
        return cat;
    }

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var sharp = data.UpgradeList.Single(u => u.Id == "sharp");
        var endlessSim = new BattleSim(data, 0, 1600, seed: 1, stage: data.Endless!.Stage());
        var adventureSim = new BattleSim(data, 0, 1600, seed: 1, stage: data.Campaign!.Chapters[0].Stages[1]);
        check(endlessSim.StackLimit(sharp) == 5 && adventureSim.StackLimit(sharp) == 3 && endlessSim.StackLimit(data.UpgradeList.Single(u => u.Id == "heavy_push")) == 2,
            "Infinity has its own card limits (Sharp edge 5 there, 3 elsewhere; Heavy push stays at 2)");

        // Infinity-only synergy tiers: 6 defense points switch on Last stand only in Infinity.
        var defense = new Dictionary<string, int> { ["thick_fur"] = 2, ["big_heart"] = 2, ["regrowth"] = 2 };
        var inEndless = new BattleSim(data, 0, 1600, seed: 1, stage: data.Endless.Stage(), build: defense);
        var inAdventure = new BattleSim(data, 0, 1600, seed: 1, stage: data.Campaign.Chapters[0].Stages[1], build: defense);
        check(inEndless.Synergies.Select(y => y.Id).SequenceEqual(new[] { "thorns", "last_stand" }) && inAdventure.Synergies.Select(y => y.Id).SequenceEqual(new[] { "thorns" }),
            $"6 defense points: Thorns and Last stand in Infinity, only Thorns elsewhere ({string.Join(",", inEndless.Synergies.Select(y => y.Id))})");

        // Last stand: once a wave a fatal hit leaves 1 HP.
        var stand = new BattleSim(Data(synergies: new[] { new SynergySpec { Id = "ls", Group = "defense", At = 1, Effect = "lastStand", Amount = 1, EndlessOnly = true, NameVi = "a", NameEn = "a", DescVi = "a", DescEn = "a" } },
            cards: new[] { Card("d", "armor", 0.01, "defense") }, kinds: new[] { Kind(damage: 500, speed: 80) }, catDamage: 0), 0, 1600, seed: 1, stage: Endless(), build: new Dictionary<string, int> { ["d"] = 1 });
        Walk(stand, 20, () => stand.LastStands > 0 || stand.Over);
        check(stand.LastStands == 1 && stand.CatHp == 1 && !stand.Over, $"Last stand: a fatal hit leaves the cat at 1 HP ({stand.CatHp})");
        Walk(stand, 20, () => stand.Over);
        check(stand.Over && stand.LastStands == 1, "...once a wave: the next fatal hit ends the battle");

        // Claws (crit 100 % here): every hit deals double.
        var crit = new BattleSim(Data(cards: new[] { Card("c", "crit", 1) }), 0, 1600, seed: 1, stage: Plain(), build: new Dictionary<string, int> { ["c"] = 1 });
        Walk(crit, 20, () => crit.Slimes.Count > 0 && crit.Slimes[0].Hp <= 1000 - 20);
        check(crit.Crits >= 1 && crit.Slimes[0].MaxHp - crit.Slimes[0].Hp == 20, $"Claws: a crit hit deals ×2 ({crit.Slimes[0].MaxHp - crit.Slimes[0].Hp})");
        check(crit.Effects.Any(e => e.Kind == BattleEffectKind.Number && e.Value > 0 && e.Tone == NumberTone.Crit), "...and its damage number reads as a crit (gold, \"!\")");

        // Finishing blow (+100 % here) below 30 % HP.
        var exec = new BattleSim(Data(cards: new[] { Card("e", "execute", 1) }), 0, 1600, seed: 1, stage: Plain(), build: new Dictionary<string, int> { ["e"] = 1 });
        Walk(exec, 5, () => exec.Slimes.Count > 0 && exec.Slimes[0].Targetable);
        exec.Slimes[0].Hp = 200;
        Walk(exec, 20, () => exec.Slimes[0].Hp < 200);
        check(exec.ExecuteHits >= 1 && exec.Slimes[0].Hp == 180, $"Finishing blow: a hit on an enemy below 30 % HP deals more ({200 - exec.Slimes[0].Hp})");

        // Regrowth: HP back while fighting, never past max.
        var regen = new BattleSim(Data(cards: new[] { Card("r", "regen", 10, "defense") }), 0, 1600, seed: 1, stage: Plain(), build: new Dictionary<string, int> { ["r"] = 1 });
        regen.Step(dt, 800, false);
        typeof(BattleSim).GetProperty("CatHp")!.SetValue(regen, 50.0);
        Walk(regen, 2);
        check(regen.CatHp > 60 && regen.CatHp <= regen.CatMaxHp, $"Regrowth gives HP back while fighting ({regen.CatHp:0})");

        // Steady (−50 % here): a dash stuns half as long.
        var dasher = new EnemyType
        {
            Id = "slime", MaxHp = 1000, Speed = 70, Damage = 1, Range = 54, Windup = 0.7, AttackCooldown = 2.6, Recover = 0.4, Scale = 0.75,
            Dash = true, DashRange = 170, DashDistance = 230, DashSpeed = 420
        };
        var steady = new BattleSim(Data(cards: new[] { Card("s", "stunCut", 0.5, "defense") }, kinds: new[] { dasher }, catDamage: 0), 0, 1600, new Random(3), stage: Plain(), build: new Dictionary<string, int> { ["s"] = 1 });
        for (int i = 0; i < 1200 && !steady.CatStunned; i++) steady.Step(dt, 800, false);
        check(steady.CatStunned && Math.Abs(steady.CatStunRemaining - 0.75) < 0.05, $"Steady: the dash stun lasts half as long ({steady.CatStunRemaining:0.00} s)");

        // Utility cards: skills, bomb, Shield.
        var util = new BattleSim(Data(cards: new[] { Card("q", "skillCooldown", 0.1, null), Card("b", "bomb", 0.5, null), Card("t", "shieldTime", 1, null) }), 0, 1600, seed: 1, stage: Plain(),
            build: new Dictionary<string, int> { ["q"] = 1, ["b"] = 1, ["t"] = 1 });
        Walk(util, 5, () => util.Slimes.Count > 0 && util.Slimes[0].Targetable);
        check(Math.Abs(util.SkillCooldown(BattleSkill.Heal) - util.Settings.HealCooldown * 0.9) < 1e-9, "Quick hands: −10 % on every skill's cooldown");
        util.Use(BattleSkill.Shield, 800);
        check(Math.Abs(util.ShieldRemaining - (util.Settings.ShieldSeconds + 1)) < 1e-9, "Thick bubble: the Shield lasts 1 s longer");
        var target = util.Slimes[0];
        util.Use(BattleSkill.Bomb, target.Id, 800);
        double before = target.Hp;
        Walk(util, 0.7);
        check(before - target.Hp >= util.Settings.BombDamage * 1.5 - 1e-6, $"Big bomb: the yarn bomb hits harder ({before - target.Hp:0})");

        // App cards only where there are apps.
        bool appCards = false;
        for (int seed = 1; seed <= 30 && !appCards; seed++)
        {
            var offerSim = new BattleSim(data, 0, 1600, seed: seed, stage: data.Campaign.Chapters[0].Stages[1], apps: false);
            appCards |= offerSim.OfferCards()?.Any(u => u.NeedsApps) == true;
        }
        check(!appCards, "Power bank and Energy drain are never offered with the apps turned off");
        bool utilityOffered = false;
        for (int seed = 1; seed <= 30 && !utilityOffered; seed++)
            utilityOffered |= new BattleSim(data, 0, 1600, seed: seed, stage: data.Campaign.Chapters[0].Stages[1]).OfferCards()?.Any(u => u.Group == null && u.Effect != "heal") == true;
        check(utilityOffered, "A utility card turns up in some offers");

        // A boss wave cleared with BossBonusPick: two offers in a row.
        var king = new EnemyType { Id = "king", MaxHp = 1, Speed = 30, Damage = 0, Range = 110, Windup = 1.2, AttackCooldown = 2.6, Boss = true, Slam = true, Scale = 2 };
        var bossStage = new StageSpec
        {
            Id = "boss_bonus", Objective = StageSpec.EndlessObjective, Upgrades = true, BossBonusPick = true,
            Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "king" } } }, new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime" } } } }
        };
        var bonus = new BattleSim(Data(kinds: new[] { Kind(), king }, catDamage: 50), 0, 1600, seed: 1, stage: bossStage);
        Walk(bonus, 30, () => bonus.ChoosingUpgrade);
        bool first = bonus.ChoosingUpgrade && bonus.ChooseUpgrade(bonus.UpgradeOffer![0].Id, 800);
        bool second = bonus.ChoosingUpgrade && bonus.ChooseUpgrade(bonus.UpgradeOffer![0].Id, 800);
        check(first && second && !bonus.ChoosingUpgrade && bonus.Picks == 2, "A cleared boss wave in Infinity gives two cards, one after the other");

        // From allSynergiesAt every synergy is on.
        var netStage = new StageSpec
        {
            Id = "net_test", Objective = StageSpec.EndlessObjective, AllSynergiesAt = 2,
            Waves = Enumerable.Range(0, 3).Select(_ => new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime" } } }).ToArray()
        };
        var net = new BattleSim(Data(kinds: new[] { Kind(hp: 10) }), 0, 1600, seed: 1, stage: netStage);
        Walk(net, 30, () => net.Wave >= 2);
        check(net.Wave == 2 && net.Synergies.Count == net.Settings.SynergyList.Count, $"From wave allSynergiesAt every synergy is on ({net.Synergies.Count}/{net.Settings.SynergyList.Count})");

        // Infinity's elites: the chance grows with the waves, and more may be alive.
        var affix = new EliteSpec { Hp = 1.2, DescVi = "a", DescEn = "a" };
        var growing = new StageSpec
        {
            Id = "elite_growth", Objective = StageSpec.EndlessObjective, EliteChance = 0.0001, EliteChancePerWave = 1, EliteChanceMax = 1, MoreElitesEvery = 1, MoreElitesMax = 2,
            Waves = Enumerable.Range(0, 4).Select(_ => new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 3 } } }).ToArray()
        };
        var elites = new BattleSim(new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, WaveBreakSeconds = 0.3, CatDamage = 0, MaxAlive = 6, Enemies = new[] { Kind(elite: affix) } },
            0, 1600, seed: 1, stage: growing);
        Walk(elites, 5, () => elites.Slimes.Count(s => s.Targetable) >= 3);
        int wave1 = elites.Slimes.Count(s => s.Elite != null);
        foreach (var s in elites.Slimes) { s.Hp = 0; s.Set(SlimeState.Defeated); }
        Walk(elites, 8, () => elites.Wave >= 2 && elites.Slimes.Count(s => s.Targetable) >= 3);
        int wave2 = elites.Slimes.Count(s => s.Targetable && s.Elite != null);
        check(wave1 == 0 && wave2 >= 2, $"Infinity: from wave 2 the elite chance is high and more than maxElites may be alive ({wave1} → {wave2})");

        // Shockwave, Gale, Finisher and Fortress (at 1 point here).
        SynergySpec Syn(string effect, double amount) => new() { Id = effect.ToLowerInvariant(), Group = "power", At = 1, Effect = effect, Amount = amount, NameVi = "a", NameEn = "a", DescVi = "a", DescEn = "a" };
        var pack = new BattleSim(Data(cards: new[] { Card("p", "damage", 0.01) }, synergies: new[] { Syn("comboStun", 0.6), Syn("rush", 0.2) }), 0, 1600, seed: 1, stage: Plain(), build: new Dictionary<string, int> { ["p"] = 1 });
        double baseMove = pack.MoveFactor;
        Walk(pack, 20, () => pack.CombosLanded > 0);
        check(pack.ComboRushRemaining > 2.9 && pack.MoveFactor > baseMove * 1.19 && pack.Slimes[0].State == SlimeState.Stunned,
            "Shockwave stuns the combo's target; Gale makes the cat faster for 3 s");
        var finish = new BattleSim(Data(cards: new[] { Card("p", "damage", 0.01) }, synergies: new[] { Syn("finisher", 0.5) }), 0, 1600, seed: 1, stage: Plain(), build: new Dictionary<string, int> { ["p"] = 1 });
        Walk(finish, 5, () => finish.Slimes.Count > 0 && finish.Slimes[0].Targetable);
        finish.Slimes[0].Hp = 450;
        Walk(finish, 10, () => finish.Finishers > 0);
        check(finish.Finishers == 1 && finish.Slimes[0].State == SlimeState.Defeated, "Finisher ends an enemy left below its share of HP");
        var fort = new BattleSim(Data(cards: new[] { Card("p", "damage", 0.01) }, synergies: new[] { Syn("autoShield", 30) }, kinds: new[] { Kind(damage: 5, speed: 80) }, catDamage: 0),
            0, 1600, seed: 1, stage: Plain(), build: new Dictionary<string, int> { ["p"] = 1 });
        fort.Step(dt, 800, false);
        typeof(BattleSim).GetProperty("CatHp")!.SetValue(fort, 25.0);
        fort.Step(dt, 800, false);
        double shield = fort.ShieldRemaining;
        Walk(fort, 4);
        check(fort.AutoShields == 1 && shield > 2.9, "Fortress: below 30 % HP the Shield goes up by itself, once per 30 s");

        // The shipped data: Infinity's cards and tiers.
        check(data.UpgradeList.Count(u => u.EndlessMaxStacks != null) == 6 && data.SynergyList.Count(y => y.EndlessOnly) == 6 && data.UtilityChance == 0.25,
            "battle.json: six cards have higher Infinity limits, six Infinity-only synergies, utility cards 25 % of offers");
    }
}
