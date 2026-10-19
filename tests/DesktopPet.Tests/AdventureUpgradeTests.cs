using DesktopPet;

// Adventure milestone 3: upgrades between waves (offer, choice, limits, effects, reset) and the
// survive objective (its clock, repeated waves, win and loss). BattleSim only, no window.
internal static class AdventureUpgradeTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        var data = BattleData.Settings;
        var json = data.UpgradeList;
        check(json.Count == 6 && json.Select(u => u.Effect).Distinct().Count() == 6 && json.All(u => u.NameVi.Length > 0 && u.DescEn.Length > 0),
            "battle.json lists 6 upgrades, one per effect, named in both languages");
        var stages = data.Campaign!.Chapters[0].Stages;
        check(!stages[0].Upgrades && stages.Skip(1).All(s => s.Upgrades && s.RulesVersion == 2), "Stage 1 has no upgrades; stages 2–5 do (rules version 2)");
        check(stages[2] is { Survival: true, SurviveSeconds: 75 } && stages.Count(s => s.Survival) == 1, "Stage 3 (Hold on) is the survive stage: 75 s");

        WaveSpec[] Waves(int count) => Enumerable.Range(0, count).Select(_ => new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 } } }).ToArray();
        StageSpec Stage(int waves = 3, bool upgrades = true) => new() { Id = "t", Upgrades = upgrades, Waves = Waves(waves) };
        BattleSettings Quick(UpgradeSpec[]? list = null, double slimeHp = 1) =>
            new() { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = slimeHp, SlimeDamage = 0, WaveBreakSeconds = 0.5, OutroSeconds = 0.5, Upgrades = list };
        // Runs the cat toward its target until the predicate holds (or time runs out).
        double cat = 800;
        void Until(BattleSim sim, Func<bool> done, double seconds = 30, bool held = false)
        {
            for (double t = 0; t < seconds && !done(); t += dt)
            {
                var order = sim.Step(dt, cat, held);
                if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -72 * 1.8 * dt, 72 * 1.8 * dt);
            }
        }

        // The offer: after a cleared wave with another to come; the battle stands still meanwhile.
        cat = 800;
        var sim = new BattleSim(Quick(), 0, 1600, new Random(3), stage: Stage());
        Until(sim, () => sim.ChoosingUpgrade);
        var offer = sim.UpgradeOffer;
        check(offer is { Count: 3 } && offer.Select(u => u.Id).Distinct().Count() == 3 && sim.Wave == 1 && sim.Phase == BattlePhase.WaveBreak,
            "Clearing wave 1 of 3 offers 3 different upgrades");
        double time = sim.Time, left = sim.PhaseRemaining;
        Until(sim, () => false, 2);
        check(sim.Time == time && sim.PhaseRemaining == left && sim.Wave == 1 && !sim.Ready(BattleSkill.Heal) && sim.ChoosingUpgrade,
            "While choosing nothing moves: no clock (records), no wave break, no skills");
        check(!sim.ChooseUpgrade("not_offered") && !sim.ChooseUpgrade(json.First(u => offer!.All(o => o.Id != u.Id)).Id) && sim.ChoosingUpgrade,
            "Only an upgrade on offer can be chosen");
        string pick = offer![0].Id;
        check(sim.ChooseUpgrade(pick) && !sim.ChoosingUpgrade && sim.Stacks(pick) == 1 && !sim.ChooseUpgrade(pick) && sim.Stacks(pick) == 1,
            "Choosing takes it once; a second choice (double click) does nothing");
        Until(sim, () => sim.Wave == 2 && sim.Phase == BattlePhase.Fighting, 5);
        check(sim.Wave == 2 && sim.Time > time, "The battle goes on after the choice");
        Until(sim, () => sim.ChoosingUpgrade || sim.Over);
        check(sim.ChoosingUpgrade && sim.Wave == 2, "Wave 2 of 3 cleared: another offer");
        sim.ChooseUpgrade(sim.UpgradeOffer![0].Id);
        Until(sim, () => sim.Over || sim.ChoosingUpgrade);
        check(sim.Phase == BattlePhase.Won && !sim.ChoosingUpgrade && sim.UpgradeOffer == null, "No offer after the last wave: the stage is won");

        // No offer in a free battle or a stage without upgrades.
        cat = 800;
        var free = new BattleSim(new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 1, Waves = new[] { 1, 1 }, Boss = false, WaveBreakSeconds = 0.5 }, 0, 1600, new Random(3));
        bool offered = false;
        Until(free, () => { offered |= free.ChoosingUpgrade; return free.Over; });
        check(free.Phase == BattlePhase.Won && !offered, "A free battle never offers upgrades");
        cat = 800;
        var plain = new BattleSim(Quick(), 0, 1600, new Random(3), stage: Stage(upgrades: false));
        offered = false;
        Until(plain, () => { offered |= plain.ChoosingUpgrade; return plain.Over; });
        check(plain.Phase == BattlePhase.Won && !offered, "A stage without upgrades never offers them");

        // Limits: fewer than 3 left, none left, and no healing at full HP.
        UpgradeSpec Up(string id, string effect, double amount, int max) => new() { Id = id, NameVi = id, NameEn = id, DescVi = "", DescEn = "", Effect = effect, Amount = amount, MaxStacks = max };
        cat = 800;
        var two = new BattleSim(Quick(new[] { Up("a", "damage", 0.2, 1), Up("b", "heal", 30, 0), Up("c", "armor", 0.1, 1) }), 0, 1600, new Random(3), stage: Stage(4));
        Until(two, () => two.ChoosingUpgrade);
        check(two.UpgradeOffer is { Count: 2 } o2 && o2.All(u => u.Id != "b"), "Fewer cards when fewer fit; healing is not offered at full HP");
        two.ChooseUpgrade("a");
        Until(two, () => two.ChoosingUpgrade || two.Over);
        check(two.UpgradeOffer is { Count: 1 } o1 && o1[0].Id == "c", "An upgrade at its limit is not offered again");
        two.ChooseUpgrade("c");
        Until(two, () => two.ChoosingUpgrade || two.Wave == 4);
        check(!two.ChoosingUpgrade && two.Wave == 4, "Nothing left to offer: the next wave just starts");

        // Effects on this run's numbers; a new run starts without them and the data is unchanged.
        var all = new[] { Up("dmg", "damage", 0.2, 3), Up("run", "moveSpeed", 0.15, 2), Up("cd", "attackSpeed", 0.12, 3), Up("kb", "knock", 80, 2), Up("fur", "armor", 0.15, 2) };
        cat = 800;
        var buff = new BattleSim(Quick(all), 0, 1600, new Random(3), stage: Stage(6));
        foreach (var id in new[] { "dmg", "run", "cd", "kb", "fur" })
        {
            Until(buff, () => buff.ChoosingUpgrade);
            if (!buff.ChooseUpgrade(id)) { var ids = buff.UpgradeOffer!.Select(u => u.Id).ToList(); buff.ChooseUpgrade(ids[0]); }
        }
        check(buff.UpgradesTaken.Values.Sum() == 5, "Five upgrades taken over five offers");
        double expect(string id, double per) => 1 + buff.Stacks(id) * per;
        check(Math.Abs(buff.DamageFactor - expect("dmg", 0.2)) < 1e-9 && Math.Abs(buff.MoveFactor - expect("run", 0.15)) < 1e-9 &&
              Math.Abs(buff.CooldownFactor - (1 - buff.Stacks("cd") * 0.12)) < 1e-9 && Math.Abs(buff.ArmorFactor - (1 - buff.Stacks("fur") * 0.15)) < 1e-9 &&
              Math.Abs(buff.KnockBonus - buff.Stacks("kb") * 80) < 1e-9, "Each effect changes only this run's own numbers");
        var fresh = new BattleSim(Quick(all), 0, 1600, new Random(3), stage: Stage(6));
        check(fresh.UpgradesTaken.Count == 0 && fresh.DamageFactor == 1 && fresh.CooldownFactor == 1 && fresh.ArmorFactor == 1 && fresh.MoveFactor == 1 && fresh.KnockBonus == 0 &&
              all[0].Amount == 0.2 && data.Weapon(BattleWeapon.Sword).Damage == 25, "A new run (play again, next stage) starts with no upgrades; definitions never change");
        // Damage really grows: a sharp sword takes 25 × 1.2 = 30 HP off a sturdy slime.
        cat = 800;
        var sharp = new BattleSim(Quick(new[] { Up("dmg", "damage", 0.2, 3) }, slimeHp: 1), 0, 1600, new Random(3),
            stage: new StageSpec { Id = "s", Upgrades = true, Waves = new[] { Waves(1)[0], new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "king", Count = 1 } } } } });
        Until(sharp, () => sharp.ChoosingUpgrade);
        sharp.ChooseUpgrade("dmg");
        Until(sharp, () => sharp.Slimes.Any(s => s.IsBoss && s.Hp < s.MaxHp), 20);
        var king = sharp.Slimes.First(s => s.IsBoss);
        check(Math.Abs(king.MaxHp - king.Hp - 30) < 1e-6 || Math.Abs(king.MaxHp - king.Hp - 60) < 1e-6, $"Sharp edge: a sword hit deals 30 instead of 25 (took {king.MaxHp - king.Hp})");
        // Armor: a slime's 10 damage becomes 8.5 with Thick fur.
        cat = 800;
        var armored = new BattleSim(new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 1000, SlimeDamage = 10, WaveBreakSeconds = 0.5, WaveHeal = 0, CatDamage = 1,
            Upgrades = new[] { Up("fur", "armor", 0.15, 2) } }, 0, 1600, new Random(3),
            stage: new StageSpec { Id = "a", Upgrades = true, Waves = new[] { Waves(1)[0], Waves(1)[0] } });
        Until(armored, () => armored.Slimes.Count > 0, 2);
        armored.Slimes[0].Hp = 0.5; // The cat's next 1-damage hit ends wave 1; wave 2's slime lasts.
        Until(armored, () => armored.ChoosingUpgrade, 20);
        bool chose = armored.ChooseUpgrade("fur");
        double before = armored.CatHp;
        Until(armored, () => armored.CatHp < before, 20);
        check(chose && Math.Abs(before - armored.CatHp - 8.5) < 1e-6, $"Thick fur: a 10-damage hit takes 8.5 HP (took {before - armored.CatHp})");
        // A heal card at less than full HP gives HP back at once, never above the maximum.
        cat = 800;
        var hurt = new BattleSim(new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 1000, SlimeDamage = 40, WaveBreakSeconds = 0.5, WaveHeal = 0, CatDamage = 1,
            Upgrades = new[] { Up("fish", "heal", 30, 0) } }, 0, 1600, new Random(3), stage: new StageSpec { Id = "h", Upgrades = true, Waves = new[] { Waves(1)[0], Waves(1)[0] } });
        Until(hurt, () => hurt.CatHp < 100, 20);
        hurt.Slimes[0].Hp = 0.5;
        Until(hurt, () => hurt.ChoosingUpgrade || hurt.Over, 20);
        double low = hurt.CatHp;
        check(hurt.ChooseUpgrade("fish", 800) && Math.Abs(hurt.CatHp - Math.Min(100, low + 30)) < 1e-6, "Grilled fish gives up to 30 HP back at once");

        // Survive: the clock runs only with the cat on the ground; the last wave comes again; time up wins.
        var hold = new StageSpec { Id = "hold", Objective = "survive", SurviveSeconds = 12, MaxAlive = 2, Waves = Waves(2) };
        cat = 800;
        cat = 300;
        var survive = new BattleSim(Quick(slimeHp: 1), 0, 600, new Random(3), stage: hold); // A narrow floor: the cat reaches every slime quickly.
        check(survive.Survival && survive.SurviveRemaining == 12 && !free.Survival && double.IsNaN(free.SurviveRemaining), "A survive stage starts its clock; other battles have none");
        Until(survive, () => false, 3, held: true);
        check(survive.SurviveRemaining == 12, "Carrying the cat stops the survive clock");
        int most = 0;
        Until(survive, () => { most = Math.Max(most, survive.Alive); return survive.Over; }, 20);
        check(survive.Phase == BattlePhase.Won && survive.Wave > 2 && survive.SurviveRemaining == 0 && most <= 2 && survive.FightSeconds >= 12,
            $"Waves repeat until the time is up, then the stage is won (wave {survive.Wave}, at most {most} at once, {survive.Phase})");
        cat = 800;
        var doomed = new BattleSim(new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 100000, SlimeDamage = 60, LandingGraceSeconds = 0 }, 0, 1600, new Random(3),
            stage: new StageSpec { Id = "d", Objective = "survive", SurviveSeconds = 60, Waves = Waves(1) });
        Until(doomed, () => doomed.Over, 30);
        check(doomed.Phase == BattlePhase.Lost && doomed.SurviveRemaining > 0, "Losing all HP before the time is up loses a survive stage");
        var adventure = new Adventure(data.Campaign, new AdventureProgress());
        var outcome = adventure.Apply("r", stages[2], true, true, BattleWeapon.Sword, BattleDifficulty.Normal, 9, 75);
        check(outcome.Counted && !outcome.News.Fastest && adventure.Record(stages[2], BattleWeapon.Sword, BattleDifficulty.Normal).BestSeconds == null && outcome.News.MostDefeated,
            "A survive stage keeps no fastest win, only the most slimes");

        // Validation of the new data.
        string Error(string extra)
        {
            try { BattleData.Parse("{ \"schemaVersion\": 1, " + extra + " }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        string Camp(string stageExtra) => "\"campaign\": { \"chapters\": [ { \"id\": \"c\", \"nameVi\": \"C\", \"nameEn\": \"C\", \"stages\": [ { \"id\": \"s\", \"nameVi\": \"S\", \"nameEn\": \"S\", " +
            stageExtra + ", \"waves\": [ { \"groups\": [ { \"enemy\": \"slime\", \"count\": 1 } ] } ] } ] } ] }";
        check(Error(Camp("\"objective\": \"survive\", \"surviveSeconds\": 60, \"upgrades\": true")) == "", "A survive stage with upgrades is accepted");
        check(Error(Camp("\"objective\": \"survive\"")).Contains("surviveSeconds"), "A survive stage needs surviveSeconds");
        check(Error(Camp("\"objective\": \"survive\", \"surviveSeconds\": 5")).Contains("10–600"), "surviveSeconds outside 10–600 is rejected");
        check(Error(Camp("\"surviveSeconds\": 60")).Contains("only for a survive stage"), "surviveSeconds on a clear stage is rejected");
        string Upg(string body) => "\"upgrades\": [ { \"id\": \"x\", \"nameVi\": \"X\", \"nameEn\": \"X\", \"descVi\": \"d\", \"descEn\": \"d\", " + body + " } ]";
        check(Error(Upg("\"effect\": \"damage\", \"amount\": 0.3, \"maxStacks\": 2")) == "", "A valid custom upgrade list is accepted");
        check(Error(Upg("\"effect\": \"fly\", \"amount\": 0.3")).Contains("effect"), "An unknown upgrade effect is rejected");
        check(Error(Upg("\"effect\": \"damage\", \"amount\": 5")).Contains("amount"), "An upgrade amount out of range is rejected");
        check(Error(Upg("\"effect\": \"heal\", \"amount\": 10, \"maxStacks\": 11")).Contains("maxStacks"), "maxStacks over 10 is rejected");
        check(Error("\"upgrades\": []").Contains("at least one"), "An empty upgrade list is rejected");
    }
}
