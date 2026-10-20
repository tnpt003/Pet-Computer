using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// One battle's randomness: a seed (logged when a battle starts, so a run can be replayed) split into
// separate streams, so a roll added to one part (a new animation, another card) never changes
// what the others draw.
public static class BattleRandom
{
    public const int Spawn = 1, Offer = 2, Block = 3, Affix = 4;

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
    // or a whole squad.
    public static List<string[]> Units(WaveSpec wave, Random random)
    {
        var units = new List<string[]>();
        foreach (var g in wave.Groups)
        {
            if (g.Squad != null)
            {
                var members = g.Squad.SelectMany(m => Enumerable.Repeat(m.Enemy, m.Count)).ToArray();
                for (int i = 0; i < g.Count; i++) units.Add(members);
            }
            else if (g.Pick != null) foreach (var kind in Draw(g, random)) units.Add(new[] { kind });
            else for (int i = 0; i < g.Count; i++) units.Add(new[] { g.Enemy });
        }
        return units;
    }

    // Count kinds from the weights, then AtLeast made good by swapping drawn kinds that are not
    // themselves needed (at random places, so a guaranteed one is not always last).
    private static List<string> Draw(WaveGroup g, Random random)
    {
        var kinds = new List<string>();
        for (int i = 0; i < g.Count; i++) kinds.Add(Pick(g.Pick!, random));
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
