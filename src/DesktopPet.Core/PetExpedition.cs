using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopPet;

// Expedition (docs/EXPEDITION-DIRECTION.md): one run over a small map of steps. Each step offers
// one to three nodes; the user picks one, fights it with the usual battle engine (an encounter is
// a StageSpec), and the cat's HP and build go on to the next step. The last step is the boss.
// battle.json "expedition": the map's node kinds per step ("fight", "elite", "rest", "event"), the
// encounter pools the run draws from, the boss, the events and relics (milestone 3), and the run's
// rules. Ids are stable: a saved run names them.
public sealed class ExpeditionSpec
{
    // 1.5 M23: "shop", the cat shop (ShopSpec): coins buy cards, HP and relics.
    public const string Fight = "fight", Elite = "elite", Rest = "rest", Event = "event", Shop = "shop";
    public static readonly string[] Kinds = { Fight, Elite, Rest, Event, Shop };
    public static bool IsBattle(string kind) => kind is Fight or Elite;

    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    // Raise it when the run changes enough that a saved run or old records should not go on.
    public int RulesVersion { get; init; } = 1;
    // 1.5 M23: older rules versions whose saved run still goes on to its end (its records stay under
    // its own version; its steps still to spin draw no shop).
    public int[] KeepRuns { get; init; } = Array.Empty<int>();
    // 1.5 M25: the first rules version with the cat shop (a kept run of an older one draws no shop; 1: always).
    public int ShopFrom { get; init; } = 1;
    // 1.5 M23: more routes ("routes", each its own map, encounters, boss and records). A route gives
    // its id, names, rules version, unlockAfter, map (steps or kindGacha), encounters, zones and scene;
    // the rest (HP rules, events, relics, capsules, shop) is this Expedition's (Routes, read through All).
    [JsonPropertyName("routes")] public ExpeditionSpec[] RouteData { get; init; } = Array.Empty<ExpeditionSpec>();
    // 1.5 M23: a route's zones, in order over its steps before the boss: each step draws its fights and
    // elites only from its zone's (none: from the whole pools).
    public ExpeditionZoneSpec[] Zones { get; init; } = Array.Empty<ExpeditionZoneSpec>();
    // 1.5 M23: the backdrop behind the route's battles (ChapterSpec.Dump; null: none).
    public string? Scene { get; init; }
    // 1.5 M23: the cat shop's wares and prices (null: no shop nodes).
    [JsonPropertyName("shop")] public ShopSpec? Store { get; init; }

    private ExpeditionSpec[]? all;
    // This Expedition and its routes, each route with the shared rules filled in.
    [JsonIgnore] public IReadOnlyList<ExpeditionSpec> All => all ??= new[] { this }.Concat(RouteData.Where(r => r != null).Select(Inherit)).ToArray();
    public ExpeditionSpec? Route(string? id) => All.FirstOrDefault(r => r.Id == id);
    private ExpeditionSpec Inherit(ExpeditionSpec r) => new()
    {
        Id = r.Id, NameVi = r.NameVi, NameEn = r.NameEn, RulesVersion = r.RulesVersion, KeepRuns = r.KeepRuns, ShopFrom = r.ShopFrom, UnlockAfter = r.UnlockAfter,
        Steps = r.Steps, KindGacha = r.KindGacha, Fights = r.Fights, Elites = r.Elites, Boss = r.Boss, Zones = r.Zones, Scene = r.Scene, Keepsakes = r.Keepsakes,
        RetreatCost = RetreatCost, WinHeal = WinHeal, StepThreat = StepThreat, RestHeal = RestHeal, MaxRelics = MaxRelics, EliteRelics = EliteRelics,
        Events = Events, Relics = Relics, Capsules = Capsules, Store = Store
    };
    // The zone of a step before the boss (null: no zones).
    public ExpeditionZoneSpec? Zone(int step)
    {
        foreach (var z in Zones) { if (step < z.Steps) return z; step -= z.Steps; }
        return null;
    }
    // The Adventure stage that must be won first (null, or a stage the data no longer has, as with a
    // user's battle.json that replaces the campaign: always open).
    public string? UnlockAfter { get; init; }
    // Leaving a step's battle (End, closing the app, a crash): the step can be fought again, for
    // this share of the cat's max HP.
    public double RetreatCost { get; init; } = 0.25;
    // HP given back after a won step, as a share of the cat's max HP.
    public double WinHeal { get; init; } = 0.1;
    // Each step makes its enemies this much stronger than the one before (step 1: × 1).
    public double StepThreat { get; init; }
    // The node kinds of each step before the boss, in lane order (fixed for every run). Either this
    // or KindGacha: then each step's kinds are drawn when the run reaches it.
    public string[][] Steps { get; init; } = Array.Empty<string[]>();
    public KindGachaSpec? KindGacha { get; init; }
    // The capsule machine after each won fight node (null: none).
    public CapsuleSpec? Capsules { get; init; }
    public StageSpec[] Fights { get; init; } = Array.Empty<StageSpec>();
    public StageSpec[] Elites { get; init; } = Array.Empty<StageSpec>();
    public StageSpec? Boss { get; init; }
    // A rest node: RestHeal of the max HP back, or one card instead.
    public double RestHeal { get; init; } = 0.3;
    // Relics the run may hold at once, and how many a won elite offers to choose from.
    public int MaxRelics { get; init; } = 3;
    public int EliteRelics { get; init; } = 2;
    public ExpeditionEventSpec[] Events { get; init; } = Array.Empty<ExpeditionEventSpec>();
    public RelicSpec[] Relics { get; init; } = Array.Empty<RelicSpec>();
    // Keepsakes (milestone 4): given once each, for a won run at their difficulty.
    public KeepsakeSpec[] Keepsakes { get; init; } = Array.Empty<KeepsakeSpec>();

    public IReadOnlyList<StageSpec> Pool(string kind) => kind == Elite ? Elites : Fights;
    // A step's pool: its zone's encounters of that kind (the whole pool without zones).
    public IReadOnlyList<StageSpec> Pool(string kind, int step) => Zone(step) is ExpeditionZoneSpec z
        ? Pool(kind).Where(s => (kind == Elite ? z.Elites : z.Fights).Contains(s.Id)).ToArray() : Pool(kind);
    // How many nodes of a kind a step may have (a shop or rest: one; a battle: its pool's size).
    public int Room(string kind, int step) => kind is Rest or Shop ? 1 : kind == Event ? Events.Length : Pool(kind, step).Count;
    public StageSpec? Encounter(string id) => Fights.Concat(Elites).Append(Boss).FirstOrDefault(s => s?.Id == id);
    public ExpeditionEventSpec? FindEvent(string? id) => Events.FirstOrDefault(e => e.Id == id);
    public RelicSpec? Relic(string? id) => Relics.FirstOrDefault(r => r.Id == id);
    public KeepsakeSpec? Keepsake(string? id) => Keepsakes.FirstOrDefault(k => k.Id == id);
    // Steps before the boss, and including the boss's.
    [JsonIgnore] public int PathSteps => KindGacha?.Steps ?? Steps.Length;
    [JsonIgnore] public int StepCount => PathSteps + 1;
    public double Threat(int step) => 1 + StepThreat * step;

    internal static void Validate(ExpeditionSpec x, BattleSettings s, Action<IReadOnlyList<WaveSpec>?, string, int?, bool> waves, Action<string?, string> id, Action<string?, string> text)
    {
        Validate(x, s, waves, id, text, null);
        // 1.5 M23: routes take the shared rules from the Expedition, have their own ids and encounters.
        if (x.RouteData == null || x.RouteData.Length > 4) throw new InvalidDataException("expedition.routes must list at most 4 routes");
        var routeIds = new HashSet<string> { x.Id };
        var encounterIds = x.Fights.Concat(x.Elites).Append(x.Boss!).Select(e => e.Id).ToHashSet();
        foreach (var r in x.RouteData)
        {
            string where = $"expedition.routes[{r?.Id}]";
            if (r == null) throw new InvalidDataException("expedition.routes has an empty entry");
            if (r.RouteData is { Length: > 0 } || r.Events is { Length: > 0 } || r.Relics is { Length: > 0 } || r.Keepsakes is { Length: > 0 } || r.Capsules != null || r.Store != null)
                throw new InvalidDataException(where + ": a route takes its events, relics, capsules and shop from the expedition, and has no routes or keepsakes");
            if (!routeIds.Add(r.Id ?? "")) throw new InvalidDataException($"route id \"{r.Id}\" is used twice");
            var route = x.Route(r.Id)!;
            Validate(route, s, waves, id, text, where);
            foreach (var e in route.Fights.Concat(route.Elites).Append(route.Boss!))
                if (!encounterIds.Add(e.Id)) throw new InvalidDataException($"encounter id \"{e.Id}\" is used by two routes");
        }
    }

    private static void Validate(ExpeditionSpec x, BattleSettings s, Action<IReadOnlyList<WaveSpec>?, string, int?, bool> waves, Action<string?, string> id, Action<string?, string> text, string? route)
    {
        string at = route ?? "expedition";
        id(x.Id, at + ".id");
        if (x.KeepRuns == null || x.KeepRuns.Any(v => v < 1 || v >= x.RulesVersion)) throw new InvalidDataException(at + ".keepRuns must list rules versions below rulesVersion");
        if (x.Scene != null && x.Scene != ChapterSpec.Dump) throw new InvalidDataException(at + ".scene must be \"" + ChapterSpec.Dump + "\"");
        text(x.NameVi, at + ".nameVi"); text(x.NameEn, at + ".nameEn");
        if (x.RulesVersion is < 1 or > 1000) throw new InvalidDataException(at + ".rulesVersion must be 1–1000");
        if (x.ShopFrom < 1 || x.ShopFrom > x.RulesVersion) throw new InvalidDataException(at + ".shopFrom must be 1 to rulesVersion");
        if (!double.IsFinite(x.RetreatCost) || x.RetreatCost is < 0 or > 1) throw new InvalidDataException(at + ".retreatCost must be 0–1");
        if (!double.IsFinite(x.WinHeal) || x.WinHeal is < 0 or > 1) throw new InvalidDataException(at + ".winHeal must be 0–1");
        if (!double.IsFinite(x.StepThreat) || x.StepThreat is < 0 or > 0.5) throw new InvalidDataException(at + ".stepThreat must be 0–0.5");
        var stageIds = new HashSet<string>(s.Campaign?.Chapters.SelectMany(c => c.Stages).Select(st => st.Id) ?? Enumerable.Empty<string>());
        if (x.UnlockAfter != null) id(x.UnlockAfter, at + ".unlockAfter");
        // Encounter ids are unique, and never an Adventure stage's (titles and records stay apart).
        var own = new HashSet<string>();
        void Encounter(StageSpec? stage, string where)
        {
            if (stage == null) throw new InvalidDataException(where + " is empty");
            id(stage.Id, where + ".id");
            if (stageIds.Contains(stage.Id) || !own.Add(stage.Id)) throw new InvalidDataException($"encounter id \"{stage.Id}\" is used twice (or is an Adventure stage's)");
            BattleData.ValidateStage(stage, where, waves, text);
            if (stage.Survival) throw new InvalidDataException(where + ": an encounter is a clear stage");
            if (stage.Reward != null || stage.Keepsake != null || stage.TipVi != null) throw new InvalidDataException(where + ": an encounter has no reward or tip");
        }
        if (x.Fights == null || x.Fights.Length is < 1 or > 20) throw new InvalidDataException(at + ".fights must list 1–20 encounters");
        if (x.Elites == null || x.Elites.Length > 20) throw new InvalidDataException(at + ".elites must list at most 20 encounters");
        for (int i = 0; i < x.Fights.Length; i++) Encounter(x.Fights[i], $"{at}.fights[{x.Fights[i]?.Id ?? i.ToString()}]");
        for (int i = 0; i < x.Elites.Length; i++) Encounter(x.Elites[i], $"{at}.elites[{x.Elites[i]?.Id ?? i.ToString()}]");
        Encounter(x.Boss, at + ".boss");
        if (!x.Boss!.Waves.Any(w => w.Groups.Any(g => g.Fixed && s.Enemy(g.Enemy).Boss))) throw new InvalidDataException(at + ".boss needs a wave with a boss");
        if (!double.IsFinite(x.RestHeal) || x.RestHeal is < 0 or > 1) throw new InvalidDataException(at + ".restHeal must be 0–1");
        if (x.MaxRelics is < 0 or > 6) throw new InvalidDataException(at + ".maxRelics must be 0–6");
        if (x.EliteRelics is < 0 or > 3) throw new InvalidDataException(at + ".eliteRelics must be 0–3");
        if (x.Relics == null || x.Relics.Length > 12) throw new InvalidDataException(at + ".relics must list at most 12 relics");
        static void Desc(string? vi, string? en, string where)
        {
            if (string.IsNullOrWhiteSpace(vi) || string.IsNullOrWhiteSpace(en) || vi.Length > 120 || en.Length > 120) throw new InvalidDataException(where + " descriptions must be 1–120 characters");
        }
        var relicIds = new HashSet<string>();
        foreach (var r in x.Relics)
        {
            string where = $"{at}.relics[{r?.Id}]";
            if (r == null) throw new InvalidDataException(at + ".relics has an empty entry");
            id(r.Id, where + ".id");
            if (!relicIds.Add(r.Id)) throw new InvalidDataException($"relic id \"{r.Id}\" is used twice");
            text(r.NameVi, where + ".nameVi"); text(r.NameEn, where + ".nameEn"); Desc(r.DescVi, r.DescEn, where);
            if (Array.IndexOf(RelicSpec.Effects, r.Effect) < 0) throw new InvalidDataException($"{where}.effect must be one of: {string.Join(", ", RelicSpec.Effects)}");
            if (r.Skill != null && BattleSkills.Parse(r.Skill) == null) throw new InvalidDataException($"{where}.skill must be a skill id: {BattleData.KnownSkills}");
            foreach (var (value, name, max) in new[] { (r.Amount, "amount", r.Effect is RelicSpec.Thorns or RelicSpec.Battery ? 0.9 : 2.0), (r.Extra, "extra", 0.5),
                (r.Penalty, "penalty", r.Effect switch { RelicSpec.Light => 50.0, RelicSpec.Counter or RelicSpec.Magnet => 30.0, RelicSpec.Thorns or RelicSpec.Venom or RelicSpec.Frost or RelicSpec.Helmet => 0.9, _ => 2.0 }), (r.Below, "below", 1.0) })
                if (!double.IsFinite(value) || value < 0 || value > max) throw new InvalidDataException($"{where}.{name} must be 0–{max}");
            // 1.5 M23b: a magnet's floor time is at least 2 s (0: unchanged); venom poisons, frost chills, the others leave nothing.
            if (r.Effect == RelicSpec.Magnet && r.Penalty is > 0 and < 2) throw new InvalidDataException(where + ".penalty (seconds on the floor) must be 0 or 2–30");
            string? status = r.Effect switch { RelicSpec.Venom => "poison", RelicSpec.Frost => "chill", _ => null };
            if (status == null ? r.OnHit != null : r.OnHit?.Status != status) throw new InvalidDataException(where + ".onHit " + (status == null ? "is only for venom and frost" : "must be a \"" + status + "\" status"));
            if (r.OnHit != null) StatusHit.Validate(r.OnHit, where + ".onHit");
        }
        if (x.Events == null || x.Events.Length > 12) throw new InvalidDataException(at + ".events must list at most 12 events");
        var eventIds = new HashSet<string>();
        foreach (var e in x.Events)
        {
            string where = $"{at}.events[{e?.Id}]";
            if (e == null) throw new InvalidDataException(at + ".events has an empty entry");
            id(e.Id, where + ".id");
            if (!eventIds.Add(e.Id)) throw new InvalidDataException($"event id \"{e.Id}\" is used twice");
            text(e.NameVi, where + ".nameVi"); text(e.NameEn, where + ".nameEn"); Desc(e.DescVi, e.DescEn, where);
            if (Array.IndexOf(ExpeditionEventSpec.Kinds, e.Kind) < 0) throw new InvalidDataException($"{where}.kind must be one of: {string.Join(", ", ExpeditionEventSpec.Kinds)}");
            if (!double.IsFinite(e.Heal) || e.Heal is < 0 or > 1) throw new InvalidDataException(where + ".heal must be 0–1");
            if (!double.IsFinite(e.Cost) || e.Cost is < 0 or > 0.9) throw new InvalidDataException(where + ".cost must be 0–0.9");
            if (e.Kind is ExpeditionEventSpec.Chest or ExpeditionEventSpec.Shrine && x.Relics.Length == 0) throw new InvalidDataException(where + ": a " + e.Kind + " event needs relics");
            // 1.5 M23b: the forge, the curse and the lost cat.
            bool forge = e.Kind == ExpeditionEventSpec.Forge, curse = e.Kind == ExpeditionEventSpec.Curse, lost = e.Kind == ExpeditionEventSpec.Lost;
            if (e.Coins is < 0 or > 99 || (forge || lost) != e.Coins > 0) throw new InvalidDataException(where + ".coins must be 1–99 for a forge or a lost cat (none otherwise)");
            if (!double.IsFinite(e.Amount) || e.Amount is < 0 or > 1 || (forge || curse) != e.Amount > 0) throw new InvalidDataException(where + ".amount must be above 0, at most 1, for a forge or a curse (none otherwise)");
            if (e.Levels is < 0 or > 5 || forge != e.Levels > 0) throw new InvalidDataException(where + ".levels must be 1–5 for a forge (none otherwise)");
            if (e.Battles is < 0 or > 5 || curse != e.Battles > 0) throw new InvalidDataException(where + ".battles must be 1–5 for a curse (none otherwise)");
            if (!double.IsFinite(e.EliteChance) || e.EliteChance is < 0 or > 1 || !curse && e.EliteChance > 0) throw new InvalidDataException(where + ".eliteChance must be 0–1, for a curse only");
            if (e.Cards is < 0 or > 3 || (curse || lost) != e.Cards > 0) throw new InvalidDataException(where + ".cards must be 1–3 for a curse or a lost cat (none otherwise)");
            if ((forge || lost) && e.Cost <= 0) throw new InvalidDataException(where + ".cost must be above 0 for a " + e.Kind);
        }
        if (x.Keepsakes == null || x.Keepsakes.Length > 12) throw new InvalidDataException(at + ".keepsakes must list at most 12 keepsakes");
        var keepIds = new HashSet<string>();
        foreach (var k in x.Keepsakes)
        {
            string where = $"{at}.keepsakes[{k?.Id}]";
            if (k == null) throw new InvalidDataException(at + ".keepsakes has an empty entry");
            id(k.Id, where + ".id");
            if (!keepIds.Add(k.Id)) throw new InvalidDataException($"keepsake id \"{k.Id}\" is used twice");
            text(k.NameVi, where + ".nameVi"); text(k.NameEn, where + ".nameEn");
            if (Array.IndexOf(KeepsakeSpec.Kinds, k.Kind) < 0) throw new InvalidDataException($"{where}.kind must be one of: {string.Join(", ", KeepsakeSpec.Kinds)}");
            if (k.Kind == KeepsakeSpec.Cup && Array.IndexOf(KeepsakeSpec.Tiers, k.Tier) < 0) throw new InvalidDataException($"{where}.tier must be one of: {string.Join(", ", KeepsakeSpec.Tiers)}");
            if (!Enum.TryParse<BattleDifficulty>(k.Difficulty, out var level) || !Enum.IsDefined(level)) throw new InvalidDataException(where + ".difficulty must be Easy, Normal or Hard");
            if (k.Kind == KeepsakeSpec.AccessoryKind && PetAccessories.Find(k.Accessory) is not { Reward: not null })
                throw new InvalidDataException(where + ".accessory must name a reward accessory of accessories.json");
        }
        if (x.Capsules is CapsuleSpec c)
        {
            foreach (var (value, name) in new[] { (c.FreeHeal, "freeHeal"), (c.FreeCard, "freeCard"), (c.GoldHeal, "goldHeal") })
                if (!double.IsFinite(value) || value is < 0 or > 1) throw new InvalidDataException($"{at}.capsules.{name} must be 0–1");
            if (!double.IsFinite(c.GoldCost) || c.GoldCost is < 0 or > 0.9) throw new InvalidDataException(at + ".capsules.goldCost must be 0–0.9");
            foreach (var (value, name) in new[] { (c.Relic, "relic"), (c.Rare, "rare"), (c.CardHeal, "cardHeal") })
                if (!double.IsFinite(value) || value is < 0 or > 1000) throw new InvalidDataException($"{at}.capsules.{name} must be 0–1000");
            if (c.Relic + c.Rare + c.CardHeal <= 0) throw new InvalidDataException(at + ".capsules needs a gold weight above 0");
        }
        if (x.Store is ShopSpec shop)
        {
            foreach (var (value, name) in new[] { (shop.HealPrice, "healPrice"), (shop.CardPrice, "cardPrice"), (shop.RarePrice, "rarePrice"), (shop.RelicPrice, "relicPrice") })
                if (value is < 1 or > 99) throw new InvalidDataException($"{at}.shop.{name} must be 1–99 coins");
            if (!double.IsFinite(shop.Heal) || shop.Heal is <= 0 or > 1) throw new InvalidDataException(at + ".shop.heal must be above 0, at most 1");
            if (shop.Cards is < 0 or > 4 || shop.Rares is < 0 or > 2 || shop.Relics is < 0 or > 2) throw new InvalidDataException(at + ".shop sells 0–4 cards, 0–2 rare cards and 0–2 relics");
        }
        // Zones: in step order, together as many steps as the map has before the boss, each with fights.
        if (x.Zones == null || x.Zones.Length > 10) throw new InvalidDataException(at + ".zones must list at most 10 zones");
        if (x.Zones.Length > 0)
        {
            var zoneIds = new HashSet<string>();
            foreach (var z in x.Zones)
            {
                string where = $"{at}.zones[{z?.Id}]";
                if (z == null) throw new InvalidDataException(at + ".zones has an empty entry");
                id(z.Id, where + ".id");
                if (!zoneIds.Add(z.Id)) throw new InvalidDataException($"zone id \"{z.Id}\" is used twice");
                text(z.NameVi, where + ".nameVi"); text(z.NameEn, where + ".nameEn");
                if (z.Steps is < 1 or > 10) throw new InvalidDataException(where + ".steps must be 1–10");
                if (z.Fights == null || z.Fights.Length == 0 || z.Fights.Any(f => x.Fights.All(e => e.Id != f))) throw new InvalidDataException(where + ".fights must list fight encounters of the route");
                if (z.Elites == null || z.Elites.Any(f => x.Elites.All(e => e.Id != f))) throw new InvalidDataException(where + ".elites must list elite encounters of the route");
            }
            if (x.Zones.Sum(z => z.Steps) != x.PathSteps) throw new InvalidDataException(at + ".zones must cover every step before the boss");
        }
        if (x.KindGacha is KindGachaSpec g)
        {
            if (x.Steps is { Length: > 0 }) throw new InvalidDataException(at + " has both steps and kindGacha: use one");
            if (g.Steps is < 1 or > 10) throw new InvalidDataException(at + ".kindGacha.steps must be 1–10");
            if (g.MinLanes is < 1 or > 3 || g.MaxLanes is < 1 or > 3 || g.MinLanes > g.MaxLanes) throw new InvalidDataException(at + ".kindGacha lanes must be 1–3, minLanes ≤ maxLanes");
            if (g.Weights == null || g.Weights.Count == 0) throw new InvalidDataException(at + ".kindGacha.weights must list node kinds");
            if (!double.IsFinite(g.RerollCost) || g.RerollCost is < 0 or > 0.5) throw new InvalidDataException(at + ".kindGacha.rerollCost must be 0–0.5");
            foreach (var (kind, weight) in g.Weights)
            {
                if (Array.IndexOf(Kinds, kind) < 0) throw new InvalidDataException($"{at}.kindGacha.weights kinds must be one of: {string.Join(", ", Kinds)}");
                if (!double.IsFinite(weight) || weight is < 0 or > 1000) throw new InvalidDataException($"{at}.kindGacha.weights.{kind} must be 0–1000");
            }
            if (g.Weights.GetValueOrDefault(Fight) <= 0) throw new InvalidDataException(at + ".kindGacha.weights.fight must be above 0");
            if (g.Weights.GetValueOrDefault(Event) > 0 && x.Events.Length == 0) throw new InvalidDataException(at + ".kindGacha draws events but there are none");
            if (g.Weights.GetValueOrDefault(Elite) > 0 && x.Elites.Length == 0) throw new InvalidDataException(at + ".kindGacha draws elites but there are none");
            if (g.Weights.GetValueOrDefault(Shop) > 0 && x.Store == null) throw new InvalidDataException(at + ".kindGacha draws shops but there is no shop");
            for (int i = 0; i < x.PathSteps; i++)
                if (x.Room(Fight, i) == 0) throw new InvalidDataException($"{at}: step {i + 1} has no fight encounter to draw");
            return;
        }
        if (x.Steps == null || x.Steps.Length is < 1 or > 10) throw new InvalidDataException(at + ".steps must list 1–10 steps");
        for (int i = 0; i < x.Steps.Length; i++)
        {
            var step = x.Steps[i];
            if (step == null || step.Length is < 1 or > 3) throw new InvalidDataException($"{at}.steps[{i}] must list 1–3 nodes");
            foreach (var kind in step)
            {
                if (Array.IndexOf(Kinds, kind) < 0) throw new InvalidDataException($"{at}.steps[{i}] node kinds must be one of: {string.Join(", ", Kinds)}");
                // A step's nodes get different encounters (or events): its pool needs enough of them.
                if (kind == Shop && x.Store == null) throw new InvalidDataException($"{at}.steps[{i}] has a shop but there is no shop");
                if (x.Room(kind, i) < step.Count(k => k == kind)) throw new InvalidDataException($"{at}.steps[{i}] has more {kind} nodes than {(kind is Rest or Shop ? "one" : kind + " encounters")}");
            }
        }
    }
}

// The node-kind gacha: a run's map shows only the boss at first; each step's nodes (MinLanes to
// MaxLanes of them) are drawn when the run reaches that step (the map's Spin), by Weights, and kept.
// The draw always follows these rules: step 1 has no elite or rest; a step has at most one rest and
// never only elites; the step before the boss has a rest; never more nodes of a kind than its pool.
// RerollCost: spinning a drawn step again (once a step, before going to one of its nodes) costs
// this share of the max HP; 0: no second spin.
public sealed class KindGachaSpec
{
    public int Steps { get; init; } = 5;
    public int MinLanes { get; init; } = 2;
    public int MaxLanes { get; init; } = 3;
    public Dictionary<string, double> Weights { get; init; } = new();
    public double RerollCost { get; init; }
}

// The capsule machine after a won fight node (not an elite: it leaves relics; not the boss). Both
// capsules are drawn when the battle is won and kept, shown only once opened; the user picks before
// knowing. Free: a common card from the battle's offer (FreeCard of the time) or FreeHeal of the max
// HP. Gold, for GoldCost of the max HP: by weight, a relic (while the run has room), a rare card it
// can still take, or a card with GoldHeal of the max HP.
public sealed class CapsuleSpec
{
    public double FreeHeal { get; init; } = 0.1;
    public double FreeCard { get; init; } = 0.5;
    public double GoldCost { get; init; } = 0.12;
    public double GoldHeal { get; init; } = 0.1;
    public double Relic { get; init; } = 40;
    public double Rare { get; init; } = 30;
    public double CardHeal { get; init; } = 30;
}

// 1.5 M23: the cat shop (a "shop" node). Its wares are drawn when the node is visited and kept:
// Heal of the max HP, Cards common cards and Rares rare cards the build can still take, Relics relics
// (while the run has room). Each sells once, for the run's coins (ExpeditionRun.Coins).
public sealed class ShopSpec
{
    public double Heal { get; init; } = 0.2;
    public int HealPrice { get; init; } = 2;
    public int CardPrice { get; init; } = 3;
    public int RarePrice { get; init; } = 5;
    public int RelicPrice { get; init; } = 6;
    public int Cards { get; init; } = 2;
    public int Rares { get; init; } = 1;
    public int Relics { get; init; } = 1;
}

// 1.5 M23: a route's zone: Steps steps in a row drawing their fights and elites from these ids.
public sealed class ExpeditionZoneSpec
{
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public int Steps { get; init; } = 1;
    public string[] Fights { get; init; } = Array.Empty<string>();
    public string[] Elites { get; init; } = Array.Empty<string>();
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
}

// A keepsake (Expedition milestone 4): given once, the first time a run is won at Difficulty or
// harder (only at it when Exact). cup: a trophy (Tier bronze, silver, gold) on the display corner;
// cushion: a slime cushion there; accessory: a reward accessory (accessories.json) the cat can wear;
// hourglass, orb: Infinity's displayed keepsakes (EndlessSpec.Milestones, given for Wave instead);
// plush (1.5 M22): the Zip Slime plush, an Adventure stage's keepsake (StageSpec.Keepsake).
public sealed class KeepsakeSpec
{
    public const string Cup = "cup", Cushion = "cushion", AccessoryKind = "accessory", Hourglass = "hourglass", Orb = "orb", Plush = "plush";
    public static readonly string[] Kinds = { Cup, Cushion, AccessoryKind, Hourglass, Orb, Plush };
    // Infinity's milestones only: the wave to clear (0 in the Expedition).
    public int Wave { get; init; }
    public static readonly string[] Tiers = { "bronze", "silver", "gold" };
    public string Id { get; init; } = "";
    public string Kind { get; init; } = Cup;
    public string? Tier { get; init; }
    public string Difficulty { get; init; } = nameof(BattleDifficulty.Easy);
    public bool Exact { get; init; }
    public string? Accessory { get; init; }
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    [JsonIgnore] public bool Shown => Kind is Cup or Cushion or Hourglass or Orb or Plush;
    public bool EarnedAt(BattleDifficulty level) =>
        Enum.TryParse<BattleDifficulty>(Difficulty, out var at) && (Exact ? level == at : level >= at);
}

// An event node (Expedition milestone 3): what it offers is said before choosing.
// swap: give up one card of the build for one of three new ones (or leave).
// chest: safe (Heal of the max HP back) or risky (Cost of the max HP for one relic drawn at random).
// shrine: one of two relics shown, for Cost of the max HP (or leave).
// 1.5 M23b: forge: the weapon deals Amount more for the rest of the run, for Coins coins or Cost of
//   the max HP (at most Levels times a run). curse: one of Cards rare cards; the next Battles battles'
//   enemies are Amount stronger and EliteChance likelier to be elite (or leave). lost: Cost of the max
//   HP for Coins coins and one of Cards cards (or leave).
public sealed class ExpeditionEventSpec
{
    public const string Swap = "swap", Chest = "chest", Shrine = "shrine", Forge = "forge", Curse = "curse", Lost = "lost";
    public static readonly string[] Kinds = { Swap, Chest, Shrine, Forge, Curse, Lost };
    public string Id { get; init; } = "";
    public string Kind { get; init; } = Swap;
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public double Heal { get; init; }
    public double Cost { get; init; }
    public int Coins { get; init; }
    public double Amount { get; init; }
    public int Levels { get; init; }
    public int Battles { get; init; }
    public double EliteChance { get; init; }
    public int Cards { get; init; }
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    public string Describe(string language) => PetText.Data(language, DescVi, DescEn);
}

// A relic: kept for the rest of the run only, a gain with a price (BattleSim applies it as its own
// modifier; the cards and skills are never changed).
// counter: after the Bubble shield or Turtle shell stops a hit, the next landed swing deals Amount
//   more; the Shield's cooldown is Penalty seconds longer.
// relief: the Healing fish heals Amount more; its cooldown is Penalty longer (a share).
// reckless: Amount more damage while HP is below Below of the max; Penalty more damage taken always.
// light: Amount more running speed and Extra less time between swings; Penalty less max HP.
// 1.5 M23b:
// magnet: Amount likelier drops; a drop lies on the floor at most Penalty seconds.
// thorns: Amount of the HP a slime's own blow (its strike or dash) took goes back to it; Penalty less swing damage.
// venom, frost: every landed swing leaves OnHit (poison, chill); venom: Penalty more time between
//   swings; frost: Penalty less swing damage.
// battery: the skills recharge Amount faster; no potions drop.
// helmet: a roll, a spring or a burrower coming up never stuns the cat; Penalty less running speed.
public sealed class RelicSpec
{
    public const string Counter = "counter", Relief = "relief", Reckless = "reckless", Light = "light",
        Magnet = "magnet", Thorns = "thorns", Venom = "venom", Frost = "frost", Battery = "battery", Helmet = "helmet";
    public static readonly string[] Effects = { Counter, Relief, Reckless, Light, Magnet, Thorns, Venom, Frost, Battery, Helmet };
    // venom and frost: the status every landed swing leaves (poison, chill).
    public StatusHit? OnHit { get; init; }
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string Effect { get; init; } = Counter;
    public double Amount { get; init; }
    public double Extra { get; init; }
    public double Penalty { get; init; }
    public double Below { get; init; }
    // A skill id ("shield", "heal"…): only offered to a run that carries that skill.
    public string? Skill { get; init; }
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    public string Describe(string language) => PetText.Data(language, DescVi, DescEn);
}

// The run in progress: the drawn map, the steps won (the lane picked at each), the cat's HP and
// build. Fighting: the lane of the step's battle under way (saved when it starts); still set when
// the run is read again, the battle was left (closed, crashed) and costs RetreatCost.
public sealed class ExpeditionRun
{
    public string RunId { get; set; } = "";
    public string Expedition { get; set; } = "";
    public int RulesVersion { get; set; }
    public int Seed { get; set; }
    public string Weapon { get; set; } = BattleWeapon.Sword;
    public string Difficulty { get; set; } = nameof(BattleDifficulty.Normal);
    // The app icons were turned off when the run set out: none in its battles, its records kept apart.
    public bool NoApps { get; set; }
    public List<BattleAppKind>? AppLoadout { get; set; }
    // The five skills the run set out with (null: BattleSkills.Default; read through BattleSkills.Loadout).
    public List<string>? Skills { get; set; }
    [JsonIgnore] public BattleSkill[] Loadout => BattleSkills.Loadout(Skills);
    // Encounter ids per step, in lane order (the boss's step last). With the kind gacha a step not
    // drawn yet is empty, and Kinds holds each drawn step's node kinds (null: the data's fixed steps).
    public List<string[]> Map { get; set; } = new();
    public List<string[]>? Kinds { get; set; }
    public List<int> Path { get; set; } = new();
    public double Hp { get; set; }
    public Dictionary<string, int> Build { get; set; } = new();
    public int? Fighting { get; set; }
    public int Retreats { get; set; }
    public double Seconds { get; set; }
    public int Defeated { get; set; }
    // The relics held (milestone 3), and the choice open now: a rest or event node visited, or the
    // relics a won elite offers. Its options were drawn once and are kept, so reopening the map,
    // closing the app or resuming never draws them again.
    public List<string> Relics { get; set; } = new();
    public ExpeditionChoice? Pending { get; set; }
    // What happened so far (won battles, left battles, choices made), for the Bag's run log. Only
    // shown: the rules never read it, and a file without it reads as an empty log.
    public List<ExpeditionLogEntry> Log { get; set; } = new();
    public const int LogKept = 60;
    // Steps spun a second time (each at most once).
    public List<int> Rerolled { get; set; } = new();
    // 1.5 M19: the coins the run's won battles picked up (BattleSim.Coins), for its shop; they end with
    // the run (never kept past it). A file without it reads as 0.
    public int Coins { get; set; }
    // 1.5 M23b: the forge's levels and the weapon damage they add (a share) for the rest of the run;
    // a curse: battles still cursed, and how much stronger (a share) and likelier elite their enemies are.
    public int Forged { get; set; }
    public double WeaponBonus { get; set; }
    public int Cursed { get; set; }
    public double CurseThreat { get; set; }
    public double CurseElite { get; set; }

    [JsonIgnore] public int Step => Path.Count;
    [JsonIgnore] public BattleDifficulty Level => Enum.TryParse<BattleDifficulty>(Difficulty, out var level) && Enum.IsDefined(level) ? level : BattleDifficulty.Normal;
}

// An open choice. Kind: "rest", "event" (Event: its id) at node Lane of the step to go now, or
// "reward" after a won elite / "capsule" after a won fight (the step is already passed). Cards and
// Relics: what is offered. Free and Gold: a capsule's hidden prizes ("heal", "card:<id>",
// "rare:<id>", "relic:<id>").
public sealed class ExpeditionChoice
{
    public const string RestKind = "rest", EventKind = "event", RewardKind = "reward", CapsuleKind = "capsule", ShopKind = "shop";
    public string Kind { get; set; } = RestKind;
    public int Lane { get; set; }
    public string? Event { get; set; }
    public string[] Cards { get; set; } = Array.Empty<string>();
    public string[] Relics { get; set; } = Array.Empty<string>();
    public string? Free { get; set; }
    public string? Gold { get; set; }
    // 1.5 M23: a shop's wares (prizes as a capsule's: "heal", "card:<id>", "rare:<id>", "relic:<id>")
    // and the ones bought already.
    public string[] Wares { get; set; } = Array.Empty<string>();
    public List<string> Sold { get; set; } = new();
}

// One line of a run's log. Kind: a won battle's node kind (fight, elite, boss), "retreat" (a battle
// left), or the choice's kind (rest, event, reward, capsule) with the option taken and what it gave.
// Hp: what the run's HP changed by.
public sealed class ExpeditionLogEntry
{
    public const string Retreat = "retreat", Reroll = "reroll", Buy = "buy";
    public int Step { get; set; }
    // 1.5 M23: coins a shop purchase (1.5 M23b: or the forge) cost; negative: given (the lost cat).
    public int Coins { get; set; }
    public string Kind { get; set; } = "";
    public string? Option { get; set; }
    public string? Event { get; set; }
    public string? Card { get; set; }
    public string? Dropped { get; set; }
    public string? Relic { get; set; }
    public string? Prize { get; set; }
    public double Hp { get; set; }
}

// What resolving a choice did: HP gained (or lost), the card taken and dropped, the relic gained,
// and for a capsule the prize it held.
// Coins (1.5 M23b): what the run's coins changed by.
public readonly record struct ExpeditionChoiceOutcome(bool Done, string Option, double Hp = 0, string? Card = null, string? Dropped = null, string? Relic = null, string? Prize = null, int Coins = 0);

// Best results per "expedition|r<rulesVersion>|weapon|difficulty": runs ended, runs won, the most
// steps won in one run, the fastest won run (fight time only).
public sealed class ExpeditionRecord
{
    public int Runs { get; set; }
    public int Wins { get; set; }
    public int BestSteps { get; set; }
    public double? BestSeconds { get; set; }
}

// expedition.json next to preferences.json, apart from the Adventure progress: the run in progress
// and what finished runs left. Saved like the Adventure progress: atomic (.tmp moved over the old
// file, kept as .bak), a broken file read from its .bak or started over, a newer version's file read
// as far as it is understood and never overwritten.
public sealed class ExpeditionProgress
{
    // 2: relics and open choices (milestone 3). 3: keepsakes (milestone 4). 4: the kind gacha and
    // capsules (run Kinds, choice Free/Gold). An older file is read as is (an older run ends when the
    // map opens: the rules changed); an older app reads this one without overwriting it.
    // 5 (1.5 M23): routes (a run's Expedition is its route's id) and the shop (choice Wares/Sold).
    // 6 (1.5 M23b): the forge and the curse (run Forged/WeaponBonus, Cursed/CurseThreat/CurseElite).
    public const int CurrentVersion = 6;
    public int Version { get; set; } = CurrentVersion;
    public ExpeditionRun? Active { get; set; }
    public Dictionary<string, ExpeditionRecord> Records { get; set; } = new();
    // The last finished runs already counted, so one is never counted twice.
    public List<string> Runs { get; set; } = new();
    // Keepsake ids given (each once, never taken back).
    public List<string> Keepsakes { get; set; } = new();
    public const int RunsKept = 50;
    [JsonIgnore] public bool ReadOnly { get; private set; }

    public static string DefaultPath => Path.Combine(PetPreferences.DataDirectory, "expedition.json");

    public static ExpeditionProgress Load(string path)
    {
        var progress = JsonStore.ReadOrBackup<ExpeditionProgress>(path, "Expedition progress");
        if (progress == null) return new();
        progress.ReadOnly = progress.Version > CurrentVersion;
        if (!progress.ReadOnly) progress.Version = CurrentVersion;
        progress.Records = (progress.Records ?? new()).Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null).ToDictionary(pair => pair.Key, pair => pair.Value);
        progress.Runs = (progress.Runs ?? new()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().TakeLast(RunsKept).ToList();
        progress.Keepsakes = (progress.Keepsakes ?? new()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (progress.Active is ExpeditionRun run)
        {
            run.Map ??= new(); run.Path ??= new(); run.Build ??= new(); run.Relics ??= new(); run.Rerolled ??= new();
            run.Log = (run.Log ?? new()).Where(e => e != null).TakeLast(ExpeditionRun.LogKept).ToList();
            // A hand-edited file never makes the forge or the curse more than the data allows.
            run.Forged = Math.Clamp(run.Forged, 0, 5);
            run.WeaponBonus = double.IsFinite(run.WeaponBonus) ? Math.Clamp(run.WeaponBonus, 0, 5) : 0;
            run.Cursed = Math.Clamp(run.Cursed, 0, 5);
            run.CurseThreat = double.IsFinite(run.CurseThreat) ? Math.Clamp(run.CurseThreat, 0, 1) : 0;
            run.CurseElite = double.IsFinite(run.CurseElite) ? Math.Clamp(run.CurseElite, 0, 1) : 0;
            if (run.Pending != null)
            {
                run.Pending.Cards ??= Array.Empty<string>(); run.Pending.Relics ??= Array.Empty<string>();
                run.Pending.Wares ??= Array.Empty<string>(); run.Pending.Sold ??= new();
            }
        }
        return progress;
    }

    public bool Save(string path)
    {
        if (ReadOnly)
        {
            Trace.WriteLine("Expedition progress comes from a newer version of the pet; it is not overwritten.");
            return false;
        }
        return JsonStore.Write(path, this, "Expedition progress", indented: true, backup: true);
    }
}

// What one step's battle (or leaving it) changed. RunOver: the run ended (Won: at the boss).
// Reward: a won elite left relics to choose from on the map.
// Keepsakes: the ones this won run gave (each only ever once).
// Capsule: a won fight left the capsule machine on the map.
public readonly record struct ExpeditionStepOutcome(bool Counted, int Step, bool Won, bool RunOver, bool RunWon, double Hp, double MaxHp, double Healed = 0, bool BestSteps = false, bool Fastest = false, bool Reward = false,
    IReadOnlyList<string>? Keepsakes = null, bool Capsule = false, int Coins = 0, int CoinsGained = 0);

// One battle node the user can fight now: its encounter, its kind, and the battle it starts.
// 1.5 M23b: Threat has a curse in it; EliteBonus: the curse's elite chance; WeaponBonus: the forge's.
public readonly record struct ExpeditionNode(int Step, int Lane, string Kind, StageSpec Stage, int Seed, double Threat, double EliteBonus = 0, double WeaponBonus = 0, bool Cursed = false);

// The Expedition's rules over its data and the saved progress (no WPF, no files: the caller saves).
public sealed class Expedition
{
    public const string BossKind = "boss";
    public ExpeditionSpec Spec { get; }
    public BattleSettings Settings { get; }
    public ExpeditionProgress Progress { get; }

    // route (1.5 M23): the route to show and start (null or unknown: the first); a run under way is
    // always on its own route (one the data no longer has: the first, where it ends as changed).
    public Expedition(BattleSettings settings, ExpeditionProgress progress, string? route = null)
    {
        Settings = settings;
        var main = settings.Expedition ?? throw new ArgumentException("The battle data has no expedition.");
        Spec = progress.Active is ExpeditionRun active ? main.Route(active.Expedition) ?? main : main.Route(route) ?? main;
        Progress = progress;
    }

    public ExpeditionRun? Run => Progress.Active;
    // False with the user's own battle data: its runs give no keepsakes.
    public bool GrantKeepsakes { get; init; } = true;
    public bool IsUnlocked(AdventureProgress adventure) => IsUnlocked(Settings, Spec, adventure);
    public static bool IsUnlocked(BattleSettings settings, ExpeditionSpec spec, AdventureProgress adventure) => spec.UnlockAfter is not string stage || adventure.Completed.Contains(stage)
        || settings.Campaign?.Chapters.Any(c => c.Stages.Any(s => s.Id == stage)) != true;
    public static string RecordKey(ExpeditionSpec spec, string weapon, BattleDifficulty difficulty, bool noApps = false, int? rulesVersion = null) =>
        $"{spec.Id}|r{rulesVersion ?? spec.RulesVersion}|{weapon}|{difficulty}{(noApps ? "|noapps" : "")}";
    // rulesVersion: an older run's (KeepRuns) records are its own version's.
    public ExpeditionRecord Record(string weapon, BattleDifficulty difficulty, bool noApps = false, int? rulesVersion = null) =>
        Progress.Records.TryGetValue(RecordKey(Spec, weapon, difficulty, noApps, rulesVersion), out var record) ? record : new();
    // A run going on under rules from before the shop (KeepRuns, ShopFrom): its steps still to spin draw no shop.
    private bool Older(ExpeditionRun run) => run.RulesVersion < Spec.ShopFrom;
    private bool Draws(ExpeditionRun run, string kind) => Spec.KindGacha!.Weights.GetValueOrDefault(kind) > 0 && !(kind == ExpeditionSpec.Shop && Older(run));

    // The cat's max HP with a build (cards like Big heart raise it) and relics, as the battle would have it.
    public double MaxHp(IReadOnlyDictionary<string, int> build, IReadOnlyList<string>? relics = null) => new BattleSim(Settings, 0, 1000, build: build, seed: 0, relics: relics).CatMaxHp;
    public double MaxHp(ExpeditionRun run) => MaxHp(run.Build, run.Relics);

    // A new run (replacing none: the caller ends the old one first). The map is drawn from the
    // seed once and kept, so closing and opening the map, or resuming, never draws it again.
    // With the kind gacha only the boss is drawn now; each step is drawn by Roll when the run gets there.
    public ExpeditionRun Start(string weapon, BattleDifficulty difficulty, int seed, string? runId = null, bool noApps = false, IReadOnlyList<BattleSkill>? skills = null)
    {
        var random = BattleRandom.Stream(seed, 50);
        var map = new List<string[]>();
        List<string[]>? kinds = null;
        if (Spec.KindGacha != null)
        {
            kinds = Enumerable.Range(0, Spec.PathSteps).Select(_ => Array.Empty<string>()).ToList();
            map.AddRange(kinds.Select(_ => Array.Empty<string>()));
        }
        else foreach (var step in Spec.Steps) map.Add(DrawEncounters(step, map.Count, map, random));
        map.Add(new[] { Spec.Boss!.Id });
        var run = new ExpeditionRun
        {
            RunId = runId ?? Guid.NewGuid().ToString("N"), Expedition = Spec.Id, RulesVersion = Spec.RulesVersion, Seed = seed,
            Weapon = Settings.Weapon(weapon).Id, Difficulty = difficulty.ToString(), NoApps = noApps, Map = map, Kinds = kinds,
            Skills = BattleSkills.Save(BattleSkills.Order(skills ?? BattleSkills.Default) ?? BattleSkills.Default)
        };
        run.Hp = MaxHp(run);
        Progress.Active = run;
        return run;
    }

    // A step's content for its node kinds: an encounter or event each (never one the step already
    // has; among the rest, one the run has used least so far), "rest" for a rest.
    private string[] DrawEncounters(IReadOnlyList<string> kinds, int step, IEnumerable<string[]> before, Random random, ExpeditionRun? run = null)
    {
        var uses = new Dictionary<string, int>();
        foreach (var id in before.SelectMany(row => row)) uses[id] = uses.GetValueOrDefault(id) + 1;
        var row = new string[kinds.Count];
        for (int lane = 0; lane < kinds.Count; lane++)
        {
            if (kinds[lane] is ExpeditionSpec.Rest or ExpeditionSpec.Shop) { row[lane] = kinds[lane]; continue; }
            var options = (kinds[lane] == ExpeditionSpec.Event ? Spec.Events.Select(e => e.Id) : Spec.Pool(kinds[lane], step).Select(s => s.Id)).Where(id => !row.Contains(id)).ToList();
            // 1.5 M23b: no forge the run can no longer use (unless nothing else is left).
            if (run != null && options.Where(id => Spec.FindEvent(id) is not { Kind: ExpeditionEventSpec.Forge } f || run.Forged < f.Levels).ToList() is { Count: > 0 } useful) options = useful;
            int least = options.Min(id => uses.GetValueOrDefault(id));
            options = options.Where(id => uses.GetValueOrDefault(id) == least).ToList();
            var pick = options[random.Next(options.Count)];
            row[lane] = pick;
            uses[pick] = least + 1;
        }
        return row;
    }

    // The step to go now still has to be drawn (kind gacha: the map's Spin).
    public bool NeedsRoll => Run is ExpeditionRun run && run.Kinds != null && run.Pending == null && run.Fighting == null
        && run.Step < Spec.PathSteps && run.Step < run.Kinds.Count && run.Kinds[run.Step].Length == 0;

    // Whether a step's nodes are known (always, without the kind gacha).
    public bool Rolled(int step) => step >= Spec.PathSteps || Run?.Kinds is not List<string[]> kinds || step < kinds.Count && kinds[step].Length > 0;

    // Spin: the step to go now gets its node kinds and encounters, from the run's seed (the same
    // whenever it is drawn) and kept. False when there is nothing to draw (a double click).
    public bool Roll()
    {
        if (!NeedsRoll || Spec.KindGacha is null) return false;
        Draw(Run!, Run!.Step, BattleRandom.Stream(Run.Seed, 6000 + Run.Step));
        return true;
    }

    // Spin again: the price in HP (RerollCost of the max HP), and whether the step to go now can be
    // spun again — drawn, not spun again yet, no node entered or visited, and the cat survives it.
    public double RerollPrice(ExpeditionRun run) => Math.Round(MaxHp(run) * (Spec.KindGacha?.RerollCost ?? 0));
    public bool CanReroll => Run is ExpeditionRun run && Spec.KindGacha is { RerollCost: > 0 } && run.Kinds != null && run.Pending == null
        && run.Fighting == null && run.Step < Spec.PathSteps && run.Step < run.Kinds.Count && run.Kinds[run.Step].Length > 0
        && !run.Rerolled.Contains(run.Step) && run.Hp > RerollPrice(run);

    // The step's nodes drawn again from the run's seed (another stream than the first spin, so the
    // same whenever it happens; a draw equal to the old one is skipped), paid in HP and logged.
    public bool Reroll()
    {
        if (!CanReroll) return false;
        var run = Run!;
        int step = run.Step;
        double price = RerollPrice(run);
        var oldKinds = run.Kinds![step];
        var oldMap = run.Map[step];
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Draw(run, step, BattleRandom.Stream(run.Seed, 7000 + step * 8 + attempt));
            if (!run.Kinds[step].SequenceEqual(oldKinds) || !run.Map[step].SequenceEqual(oldMap)) break;
        }
        run.Hp -= price;
        run.Rerolled.Add(step);
        AddLog(run, new() { Step = step, Kind = ExpeditionLogEntry.Reroll, Hp = -price });
        return true;
    }

    // A step's node kinds and encounters, drawn by the kind gacha's rules and kept in the run.
    private void Draw(ExpeditionRun run, int step, Random random)
    {
        var g = Spec.KindGacha!;
        bool first = step == 0, beforeBoss = step == Spec.PathSteps - 1 && !first;
        int lanes = random.Next(g.MinLanes, g.MaxLanes + 1);
        var kinds = new List<string>();
        if (beforeBoss) kinds.Add(ExpeditionSpec.Rest);
        while (kinds.Count < lanes)
        {
            var allowed = ExpeditionSpec.Kinds.Where(kind => Draws(run, kind) && !(first && kind is ExpeditionSpec.Elite or ExpeditionSpec.Rest or ExpeditionSpec.Shop)
                && kinds.Count(k => k == kind) < Spec.Room(kind, step)).ToList();
            if (allowed.Count == 0) break;
            double roll = random.NextDouble() * allowed.Sum(kind => g.Weights[kind]);
            string pick = allowed[^1];
            foreach (var kind in allowed) { roll -= g.Weights[kind]; if (roll < 0) { pick = kind; break; } }
            kinds.Add(pick);
        }
        // Never only elites: the last one becomes a fight (the fights' pool always has one left then).
        if (kinds.All(k => k == ExpeditionSpec.Elite)) kinds[^1] = ExpeditionSpec.Fight;
        // Shuffled, so the rest before the boss is not always on top.
        for (int i = kinds.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (kinds[i], kinds[j]) = (kinds[j], kinds[i]); }
        run.Map[step] = DrawEncounters(kinds, step, run.Map.Take(step), random, run);
        run.Kinds![step] = kinds.ToArray();
    }

    // Whether a node's content (an encounter id, "rest", an event id) fits its kind in this data.
    private bool Fits(string kind, string? id) => kind switch
    {
        ExpeditionSpec.Rest => id == ExpeditionSpec.Rest,
        ExpeditionSpec.Shop => id == ExpeditionSpec.Shop && Spec.Store != null,
        ExpeditionSpec.Event => Spec.FindEvent(id) != null,
        BossKind => id == Spec.Boss?.Id,
        _ => Spec.Pool(kind).Any(s => s.Id == id)
    };

    // Null when the saved run still fits this data; otherwise why it cannot go on.
    public string? Mismatch(ExpeditionRun run)
    {
        if (run.Expedition != Spec.Id || run.RulesVersion != Spec.RulesVersion && !Spec.KeepRuns.Contains(run.RulesVersion)) return "the expedition's rules changed";
        if (run.Map.Count != Spec.StepCount || run.Path.Count >= run.Map.Count) return "the map changed";
        // A run drawn with the kind gacha needs data with it, and the other way round.
        if ((run.Kinds != null) != (Spec.KindGacha != null) || run.Kinds != null && run.Kinds.Count != Spec.PathSteps) return "the map changed";
        for (int i = 0; i < run.Map.Count; i++)
        {
            int lanes = i >= Spec.PathSteps ? 1 : run.Kinds?[i]?.Length ?? (Spec.KindGacha != null ? -1 : Spec.Steps[i].Length);
            if (run.Map[i] == null || lanes is < 0 or > 3 || run.Map[i].Length != lanes) return "the map changed";
            // Steps passed (and one being fought) were drawn.
            if (lanes == 0 && i <= run.Step && (i < run.Step || run.Fighting != null || run.Pending is { Kind: ExpeditionChoice.RestKind or ExpeditionChoice.EventKind or ExpeditionChoice.ShopKind })) return "the map changed";
            for (int lane = 0; lane < lanes; lane++) if (!Fits(KindOf(run, i, lane), run.Map[i][lane])) return "the map changed";
        }
        if (run.Path.Where((lane, i) => lane < 0 || lane >= run.Map[i].Length).Any()) return "the map changed";
        if (run.Fighting is int f && (f < 0 || f >= run.Map[run.Step].Length || !ExpeditionSpec.IsBattle(KindOf(run, run.Step, f)) && KindOf(run, run.Step, f) != BossKind)) return "the map changed";
        if (run.Relics.Any(id => Spec.Relic(id) == null)) return "the relics changed";
        if (run.Pending is ExpeditionChoice open)
        {
            bool node = open.Kind is ExpeditionChoice.RestKind or ExpeditionChoice.EventKind or ExpeditionChoice.ShopKind;
            bool PrizeFits(string? prize) => Prize(prize) switch
            {
                (Heal, _) => true,
                (PrizeCard or PrizeRare, string id) => Settings.UpgradeList.Any(u => u.Id == id),
                (PrizeRelic, string id) => Spec.Relic(id) != null,
                _ => false
            };
            if (run.Fighting != null || open.Kind is not (ExpeditionChoice.RestKind or ExpeditionChoice.EventKind or ExpeditionChoice.RewardKind or ExpeditionChoice.CapsuleKind or ExpeditionChoice.ShopKind)
                || node && (open.Lane < 0 || open.Lane >= run.Map[run.Step].Length || KindOf(run, run.Step, open.Lane) != open.Kind || open.Kind == ExpeditionChoice.EventKind && run.Map[run.Step][open.Lane] != open.Event)
                || open.Kind == ExpeditionChoice.CapsuleKind && (!PrizeFits(open.Free) || !PrizeFits(open.Gold))
                || !open.Wares.All(PrizeFits) || open.Sold.Any(w => !open.Wares.Contains(w))
                || open.Relics.Any(id => Spec.Relic(id) == null) || open.Cards.Any(id => Settings.UpgradeList.All(u => u.Id != id)))
                return "the map changed";
        }
        return null;
    }

    // A node's kind: "fight", "elite", "rest", "event", "shop", "boss", or "" for a node not drawn yet.
    public string Kind(int step, int lane) => KindOf(Run, step, lane);
    private string KindOf(ExpeditionRun? run, int step, int lane)
    {
        if (step >= Spec.PathSteps) return BossKind;
        if (Spec.KindGacha == null) return Spec.Steps[step][lane];
        return run?.Kinds is { } kinds && step < kinds.Count && lane >= 0 && lane < kinds[step].Length ? kinds[step][lane] : "";
    }

    // A battle node of the step to go now (null: not one, or a choice is still open).
    public ExpeditionNode? Node(int lane)
    {
        if (Run is not ExpeditionRun run || run.Pending != null || run.Step >= run.Map.Count || lane < 0 || lane >= run.Map[run.Step].Length) return null;
        if (!ExpeditionSpec.IsBattle(Kind(run.Step, lane)) && Kind(run.Step, lane) != BossKind) return null;
        if (Spec.Encounter(run.Map[run.Step][lane]) is not StageSpec stage) return null;
        // Each node's battle has its own seed: fighting it again (after leaving it) plays the same.
        int seed = BattleRandom.Stream(run.Seed, 1000 + run.Step * 8 + lane).Next();
        // 1.5 M23b: a curse makes the battle's enemies stronger and likelier elite; the forge's bonus goes along.
        bool cursed = run.Cursed > 0;
        return new(run.Step, lane, Kind(run.Step, lane), stage, seed, Spec.Threat(run.Step) * (cursed ? 1 + run.CurseThreat : 1),
            cursed ? run.CurseElite : 0, run.WeaponBonus, cursed);
    }

    // The node's battle has started: from now on leaving it costs RetreatCost.
    public bool Enter(int lane)
    {
        if (Run is not ExpeditionRun run || run.Fighting != null || Node(lane) == null) return false;
        run.Fighting = lane;
        return true;
    }

    // The step's battle ended in a win or a loss. A win moves the run on (HP and build kept, WinHeal
    // given back, its coins added); a won boss or any loss ends the run and counts it once.
    public ExpeditionStepOutcome Finish(bool won, double hp, IReadOnlyDictionary<string, int> build, double seconds, int defeated, int coins = 0)
    {
        if (Run is not ExpeditionRun run || run.Fighting is not int lane) return default;
        run.Fighting = null;
        if (won) run.Coins += Math.Max(0, coins);
        run.Seconds += double.IsFinite(seconds) && seconds > 0 ? seconds : 0;
        run.Defeated += Math.Max(0, defeated);
        int step = run.Step;
        if (!won) return End(run, false, step, 0, MaxHp(run));
        run.Path.Add(lane);
        // 1.5 M23b: a won battle is one cursed battle less.
        if (run.Cursed > 0 && --run.Cursed == 0) run.CurseThreat = run.CurseElite = 0;
        run.Build = build.Where(card => card.Value > 0).ToDictionary(card => card.Key, card => card.Value);
        double max = MaxHp(run);
        if (run.Step >= run.Map.Count) return End(run, true, step, Math.Clamp(hp, 0, max), max);
        double before = Math.Clamp(hp, 1, max), started = run.Hp;
        run.Hp = Math.Min(max, before + Spec.WinHeal * max);
        AddLog(run, new() { Step = step, Kind = Kind(step, lane), Hp = run.Hp - started });
        // A won elite: relics to choose from (drawn now, kept with the run).
        bool reward = false, capsule = false;
        if (Kind(step, lane) == ExpeditionSpec.Elite && DrawRelics(run, Spec.EliteRelics, BattleRandom.Stream(run.Seed, 4000 + step * 8 + lane)) is { Length: > 0 } relics)
        {
            run.Pending = new() { Kind = ExpeditionChoice.RewardKind, Lane = lane, Relics = relics };
            reward = true;
        }
        // A won fight: the capsule machine (both capsules drawn now, kept hidden).
        else if (Kind(step, lane) == ExpeditionSpec.Fight && Spec.Capsules is CapsuleSpec c)
        {
            run.Pending = DrawCapsules(run, c, BattleRandom.Stream(run.Seed, 5000 + step * 8 + lane), lane);
            capsule = true;
        }
        return new(true, step, true, false, false, run.Hp, max, run.Hp - before, Reward: reward, Capsule: capsule, Coins: run.Coins, CoinsGained: Math.Max(0, coins));
    }

    public const string PrizeCard = "card", PrizeRare = "rare", PrizeRelic = "relic";
    // A capsule prize split into its kind and id: ("heal", null), ("card", id), ("rare", id), ("relic", id).
    public static (string Kind, string? Id) Prize(string? prize) =>
        prize == null ? ("", null) : prize.IndexOf(':') is int at and > 0 ? (prize[..at], prize[(at + 1)..]) : (prize, null);

    private ExpeditionChoice DrawCapsules(ExpeditionRun run, CapsuleSpec c, Random random, int lane)
    {
        bool Rare(string id) => Settings.UpgradeList.FirstOrDefault(u => u.Id == id)?.Rare == true;
        string free = Heal;
        var commons = DrawCards(run, random.Next()).Where(id => !Rare(id)).ToArray();
        if (random.NextDouble() < c.FreeCard && commons.Length > 0) free = PrizeCard + ":" + commons[random.Next(commons.Length)];
        var gold = new List<(string Prize, double Weight)>();
        if (DrawRelics(run, 1, random) is { Length: 1 } relic && c.Relic > 0) gold.Add((PrizeRelic + ":" + relic[0], c.Relic));
        var rares = Settings.UpgradeList.Where(u => u.Rare && (u.MaxStacks == 0 || run.Build.GetValueOrDefault(u.Id) < u.MaxStacks)).ToList();
        if (rares.Count > 0 && c.Rare > 0) gold.Add((PrizeRare + ":" + rares[random.Next(rares.Count)].Id, c.Rare));
        var cards = DrawCards(run, random.Next());
        if (cards.Length > 0 && c.CardHeal > 0) gold.Add((PrizeCard + ":" + cards[random.Next(cards.Length)], c.CardHeal));
        string goldPrize = Heal;
        double roll = random.NextDouble() * gold.Sum(g => g.Weight);
        foreach (var (prize, weight) in gold) { goldPrize = prize; roll -= weight; if (roll < 0) break; }
        return new() { Kind = ExpeditionChoice.CapsuleKind, Lane = lane, Free = free, Gold = goldPrize };
    }

    // Up to count relics the run does not hold, in a drawn order; none once it holds MaxRelics.
    private string[] DrawRelics(ExpeditionRun run, int count, Random random)
    {
        if (run.Relics.Count >= Spec.MaxRelics) return Array.Empty<string>();
        var carried = run.Loadout;
        var left = Spec.Relics.Where(r => r.Skill == null || Array.IndexOf(carried, BattleSkills.Parse(r.Skill)) >= 0)
            .Select(r => r.Id).Where(id => !run.Relics.Contains(id)).ToList();
        var drawn = new List<string>();
        while (drawn.Count < count && left.Count > 0) { int i = random.Next(left.Count); drawn.Add(left[i]); left.RemoveAt(i); }
        return drawn.ToArray();
    }

    // Cards offered outside a battle (a rest, a swap): the battle's own offer for this build, from a
    // battle seeded by the node, at full HP (no instant heal: resting is the heal).
    private string[] DrawCards(ExpeditionRun run, int seed)
    {
        var sim = new BattleSim(Settings, 0, 1000, build: run.Build, seed: seed, relics: run.Relics, skills: run.Loadout);
        return sim.OfferCards()?.Select(u => u.Id).ToArray() ?? Array.Empty<string>();
    }

    // Go to a rest or event node of the step to go now: its choice opens (drawn once, kept).
    public bool Visit(int lane)
    {
        if (Run is not ExpeditionRun run || run.Pending != null || run.Fighting != null || run.Step >= run.Map.Count || lane < 0 || lane >= run.Map[run.Step].Length) return false;
        string kind = Kind(run.Step, lane);
        if (kind is not (ExpeditionSpec.Rest or ExpeditionSpec.Event or ExpeditionSpec.Shop)) return false;
        var random = BattleRandom.Stream(run.Seed, 3000 + run.Step * 8 + lane);
        var choice = new ExpeditionChoice { Kind = kind, Lane = lane };
        if (kind == ExpeditionSpec.Rest) choice.Cards = DrawCards(run, random.Next());
        else if (kind == ExpeditionSpec.Shop) choice.Wares = DrawWares(run, Spec.Store!, random);
        else
        {
            var e = Spec.FindEvent(run.Map[run.Step][lane])!;
            choice.Event = e.Id;
            if (e.Kind == ExpeditionEventSpec.Swap) choice.Cards = DrawCards(run, random.Next());
            // 1.5 M23b: the curse's rare cards, the lost cat's cards; the forge offers nothing to pick.
            else if (e.Kind == ExpeditionEventSpec.Curse) choice.Cards = DrawRares(run, e.Cards, random);
            else if (e.Kind == ExpeditionEventSpec.Lost) choice.Cards = DrawCards(run, random.Next()).Take(e.Cards).ToArray();
            else if (e.Kind != ExpeditionEventSpec.Forge) choice.Relics = DrawRelics(run, e.Kind == ExpeditionEventSpec.Chest ? 1 : 2, random);
        }
        run.Pending = choice;
        return true;
    }

    // A rest open (1.5 M06): the run's five skills may change for free, as often as wanted; the heal or
    // card is still to choose. A card on offer for a skill no longer carried is taken out (no new draw).
    // open: whether a skill may be carried (the setup panel's rule). False: no rest open, not five
    // different skills, or one not open; the same five again changes nothing.
    public bool ChangeSkills(IReadOnlyList<BattleSkill> skills, Func<BattleSkill, bool> open)
    {
        if (Run is not ExpeditionRun run || run.Pending is not { Kind: ExpeditionChoice.RestKind } rest || run.Fighting != null
            || BattleSkills.Order(skills) is not BattleSkill[] ordered || !ordered.All(open)) return false;
        run.Skills = BattleSkills.Save(ordered);
        rest.Cards = rest.Cards.Where(id => Settings.UpgradeList.FirstOrDefault(u => u.Id == id) is not { Skill: string need }
            || BattleSkills.Parse(need) is BattleSkill skill && Array.IndexOf(ordered, skill) >= 0).ToArray();
        return true;
    }

    public const string Heal = "heal", Card = "card", Safe = "safe", Risk = "risk", SwapOption = "swap", Take = "take", Leave = "leave", Free = "free", Gold = "gold";

    // The options the open choice has: rest (heal, card), swap (swap, leave), chest (safe, risk),
    // shrine and a won elite (take, leave), the capsule machine (free, gold, leave).
    public string[] Options()
    {
        if (Run?.Pending is not ExpeditionChoice open) return Array.Empty<string>();
        if (open.Kind == ExpeditionChoice.RestKind) return new[] { Heal, Card };
        if (open.Kind == ExpeditionChoice.RewardKind) return new[] { Take, Leave };
        if (open.Kind == ExpeditionChoice.CapsuleKind) return new[] { Free, Gold, Leave };
        if (open.Kind == ExpeditionChoice.ShopKind) return new[] { Leave };
        return Spec.FindEvent(open.Event)?.Kind switch
        {
            ExpeditionEventSpec.Swap => new[] { SwapOption, Leave },
            ExpeditionEventSpec.Chest => new[] { Safe, Risk },
            // 1.5 M23b: the forge (pay in coins or in HP), the curse and the lost cat (a card from the offer).
            ExpeditionEventSpec.Forge => new[] { PayCoins, PayHp, Leave },
            ExpeditionEventSpec.Curse or ExpeditionEventSpec.Lost => new[] { Card, Leave },
            _ => new[] { Take, Leave }
        };
    }

    public const string PayCoins = "coins", PayHp = "hp";
    // The open event (null: none open, or not an event).
    private ExpeditionEventSpec? OpenEvent => Run?.Pending is { Kind: ExpeditionChoice.EventKind } open ? Spec.FindEvent(open.Event) : null;

    // HP an option costs (a share of the max HP; 0: free).
    public double Cost(string option)
    {
        if (Run is not ExpeditionRun run || run.Pending is not ExpeditionChoice open) return 0;
        if (open.Kind == ExpeditionChoice.CapsuleKind) return option == Gold ? (Spec.Capsules?.GoldCost ?? 0) * MaxHp(run) : 0;
        if (open.Kind != ExpeditionChoice.EventKind) return 0;
        var e = Spec.FindEvent(open.Event);
        return e != null && (option == Risk && e.Kind == ExpeditionEventSpec.Chest || option == Take && e.Kind == ExpeditionEventSpec.Shrine
            || option == PayHp && e.Kind == ExpeditionEventSpec.Forge || option == Card && e.Kind == ExpeditionEventSpec.Lost) ? e.Cost * MaxHp(run) : 0;
    }

    // 1.5 M23b: coins an option costs (negative: gives).
    public int CoinCost(string option) => OpenEvent switch
    {
        { Kind: ExpeditionEventSpec.Forge } e when option == PayCoins => e.Coins,
        { Kind: ExpeditionEventSpec.Lost } e when option == Card => -e.Coins,
        _ => 0
    };

    // HP an option gives back now.
    public double Gain(string option)
    {
        if (Run is not ExpeditionRun run || run.Pending is not ExpeditionChoice open) return 0;
        double max = MaxHp(run), share = option == Heal && open.Kind == ExpeditionChoice.RestKind ? Spec.RestHeal
            : option == Safe ? Spec.FindEvent(open.Event)?.Heal ?? 0
            // A capsule's heal (its prize stays hidden: only Choose uses this for it).
            : open.Kind == ExpeditionChoice.CapsuleKind && Spec.Capsules is CapsuleSpec c && option is Free or Gold
                ? Prize(option == Free ? open.Free : open.Gold).Kind switch { Heal => option == Free ? c.FreeHeal : c.GoldHeal, PrizeCard when option == Gold => c.GoldHeal, _ => 0 } : 0;
        return Math.Max(0, Math.Min(share * max, max - run.Hp));
    }

    // Null when the option can be taken now (with this card, card to drop, relic), otherwise why not.
    public string? Refusal(string option, string? card = null, string? drop = null, string? relic = null)
    {
        if (Run is not ExpeditionRun run || run.Pending is not ExpeditionChoice open || !Options().Contains(option)) return "no such choice";
        switch (option)
        {
            case Card:
                if (open.Cards.Length == 0) return "no cards";
                if (Cost(option) > 0 && run.Hp - Cost(option) < 1) return "too weak";
                return open.Cards.Contains(card) ? null : "pick a card";
            case PayCoins:
            case PayHp:
                if (OpenEvent is not { } forge || run.Forged >= forge.Levels) return "maxed";
                if (option == PayCoins) return run.Coins < forge.Coins ? "too poor" : null;
                return run.Hp - Cost(option) < 1 ? "too weak" : null;
            case SwapOption:
                if (run.Build.Count == 0) return "no card to give up";
                if (drop == null || !run.Build.ContainsKey(drop)) return "pick a card to give up";
                return open.Cards.Contains(card) ? null : "pick a card";
            case Risk:
            case Take:
                if (open.Relics.Length == 0 || run.Relics.Count >= Spec.MaxRelics) return "no relic";
                if (Cost(option) > 0 && run.Hp - Cost(option) < 1) return "too weak";
                return option == Risk || open.Relics.Contains(relic) ? null : "pick a relic";
            case Gold:
                return run.Hp - Cost(option) < 1 ? "too weak" : null;
            default:
                return null;
        }
    }

    // Resolve the open choice once. A node's choice passes its step; a won elite's only closes.
    public ExpeditionChoiceOutcome Choose(string option, string? card = null, string? drop = null, string? relic = null)
    {
        if (Refusal(option, card, drop, relic) != null || Run is not ExpeditionRun run || run.Pending is not ExpeditionChoice open) return default;
        double before = run.Hp;
        run.Hp = Math.Max(1, run.Hp - Cost(option)) + Gain(option);
        string? taken = null, dropped = null, gained = null;
        if (option is Card or SwapOption)
        {
            if (option == SwapOption)
            {
                dropped = drop;
                if (--run.Build[drop!] <= 0) run.Build.Remove(drop!);
            }
            taken = card;
            run.Build[card!] = run.Build.GetValueOrDefault(card!) + 1;
            // More max HP comes with HP now, as it does in a battle.
            if (Settings.UpgradeList.FirstOrDefault(u => u.Id == card) is { Effect: "maxHp" } heart) run.Hp += heart.Amount;
        }
        if (option is Risk or Take)
        {
            gained = option == Risk ? open.Relics[0] : relic;
            run.Relics.Add(gained!);
        }
        // 1.5 M23b: the forge's level, the curse on the battles ahead, the coins paid or given.
        int coins = CoinCost(option);
        run.Coins -= coins;
        if (OpenEvent is ExpeditionEventSpec e)
        {
            if (option is PayCoins or PayHp) { run.Forged++; run.WeaponBonus += e.Amount; }
            if (option == Card && e.Kind == ExpeditionEventSpec.Curse)
            {
                run.Cursed = Math.Max(run.Cursed, e.Battles);
                run.CurseThreat = Math.Max(run.CurseThreat, e.Amount);
                run.CurseElite = Math.Max(run.CurseElite, e.EliteChance);
            }
        }
        // A capsule: its prize (a heal came with Gain above; a gold card also brings GoldHeal).
        string? prize = open.Kind == ExpeditionChoice.CapsuleKind && option is Free or Gold ? (option == Free ? open.Free : open.Gold) : null;
        var (prizeCard, prizeRelic) = GivePrize(run, prize);
        taken ??= prizeCard; gained ??= prizeRelic;
        run.Hp = Math.Min(run.Hp, MaxHp(run));
        // The step the choice belongs to: a node's is the step to go now, a won battle's the one passed.
        int at = open.Kind is ExpeditionChoice.RewardKind or ExpeditionChoice.CapsuleKind ? run.Step - 1 : run.Step;
        AddLog(run, new() { Step = Math.Max(0, at), Kind = open.Kind, Option = option, Event = open.Event, Card = taken, Dropped = dropped, Relic = gained, Prize = prize, Hp = run.Hp - before, Coins = coins });
        if (open.Kind is not (ExpeditionChoice.RewardKind or ExpeditionChoice.CapsuleKind)) run.Path.Add(open.Lane);
        run.Pending = null;
        return new(true, option, run.Hp - before, taken, dropped, gained, prize, -coins);
    }

    // A capsule's or shop's card or relic, given to the run (a heal is the caller's): the card and relic gained.
    private (string? Card, string? Relic) GivePrize(ExpeditionRun run, string? prize)
    {
        switch (Prize(prize))
        {
            case (PrizeCard or PrizeRare, string id):
                run.Build[id] = run.Build.GetValueOrDefault(id) + 1;
                // More max HP comes with HP now, as it does in a battle.
                if (Settings.UpgradeList.FirstOrDefault(u => u.Id == id) is { Effect: "maxHp" } bigHeart) run.Hp += bigHeart.Amount;
                return (id, null);
            case (PrizeRelic, string id) when run.Relics.Count < Spec.MaxRelics && !run.Relics.Contains(id):
                run.Relics.Add(id);
                return (null, id);
        }
        return (null, null);
    }

    // 1.5 M23: a shop's wares, drawn once when it is visited: a heal, common and rare cards the build
    // can still take (the battle's own offer for commons), relics while the run has room.
    private string[] DrawWares(ExpeditionRun run, ShopSpec shop, Random random)
    {
        var wares = new List<string> { Heal };
        var commons = DrawCards(run, random.Next()).Where(id => Settings.UpgradeList.FirstOrDefault(u => u.Id == id)?.Rare != true).ToList();
        if (commons.Count < shop.Cards) commons = commons.Union(DrawCards(run, random.Next()).Where(id => Settings.UpgradeList.FirstOrDefault(u => u.Id == id)?.Rare != true)).ToList();
        wares.AddRange(commons.Take(shop.Cards).Select(id => PrizeCard + ":" + id));
        wares.AddRange(DrawRares(run, shop.Rares, random).Select(id => PrizeRare + ":" + id));
        wares.AddRange(DrawRelics(run, shop.Relics, random).Select(id => PrizeRelic + ":" + id));
        return wares.ToArray();
    }

    // Up to count different rare cards the build can still take (a skill's only when it is carried).
    private string[] DrawRares(ExpeditionRun run, int count, Random random)
    {
        var carried = run.Loadout;
        var rares = Settings.UpgradeList.Where(u => u.Rare && (u.MaxStacks == 0 || run.Build.GetValueOrDefault(u.Id) < u.MaxStacks)
            && (u.Skill == null || BattleSkills.Parse(u.Skill) is BattleSkill skill && Array.IndexOf(carried, skill) >= 0)).Select(u => u.Id).ToList();
        var drawn = new List<string>();
        for (int i = 0; i < count && rares.Count > 0; i++) { int at = random.Next(rares.Count); drawn.Add(rares[at]); rares.RemoveAt(at); }
        return drawn.ToArray();
    }

    // A shop ware's price in coins (0: not a ware of this data's shop).
    public int Price(string ware) => Spec.Store is not ShopSpec shop ? 0 : Prize(ware).Kind switch
    {
        Heal => shop.HealPrice, PrizeCard => shop.CardPrice, PrizeRare => shop.RarePrice, PrizeRelic => shop.RelicPrice, _ => 0
    };

    // HP a shop's heal gives back now.
    public double ShopHeal() => Run is ExpeditionRun run && Spec.Store is ShopSpec shop ? Math.Max(0, Math.Min(shop.Heal * MaxHp(run), MaxHp(run) - run.Hp)) : 0;

    // Null when the ware can be bought now: "no shop", "sold", "too poor", "no relic" (the run is full),
    // "full" (nothing to heal).
    public string? BuyRefusal(string ware)
    {
        if (Run is not ExpeditionRun run || run.Pending is not { Kind: ExpeditionChoice.ShopKind } shop || !shop.Wares.Contains(ware)) return "no shop";
        if (shop.Sold.Contains(ware)) return "sold";
        if (Prize(ware).Kind == PrizeRelic && run.Relics.Count >= Spec.MaxRelics) return "no relic";
        if (ware == Heal && ShopHeal() < 0.5) return "full";
        return run.Coins < Price(ware) ? "too poor" : null;
    }

    // Buy one ware of the open shop: its coins paid, what it gives given, logged; the shop stays open.
    public ExpeditionChoiceOutcome Buy(string ware)
    {
        if (BuyRefusal(ware) != null) return default;
        var run = Run!;
        var shop = run.Pending!;
        double before = run.Hp;
        int price = Price(ware);
        run.Coins -= price;
        if (ware == Heal) run.Hp += ShopHeal();
        var (card, relic) = GivePrize(run, ware);
        run.Hp = Math.Min(run.Hp, MaxHp(run));
        shop.Sold.Add(ware);
        AddLog(run, new() { Step = run.Step, Kind = ExpeditionChoice.ShopKind, Option = ExpeditionLogEntry.Buy, Prize = ware, Card = card, Relic = relic, Coins = price, Hp = run.Hp - before });
        return new(true, ExpeditionLogEntry.Buy, run.Hp - before, card, null, relic, ware);
    }

    private static void AddLog(ExpeditionRun run, ExpeditionLogEntry entry)
    {
        run.Log.Add(entry);
        if (run.Log.Count > ExpeditionRun.LogKept) run.Log.RemoveAt(0);
    }

    // The step's battle was left (End, closing, a crash): the step stays to be fought again, for
    // RetreatCost of the max HP. HP gone ends the run as lost.
    public ExpeditionStepOutcome Retreat()
    {
        if (Run is not ExpeditionRun run || run.Fighting == null) return default;
        run.Fighting = null;
        run.Retreats++;
        double max = MaxHp(run);
        run.Hp -= Spec.RetreatCost * max;
        if (run.Hp <= 0.5) return End(run, false, run.Step, 0, max);
        AddLog(run, new() { Step = run.Step, Kind = ExpeditionLogEntry.Retreat, Hp = -Spec.RetreatCost * max });
        return new(true, run.Step, false, false, false, run.Hp, max, -Spec.RetreatCost * max);
    }

    // The user gives the run up: counted as ended where it is.
    public ExpeditionStepOutcome Abandon()
    {
        if (Run is not ExpeditionRun run) return default;
        return End(run, false, run.Step, run.Hp, MaxHp(run));
    }

    private ExpeditionStepOutcome End(ExpeditionRun run, bool won, int step, double hp, double max)
    {
        Progress.Active = null;
        if (Progress.Runs.Contains(run.RunId)) return new(false, step, won, true, won, hp, max);
        Progress.Runs.Add(run.RunId);
        if (Progress.Runs.Count > ExpeditionProgress.RunsKept) Progress.Runs.RemoveAt(0);
        string key = RecordKey(Spec, run.Weapon, run.Level, run.NoApps, run.RulesVersion);
        if (!Progress.Records.TryGetValue(key, out var record)) Progress.Records[key] = record = new();
        record.Runs++;
        bool best = run.Step > record.BestSteps, fastest = false;
        if (best) record.BestSteps = run.Step;
        if (won)
        {
            record.Wins++;
            if (run.Seconds > 0 && (record.BestSeconds is not double s || run.Seconds < s - 0.05)) { record.BestSeconds = run.Seconds; fastest = true; }
        }
        // Keepsakes: given in the same save as the run is counted, so never twice.
        var given = new List<string>();
        if (won && GrantKeepsakes)
            foreach (var k in Spec.Keepsakes)
                if (k.EarnedAt(run.Level) && !Progress.Keepsakes.Contains(k.Id)) { Progress.Keepsakes.Add(k.Id); given.Add(k.Id); }
        return new(true, step, won, true, won, hp, max, 0, best, fastest, Keepsakes: given);
    }
}
