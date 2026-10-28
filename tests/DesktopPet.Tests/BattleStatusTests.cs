using DesktopPet;

// 1.5 M04 (PetBattle.Status.cs), with stand-in enemies: poison's damage over its time, its layers and
// their limit, never past a shield or a heal's guard, through a rock's block, a defeat by it counted;
// chill slows walking and the windup, keeps the stronger slow; stick holds an enemy in place (no
// walking, knock back or shove) while it still hits, then a short immunity; a boss takes half of each;
// Pause freezes them; a weapon's and a card's onHit put them on; the data limits; none built in yet.
internal static class BattleStatusTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id, double speed = 0, double damage = 0, double hp = 1000, bool boss = false, double block = 0) => new()
    {
        Id = id, MaxHp = hp, Speed = speed, Damage = damage, Range = 64, Windup = 0.4, AttackCooldown = 1, Recover = 0.2, Boss = boss,
        BlockChance = block, BlockMaxStreak = block > 0 ? 100 : 2
    };

    private static BattleSim Sim(EnemyType[] kinds, WeaponSpec? weapon = null, UpgradeSpec[]? cards = null)
    {
        var settings = new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1000, MaxAlive = 8, Enemies = kinds,
            Weapons = new[] { weapon ?? new WeaponSpec { Id = "sword", Damage = 10, Knock = 0, StunChance = 0 } },
            Upgrades = cards,
            Plan = new[] { new WaveSpec { Groups = kinds.Select(k => new WaveGroup { Enemy = k.Id, Count = 1 }).ToArray() } }
        };
        var build = cards?.ToDictionary(c => c.Id, _ => 1);
        var sim = new BattleSim(settings, 0, 1600, seed: 7, build: build);
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < kinds.Length; i++) sim.Step(dt, 100, false);
        sim.ClearEvents();
        return sim;
    }

    private static void Run(BattleSim sim, double seconds, double catX = 100, List<BattleEvent>? seen = null)
    {
        for (double t = 0; t < seconds - 1e-9 && !sim.Over; t += dt)
        {
            sim.Step(dt, catX, false);
            seen?.AddRange(sim.Events);
            sim.ClearEvents();
        }
    }

    public static void Run(Action<bool, string> check)
    {
        PoisonChecks(check);
        ChillChecks(check);
        StickChecks(check);
        PauseAndBossChecks(check);
        OnHitChecks(check);
        DataChecks(check);
    }

    private static void PoisonChecks(Action<bool, string> check)
    {
        var sim = Sim(new[] { Kind("slime") });
        var slime = sim.Slimes[0];
        slime.X = 1200;
        var seen = new List<BattleEvent>();
        check(sim.ApplyStatus(slime, SlimeStatus.Poison, 4, 10) && slime.Poisoned && slime.PoisonLayers == 1, "Poison takes");
        int ticks = 0;
        for (int i = 0; i < 120; i++)
        {
            sim.Step(dt, 100, false);
            sim.ClearEvents();
            ticks += sim.Effects.Count(e => e.Kind == BattleEffectKind.Number && e.Tone == NumberTone.Poison && e.Age == 0);
        }
        check(Math.Abs(slime.MaxHp - slime.Hp - 20) < 0.01, $"Poison deals its power each second ({slime.MaxHp - slime.Hp} after 2 s of 10/s)");
        check(ticks == 4, $"It ticks every {BattleSim.PoisonTick} s with a poison-coloured number ({ticks} in 2 s)");
        Run(sim, 2.5);
        check(Math.Abs(slime.MaxHp - slime.Hp - 40) < 0.01 && !slime.Poisoned && slime.PoisonLayers == 0,
            $"It ends on time having dealt power × seconds ({slime.MaxHp - slime.Hp})");

        // Layers: each adds its own power, at most three; the longer time stays.
        slime.Hp = slime.MaxHp;
        sim.ApplyStatus(slime, SlimeStatus.Poison, 3, 10);
        sim.ApplyStatus(slime, SlimeStatus.Poison, 1, 10);
        check(slime.PoisonLayers == 2 && slime.PoisonDps == 20 && Math.Abs(slime.PoisonRemaining - 3) < 1e-9, "A second poison adds a layer and keeps the longer time");
        sim.ApplyStatus(slime, SlimeStatus.Poison, 5, 10);
        sim.ApplyStatus(slime, SlimeStatus.Poison, 5, 10);
        check(slime.PoisonLayers == BattleSim.PoisonMaxLayers && slime.PoisonDps == 30 && Math.Abs(slime.PoisonRemaining - 5) < 1e-9,
            "At most three layers; a longer one lengthens the time");
        Run(sim, 5.2);
        check(Math.Abs(slime.MaxHp - slime.Hp - 150) < 0.01, $"Three layers deal three times the power ({slime.MaxHp - slime.Hp} over 5 s)");

        // Behind a shield or a heal's guard: nothing lost, the time still runs, no IMMUNE.
        slime.Hp = slime.MaxHp;
        sim.ApplyStatus(slime, SlimeStatus.Poison, 2, 10);
        slime.WardUntil = sim.Time + 10;
        seen.Clear();
        Run(sim, 2.2, seen: seen);
        check(slime.Hp == slime.MaxHp && !slime.Poisoned && !seen.Any(e => e.Kind == BattleEventKind.HitWarded), "A guarded enemy loses nothing to poison and shows no IMMUNE");
        slime.WardUntil = -1;

        // A rock's block never stops it; a defeat by it counts.
        var rockSim = Sim(new[] { Kind("rock", block: 1, hp: 30) });
        var rock = rockSim.Slimes[0];
        rock.X = 1200;
        rockSim.ApplyStatus(rock, SlimeStatus.Poison, 10, 10);
        rockSim.ApplyStatus(rock, SlimeStatus.Chill, 10, 0.5);
        int defeated = rockSim.Defeated;
        Run(rockSim, 3.2);
        check(rockSim.BlocksMade == 0 && rock.State == SlimeState.Defeated && rockSim.Defeated == defeated + 1, "Poison goes through a rock's block and its defeat counts");
        check(!rock.Poisoned && !rock.Chilled && rock.PoisonLayers == 0, "A defeat clears the statuses");
        check(!rockSim.ApplyStatus(rock, SlimeStatus.Poison, 3, 10), "No status on a defeated enemy");
    }

    private static void ChillChecks(Action<bool, string> check)
    {
        var sim = Sim(new[] { Kind("walker", speed: 100) });
        var slime = sim.Slimes[0];
        slime.X = 1200;
        Run(sim, 1);
        double plain = 1200 - slime.X;
        slime.X = 1200;
        sim.ApplyStatus(slime, SlimeStatus.Chill, 3, 0.5);
        Run(sim, 1);
        double slowed = 1200 - slime.X;
        check(plain > 50 && Math.Abs(slowed - plain / 2) < 3, $"Chill 0.5 halves its walk ({plain:0} → {slowed:0} DIPs in 1 s)");
        sim.ApplyStatus(slime, SlimeStatus.Chill, 1, 0.3);
        check(slime.ChillPower == 0.5 && Math.Abs(slime.ChillRemaining - 2) < 0.02, "A weaker, shorter chill changes nothing");
        sim.ApplyStatus(slime, SlimeStatus.Chill, 1, 0.7);
        check(slime.ChillPower == 0.7 && Math.Abs(slime.ChillRemaining - 2) < 0.02, "A stronger chill takes over, the longer time stays");
        Run(sim, 2.1);
        check(!slime.Chilled && slime.ChillPower == 0 && slime.ChillFactor == 1, "Chill ends on time");

        // The windup: 0.4 s, twice as long at half speed.
        var hitter = Sim(new[] { Kind("hitter", damage: 5) });
        var h = hitter.Slimes[0];
        h.X = 140;
        double Windup(BattleSim s, Slime e)
        {
            for (int i = 0; i < 600 && e.State != SlimeState.Windup; i++) { s.Step(dt, 100, false); s.ClearEvents(); }
            double t = 0;
            while (e.State == SlimeState.Windup && t < 5) { s.Step(dt, 100, false); s.ClearEvents(); t += dt; }
            return t;
        }
        double normal = Windup(hitter, h);
        hitter.ApplyStatus(h, SlimeStatus.Chill, 10, 0.5);
        double chilled = Windup(hitter, h);
        check(Math.Abs(normal - 0.4) < 0.04 && Math.Abs(chilled - 0.8) < 0.04, $"Chill 0.5 doubles the windup ({normal:0.00} → {chilled:0.00} s)");
    }

    private static void StickChecks(Action<bool, string> check)
    {
        var sim = Sim(new[] { Kind("walker", speed: 100) });
        var slime = sim.Slimes[0];
        slime.X = 1200;
        check(sim.ApplyStatus(slime, SlimeStatus.Stick, 1), "Stick takes");
        slime.KnockVelocity = 300;
        Run(sim, 0.9);
        check(slime.X == 1200 && slime.Stuck, "A stuck enemy neither walks nor is knocked back");
        Run(sim, 0.2);
        check(!slime.Stuck && slime.StickImmune > 0 && !sim.ApplyStatus(slime, SlimeStatus.Stick, 1), "Once free it cannot be stuck again at once");
        Run(sim, 0.4);
        check(slime.X < 1200, "Then it walks again");
        Run(sim, BattleSim.StickImmuneSeconds);
        check(sim.ApplyStatus(slime, SlimeStatus.Stick, 1), $"After {BattleSim.StickImmuneSeconds} s it can be stuck again");
        sim.ApplyStatus(slime, SlimeStatus.Stick, 0.3);
        check(Math.Abs(slime.StuckRemaining - 1) < 1e-9, "A shorter stick does not shorten it");

        // Still hits the cat in reach.
        var hitter = Sim(new[] { Kind("hitter", damage: 5) });
        var h = hitter.Slimes[0];
        h.X = 150;
        hitter.ApplyStatus(h, SlimeStatus.Stick, 3);
        double hp = hitter.CatHp;
        Run(hitter, 2.5);
        check(hitter.CatHp < hp && h.X == 150, "A stuck enemy still hits a cat in reach, without moving");

        // Not shoved by the others.
        var pair = Sim(new[] { Kind("a"), Kind("b") });
        var a = pair.Slimes.Single(s => s.Type.Id == "a");
        var b = pair.Slimes.Single(s => s.Type.Id == "b");
        a.X = 800; b.X = 805;
        pair.ApplyStatus(a, SlimeStatus.Stick, 2);
        Run(pair, 0.1, catX: 100);
        check(a.X == 800 && b.X > 820, $"Others make room round a stuck enemy ({a.X:0}, {b.X:0})");
    }

    private static void PauseAndBossChecks(Action<bool, string> check)
    {
        var sim = Sim(new[] { Kind("slime") });
        var slime = sim.Slimes[0];
        slime.X = 1200;
        sim.ApplyStatus(slime, SlimeStatus.Poison, 3, 10);
        sim.ApplyStatus(slime, SlimeStatus.Chill, 3, 0.5);
        sim.ApplyStatus(slime, SlimeStatus.Stick, 2);
        Run(sim, 1);
        double hp = slime.Hp, poison = slime.PoisonRemaining, chill = slime.ChillRemaining, stuck = slime.StuckRemaining;
        sim.Paused = true;
        Run(sim, 3);
        check(slime.Hp == hp && slime.PoisonRemaining == poison && slime.ChillRemaining == chill && slime.StuckRemaining == stuck,
            "Pause freezes every status (time and poison)");
        sim.Paused = false;
        Run(sim, 0.5);
        check(slime.PoisonRemaining < poison && slime.Hp < hp, "They go on after Pause");

        var bossSim = Sim(new[] { Kind("king", boss: true) });
        var king = bossSim.Slimes[0];
        king.X = 1200;
        bossSim.ApplyStatus(king, SlimeStatus.Poison, 2, 10);
        bossSim.ApplyStatus(king, SlimeStatus.Chill, 2, 0.6);
        bossSim.ApplyStatus(king, SlimeStatus.Stick, 2);
        check(king.PoisonDps == 5 && Math.Abs(king.ChillPower - 0.3) < 1e-9 && king.StuckRemaining == 1,
            "A boss takes half the poison, half the chill and half as long a stick");
        Run(bossSim, 2.1);
        check(Math.Abs(king.MaxHp - king.Hp - 10) < 0.01, $"Boss poison over its time ({king.MaxHp - king.Hp})");
        king.Shielded = true;
        check(!bossSim.ApplyStatus(king, SlimeStatus.Poison, 2, 10), "No status on a boss behind its shield");
    }

    private static void OnHitChecks(Action<bool, string> check)
    {
        // The weapon's own onHit (chance 1): its first landed hit poisons.
        var weapon = new WeaponSpec { Id = "fang", Damage = 10, Knock = 0, StunChance = 0, OnHit = new StatusHit { Status = "poison", Seconds = 3, Power = 4 } };
        var sim = Sim(new[] { Kind("slime") }, weapon);
        var slime = sim.Slimes[0];
        slime.X = 150;
        var seen = new List<BattleEvent>();
        for (int i = 0; i < 300 && !slime.Poisoned; i++) { sim.Step(dt, 100, false); seen.AddRange(sim.Events); sim.ClearEvents(); }
        check(slime.Poisoned && seen.Any(e => e.Kind == BattleEventKind.StatusApplied && e.Value == (int)SlimeStatus.Poison) && sim.StatusesApplied(SlimeStatus.Poison) == 1,
            "A weapon's onHit poisons on a landed hit (StatusApplied event)");

        // A card: effect onHit, its amount the chance.
        var glue = new UpgradeSpec { Id = "glue", NameVi = "Keo", NameEn = "Glue", Effect = "onHit", Amount = 1, OnHit = new StatusHit { Status = "stick", Seconds = 1 } };
        var carded = Sim(new[] { Kind("slime") }, cards: new[] { glue });
        var stuck = carded.Slimes[0];
        stuck.X = 150;
        for (int i = 0; i < 300 && !stuck.Stuck; i++) { carded.Step(dt, 100, false); carded.ClearEvents(); }
        check(stuck.Stuck, "An onHit card sticks on a landed hit");

        // No onHit: nothing.
        var plainSim = Sim(new[] { Kind("slime") });
        var plain = plainSim.Slimes[0];
        plain.X = 150;
        Run(plainSim, 3);
        check(plain.Hp < plain.MaxHp && !plain.Poisoned && !plain.Chilled && !plain.Stuck, "Without onHit the hits leave no status");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        bool Rejects(string json)
        {
            try { BattleData.Parse(json); return false; }
            catch (InvalidDataException) { return true; }
        }
        string Weapon(string onHit) => "{\"weapons\":[{\"id\":\"fang\",\"onHit\":" + onHit + "}]}";
        string Card(string extra) => "{\"upgrades\":[{\"id\":\"glue\",\"nameVi\":\"Keo\",\"nameEn\":\"Glue\",\"descVi\":\"\",\"descEn\":\"\"," + extra + "}]}";
        var parsed = BattleData.Parse(Weapon("{\"status\":\"chill\",\"chance\":0.3,\"seconds\":2,\"power\":0.4}")).Weapon("fang");
        check(parsed.OnHit is { Status: "chill", Chance: 0.3, Seconds: 2, Power: 0.4 }, "battle.json reads a weapon's onHit");
        check(!Rejects(Weapon("{\"status\":\"poison\",\"seconds\":3,\"power\":5}")) && !Rejects(Weapon("{\"status\":\"stick\",\"seconds\":1}")), "Valid onHit entries are accepted");
        check(Rejects(Weapon("{\"status\":\"burn\",\"seconds\":3,\"power\":5}")) && Rejects(Weapon("{\"status\":\"poison\",\"seconds\":3}"))
            && Rejects(Weapon("{\"status\":\"chill\",\"seconds\":2,\"power\":0.95}")) && Rejects(Weapon("{\"status\":\"stick\",\"seconds\":1,\"power\":1}"))
            && Rejects(Weapon("{\"status\":\"stick\",\"seconds\":9}")) && Rejects(Weapon("{\"status\":\"poison\",\"seconds\":3,\"power\":5,\"chance\":0}")),
            "An unknown status, a missing or out-of-range power or time, or a zero chance is refused");
        check(!Rejects(Card("\"effect\":\"onHit\",\"amount\":0.25,\"onHit\":{\"status\":\"poison\",\"seconds\":3,\"power\":4}")), "An onHit card is accepted");
        check(Rejects(Card("\"effect\":\"onHit\",\"amount\":0.25")) && Rejects(Card("\"effect\":\"damage\",\"amount\":0.1,\"onHit\":{\"status\":\"stick\",\"seconds\":1}"))
            && Rejects(Card("\"effect\":\"onHit\",\"amount\":0.25,\"onHit\":{\"status\":\"stick\",\"seconds\":1,\"chance\":0.5}")),
            "An onHit card needs its object, only it may have one, and its chance is its amount");
        check(BattleData.Settings.WeaponList.All(w => w.OnHit == null) && BattleData.Settings.UpgradeList.All(u => u.OnHit == null),
            "No built-in weapon or card leaves a status yet");
    }
}
