using System;

namespace DesktopPet;

// 1.5 M18, B3 "Vua Slime biến dị" (the Mutant King, for Infinity and the Expedition; docs/UPDATE-1.5.md). The
// Slime King's shield phases and rally call, plus:
// - Crown links (EnemyType.CrownLinks): each rally also ties a link to the helper it made elite, the kinds in
//   turn: a tether (another helper of the boss's takes a share of the elite's hits, a rope to cut) or a
//   rhythm (the elite beats a drum: on the last beat everyone in its ring strikes at once). A kind the battle
//   has no room for (two kinds of link already) is skipped for the next one; none fits: no link.
// - Goo rain (EnemyType.Rain): now and then, with its shield down, drops fall from the sky around the cat,
//   each ring on the floor first. They land as a spitter's drops do (PetBattle.Spit.cs): lifting the cat or
//   the Bubble shield dodges them.
public sealed partial class BattleSim
{
    public int CrownLinksTied { get; private set; }
    public int Rains { get; private set; }
    public int RainHits { get; private set; }

    // After a rally (PetBattle.Boss.cs): the next crown link that fits, on the helper just made elite.
    private void CrownLink(Slime boss, Slime elite)
    {
        if (boss.Type.CrownLinks is not { Length: > 0 } crown || !elite.Targetable || elite.Link != null) return;
        for (int t = 0; t < crown.Length; t++)
        {
            var link = crown[(boss.CrownTurn + t) % crown.Length];
            if (!link.Enabled || !linkKinds.Contains(link.Kind) && linkKinds.Count >= EnemyLinks.MaxKinds) continue;
            Slime holder = elite;
            if (link.Kind == EnemyLinks.Tether)
            {
                // The nearest other helper of the boss's without a link holds the rope to the elite.
                Slime? best = null;
                foreach (var s in slimes)
                    if (s != elite && s.SummonedBy == boss.Id && s.Targetable && s.Link == null && !s.Riding && !s.Handled && !s.Decoy && !s.Airborne && !s.Burrowed
                        && Math.Abs(s.X - elite.X) <= link.Range && (best == null || Math.Abs(s.X - elite.X) < Math.Abs(best.X - elite.X))) best = s;
                if (best == null || Tethers(elite) >= EnemyLinks.MaxTethers) continue;
                holder = best;
            }
            if (!linkKinds.Contains(link.Kind)) linkKinds.Add(link.Kind);
            holder.Link = link;
            holder.BeatCount = 0;
            holder.BeatClock = 0;
            if (link.Kind == EnemyLinks.Tether)
            {
                holder.LinkedTo = elite.Id;
                events.Add(new(BattleEventKind.LinkFormed, holder.X, holder.Id));
            }
            boss.CrownTurn = (boss.CrownTurn + t + 1) % crown.Length;
            CrownLinksTied++;
            events.Add(new(BattleEventKind.CrownLinked, holder.X, holder.Id));
            return;
        }
    }

    // Every frame for a raining boss: its clock (shield down, not stunned), then the drops.
    private void StepRain(Slime boss, double dt)
    {
        if (boss.Type.Rain is not RainSpec rain || boss.State is SlimeState.Spawning or SlimeState.Defeated) return;
        if (boss.Shielded || boss.State == SlimeState.Stunned || boss.LeapHeight > 0) return;
        boss.RainClock -= dt * boss.ChillFactor;
        if (boss.RainClock > 0) return;
        boss.RainClock = rain.Every;
        Rains++;
        var boost = new SpitSpec();
        double damage = boss.Type.Damage > 0 ? boss.Damage * rain.Damage / boss.Type.Damage : rain.Damage;
        for (int i = 0; i < rain.Drops; i++)
        {
            // The first on the cat's spot, then one step further out on each side in turn.
            int step = (i + 1) / 2, side = i % 2 == 1 ? 1 : -1;
            double x = Math.Clamp(lastCatX + side * step * rain.Spacing, Left, Right);
            goos.Add(new BattleGoo
            {
                OwnerId = boss.Id, FromX = x, X = x, FromHeight = rain.Height, Rain = true, Flight = rain.Warning, Remaining = rain.Warning,
                Damage = damage, Radius = rain.Radius, PuddleSeconds = rain.PuddleSeconds, PuddleRadius = rain.Radius * 1.3,
                DashDistance = boost.DashDistance, DashSpeed = boost.DashSpeed, Tint = boss.Type.Color
            });
        }
        events.Add(new(BattleEventKind.RainCalled, boss.X, boss.Id));
    }
}
