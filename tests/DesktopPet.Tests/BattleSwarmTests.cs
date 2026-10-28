using DesktopPet;

// 1.5 M11 (PetBattle.Fuse.cs, PetBattle.Split.cs, merge/ride in PetBattle.Links.cs), with stand-ins that
// carry the shipped fuse, split and link numbers. The bomb slime: it lights its fuse next to the cat and
// stands still, blows up on a cat on the floor (hurt, stunned) and on the slimes around, is then gone
// without counting as the cat's; a lifted cat, the Shield and Pause beat it; defeated before it is lit it
// fizzles, while lit it blows up; knocked off its mount it lights a short fuse and blows up where it fell;
// thrown by the Grabber it blows up where it lands; an elite burns shorter and blasts wider. The fusing
// slimes merge into a giant. The splitting slime breaks into halves (an elite into three; even past
// maxAlive), the halves never split, keep the wave going, and merge back only once a healer has healed
// each. The data limits, the shipped kinds (kept out of every battle for now) and the try-out file.
internal static class BattleSwarmTests
{
    const double dt = 1.0 / 60;

    // Stand-ins named like the shipped kinds, so the shipped links' mount/into names fit.
    private static EnemyType Kind(string id, double damage = 0, double hp = 1000, double scale = 1, LinkSpec? link = null, FuseSpec? fuse = null,
        SplitSpec? split = null, double heal = 0) => new()
    {
        Id = id, MaxHp = hp, Speed = 0, Damage = damage, Range = 50, Windup = 0.8, AttackCooldown = 2, Recover = 0.3, Scale = scale,
        Elite = new EliteSpec { Hp = 1.2 }, Link = link, Fuse = fuse, Split = split,
        HealAmount = heal, HealRange = 300, HealCooldown = 0.2, HealWindup = 0.2, HealBudget = 1000
    };

    private static EnemyType Bomb() => Kind("bomber", 22, 30, 0.85, BattleData.Settings.Enemy("bomber").Link, BattleData.Settings.Enemy("bomber").Fuse);

    // A field with these kinds and counts (elite: the first kind elite) and the cat at 100.
    private static BattleSim Field(EnemyType[] kinds, int[] counts, bool elite = false, int maxAlive = 8, IEnumerable<BattleSkill>? skills = null,
        EnemyType[]? extra = null)
    {
        var arms = BattleData.Settings.Weapon("sword") with { Range = 5, StunChance = 0 };
        var settings = new BattleSettings
        {
            Weapons = new[] { arms }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1000, MaxAlive = maxAlive, MaxElites = 8,
            Enemies = kinds.Concat(extra ?? Array.Empty<EnemyType>()).ToArray(), CatDamage = 0, RageHitGain = 0,
            BombDamage = 1000, BombCooldown = 0.1, BombDelay = 0.01, BombRadius = 10,
            Plan = new[] { new WaveSpec { Groups = kinds.Select((k, i) => new WaveGroup { Enemy = k.Id, Count = counts[i], Elite = elite && i == 0 }).ToArray() } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 4, skills: skills);
        int total = counts.Sum();
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < total; i++) sim.Step(dt, 100, false);
        foreach (var s in sim.Slimes) s.X = 1500;
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

    // The yarn bomb on one enemy (its damage is lethal here): it lands at once.
    private static void Defeat(BattleSim sim, Slime slime)
    {
        for (int i = 0; i < 30 && !sim.Ready(BattleSkill.Bomb); i++) sim.Step(dt, 100, false);
        sim.Use(BattleSkill.Bomb, slime.Id, 100);
        sim.Step(dt, 100, false);
    }

    public static void Run(Action<bool, string> check)
    {
        FuseChecks(check);
        RideChecks(check);
        MergeChecks(check);
        SplitChecks(check);
        DataChecks(check);
    }

    private static void FuseChecks(Action<bool, string> check)
    {
        var fuse = BattleData.Settings.Enemy("bomber").Fuse!;
        var sim = Field(new[] { Bomb(), Kind("slime") }, new[] { 1, 1 });
        var bomb = sim.Slimes.Single(s => s.Type.Id == "bomber");
        var other = sim.Slimes.Single(s => s.Type.Id == "slime");
        bomb.X = 150; other.X = 200;
        bool lit = Run(sim, dt, 100, BattleEventKind.FuseLit);
        check(lit && bomb.Lit && Math.Abs(bomb.FuseLeft - fuse.Seconds) < 0.05, $"Within {fuse.Range} DIPs of the cat a bomb lights a {fuse.Seconds} s fuse");
        double hp = sim.CatHp, otherHp = other.Hp;
        int defeated = sim.Defeated;
        bool early = Run(sim, fuse.Seconds - 0.1, 100, BattleEventKind.Blasted);
        check(!early && Math.Abs(bomb.X - 150) < 1 && bomb.State == SlimeState.Moving, "A lit bomb stands still and does not attack while the fuse burns");
        bool blasted = Run(sim, 0.2, 100, BattleEventKind.Blasted, 1);
        check(blasted && sim.CatHp < hp - 1 && sim.CatStunned, "It blows up on the cat on the floor in its ring: hurt and stunned");
        check(bomb is { Exploded: true, State: SlimeState.Defeated } && sim.Defeated == defeated, "Then it is gone, not counted as the cat's kill");
        check(Math.Abs(other.Hp - (otherHp - fuse.EnemyDamage)) < 0.01, $"Slimes in the ring lose {fuse.EnemyDamage}");

        sim = Field(new[] { Bomb() }, new[] { 1 });
        bomb = sim.Slimes[0];
        bomb.X = 150;
        Run(sim, dt, 100);
        hp = sim.CatHp;
        blasted = Run(sim, fuse.Seconds + 0.1, 100, BattleEventKind.Blasted, 0, held: true);
        check(blasted && sim.CatHp == hp && !sim.CatStunned, "A cat lifted out of the ring dodges the blast");

        sim = Field(new[] { Bomb() }, new[] { 1 });
        bomb = sim.Slimes[0];
        Run(sim, 0.5, 1500); // The shield's cooldown.
        bomb.X = 150;
        Run(sim, dt, 100);
        sim.Use(BattleSkill.Shield, 100);
        hp = sim.CatHp;
        Run(sim, fuse.Seconds + 0.1, 100);
        check(bomb.Exploded && sim.CatHp == hp, "The Shield takes the blast");

        sim = Field(new[] { Bomb() }, new[] { 1 });
        bomb = sim.Slimes[0];
        bomb.X = 150;
        Run(sim, dt, 100);
        double left = bomb.FuseLeft;
        sim.Paused = true;
        Run(sim, 5, 100);
        sim.Paused = false;
        check(bomb.Lit && bomb.FuseLeft == left, "Pause freezes the fuse");

        sim = Field(new[] { Bomb() }, new[] { 1 });
        bomb = sim.Slimes[0];
        defeated = sim.Defeated;
        Defeat(sim, bomb);
        check(bomb.State == SlimeState.Defeated && !bomb.Exploded && sim.Blasts == 0 && sim.Defeated == defeated + 1, "Defeated before it is lit, it fizzles (the cat's kill)");

        sim = Field(new[] { Bomb(), Kind("slime") }, new[] { 1, 1 });
        bomb = sim.Slimes.Single(s => s.Type.Id == "bomber");
        other = sim.Slimes.Single(s => s.Type.Id == "slime");
        bomb.X = 150; other.X = 210;
        Run(sim, dt, 100);
        otherHp = other.Hp;
        Defeat(sim, bomb);
        check(sim.Blasts == 1 && other.Hp < otherHp, "Defeated while lit, it blows up on the spot");

        sim = Field(new[] { Bomb() }, new[] { 1 }, elite: true);
        bomb = sim.Slimes[0];
        bomb.X = 150;
        Run(sim, dt, 100);
        check(bomb.Elite != null && Math.Abs(bomb.FuseLeft - fuse.EliteSeconds) < 0.05 && Math.Abs(BattleSim.FuseRadius(bomb) - fuse.Radius * fuse.EliteRadius) < 0.01,
            "An elite burns a shorter fuse and blasts wider");

        // The Grabber throws a bomb: it blows up where it lands.
        var skills = new[] { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Bomb, BattleSkill.Grabber, BattleSkill.Shield };
        sim = Field(new[] { Bomb() }, new[] { 1 }, skills: skills);
        bomb = sim.Slimes[0];
        bomb.X = 900;
        sim.Arm(BattleSkill.Grabber);
        bool grabbed = sim.Grab(bomb.Id, 900, 20);
        for (int i = 0; i < 6; i++) { sim.MoveGrab(900 + 40 * (i + 1), 40 + 10 * i); sim.Step(dt, 100, false); }
        sim.ReleaseGrab();
        blasted = Run(sim, 2, 100, BattleEventKind.Blasted);
        check(grabbed && blasted && bomb.Exploded && bomb.X > 900, "Thrown by the Grabber, a bomb blows up where it lands");
    }

    private static void RideChecks(Action<bool, string> check)
    {
        var fuse = BattleData.Settings.Enemy("bomber").Fuse!;
        var mount = Kind("runner", hp: 30, scale: 0.75);
        var sim = Field(new[] { Bomb(), mount }, new[] { 1, 1 });
        var bomb = sim.Slimes.Single(s => s.Type.Id == "bomber");
        var horse = sim.Slimes.Single(s => s.Type.Id == "runner");
        bomb.X = 1000; horse.X = 1040;
        bool formed = Run(sim, 0.5, 100, BattleEventKind.LinkFormed);
        check(formed && bomb.Riding && bomb.LinkedTo == horse.Id && bomb.X == horse.X, "A bomb rides a mount near it, far from the cat");
        check(sim.Slimes.Count(s => s.Targetable) == 2 && sim.Alive == 2, "Rider and mount still count as two on the floor");
        bomb.Hp = bomb.MaxHp = 1e6; // The yarn bomb on its mount must not defeat the rider too.
        Defeat(sim, horse);
        check(!bomb.Riding && bomb.Lit && Math.Abs(bomb.FuseLeft - fuse.DropFuse) < 0.05 && bomb.State == SlimeState.Stunned,
            $"Its mount defeated on the way, it falls dazed with a {fuse.DropFuse} s fuse");
        double where = bomb.X, hp = sim.CatHp;
        bool blasted = Run(sim, fuse.DropFuse + 0.1, 100, BattleEventKind.Blasted, 0);
        check(blasted && bomb.Exploded && Math.Abs(bomb.X - where) < 1 && sim.CatHp == hp, "and blows up where it fell, far from the cat");
    }

    private static void MergeChecks(Action<bool, string> check)
    {
        var shipped = BattleData.Settings.Enemy("fuser").Link!;
        var giant = Kind("giant", hp: 78, scale: 1.6);
        var fuser = Kind("fuser", hp: 26, scale: 0.7, link: shipped);
        var sim = Field(new[] { fuser }, new[] { 3 }, extra: new[] { giant });
        double x = 1200;
        foreach (var s in sim.Slimes) s.X = x += 20;
        double sum = sim.Slimes.Sum(s => s.Hp);
        bool merged = Run(sim, shipped.MergeSeconds + 0.3, 100, BattleEventKind.Merged);
        var big = sim.Slimes.SingleOrDefault(s => s.Type.Id == "giant");
        check(merged && big != null && Math.Abs(big.Hp - sum) < 0.01 && sim.Slimes.Count(s => s.Type.Id == "fuser") == 0,
            $"Three fusing slimes together {shipped.MergeSeconds} s become one giant with their HP added up");
        check(big != null && !sim.Grabbable(big) == (big.Type.Scale > sim.Settings.GrabberMaxScale), "The giant is too big for the Grabber");
    }

    private static (BattleSim Sim, EnemyType Half) Splitting(bool elite = false, int maxAlive = 8, EnemyType[]? more = null, int[]? counts = null)
    {
        var shipped = BattleData.Settings;
        var half = Kind("splitling", hp: 35, scale: 0.7, link: shipped.Enemy("splitling").Link);
        var splitter = Kind("splitter", hp: 70, split: shipped.Enemy("splitter").Split);
        var kinds = new[] { splitter }.Concat(more ?? Array.Empty<EnemyType>()).ToArray();
        return (Field(kinds, counts ?? new[] { 1 }, elite, maxAlive, extra: new[] { half }), half);
    }

    private static void SplitChecks(Action<bool, string> check)
    {
        var split = BattleData.Settings.Enemy("splitter").Split!;
        var (sim, _) = Splitting();
        var mother = sim.Slimes[0];
        mother.X = 1000;
        int defeated = sim.Defeated;
        Defeat(sim, mother);
        var halves = sim.Slimes.Where(s => s.Type.Id == "splitling").ToList();
        check(halves.Count == split.Count && halves.All(h => Math.Abs(h.Hp - Math.Round(h.MaxHp * split.Health)) < 0.01) && sim.Defeated == defeated + 1 && sim.Splits == 1,
            $"Defeated, a splitting slime breaks into {split.Count} halves at {split.Health:P0} HP");
        Run(sim, 1, 1500);
        check(sim.Phase == BattlePhase.Fighting && sim.Alive == split.Count, "The halves keep the wave going");
        foreach (var h in halves) h.X = 1000;
        Run(sim, 5, 100);
        check(sim.Slimes.Count(s => s.Type.Id == "splitling" && s.Targetable) == split.Count && sim.Slimes.All(s => s.Type.Id != "splitter" || !s.Targetable),
            "Halves nobody healed never merge back");
        Defeat(sim, halves[0]);
        check(sim.Splits == 1, "A half does not split again");

        (sim, _) = Splitting(elite: true);
        Defeat(sim, sim.Slimes[0]);
        check(sim.Slimes.Count(s => s.Type.Id == "splitling") == split.EliteCount, $"An elite breaks into {split.EliteCount}");

        (sim, _) = Splitting(maxAlive: 1);
        Defeat(sim, sim.Slimes[0]);
        check(sim.Alive == split.Count, "The halves come even past maxAlive");

        // A healer heals each half: they merge back into the splitting slime, their HP added up.
        var healer = Kind("healer", heal: 10);
        (sim, _) = Splitting(more: new[] { healer }, counts: new[] { 1, 1 });
        mother = sim.Slimes.Single(s => s.Type.Id == "splitter");
        var mender = sim.Slimes.Single(s => s.Type.Id == "healer");
        mother.X = 1000; mender.X = 1100;
        Defeat(sim, mother);
        halves = sim.Slimes.Where(s => s.Type.Id == "splitling").ToList();
        bool merged = Run(sim, 6, 1500, BattleEventKind.Merged, each: () => { foreach (var h in halves) if (h.Targetable) h.X = 1000; });
        var whole = sim.Slimes.LastOrDefault(s => s.Type.Id == "splitter" && s.Targetable);
        check(merged && halves.All(h => h.Healed) && whole != null && Math.Abs(whole.MaxHp - 70) < 0.01 && whole.Hp > 2 * Math.Round(35 * split.Health),
            "Healed by a healer, the halves merge back into the splitting slime");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        bool Rejects(string extra, string more = "")
        {
            try
            {
                BattleData.Parse("{\"enemies\":[{\"id\":\"slime\",\"nameVi\":\"S\",\"nameEn\":\"S\"" + extra + "}," + more +
                    "{\"id\":\"king\",\"nameVi\":\"K\",\"nameEn\":\"K\",\"boss\":true}]}");
                return false;
            }
            catch (InvalidDataException) { return true; }
        }
        check(!Rejects(",\"fuse\":{\"range\":80,\"seconds\":1}") && !Rejects(",\"split\":{\"into\":\"half\"}", "{\"id\":\"half\",\"nameVi\":\"H\",\"nameEn\":\"H\"},"),
            "A fuse and a split within their limits are accepted");
        check(Rejects(",\"fuse\":{\"seconds\":9}") && Rejects(",\"fuse\":{\"radius\":5}") && Rejects(",\"support\":true,\"fuse\":{}") && Rejects(",\"dash\":true,\"fuse\":{}"),
            "Out-of-range fuse numbers, or a fuse on a support or dash enemy, are refused");
        check(Rejects(",\"split\":{\"into\":\"slime\"}") && Rejects(",\"split\":{\"into\":\"nobody\"}") && Rejects(",\"split\":{\"into\":\"king\"}")
              && Rejects(",\"split\":{\"into\":\"half\",\"count\":5}", "{\"id\":\"half\",\"nameVi\":\"H\",\"nameEn\":\"H\"},"),
            "A split into itself, an unknown kind, a boss, or into too many is refused");
        check(Rejects(",\"link\":{\"kind\":\"tether\",\"afterHeal\":true}"), "afterHeal belongs to a merge link");

        var s = BattleData.Settings;
        string[] ids = { "fuser", "giant", "bomber", "splitter", "splitling" };
        check(ids.All(id => s.Enemy(id).Id == id && !s.Enemy(id).BinSpit), "The shipped fusing, giant, bomb, splitting and half slimes, never spat by a bin");
        check(s.Enemy("fuser").Link is { Kind: EnemyLinks.Merge, Into: "giant", Count: 3 } && s.Enemy("bomber") is { Fuse: not null, Link: { Kind: EnemyLinks.Ride, Mount: "runner" } }
              && s.Enemy("splitter").Split is { Into: "splitling" } && s.Enemy("splitling").Link is { Kind: EnemyLinks.Merge, Into: "splitter", AfterHeal: true },
            "They carry their merge, ride, fuse and split");
        bool Uses(WaveGroup g) => ids.Contains(g.Enemy) || g.Pick?.Keys.Any(ids.Contains) == true || g.Squad?.Any(m => ids.Contains(m.Enemy)) == true;
        check(!s.WavePlan.SelectMany(w => w.Groups).Any(Uses), "The free battle's waves do not use them yet");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m11.json"))) dir = dir.Parent;
        var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", "battle-m11.json"));
        check(load is { Custom: true, Error: null } && load.Settings.WavePlan.SelectMany(w => w.Groups).Count(Uses) >= 3,
            $"docs/examples/battle-m11.json loads as custom data with the new slimes ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
    }
}
