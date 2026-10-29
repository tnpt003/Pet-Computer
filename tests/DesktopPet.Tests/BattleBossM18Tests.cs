using DesktopPet;

// 1.5 M18, B2 the Slime Mother (PetBattle.Brood.cs) and B3 the Mutant Slime King (PetBattle.Mutant.cs), with
// stand-in bosses carrying the shipped brood, crown link and rain numbers. The mother: her clutch is announced,
// then laid on the cat's side of her within her summon cap, never while she is stunned; it merges into a giant
// that walks back to her without attacking and feeds her its HP until it is gone (not the cat's kill); beaten on
// the way it gives her nothing; with her gone it fights. Merging slimes she did not lay never nurse. The king:
// its rallies tie a tether, then a rhythm, to the elite; a battle with no room for a kind skips it; its rain
// lands around the cat after its rings, hurts a cat on the floor, never a lifted one, and waits while its shield
// is up. The data limits, the shipped bosses (kept out of every battle for now) and the try-out files.
internal static class BattleBossM18Tests
{
    const double dt = 1.0 / 60;

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static readonly EnemyType ShippedMother = Shipped.Enemy("mother");
    private static readonly EnemyType ShippedKing = Shipped.Enemy("mutant_king");

    private static EnemyType Plain(string id = "slime", double speed = 40) => new() { Id = id, MaxHp = 300, Speed = speed, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3 };
    private static EnemyType Mother(int cap = 6, double speed = 0) => new()
    {
        Id = "mother", MaxHp = 1000, Speed = speed, Damage = 0, Range = 110, Windup = 1.3, AttackCooldown = 2.8, Recover = 0.7, Scale = 2,
        Boss = true, Slam = true, PushFactor = 0.35, StunFactor = 0.4, SummonCap = cap, Brood = ShippedMother.Brood
    };
    private static EnemyType King(LinkSpec[]? crown = null) => new()
    {
        Id = "king", MaxHp = 1000, Speed = 0, Damage = 0, Range = 110, Windup = 1.2, AttackCooldown = 2.6, Recover = 0.7, Scale = 2,
        Boss = true, Slam = true, PushFactor = 0.35, StunFactor = 0.4, SummonCap = 5, RallyEvery = 2, RallyCharge = 0.3, RallyHp = 0.5, RallyDamage = 1.25,
        RetreatLeap = true, CrownLinks = crown ?? ShippedKing.CrownLinks, Rain = ShippedKing.Rain,
        Phases = new[] { new BossPhase { At = 0.75, Shield = true, Summons = new[] { new WaveGroup { Enemy = "slime", Count = 4 } } } }
    };

    private static BattleSettings Settings(EnemyType boss, params EnemyType[] more) => Settings(boss, boss.Id, 1, more);
    private static BattleSettings Settings(EnemyType boss, string wave, int count, params EnemyType[] more) => new()
    {
        Weapons = new[] { Shipped.Weapon("sword") with { Range = 60, StunChance = 0, Damage = 0 } }, FirstWaveDelay = 0, SpawnInterval = 0.05,
        CatMaxHp = 1000, MaxAlive = 8, RageHitGain = 0,
        Enemies = new[] { boss, Plain(), Shipped.Enemy("fuser"), Shipped.Enemy("giant") }.Concat(more).ToArray(),
        Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = wave, Count = count } } } }
    };

    // The boss in, standing at bossX, the cat at catX.
    private static (BattleSim Sim, Slime Boss) Field(BattleSettings settings, double bossX = 1200, double catX = 100)
    {
        var sim = new BattleSim(settings, 0, 1600, seed: 4, startHp: 900);
        for (int i = 0; i < 600 && !sim.Slimes.Any(s => s.IsBoss && s.Targetable); i++) sim.Step(dt, catX, false);
        var boss = sim.Slimes.Single(s => s.IsBoss);
        boss.X = bossX;
        sim.ClearEvents();
        return (sim, boss);
    }

    // Steps `seconds` with the cat at catX (lifted: in the hand, high up), until an event of `kind` (then true).
    private static bool Run(BattleSim sim, double seconds, double catX = 100, BattleEventKind? kind = null, bool lifted = false, List<BattleEvent>? seen = null)
    {
        for (int i = 0; i < Math.Round(seconds / dt); i++)
        {
            sim.Step(dt, catX, lifted, lifted, lifted ? new CatPose(catX, 200, 0, 0, true, false) : null);
            seen?.AddRange(sim.Events);
            bool hit = kind is BattleEventKind k && sim.Events.Any(e => e.Kind == k);
            sim.ClearEvents();
            if (hit) return true;
            if (sim.Over) break;
        }
        return false;
    }

    public static void Run(Action<bool, string> check)
    {
        BroodChecks(check);
        NurseChecks(check);
        CrownChecks(check);
        RainChecks(check);
        DataChecks(check);
    }

    private static void BroodChecks(Action<bool, string> check)
    {
        var brood = ShippedMother.Brood!;
        var (sim, mother) = Field(Settings(Mother()));
        var seen = new List<BattleEvent>();
        bool warned = Run(sim, brood.Every, kind: BattleEventKind.BroodWarned, seen: seen);
        check(warned && !sim.Slimes.Any(s => s.Type.Id == "fuser") && mother.BroodSpots is { Count: 3 }, "The mother announces her clutch with rings before it comes");
        bool laid = Run(sim, brood.Warning + 0.1, kind: BattleEventKind.BroodLaid);
        var clutch = sim.Slimes.Where(s => s.Type.Id == "fuser").ToArray();
        check(laid && clutch.Length == brood.Count && clutch.All(s => s.SummonedBy == mother.Id && s.X < mother.X), $"Then {brood.Count} fusing slimes come, hers, on the cat's side of her");
        check(clutch.Max(s => s.X) - clutch.Min(s => s.X) <= Shipped.Enemy("fuser").Link!.Range * 1.5, "Side by side, close enough to huddle");
        check(sim.Clutches == 1 && sim.Defeated == 0, "One clutch counted, nobody defeated");

        // Stunned, her clock waits.
        (sim, mother) = Field(Settings(Mother()));
        mother.BroodClock = 0.2;
        mother.Set(SlimeState.Stunned);
        mother.StunRemaining = 3;
        check(!Run(sim, 2.5, kind: BattleEventKind.BroodWarned), "No clutch while she is stunned");
        check(Run(sim, 2, kind: BattleEventKind.BroodWarned), "Her clutch comes once she is up again");

        // Her summon cap: no room, no clutch (the clock tries again soon).
        (sim, mother) = Field(Settings(Mother(cap: 3)));
        mother.BroodClock = 0;
        Run(sim, 1.5);
        int first = sim.Slimes.Count(s => s.Type.Id == "fuser" && s.Targetable);
        foreach (var s in sim.Slimes.Where(s => s.Type.Id == "fuser")) s.Set(SlimeState.Stunned); // Kept apart, never merging.
        foreach (var s in sim.Slimes.Where(s => s.Type.Id == "fuser")) s.StunRemaining = 60;
        mother.BroodClock = 0;
        Run(sim, 3);
        check(first == 3 && sim.Slimes.Count(s => s.Type.Id == "fuser" && s.State != SlimeState.Defeated) == 3 && sim.Clutches == 1, "With her summon cap full she lays no more");
    }

    private static void NurseChecks(Action<bool, string> check)
    {
        var feed = ShippedMother.Brood!.FeedRate;
        // The clutch merges, the giant walks back and feeds her.
        var (sim, mother) = Field(Settings(Mother()), bossX: 1200, catX: 300);
        mother.Hp = 500;
        mother.BroodClock = 0;
        bool heading = Run(sim, 6, catX: 300, kind: BattleEventKind.NurseHeading);
        var giant = sim.Slimes.SingleOrDefault(s => s.Type.Id == "giant");
        check(heading && giant is { Nursing: true } && giant.SummonedBy == mother.Id, "Her clutch merged: the giant is hers and heads back to her");
        mother.BroodClock = 100; // No second clutch in its way.
        double giantHp = giant?.Hp ?? 0, startX = giant?.X ?? 0;
        var seen = new List<BattleEvent>();
        Run(sim, 1, catX: 300, seen: seen);
        check(giant != null && Math.Abs(giant.X - mother.X) < Math.Abs(startX - mother.X) + 0.01 && giant.State != SlimeState.Windup, "It walks to her, not at the cat");
        bool fed = Run(sim, giantHp / feed + 12, catX: 300, kind: BattleEventKind.NurseFed, seen: seen);
        check(fed && giant is { State: SlimeState.Defeated, Escaped: true } && sim.Nursed == 1 && sim.Defeated == 0, "Next to her it feeds her until it is gone (not the cat's kill)");
        check(Math.Abs(mother.Hp - (500 + giantHp)) < 1 && Math.Abs(sim.HpFed - giantHp) < 1, $"She gets its HP back ({mother.Hp - 500:0} of {giantHp:0})");
        check(seen.Any(e => e.Kind == BattleEventKind.NurseFed && Math.Abs(e.Value - Math.Round(giantHp)) <= 1), "Its feed is told once, with the HP given");
        check(!seen.Any(e => e.Kind == BattleEventKind.CatHurt), "It never attacked the cat");

        // Beaten on the way: she gets nothing.
        (sim, mother) = Field(Settings(Mother()), bossX: 1200, catX: 300);
        mother.Hp = 500;
        mother.BroodClock = 0;
        Run(sim, 6, catX: 300, kind: BattleEventKind.NurseHeading);
        giant = sim.Slimes.Single(s => s.Type.Id == "giant");
        sim.Damage(giant, giant.Hp + 1, DamageSource.App);
        Run(sim, 4, catX: 300);
        check(mother.Hp == 500 && sim.Nursed == 0 && sim.Defeated == 1, "Beaten on its way it gives her nothing (and is the cat's kill)");

        // With her gone it fights as any other.
        (sim, mother) = Field(Settings(Mother()), bossX: 1200, catX: 300);
        mother.BroodClock = 0;
        Run(sim, 6, catX: 300, kind: BattleEventKind.NurseHeading);
        giant = sim.Slimes.Single(s => s.Type.Id == "giant");
        sim.Damage(mother, mother.Hp + 1, DamageSource.App);
        Run(sim, 0.5, catX: 300);
        check(!giant.Nursing, "Her gone, it stops nursing");

        // Fusing slimes from a wave merge into a giant that fights.
        var wave = Settings(Plain("dummy", 0), "fuser", 3);
        var free = new BattleSim(wave, 0, 1600, seed: 4, startHp: 900);
        bool merged = false;
        for (int i = 0; i < 1200 && !merged; i++)
        {
            free.Step(dt, 100, false);
            merged = free.Events.Any(e => e.Kind == BattleEventKind.Merged);
            free.ClearEvents();
        }
        check(merged && free.Slimes.Single(s => s.Type.Id == "giant") is { Nursing: false, SummonedBy: 0 }, "Merging slimes nobody laid make a giant that fights");
    }

    private static void CrownChecks(Action<bool, string> check)
    {
        var (sim, king) = Field(Settings(King()), bossX: 1200, catX: 100);
        sim.Damage(king, king.MaxHp * 0.3, DamageSource.App);
        var seen = new List<BattleEvent>();
        bool tied = Run(sim, 8, kind: BattleEventKind.CrownLinked, seen: seen);
        var elite = sim.Slimes.SingleOrDefault(s => s.Rallied);
        var holder = sim.Slimes.SingleOrDefault(s => s.Link?.Kind == EnemyLinks.Tether);
        check(tied && seen.Any(e => e.Kind == BattleEventKind.BossRallied) && elite != null && holder != null && holder != elite && holder.LinkedTo == elite.Id
              && holder.SummonedBy == king.Id && sim.LinkKinds.Contains(EnemyLinks.Tether), "Its first rally ties a chain rope from another helper to the elite");
        check(sim.TetherHolder(elite!) == holder, "The elite's rope is a tether like a chain slime's");
        bool second = Run(sim, 6, kind: BattleEventKind.CrownLinked);
        var drummer = sim.Slimes.SingleOrDefault(s => s.Link?.Kind == EnemyLinks.Rhythm);
        check(second && drummer is { Rallied: true } && sim.LinkKinds.Contains(EnemyLinks.Rhythm) && sim.CrownLinksTied == 2, "Its next rally hands the next elite a drum");
        bool beat = Run(sim, 4, kind: BattleEventKind.LinkBeat);
        check(beat, "The drum beats");

        // A battle with two kinds of link already: the crown ties none.
        var more = new[]
        {
            new EnemyType { Id = "merger", MaxHp = 50, Speed = 0, Damage = 0, Range = 50, Link = new LinkSpec { Kind = EnemyLinks.Merge, Into = "slime" } },
            new EnemyType { Id = "flag", MaxHp = 50, Speed = 0, Damage = 0, Range = 50, Support = true, Link = new LinkSpec { Kind = EnemyLinks.Aura } }
        };
        (sim, king) = Field(Settings(King(), more), bossX: 1200, catX: 100);
        sim.AddSlime(40, more[0]);
        sim.AddSlime(60, more[1]);
        sim.Damage(king, king.MaxHp * 0.3, DamageSource.App);
        seen.Clear();
        Run(sim, 10, seen: seen);
        check(seen.Any(e => e.Kind == BattleEventKind.BossRallied) && !seen.Any(e => e.Kind == BattleEventKind.CrownLinked) && sim.LinkKinds.Count == 2,
            "With two kinds of link in the battle already, its rallies tie none");

        // Only a rhythm in its crown: every rally hands out a drum.
        (sim, king) = Field(Settings(King(new[] { ShippedKing.CrownLinks!.Single(l => l.Kind == EnemyLinks.Rhythm) })), bossX: 1200, catX: 100);
        sim.Damage(king, king.MaxHp * 0.3, DamageSource.App);
        Run(sim, 8, kind: BattleEventKind.CrownLinked);
        Run(sim, 6, kind: BattleEventKind.CrownLinked);
        check(sim.Slimes.Count(s => s.Link?.Kind == EnemyLinks.Rhythm) == 2 && !sim.LinkKinds.Contains(EnemyLinks.Tether), "A crown of one kind hands out that kind each time");
    }

    private static void RainChecks(Action<bool, string> check)
    {
        var rain = ShippedKing.Rain!;
        var (sim, king) = Field(Settings(King()), bossX: 1200, catX: 400);
        king.RainClock = 0.05;
        bool called = Run(sim, 0.5, catX: 400, kind: BattleEventKind.RainCalled);
        var drops = sim.Goos.Where(g => g.Rain).ToArray();
        check(called && drops.Length == rain.Drops && drops[0].X == 400 && drops.Select(g => g.X).Distinct().Count() == rain.Drops
              && drops.All(g => Math.Abs(g.X - 400) <= rain.Spacing * ((rain.Drops + 1) / 2) + 0.01), $"Its rain: {rain.Drops} drops around the cat, the first on it");
        double hp = sim.CatHp;
        var seen = new List<BattleEvent>();
        Run(sim, rain.Warning + 0.2, catX: 400, seen: seen);
        check(sim.CatHp < hp && sim.RainHits == 1 && seen.Count(e => e.Kind == BattleEventKind.GooLanded) == rain.Drops, "After its rings the drops land; the one on the cat hurts it");
        check(rain.PuddleSeconds <= 0 || sim.Puddles.Count > 0, "Its drops leave puddles");
        check(king.RainClock > rain.Every - rain.Warning - 1, "The next rain waits its turn");

        // Lifted, the cat is not hit.
        (sim, king) = Field(Settings(King()), bossX: 1200, catX: 400);
        king.RainClock = 0.05;
        Run(sim, 0.2, catX: 400);
        hp = sim.CatHp;
        Run(sim, rain.Warning + 0.2, catX: 400, lifted: true);
        check(sim.CatHp == hp && sim.RainHits == 0, "Lifting the cat dodges the rain");

        // Its shield up, the rain waits.
        (sim, king) = Field(Settings(King()), bossX: 1200, catX: 400);
        sim.Damage(king, king.MaxHp * 0.3, DamageSource.App);
        king.RainClock = 0.05;
        check(king.Shielded && !Run(sim, 2, catX: 400, kind: BattleEventKind.RainCalled), "No rain while its shield is up");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        const string slime = "{\"id\":\"slime\",\"nameVi\":\"S\",\"nameEn\":\"S\"}";
        const string fuser = "{\"id\":\"f\",\"nameVi\":\"F\",\"nameEn\":\"F\",\"link\":{\"kind\":\"merge\",\"into\":\"slime\"}}";
        static string Enemy(string id, string extra) => $"{{\"id\":\"{id}\",\"nameVi\":\"T\",\"nameEn\":\"T\",{extra}}}";
        bool Rejects(string enemies)
        {
            try { BattleData.Parse("{\"enemies\":[" + slime + "," + fuser + "," + enemies + "],\"waves\":[{\"groups\":[{\"enemy\":\"a\",\"count\":1}]}]}"); return false; }
            catch (InvalidDataException) { return true; }
        }
        const string boss = "\"boss\":true,";
        const string shield = "\"rallyEvery\":5,\"phases\":[{\"at\":0.5,\"shield\":true,\"summon\":\"slime\",\"count\":2}],";
        check(!Rejects(Enemy("a", boss + "\"brood\":{\"enemy\":\"f\"}")) && !Rejects(Enemy("a", boss + shield + "\"crownLinks\":[{\"kind\":\"tether\"},{\"kind\":\"rhythm\"}],\"rain\":{}")),
            "A brooding boss, and a crowned boss with a rain, are accepted");
        check(Rejects(Enemy("a", "\"brood\":{\"enemy\":\"f\"}")) && Rejects(Enemy("a", "\"rain\":{}")) && Rejects(Enemy("a", boss + "\"brood\":{\"enemy\":\"slime\"}"))
              && Rejects(Enemy("a", boss + "\"brood\":{\"enemy\":\"f\",\"count\":0}")),
            "A brood or rain on a plain enemy, a brood of a kind that does not merge, or of none, are refused");
        check(Rejects(Enemy("a", boss + "\"crownLinks\":[{\"kind\":\"tether\"}]")) && Rejects(Enemy("a", boss + shield + "\"crownLinks\":[{\"kind\":\"merge\"}]"))
              && Rejects(Enemy("a", boss + shield + "\"crownLinks\":[]")) && Rejects(Enemy("a", boss + "\"rain\":{\"drops\":0}")) && Rejects(Enemy("a", boss + "\"rain\":{\"warning\":0}")),
            "Crown links without a rally, of another kind or none, a rain of no drops or no warning, are refused");
        // Its two crown kinds and an aura enemy in one battle: three kinds of link.
        string crowned = Enemy("a", boss + shield + "\"crownLinks\":[{\"kind\":\"tether\"},{\"kind\":\"rhythm\"}]"), flag = Enemy("b", "\"link\":{\"kind\":\"aura\"}");
        bool three;
        try { BattleData.Parse("{\"enemies\":[" + slime + "," + crowned + "," + flag + "],\"waves\":[{\"groups\":[{\"enemy\":\"b\",\"count\":1},{\"enemy\":\"a\",\"count\":1}]}]}"); three = false; }
        catch (InvalidDataException) { three = true; }
        check(three, "A crowned boss's links count against a battle's two kinds");

        var s = Shipped;
        var mother = s.Enemy("mother");
        check(mother is { Boss: true, BinSpit: false, Brood: { Enemy: "fuser", Count: 3 } } && s.Enemy("fuser").Link?.Into == "giant" && mother.Phases?.Any(p => p.Enrage) == true,
            "The shipped Slime Mother: clutches of three fusing slimes, angry at the end");
        var king = s.Enemy("mutant_king");
        var kp = king.Phases ?? Array.Empty<BossPhase>();
        check(king is { Boss: true, BinSpit: false, RallyEvery: > 0, Rain: not null } && king.CrownLinks!.Select(l => l.Kind).SequenceEqual(new[] { EnemyLinks.Tether, EnemyLinks.Rhythm })
              && kp.Length == 3 && kp.All(p => p.Shield && s.PhaseCalls(p).Contains("spitter") && s.PhaseCalls(p).Count() <= king.SummonCap),
            "The shipped Mutant King: shield phases calling spitters, a chain and a drum in turn, a goo rain");
        var kinds = new[] { "mother", "mutant_king" };
        bool Uses(WaveGroup g) => kinds.Contains(g.Enemy) || g.Pick?.Keys.Any(kinds.Contains) == true || g.Squad?.Any(m => kinds.Contains(m.Enemy)) == true;
        var all = s.WavePlan.Concat(s.Campaign?.Chapters.Take(1).SelectMany(ch => ch.Stages).SelectMany(st => st.Waves) ?? Enumerable.Empty<WaveSpec>())
            .Concat(s.Expedition is { } x ? x.Fights.Concat(x.Elites).Concat(x.Boss is { } b ? new[] { b } : Array.Empty<StageSpec>()).SelectMany(st => st.Waves) : Enumerable.Empty<WaveSpec>());
        check(!all.SelectMany(w => w.Groups).Any(Uses) && !kinds.Contains(s.Endless?.Boss) && !kinds.Contains(s.Endless?.LateBoss), "No built-in battle uses them yet outside chapter 2 (1.5 M21 brings it in)");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m18.json"))) dir = dir.Parent;
        foreach (var (file, kind) in new[] { ("battle-m18.json", "mother"), ("battle-m18b.json", "mutant_king") })
        {
            var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", file));
            check(load is { Custom: true, Error: null } && load.Settings.WavePlan.SelectMany(w => w.Groups).Any(g => g.Enemy == kind),
                $"docs/examples/{file} loads as custom data with the {kind} ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
        }
    }
}
