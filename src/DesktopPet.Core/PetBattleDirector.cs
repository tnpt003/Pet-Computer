using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// One battle's randomness: a seed (logged when a battle starts, so a run can be replayed) split into
// separate streams, so a roll added to one part (a new animation, another card) never changes
// what the others draw.
public static class BattleRandom
{
    public const int Spawn = 1, Offer = 2, Block = 3, Affix = 4, Rarity = 5, Apps = 6, Strike = 7, Rally = 8, Utility = 9, Status = 10, Sky = 11;

    // The same seed and stream always give the same numbers, on every machine (no string hashing).
    public static Random Stream(int seed, int stream)
    {
        uint x = unchecked((uint)seed * 0x9E3779B9u + (uint)stream * 0x85EBCA6Bu);
        x ^= x >> 16; x = unchecked(x * 0x7FEB352Du);
        x ^= x >> 15; x = unchecked(x * 0x846CA68Bu);
        x ^= x >> 16;
        return new Random(unchecked((int)x));
    }
}

// One spawn: a single enemy or a whole squad, from the side its group asks for ("a", "b" or either).
// Elite: which of Kinds come elite whatever the chance (WaveGroup.Elite; null: none). Formation: the
// formation whose first part this is (1.5 M12; its banner shows when it comes).
public readonly record struct SpawnUnit(string[] Kinds, string? Side = null, bool[]? Elite = null, string? Formation = null);

// What a wave sends and how strong (no WPF, no state of its own: BattleSim keeps the queue).
public static class SpawnDirector
{
    // One kind from the weights (ordered by id, so the draw never depends on how the file listed them).
    public static string Pick(IReadOnlyDictionary<string, double> weights, Random random)
    {
        var kinds = weights.Where(w => w.Value > 0).OrderBy(w => w.Key, StringComparer.Ordinal).ToList();
        double roll = random.NextDouble() * kinds.Sum(w => w.Value);
        foreach (var (kind, weight) in kinds)
        {
            if (roll < weight) return kind;
            roll -= weight;
        }
        return kinds[^1].Key;
    }

    // The wave's spawns in order, drawn once when it starts. Each unit comes as one: a single enemy
    // or a whole squad. cost: what a kind weighs against the wave's Budget (needed when it has one).
    // formation: the data's formations by id, threat: the battle's now (for "formation" groups).
    public static List<SpawnUnit> Units(WaveSpec wave, Random random, Func<string, double>? cost = null, Func<string, FormationSpec?>? formation = null, double threat = 1)
    {
        var units = new List<SpawnUnit>();
        // A budget: the fixed groups and squads always come; formations take what fits of the rest,
        // then drawn kinds share what is left.
        double left = double.PositiveInfinity;
        if (wave.Budget > 0 && cost != null)
            left = wave.Budget - wave.Groups.Where(g => !g.Drawn).Sum(g => g.Count * (g.Squad?.Sum(m => m.Count * cost(m.Enemy)) ?? cost(g.Enemy)));
        var teams = new Dictionary<WaveGroup, List<FormationSpec>>();
        foreach (var g in wave.Groups)
        {
            if (g.Formation == null || formation == null) continue;
            var drawn = teams[g] = new List<FormationSpec>();
            for (int i = 0; i < g.Count; i++) drawn.Add(DrawFormation(g.Formation, formation, random, threat, cost, ref left));
        }
        foreach (var g in wave.Groups)
        {
            if (g.Squad != null) Squads(units, g, g.Count, g.Side);
            else if (g.Formation != null)
            {
                // Its parts from one edge (the group's, else the far one), a "b" part from the other.
                foreach (var f in teams.GetValueOrDefault(g) ?? new())
                {
                    int first = units.Count;
                    foreach (var part in f.Parts)
                    {
                        string side = g.Side ?? "a";
                        if (part.Side == "b") side = side == "a" ? "b" : "a";
                        if (part.Squad != null) Squads(units, part, 1, side);
                        else for (int i = 0; i < part.Count; i++) units.Add(new(new[] { part.Enemy }, side, part.Elite ? new[] { true } : null));
                    }
                    if (units.Count > first) units[first] = units[first] with { Formation = f.Id };
                }
            }
            else if (g.Pick != null) foreach (var kind in Draw(g, random, cost, ref left)) units.Add(new(new[] { kind }, g.Side, g.Elite ? new[] { true } : null));
            else for (int i = 0; i < g.Count; i++) units.Add(new(new[] { g.Enemy }, g.Side, g.Elite ? new[] { true } : null));
        }
        return units;
    }

    private static void Squads(List<SpawnUnit> units, WaveGroup g, int count, string? side)
    {
        var members = g.Squad!.SelectMany(m => Enumerable.Repeat(m.Enemy, m.Count)).ToArray();
        var elite = g.Squad!.SelectMany(m => Enumerable.Repeat(m.Elite || g.Elite, m.Count)).ToArray();
        for (int i = 0; i < count; i++) units.Add(new(members, side, elite.Any(e => e) ? elite : null));
    }

    // What a formation weighs against a budget: its members' costs added up.
    public static double FormationCost(FormationSpec f, Func<string, double> cost) => f.Members.Sum(cost);

    // One formation of these weights (1.5 M12): among those the threat reaches (none: those asking
    // the least threat), the ones that fit the budget left, drawn by weight; none fits: the cheapest.
    // One roll of the spawn stream each, whatever is left to choose from.
    private static FormationSpec DrawFormation(Dictionary<string, double> weights, Func<string, FormationSpec?> formation, Random random,
        double threat, Func<string, double>? cost, ref double left)
    {
        var known = weights.Where(w => w.Value > 0 && formation(w.Key) != null).ToDictionary(w => w.Key, w => w.Value);
        var reached = known.Where(w => formation(w.Key)!.MinThreat <= threat + 1e-9).ToDictionary(w => w.Key, w => w.Value);
        if (reached.Count == 0)
        {
            double least = known.Keys.Min(k => formation(k)!.MinThreat);
            reached = known.Where(w => formation(w.Key)!.MinThreat == least).ToDictionary(w => w.Key, w => w.Value);
        }
        double Weight(string id) => cost != null ? FormationCost(formation(id)!, cost) : 0;
        double room = left;
        var fits = reached.Where(w => Weight(w.Key) <= room + 1e-9).ToDictionary(w => w.Key, w => w.Value);
        string pick = Pick(fits.Count > 0 ? fits : reached, random);
        if (fits.Count == 0) pick = reached.Keys.OrderBy(Weight).ThenBy(k => k, StringComparer.Ordinal).First();
        left -= Weight(pick);
        return formation(pick)!;
    }

    // What a wave's budget leaves for elites once its kinds are drawn (infinite without a budget).
    public static double BudgetLeft(WaveSpec wave, IEnumerable<SpawnUnit> units, Func<string, double> cost) =>
        wave.Budget > 0 ? wave.Budget - units.Sum(u => u.Kinds.Sum(cost)) : double.PositiveInfinity;

    // The cheapest kind of these weights (ties by id), for a draw that does not fit a budget.
    public static string Cheapest(IReadOnlyDictionary<string, double> weights, Func<string, double> cost) =>
        weights.Where(w => w.Value > 0).OrderBy(w => cost(w.Key)).ThenBy(w => w.Key, StringComparer.Ordinal).First().Key;

    // Count kinds from the weights (one that does not fit the budget left, keeping room for the draws
    // still to come, gives way to the cheapest),
    // then AtLeast made good by swapping drawn kinds that are not themselves needed (at random places,
    // so a guaranteed one is not always last). AtLeast wins over the budget.
    private static List<string> Draw(WaveGroup g, Random random, Func<string, double>? cost, ref double left)
    {
        var kinds = new List<string>();
        string? cheapest = cost != null ? Cheapest(g.Pick!, cost) : null;
        for (int i = 0; i < g.Count; i++)
        {
            string kind = Pick(g.Pick!, random);
            // Room is kept for the draws still to come (at the cheapest kind each).
            if (cost != null && cost(kind) > left - (g.Count - i - 1) * cost(cheapest!)) kind = cheapest!;
            if (cost != null) left -= cost(kind);
            kinds.Add(kind);
        }
        if (g.AtLeast == null) return kinds;
        int Need(string kind) => g.AtLeast.TryGetValue(kind, out int n) ? n : 0;
        foreach (var (kind, n) in g.AtLeast.OrderBy(a => a.Key, StringComparer.Ordinal))
            while (kinds.Count(k => k == kind) < n)
            {
                var spare = Enumerable.Range(0, kinds.Count).Where(i => kinds[i] != kind && kinds.Count(k => k == kinds[i]) > Need(kinds[i])).ToList();
                if (spare.Count == 0) break;
                kinds[spare[random.Next(spare.Count)]] = kind;
            }
        return kinds;
    }

    // HP and damage multipliers for a threat (1: unchanged): HP takes all of it, damage its share.
    public static (double Hp, double Damage) Scale(double threat, double damageShare) => (threat, 1 + (threat - 1) * damageShare);
}
