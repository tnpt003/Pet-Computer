using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

public sealed partial class BattleSim
{
    // Each bundle has protection/control and a different source of pressure; no timers or fuser swarms.
    internal static readonly string[][] KingFormationSets =
    {
        new[] { "fortress", "drums", "artillery" },
        new[] { "fortress", "trampoline", "artillery" },
        new[] { "fortress", "trampoline", "drums" },
        new[] { "artillery", "trampoline", "drums" }
    };

    private List<string> KingPhaseCalls(Slime boss, BossPhase phase)
    {
        boss.KingFormationIds = null;
        if (!boss.Type.KingFormations) return Settings.PhaseCalls(phase).ToList();
        var sets = KingFormationSets.Where(set => set.All(id => Settings.Formation(id) is { } formation
                && formation.Members.All(member => !Settings.Enemy(member).Boss && Settings.Enemy(member).Clock == null))
            && set.Sum(id => Settings.Formation(id)!.Members.Count()) <= boss.Type.SummonCap).ToArray();
        if (sets.Length == 0) return Settings.PhaseCalls(phase).ToList();
        boss.KingFormationIds = sets[rallyRandom.Next(sets.Length)];
        return boss.KingFormationIds.SelectMany(id => Settings.Formation(id)!.Members).ToList();
    }
}
