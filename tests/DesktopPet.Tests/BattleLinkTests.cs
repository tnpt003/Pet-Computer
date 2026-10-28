using DesktopPet;

// 1.5 M01: links between enemies (PetBattle.Links.cs), each kind with stand-in enemies: a tether
// shares hits and dazes its ally when cut or when its holder falls; a rhythm makes allies wind up
// together on its beat (a stun starts the count again); a rider is carried by its mount, hops off
// near the cat and falls dazed when the mount does; a merge makes one big enemy out of a huddle (a
// stun starts its clock again); an aura speeds allies up and they panic when its holder falls. A
// battle lets in at most two kinds; a link can be switched off; Pause freezes them all; the built-in
// battles have no link yet (chapter 1 is untouched).
internal static class BattleLinkTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id, double hp = 1000, double speed = 0, double damage = 0, double range = 64, LinkSpec? link = null) => new()
    {
        Id = id, MaxHp = hp, Speed = speed, Damage = damage, Range = range, Windup = 0.8, AttackCooldown = 1.8, Recover = 0.35, Link = link
    };

    private static BattleSettings Settings(EnemyType[] kinds, WaveSpec[] plan, int maxAlive = 8) => new()
    {
        FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1_000_000, MaxAlive = maxAlive, Enemies = kinds, Plan = plan, CatDamage = 0,
        BombDamage = 40, BombCooldown = 0.1, BombDelay = 0.01, BombRadius = 40, PushCooldown = 0.1
    };

    private static WaveSpec[] Plan(params WaveGroup[] groups) => new[] { new WaveSpec { Groups = groups } };
    private static WaveGroup G(string enemy, int count = 1) => new() { Enemy = enemy, Count = count };

    // Steps with the cat standing at `cat` (never walking), events kept for `seen`.
    private static void Run(BattleSim sim, double cat, double seconds, Action? each = null, HashSet<BattleEventKind>? seen = null, bool held = false)
    {
        for (double t = 0; t < seconds && !sim.Over; t += dt)
        {
            sim.Step(dt, cat, held, held);
            if (seen != null) foreach (var e in sim.Events) seen.Add(e.Kind);
            each?.Invoke();
            sim.ClearEvents();
        }
    }

    private static void Ready(BattleSim sim, int count, double cat)
    {
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < count; i++) sim.Step(dt, cat, false);
        sim.ClearEvents();
    }

    // A bomb on one enemy (once the skill is ready), burst at once; its events go to `seen`.
    private static void Bomb(BattleSim sim, Slime slime, double cat, HashSet<BattleEventKind>? seen = null)
    {
        for (int i = 0; i < 60 && !sim.Ready(BattleSkill.Bomb); i++) { sim.Step(dt, cat, false); sim.ClearEvents(); }
        sim.Use(BattleSkill.Bomb, slime.Id, cat);
        for (int i = 0; i < 3; i++)
        {
            sim.Step(dt, cat, false);
            if (seen != null) foreach (var e in sim.Events) seen.Add(e.Kind);
            sim.ClearEvents();
        }
    }

    public static void Run(Action<bool, string> check)
    {
        TetherChecks(check);
        RhythmChecks(check);
        RideChecks(check);
        MergeChecks(check);
        AuraChecks(check);
        CapChecks(check);
        DataChecks(check);
    }

    private static LinkSpec Tether(bool enabled = true, double range = 300) => new() { Kind = EnemyLinks.Tether, Enabled = enabled, Range = range, Share = 0.5, BreakStun = 1, Relink = 2 };

    private static BattleSim TetherSim(LinkSpec link, double holderSpeed = 0, double holderHp = 1000)
    {
        var sim = new BattleSim(Settings(new[] { Kind("chain", holderHp, holderSpeed, link: link), Kind("slime") }, Plan(G("chain"), G("slime"))), 0, 1600, seed: 3);
        Ready(sim, 2, 200);
        sim.Slimes.Single(s => s.Type.Id == "chain").X = 1100;
        sim.Slimes.Single(s => s.Type.Id == "slime").X = 900;
        return sim;
    }

    private static void TetherChecks(Action<bool, string> check)
    {
        var seen = new HashSet<BattleEventKind>();
        var sim = TetherSim(Tether());
        var holder = sim.Slimes.Single(s => s.Type.Id == "chain");
        var ally = sim.Slimes.Single(s => s.Type.Id == "slime");
        Run(sim, 200, 0.3, seen: seen);
        check(holder.LinkedTo == ally.Id && sim.LinkPartner(holder) == ally && sim.TetherHolder(ally) == holder && seen.Contains(BattleEventKind.LinkFormed),
            "A tether holder ties itself to the ally within its range");

        double allyHp = ally.Hp, holderHp = holder.Hp;
        Bomb(sim, ally, 200);
        check(Math.Abs(allyHp - ally.Hp - 20) < 0.01 && Math.Abs(holderHp - holder.Hp - 20) < 0.01, $"The holder takes half of a hit on its ally ({allyHp - ally.Hp} / {holderHp - holder.Hp})");

        seen.Clear();
        check(sim.CutLink(holder.Id) && holder.LinkedTo == 0 && ally.State == SlimeState.Stunned, "Cutting the rope dazes the ally");
        foreach (var e in sim.Events) seen.Add(e.Kind);
        sim.ClearEvents();
        check(seen.Contains(BattleEventKind.LinkCut) && !sim.CutLink(holder.Id), "A cut is announced, and there is nothing left to cut");
        allyHp = ally.Hp; holderHp = holder.Hp;
        Bomb(sim, ally, 200);
        check(allyHp - ally.Hp > 39 && holder.Hp == holderHp, "With the rope cut the ally takes the whole hit");
        Run(sim, 200, 1.5);
        check(holder.LinkedTo == 0, "The holder does not tie itself again before its relink time");
        Run(sim, 200, 1.5);
        check(holder.LinkedTo == ally.Id, "After its relink time it ties itself again");

        // Stretched too far: it snaps, with no reward.
        seen.Clear();
        ally.X = 1550; holder.X = 400;
        Run(sim, 200, 0.05, seen: seen);
        check(holder.LinkedTo == 0 && seen.Contains(BattleEventKind.LinkSnapped) && ally.State != SlimeState.Stunned, "A rope stretched too far snaps without dazing the ally");

        // The holder defeated: its ally is dazed.
        var weak = TetherSim(Tether(), holderHp: 10);
        var weakHolder = weak.Slimes.Single(s => s.Type.Id == "chain");
        var weakAlly = weak.Slimes.Single(s => s.Type.Id == "slime");
        Run(weak, 200, 0.3);
        Bomb(weak, weakHolder, 200);
        check(weakHolder.State == SlimeState.Defeated && weakAlly.State == SlimeState.Stunned, "Defeating the holder dazes its ally");

        // It keeps just behind its ally, on the side away from the cat.
        var walker = TetherSim(Tether(range: 500), holderSpeed: 60);
        var w = walker.Slimes.Single(s => s.Type.Id == "chain");
        var wAlly = walker.Slimes.Single(s => s.Type.Id == "slime");
        w.X = 1350;
        Run(walker, 200, 8);
        double post = wAlly.X + wAlly.Width / 2 + w.Width / 2 + 6;
        check(w.LinkedTo == wAlly.Id && Math.Abs(w.X - post) < 10, $"A tethered holder keeps behind its ally ({w.X:0} for {post:0})");

        // Switched off: no rope at all.
        var off = TetherSim(Tether(enabled: false));
        Run(off, 200, 1);
        check(off.Slimes.All(s => s.LinkedTo == 0 && s.Link == null) && off.LinkKinds.Count == 0, "A link switched off never ties");
    }

    private static void RhythmChecks(Action<bool, string> check)
    {
        var link = new LinkSpec { Kind = EnemyLinks.Rhythm, Range = 400, Beat = 0.5, Beats = 4 };
        BattleSim Make()
        {
            var settings = Settings(new[] { Kind("drum", link: link), Kind("slime") }, Plan(G("drum"), G("slime", 3)));
            settings = new BattleSettings
            {
                FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1_000_000, MaxAlive = 8, Enemies = settings.Enemies, Plan = settings.Plan, CatDamage = 0,
                MaxAttackers = 1, AttackStagger = 3, PushCooldown = 0.1
            };
            var sim = new BattleSim(settings, 0, 1600, seed: 5);
            Ready(sim, 4, 800);
            double[] spots = { 980, 750, 800, 850 };
            for (int i = 0; i < 4; i++) sim.Slimes[i].X = spots[i];
            foreach (var s in sim.Slimes) { s.Cooldown = 30; }
            return sim;
        }
        var sim = Make();
        int most = 0;
        var seen = new HashSet<BattleEventKind>();
        Run(sim, 800, 2.2, () => most = Math.Max(most, sim.Slimes.Count(s => s.State == SlimeState.Windup)), seen);
        check(most >= 3 && seen.Contains(BattleEventKind.LinkStrike) && seen.Contains(BattleEventKind.LinkBeat),
            $"On the rhythm's strike every ally in reach winds up together, past attack turns and cooldowns ({most} at once)");

        var pushed = Make();
        var drum = pushed.Slimes.Single(s => s.Type.Id == "drum");
        Run(pushed, 800, 1.2);
        check(drum.BeatCount == 2, $"The beats count up ({drum.BeatCount})");
        pushed.UsePush(drum.Id, 800);
        Run(pushed, 800, 0.1);
        check(drum.BeatCount == 0 && drum.BeatClock == 0, "A push on the drummer starts its count again");

        var held = Make();
        int heldMost = 0;
        Run(held, 800, 2.2, () => heldMost = Math.Max(heldMost, held.Slimes.Count(s => s.State == SlimeState.Windup)), held: true);
        check(heldMost == 0, "The strike never winds up on a cat in the user's hands");

        var paused = Make();
        var pd = paused.Slimes.Single(s => s.Type.Id == "drum");
        Run(paused, 800, 0.7);
        var (count, clock) = (pd.BeatCount, pd.BeatClock);
        paused.Paused = true;
        Run(paused, 800, 3);
        check(pd.BeatCount == count && pd.BeatClock == clock, "Pause freezes the beat");
    }

    private static void RideChecks(Action<bool, string> check)
    {
        var link = new LinkSpec { Kind = EnemyLinks.Ride, Mount = "runner", Range = 150, DropRange = 90, BreakStun = 1, Relink = 30 };
        BattleSim Make(double runnerSpeed, double runnerHp = 1000)
        {
            var sim = new BattleSim(Settings(new[] { Kind("bomber", link: link), Kind("runner", runnerHp, runnerSpeed) }, Plan(G("bomber"), G("runner"))), 0, 1600, seed: 7);
            Ready(sim, 2, 200);
            sim.Slimes.Single(s => s.Type.Id == "runner").X = 1200;
            sim.Slimes.Single(s => s.Type.Id == "bomber").X = 1250;
            return sim;
        }
        var sim = Make(0);
        var rider = sim.Slimes.Single(s => s.Type.Id == "bomber");
        var mount = sim.Slimes.Single(s => s.Type.Id == "runner");
        Run(sim, 200, 0.3);
        check(rider.Riding && rider.LinkedTo == mount.Id && rider.X == mount.X && rider.RideHeight > 0, "A rider mounts the kind it rides and is carried on top");
        bool ignored = true;
        Run(sim, 200, 1, () => ignored &= sim.TargetId != rider.Id);
        check(ignored && !sim.Select(rider.Id) && sim.Select(mount.Id), "The cat goes for the mount, never the rider on it");

        var walk = Make(80);
        var wr = walk.Slimes.Single(s => s.Type.Id == "bomber");
        var wm = walk.Slimes.Single(s => s.Type.Id == "runner");
        var seen = new HashSet<BattleEventKind>();
        bool together = true;
        Run(walk, 200, 0.3);
        Run(walk, 200, 14, () => { if (wr.Riding) together &= wr.X == wm.X; }, seen);
        check(together && !wr.Riding && seen.Contains(BattleEventKind.Dismounted) && wr.State != SlimeState.Stunned && Math.Abs(wr.X - 200) <= 140,
            $"Carried to the cat, the rider hops off there without a stun (at {wr.X:0})");

        var fall = Make(0, runnerHp: 10);
        var fr = fall.Slimes.Single(s => s.Type.Id == "bomber");
        var fm = fall.Slimes.Single(s => s.Type.Id == "runner");
        Run(fall, 200, 0.3);
        Bomb(fall, fm, 200);
        check(fm.State == SlimeState.Defeated && !fr.Riding && fr.State == SlimeState.Stunned && fr.RideHeight == 0, "Its mount defeated, the rider falls dazed");

        var cut = Make(0);
        var cr = cut.Slimes.Single(s => s.Type.Id == "bomber");
        Run(cut, 200, 0.3);
        check(cut.CutLink(cr.Id) && !cr.Riding && cr.State == SlimeState.Stunned, "Cutting a rider off its mount dazes it");
    }

    private static void MergeChecks(Action<bool, string> check)
    {
        var link = new LinkSpec { Kind = EnemyLinks.Merge, Count = 3, Range = 80, MergeSeconds = 2, Into = "giant" };
        BattleSim Make()
        {
            var sim = new BattleSim(Settings(new[] { Kind("mini", 40, link: link), Kind("giant", 300) }, Plan(G("mini", 3))), 0, 1600, seed: 9);
            Ready(sim, 3, 200);
            for (int i = 0; i < 3; i++) sim.Slimes[i].X = 1000 + 20 * i;
            return sim;
        }
        var sim = Make();
        Run(sim, 200, 1.8);
        check(sim.Slimes.Count(s => s.Type.Id == "mini") == 3 && sim.Slimes[0].MergeClock > 1.5, "The huddle's clock runs before the merge");
        var seen = new HashSet<BattleEventKind>();
        Run(sim, 200, 0.4, seen: seen);
        var giant = sim.Slimes.SingleOrDefault(s => s.Type.Id == "giant");
        check(giant != null && sim.Slimes.Count == 1 && giant.Hp == 120 && giant.MaxHp == 120 && seen.Contains(BattleEventKind.Merged),
            $"Three in a huddle for 2 s become one giant with their HP added up ({giant?.Hp}/{giant?.MaxHp})");
        check(sim.Defeated == 0 && sim.Phase == BattlePhase.Fighting && sim.Alive == 1, "A merge defeats no one and does not end the wave");

        var pushed = Make();
        Run(pushed, 200, 1);
        pushed.UsePush(pushed.Slimes[1].Id, 200);
        Run(pushed, 200, 1.3);
        check(pushed.Slimes.Count(s => s.Type.Id == "mini") == 3, "A push on one of them starts the merge clock again");
    }

    private static void AuraChecks(Action<bool, string> check)
    {
        BattleSim Make(bool enabled, double flagHp = 1000)
        {
            var link = new LinkSpec { Kind = EnemyLinks.Aura, Enabled = enabled, Range = 300, Speed = 1.5, BreakStun = 2 };
            var sim = new BattleSim(Settings(new[] { Kind("flag", flagHp, link: link), Kind("slime", 1000, 40) }, Plan(G("flag"), G("slime"))), 0, 1600, seed: 11);
            Ready(sim, 2, 200);
            sim.Slimes.Single(s => s.Type.Id == "flag").X = 1200;
            sim.Slimes.Single(s => s.Type.Id == "slime").X = 1100;
            return sim;
        }
        double Moved(BattleSim sim)
        {
            var slime = sim.Slimes.Single(s => s.Type.Id == "slime");
            double from = slime.X;
            Run(sim, 200, 1);
            return from - slime.X;
        }
        double fast = Moved(Make(true)), plain = Moved(Make(false));
        check(Math.Abs(fast - plain * 1.5) < 3 && plain > 30, $"An ally in the aura moves 1.5 × faster ({fast:0} against {plain:0} DIPs in 1 s)");

        var fall = Make(true, flagHp: 10);
        var flag = fall.Slimes.Single(s => s.Type.Id == "flag");
        var ally = fall.Slimes.Single(s => s.Type.Id == "slime");
        ally.X = 400; // Out of the bomb's reach.
        var seen = new HashSet<BattleEventKind>();
        Bomb(fall, flag, 200, seen);
        double before = ally.X;
        bool calm = true;
        Run(fall, 200, 1.5, () => calm &= ally.State != SlimeState.Windup, seen);
        check(flag.State == SlimeState.Defeated && seen.Contains(BattleEventKind.Panicked) && ally.X > before + 30 && calm,
            $"Its holder defeated, the allies panic: they run from the cat and do not attack ({before:0} → {ally.X:0})");
        Run(fall, 200, 1);
        check(ally.PanicRemaining == 0, "The panic wears off");
    }

    private static void CapChecks(Action<bool, string> check)
    {
        var kinds = new[]
        {
            Kind("chain", link: Tether()), Kind("drum", link: new LinkSpec { Kind = EnemyLinks.Rhythm }),
            Kind("flag", link: new LinkSpec { Kind = EnemyLinks.Aura }), Kind("slime")
        };
        var plan = Plan(G("chain"), G("drum"), G("flag"), G("slime"));
        bool rejected;
        try { BattleData.Validate(new BattleSettings { Enemies = kinds, Plan = plan }); rejected = false; }
        catch (InvalidDataException) { rejected = true; }
        check(rejected, "Data with three kinds of link in one battle is refused");

        // A battle built anyway (generated waves): the third kind fights without its link.
        var sim = new BattleSim(Settings(kinds, plan), 0, 1600, seed: 13);
        Ready(sim, 4, 200);
        check(sim.LinkKinds.Count == 2 && sim.Slimes.Single(s => s.Type.Id == "flag").Link == null && sim.Slimes.Single(s => s.Type.Id == "chain").Link != null,
            "At most two kinds of link come into a battle, first come");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        bool Rejects(params EnemyType[] enemies)
        {
            try { BattleData.Validate(new BattleSettings { Enemies = enemies, Plan = Plan(G(enemies[0].Id)) }); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(!Rejects(Kind("chain", link: Tether()), Kind("slime")), "A plain tether is accepted");
        check(Rejects(Kind("x", link: new LinkSpec { Kind = "bounce" })), "An unknown kind of link (bounce was dropped) is refused");
        check(Rejects(Kind("x", link: new LinkSpec { Kind = EnemyLinks.Tether, Share = 1 })) && Rejects(Kind("x", link: new LinkSpec { Kind = EnemyLinks.Rhythm, Beats = 1 })),
            "Link numbers out of their limits are refused");
        check(Rejects(Kind("x", link: new LinkSpec { Kind = EnemyLinks.Ride })) && Rejects(Kind("x", link: new LinkSpec { Kind = EnemyLinks.Ride, Mount = "nobody" })) &&
              !Rejects(Kind("x", link: new LinkSpec { Kind = EnemyLinks.Ride, Mount = "slime" }), Kind("slime")),
            "A ride needs a known mount");
        check(Rejects(Kind("x", link: new LinkSpec { Kind = EnemyLinks.Merge, Into = "x" })) && !Rejects(Kind("x", link: new LinkSpec { Kind = EnemyLinks.Merge, Into = "big" }), Kind("big")),
            "A merge needs a known result that does not merge again");
        check(Rejects(Kind("x", link: new LinkSpec { Kind = EnemyLinks.Aura, Mount = "slime" }), Kind("slime")), "mount/into belong to their own kind of link");
        check(Rejects(new EnemyType { Id = "king", Boss = true, Link = Tether() }), "A boss has no link");

        // A user file switches a link off with "enabled": false (merged over the built-in one).
        const string builtIn = "{\"enemies\":[{\"id\":\"slime\"},{\"id\":\"chain\",\"link\":{\"kind\":\"tether\",\"share\":0.4}},{\"id\":\"king\",\"boss\":true}]}";
        var on = BattleData.Parse(builtIn).Enemy("chain");
        var off = BattleData.ParseWithOverride(builtIn, $"{{\"schemaVersion\":{BattleData.SchemaVersion},\"enemies\":[{{\"id\":\"chain\",\"link\":{{\"enabled\":false}}}}]}}").Enemy("chain");
        check(on.ActiveLink is { Kind: EnemyLinks.Tether, Share: 0.4 } && off.Link is { Kind: EnemyLinks.Tether, Share: 0.4, Enabled: false } && off.ActiveLink == null,
            "\"link\": { \"enabled\": false } in a user file switches the link off and keeps the rest");

        // The built-in battles have no link yet: chapter 1 and every current mode play as before. Only the
        // 1.5 M10 drummer and chain slime carry one, and no battle uses them yet (BattleBandTests).
        check(BattleData.Settings.EnemyTypes.Where(e => e.Link != null).Select(e => e.Id).OrderBy(id => id).SequenceEqual(new[] { "chain", "drummer" }),
            "Only the drummer and the chain slime have a link");
    }
}
