using DesktopPet;

// 1.5 M19 (PetBattle.Drops.cs): drops only where the battle has loot (coins only in an Expedition step);
// a click picks one up (fish: HP, coin: the battle's coins, potion: the longest skill cooldown done);
// it fades after its life, and its clock stands still while paused; at most maxOnFloor; a win gathers
// the coins still lying; the drop roll has its own random stream (a battle without pickups plays the
// same as one without drops). The Expedition run keeps the coins of its won steps (saved, read back,
// an old file reads 0). The shipped numbers and the data limits.
internal static class BattleDropTests
{
    const double dt = 1.0 / 60;

    private static readonly BattleSettings Shipped = BattleData.Settings;
    private static EnemyType Plain(double hp = 10) => new() { Id = "slime", MaxHp = hp, Speed = 60, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3 };
    private static EnemyType Tough() => new() { Id = "tough", MaxHp = 1e9, Speed = 60, Damage = 0, Range = 50, Windup = 0.6, AttackCooldown = 1.5, Recover = 0.3 };
    private static readonly BattleSkill[] Tools = { BattleSkill.Bomb, BattleSkill.Shield, BattleSkill.Push, BattleSkill.Cheer, BattleSkill.Heal };

    // A 400-wide field, the cat at 100 with a weapon that defeats a slime in one hit. Waves: `counts`
    // (tough: the waves after the first send a slime nothing defeats, so the battle goes on).
    private static BattleSim Field(BattleLoot loot, DropSpec? drops, int[]? counts = null, double hit = 1000, int seed = 4, double hp = 10, double startHp = 50, bool tough = false)
    {
        var arms = Shipped.Weapon("sword") with { Range = 60, StunChance = 0, Damage = hit };
        var settings = new BattleSettings
        {
            Weapons = new[] { arms }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 100, MaxAlive = 8, Enemies = new[] { Plain(hp), Tough() }, RageHitGain = 0,
            ShieldCooldown = 20, CheerCooldown = 10, HealCooldown = 5, Drops = drops,
            Plan = (counts ?? new[] { 1, 3 }).Select((n, i) => new WaveSpec { Groups = new[] { new WaveGroup { Enemy = tough && i > 0 ? "tough" : "slime", Count = n } } }).ToArray()
        };
        return new BattleSim(settings, 0, 400, seed: seed, skills: Tools, startHp: startHp, loot: loot);
    }

    private static DropSpec Only(string kind, double chance = 1, double life = 8, int max = 6) =>
        new() { Chance = chance, EliteChance = chance, Life = life, Fade = 1.5, MaxOnFloor = max, Weights = new() { [kind] = 1 }, FishHeal = 10, Coin = 1 };

    // Steps until an event of `kind` (true) or `seconds` passed (false).
    private static bool Until(BattleSim sim, BattleEventKind kind, double seconds = 20)
    {
        for (int i = 0; i < Math.Round(seconds / dt); i++)
        {
            sim.Step(dt, 100, false);
            bool seen = sim.Events.Any(e => e.Kind == kind);
            sim.ClearEvents();
            if (seen) return true;
            if (sim.Over) break;
        }
        return false;
    }

    private static void Steps(BattleSim sim, double seconds) { for (int i = 0; i < Math.Round(seconds / dt); i++) { sim.Step(dt, 100, false); sim.ClearEvents(); } }

    public static void Run(Action<bool, string> check)
    {
        DropChecks(check);
        PickupChecks(check);
        TimeChecks(check);
        ExpeditionChecks(check);
        DataChecks(check);
    }

    private static void DropChecks(Action<bool, string> check)
    {
        var none = Field(BattleLoot.None, Only("fish"));
        Until(none, BattleEventKind.SlimeDefeated);
        Steps(none, 0.5);
        check(none.Defeated >= 1 && none.Drops.Count == 0, "No loot (Adventure chapter 1, free battle): a defeated slime leaves nothing");
        var noData = Field(BattleLoot.Coins, null);
        Until(noData, BattleEventKind.SlimeDefeated);
        check(noData.Drops.Count == 0, "No \"drops\" in the data: nothing drops anywhere");

        var coins = Field(BattleLoot.Coins, Only("coin"));
        check(Until(coins, BattleEventKind.DropAppeared) && coins.Drops.Count == 1 && coins.Drops[0].Kind == BattleDropKind.Coin && coins.Drops[0].Value == 1,
            "Expedition loot: a defeated slime leaves a coin (worth 1)");
        var items = Field(BattleLoot.Items, Only("coin"));
        Until(items, BattleEventKind.SlimeDefeated);
        Steps(items, 0.2);
        check(items.Drops.Count == 0, "Infinity's loot has no coins: with only coins weighted, nothing drops");
        var mixed = Field(BattleLoot.Items, new DropSpec { Chance = 1, Weights = new() { ["coin"] = 10, ["fish"] = 1 } }, new[] { 4, 1 });
        Steps(mixed, 8);
        check(mixed.Defeated >= 4 && mixed.Drops.Count >= 3 && mixed.Drops.All(d => d.Kind == BattleDropKind.Fish), "Infinity: the other kinds still drop by their weights (coins left out)");
        var rare = Field(BattleLoot.Coins, Only("coin", 0), new[] { 4, 1 });
        Steps(rare, 8);
        check(rare.Defeated >= 4 && rare.Drops.Count == 0, "Chance 0: nothing drops");

        var crowd = Field(BattleLoot.Coins, Only("coin", max: 2, life: 30), new[] { 5, 1 });
        Steps(crowd, 8);
        check(crowd.Defeated >= 4 && crowd.Drops.Count == 2, "At most maxOnFloor drops lie on the floor at once (a new one replaces the oldest)");

        // Its own random stream: without pickups a battle with loot plays as one without.
        var plain = Field(BattleLoot.None, Only("coin", 0.5), new[] { 4, 4 }, hit: 6, hp: 12);
        var looted = Field(BattleLoot.Coins, Only("coin", 0.5), new[] { 4, 4 }, hit: 6, hp: 12);
        Steps(plain, 15); Steps(looted, 15);
        check(plain.Defeated == looted.Defeated && plain.Defeated > 0 && plain.Wave == looted.Wave && Math.Abs(plain.CatHp - looted.CatHp) < 1e-9
            && plain.Slimes.Select(s => s.X).SequenceEqual(looted.Slimes.Select(s => s.X)),
            "Drops draw from their own random stream: a battle without pickups plays the same as one without drops");
        var again = Field(BattleLoot.Coins, Only("coin", 0.5), new[] { 4, 4 }, hit: 6, hp: 12);
        Steps(again, 15);
        check(again.Drops.Select(d => d.X).SequenceEqual(looted.Drops.Select(d => d.X)), "The same seed drops the same things in the same places");
    }

    private static void PickupChecks(Action<bool, string> check)
    {
        var coins = Field(BattleLoot.Coins, Only("coin"));
        Until(coins, BattleEventKind.DropAppeared);
        int id = coins.Drops[0].Id;
        check(coins.PickUp(id) && coins.Coins == 1 && coins.Drops.Count == 0 && coins.Events.Any(e => e.Kind == BattleEventKind.DropPicked) && !coins.PickUp(id),
            "A click picks a coin up (once): the battle's coins +1");

        var fish = Field(BattleLoot.Items, Only("fish"), startHp: 50);
        Until(fish, BattleEventKind.DropAppeared);
        double before = fish.CatHp;
        check(fish.PickUp(fish.Drops[0].Id) && Math.Abs(fish.CatHp - (before + 10)) < 1e-9, "A fish: +fishHeal HP");
        var full = Field(BattleLoot.Items, Only("fish"), startHp: 100);
        Until(full, BattleEventKind.DropAppeared);
        check(full.PickUp(full.Drops[0].Id) && full.CatHp == full.CatMaxHp, "A fish at full HP: no HP past the max");

        var potion = Field(BattleLoot.Items, Only("potion"));
        check(potion.Use(BattleSkill.Shield, 100) && potion.Use(BattleSkill.Cheer, 100), "Two skills used (cooling down)");
        Until(potion, BattleEventKind.DropAppeared);
        double cheer = potion.Cooldown(BattleSkill.Cheer);
        check(potion.PickUp(potion.Drops[0].Id) && potion.Cooldown(BattleSkill.Shield) == 0 && Math.Abs(potion.Cooldown(BattleSkill.Cheer) - cheer) < 1e-9,
            "A potion: the carried skill with the longest cooldown left is ready at once (the other keeps its time)");
        var idle = Field(BattleLoot.Items, Only("potion"));
        Until(idle, BattleEventKind.DropAppeared);
        check(idle.PickUp(idle.Drops[0].Id) && Tools.All(s => idle.Cooldown(s) == 0), "A potion with nothing cooling down: picked up, nothing else changes");

        var paused = Field(BattleLoot.Coins, Only("coin"));
        Until(paused, BattleEventKind.DropAppeared);
        paused.Paused = true;
        check(!paused.PickUp(paused.Drops[0].Id) && paused.Coins == 0 && paused.Drops.Count == 1, "Paused: a drop cannot be picked up");

        // A win gathers the coins still on the floor; a loss leaves the battle's coins as they were.
        var win = Field(BattleLoot.Coins, Only("coin", life: 30), new[] { 2 });
        Until(win, BattleEventKind.Won, 30);
        check(win.Phase == BattlePhase.Won && win.Coins == 2 && win.Drops.Count == 0, "A won battle gathers the coins left on the floor");
    }

    private static void TimeChecks(Action<bool, string> check)
    {
        var sim = Field(BattleLoot.Coins, Only("coin", life: 8), new[] { 1, 1 }, tough: true);
        Until(sim, BattleEventKind.DropAppeared);
        var drop = sim.Drops[0];
        Steps(sim, 7.8);
        check(sim.Drops.Contains(drop) && drop.Opacity(1.5) < 1, "Near the end of its life the drop is still there, fading");
        check(new BattleDrop { Life = 8, Age = 2 }.Opacity(1.5) == 1, "Before its last fade seconds a drop shows fully");
        sim.Paused = true;
        double age = drop.Age;
        Steps(sim, 20);
        check(sim.Drops.Contains(drop) && drop.Age == age, "Pause freezes a drop's clock (it waits, not fading)");
        sim.Paused = false;
        check(Until(sim, BattleEventKind.DropFaded, 1) && !sim.Drops.Contains(drop) && Math.Abs(drop.Age - 8) < 2 * dt, "Left alone it is gone once its life (8 s of battle time) is over");
        check(sim.Coins == 0, "A faded coin is not counted");
    }

    private static void ExpeditionChecks(Action<bool, string> check)
    {
        var data = ExpeditionFixture.Fixed;
        var expedition = new Expedition(data, new ExpeditionProgress());
        var run = expedition.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 42, "run-coins");
        check(run.Coins == 0, "A new run has no coins");
        expedition.Enter(0);
        var won = expedition.Finish(true, 70, new Dictionary<string, int>(), 30, 4, coins: 5);
        check(won.Counted && run.Coins == 5 && won.Coins == 5 && won.CoinsGained == 5, "A won step adds the coins its battle picked up to the run");
        expedition.Enter(0);
        expedition.Finish(true, 70, new Dictionary<string, int>(), 30, 4, coins: -3);
        check(run.Coins == 5, "Negative coins change nothing");
        string file = Path.Combine(Path.GetTempPath(), $"desktop-pet-drops-{Guid.NewGuid():N}.json");
        try
        {
            check(expedition.Progress.Save(file) && ExpeditionProgress.Load(file).Active?.Coins == 5, "The run's coins are saved and read back");
            var saved = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!;
            static void Strip(System.Text.Json.Nodes.JsonNode? node)
            {
                if (node is System.Text.Json.Nodes.JsonObject o)
                {
                    foreach (var key in o.Select(p => p.Key).Where(k => k.Equals("coins", StringComparison.OrdinalIgnoreCase)).ToList()) o.Remove(key);
                    foreach (var p in o) Strip(p.Value);
                }
                else if (node is System.Text.Json.Nodes.JsonArray a) foreach (var item in a) Strip(item);
            }
            Strip(saved);
            File.WriteAllText(file, saved.ToJsonString());
            File.Delete(file + ".bak");
            check(ExpeditionProgress.Load(file).Active is { RunId: "run-coins", Coins: 0 }, "An older run file without coins reads as 0 coins");
        }
        finally { foreach (var f in new[] { file, file + ".bak", file + ".tmp" }) if (File.Exists(f)) File.Delete(f); }
        expedition.Enter(1); // Step 3: a rest and an elite.
        var lost = expedition.Finish(false, 0, new Dictionary<string, int>(), 30, 4, coins: 7);
        check(lost.RunOver && expedition.Run == null, "A lost step ends the run (its coins go with it: never kept past the run)");
        var next = expedition.Start(BattleWeapon.Sword, BattleDifficulty.Normal, 43, "run-next");
        check(next.Coins == 0, "The next run starts again with no coins");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        var d = Shipped.Drops;
        check(d is { Chance: 0.2, EliteChance: 0.6, BossDrops: 3, Life: 8, Fade: 1.5, MaxOnFloor: 6, FishHeal: 10, Coin: 1, EliteCoin: 2, BossCoin: 3 }
            && d.Weights["fish"] == 3 && d.Weights["coin"] == 5 && d.Weights["potion"] == 2,
            "Shipped drops: 20% (elite 60%), boss 3 coins, 8 s (1.5 s fade), 6 on the floor, fish 10 HP, coins 1/2/3, weights 3/5/2");
        bool Rejects(string drops)
        {
            try { BattleData.ParseWithOverride(ExpeditionFixture.Embedded(), "{ \"schemaVersion\": 1, \"drops\": " + drops + " }"); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(Rejects("{ \"chance\": 1.5 }") && Rejects("{ \"life\": 1 }") && Rejects("{ \"fade\": 9 }") && Rejects("{ \"weights\": { \"gem\": 1 } }")
            && Rejects("{ \"maxOnFloor\": 0 }") && Rejects("{ \"coin\": 0 }"),
            "Drop data out of range or an unknown kind is refused");
        check(!Rejects("{ \"chance\": 0.5, \"weights\": { \"fish\": 1 } }"), "A valid drops override is read");
    }
}
