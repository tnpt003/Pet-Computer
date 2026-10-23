using DesktopPet;

// Adventure milestone 5: the chapter's balance in simulation (NOT real play: a real user also
// lifts the cat out of danger). Every stage × weapon × difficulty, Seeds seeds, three players:
// "idle" lets the cat fight alone (no skills), "simple" also uses skills by plain rules and
// answers every windup or heal the frame it starts (a best case), "casual" follows the same rules
// but notices a windup or heal only CasualReaction s (simulation time) after it starts and misses
// CasualMiss of them (rolled from the seed). Each seed plays the chapter in order and carries the
// build of each won stage into the next (Combat Depth); all players build toward one group.
// Prints the tables and checks the agreed targets (on the simple player, as before; the casual
// player is measured, not yet a target).
internal static class AdventureBalanceTests
{
    public enum Player { Idle, Simple, Casual }
    // Runs per row: 20 since the desktop apps (2026-10-23, user): with 10 a single seed moved a
    // row by 10 points and rows near a target flipped on any change (the targets are shares, unchanged).
    public const int Seeds = 20;
    // The casual player's reaction time and the share of windups/heals it never answers.
    public const double CasualReaction = 0.6, CasualMiss = 0.3;

    // Also measured (review 2026-10-20, R3): hits lost on heal-guarded enemies (Warded), heals
    // started/landed by support enemies, the share of fighting time the enemies took no damage for
    // QuietAfter s or more (QuietShare), the cat's target changes a minute, stuns that come right
    // after the last one's immunity (StunChains).
    public readonly record struct Outcome(bool Won, double Seconds, double Hp, int Stuns, int Synergies, Dictionary<string, int> Build, int Blocks = 0, double ShieldSeconds = 0, double HpShare = 0, int Elites = 0,
        int Warded = 0, int HealsCast = 0, int HealsLanded = 0, double QuietShare = 0, double Switches = 0, int StunChains = 0);
    private const double QuietAfter = 2;

    // Order cards are liked in when the build gives no lead (fish when hurt).
    private static readonly string[] Preference = { "sharp", "fast_paws", "thick_fur", "big_heart", "quick_feet", "heavy_push", "grilled_fish" };

    // startHp, threat, battleSeed: an Expedition step's (ExpeditionTests); unset for the Adventure.
    public static Outcome Play(BattleSettings data, StageSpec stage, string weapon, BattleDifficulty level, int seed, Player player, IReadOnlyDictionary<string, int>? build = null,
        double? startHp = null, double threat = 1, int? battleSeed = null, IReadOnlyList<string>? relics = null)
    {
        const double dt = 1.0 / 30;
        var sim = new BattleSim(data, 0, 1600, new Random(seed), level, weapon, stage, build, battleSeed, startHp, threat, relics);
        var reflex = new Reflex(player, seed);
        double cat = 800;
        // The measures: HP each enemy had last frame, the fighting time and how much of it was quiet.
        var hpBefore = new Dictionary<int, double>();
        double fighting = 0, quiet = 0, sinceDamage = 0, lastStun = double.NegativeInfinity;
        int healsCast = 0, healsLanded = 0, switches = 0, chains = 0;
        int? lastTarget = null;
        double chainGap = data.StunDurationSeconds + data.StunImmunitySeconds + 2;
        for (double t = 0; t < 600 && !sim.Over; t += dt)
        {
            if (sim.ChoosingUpgrade)
            {
                // Goes on with the group it has most of (a plan), the fish when low.
                var offer = sim.UpgradeOffer!;
                var fish = offer.FirstOrDefault(u => u.Effect == "heal");
                var pick = fish != null && sim.CatHp < sim.CatMaxHp * 0.6 ? fish
                    : offer.OrderByDescending(u => u.Group != null ? sim.GroupPoints(u.Group) : -1)
                        .ThenBy(u => Array.IndexOf(Preference, u.Id) is int i && i >= 0 ? i : 99).First();
                sim.ChooseUpgrade(pick.Id, cat);
            }
            if (player != Player.Idle) UseSkills(sim, cat, data, reflex, t);
            var order = sim.Step(dt, cat, false);
            double run = 72 * data.CatRunScale * sim.MoveFactor; // Walking speed × CatRunScale × upgrades.
            if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -run * dt, run * dt);
            foreach (var e in sim.Events)
            {
                if (e.Kind == BattleEventKind.EnemyHealing) healsCast++;
                else if (e.Kind == BattleEventKind.EnemyHealed) healsLanded++;
                else if (e.Kind == BattleEventKind.CatStunned) { if (t - lastStun <= chainGap) chains++; lastStun = t; }
            }
            sim.ClearEvents();
            bool damaged = false;
            foreach (var s in sim.Slimes)
            {
                if (hpBefore.TryGetValue(s.Id, out double was) && s.Hp < was) damaged = true;
                hpBefore[s.Id] = s.Hp;
            }
            if (sim.TargetId is int target) { if (lastTarget is int before && before != target) switches++; lastTarget = target; }
            if (sim.Phase == BattlePhase.Fighting && !sim.ChoosingUpgrade && sim.Slimes.Any(s => s.Targetable))
            {
                fighting += dt;
                sinceDamage = damaged ? 0 : sinceDamage + dt;
                if (sinceDamage >= QuietAfter) quiet += dt;
            }
            else sinceDamage = 0;
        }
        return new(sim.Phase == BattlePhase.Won, sim.FightSeconds, sim.CatHp, sim.StunsTaken, sim.Synergies.Count, sim.Build(), sim.BlocksMade, sim.BossShieldSeconds, sim.CatHp / sim.CatMaxHp, sim.ElitesSpawned,
            sim.WardedHits, healsCast, healsLanded, fighting > 0 ? quiet / fighting : 0, sim.FightSeconds > 0 ? switches * 60 / sim.FightSeconds : 0, chains);
    }

    // When the player notices a windup or a heal: the simple player at once, the casual one after
    // CasualReaction s, and not at all for the CasualMiss share it misses (one roll per windup, from
    // the seed, so a run plays the same every time).
    private sealed class Reflex(Player player, int seed)
    {
        private readonly Random rolls = new(seed * 7919 + 17);
        private readonly Dictionary<int, (SlimeState State, double Since, bool Missed)> seen = new();

        public bool Notices(Slime s, double now)
        {
            if (player != Player.Casual) return true;
            double since = now - s.StateTime;
            if (!seen.TryGetValue(s.Id, out var known) || known.State != s.State || Math.Abs(since - known.Since) > 0.05)
                seen[s.Id] = known = (s.State, since, rolls.NextDouble() < CasualMiss);
            return !known.Missed && s.StateTime >= CasualReaction;
        }

        public bool NoticesAge(double age) => player != Player.Casual || age >= CasualReaction;
    }

    // Plain rules a careful user could follow without lifting the cat (windups and heals only once
    // the player has noticed them).
    private static void UseSkills(BattleSim sim, double cat, BattleSettings data, Reflex reflex, double now)
    {
        sim.SetCatState(false, false);
        if (sim.CatHp < sim.CatMaxHp * 0.5) sim.Use(BattleSkill.Heal, cat);
        // A windup that will reach the cat: a plain or slam attack in its reach, or a dash whose lane
        // (shown on the floor) runs over the cat.
        bool Threat(Slime s) => s.State == SlimeState.Windup && reflex.Notices(s, now) && (s.Type.Dash
            ? (cat - s.X) * s.DashDirection >= -BattleSim.CatHalfWidth && Math.Abs(cat - s.X) <= s.Type.DashDistance + s.HitWidth / 2 + BattleSim.CatHalfWidth
            : Math.Abs(s.X - cat) <= s.Range + 12);
        // The Shield (long cooldown) is kept for plain and slam hits; a dash is Pushed away instead.
        bool danger = sim.Slimes.Any(s => Threat(s) && !s.Type.Dash && s.StateTime > sim.Windup(s) * 0.5);
        if (danger) sim.Use(BattleSkill.Shield, cat);
        var healer = sim.Slimes.FirstOrDefault(s => s.State == SlimeState.Casting && reflex.Notices(s, now));
        var dasher = sim.Slimes.FirstOrDefault(s => Threat(s) && s.Type.Dash);
        if (healer != null) sim.Use(BattleSkill.Push, healer.Id, cat);
        else if (dasher != null) sim.Use(BattleSkill.Push, dasher.Id, cat);
        else if (sim.Slimes.FirstOrDefault(Threat) is Slime hitter && !danger) sim.Use(BattleSkill.Push, hitter.Id, cat);
        // A shielded boss takes nothing: the bomb goes to its helpers.
        var crowd = sim.Slimes.Where(s => s.Targetable && !s.Shielded).ToList();
        var centre = crowd.FirstOrDefault(s => crowd.Count(o => Math.Abs(o.X - s.X) <= data.BombRadius) >= 2 || s.IsBoss);
        if (centre != null) sim.Use(BattleSkill.Bomb, centre.Id, cat);
        if (crowd.Count > 0) sim.Use(BattleSkill.Cheer, cat);
        // Desktop apps: an ad or malware is closed (each virus.exe deleted) and an infected icon scrubbed once noticed (the
        // casual player after its reaction time; one click a frame); a charged icon is clicked at once,
        // Undo only after a real loss.
        foreach (var popup in sim.Popups.ToList()) if (reflex.NoticesAge(popup.Age)) sim.ClosePopup(popup.Id);
        foreach (var hazard in sim.Hazards.ToList()) if (reflex.NoticesAge(hazard.Age)) sim.CloseHazard(hazard.Id);
        foreach (var file in sim.VirusFiles.ToList()) if (reflex.NoticesAge(sim.Time - file.BornAt)) sim.DeleteVirusFile(file.Id);
        foreach (var app in sim.Desktop)
        {
            if (app.Infected) { if (reflex.NoticesAge(sim.Time - app.InfectedAt)) sim.UseApp(app.Id, cat); continue; }
            if (!app.Ready || app.Kind == BattleAppKind.Undo && sim.CatHp > sim.CatMaxHp * 0.8) continue;
            sim.UseApp(app.Id, cat);
        }
    }

    private static readonly Player[] Players = { Player.Idle, Player.Simple, Player.Casual };
    private static string Name(Player player) => player.ToString().ToLowerInvariant();

    // data: another battle (balance experiments); the built-in one by default.
    public static void Run(Action<bool, string> check, BattleSettings? data = null)
    {
        data ??= BattleData.Settings;
        var stages = data.Campaign!.Chapters[0].Stages;
        var levels = new[] { BattleDifficulty.Easy, BattleDifficulty.Normal, BattleDifficulty.Hard };
        var weapons = new[] { BattleWeapon.Sword, BattleWeapon.Hammer };
        const int seeds = Seeds;
        var table = new Dictionary<(int Stage, string Weapon, BattleDifficulty Level, Player Player), (double Wins, double Seconds, double Hp, double HpShare)>();
        // Each seed plays the chapter in order; a stage starts with the build the stage before was
        // won with (a lost stage passes on the last winning build, as a retry would).
        var played = new Dictionary<(int Stage, string Weapon, BattleDifficulty Level, Player Player), List<Outcome>>();
        foreach (var weapon in weapons)
            foreach (var level in levels)
                foreach (var player in Players)
                    for (int seed = 1; seed <= seeds; seed++)
                    {
                        var carried = new Dictionary<string, int>();
                        for (int i = 0; i < stages.Length; i++)
                        {
                            var run = Play(data, stages[i], weapon, level, seed, player, carried);
                            if (run.Won) carried = run.Build;
                            if (!played.TryGetValue((i + 1, weapon, level, player), out var list)) played[(i + 1, weapon, level, player)] = list = new();
                            list.Add(run);
                        }
                    }
        Console.WriteLine($"Adventure balance (simulation, not real play): stage | weapon | level | player | wins/{seeds} | avg s (wins) | avg HP left (wins) | stuns/run | synergies at end | blocks/run | boss shield s/run | HP % left (wins) | elites/run");
        for (int i = 0; i < stages.Length; i++)
            foreach (var weapon in weapons)
                foreach (var level in levels)
                    foreach (var player in Players)
                    {
                        var runs = played[(i + 1, weapon, level, player)];
                        var wins = runs.Where(r => r.Won).ToList();
                        var row = ((double)wins.Count / seeds, wins.Count > 0 ? wins.Average(r => r.Seconds) : 0, wins.Count > 0 ? wins.Average(r => r.Hp) : 0, wins.Count > 0 ? wins.Average(r => r.HpShare) : 0);
                        table[(i + 1, weapon, level, player)] = row;
                        Console.WriteLine($"  {i + 1} | {weapon,-6} | {level,-6} | {Name(player),-6} | {wins.Count,2}/{seeds} | {row.Item2,6:0} | {row.Item3,4:0} | {runs.Average(r => r.Stuns),4:0.0} | {runs.Average(r => r.Synergies),3:0.0} | {runs.Average(r => r.Blocks),4:0.0} | {runs.Average(r => r.ShieldSeconds),4:0} | {row.Item4,4:P0} | {runs.Average(r => r.Elites),3:0.0}");
                    }
        // What the players meet (R3): per stage, level and player, both weapons together.
        Console.WriteLine($"Adventure tactics (simulation; casual: notices after {CasualReaction} s, misses {CasualMiss:P0}): stage | level | player | warded hits/run | enemy heals landed/started | quiet time (≥ {QuietAfter} s without damage) | target changes/min | stun chains/run | rock blocks/run");
        List<Outcome> Runs(int stage, BattleDifficulty level, Player player) =>
            played.Where(p => p.Key.Stage == stage && p.Key.Level == level && p.Key.Player == player).SelectMany(p => p.Value).ToList();
        for (int i = 1; i <= stages.Length; i++)
            foreach (var level in levels)
                foreach (var player in Players)
                {
                    var runs = Runs(i, level, player);
                    int cast = runs.Sum(r => r.HealsCast), landed = runs.Sum(r => r.HealsLanded);
                    Console.WriteLine($"  {i} | {level,-6} | {Name(player),-6} | {runs.Average(r => r.Warded),5:0.0} | {landed,3}/{cast,-3} ({(cast > 0 ? (double)landed / cast : 0),4:P0}) | {runs.Average(r => r.QuietShare),4:P0} | {runs.Average(r => r.Switches),4:0.0} | {runs.Average(r => r.StunChains),3:0.0} | {runs.Average(r => r.Blocks),4:0.0}");
                }
        // The casual player is measured, not held to a target yet (the user plays before balance is
        // changed); it must still do worse than the best case, or the delay does nothing. (How many
        // enemy heals land is about the same for both: the Push cooldown, not the reaction, limits it.)
        var all = played.Where(p => p.Key.Player != Player.Idle).GroupBy(p => p.Key.Player).ToDictionary(g => g.Key, g => g.SelectMany(p => p.Value).ToList());
        double Wins(Player player) => all[player].Count(r => r.Won) / (double)all[player].Count;
        double Left(Player player) => all[player].Where(r => r.Won).Average(r => r.HpShare);
        check(Wins(Player.Casual) <= Wins(Player.Simple) && Left(Player.Casual) < Left(Player.Simple),
            $"Balance: the casual player wins no more than the simple one ({Wins(Player.Casual):P0} vs {Wins(Player.Simple):P0}) and ends its wins with less HP ({Left(Player.Casual):P0} vs {Left(Player.Simple):P0})");
        var again = Play(data, stages[3], BattleWeapon.Sword, BattleDifficulty.Normal, 3, Player.Casual);
        var once = Play(data, stages[3], BattleWeapon.Sword, BattleDifficulty.Normal, 3, Player.Casual);
        check(again.Won == once.Won && again.Seconds == once.Seconds && again.Hp == once.Hp && again.Warded == once.Warded,
            "Balance: the casual player's misses come from the seed (the same run plays the same twice)");

        // A build that is followed reaches its synergy within the chapter (stage 3 at the latest).
        // Counted on the simple player's runs that won stage 3: a run lost early never got its picks
        // (since Enemy Director milestone 3 the idle player loses stage 2 on Normal and Hard).
        var thirdWins = played.Where(p => p.Key.Stage == 3 && p.Key.Player == Player.Simple).SelectMany(p => p.Value).Where(r => r.Won).ToList();
        // 88 % (user, 2026-10-24): with the battle malware (virus.exe) this bot reached 85/95, one run
        // short of 90 %; 92 % without virus.exe.
        double reached = thirdWins.Count(r => r.Synergies > 0) / (double)thirdWins.Count;
        check(reached >= 0.88, $"Balance: following one group, a synergy is on by the end of stage 3 in ≥ 88 % of the simple player's runs that win it ({reached:P0}: {thirdWins.Count(r => r.Synergies > 0)}/{thirdWins.Count})");

        // Targets (2026-10-20): Normal with the simple player wins ≥ 80 % everywhere; Easy wins
        // stages 1–2 even idle; no win takes longer than ~4 minutes; sword and hammer stay close.
        foreach (var weapon in weapons)
            for (int s = 1; s <= stages.Length; s++)
                check(table[(s, weapon, BattleDifficulty.Normal, Player.Simple)].Wins >= 0.8,
                    $"Balance: stage {s}, {weapon}, Normal, simple player wins ≥ 80 % ({table[(s, weapon, BattleDifficulty.Normal, Player.Simple)].Wins:P0})");
        // Since the 1.2.0-beta enemy buff (user, 2026-10-21: keep the buff, lower the targets) only
        // stage 1 is won idle on Easy; stage 2 on Easy needs the skills (the simple player).
        foreach (var weapon in weapons)
        {
            check(table[(1, weapon, BattleDifficulty.Easy, Player.Idle)].Wins >= 0.8,
                $"Balance: stage 1, {weapon}, Easy, even an idle player wins ({table[(1, weapon, BattleDifficulty.Easy, Player.Idle)].Wins:P0})");
            check(table[(2, weapon, BattleDifficulty.Easy, Player.Simple)].Wins >= 0.8,
                $"Balance: stage 2, {weapon}, Easy, the simple player wins ({table[(2, weapon, BattleDifficulty.Easy, Player.Simple)].Wins:P0})");
        }
        // Enemy Director milestone 3: the idle player wins at most 30 % of stages 2–5 on Normal, and the
        // simple player ends them with less HP than before (it used to finish near full: 95–119 HP).
        // The agreed ≤ 60 % HP left is not reached by this bot (it answers every windup at once).
        foreach (var weapon in weapons)
            for (int s = 2; s <= stages.Length; s++)
            {
                check(table[(s, weapon, BattleDifficulty.Normal, Player.Idle)].Wins <= 0.3,
                    $"Balance: stage {s}, {weapon}, Normal, the idle player wins ≤ 30 % ({table[(s, weapon, BattleDifficulty.Normal, Player.Idle)].Wins:P0})");
                // Stage 5 allows more: since the boss leaps to the far edge its shield phases last longer,
                // and a longer fight gives the bot more Healing fish (one per 20 s).
                // Stage 4 allows 88 % (user, 2026-10-23): with the desktop apps' staggered start (Tabs
                // ready ~20 s in) this bot, which clicks each app the moment it is charged, ends it at
                // 85–87 % and app tuning only moved it within that noise.
                double most = s == 5 ? 0.92 : s == 4 ? 0.88 : 0.85;
                check(table[(s, weapon, BattleDifficulty.Normal, Player.Simple)].HpShare <= most,
                    $"Balance: stage {s}, {weapon}, Normal, the simple player ends with ≤ {most:P0} HP ({table[(s, weapon, BattleDifficulty.Normal, Player.Simple)].HpShare:P0})");
            }
        // Elites: some on Normal from stage 3 on, none on Easy, none before stage 3.
        double Elites(int s, BattleDifficulty level) => played.Where(p => p.Key.Stage == s && p.Key.Level == level).SelectMany(p => p.Value).Average(r => r.Elites);
        check(Enumerable.Range(3, 3).All(s => Elites(s, BattleDifficulty.Normal) >= 0.5) && Enumerable.Range(1, 5).All(s => Elites(s, BattleDifficulty.Easy) == 0)
            && Elites(1, BattleDifficulty.Normal) == 0 && Elites(2, BattleDifficulty.Hard) == 0,
            $"Balance: elites come from stage 3 on Normal/Hard (stage 3–5 Normal: {Elites(3, BattleDifficulty.Normal):0.0}/{Elites(4, BattleDifficulty.Normal):0.0}/{Elites(5, BattleDifficulty.Normal):0.0} a run), never on Easy");
        // An average needs at least two wins (one lucky win of an otherwise lost row is not one).
        double longest = table.Values.Where(r => r.Wins * seeds >= 2).Max(r => r.Seconds);
        check(longest <= 240, $"Balance: no stage takes longer than 4 minutes on average (longest {longest:0} s)");
        foreach (var level in levels)
            for (int s = 1; s <= stages.Length; s++)
            {
                double sword = table[(s, BattleWeapon.Sword, level, Player.Simple)].Wins, hammer = table[(s, BattleWeapon.Hammer, level, Player.Simple)].Wins;
                // In whole points: 6/10 against 9/10 is 30 points, not 0.30000000000000004.
                check(Math.Round(Math.Abs(sword - hammer) * 100) <= 30, $"Balance: stage {s}, {level}: sword and hammer within 30 points ({sword:P0} vs {hammer:P0})");
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
                // On Easy since the 1.2.0-beta enemy buff: this checks the saved flow, not the balance.
                var run = Play(data, stages[i], BattleWeapon.Sword, BattleDifficulty.Easy, 1, Player.Simple, adventure.StartBuild(stages[i]));
                adventure.Apply("chapter" + i, stages[i], true, run.Won, BattleWeapon.Sword, BattleDifficulty.Easy, 1, run.Seconds, run.Build);
                inOrder &= run.Won && adventure.Progress.Save(file);
            }
            var done = AdventureProgress.Load(file);
            check(inOrder && done.Completed.Count == 5 && done.Rewards.OrderBy(r => r).SequenceEqual(new[] { "SlimeHat", "SlimeKingCrown" }) && File.Exists(file + ".bak"),
                "The whole chapter plays in order (simulation, Easy, sword): each stage opens the next, both rewards once, saved after every stage");
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
        check(table[(5, BattleWeapon.Sword, BattleDifficulty.Normal, Player.Idle)].Wins <= table[(1, BattleWeapon.Sword, BattleDifficulty.Normal, Player.Idle)].Wins,
            "Balance: the boss stage is not easier than the first stage");
    }
}
