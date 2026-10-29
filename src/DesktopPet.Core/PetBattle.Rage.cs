using System;

namespace DesktopPet;

// 1.5 M07, docs/UPDATE-1.5.md K5 "Mèo nổi giận": a battle stance, not the Angry emotion.
// - The rage bar fills as the cat lands its own swings (RageHitGain each, held swings too) and as it
//   loses HP (RageHurtGain a point); never while raging. It starts empty in every battle.
// - Full, it waits for the user's click on the bar (never by itself). The click works while the battle
//   runs and the cat is on the floor and not stunned (a stun already under way is never cut short).
// - For RageSeconds of battle time (Pause, the guide and the card choice stop it; held or not it runs
//   on): every landed swing also hits every other enemy in the weapon's reach (RageSweep), its knock
//   back is stronger, and nothing stuns the cat. The cat is drawn RageScale bigger (drawing only: its
//   window, the drag and its hit box on the floor stay the same).
public sealed partial class BattleSim
{
    public double Rage { get; private set; }
    public double RageRemaining { get; private set; }
    public bool Raging => RageRemaining > 0;
    public bool RageFull => Rage >= 1;
    public bool RageReady => RageFull && !Raging && !Over && !Paused && !ChoosingUpgrade && !SkillsLocked && !CatStunned;
    public int Rages { get; private set; }
    public int RageHits { get; private set; }
    // How much of the stance shows (0..1): it swells over its first RageEase s and shrinks over its last.
    public const double RageEase = 0.25;
    public double RageLook => Raging ? Math.Clamp(Math.Min(Settings.RageSeconds - RageRemaining, RageRemaining) / RageEase, 0, 1) : 0;

    public bool StartRage()
    {
        if (!RageReady) return false;
        Rage = 0;
        RageRemaining = Settings.RageSeconds;
        Rages++;
        events.Add(new(BattleEventKind.Raged, lastCatX));
        return true;
    }

    private void GainRage(double amount)
    {
        if (Raging || Over || !(amount > 0) || Rage >= 1) return;
        Rage = Math.Min(1, Rage + amount);
        if (Rage >= 1) events.Add(new(BattleEventKind.RageFull, lastCatX));
    }

    private void StepRage(double dt)
    {
        if (!Raging) return;
        RageRemaining = Math.Max(0, RageRemaining - dt);
        if (RageRemaining <= 0) events.Add(new(BattleEventKind.RageEnded, lastCatX));
    }

    // A landed hit's knock back while raging: stronger, at least RageKnock.
    private double RageKnockOf(double knock) => Raging ? Math.Max(knock * Settings.RageKnockFactor, Settings.RageKnock) : knock;

    // A sweeping swing (raging, or a weapon with Sweep, PetBattle.Weapons.cs): `damage` on every other
    // enemy within the weapon's reach (+ the hit's 14 DIPs of slack): in front of the cat or overlapping
    // it, on the floor (air: only enemies in the air, for the held cat). Blocked like a combo's splash; no
    // bonus of its own; knocked back away from the cat if the hit knocks at all; a pulling weapon pulls.
    private void Sweep(Slime target, double damage, double catX, int facing, CatPose? held = null)
    {
        blasting = true;
        foreach (var other in slimes.ToArray())
        {
            if (other == target || !other.Targetable || other.Shielded || other.Handled || other.Faded) continue;
            if (held is CatPose cat)
            {
                if (!other.Airborne || Gap(cat, other) > HeldReach + 14) continue;
            }
            else
            {
                if (other.Airborne || other.Riding) continue;
                double dx = other.X - catX;
                if (dx * facing < -CatHalfWidth || Math.Abs(dx) - other.HitWidth / 2 > arms.Range + 14) continue;
            }
            if (Raging) RageHits++; else SweepHits++;
            AddEffect(BattleEffectKind.Spark, other.X, other.Altitude + other.Height * 0.6);
            if (Damage(other, damage, DamageSource.Splash) == HitResult.Landed && other.State != SlimeState.Defeated && held == null)
            {
                double knock = RageKnockOf(arms.Knock + knockBonus);
                if (knock > 0 && other.Elite is not { PushImmune: true }) other.KnockVelocity = (other.X < catX ? -1 : 1) * knock * other.Type.PushFactor;
                PullIn(other, catX);
            }
            if (Over) break;
        }
        blasting = false;
        RefreshGuards();
    }
}
