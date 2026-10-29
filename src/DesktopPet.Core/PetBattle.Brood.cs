using System;
using System.Collections.Generic;

namespace DesktopPet;

// 1.5 M18, B2 "Slime Mẹ" (the Slime Mother, chapter 2's side boss; docs/UPDATE-1.5.md):
// - The brood (EnemyType.Brood): now and then, with its shield down and not stunned, it lays a clutch of
//   merging slimes side by side on the cat's side of it (rings on the floor first), within its SummonCap.
// - The clutch merges as any merging slimes do (PetBattle.Links.cs). What it merges into is the mother's
//   too: it walks back to her instead of fighting (nursing) and, next to her, feeds her its own HP until it
//   has none left (gone then, not the cat's kill). Defeating it on the way, or while it feeds, keeps the
//   rest from her. A push, a stun, the laser or a panic hold it up as they hold up anyone.
public sealed partial class BattleSim
{
    public int Clutches { get; private set; }
    public int Nursed { get; private set; }
    public double HpFed { get; private set; }

    private static double BroodWait(Slime boss, BroodSpec brood) => boss.Enraged ? brood.EnragedEvery : brood.Every;

    // Every frame for a brooding boss: its clock to the next clutch, then the warning, then the clutch.
    private void StepBrood(Slime boss, double dt)
    {
        if (boss.Type.Brood is not BroodSpec brood || boss.State is SlimeState.Spawning or SlimeState.Defeated) return;
        if (boss.BroodSpots is { } announced)
        {
            boss.BroodWarn = Math.Max(0, boss.BroodWarn - dt);
            if (boss.BroodWarn > 0) return;
            boss.BroodSpots = null;
            Lay(boss, brood, announced);
            return;
        }
        if (boss.Shielded || boss.State == SlimeState.Stunned || boss.LeapHeight > 0) return;
        boss.BroodClock -= dt * boss.ChillFactor;
        if (boss.BroodClock > 0) return;
        var calls = new List<string>(brood.Count);
        for (int i = 0; i < brood.Count; i++) calls.Add(brood.Enemy);
        // Side by side on the cat's side of her (they huddle at once: a bomb's chance, or a push's).
        var spots = HelperSpots(boss, calls, lastCatX < boss.X ? 1 : -1);
        if (spots.Count == 0) { boss.BroodClock = 1; return; } // No room under her SummonCap: soon again.
        boss.BroodClock = BroodWait(boss, brood);
        if (brood.Warning > 0)
        {
            boss.BroodSpots = spots;
            boss.BroodWarn = brood.Warning;
            events.Add(new(BattleEventKind.BroodWarned, boss.X, boss.Id));
            return;
        }
        Lay(boss, brood, spots);
    }

    private void Lay(Slime boss, BroodSpec brood, List<(string Kind, double X)> spots)
    {
        int laid = SpawnHelpers(boss, spots, 0);
        if (laid == 0) return;
        Clutches++;
        events.Add(new(BattleEventKind.BroodLaid, boss.X, laid));
    }

    // A merge (PetBattle.Links.cs): what one mother's clutch merged into is hers, and heads back to her.
    private void Hatched(Slime merged, IReadOnlyList<Slime> group)
    {
        int mother = group[0].SummonedBy;
        if (mother == 0) return;
        foreach (var s in group) if (s.SummonedBy != mother) return;
        merged.SummonedBy = mother;
        if (Find(mother) is not { Targetable: true, Type.Brood: not null }) return;
        merged.Nursing = true;
        events.Add(new(BattleEventKind.NurseHeading, merged.X, merged.Id));
    }

    // A nursing one's turn (true: that was all it did): back to its mother, then feeding her. With her gone
    // it fights as any other.
    private bool Nurse(Slime slime, double dt, double minX, double maxX)
    {
        if (!slime.Nursing) return false;
        if (Find(slime.SummonedBy) is not { Targetable: true, Type.Brood: BroodSpec brood } mother)
        {
            slime.Nursing = slime.Feeding = false;
            return false;
        }
        // Within what Separate leaves between them, so it never shoves her away.
        double gap = (mother.Width + slime.Width) / 2 * 0.72, dx = mother.X - slime.X;
        if (Math.Abs(dx) > gap + 4)
        {
            slime.Feeding = false; // Pulled away: it comes back for the rest.
            slime.Facing = dx < 0 ? -1 : 1;
            slime.X = Math.Clamp(slime.X + Math.Sign(dx) * Math.Min(Math.Abs(dx) - gap, Speed(slime) * dt), minX, maxX);
            return true;
        }
        slime.Facing = dx < 0 ? -1 : 1;
        slime.Feeding = true;
        double give = Math.Min(slime.Hp, brood.FeedRate * dt);
        slime.Hp -= give;
        slime.FedHp += give;
        mother.Hp = Math.Min(mother.MaxHp, mother.Hp + give);
        HpFed += give;
        // A heart over her for every 10 HP it gives.
        if (Math.Floor(slime.FedHp / 10) > Math.Floor((slime.FedHp - give) / 10)) AddEffect(BattleEffectKind.Heart, mother.X, mother.Height);
        if (slime.Hp > 0.001) return true;
        // All of it given: gone into her (not the cat's kill; drawn as a puff), what it gave told once.
        slime.Feeding = false;
        events.Add(new(BattleEventKind.NurseFed, slime.X, Math.Round(slime.FedHp)));
        slime.Hp = 0;
        slime.Set(SlimeState.Defeated);
        slime.Escaped = true;
        slime.KnockVelocity = 0;
        ClearStatus(slime);
        if (SelectedId == slime.Id) SelectedId = null;
        LinkFallen(slime, false);
        Nursed++;
        AddEffect(BattleEffectKind.Poof, slime.X, slime.Height / 2, 1);
        effects[^1].Tint = slime.Type.Color;
        return true;
    }
}
