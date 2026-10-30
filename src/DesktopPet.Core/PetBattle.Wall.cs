using System;
using System.Collections.Generic;

namespace DesktopPet;

// 1.5 M15, docs/UPDATE-1.5.md (S7, S8) and docs/APPROVED-EXCEPTIONS.md → Battle Mode (a throw reaches only
// enemies with their own throw interaction):
// - S7 the ice slime (EnemyType.Ice): it comes in with an ice shell that the cat's weapon only scratches
//   (Armor × the hit; the cat goes for others first). The thrown cat flying into it fast enough breaks
//   the shell (its throwHit too); wet from the spray, a slower throw does. The shell grows back later.
// - S8 the wall climber (EnemyType.Cling): it goes to the nearest edge of the field and climbs it, out of
//   reach from the floor (Altitude > 0: the cat in the user's hand swings at it, PetBattle.Held.cs). Up
//   there it aims at the cat's spot (a ring on the floor) and drops spikes: the cat on the floor in the
//   ring is hurt (no stun; lifting, landing protection and the Shield dodge it, as the spitter's goo).
//   The thrown cat flying into it, Push, the Grabber's yank or any daze knock it off: it falls, lies
//   dazed, fights on the floor a while and goes back up.
// The cat's physics are only read (its pose). No random stream is used.
public sealed partial class BattleSim
{
    // The highest a wall climber's bottom goes over the floor (the field strip is 170 DIPs high).
    public const double ClingTop = 110;
    public int IceScratches { get; private set; }
    public int IceBreaks { get; private set; }
    public int ClingKnocks { get; private set; }
    public int SpikeHits { get; private set; }

    // The cat's own blows an ice shell turns into scratches (bombs, skills, apps, poison, throws go through).
    private static bool Armoured(DamageSource source) => source is DamageSource.Weapon or DamageSource.Combo or DamageSource.Splash or DamageSource.Echo;

    // A hit on an ice slime (from Damage): what is left of it after the shell.
    private double IceShell(Slime s, double amount, DamageSource source)
    {
        if (!s.Iced || s.Type.Ice is not IceSpec ice || !Armoured(source)) return amount;
        IceScratches++;
        events.Add(new(BattleEventKind.IceScratched, s.X, s.Id));
        return amount * ice.Armor;
    }

    // How fast the thrown cat must fly into it for its throwHit: an ice shell wet from the spray gives way
    // to a slower throw.
    public static double ThrowSpeed(Slime s, ThrowHitSpec rule) =>
        s.Type.Ice is IceSpec ice && s.Iced && s.Wet ? Math.Min(rule.MinSpeed, ice.WetSpeed) : rule.MinSpeed;

    public static double IceRegrowTime(Slime s) => s.Type.Ice is not IceSpec ice ? 0 : s.Elite != null ? ice.EliteRegrow : ice.Regrow;

    // The thrown cat flew into it (PetBattle.Held.cs, before its throwHit): a shell breaks, a climber
    // falls off its edge.
    private void Thrown(Slime s)
    {
        if (s.Iced)
        {
            s.Iced = false;
            s.IceClock = IceRegrowTime(s);
            IceBreaks++;
            AddEffect(BattleEffectKind.Poof, s.X, s.Altitude + s.Height / 2);
            events.Add(new(BattleEventKind.IceShattered, s.X, s.Id));
        }
        if (s.OnWall) KnockOff(s, false);
    }

    // Every step of an ice slime still in the battle: a broken shell grows back.
    private void StepIce(Slime s, double dt)
    {
        if (s.Iced || s.Type.Ice == null || IceRegrowTime(s) <= 0) return;
        s.IceClock -= dt;
        if (s.IceClock > 0) return;
        s.Iced = true;
        events.Add(new(BattleEventKind.IceRegrown, s.X, s.Id));
    }

    // Where a climber on its edge is (battle x of its centre).
    private double WallX(Slime s) => s.WallSide < 0 ? Left + s.Width * 0.35 : Right - s.Width * 0.35;

    // The height its bottom climbs to: a second one on the same edge goes higher.
    private double ClingHeight(Slime s, ClingSpec c)
    {
        int below = 0;
        foreach (var o in slimes)
            if (o != s && o.Targetable && o.Type.Cling != null && o.OnWall && o.WallSide == s.WallSide) below++;
        return Math.Min(ClingTop, c.Height + c.Spacing * below);
    }

    // Every step of a wall climber that goes on (from StepSlimes). True: its step is done here (going to its
    // edge, climbing, up there, falling); false: it is on the floor and steps as any slime.
    private bool StepClinger(Slime s, ClingSpec c, double dt, double catX, double minX, double maxX)
    {
        switch (s.Cling)
        {
            case ClingPhase.Ground:
                if (s.State != SlimeState.Moving) return false;
                if (s.ClingClock > 0) { s.ClingClock -= dt; return false; }
                // Off to the nearest edge (no blows on the way).
                if (s.WallSide == 0) s.WallSide = s.X - Left <= Right - s.X ? -1 : 1;
                double wall = WallX(s), gap = wall - s.X;
                if (Math.Abs(gap) > 1)
                {
                    s.Facing = gap < 0 ? -1 : 1;
                    s.X = Math.Clamp(s.X + Math.Sign(gap) * Math.Min(Math.Abs(gap), Speed(s) * dt), minX, maxX);
                    if (Math.Abs(wall - s.X) > 1) return true;
                }
                s.X = wall;
                s.Cling = ClingPhase.Climbing;
                s.ClingTo = ClingHeight(s, c);
                s.KnockVelocity = 0;
                if (SelectedId == s.Id) SelectedId = null;
                return true;
            case ClingPhase.Climbing:
            case ClingPhase.Clinging:
                if (s.State == SlimeState.Stunned) { KnockOff(s, false); return true; }
                if (s.State != SlimeState.Moving) s.Set(SlimeState.Moving);
                s.X = WallX(s);
                s.Facing = -s.WallSide;
                s.KnockVelocity = 0;
                double act = dt * s.ChillFactor;
                if (s.Cling == ClingPhase.Climbing)
                {
                    s.Altitude = Math.Min(s.ClingTo, Math.Max(1, s.Altitude + c.Climb * act));
                    if (s.Altitude < s.ClingTo) return true;
                    s.Cling = ClingPhase.Clinging;
                    s.ShotClock = c.Cooldown / 2;
                    events.Add(new(BattleEventKind.ClingClimbed, s.X, s.Id));
                    return true;
                }
                Spike(s, c, act, catX);
                return true;
            default: // Falling: down off its edge, drifting into the field.
                s.FallSpeed += Gravity * dt;
                s.Altitude = Math.Max(0, s.Altitude - s.FallSpeed * dt);
                s.X = Math.Clamp(s.X - s.WallSide * 60 * dt, minX, maxX);
                if (s.Altitude > 0) return true;
                s.Cling = ClingPhase.Ground;
                s.FallSpeed = 0;
                s.WallSide = 0;
                s.ClingClock = c.Ground;
                if (s.State != SlimeState.Stunned) s.Set(SlimeState.Stunned);
                s.StunRemaining = Math.Max(s.StunRemaining, c.Downed);
                AddEffect(BattleEffectKind.Thud, s.X, 0);
                events.Add(new(BattleEventKind.ClingLanded, s.X, s.Id));
                return true;
        }
    }

    // Up its edge: a wait, an aim at the cat's spot on the floor, then its spikes drop there.
    private void Spike(Slime s, ClingSpec c, double act, double catX)
    {
        if (s.Aiming)
        {
            s.AimLeft = Math.Max(0, s.AimLeft - act);
            if (s.AimLeft > 0) return;
            foreach (double x in Spots(s, c))
                goos.Add(new BattleGoo
                {
                    OwnerId = s.Id, FromX = s.X, FromHeight = s.Altitude + s.Width / 2, X = x, Flight = c.Flight, Remaining = c.Flight,
                    Damage = s.Damage, Radius = c.Radius, Spike = true, Tint = s.Type.Color
                });
            s.ShotClock = c.Cooldown;
            events.Add(new(BattleEventKind.ClingShot, s.X, s.Id));
            return;
        }
        s.ShotClock -= act;
        if (s.ShotClock > 0) return;
        s.SpitAt = Math.Clamp(catX, Left, Right);
        s.AimLeft = c.Aim;
        events.Add(new(BattleEventKind.ClingAimed, s.SpitAt, s.Id));
    }

    // Where a climber aiming will drop its spikes: the cat's spot then (an elite's spread round it).
    public IEnumerable<double> SpikeTargets(Slime s) =>
        s.Aiming && s.Type.Cling is ClingSpec c && s.OnWall ? Spots(s, c) : Array.Empty<double>();

    private IEnumerable<double> Spots(Slime s, ClingSpec c)
    {
        int n = s.Elite != null ? c.EliteSpikes : 1;
        for (int i = 0; i < n; i++) yield return Math.Clamp(s.SpitAt + (i - (n - 1) / 2.0) * c.EliteSpread, Left, Right);
    }

    // Knocked off its edge (the thrown cat, Push, a daze, the Grabber's yank): it falls and lies dazed.
    private bool KnockOff(Slime s, bool yanked)
    {
        if (s.Type.Cling == null || !s.OnWall || !s.Targetable) return false;
        s.Cling = ClingPhase.Falling;
        s.FallSpeed = 0;
        s.AimLeft = 0;
        if (s.State != SlimeState.Stunned) s.Set(SlimeState.Stunned);
        s.StunRemaining = Math.Max(s.StunRemaining, 0.1);
        ClingKnocks++;
        AddEffect(BattleEffectKind.Push, s.X, s.Altitude + s.Height / 2);
        events.Add(new(BattleEventKind.ClingKnocked, s.X, yanked ? 1 : 0));
        return true;
    }
}
