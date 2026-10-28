using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// Where the cat really is while the user handles it (1.5 M03), read from the window and never
// written back (the drag and throw physics stay the window's): X its centre (DIPs, screen), Height
// its centre over the battle floor (DIPs, up), VX/VY its velocity (DIPs/s, VY down), InHand carried
// by the user (past the drag threshold), Flying thrown or falling.
public readonly record struct CatPose(double X, double Height, double VX, double VY, bool InHand, bool Flying);

// 1.5 M03, docs/UPDATE-1.5.md (approved Battle Mode addition, docs/APPROVED-EXCEPTIONS.md):
// - The cat in the user's hand swings by itself at an enemy in the air (EnemyType.Hover) within its
//   weapon's reach, never at one on the floor. A plain hit (the build's damage, Cheer, Reckless, Claws
//   and Finishing blow count; no combo, Opener, Momentum, counter, echo, splash or knock back), on its
//   own swing clock so the swing on the floor is untouched.
// - The thrown cat flying through an enemy with a ThrowHit, fast enough, hurts and/or dazes it, once
//   per enemy per throw; every other enemy ignores a throw.
// The cat still takes no hit while held or flying.
public sealed partial class BattleSim
{
    private double heldSwingTime = -1, heldCooldown;
    private int heldTargetId;
    private bool heldHit;
    private CatPose? lastFlight;
    private readonly HashSet<int> thrownInto = new();

    public bool HeldSwinging => heldSwingTime >= 0;
    // The way the held cat's swing goes (1 right, -1 left).
    public int HeldFacing { get; private set; } = 1;
    public int HeldHits { get; private set; }
    public int ThrowHits { get; private set; }

    // How far from the cat's centre to an enemy's edge its weapon reaches: as on the floor
    // (Reach: arms.Range centre to centre for a basic slime).
    private double HeldReach => arms.Range - Settings.SlimeWidth / 2;

    // Every battle step that goes on (not paused, not over), held or not.
    private void StepHeld(double dt, CatPose? pose)
    {
        heldCooldown = Math.Max(0, heldCooldown - dt);
        if (pose is not CatPose cat)
        {
            CancelHeldSwing();
            thrownInto.Clear();
            lastFlight = null;
            return;
        }
        if (cat.InHand && CatLifted && !CatStunned) HeldSwing(dt, cat);
        else CancelHeldSwing();
        if (cat.Flying && !cat.InHand) ThrowContact(cat);
        else { thrownInto.Clear(); lastFlight = null; }
    }

    private void CancelHeldSwing() { heldSwingTime = -1; heldHit = false; heldTargetId = 0; }

    private void HeldSwing(double dt, CatPose cat)
    {
        if (heldSwingTime >= 0)
        {
            heldSwingTime += dt;
            if (!heldHit && heldSwingTime >= arms.HitTime) { heldHit = true; ResolveHeldHit(cat); }
            if (heldSwingTime >= arms.SwingDuration) CancelHeldSwing();
            return;
        }
        if (heldCooldown > 0 || AirTarget(cat, 0) is not Slime target) return;
        heldSwingTime = 0; heldHit = false; heldTargetId = target.Id;
        HeldFacing = target.X < cat.X ? -1 : 1;
        heldCooldown = Math.Max(arms.AttackCooldown * CooldownFactor / (CheerRemaining > 0 ? Settings.CheerSpeed : 1) / LagFactor, arms.SwingDuration);
        events.Add(new(BattleEventKind.Swing, cat.X));
    }

    // The nearest enemy in the air within the held cat's reach (+ slack), never a shielded one.
    private Slime? AirTarget(CatPose cat, double slack)
    {
        Slime? best = null;
        double bestGap = double.PositiveInfinity;
        foreach (var s in slimes)
        {
            if (!s.Airborne || !s.Targetable || s.Shielded) continue;
            double gap = Gap(cat, s);
            if (gap <= HeldReach + slack && gap < bestGap) { best = s; bestGap = gap; }
        }
        return best;
    }

    // From the cat's centre to the nearest point of the enemy's body (0: inside it).
    private static double Gap(CatPose cat, Slime s)
    {
        double dx = Math.Max(0, Math.Abs(cat.X - s.X) - s.HitWidth / 2);
        double dy = cat.Height < s.Altitude ? s.Altitude - cat.Height : Math.Max(0, cat.Height - (s.Altitude + s.Height));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // The held swing's one hit: its own target, still in the air and in reach at this moment.
    private void ResolveHeldHit(CatPose cat)
    {
        var slime = Find(heldTargetId);
        if (slime == null || !slime.Targetable || !slime.Airborne || Gap(cat, slime) > HeldReach + 14) return;
        double damage = arms.Damage * DamageFactor * (CheerRemaining > 0 ? Settings.CheerDamage : 1);
        if (Reckless) damage *= 1 + recklessBonus;
        bool crit = critChance > 0 && strikeRandom.NextDouble() < critChance;
        if (crit) { damage *= 2; Crits++; }
        if (executeBonus > 0 && slime.Hp < slime.MaxHp * ExecuteBelow) { damage *= 1 + executeBonus; ExecuteHits++; }
        HeldHits++;
        events.Add(new(BattleEventKind.SlimeHit, slime.X));
        events.Add(new(BattleEventKind.HeldHit, slime.X, slime.Id));
        AddEffect(crit ? BattleEffectKind.Combo : BattleEffectKind.Spark, slime.X, slime.Altitude + slime.Height / 2);
        hitTone = crit ? NumberTone.Crit : NumberTone.Plain;
        double plain = damage;
        if (Damage(slime, damage, DamageSource.Weapon) == HitResult.Landed) { slime.Opened = true; HitStatuses(slime); GainRage(Settings.RageHitGain); }
        hitTone = NumberTone.Plain;
        // Raging or a sweeping weapon: every other enemy in the air within reach too (PetBattle.Rage.cs).
        if ((Raging || arms.Sweep) && !Over) Sweep(slime, plain, cat.X, HeldFacing, cat);
    }

    // The flight since the last step (a segment, so a fast throw cannot skip over an enemy) against
    // every enemy that reacts to a throw, its body grown by the cat's half width.
    private void ThrowContact(CatPose cat)
    {
        var from = lastFlight ?? cat;
        lastFlight = cat;
        double speed = Math.Sqrt(cat.VX * cat.VX + cat.VY * cat.VY);
        foreach (var s in slimes.ToArray())
        {
            if (s.Type.ThrowHit is not ThrowHitSpec rule || !s.Targetable || speed < rule.MinSpeed || thrownInto.Contains(s.Id)) continue;
            double pad = CatHalfWidth;
            if (!Crosses(from.X, from.Height, cat.X, cat.Height, s.X - s.HitWidth / 2 - pad, s.Altitude - pad, s.X + s.HitWidth / 2 + pad, s.Altitude + s.Height + pad)) continue;
            thrownInto.Add(s.Id);
            ThrowHits++;
            events.Add(new(BattleEventKind.ThrowHit, s.X, s.Id));
            AddEffect(BattleEffectKind.Combo, s.X, s.Altitude + s.Height / 2);
            if (rule.Damage > 0) Damage(s, rule.Damage, DamageSource.Throw);
            if (rule.Stun > 0 && s.Targetable) Daze(s, rule.Stun);
        }
    }

    // Whether the segment (x0, y0)–(x1, y1) touches the box (slab test).
    internal static bool Crosses(double x0, double y0, double x1, double y1, double left, double bottom, double right, double top)
    {
        double enter = 0, leave = 1;
        bool Slab(double start, double delta, double min, double max)
        {
            if (Math.Abs(delta) < 1e-9) return start >= min && start <= max;
            double a = (min - start) / delta, b = (max - start) / delta;
            if (a > b) (a, b) = (b, a);
            enter = Math.Max(enter, a); leave = Math.Min(leave, b);
            return enter <= leave;
        }
        return Slab(x0, x1 - x0, left, right) && Slab(y0, y1 - y0, bottom, top);
    }
}
