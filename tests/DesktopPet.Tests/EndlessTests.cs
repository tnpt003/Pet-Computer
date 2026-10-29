using DesktopPet;

// Infinity (2026-10-22): the waves EndlessSpec makes (size, threat, kinds joining, the boss), a
// battle that is never won (cards every few waves, End as a loss at the wave reached), its
// records, and the data checks. BattleSim only, no window.
internal static class EndlessTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        var spec = BattleData.Settings.Endless;
        check(spec is { UpgradeEvery: 2, BossEvery: 10, Boss: "king_c1", LateBoss: "king", LateBossFrom: 40, BossBonusPick: true, AllSynergiesAt: 48, UnlockAfter: "c1_slime_king", RulesVersion: 6 } && spec.Kinds.Length == 4,
            "Built-in Infinity: a card every 2 waves (two after a boss), bosses every 10 (the plain King beside them from 40), every synergy from 48, 4 kinds"); 
        var first = spec!.Wave(1);
        check(first.Groups.Single() is { Count: 3, Pick: { Count: 1 } pick } && pick.ContainsKey("slime") && first.Threat == 1, "Wave 1: 3 plain slimes at threat 1");
        check(spec.Wave(3).Groups[0].Pick!.ContainsKey("runner") && !spec.Wave(4).Groups[0].Pick!.ContainsKey("mender") && spec.Wave(7).Groups[0].Pick!.Count == 4,
            "Kinds join the draw from their wave (quick slimes 3, herb slimes 5, rock slimes 7)");
        check(spec.Wave(10).Groups.Single() is { Enemy: "king_c1", Count: 1, Pick: null } && spec.Wave(20).Groups.Length == 1 && spec.IsBossWave(20) && !spec.IsBossWave(11)
              && spec.Wave(40).Groups.Select(g => g.Enemy).SequenceEqual(new[] { "king_c1", "king" }),
            "Every 10th wave is a boss alone (the chapter King first, EndlessMutationTests: the rotation); from wave 40 the plain King comes with it");
        check(spec.Count(2) == 3 && spec.Count(5) == 4 && spec.Count(100) == 14 && spec.Threat(11) > spec.Threat(2), "Waves grow (capped at 14 enemies) and get stronger");
        var stage = spec.Stage();
        check(stage is { Endless: true, Survival: false, Upgrades: true, UpgradeEvery: 2, BossBonusPick: true, AllSynergiesAt: 48, MoreElitesEvery: 20 } && stage.Waves.Length == EndlessSpec.PlannedWaves,
            "Its stage: endless, cards every 2 waves, Infinity's elite and synergy rules"); 

        // A quick battle: one-hit slimes and boss, cards every 2 waves, the boss every 4.
        var quick = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 1, BossMaxHp = 1, SlimeDamage = 0, WaveBreakSeconds = 0.3, OutroSeconds = 0.3 };
        var small = new EndlessSpec { UpgradeEvery = 2, BossEvery = 4, FirstCount = 1, CountPerWave = 0, Kinds = new[] { new EndlessKind { Enemy = "slime", Weight = 1 } } };
        EndlessSpec.Validate(small, quick);
        var sim = new BattleSim(quick, 0, 1600, new Random(5), stage: small.Stage());
        double cat = 800;
        bool offeredEarly = false;
        void Until(Func<bool> done, double seconds = 60)
        {
            for (double t = 0; t < seconds && !done(); t += dt)
            {
                var order = sim.Step(dt, cat, false);
                if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -72 * 1.8 * dt, 72 * 1.8 * dt);
            }
        }
        check(sim.Endless && sim.WaveCount == EndlessSpec.PlannedWaves, "The battle knows it is Infinity");
        Until(() => sim.ChoosingUpgrade || sim.Over);
        offeredEarly = sim.ChoosingUpgrade && sim.Wave < 2;
        check(!offeredEarly && sim.ChoosingUpgrade && sim.Wave == 2, $"No card after wave 1; one after wave 2 (wave {sim.Wave})");
        sim.ChooseUpgrade(sim.OfferCards()![0].Id);
        Until(() => sim.Wave == 4 && sim.Phase == BattlePhase.Fighting || sim.Over || sim.ChoosingUpgrade && sim.Wave != 4 && sim.Wave != 2);
        check(sim.IsBossWave(4) && sim.Wave == 4, "Wave 4 is the boss's");
        for (int guard = 0; guard < 20 && !(sim.Wave >= 9 || sim.Over); guard++)
        {
            Until(() => sim.ChoosingUpgrade || sim.Wave >= 9 || sim.Over);
            if (sim.ChoosingUpgrade) sim.ChooseUpgrade(sim.OfferCards()![0].Id);
        }
        check(sim.Wave >= 9 && !sim.Over && sim.Picks >= 3, $"It goes on past the boss and never ends in a win (wave {sim.Wave}, {sim.Picks} cards)");
        check(sim.WavesCleared == (sim.Phase == BattlePhase.WaveBreak ? sim.Wave : sim.Wave - 1), $"Waves cleared are counted ({sim.WavesCleared} by wave {sim.Wave})");
        int reached = sim.Wave;
        check(sim.Surrender() && sim.Phase == BattlePhase.Lost && sim.Wave == reached && !sim.Surrender(), "End: a loss at the wave reached (once)");
        var plain = new BattleSim(quick, 0, 1600, new Random(5));
        check(!plain.Surrender() && !plain.Endless, "Other battles do not end that way");

        // Records: the furthest wave per loadout, apart from the free battle's.
        var records = new BattleRecords();
        var news = records.AddEndless(spec, BattleWeapon.Sword, BattleDifficulty.Normal, false, 7, 20);
        check(news is { FurthestWave: true, MostDefeated: true, Fastest: false } && news.Any, "A first run sets both records");
        check(!records.AddEndless(spec, BattleWeapon.Sword, BattleDifficulty.Normal, false, 5, 10).Any, "A shorter run sets none");
        check(records.AddEndless(spec, BattleWeapon.Sword, BattleDifficulty.Normal, false, 9, 12) is { FurthestWave: true, MostDefeated: false }, "Further, fewer slimes: only the wave");
        records.AddEndless(spec, BattleWeapon.Hammer, BattleDifficulty.Hard, true, 40, 99);
        check(records.ForEndless(spec, BattleWeapon.Sword, BattleDifficulty.Normal).MostWaves == 9 && records.EndlessBest(spec) == 9
              && records.For(BattleWeapon.Sword, BattleDifficulty.Normal).Empty, "Kept per loadout; the hub's best leaves custom data out; the free battle's records untouched");
        string dir = Path.Combine(Path.GetTempPath(), "pet-endless-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "battle-records.json");
            check(records.Save(file) && BattleRecords.Load(file).ForEndless(spec, BattleWeapon.Sword, BattleDifficulty.Normal).MostWaves == 9, "Saved and read back");
        }
        finally { Directory.Delete(dir, true); }

        // Milestone rewards (2026-10-22): earned from the waves cleared over every loadout and rules version.
        check(spec.Milestones.Select(m => m.Wave).SequenceEqual(new[] { 10, 20, 30, 40, 50 }) &&
              spec.Milestones.Count(m => m.Kind == KeepsakeSpec.AccessoryKind && PetAccessories.Find(m.Accessory) is { Reward: "endless" }) == 3 &&
              spec.Milestones.Count(m => m.Shown) == 2 && spec.MilestoneAt(20)?.Kind == KeepsakeSpec.Hourglass && spec.MilestoneAt(40)?.Kind == KeepsakeSpec.Orb,
            "Built-in milestones: waves 10–50, three accessories and two displayed keepsakes (hourglass, orb)");
        var cleared = new BattleRecords();
        check(cleared.EndlessCleared(spec) == 0 && !spec.EarnedBy(0).Any(), "Nothing played: nothing earned");
        cleared.Loadouts[BattleRecords.EndlessKey(spec, BattleWeapon.Sword, BattleDifficulty.Easy)] = new BattleRecord { MostWaves = 11 };
        check(cleared.EndlessCleared(spec) == 10 && spec.EarnedBy(10).Single().Id == "infinity_collar", "A record from before: lost at wave 11 means wave 10 was cleared");
        cleared.AddEndless(spec, BattleWeapon.Hammer, BattleDifficulty.Hard, false, 21, 50, 21);
        cleared.AddEndless(spec, BattleWeapon.Hammer, BattleDifficulty.Hard, true, 60, 99, 60);
        cleared.Loadouts["endless|r9|" + BattleRecords.Key(BattleWeapon.Sword, BattleDifficulty.Hard)] = new BattleRecord { MostWaves = 30, MostCleared = 30 };
        check(cleared.EndlessCleared(spec) == 30 && spec.EarnedBy(30).Count() == 3 && cleared.ForEndless(spec, BattleWeapon.Hammer, BattleDifficulty.Hard).MostCleared == 21,
            "Ended in a break: all its waves count; other rules versions count; custom data does not");
        cleared.AddEndless(spec, BattleWeapon.Hammer, BattleDifficulty.Hard, false, 8, 5, 7);
        check(cleared.ForEndless(spec, BattleWeapon.Hammer, BattleDifficulty.Hard).MostCleared == 21, "A shorter run keeps the most waves cleared");

        // Data checks (the whole file is refused with the reason).
        string builtIn = ExpeditionFixture.Embedded();
        string Error(string endless)
        {
            try { BattleData.ParseWithOverride(builtIn, "{ \"schemaVersion\": 1, \"endless\": " + endless + " }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        check(Error("{ \"upgradeEvery\": 5 }") == "", "A user battle.json can change Infinity's numbers");
        check(Error("{ \"boss\": \"slime\" }").Contains("boss"), "A boss that is not a boss is rejected");
        check(Error("{ \"kinds\": [ { \"enemy\": \"runner\", \"weight\": 1, \"from\": 2 } ] }").Contains("wave 1"), "Kinds with nothing from wave 1 are rejected");
        check(Error("{ \"kinds\": [ { \"enemy\": \"king\", \"weight\": 1 } ] }").Contains("not bosses"), "The boss in the draw is rejected");
        check(Error("{ \"maxCount\": 0 }").Contains("maxCount"), "maxCount 0 is rejected");
        const string Gift = "\"nameVi\": \"a\", \"nameEn\": \"a\"";
        check(Error("{ \"milestones\": [ { \"id\": \"a\", \"wave\": 5, \"kind\": \"orb\", " + Gift + " }, { \"id\": \"b\", \"wave\": 5, \"kind\": \"orb\", " + Gift + " } ] }").Contains("grow") &&
              Error("{ \"milestones\": [ { \"id\": \"cup_gold\", \"wave\": 5, \"kind\": \"orb\", " + Gift + " } ] }").Contains("used twice") &&
              Error("{ \"milestones\": [ { \"id\": \"a\", \"wave\": 5, \"kind\": \"cup\", \"tier\": \"gold\", " + Gift + " } ] }").Contains("kind") &&
              Error("{ \"milestones\": [ { \"id\": \"a\", \"wave\": 5, \"kind\": \"accessory\", \"accessory\": \"Bell\", " + Gift + " } ] }").Contains("reward accessory") &&
              Error("{ \"milestones\": [ { \"id\": \"a\", \"wave\": 5, \"kind\": \"orb\", \"nameVi\": \"\", \"nameEn\": \"a\" } ] }").Contains("names") &&
              Error("{ \"milestones\": [] }") == "",
            "Bad milestones are rejected: waves not growing, an id the Expedition uses, a cup, a plain accessory, no name");
    }
}
