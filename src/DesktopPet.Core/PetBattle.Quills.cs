using System;
using System.Collections.Generic;
using System.Linq;

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
    private readonly Random callRandom;
    // The call about to go is a formation: one place on the floor, its members side by side.
    private bool callFormation;
    private readonly HashSet<string[]> formationCalls = new();
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

    // A timer's last seconds (2026-11-04): its clock swells and rings an alarm (seen and heard).
    public const double ClockAlarm = 3;

    // Every step of a timer still in the battle: its clock (slower while dazed); run out, it calls.
    private void StepClock(Slime s, double dt)
    {
        if (s.Type.Clock is not ClockSpec c || !s.Targetable || s.Grabbed || s.ClockCalls >= c.Calls) return; // In the claw its clock waits (2026-11-04).
        if (s.ClockTotal <= 0)
        {
            s.ClockTotal = s.ClockLeft = s.Elite != null ? c.EliteSeconds : c.Seconds;
            events.Add(new(BattleEventKind.ClockStarted, s.X, s.Id));
        }
        double was = s.ClockLeft;
        s.ClockLeft -= dt * (s.State == SlimeState.Stunned ? c.Stunned : 1);
        if (was > ClockAlarm && s.ClockLeft <= ClockAlarm && s.ClockLeft > 0) events.Add(new(BattleEventKind.ClockAlarm, s.X, s.Id));
        if (s.ClockLeft > 0) return;
        s.ClockCalls++;
        ClockRings++;
        calls.Add(CallOf(c));
        AddEffect(BattleEffectKind.Bell, s.X, s.Height + 14);
        // 2026-11-04: a big ring of sound out of it, so the call cannot be missed.
        AddEffect(BattleEffectKind.Slam, s.X, 0, 220);
        events.Add(new(BattleEventKind.ClockRang, s.X, s.Id));
        s.ClockLeft = s.ClockCalls < c.Calls ? s.ClockTotal : 0;
    }

    // What a timer calls (2026-11-04): a formation its battle's threat reaches, else its plain call.
    private string[] CallOf(ClockSpec c)
    {
        if (c.CallFormations == null) return c.Call;
        var open = c.CallFormations.Where(w => w.Value > 0 && Settings.Formation(w.Key) is { } f && f.MinThreat <= Threat + 1e-9).OrderBy(w => w.Key, StringComparer.Ordinal).ToList();
        if (open.Count == 0) return c.Call;
        double roll = callRandom.NextDouble() * open.Sum(w => w.Value);
        foreach (var w in open)
        {
            roll -= w.Value;
            if (roll > 0) continue;
            var kinds = Settings.Formation(w.Key)!.Members.ToArray();
            formationCalls.Add(kinds);
            return kinds;
        }
        return c.Call;
    }

    // From StepWaves (after an announced spawn is out): the next call, once the floor has room for it, is
    // announced at the edge behind the cat. True: one went out this step.
    private bool SendCall(double catX)
    {
        if (calls.Count == 0 || Alive + (formationCalls.Contains(calls[0]) ? 1 : calls[0].Length) > maxAlive) return false;
        var kinds = calls[0];
        calls.RemoveAt(0);
        callFormation = formationCalls.Remove(kinds);
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
        formationCalls.Clear();
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
        if (!TakeOne(steal, out s.Loot, out s.LootApp, out s.LootCharge, out s.LootSkill)) return;
        // 2026-11-04: an elite takes a second thing (another icon's charge or another ready skill).
        if (s.Elite != null) TakeOne(steal, out s.Loot2, out s.LootApp2, out s.LootCharge2, out s.LootSkill2);
        Thefts++;
        s.FleeSide = s.X >= catX ? 1 : -1;
        s.LingerClock = 0;
        AddEffect(BattleEffectKind.Miss, catX, 60);
        events.Add(new(BattleEventKind.ThiefStole, s.X, s.Loot == StolenKind.Skill ? 1 : 0));
    }

    // One take: the fullest clean icon's charge, otherwise the ready skill with the longest cooldown.
    private bool TakeOne(StealSpec steal, out StolenKind kind, out int appId, out double charge, out BattleSkill skill)
    {
        kind = StolenKind.None; appId = 0; charge = 0; skill = default;
        if (StealableApp(steal) is DesktopApp app)
        {
            kind = StolenKind.App;
            appId = app.Id;
            charge = app.Charge;
            app.Charge = 0;
            return true;
        }
        if (StealableSkill() is BattleSkill ready)
        {
            kind = StolenKind.Skill;
            skill = ready;
            cooldowns[(int)ready] = SkillCooldown(ready);
            if (Armed == ready) Arm(null);
            return true;
        }
        return false;
    }

    // A flag slime (2026-11-04): it plants its flags, posts with the ring (they count as the wave's). From
    // 2026-11-05 it picks its moment and its ground: it walks behind its allies until the front of them is
    // near the cat and in its ring (plant.near), or plant.by s have gone, and plants clear of the other flags
    // (rings do not add up): the ground being taken it walks to the free ground covering the most allies no
    // ring covers. No ally left: at once. Nothing hurts it before its first flag, nothing hurts its flags
    // while it lives but the Scissors. Its next flag comes plant.every s later, where an ally has no ring
    // (plant.count in all, a lost one is not replaced); with the cat near it runs first and plants as soon
    // as it may, or wherever it is cornered, so as not to die with its flag unplanted.
    private void StepPlant(Slime s, double dt, double catX)
    {
        if (s.Type.Plant is not PlantSpec plant || s.Planted || !s.Targetable || s.Handled || s.Decoy) return;
        s.PlantClock += dt;
        bool first = s.FlagsPlanted == 0;
        double wait = first ? plant.After : plant.Every, by = first ? plant.By : plant.Every + plant.By;
        if (s.State != SlimeState.Moving) return;
        var type = Settings.Enemy(plant.Post);
        double ring = s.Elite != null && type.Elite != null && type.Link is { EliteRange: > 0 } link ? link.EliteRange : type.Link?.Range ?? 0;
        if (CatPresses(s, plant, catX))
        {
            var (minX, maxX) = Bounds(s);
            bool cornered = s.X <= minX + 1 || s.X >= maxX - 1;
            s.PlantAt = double.NaN;
            if (s.PlantClock >= (cornered ? plant.After : wait) && FlagRoom(s.X, ring, plant)) PlantFlag(s, plant, type);
            return;
        }
        if (s.PlantClock < wait) return;
        bool late = s.PlantClock >= by;
        if (!late && double.IsNaN(s.PlantAt) && !FrontInRing(s, plant, ring, catX)) return; // Once on its way it goes on.
        bool room = FlagRoom(s.X, ring, plant);
        if ((!room || (!first && !late && BareAllies(s, s.X, ring) == 0)) && s.PlantClock < by * 2)
        {
            s.PlantAt = PlantSpot(s, plant, ring, catX, first || late);
            if (!double.IsNaN(s.PlantAt) || !late) return;
        }
        // The first flag is planted at the last anywhere (it ends the slime's guard), a later one only on free ground.
        if (first || room) PlantFlag(s, plant, type);
        else { s.Planted = true; s.PlantAt = double.NaN; }
    }

    private void PlantFlag(Slime s, PlantSpec plant, EnemyType type)
    {
        s.FlagsPlanted++;
        s.Planted = s.FlagsPlanted >= plant.Count;
        s.PlantClock = 0;
        s.PlantAt = double.NaN;
        s.Shielded = s.FlagGuard = false;
        var post = AddSlime(s.X, type);
        post.SummonedBy = CalledBy;
        post.FlagOf = s.Id;
        post.Shielded = post.FlagGuard = true;
        if (s.Elite != null && type.Elite is EliteSpec elite) ApplyElite(post, elite);
        events.Add(new(BattleEventKind.FlagPlanted, s.X, post.Id));
    }

    // Its flag slime down: its flags can be knocked down (a fall panics the others as ever).
    private void FlagOwnerFell(Slime owner)
    {
        if (owner.Type.Plant == null) return;
        foreach (var f in slimes)
        {
            if (f.FlagOf != owner.Id) continue;
            FreeFlag(f);
            if (f.State != SlimeState.Defeated) AddEffect(BattleEffectKind.Spark, f.X, f.Height / 2);
        }
    }

    private static void FreeFlag(Slime flag) { flag.FlagOf = 0; flag.Shielded = flag.FlagGuard = false; }
    public static bool IsFlagPost(Slime s) => s.Type.Still && (s.FlagOf != 0 || s.Link?.Kind == EnemyLinks.Aura);

    // The cat close to a flag slime with a flag still to plant.
    private static bool CatPresses(Slime s, PlantSpec plant, double catX) =>
        s.FlagsPlanted > 0 && !s.Planted && Math.Abs(s.X - catX) < plant.Dodge;

    // Running from the cat comes before walking to free ground or keeping behind its allies.
    private bool DodgeCat(Slime s, double dt, double catX, double minX, double maxX)
    {
        if (s.Type.Plant is not PlantSpec plant || !CatPresses(s, plant, catX) || Math.Abs(s.KnockVelocity) > 1) return false; // Pulled or knocked: it goes where it is sent.
        int away = s.X >= catX ? 1 : -1;
        s.Facing = away;
        s.X = Math.Clamp(s.X + away * Speed(s) * plant.DodgeSpeed * dt, minX, maxX);
        return true;
    }

    // Who a flag is for: the enemies that fight (not a support, a decoy or another post).
    private static bool FlagCovers(Slime bearer, Slime s) =>
        s != bearer && s.State != SlimeState.Defeated && !s.Type.Support && !s.Type.Still && !s.Decoy;

    // Time to plant: no ally left to wait for, or the nearest one on its side of the cat is near it and in the ring.
    private bool FrontInRing(Slime bearer, PlantSpec plant, double ring, double catX)
    {
        int side = bearer.X >= catX ? 1 : -1;
        bool allies = false;
        double nearest = double.NaN;
        foreach (var s in slimes)
        {
            if (!FlagCovers(bearer, s)) continue;
            allies = true;
            double ahead = (s.X - catX) * side;
            if (ahead >= 0 && (double.IsNaN(nearest) || ahead < nearest)) nearest = ahead;
        }
        if (!allies) return true;
        return !double.IsNaN(nearest) && nearest <= plant.Near && Math.Abs(bearer.X - (catX + side * nearest)) <= ring;
    }

    // How far a flag with this ring stands from one that is planted (the wider ring of the two sets it).
    private static double FlagGap(Slime flag, double ring, PlantSpec plant) => plant.Spacing * Math.Max(AuraRange(flag), ring);
    private static bool IsFlag(Slime s) => s.State != SlimeState.Defeated && s.Link?.Kind == EnemyLinks.Aura;

    private bool FlagRoom(double x, double ring, PlantSpec plant)
    {
        foreach (var f in slimes)
            if (IsFlag(f) && Math.Abs(f.X - x) < FlagGap(f, ring, plant) - 1) return false;
        return true;
    }

    // The free ground beside the planted flags on its side of the cat, no nearer the cat than it keeps: where
    // its ring takes in the most allies no ring has yet, the nearest such. NaN: none on this field.
    private double PlantSpot(Slime bearer, PlantSpec plant, double ring, double catX, bool anywhere)
    {
        int side = bearer.X >= catX ? 1 : -1;
        var (minX, maxX) = Bounds(bearer);
        double best = double.NaN, bestWalk = 0;
        int bestAllies = -1;
        foreach (var f in slimes)
        {
            if (!IsFlag(f)) continue;
            foreach (int way in new[] { -1, 1 })
            {
                double x = f.X + way * FlagGap(f, ring, plant);
                if (x < minX || x > maxX || (x - catX) * side < bearer.Type.KeepBack || !FlagRoom(x, ring, plant)) continue;
                int allies = BareAllies(bearer, x, ring);
                double walk = Math.Abs(x - bearer.X);
                if (allies > bestAllies || (allies == bestAllies && walk < bestWalk)) (best, bestWalk, bestAllies) = (x, walk, allies);
            }
        }
        return bestAllies > 0 || anywhere ? best : double.NaN;
    }

    // The allies a flag at x would cover that no ring covers yet.
    private int BareAllies(Slime bearer, double x, double ring)
    {
        int allies = 0;
        foreach (var s in slimes)
            if (FlagCovers(bearer, s) && s.AuraCut <= 0 && Math.Abs(s.X - x) <= ring) allies++;
        return allies;
    }

    // On its way to free ground it does that rather than keep behind its allies.
    private bool WalkToPlant(Slime s, double dt, double minX, double maxX)
    {
        if (double.IsNaN(s.PlantAt)) return false;
        double gap = s.PlantAt - s.X;
        s.Facing = gap < 0 ? -1 : 1;
        s.X = Math.Clamp(s.X + Math.Sign(gap) * Math.Min(Math.Abs(gap), Speed(s) * dt), minX, maxX);
        return true;
    }

    // A thief defeated (from Defeat): what it carried goes back (an icon infected meanwhile gets nothing).
    private void GiveBack(Slime s)
    {
        if (s.Loot == StolenKind.None) return;
        void Return(StolenKind kind, BattleSkill skill, int appId, double charge)
        {
            if (kind == StolenKind.Skill) cooldowns[(int)skill] = 0;
            else if (kind == StolenKind.App) foreach (var app in desktop) if (app.Id == appId) Charge(app, charge);
        }
        Return(s.Loot, s.LootSkill, s.LootApp, s.LootCharge);
        Return(s.Loot2, s.LootSkill2, s.LootApp2, s.LootCharge2);
        s.Loot = s.Loot2 = StolenKind.None;
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
