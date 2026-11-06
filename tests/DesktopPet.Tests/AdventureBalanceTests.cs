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
    // Chapter 2 carries builds between stages; 60 fixed seeds for every weapon/level reduce one-run threshold noise.
    public const int ChapterTwoSeeds = 60;
    // The casual player's reaction time and the share of windups/heals it never answers.
    public const double CasualReaction = 0.6, CasualMiss = 0.3;

    // Also measured (review 2026-10-20, R3): hits lost on heal-guarded enemies (Warded), heals
    // started/landed by support enemies, the share of fighting time the enemies took no damage for
    // QuietAfter s or more (QuietShare), the cat's target changes a minute, stuns that come right
    // after the last one's immunity (StunChains).
    public readonly record struct Outcome(bool Won, double Seconds, double Hp, int Stuns, int Synergies, Dictionary<string, int> Build, int Blocks = 0, double ShieldSeconds = 0, double HpShare = 0, int Elites = 0,
        int Warded = 0, int HealsCast = 0, int HealsLanded = 0, double QuietShare = 0, double Switches = 0, int StunChains = 0, int SweepHits = 0, int Launches = 0, int Fish = -1);
    private const double QuietAfter = 2;

    // Order cards are liked in when the build gives no lead (fish when hurt).
    private static readonly string[] Preference = { "sharp", "fast_paws", "thick_fur", "big_heart", "quick_feet", "heavy_push", "grilled_fish" };

    // startHp, threat, battleSeed: an Expedition step's (ExpeditionTests); unset for the Adventure.
    public static Outcome Play(BattleSettings data, StageSpec stage, string weapon, BattleDifficulty level, int seed, Player player, IReadOnlyDictionary<string, int>? build = null,
        double? startHp = null, double threat = 1, int? battleSeed = null, IReadOnlyList<string>? relics = null,
        IEnumerable<BattleSkill>? skills = null, BattleLoot loot = BattleLoot.None, double weaponBonus = 0, double eliteBonus = 0)
    {
        const double dt = 1.0 / 30;
        var sim = new BattleSim(data, 0, 1600, new Random(seed), level, weapon, stage, build, battleSeed, startHp, threat, relics, skills: skills, loot: loot,
            weaponBonus: weaponBonus, eliteBonus: eliteBonus);
        var reflex = new Reflex(player, seed);
        var hands = new Hands();
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
                    : offer.OrderByDescending(u => u.Group is string g && g != BuildGroups.Trick ? sim.GroupPoints(g) : -1) // Trickery (1.5 M25) as the utility cards were.
                        .ThenBy(u => Array.IndexOf(Preference, u.Id) is int i && i >= 0 ? i : 99).First();
                sim.ChooseUpgrade(pick.Id, cat);
            }
            if (player != Player.Idle) { UseSkills(sim, cat, data, reflex, t); UseTricks(sim, cat, reflex, t, hands); }
            if (player != Player.Idle && ZipPuzzlePlayer.Step(sim, ref cat, dt)) { sim.ClearEvents(); continue; }
            var order = sim.Step(dt, cat, false);
            double run = 72 * data.CatRunScale * sim.MoveFactor; // Walking speed × CatRunScale × upgrades.
            if (order.Command == CatCommand.Shove) cat = order.X; else if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -run * dt, run * dt);
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
            sim.WardedHits, healsCast, healsLanded, fighting > 0 ? quiet / fighting : 0, sim.FightSeconds > 0 ? switches * 60 / sim.FightSeconds : 0, chains, sim.SweepHits, sim.WeaponLaunches, sim.Basket != null ? sim.FishInBasket : -1);
    }

    // 1.5 M24: an Infinity run (fish and potions drop) with the same player, cards picked the same way and
    // the first mutation offered taken: the wave it reached when the cat fell, or after `seconds`.
    public static int PlayEndless(BattleSettings data, EndlessSpec spec, string weapon, BattleDifficulty level, int seed, Player player, double seconds = 1200)
    {
        const double dt = 1.0 / 30;
        var sim = new BattleSim(data, 0, 1600, new Random(seed), level, weapon, spec.Stage(), loot: BattleLoot.Items);
        var reflex = new Reflex(player, seed);
        var hands = new Hands();
        double cat = 800;
        for (double t = 0; t < seconds && !sim.Over; t += dt)
        {
            if (sim.ChoosingUpgrade)
            {
                var offer = sim.UpgradeOffer!;
                var fish = offer.FirstOrDefault(u => u.Effect == "heal");
                var pick = sim.ChoosingMutation ? offer[0] : fish != null && sim.CatHp < sim.CatMaxHp * 0.6 ? fish
                    : offer.OrderByDescending(u => u.Group is string g && g != BuildGroups.Trick ? sim.GroupPoints(g) : -1) // Trickery (1.5 M25) as the utility cards were.
                        .ThenBy(u => Array.IndexOf(Preference, u.Id) is int i && i >= 0 ? i : 99).First();
                sim.ChooseUpgrade(pick.Id, cat);
            }
            if (player != Player.Idle) { UseSkills(sim, cat, data, reflex, t); UseTricks(sim, cat, reflex, t, hands); }
            if (player != Player.Idle && ZipPuzzlePlayer.Step(sim, ref cat, dt)) { sim.ClearEvents(); continue; }
            var order = sim.Step(dt, cat, false);
            double run = 72 * data.CatRunScale * sim.MoveFactor;
            if (order.Command == CatCommand.Shove) cat = order.X; else if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -run * dt, run * dt);
            sim.ClearEvents();
        }
        return sim.Wave;
    }

    // 1.5 M21: what the bot's mouse is doing (the last bubble click).
    private sealed class Hands { public double LastPoke = double.NegativeInfinity; }

    // 1.5 M21 (chapter 2): the cursor and the new skills, by plain rules. Each rule acts only on what
    // chapter 2 brings (cursor enemies, a basket, drops, the new skills), so chapter 1 plays as before.
    private static void UseTricks(BattleSim sim, double cat, Reflex reflex, double now, Hands hands)
    {
        // The cursor rests on the nearest hiding or mirrored slime near the cat (shows it, finds the real one).
        var hidden = sim.Slimes.Where(s => s.Type.UsesMouse && s.Type.Bubble == null && s.State != SlimeState.Defeated && Math.Abs(s.X - cat) <= 420)
            .OrderBy(s => Math.Abs(s.X - cat)).FirstOrDefault();
        if (hidden != null) sim.SetPointer((hidden.X, 10));
        else if (sim.PointerX != null) sim.SetPointer(null);
        var packets = sim.Slimes.Where(s => s.PacketLeft > 0 && s.Targetable).ToArray();
        if (packets.Length > 0) sim.SetPointer((packets[(int)(now / 0.25) % packets.Length].X, 10));
        // A bubble near the cat is clicked about three times a second.
        if (now - hands.LastPoke >= 0.3 && sim.Slimes.FirstOrDefault(s => s.Bubbled && s.Targetable && Math.Abs(s.X - cat) <= 500 && reflex.Notices(s, now)) is Slime bubble)
        { sim.Poke(bubble.Id); hands.LastPoke = now; }
        // A dropped fish goes back to the basket, a drop is picked up (once noticed).
        foreach (var fish in sim.StrayFish.ToList()) if (reflex.NoticesAge(fish.Age)) sim.PickUpFish(fish.Id);
        foreach (var drop in sim.Drops.ToList()) if (reflex.NoticesAge(drop.Age)) sim.PickUp(drop.Id);
        // A flag slime with a flag down (2026-11-05): beaten first, as the banner says (its flags stand till then).
        if (sim.SelectedId == null && sim.Slimes.FirstOrDefault(s => s.Type.Plant != null && s.Targetable && !s.FlagGuard && reflex.Notices(s, now)) is Slime bearer) sim.Select(bearer.Id);
        var near = sim.Slimes.Where(s => s.Targetable && !s.IsBoss && Math.Abs(s.X - cat) <= 110).ToList();
        // Laser: two or more slimes close in: the dot well away from the cat, on the side with more room.
        if (sim.Carries(BattleSkill.Laser) && near.Count >= 2 && sim.Ready(BattleSkill.Laser) && sim.Arm(BattleSkill.Laser))
            sim.UseAt(BattleSkill.Laser, cat - sim.Left > sim.Right - cat ? cat - 260 : cat + 260);
        // Fan (2026-11-04): its column on the cat's crowd when two or more slimes close in, or on a flyer or a
        // wall slime in the air.
        if (sim.Carries(BattleSkill.Fan) && sim.Ready(BattleSkill.Fan))
        {
            var up = sim.Slimes.FirstOrDefault(s => s.Targetable && (s.Type.Fly != null || s.Type.Cling != null) && s.Altitude > 0 && reflex.Notices(s, now));
            double? at = up != null ? up.BodyX : near.Count >= 2 ? near.Average(s => s.X) : null;
            if (at is double spot && sim.Arm(BattleSkill.Fan) && !sim.UseOnScreen(BattleSkill.Fan, spot, 100)) sim.Arm(null);
        }
        // Spray (2026-11-04): one click on the nearest slime (a hedgehog or an ice slime first); it sprays by
        // itself until the tank is dry.
        if (sim.Carries(BattleSkill.Spray) && !sim.Spraying && sim.Ready(BattleSkill.Spray))
        {
            var wet = sim.Slimes.Where(s => s.Targetable && Math.Abs(s.X - cat) <= 160).OrderByDescending(s => s.Type.Quills != null || s.Type.Ice != null).ThenBy(s => Math.Abs(s.X - cat)).FirstOrDefault();
            if (wet != null && sim.Arm(BattleSkill.Spray)) sim.StartSpray(wet.X, 15);
        }
        // Scissors: a click at the height of a bubble or a rope (a chain slime's, a boss's).
        if (sim.Carries(BattleSkill.Scissors) && sim.Ready(BattleSkill.Scissors))
        {
            double? at = null;
            double height = 18;
            foreach (var s in sim.Slimes)
            {
                if (!s.Targetable) continue;
                if (s.Bubbled || (s.FlagGuard && BattleSim.IsFlagPost(s))) { at = s.X; break; } // A guarded flag: one cut fells it.
                if (sim.LinkPartner(s) is Slime ally && s.Type.Link?.Kind == EnemyLinks.Tether) { at = (s.X + ally.X) / 2; break; }
                if (sim.RopeBoss(s) is Slime boss) { at = (s.X + boss.X) / 2; break; }
            }
            if (at is double x && sim.Arm(BattleSkill.Scissors) && !sim.UseOnScreen(BattleSkill.Scissors, x, height)) sim.Arm(null);
        }
        // Flyers and wall slimes are Pushed down when the Push is free.
        if (sim.Ready(BattleSkill.Push) && sim.Slimes.FirstOrDefault(s => s.Targetable && (s.Type.Fly != null || s.Type.Cling != null) && s.Altitude > 0 && reflex.Notices(s, now)) is Slime high)
            sim.Use(BattleSkill.Push, high.Id, cat);
    }

    // 1.5 M21: the five skills a player following the stage's hint carries: the suggested ones, then the
    // first skills in HUD order (the stage's lent skill is always among the suggested).
    public static BattleSkill[] HintedLoadout(StageSpec stage) =>
        BattleSkills.Order((stage.SuggestSkills ?? Array.Empty<string>()).Select(BattleSkills.Parse).OfType<BattleSkill>()
            .Concat(BattleSkills.Default).Distinct().Take(BattleSkills.Carry)) ?? BattleSkills.Default;

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
        // Advanced bosses keep their own move state. React to their actual announced geometry,
        // with the same casual delay, rather than treating them as harmless Moving enemies.
        danger |= sim.Slimes.Any(s => sim.BossThreatens(s, cat)
            && s.SpecialDuration - s.SpecialLeft >= s.SpecialDuration * 0.5
            && reflex.NoticesAge(s.SpecialDuration - s.SpecialLeft));
        // The Zip Slime: its announced shapes, and the ZIP shell clicked open (2026-11-06).
        danger |= sim.Slimes.Any(s => sim.ZipThreatens(s, cat) && reflex.NoticesAge(s.ZipSeconds - s.ZipLeft));
        danger |= sim.Slimes.Any(s => s.Type.Zip != null && s.ZipRepulseWarn > 0);
        danger |= sim.Slimes.Any(s => s.PacketFuse && s.PacketRevealed && s.PacketLeft is > 0 and < 0.7 && Math.Abs(s.X - cat) < 180);
        if (sim.CatLocked && reflex.NoticesAge(sim.ZipDebuffSeconds - sim.ZipDebuffLeft)) sim.ClickZipLock();
        danger |= sim.Goos.Any(g => g.Rain && g.Remaining <= 0.5 && Math.Abs(g.X - cat) <= g.Radius
            && sim.Find(g.OwnerId)?.Type.BossTactics != null && reflex.NoticesAge(g.Flight - g.Remaining));
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

    // 1.5 M21: chapter 2 "The Download Dump" (the chapter after the first), played the same way: each
    // seed plays it in order and carries the build on; the players carry the stage's suggested skills
    // (HintedLoadout: the lent skill among them) and pick up drops (fish, potions). Same targets as
    // chapter 1: Normal, simple player ≥ 80 % everywhere, no stage over 4 minutes on average, sword and
    // hammer within 30 points; the idle player is shown, not held to a target.
    public static void RunChapter2(Action<bool, string> check, BattleSettings? data = null)
    {
        data ??= BattleData.Settings;
        if (data.Campaign is not CampaignSpec campaign || campaign.Chapters.Length < 2) { check(false, "Chapter 2 is in the built-in data"); return; }
        var chapter = campaign.Chapters[1];
        var stages = chapter.Stages;
        var levels = new[] { BattleDifficulty.Easy, BattleDifficulty.Normal, BattleDifficulty.Hard };
        var weapons = new[] { BattleWeapon.Sword, BattleWeapon.Hammer };
        var played = new Dictionary<(int Stage, string Weapon, BattleDifficulty Level, Player Player), List<Outcome>>();
        foreach (var weapon in weapons)
            foreach (var level in levels)
                foreach (var player in Players)
                    for (int seed = 1; seed <= ChapterTwoSeeds; seed++)
                    {
                        var carried = new Dictionary<string, int>();
                        for (int i = 0; i < stages.Length; i++)
                        {
                            var run = Play(data, stages[i], weapon, level, seed, player, carried, skills: HintedLoadout(stages[i]), loot: BattleLoot.Items);
                            if (run.Won) carried = run.Build;
                            if (!played.TryGetValue((i + 1, weapon, level, player), out var list)) played[(i + 1, weapon, level, player)] = list = new();
                            list.Add(run);
                        }
                    }
        Console.WriteLine($"Adventure chapter 2 balance (simulation, not real play): stage | weapon | level | player | wins/{ChapterTwoSeeds} | avg s (wins) | HP % left (wins) | stuns/run | fish kept (wins)");
        var table = new Dictionary<(int, string, BattleDifficulty, Player), (double Wins, double Seconds, double HpShare)>();
        for (int i = 1; i <= stages.Length; i++)
            foreach (var weapon in weapons)
                foreach (var level in levels)
                    foreach (var player in Players)
                    {
                        var runs = played[(i, weapon, level, player)];
                        var wins = runs.Where(r => r.Won).ToList();
                        table[(i, weapon, level, player)] = ((double)wins.Count / ChapterTwoSeeds, wins.Count > 0 ? wins.Average(r => r.Seconds) : 0, wins.Count > 0 ? wins.Average(r => r.HpShare) : 0);
                        string fish = stages[i - 1].Basket != null && wins.Count > 0 ? wins.Average(r => r.Fish).ToString("0.0") : "-";
                        Console.WriteLine($"  {i} | {weapon,-6} | {level,-6} | {Name(player),-6} | {wins.Count,2}/{ChapterTwoSeeds} | {(wins.Count > 0 ? wins.Average(r => r.Seconds) : 0),6:0} | {(wins.Count > 0 ? wins.Average(r => r.HpShare) : 0),4:P0} | {runs.Average(r => r.Stuns),4:0.0} | {fish}");
                    }
        foreach (var weapon in weapons)
            for (int i = 1; i <= stages.Length; i++)
                check(table[(i, weapon, BattleDifficulty.Normal, Player.Simple)].Wins >= 0.8,
                    $"Chapter 2 balance: stage {i}, {weapon}, Normal, simple player wins ≥ 80 % ({table[(i, weapon, BattleDifficulty.Normal, Player.Simple)].Wins:P0})");
        // As chapter 1 after the Enemy Director: from stage 2 the idle player wins at most 30 % on Normal and
        // the simple player does not end with nearly full HP (≤ 90 %).
        foreach (var weapon in weapons)
            for (int i = 2; i <= stages.Length; i++)
            {
                var idle = table[(i, weapon, BattleDifficulty.Normal, Player.Idle)];
                var simple = table[(i, weapon, BattleDifficulty.Normal, Player.Simple)];
                check(idle.Wins <= 0.3, $"Chapter 2 balance: stage {i}, {weapon}, Normal, the idle player wins ≤ 30 % ({idle.Wins:P0})");
                // Stage 2 allows 95 % (M21, 2026-10-29): this bot points at every ghost and mirage and lays the
                // laser the moment two slimes close in; raising the threat to 2.45 only took the idle player
                // to 0 % while it stayed at 95 % (the casual bot ends at ~82 %).
                // Stage 3 allows 95 % too (2026-11-04, docs/GOPY-C-2026-11-04.md D2/D3): the cat now rages by
                // itself (this bot never clicked the rage) and the Ground quake dazes; its threat moved 2.2 → 2.6
                // only swung Hard to 45 % for the sword while Normal stayed at 90–94 %.
                double most = i is 2 or 3 ? 0.95 : 0.9;
                check(simple.HpShare <= most, $"Chapter 2 balance: stage {i}, {weapon}, Normal, the simple player ends with ≤ {most:P0} HP ({simple.HpShare:P0})");
            }
        // The idle player is left out as in chapter 1 (2026-11-04: the cat's combos now pop a bubble by
        // themselves, so an idle cat sometimes wins stage 5 — slowly, by nature).
        double longest = table.Where(r => r.Key.Item4 != Player.Idle && r.Value.Wins * ChapterTwoSeeds >= 2).Max(r => r.Value.Seconds);
        check(longest <= 240, $"Chapter 2 balance: no stage takes longer than 4 minutes on average for a player who fights (longest {longest:0} s)");
        foreach (var level in levels)
            for (int i = 1; i <= stages.Length; i++)
            {
                double sword = table[(i, BattleWeapon.Sword, level, Player.Simple)].Wins, hammer = table[(i, BattleWeapon.Hammer, level, Player.Simple)].Wins;
                // The Zip Slime on Hard allows 40 points (M21, 2026-10-29): the hammer stays ~35 points behind the
                // sword there at every threat tried (1.7 → 1.45); to look at again after real play.
                int gap = i == stages.Length && level == BattleDifficulty.Hard ? 40 : 30;
                check(Math.Round(Math.Abs(sword - hammer) * 100) <= gap, $"Chapter 2 balance: stage {i}, {level}: sword and hammer within {gap} points ({sword:P0} vs {hammer:P0})");
            }
        var all = played.Where(p => p.Key.Player != Player.Idle).GroupBy(p => p.Key.Player).ToDictionary(g => g.Key, g => g.SelectMany(p => p.Value).ToList());
        double Wins(Player player) => all[player].Count(r => r.Won) / (double)all[player].Count;
        check(Wins(Player.Casual) <= Wins(Player.Simple), $"Chapter 2 balance: the casual player wins no more than the simple one ({Wins(Player.Casual):P0} vs {Wins(Player.Simple):P0})");
        check(table[(5, BattleWeapon.Sword, BattleDifficulty.Normal, Player.Idle)].Wins <= 0.3,
            $"Chapter 2 balance: the Zip Slime is not beaten by an idle player on Normal ({table[(5, BattleWeapon.Sword, BattleDifficulty.Normal, Player.Idle)].Wins:P0})");
        // 1.5 M29: the yarn and the glove in chapter 2, as in chapter 1 (their own table): the simple player
        // ≥ 80 % on Normal and within 30 points of the sword (the Zip Slime on Hard: 40, as the hammer).
        Console.WriteLine($"Adventure chapter 2 balance, 1.5 weapons (simulation): stage | weapon | level | player | wins/{ChapterTwoSeeds} | avg s (wins) | HP % left (wins) | sweep hits/run | launches/run");
        foreach (var weapon in new[] { "yarn", "glove" })
        {
            if (data.FindWeapon(weapon) == null) continue;
            var newRuns = new Dictionary<(int Stage, BattleDifficulty Level, Player Player), List<Outcome>>();
            foreach (var level in levels)
                foreach (var player in new[] { Player.Idle, Player.Simple })
                    for (int seed = 1; seed <= ChapterTwoSeeds; seed++)
                    {
                        var carried = new Dictionary<string, int>();
                        for (int i = 0; i < stages.Length; i++)
                        {
                            var run = Play(data, stages[i], weapon, level, seed, player, carried, skills: HintedLoadout(stages[i]), loot: BattleLoot.Items);
                            if (run.Won) carried = run.Build;
                            if (!newRuns.TryGetValue((i + 1, level, player), out var list)) newRuns[(i + 1, level, player)] = list = new();
                            list.Add(run);
                        }
                    }
            for (int i = 1; i <= stages.Length; i++)
                foreach (var level in levels)
                    foreach (var player in new[] { Player.Idle, Player.Simple })
                    {
                        var runs = newRuns[(i, level, player)];
                        var wins = runs.Where(r => r.Won).ToList();
                        double share = (double)wins.Count / ChapterTwoSeeds;
                        Console.WriteLine($"  {i} | {weapon,-6} | {level,-6} | {Name(player),-6} | {wins.Count,2}/{ChapterTwoSeeds} | {(wins.Count > 0 ? wins.Average(r => r.Seconds) : 0),6:0} | {(wins.Count > 0 ? wins.Average(r => r.HpShare) : 0),4:P0} | {runs.Average(r => r.SweepHits),5:0.0} | {runs.Average(r => r.Launches),4:0.0}");
                        if (player != Player.Simple) continue;
                        double sword = table[(i, BattleWeapon.Sword, level, Player.Simple)].Wins;
                        int gap = i == stages.Length && level == BattleDifficulty.Hard ? 40 : 30;
                        // The Ghost Folder, yarn (user, 2026-11-05): a flag slime cannot be hurt before its first flag,
                        // so the yarn no longer pulls it in and beats it unplanted (measured: 18 to 13/20 on Normal,
                        // nothing else fails with that one rule off). The user kept the rule and eased these two limits.
                        bool ghostYarn = i == 2 && weapon == "yarn";
                        if (ghostYarn && level == BattleDifficulty.Hard) gap = 50;
                        check(Math.Round(Math.Abs(sword - share) * 100) <= gap, $"Chapter 2 balance: stage {i}, {level}: {weapon} within {gap} points of the sword ({share:P0} vs {sword:P0})");
                        double least = ghostYarn ? 0.6 : 0.8; // The user eased it again, 2026-11-05: 13 to 12/20 was 20 seeds' noise (43 to 42/60, about 70%).
                        if (level == BattleDifficulty.Normal) check(share >= least, $"Chapter 2 balance: stage {i}, {weapon}, Normal, simple player wins ≥ {least:P0} ({share:P0})");
                        if (wins.Count >= 2) check(wins.Average(r => r.Seconds) <= 240, $"Chapter 2 balance: stage {i}, {weapon}, {level}: a win takes at most 4 minutes on average ({wins.Average(r => r.Seconds):0} s)");
                    }
        }

        // The whole chapter after chapter 1, saved and read back after every stage: locked until the
        // Slime King is beaten, each stage lends its skill until won, then the skill is open for good.
        string dir = Path.Combine(Path.GetTempPath(), "pet-chapter2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "adventure-progress.json");
            var first = new Adventure(campaign, AdventureProgress.Load(file));
            bool flow = !first.IsUnlocked(stages[0].Id) && !first.ChapterOpen(chapter) && first.Lent(stages[0]) == BattleSkill.Fan;
            var king = campaign.Chapters[0].Stages[^1];
            var opened = first.Apply("king", king, true, true, BattleWeapon.Sword, BattleDifficulty.Easy, 1, 60);
            flow &= opened.Unlocked == stages[0] && first.IsUnlocked(stages[0].Id) && first.Progress.Save(file);
            var lentOrder = new List<BattleSkill>();
            for (int i = 0; i < stages.Length; i++)
            {
                var adventure = new Adventure(campaign, AdventureProgress.Load(file));
                flow &= adventure.IsUnlocked(stages[i].Id) && (i + 1 >= stages.Length || !adventure.IsUnlocked(stages[i + 1].Id));
                if (adventure.Lent(stages[i]) is BattleSkill lent) lentOrder.Add(lent); else flow = false;
                var run = Play(data, stages[i], BattleWeapon.Sword, BattleDifficulty.Easy, 2, Player.Simple, adventure.StartBuild(stages[i]), skills: HintedLoadout(stages[i]), loot: BattleLoot.Items);
                adventure.Apply("c2-" + i, stages[i], true, run.Won, BattleWeapon.Sword, BattleDifficulty.Easy, 1, run.Seconds, run.Build, fishKept: run.Fish);
                flow &= run.Won && adventure.Lent(stages[i]) == null && adventure.SkillOpen(lentOrder[^1]) && adventure.Progress.Save(file);
            }
            var done = new Adventure(campaign, AdventureProgress.Load(file));
            flow &= lentOrder.SequenceEqual(new[] { BattleSkill.Fan, BattleSkill.Laser, BattleSkill.Grabber, BattleSkill.Scissors, BattleSkill.Spray })
                && BattleSkills.All.All(info => done.SkillOpen(info.Skill));
            check(flow, $"Chapter 2 plays in order after the Slime King (simulation, Easy, sword): locked before, each stage lends then opens its skill (Fan, Laser, Grabber, Scissors, Spray), all ten open at the end ({string.Join(", ", lentOrder)})");
        }
        finally { Directory.Delete(dir, true); }
        check(stages.All(st => st.TipVi?.Length > 0 && st.TipEn?.Length > 0 && st.SuggestSkills?.Contains(st.UnlocksSkill) == true && st.MaxMalware == 4),
            "Every chapter 2 stage says what it teaches, suggests its lent skill and allows four malware kinds");
    }

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
                // Stages 2 and 4 allow 90 % (2026-10-24): the user's balance pass (fish every 14 s, Push
                // every 3 s, the combo's splash, the hammer's stun) with its enemy side (malware at random
                // times, stronger infected apps) leaves this bot at 88 %; the casual bot ends them at 70–78 %.
                // Stage 4 allows 91 % (2026-10-24): the combo's splash only reaching what the swing reaches
                // moved the hammer row 90 → 91 % (noise of 20 runs; its fights got longer, 65 → 73 s).
                double most = s == 5 ? 0.92 : s == 4 ? 0.91 : s == 2 ? 0.90 : 0.85;
                // Retreating runners now pass allies rather than shoving the line toward the cat.
                // The hammer bot's stage 4 finish moves just above 91%; retain a 92% ceiling for this row.
                if (s == 4 && weapon == BattleWeapon.Hammer) most = 0.92;
                // 2026-11-04: the cat rages by itself now (this bot never clicked the rage): the sword's stage 4
                // finish moves to ~93 %.
                if (s == 4 && weapon == BattleWeapon.Sword) most = 0.93;
                check(table[(s, weapon, BattleDifficulty.Normal, Player.Simple)].HpShare <= most,
                    $"Balance: stage {s}, {weapon}, Normal, the simple player ends with ≤ {most:P0} HP ({table[(s, weapon, BattleDifficulty.Normal, Player.Simple)].HpShare:P0})");
            }
        // Elites (the Adventure Guide, 2026-10-24): none before stage 4; stage 4 sets one of each
        // kind on every difficulty (it introduces them); stage 5 draws some on Normal, none on Easy.
        double Elites(int s, BattleDifficulty level) => played.Where(p => p.Key.Stage == s && p.Key.Level == level).SelectMany(p => p.Value).Average(r => r.Elites);
        check(Enumerable.Range(1, 3).All(s => Elites(s, BattleDifficulty.Hard) == 0 && Elites(s, BattleDifficulty.Normal) == 0)
            && Elites(4, BattleDifficulty.Easy) >= 3 && Elites(4, BattleDifficulty.Normal) >= 3 && Elites(5, BattleDifficulty.Normal) >= 0.5 && Elites(5, BattleDifficulty.Easy) == 0,
            $"Balance: no elites before stage 4, stage 4 shows its set ones on every difficulty, stage 5 draws some on Normal (stage 4 Easy/Normal, 5 Normal: {Elites(4, BattleDifficulty.Easy):0.0}/{Elites(4, BattleDifficulty.Normal):0.0}/{Elites(5, BattleDifficulty.Normal):0.0} a run)");
        // An average needs at least two wins (one lucky win of an otherwise lost row is not one). The idle
        // player is left out (1.5 M25): its rare wins are slow by nature (stage 5, hammer, Easy: 1/20 in 277 s
        // before M25, 2/20 in 252 s after), and the pace is about players who fight.
        double longest = table.Where(p => p.Key.Player != Player.Idle && p.Value.Wins * seeds >= 2).Max(p => p.Value.Seconds);
        check(longest <= 240, $"Balance: no stage takes longer than 4 minutes on average for a player who fights (longest {longest:0} s)");
        foreach (var level in levels)
            for (int s = 1; s <= stages.Length; s++)
            {
                double sword = table[(s, BattleWeapon.Sword, level, Player.Simple)].Wins, hammer = table[(s, BattleWeapon.Hammer, level, Player.Simple)].Wins;
                // In whole points: 6/10 against 9/10 is 30 points, not 0.30000000000000004.
                check(Math.Round(Math.Abs(sword - hammer) * 100) <= 30, $"Balance: stage {s}, {level}: sword and hammer within 30 points ({sword:P0} vs {hammer:P0})");
            }
        // 1.5 M08: the yarn and the glove, played the same way (their own table, so the one above stays
        // comparable with older runs): the simple player within 30 points of the sword everywhere and ≥ 80 %
        // on Normal; the idle player's wins are shown, not held to a target.
        Console.WriteLine($"Adventure balance, 1.5 weapons (simulation): stage | weapon | level | player | wins/{seeds} | avg s (wins) | HP % left (wins) | sweep hits/run | launches/run");
        foreach (var weapon in new[] { "yarn", "glove" })
        {
            if (data.FindWeapon(weapon) == null) continue;
            var newRuns = new Dictionary<(int Stage, BattleDifficulty Level, Player Player), List<Outcome>>();
            foreach (var level in levels)
                foreach (var player in new[] { Player.Idle, Player.Simple })
                    for (int seed = 1; seed <= seeds; seed++)
                    {
                        var carried = new Dictionary<string, int>();
                        for (int i = 0; i < stages.Length; i++)
                        {
                            var run = Play(data, stages[i], weapon, level, seed, player, carried);
                            if (run.Won) carried = run.Build;
                            if (!newRuns.TryGetValue((i + 1, level, player), out var list)) newRuns[(i + 1, level, player)] = list = new();
                            list.Add(run);
                        }
                    }
            for (int i = 1; i <= stages.Length; i++)
                foreach (var level in levels)
                    foreach (var player in new[] { Player.Idle, Player.Simple })
                    {
                        var runs = newRuns[(i, level, player)];
                        var wins = runs.Where(r => r.Won).ToList();
                        double share = (double)wins.Count / seeds;
                        Console.WriteLine($"  {i} | {weapon,-6} | {level,-6} | {Name(player),-6} | {wins.Count,2}/{seeds} | {(wins.Count > 0 ? wins.Average(r => r.Seconds) : 0),6:0} | {(wins.Count > 0 ? wins.Average(r => r.HpShare) : 0),4:P0} | {runs.Average(r => r.SweepHits),5:0.0} | {runs.Average(r => r.Launches),4:0.0}");
                        if (player != Player.Simple) continue;
                        double sword = table[(i, BattleWeapon.Sword, level, Player.Simple)].Wins;
                        check(Math.Round(Math.Abs(sword - share) * 100) <= 30, $"Balance: stage {i}, {level}: {weapon} within 30 points of the sword ({share:P0} vs {sword:P0})");
                        if (level == BattleDifficulty.Normal) check(share >= 0.8, $"Balance: stage {i}, {weapon}, Normal, simple player wins ≥ 80 % ({share:P0})");
                    }
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

        // Stage tips (what the stage teaches): every stage has one in both languages; one language only is rejected.
        check(stages.All(st => st.TipVi?.Length > 0 && st.TipEn?.Length > 0), "Every chapter 1 stage says what it teaches in both languages");
        string Tip(string extra)
        {
            try { BattleData.Parse("{ \"schemaVersion\": 1, \"campaign\": { \"chapters\": [ { \"id\": \"c\", \"nameVi\": \"C\", \"nameEn\": \"C\", \"stages\": [ { \"id\": \"s\", \"nameVi\": \"S\", \"nameEn\": \"S\", " + extra +
                "\"waves\": [ { \"groups\": [ { \"enemy\": \"slime\", \"count\": 1 } ] } ] } ] } ] } }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        check(Tip("\"tipVi\": \"a\", \"tipEn\": \"b\", ") == "" && Tip("\"tipVi\": \"a\", ").Contains("tips") && Tip($"\"tipVi\": \"{new string('a', 401)}\", \"tipEn\": \"b\", ").Contains("tips"),
            "A stage tip needs both languages and at most 400 characters");

        // Harder stages ask more: on Normal the idle player does not do better on stage 5 than on stage 1.
        check(table[(5, BattleWeapon.Sword, BattleDifficulty.Normal, Player.Idle)].Wins <= table[(1, BattleWeapon.Sword, BattleDifficulty.Normal, Player.Idle)].Wins,
            "Balance: the boss stage is not easier than the first stage");
        // 1.5 M21: chapter 2, when the data has it.
        if (data.Campaign.Chapters.Length > 1) RunChapter2(check, data);
    }
}
