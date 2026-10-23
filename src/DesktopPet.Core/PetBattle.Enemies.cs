using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// BattleSim: every enemy's turn — targets, attacks, dashes, guards, heals, keeping back, running away.
public sealed partial class BattleSim
{
    // The wait after an attack and after a heal (an elite's are shorter).
    private static double AttackCooldown(Slime slime) => slime.Type.AttackCooldown * (slime.Elite?.AttackCooldown ?? 1);
    private static double HealCooldown(Slime slime) => slime.Type.HealCooldown * (slime.Elite?.HealCooldown ?? 1);

    private Slime? PickTarget(double catX)
    {
        if (SelectedId is int selected)
        {
            var chosen = Find(selected);
            // A shielded boss stays chosen; the cat goes for the others until its shield drops.
            if (chosen != null && chosen.Targetable && !chosen.Shielded && !chosen.Mended) return chosen;
            if (chosen == null || !chosen.Targetable) SelectedId = null;
        }
        // Never a shielded boss: the cat cannot hurt it (with nothing else to hit it waits).
        // One guarded by a heal is left alone too while there is another to hit.
        Slime? nearest = null, guarded = null;
        foreach (var s in slimes)
        {
            if (!s.Targetable || s.Shielded) continue;
            if (s.Mended) { if (guarded == null || Math.Abs(s.X - catX) < Math.Abs(guarded.X - catX)) guarded = s; continue; }
            if (nearest == null || Math.Abs(s.X - catX) < Math.Abs(nearest.X - catX)) nearest = s;
        }
        nearest ??= guarded;
        // Keep the current choice unless another slime is clearly closer (no dithering).
        var current = Find(autoTargetId);
        if (current != null && current.Targetable && !current.Shielded && (!current.Mended || nearest == current) && nearest != null &&
            Math.Abs(current.X - catX) <= Math.Abs(nearest.X - catX) + 40) nearest = current;
        autoTargetId = nearest?.Id ?? 0;
        return nearest;
    }

    // The floor a slime may stand on: the strip, up to a forced update on either side (a boss leaps over).
    private (double Min, double Max) Bounds(Slime slime)
    {
        double min = Left + slime.Width / 2, max = Right - slime.Width / 2;
        if (!slime.IsBoss)
            foreach (var h in hazards)
            {
                if (h.Kind != HazardKind.Update) continue;
                if (slime.X < h.X) max = Math.Min(max, h.X - WallHalfWidth - slime.Width / 2);
                else min = Math.Max(min, h.X + WallHalfWidth + slime.Width / 2);
            }
        return (min, Math.Max(min, max));
    }

    private void StepSlimes(double dt, double catX, bool catHeld)
    {
        var s = Settings;
        // Who guards whom is decided a few times a second, from where the cat was then (a reaction
        // delay, never where it will be); the slimes still move every frame.
        decisionClock -= dt;
        if (decisionClock <= 0 && !Over)
        {
            decisionClock = DecisionInterval;
            decisionCatX = catX;
            AssignGuards();
        }
        RefreshGuards();
        // By index over the slimes there were at the start: a hit on the cat can send damage back
        // (Thorns) and make a boss call helpers, which join the list and move from the next frame.
        int count = slimes.Count;
        for (int index = 0; index < count; index++)
        {
            var slime = slimes[index];
            var (minX, maxX) = Bounds(slime);
            slime.StateTime += dt;
            slime.HitFlash = Math.Max(0, slime.HitFlash - dt);
            slime.Cooldown = Math.Max(0, slime.Cooldown - dt);
            slime.HealCooldown = Math.Max(0, slime.HealCooldown - dt);
            slime.FarHealCooldown = Math.Max(0, slime.FarHealCooldown - dt);
            if (slime.KnockVelocity != 0)
            {
                slime.X += slime.KnockVelocity * dt;
                slime.KnockVelocity *= Math.Exp(-6 * dt);
                if (Math.Abs(slime.KnockVelocity) < 5) slime.KnockVelocity = 0;
            }
            slime.X = Math.Clamp(slime.X, minX, maxX);
            if (Over) continue; // The outro: slimes just bounce in place.
            if (slime.Shielded && slime.State != SlimeState.Defeated)
            {
                StepShield(slime, dt, minX, maxX);
                if (slime.Retreating && slime.State == SlimeState.Moving) continue; // Running for the edge: nothing else.
            }
            double dx = catX - slime.X;
            switch (slime.State)
            {
                case SlimeState.Spawning:
                    if (slime.StateTime >= s.SpawnDuration * (slime.IsBoss ? 2 : 1)) slime.Set(SlimeState.Moving);
                    break;
                case SlimeState.Stunned:
                    slime.StunRemaining -= dt;
                    if (slime.StunRemaining <= 0) slime.Set(SlimeState.Moving);
                    break;
                case SlimeState.Recover:
                    if (slime.StateTime >= slime.Type.Recover) slime.Set(SlimeState.Moving);
                    break;
                case SlimeState.Windup:
                    // A rescue never freezes the field: the windup runs on its announced time, so no
                    // attack waits to land on the cat's way down. Falling while the cat is lifted it
                    // misses; once the cat is down again it lands as usual (in reach, no landing
                    // protection), so lifting and dropping on the spot cannot cancel every hit. A cat
                    // only pressed (not lifted) is on the floor and is hit too.
                    if (slime.StateTime >= Windup(slime))
                    {
                        if (slime.Type.Dash) { slime.DashLeft = slime.Type.DashDistance; slime.DashHit = false; slime.Set(SlimeState.Dashing); }
                        else Strike(slime, catX);
                    }
                    break;
                case SlimeState.Dashing:
                    StepDash(slime, dt, catX, minX, maxX);
                    break;
                case SlimeState.Casting:
                    if (slime.StateTime >= HealWindup(slime)) FinishHeal(slime);
                    break;
                case SlimeState.Moving:
                    // A support enemy heals a hurt ally when it can (before thinking of the cat): one in
                    // reach, or else a badly hurt one anywhere on the field (a far heal).
                    if (slime.Type.HealAmount > 0 && slime.HealCooldown <= 0 && slime.HealLeft > 0 && (HealTarget(slime) ?? FarHealTarget(slime)) is Slime ally)
                    {
                        slime.FarHeal = Math.Abs(ally.X - slime.X) > slime.Type.HealRange;
                        slime.HealTargetId = ally.Id;
                        slime.Facing = ally.X < slime.X ? -1 : 1;
                        slime.Set(SlimeState.Casting);
                        events.Add(new(BattleEventKind.EnemyHealing, slime.X, slime.FarHeal ? 1 : 0));
                        // HealGuard: the ally takes no damage while the heal winds up (never a boss; the
                        // same ally only again HealGuardRepeat s after this heal would end).
                        slime.MendingId = 0;
                        if (slime.Type.HealGuard && !ally.IsBoss && ally.GuardAgainAt <= Time)
                        {
                            slime.MendingId = ally.Id;
                            ally.Mended = true;
                            ally.GuardAgainAt = Time + HealWindup(slime) + slime.Type.HealGuardRepeat;
                            events.Add(new(BattleEventKind.EnemyWarded, ally.X));
                        }
                        break;
                    }
                    // A support keeps behind its fighting allies and leaves the cat to them.
                    if (slime.Type.Support && (slime.KeepingBack = KeepBack(slime, dt, catX, minX, maxX))) break;
                    // A dasher runs off after its lunge.
                    if (RunAway(slime, dt, catX, minX, maxX)) break;
                    // While the user carries the cat the slimes follow it along the floor, but never
                    // wind up: a lifted cat cannot be hit. A slow-turning kind faces the cat only after
                    // its turn, and neither walks nor winds up meanwhile.
                    bool facingCat = Math.Abs(dx) <= 1 || Turn(slime, dx < 0 ? -1 : 1, dt);
                    // A dash enemy starts its windup from further away; its lane is set now.
                    double reach = slime.Type.Dash ? slime.Type.DashRange : slime.Range;
                    if (!catHeld && facingCat && Math.Abs(dx) <= reach && slime.Cooldown <= 0 && Grace <= 0 && MayAttack(slime) && (slime.IsBoss || WallBetween(slime.X, catX) == null))
                    {
                        slime.DashDirection = slime.Facing;
                        slime.Set(SlimeState.Windup);
                        lastWindup = Time;
                    }
                    // A guard holds its post between the cat and its support (it still hits a cat in reach).
                    else if (facingCat && GuardPost(slime) is double post)
                    {
                        if (Math.Abs(post - slime.X) > 6) slime.X = Math.Clamp(slime.X + Math.Sign(post - slime.X) * Math.Min(Math.Abs(post - slime.X), Speed(slime) * dt), minX, maxX);
                    }
                    // A shielded boss guards its edge: it slams a cat that comes close but never walks up.
                    else if (facingCat && Math.Abs(dx) > reach * 0.75 && !slime.Shielded)
                        slime.X =Math.Clamp(slime.X + Math.Sign(dx) * Math.Min(Math.Abs(dx), Speed(slime) * dt), minX, maxX);
                    break;
            }
            if (Over) break;
        }
        // Plain loops here and below: this runs every frame, so no per-call lambdas or lists.
        for (int i = slimes.Count - 1; i >= 0; i--)
            if (slimes[i].State == SlimeState.Defeated && slimes[i].StateTime >= s.DefeatDuration * (slimes[i].IsBoss ? 1.5 : 1)) slimes.RemoveAt(i);
        Separate();
    }

    public double Windup(Slime slime) => (slime.Enraged ? slime.Type.EnragedWindup : slime.Type.Windup) + (slime.Elite?.WindupAdd ?? 0);
    // A far heal winds up longer than one in reach.
    public static double HealWindup(Slime healer) => healer.FarHeal ? healer.Type.FarHealWindup : healer.Type.HealWindup;

    // How often guards are chosen (8 times a second) and where the cat stood when they last were.
    private const double DecisionInterval = 0.125;
    private double decisionClock, decisionCatX;
    // When the last windup began (attack turns).
    private double lastWindup = double.NegativeInfinity;

    // Faces `want` now, or after the kind's TurnSeconds of wanting to; true once it does.
    private static bool Turn(Slime slime, int want, double dt)
    {
        if (slime.Facing == want) { slime.TurnTime = 0; return true; }
        slime.TurnTime += dt;
        if (slime.Type.TurnSeconds > 0 && slime.TurnTime < slime.Type.TurnSeconds) return false;
        slime.Facing = want;
        slime.TurnTime = 0;
        return true;
    }

    // Attack turns: a free turn (fewer than MaxAttackers winding up or dashing) and AttackStagger
    // since the last windup. The boss never waits for one (its slam is the fight's centre).
    private bool MayAttack(Slime slime)
    {
        if (slime.IsBoss) return true;
        if (Time - lastWindup < Settings.AttackStagger) return false;
        if (Settings.MaxAttackers <= 0) return true;
        int busy = 0;
        foreach (var s in slimes) if (s != slime && s.State is SlimeState.Windup or SlimeState.Dashing) busy++;
        return busy < Settings.MaxAttackers;
    }

    // Each support still standing gets the nearest free blocker as its guard.
    private void AssignGuards()
    {
        foreach (var s in slimes) s.GuardOf = 0;
        foreach (var support in slimes)
        {
            if (!support.Type.Support || support.State == SlimeState.Defeated) continue;
            Slime? guard = null;
            foreach (var s in slimes)
                if (s.Type.Blocker && s.GuardOf == 0 && s.Targetable && !s.Shielded && (guard == null || Math.Abs(s.X - support.X) < Math.Abs(guard.X - support.X))) guard = s;
            if (guard != null) guard.GuardOf = support.Id;
        }
    }

    // A guard's post: between the cat (where it was at the last decision) and its support, the
    // support's KeepBack in front of it (closer when the cat is nearer). Null: not guarding.
    private double? GuardPost(Slime guard)
    {
        if (guard.GuardOf == 0 || Find(guard.GuardOf) is not { State: not SlimeState.Defeated } support) return null;
        int side = support.X >= decisionCatX ? 1 : -1;
        return support.X - side * Math.Min(support.Type.KeepBack, Math.Max(0, Math.Abs(support.X - decisionCatX) - 40));
    }

    // A far heal (FarHealBelow > 0, once per FarHealCooldown s): the most hurt fighting ally below
    // FarHealBelow of its HP, wherever it is on the field (null: none, or not yet).
    private Slime? FarHealTarget(Slime healer)
    {
        var type = healer.Type;
        if (type.FarHealBelow <= 0 || healer.FarHealCooldown > 0) return null;
        Slime? hurt = null;
        foreach (var s in slimes)
            if (s != healer && s.Targetable && !s.Type.Support && !s.Shielded && s.Hp < s.MaxHp * type.FarHealBelow &&
                (hurt == null || s.Hp / s.MaxHp < hurt.Hp / hurt.MaxHp)) hurt = s;
        return hurt;
    }

    // The most hurt ally in reach: alive, not this one, below its max HP.
    private Slime? HealTarget(Slime healer)
    {
        Slime? best = null;
        foreach (var s in slimes)
        {
            if (s == healer || !s.Targetable || s.Shielded || s.Hp >= s.MaxHp || Math.Abs(s.X - healer.X) > healer.Type.HealRange) continue;
            if (best == null || s.MaxHp - s.Hp > best.MaxHp - best.Hp) best = s;
        }
        return best;
    }

    // A support with a fighting ally (any enemy but a support, even one still appearing) on the
    // field: it stays on its side of the cat, KeepBack DIPs behind the nearest such ally there (or
    // that far from the cat when none is), walking at its own speed (slower than the cat runs, so
    // the cat catches it; at the field's edge it stops). False: no fighting ally, so it fights.
    private bool KeepBack(Slime support, double dt, double catX, double minX, double maxX)
    {
        int side = support.X >= catX ? 1 : -1;
        bool allies = false;
        double nearest = double.NaN, guard = double.NaN;
        foreach (var s in slimes)
        {
            if (s == support || s.State == SlimeState.Defeated || s.Type.Support) continue;
            allies = true;
            double ahead = (s.X - catX) * side;
            if (ahead >= 0 && (double.IsNaN(nearest) || ahead < nearest)) nearest = ahead;
            if (ahead >= 0 && s.GuardOf == support.Id && s.Targetable) guard = ahead;
        }
        if (!allies) return false;
        // HideBehindGuard: behind its own guard (the rock between it and the cat), not just any ally.
        if (support.Type.HideBehindGuard && !double.IsNaN(guard)) nearest = guard;
        double want = Math.Clamp(catX + side * ((double.IsNaN(nearest) ? 0 : nearest) + support.Type.KeepBack), minX, maxX);
        double gap = want - support.X;
        support.Facing = catX < support.X ? -1 : 1;
        // FleeSpeed: backing off from a cat that comes close it hurries (still slower than the cat runs).
        double speed = Speed(support);
        if (Math.Sign(gap) == side) speed *= FleeFactor(support, Math.Abs(support.X - catX));
        if (Math.Abs(gap) > 6) support.X = Math.Clamp(support.X + Math.Sign(gap) * Math.Min(Math.Abs(gap), speed * dt), minX, maxX);
        return true;
    }

    // How much faster a support backs off with the cat this close: 1 from 200 DIPs, FleeSpeed at 60.
    public static double FleeFactor(Slime support, double distance) =>
        1 + (support.Type.FleeSpeed - 1) * Math.Clamp((200 - distance) / 140, 0, 1);

    // HitAndRun: after its dash a dasher runs straight away from the cat (to the field's edge at most).
    private bool RunAway(Slime slime, double dt, double catX, double minX, double maxX)
    {
        if (slime.RunAwayLeft <= 0) return false;
        int away = slime.X >= catX ? 1 : -1;
        double before = slime.X, step = Speed(slime) * dt;
        slime.X = Math.Clamp(slime.X + away * step, minX, maxX);
        slime.Facing = away;
        slime.RunAwayLeft = slime.X == before ? 0 : slime.RunAwayLeft - step;
        return true;
    }

    // Who a heal guards: only allies of supports still winding up their heal on them. Run again the
    // moment a guard ends (heal done, healer pushed or defeated), so no hit falls in a stale guard;
    // another healer's guard still standing is kept.
    private void RefreshGuards()
    {
        foreach (var e in slimes) e.Mended = false;
        foreach (var e in slimes)
            if (e.State == SlimeState.Casting && e.MendingId != 0 && Find(e.MendingId) is { Targetable: true } guarded) guarded.Mended = true;
    }

    // The heal lands if its ally is still there, alive and in reach (a little slack for movement);
    // a far heal reaches it anywhere.
    private void FinishHeal(Slime healer)
    {
        healer.MendingId = 0;
        healer.Set(SlimeState.Recover);
        RefreshGuards();
        healer.HealCooldown = HealCooldown(healer);
        bool far = healer.FarHeal;
        if (far) healer.FarHealCooldown = healer.Type.FarHealCooldown;
        healer.FarHeal = false;
        var ally = Find(healer.HealTargetId);
        healer.HealTargetId = 0;
        if (ally == null || !ally.Targetable || (!far && Math.Abs(ally.X - healer.X) > healer.Type.HealRange + 20)) return;
        // Its heals scale like enemy HP (HealPower); an elite's first heal that lands is stronger (still within its budget).
        double heal = healer.Type.HealAmount * healer.HealPower * healer.HealBoost;
        if (healer.HealPower != 1) heal = Math.Round(heal);
        double amount = Math.Min(Math.Min(heal, ally.MaxHp - ally.Hp), healer.HealLeft);
        if (amount <= 0) return;
        ally.Hp += amount;
        healer.HealLeft -= amount;
        healer.HealBoost = 1;
        AddEffect(BattleEffectKind.Mend, ally.X, ally.Height / 2);
        AddEffect(BattleEffectKind.Number, ally.X + (ally.IsBoss ? ally.Width * 0.38 : 0), ally.Height + 10, -Math.Round(amount));
        events.Add(new(BattleEventKind.EnemyHealed, ally.X, amount));
    }

    // The ally a support enemy is healing right now (null when it is not casting).
    public Slime? Healing(Slime healer) => healer.State == SlimeState.Casting ? Find(healer.HealTargetId) : null;
    private double Speed(Slime slime) => (slime.Enraged ? slime.Type.EnragedSpeed : slime.Type.Speed) * level.EnemySpeed;

    private readonly List<Slime> scratch = new();
    private static readonly Comparison<Slime> ByX = (a, b) => a.X.CompareTo(b.X);

    // Slimes do not stack on one spot. A support falling back stays out of this: it has to slip past
    // the ally it gets behind (pushing each other, the two would jam and hold the ally off the cat).
    // Blockers keep apart only from each other: they bar the cat's way, not their allies' (a rock in
    // front would otherwise shield the cat from everything behind it).
    private void Separate()
    {
        Separate(false);
        Separate(true);
    }

    private void Separate(bool blockers)
    {
        var live = scratch;
        live.Clear();
        // A leaping boss flies over the others.
        foreach (var slime in slimes) if (slime.State != SlimeState.Defeated && !slime.KeepingBack && slime.LeapHeight <= 0 && slime.Type.Blocker == blockers) live.Add(slime);
        live.Sort(ByX);
        for (int i = 1; i < live.Count; i++)
        {
            double gap = (live[i].Width + live[i - 1].Width) / 2 * 0.7;
            double overlap = gap - (live[i].X - live[i - 1].X);
            if (overlap <= 0) continue;
            var (minA, maxA) = Bounds(live[i - 1]);
            var (minB, maxB) = Bounds(live[i]);
            live[i - 1].X = Math.Clamp(live[i - 1].X - overlap / 2, minA, maxA);
            live[i].X = Math.Clamp(live[i].X + overlap / 2, minB, maxB);
        }
    }

    private void Strike(Slime slime, double catX)
    {
        slime.Set(SlimeState.Recover);
        slime.Cooldown = AttackCooldown(slime);
        if (slime.Type.Slam) AddEffect(BattleEffectKind.Slam, slime.X, 0, slime.Range);
        // Missed: the cat moved away, is protected, or is off the floor right now.
        if (CatLifted || Math.Abs(catX - slime.X) > slime.Range + 12 || Grace > 0) return;
        HurtCat(slime, catX, false, slime.Type.SlamShare);
    }

    // Half the cat's width on the floor: a dash that passes over this hits it.
    public const double CatHalfWidth = 18;

    // The lunge is fast, so the hit is checked along the whole path of this frame (never skipped
    // between two frames), once per dash, and only while the cat is on the floor (a pressed cat
    // too, without the stun) and not under landing protection. The dash runs its locked lane to
    // the end whatever the cat does, never turning after it.
    private void StepDash(Slime slime, double dt, double catX, double minX, double maxX)
    {
        double before = slime.X;
        double step = Math.Min(slime.DashLeft, slime.Type.DashSpeed * (slime.Elite?.DashSpeed ?? 1) * level.EnemySpeed * dt);
        slime.X = Math.Clamp(slime.X + slime.DashDirection * step, minX, maxX);
        slime.DashLeft = slime.X == before ? 0 : slime.DashLeft - step; // Stopped by the field's edge.
        double reach = slime.HitWidth / 2 + CatHalfWidth;
        if (!slime.DashHit && !CatLifted && Grace <= 0 && catX >= Math.Min(before, slime.X) - reach && catX <= Math.Max(before, slime.X) + reach)
        {
            slime.DashHit = true;
            HurtCat(slime, catX, true);
        }
        if (slime.DashLeft <= 0 && !Over)
        {
            slime.Set(SlimeState.Recover);
            slime.Cooldown = AttackCooldown(slime);
            slime.RunAwayLeft = slime.Type.HitAndRun; // Hit and run: off it goes once it recovers.
        }
    }

    // Every hit on the cat: the Shield takes all of it (no damage, no stun), otherwise armor
    // lessens it. stun: also stun the cat, unless it is in the user's hands, already stunned or
    // still immune after its last stun.
    private void HurtCat(Slime from, double catX, bool stun, double share = 1)
    {
        if (!LandHit(from.Damage * share, catX, out double hurt)) return;
        // Thorns: part of what the cat really took goes back to the attacker.
        if (Active("thorns") is SynergySpec thorns && hurt > 0 && from.Targetable && !from.Shielded)
        {
            Reflects++;
            events.Add(new(BattleEventKind.Reflected, from.X));
            Damage(from, hurt * thorns.Amount, DamageSource.Reflect);
            if (Over) return;
        }
        if (stun && !from.Defanged && !CatHeld && !CatStunned && StunImmunity <= 0 && Settings.StunDurationSeconds > 0)
        {
            CatStunRemaining = Settings.StunDurationSeconds;
            StunsTaken++;
            CancelSwing();
            events.Add(new(BattleEventKind.CatStunned, catX));
        }
    }

    // A hit of `damage` on the cat (a slime's, or an infected app's bell): the Shield takes all of it,
    // then the Turtle shell, otherwise armor lessens it. True when it got through and the cat is still up.
    private bool LandHit(double damage, double catX, out double hurt)
    {
        hurt = 0;
        if (ShieldRemaining > 0)
        {
            events.Add(new(BattleEventKind.Blocked, catX));
            if (counterBonus > 0) CounterReady = true;
            return false;
        }
        // Turtle shell: one whole hit stopped (no damage, no stun), then it grows back.
        if (ShellReady)
        {
            ShellCooldown = shellRegrow;
            ShellBlocks++;
            if (counterBonus > 0) CounterReady = true;
            AddEffect(BattleEffectKind.Block, catX, 50);
            events.Add(new(BattleEventKind.ShellBlocked, catX));
            return false;
        }
        hurt = damage * ArmorFactor * (1 + recklessHurt);
        CatHp = Math.Max(0, CatHp - hurt);
        NoteHurt(hurt);
        HurtFlash = Settings.HurtFlashSeconds;
        events.Add(new(BattleEventKind.CatHurt, catX, hurt));
        if (CatHp <= 0) { Finish(BattlePhase.Lost); return false; }
        return true;
    }
}
