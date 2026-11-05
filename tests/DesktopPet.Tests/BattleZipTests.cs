using DesktopPet;

// 1.5 M17, B1 the Zip Slime (PetBattle.Zip.cs), stages and riddles redone 2026-11-05, on the shipped numbers.
// Stages: it comes in small, three quick leaps a round, then a roll and a slam, then one huge blow; it is bigger
// at each stage. The press riddle: the rolls, a lift dodging them, a drop on it pinning it, two pins breaking the
// lock (stun, more damage), missing it opening the lock with no reward. The roped phase: its formation comes tied
// to it, no damage while a rope is left, the Scissors and the cat (held or thrown, fast enough) cut ropes, a slow
// drag and a paused battle do not, two cuts together stun it longer. The bomb: the clock, pulls by the cat and the
// Scissors, the flight and reward, the burst sparing a lifted cat and arming again. The data limits.
internal static class BattleZipTests
{
    const double dt = 1.0 / 60;

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static readonly EnemyType ShippedZip = Shipped.Enemy("zip");
    private static readonly ZipSpec Spec = ShippedZip.Zip!;

    private static EnemyType Plain(string id = "slime") => new() { Id = id, MaxHp = 300, Speed = 40, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3 };
    private static readonly FormationSpec Duo = new()
    {
        Id = "duo", NameVi = "Đôi", NameEn = "Duo",
        Parts = new[] { new WaveGroup { Squad = new[] { new WaveGroup { Enemy = "slime", Count = 2 } } } }
    };
    private static BossPhase[] AllPhases => new[]
    {
        new BossPhase { At = 0.66, Shield = true, Press = true },
        new BossPhase { At = 0.33, Shield = true, Roped = true, Formation = "duo" },
        new BossPhase { At = 0.2, Shield = true, Bomb = true }
    };
    private static EnemyType Zip(double damage = 20) => new()
    {
        Id = "zip", MaxHp = 1000, Speed = 20, Damage = damage, Range = 110, Windup = 1.2, AttackCooldown = 2.6, Recover = 0.7, Scale = 2,
        Boss = true, Slam = true, PushFactor = 0.35, StunFactor = 0.4, SummonCap = 10, ShieldBreakStun = 2,
        RopeCutSpeed = ShippedZip.RopeCutSpeed, RopeCutStun = ShippedZip.RopeCutStun, Roll = ShippedZip.Roll, Zip = Spec, Phases = AllPhases
    };

    private static BattleSettings Settings(EnemyType boss) => new()
    {
        Weapons = new[] { Shipped.Weapon("sword") with { Range = 60, StunChance = 0, Damage = 0 } }, FirstWaveDelay = 0, SpawnInterval = 0.05,
        CatMaxHp = 1000, MaxAlive = 8, RageHitGain = 0, ScissorsCooldown = 0.1,
        Enemies = new[] { boss, Plain() }, Formations = new[] { Duo },
        Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "zip", Count = 1 } } } }
    };

    private static readonly BattleSkill[] Tools = { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Bomb, BattleSkill.Scissors, BattleSkill.Shield };

    // The boss in, standing at bossX, the cat at catX.
    private static (BattleSim Sim, Slime Boss) Field(EnemyType boss, double bossX = 1200, double catX = 100)
    {
        var sim = new BattleSim(Settings(boss), 0, 1600, seed: 4, skills: Tools, startHp: 900);
        for (int i = 0; i < 600 && !sim.Slimes.Any(s => s.IsBoss && s.Targetable); i++) sim.Step(dt, catX, false);
        var zip = sim.Slimes.Single(s => s.IsBoss);
        zip.X = bossX;
        sim.ClearEvents();
        return (sim, zip);
    }

    // Steps `seconds` with the cat at catX (held: in the hand past the drag threshold, high up), until an
    // event of `kind` (then true).
    private static bool Run(BattleSim sim, double seconds, double catX = 100, BattleEventKind? kind = null, bool held = false, HashSet<BattleEventKind>? seen = null)
    {
        for (int i = 0; i < Math.Round(seconds / dt); i++)
        {
            sim.Step(dt, catX, held, held, held ? new CatPose(catX, 200, 0, 0, true, false) : null);
            foreach (var e in sim.Events) seen?.Add(e.Kind);
            bool hit = kind is BattleEventKind k && sim.Events.Any(e => e.Kind == k);
            sim.ClearEvents();
            if (hit) return true;
            if (sim.Over) break;
        }
        return false;
    }

    // The cat lifted a moment, then let go at x.
    private static void Drop(BattleSim sim, double x, HashSet<BattleEventKind>? seen = null)
    {
        Run(sim, 0.1, x, held: true, seen: seen);
        Run(sim, dt, x, seen: seen);
    }

    public static void Run(Action<bool, string> check)
    {
        StageChecks(check);
        PressChecks(check);
        RopeChecks(check);
        BombChecks(check);
        WholeFightChecks(check);
        DataChecks(check);
    }

    private static void StageChecks(Action<bool, string> check)
    {
        var stages = Spec.Stages;
        // It comes in small, at its first stage's size and reach.
        var (sim, zip) = Field(Zip());
        check(zip.ZipStage == 0 && Math.Abs(zip.Width - sim.Settings.SlimeWidth * stages[0].Scale) < 1e-6 && zip.Range == stages[0].Range, "It comes in small, at its first stage's size");
        check(stages[0].Scale < stages[1].Scale && stages[1].Scale < stages[2].Scale, "Each stage is bigger than the last");
        check(stages[0].Speed > stages[1].Speed && stages[1].Speed > stages[2].Speed, "And slower");
        check(stages[0].Hits > stages[1].Hits && stages[1].Hits > stages[2].Hits, "With fewer blows a round");
        check(stages[0].Damage < stages[1].Damage && stages[1].Damage < stages[2].Damage, "And heavier ones");

        // A round of three leaps: each lands on the cat, then the rest.
        (sim, zip) = Field(Zip(), bossX: 300, catX: 500);
        double hp = sim.CatHp;
        int leaps = 0;
        bool hopped = false, wasWindup = false;
        double top = 0;
        for (int i = 0; i < 300; i++)
        {
            sim.Step(dt, 500, false);
            sim.ClearEvents();
            hopped |= zip.Hopping;
            top = Math.Max(top, zip.LeapHeight);
            bool windup = zip.State == SlimeState.Windup;
            if (windup && !wasWindup) leaps++;
            wasWindup = windup;
            if (leaps == stages[0].Hits && !windup) break;
        }
        double taken = hp - sim.CatHp;
        check(leaps == 3 && hopped && top > 10, $"The first stage leaps at the cat, three times a round ({leaps}, up {top:0})");
        check(taken > 0 && taken <= 3 * stages[0].Damage * 1.2, $"Each leap lands a light hit ({taken:0})");
        check(zip.ComboLeft == 0 && zip.Cooldown > stages[0].Gap, "Then it rests");

        // A cat lifted during a leap is not hit, and a leap never lands off the field.
        (sim, zip) = Field(Zip(), bossX: 300, catX: 500);
        hp = sim.CatHp;
        Run(sim, 3, 500, held: true);
        check(sim.CatHp == hp, "A lifted cat takes none of its blows");

        // The second stage: a roll then a slam. Its size eases up, not in one jump.
        (sim, zip) = Field(Zip(damage: 0));
        zip.PhasesDone = 1;
        zip.ZipStage = 1;
        zip.Range = stages[1].Range;
        check(!Run(sim, 0.1, 100, BattleEventKind.ZipGrew), "No new stage on its own");
        check(Run(sim, 3, catX: 900, kind: BattleEventKind.RollAimed) && zip.Rolling && zip.DashDirection == -1 && zip.RollDistance == ShippedZip.Roll!.Distance, "The second stage opens with a roll at a far cat");
        check(zip.ComboLeft == stages[1].Hits - 1, "One more blow left in the round");
        var seen = new HashSet<BattleEventKind>();
        Run(sim, ShippedZip.Roll!.Windup + ShippedZip.Roll.Distance / ShippedZip.Roll.Speed + 0.3, catX: 900, seen: seen);
        check(seen.Contains(BattleEventKind.Rolled) && seen.Contains(BattleEventKind.RollHit) && sim.RollHits == 1, "It rolls over the cat");
        bool slam = false;
        for (int i = 0; i < 300 && !slam; i++) { sim.Step(dt, 900, false); sim.ClearEvents(); slam = zip.State == SlimeState.Windup && !zip.Rolling; }
        check(slam, "Then the round's slam");

        // The last stage: no leap, one blow in place, its share of the stage's damage.
        (sim, zip) = Field(Zip());
        zip.PhasesDone = 3;
        zip.ZipStage = 2;
        zip.Range = stages[2].Range;
        hp = sim.CatHp;
        double bossX = zip.X;
        Run(sim, stages[2].Windup + 0.2, bossX - 100);
        check(zip.X == bossX && !zip.Hopping && Math.Abs(hp - sim.CatHp - stages[2].Damage) < stages[2].Damage * 0.3, $"The last stage's one blow lands where it stands ({hp - sim.CatHp:0})");
    }

    // At the press riddle, the boss's first roll under way or about to be.
    private static (BattleSim Sim, Slime Boss, HashSet<BattleEventKind> Seen) Pressing(double damage = 0)
    {
        var (sim, zip) = Field(Zip(damage), bossX: 800, catX: 400);
        var seen = new HashSet<BattleEventKind>();
        sim.Damage(zip, zip.MaxHp * 0.5, DamageSource.App);
        foreach (var e in sim.Events) seen.Add(e.Kind);
        sim.ClearEvents();
        return (sim, zip, seen);
    }

    private static bool ToStop(BattleSim sim, Slime zip, double catX)
    {
        for (int i = 0; i < 600 && zip.PressWindow <= 0; i++) { sim.Step(dt, catX, true, true, new CatPose(catX, 200, 0, 0, true, false)); sim.ClearEvents(); }
        return zip.PressWindow > 0;
    }

    private static void PressChecks(Action<bool, string> check)
    {
        var press = Spec.Press;
        var (sim, zip, seen) = Pressing();
        check(zip.Shielded && Math.Abs(zip.Hp - zip.MaxHp * 0.66) < 1e-6 && seen.Contains(BattleEventKind.ZipPressed), "A hit stops at the line and it locks itself");
        double before = zip.Hp;
        check(sim.Damage(zip, 50, DamageSource.Weapon) == HitResult.Immune && zip.Hp == before, "Locked, it takes no damage");
        check(zip.ZipStage == 0, "Still small");

        // It rolls even with the cat in the air: the lift is how the riddle is solved.
        check(ToStop(sim, zip, 300) && zip.PressLeft == press.Rolls - 1, "It rolls at a lifted cat too, and stops");
        check(BattleSim.PressSpot(zip) == zip.X, "The spot to drop on is where it stopped");

        // A drop far off does nothing; a drop on it pins it, not a shot at the shield.
        Drop(sim, zip.X + 400);
        check(sim.ZipPins == 0, "A drop far from it pins nothing");
        (sim, zip, seen) = Pressing();
        ToStop(sim, zip, 300);
        Drop(sim, zip.X, seen);
        check(sim.ZipPins == 1 && zip.Pins == 1 && zip.State == SlimeState.Stunned && zip.Shielded && seen.Contains(BattleEventKind.ZipPinned), "A drop on it pins it; the lock holds");
        double hp = sim.CatHp;
        check(ToStop(sim, zip, 300), "It rolls again");
        seen.Clear();
        Drop(sim, zip.X, seen);
        check(sim.ZipPins == 2 && !zip.Shielded && seen.Contains(BattleEventKind.ZipUnlocked) && seen.Contains(BattleEventKind.ZipGrew), "The second pin breaks the lock");
        check(zip.State == SlimeState.Stunned && zip.ZipStage == 1 && zip.MechBonusClock > 0 && zip.Range == Spec.Stages[1].Range, "Dazed, its next stage on");
        before = zip.Hp;
        check(sim.Damage(zip, 10, DamageSource.App) == HitResult.Landed && Math.Abs(before - zip.Hp - 10 * press.Bonus) < 1e-6, "Taking more damage for a while");
        double width = zip.Width;
        Run(sim, 0.3);
        check(zip.Width > width && zip.Width <= sim.Settings.SlimeWidth * Spec.Stages[1].Scale + 1e-6, "Swelling up bit by bit");
        Run(sim, press.Stun + Spec.Grow);
        check(Math.Abs(zip.Width - sim.Settings.SlimeWidth * Spec.Stages[1].Scale) < 1e-6 && zip.MechBonusClock == 0, "To its new size, the reward over");

        // A drop onto it as it rolls stops the roll short.
        (sim, zip, seen) = Pressing();
        for (int i = 0; i < 600 && zip.State != SlimeState.Dashing; i++) { sim.Step(dt, 300, true, true, new CatPose(300, 200, 0, 0, true, false)); sim.ClearEvents(); }
        Drop(sim, zip.X);
        check(sim.ZipPins == 1 && !zip.Rolling, "A drop on it mid-roll pins it too");

        // A roll over the cat on the floor hurts; missing every chance opens the lock, no reward.
        (sim, zip, seen) = Pressing(damage: 20);
        double start = sim.CatHp;
        seen.Clear();
        bool opened = Run(sim, press.Rolls * (press.Roll.Windup + press.Roll.Distance / press.Roll.Speed + press.Window + 1) + 2, catX: 400, kind: BattleEventKind.ZipUnlocked, seen: seen);
        check(opened && !zip.Shielded && zip.ZipStage == 1 && zip.State != SlimeState.Stunned && zip.MechBonusClock == 0 && !seen.Contains(BattleEventKind.BossShieldDown),
            "Out of rolls the lock opens by itself: its next stage, no reward");
        check(sim.CatHp < start && seen.Contains(BattleEventKind.RollHit), "A cat left on the floor is rolled over");

        // A stun cutting a roll short never leaves the riddle hanging.
        (sim, zip, seen) = Pressing();
        zip.PressLeft = 1;
        for (int i = 0; i < 600 && zip.State != SlimeState.Dashing; i++) { sim.Step(dt, 300, true, true, new CatPose(300, 200, 0, 0, true, false)); sim.ClearEvents(); }
        zip.StunRemaining = 0.5;
        zip.Set(SlimeState.Stunned);
        check(Run(sim, 2, 300, BattleEventKind.ZipUnlocked) && !zip.Shielded, "A stunned last roll still ends the riddle");

        // Paused, the riddle waits.
        (sim, zip, seen) = Pressing();
        double left = zip.Cooldown;
        sim.Paused = true;
        Run(sim, 1, 300);
        sim.Paused = false;
        check(zip.Cooldown == left && zip.PressLeft == press.Rolls, "A paused battle freezes it");
    }

    // A rope crossed in the middle by a stroke (or the cat's way) going down through it.
    private static (double X, double Top, double Bottom) Across(Slime helper, Slime boss) => ((helper.X + boss.X) / 2, 70, 0);

    private static bool Through(BattleSim sim, CatPose from, CatPose to, bool lifted = true)
    {
        bool cut = false;
        foreach (var pose in new[] { from, to })
        {
            sim.Step(dt, 100, true, lifted, pose);
            cut |= sim.Events.Any(e => e.Kind == BattleEventKind.BossRopeCut);
            sim.ClearEvents();
        }
        return cut;
    }

    // The roped phase begun, its two helpers tied to the boss.
    private static (BattleSim Sim, Slime Boss, Slime[] Helpers, HashSet<BattleEventKind> Seen) Roped()
    {
        var (sim, zip) = Field(Zip(damage: 0));
        zip.PhasesDone = 1;
        zip.ZipStage = 1;
        zip.Range = Spec.Stages[1].Range;
        var seen = new HashSet<BattleEventKind>();
        sim.Damage(zip, zip.MaxHp * 0.8, DamageSource.App);
        foreach (var e in sim.Events) seen.Add(e.Kind);
        sim.ClearEvents();
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.RopedTo == zip.Id && s.Targetable) < 2; i++)
        {
            sim.Step(dt, 100, false);
            foreach (var e in sim.Events) seen.Add(e.Kind);
            sim.ClearEvents();
        }
        // One rope on each side of the boss, so a stroke through one never meets the other.
        var helpers = sim.Slimes.Where(s => s.RopedTo == zip.Id).ToArray();
        zip.X = 800;
        if (helpers.Length == 2) { helpers[0].X = 560; helpers[1].X = 1040; }
        return (sim, zip, helpers, seen);
    }

    private static void RopeChecks(Action<bool, string> check)
    {
        var (sim, zip, helpers, seen) = Roped();
        check(zip.Shielded && Math.Abs(zip.Hp - zip.MaxHp * 0.33) < 1e-6, "A hit stops at the roped phase's line and the shield goes up");
        check(helpers.Length == 2 && helpers.All(h => sim.RopeBoss(h) == zip) && sim.RopesLeft(zip) == 2,
            "Its formation comes tied to it by ropes, one each");
        check(seen.Contains(BattleEventKind.BossRoped) && seen.Contains(BattleEventKind.FormationArrived) && sim.FormationsMet == 1,
            "The ropes and the formation are announced");
        double before = zip.Hp;
        check(sim.Damage(zip, 50, DamageSource.Weapon) == HitResult.Immune && zip.Hp == before, "While a rope is left it takes no damage");
        Run(sim, 0.5);
        check(zip.Shielded, "The shield holds while the ropes stay");

        // A slow drag, a cat only pressed, a paused battle: the ropes stay.
        double fast = zip.Type.RopeCutSpeed * 2, slow = zip.Type.RopeCutSpeed * 0.5;
        var (x, top, bottom) = Across(helpers[0], zip);
        check(!Through(sim, new CatPose(x, top, 0, -slow, true, false), new CatPose(x, bottom, 0, -slow, true, false)) && sim.RopesLeft(zip) == 2,
            $"A slow drag (under {zip.Type.RopeCutSpeed} DIPs/s) leaves the rope");
        check(!Through(sim, new CatPose(x, top, 0, -fast, true, false), new CatPose(x, bottom, 0, -fast, true, false), lifted: false), "A cat only pressed leaves it");
        sim.Paused = true;
        bool paused = Through(sim, new CatPose(x, top, 0, -fast, true, false), new CatPose(x, bottom, 0, -fast, true, false));
        sim.Paused = false;
        check(!paused && sim.RopesLeft(zip) == 2, "A paused battle cuts nothing");

        // The Scissors cut one: its helper is dazed, the shield holds on the other rope.
        Run(sim, 0.1); // The cat put down: the mouse skills wait while it is held.
        (x, top, bottom) = Across(helpers[0], zip);
        sim.Arm(BattleSkill.Scissors);
        // (2026-11-04: a cut across at the rope's height; the blades stop short of the other rope.)
        helpers[1].X = helpers[0].X > zip.X ? zip.X - 300 : zip.X + 300;
        bool snipped = sim.UseOnScreen(BattleSkill.Scissors, x + (helpers[0].X > zip.X ? 180 : -180), (top + bottom) / 2);
        for (int i = 0; i < 120 && sim.Cut != null; i++) { sim.Step(1.0 / 60, 100, false); sim.ClearEvents(); }
        check(snipped && helpers[0].RopedTo == 0 && helpers[0].State == SlimeState.Stunned && sim.BossRopesCut == 1,
            "A Scissors cut through a rope cuts it and dazes its helper");
        Run(sim, 0.3);
        check(zip.Shielded && sim.RopesLeft(zip) == 1, "One rope left: the shield holds");

        // The held cat dragged fast through the last one: the shield drops, the helpers fight on, the last stage on.
        (x, top, bottom) = Across(helpers[1], zip);
        check(Through(sim, new CatPose(x, top, 0, -fast, true, false), new CatPose(x, bottom, 0, -fast, true, false)) && sim.CatRopeCuts == 1,
            "The held cat dragged fast through a rope cuts it");
        seen.Clear();
        Run(sim, 0.3, seen: seen);
        check(!zip.Shielded && seen.Contains(BattleEventKind.BossRopesGone) && seen.Contains(BattleEventKind.BossShieldDown) && helpers.All(h => h.Targetable),
            "The last rope gone, the shield drops; its helpers fight on");
        check(zip.ZipStage == 2 && seen.Contains(BattleEventKind.ZipGrew), "And it swells to its last stage");
        check(!seen.Contains(BattleEventKind.ZipRopeCombo) && zip.StunRemaining <= zip.Type.ShieldBreakStun, "Cuts far apart earn no extra stun");

        // Two ropes cut together stun it longer.
        (sim, zip, helpers, seen) = Roped();
        (x, top, bottom) = Across(helpers[0], zip);
        var (x2, _, _) = Across(helpers[1], zip);
        seen.Clear();
        Through(sim, new CatPose(x, top, 0, -fast, true, false), new CatPose(x, bottom, 0, -fast, true, false));
        Through(sim, new CatPose(x2, top, 0, -fast, true, false), new CatPose(x2, bottom, 0, -fast, true, false));
        Run(sim, 0.1, seen: seen);
        check(zip.BossRopesCutTogether(), "Two cuts together earn the extra stun");

        // The thrown cat flying through a rope cuts it too; defeating a helper ends its rope.
        (sim, zip, helpers, _) = Roped();
        (x, top, bottom) = Across(helpers[0], zip);
        check(Through(sim, new CatPose(x - 120, top, fast, -fast / 3, false, true), new CatPose(x + 120, bottom, fast, -fast / 3, false, true)) && helpers[0].RopedTo == 0,
            "The thrown cat flying through a rope cuts it");
        sim.Damage(helpers[1], helpers[1].Hp + 1, DamageSource.App);
        Run(sim, 0.3);
        check(!zip.Shielded && sim.RopesLeft(zip) == 0, "Defeating the last tied helper ends its rope and drops the shield");
    }

    private static bool BossRopesCutTogether(this Slime zip) =>
        Math.Abs(zip.StunRemaining - (zip.Type.ShieldBreakStun + Spec.RopeComboStun)) < 0.2 && zip.State == SlimeState.Stunned;

    // The bomb armed on its boss (the last stage, the first two phases done).
    private static (BattleSim Sim, Slime Boss) Bomb(double damage = 0)
    {
        var (sim, zip) = Field(Zip(damage), bossX: 800, catX: 400);
        zip.PhasesDone = 3;
        zip.ZipStage = 2;
        zip.Range = Spec.Stages[2].Range;
        sim.Damage(zip, zip.MaxHp * 0.9, DamageSource.App);
        sim.ClearEvents();
        return (sim, zip);
    }

    // The cat flung through the tab; true when it was pulled.
    private static bool Pull(BattleSim sim, Slime zip, double speed = 900, bool lifted = true)
    {
        var (x, h) = BattleSim.BombTab(zip)!.Value;
        int pulls = zip.BombPulls;
        sim.Step(dt, x, true, lifted, new CatPose(x - 40, h, speed, 0, true, false));
        sim.Step(dt, x, true, lifted, new CatPose(x + 40, h, speed, 0, true, false));
        sim.ClearEvents();
        Run(sim, 0.6, 400);
        return zip.BombPulls > pulls || zip.BombClock <= 0;
    }

    private static void BombChecks(Action<bool, string> check)
    {
        var bomb = Spec.Bomb;
        var (sim, zip) = Bomb();
        check(zip.Shielded && Math.Abs(zip.Hp - zip.MaxHp * 0.2) < 1e-6 && BattleSim.BombFill(zip) is >= 0 and < 0.1 && BattleSim.BombTab(zip) != null, "At its line the bomb arms: a shield, a clock, a tab");
        check(sim.Damage(zip, 50, DamageSource.Weapon) == HitResult.Immune, "It takes no damage then");
        var (tabX, tabHeight) = BattleSim.BombTab(zip)!.Value;
        check(Math.Abs(tabX - zip.X) > zip.Width / 2 && tabHeight is > 20 and < 110, $"The tab hangs off it within the cat's reach ({tabX - zip.X:0}, {tabHeight:0})");
        check(!Pull(sim, zip, speed: zip.Type.RopeCutSpeed * 0.5), "A slow drag does not pull it");
        check(!Pull(sim, zip, lifted: false), "Nor a cat only pressed");
        double fill = BattleSim.BombFill(zip)!.Value;
        sim.Paused = true;
        Run(sim, 1, 400);
        sim.Paused = false;
        check(BattleSim.BombFill(zip) == fill, "Paused, its clock stops");

        check(Pull(sim, zip) && zip.BombPulls == 1, "The cat dragged fast through the tab pulls it");
        var (nextX, _) = BattleSim.BombTab(zip)!.Value;
        check(Math.Sign(nextX - zip.X) != Math.Sign(tabX - zip.X), "The tab swings to its other side");
        check(sim.Arm(BattleSkill.Scissors) && sim.UseOnScreen(BattleSkill.Scissors, nextX + 90, BattleSim.BombTab(zip)!.Value.Height), "The Scissors reach it");
        for (int i = 0; i < 120 && sim.Cut != null; i++) { sim.Step(dt, 400, false); sim.ClearEvents(); }
        check(zip.BombPulls == 2, "And pull it too");
        var seen = new HashSet<BattleEventKind>();
        Run(sim, 0.6, 400);
        Pull(sim, zip);
        check(zip.BombDone && zip.Deflating > 0 && BattleSim.BombFill(zip) == null && zip.Shielded, "The last pull lets its air out");
        double x0 = zip.X, far = 0;
        bool hopped = false;
        for (int i = 0; i < 200 && zip.Deflating > 0; i++) { sim.Step(dt, 400, false); sim.ClearEvents(); far = Math.Max(far, Math.Abs(zip.X - x0)); hopped |= zip.LeapHeight > 0; }
        check(far > 300 && hopped, $"It flies round the field ({far:0})");
        check(!zip.Shielded && zip.State == SlimeState.Stunned && zip.MechBonusClock > 0, "Then lies dazed, its shield gone");
        double before = zip.Hp;
        check(sim.Damage(zip, 10, DamageSource.App) == HitResult.Landed && Math.Abs(before - zip.Hp - 10 * bomb.Bonus) < 1e-6, "Taking more damage");
        Run(sim, bomb.Rearm + bomb.Stun + 1, 400, seen: seen);
        check(!seen.Contains(BattleEventKind.ZipBombArmed), "Solved, the bomb never arms again");

        // Time out: the floor blows; a lifted cat is spared; it arms again later.
        (sim, zip) = Bomb(damage: 20);
        double hp = sim.CatHp;
        seen = new HashSet<BattleEventKind>();
        check(Run(sim, bomb.Charge + 1, 400, BattleEventKind.ZipBurst, seen: seen) && sim.ZipBursts == 1, "Time out: it bursts");
        check(sim.CatHp < hp && sim.CatStunned && !zip.Shielded && !seen.Contains(BattleEventKind.BossShieldDown), "The cat on the floor takes it, stunned; no shield-break banner");
        Run(sim, 0.1, 400);
        before = zip.Hp;
        check(sim.Damage(zip, 5, DamageSource.App) == HitResult.Landed && zip.Hp < before, "It takes damage between bombs");
        (sim, zip) = Bomb(damage: 20);
        hp = sim.CatHp;
        check(Run(sim, bomb.Charge + 1, 400, BattleEventKind.ZipBurst, held: true) && sim.CatHp == hp, "A lifted cat is spared the burst");
        check(Run(sim, bomb.Rearm + 1, 400, BattleEventKind.ZipBombArmed, held: true) && zip.Shielded && BattleSim.BombFill(zip) != null, "It arms again after a while");
    }

    private static void WholeFightChecks(Action<bool, string> check)
    {
        // The three stages in order, to the end, no riddle solved: nothing hangs.
        var (sim, zip) = Field(Zip(damage: 0));
        int shields = 0;
        double top = zip.Width;
        for (int step = 0; step < 36000 && !sim.Over; step++)
        {
            if (!zip.Shielded) sim.Damage(zip, 4, DamageSource.App);
            foreach (var s in sim.Slimes.Where(s => !s.IsBoss && s.Targetable).ToArray()) sim.Damage(s, s.Hp + 1, DamageSource.App);
            sim.Step(dt, 100, false);
            shields += sim.Events.Count(e => e.Kind == BattleEventKind.BossShielded);
            top = Math.Max(top, zip.Width);
            sim.ClearEvents();
        }
        check(sim.BossDefeated && shields == 3 && zip.ZipStage == 2 && Math.Abs(top - sim.Settings.SlimeWidth * Spec.Stages[2].Scale) < 1e-6,
            $"Its stages run in order, it grows to full size and it can be beaten with no riddle solved ({shields} riddles, {sim.Time:0} s)");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        const string slime = "{\"id\":\"slime\",\"nameVi\":\"S\",\"nameEn\":\"S\"}";
        static string Enemy(string id, string extra) => $"{{\"id\":\"{id}\",\"nameVi\":\"T\",\"nameEn\":\"T\",{extra}}}";
        const string formation = ",\"formations\":[{\"id\":\"duo\",\"nameVi\":\"D\",\"nameEn\":\"D\",\"parts\":[{\"enemy\":\"slime\",\"count\":2}]}]";
        bool Rejects(string enemies, string more = formation)
        {
            try { BattleData.Parse("{\"enemies\":[" + slime + "," + enemies + "]" + more + ",\"waves\":[{\"groups\":[{\"enemy\":\"a\",\"count\":1}]}]}"); return false; }
            catch (InvalidDataException) { return true; }
        }
        const string boss = "\"boss\":true,", zip1 = "\"zip\":{\"stages\":[{}]},";
        check(!Rejects(Enemy("a", boss + "\"roll\":{}," + zip1 + "\"phases\":[{\"at\":0.6,\"shield\":true,\"press\":true},{\"at\":0.4,\"shield\":true,\"roped\":true,\"formation\":\"duo\"},{\"at\":0.2,\"shield\":true,\"bomb\":true}]")),
            "A boss with stages, a press, a roped formation and a bomb phase is accepted");
        check(Rejects(Enemy("a", "\"roll\":{}")) && Rejects(Enemy("a", boss + "\"roll\":{\"minRange\":600,\"range\":500}")) && Rejects(Enemy("a", boss + "\"roll\":{\"speed\":0}")),
            "A roll on a plain enemy, or with numbers out of range, is refused");
        check(Rejects(Enemy("a", boss + "\"phases\":[{\"at\":0.5,\"shield\":true,\"formation\":\"nobody\"}]")) && Rejects(Enemy("a", boss + "\"phases\":[{\"at\":0.5,\"roped\":true,\"summon\":\"slime\",\"count\":1}]"))
              && Rejects(Enemy("a", boss + zip1 + "\"phases\":[{\"at\":0.5,\"shield\":true,\"roped\":true,\"press\":true,\"summon\":\"slime\",\"count\":1}]")),
            "An unknown formation, a roped phase without a shield, two riddles in one phase are refused");
        check(Rejects(Enemy("a", boss + "\"phases\":[{\"at\":0.5,\"shield\":true,\"press\":true}]")) && Rejects(Enemy("a", boss + zip1 + "\"phases\":[{\"at\":0.5,\"press\":true}]"))
              && Rejects(Enemy("a", boss + zip1 + "\"phases\":[{\"at\":0.5,\"shield\":true,\"bomb\":true,\"summon\":\"slime\",\"count\":1}]")),
            "A riddle needs a zip block and a shield, and calls no helpers");
        check(Rejects(Enemy("a", zip1 + "\"damage\":1")) && Rejects(Enemy("a", boss + "\"zip\":{\"stages\":[]}")) && Rejects(Enemy("a", boss + "\"zip\":{\"stages\":[{\"scale\":9}]}"))
              && Rejects(Enemy("a", boss + "\"zip\":{\"stages\":[{\"roll\":true}]}")) && Rejects(Enemy("a", boss + "\"zip\":{\"stages\":[{}],\"press\":{\"rolls\":1,\"pins\":2}}")) && Rejects(Enemy("a", boss + "\"zip\":{\"stages\":[{}],\"bomb\":{\"charge\":1}}")),
            "Stages on a plain enemy, or out of range, or a riddle's numbers out of range, are refused");
        check(Rejects(Enemy("a", boss + "\"still\":true")) && Rejects(Enemy("a", "\"still\":true,\"dash\":true")) && Rejects(Enemy("a", boss + "\"ropeCutSpeed\":10")),
            "Still on a boss or an attacker, a rope cut speed out of range, are refused");

        var s = Shipped;
        var zip = s.Enemy("zip");
        var p = zip.Phases ?? Array.Empty<BossPhase>();
        check(zip is { Boss: true, Roll: not null, BinSpit: false, Zip.Stages.Length: 3 } && p.Length == 3 && p[0] is { Press: true } && p[1] is { Roped: true, Formation: "fortress" } && p[2] is { Bomb: true },
            "The shipped Zip Slime: three stages, the press, Fortress on ropes, then the bomb");
        check(p.All(ph => s.PhaseCalls(ph).Count() <= zip.SummonCap), "Each of its phases fits its summon cap");
        check(s.Enemies.All(e => e.Id != "zipshard"), "No more file shards");
        check(Spec.Stages.All(st => s.SlimeHeight * st.Scale * 1.15 <= 130), "Even swollen it fits under the field's top");
        bool Uses(WaveGroup g) => g.Enemy == "zip" || g.Pick?.Keys.Contains("zip") == true || g.Squad?.Any(m => m.Enemy == "zip") == true;
        var all = s.WavePlan.Concat(s.Campaign?.Chapters.Take(1).SelectMany(ch => ch.Stages).SelectMany(st => st.Waves) ?? Enumerable.Empty<WaveSpec>());
        check(!all.SelectMany(w => w.Groups).Any(Uses) && s.Endless?.Boss != "zip", "No built-in battle uses it outside chapter 2 and later");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m17.json"))) dir = dir.Parent;
        var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", "battle-m17.json"));
        check(load is { Custom: true, Error: null } && load.Settings.WavePlan.SelectMany(w => w.Groups).Any(g => g.Enemy == "zip"),
            $"docs/examples/battle-m17.json loads as custom data with the Zip Slime ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
    }
}
