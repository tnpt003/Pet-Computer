using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// Advanced bosses opt in explicitly. All clocks use simulation time; no Python or window physics.
public enum BossMove { None, Recall, BroodJump, Charge, ChargeRun, Sweep, Burst, Wave, Absorb, Rain, Rest }

public sealed partial class BattleSim
{
    public int NestsBroken { get; private set; }
    public int NurseCordsCut { get; private set; }
    public int BossSpecials { get; private set; }

    private static void BossRest(Slime s, double seconds)
    {
        s.Special = BossMove.Rest;
        s.SpecialLeft = s.SpecialDuration = seconds;
        s.Set(SlimeState.Moving);
    }

    // A nest is an ordinary damageable target, but cannot attack, share hits or merge before hatching.
    private bool StepNest(Slime s, double dt)
    {
        if (!s.BroodNest) return false;
        if (s.State == SlimeState.Spawning) return false; // Let the ordinary spawn transition finish.
        if (Find(s.SummonedBy) is not { Targetable: true, Type.Brood: BroodSpec brood } mother)
        {
            s.BroodNest = false;
            s.Link = s.Type.ActiveLink;
            return false;
        }
        if (s.Handled) return true;
        if (s.State == SlimeState.Stunned)
        {
            s.StunRemaining -= dt;
            if (s.StunRemaining <= 0) s.Set(SlimeState.Moving);
            return true;
        }
        s.NestLeft -= dt * s.ChillFactor;
        if (s.NestLeft > 0) return true;
        // Remove the shell without a kill/reward; its place is available to the hatchlings.
        s.Hp = 0;
        s.Set(SlimeState.Defeated);
        s.Escaped = true;
        var spots = new List<(string Kind, double X)>();
        for (int i = 0; i < brood.Count; i++) spots.Add((brood.Enemy, s.X + (i - (brood.Count - 1) / 2.0) * 26));
        SpawnHelpers(mother, spots, 0);
        AddPuddle(new BattleGoo { OwnerId = mother.Id, X = s.X, PuddleSeconds = 5, PuddleRadius = 65, PuddleSlow = 0.55,
            DashDistance = 1, DashSpeed = 1 });
        AddEffect(BattleEffectKind.Splat, s.X, 4);
        effects[^1].Tint = mother.Type.Color;
        return true;
    }

    private List<(string Kind, double X)> NestSpots(Slime mother, BroodSpec brood)
    {
        var spots = new List<(string Kind, double X)>();
        int nests = slimes.Count(s => s.SummonedBy == mother.Id && s.BroodNest && s.State != SlimeState.Defeated);
        int alive = slimes.Count(s => s.SummonedBy == mother.Id && s.State != SlimeState.Defeated);
        int wanted = mother.Hp <= mother.MaxHp * 0.6 ? 2 : 1;
        for (int i = 0; i < wanted && nests < 2 && alive < mother.Type.SummonCap; i++, nests++, alive++)
        {
            double offset = Math.Min(230, (Right - Left) * 0.25);
            double x = Math.Clamp(mother.X + ((mother.NestTurn + i) % 2 == 0 ? -1 : 1) * offset, Left + 35, Right - 35);
            spots.Add((brood.Enemy, x));
        }
        return spots;
    }

    private void LayNests(Slime mother, BroodSpec brood, List<(string Kind, double X)>? announced = null)
    {
        var spec = mother.Type.BossTactics!;
        var spots = announced ?? NestSpots(mother, brood);
        int room = Math.Min(2 - slimes.Count(s => s.SummonedBy == mother.Id && s.BroodNest && s.State != SlimeState.Defeated),
            mother.Type.SummonCap - slimes.Count(s => s.SummonedBy == mother.Id && s.State != SlimeState.Defeated));
        for (int i = 0; i < spots.Count && i < room; i++)
        {
            var nest = AddSlime(spots[i].X, Settings.Enemy(brood.Enemy));
            mother.NestTurn++;
            nest.SummonedBy = mother.Id;
            nest.BroodNest = true;
            nest.NestLeft = spec.Incubation + i * 1.2;
            nest.Hp = nest.MaxHp = spec.NestHp * mother.MaxHp / mother.Type.MaxHp;
            nest.Link = null;
        }
    }

    // Called after status/freeze processing and before the ordinary attack state machine.
    private bool StepBossTactics(Slime s, double dt)
    {
        if (s.BroodJumpLeft > 0)
        {
            if (s.Handled || s.State == SlimeState.Stunned) { s.BroodJumpLeft = s.LeapHeight = 0; s.Link = s.Type.ActiveLink; return false; }
            s.BroodJumpLeft = Math.Max(0, s.BroodJumpLeft - dt);
            double k = 1 - s.BroodJumpLeft / 0.7;
            s.X = s.SpecialFrom + (s.SpecialAt - s.SpecialFrom) * k;
            s.LeapHeight = 85 * 4 * k * (1 - k);
            if (s.BroodJumpLeft > 0) return true;
            s.LeapHeight = 0;
            s.Link = s.Type.ActiveLink;
            SpecialHit(s, s.SpecialAt, 42, 0.7);
            s.StunRemaining = 1.2;
            s.Set(SlimeState.Stunned);
            return true;
        }
        if (s.Type.BossTactics is not BossTacticsSpec spec || s.State == SlimeState.Spawning) return false;
        s.CordCooldown = Math.Max(0, s.CordCooldown - dt);
        // Shield phases and crowd control interrupt a pending attack; no delayed hit after a stun.
        if (s.Shielded || s.State == SlimeState.Stunned || s.Handled || s.LeapHeight > 0)
        {
            s.Special = BossMove.None;
            s.SpecialLeft = 0;
            s.TacticClock = Math.Max(s.TacticClock, 1.5);
            s.NurseCord = 0;
            return false;
        }
        bool mother = s.Type.Brood != null;
        if (mother)
        {
            if (s.NurseCord != 0)
            {
                s.CordLeft -= dt;
                if (Find(s.NurseCord) is not { Targetable: true, Nursing: true } || s.CordLeft <= 0) s.NurseCord = 0;
            }
            if (s.NurseCord == 0 && s.CordCooldown <= 0)
                foreach (var child in slimes)
                    if (child.SummonedBy == s.Id && child.Targetable && child.Nursing && child.Hp <= child.MaxHp * 0.5 && Math.Abs(child.X - s.X) <= 360)
                    {
                        s.NurseCord = child.Id; s.CordLeft = 6; s.CordCooldown = 14;
                        break;
                    }
            if (!s.LastBrood && s.Hp <= s.MaxHp * 0.2)
            {
                s.LastBrood = true;
                s.Hp = Math.Max(1, s.Hp - s.MaxHp * 0.06);
                LayNests(s, s.Type.Brood!);
                AddEffect(BattleEffectKind.Heart, s.X, s.Height);
            }
        }
        if (s.Special != BossMove.None)
        {
            s.SpecialLeft = Math.Max(0, s.SpecialLeft - dt * s.ChillFactor);
            if (s.Special == BossMove.ChargeRun)
            {
                double previous = s.X;
                s.X = s.SpecialFrom + (s.SpecialAt - s.SpecialFrom) * (1 - s.SpecialLeft / s.SpecialDuration);
                bool inLane = lastCatX >= Math.Min(previous, s.X) - 35 && lastCatX <= Math.Max(previous, s.X) + 35;
                if (!s.SpecialHitOnce && inLane && !CatLifted && Grace <= 0)
                {
                    s.SpecialHitOnce = true;
                    if (SpecialHit(s, lastCatX, 1, 1) && !CrushProof && !Over) Shove(s.Facing, 80, 0.25);
                }
            }
            if (s.SpecialLeft > 0) return true;
            var move = s.Special;
            s.Special = BossMove.None;
            if (move == BossMove.Rest) return true;
            if (move == BossMove.Charge)
            {
                s.Special = BossMove.ChargeRun;
                s.SpecialLeft = s.SpecialDuration = 0.45;
                s.SpecialFrom = s.X;
                s.SpecialHitOnce = false;
                return true;
            }
            ResolveBossMove(s, move);
            if (Over || !s.Targetable) return true;
            s.SpecialCount++;
            if (!mother && s.SpecialCount % 4 == 0) s.GooArmor = 0; // Overload sheds all regenerated armor.
            // A late king chains two announced moves, then has a compulsory opening.
            if (!mother && s.Hp <= s.MaxHp * 0.25 && s.SpecialCount % 2 == 1)
                BeginBossMove(s, move == BossMove.Rain ? BossMove.Wave : BossMove.Burst);
            else BossRest(s, s.SpecialCount % 4 == 0 ? spec.Opening : 1.25);
            s.TacticClock = spec.Every * (s.Enraged ? 0.75 : 1);
            return true;
        }
        s.TacticClock -= dt * s.ChillFactor;
        if (s.TacticClock > 0) return mother && s.NurseCord != 0;
        BossMove next;
        if (mother)
        {
            if (s.MotherFury >= 3) { next = BossMove.Charge; s.MotherFury = 0; }
            else next = s.TacticTurn++ % 2 == 0 ? BossMove.Recall : BossMove.BroodJump;
            // An empty field is no reason to stop attacking: keep fighting and incubating.
            if (next is BossMove.Recall or BossMove.BroodJump)
            {
                bool available = false;
                foreach (var child in slimes)
                    if (child.SummonedBy == s.Id && child.Targetable && !child.BroodNest && !child.Handled && !child.Airborne
                        && (next == BossMove.Recall ? !child.Nursing && child.Hp <= child.MaxHp * 0.5 : child.State == SlimeState.Moving && !child.Nursing))
                    { available = true; break; }
                if (!available) { s.TacticClock = 1; return false; }
            }
        }
        else
        {
            int turn = s.TacticTurn++ % (s.Hp <= s.MaxHp * 0.6 ? 5 : 3);
            next = turn switch { 0 => BossMove.Sweep, 1 => BossMove.Rain, 2 => BossMove.Wave, 3 => BossMove.Absorb, _ => BossMove.Burst };
        }
        BeginBossMove(s, next);
        return true;
    }

    private void BeginBossMove(Slime s, BossMove move)
    {
        s.Set(SlimeState.Moving);
        s.Special = move;
        s.SpecialLeft = s.SpecialDuration = s.Type.BossTactics!.Warning;
        s.SpecialFrom = s.X;
        s.SpecialAt = Math.Clamp(lastCatX, Left + 32, Right - 32);
        s.Facing = s.SpecialAt < s.X ? -1 : 1;
        if (move == BossMove.Absorb)
            foreach (var p in puddles) if (p.OwnerId == s.Id) p.DrainCut = false;
        if (move == BossMove.Charge) s.SpecialAt = Math.Clamp(s.SpecialAt, Math.Max(Left + s.Width / 2, s.X - 220), Math.Min(Right - s.Width / 2, s.X + 220));
        if (move == BossMove.Absorb) s.SpecialLeft = s.SpecialDuration = 2.4;
        BossSpecials++;
        events.Add(new(BattleEventKind.BossSpecialWarned, s.X, s.Id));
    }

    private void ResolveBossMove(Slime boss, BossMove move)
    {
        switch (move)
        {
            case BossMove.Recall:
                foreach (var child in slimes)
                    if (child.SummonedBy == boss.Id && child.Targetable && !child.BroodNest && child.Hp <= child.MaxHp * 0.5 && !child.Handled && !child.Airborne)
                        child.Nursing = true;
                break;
            case BossMove.BroodJump:
                int jumped = 0;
                foreach (var child in slimes)
                    if (jumped < 3 && child.SummonedBy == boss.Id && child.State == SlimeState.Moving && !child.BroodNest && !child.Nursing && !child.Handled && !child.Airborne)
                    {
                        child.Link = null; // The charge breaks this clutch's gathering, restored on landing.
                        child.BroodJumpLeft = 0.7;
                        child.SpecialFrom = child.X;
                        child.SpecialAt = Math.Clamp(boss.SpecialAt + (jumped++ - 1) * 65, Left + child.Width / 2, Right - child.Width / 2);
                    }
                break;
            case BossMove.ChargeRun: break; // Swept movement/hit handled each frame above.
            case BossMove.Sweep:
                SpecialHit(boss, boss.X + boss.Facing * 125, 125, 0.85);
                break;
            case BossMove.Burst:
                if (SpecialHit(boss, boss.X, 160, 1.1) && !CrushProof && !Over) Shove(lastCatX < boss.X ? -1 : 1, 65, 0.25);
                break;
            case BossMove.Wave:
                // Ordered, visible travelling impacts; a wave moves away from the boss and never turns.
                for (int i = 1; i <= 7; i++)
                {
                    double x = boss.X + boss.Facing * i * 65;
                    if (x < Left + 24 || x > Right - 24) break;
                    QueueTacticDrop(boss, x, 0.4 + i * 0.18, 38, boss.Damage * 0.65, 22, 0);
                }
                break;
            case BossMove.Rain:
                TacticRain(boss);
                break;
            case BossMove.Absorb:
                double gained = 0;
                for (int i = puddles.Count - 1; i >= 0; i--)
                    if (puddles[i].OwnerId == boss.Id && !puddles[i].DrainCut && Math.Abs(puddles[i].X - boss.X) <= 650)
                    { gained += boss.MaxHp * 0.025; puddles.RemoveAt(i); }
                boss.GooArmor = Math.Min(boss.MaxHp * 0.1, boss.GooArmor + gained);
                break;
        }
        AddEffect(BattleEffectKind.Slam, boss.X, 0, move == BossMove.Burst ? 160 : 60);
        events.Add(new(BattleEventKind.BossSpecialLanded, boss.X, boss.Id));
    }

    private bool SpecialHit(Slime owner, double x, double radius, double share)
    {
        if (CatLifted || Grace > 0 || Math.Abs(lastCatX - x) > radius) return false;
        return HurtCat(owner, lastCatX, false, share);
    }

    private void QueueTacticDrop(Slime boss, double x, double delay, double radius, double damage, double height, double puddle)
    {
        goos.Add(new BattleGoo { OwnerId = boss.Id, FromX = x, X = x, Flight = delay, Remaining = delay, Rain = true,
            FromHeight = height, Radius = radius, Damage = damage, PuddleRadius = radius * 1.3, PuddleSeconds = puddle,
            PuddleSlow = 0.65, DashDistance = 1.3, DashSpeed = 1.3, Tint = boss.Type.Color });
    }

    private void TacticRain(Slime boss)
    {
        var rain = boss.Type.Rain!;
        // Both beats announced together, including their order. Fixed centre: no tracking on the second beat.
        double centre = boss.SpecialAt;
        double damage = boss.Type.Damage > 0 ? boss.Damage * rain.Damage / boss.Type.Damage : rain.Damage;
        for (int beat = 0; beat < 2; beat++)
            for (int i = 0; i < rain.Drops; i++)
            {
                double x = centre + (i - (rain.Drops - 1) / 2.0) * rain.Spacing + beat * rain.Spacing / 2;
                if (x < Left + rain.Radius || x > Right - rain.Radius) continue;
                QueueTacticDrop(boss, x, rain.Warning + beat * 0.8, rain.Radius, damage, rain.Height, rain.PuddleSeconds);
            }
        Rains++;
        events.Add(new(BattleEventKind.RainCalled, boss.X, boss.Id));
    }

    private double BossTacticDamage(Slime s, double amount)
    {
        if (s.Type.BossTactics != null)
        {
            if (s.Special == BossMove.Rest && s.SpecialCount % 4 == 0) amount *= 1.4;
            if (s.Type.Brood != null)
                foreach (var child in slimes)
                    if (child.SummonedBy == s.Id && child.Targetable && child.Nursing && !child.BroodNest && Math.Abs(child.X - s.X) < (child.Width + s.Width) * 0.65
                        && (lastCatX - s.X) * (child.X - s.X) > 0) { amount *= 0.6; break; }
            double blocked = Math.Min(s.GooArmor, amount);
            s.GooArmor -= blocked;
            amount -= blocked;
        }
        if (Find(s.SummonedBy) is { Targetable: true } mother && mother.NurseCord == s.Id) amount *= 0.5;
        return amount;
    }

    private void TacticDefeated(Slime s)
    {
        if (s.Type.BossTactics != null)
        {
            s.Special = BossMove.None; s.NurseCord = 0;
            goos.RemoveAll(g => g.OwnerId == s.Id);
        }
        if (Find(s.SummonedBy) is not { Targetable: true, Type.BossTactics: not null, Type.Brood: not null } mother) return;
        if (s.BroodNest) NestsBroken++;
        mother.MotherFury++;
        if (mother.NurseCord == s.Id)
        {
            mother.NurseCord = 0;
            mother.BroodClock = Math.Max(mother.BroodClock, 4);
            StaggerTacticBoss(mother, 2);
        }
    }

    private static void StaggerTacticBoss(Slime boss, double seconds)
    {
        boss.Special = BossMove.None;
        boss.TacticClock = Math.Max(boss.TacticClock, seconds + 1);
        boss.StunRemaining = Math.Max(boss.StunRemaining, seconds);
        boss.Set(SlimeState.Stunned);
    }

    private int CutTacticCords(double x0, double h0, double x1, double h1, double pad)
    {
        int cut = 0;
        foreach (var boss in slimes)
        {
            if (!boss.Targetable || boss.Type.BossTactics == null) continue;
            if (boss.NurseCord != 0 && Find(boss.NurseCord) is { Targetable: true } child && RopeCrossed(boss, child, x0, h0, x1, h1, pad))
            {
                boss.NurseCord = 0; boss.CordCooldown = 14;
                child.Nursing = child.Feeding = false;
                child.StunRemaining = 2; child.Set(SlimeState.Stunned);
                boss.BroodClock = Math.Max(boss.BroodClock, 4);
                StaggerTacticBoss(boss, 2);
                NurseCordsCut++; cut++;
            }
            if (boss.Special != BossMove.Absorb) continue;
            foreach (var p in puddles)
                if (p.OwnerId == boss.Id && !p.DrainCut && Math.Abs(p.X - boss.X) <= 650 && SegmentGap(x0, h0, x1, h1, p.X, 4, boss.X, boss.Height * 0.45) <= pad)
                { p.DrainCut = true; cut++; }
        }
        return cut;
    }
}
