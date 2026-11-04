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
    public static void Run(Action<bool, string> check)
    {
        GrabberChecks(check);
        ScissorsChecks(check);
        SprayChecks(check);
        DataChecks(check);
        RestChecks(check);
    }

    // 2026-11-04 (docs/GOPY-C-2026-11-04.md D4): grab – hold – slam.
    private static void GrabberChecks(Action<bool, string> check)
    {
        var sim = Sim(Kind("small"), Kind("rock", blocker: true, scale: 1.35), Kind("king", boss: true, scale: 2), Kind("big", scale: 1.5), Kind("near"));
        Slime small = sim.Slimes[0], rock = sim.Slimes[1], king = sim.Slimes[2], big = sim.Slimes[3], near = sim.Slimes[4];
        small.X = 600; rock.X = 900; king.X = 1200; big.X = 1450; near.X = 300;
        check(!sim.Grab(small.Id, 600, 10), "The Grabber lifts only once chosen");
        check(sim.Arm(BattleSkill.Grabber) && !sim.Grab(rock.Id, 900, 10) && !sim.Grab(king.Id, 1200, 10) && !sim.Grab(big.Id, 1450, 10) && sim.Armed == BattleSkill.Grabber,
            "Never a rock, a boss or a big enemy (still chosen)");
        check(sim.Grab(small.Id, 600, 10) && sim.Grabbing && small.Grabbed && sim.Armed == null && sim.GrabberBusy, "A small one is grabbed");
        sim.Select(small.Id);
        bool woundUp = false, targeted = false;
        for (int i = 0; i < 60; i++) { sim.Step(dt, 580, false); sim.ClearEvents(); woundUp |= small.State == SlimeState.Windup; targeted |= sim.TargetId == small.Id; }
        check(small.Lift > BattleSim.GrabLift - 1 && Math.Abs(small.X - 600) < 1 && !woundUp && !targeted, $"Lifted out of the fight ({small.Lift:0} DIPs up): it does not attack and the cat does not go for it, even chosen");
        // A click carries it there and slams it: it loses the slam's damage, is dazed; the one near loses the splash.
        double smallHp = small.Hp, nearHp = near.Hp;
        check(sim.GrabDrop(310) && !sim.GrabDrop(500), "A click says where to slam it (once)");
        var seen = new List<BattleEvent>();
        for (int i = 0; i < 120 && (sim.Grabbing || small.Lift > 0); i++) { sim.Step(dt, 100, false); seen.AddRange(sim.Events); sim.ClearEvents(); }
        check(!sim.Grabbing && Math.Abs(small.X - 310) < 1 && small.Lift == 0 && Math.Abs(smallHp - small.Hp - sim.Settings.GrabberSlamDamage) < 1e-6 && small.State == SlimeState.Stunned,
            $"Carried and slammed: {sim.Settings.GrabberSlamDamage} damage and dazed ({smallHp - small.Hp:0})");
        check(Math.Abs(nearHp - near.Hp - sim.Settings.GrabberHitDamage) < 1e-6 && seen.Any(e => e.Kind == BattleEventKind.GrabHit) && sim.Cooldown(BattleSkill.Grabber) > 0,
            $"An enemy beside the spot loses {sim.Settings.GrabberHitDamage}; the cooldown starts once it is put down");

        // Held to the end without a click: put down away from the cat, dazed, no damage.
        sim = Sim(Kind("small"));
        small = sim.Slimes[0];
        small.X = 600;
        sim.Arm(BattleSkill.Grabber); sim.Grab(small.Id, 600, 10);
        smallHp = small.Hp;
        Run(sim, sim.Settings.GrabberHoldSeconds + 0.8);
        check(!sim.Grabbing && small.Lift == 0 && small.X > 600 && small.Hp == smallHp, "Held to the end it is set down away from the cat, unhurt");

        // Lifting the cat drops it, no slam; Pause freezes it.
        sim = Sim(Kind("small"));
        small = sim.Slimes[0];
        small.X = 600;
        sim.Arm(BattleSkill.Grabber); sim.Grab(small.Id, 600, 10);
        sim.Paused = true;
        Run(sim, 0.5);
        check(small.X == 600 && small.Lift == 0 && sim.Grabbing, "Pause freezes the claw");
        sim.Paused = false;
        Run(sim, 0.1, held: true);
        check(!sim.Grabbing && !small.Grabbed && !small.Slammed, "Lifting the cat drops the held enemy without a slam");

        // A thief is never slammed: held to the end, it gives back what it stole and is gone.
        var thiefKind = new EnemyType { Id = "thief", MaxHp = 300, Speed = 0, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3, Steal = BattleData.Settings.Enemy("thief").Steal };
        sim = Sim(thiefKind);
        var thief = sim.Slimes[0];
        thief.X = 600;
        thief.Loot = StolenKind.Skill; thief.LootSkill = BattleSkill.Heal;
        sim.Use(BattleSkill.Heal, 100);
        thief.LootSkill = BattleSkill.Heal;
        sim.Arm(BattleSkill.Grabber); sim.Grab(thief.Id, 600, 10);
        check(!sim.GrabDrop(300), "A thief cannot be slammed");
        int down = sim.Defeated;
        seen.Clear();
        Run(sim, sim.Settings.GrabberHoldSeconds + 0.2, seen: seen);
        check(thief.State == SlimeState.Defeated && sim.Defeated == down + 1 && sim.Cooldown(BattleSkill.Heal) == 0 && seen.Any(e => e.Kind == BattleEventKind.ThiefShaken),
            "Held to the end, the thief shakes out the skill it stole and is gone (the cat's)");
    }

    private static void ScissorsChecks(Action<bool, string> check)
    {
        // 2026-11-04 (D4): a click anywhere: a dashed line first, then the blades run along it; each enemy in the
        // band loses the cut's damage once; a bubble bursts; enemies off the band are untouched.
        {
            var cutSim = Sim(Kind("a"), Kind("b"), Kind("off"));
            Slime a = cutSim.Slimes[0], b = cutSim.Slimes[1], off = cutSim.Slimes[2];
            a.X = 700; b.X = 820; off.X = 1300;
            double ah = a.Hp, bh = b.Hp, oh = off.Hp;
            check(cutSim.Arm(BattleSkill.Scissors) && cutSim.UseOnScreen(BattleSkill.Scissors, 760, 15) && cutSim.Cut != null && cutSim.Armed == null, "Chosen, a click anywhere starts a cut across");
            Run(cutSim, cutSim.Settings.ScissorsPreview * 0.8);
            check(a.Hp == ah && b.Hp == bh, "Nothing is cut while the dashed line shows");
            Run(cutSim, cutSim.Settings.ScissorsSweep + 0.3);
            check(cutSim.Cut == null && Math.Abs(ah - a.Hp - cutSim.Settings.ScissorsDamage) < 1e-6 && Math.Abs(bh - b.Hp - cutSim.Settings.ScissorsDamage) < 1e-6 && off.Hp == oh,
                $"The blades run along the band: both in it lose {cutSim.Settings.ScissorsDamage} once, the far one nothing");
            var high = Sim(Kind("low"));
            var low = high.Slimes[0];
            low.X = 760;
            double lh = low.Hp;
            high.Arm(BattleSkill.Scissors); high.UseOnScreen(BattleSkill.Scissors, 760, 300);
            Run(high, 1.2);
            check(low.Hp == lh, "A cut high up the screen misses a slime on the floor");
        }
        var tether = new LinkSpec { Kind = EnemyLinks.Tether, Range = 300, Share = 0.5, BreakStun = 1, Relink = 30 };
        var sim = Sim(Kind("chain", link: tether), Kind("slime"));
        Slime holder = sim.Slimes[0], ally = sim.Slimes[1];
        holder.X = 1100; ally.X = 900;
        Run(sim, 0.4);
        check(holder.LinkedTo == ally.Id, "(the rope is tied)");
        check(!sim.UseOnScreen(BattleSkill.Scissors, 1000, 18), "The Scissors cut only once chosen");
        double holderHp = holder.Hp, allyHp = ally.Hp;
        check(sim.Arm(BattleSkill.Scissors) && sim.UseOnScreen(BattleSkill.Scissors, 1000, 18) && sim.Armed == null && sim.Cooldown(BattleSkill.Scissors) > 0, "A click cuts; the cooldown starts");
        Run(sim, sim.Settings.ScissorsPreview + sim.Settings.ScissorsSweep + 0.1);
        check(holder.LinkedTo == 0 && sim.RopesCut >= 1, "The cut across the rope ends it");
        check(Math.Abs(holderHp - holder.Hp - sim.Settings.ScissorsDamage) < 0.01 && Math.Abs(allyHp - ally.Hp - sim.Settings.ScissorsDamage) < 0.01,
            $"Each slime in the band loses {sim.Settings.ScissorsDamage} once ({holderHp - holder.Hp}, {allyHp - ally.Hp})");
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
        // 2026-11-04, the hot-water spray: locked on its slime, the mouse neither steers nor stops it.
        check(sim.Spraying && sim.SprayTargetId == near.Id && Math.Abs(sim.SprayX!.Value - near.X) < 1, "Locked on the clicked slime, the mouse free");
        near.X = 700;
        Run(sim, 0.1);
        check(Math.Abs(sim.SprayX!.Value - near.X) < 1 && near.X < 710, "It follows its slime");
        check(sim.SprayTank < 0.75 && sim.SprayTank > 0.6, $"Spraying uses its water ({sim.SprayTank:0.00})");
        // Its slime falls while sprayed: more water, and it moves to the nearest other slime (the king here).
        double before = sim.SprayTank;
        var refilled = new List<BattleEvent>();
        near.Hp = 0.5;
        for (int i = 0; i < 120 && near.State != SlimeState.Defeated; i++) { sim.Step(dt, 100, false); refilled.AddRange(sim.Events); sim.ClearEvents(); }
        Run(sim, dt);
        check(near.State == SlimeState.Defeated && refilled.Any(e => e.Kind == BattleEventKind.SprayRefilled) && sim.SprayTank > before && sim.SprayTargetId == king.Id,
            $"A slime falling to it adds water ({before:0.00} → {sim.SprayTank:0.00}) and the spray moves to the nearest other slime");
        kingHp = king.Hp;
        Run(sim, 1);
        check(Math.Abs(kingHp - king.Hp - sim.Settings.SprayDps * BattleSim.BossToolShare) < 0.6, $"A boss takes half ({kingHp - king.Hp:0.##})");
        var seen = new List<BattleEvent>();
        for (int i = 0; i < 900 && sim.Spraying; i++) { sim.Step(dt, 100, false); seen.AddRange(sim.Events); sim.ClearEvents(); }
        check(!sim.Spraying && sim.Armed == null && sim.SprayTank == 0 && seen.Any(e => e.Kind == BattleEventKind.SpraySpent)
              && Math.Abs(sim.Cooldown(BattleSkill.Spray) - sim.Settings.SprayRefillSeconds) < 0.01, "Out of water it is put away and waits its refill");
        Run(sim, sim.Settings.SprayRefillSeconds + 0.1);
        check(sim.Ready(BattleSkill.Spray) && sim.SprayTank > 0.999, "Refilled, it is ready again");
        check(!far.Wet || far.Hp < farHp, "(the far slime only ever got wet once the spray came to it)");

        // Hot water melts an ice shell.
        var cold = Sim(new EnemyType { Id = "icy", MaxHp = 300, Speed = 0, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3,
            Ice = BattleData.Settings.Enemy("icy").Ice, ThrowHit = BattleData.Settings.Enemy("icy").ThrowHit });
        var ice = cold.Slimes[0];
        ice.X = 800;
        cold.Arm(BattleSkill.Spray); cold.StartSpray(800, 20);
        Run(cold, cold.Settings.SprayMeltSeconds + 0.2);
        check(!ice.Iced && cold.SprayMelts == 1, $"Sprayed {cold.Settings.SprayMeltSeconds} s, an ice shell melts");

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
        check(Rejects("{\"grabberCooldown\":0}") && Rejects("{\"grabberMaxScale\":5}") && Rejects("{\"scissorsLength\":1}") && Rejects("{\"sprayChill\":1}") && Rejects("{\"sprayDps\":-1}"),
            "Out-of-range Grabber, Scissors and Spray numbers are refused");
        check(!Rejects("{\"grabberHitDamage\":12,\"scissorsDamage\":0,\"sprayRange\":120}"), "In-range ones are accepted");
        var d = new BattleSettings();
        var s = BattleData.Settings;
        check(s.GrabberCooldown == d.GrabberCooldown && s.GrabberHoldSeconds == d.GrabberHoldSeconds && s.GrabberMaxScale == d.GrabberMaxScale && s.GrabberHitDamage == d.GrabberHitDamage
              && s.GrabberStun == d.GrabberStun && s.ScissorsCooldown == d.ScissorsCooldown && s.ScissorsDamage == d.ScissorsDamage
              && s.ScissorsLength == d.ScissorsLength && s.SprayTankSeconds == d.SprayTankSeconds && s.SprayRefillSeconds == d.SprayRefillSeconds && s.SprayRange == d.SprayRange
              && s.SprayDps == d.SprayDps && s.SprayChill == d.SprayChill && s.SpraySoakSeconds == d.SpraySoakSeconds, "The shipped battle.json has the built-in Grabber, Scissors and Spray numbers");
        check(BattleSkills.Get(BattleSkill.Grabber).Aim == SkillAim.Drag && BattleSkills.Get(BattleSkill.Scissors).Aim == SkillAim.Screen && BattleSkills.Get(BattleSkill.Spray).Aim == SkillAim.Hold,
            "The Grabber takes a slime, the Scissors are aimed anywhere on the screen, the Spray starts on a press");
        check(BattleSkills.Get(BattleSkill.Grabber).Describe(s, "en").Contains("20 HP") && BattleSkills.Get(BattleSkill.Spray).Describe(s, "vi").Contains("2 máu/giây")
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
