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
    private static BattleSim Field(EnemyType[] kinds, int[] counts, bool elite = false, double catHp = 1000)
    {
        var arms = Shipped.Weapon("sword") with { Range = 5, StunChance = 0, Damage = 0 };
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
        for (int i = 0; i < sim.Slimes.Count; i++) { sim.Slimes[i].X = 1000 - 160 * i; sim.Slimes[i].GhostClock = sim.Slimes[i].Type.Ghost?.Visible ?? 0; }
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
        check(sim.Snip(bubble.X - 40, 20, bubble.X + 40, 20) && !bubble.Bubbled && bubble.Hp == hp, "A Scissors stroke through it pops the bubble (and hurts nothing)");

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
        var sim = Field(new[] { Sulk(damage: 10) }, new[] { 1 });
        var sulky = sim.Slimes.Single();
        double hp = sulky.Hp;
        Bomb(sim, sulky);
        check(Run(sim, 0.5, kind: BattleEventKind.SulkPuffed) && sulky.Hp == hp && Math.Abs(sulky.Puff - (1 + spec.Grow)) < 1e-9, "A hit takes no HP from a sulky slime and puffs it up");
        for (int i = 0; i < 20; i++) { Bomb(sim, sulky); Run(sim, 0.4); }
        check(Math.Abs(sulky.Puff - spec.MaxGrow) < 1e-9, $"It puffs up to {spec.MaxGrow} × at most");
        sulky.X = 140;
        double before = sim.CatHp;
        Run(sim, 1.5);
        check(Math.Abs(before - sim.CatHp - 10 * spec.MaxGrow) < 0.01, $"Its hits grow with it ({before - sim.CatHp:0.#} for 10 × {spec.MaxGrow})");

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
        Run(sim, 0.5, held: true, each: Stroke);
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
        var all = s.WavePlan.Concat(s.Campaign?.Chapters.SelectMany(c => c.Stages).SelectMany(st => st.Waves) ?? Enumerable.Empty<WaveSpec>());
        check(!all.SelectMany(w => w.Groups).Any(Uses) && !s.FormationList.SelectMany(f => f.Members).Any(tricks.Contains), "No built-in battle uses them yet");

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
