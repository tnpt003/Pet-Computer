using DesktopPet;

// 1.5 M17, B1 the Zip Slime (PetBattle.Zip.cs), with a stand-in boss carrying the shipped roll and rope numbers.
// The roll: from afar it winds up and rolls over the cat (hurt and stunned once per roll), lifting the cat
// dodges it, never up close or while its shield is up. The roped phase: its formation comes tied to it, it takes
// no damage while a rope is left, the Scissors and the cat (held or thrown, fast enough) cut ropes, a slow drag
// and a paused battle do not, defeating a helper ends its rope; the last rope gone, the shield drops while the
// helpers fight on. The pack phase: it stays where it is, its still shards around it, the shield drops once they
// are broken. All three phases run in order. The data limits, the shipped boss (kept out of every battle for
// now) and the try-out file.
internal static class BattleZipTests
{
    const double dt = 1.0 / 60;

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static readonly EnemyType ShippedZip = Shipped.Enemy("zip");

    private static EnemyType Plain(string id = "slime") => new() { Id = id, MaxHp = 300, Speed = 40, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3 };
    private static EnemyType Shard() => new() { Id = "shard", MaxHp = 40, Speed = 0, Damage = 0, Range = 40, Scale = 0.6, Still = true };
    private static readonly FormationSpec Duo = new()
    {
        Id = "duo", NameVi = "Đôi", NameEn = "Duo",
        Parts = new[] { new WaveGroup { Squad = new[] { new WaveGroup { Enemy = "slime", Count = 2 } } } }
    };
    private static BossPhase[] AllPhases => new[]
    {
        new BossPhase { At = 0.7, Shield = true, Roped = true, Formation = "duo" },
        new BossPhase { At = 0.3, Shield = true, Pack = true, Enrage = true, Summons = new[] { new WaveGroup { Enemy = "shard", Count = 3 } } }
    };
    private static EnemyType Zip(double damage = 20, BossPhase[]? phases = null) => new()
    {
        Id = "zip", MaxHp = 1000, Speed = 20, Damage = damage, Range = 110, Windup = 1.2, AttackCooldown = 2.6, Recover = 0.7, Scale = 2,
        Boss = true, Slam = true, PushFactor = 0.35, StunFactor = 0.4, EnragedSpeed = 30, EnragedWindup = 0.8, SummonCap = 10,
        RopeCutSpeed = ShippedZip.RopeCutSpeed, RopeCutStun = ShippedZip.RopeCutStun, Roll = ShippedZip.Roll, Phases = phases ?? AllPhases
    };

    private static BattleSettings Settings(EnemyType boss) => new()
    {
        Weapons = new[] { Shipped.Weapon("sword") with { Range = 60, StunChance = 0, Damage = 0 } }, FirstWaveDelay = 0, SpawnInterval = 0.05,
        CatMaxHp = 1000, MaxAlive = 8, RageHitGain = 0, ScissorsCooldown = 0.1,
        Enemies = new[] { boss, Plain(), Shard() }, Formations = new[] { Duo },
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

    public static void Run(Action<bool, string> check)
    {
        RollChecks(check);
        RopeChecks(check);
        PackChecks(check);
        DataChecks(check);
    }

    private static void RollChecks(Action<bool, string> check)
    {
        var roll = ShippedZip.Roll!;
        // From afar: it winds up a roll, then rolls over the cat once.
        var (sim, zip) = Field(Zip());
        zip.RollCooldown = 0;
        double hp = sim.CatHp;
        check(Run(sim, 1, catX: 900, kind: BattleEventKind.RollAimed) && zip.Rolling && zip.State == SlimeState.Windup && zip.DashDirection == -1,
            "With the cat far off on the floor, it winds up a roll its way");
        check(Math.Abs(sim.Windup(zip) - roll.Windup) < 1e-9, "The roll's windup is its own");
        var seen = new HashSet<BattleEventKind>();
        Run(sim, roll.Windup + roll.Distance / roll.Speed + 0.5, catX: 900, seen: seen);
        check(seen.Contains(BattleEventKind.Rolled) && seen.Contains(BattleEventKind.RollHit) && seen.Contains(BattleEventKind.CatStunned) && sim.RollHits == 1 && sim.CatHp < hp,
            "It rolls over the cat: hurt and stunned, once");
        check(!zip.Rolling && zip.X < 900 && zip.RollCooldown > 0, "The roll runs past the cat, then it waits for the next");

        // Lifting the cat during the windup dodges it.
        (sim, zip) = Field(Zip());
        zip.RollCooldown = 0;
        Run(sim, 1, catX: 900, kind: BattleEventKind.RollAimed);
        hp = sim.CatHp;
        seen.Clear();
        Run(sim, roll.Windup + roll.Distance / roll.Speed + 0.3, catX: 900, held: true, seen: seen);
        check(seen.Contains(BattleEventKind.Rolled) && !seen.Contains(BattleEventKind.RollHit) && sim.CatHp == hp, "Lifting the cat out of its way dodges the roll");

        // Up close it slams instead, and a cat already in hand sees no roll.
        (sim, zip) = Field(Zip());
        zip.RollCooldown = 0;
        check(!Run(sim, 2, catX: 1200 - roll.MinRange + 40, kind: BattleEventKind.RollAimed), "Closer than its minRange it never rolls");
        (sim, zip) = Field(Zip());
        zip.RollCooldown = 0;
        check(!Run(sim, 2, catX: 900, kind: BattleEventKind.RollAimed, held: true), "Nor at a cat in the user's hand");

        // Never while its shield is up.
        (sim, zip) = Field(Zip());
        zip.RollCooldown = 0;
        sim.Damage(zip, zip.MaxHp * 0.35, DamageSource.App);
        check(zip.Shielded && !Run(sim, 4, catX: 700, kind: BattleEventKind.RollAimed), "No roll while the shield is up");
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
        var seen = new HashSet<BattleEventKind>();
        sim.Damage(zip, zip.MaxHp * 0.4, DamageSource.App);
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
        check(zip.Shielded && Math.Abs(zip.Hp - zip.MaxHp * 0.7) < 1e-6, "A hit stops at the roped phase's line and the shield goes up");
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
        check(sim.Snip(x, top, x, bottom) && helpers[0].RopedTo == 0 && helpers[0].State == SlimeState.Stunned && sim.RopesCut == 1 && sim.BossRopesCut == 1,
            "A Scissors stroke through a rope cuts it and dazes its helper");
        Run(sim, 0.3);
        check(zip.Shielded && sim.RopesLeft(zip) == 1, "One rope left: the shield holds");

        // The held cat dragged fast through the last one: the shield drops, the helpers fight on.
        (x, top, bottom) = Across(helpers[1], zip);
        check(Through(sim, new CatPose(x, top, 0, -fast, true, false), new CatPose(x, bottom, 0, -fast, true, false)) && sim.CatRopeCuts == 1,
            "The held cat dragged fast through a rope cuts it");
        seen.Clear();
        Run(sim, 0.3, seen: seen);
        check(!zip.Shielded && seen.Contains(BattleEventKind.BossRopesGone) && seen.Contains(BattleEventKind.BossShieldDown) && helpers.All(h => h.Targetable),
            "The last rope gone, the shield drops; its helpers fight on");
        before = zip.Hp;
        check(sim.Damage(zip, 30, DamageSource.App) == HitResult.Landed && zip.Hp < before, "It takes damage again");

        // The thrown cat flying through a rope cuts it too; defeating a helper ends its rope.
        (sim, zip, helpers, _) = Roped();
        (x, top, bottom) = Across(helpers[0], zip);
        check(Through(sim, new CatPose(x - 120, top, fast, -fast / 3, false, true), new CatPose(x + 120, bottom, fast, -fast / 3, false, true)) && helpers[0].RopedTo == 0,
            "The thrown cat flying through a rope cuts it");
        sim.Damage(helpers[1], helpers[1].Hp + 1, DamageSource.App);
        Run(sim, 0.3);
        check(!zip.Shielded && sim.RopesLeft(zip) == 0, "Defeating the last tied helper ends its rope and drops the shield");
    }

    private static void PackChecks(Action<bool, string> check)
    {
        // Straight to the pack phase (the roped one already done).
        var (sim, zip) = Field(Zip(damage: 0), bossX: 800);
        zip.PhasesDone = 1;
        var seen = new HashSet<BattleEventKind>();
        sim.Damage(zip, zip.MaxHp * 0.8, DamageSource.App);
        foreach (var e in sim.Events) seen.Add(e.Kind);
        sim.ClearEvents();
        double x = zip.X;
        Run(sim, 2, catX: 300, seen: seen);
        var shards = sim.Slimes.Where(s => s.Type.Still && s.Targetable).ToArray();
        check(zip.Shielded && zip.Packed && zip.Enraged && seen.Contains(BattleEventKind.BossPacked) && Math.Abs(zip.X - x) < 1,
            "The pack phase: it shields itself small where it stands (no run to the edge)");
        check(shards.Length == 3 && shards.Any(s => s.X < zip.X) && shards.Any(s => s.X > zip.X) && shards.All(s => s.SummonedBy == zip.Id && s.RopedTo == 0),
            "Its file shards come all round it");
        var spots = shards.Select(s => s.X).ToArray();
        Run(sim, 3, catX: 300);
        check(shards.Select(s => s.X).SequenceEqual(spots), "The shards never move");
        double hp = sim.CatHp;
        bool windup = false;
        for (int i = 0; i < 180; i++)
        {
            sim.Step(dt, shards.Min(s => s.X) - 30, false);
            windup |= shards.Any(s => s.State == SlimeState.Windup);
            sim.ClearEvents();
        }
        check(!windup && sim.CatHp == hp, "Nor attack, the cat right next to them");
        check(!Run(sim, 2, catX: 300, kind: BattleEventKind.RollAimed) && zip.Shielded, "Packed, it does not roll");
        foreach (var s in shards.Take(2)) sim.Damage(s, s.Hp + 1, DamageSource.App);
        Run(sim, 0.3);
        check(zip.Shielded, "A shard left: the shield holds");
        sim.Damage(shards[2], shards[2].Hp + 1, DamageSource.App);
        seen.Clear();
        Run(sim, 0.3, seen: seen);
        check(!zip.Shielded && !zip.Packed && seen.Contains(BattleEventKind.BossShieldDown), "Every shard broken: the shield drops and it unpacks");

        // Near the field's edge the shards all go on the open side.
        (sim, zip) = Field(Zip(damage: 0), bossX: 1600);
        zip.PhasesDone = 1;
        sim.Damage(zip, zip.MaxHp * 0.8, DamageSource.App);
        Run(sim, 1.5, catX: 300);
        shards = sim.Slimes.Where(s => s.Type.Still && s.Targetable).ToArray();
        check(shards.Length == 3 && shards.All(s => s.X < zip.X) && shards.Select(s => Math.Round(s.X)).Distinct().Count() == 3, "At the edge, all its shards stand on the open side, apart");

        // The three phases in order, to the end.
        (sim, zip) = Field(Zip(damage: 0));
        int phases = 0;
        for (int step = 0; step < 6000 && !sim.Over; step++)
        {
            if (!zip.Shielded) sim.Damage(zip, 20, DamageSource.App);
            foreach (var s in sim.Slimes.Where(s => !s.IsBoss && s.Targetable).ToArray())
                if (s.RopedTo != 0) { s.RopedTo = 0; } else if (s.Type.Still) sim.Damage(s, s.Hp + 1, DamageSource.App);
            sim.Step(dt, 100, false);
            phases += sim.Events.Count(e => e.Kind == BattleEventKind.BossShielded);
            sim.ClearEvents();
        }
        check(sim.BossDefeated && phases == 2, "Its phases run in order and it can be beaten");
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
        const string boss = "\"boss\":true,";
        check(!Rejects(Enemy("a", boss + "\"roll\":{},\"phases\":[{\"at\":0.6,\"shield\":true,\"roped\":true,\"formation\":\"duo\"},{\"at\":0.3,\"shield\":true,\"pack\":true,\"summon\":\"b\",\"count\":3}]") + "," + Enemy("b", "\"still\":true")),
            "A rolling boss with a roped formation phase and a pack phase of still shards is accepted");
        check(Rejects(Enemy("a", "\"roll\":{}")) && Rejects(Enemy("a", boss + "\"roll\":{\"minRange\":600,\"range\":500}")) && Rejects(Enemy("a", boss + "\"roll\":{\"speed\":0}")),
            "A roll on a plain enemy, or with numbers out of range, is refused");
        check(Rejects(Enemy("a", boss + "\"phases\":[{\"at\":0.5,\"shield\":true,\"formation\":\"nobody\"}]")) && Rejects(Enemy("a", boss + "\"phases\":[{\"at\":0.5,\"roped\":true,\"summon\":\"slime\",\"count\":1}]"))
              && Rejects(Enemy("a", boss + "\"phases\":[{\"at\":0.5,\"shield\":true,\"roped\":true,\"pack\":true,\"summon\":\"slime\",\"count\":1}]")),
            "An unknown formation, a roped phase without a shield, roped and pack together are refused");
        check(Rejects(Enemy("a", boss + "\"still\":true")) && Rejects(Enemy("a", "\"still\":true,\"dash\":true")) && Rejects(Enemy("a", boss + "\"ropeCutSpeed\":10")),
            "Still on a boss or an attacker, a rope cut speed out of range, are refused");

        var s = Shipped;
        var zip = s.Enemy("zip");
        var p = zip.Phases ?? Array.Empty<BossPhase>();
        check(zip is { Boss: true, Roll: not null, BinSpit: false } && p.Length == 3 && p[0] is { Roped: true, Formation: "fortress" } && p[1] is { Roped: true, Formation: "cavalry" }
              && p[2] is { Pack: true, Enrage: true } && s.PhaseCalls(p[2]).All(k => s.Enemy(k).Still) && s.Enemy("zipshard") is { Still: true, Damage: 0.0, BinSpit: false },
            "The shipped Zip Slime: a roll, Fortress then Cavalry on ropes, then packing behind still file shards");
        check(p.All(ph => s.PhaseCalls(ph).Count() <= zip.SummonCap), "Each of its phases fits its summon cap");
        var kinds = new[] { "zip", "zipshard" };
        bool Uses(WaveGroup g) => kinds.Contains(g.Enemy) || g.Pick?.Keys.Any(kinds.Contains) == true || g.Squad?.Any(m => kinds.Contains(m.Enemy)) == true;
        var all = s.WavePlan.Concat(s.Campaign?.Chapters.Take(1).SelectMany(ch => ch.Stages).SelectMany(st => st.Waves) ?? Enumerable.Empty<WaveSpec>());
        check(!all.SelectMany(w => w.Groups).Any(Uses) && s.Endless?.Boss != "zip", "No built-in battle uses it yet outside chapter 2 (1.5 M21 brings it in)");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m17.json"))) dir = dir.Parent;
        var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", "battle-m17.json"));
        check(load is { Custom: true, Error: null } && load.Settings.WavePlan.SelectMany(w => w.Groups).Any(g => g.Enemy == "zip"),
            $"docs/examples/battle-m17.json loads as custom data with the Zip Slime ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
    }
}
