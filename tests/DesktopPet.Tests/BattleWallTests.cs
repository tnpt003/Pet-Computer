using DesktopPet;

// 1.5 M15 (PetBattle.Wall.cs), with stand-ins that carry the shipped ice, cling and throwHit numbers. The
// ice slime: its shell turns the cat's weapon into scratches (the cat goes for others first) while bombs
// go through; the thrown cat breaks it fast enough, slower when the spray has it wet; it grows back. The
// wall climber: it goes to the nearest edge and climbs it (out of the floor's reach), aims at the cat's
// spot and drops spikes that hurt the cat on the floor only; the thrown cat, Push and the Grabber knock
// it off, it lies dazed, fights on the floor a while and climbs again. The data limits, the shipped kinds
// (kept out of every battle for now) and the try-out file.
internal static class BattleWallTests
{
    const double dt = 1.0 / 60;

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static EnemyType Icy(double damage = 10) => new()
    {
        Id = "icy", MaxHp = 300, Speed = 40, Damage = damage, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3,
        Elite = new EliteSpec { Hp = 1.2 }, Ice = Shipped.Enemy("icy").Ice, ThrowHit = Shipped.Enemy("icy").ThrowHit
    };
    // Still on its edge between volleys unless `shift` (the shipped slide is checked on its own).
    private static EnemyType Clinger(double damage = 10, bool shift = false) => new()
    {
        Id = "clinger", MaxHp = 300, Speed = 40, Damage = damage, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3,
        Elite = new EliteSpec { Hp = 1.2 }, Cling = Shipped.Enemy("clinger").Cling! with { Shift = shift ? Shipped.Enemy("clinger").Cling!.Shift : 0 }, ThrowHit = Shipped.Enemy("clinger").ThrowHit
    };
    private static EnemyType Plain() => new() { Id = "slime", MaxHp = 300, Speed = 40, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3 };

    private static readonly BattleSkill[] Tools = { BattleSkill.Bomb, BattleSkill.Shield, BattleSkill.Push, BattleSkill.Grabber, BattleSkill.Spray };

    // `count` enemies of each kind on a 1600-wide field, the cat at 100 (weapon damage `hit`, reach 60).
    private static BattleSim Field(EnemyType[] kinds, double hit = 0, int count = 1, bool elite = false, int seed = 4)
    {
        var arms = Shipped.Weapon("sword") with { Range = 60, StunChance = 0, Damage = hit };
        var settings = new BattleSettings
        {
            Weapons = new[] { arms }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1000, MaxAlive = 8, MaxElites = 8,
            Enemies = kinds, RageHitGain = 0, BombDamage = 10, BombCooldown = 0.1, BombDelay = 0.3, BombRadius = 40,
            PushCooldown = 0.1, GrabberCooldown = 0.1, ShieldCooldown = 0.1,
            Plan = new[] { new WaveSpec { Groups = kinds.Select(k => new WaveGroup { Enemy = k.Id, Count = count, Elite = elite }).ToArray() },
                new WaveSpec { Groups = new[] { new WaveGroup { Enemy = kinds[0].Id, Count = 1 } } } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: seed, skills: Tools);
        int all = kinds.Length * count;
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < all; i++) sim.Step(dt, 100, false);
        sim.ClearEvents();
        return sim;
    }
    private static BattleSim Field(EnemyType kind, double hit = 0, int count = 1, bool elite = false, int seed = 4) => Field(new[] { kind }, hit, count, elite, seed);

    // Steps `seconds` with the cat at catX (or up to the step an event of `kind` came: then true).
    private static bool Run(BattleSim sim, double seconds, double catX = 100, BattleEventKind? kind = null, bool held = false, CatPose? pose = null, Action? each = null)
    {
        for (int i = 0; i < Math.Round(seconds / dt); i++)
        {
            each?.Invoke();
            sim.Step(dt, catX, held, held, held ? pose : null);
            bool seen = kind is BattleEventKind k && sim.Events.Any(e => e.Kind == k);
            sim.ClearEvents();
            if (seen) return true;
            if (sim.Over) break;
        }
        return false;
    }

    // The thrown cat flying from one pose to the next, then landing at the last x.
    private static void Throw(BattleSim sim, CatPose from, CatPose to, HashSet<BattleEventKind>? seen = null)
    {
        foreach (var pose in new[] { from, to })
        {
            sim.Step(dt, pose.X, true, true, pose);
            if (seen != null) foreach (var e in sim.Events) seen.Add(e.Kind);
            sim.ClearEvents();
        }
        sim.Step(dt, to.X, false, false, null);
        if (seen != null) foreach (var e in sim.Events) seen.Add(e.Kind);
        sim.ClearEvents();
    }
    // Fast along the floor through x, left to right.
    private static void ThrowThrough(BattleSim sim, double x, double speed, HashSet<BattleEventKind>? seen = null, double height = 20) =>
        Throw(sim, new CatPose(x - 100, height, speed, 0, false, true), new CatPose(x + 100, height, speed, 0, false, true), seen);

    public static void Run(Action<bool, string> check)
    {
        IceChecks(check);
        ClingChecks(check);
        KnockChecks(check);
        WatchChecks(check);
        DataChecks(check);
    }

    // 2026-11-04: with only a wall climber up its edge left (or one chosen), the cat walks near that edge and
    // stands looking up, WallWatchGap from it, never against the edge.
    private static void WatchChecks(Action<bool, string> check)
    {
        var sim = Field(Clinger(damage: 0));
        var s = sim.Slimes.Single();
        bool told = false;
        for (int i = 0; i < 1200 && !(s.OnWall && s.Airborne); i++) { sim.Step(dt, 100, false); told |= sim.Events.Any(e => e.Kind == BattleEventKind.WallWatched); sim.ClearEvents(); }
        double spot = s.WallSide < 0 ? sim.Left + BattleSim.WallWatchGap : sim.Right - BattleSim.WallWatchGap;
        var order = sim.Step(dt, 800, false);
        told |= sim.Events.Any(e => e.Kind == BattleEventKind.WallWatched);
        sim.ClearEvents();
        check(s.OnWall && order.Command == CatCommand.Move && Math.Abs(order.X - spot) < 1 && sim.WatchedWallId == s.Id && told,
            $"Only a wall climber left: the cat walks to {BattleSim.WallWatchGap} DIPs from its edge ({order.Command} to {order.X:0})");
        order = sim.Step(dt, spot, false);
        check(order.Command == CatCommand.Stand && order.Facing == s.WallSide, "There it stands and looks up at the climber");
        order = sim.Step(dt, spot, false);
        check(!sim.Events.Any(e => e.Kind == BattleEventKind.WallWatched), "The hint comes once per climber");
        sim.ClearEvents();

        // A floor enemy too: the cat goes for it; the climber chosen: the cat goes to watch it instead
        // (unless the floor enemy is already within reach).
        sim = Field(new[] { Clinger(damage: 0), Plain() });
        var up = sim.Slimes.Single(x => x.Type.Id == "clinger");
        var floor = sim.Slimes.Single(x => x.Type.Id == "slime");
        for (int i = 0; i < 1200 && !(up.OnWall && up.Airborne); i++) { floor.X = 800; sim.Step(dt, 400, false); sim.ClearEvents(); }
        floor.X = 800;
        sim.Step(dt, 400, false);
        check(sim.WatchedWallId == 0 && sim.TargetId == floor.Id, "With a floor enemy left the cat fights it, not the wall");
        sim.Select(up.Id);
        floor.X = 800;
        order = sim.Step(dt, 400, false);
        check(sim.WatchedWallId == up.Id && order.Command == CatCommand.Move, "The climber chosen: the cat goes to watch it");
        floor.X = 410;
        order = sim.Step(dt, 400, false);
        check(sim.WatchedWallId == 0, "A floor enemy within reach is hit first");
    }

    private static void IceChecks(Action<bool, string> check)
    {
        var ice = Shipped.Enemy("icy").Ice!;
        var rule = Shipped.Enemy("icy").ThrowHit!;
        // The cat's weapon only scratches the shell.
        var sim = Field(Icy(), hit: 20);
        var s = sim.Slimes.Single();
        check(s.Iced, "An ice slime comes in with its shell on");
        s.X = 160;
        double before = s.Hp;
        Run(sim, 4, each: () => s.ShotClock = 0);
        double lost = before - s.Hp;
        check(sim.IceScratches > 0 && lost > 0 && lost <= sim.IceScratches * 20 * ice.Armor * 2.5 + 1e-6,
            $"The cat's blows only scratch it ({sim.IceScratches} blows, lost {lost:0.#})");
        // A bomb goes through.
        before = s.Hp;
        sim.Use(BattleSkill.Bomb, s.Id, 100);
        Run(sim, 0.5, catX: 1500);
        check(Math.Abs(before - s.Hp - 10) < 1e-6, $"A yarn bomb goes through the shell (lost {before - s.Hp:0.#})");

        // 2026-11-04: a throw skimming the floor (the cat's centre 56 DIPs up, as on the screen) touches it,
        // and any throw into the shell bonks: the cat bounces back. Too slow to crack: only the bonk.
        sim = Field(Icy());
        s = sim.Slimes.Single();
        s.X = 800;
        before = s.Hp;
        double slow = rule.MinSpeed * BattleSim.CrackSpeed * 0.8;
        sim.Step(dt, 700, true, true, new CatPose(700, 56, slow, 0, false, true));
        sim.ClearEvents();
        sim.Step(dt, 900, true, true, new CatPose(900, 56, slow, 0, false, true));
        bool bonked = sim.Events.Any(e => e.Kind == BattleEventKind.IceBonked);
        var bounce = sim.TakeCatBounce();
        check(s.Iced && s.Hp == before && sim.IceBreaks == 0 && s.IceCracks == 0 && bonked && sim.IceBonks == 1,
            "A slow throw skimming the floor bonks the shell (no crack, no break)");
        check(bounce is var (bx, by) && bx < 0 && by < 0 && sim.TakeCatBounce() == null, "The cat bounces back the way it came, a little up (taken once)");
        sim.Step(dt, 900, false, false, null);
        sim.ClearEvents();
        // A slowish throw cracks the shell (no hurt), and each crack lets a slower throw break it.
        var cracked = new HashSet<BattleEventKind>();
        ThrowThrough(sim, 800, rule.MinSpeed * 0.8, cracked);
        check(s.Iced && s.Hp == before && sim.IceBreaks == 0 && s.IceCracks == 1 && cracked.Contains(BattleEventKind.IceCracked)
              && !cracked.Contains(BattleEventKind.ThrowHit) && Math.Abs(BattleSim.ThrowSpeed(s, rule) - rule.MinSpeed * (1 - BattleSim.CrackEase)) < 1e-9,
            "A throw below its minSpeed only cracks the shell, and the next needs less");
        ThrowThrough(sim, 800, rule.MinSpeed * 0.8);
        check(s.Iced && s.IceCracks == 2 && sim.IceBreaks == 0, "A second crack, still holding");
        ThrowThrough(sim, 800, rule.MinSpeed * 0.75);
        check(!s.Iced && s.IceCracks == 0 && sim.IceBreaks == 1, "Twice cracked, a throw at 75% of its minSpeed breaks it");
        sim = Field(Icy());
        s = sim.Slimes.Single();
        s.X = 800;
        before = s.Hp;
        var seen = new HashSet<BattleEventKind>();
        ThrowThrough(sim, 800, rule.MinSpeed * 1.2, seen);
        check(!s.Iced && sim.IceBreaks == 1 && seen.Contains(BattleEventKind.IceShattered) && before - s.Hp == rule.Damage && s.State == SlimeState.Stunned,
            $"A fast throw breaks the shell, hurts and dazes it (lost {before - s.Hp:0.#})");
        // Broken: the cat's weapon hits in full.
        check(!sim.Slimes.Any(x => x.Iced), "Broken, the shell is off");

        // Wet from the spray: a slower throw is enough.
        sim = Field(Icy());
        s = sim.Slimes.Single();
        s.X = 800;
        s.WetRemaining = 3;
        ThrowThrough(sim, 800, (ice.WetSpeed + rule.MinSpeed) / 2);
        check(!s.Iced && sim.IceBreaks == 1, "Wet from the spray, a slower throw breaks the shell");
        check(BattleSim.ThrowSpeed(s, rule) == rule.MinSpeed, "Without its shell the throw rule's own speed counts again");

        // It grows back (an elite's sooner).
        bool back = Run(sim, ice.Regrow + 0.5, catX: 1500, kind: BattleEventKind.IceRegrown);
        check(back && s.Iced, $"The shell grows back after {ice.Regrow} s");
        sim = Field(Icy(), elite: true);
        s = sim.Slimes.Single();
        s.X = 800;
        ThrowThrough(sim, 800, rule.MinSpeed * 1.2);
        double grew = 0;
        Run(sim, ice.Regrow, catX: 1500, kind: BattleEventKind.IceRegrown, each: () => grew += dt);
        check(s.Elite != null && s.Iced && grew <= ice.EliteRegrow + 0.2, $"An elite's shell grows back sooner ({grew:0.0} s)");

        // The cat goes for another slime first while the shell is on.
        sim = Field(new[] { Icy(), Plain() });
        var icy = sim.Slimes.Single(x => x.Type.Id == "icy");
        var plain = sim.Slimes.Single(x => x.Type.Id == "slime");
        icy.X = 200; plain.X = 400;
        Run(sim, 0.2);
        check(sim.TargetId == plain.Id, "With its shell on, the cat goes for another slime first");
        // Pause holds the regrowth.
        icy.Iced = false; icy.IceClock = 1;
        sim.Paused = true;
        Run(sim, 3);
        check(!icy.Iced, "Pause holds its regrowth");

        // 2026-11-05: only the shell meets a throw; without it the thrown cat flies through.
        sim = Field(Icy());
        s = sim.Slimes.Single();
        s.X = 800;
        s.Iced = false; s.IceClock = 99;
        before = s.Hp;
        var through = new HashSet<BattleEventKind>();
        ThrowThrough(sim, 800, rule.MinSpeed * 1.5, through);
        check(s.Hp == before && s.State != SlimeState.Stunned && sim.ThrowHits == 0 && sim.IceBonks == 0 && sim.TakeCatBounce() == null
              && !through.Contains(BattleEventKind.ThrowHit) && !through.Contains(BattleEventKind.IceBonked),
            "Without its shell the thrown cat flies through an ice slime (no hurt, no daze, no bonk)");

        // 2026-11-05: the Grabber's slam breaks the shell of the one slammed and of those it splashes.
        sim = Field(Icy(), count: 2);
        Slime slammed = sim.Slimes[0], beside = sim.Slimes[1];
        slammed.X = 800; beside.X = 840;
        check(sim.Arm(BattleSkill.Grabber) && sim.Grab(slammed.Id, 800, 10) && slammed.Iced, "The claw lifts an ice slime, shell on");
        Run(sim, 0.5, catX: 100);
        beside.X = 840;
        check(sim.GrabDrop(800), "...and carries it to be slammed");
        Run(sim, 1.5, catX: 100);
        check(!slammed.Iced && !beside.Iced && sim.IceBreaks == 2 && slammed.Hp < slammed.MaxHp && beside.Hp < beside.MaxHp,
            "The Grabber's slam breaks both shells: the slammed slime's and the one in its splash");
        // Set down when the hold runs out it takes no hit: the shell stays.
        sim = Field(Icy());
        s = sim.Slimes.Single();
        s.X = 800;
        sim.Arm(BattleSkill.Grabber);
        sim.Grab(s.Id, 800, 10);
        Run(sim, sim.Settings.GrabberHoldSeconds + 1.2, catX: 100);
        check(!s.Grabbed && s.Iced && sim.IceBreaks == 0, "Only set down when the hold runs out, the shell stays whole");
    }

    private static void ClingChecks(Action<bool, string> check)
    {
        var c = Shipped.Enemy("clinger").Cling!;
        var sim = Field(Clinger());
        var s = sim.Slimes.Single();
        bool up = Run(sim, 30, catX: 100, kind: BattleEventKind.ClingClimbed);
        bool edge = Math.Abs(s.X - (s.WallSide < 0 ? sim.Left + s.Width * 0.35 : sim.Right - s.Width * 0.35)) < 1e-6;
        check(Math.Abs(s.BodyX + s.WallSide * s.Height / 2 - (s.WallSide < 0 ? sim.Left + BattleSim.WallInset : sim.Right - BattleSim.WallInset)) < 0.01,
            "The rotated body's flat base meets the wall without moving its simulation anchor");
        check(up && s.OnWall && s.Cling == ClingPhase.Clinging && edge && Math.Abs(s.Altitude - c.Height) < 1e-6,
            $"It goes to the nearest edge and climbs it (side {s.WallSide}, x {s.X:0}, height {s.Altitude:0})");
        // 2026-11-04: it can be chosen (the cat then goes to watch it), never swung at from the floor.
        check(s.Airborne && sim.TargetId != s.Id, "Up its edge the cat on the floor never goes for it (choosing it only sends the cat to watch: WatchChecks)");

        // 2026-11-05: the Spray reaches it however high it climbed.
        {
            double height = s.Altitude;
            s.Altitude = BattleSim.ClingTop + 40;
            check(BattleSim.InSky(s) && sim.Arm(BattleSkill.Spray) && sim.StartSpray(s.X, s.Altitude + 10) && sim.SprayTargetId == s.Id,
                "The Spray locks on a wall climber high up its edge");
            sim.Arm(null);
            sim.Step(dt, 100, false);
            s.Altitude = height;
            sim.ClearEvents();
        }

        // It aims at the cat's spot, then a spike hurts the cat on the floor (no stun).
        double hp = sim.CatHp;
        s.ShotClock = 0.05;
        bool aimed = false;
        Run(sim, 2, catX: 700, each: () => aimed |= s.Aiming && sim.SpikeTargets(s).Any(x => Math.Abs(x - 700) < 1));
        check(aimed && sim.CatHp < hp && sim.SpikeHits == 1 && !sim.CatStunned, $"It aims at the cat's spot and a spike hurts the cat on the floor, no stun ({hp - sim.CatHp:0}))");
        // Out of the ring: missed.
        hp = sim.CatHp;
        s.ShotClock = 0.05;
        Run(sim, c.Aim + 0.15, catX: 700);
        Run(sim, c.Flight + 0.2, catX: 700 + c.Radius + 80, each: () => s.ShotClock = 99);
        check(sim.CatHp == hp && sim.SpikeHits == 1, "Stepping out of the ring dodges the spike");
        // Lifted: never hit.
        hp = sim.CatHp;
        s.ShotClock = 0.05;
        Run(sim, c.Aim + c.Flight + 0.3, catX: 700, held: true, pose: new CatPose(700, 200, 0, 0, true, false), each: () => { if (!s.Aiming && sim.Goos.Count > 0) s.ShotClock = 99; });
        check(sim.CatHp == hp, "A lifted cat is never hit by a spike");

        // An elite drops two spikes round the cat's spot.
        sim = Field(Clinger(), elite: true);
        s = sim.Slimes.Single();
        Run(sim, 30, kind: BattleEventKind.ClingClimbed);
        s.ShotClock = 0.01;
        Run(sim, 0.1, catX: 700);
        var spots = sim.SpikeTargets(s).ToArray();
        check(s.Elite != null && spots.Length == c.EliteSpikes && Math.Abs(spots.Max() - spots.Min() - c.EliteSpread) < 1e-6, $"An elite drops {c.EliteSpikes} spikes {c.EliteSpread} apart");

        // A second one on the same edge climbs higher.
        sim = Field(Clinger(), count: 2);
        var pair = sim.Slimes.ToArray();
        foreach (var p in pair) p.X = 1200;
        Run(sim, 30, catX: 100, each: () => { });
        check(pair.All(p => p.OnWall && p.Cling == ClingPhase.Clinging) && Math.Abs(Math.Abs(pair[0].Altitude - pair[1].Altitude) - c.Spacing) < 1e-6,
            $"A second one on the same edge clings {c.Spacing} higher ({pair[0].Altitude:0}, {pair[1].Altitude:0})");

        // Pause and the frozen guide hold it still.
        sim = Field(Clinger());
        s = sim.Slimes.Single();
        Run(sim, 30, kind: BattleEventKind.ClingClimbed);
        s.ShotClock = 0.01;
        sim.Paused = true;
        Run(sim, 1);
        bool still = !s.Aiming;
        sim.Paused = false;
        for (int i = 0; i < 30; i++) sim.Step(0, 100, false);
        check(still && !s.Aiming, "Pause and the frozen guide hold it still");

        // The shipped one slides up or down its edge after each volley (at least `shift`, within its range),
        // then aims again from there; the same seed slides the same way.
        double[] Heights(int seed)
        {
            var field = Field(Clinger(shift: true), seed: seed);
            var one = field.Slimes.Single();
            Run(field, 30, kind: BattleEventKind.ClingClimbed);
            var seen = new List<double> { one.Altitude };
            for (int volley = 0; volley < 4; volley++)
            {
                Run(field, 20, catX: 700, kind: BattleEventKind.ClingShot);
                for (int i = 0; i < 600 && (one.Cling != ClingPhase.Clinging || one.ClingShifting); i++) { field.Step(dt, 700, false); field.ClearEvents(); }
                if (one.Cling != ClingPhase.Clinging) return Array.Empty<double>();
                seen.Add(one.Altitude);
            }
            return seen.ToArray();
        }
        var heights = Heights(4);
        bool apart = heights.Zip(heights.Skip(1)).All(p => Math.Abs(p.First - p.Second) >= c.Shift - 1e-6);
        check(c.Shift > 0 && heights.Length == 5 && apart && heights.All(h => h >= c.Height - 1e-6 && h <= c.Top + 1e-6) && heights.Any(h => h > BattleSim.ClingTop) && heights.SequenceEqual(Heights(4)),
            $"After each volley it slides at least {c.Shift} up or down its edge, up the screen (to {c.Top:0}), the same for a seed ({string.Join(", ", heights.Select(h => h.ToString("0")))})");
    }

    // A clinger up its edge, ready to be knocked off.
    private static (BattleSim, Slime) OnEdge()
    {
        var sim = Field(Clinger());
        var s = sim.Slimes.Single();
        Run(sim, 30, kind: BattleEventKind.ClingClimbed);
        s.ShotClock = 99;
        return (sim, s);
    }

    private static void KnockChecks(Action<bool, string> check)
    {
        var c = Shipped.Enemy("clinger").Cling!;
        var rule = Shipped.Enemy("clinger").ThrowHit!;
        // The thrown cat flying into it knocks it off: it falls, lies dazed, then climbs again.
        var (sim, s) = OnEdge();
        double hp = s.Hp, h = s.Altitude + s.Height / 2;
        var seen = new HashSet<BattleEventKind>();
        double towards = s.WallSide;
        Throw(sim, new CatPose(s.X - towards * 160, h, towards * rule.MinSpeed * 1.5, 0, false, true), new CatPose(s.X - towards * 10, h, towards * rule.MinSpeed * 1.5, 0, false, true), seen);
        check(seen.Contains(BattleEventKind.ClingKnocked) && s.Cling is ClingPhase.Falling or ClingPhase.Ground && hp - s.Hp == rule.Damage,
            $"The thrown cat flying into it knocks it off its edge (lost {hp - s.Hp:0})");
        bool landed = Run(sim, 2, catX: 800, kind: BattleEventKind.ClingLanded);
        check(landed && !s.Airborne && s.State == SlimeState.Stunned && s.StunRemaining >= c.Downed - 0.05, "It lands on the floor dazed");
        Run(sim, 0.1, catX: 800);
        check(sim.TargetId == s.Id, "On the floor the cat goes for it");
        // No fighting on the floor (ground 0): once the daze is over it heads straight back to its edge.
        double dazed = s.StunRemaining, toWall = 0;
        Run(sim, dazed + 0.05, catX: 800);
        bool walking = false;
        Run(sim, 0.5, catX: 800, each: () => { walking |= s.State == SlimeState.Moving && !s.OnWall && s.Cling == ClingPhase.Ground; toWall = s.X; });
        bool back = Run(sim, 30, catX: 800, kind: BattleEventKind.ClingClimbed);
        check(c.Ground == 0 && walking && back && s.OnWall,
            $"Its daze over, it goes straight back up its edge (no fight on the floor; x {toWall:0} on the way)");

        // Too slow, or high over it: nothing.
        (sim, s) = OnEdge();
        towards = s.WallSide;
        h = s.Altitude + s.Height / 2;
        Throw(sim, new CatPose(s.X - towards * 160, h, towards * rule.MinSpeed * 0.6, 0, false, true), new CatPose(s.X - towards * 10, h, towards * rule.MinSpeed * 0.6, 0, false, true));
        Throw(sim, new CatPose(s.X - towards * 160, h + 150, towards * rule.MinSpeed * 2, 0, false, true), new CatPose(s.X - towards * 10, h + 150, towards * rule.MinSpeed * 2, 0, false, true));
        check(s.OnWall && sim.ClingKnocks == 0, "A slow throw, or one high over it, leaves it up its edge");

        // Push knocks it off.
        (sim, s) = OnEdge();
        check(sim.Use(BattleSkill.Push, s.Id, 100) && Run(sim, 2, kind: BattleEventKind.ClingLanded) && !s.Airborne, "Push knocks it off its edge");
        // The Grabber yanks it off at once and is spent.
        (sim, s) = OnEdge();
        check(sim.Arm(BattleSkill.Grabber) && sim.Grab(s.Id, s.X, s.Altitude) && !sim.Grabbing && sim.Cooldown(BattleSkill.Grabber) > 0 && s.Cling == ClingPhase.Falling,
            "The Grabber yanks it off at once (not held in the claw) and is spent");
        // The held cat swings at it (it is in the air).
        var shipped = Field(Clinger(), hit: 5);
        var t = shipped.Slimes.Single();
        Run(shipped, 30, kind: BattleEventKind.ClingClimbed);
        t.ShotClock = 99;
        hp = t.Hp;
        Run(shipped, 2, held: true, pose: new CatPose(t.X - t.WallSide * 45, t.Altitude + t.Height / 2, 0, 0, true, false), each: () => t.ShotClock = 99);
        check(t.Hp < hp && shipped.HeldHits > 0, "The cat in the user's hand swings at it up its edge");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        const string slime = "{\"id\":\"slime\",\"nameVi\":\"S\",\"nameEn\":\"S\"}";
        const string thrown = "\"throwHit\":{\"minSpeed\":600,\"damage\":5}";
        static string Enemy(string id, string extra) => $"{{\"id\":\"{id}\",\"nameVi\":\"T\",\"nameEn\":\"T\",{extra}}}";
        bool Rejects(string enemies)
        {
            try { BattleData.Parse("{\"enemies\":[" + slime + "," + enemies + "],\"waves\":[{\"groups\":[{\"enemy\":\"a\",\"count\":1}]}]}"); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(!Rejects(Enemy("a", "\"ice\":{}," + thrown)) && !Rejects(Enemy("a", "\"cling\":{}," + thrown)), "An ice slime and a wall climber within their limits are accepted");
        check(Rejects(Enemy("a", "\"ice\":{}")) && Rejects(Enemy("a", "\"cling\":{}")), "Without a throwHit they are refused");
        check(Rejects(Enemy("a", "\"ice\":{},\"cling\":{}," + thrown)) && Rejects(Enemy("a", "\"ice\":{},\"fly\":{}," + thrown)) && Rejects(Enemy("a", "\"boss\":true,\"cling\":{}," + thrown)),
            "Ice and cling together, with flying, or on a boss are refused");
        check(Rejects(Enemy("a", "\"ice\":{\"wetSpeed\":900}," + thrown)) && Rejects(Enemy("a", "\"ice\":{\"armor\":2}," + thrown)) && Rejects(Enemy("a", "\"cling\":{\"height\":300}," + thrown)),
            "Numbers out of range (a wet speed over the dry one, a climb over the strip) are refused");

        var s = Shipped;
        check(s.Enemy("icy") is { Ice: not null, ThrowHit: not null, BinSpit: false, Elite: not null } && s.Enemy("clinger") is { Cling: not null, ThrowHit: not null, BinSpit: false, Elite: not null },
            "The two shipped kinds, each with an elite and a throw rule, never spat by a bin");
        var kinds = new[] { "icy", "clinger" };
        bool Uses(WaveGroup g) => kinds.Contains(g.Enemy) || g.Pick?.Keys.Any(kinds.Contains) == true || g.Squad?.Any(m => kinds.Contains(m.Enemy)) == true;
        var all = s.WavePlan.Concat(s.Campaign?.Chapters.Take(1).SelectMany(ch => ch.Stages).SelectMany(st => st.Waves) ?? Enumerable.Empty<WaveSpec>());
        check(!all.SelectMany(w => w.Groups).Any(Uses), "No built-in battle uses them yet outside chapter 2 (1.5 M21 brings it in)");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m15.json"))) dir = dir.Parent;
        var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", "battle-m15.json"));
        check(load is { Custom: true, Error: null } && load.Settings.WavePlan.SelectMany(w => w.Groups).Count(Uses) >= 4,
            $"docs/examples/battle-m15.json loads as custom data with both kinds ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
    }
}
