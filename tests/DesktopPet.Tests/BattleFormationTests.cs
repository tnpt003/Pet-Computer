using DesktopPet;

// 1.5 M12: the flag slime (Q8) and the Enemy Director's formations. The flag slime's aura speeds up the
// allies in its ring (an elite's more); defeated, every other enemy panics (runs off). The Director draws
// one formation of a "formation" group by weight, among those the threat reaches and the budget fits
// (none fits: the cheapest), with the spawn stream only, so waves without formations draw as before; its
// parts come one after the other, a "b" part from the other edge; the first part says which formation it
// is (a banner). Two chain slimes may tie one ally: the second takes its share of what is left. The data
// limits, the shipped formations (kept out of every battle for now) and the try-out file.
internal static class BattleFormationTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id, double cost = 1, LinkSpec? link = null, bool support = false, double speed = 30) => new()
    {
        Id = id, MaxHp = 1000, Speed = speed, Damage = 0, Range = 50, Windup = 0.8, AttackCooldown = 2, Recover = 0.3, Cost = cost, Link = link,
        Support = support, Elite = new EliteSpec { Hp = 1.2 }
    };

    private static BattleSim Field(EnemyType[] kinds, int[] counts, bool elite = false, int maxAlive = 8, int seed = 5)
    {
        var arms = BattleData.Settings.Weapon("sword") with { Range = 5, StunChance = 0 };
        var settings = new BattleSettings
        {
            Weapons = new[] { arms }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1_000_000, MaxAlive = maxAlive, MaxElites = 8, Enemies = kinds,
            CatDamage = 0, RageHitGain = 0, BombDamage = 40, BombCooldown = 0.1, BombDelay = 0.01, BombRadius = 40,
            Plan = new[] { new WaveSpec { Groups = kinds.Select((k, i) => new WaveGroup { Enemy = k.Id, Count = counts[i], Elite = elite && k.Link != null }).ToArray() } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: seed);
        int total = counts.Sum();
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < total; i++) sim.Step(dt, 100, false);
        sim.ClearEvents();
        return sim;
    }

    public static void Run(Action<bool, string> check)
    {
        FlagChecks(check);
        DirectorChecks(check);
        FieldChecks(check);
        TwoRopeChecks(check);
        DataChecks(check);
    }

    private static void Steps(BattleSim sim, double seconds, double catX = 200)
    {
        for (int i = 0; i < Math.Round(seconds / dt); i++) { sim.Step(dt, catX, false); sim.ClearEvents(); }
    }

    private static void Bomb(BattleSim sim, Slime slime)
    {
        for (int i = 0; i < 60 && !sim.Ready(BattleSkill.Bomb); i++) { sim.Step(dt, 200, false); sim.ClearEvents(); }
        sim.Use(BattleSkill.Bomb, slime.Id, 200);
        for (int i = 0; i < 3; i++) { sim.Step(dt, 200, false); sim.ClearEvents(); }
    }

    private static void FlagChecks(Action<bool, string> check)
    {
        TogetherChecks(check);
        // 2026-11-04: the shipped flag slime plants a post that holds the ring.
        var shippedPlant = BattleData.Settings.Enemy("flagbearer").Plant!;
        var link = BattleData.Settings.Enemy(shippedPlant.Post).Link!;
        var flag = Kind("flagpost", 0, link, speed: 0);
        var carrier = new EnemyType { Id = "carrier", MaxHp = 100, Speed = 0, Damage = 0, Range = 50, Windup = 0.8, AttackCooldown = 2, Recover = 0.3, Plant = new PlantSpec { Post = "flagpost", After = shippedPlant.After } };
        var planting = Field(new[] { carrier, flag }, new[] { 1, 0 });
        var bearerSlime = planting.Slimes.Single();
        bearerSlime.X = 900;
        Steps(planting, shippedPlant.After + 0.1);
        check(planting.Slimes.Count(s => s.Type.Id == "flagpost") == 1 && Math.Abs(planting.Slimes.Single(s => s.Type.Id == "flagpost").X - 900) < 60, "A flag slime plants its flag where it stands, once");
        var sim = Field(new[] { flag, Kind("slime") }, new[] { 1, 2 });
        var bearer = sim.Slimes.Single(s => s.Link != null);
        var near = sim.Slimes.First(s => s.Link == null);
        var far = sim.Slimes.Last(s => s.Link == null);
        bearer.X = 1000; near.X = 1000 + link.Range - 20; far.X = 1000 + link.Range + 150;
        sim.Step(dt, 200, false);
        check(Math.Abs(near.AuraCut - link.Guard) < 1e-9 && far.AuraCut == 0 && bearer.AuraCut == 0,
            $"An ally in the flag's ring takes {link.Guard:P0} less, one outside and the flag itself do not ({near.AuraCut}, {far.AuraCut})");
        double hpNear = near.Hp;
        sim.Damage(near, 100, DamageSource.Bomb);
        sim.Arm(BattleSkill.Push);
        check(Math.Abs(hpNear - near.Hp - 100 * (1 - link.Guard)) < 1e-6, $"The hit lands {link.Guard:P0} lighter ({hpNear - near.Hp:0})");
        near.Set(SlimeState.Moving);
        sim.UsePush(near.Id, 200);
        check(near.State != SlimeState.Stunned, "In the ring nothing dazes it");

        sim = Field(new[] { flag, Kind("slime") }, new[] { 1, 1 }, elite: true);
        bearer = sim.Slimes.Single(s => s.Link != null);
        near = sim.Slimes.Single(s => s.Link == null);
        bearer.X = 1000; near.X = 1000 + link.EliteRange - 10;
        sim.Step(dt, 200, false);
        check(bearer.Elite != null && near.AuraCut > 0, $"An elite flag's ring reaches {link.EliteRange} DIPs");

        // Defeated: every other enemy panics breakStun s, running away from the cat without attacking.
        sim = Field(new[] { flag, Kind("slime") }, new[] { 1, 2 });
        bearer = sim.Slimes.Single(s => s.Link != null);
        var allies = sim.Slimes.Where(s => s.Link == null).ToList();
        bearer.X = 1400; allies[0].X = 600; allies[1].X = 700;
        bearer.Hp = 1;
        bool panicked = false;
        for (int i = 0; i < 60 && !sim.Ready(BattleSkill.Bomb); i++) { sim.Step(dt, 200, false); sim.ClearEvents(); }
        sim.Use(BattleSkill.Bomb, bearer.Id, 200);
        for (int i = 0; i < 3; i++) { sim.Step(dt, 200, false); panicked |= sim.Events.Any(e => e.Kind == BattleEventKind.Panicked); sim.ClearEvents(); }
        double x0 = allies[0].X;
        check(!bearer.Targetable && panicked && allies.All(a => a.PanicRemaining > link.BreakStun - 0.1), $"The flag defeated: its allies panic {link.BreakStun} s");
        Steps(sim, 1);
        check(allies[0].X > x0 + 10 && allies.All(a => a.State != SlimeState.Windup), "Panicking, they run away from the cat and do not attack");
        Steps(sim, link.BreakStun);
        check(allies.All(a => a.PanicRemaining <= 0), "The panic wears off");
    }

    private static readonly FormationSpec Cheap = new() { Id = "cheap", NameVi = "R", NameEn = "C", Parts = new[] { new WaveGroup { Enemy = "slime", Count = 2 } } };
    private static readonly FormationSpec Dear = new()
    {
        Id = "dear", NameVi = "Đ", NameEn = "D", MinThreat = 1.5,
        Parts = new[] { new WaveGroup { Squad = new[] { new WaveGroup { Enemy = "rock", Count = 1 }, new WaveGroup { Enemy = "mender", Count = 1 } } }, new WaveGroup { Enemy = "runner", Side = "b" } }
    };

    // 2026-11-04 (D6): a formation is one unit: every member together, from one edge, taking one place; a
    // part marked "b" comes apart from the other edge. A timer's call may be a formation.
    private static void TogetherChecks(Action<bool, string> check)
    {
        var slime = Kind("slime"); var rock = Kind("rock", 2); var mender = Kind("mender", 2); var runner = Kind("runner");
        var squad = new FormationSpec { Id = "squad", NameVi = "S", NameEn = "S", Parts = new[] {
            new WaveGroup { Squad = new[] { new WaveGroup { Enemy = "rock", Count = 1 }, new WaveGroup { Enemy = "mender", Count = 1 } } },
            new WaveGroup { Enemy = "runner", Count = 2 } } };
        var units = SpawnDirector.Units(new WaveSpec { Groups = new[] { new WaveGroup { Formation = new() { ["squad"] = 1 }, Count = 1 } } }, new Random(1), _ => 1, id => id == "squad" ? squad : null, 1);
        check(units.Count == 1 && units[0].Kinds.Length == 4 && units[0].Formation == "squad", $"All its parts are one unit ({units.Count} unit, {units[0].Kinds.Length} members)");
        var split = SpawnDirector.Units(new WaveSpec { Groups = new[] { new WaveGroup { Formation = new() { ["dear"] = 1 }, Count = 1 } } }, new Random(1), _ => 1, id => id == "dear" ? Dear : null, 2);
        check(split.Count == 2 && split[1].Side == "b", "Only a part marked b comes apart, from the other edge");
        var settings = new BattleSettings
        {
            Weapons = new[] { BattleData.Settings.Weapon("sword") with { Range = 5, StunChance = 0 } }, FirstWaveDelay = 0, SpawnInterval = 1.6, CatMaxHp = 1000, MaxAlive = 1, Enemies = new[] { slime, rock, mender, runner },
            CatDamage = 0, RageHitGain = 0, Formations = new[] { squad },
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Formation = new() { ["squad"] = 1 }, Count = 1 } } } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 3);
        int atFirst = 0;
        for (int i = 0; i < 300 && atFirst == 0; i++) { sim.Step(dt, 100, false); atFirst = sim.Slimes.Count; sim.ClearEvents(); }
        check(atFirst == 4, $"The formation comes in at once even with room for one ({atFirst})");
        check(sim.Slimes.All(s => s.X > 800), $"All on one side, the far one ({string.Join(", ", sim.Slimes.Select(s => s.X.ToString("0")))})");
        var shipped = BattleData.Settings;
        check(shipped.Formation("kamikaze") != null && shipped.Formation("artillery") != null && shipped.Formation("alarm") != null, "The three new formations ship");
        check(shipped.Enemy("timer").Clock!.CallFormations!.Keys.All(k => shipped.Formation(k) != null), "A timer's call names real formations");
    }

    private static void DirectorChecks(Action<bool, string> check)
    {
        var table = new Dictionary<string, FormationSpec> { ["cheap"] = Cheap, ["dear"] = Dear };
        FormationSpec? Find(string id) => table.GetValueOrDefault(id);
        double Cost(string kind) => kind is "rock" or "mender" ? 2 : 1;
        WaveSpec Wave(double budget = 0, string? side = null) => new()
        {
            Budget = budget, Groups = new[] { new WaveGroup { Formation = new() { ["cheap"] = 1, ["dear"] = 1 }, Count = 1, Side = side } }
        };
        string Drawn(List<SpawnUnit> units) => units.Count > 0 ? units[0].Formation ?? "?" : "-";

        var low = Enumerable.Range(0, 200).Select(i => Drawn(SpawnDirector.Units(Wave(), new Random(i), Cost, Find, 1))).ToList();
        check(low.All(f => f == "cheap"), "Below its minThreat a formation is never drawn");
        var high = Enumerable.Range(0, 200).Select(i => Drawn(SpawnDirector.Units(Wave(), new Random(i), Cost, Find, 1.6))).ToList();
        check(high.Count(f => f == "dear") is > 60 and < 140 && high.Count(f => f == "cheap") > 60, $"At its threat both come by weight ({high.Count(f => f == "dear")} of 200)");
        var tight = Enumerable.Range(0, 100).Select(i => Drawn(SpawnDirector.Units(Wave(budget: 3), new Random(i), Cost, Find, 1.6))).ToList();
        check(tight.All(f => f == "cheap"), "One that does not fit the budget is not drawn");
        var none = Enumerable.Range(0, 50).Select(i => Drawn(SpawnDirector.Units(Wave(budget: 1), new Random(i), Cost, Find, 1.6))).ToList();
        check(none.All(f => f == "cheap"), "Nothing fits: the cheapest still comes");
        check(Enumerable.Range(0, 50).All(i => string.Join(",", SpawnDirector.Units(Wave(), new Random(i), Cost, Find, 1.6).SelectMany(u => u.Kinds))
                == string.Join(",", SpawnDirector.Units(Wave(), new Random(i), Cost, Find, 1.6).SelectMany(u => u.Kinds))), "The same seed draws the same formations");

        var dear = SpawnDirector.Units(new WaveSpec { Groups = new[] { new WaveGroup { Formation = new() { ["dear"] = 1 } } } }, new Random(1), Cost, Find, 1);
        check(dear.Count == 2 && dear[0].Kinds.SequenceEqual(new[] { "rock", "mender" }) && dear[0].Side == "a" && dear[0].Formation == "dear"
              && dear[1].Kinds.SequenceEqual(new[] { "runner" }) && dear[1].Side == "b" && dear[1].Formation == null,
            "Its parts come in order from the far edge, a \"b\" part from the other one; only the first names the formation (a lone one is used even below its threat)");
        var flipped = SpawnDirector.Units(new WaveSpec { Groups = new[] { new WaveGroup { Formation = new() { ["dear"] = 1 }, Side = "b" } } }, new Random(1), Cost, Find, 1);
        check(flipped[0].Side == "b" && flipped[1].Side == "a", "A group from side \"b\": its \"b\" part comes from the far edge");

        // A wave without formations draws exactly as before.
        var pick = new WaveSpec { Groups = new[] { new WaveGroup { Pick = new() { ["slime"] = 1, ["runner"] = 1 }, Count = 5 } } };
        check(Enumerable.Range(0, 50).All(i => SpawnDirector.Units(pick, new Random(i), Cost).SelectMany(u => u.Kinds).SequenceEqual(SpawnDirector.Units(pick, new Random(i), Cost, Find, 2).SelectMany(u => u.Kinds))),
            "Waves without a formation group draw as before");
    }

    private static void FieldChecks(Action<bool, string> check)
    {
        var kinds = new[] { Kind("slime"), Kind("runner"), Kind("rock", 2), Kind("mender", 2, support: true) };
        var arms = BattleData.Settings.Weapon("sword") with { Range = 5, StunChance = 0 };
        var settings = new BattleSettings
        {
            Weapons = new[] { arms }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 1_000_000, MaxAlive = 8, Enemies = kinds, CatDamage = 0, RageHitGain = 0,
            IncomingWarning = 0.5, Formations = new[] { Cheap, Dear },
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Formation = new() { ["dear"] = 1 } }, new WaveGroup { Formation = new() { ["dear"] = 1 } } } } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 2);
        var arrived = new List<double>();
        bool incoming = false;
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < 6; i++)
        {
            sim.Step(dt, 800, false);
            arrived.AddRange(sim.Events.Where(e => e.Kind == BattleEventKind.FormationArrived).Select(e => e.Value));
            incoming |= sim.Events.Any(e => e.Kind == BattleEventKind.Incoming);
            sim.ClearEvents();
        }
        check(sim.Slimes.Count(s => s.Targetable) == 6 && arrived.SequenceEqual(new double[] { 1, 1 }) && sim.FormationsMet == 2,
            $"Each formation comes whole and says so once (its index in the data; {string.Join(",", arrived)})");
        check(incoming, "Its \"b\" part is announced by an arrow at the other edge");
    }

    private static void TwoRopeChecks(Action<bool, string> check)
    {
        var link = BattleData.Settings.Enemy("chain").Link!;
        var chain = Kind("chain", 2, link, speed: 0);
        var rock = Kind("rock", 2, speed: 0);
        var sim = Field(new[] { chain, rock }, new[] { 2, 1 });
        var holders = sim.Slimes.Where(s => s.Link != null).ToList();
        var ally = sim.Slimes.Single(s => s.Link == null);
        ally.X = 900; holders[0].X = 1050; holders[1].X = 1100;
        for (int i = 0; i < 60 && holders.Any(h => h.LinkedTo == 0); i++) { sim.Step(dt, 200, false); sim.ClearEvents(); }
        ally.X = 900; holders[0].X = 1050; holders[1].X = 1100;
        check(holders.All(h => h.LinkedTo == ally.Id), "Two chain slimes with one ally both tie their ropes to it");
        double allyHp = ally.Hp;
        var hp = holders.Select(h => h.Hp).ToArray();
        Bomb(sim, ally);
        double taken = allyHp - ally.Hp, shared = hp[0] - holders[0].Hp + hp[1] - holders[1].Hp;
        check(Math.Abs(taken - 40 * (1 - link.Share) * (1 - link.Share)) < 0.01 && Math.Abs(shared / link.Armor + taken - 40) < 0.01,
            $"The second rope takes its share of what is left: the ally takes {(1 - link.Share) * (1 - link.Share):P0} ({taken})");

        // A free ally comes first: with two allies, one rope each.
        sim = Field(new[] { chain, Kind("slime", speed: 0) }, new[] { 2, 2 });
        holders = sim.Slimes.Where(s => s.Link != null).ToList();
        var mates = sim.Slimes.Where(s => s.Link == null).ToList();
        mates[0].X = 900; mates[1].X = 1000; holders[0].X = 950; holders[1].X = 960;
        foreach (var h in holders) { h.LinkedTo = 0; h.RelinkAt = 0; }
        for (int i = 0; i < 60 && holders.Any(h => h.LinkedTo == 0); i++) { sim.Step(dt, 200, false); sim.ClearEvents(); }
        check(holders.Select(h => h.LinkedTo).Distinct().Count() == 2, "With an ally still free, a chain slime ties that one first");
    }

    private static void DataChecks(Action<bool, string> check)
    {
        const string enemies = "\"enemies\":[{\"id\":\"slime\",\"nameVi\":\"S\",\"nameEn\":\"S\"},{\"id\":\"rock\",\"nameVi\":\"R\",\"nameEn\":\"R\"},"
            + "{\"id\":\"king\",\"nameVi\":\"K\",\"nameEn\":\"K\",\"boss\":true}]";
        bool Rejects(string formations, string group = "{\"formation\":{\"f\":1}}", string more = "")
        {
            try
            {
                BattleData.Parse("{" + enemies + ",\"formations\":[" + formations + "],\"waves\":[{\"groups\":[" + group + "]}]" + more + "}");
                return false;
            }
            catch (InvalidDataException) { return true; }
        }
        const string ok = "{\"id\":\"f\",\"nameVi\":\"F\",\"nameEn\":\"F\",\"parts\":[{\"squad\":[{\"enemy\":\"slime\",\"count\":1},{\"enemy\":\"rock\",\"count\":1}]},{\"enemy\":\"slime\",\"side\":\"b\"}]}";
        check(!Rejects(ok), "A formation within its limits is accepted");
        check(Rejects(ok, "{\"formation\":{\"nobody\":1}}") && Rejects(ok, "{\"formation\":{\"f\":1},\"pick\":{\"slime\":1}}") && Rejects(ok, "{\"formation\":{\"f\":0}}"),
            "A group naming an unknown formation, mixing in a pick, or with no weight is refused");
        check(Rejects(ok.Replace("\"enemy\":\"rock\"", "\"enemy\":\"king\"")) && Rejects(ok.Replace("\"enemy\":\"rock\"", "\"enemy\":\"ghost\"")) && Rejects(ok.Replace("\"side\":\"b\"", "\"side\":\"c\"")),
            "A part with a boss, an unknown enemy or a bad side is refused");
        check(Rejects(ok, more: ",\"maxAlive\":1") && Rejects(ok.Replace("\"parts\":[", "\"minThreat\":5,\"parts\":[")) && Rejects(ok + "," + ok),
            "A part bigger than maxAlive, a minThreat past 3, or one id used twice is refused");

        var s = BattleData.Settings;
        var flag = s.Enemy("flagbearer");
        var post = s.Enemy("flagpost");
        check(flag is { Id: "flagbearer", Support: true, BinSpit: false, Plant.Post: "flagpost", Link: null } && post is { Still: true, Link: { Kind: EnemyLinks.Aura, BreakStun: 2, Guard: > 0 } } && post.MaxHp == 30,
            "The shipped flag slime: a support planting a 30-HP flag; the flag's ring guards its team, its fall panics them 2 s");
        var ids = s.FormationList.Select(f => f.Id).ToArray();
        check(ids.SequenceEqual(new[] { "fortress", "cavalry", "drums", "assembly", "trampoline", "swarms", "kamikaze", "artillery", "alarm" }) && s.FormationList.All(f => f.TipVi != null && f.TipEn != null),
            "The nine shipped formations, each with its tip");
        string Team(string id) => string.Join(",", s.Formation(id)!.Members);
        check(Team("fortress") == "spitter,mender,rock" && Team("cavalry") == "bomber,bomber,runner,runner,spitter" && Team("drums") == "drummer,slime,slime,slime"
              && Team("assembly") == "chain,chain,rock" && s.Formation("assembly")!.Parts[0].Squad![1].Elite && Team("trampoline") == "slime,runner,springer"
              && s.Formation("trampoline")!.Parts[1].Side == "b" && Team("swarms") == "mender,fuser,fuser,fuser,fuser,fuser,fuser",
            "Their members as designed (an elite rock on the assembly line, the spring from behind)");
        bool Uses(WaveGroup g) => g.Formation != null || g.Enemy == "flagbearer" || g.Pick?.ContainsKey("flagbearer") == true || g.Squad?.Any(m => m.Enemy == "flagbearer") == true;
        var all = s.WavePlan.Concat(s.Campaign?.Chapters.Take(1).SelectMany(c => c.Stages).SelectMany(st => st.Waves) ?? Enumerable.Empty<WaveSpec>());
        check(!all.SelectMany(w => w.Groups).Any(Uses), "No built-in battle uses them yet outside chapter 2 (1.5 M21 brings it in)");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "examples", "battle-m12.json"))) dir = dir.Parent;
        var load = dir == null ? null : BattleData.Load(Path.Combine(dir.FullName, "docs", "examples", "battle-m12.json"));
        check(load is { Custom: true, Error: null } && load.Settings.WavePlan.SelectMany(w => w.Groups).Count(Uses) >= 4,
            $"docs/examples/battle-m12.json loads as custom data with the flag slime and the formations ({load?.Error ?? (dir == null ? "file not found" : "ok")})");
    }
}
