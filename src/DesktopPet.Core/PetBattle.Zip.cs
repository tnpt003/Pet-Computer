using System;
using System.Collections.Generic;

namespace DesktopPet;

// 1.5 M17, B1 "Slime Nén" (the .zip slime, chapter 2's boss; docs/UPDATE-1.5.md):
// - Phase 1, the roll (EnemyType.Roll): from afar it winds up (a lane on the floor) and rolls over the
//   floor; the cat on the floor in its way is hurt and stunned, once per roll. Lifting the cat dodges it.
// - Phase 2, unpacking (BossPhase.Formation + Roped): a shield phase whose helpers are a formation's
//   members, each tied to the boss by a rope; the shield holds while any rope is left. The cat going
//   through a rope fast enough (held past the drag threshold, or thrown) or a Scissors stroke cuts it
//   (the helper is dazed); defeating the helper ends its rope too.
// - Phase 3, packing (BossPhase.Pack): the boss squeezes itself small where it stands, behind a shield,
//   with its file shards (Still enemies) around it; the shield drops once they are all broken.
// Physics of the cat stay the window's: only its pose is read (PetBattle.Held.cs).
public sealed partial class BattleSim
{
    // How small a packed boss is drawn (its shards stand just outside that).
    public const double PackedScale = 0.72;
    public int BossRopesCut { get; private set; }
    public int Rolls { get; private set; }
    public int RollHits { get; private set; }

    // The phase a shield tag stands for (null: none).
    private static BossPhase? PhaseOf(Slime boss, int tag) =>
        tag > 0 && boss.Type.Phases is { } phases && tag <= phases.Length ? phases[tag - 1] : null;
    // The shield phase a boss is in now (null: its shield is down).
    public BossPhase? ShieldPhase(Slime boss) => boss.Shielded ? PhaseOf(boss, boss.ShieldTag) : null;

    // The time to the next roll runs only while the shield is down.
    private static void TickRoll(Slime boss, double dt)
    {
        if (boss.Type.Roll == null || boss.Shielded) return;
        boss.RollCooldown = Math.Max(0, boss.RollCooldown - dt * boss.ChillFactor);
    }

    // A roll's windup instead of walking up: the cat on the floor between MinRange and Range away.
    private bool StartRoll(Slime boss, RollSpec roll, double dx, bool catHeld)
    {
        if (boss.Shielded || boss.RollCooldown > 0 || boss.Cooldown > 0 || catHeld || CatLifted || Grace > 0) return false;
        if (Math.Abs(dx) < roll.MinRange || Math.Abs(dx) > roll.Range) return false;
        boss.Facing = boss.DashDirection = dx < 0 ? -1 : 1;
        boss.Set(SlimeState.Windup);
        boss.Rolling = true;
        lastWindup = Time;
        events.Add(new(BattleEventKind.RollAimed, boss.X, boss.Id));
        return true;
    }

    private void BeginRoll(Slime boss)
    {
        boss.DashLeft = boss.Type.Roll!.Distance;
        boss.RollHit = false;
        boss.Set(SlimeState.Dashing); // Rolling stays on (SlimeState.Dashing is the roll).
        Rolls++;
        events.Add(new(BattleEventKind.Rolled, boss.X, boss.Id));
    }

    // The roll itself: its locked lane to the end (or the field's edge), the cat checked along the whole
    // way of this frame, as a dash is.
    private void StepRoll(Slime boss, double dt, double catX, double minX, double maxX)
    {
        var roll = boss.Type.Roll!;
        double before = boss.X;
        double step = boss.Stuck ? 0 : Math.Min(boss.DashLeft, roll.Speed * level.EnemySpeed * boss.ChillFactor * dt);
        boss.X = Math.Clamp(boss.X + boss.DashDirection * step, minX, maxX);
        boss.DashLeft = boss.X == before ? 0 : boss.DashLeft - step;
        double reach = boss.HitWidth / 2 + CatHalfWidth;
        if (!boss.RollHit && !CatLifted && Grace <= 0 && catX >= Math.Min(before, boss.X) - reach && catX <= Math.Max(before, boss.X) + reach)
        {
            boss.RollHit = true;
            RollHits++;
            events.Add(new(BattleEventKind.RollHit, catX, boss.Id));
            AddEffect(BattleEffectKind.Thud, catX, 0);
            HurtCat(boss, catX, true, boss.Type.Damage > 0 ? roll.Damage / boss.Type.Damage : 0, roll.Stun, crush: true);
        }
        if (boss.DashLeft <= 0 && !Over)
        {
            boss.Set(SlimeState.Recover);
            boss.Cooldown = AttackCooldown(boss);
            boss.RollCooldown = boss.Enraged ? roll.EnragedEvery : roll.Every;
            AddEffect(BattleEffectKind.Poof, boss.X, 4, 2);
        }
    }

    // How far a rolling boss has turned (degrees, drawing): its way so far over its half height.
    public static double RollTurn(Slime boss) =>
        boss.Rolling && boss.State == SlimeState.Dashing && boss.Type.Roll is RollSpec roll
            ? (roll.Distance - boss.DashLeft) / Math.Max(1, boss.Height / 2) * 180 / Math.PI * boss.DashDirection : 0;

    // Phase 3: it packs itself where it stands (no run to the edge), its shards around it, announced by
    // the rings as other helpers are.
    private void Pack(Slime boss)
    {
        boss.Retreating = false;
        boss.Packed = true;
        var spots = PackSpots(boss, boss.PendingCalls ?? new List<string>());
        boss.PendingCalls = null;
        events.Add(new(BattleEventKind.BossPacked, boss.X, boss.Id));
        if (boss.Type.SummonWarning > 0 && spots.Count > 0)
        {
            boss.CallSpots = spots;
            boss.SummonWarn = boss.Type.SummonWarning;
            events.Add(new(BattleEventKind.BossSummoning, boss.X));
            return;
        }
        if (SpawnHelpers(boss, spots, boss.ShieldTag) > 0) events.Add(new(BattleEventKind.BossSummoned, boss.X));
    }

    // The shards' spots: both sides in turn, nearest first, just outside the packed boss; a side the field
    // ends on gives its spots to the other.
    private List<(string Kind, double X)> PackSpots(Slime boss, List<string> calls)
    {
        var spots = new List<(string Kind, double X)>();
        double near = boss.Width * PackedScale / 2 + 24;
        for (int ring = 0; spots.Count < calls.Count && ring < 12; ring++)
            for (int side = -1; side <= 1 && spots.Count < calls.Count; side += 2)
            {
                double w = Settings.SlimeWidth * Settings.Enemy(calls[spots.Count]).Scale;
                double x = boss.X + side * (near + w / 2 + ring * (w + 12));
                if (x - w / 2 < Left || x + w / 2 > Right) continue;
                spots.Add((calls[spots.Count], x));
            }
        return spots;
    }

    // The boss a helper's rope goes to (null: no rope, or the boss is gone).
    public Slime? RopeBoss(Slime helper) =>
        helper.RopedTo != 0 && helper.Targetable && Find(helper.RopedTo) is { Targetable: true } boss ? boss : null;

    // A rope cut: the helper is dazed, the boss's shield holds while any other rope is left.
    private bool CutBossRope(Slime helper)
    {
        if (RopeBoss(helper) is not Slime boss) return false;
        helper.RopedTo = 0;
        Daze(helper, boss.Type.RopeCutStun);
        AddEffect(BattleEffectKind.Spark, (helper.X + boss.X) / 2, Math.Min(helper.Height, boss.Height) * 0.6);
        BossRopesCut++;
        events.Add(new(BattleEventKind.BossRopeCut, helper.X, helper.Id));
        return true;
    }

    // The cat's way since the last step against every rope to a boss (as for a tether's rope).
    private void CatThroughBossRopes(CatPose from, CatPose cat, double speed)
    {
        // Every frame the cat is held: nothing to copy in a battle without ropes.
        bool any = false;
        foreach (var s in slimes) if (s.RopedTo != 0) { any = true; break; }
        if (!any) return;
        foreach (var helper in slimes.ToArray())
        {
            if (RopeBoss(helper) is not Slime boss || speed < boss.Type.RopeCutSpeed) continue;
            if (!RopeCrossed(helper, boss, from.X, from.Height, cat.X, cat.Height, CatHalfWidth * 2 / 3)) continue;
            if (CutBossRope(helper)) CatRopeCuts++;
            if (Over) return;
        }
    }

    // A Scissors stroke: every rope to a boss it crosses; how many it cut.
    private int SnipBossRopes(double x0, double h0, double x1, double h1)
    {
        int cut = 0;
        foreach (var helper in slimes.ToArray())
            if (RopeBoss(helper) is Slime boss && RopeCrossed(helper, boss, x0, h0, x1, h1) && CutBossRope(helper)) cut++;
        return cut;
    }

    // A roped phase: its helpers still tied to the boss (one still appearing too).
    public int RopesLeft(Slime boss)
    {
        int n = 0;
        foreach (var s in slimes) if (s.RopedTo == boss.Id && s.State != SlimeState.Defeated) n++;
        return n;
    }
}
