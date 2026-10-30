using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// BattleSim: the boss — shield phases, leaping to an edge, calling and rallying helpers, elites.
public sealed partial class BattleSim
{
    // The shield goes up at once; the boss drops what it was doing and runs to the edge far from the cat.
    private void StartShield(Slime boss, BossPhase phase, int tag)
    {
        boss.Shielded = boss.Retreating = true;
        boss.ShieldTag = tag;
        boss.RetreatTime = 0;
        boss.RetreatSide = RetreatEdge(boss.X, lastCatX);
        boss.Crouch = boss.LeapHeight = 0;
        boss.LeapFromX = boss.X;
        boss.PendingCalls = Settings.PhaseCalls(phase).ToList();
        boss.RallyCooldown = boss.Type.RallyEvery / 2;
        boss.RallyCharge = 0;
        if (boss.State is SlimeState.Windup or SlimeState.Dashing or SlimeState.Casting) boss.Set(SlimeState.Moving);
        events.Add(new(BattleEventKind.BossShielded, boss.X));
        // 1.5 M17: a pack phase stays where it is, its shards around it (PetBattle.Zip.cs).
        if (phase.Pack) Pack(boss);
    }

    // The edge a shield phase goes to (+1 right, -1 left): the one opposite the boss's half of the
    // floor, unless the cat is in that half too; then the other one (never next to the cat).
    private int RetreatEdge(double bossX, double catX)
    {
        double middle = (Left + Right) / 2;
        int edge = bossX >= middle ? -1 : 1;
        int catHalf = catX >= middle ? 1 : -1;
        return edge == catHalf ? -edge : edge;
    }

    // Calls helpers next to the boss, within its SummonCap of live helpers; returns how many came.
    // side 0: both sides in turn (as before); otherwise all on the open side, away from that edge.
    private int CallHelpers(Slime boss, List<string> calls, int tag, int side) => SpawnHelpers(boss, HelperSpots(boss, calls, side), tag);

    // Where the helpers will stand (as many as the SummonCap leaves room for). On the open side they
    // line up by role from the boss outwards: supports next to it, blockers in front of them, plain
    // fighters, then dashers nearest the cat.
    private List<(string Kind, double X)> HelperSpots(Slime boss, List<string> calls, int side)
    {
        int alive = 0;
        foreach (var s in slimes) if (s.SummonedBy == boss.Id && s.State != SlimeState.Defeated) alive++;
        int count = Math.Min(calls.Count, Math.Max(0, boss.Type.SummonCap - alive));
        var spots = new List<(string Kind, double X)>();
        if (side == 0)
        {
            for (int k = 0; k < count; k++) spots.Add((calls[k], boss.X + (k % 2 == 0 ? -1 : 1) * (boss.Width / 2 + 30 + 20 * (k / 2))));
            return spots;
        }
        static int Rank(EnemyType t) => t.Support ? 0 : t.Blocker ? 1 : t.Dash ? 3 : 2;
        var ordered = calls.Take(count).Select((kind, i) => (Kind: kind, Order: i)).OrderBy(c => Rank(Settings.Enemy(c.Kind))).ThenBy(c => c.Order).Select(c => c.Kind).ToList();
        double offset = boss.Width / 2 + 10, previous = 0;
        foreach (var kind in ordered)
        {
            double w = Settings.SlimeWidth * Settings.Enemy(kind).Scale;
            offset += previous == 0 ? w / 2 : (previous + w) / 2 * 0.85;
            previous = w;
            spots.Add((kind, boss.X - side * offset));
        }
        return spots;
    }

    // The helpers appear at their spots (still within the SummonCap), never on top of the cat: at
    // least SquadCatGap away, on the boss's side of it. 1.5 M17: a packed boss's shards come all of them,
    // right at their spots (they never move or attack); a roped phase's helpers come on their ropes; a
    // formation's name shows as its banner.
    private int SpawnHelpers(Slime boss, List<(string Kind, double X)> spots, int tag)
    {
        int alive = 0;
        foreach (var s in slimes) if (s.SummonedBy == boss.Id && s.State != SlimeState.Defeated) alive++;
        int count = boss.Packed ? spots.Count : Math.Min(spots.Count, Math.Max(0, boss.Type.SummonCap - alive));
        var phase = PhaseOf(boss, tag);
        for (int k = 0; k < count; k++)
        {
            double x = spots[k].X;
            if (!boss.Packed && Math.Abs(x - lastCatX) < SquadCatGap) x = lastCatX + (boss.X >= lastCatX ? 1 : -1) * SquadCatGap;
            var helper = AddSlime(x, Settings.Enemy(spots[k].Kind));
            helper.SummonedBy = boss.Id;
            helper.PhaseTag = tag;
            helper.Facing = lastCatX < helper.X ? -1 : 1;
            if (phase is { Roped: true }) helper.RopedTo = boss.Id;
        }
        if (count > 0 && phase?.Formation is string id)
        {
            FormationsMet++;
            NoteFormation(id);
            events.Add(new(BattleEventKind.FormationArrived, boss.X, Settings.FormationList.ToList().FindIndex(f => f.Id == id)));
        }
        if (count > 0 && phase is { Roped: true }) events.Add(new(BattleEventKind.BossRoped, boss.X, boss.Id));
        return count;
    }

    // The leap (RetreatLeap): a crouch where it stands (a stun or push makes it wait), then a flight to
    // the goal nothing can stop; true once it has landed.
    private bool StepLeap(Slime boss, double dt, double goal)
    {
        var type = boss.Type;
        if (boss.RetreatTime < type.LeapCrouch)
        {
            if (boss.State != SlimeState.Moving) return false;
            boss.RetreatTime += dt;
            boss.Crouch = Math.Clamp(boss.RetreatTime / type.LeapCrouch, 0, 1);
            boss.LeapFromX = boss.X;
            boss.Facing = goal < boss.X ? -1 : 1;
            if (boss.RetreatTime >= type.LeapCrouch) events.Add(new(BattleEventKind.BossLeaping, boss.X));
            return false;
        }
        boss.RetreatTime += dt;
        boss.Crouch = 0;
        boss.KnockVelocity = 0;
        double k = Math.Clamp((boss.RetreatTime - type.LeapCrouch) / type.LeapSeconds, 0, 1);
        boss.X = boss.LeapFromX + (goal - boss.LeapFromX) * k;
        boss.LeapHeight = Math.Sin(Math.PI * k) * LeapPeak(Math.Abs(goal - boss.LeapFromX));
        if (k < 1) return false;
        boss.LeapHeight = 0;
        AddEffect(BattleEffectKind.Thud, boss.X, 0);
        AddEffect(BattleEffectKind.Poof, boss.X, 4, 2);
        events.Add(new(BattleEventKind.BossLanded, boss.X));
        return true;
    }

    // How high a leap flies: higher for a longer one, never out of the battle strip.
    public static double LeapPeak(double distance) => Math.Clamp(25 + distance * 0.08, 25, 80);

    // The shield phase on the move: the run to the far edge (at most RetreatSeconds), then the call;
    // the shield drops once every helper of this phase is gone (at once if none could come).
    private void StepShield(Slime boss, double dt, double minX, double maxX)
    {
        BossShieldSeconds += dt;
        if (boss.Retreating)
        {
            double goal = boss.RetreatSide > 0 ? maxX : minX;
            if (boss.Type.RetreatLeap) { if (!StepLeap(boss, dt, goal)) return; }
            else
            {
                boss.RetreatTime += dt;
                if (boss.State == SlimeState.Moving)
                {
                    double step = boss.Type.RetreatSpeed * level.EnemySpeed * dt;
                    boss.X = Math.Clamp(boss.X + Math.Sign(goal - boss.X) * Math.Min(Math.Abs(goal - boss.X), step), minX, maxX);
                    boss.Facing = goal < boss.X ? -1 : 1;
                }
                if (Math.Abs(goal - boss.X) > 2 && boss.RetreatTime < boss.Type.RetreatSeconds) return;
            }
            boss.Retreating = false;
            boss.Facing = -boss.RetreatSide;
            var spots = HelperSpots(boss, boss.PendingCalls ?? new List<string>(), boss.RetreatSide);
            boss.PendingCalls = null;
            // Announced first: rings on the floor where they will appear (the shield holds meanwhile).
            if (boss.Type.SummonWarning > 0 && spots.Count > 0)
            {
                boss.CallSpots = spots;
                boss.SummonWarn = boss.Type.SummonWarning;
                events.Add(new(BattleEventKind.BossSummoning, boss.X));
                return;
            }
            if (SpawnHelpers(boss, spots, boss.ShieldTag) > 0) { events.Add(new(BattleEventKind.BossSummoned, boss.X)); return; }
        }
        if (boss.CallSpots != null)
        {
            boss.SummonWarn = Math.Max(0, boss.SummonWarn - dt);
            if (boss.SummonWarn > 0) return;
            var called = boss.CallSpots;
            boss.CallSpots = null;
            if (SpawnHelpers(boss, called, boss.ShieldTag) > 0) { events.Add(new(BattleEventKind.BossSummoned, boss.X)); return; }
        }
        // While this phase's helpers stand, the boss may rally one of them that is not elite yet, drawn at
        // random (2026-10-24: supports too; it used to be the fighter nearest the cat).
        // A roped phase holds only while a rope is left (1.5 M17): its helpers off their ropes fight on.
        bool helpers = false, roped = PhaseOf(boss, boss.ShieldTag) is { Roped: true };
        rallyable.Clear();
        foreach (var s in slimes)
        {
            if (s.SummonedBy != boss.Id || s.PhaseTag != boss.ShieldTag || s.State == SlimeState.Defeated) continue;
            if (roped && s.RopedTo != boss.Id) continue; // Still appearing counts (RopeBoss waits for it).
            helpers = true;
            if (s.Elite == null && s.Targetable) rallyable.Add(s);
        }
        if (helpers) { StepRally(boss, dt); return; }
        if (roped) events.Add(new(BattleEventKind.BossRopesGone, boss.X, boss.Id));
        boss.Shielded = boss.Packed = false;
        boss.ShieldTag = 0;
        boss.RallyCharge = 0;
        events.Add(new(BattleEventKind.BossShieldDown, boss.X));
        // An opening: dazed for a moment, taking damage as usual.
        if (boss.Type.ShieldBreakStun > 0)
        {
            boss.KnockVelocity = 0;
            boss.StunRemaining = boss.Type.ShieldBreakStun;
            boss.Set(SlimeState.Stunned);
        }
    }

    // The rally call: every RallyEvery s the crown glows RallyCharge s (a push now cuts it short),
    // then the chosen helper becomes elite for good (MakeElite).
    private readonly List<Slime> rallyable = new();
    private Slime? DrawRallied() => rallyable.Count == 0 ? null : rallyable[rallyRandom.Next(rallyable.Count)];
    private void StepRally(Slime boss, double dt)
    {
        Slime? helper = null;
        var type = boss.Type;
        if (type.RallyEvery <= 0) return;
        if (boss.RallyCharge > 0)
        {
            if (boss.State == SlimeState.Stunned) { boss.RallyCharge = 0; boss.RallyTargetId = 0; boss.RallyCooldown = type.RallyEvery; return; }
            boss.RallyCharge = Math.Max(0, boss.RallyCharge - dt);
            if (boss.RallyCharge > 0) return;
            boss.RallyCooldown = type.RallyEvery;
            // The helper the glow pointed at, or another one drawn now if that one is gone.
            helper = Find(boss.RallyTargetId) is { Targetable: true, Elite: null } chosen ? chosen : DrawRallied();
            boss.RallyTargetId = 0;
            if (helper == null) return;
            MakeElite(helper, type);
            events.Add(new(BattleEventKind.BossRallied, helper.X, helper.Id));
            CrownLink(boss, helper); // 1.5 M18: a crowned boss ties a link to it too (PetBattle.Mutant.cs).
            return;
        }
        boss.RallyCooldown -= dt;
        if (boss.RallyCooldown > 0 || rallyable.Count == 0 || boss.State == SlimeState.Stunned) return;
        helper = DrawRallied()!;
        boss.RallyCharge = type.RallyCharge;
        boss.RallyTargetId = helper.Id;
        events.Add(new(BattleEventKind.BossRallying, boss.X));
    }

    // The helper a glowing crown is about to rally (null when it is not rallying).
    public Slime? RallyTarget(Slime boss) => boss.RallyCharge > 0 ? Find(boss.RallyTargetId) : null;

    // Rallied: its kind's own elite affix if it has one (a plain one otherwise), RallyHp more max HP
    // (that much HP at once) and its hits × RallyDamage. Never counted against MaxElites.
    private static readonly EliteSpec PlainElite = new();
    private static void MakeElite(Slime helper, EnemyType boss)
    {
        helper.Rallied = true;
        ApplyElite(helper, helper.Type.Elite ?? PlainElite);
        double more = Math.Round(helper.MaxHp * boss.RallyHp);
        helper.MaxHp += more;
        helper.Hp += more;
        helper.Damage = Math.Round(helper.Damage * boss.RallyDamage);
    }

    // Turning elite, once per enemy (a roll at spawn or a rally): the affix's trait and its stats on
    // top of what difficulty and threat gave it. More max HP comes with that much HP at once.
    private static void ApplyElite(Slime slime, EliteSpec elite)
    {
        if (slime.Elite != null) return;
        slime.Elite = elite;
        slime.SureBlocks = elite.SureBlocks;
        slime.VirusClock = Math.Min(3, elite.VirusEvery); // Its first virus soon after.
        slime.HealBoost = elite.FirstHeal;
        double more = Math.Round(slime.MaxHp * (elite.Hp - 1));
        slime.MaxHp += more;
        slime.Hp += more;
        if (elite.Damage != 1) slime.Damage = Math.Round(slime.Damage * elite.Damage);
        if (elite.HealBudget != 1) slime.HealLeft = Math.Round(slime.HealLeft * elite.HealBudget);
        slime.HealCooldown *= elite.HealCooldown;
    }
}
