using DesktopPet;

// Rescuing the cat no longer freezes the slimes (user request 2026-10-20, handoff in NEXT-STEPS):
// an attack under way runs on its announced time and misses if it falls while the cat is lifted
// (put back before, it lands as usual), a dash runs its locked lane, supports go on healing,
// walking slimes go by where the cat last stood on the floor. A cat only pressed (not lifted) is still on the floor: an attack under way reaches it,
// without a stun. Waves, the survive clock and the skills still wait while the cat is held.
internal static class RescueTests
{
    const double dt = 1.0 / 60;

    private static readonly EnemyType Fighter = new() { Id = "slime", MaxHp = 1000, Speed = 0, Damage = 10, Range = 64, Windup = 0.8, AttackCooldown = 1.8, Recover = 0.35 };
    private static readonly EnemyType Runner = new()
    {
        Id = "runner", MaxHp = 1000, Speed = 0, Damage = 10, Range = 54, Windup = 0.7, AttackCooldown = 2.6, Recover = 0.4, Scale = 0.75,
        Dash = true, DashRange = 170, DashDistance = 230, DashSpeed = 420
    };
    private static readonly EnemyType Mender = new()
    {
        Id = "mender", MaxHp = 40, Speed = 0, Damage = 0, Range = 56, Windup = 0.8, AttackCooldown = 2, Scale = 0.9, Support = true, KeepBack = 110,
        HealAmount = 10, HealRange = 2000, HealCooldown = 0.5, HealWindup = 1, HealBudget = 1000, HealGuard = true, HealGuardRepeat = 2
    };

    // The kinds put at these places, the cat at 800 (no landing protection unless asked, so only
    // the rescue rule decides whether a hit lands).
    private static BattleSim Field(double grace, params (EnemyType Kind, double X)[] place)
    {
        var settings = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, CatDamage = 0, CatMaxHp = 1000, MaxAlive = 8, SpawnMinDistance = 0, LandingGraceSeconds = grace,
            Enemies = place.Select(p => p.Kind).DistinctBy(k => k.Id).ToArray(),
            Plan = new[] { new WaveSpec { Groups = place.Select(p => new WaveGroup { Enemy = p.Kind.Id }).ToArray() } } };
        var sim = new BattleSim(settings, 0, 1600, seed: 1);
        for (int i = 0; i < 120 && sim.Slimes.Count(s => s.Targetable) < place.Length; i++) sim.Step(dt, -5000, false);
        for (int i = 0; i < place.Length; i++) { sim.Slimes[i].X = place[i].X; sim.Slimes[i].Cooldown = 0; }
        sim.ClearEvents();
        return sim;
    }

    // Steps until done() (or the time runs out), counting events of a kind on the way.
    private static int Until(BattleSim sim, Func<bool> done, double seconds, double catX = 800, bool held = false, bool lifted = false, BattleEventKind? count = null)
    {
        int n = 0;
        for (double t = 0; t < seconds && !done(); t += dt)
        {
            sim.Step(dt, catX, held, lifted);
            if (count is BattleEventKind kind) n += sim.Events.Count(e => e.Kind == kind);
            sim.ClearEvents();
        }
        return n;
    }

    public static void Run(Action<bool, string> check)
    {
        WindupChecks(check);
        DashChecks(check);
        FieldChecks(check);
        FreezeChecks(check);
    }

    private static void WindupChecks(Action<bool, string> check)
    {
        // 1. Lifted during a windup and carried away: it runs on its own time, misses once and
        // recovers once; no new windup while carried, no hit.
        var sim = Field(0, (Fighter, 840));
        var slime = sim.Slimes[0];
        Until(sim, () => slime.State == SlimeState.Windup, 1);
        double at = slime.StateTime;
        sim.Step(dt, 1300, true, true);
        bool runs = slime.State == SlimeState.Windup && Math.Abs(slime.StateTime - at - dt) < 1e-9;
        int recovers = 0, hurt = 0;
        bool rewound = false;
        for (double t = 0; t < 2.5; t += dt)
        {
            var before = slime.State;
            sim.Step(dt, 1300, true, true);
            if (before != SlimeState.Recover && slime.State == SlimeState.Recover) recovers++;
            rewound |= recovers > 0 && slime.State == SlimeState.Windup;
            hurt += sim.Events.Count(e => e.Kind == BattleEventKind.CatHurt);
            sim.ClearEvents();
        }
        check(runs && hurt == 0 && sim.CatHp == 1000 && recovers == 1 && !rewound,
            "Lifted during a windup: it is not frozen, it plays out, misses once and recovers once; no new windup while carried");

        // 2. The hit falls on its announced time whatever the lifting: on a frame the cat is up it
        // misses; put back before, it lands then (not at once on the way down, not later).
        var up = Field(0, (Fighter, 840));
        var foe = up.Slimes[0];
        Until(up, () => foe.State == SlimeState.Windup && foe.StateTime >= up.Windup(foe) - dt * 1.5, 2);
        up.Step(dt, 800, true, true);
        check(foe.State == SlimeState.Recover && up.CatHp == 1000 && up.Grace == 0,
            "Lifted on the very frame the hit falls (back on the same spot, no landing protection): it misses");
        var down = Field(0, (Fighter, 840));
        var hitter = down.Slimes[0];
        Until(down, () => hitter.State == SlimeState.Windup, 1);
        double startedAt = down.Time - hitter.StateTime;
        for (int i = 0; i < 6; i++) down.Step(dt, 800, true, true);
        bool notYet = down.CatHp == 1000 && hitter.State == SlimeState.Windup;
        down.Step(dt, 800, false);
        bool noBurst = down.CatHp == 1000;
        Until(down, () => down.CatHp < 1000, 1);
        check(notYet && noBurst && down.CatHp == 990 && Math.Abs(down.Time - startedAt - down.Windup(hitter)) <= dt * 1.01,
            $"Put back before the hit falls, it lands on its announced time ({down.Time - startedAt:F3} s after the windup began, windup {down.Windup(hitter)} s), never at once on the way down");

        // Lifting and dropping on the spot does not stack landing protection (one per RescueGraceCooldown).
        var lifts = Field(1.5, (Fighter, 840));
        Until(lifts, () => false, 0.4, 800, true, true); lifts.Step(dt, 800, false);
        double first = lifts.Grace;
        for (int i = 0; i < 5; i++) { lifts.Step(dt, 800, true, true); lifts.Step(dt, 800, false); }
        check(first > 0 && lifts.Grace < first && lifts.GraceCooldown > 0, "Lifting again and again neither renews nor extends the landing protection");

        // 1a: a cat only pressed (a click, not lifted) is on the floor: the windup under way lands.
        var press = Field(0, (Fighter, 840));
        var pressed = press.Slimes[0];
        Until(press, () => pressed.State == SlimeState.Windup, 1);
        Until(press, () => pressed.State != SlimeState.Windup, 1, held: true);
        check(press.CatHp == 990, "A cat only pressed (not lifted) is still hit by the windup under way");
        Until(press, () => false, 3, held: true);
        check(press.CatHp == 990 && pressed.State != SlimeState.Windup, "No new windup starts while the cat is pressed");
    }

    private static void DashChecks(Action<bool, string> check)
    {
        // 3. A dash under way runs its whole locked lane even if the cat is carried behind it; no
        // damage, no stun; the next one is fully announced.
        var sim = Field(0, (Runner, 950));
        var runner = sim.Slimes[0];
        Until(sim, () => runner.State == SlimeState.Windup, 1);
        int lane = runner.DashDirection;
        double from = runner.X;
        Until(sim, () => runner.State == SlimeState.Dashing, 1, 400, true, true);
        bool locked = true;
        for (double t = 0; t < 1.5 && runner.State == SlimeState.Dashing; t += dt) { sim.Step(dt, 1500, true, true); locked &= runner.DashDirection == lane; }
        check(lane == -1 && locked && Math.Abs(runner.X - (from - runner.Type.DashDistance)) < 1 && sim.CatHp == 1000 && !sim.CatStunned,
            $"A dash under way runs its locked lane to the end while the cat is carried the other way ({from:F0} → {runner.X:F0}); no damage, no stun");
        // A cat held up over the lane while the dash passes under it is not hit.
        var over = Field(0, (Runner, 950));
        var r2 = over.Slimes[0];
        Until(over, () => r2.State == SlimeState.Dashing, 2);
        Until(over, () => r2.State != SlimeState.Dashing, 1, 800, true, true);
        check(over.CatHp == 1000 && !over.CatStunned, "A dash passing under the lifted cat does not hit it");
        over.Step(dt, 800, false);
        Until(over, () => r2.State == SlimeState.Windup, 4);
        check(r2.State == SlimeState.Windup && r2.StateTime < 2 * dt, "The next dash starts with its whole windup");
        // 1a: a pressed cat in the lane takes the dash's damage but no stun.
        var press = Field(0, (Runner, 950));
        var r3 = press.Slimes[0];
        Until(press, () => r3.State == SlimeState.Windup, 1);
        Until(press, () => r3.State is SlimeState.Recover or SlimeState.Moving, 2, held: true);
        check(press.CatHp == 990 && !press.CatStunned && press.StunsTaken == 0, "A pressed cat in a dash's lane takes its damage but is not stunned");
    }

    private static void FieldChecks(Action<bool, string> check)
    {
        // Walking slimes go by where the cat last stood, not by the cursor.
        var sim = Field(0, (new EnemyType { Id = "walker", MaxHp = 1000, Speed = 60, Damage = 0, Range = 64, Windup = 0.8, AttackCooldown = 1.8 }, 300));
        var walker = sim.Slimes[0];
        sim.Step(dt, 800, false); // The cat stands at 800 before it is lifted.
        Until(sim, () => false, 3, 1500, true, true);
        check(walker.X > 330 && Math.Abs(walker.X - 800) < Math.Abs(300 - 800) && sim.Slimes[0].State != SlimeState.Windup,
            $"While the cat is carried to 1500, a slime walks toward where it stood (800): {walker.X:F0}");
        sim.Step(dt, 1500, false);
        double towards = walker.X;
        Until(sim, () => false, 0.5, 1500);
        check(walker.X > towards, "Put down elsewhere, the slimes head for the new spot");

        // 4. A support goes on healing (and guarding) while the cat is carried.
        var heal = Field(0, (Fighter, 600), (Mender, 900));
        var ally = heal.Slimes[0];
        var mender = heal.Slimes[1];
        ally.Hp = 300;
        Until(heal, () => mender.State == SlimeState.Casting, 2, 100, true, true);
        bool guarded = ally.Mended;
        int healed = Until(heal, () => mender.State != SlimeState.Casting, 2, 100, true, true, BattleEventKind.EnemyHealed);
        check(guarded && healed == 1 && ally.Hp > 300 && !ally.Mended, "A support heals (and guards) as usual while the cat is carried; the guard ends with the heal");
    }

    private static void FreezeChecks(Action<bool, string> check)
    {
        // 5. Pause freezes everything, lifted or not, and resuming does not release stored time.
        var sim = Field(0, (Fighter, 840));
        var slime = sim.Slimes[0];
        Until(sim, () => slime.State == SlimeState.Windup, 1);
        double at = slime.StateTime;
        sim.Paused = true;
        Until(sim, () => false, 2, 800, true, true);
        check(slime.State == SlimeState.Windup && slime.StateTime == at, "Paused while carried: the windup does not move");
        sim.Paused = false;
        sim.Step(dt, 800, false);
        check(slime.State == SlimeState.Windup && Math.Abs(slime.StateTime - at - dt) < 1e-9 && sim.CatHp == 1000, "Resuming goes on from where it stopped (no burst)");

        // Waves and the survive clock still wait while the cat is carried.
        var stage = new StageSpec { Id = "s", NameVi = "S", NameEn = "S", Objective = "survive", SurviveSeconds = 30, Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 } } } } };
        var hold = new BattleSim(new BattleSettings { FirstWaveDelay = 0 }, 0, 1600, new Random(1), stage: stage);
        hold.Step(dt, 800, false);
        double left = hold.SurviveRemaining;
        int toSpawn = hold.ToSpawn;
        Until(hold, () => false, 2, 800, true, true);
        check(hold.SurviveRemaining == left && hold.ToSpawn == toSpawn && hold.SkillsLocked, "Carrying the cat still stops the survive clock, the waves and the skills");

        // 6. The last slime falls (to a bomb thrown just before) while the cat is carried: the win
        // waits for the cat to be put down, as the wave does, and leaves nothing targeted.
        var end = Field(0, (Fighter, 840));
        var last = end.Slimes[0];
        last.Hp = 1;
        bool thrown = end.Use(BattleSkill.Bomb, last.Id, 800);
        Until(end, () => false, 1.5, 800, true, true);
        bool waited = !end.Over && last.State is SlimeState.Defeated || !end.Slimes.Contains(last);
        Until(end, () => end.Over, 3);
        check(thrown && waited && end.Phase == BattlePhase.Won && end.TargetId == null && end.CatHp == 1000,
            "The last slime falling while the cat is carried: the win comes once it is put down, nothing left targeted");
    }
}
