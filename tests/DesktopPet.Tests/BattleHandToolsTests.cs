using DesktopPet;

// 1.5 M06 (PetBattle.Grabber.cs, PetBattle.Tools.cs), with stand-in enemies: the Grabber lifts only a
// small enemy (never a rock, a boss or a big one), the held one does nothing, a fast let-go throws it
// into another (both hurt, the other dazed, a boss half) or the edge, a slow one just drops, it slips
// free after its time, lifting the cat drops it; the Scissors cut a tether rope and hurt the slimes a
// stroke crosses (a short stroke does nothing); the Spray soaks, chills and hurts the slimes near the
// cursor, its water runs out and refills; Pause freezes them; their data limits; the shipped numbers;
// an Expedition rest's free skill change.
internal static class BattleHandToolsTests
{
    const double dt = 1.0 / 60;
    private static readonly BattleSkill[] Hands = { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Grabber, BattleSkill.Scissors, BattleSkill.Spray };

    private static EnemyType Kind(string id, double speed = 0, double hp = 1000, bool boss = false, bool blocker = false, double scale = 1, LinkSpec? link = null) => new()
    {
        Id = id, MaxHp = hp, Speed = speed, Damage = 5, Range = 64, Windup = 0.4, AttackCooldown = 1, Recover = 0.2, Boss = boss, Blocker = blocker, Scale = scale, Link = link
    };

    private static BattleSim Sim(params EnemyType[] kinds)
    {
        var settings = new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1000, MaxAlive = 8, Enemies = kinds,
            Weapons = new[] { new WeaponSpec { Id = "sword", Damage = 10, Knock = 0, StunChance = 0 } },
            Plan = new[] { new WaveSpec { Groups = kinds.Select(k => new WaveGroup { Enemy = k.Id, Count = 1 }).ToArray() } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 7, skills: Hands);
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < kinds.Length; i++) sim.Step(dt, 100, false);
        sim.ClearEvents();
        return sim;
    }

    private static void Run(BattleSim sim, double seconds, double catX = 100, List<BattleEvent>? seen = null, bool held = false)
    {
        for (double t = 0; t < seconds - 1e-9 && !sim.Over; t += dt)
        {
            sim.Step(dt, catX, held, held);
            seen?.AddRange(sim.Events);
            sim.ClearEvents();
        }
    }

    // The cursor carrying the held enemy from x to x + by over these frames, then let go.
    private static void Fling(BattleSim sim, double x, double height, double by, int frames)
    {
        for (int i = 1; i <= frames; i++) { sim.MoveGrab(x + by * i / frames, height); sim.Step(dt, 100, false); sim.ClearEvents(); }
        sim.ReleaseGrab();
    }

    public static void Run(Action<bool, string> check)
    {
        GrabberChecks(check);
        ThrowChecks(check);
        ScissorsChecks(check);
        SprayChecks(check);
        DataChecks(check);
        RestChecks(check);
    }

    private static void GrabberChecks(Action<bool, string> check)
    {
        var sim = Sim(Kind("small"), Kind("rock", blocker: true, scale: 1.35), Kind("king", boss: true, scale: 2), Kind("big", scale: 1.5));
        Slime small = sim.Slimes[0], rock = sim.Slimes[1], king = sim.Slimes[2], big = sim.Slimes[3];
        small.X = 600; rock.X = 900; king.X = 1200; big.X = 1450;
        check(!sim.Grab(small.Id, 600, 10), "The Grabber lifts only once chosen");
        check(sim.Arm(BattleSkill.Grabber) && !sim.Grab(rock.Id, 900, 10) && !sim.Grab(king.Id, 1200, 10) && !sim.Grab(big.Id, 1450, 10) && sim.Armed == BattleSkill.Grabber,
            "Never a rock, a boss or a big enemy (still chosen)");
        check(sim.Grab(small.Id, 600, 10) && sim.Grabbing && small.Grabbed && sim.Armed == null && sim.GrabberBusy, "A small one is lifted");
        for (int i = 0; i < 20; i++) { sim.MoveGrab(600, 10 + i * 3); sim.Step(dt, 100, false); sim.ClearEvents(); }
        check(small.Lift > 40 && Math.Abs(small.X - 600) < 1, $"It hangs where the cursor holds it ({small.Lift:0} DIPs up)");
        // Next to the cat, held, it never winds up and the cat does not go for it.
        bool woundUp = false, targeted = false;
        for (int i = 0; i < 90; i++) { sim.MoveGrab(140, 50); sim.Step(dt, 100, false); sim.ClearEvents(); woundUp |= small.State == SlimeState.Windup; targeted |= sim.TargetId == small.Id; }
        check(!woundUp && !targeted, "Held next to the cat it does not attack, nor is it the cat's target");
        Run(sim, sim.Settings.GrabberHoldSeconds);
        check(!sim.Grabbing && !small.Grabbed && sim.Cooldown(BattleSkill.Grabber) > sim.Settings.GrabberCooldown - sim.Settings.GrabberHoldSeconds - 0.2,
            "Held too long it slips free and the cooldown starts");
        Run(sim, 1);
        check(small.Lift == 0 && !small.Tossed && small.State != SlimeState.Stunned, "Dropped (not thrown) it lands without a daze");

        // Lifting the cat drops it, no throw; Pause freezes it.
        sim = Sim(Kind("small"));
        small = sim.Slimes[0];
        small.X = 600;
        sim.Arm(BattleSkill.Grabber); sim.Grab(small.Id, 600, 10);
        sim.Paused = true;
        sim.MoveGrab(900, 80);
        Run(sim, 0.5);
        check(small.X == 600 && small.Lift == 0 && sim.Grabbing, "Pause freezes the claw");
        sim.Paused = false;
        Run(sim, 0.1, held: true);
        check(!sim.Grabbing && !small.Grabbed && !small.Thrown, "Lifting the cat drops the held enemy without a throw");
    }

    private static void ThrowChecks(Action<bool, string> check)
    {
        var sim = Sim(Kind("small"), Kind("target"), Kind("king", boss: true, scale: 2));
        Slime small = sim.Slimes[0], target = sim.Slimes[1], king = sim.Slimes[2];
        small.X = 500; target.X = 760; king.X = 1400;
        double hp = small.Hp, targetHp = target.Hp, dmg = sim.Settings.GrabberHitDamage;
        var seen = new List<BattleEvent>();
        sim.Arm(BattleSkill.Grabber); sim.Grab(small.Id, 500, 20);
        Fling(sim, 500, 20, 90, 6); // ~900 DIPs/s
        check(small.Thrown && small.Tossed && !sim.Grabbing && Math.Abs(sim.Cooldown(BattleSkill.Grabber) - sim.Settings.GrabberCooldown) < 0.01,
            "A fast let-go throws it; the cooldown starts");
        Run(sim, 1.2, seen: seen);
        check(Math.Abs(hp - small.Hp - dmg) < 0.01 && Math.Abs(targetHp - target.Hp - dmg) < 0.01 && sim.GrabberHits == 1 && seen.Any(e => e.Kind == BattleEventKind.GrabHit),
            $"Thrown into another, both lose {dmg} ({hp - small.Hp}, {targetHp - target.Hp})");
        check(!small.Tossed && small.Lift == 0, "It falls to the floor after the hit");

        // Into a boss: half. Into the edge: hurt alone.
        sim = Sim(Kind("small"), Kind("king", boss: true, scale: 2));
        small = sim.Slimes[0]; king = sim.Slimes[1];
        small.X = 900; king.X = 1250;
        double kingHp = king.Hp;
        sim.Arm(BattleSkill.Grabber); sim.Grab(small.Id, 900, 20);
        Fling(sim, 900, 20, 90, 6);
        Run(sim, 1.2);
        check(Math.Abs(kingHp - king.Hp - dmg * BattleSim.BossToolShare) < 0.01, $"A boss hit by a thrown one takes half ({kingHp - king.Hp})");
        sim = Sim(Kind("small"));
        small = sim.Slimes[0];
        small.X = 1450;
        hp = small.Hp;
        sim.Arm(BattleSkill.Grabber); sim.Grab(small.Id, 1450, 20);
        Fling(sim, 1450, 20, 90, 6);
        Run(sim, 1.2);
        check(Math.Abs(hp - small.Hp - dmg) < 0.01 && small.State == SlimeState.Stunned, $"Thrown into the edge it is hurt ({hp - small.Hp}) and lands dazed");

        // A slow let-go just drops it: no damage to anyone.
        sim = Sim(Kind("small"), Kind("target"));
        small = sim.Slimes[0]; target = sim.Slimes[1];
        small.X = 700; target.X = 760;
        hp = small.Hp; targetHp = target.Hp;
        sim.Arm(BattleSkill.Grabber); sim.Grab(small.Id, 700, 20);
        Fling(sim, 700, 20, 4, 6);
        Run(sim, 1);
        check(!small.Thrown && small.Hp == hp && target.Hp == targetHp && small.State != SlimeState.Stunned, "A slow let-go drops it with no hit");
    }

    private static void ScissorsChecks(Action<bool, string> check)
    {
        var tether = new LinkSpec { Kind = EnemyLinks.Tether, Range = 300, Share = 0.5, BreakStun = 1, Relink = 30 };
        var sim = Sim(Kind("chain", link: tether), Kind("slime"));
        Slime holder = sim.Slimes[0], ally = sim.Slimes[1];
        holder.X = 1100; ally.X = 900;
        Run(sim, 0.4);
        check(holder.LinkedTo == ally.Id, "(the rope is tied)");
        check(!sim.Snip(1000, 60, 1000, 0), "The Scissors cut only once chosen");
        check(sim.Arm(BattleSkill.Scissors) && !sim.Snip(1000, 30, 1000, 10) && sim.Armed == BattleSkill.Scissors, "A stroke too short does nothing (still chosen)");
        double holderHp = holder.Hp, allyHp = ally.Hp;
        var seen = new List<BattleEvent>();
        check(sim.Snip(1000, 60, 1000, 0) && holder.LinkedTo == 0 && sim.RopesCut == 1 && ally.State == SlimeState.Stunned,
            "A stroke across the rope cuts it (the ally is dazed)");
        check(holder.Hp == holderHp && ally.Hp == allyHp && sim.Armed == null && Math.Abs(sim.Cooldown(BattleSkill.Scissors) - sim.Settings.ScissorsCooldown) < 0.01,
            "Between the bodies it hurts no one; the cooldown starts");
        Run(sim, sim.Settings.ScissorsCooldown + 0.1);
        sim.Arm(BattleSkill.Scissors);
        holderHp = holder.Hp; allyHp = ally.Hp;
        check(sim.Snip(850, 20, 1150, 20), "A long stroke along the floor");
        check(Math.Abs(holderHp - holder.Hp - sim.Settings.ScissorsDamage) < 0.01 && Math.Abs(allyHp - ally.Hp - sim.Settings.ScissorsDamage) < 0.01,
            $"Each slime it crosses loses {sim.Settings.ScissorsDamage} once ({holderHp - holder.Hp}, {allyHp - ally.Hp})");
        sim.Paused = true;
        Run(sim, sim.Settings.ScissorsCooldown + 0.1);
        sim.Paused = false;
        check(!sim.Arm(BattleSkill.Scissors), "Pause freezes the cooldown");
    }

    private static void SprayChecks(Action<bool, string> check)
    {
        var sim = Sim(Kind("near", speed: 40), Kind("far"), Kind("king", boss: true, scale: 2));
        Slime near = sim.Slimes[0], far = sim.Slimes[1], king = sim.Slimes[2];
        near.X = 820; far.X = 1300; king.X = 600;
        check(!sim.StartSpray(800), "The Spray works only once chosen");
        double nearHp = near.Hp, farHp = far.Hp, kingHp = king.Hp;
        check(sim.Arm(BattleSkill.Spray) && sim.StartSpray(800, 20) && sim.Spraying, "Chosen, a press on the field starts it");
        Run(sim, 1);
        double nearLost = nearHp - near.Hp;
        check(near.Wet && near.Chilled && !far.Wet && far.Hp == farHp && Math.Abs(nearLost - sim.Settings.SprayDps) < 0.6,
            $"A slime near the cursor is soaked, chilled and loses {sim.Settings.SprayDps} HP a second ({nearLost:0.##}); a far one is not touched");
        sim.MoveSpray(640, 20);
        nearHp = near.Hp;
        Run(sim, 1);
        check(Math.Abs(kingHp - king.Hp - sim.Settings.SprayDps * BattleSim.BossToolShare) < 0.6, $"A boss takes half ({kingHp - king.Hp:0.##})");
        check(sim.SprayTank < 0.6 && sim.SprayTank > 0.4, $"Spraying uses its water ({sim.SprayTank:0.00})");
        sim.StopSpray();
        double tank = sim.SprayTank;
        Run(sim, 0.5);
        check(!sim.Spraying && sim.SprayTank > tank && sim.Armed == BattleSkill.Spray, "Let go, it stops, its water comes back and it stays chosen");
        Run(sim, sim.Settings.SpraySoakSeconds + 0.1);
        check(!near.Wet, "A soaking dries after its time");
        sim.StartSpray(800, 20);
        var seen = new List<BattleEvent>();
        for (int i = 0; i < 600 && sim.Spraying; i++) { sim.Step(dt, 100, false); seen.AddRange(sim.Events); sim.ClearEvents(); }
        check(!sim.Spraying && sim.Armed == null && sim.SprayTank == 0 && seen.Any(e => e.Kind == BattleEventKind.SpraySpent)
              && Math.Abs(sim.Cooldown(BattleSkill.Spray) - sim.Settings.SprayRefillSeconds) < 0.01, "Out of water it is put away and waits its refill");
        Run(sim, sim.Settings.SprayRefillSeconds + 0.1);
        check(sim.Ready(BattleSkill.Spray) && sim.SprayTank > 0.999, "Refilled, it is ready again");

        // Lifting the cat stops it and puts it away; the Fan and the Spray are one choice at a time.
        sim.Arm(BattleSkill.Spray); sim.StartSpray(800, 20);
        Run(sim, 0.1, held: true);
        check(!sim.Spraying && sim.Armed == null, "Lifting the cat stops the Spray and puts it away");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        bool Rejects(string json)
        {
            try { BattleData.Parse(json); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(Rejects("{\"grabberCooldown\":0}") && Rejects("{\"grabberMaxScale\":5}") && Rejects("{\"scissorsMinLength\":1}") && Rejects("{\"sprayChill\":1}") && Rejects("{\"sprayDps\":-1}"),
            "Out-of-range Grabber, Scissors and Spray numbers are refused");
        check(!Rejects("{\"grabberHitDamage\":12,\"scissorsDamage\":0,\"sprayRange\":120}"), "In-range ones are accepted");
        var d = new BattleSettings();
        var s = BattleData.Settings;
        check(s.GrabberCooldown == d.GrabberCooldown && s.GrabberHoldSeconds == d.GrabberHoldSeconds && s.GrabberMaxScale == d.GrabberMaxScale && s.GrabberHitDamage == d.GrabberHitDamage
              && s.GrabberStun == d.GrabberStun && s.GrabberThrowSpeed == d.GrabberThrowSpeed && s.ScissorsCooldown == d.ScissorsCooldown && s.ScissorsDamage == d.ScissorsDamage
              && s.ScissorsMinLength == d.ScissorsMinLength && s.SprayTankSeconds == d.SprayTankSeconds && s.SprayRefillSeconds == d.SprayRefillSeconds && s.SprayRange == d.SprayRange
              && s.SprayDps == d.SprayDps && s.SprayChill == d.SprayChill && s.SpraySoakSeconds == d.SpraySoakSeconds, "The shipped battle.json has the built-in Grabber, Scissors and Spray numbers");
        check(BattleSkills.Get(BattleSkill.Grabber).Aim == SkillAim.Drag && BattleSkills.Get(BattleSkill.Scissors).Aim == SkillAim.Swipe && BattleSkills.Get(BattleSkill.Spray).Aim == SkillAim.Hold,
            "The Grabber drags a slime, the Scissors swipe, the Spray is held");
        check(BattleSkills.Get(BattleSkill.Grabber).Describe(s, "en").Contains("8 HP") && BattleSkills.Get(BattleSkill.Spray).Describe(s, "vi").Contains("2 máu/giây")
              && !s.Skill(BattleSkill.Scissors).Desc("en").Contains("chapter 2"), "The descriptions show the battle's numbers");
    }

    // An Expedition rest: the skills change for free (the heal still to choose); a card for a skill no
    // longer carried leaves the offer; a skill that is not open, four skills or no rest: no change.
    private static void RestChecks(Action<bool, string> check)
    {
        var data = ExpeditionFixture.Fixed;
        BattleSkill[] withBomb = { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Bomb, BattleSkill.Shield };
        BattleSkill[] noBomb = { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Grabber, BattleSkill.Spray };
        // A seed whose step-3 rest offers a card about the Yarn bomb or the shield.
        foreach (int seed in Enumerable.Range(1, 300))
        {
            var x = new Expedition(data, new ExpeditionProgress());
            var run = x.Start(BattleWeapon.Sword, BattleDifficulty.Normal, seed, "rest-" + seed, skills: withBomb);
            check(!x.ChangeSkills(noBomb, _ => true), seed == 1 ? "No rest open: no change" : "(no rest)");
            x.Enter(0); x.Finish(true, 50, new Dictionary<string, int>(), 30, 3);
            x.Enter(0); x.Finish(true, 50, new Dictionary<string, int>(), 30, 3);
            int rest = Enumerable.Range(0, run.Map[run.Step].Length).FirstOrDefault(lane => x.Kind(run.Step, lane) == ExpeditionSpec.Rest, -1);
            if (rest < 0 || !x.Visit(rest)) continue;
            bool skillCard = run.Pending!.Cards.Any(id => data.UpgradeList.First(u => u.Id == id).Skill is "bomb" or "shield");
            if (!skillCard && seed < 300) continue;
            check(!x.ChangeSkills(noBomb.Take(4).ToArray(), _ => true) && !x.ChangeSkills(noBomb, skill => skill != BattleSkill.Spray) && run.Loadout.SequenceEqual(withBomb),
                "Four skills, or one that is not open: no change");
            check(x.ChangeSkills(noBomb, _ => true) && run.Loadout.SequenceEqual(noBomb) && run.Pending is { Kind: "rest" },
                "At a rest the skills change, and the rest's choice stays open");
            check(skillCard && run.Pending!.Cards.All(id => data.UpgradeList.First(u => u.Id == id).Skill is not ("bomb" or "shield")),
                $"A card for a skill no longer carried leaves the offer (seed {seed}: {string.Join(", ", run.Pending!.Cards)})");
            double hp = run.Hp;
            check(x.ChangeSkills(withBomb, _ => true) && run.Loadout.SequenceEqual(withBomb) && run.Hp == hp, "Free, and as often as wanted");
            var healed = x.Choose(Expedition.Heal);
            check(healed.Done && run.Pending == null && run.Hp > hp && !x.ChangeSkills(noBomb, _ => true), "The heal is still taken afterwards; once passed, no more change");
            var saved = new Expedition(data, x.Progress);
            check(saved.Run!.Loadout.SequenceEqual(withBomb), "The run keeps the changed skills");
            return;
        }
        check(false, "No seed with a rest found");
    }
}
