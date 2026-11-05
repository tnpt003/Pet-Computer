using DesktopPet;

// 1.5 M13 (PetBattle.Tricks.cs), with stand-ins that carry the shipped ghost, bubble, mirage and sulk
// numbers. The ghost fades out on its clock: then the cat does not go for it, hits pass through, it never
// attacks and drifts through the cat; the cursor or the laser brings it out and keeps it out; a cat in the
// hand, Pause and the frozen guide leave it be. The bubble takes every hit until clicked enough (an elite
// more) or cut by the Scissors, then drops dazed and grows back. The mirage comes with copies that hurt
// nothing, vanish when hit (never the cat's kill, never in Alive), mirror the real one's HP and go with it;
// the cursor shows them for what they are. The sulky slime takes no HP and puffs up (its hits grow with
// it); strokes of the cursor soothe it into leaving, counted as the cat's; a still cursor, a jump of it or
// a cat in the hand do nothing; left alone it sulks again. The data limits, the shipped kinds (kept out of
// every battle for now) and the try-out files.
internal static class BattleTrickTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id, double damage = 0, double speed = 0, GhostSpec? ghost = null, BubbleSpec? bubble = null,
        MirageSpec? mirage = null, SulkSpec? sulk = null) => new()
    {
        Id = id, MaxHp = 100, Speed = speed, Damage = damage, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3,
        Elite = new EliteSpec { Hp = 1.2 }, Ghost = ghost, Bubble = bubble, Mirage = mirage, Sulk = sulk
    };

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static EnemyType Ghost(double damage = 0, double speed = 0) => Kind("phantom", damage, speed, ghost: Shipped.Enemy("phantom").Ghost);
    private static EnemyType Bubble(double damage = 0) => Kind("bubble", damage, bubble: Shipped.Enemy("bubble").Bubble);
    private static EnemyType Mirage(double damage = 0) => Kind("mirage", damage, mirage: Shipped.Enemy("mirage").Mirage);
    private static EnemyType Sulk(double damage = 0) => Kind("sulky", damage, sulk: Shipped.Enemy("sulky").Sulk);

    private static readonly BattleSkill[] Tools = { BattleSkill.Bomb, BattleSkill.Laser, BattleSkill.Scissors, BattleSkill.Push, BattleSkill.Grabber };

    // A field with these kinds and counts (elite: the first kind elite), the enemies at 1000 and the cat at 100.
    private static BattleSim Field(EnemyType[] kinds, int[] counts, bool elite = false, double catHp = 1000, WeaponSpec? weapon = null, bool spread = true)
    {
        var arms = weapon ?? Shipped.Weapon("sword") with { Range = 5, StunChance = 0, Damage = 0 };
        var settings = new BattleSettings
        {
            Weapons = new[] { arms }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = catHp, MaxAlive = 8, MaxElites = 8,
            Enemies = kinds, RageHitGain = 0, BombDamage = 10, BombCooldown = 0.1, BombDelay = 0.3, BombRadius = 20,
            LaserCooldown = 0.1, ScissorsCooldown = 0.1, PushCooldown = 0.1,
            Plan = new[] { new WaveSpec { Groups = kinds.Select((k, i) => new WaveGroup { Enemy = k.Id, Count = counts[i], Elite = elite && i == 0 }).ToArray() },
                new WaveSpec { Groups = new[] { new WaveGroup { Enemy = kinds[0].Id, Count = 1 } } } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 4, skills: Tools);
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable && !s.Decoy) < counts.Sum(); i++) sim.Step(dt, 100, false);
        for (int i = 0; i < 30; i++) sim.Step(dt, 100, false); // Copies appear and finish appearing too.
        // Apart, so a bomb on one does not reach another (a mirage's copies too); a ghost's clock starts now.
        for (int i = 0; spread && i < sim.Slimes.Count; i++) { sim.Slimes[i].X = 1000 - 160 * i; sim.Slimes[i].GhostClock = sim.Slimes[i].Type.Ghost?.Visible ?? 0; }
        sim.ClearEvents();
        return sim;
    }

    // Steps `seconds` with the cat at catX; true if an event of `kind` came.
    private static bool Run(BattleSim sim, double seconds, double catX = 100, BattleEventKind? kind = null, bool held = false, Action? each = null)
    {
        bool seen = false;
        for (int i = 0; i < Math.Round(seconds / dt); i++)
        {
            each?.Invoke();
            sim.Step(dt, catX, held, held);
            seen |= kind is BattleEventKind k && sim.Events.Any(e => e.Kind == k);
            sim.ClearEvents();
        }
        return seen;
    }

    // The yarn bomb on one enemy (it lands BombDelay later).
    private static bool Bomb(BattleSim sim, Slime slime)
    {
        for (int i = 0; i < 30 && !sim.Ready(BattleSkill.Bomb); i++) sim.Step(dt, 100, false);
        return sim.Use(BattleSkill.Bomb, slime.Id, 100);
    }

    public static void Run(Action<bool, string> check)
    {
        GhostChecks(check);
        BubbleChecks(check);
        MirageChecks(check);
        SulkChecks(check);
        DataChecks(check);
    }

    private static void GhostChecks(Action<bool, string> check)
    {
        var spec = Shipped.Enemy("phantom").Ghost!;
        var sim = Field(new[] { Ghost() }, new[] { 1 });
        var ghost = sim.Slimes.Single();
        check(!ghost.Faded && !Run(sim, spec.Visible - 0.3, kind: BattleEventKind.GhostFaded), "A ghost starts seen and stays seen for its visible time");
        check(Run(sim, 0.5, kind: BattleEventKind.GhostFaded) && ghost.Faded, $"Then it fades out ({spec.Visible} s seen)");
        sim.Step(dt, 100, false);
        check(sim.TargetId == null && !sim.Select(ghost.Id) && !sim.Use(BattleSkill.Bomb, ghost.Id, 100), "Faded, the cat does not go for it, a click cannot choose it, a bomb cannot be aimed at it");
        Run(sim, spec.Hidden + 0.1);
        check(!ghost.Faded, $"It comes back by itself after {spec.Hidden} s");

        // A bomb thrown while seen lands after it faded: it passes through.
        sim = Field(new[] { Ghost() }, new[] { 1 });
        ghost = sim.Slimes.Single();
        Bomb(sim, ghost);
        ghost.Faded = true; ghost.GhostClock = 5;
        double hp = ghost.Hp;
        check(Run(sim, 0.5, kind: BattleEventKind.GhostMissed) && ghost.Hp == hp, "A hit on a faded ghost passes through (no damage, a miss)");
        ghost.Faded = false;
        Bomb(sim, ghost);
        Run(sim, 0.5);
        check(ghost.Hp < hp, "Seen, the same bomb hurts it");

        // Faded next to the cat: no attack; it drifts through the cat to its other side.
        sim = Field(new[] { Ghost(damage: 10, speed: 60) }, new[] { 1 });
        ghost = sim.Slimes.Single();
        ghost.X = 160;
        ghost.Faded = true; ghost.GhostClock = 3; ghost.FadeSide = 1;
        bool wound = false;
        Run(sim, 2.5, each: () => wound |= ghost.State == SlimeState.Windup);
        check(!wound && sim.CatHp == sim.CatMaxHp, "A faded ghost next to the cat never attacks");
        check(ghost.X < 100, $"It drifts through the cat to its other side (x {ghost.X:0}, cat at 100)");

        // The cursor brings it out and keeps it out; the laser too.
        sim = Field(new[] { Ghost() }, new[] { 1 });
        ghost = sim.Slimes.Single();
        ghost.Faded = true; ghost.GhostClock = 10;
        sim.SetPointer((ghost.X, 10));
        check(Run(sim, dt, kind: BattleEventKind.GhostRevealed) && !ghost.Faded && sim.GhostsRevealed == 1, "The cursor over a faded ghost brings it out");
        check(!Run(sim, 6, kind: BattleEventKind.GhostFaded) && !ghost.Faded, "It stays out while the cursor stays on it");
        sim.SetPointer(null);
        check(!Run(sim, spec.Reveal - 0.3, kind: BattleEventKind.GhostFaded), $"Off it, it stays out {spec.Reveal} s more");
        check(Run(sim, 0.6, kind: BattleEventKind.GhostFaded), "Then it fades again");
        sim.SetPointer((ghost.X + 200, 10));
        ghost.GhostClock = 10;
        check(!Run(sim, 0.5, kind: BattleEventKind.GhostRevealed) && ghost.Faded, "The cursor elsewhere on the field does nothing");
        sim.SetPointer((ghost.X, 10));
        check(!Run(sim, 0.5, held: true, kind: BattleEventKind.GhostRevealed) && ghost.Faded, "With the cat in the user's hand the cursor does nothing");
        sim.Paused = true;
        check(!Run(sim, 0.5, kind: BattleEventKind.GhostRevealed) && ghost.Faded, "Paused, nothing changes");
        sim.Paused = false;
        sim.SetPointer(null);
        sim.Arm(BattleSkill.Laser);
        check(sim.UseAt(BattleSkill.Laser, ghost.X + spec.LaserRange - 20) && Run(sim, dt, kind: BattleEventKind.GhostRevealed) && !ghost.Faded,
            $"The laser's dot within {spec.LaserRange} DIPs brings it out");

        sim = Field(new[] { Ghost() }, new[] { 1 }, elite: true);
        ghost = sim.Slimes.Single();
        ghost.Faded = true; ghost.GhostClock = dt;
        Run(sim, 2 * dt);
        check(ghost.Elite != null && !ghost.Faded && Run(sim, spec.EliteVisible + 0.2, kind: BattleEventKind.GhostFaded), $"An elite shows itself only {spec.EliteVisible} s at a time");
    }

    private static void BubbleChecks(Action<bool, string> check)
    {
        var spec = Shipped.Enemy("bubble").Bubble!;
        // 2026-11-04: the cat's combo blow counts as one poke; plain blows only bounce off.
        {
            var paw = Shipped.Weapon("sword") with { Range = 5, StunChance = 0, Damage = 1 };
            var field = Field(new[] { Bubble() }, new[] { 1 }, weapon: paw);
            var one = field.Slimes.Single();
            one.X = 100;
            int pokes = 0, blocked = 0;
            for (int i = 0; i < 600 && one.Bubbled; i++)
            {
                field.Step(dt, 100, false);
                pokes += field.Events.Count(e => e.Kind == BattleEventKind.BubblePoked);
                blocked += field.Events.Count(e => e.Kind == BattleEventKind.BubbleBlocked);
                field.ClearEvents();
            }
            check(!one.Bubbled && pokes == spec.Pops && blocked >= pokes * (field.ComboHits - 1),
                $"Only the cat's combo blows poke a bubble, one poke each ({pokes} pokes, {blocked} plain blows bounced)");
        }
        var sim = Field(new[] { Bubble() }, new[] { 1 });
        var bubble = sim.Slimes.Single();
        double hp = bubble.Hp;
        Bomb(sim, bubble);
        check(bubble.Bubbled && Run(sim, 0.5, kind: BattleEventKind.BubbleBlocked) && bubble.Hp == hp, "In its bubble it takes no hit");
        check(BattleSim.BubbleLift(bubble) == spec.Lift, $"The bubble floats it {spec.Lift} DIPs up");
        for (int i = 0; i < spec.Pops - 1; i++) sim.Poke(bubble.Id);
        check(bubble.Bubbled && bubble.Pokes == spec.Pops - 1, $"{spec.Pops - 1} clicks do not pop it yet");
        check(sim.Poke(bubble.Id) && !bubble.Bubbled && bubble.State == SlimeState.Stunned && sim.BubblesPopped == 1, $"Click {spec.Pops} pops it: it drops dazed");
        check(!sim.Poke(bubble.Id) && sim.Select(bubble.Id), "Out of its bubble, a click chooses it as usual");
        Bomb(sim, bubble);
        Run(sim, 0.5);
        check(bubble.Hp < hp, "Out of its bubble it is hurt");
        check(!Run(sim, spec.Regrow - 1, kind: BattleEventKind.BubbleRegrown) && Run(sim, 1.2, kind: BattleEventKind.BubbleRegrown) && bubble.Bubbled,
            $"The bubble grows back {spec.Regrow} s later");
        sim.Paused = true;
        check(!sim.Poke(bubble.Id) && bubble.Pokes == 0, "Paused, a click does nothing");
        sim.Paused = false;

        // The Scissors cut through it.
        sim.Arm(BattleSkill.Scissors);
        hp = bubble.Hp;
        bool snipped = sim.UseOnScreen(BattleSkill.Scissors, bubble.X, 20);
        for (int i = 0; i < 120 && sim.Cut != null; i++) { sim.Step(1.0 / 60, 100, false); sim.ClearEvents(); }
        check(snipped && !bubble.Bubbled && bubble.Hp == hp, "A Scissors cut through it pops the bubble (and hurts nothing)");

        sim = Field(new[] { Bubble() }, new[] { 1 }, elite: true);
        bubble = sim.Slimes.Single();
        for (int i = 0; i < spec.Pops; i++) sim.Poke(bubble.Id);
        check(bubble.Elite != null && bubble.Bubbled, $"An elite's bubble takes more than {spec.Pops} clicks");
        for (int i = spec.Pops; i < spec.ElitePops; i++) sim.Poke(bubble.Id);
        check(!bubble.Bubbled, $"It pops on click {spec.ElitePops}");

        // The cat leaves one in a bubble alone while there is another to hit.
        sim = Field(new[] { Bubble(), Kind("slime") }, new[] { 1, 1 });
        var plain = sim.Slimes.Single(s => s.Type.Id == "slime");
        bubble = sim.Slimes.Single(s => s.Type.Id == "bubble");
        bubble.X = 200; plain.X = 600;
        sim.Step(dt, 100, false);
        check(sim.TargetId == plain.Id, "The cat goes for another enemy rather than one in a bubble");
    }

    private static void MirageChecks(Action<bool, string> check)
    {
        var spec = Shipped.Enemy("mirage").Mirage!;
        // 2026-11-04: the copies come on the cat's other side; the gacha now and then swaps the real one with a copy.
        {
            var field = Field(new[] { Mirage() }, new[] { 1 }, spread: false);
            var one = field.Slimes.Single(x => !x.Decoy);
            var fakes = field.Slimes.Where(x => x.Decoy).ToList();
            int side = Math.Sign(one.X - 100);
            check(fakes.Count == spec.Copies && fakes.All(c => Math.Sign(c.X - 100) == -side),
                $"The copies come on the cat's other side (real at {one.X:0}, copies at {string.Join(", ", fakes.Select(c => c.X.ToString("0")))})");
            int swaps = 0, smokes = 0;
            double startX = one.X;
            var spots = field.Slimes.Where(x => x.Targetable).Select(x => x.X).OrderBy(x => x).ToList();
            for (int i = 0; i < Math.Round(spec.Shuffle * 20 / dt); i++)
            {
                foreach (var x in field.Slimes) { x.Cooldown = 9; x.KnockVelocity = 0; } // Standing still: only the gacha moves them.
                field.Step(dt, 100, false);
                foreach (var e in field.Events.Where(e => e.Kind == BattleEventKind.MirageShuffled)) { smokes++; if (e.Value > 0) swaps++; }
                field.ClearEvents();
            }
            check(smokes >= 15 && swaps > 0 && swaps < smokes, $"Every {spec.Shuffle} s the gacha smokes the group, sometimes swapping ({swaps}/{smokes})");
            // 2026-11-05: its own big smoke over each of them, and a warning just before, the same on all.
            var group = field.Slimes.Where(x => x.Targetable).ToList();
            foreach (var x in field.Slimes) { x.Cooldown = 9; x.KnockVelocity = 0; }
            one.ShuffleClock = BattleSim.ShuffleWarn + 0.05;
            bool calm = !group.Any(field.ShuffleSoon);
            one.ShuffleClock = BattleSim.ShuffleWarn - 0.05;
            bool warned = group.All(field.ShuffleSoon);
            one.ShuffleClock = dt / 2;
            field.ClearEvents();
            field.Step(dt, 100, false);
            int fresh = field.Effects.Count(e => e.Kind == BattleEffectKind.MirageSmoke && e.Age < 0.1);
            check(calm && warned, $"The whole group shimmers just before the gacha, alike ({group.Count} slimes)");
            check(fresh == group.Count, $"Then each one gets the big smoke ({fresh}/{group.Count})");
        }
        var sim = Field(new[] { Mirage(damage: 10) }, new[] { 1 });
        var real = sim.Slimes.Single(s => !s.Decoy);
        var copies = sim.Slimes.Where(s => s.Decoy).ToList();
        check(copies.Count == spec.Copies && copies.All(c => c.DecoyOf == real.Id && c.Type == real.Type) && sim.Alive == 1,
            $"A mirage comes with {spec.Copies} look-alike copies, not counted as enemies on the field");
        Bomb(sim, real);
        Run(sim, 0.5);
        check(real.Hp < real.MaxHp && copies.All(c => c.Hp == real.Hp), "Its copies show the real one's HP");
        int defeated = sim.Defeated;
        Bomb(sim, copies[0]);
        check(Run(sim, 0.5, kind: BattleEventKind.MirageDispelled) && copies[0].State == SlimeState.Defeated && sim.Defeated == defeated && sim.DecoysDispelled == 1,
            "A hit on a copy makes it vanish, not counted as the cat's");
        sim.Arm(BattleSkill.Push);
        check(sim.Use(BattleSkill.Push, copies[1].Id, 100) && copies[1].State == SlimeState.Defeated, "A push on a copy makes it vanish too");

        // A copy's blow hurts nothing; the real one's does.
        sim = Field(new[] { Mirage(damage: 10) }, new[] { 1 });
        real = sim.Slimes.Single(s => !s.Decoy);
        var copy = sim.Slimes.First(s => s.Decoy);
        copy.X = 140; real.X = 900;
        bool struck = false;
        Run(sim, 2, each: () => struck |= copy.State == SlimeState.Windup);
        check(struck && sim.CatHp == sim.CatMaxHp, "A copy winds up and strikes like the real one, but hurts nothing");
        copy.X = 900; real.X = 140;
        Run(sim, 2);
        check(sim.CatHp < sim.CatMaxHp, "The real one's strike hurts");

        // The cursor shows them for what they are.
        sim = Field(new[] { Mirage() }, new[] { 1 });
        real = sim.Slimes.Single(s => !s.Decoy);
        copy = sim.Slimes.First(s => s.Decoy);
        copy.X = 600; real.X = 1000;
        sim.SetPointer((600, 10));
        Run(sim, dt);
        check(copy.Revealed > 0 && real.Revealed == 0, "The cursor over a copy shows it for what it is (only that one)");
        sim.SetPointer(null);
        Run(sim, spec.Reveal - 0.3);
        bool still = copy.Revealed > 0;
        Run(sim, 0.4);
        check(still && copy.Revealed == 0, $"For {spec.Reveal} s after the cursor leaves");

        // The real one defeated: its copies go, the wave is cleared.
        sim = Field(new[] { Mirage() }, new[] { 1 });
        real = sim.Slimes.Single(s => !s.Decoy);
        real.Hp = 1;
        Bomb(sim, real);
        bool cleared = Run(sim, 0.4, kind: BattleEventKind.WaveCleared);
        check(real.State == SlimeState.Defeated && sim.Slimes.Where(s => s.Decoy).All(s => s.State == SlimeState.Defeated) && sim.Defeated == 1 && sim.DecoysDispelled == spec.Copies,
            "The real one defeated (the cat's), its copies vanish with it");
        check(cleared || Run(sim, 4, kind: BattleEventKind.WaveCleared), "And the wave is cleared");

        sim = Field(new[] { Mirage() }, new[] { 1 }, elite: true);
        check(sim.Slimes.Count(s => s.Decoy) == spec.EliteCopies && sim.Slimes.Where(s => s.Decoy).All(s => s.Elite != null),
            $"An elite comes with {spec.EliteCopies} copies, elite-looking too");
    }

    private static void SulkChecks(Action<bool, string> check)
    {
        var spec = Shipped.Enemy("sulky").Sulk!;
        // A weapon that lands (1 damage): a 0-damage blow never reaches the sulky slime's own rule.
        var paw = Shipped.Weapon("sword") with { Range = 5, StunChance = 0, Damage = 1 };
        var sim = Field(new[] { Sulk(damage: 10) }, new[] { 1 }, weapon: paw);
        var sulky = sim.Slimes.Single();
        double hp = sulky.Hp;
        // 2026-11-04: only the cat's own blow on it counts; a bomb (any skill, app, splash) does nothing.
        Bomb(sim, sulky);
        check(!Run(sim, 0.5, kind: BattleEventKind.SulkPuffed) && sulky.Hp == hp && sulky.Puff == 1, "A bomb neither hurts nor puffs up a sulky slime");
        sulky.X = 100; // Under the cat: the only enemy left, so the cat swings at it.
        check(Run(sim, 1.2, kind: BattleEventKind.SulkPuffed) && sulky.Hp == hp && sulky.Puff > 1, "The cat's own hit takes no HP from a sulky slime and puffs it up");
        sulky.Puff = spec.MaxGrow;
        sulky.X = 140;
        double before = sim.CatHp;
        for (int i = 0; i < 400 && sim.CatHp == before; i++) sim.Step(dt, 100, false); // Its next blow.
        check(Math.Abs(before - sim.CatHp - 10 * spec.MaxGrow) < 0.01, $"Its hits grow with it ({before - sim.CatHp:0.#} for 10 × {spec.MaxGrow})");

        // Hit on and on: fully puffed, a few more blows and it stamps off in tears (the cat's defeat, no loot, not a soothing).
        sim = Field(new[] { Sulk(damage: 10) }, new[] { 1 }, weapon: paw);
        sulky = sim.Slimes.Single();
        sulky.X = 100;
        int down = sim.Defeated;
        before = sim.CatHp;
        bool burst = Run(sim, 30, kind: BattleEventKind.SulkTantrum);
        check(burst && sulky.Tantrumed && sulky.State == SlimeState.Defeated && sim.Defeated == down + 1 && sim.SulksCalmed == 0 && sim.CatHp < before,
            $"Fully puffed and hit {spec.Tantrum} more times it stamps off in tears (cat {before - sim.CatHp:0.#} HP)");
        check(sulky.Puff >= spec.MaxGrow - 1e-9 && sulky.Tantrum == spec.Tantrum, "It burst only once fully puffed, after its tantrum count");
        // 2026-11-05: it stays to cry and run off (no puff of smoke), then it is gone.
        sim = Field(new[] { Sulk(damage: 10) }, new[] { 1 }, weapon: paw);
        sulky = sim.Slimes.Single();
        sulky.X = 100;
        for (int i = 0; i < 30 / dt && !sulky.Tantrumed; i++) { sim.Step(dt, 100, false); sim.ClearEvents(); }
        check(sulky.Tantrumed && !sim.Effects.Any(e => e.Kind == BattleEffectKind.Poof), "A tantrum ends in tears, no puff of smoke");
        Run(sim, BattleSim.TantrumLeave - 0.2);
        check(sim.Slimes.Contains(sulky), $"It is still there crying and running off after {BattleSim.TantrumLeave - 0.2:0.0} s");
        Run(sim, 0.3);
        check(!sim.Slimes.Contains(sulky), $"Gone after {BattleSim.TantrumLeave:0.0} s");

        // The cat holds back from the one being stroked.
        sim = Field(new[] { Sulk() }, new[] { 1 });
        sulky = sim.Slimes.Single();
        sulky.X = 100;
        int beat = 0;
        bool puffed = Run(sim, 1.5, kind: BattleEventKind.SulkPuffed, each: () => sim.SetPointer((sulky.X + (beat++ % 2 == 0 ? -3 : 3), 10)));
        check(!puffed && sulky.Puff == 1 && sulky.Calm > 0, "While the user strokes it the cat does not hit it");

        // The thrown cat flying or bouncing leaves the cursor free; the calm waits meanwhile.
        sim = Field(new[] { Sulk() }, new[] { 1 });
        sulky = sim.Slimes.Single();
        var flying = new CatPose(100, 80, 300, 0, false, true);
        for (int i = 0; i < 30; i++) { sim.SetPointer((sulky.X + (i % 2 == 0 ? -3 : 3), 10)); sim.Step(dt, 100, true, false, flying); }
        double soothed = sulky.Calm;
        check(soothed > 0, "Strokes count while the thrown cat is in the air");
        sim.SetPointer(null);
        for (int i = 0; i < 240; i++) sim.Step(dt, 100, true, false, flying);
        check(sulky.Calm == soothed, "The calm does not fade while the cat is thrown or bouncing");

        // Two sulky ones left: the cat goes for the one not being stroked; soothing one cheers up the other.
        sim = Field(new[] { Sulk() }, new[] { 2 }, weapon: paw);
        var a = sim.Slimes[0];
        var b = sim.Slimes[1];
        a.X = 300; b.X = 420;
        int beat2 = 0;
        Run(sim, 0.3, each: () => sim.SetPointer((a.X + (beat2++ % 2 == 0 ? -3 : 3), 10)));
        check(sim.TargetId == b.Id, "With only sulky ones left the cat goes for the one not being stroked");
        for (int i = 0; i < 1200 && !a.Calmed; i++) { sim.SetPointer((a.X + (beat2++ % 2 == 0 ? -3 : 3), 10)); sim.Step(dt, 100, false); }
        bool done = a.Calmed;
        check(done && a.Calmed && b.Calm >= spec.SpreadCalm - 0.05, $"Soothing one cheers up a sulky one nearby ({b.Calm:0.##})");

        // The yarn's sweep (a splash) does not touch a sulky slime next to its target.
        var yarn = Shipped.Weapon("yarn") with { Damage = 1, StunChance = 0 };
        sim = Field(new[] { Kind("slime"), Sulk() }, new[] { 1, 1 }, weapon: yarn);
        var plainOne = sim.Slimes.Single(s => s.Type.Id == "slime");
        sulky = sim.Slimes.Single(s => s.Type.Id == "sulky");
        plainOne.X = 140; sulky.X = 170;
        check(!Run(sim, 2, kind: BattleEventKind.SulkPuffed) && sulky.Puff == 1 && plainOne.Hp < plainOne.MaxHp, "The yarn's sweep leaves a sulky slime be");

        // Strokes soothe it.
        sim = Field(new[] { Sulk() }, new[] { 1 });
        sulky = sim.Slimes.Single();
        sim.SetPointer((sulky.X, 10));
        check(!Run(sim, 2, kind: BattleEventKind.SulkPetted) && sulky.Calm == 0, "A cursor resting on it does not soothe it");
        int frame = 0;
        void Stroke() => sim.SetPointer((sulky.X + (frame++ % 2 == 0 ? -3 : 3), 10));
        Run(sim, 0.5, each: Stroke);
        check(sulky.Calm > 0 && sulky.State != SlimeState.Defeated, "Strokes back and forth over it start to soothe it");
        double calm = sulky.Calm;
        sim.SetPointer(null);
        Run(sim, spec.Idle + 2);
        check(sulky.Calm < calm, "Left alone it sulks again");
        sim.SetPointer((sulky.X - 1000, 10));
        sim.Step(dt, 100, false);
        sim.SetPointer((sulky.X, 10));
        sim.Step(dt, 100, false);
        check(sulky.Calm <= calm, "A jump of the cursor onto it is not a stroke");
        for (int i = 0; i < 30; i++) { Stroke(); sim.Step(dt, 100, true, true, new CatPose(100, 80, 0, 0, true, false)); sim.ClearEvents(); }
        check(sulky.Calm <= calm, "With the cat in the user's hand strokes do nothing");
        int defeated = sim.Defeated;
        bool calmed = Run(sim, 20, kind: BattleEventKind.SulkCalmed, each: Stroke);
        check(calmed && sulky.Calmed && sulky.State == SlimeState.Defeated && sim.Defeated == defeated + 1 && sim.SulksCalmed == 1,
            "Soothed, it leaves, counted as the cat's");

        // An elite takes longer.
        double Seconds(bool elite)
        {
            var s = Field(new[] { Sulk() }, new[] { 1 }, elite);
            var one = s.Slimes.Single();
            int n = 0, f = 0;
            while (!one.Calmed && n++ < 6000) { s.SetPointer((one.X + (f++ % 2 == 0 ? -3 : 3), 10)); s.Step(dt, 100, false); s.ClearEvents(); }
            return n * dt;
        }
        double plain = Seconds(false), elite = Seconds(true);
        check(elite > plain * 1.3, $"An elite takes longer to soothe ({plain:0.#} s → {elite:0.#} s)");

        // The cat leaves it alone while there is another to hit.
        sim = Field(new[] { Sulk(), Kind("slime") }, new[] { 1, 1 });
        sim.Slimes.Single(s => s.Type.Id == "sulky").X = 200;
        var other = sim.Slimes.Single(s => s.Type.Id == "slime");
        other.X = 600;
        sim.Step(dt, 100, false);
        check(sim.TargetId == other.Id, "The cat goes for another enemy rather than a sulky one");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        const string slime = "{\"id\":\"slime\",\"nameVi\":\"S\",\"nameEn\":\"S\"}";
        static string Enemy(string id, string trick) => $"{{\"id\":\"{id}\",\"nameVi\":\"T\",\"nameEn\":\"T\",{trick}}}";
        bool Rejects(string enemies, string waves)
        {
            try { BattleData.Parse("{\"enemies\":[" + slime + "," + enemies + "],\"waves\":[" + waves + "]}"); return false; }
            catch (InvalidDataException) { return true; }
        }
        string one = "{\"groups\":[{\"enemy\":\"a\",\"count\":1}]}";
        check(!Rejects(Enemy("a", "\"ghost\":{}"), one), "A ghost kind within its limits is accepted");
        check(Rejects(Enemy("a", "\"ghost\":{},\"bubble\":{}"), one), "A kind with two cursor tricks is refused");
        check(Rejects(Enemy("a", "\"boss\":true,\"sulk\":{}"), one) && Rejects(Enemy("a", "\"blocker\":true,\"mirage\":{}"), one), "A boss or a blocker with a cursor trick is refused");
        check(Rejects(Enemy("a", "\"bubble\":{\"pops\":0}"), one) && Rejects(Enemy("a", "\"mirage\":{\"copies\":9}"), one) && Rejects(Enemy("a", "\"sulk\":{\"maxGrow\":5}"), one),
            "Numbers out of range are refused");
        string three = Enemy("a", "\"ghost\":{}") + "," + Enemy("b", "\"bubble\":{}") + "," + Enemy("c", "\"sulk\":{}");
        check(!Rejects(three, "{\"groups\":[{\"enemy\":\"a\",\"count\":1},{\"enemy\":\"b\",\"count\":1}]}"), $"{BattleData.MaxMouseKinds} kinds the cursor beats in one battle are accepted");
        check(Rejects(three, "{\"groups\":[{\"enemy\":\"a\",\"count\":1},{\"enemy\":\"b\",\"count\":1}]},{\"groups\":[{\"enemy\":\"c\",\"count\":1}]}"),
            "A third kind the cursor beats in the same battle is refused");

        var s = Shipped;
        check(s.Enemy("phantom") is { Ghost: not null, BinSpit: false, Elite: not null } && s.Enemy("bubble") is { Bubble: not null, BinSpit: false, Elite: not null }
              && s.Enemy("mirage") is { Mirage: not null, BinSpit: false, Elite: not null } && s.Enemy("sulky") is { Sulk: not null, BinSpit: false, Elite: not null },
            "The four shipped kinds, each with an elite, never spat by a bin");
        var tricks = new[] { "phantom", "bubble", "mirage", "sulky" };
        bool Uses(WaveGroup g) => tricks.Contains(g.Enemy) || g.Pick?.Keys.Any(tricks.Contains) == true || g.Squad?.Any(m => tricks.Contains(m.Enemy)) == true;
        var all = s.WavePlan.Concat(s.Campaign?.Chapters.Take(1).SelectMany(c => c.Stages).SelectMany(st => st.Waves) ?? Enumerable.Empty<WaveSpec>());
        check(!all.SelectMany(w => w.Groups).Any(Uses), "No built-in battle uses them yet outside chapter 2 (1.5 M21 brings it in)");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m13.json"))) dir = dir.Parent;
        foreach (var file in new[] { "battle-m13.json", "battle-m13b.json" })
        {
            var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", file));
            check(load is { Custom: true, Error: null } && load.Settings.WavePlan.SelectMany(w => w.Groups).Count(Uses) >= 4,
                $"docs/examples/{file} loads as custom data with two of the kinds ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
        }
    }
}
