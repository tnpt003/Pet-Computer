using DesktopPet;

// 1.5 M10, with stand-ins that carry the shipped drummer and chain links. The drummer: its allies in
// range wind up together on its last beat; an elite counts fewer beats. The chain slime: it ties itself to
// an ally and takes half of its hits (an elite more); the cat held past the threshold or thrown through the
// rope fast enough cuts it (the ally is dazed), a slow drag, a cat only pressed, one flying over the rope or
// a paused battle does not; the Scissors cut it too. The data limits and the shipped kinds (kept out of
// every battle for now).
internal static class BattleBandTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id, double range = 200, LinkSpec? link = null) => new()
    {
        Id = id, MaxHp = 1000, Speed = 0, Damage = 0, Range = range, Windup = 0.8, AttackCooldown = 1.8, Recover = 0.35, Link = link,
        Elite = new EliteSpec { Hp = 1.2 }
    };

    private static BattleSim Field(EnemyType[] kinds, int[] counts, bool elite = false, IEnumerable<BattleSkill>? skills = null)
    {
        // The cat's weapon reaches nothing (its knock back would get in the way).
        var arms = BattleData.Settings.Weapon("sword") with { Range = 5, StunChance = 0 };
        var settings = new BattleSettings
        {
            Weapons = new[] { arms }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1_000_000, MaxAlive = 8, MaxElites = 8, Enemies = kinds, CatDamage = 0, RageHitGain = 0,
            BombDamage = 40, BombCooldown = 0.1, BombDelay = 0.01, BombRadius = 40,
            Plan = new[] { new WaveSpec { Groups = kinds.Select((k, i) => new WaveGroup { Enemy = k.Id, Count = counts[i], Elite = elite && k.Link != null }).ToArray() } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 3, skills: skills);
        int total = counts.Sum();
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < total; i++) sim.Step(dt, 100, false);
        sim.ClearEvents();
        return sim;
    }

    public static void Run(Action<bool, string> check)
    {
        DrummerChecks(check);
        ChainChecks(check);
        CutChecks(check);
        DataChecks(check);
    }

    // Seconds until the drummer's strike with the cat at 100 and its allies kept off their own attacks;
    // how many wound up at that moment.
    private static (double Seconds, int Windups) Strike(BattleSim sim)
    {
        var drummer = sim.Slimes.Single(s => s.Link != null);
        drummer.BeatClock = 0; drummer.BeatCount = 0;
        for (double t = 0; t < 8; t += dt)
        {
            foreach (var s in sim.Slimes) if (s != drummer && s.State == SlimeState.Moving) s.Cooldown = 100;
            sim.Step(dt, 100, false);
            bool struck = sim.Events.Any(e => e.Kind == BattleEventKind.LinkStrike);
            sim.ClearEvents();
            if (struck) return (t + dt, sim.Slimes.Count(s => s != drummer && s.State == SlimeState.Windup));
        }
        return (double.NaN, 0);
    }

    private static void DrummerChecks(Action<bool, string> check)
    {
        var link = BattleData.Settings.Enemy("drummer").Link!;
        var sim = Field(new[] { Kind("drummer", 56, link), Kind("slime") }, new[] { 1, 3 });
        var drummer = sim.Slimes.Single(s => s.Link != null);
        double[] spots = { 170, 190, 210 };
        int k = 0;
        foreach (var s in sim.Slimes) s.X = s == drummer ? 320 : spots[k++];
        var (seconds, windups) = Strike(sim);
        check(Math.Abs(seconds - link.Beat * link.Beats) < 0.05 && windups == 3,
            $"On its {link.Beats}th beat ({link.Beat * link.Beats:0.0} s) every ally in its range winds up at once ({seconds:0.00} s, {windups} of 3)");
        check(BattleSim.Beats(drummer) == link.Beats, "A plain drummer counts its beats");

        sim = Field(new[] { Kind("drummer", 56, link), Kind("slime") }, new[] { 1, 3 }, elite: true);
        drummer = sim.Slimes.Single(s => s.Link != null);
        k = 0;
        foreach (var s in sim.Slimes) s.X = s == drummer ? 320 : spots[k++];
        (seconds, windups) = Strike(sim);
        check(drummer.Elite != null && BattleSim.Beats(drummer) == link.EliteBeats && Math.Abs(seconds - link.Beat * link.EliteBeats) < 0.05 && windups == 3,
            $"An elite drummer strikes every {link.EliteBeats} beats ({seconds:0.00} s)");
    }

    // A chain slime at 1100 tied to a slime at 900, the cat at 200.
    private static (BattleSim Sim, Slime Holder, Slime Ally) Rope(bool elite = false, IEnumerable<BattleSkill>? skills = null)
    {
        var link = BattleData.Settings.Enemy("chain").Link!;
        var sim = Field(new[] { Kind("chain", 52, link), Kind("slime") }, new[] { 1, 1 }, elite, skills);
        var holder = sim.Slimes.Single(s => s.Link != null);
        var ally = sim.Slimes.Single(s => s.Link == null);
        holder.X = 1100; ally.X = 900;
        for (int i = 0; i < 30 && holder.LinkedTo == 0; i++) { sim.Step(dt, 200, false); sim.ClearEvents(); }
        holder.X = 1100; ally.X = 900;
        return (sim, holder, ally);
    }

    private static void ChainChecks(Action<bool, string> check)
    {
        var link = BattleData.Settings.Enemy("chain").Link!;
        var (sim, holder, ally) = Rope();
        check(holder.LinkedTo == ally.Id && sim.TetherHolder(ally) == holder, "A chain slime ties its rope to the ally in range");
        double allyHp = ally.Hp, holderHp = holder.Hp;
        Bomb(sim, ally);
        check(Math.Abs(holderHp - holder.Hp - 40 * link.Share) < 0.01 && Math.Abs(allyHp - ally.Hp - 40 * (1 - link.Share)) < 0.01,
            $"It takes {link.Share:P0} of a hit on its ally ({holderHp - holder.Hp} / {allyHp - ally.Hp})");

        (sim, holder, ally) = Rope(elite: true);
        allyHp = ally.Hp; holderHp = holder.Hp;
        Bomb(sim, ally);
        check(holder.Elite != null && Math.Abs(holderHp - holder.Hp - 40 * link.EliteShare) < 0.01, $"An elite takes {link.EliteShare:P0} ({holderHp - holder.Hp})");
    }

    private static void Bomb(BattleSim sim, Slime slime)
    {
        for (int i = 0; i < 60 && !sim.Ready(BattleSkill.Bomb); i++) { sim.Step(dt, 200, false); sim.ClearEvents(); }
        sim.Use(BattleSkill.Bomb, slime.Id, 200);
        for (int i = 0; i < 3; i++) { sim.Step(dt, 200, false); sim.ClearEvents(); }
    }

    // The cat moved from one pose to the next over two steps; true if a rope was cut.
    private static bool Through(BattleSim sim, CatPose from, CatPose to, bool lifted = true)
    {
        bool cut = false;
        foreach (var pose in new[] { from, to })
        {
            sim.Step(dt, pose.X, true, lifted, pose);
            cut |= sim.Events.Any(e => e.Kind == BattleEventKind.LinkCut);
            sim.ClearEvents();
        }
        return cut;
    }

    private static void CutChecks(Action<bool, string> check)
    {
        var link = BattleData.Settings.Enemy("chain").Link!;
        double fast = link.CutSpeed + 600, slow = link.CutSpeed / 2;
        var (sim, holder, ally) = Rope();
        bool cut = Through(sim, new CatPose(1000, 70, 0, -fast, InHand: true, Flying: false), new CatPose(1000, 0, 0, -fast, InHand: true, Flying: false));
        check(cut && holder.LinkedTo == 0 && ally.State == SlimeState.Stunned && sim.CatRopeCuts == 1, "The held cat dragged down through the rope cuts it and dazes the ally");

        (sim, holder, ally) = Rope();
        cut = Through(sim, new CatPose(1000, 70, 0, -slow, true, false), new CatPose(1000, 0, 0, -slow, true, false));
        check(!cut && holder.LinkedTo == ally.Id, $"A slow drag (under {link.CutSpeed} DIPs/s) leaves the rope");
        cut = Through(sim, new CatPose(1000, 70, 0, -fast, true, false), new CatPose(1000, 0, 0, -fast, true, false), lifted: false);
        check(!cut && holder.LinkedTo == ally.Id, "A cat only pressed (not past the drag threshold) leaves it");
        cut = Through(sim, new CatPose(850, 200, fast, 0, false, true), new CatPose(1150, 200, fast, 0, false, true));
        check(!cut && holder.LinkedTo == ally.Id, "A cat thrown high over the rope leaves it");
        sim.Paused = true;
        cut = Through(sim, new CatPose(1000, 70, 0, -fast, true, false), new CatPose(1000, 0, 0, -fast, true, false));
        sim.Paused = false;
        check(!cut && holder.LinkedTo == ally.Id, "A paused battle cuts nothing");

        (sim, holder, ally) = Rope();
        cut = Through(sim, new CatPose(880, 40, fast, -fast / 3, false, true), new CatPose(1120, 0, fast, -fast / 3, false, true));
        check(cut && holder.LinkedTo == 0 && ally.State == SlimeState.Stunned && sim.CatRopeCuts == 1, "The thrown cat flying through the rope cuts it");

        (sim, holder, ally) = Rope(skills: new[] { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Bomb, BattleSkill.Scissors, BattleSkill.Shield });
        sim.Arm(BattleSkill.Scissors);
        check(sim.Snip(1000, 60, 1000, 0) && holder.LinkedTo == 0 && sim.RopesCut == 1 && sim.CatRopeCuts == 0, "The Scissors cut its rope as well");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        bool Rejects(string link)
        {
            try { BattleData.Parse("{\"enemies\":[{\"id\":\"slime\",\"nameVi\":\"S\",\"nameEn\":\"S\",\"link\":" + link + "},{\"id\":\"king\",\"nameVi\":\"K\",\"nameEn\":\"K\",\"boss\":true}]}"); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(!Rejects("{\"kind\":\"rhythm\",\"eliteBeats\":3}") && !Rejects("{\"kind\":\"tether\",\"cutSpeed\":500,\"eliteShare\":0.7}"), "Elite beats, a cut speed and an elite share within their limits are accepted");
        check(Rejects("{\"kind\":\"rhythm\",\"eliteBeats\":9}") && Rejects("{\"kind\":\"rhythm\",\"eliteBeats\":1}") && Rejects("{\"kind\":\"tether\",\"cutSpeed\":50}")
              && Rejects("{\"kind\":\"tether\",\"eliteShare\":1}"), "Out-of-range elite beats, cut speed or elite share is refused");
        var s = BattleData.Settings;
        check(s.Enemy("drummer") is { Id: "drummer", BinSpit: false, Marks: "drum", Elite: not null, Link: { Kind: EnemyLinks.Rhythm, EliteBeats: > 0 } }
              && s.Enemy("chain") is { Id: "chain", BinSpit: false, Marks: "chain", Elite: not null, Link: { Kind: EnemyLinks.Tether, EliteShare: > 0 } },
            "The shipped drummer (rhythm) and chain slime (tether), each with an elite, never spat by a bin");
        bool Uses(WaveGroup g) => g.Enemy is "drummer" or "chain" || g.Pick?.Keys.Any(k => k is "drummer" or "chain") == true;
        check(!s.WavePlan.SelectMany(w => w.Groups).Any(Uses), "The free battle's waves do not use them yet");

        // The documented try-out file loads and brings both in.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m10.json"))) dir = dir.Parent;
        var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", "battle-m10.json"));
        check(load is { Custom: true, Error: null } && load.Settings.WavePlan.SelectMany(w => w.Groups).Count(Uses) >= 3,
            $"docs/examples/battle-m10.json loads as custom data with the drummer and the chain slime ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
    }
}
