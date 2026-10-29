using DesktopPet;

// 1.5 M03 (PetBattle.Held.cs), with stand-in enemies: the cat in the user's hand swings by itself at
// an enemy in the air near it, never at one on the floor, never when only pressed, far away, paused
// or put down; the cat on the floor leaves enemies in the air alone and they never attack up close;
// the thrown cat hurts and dazes only an enemy with a throw rule, fast enough, once per throw (a fast
// flight cannot skip over it); the cat takes no hit meanwhile; the built-in battles have neither yet.
internal static class BattleHeldTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id, double hover = 0, ThrowHitSpec? thrown = null, double damage = 0, double speed = 0) => new()
    {
        Id = id, MaxHp = 1000, Speed = speed, Damage = damage, Range = 64, Windup = 0.4, AttackCooldown = 1, Recover = 0.2, Hover = hover, ThrowHit = thrown
    };

    private static BattleSim Sim(params EnemyType[] kinds)
    {
        var settings = new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 100, MaxAlive = 8, Enemies = kinds,
            Plan = new[] { new WaveSpec { Groups = kinds.Select(k => new WaveGroup { Enemy = k.Id, Count = 1 }).ToArray() } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 5);
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < kinds.Length; i++) sim.Step(dt, 200, false);
        sim.ClearEvents();
        return sim;
    }

    // Steps with the cat in hand at (x, height) (or pressed only, or flying with a velocity).
    private static void Hold(BattleSim sim, double x, double height, double seconds, bool lifted = true, HashSet<BattleEventKind>? seen = null)
    {
        for (double t = 0; t < seconds && !sim.Over; t += dt)
        {
            sim.Step(dt, x, true, lifted, new CatPose(x, height, 0, 0, InHand: true, Flying: false));
            if (seen != null) foreach (var e in sim.Events) seen.Add(e.Kind);
            sim.ClearEvents();
        }
    }

    private static void Fly(BattleSim sim, CatPose from, CatPose to, HashSet<BattleEventKind>? seen = null)
    {
        foreach (var pose in new[] { from, to })
        {
            sim.Step(dt, pose.X, true, true, pose);
            if (seen != null) foreach (var e in sim.Events) seen.Add(e.Kind);
            sim.ClearEvents();
        }
    }

    public static void Run(Action<bool, string> check)
    {
        HeldChecks(check);
        FloorChecks(check);
        ThrowChecks(check);
        DataChecks(check);
    }

    private static void HeldChecks(Action<bool, string> check)
    {
        var sim = Sim(Kind("flyer", hover: 200), Kind("slime"));
        var flyer = sim.Slimes.Single(s => s.Type.Id == "flyer");
        var ground = sim.Slimes.Single(s => s.Type.Id == "slime");
        flyer.X = 800; ground.X = 400;
        check(flyer.Airborne && flyer.Altitude == 200 && !ground.Airborne, "An enemy with hover floats that high over the floor; others stay on it");

        var seen = new HashSet<BattleEventKind>();
        double hp = flyer.Hp, catHp = sim.CatHp;
        bool swung = false;
        for (double t = 0; t < 2; t += dt)
        {
            sim.Step(dt, 780, true, true, new CatPose(780, 190, 0, 0, InHand: true, Flying: false));
            swung |= sim.SwingProgress >= 0 && sim.HeldSwinging;
            foreach (var e in sim.Events) seen.Add(e.Kind);
            sim.ClearEvents();
        }
        check(flyer.Hp < hp && sim.HeldHits >= 2 && seen.Contains(BattleEventKind.HeldHit) && seen.Contains(BattleEventKind.Swing) && swung,
            $"The cat in hand swings by itself at an enemy in the air near it ({hp - flyer.Hp} damage, {sim.HeldHits} hits)");
        check(Math.Abs(hp - flyer.Hp - sim.HeldHits * 25) < 0.01, "A held hit is a plain weapon hit");
        check(sim.HeldFacing == 1, "The held swing faces the enemy");
        check(sim.CatHp == catHp, "The cat in hand takes no hit");
        check(sim.HeldHits <= 3, $"Held swings keep the weapon's cooldown ({sim.HeldHits} in 2 s)");

        hp = ground.Hp;
        Hold(sim, 400, 40, 2);
        check(ground.Hp == hp, "The cat in hand never hits an enemy on the floor, even right next to it");

        hp = flyer.Hp;
        Hold(sim, 780, 190, 2, lifted: false);
        check(flyer.Hp == hp && !sim.HeldSwinging, "A cat only pressed (not lifted) does not swing");

        Hold(sim, 200, 190, 2);
        check(flyer.Hp == hp, "Far from the enemy in the air the held cat does not swing");

        sim.Paused = true;
        Hold(sim, 780, 190, 2);
        check(flyer.Hp == hp && !sim.HeldSwinging, "Pause stops the held swing");
        sim.Paused = false;

        // A swing under way stops when the cat is put down (the floor swing is its own).
        hp = flyer.Hp;
        Hold(sim, 780, 190, 0.1);
        bool under = sim.HeldSwinging;
        sim.Step(dt, 780, false, false, null);
        sim.ClearEvents();
        check(under && !sim.HeldSwinging && flyer.Hp == hp, "Put down before its hit, the held swing ends and lands nothing");

        // No pose (the old call): nothing new happens.
        hp = flyer.Hp;
        for (double t = 0; t < 2; t += dt) { sim.Step(dt, 780, true, true); sim.ClearEvents(); }
        check(flyer.Hp == hp, "Without the cat's pose the held cat does nothing, as before");

        // Its weapon's hit time: nothing before it, the weapon's damage at it.
        var fresh = Sim(Kind("flyer", hover: 200));
        var target = fresh.Slimes.Single();
        target.X = 800;
        Hold(fresh, 780, 190, fresh.Weapon.HitTime - 0.05);
        bool early = target.Hp == target.MaxHp;
        Hold(fresh, 780, 190, 0.1);
        check(early && target.MaxHp - target.Hp == 25, $"The held hit lands at the swing's hit time, at the weapon's damage ({target.MaxHp - target.Hp})");
    }

    private static void FloorChecks(Action<bool, string> check)
    {
        // A flying enemy with a sting right over the cat on the floor: the cat neither goes for it
        // nor gets hit by it.
        var sim = Sim(Kind("flyer", hover: 150, damage: 30, speed: 40));
        var flyer = sim.Slimes.Single();
        flyer.X = 300;
        double catHp = sim.CatHp;
        int? target = 0;
        bool windup = false;
        for (double t = 0; t < 5; t += dt)
        {
            sim.Step(dt, 300, false);
            target = sim.TargetId;
            windup |= flyer.State == SlimeState.Windup;
            sim.ClearEvents();
        }
        check(target == null && !windup && sim.CatHp == catHp && flyer.Hp == flyer.MaxHp,
            "The cat on the floor leaves an enemy in the air alone, and it never attacks up close");
        check(!sim.Select(flyer.Id), "An enemy in the air cannot be chosen as the floor cat's target");
    }

    private static void ThrowChecks(Action<bool, string> check)
    {
        var ice = new ThrowHitSpec { MinSpeed = 600, Damage = 40, Stun = 1 };
        var sim = Sim(Kind("ice", thrown: ice), Kind("slime"));
        var target = sim.Slimes.Single(s => s.Type.Id == "ice");
        var plain = sim.Slimes.Single(s => s.Type.Id == "slime");
        target.X = 800; plain.X = 400;
        double catHp = sim.CatHp;

        // Through the plain slime fast: nothing.
        double plainHp = plain.Hp;
        Fly(sim, new CatPose(330, 20, 1200, 0, false, true), new CatPose(470, 20, 1200, 0, false, true));
        check(plain.Hp == plainHp && plain.State != SlimeState.Stunned && sim.ThrowHits == 0, "A throw through a plain enemy does nothing");

        // Through the ice slime too slowly: nothing.
        Fly(sim, new CatPose(740, 20, 300, 0, false, true), new CatPose(760, 20, 300, 0, false, true));
        sim.Step(dt, 760, false, false, null); // Landed.
        check(target.Hp == target.MaxHp && sim.ThrowHits == 0, "Below its minSpeed the throw does nothing");

        // Fast, and in one frame from one side to the other (a segment, not a point).
        var seen = new HashSet<BattleEventKind>();
        Fly(sim, new CatPose(700, 20, 1500, 0, false, true), new CatPose(900, 20, 1500, 0, false, true), seen);
        check(target.MaxHp - target.Hp == 40 && target.State == SlimeState.Stunned && sim.ThrowHits == 1 && seen.Contains(BattleEventKind.ThrowHit),
            $"A fast throw through an enemy with a throw rule hurts and dazes it, even across it in one frame ({target.MaxHp - target.Hp})");
        Fly(sim, new CatPose(900, 20, -1500, 0, false, true), new CatPose(700, 20, -1500, 0, false, true));
        check(sim.ThrowHits == 1, "Once per enemy per throw");
        sim.Step(dt, 700, false, false, null);
        Fly(sim, new CatPose(700, 20, 1500, 0, false, true), new CatPose(900, 20, 1500, 0, false, true));
        check(sim.ThrowHits == 2, "The next throw counts again");

        // Over its head: no contact.
        sim.Step(dt, 700, false, false, null);
        Fly(sim, new CatPose(700, 200, 1500, 0, false, true), new CatPose(900, 200, 1500, 0, false, true));
        check(sim.ThrowHits == 2, "A throw high over the enemy misses it");

        // Carried through it fast is not a throw.
        sim.Step(dt, 700, false, false, null);
        Fly(sim, new CatPose(700, 20, 1500, 0, true, false), new CatPose(900, 20, 1500, 0, true, false));
        check(sim.ThrowHits == 2, "Dragging the cat through it is not a throw");
        check(sim.CatHp == catHp, "The flying cat takes no hit");

        check(BattleSim.Crosses(0, 0, 10, 10, 4, 4, 6, 6) && !BattleSim.Crosses(0, 0, 10, 0, 4, 4, 6, 6) && BattleSim.Crosses(5, 5, 5, 5, 4, 4, 6, 6),
            "The flight segment test finds crossings, misses and a cat inside");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        bool Rejects(EnemyType e)
        {
            try { BattleData.Validate(new BattleSettings { Enemies = new[] { e }, Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = e.Id, Count = 1 } } } } }); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(!Rejects(Kind("x", hover: 120)) && !Rejects(Kind("x", thrown: new ThrowHitSpec { Stun = 1 })), "hover and throwHit are accepted");
        check(Rejects(Kind("x", hover: -5)) && Rejects(new EnemyType { Id = "king", Boss = true, Hover = 100 }) && Rejects(new EnemyType { Id = "x", Dash = true, Hover = 100 }),
            "hover out of its limits, or on a boss or a dash enemy, is refused");
        check(Rejects(Kind("x", thrown: new ThrowHitSpec())) && Rejects(Kind("x", thrown: new ThrowHitSpec { MinSpeed = 10, Stun = 1 })),
            "A throw rule that changes nothing or has a minSpeed out of its limits is refused");
        var parsed = BattleData.Parse("{\"enemies\":[{\"id\":\"slime\"},{\"id\":\"ice\",\"hover\":80,\"throwHit\":{\"minSpeed\":700,\"damage\":30,\"stun\":1.5}},{\"id\":\"king\",\"boss\":true}]}").Enemy("ice");
        check(parsed is { Hover: 80, ThrowHit: { MinSpeed: 700, Damage: 30, Stun: 1.5 } }, "battle.json reads hover and throwHit");
        // 1.5 M15: only the ice slime and the wall climber react to a throw (BattleWallTests).
        check(BattleData.Settings.EnemyTypes.All(e => e.Hover == 0 && (e.ThrowHit == null) == (e.Ice == null && e.Cling == null)),
            "No built-in enemy hovers; only the ice slime and the wall climber react to a throw");
    }
}
