using DesktopPet;

// Adventure milestone 5: the chapter's balance in simulation (NOT real play: a real user also
// lifts the cat out of danger). Every stage × weapon × difficulty, 10 seeds, two players:
// "idle" lets the cat fight alone (no skills; upgrades by a fixed order) and "simple" also uses
// skills by plain rules. Prints a table and checks the targets agreed on 2026-10-20.
internal static class AdventureBalanceTests
{
    public readonly record struct Outcome(bool Won, double Seconds, double Hp);

    // Order the simple player takes upgrades in (the first one offered wins; fish when hurt).
    private static readonly string[] Preference = { "sharp", "fast_paws", "thick_fur", "quick_feet", "heavy_push", "grilled_fish" };

    public static Outcome Play(BattleSettings data, StageSpec stage, string weapon, BattleDifficulty level, int seed, bool skills)
    {
        const double dt = 1.0 / 30;
        var sim = new BattleSim(data, 0, 1600, new Random(seed), level, weapon, stage);
        double cat = 800;
        for (double t = 0; t < 600 && !sim.Over; t += dt)
        {
            if (sim.ChoosingUpgrade)
            {
                var offer = sim.UpgradeOffer!;
                var fish = offer.FirstOrDefault(u => u.Effect == "heal");
                var pick = fish != null && sim.CatHp < data.CatMaxHp * 0.6 ? fish
                    : offer.OrderBy(u => Array.IndexOf(Preference, u.Id) is int i && i >= 0 ? i : 99).First();
                sim.ChooseUpgrade(pick.Id, cat);
            }
            if (skills) UseSkills(sim, cat, data);
            var order = sim.Step(dt, cat, false);
            double run = 72 * data.CatRunScale * sim.MoveFactor; // Walking speed × CatRunScale × upgrades.
            if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -run * dt, run * dt);
        }
        return new(sim.Phase == BattlePhase.Won, sim.FightSeconds, sim.CatHp);
    }

    // Plain rules a careful user could follow without lifting the cat.
    private static void UseSkills(BattleSim sim, double cat, BattleSettings data)
    {
        sim.SetCatState(false, false);
        if (sim.CatHp < data.CatMaxHp * 0.5) sim.Use(BattleSkill.Heal, cat);
        bool danger = sim.Slimes.Any(s => s.State == SlimeState.Windup && Math.Abs(s.X - cat) <= s.Range + 12 && s.StateTime > sim.Windup(s) * 0.5);
        if (danger) sim.Use(BattleSkill.Shield, cat);
        var healer = sim.Slimes.FirstOrDefault(s => s.State == SlimeState.Casting);
        if (healer != null) sim.Use(BattleSkill.Push, healer.Id, cat);
        else if (sim.Slimes.FirstOrDefault(s => s.State == SlimeState.Windup && Math.Abs(s.X - cat) <= s.Range + 12) is Slime hitter && !danger) sim.Use(BattleSkill.Push, hitter.Id, cat);
        var crowd = sim.Slimes.Where(s => s.Targetable).ToList();
        var centre = crowd.FirstOrDefault(s => crowd.Count(o => Math.Abs(o.X - s.X) <= data.BombRadius) >= 2 || s.IsBoss);
        if (centre != null) sim.Use(BattleSkill.Bomb, centre.Id, cat);
        if (crowd.Count > 0) sim.Use(BattleSkill.Cheer, cat);
    }

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var stages = data.Campaign!.Chapters[0].Stages;
        var levels = new[] { BattleDifficulty.Easy, BattleDifficulty.Normal, BattleDifficulty.Hard };
        var weapons = new[] { BattleWeapon.Sword, BattleWeapon.Hammer };
        const int seeds = 10;
        var table = new Dictionary<(int Stage, string Weapon, BattleDifficulty Level, bool Skills), (double Wins, double Seconds, double Hp)>();
        Console.WriteLine("Adventure balance (simulation, not real play): stage | weapon | level | player | wins/10 | avg s (wins) | avg HP left (wins)");
        for (int i = 0; i < stages.Length; i++)
            foreach (var weapon in weapons)
                foreach (var level in levels)
                    foreach (bool skills in new[] { false, true })
                    {
                        var runs = Enumerable.Range(1, seeds).Select(seed => Play(data, stages[i], weapon, level, seed, skills)).ToList();
                        var wins = runs.Where(r => r.Won).ToList();
                        var row = ((double)wins.Count / seeds, wins.Count > 0 ? wins.Average(r => r.Seconds) : 0, wins.Count > 0 ? wins.Average(r => r.Hp) : 0);
                        table[(i + 1, weapon, level, skills)] = row;
                        Console.WriteLine($"  {i + 1} | {weapon,-6} | {level,-6} | {(skills ? "simple" : "idle"),-6} | {wins.Count,2}/10 | {row.Item2,6:0} | {row.Item3,4:0}");
                    }

        // Targets (2026-10-20): Normal with the simple player wins ≥ 80 % everywhere; Easy wins
        // stages 1–2 even idle; no win takes longer than ~4 minutes; sword and hammer stay close.
        foreach (var weapon in weapons)
            for (int s = 1; s <= stages.Length; s++)
                check(table[(s, weapon, BattleDifficulty.Normal, true)].Wins >= 0.8,
                    $"Balance: stage {s}, {weapon}, Normal, simple player wins ≥ 80 % ({table[(s, weapon, BattleDifficulty.Normal, true)].Wins:P0})");
        foreach (var weapon in weapons)
            for (int s = 1; s <= 2; s++)
                check(table[(s, weapon, BattleDifficulty.Easy, false)].Wins >= 0.8,
                    $"Balance: stage {s}, {weapon}, Easy, even an idle player wins ({table[(s, weapon, BattleDifficulty.Easy, false)].Wins:P0})");
        double longest = table.Values.Where(r => r.Wins > 0).Max(r => r.Seconds);
        check(longest <= 240, $"Balance: no stage takes longer than 4 minutes on average (longest {longest:0} s)");
        foreach (var level in levels)
            for (int s = 1; s <= stages.Length; s++)
            {
                double sword = table[(s, BattleWeapon.Sword, level, true)].Wins, hammer = table[(s, BattleWeapon.Hammer, level, true)].Wins;
                check(Math.Abs(sword - hammer) <= 0.3, $"Balance: stage {s}, {level}: sword and hammer within 30 points ({sword:P0} vs {hammer:P0})");
            }
        // The whole chapter in one go, saved and read back after every stage (like closing the app).
        string dir = Path.Combine(Path.GetTempPath(), "pet-chapter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "adventure-progress.json");
            bool inOrder = true;
            for (int i = 0; i < stages.Length; i++)
            {
                var adventure = new Adventure(data.Campaign, AdventureProgress.Load(file));
                inOrder &= adventure.IsUnlocked(stages[i].Id) && (i + 1 >= stages.Length || !adventure.IsUnlocked(stages[i + 1].Id));
                var run = Play(data, stages[i], BattleWeapon.Sword, BattleDifficulty.Normal, 1, true);
                adventure.Apply("chapter" + i, stages[i], true, run.Won, BattleWeapon.Sword, BattleDifficulty.Normal, 1, run.Seconds);
                inOrder &= run.Won && adventure.Progress.Save(file);
            }
            var done = AdventureProgress.Load(file);
            check(inOrder && done.Completed.Count == 5 && done.Rewards.OrderBy(r => r).SequenceEqual(new[] { "SlimeHat", "SlimeKingCrown" }) && File.Exists(file + ".bak"),
                "The whole chapter plays in order (simulation, Normal, sword): each stage opens the next, both rewards once, saved after every stage");
        }
        finally { Directory.Delete(dir, true); }

        // Stage tips: stages 2–5 have one in both languages; a tip in one language only is rejected.
        check(stages[0].TipVi == null && stages.Skip(1).All(st => st.TipVi?.Length > 0 && st.TipEn?.Length > 0), "Stages 2–5 have a tip in both languages");
        string Tip(string extra)
        {
            try { BattleData.Parse("{ \"schemaVersion\": 1, \"campaign\": { \"chapters\": [ { \"id\": \"c\", \"nameVi\": \"C\", \"nameEn\": \"C\", \"stages\": [ { \"id\": \"s\", \"nameVi\": \"S\", \"nameEn\": \"S\", " + extra +
                "\"waves\": [ { \"groups\": [ { \"enemy\": \"slime\", \"count\": 1 } ] } ] } ] } ] } }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        check(Tip("\"tipVi\": \"a\", \"tipEn\": \"b\", ") == "" && Tip("\"tipVi\": \"a\", ").Contains("tips") && Tip($"\"tipVi\": \"{new string('a', 221)}\", \"tipEn\": \"b\", ").Contains("tips"),
            "A stage tip needs both languages and at most 220 characters");

        // Harder stages ask more: on Normal the idle player does not do better on stage 5 than on stage 1.
        check(table[(5, BattleWeapon.Sword, BattleDifficulty.Normal, false)].Wins <= table[(1, BattleWeapon.Sword, BattleDifficulty.Normal, false)].Wins,
            "Balance: the boss stage is not easier than the first stage");
    }
}
