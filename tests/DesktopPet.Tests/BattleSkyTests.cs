using DesktopPet;

// 1.5 M14 (PetBattle.Fly.cs, PetBattle.Burrow.cs), with stand-ins that carry the shipped fly and burrow
// numbers. The flying slime stays up in its sky (out of the floor's reach, never a target of the cat on
// the floor, never hit by a yarn bomb), moves from spot to spot, aims, then shoots goo that hurts the cat
// standing (and slows it) or in the user's hand (no stun, no slow) and in its landing protection; the
// Shield takes it and moving the held cat away dodges it. Held hits, Push and the Grabber knock it down:
// it falls, lies dazed taking more from every hit, crouches and flies up again. The burrowing slime digs
// in (nothing hits it then), goes to the cat, shows its cracks and comes up hitting the cat on the floor;
// a lifted cat dodges, the cat let go onto the cracks pins it, the Grabber pulls it up. The data limits,
// the shipped kinds (kept out of every battle for now) and the try-out file.
internal static class BattleSkyTests
{
    const double dt = 1.0 / 60;

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static EnemyType Flyer(double damage = 10) => new()
    {
        Id = "flyer", MaxHp = 100, Speed = 40, Damage = damage, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3,
        Elite = new EliteSpec { Hp = 1.2 }, Fly = Shipped.Enemy("flyer").Fly
    };
    private static EnemyType Burrower(double damage = 10) => new()
    {
        Id = "burrower", MaxHp = 100, Speed = 40, Damage = damage, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3,
        Elite = new EliteSpec { Hp = 1.2 }, Burrow = Shipped.Enemy("burrower").Burrow
    };

    private static readonly BattleSkill[] Tools = { BattleSkill.Bomb, BattleSkill.Shield, BattleSkill.Push, BattleSkill.Grabber, BattleSkill.Heal };

    // One enemy of this kind on a 1600-wide field, the cat at 100 (weapon damage `hit`, reach 60).
    private static BattleSim Field(EnemyType kind, double hit = 0, double catHp = 1000)
    {
        var arms = Shipped.Weapon("sword") with { Range = 60, StunChance = 0, Damage = hit };
        var settings = new BattleSettings
        {
            Weapons = new[] { arms }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = catHp, MaxAlive = 8, MaxElites = 8,
            Enemies = new[] { kind }, RageHitGain = 0, BombDamage = 10, BombCooldown = 0.1, BombDelay = 0.3, BombRadius = 40,
            PushCooldown = 0.1, GrabberCooldown = 0.1, ShieldCooldown = 0.1,
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = kind.Id, Count = 1 } } },
                new WaveSpec { Groups = new[] { new WaveGroup { Enemy = kind.Id, Count = 1 } } } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 4, skills: Tools);
        for (int i = 0; i < 600 && !sim.Slimes.Any(s => s.Targetable); i++) sim.Step(dt, 100, false);
        sim.ClearEvents();
        return sim;
    }

    // Steps `seconds` with the cat at catX (pose: where the held cat is), or up to the step an event of
    // `kind` came (then true).
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

    // Puts a flyer still at (x, height) for a while (its next spot there too, its shot far off).
    private static void Hold(Slime s, double x, double height, double shotIn = 99)
    {
        s.X = s.FlyToX = x; s.Altitude = s.FlyToHeight = height; s.FlyClock = 99; s.ShotClock = shotIn; s.AimLeft = 0; s.ShotsLeft = 0;
    }

    public static void Run(Action<bool, string> check)
    {
        FlyChecks(check);
        KnockChecks(check);
        BurrowChecks(check);
        DataChecks(check);
    }

    private static void FlyChecks(Action<bool, string> check)
    {
        var fly = Shipped.Enemy("flyer").Fly!;
        var sim = Field(Flyer());
        var s = sim.Slimes.Single();
        check(s.Airborne && s.Altitude >= fly.MinHeight - 1e-6 && s.Altitude <= sim.SkyHeight - s.Height && BattleSim.InSky(s), "A flyer comes in up in its sky");
        double x0 = s.X, h0 = s.Altitude, low = double.MaxValue, high = 0, far = 0;
        Run(sim, 12, each: () => { low = Math.Min(low, s.Altitude); high = Math.Max(high, s.Altitude); far = Math.Max(far, Math.Abs(s.X - x0) + Math.Abs(s.Altitude - h0)); });
        check(far > 150 && low >= fly.MinHeight - 1e-6 && high <= sim.SkyHeight - s.Height, $"It flies from spot to spot inside its sky (moved {far:0}, heights {low:0}–{high:0})");
        check(sim.TargetId != s.Id && sim.Slimes.All(x => x.State != SlimeState.Windup), "The cat on the floor never goes for it and it never winds up a blow");
        sim.SetSky(400);
        check(s.Altitude <= 400 - s.Height, "A lower screen brings it down inside it");
        sim.SetSky(BattleSim.DefaultSky);

        // Aim, then a drop at the standing cat: it hurts and slows it, never stuns it.
        sim = Field(Flyer());
        s = sim.Slimes.Single();
        Hold(s, 300, 400, shotIn: 0.05);
        double hp = sim.CatHp;
        bool aimed = false, shot = false, hit = false;
        Run(sim, 2.5, each: () =>
        {
            aimed |= s.Aiming;
            shot |= sim.Shots.Count > 0;
            if (!hit && sim.CatHp < hp) { hit = true; check(sim.CatSlowRemaining > 0 && sim.LagFactor < 1 && !sim.CatStunned, "The drop slows the cat on the floor, never stuns it"); }
            if (hit) s.ShotClock = 99;
        });
        check(aimed && shot && hit, "It aims, then shoots a drop that hits the cat on the floor");
        check(Run(sim, 2.5) is false && sim.CatSlowRemaining <= 0 && sim.LagFactor == 1, "The slow wears off");

        // The cat in the user's hand, high on the screen: hit all the same, no stun, no slow, still held.
        sim = Field(Flyer());
        s = sim.Slimes.Single();
        Hold(s, 600, 500, shotIn: 0.05);
        hp = sim.CatHp;
        var pose = new CatPose(400, 300, 0, 0, true, false);
        bool held = false;
        Run(sim, 2, held: true, pose: pose, each: () => { if (sim.CatHp < hp && !held) { held = true; s.ShotClock = 99; } });
        check(held && !sim.CatStunned && sim.CatSlowRemaining <= 0 && sim.CatHeld, "A drop hits the cat in the user's hand (no stun, no slow, still in the hand)");

        // Moving the held cat away while the drop flies dodges it.
        sim = Field(Flyer());
        s = sim.Slimes.Single();
        Hold(s, 1400, 600, shotIn: 0.05);
        hp = sim.CatHp;
        var at = new CatPose(200, 200, 0, 0, true, false);
        Run(sim, 0.9, held: true, pose: at);
        check(sim.Shots.Count == 1, "Its drop is on its way");
        Run(sim, 4.5, held: true, pose: new CatPose(200, 650, 0, 0, true, false), each: () => s.ShotClock = 99);
        check(sim.CatHp == hp && sim.Shots.Count == 0, "Carried out of its way the cat is not hit");

        // The Shield takes it; landing protection does not.
        sim = Field(Flyer());
        s = sim.Slimes.Single();
        sim.Use(BattleSkill.Shield, 100);
        Hold(s, 300, 400, shotIn: 0.05);
        hp = sim.CatHp;
        Run(sim, 2, each: () => { if (sim.Shots.Count > 0) s.ShotClock = 99; });
        check(sim.CatHp == hp && sim.CatSlowRemaining <= 0, "The Shield takes a drop (no damage, no slow)");
        sim = Field(Flyer());
        s = sim.Slimes.Single();
        Run(sim, 1.2, held: true, pose: new CatPose(100, 200, 0, 0, true, false), each: () => s.ShotClock = 99);
        Run(sim, 0.05);
        check(sim.Grace > 0, "Put down after a real lift: landing protection");
        // Its aim almost done, close by: the drop lands while the protection still runs.
        Hold(s, 160, 150);
        s.AimLeft = 0.02;
        hp = sim.CatHp;
        bool graced = false;
        Run(sim, 1, each: () => { if (sim.Grace > 0) graced = true; }, kind: BattleEventKind.FlyShotHit);
        check(sim.CatHp < hp && graced && sim.Grace > 0, "A drop still hits under landing protection");

        // Only its own drops reach a held cat: it never touches a held cat otherwise, and a yarn bomb never reaches it.
        sim = Field(Flyer());
        s = sim.Slimes.Single();
        Hold(s, 300, 300);
        double its = s.Hp;
        sim.Use(BattleSkill.Bomb, s.Id, 100);
        Run(sim, 1);
        check(s.Hp == its, "A yarn bomb on the floor never reaches it up in the air");
        // dt 0 (the frozen guide) and Pause stop it.
        Hold(s, 300, 300, shotIn: 0.01);
        s.FlyToX = 900; s.FlyClock = 99;
        sim.Paused = true;
        Run(sim, 1);
        bool still = s.X == 300 && !s.Aiming;
        sim.Paused = false;
        for (int i = 0; i < 30; i++) sim.Step(0, 100, false);
        check(still && s.X == 300 && !s.Aiming, "Pause and the frozen guide hold it still");
    }

    private static void KnockChecks(Action<bool, string> check)
    {
        var fly = Shipped.Enemy("flyer").Fly!;
        // Held hits: the cat carried up to it swings; HeldHits of them knock it down.
        var sim = Field(Flyer(), hit: 1);
        var s = sim.Slimes.Single();
        Hold(s, 600, 400);
        var near = new CatPose(600 - 40, 400 + s.Height / 2, 0, 0, true, false);
        bool knocked = Run(sim, 8, held: true, pose: near, kind: BattleEventKind.FlyerKnocked, each: () => s.ShotClock = 99);
        check(knocked && sim.HeldHits >= fly.HeldHits, $"{fly.HeldHits} held hits knock it down ({sim.HeldHits} hits)");
        bool landed = Run(sim, 2, kind: BattleEventKind.FlyerDowned);
        check(landed && !s.Airborne && s.Fly == FlyPhase.Downed && s.State == SlimeState.Stunned, "It falls to the floor and lies there dazed");
        double before = s.Hp;
        sim.Use(BattleSkill.Bomb, s.Id, 100);
        Run(sim, 0.5);
        check(Math.Abs(before - s.Hp - 10 * fly.DownedDamage) < 1e-6, $"Down, it takes {fly.DownedDamage}× from a hit (lost {before - s.Hp:0.#})");
        Run(sim, 0.1);
        check(sim.TargetId == s.Id, "Down, the cat on the floor goes for it");
        bool rose = Run(sim, fly.Downed + fly.Rise + 1, catX: 1500, kind: BattleEventKind.FlyerRose);
        Run(sim, 1, catX: 1500);
        check(rose && s.Airborne && s.Fly == FlyPhase.Aloft, "After its daze it crouches and flies up again");

        // Push knocks it down (a click on its window).
        sim = Field(Flyer());
        s = sim.Slimes.Single();
        Hold(s, 600, 400);
        check(sim.Use(BattleSkill.Push, s.Id, 100) && Run(sim, 2, kind: BattleEventKind.FlyerDowned) && s.Fly == FlyPhase.Downed, "Push knocks it down");
        // The Grabber yanks it down at once and is spent.
        sim = Field(Flyer());
        s = sim.Slimes.Single();
        Hold(s, 600, 400);
        check(sim.Arm(BattleSkill.Grabber) && sim.Grab(s.Id, s.X, s.Altitude) && !sim.Grabbing && sim.Cooldown(BattleSkill.Grabber) > 0 && s.Fly == FlyPhase.Falling,
            "The Grabber yanks it down at once (not held in the claw) and is spent");
        Run(sim, 2);
        check(s.Fly == FlyPhase.Downed, "Yanked, it lands dazed");
        // A push on it while it is down never shortens its daze.
        double left = s.StunRemaining;
        Run(sim, 0.2);
        sim.Use(BattleSkill.Push, s.Id, 100);
        check(s.StunRemaining >= left - 0.25, "A push while it is down keeps the rest of its daze");
    }

    private static void BurrowChecks(Action<bool, string> check)
    {
        var b = Shipped.Enemy("burrower").Burrow!;
        var sim = Field(Burrower());
        var s = sim.Slimes.Single();
        s.X = 900;
        check(Run(sim, 3, catX: 100, kind: BattleEventKind.BurrowDug) && s.Burrowed, "It comes out a moment, then digs in");
        double hp = s.Hp;
        sim.Use(BattleSkill.Bomb, s.Id, 100);
        Run(sim, 0.5, catX: 100);
        check(s.Hp == hp && sim.TargetId != s.Id && !sim.Select(s.Id) && !sim.Use(BattleSkill.Push, s.Id, 100), "Under the floor nothing hits it and the cat does not go for it");
        // To the cat, rest done: cracks, then up under a cat on the floor (hurt and stunned).
        s.BurrowRest = 0;
        double catHp = sim.CatHp;
        check(Run(sim, 12, catX: 300, kind: BattleEventKind.BurrowCracked), "Under the cat it shows its cracks");
        check(s.Burrow == BurrowPhase.Cracking && Math.Abs(s.CrackX - 300) <= 12, "Where the cat stands");
        bool up = Run(sim, b.Crack + 0.2, catX: 300, kind: BattleEventKind.BurrowEmerged);
        check(up && sim.CatHp < catHp && sim.CatStunned && !s.Burrowed, "It comes up there: the cat on the floor is hurt and stunned");

        // Lifted while the cracks show: it comes up for nothing.
        sim = Field(Burrower());
        s = sim.Slimes.Single();
        Run(sim, 3, catX: 300);
        s.X = 600;
        s.BurrowRest = 0;
        Run(sim, 12, catX: 300, kind: BattleEventKind.BurrowCracked);
        check(s.Burrow == BurrowPhase.Cracking, "Its cracks show");
        catHp = sim.CatHp;
        Run(sim, b.Crack + 0.1, catX: 300, held: true, pose: new CatPose(300, 200, 0, 0, true, false));
        check(sim.CatHp == catHp && s.Burrow is BurrowPhase.Emerging or BurrowPhase.Above, "A lifted cat is not hit");

        // Let go onto the cracks: pinned.
        sim = Field(Burrower());
        s = sim.Slimes.Single();
        Run(sim, 3, catX: 300);
        s.X = 600;
        s.BurrowRest = 0;
        Run(sim, 12, catX: 300, kind: BattleEventKind.BurrowCracked);
        Run(sim, 0.3, catX: 300, held: true, pose: new CatPose(300, 200, 0, 0, true, false));
        catHp = sim.CatHp;
        bool pinned = Run(sim, 0.1, catX: 310, kind: BattleEventKind.BurrowPinned);
        check(pinned && s.State == SlimeState.Stunned && s.StunRemaining > b.Pinned - 0.2 && !s.Burrowed && sim.CatHp == catHp, "The cat let go onto its cracks pins it, dazed, and is not hit");
        Run(sim, b.Pinned + 0.5, catX: 900);
        check(!s.Burrowed, "Once free it stays out a while before digging in");

        // Let go away from the ring: nothing.
        sim = Field(Burrower());
        s = sim.Slimes.Single();
        Run(sim, 3, catX: 300);
        s.X = 600;
        s.BurrowRest = 0;
        Run(sim, 12, catX: 300, kind: BattleEventKind.BurrowCracked);
        Run(sim, 0.3, catX: 300, held: true, pose: new CatPose(300, 200, 0, 0, true, false));
        check(!Run(sim, 0.1, catX: 300 + b.Radius + 40, kind: BattleEventKind.BurrowPinned), "Let go away from its ring, nothing is pinned");

        // The cat in the user's hand: it waits under the floor, no cracks.
        sim = Field(Burrower());
        s = sim.Slimes.Single();
        Run(sim, 3, catX: 300);
        s.X = 600;
        s.BurrowRest = 0;
        check(!Run(sim, 6, catX: 300, held: true, pose: new CatPose(300, 200, 0, 0, true, false), kind: BattleEventKind.BurrowCracked) && s.Burrowed,
            "While the cat is held it waits under the floor");

        // The Grabber pulls it up.
        sim = Field(Burrower());
        s = sim.Slimes.Single();
        Run(sim, 3, catX: 1500);
        check(s.Burrowed, "Under the floor");
        check(sim.Arm(BattleSkill.Grabber) && sim.Grab(s.Id, s.X, 5) && sim.Grabbing && !s.Burrowed && s.Burrow == BurrowPhase.Above, "The Grabber pulls it up from under the floor");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        const string slime = "{\"id\":\"slime\",\"nameVi\":\"S\",\"nameEn\":\"S\"}";
        static string Enemy(string id, string extra) => $"{{\"id\":\"{id}\",\"nameVi\":\"T\",\"nameEn\":\"T\",{extra}}}";
        bool Rejects(string enemies)
        {
            try { BattleData.Parse("{\"enemies\":[" + slime + "," + enemies + "],\"waves\":[{\"groups\":[{\"enemy\":\"a\",\"count\":1}]}]}"); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(!Rejects(Enemy("a", "\"fly\":{}")) && !Rejects(Enemy("a", "\"burrow\":{}")), "A flyer and a burrower within their limits are accepted");
        check(Rejects(Enemy("a", "\"fly\":{},\"burrow\":{}")), "A kind both flying and burrowing is refused");
        check(Rejects(Enemy("a", "\"boss\":true,\"fly\":{}")) && Rejects(Enemy("a", "\"hover\":50,\"fly\":{}")) && Rejects(Enemy("a", "\"ghost\":{},\"burrow\":{}")),
            "A boss, a hovering or a cursor kind cannot also fly or burrow");
        check(Rejects(Enemy("a", "\"fly\":{\"minHeight\":10}")) && Rejects(Enemy("a", "\"fly\":{\"heldHits\":0}")) && Rejects(Enemy("a", "\"burrow\":{\"radius\":500}")),
            "Numbers out of range are refused");

        var s = Shipped;
        check(s.Enemy("flyer") is { Fly: not null, BinSpit: false, Elite: not null } && s.Enemy("burrower") is { Burrow: not null, BinSpit: false, Elite: not null },
            "The two shipped kinds, each with an elite, never spat by a bin");
        var kinds = new[] { "flyer", "burrower" };
        bool Uses(WaveGroup g) => kinds.Contains(g.Enemy) || g.Pick?.Keys.Any(kinds.Contains) == true || g.Squad?.Any(m => kinds.Contains(m.Enemy)) == true;
        var all = s.WavePlan.Concat(s.Campaign?.Chapters.SelectMany(c => c.Stages).SelectMany(st => st.Waves) ?? Enumerable.Empty<WaveSpec>());
        check(!all.SelectMany(w => w.Groups).Any(Uses) && !s.FormationList.SelectMany(f => f.Members).Any(kinds.Contains), "No built-in battle uses them yet");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m14.json"))) dir = dir.Parent;
        var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", "battle-m14.json"));
        check(load is { Custom: true, Error: null } && load.Settings.WavePlan.SelectMany(w => w.Groups).Count(Uses) >= 4,
            $"docs/examples/battle-m14.json loads as custom data with both kinds ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
    }
}
