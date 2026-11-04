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
        // Merged back by a herb slime's rejoin (2026-10-31): beaten again, it is gone for good.
        if (slime.Type.Split is not SplitSpec split || Over || slime.Rejoined) return;
        var half = Settings.Enemy(split.Into);
        int count = slime.Elite != null ? split.EliteCount : split.Count;
        double gap = Settings.SlimeWidth * half.Scale * 0.9;
        var pieces = new System.Collections.Generic.List<Slime>(count);
        for (int i = 0; i < count; i++)
        {
            var piece = AddSlime(slime.X + (i - (count - 1) / 2.0) * gap, half);
            piece.Hp = Math.Max(1, Math.Round(piece.MaxHp * split.Health));
            piece.Facing = slime.Facing;
            // A boss's helper's halves still hold up its shield phase.
            piece.SummonedBy = slime.SummonedBy;
            piece.PhaseTag = slime.PhaseTag;
            pieces.Add(piece);
        }
        Splits++;
        events.Add(new(BattleEventKind.SlimeSplit, slime.X, slime.Id));
        Rejoin(pieces);
    }

    public int Rejoins { get; private set; }
    // Of a herb slime's heal, what a rejoin gives each half.
    public const double RejoinHeal = 0.5;

    // 2026-10-31: a herb slime (RejoinSplits) on the field heals the fresh halves at once, wherever they are:
    // RejoinHeal × its HealAmount each (its heal power), not from its budget, not a cast (it may be healing
    // another). Healed, they gather and merge back (their merge link's AfterHeal); the one they become, beaten
    // again, is gone for good (it does not split again).
    private void Rejoin(System.Collections.Generic.List<Slime> pieces)
    {
        Slime? herb = null;
        foreach (var s in slimes)
            if (s.Type.RejoinSplits && s.Targetable && !s.Handled && (herb == null || Math.Abs(s.X - pieces[0].X) < Math.Abs(herb.X - pieces[0].X))) herb = s;
        if (herb == null) return;
        double heal = herb.Type.HealAmount * herb.HealPower * RejoinHeal;
        if (herb.HealPower != 1) heal = Math.Round(heal);
        foreach (var piece in pieces)
        {
            piece.Hp = Math.Min(piece.MaxHp, piece.Hp + heal);
            piece.Healed = true;
            piece.Rejoin = true;
            AddEffect(BattleEffectKind.Mend, piece.X, piece.Height / 2);
        }
        Rejoins++;
        events.Add(new(BattleEventKind.HalvesRejoined, herb.X, pieces[0].X));
    }
}
