using System;

namespace DesktopPet;

// 1.5 M11, Q7 the splitting slime (EnemyType.Split): defeated, it breaks into smaller halves on the spot
// (they appear like any enemy, a little hurt). The halves never split again. Their "merge" link with
// AfterHeal: once a herb slime has healed each of them and they stand together, they merge back into
// the whole one (PetBattle.Links.cs). The halves always come, even past maxAlive: the next spawns wait
// for room as usual. No random stream is used, so battles without it play exactly as before.
public sealed partial class BattleSim
{
    public int Splits { get; private set; }

    // Just defeated (PetBattle.cs Damage).
    private void SplitApart(Slime slime)
    {
        if (slime.Type.Split is not SplitSpec split || Over) return;
        var half = Settings.Enemy(split.Into);
        int count = slime.Elite != null ? split.EliteCount : split.Count;
        double gap = Settings.SlimeWidth * half.Scale * 0.9;
        for (int i = 0; i < count; i++)
        {
            var piece = AddSlime(slime.X + (i - (count - 1) / 2.0) * gap, half);
            piece.Hp = Math.Max(1, Math.Round(piece.MaxHp * split.Health));
            piece.Facing = slime.Facing;
            // A boss's helper's halves still hold up its shield phase.
            piece.SummonedBy = slime.SummonedBy;
            piece.PhaseTag = slime.PhaseTag;
        }
        Splits++;
        events.Add(new(BattleEventKind.SlimeSplit, slime.X, slime.Id));
    }
}
