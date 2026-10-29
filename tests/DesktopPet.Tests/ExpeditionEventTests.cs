using System.Reflection;
using DesktopPet;

// 1.5 M23b (docs/UPDATE-1.5.md X2, X4; docs/BATTLE-DATA.md → Sự kiện và di vật mới): the forge, the curse
// and the lost cat on the Expedition map, the six newer relics in a battle, and a measure of each relic over
// whole simulated runs. Simulated only (NOT real play). Temporary files only.
internal static class ExpeditionEventTests
{
    private const double dt = 1.0 / 30;

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        DataChecks(check, data);
        ForgeChecks(check, data);
        CurseChecks(check, data);
        LostChecks(check, data);
        FileChecks(check, data);
        RelicChecks(check, data);
        Validation(check);
        Measure(check, data, null);
        Measure(check, data, "dump_trail");
    }

    // A run standing at step 1 (index 1, so a shop or rest could be there) with the given event as its only node.
    private static (Expedition X, ExpeditionRun Run) AtEvent(BattleSettings data, string eventId, int seed = 3, string? route = null)
    {
        var x = new Expedition(data, new ExpeditionProgress(), route);
        var run = x.Start(BattleWeapon.Sword, BattleDifficulty.Normal, seed);
        x.Roll();
        run.Kinds![0] = new[] { ExpeditionSpec.Event };
        run.Map[0] = new[] { eventId };
        return (x, run);
    }

    private static void DataChecks(Action<bool, string> check, BattleSettings data)
    {
        var main = data.Expedition!;
        var kinds = main.Events.Select(e => e.Kind).ToHashSet();
        check(new[] { ExpeditionEventSpec.Forge, ExpeditionEventSpec.Curse, ExpeditionEventSpec.Lost }.All(kinds.Contains) && main.Events.Length == 6,
            "Built-in: six events, with the forge, the curse and the lost kitten");
        var effects = main.Relics.Select(r => r.Effect).ToList();
        check(main.Relics.Length == 10 && new[] { RelicSpec.Magnet, RelicSpec.Thorns, RelicSpec.Venom, RelicSpec.Frost, RelicSpec.Battery, RelicSpec.Helmet }.All(effects.Contains),
            "Built-in: ten relics, with the coin magnet, thorn collar, venom fang, frost stone, spare battery and helmet");
        check(main.All.All(r => r.Events == main.Events && r.Relics == main.Relics), "Both routes have the new events and relics");
        var forge = main.Events.First(e => e.Kind == ExpeditionEventSpec.Forge);
        var curse = main.Events.First(e => e.Kind == ExpeditionEventSpec.Curse);
        var lost = main.Events.First(e => e.Kind == ExpeditionEventSpec.Lost);
        check(forge is { Coins: 4, Cost: 0.15, Amount: 0.15, Levels: 2 } && curse is { Cards: 2, Amount: 0.25, Battles: 2, EliteChance: 0.15 } && lost is { Cost: 0.1, Coins: 3, Cards: 3 },
            "The approved numbers: forge 4 coins or 15% HP for +15% (twice), curse 2 rares for +25% over 2 battles, lost kitten 10% HP for 3 coins and a card of three");
    }

    private static void ForgeChecks(Action<bool, string> check, BattleSettings data)
    {
        var (x, run) = AtEvent(data, "cat_forge");
        check(x.Visit(0) && run.Pending is { Kind: ExpeditionChoice.EventKind, Event: "cat_forge" } && run.Pending.Cards.Length == 0 && run.Pending.Relics.Length == 0
              && x.Options().SequenceEqual(new[] { Expedition.PayCoins, Expedition.PayHp, Expedition.Leave }),
            "Forge: pay in coins, pay in HP, or move on; nothing to pick");
        double max = x.MaxHp(run);
        check(x.Refusal(Expedition.PayCoins) == "too poor" && x.Refusal(Expedition.PayHp) == null && Math.Abs(x.Cost(Expedition.PayHp) - 0.15 * max) < 1e-9 && x.CoinCost(Expedition.PayCoins) == 4,
            "Forge: with no coins only HP pays (15% of the max)");
        var hpPaid = x.Choose(Expedition.PayHp);
        check(hpPaid.Done && run.Forged == 1 && Math.Abs(run.WeaponBonus - 0.15) < 1e-9 && Math.Abs(run.Hp - 0.85 * max) < 1e-9 && run.Step == 1 && run.Log[^1].Option == Expedition.PayHp,
            $"Forge with HP: one level, +15%, −15% HP, the step passed and logged (HP {run.Hp:0}/{max:0})");
        var node = x.Node(0);
        run.Kinds![1] = new[] { ExpeditionSpec.Fight };
        run.Map[1] = new[] { x.Spec.Fights[0].Id };
        node = x.Node(0);
        check(node is { WeaponBonus: 0.15, Cursed: false, EliteBonus: 0 }, "The next battle carries the forge's bonus");
        var plain = new BattleSim(data, 0, 1600, seed: 1);
        var forged = new BattleSim(data, 0, 1600, seed: 1, weaponBonus: node!.Value.WeaponBonus);
        check(Math.Abs(forged.SwingDamage - plain.SwingDamage * 1.15) < 1e-9 && forged.DamageFactor == plain.DamageFactor,
            $"In the battle swings deal +15% ({plain.SwingDamage:0.##} → {forged.SwingDamage:0.##}); the cards' damage factor (apps, poison) is untouched");

        // A second forge with coins, then the forge is used up.
        run.Kinds[1] = new[] { ExpeditionSpec.Event };
        run.Map[1] = new[] { "cat_forge" };
        run.Coins = 5;
        x.Visit(0);
        var coinsPaid = x.Choose(Expedition.PayCoins);
        check(coinsPaid.Done && coinsPaid.Coins == -4 && run.Coins == 1 && run.Forged == 2 && Math.Abs(run.WeaponBonus - 0.3) < 1e-9 && run.Log[^1].Coins == 4,
            "Forge with coins: −4 coins, a second level (+30% in all), logged with its price");
        run.Kinds[2] = new[] { ExpeditionSpec.Event };
        run.Map[2] = new[] { "cat_forge" };
        run.Coins = 9;
        x.Visit(0);
        check(x.Refusal(Expedition.PayCoins) == "maxed" && x.Refusal(Expedition.PayHp) == "maxed" && x.Refusal(Expedition.Leave) == null && x.Choose(Expedition.Leave).Done && run.Forged == 2 && run.Coins == 9,
            "A third forge is refused (at most 2 levels); moving on costs nothing");

        // No forge is drawn once the run cannot use it any more (over many seeds).
        bool none = true, some = false;
        for (int seed = 1; seed <= 40; seed++)
        {
            var e = new Expedition(data, new ExpeditionProgress());
            var r = e.Start(BattleWeapon.Sword, BattleDifficulty.Normal, seed);
            r.Forged = 2;
            for (int step = 0; step < e.Spec.PathSteps; step++)
            {
                r.Path.Clear(); r.Path.AddRange(Enumerable.Repeat(0, step));
                e.Roll();
                none &= !r.Map[step].Contains("cat_forge");
            }
            var fresh = new Expedition(data, new ExpeditionProgress());
            var f = fresh.Start(BattleWeapon.Sword, BattleDifficulty.Normal, seed);
            for (int step = 0; step < fresh.Spec.PathSteps; step++)
            {
                f.Path.Clear(); f.Path.AddRange(Enumerable.Repeat(0, step));
                fresh.Roll();
                some |= f.Map[step].Contains("cat_forge");
            }
        }
        check(none && some, "A run forged twice never draws the forge again (40 seeds); a fresh run does");
    }

    private static void CurseChecks(Action<bool, string> check, BattleSettings data)
    {
        var (x, run) = AtEvent(data, "slime_curse");
        check(x.Visit(0) && x.Options().SequenceEqual(new[] { Expedition.Card, Expedition.Leave }) && run.Pending!.Cards.Length == 2
              && run.Pending.Cards.All(id => data.UpgradeList.First(u => u.Id == id).Rare) && run.Pending.Cards.Distinct().Count() == 2,
            "Curse: two different rare cards to pick from, or move on");
        string card = run.Pending.Cards[1];
        check(x.Refusal(Expedition.Card) == "pick a card" && x.Cost(Expedition.Card) == 0, "Curse: a card must be picked; it costs no HP");
        double hp = run.Hp;
        check(x.Choose(Expedition.Card, card).Done && run.Build.GetValueOrDefault(card) == 1 && run.Cursed == 2 && run.CurseThreat == 0.25 && run.CurseElite == 0.15 && run.Hp == hp,
            "Taking it: the rare card goes in the build, the next 2 battles are cursed");
        run.Kinds![1] = new[] { ExpeditionSpec.Fight };
        run.Map[1] = new[] { x.Spec.Fights[0].Id };
        var cursed = x.Node(0)!.Value;
        check(cursed.Cursed && Math.Abs(cursed.Threat - x.Spec.Threat(1) * 1.25) < 1e-9 && cursed.EliteBonus == 0.15,
            $"A cursed battle: enemies ×1.25 (×{cursed.Threat:0.000}) and +15% elite chance");
        x.Enter(0);
        x.Finish(true, run.Hp, run.Build, 30, 3);
        check(run.Cursed == 1, "A won battle is one cursed battle less");
        if (run.Pending != null) x.Choose(Expedition.Leave);
        run.Kinds[2] = new[] { ExpeditionSpec.Fight };
        run.Map[2] = new[] { x.Spec.Fights[1].Id };
        check(x.Node(0)!.Value.Cursed, "The second battle is cursed too");
        x.Enter(0);
        x.Retreat();
        check(run.Cursed == 1, "A battle left (retreat) does not use the curse up");
        x.Enter(0);
        x.Finish(true, run.Hp, run.Build, 30, 3);
        if (run.Pending != null) x.Choose(Expedition.Leave);
        run.Kinds[3] = new[] { ExpeditionSpec.Fight };
        run.Map[3] = new[] { x.Spec.Fights[2].Id };
        var after = x.Node(0)!.Value;
        check(run.Cursed == 0 && run.CurseThreat == 0 && run.CurseElite == 0 && !after.Cursed && Math.Abs(after.Threat - x.Spec.Threat(3)) < 1e-9,
            "After two won battles the curse is gone");

        // The elite chance it adds: a stage that never has elites gets them, never on Easy.
        StageSpec stage = new() { Id = "t", MaxAlive = 6, Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 6 } } } } };
        int Elites(BattleDifficulty level, double bonus)
        {
            var sim = new BattleSim(data, 0, 1600, difficulty: level, stage: stage, seed: 5, eliteBonus: bonus);
            for (double t = 0; t < 12; t += dt) sim.Step(dt, 800, false);
            return sim.ElitesSpawned;
        }
        check(Elites(BattleDifficulty.Normal, 0) == 0 && Elites(BattleDifficulty.Normal, 1) > 0 && Elites(BattleDifficulty.Easy, 1) == 0,
            "The curse's elite chance makes elites in a stage without any (not on Easy)");
    }

    private static void LostChecks(Action<bool, string> check, BattleSettings data)
    {
        var (x, run) = AtEvent(data, "lost_kitten");
        double max = x.MaxHp(run);
        check(x.Visit(0) && x.Options().SequenceEqual(new[] { Expedition.Card, Expedition.Leave }) && run.Pending!.Cards.Length is > 0 and <= 3,
            "Lost kitten: up to three cards to pick, or move on");
        check(Math.Abs(x.Cost(Expedition.Card) - 0.1 * max) < 1e-9 && x.CoinCost(Expedition.Card) == -3, "Helping costs 10% HP and gives 3 coins");
        string card = run.Pending.Cards[0];
        int had = run.Build.GetValueOrDefault(card);
        var done = x.Choose(Expedition.Card, card);
        check(done.Done && done.Coins == 3 && run.Coins == 3 && run.Build.GetValueOrDefault(card) == had + 1 && Math.Abs(run.Hp - (max - 0.1 * max + (data.UpgradeList.First(u => u.Id == card).Effect == "maxHp" ? data.UpgradeList.First(u => u.Id == card).Amount : 0))) < 1e-9
              && run.Log[^1].Coins == -3,
            "Helping: −10% HP, +3 coins, the card in the build, logged");

        var (y, weak) = AtEvent(data, "lost_kitten", 4);
        weak.Hp = 3;
        y.Visit(0);
        check(y.Refusal(Expedition.Card, weak.Pending!.Cards[0]) == "too weak" && y.Choose(Expedition.Leave).Done && weak.Coins == 0 && weak.Hp == 3,
            "Too weak to pay: helping is refused; moving on changes nothing");
    }

    private static void FileChecks(Action<bool, string> check, BattleSettings data)
    {
        string dir = Path.Combine(Path.GetTempPath(), "pet-expedition-events-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "expedition.json");
            var (x, run) = AtEvent(data, "slime_curse");
            run.Forged = 1; run.WeaponBonus = 0.15;
            x.Visit(0);
            x.Choose(Expedition.Card, run.Pending!.Cards[0]);
            check(x.Progress.Save(file), "A forged, cursed run is saved");
            var read = ExpeditionProgress.Load(file);
            var back = read.Active!;
            check(read.Version == 6 && back.Forged == 1 && back.WeaponBonus == 0.15 && back.Cursed == 2 && back.CurseThreat == 0.25 && back.CurseElite == 0.15
                  && new Expedition(data, read).Mismatch(back) == null,
                "Read back (version 6): the forge and the curse are kept and the run goes on");
            File.WriteAllText(file, File.ReadAllText(file).Replace("\"Cursed\": 2", "\"Cursed\": 40").Replace("\"WeaponBonus\": 0.15", "\"WeaponBonus\": 99"));
            var edited = ExpeditionProgress.Load(file).Active!;
            check(edited.Cursed == 5 && edited.WeaponBonus == 5, "A hand-edited file is held within bounds");
            File.WriteAllText(file, "{ \"Version\": 5, \"Active\": null }");
            check(!ExpeditionProgress.Load(file).ReadOnly, "A version 5 file (M23a) is read and written");
            File.WriteAllText(file, "{ \"Version\": 7, \"Active\": null }");
            check(ExpeditionProgress.Load(file).ReadOnly, "A newer file is read only");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    private static void RelicChecks(Action<bool, string> check, BattleSettings data)
    {
        var expedition = data.Expedition!;
        RelicSpec R(string effect) => expedition.Relics.First(r => r.Effect == effect);
        BattleSettings Quick(double slimeDamage = 0, double catDamage = 10) => new()
        {
            FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 1000, SlimeDamage = slimeDamage, CatDamage = catDamage, ComboHits = 99,
            WaveBreakSeconds = 0.5, OutroSeconds = 0.5, Expedition = expedition, Drops = data.Drops
        };
        StageSpec stage = new() { Id = "t", Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 } } } } };
        double cat = 800;
        void Until(BattleSim sim, Func<bool> done, double seconds = 30)
        {
            for (double t = 0; t < seconds && !done(); t += dt)
            {
                var order = sim.Step(dt, cat, false);
                if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -72 * 1.8 * dt, 72 * 1.8 * dt);
            }
        }
        BattleSim With(string effect, double slimeDamage = 0, double catDamage = 10, BattleLoot loot = BattleLoot.None) =>
            new(Quick(slimeDamage, catDamage), 0, 1600, stage: stage, seed: 4, relics: new[] { R(effect).Id }, loot: loot);
        var plain = new BattleSim(Quick(), 0, 1600, stage: stage, seed: 4);

        // Coin magnet: likelier drops, a shorter time on the floor.
        var magnet = With(RelicSpec.Magnet, loot: BattleLoot.Coins);
        check(magnet.DropLife == 5 && plain.DropLife == data.Drops!.Life, $"Coin magnet: a drop lies {magnet.DropLife} s (without it {plain.DropLife} s)");
        int Dropped(IReadOnlyList<string>? relics)
        {
            int n = 0;
            var defeat = typeof(BattleSim).GetMethod("DropLoot", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var sim = new BattleSim(Quick(), 0, 1600, stage: stage, seed: 9, relics: relics, loot: BattleLoot.Coins);
            for (int i = 0; i < 400; i++)
            {
                int before = sim.Drops.Count;
                defeat.Invoke(sim, new object[] { new Slime { Type = data.Enemy("slime"), X = 800 } });
                if (sim.Drops.Count != before || sim.Drops.Count == data.Drops.MaxOnFloor) n++;
                if (sim.Drops.Count == data.Drops.MaxOnFloor) foreach (var d in sim.Drops.ToList()) sim.PickUp(d.Id);
            }
            return n;
        }
        int without = Dropped(null), with = Dropped(new[] { R(RelicSpec.Magnet).Id });
        check(with > without * 1.3 && with < without * 1.7, $"Coin magnet: about 50% more drops over 400 defeats ({without} → {with})");

        // Spare battery: skills recharge 20% faster, no potions.
        var battery = With(RelicSpec.Battery, loot: BattleLoot.Coins);
        check(Math.Abs(battery.SkillCooldown(BattleSkill.Push) - plain.SkillCooldown(BattleSkill.Push) * 0.8) < 1e-9 && battery.NoPotions && !plain.NoPotions,
            "Spare battery: skill cooldowns ×0.8 and no potions");
        var pick = typeof(BattleSim).GetMethod("PickDrop", BindingFlags.NonPublic | BindingFlags.Instance)!;
        bool potion = false;
        for (double roll = 0; roll < 1; roll += 0.01) potion |= (BattleDropKind?)pick.Invoke(battery, new object[] { data.Drops, roll }) == BattleDropKind.Potion;
        check(!potion, "Spare battery: no roll ever picks a potion");

        // Thorn collar: swings −10%; a slime's own blow sends 30% back.
        var thorns = With(RelicSpec.Thorns, slimeDamage: 10, catDamage: 0);
        check(Math.Abs(With(RelicSpec.Thorns).SwingDamage - plain.SwingDamage * 0.9) < 1e-9 && thorns.ThornsShare == 0.3, "Thorn collar: swings ×0.9");
        cat = 800;
        Until(thorns, () => thorns.CatHp < thorns.CatMaxHp, 20);
        var biter = thorns.Slimes[0];
        check(Math.Abs(biter.MaxHp - biter.Hp - 3) < 1e-9 && thorns.Reflects == 1, $"Thorn collar: a 10-damage strike sends 3 back ({biter.MaxHp - biter.Hp})");

        // Venom fang: swings poison; more time between swings.
        var venom = With(RelicSpec.Venom);
        check(Math.Abs(venom.CooldownFactor - 1.1) < 1e-9, "Venom fang: swing cooldown ×1.1");
        cat = 800;
        Until(venom, () => venom.Slimes.Count > 0 && venom.Slimes[0].Hp < venom.Slimes[0].MaxHp, 20);
        check(venom.Slimes[0].PoisonLayers == 1 && venom.StatusesApplied(SlimeStatus.Poison) == 1, "Venom fang: the first landed swing poisons");

        // Frost stone: swings chill; −15% swing damage.
        var frost = With(RelicSpec.Frost);
        check(Math.Abs(frost.SwingDamage - plain.SwingDamage * 0.85) < 1e-9, "Frost stone: swings ×0.85");
        cat = 800;
        Until(frost, () => frost.Slimes.Count > 0 && frost.Slimes[0].Hp < frost.Slimes[0].MaxHp, 20);
        check(frost.Slimes[0].ChillRemaining > 0 && Math.Abs(frost.Slimes[0].ChillPower - 0.3) < 1e-9 && Math.Abs(frost.Slimes[0].MaxHp - frost.Slimes[0].Hp - 8.5) < 1e-9,
            "Frost stone: the first landed swing (8.5 of 10) chills the slime 30%");

        // Helmet: slower; a crushing blow (roll, spring, burrower) never stuns, any other still does.
        var helmet = With(RelicSpec.Helmet);
        check(Math.Abs(helmet.MoveFactor - 0.9) < 1e-9 && helmet.CrushProof, "Helmet: running ×0.9");
        var hurt = typeof(BattleSim).GetMethod("HurtCat", BindingFlags.NonPublic | BindingFlags.Instance)!;
        bool Stunned(BattleSim sim, bool crush)
        {
            cat = 800;
            Until(sim, () => sim.Slimes.Count > 0, 5);
            hurt.Invoke(sim, new object[] { sim.Slimes[0], 800.0, true, 1.0, 1.0, false, crush });
            return sim.CatStunned;
        }
        check(!Stunned(With(RelicSpec.Helmet, slimeDamage: 5), true) && Stunned(With(RelicSpec.Helmet, slimeDamage: 5), false) && Stunned(new BattleSim(Quick(5), 0, 1600, stage: stage, seed: 4), true),
            "Helmet: a crushing blow does not stun (it still hurts); a dash still stuns; without it the crush stuns");
    }

    private static void Validation(Action<bool, string> check)
    {
        string Error(string events, string relics = "[]")
        {
            string enc(string id, string enemy = "slime") => $"{{ \"id\": \"{id}\", \"nameVi\": \"E\", \"nameEn\": \"E\", \"goalVi\": \"g\", \"goalEn\": \"g\", \"waves\": [ {{ \"groups\": [ {{ \"enemy\": \"{enemy}\", \"count\": 1 }} ] }} ] }}";
            string json = $"{{ \"schemaVersion\": 1, \"expedition\": {{ \"id\": \"trail\", \"nameVi\": \"T\", \"nameEn\": \"T\", \"events\": {events}, \"relics\": {relics}, \"steps\": [ [ \"fight\"{(events == "[]" ? "" : ", \"event\"")} ] ], \"fights\": [ {enc("f1")} ], \"elites\": [], \"boss\": {enc("b", "king")} }} }}";
            try { BattleData.Parse(json); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        string Ev(string kind, string extra) => $"[ {{ \"id\": \"e1\", \"kind\": \"{kind}\"{extra}, \"nameVi\": \"E\", \"nameEn\": \"E\", \"descVi\": \"d\", \"descEn\": \"d\" }} ]";
        string Rel(string effect, string extra) => $"[ {{ \"id\": \"r1\", \"effect\": \"{effect}\"{extra}, \"nameVi\": \"R\", \"nameEn\": \"R\", \"descVi\": \"d\", \"descEn\": \"d\" }} ]";
        const string forge = ", \"coins\": 4, \"cost\": 0.15, \"amount\": 0.15, \"levels\": 2";
        check(Error(Ev("forge", forge)) == "" && Error(Ev("curse", ", \"cards\": 2, \"amount\": 0.25, \"battles\": 2, \"eliteChance\": 0.15")) == ""
              && Error(Ev("lost", ", \"cost\": 0.1, \"coins\": 3, \"cards\": 3")) == "",
            "A forge, a curse and a lost cat need no relics and are accepted");
        check(Error(Ev("forge", ", \"coins\": 4, \"cost\": 0.15, \"amount\": 0.15")).Contains("levels"), "A forge without levels is rejected");
        check(Error(Ev("forge", forge.Replace("\"coins\": 4", "\"coins\": 0"))).Contains("coins"), "A forge without a coin price is rejected");
        check(Error(Ev("curse", ", \"cards\": 2, \"amount\": 0.25")).Contains("battles"), "A curse without battles is rejected");
        check(Error(Ev("lost", ", \"cost\": 0.1, \"coins\": 3, \"cards\": 5")).Contains("cards"), "A lost cat with more than 3 cards is rejected");
        check(Error(Ev("swap", ", \"levels\": 2")).Contains("levels"), "A forge's key on another event is rejected");
        check(Error("[]", Rel("venom", ", \"penalty\": 0.1, \"onHit\": { \"status\": \"poison\", \"seconds\": 3, \"power\": 3 }")) == ""
              && Error("[]", Rel("frost", ", \"penalty\": 0.15, \"onHit\": { \"status\": \"chill\", \"seconds\": 2, \"power\": 0.3 }")) == "",
            "Venom and frost relics with their statuses are accepted");
        check(Error("[]", Rel("venom", ", \"penalty\": 0.1")).Contains("onHit"), "A venom relic without its poison is rejected");
        check(Error("[]", Rel("frost", ", \"onHit\": { \"status\": \"poison\", \"seconds\": 3, \"power\": 3 }")).Contains("chill"), "A frost relic that poisons is rejected");
        check(Error("[]", Rel("helmet", ", \"onHit\": { \"status\": \"chill\", \"seconds\": 2, \"power\": 0.3 }")).Contains("onHit"), "onHit on another relic is rejected");
        check(Error("[]", Rel("thorns", ", \"amount\": 1.5")).Contains("amount"), "Thorns sending back more than 90% is rejected");
        check(Error("[]", Rel("magnet", ", \"amount\": 0.5, \"penalty\": 1")).Contains("penalty"), "A magnet's floor time under 2 s is rejected");
    }

    // Whole runs in simulation (NOT real play), each new relic held from the start: the simple player,
    // Normal, the greedy route, both weapons. Measured against the same runs without a relic; each relic
    // must stay within a fair band (a price that never makes the run hopeless, a gain that never wins it alone).
    private static void Measure(Action<bool, string> check, BattleSettings data, string? route)
    {
        const int seeds = 12;
        var weapons = new[] { BattleWeapon.Sword, BattleWeapon.Hammer };
        int Wins(string? relic)
        {
            int wins = 0;
            foreach (var weapon in weapons)
                for (int seed = 1; seed <= seeds; seed++)
                {
                    var x = new Expedition(data, new ExpeditionProgress(), route);
                    var run = x.Start(weapon, BattleDifficulty.Normal, seed);
                    if (relic != null) run.Relics.Add(relic);
                    while (x.Run != null)
                    {
                        if (x.NeedsRoll) { x.Roll(); continue; }
                        if (run.Pending != null) { ExpeditionTests.Resolve(x, run, ExpeditionTests.Policy.Greedy); continue; }
                        int lane = ExpeditionTests.Route(x, run, ExpeditionTests.Policy.Greedy);
                        if (x.Node(lane) is not ExpeditionNode node) { x.Visit(lane); continue; }
                        x.Enter(lane);
                        var outcome = AdventureBalanceTests.Play(data, node.Stage, weapon, BattleDifficulty.Normal, seed * 100 + run.Step, AdventureBalanceTests.Player.Simple,
                            run.Build, run.Hp, node.Threat, node.Seed, run.Relics, skills: run.Loadout, loot: BattleLoot.Coins, weaponBonus: node.WeaponBonus, eliteBonus: node.EliteBonus);
                        if (x.Finish(outcome.Won, outcome.Hp, outcome.Build, outcome.Seconds, 0).RunWon) wins++;
                    }
                }
            return wins;
        }
        int total = seeds * weapons.Length, baseline = Wins(null);
        var results = new List<string> { $"none {baseline}/{total}" };
        bool fair = true;
        foreach (var r in data.Expedition!.Relics.Where(r => r.Effect is RelicSpec.Magnet or RelicSpec.Thorns or RelicSpec.Venom or RelicSpec.Frost or RelicSpec.Battery or RelicSpec.Helmet))
        {
            int won = Wins(r.Id);
            results.Add($"{r.Id} {won}/{total}");
            fair &= won >= baseline - total / 4 && won <= Math.Max(baseline + total / 4, total);
        }
        Console.WriteLine($"Expedition relics M23b · {route ?? data.Expedition!.Id} (simulation, not real play; sword + hammer, Normal, simple player, greedy route, relic held from the start): " + string.Join(" · ", results));
        check(fair, $"{route ?? data.Expedition!.Id}: each new relic held a whole run stays within a quarter of the runs of the same runs without it: " + string.Join(", ", results));
    }
}
