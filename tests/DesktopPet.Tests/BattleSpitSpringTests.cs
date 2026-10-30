using DesktopPet;

// 1.5 M09 (PetBattle.Spit.cs, PetBattle.Spring.cs), with stand-ins that carry the shipped spit and spring
// numbers. The spitter: its ring marks the cat's spot, the drop hurts a cat still there on the floor (no
// stun), misses a lifted one or one that walked off, the Shield takes it, a Push cancels the windup, Pause
// freezes the drop, an elite spits two; its puddle lengthens a dash through it and the Fan dries it; the
// yarn draws it in. The spring: it squeezes when the cat steps in, springs on a cat still in the ring (hurt
// and stunned), rests, never winds up an attack; a lifted cat dodges it and never sets it off; a Push spends
// the squeeze; hits flatten it; it slides up to a far cat; an elite's ring is bigger. The data limits and
// the shipped kinds (kept out of every battle for now).
internal static class BattleSpitSpringTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Spitter() => new()
    {
        Id = "spit", MaxHp = 1000, Speed = 0, Damage = 9, Range = 56, Windup = 0.8, AttackCooldown = 2, Recover = 0.3, Support = true,
        Spit = BattleData.Settings.Enemy("spitter").Spit, Elite = new EliteSpec { Hp = 1.2 }
    };

    private static EnemyType Spring() => new()
    {
        Id = "spring", MaxHp = 1000, Speed = 30, Damage = 14, Range = 40, Windup = 0.8, AttackCooldown = 2, Recover = 0.3, Scale = 1.1, HitScale = 1.3,
        Spring = BattleData.Settings.Enemy("springer").Spring, Elite = new EliteSpec { Hp = 1.2 }
    };

    private static EnemyType Runner() => new()
    {
        Id = "run", MaxHp = 1000, Speed = 0, Damage = 0, Range = 54, Windup = 0.4, AttackCooldown = 5, Recover = 0.3, Scale = 0.75,
        Dash = true, DashRange = 170, DashDistance = 230, DashSpeed = 420
    };

    // A field with these kinds (elite: all of them elite) and the cat at 100; reach: the cat's weapon range
    // (5: it never reaches anything, so its own hits do not get in the way).
    private static BattleSim Field(EnemyType[] kinds, bool elite = false, double reach = 5, string weapon = "sword", IEnumerable<BattleSkill>? skills = null)
    {
        var shipped = BattleData.Settings;
        var arms = shipped.Weapon(weapon) with { Range = weapon == "sword" ? reach : shipped.Weapon(weapon).Range, StunChance = 0 };
        var settings = new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1000, MaxAlive = 8, Enemies = kinds, RageHitGain = 0, MaxElites = 8,
            Weapons = new[] { arms },
            Plan = new[] { new WaveSpec { Groups = kinds.Select(k => new WaveGroup { Enemy = k.Id, Count = 1, Elite = elite }).ToArray() } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 5, weapon: weapon, skills: skills);
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < kinds.Length; i++) sim.Step(dt, 100, false);
        foreach (var s in sim.Slimes) { s.X = 1500; s.SpitCooldown = 0; }
        sim.ClearEvents();
        return sim;
    }

    // Steps `seconds` with the cat at catX; true if an event of `kind` (and Value, if given) came.
    private static bool Run(BattleSim sim, double seconds, double catX, BattleEventKind? kind = null, double? value = null, bool held = false, Action? each = null)
    {
        bool seen = false;
        for (int i = 0; i < Math.Round(seconds / dt); i++)
        {
            each?.Invoke();
            sim.Step(dt, catX, held, held);
            seen |= kind is BattleEventKind k && sim.Events.Any(e => e.Kind == k && (value == null || e.Value == value));
            sim.ClearEvents();
        }
        return seen;
    }

    public static void Run(Action<bool, string> check)
    {
        SpitChecks(check);
        PuddleChecks(check);
        SpringChecks(check);
        DataChecks(check);
    }

    private static void SpitChecks(Action<bool, string> check)
    {
        var sim = Field(new[] { Spitter() });
        var spitter = sim.Slimes[0];
        spitter.X = 400;
        bool aimed = Run(sim, dt, 100, BattleEventKind.SpitAimed);
        check(aimed && spitter.Spitting && sim.SpitTargets(spitter).SequenceEqual(new[] { 100.0 }), "With the cat 300 DIPs off a spitter winds up a spit aimed at the cat's spot");
        double hp = sim.CatHp;
        bool hit = Run(sim, 1.9, 100, BattleEventKind.GooLanded, 1);
        check(hit && sim.CatHp == hp - 9 && !sim.CatStunned && sim.SpitHits == 1, "The drop lands on the cat still there: its damage, no stun");
        check(sim.Puddles.Count == 1 && sim.Puddles[0].X == 100 && sim.Puddles[0].Remaining > 5, "It leaves a puddle where it landed");
        check(Run(sim, 6, 1000, BattleEventKind.SpitAimed), "A cat beyond the former range (600 DIPs off) is spat at");
        check(!Run(sim, 6, 380, BattleEventKind.SpitAimed), "Nor one closer than its least range (20 DIPs off)");

        // The cat walks off the ring, or is lifted out of it: a miss.
        sim = Field(new[] { Spitter() });
        spitter = sim.Slimes[0];
        spitter.X = 400;
        Run(sim, dt, 100);
        hp = sim.CatHp;
        check(Run(sim, 1.9, 200, BattleEventKind.GooLanded, 0) && sim.CatHp == hp && sim.Puddles.Count == 1 && sim.Puddles[0].X == 100, "A cat that walked off the ring is missed (the puddle still forms there)");
        sim = Field(new[] { Spitter() });
        spitter = sim.Slimes[0];
        spitter.X = 400;
        Run(sim, dt, 100);
        hp = sim.CatHp;
        check(Run(sim, 1.9, 100, BattleEventKind.GooLanded, 0, held: true) && sim.CatHp == hp, "A cat lifted out of the ring is missed");
        check(!Run(sim, 2, 100, BattleEventKind.SpitAimed, held: true), "No spit starts while the user holds the cat");

        // The Shield takes it; a Push during the windup cancels it.
        sim = Field(new[] { Spitter() });
        spitter = sim.Slimes[0];
        spitter.X = 400;
        Run(sim, dt, 100);
        sim.Use(BattleSkill.Shield, 100);
        hp = sim.CatHp;
        check(Run(sim, 1.9, 100, BattleEventKind.Blocked) && sim.CatHp == hp, "The Bubble shield takes the drop");
        sim = Field(new[] { Spitter() });
        spitter = sim.Slimes[0];
        spitter.X = 400;
        Run(sim, 0.5, 100);
        check(sim.UsePush(spitter.Id, 100) && !spitter.Spitting, "A Push during the windup cancels the spit");
        check(!Run(sim, 2, 100, BattleEventKind.GooLanded) && sim.Goos.Count == 0, "No drop flies after it");

        // Pause freezes a drop in flight.
        sim = Field(new[] { Spitter() });
        spitter = sim.Slimes[0];
        spitter.X = 400;
        Run(sim, 1.2, 100);
        double progress = sim.Goos.Single().Progress;
        sim.Paused = true;
        Run(sim, 2, 100);
        check(sim.Goos.Count == 1 && sim.Goos[0].Progress == progress, "Pause freezes the drop in the air");
        sim.Paused = false;

        // An elite spits two drops, the second further on, and its puddles last longer.
        sim = Field(new[] { Spitter() }, elite: true);
        spitter = sim.Slimes[0];
        spitter.X = 400;
        Run(sim, dt, 300);
        var spec = BattleData.Settings.Enemy("spitter").Spit!;
        check(spitter.Elite != null && sim.SpitTargets(spitter).SequenceEqual(new[] { 300.0, 300 - spec.EliteSpread }), "An elite spits two drops, the second further from it");
        Run(sim, 1.9, 300);
        check(sim.Puddles.Count == 2 && sim.Puddles.All(p => p.Life == spec.PuddleSeconds * spec.ElitePuddle), "Its puddles last longer");

        // The yarn draws a spitter in (a support that keeps its distance).
        var yarn = Field(new[] { Spitter(), new EnemyType { Id = "plain", MaxHp = 1000, Speed = 0, Damage = 0, Range = 64 } }, weapon: "yarn");
        yarn.Slimes[0].X = 205; yarn.Slimes[1].X = 210;
        double nearest = double.MaxValue;
        Run(yarn, 1.5, 100, each: () => nearest = Math.Min(nearest, yarn.Slimes[0].X - 100));
        check(yarn.Pulls >= 1 && nearest < 75, $"The yarn draws a spitter in ({nearest:0} DIPs from the cat)");
    }

    private static void PuddleChecks(Action<bool, string> check)
    {
        // A runner dashes at the cat (800) from 950, through a puddle made there or not.
        double Dash(bool puddle, out BattleSim sim)
        {
            sim = puddle ? Field(new[] { Spitter(), Runner() }) : Field(new[] { Runner() });
            var runner = sim.Slimes.Single(s => s.Type.Dash);
            if (puddle)
            {
                sim.Slimes[0].X = 1100;
                Run(sim, 2, 800);
                sim.Slimes[0].X = 1590; // Out of range: no second spit.
            }
            runner.X = 950;
            double start = 950;
            for (int i = 0; i < 300 && runner.State != SlimeState.Recover; i++) { sim.Step(dt, 800, false); if (runner.State is SlimeState.Moving or SlimeState.Windup) start = runner.X; }
            return start - runner.X;
        }
        double plain = Dash(false, out _), boosted = Dash(true, out var withPuddle);
        check(withPuddle.PuddleBoosts == 1 && boosted > plain * 1.3, $"A dash through a puddle goes further ({boosted:0} DIPs against {plain:0})");

        // The fan dries the puddle.
        var sim = Field(new[] { Spitter() }, skills: new[] { BattleSkill.Push, BattleSkill.Fan, BattleSkill.Heal, BattleSkill.Shield, BattleSkill.Bomb });
        sim.Slimes[0].X = 400;
        Run(sim, 2, 100);
        sim.Slimes[0].X = 1590;
        check(sim.Puddles.Count == 1, "A puddle lies on the floor");
        sim.Arm(BattleSkill.Fan);
        check(sim.StartFan(150) && Run(sim, 0.1, 100, BattleEventKind.PuddleDried) && sim.Puddles.Count == 0 && sim.PuddlesDried == 1, "The Fan's wind dries it");
    }

    private static void SpringChecks(Action<bool, string> check)
    {
        var spec = BattleData.Settings.Enemy("springer").Spring!;
        var sim = Field(new[] { Spring() });
        var spring = sim.Slimes[0];
        spring.X = 400;
        bool woundUp = false;
        void Watch() => woundUp |= spring.State == SlimeState.Windup;
        Run(sim, 8, 100, each: Watch);
        check(Math.Abs(spring.X - (100 + spec.CreepRange)) < 2 && spring.Spring == SpringState.Ready, $"It slides up to a far cat and stops {spec.CreepRange:0} DIPs off");

        // In the ring: it squeezes, then springs on the cat still there.
        double hp = sim.CatHp;
        check(Run(sim, dt, spring.X - 30, BattleEventKind.SpringSqueezed) && spring.Spring == SpringState.Squeezing, "The cat stepping into its ring sets it squeezing");
        check(!Run(sim, spec.Compress - 0.1, spring.X - 30, BattleEventKind.SpringBounced, each: Watch), "It springs only after its squeeze");
        check(Run(sim, 0.2, spring.X - 30, BattleEventKind.SpringBounced, 1, each: Watch) && sim.CatHp == hp - 14 && sim.CatStunned && Math.Abs(sim.CatStunRemaining - spec.Stun) < 0.25,
            $"Then the cat still in the ring is hurt and stunned ~{spec.Stun:0.#} s");
        check(spring.Spring == SpringState.Resting && !Run(sim, spec.Rest - 0.3, spring.X - 30, BattleEventKind.SpringSqueezed, each: Watch), "It rests before it can squeeze again");
        check(Run(sim, 0.5, spring.X - 30, BattleEventKind.SpringSqueezed, each: Watch) && !woundUp, "Then it is set again (and it never winds up an attack)");

        // Lifted out while it squeezes: dodged; a lifted cat never sets it off.
        sim = Field(new[] { Spring() });
        spring = sim.Slimes[0];
        spring.X = 400;
        Run(sim, dt, 380);
        hp = sim.CatHp;
        check(spring.Spring == SpringState.Squeezing && Run(sim, 1, 380, BattleEventKind.SpringBounced, 0, held: true) && sim.CatHp == hp && !sim.CatStunned, "A cat lifted out while it squeezes is not hit");
        sim = Field(new[] { Spring() });
        spring = sim.Slimes[0];
        spring.X = 400;
        check(!Run(sim, 2, 380, BattleEventKind.SpringSqueezed, held: true), "A lifted cat over the ring never sets it off");

        // A Push spends the squeeze.
        sim = Field(new[] { Spring() });
        spring = sim.Slimes[0];
        spring.X = 400;
        Run(sim, 0.3, 380);
        hp = sim.CatHp;
        check(sim.UsePush(spring.Id, 380) && !Run(sim, 1, 380, BattleEventKind.SpringBounced) && spring.Spring == SpringState.Resting && sim.CatHp == hp, "A Push while it squeezes spends the spring");

        // Pause freezes the squeeze.
        sim = Field(new[] { Spring() });
        spring = sim.Slimes[0];
        spring.X = 400;
        Run(sim, 0.3, 380);
        double left = spring.SpringClock;
        sim.Paused = true;
        Run(sim, 2, 380);
        check(spring.Spring == SpringState.Squeezing && spring.SpringClock == left, "Pause freezes its squeeze");

        // Hits flatten it: no trap meanwhile, set again after.
        sim = Field(new[] { Spring() }, reach: 76);
        spring = sim.Slimes[0];
        spring.X = 400;
        void Pin() { spring.X = 400; spring.KnockVelocity = 0; } // The cat's knock back would push it off.
        bool flat = Run(sim, 3, 325, BattleEventKind.SpringFlattened, each: Pin);
        check(flat && spring.Spring == SpringState.Flat && sim.SpringsFlattened == 1, $"{spec.FlattenHits} hits of the cat's weapon flatten it");
        check(!Run(sim, spring.SpringClock - 0.3, 385, BattleEventKind.SpringSqueezed, each: Pin), "Flattened, it lies still: the cat in its ring is safe");
        check(Run(sim, 0.6, 385, BattleEventKind.SpringSqueezed, each: Pin), "Then it is set again");

        // An elite's ring is bigger.
        sim = Field(new[] { Spring() }, elite: true);
        check(sim.Slimes[0].Elite != null && BattleSim.SpringRadius(sim.Slimes[0]) == spec.Radius * spec.EliteRadius, "An elite's ring is bigger");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        bool Rejects(string extra)
        {
            try { BattleData.Parse("{\"enemies\":[{\"id\":\"slime\",\"nameVi\":\"S\",\"nameEn\":\"S\"," + extra + "},{\"id\":\"king\",\"nameVi\":\"K\",\"nameEn\":\"K\",\"boss\":true}]}"); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(!Rejects("\"spit\":{\"range\":300}") && !Rejects("\"spring\":{\"radius\":80}"), "A spit and a spring within their limits are accepted");
        check(Rejects("\"spit\":{\"range\":300,\"minRange\":300}") && Rejects("\"spit\":{\"eliteDrops\":4}") && Rejects("\"dash\":true,\"spit\":{}")
              && Rejects("\"spring\":{\"stun\":4}") && Rejects("\"support\":true,\"spring\":{}") && Rejects("\"spit\":{},\"spring\":{}") && Rejects("\"marks\":\"spoon\""),
            "Out-of-range or mixed spit and spring data is refused");
        var s = BattleData.Settings;
        check(s.Enemy("spitter") is { Id: "spitter", Support: true, BinSpit: false, Spit: not null, Marks: "spout", Elite: not null }
              && s.Enemy("springer") is { Id: "springer", BinSpit: false, Spring: not null, Marks: "spring", Elite: not null },
            "The shipped spitter (a support) and spring slime, each with an elite, never spat by a bin");
        check(!s.WavePlan.SelectMany(w => w.Groups).Any(g => g.Enemy is "spitter" or "springer" || g.Pick?.Keys.Any(k => k is "spitter" or "springer") == true),
            "The free battle's waves do not use them yet");
    }
}
