using DesktopPet;

// 1.5 M24: Infinity's boss rotation and stretches (newer kinds from wave 15, at most two kinds of link
// and two the cursor beats in each), and "Mutations" (an odd rule picked after each cleared boss wave,
// held until the next one is cleared, then its reward). BattleSim only, no window.
internal static class EndlessMutationTests
{
    public static void Run(Action<bool, string> check)
    {
        var spec = BattleData.Settings.Endless!;
        check(spec.RulesVersion == 6 && spec.LateBossFrom == 40 && spec.Bosses is { Length: 3 },
            "Built-in Infinity is rules 6: three bosses in turn, the plain King beside them from wave 40");
        string Bosses(int wave) => string.Join("+", spec.Wave(wave).Groups.Select(g => g.Enemy));
        check(Bosses(10) == "king_c1" && Bosses(20) == "zip" && Bosses(30) == "mutant_king" && Bosses(40) == "king_c1+king" && Bosses(50) == "zip+king" && Bosses(60) == "mutant_king+king",
            $"Boss waves take turns: King, Slime Zip, Mutant King, round again ({Bosses(10)}, {Bosses(20)}, {Bosses(30)}, {Bosses(40)}, {Bosses(50)})");
        check(spec.Eras.Length == 4 && spec.EraIndex(14) == -1 && spec.EraIndex(15) == 0 && spec.EraIndex(20) == 0 && spec.EraIndex(21) == 1
              && spec.EraIndex(31) == 2 && spec.EraIndex(41) == 3 && spec.EraIndex(51) == 0 && spec.EraIndex(91) == 0,
            "Stretches: the first from wave 15, the next after each boss wave, round again after the fourth");
        check(spec.EraStarts(15) && spec.EraStarts(21) && spec.EraStarts(51) && !spec.EraStarts(16) && !spec.EraStarts(11) && !spec.EraStarts(30),
            "A stretch's first wave is known (its banner)");
        var plain = spec.Wave(14).Groups[0].Pick!;
        var marsh = spec.Wave(15).Groups[0].Pick!;
        var band = spec.Wave(25).Groups[0].Pick!;
        check(plain.Count == 4 && marsh.ContainsKey("spitter") && marsh.ContainsKey("slime") && marsh.Count == 8 && band.ContainsKey("drummer") && !band.ContainsKey("spitter"),
            "Wave 14 draws the four old kinds; a stretch adds its own kinds to them (and drops the last stretch's)");
        double share = spec.Eras.Min(era => era.Kinds.Sum(k => k.Weight) / (era.Kinds.Sum(k => k.Weight) + spec.Kinds.Sum(k => k.Weight)));
        check(share is > 0.3 and < 0.5, $"Newer kinds are 30–50% of each stretch's draw (least {share:P0})");
        // The whole new roster comes round: every 1.5 enemy that is not a boss, a shard, a half or a giant.
        var newer = spec.Eras.SelectMany(e => e.Kinds.Select(k => k.Enemy)).ToHashSet();
        var roster = new[] { "spitter", "springer", "drummer", "chain", "fuser", "bomber", "splitter", "flagbearer", "phantom", "bubble", "mirage", "sulky",
            "flyer", "burrower", "icy", "clinger", "hedgehog", "timer", "thief" };
        check(roster.All(newer.Contains), "Every new enemy of 1.5 shows up in some stretch: " + string.Join(", ", roster.Where(k => !newer.Contains(k))));
        var settings = BattleData.Settings;
        foreach (var (era, i) in spec.Eras.Select((e, i) => (e, i)))
        {
            var kinds = spec.Kinds.Select(k => k.Enemy).Concat(era.Kinds.Select(k => k.Enemy)).Distinct().Select(settings.Enemy).ToList();
            int links = kinds.Select(k => k.ActiveLink?.Kind).Where(k => k != null).Distinct().Count(), mouse = kinds.Count(k => k.UsesMouse);
            check(links <= 2 && mouse <= 2, $"Stretch {i} ({era.NameEn}): {links} kinds of link, {mouse} cursor kinds (at most 2 each)");
        }

        // Data checks.
        string builtIn = ExpeditionFixture.Embedded();
        string Error(string endless)
        {
            try { BattleData.ParseWithOverride(builtIn, "{ \"schemaVersion\": 1, \"endless\": " + endless + " }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        check(Error("{ \"bosses\": [ \"zip\", \"slime\" ] }").Contains("bosses"), "A rotation naming a plain enemy is rejected");
        check(Error("{ \"eras\": [ { \"nameVi\": \"a\", \"nameEn\": \"a\", \"kinds\": [ { \"enemy\": \"phantom\", \"weight\": 1 }, { \"enemy\": \"bubble\", \"weight\": 1 }, { \"enemy\": \"mirage\", \"weight\": 1 } ] } ] }").Contains("cursor"),
            "A stretch with three cursor kinds is rejected");
        check(Error("{ \"eras\": [ { \"nameVi\": \"a\", \"nameEn\": \"a\", \"kinds\": [ { \"enemy\": \"drummer\", \"weight\": 1 }, { \"enemy\": \"chain\", \"weight\": 1 }, { \"enemy\": \"flagbearer\", \"weight\": 1 } ] } ] }").Contains("link"),
            "A stretch with three kinds of link is rejected");
        check(Error("{ \"eras\": [ { \"nameVi\": \"a\", \"nameEn\": \"a\", \"kinds\": [ { \"enemy\": \"zip\", \"weight\": 1 } ] } ] }").Contains("not bosses")
              && Error("{ \"eras\": [ { \"nameVi\": \"a\", \"nameEn\": \"a\", \"kinds\": [ { \"enemy\": \"icy\", \"weight\": 1, \"from\": 3 } ] } ] }").Contains("from"),
            "A boss in a stretch, or a stretch kind with its own \"from\", is rejected");
        const string Words = "\"nameVi\": \"a\", \"nameEn\": \"a\", \"descVi\": \"a\", \"descEn\": \"a\", \"rewardVi\": \"a\", \"rewardEn\": \"a\"";
        string Rules(string first) => "{ \"mutations\": { \"offer\": 1, \"list\": [ " + first + ", { \"id\": \"m2\", \"effect\": \"hungry\", \"reward\": \"heal\", " + Words + " } ] } }";
        check(Error(Rules("{ \"id\": \"m1\", \"effect\": \"glass\", \"amount\": 0.3, \"reward\": \"damage\", \"rewardAmount\": 0.3, " + Words + " }")) == "", "A user battle.json can set its own mutations");
        check(Error(Rules("{ \"id\": \"m1\", \"effect\": \"fog\", \"amount\": 0.3, \"reward\": \"heal\", " + Words + " }")).Contains("effect")
              && Error(Rules("{ \"id\": \"m1\", \"effect\": \"glass\", \"amount\": 3, \"reward\": \"heal\", " + Words + " }")).Contains("amount")
              && Error(Rules("{ \"id\": \"m1\", \"effect\": \"glass\", \"amount\": 0.3, \"reward\": \"none\", " + Words + " }")).Contains("reward")
              && Error(Rules("{ \"id\": \"sharp\", \"effect\": \"glass\", \"amount\": 0.3, \"reward\": \"heal\", " + Words + " }")).Contains("used twice")
              && Error(Rules("{ \"id\": \"m1\", \"effect\": \"swift\", \"amount\": 0.3, \"extra\": 2, \"reward\": \"heal\", " + Words + " }")).Contains("extra")
              && Error("{ \"mutations\": { \"offer\": 3, \"list\": [ { \"id\": \"m2\", \"effect\": \"hungry\", \"reward\": \"heal\", " + Words + " } ] } }").Contains("list"),
            "Bad mutations are rejected: an unknown effect, too strong, no reward, a card's id, extra on a non-crowd rule, too few to offer");

        // The flow, on a quick battle: one-hit slimes, a boss every 2 waves, cards after each boss wave.
        var quick = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 1, BossMaxHp = 1, SlimeDamage = 0, WaveBreakSeconds = 0.3, OutroSeconds = 0.3 };
        MutationSpec Rule(string id, string effect, double amount, string reward, double rewardAmount = 0, int cards = 0, int extra = 0) => new()
        {
            Id = id, Effect = effect, Amount = amount, Reward = reward, RewardAmount = rewardAmount, Cards = cards, Extra = extra,
            NameVi = id, NameEn = id, DescVi = id, DescEn = id, RewardVi = id, RewardEn = id
        };
        EndlessSpec Small(params MutationSpec[] rules) => new()
        {
            UpgradeEvery = 2, BossEvery = 2, FirstCount = 2, CountPerWave = 0, Kinds = new[] { new EndlessKind { Enemy = "slime", Weight = 1 } },
            Mutations = new MutationRules { Offer = 1, List = rules }
        };
        const double dt = 1.0 / 60;
        (BattleSim Sim, List<string> Offers) Play(EndlessSpec small, int seed, Func<BattleSim, bool> stop, Func<IReadOnlyList<UpgradeSpec>, string>? choose = null, BattleSim? going = null)
        {
            var sim = going ?? new BattleSim(quick, 0, 1600, new Random(seed), stage: small.Stage());
            var offers = new List<string>();
            double cat = 800;
            for (double t = 0; t < 120 && !stop(sim) && !sim.Over; t += dt)
            {
                if (sim.ChoosingUpgrade)
                {
                    offers.Add(sim.ChoosingMutation ? "mutation" : "card");
                    sim.ChooseUpgrade(choose?.Invoke(sim.UpgradeOffer!) ?? sim.UpgradeOffer![0].Id, cat);
                    continue;
                }
                var order = sim.Step(dt, cat, false);
                if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -72 * 1.8 * dt, 72 * 1.8 * dt);
            }
            return (sim, offers);
        }
        var hp = Rule("m_hp", MutationSpec.Hungry, 0, MutationSpec.MaxHpReward, 20, cards: 1);
        var glass = Rule("m_glass", MutationSpec.Glass, 0.3, MutationSpec.DamageReward, 0.3);
        var small = Small(hp, glass);
        EndlessSpec.Validate(small, quick);
        // Wave 2 (a boss wave) cleared: its card first, then the mutation; the rule is no card of the build.
        var (sim, offers) = Play(small, 3, s => s.Mutation != null);
        string first = sim.Mutation?.Id ?? "";
        check(offers.SequenceEqual(new[] { "card", "mutation" }) && sim.Picks == 1 && sim.Wave == 2 && sim.Stacks(first) == 0 && !sim.ChoosingUpgrade && sim.MutationWavesLeft == 2,
            $"After the first boss wave: a card, then one mutation (no card of the build), held for the 2 waves to come ({string.Join(", ", offers)}, {first}, {sim.MutationWavesLeft})");
        var other = first == "m_hp" ? "m_glass" : "m_hp";
        // Whichever came first, its effect holds now.
        double maxBefore = sim.CatMaxHp;
        (sim, offers) = Play(small, 3, s => s.Mutation?.Id == other, offer => (offer.FirstOrDefault(u => u.Effect != "maxHp") ?? offer[0]).Id, sim); // No Big heart: only the reward counts.
        check(sim.MutationsCleared == 1 && sim.Wave == 4 && sim.Mutation?.Id == other, $"Wave 4 cleared: the rule is over and the other one comes (the last one is never offered again at once) ({sim.Mutation?.Id})");
        if (first == "m_hp")
            check(sim.CatMaxHp == maxBefore + 20 && offers.SequenceEqual(new[] { "card", "card", "mutation" }), $"Fasting paid: +20 max HP and one more card ({string.Join(", ", offers)})");
        else
            check(sim.CatMaxHp == maxBefore && offers.SequenceEqual(new[] { "card", "mutation" }) && sim.DamageFactor == 1,
                $"Glass cat over: its damage bonus goes with it ({string.Join(", ", offers)})");
        check(sim.Surrender() && sim.Phase == BattlePhase.Lost && sim.MutationsCleared == 1, "Ending the run in the middle of a rule gives nothing more");

        // Glass cat: both ways while it holds, gone with it.
        foreach (int seed in Enumerable.Range(1, 30))
        {
            var (s, _) = Play(small, seed, x => x.Mutation != null, offer => (offer.FirstOrDefault(u => u.Effect != "damage") ?? offer[0]).Id);
            if (s.Mutation?.Id != "m_glass") continue;
            bool during = Math.Abs(s.DamageFactor - 1.3) < 1e-9 && Math.Abs(s.HurtFactor - s.ArmorFactor * 1.3) < 1e-9;
            Play(small, seed, x => x.Mutation?.Id == "m_hp", offer => (offer.FirstOrDefault(u => u.Effect != "damage") ?? offer[0]).Id, s);
            check(during && s.DamageFactor == 1 && s.HurtFactor == s.ArmorFactor * 1, $"Glass cat: 30% more damage dealt and taken while it holds, none after ({s.DamageFactor})");
            break;
        }

        // Lazy skills: the skills recharge slower while it holds; then a full heal and a card.
        var lazy = Rule("m_lazy", MutationSpec.Lazy, 0.4, MutationSpec.HealReward, cards: 1);
        var crowd = Rule("m_crowd", MutationSpec.Crowd, 0.5, MutationSpec.NoReward, cards: 1, extra: 2);
        var mixed = Small(lazy, crowd);
        EndlessSpec.Validate(mixed, quick);
        var plainSim = new BattleSim(quick, 0, 1600, new Random(1), stage: mixed.Stage());
        foreach (int seed in Enumerable.Range(1, 30))
        {
            var (s, _) = Play(mixed, seed, x => x.Mutation != null);
            if (s.Mutation?.Id == "m_lazy")
            {
                check(Math.Abs(s.SkillCooldown(BattleSkill.Push) - plainSim.SkillCooldown(BattleSkill.Push) * 1.4) < 1e-9, "Lazy skills: 40% longer cooldowns while it holds");
                break;
            }
        }
        foreach (int seed in Enumerable.Range(1, 30))
        {
            var (s, _) = Play(mixed, seed, x => x.Mutation != null);
            if (s.Mutation?.Id != "m_crowd") continue;
            int before = s.Defeated;
            Play(mixed, seed, x => x.Wave == 3 && x.Phase == BattlePhase.WaveBreak, going: s);
            check(s.Defeated - before == 3, $"Packed floor: a wave of 2 brings 3 (50% more, rounded up) ({s.Defeated - before})");
            break;
        }

        // Simulation (NOT real play): the simple player on Normal with the sword, the new rules against
        // the old ones (no rotation, no stretches, no mutations, the plain King from 30). The newer
        // kinds and bosses make it harder, but not out of reach.
        var old = new EndlessSpec
        {
            Id = spec.Id, RulesVersion = 5, UpgradeEvery = spec.UpgradeEvery, BossEvery = spec.BossEvery, Boss = spec.Boss, LateBoss = spec.LateBoss, LateBossFrom = 30,
            BossBonusPick = spec.BossBonusPick, AllSynergiesAt = spec.AllSynergiesAt, EliteChancePerWave = spec.EliteChancePerWave, EliteChanceMax = spec.EliteChanceMax,
            MoreElitesEvery = spec.MoreElitesEvery, MoreElitesMax = spec.MoreElitesMax, FirstCount = spec.FirstCount, CountPerWave = spec.CountPerWave, MaxCount = spec.MaxCount,
            ThreatPerWave = spec.ThreatPerWave, MaxAlive = spec.MaxAlive, EliteChance = spec.EliteChance, Kinds = spec.Kinds
        };
        const int runs = 8;
        int[] Waves(EndlessSpec rules)
        {
            var reached = new int[runs];
            Parallel.For(0, runs, i => reached[i] = AdventureBalanceTests.PlayEndless(settings, rules, BattleWeapon.Sword, BattleDifficulty.Normal, 100 + i, AdventureBalanceTests.Player.Simple));
            return reached.OrderBy(w => w).ToArray();
        }
        var oldWaves = Waves(old);
        var newWaves = Waves(spec);
        int Median(int[] w) => w[w.Length / 2];
        Console.WriteLine($"Infinity simulation (simple, Normal, sword, {runs} runs, 20 min cap): old rules waves {string.Join(" ", oldWaves)} (median {Median(oldWaves)}), new rules {string.Join(" ", newWaves)} (median {Median(newWaves)})");
        check(Median(newWaves) >= 15 && Median(newWaves) >= Median(oldWaves) * 0.6, $"Simulated Infinity: the new rules still reach the newer kinds and at least 60% of the old median ({Median(newWaves)} vs {Median(oldWaves)})");

        // The built-in run: rare offers and the mutation come in order after a boss wave; picks always find an offer.
        var real = new BattleSim(settings, 0, 1600, new Random(2), stage: spec.Stage());
        check(real.Stage?.Infinity == spec && !real.ChoosingMutation && real.Mutation == null, "The battle knows its Infinity rules; no mutation before the first boss");
        var plainStage = new BattleSim(settings, 0, 1600, new Random(2), stage: settings.Campaign!.Chapters[0].Stages[0]);
        check(plainStage.Stage?.Infinity == null && plainStage.SkillCooldown(BattleSkill.Push) == settings.SkillCooldown(BattleSkill.Push), "Other battles have no mutations");
    }
}
