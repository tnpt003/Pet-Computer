using System;
using System.Collections.Generic;

namespace DesktopPet;

// 1.5 M16, docs/UPDATE-1.5.md (S9, S10, S11):
// - S9 the hedgehog (EnemyType.Quills): its quills lie flat, rise (a warning) and stand up in turn. Up,
//   the cat's weapon only half hurts it and pricks the cat (the cat goes for others first). Bombs, skills,
//   apps and poison go through; wet from the spray, its quills lie flat until it is dry.
// - S10 the timer (EnemyType.Clock): its clock runs from when it comes in (slower while it is dazed); run
//   out, it calls more enemies (announced at the edge behind the cat, the wave waits for them), a few
//   times at most. Defeating it first stops it.
// - S11 the thief (EnemyType.Steal): it never hurts the cat; its windup next to the cat steals an app's
//   charge or a ready skill, then it runs to the edge away from the cat and, after a while there, gets
//   away with it (gone, not a defeat of the cat's). Defeated first, it gives back what it took.
// The cat's physics are only read. No random stream is used.
public sealed partial class BattleSim
{
    public int QuillPricks { get; private set; }
    public int ClockRings { get; private set; }
    public int Thefts { get; private set; }
    public int TheftsReturned { get; private set; }
    public int ThievesEscaped { get; private set; }
    // A timer's call: the enemies it sends (SummonedBy: CalledBy, so they weigh on no wave budget).
    public const int CalledBy = -1;
    // Calls still to come (each waits for room on the floor), and whether the announced spawn is one.
    private readonly List<string[]> calls = new();
    private bool incomingCalled;
    private int CallsWaiting { get { int n = 0; foreach (var c in calls) n += c.Length; return n; } }

    // The cat's own blows a hedgehog's quills turn on it (not its echo: an afterimage pricks nothing).
    private static bool Pricking(DamageSource source) => source is DamageSource.Weapon or DamageSource.Combo or DamageSource.Splash;

    // A hit on a hedgehog (from Damage): what is left of it with its quills up, and the cat is pricked.
    private double QuillHit(Slime s, double amount, DamageSource source)
    {
        if (!s.Puffed || s.Type.Quills is not QuillsSpec q || !Pricking(source)) return amount;
        QuillPricks++;
        events.Add(new(BattleEventKind.QuillsPricked, s.X, s.Id));
        if (q.Prick > 0 && !CatLifted && !Over) LandHit(q.Prick, lastCatX, out _);
        return amount * q.Armor;
    }

    // How far its quills have risen (0–1) while they rise.
    public static double QuillRise(Slime s) => s.Quill != QuillPhase.Rising || s.Type.Quills is not QuillsSpec q || q.Warn <= 0 ? 0 : Math.Clamp(1 - s.QuillClock / q.Warn, 0, 1);

    public static double PuffedTime(Slime s) => s.Type.Quills is not QuillsSpec q ? 0 : s.Elite != null ? q.ElitePuffed : q.Puffed;

    // Every step of a hedgehog still in the battle: its quills' round (flat while wet).
    private void StepQuills(Slime s, double dt)
    {
        if (s.Type.Quills is not QuillsSpec q || !s.Targetable) return;
        if (s.Wet)
        {
            if (s.Quill != QuillPhase.Flat)
            {
                s.Quill = QuillPhase.Flat;
                events.Add(new(BattleEventKind.QuillsSoaked, s.X, s.Id));
            }
            s.QuillClock = q.Flat;
            return;
        }
        s.QuillClock -= dt;
        if (s.QuillClock > 0) return;
        switch (s.Quill)
        {
            case QuillPhase.Flat when q.Warn > 0:
                s.Quill = QuillPhase.Rising;
                s.QuillClock = q.Warn;
                events.Add(new(BattleEventKind.QuillsRising, s.X, s.Id));
                break;
            case QuillPhase.Flat:
            case QuillPhase.Rising:
                s.Quill = QuillPhase.Up;
                s.QuillClock = PuffedTime(s);
                events.Add(new(BattleEventKind.QuillsUp, s.X, s.Id));
                break;
            default:
                s.Quill = QuillPhase.Flat;
                s.QuillClock = q.Flat;
                events.Add(new(BattleEventKind.QuillsDown, s.X, s.Id));
                break;
        }
    }

    // Every step of a timer still in the battle: its clock (slower while dazed); run out, it calls.
    private void StepClock(Slime s, double dt)
    {
        if (s.Type.Clock is not ClockSpec c || !s.Targetable || s.ClockCalls >= c.Calls) return;
        if (s.ClockTotal <= 0)
        {
            s.ClockTotal = s.ClockLeft = s.Elite != null ? c.EliteSeconds : c.Seconds;
            events.Add(new(BattleEventKind.ClockStarted, s.X, s.Id));
        }
        s.ClockLeft -= dt * (s.State == SlimeState.Stunned ? c.Stunned : 1);
        if (s.ClockLeft > 0) return;
        s.ClockCalls++;
        ClockRings++;
        calls.Add(c.Call);
        AddEffect(BattleEffectKind.Bell, s.X, s.Height + 14);
        events.Add(new(BattleEventKind.ClockRang, s.X, s.Id));
        s.ClockLeft = s.ClockCalls < c.Calls ? s.ClockTotal : 0;
    }

    // From StepWaves (after an announced spawn is out): the next call, once the floor has room for it, is
    // announced at the edge behind the cat. True: one went out this step.
    private bool SendCall(double catX)
    {
        if (calls.Count == 0 || Alive + calls[0].Length > maxAlive) return false;
        var kinds = calls[0];
        calls.RemoveAt(0);
        events.Add(new(BattleEventKind.ClockCalled, catX));
        int before = slimes.Count;
        Announce(catX, kinds, -Far(catX));
        if (incoming == kinds) incomingCalled = true; // It comes after its arrow (StepWaves tags it then).
        else TagCalled(before);
        return true;
    }

    // The slimes added since `from`: a timer's call.
    private void TagCalled(int from)
    {
        for (int i = from; i < slimes.Count; i++) slimes[i].SummonedBy = CalledBy;
    }

    // A new wave, or the battle over: calls still waiting are dropped.
    private void ClearCalls()
    {
        calls.Clear();
        incomingCalled = false;
    }

    // From the Moving step of a thief: running off with its loot, or following the cat with nothing to
    // steal. True: its step is done here; false: it goes for the cat as any slime (its windup steals).
    private bool Sneak(Slime s, StealSpec steal, double dt, double catX, double minX, double maxX)
    {
        if (s.Loot == StolenKind.None)
        {
            if (Stealable(steal) != StolenKind.None) return false;
            // Nothing to take: it trails the cat, never winding up.
            double dx = catX - s.X;
            if (Math.Abs(dx) > 1) s.Facing = dx < 0 ? -1 : 1;
            if (Math.Abs(dx) > s.Range * 0.75) s.X = Math.Clamp(s.X + Math.Sign(dx) * Math.Min(Math.Abs(dx), Speed(s) * dt), minX, maxX);
            return true;
        }
        double edge = s.FleeSide < 0 ? minX : maxX, gap = edge - s.X;
        s.Facing = s.FleeSide;
        if (Math.Abs(gap) > 1)
        {
            s.X = Math.Clamp(s.X + Math.Sign(gap) * Math.Min(Math.Abs(gap), Speed(s) * (s.Elite != null ? steal.EliteFlee : steal.Flee) * dt), minX, maxX);
            return true;
        }
        s.LingerClock += dt;
        if (s.LingerClock >= steal.Linger) Escape(s);
        return true;
    }

    // What a thief would take now: an app's charge (battles with apps), otherwise a ready skill.
    private StolenKind Stealable(StealSpec steal) =>
        StealableApp(steal) != null ? StolenKind.App : StealableSkill() != null ? StolenKind.Skill : StolenKind.None;

    private DesktopApp? StealableApp(StealSpec steal)
    {
        if (AppRules == null) return null;
        DesktopApp? best = null;
        foreach (var app in desktop)
            if (!app.Infected && app.Charge >= steal.MinCharge && (best == null || app.Charge > best.Charge)) best = app;
        return best;
    }

    // The ready skill with the longest cooldown (one that works and is carried).
    private BattleSkill? StealableSkill()
    {
        BattleSkill? best = null;
        foreach (var skill in carried)
            if (BattleSkills.Get(skill).Working && cooldowns[(int)skill] <= 0 && (best is not BattleSkill b || SkillCooldown(skill) > SkillCooldown(b))) best = skill;
        return best;
    }

    // A thief's windup landed next to the cat (from Strike): it takes what it can and runs off.
    private void Steal(Slime s, double catX)
    {
        if (s.Type.Steal is not StealSpec steal || s.Loot != StolenKind.None) return;
        if (StealableApp(steal) is DesktopApp app)
        {
            s.Loot = StolenKind.App;
            s.LootApp = app.Id;
            s.LootCharge = app.Charge;
            app.Charge = 0;
        }
        else if (StealableSkill() is BattleSkill skill)
        {
            s.Loot = StolenKind.Skill;
            s.LootSkill = skill;
            cooldowns[(int)skill] = SkillCooldown(skill);
            if (Armed == skill) Arm(null);
        }
        else return;
        Thefts++;
        s.FleeSide = s.X >= catX ? 1 : -1;
        s.LingerClock = 0;
        AddEffect(BattleEffectKind.Miss, catX, 60);
        events.Add(new(BattleEventKind.ThiefStole, s.X, s.Loot == StolenKind.Skill ? 1 : 0));
    }

    // A thief defeated (from Defeat): what it carried goes back (an icon infected meanwhile gets nothing).
    private void GiveBack(Slime s)
    {
        if (s.Loot == StolenKind.None) return;
        if (s.Loot == StolenKind.Skill) cooldowns[(int)s.LootSkill] = 0;
        else foreach (var app in desktop) if (app.Id == s.LootApp) Charge(app, s.LootCharge);
        s.Loot = StolenKind.None;
        TheftsReturned++;
        AddEffect(BattleEffectKind.Heart, s.X, s.Height);
        events.Add(new(BattleEventKind.ThiefReturned, s.X, s.Id));
    }

    // Away with its loot: gone from the battle, not a defeat of the cat's (what it took is lost).
    private void Escape(Slime s)
    {
        s.Set(SlimeState.Defeated);
        s.Escaped = true;
        s.KnockVelocity = 0;
        ClearStatus(s);
        if (SelectedId == s.Id) SelectedId = null;
        LinkFallen(s, false);
        ThievesEscaped++;
        AddEffect(BattleEffectKind.Poof, s.X, s.Height / 2, 1);
        effects[^1].Tint = s.Type.Color;
        events.Add(new(BattleEventKind.ThiefEscaped, s.X, s.Id));
    }
}
