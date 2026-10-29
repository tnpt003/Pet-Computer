using System;

namespace DesktopPet;

// 1.5 M14, S6 the burrowing slime (EnemyType.Burrow), docs/UPDATE-1.5.md: it digs under the floor and
// goes to the cat there (only its mound shows: nothing hits it, it attacks nothing, the cat does not go
// for it). Under the cat on the floor (never one in the user's hand, never under landing protection),
// a ring of cracks shows where the cat stands, then it comes up there: the cat still on the floor in the
// ring is hurt and stunned (the Shield, the Turtle shell, rage and stun immunity work as for any hit).
// Out, it fights a while like any slime, then digs in again. Lifting the cat out of the ring dodges it;
// the cat let go onto the ring while the cracks show or it comes up pins it (dazed: the drop's own
// interaction, approved for S6 only). The Grabber pulls it up from under the floor. The cat's physics
// are only read (where it lands). No random stream is used.
public sealed partial class BattleSim
{
    public int BurrowHits { get; private set; }
    public int BurrowsPinned { get; private set; }
    public int BurrowsPulled { get; private set; }

    private static double AboveTime(Slime s, BurrowSpec b) => s.Elite != null ? b.EliteAbove : b.Above;
    public static double CrackTime(Slime s) => s.Type.Burrow is not BurrowSpec b ? 0 : s.Elite != null ? b.EliteCrack : b.Crack;

    // Every step of a burrower that goes on (from StepSlimes). True: its step is done here (digging,
    // under, cracks, coming up); false: it is out and steps as any slime.
    private bool StepBurrow(Slime s, BurrowSpec b, double dt, double catX, bool catHeld, double minX, double maxX)
    {
        switch (s.Burrow)
        {
            case BurrowPhase.Above:
                if (s.State != SlimeState.Moving) return false;
                s.BurrowClock -= dt;
                if (s.BurrowClock > 0) return false;
                s.Burrow = BurrowPhase.Digging;
                s.BurrowClock = b.Dig;
                return true;
            case BurrowPhase.Digging:
                // Knocked about while digging: it stays out a while longer.
                if (s.State != SlimeState.Moving) { s.Burrow = BurrowPhase.Above; s.BurrowClock = AboveTime(s, b); return false; }
                s.BurrowClock -= dt * s.ChillFactor;
                if (s.BurrowClock > 0) return true;
                s.Burrow = BurrowPhase.Under;
                s.BurrowRest = b.Rest;
                s.KnockVelocity = 0;
                if (SelectedId == s.Id) SelectedId = null;
                events.Add(new(BattleEventKind.BurrowDug, s.X, s.Id));
                return true;
            case BurrowPhase.Under:
                s.BurrowRest = Math.Max(0, s.BurrowRest - dt);
                double gap = catX - s.X;
                if (Math.Abs(gap) > 1) s.Facing = gap < 0 ? -1 : 1;
                s.X = Math.Clamp(s.X + Math.Sign(gap) * Math.Min(Math.Abs(gap), Speed(s) * b.Speed * dt), minX, maxX);
                if (s.BurrowRest <= 0 && !catHeld && Grace <= 0 && Math.Abs(catX - s.X) <= 12)
                {
                    s.Burrow = BurrowPhase.Cracking;
                    s.CrackX = catX;
                    s.BurrowClock = CrackTime(s);
                    events.Add(new(BattleEventKind.BurrowCracked, catX, s.Id));
                }
                return true;
            case BurrowPhase.Cracking:
                s.BurrowClock -= dt * s.ChillFactor;
                if (s.BurrowClock <= 0) Emerge(s, b, minX, maxX);
                return true;
            default: // Emerging
                if (s.State != SlimeState.Moving) { s.Burrow = BurrowPhase.Above; s.BurrowClock = AboveTime(s, b); return false; }
                s.BurrowClock -= dt;
                if (s.BurrowClock > 0) return true;
                s.Burrow = BurrowPhase.Above;
                s.BurrowClock = AboveTime(s, b);
                return true;
        }
    }

    // It comes up in its ring: the cat on the floor still in it is hit.
    private void Emerge(Slime s, BurrowSpec b, double minX, double maxX)
    {
        s.X = Math.Clamp(s.CrackX, minX, maxX);
        s.Burrow = BurrowPhase.Emerging;
        s.BurrowClock = b.Emerge;
        AddEffect(BattleEffectKind.Slam, s.CrackX, 0, b.Radius);
        bool hit = !CatLifted && Grace <= 0 && Math.Abs(lastCatX - s.CrackX) <= b.Radius;
        events.Add(new(BattleEventKind.BurrowEmerged, s.CrackX, hit ? 1 : 0));
        if (!hit) return;
        BurrowHits++;
        HurtCat(s, lastCatX, true, 1, b.Stun, crush: true);
    }

    // The cat let go after a real lift has just landed at x (PetBattle.cs): a burrower whose cracks show
    // there, or coming up there, is pinned.
    private void CatLanded(double x)
    {
        foreach (var s in slimes)
        {
            if (s.Type.Burrow is not BurrowSpec b || !s.Targetable || s.Burrow is not (BurrowPhase.Cracking or BurrowPhase.Emerging)) continue;
            if (Math.Abs(x - s.CrackX) > b.Radius) continue;
            var (minX, maxX) = Bounds(s);
            s.X = Math.Clamp(s.CrackX, minX, maxX);
            s.Burrow = BurrowPhase.Above;
            s.BurrowClock = AboveTime(s, b);
            s.KnockVelocity = 0;
            s.StunRemaining = b.Pinned * s.Type.StunFactor;
            s.Set(SlimeState.Stunned);
            BurrowsPinned++;
            AddEffect(BattleEffectKind.Thud, s.X, 0);
            events.Add(new(BattleEventKind.BurrowPinned, s.X, s.Id));
        }
    }

    // The Grabber took it from under the floor (PetBattle.Grabber.cs): out, and it stays out Pinned s.
    private void PullUp(Slime s)
    {
        if (s.Type.Burrow is not BurrowSpec b || !s.Burrowed) return;
        s.Burrow = BurrowPhase.Above;
        s.BurrowClock = b.Pinned;
        BurrowsPulled++;
        events.Add(new(BattleEventKind.BurrowPulled, s.X, s.Id));
    }
}
