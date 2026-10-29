using System;

namespace DesktopPet;

// 1.5 M06, docs/UPDATE-1.5.md (K) and docs/BATTLE-DATA.md → Kẹp gắp, Kéo cắt, Bình xịt nước: the Grabber
// (SkillAim.Drag). Chosen, then pressed on a small enemy (never a boss or a rock; a kind's scale at most
// GrabberMaxScale) it lifts it: the enemy hangs where the cursor holds it, does nothing (a windup is
// lost), is no target of the cat and shoves no one. A rider is taken off its mount, a mount's rider
// falls off. Let go (or held GrabberHoldSeconds) it flies on with the cursor's speed, under gravity,
// down to the floor. Let go fast enough (GrabberThrowSpeed) it is thrown: the first enemy it flies
// into and itself lose GrabberHitDamage (a boss half), the other is dazed GrabberStun s and it drops;
// into the edge or a wall it is hurt alone; landing, it is dazed GrabberStun s. The cooldown starts
// when it is let go. Lifting the cat drops it (no throw); Pause and the frozen guide freeze it (time
// runs only in Step); the battle ending or the field moving puts it down.
public sealed partial class BattleSim
{
    public const double Gravity = 1800, GrabTopSpeed = 1600, GrabMaxLift = 100, ThrowHop = 260;
    // The enemy in the claw (0: none) and the throws that hit something.
    public int GrabbedId { get; private set; }
    public int GrabberHits { get; private set; }
    // Where the cursor is (battle x, height over the floor), the grip on the enemy (from the cursor to
    // its centre and its bottom), the time held and the cursor's speed (smoothed).
    private double grabX, grabHeight, gripX, gripY, grabHeld, grabVX, grabVY;
    public bool Grabbing => GrabbedId != 0;
    // The Grabber is in use: an enemy in the claw or one of its throws still flying.
    public bool GrabberBusy { get { if (GrabbedId != 0) return true; foreach (var s in slimes) if (s.Tossed && !s.Launched) return true; return false; } }

    public bool Grabbable(Slime s) => s.Targetable && !s.IsBoss && !s.Type.Blocker && s.Type.Scale <= Settings.GrabberMaxScale + 1e-9
        && !s.Handled && !s.Airborne && s.LeapHeight <= 0 && !s.Shielded && !s.Faded && !s.Bubbled;

    // A press on an enemy (battle x, height over the floor) with the Grabber chosen.
    public bool Grab(int id, double x, double height)
    {
        if (Armed != BattleSkill.Grabber || !Ready(BattleSkill.Grabber) || GrabbedId != 0 || !double.IsFinite(x) || !double.IsFinite(height)) return false;
        // A flyer up in the air is yanked down at once (the Grabber is spent; 1.5 M14).
        if (Find(id) is { Fly: FlyPhase.Aloft, Type.Fly: not null } flyer && flyer.State != SlimeState.Spawning)
        {
            if (!KnockDown(flyer, true)) return false;
            Spend(BattleSkill.Grabber);
            return true;
        }
        // A wall climber up its edge is yanked off it at once (the Grabber is spent; 1.5 M15).
        if (Find(id) is { OnWall: true, Type.Cling: not null } clinger && clinger.State != SlimeState.Spawning)
        {
            if (!KnockOff(clinger, true)) return false;
            Spend(BattleSkill.Grabber);
            return true;
        }
        if (Find(id) is not Slime slime || !Grabbable(slime)) return false;
        // A burrower is pulled up from under the floor (1.5 M14).
        PullUp(slime);
        // A mirage's copy vanishes in the claw (the Grabber is spent; 1.5 M13).
        if (slime.Decoy) { Dispel(slime); Spend(BattleSkill.Grabber); return true; }
        if (slime.Riding) Fall(slime, 0);
        foreach (var s in slimes)
            if (s.Riding && s.LinkedTo == slime.Id) Fall(s, 0);
        // Its windup, heal or dash is lost; it hangs still.
        if (slime.State is not (SlimeState.Moving or SlimeState.Stunned)) slime.Set(SlimeState.Moving);
        if (slime.MendingId != 0 && Find(slime.MendingId) is Slime mended) mended.Mended = false;
        slime.MendingId = 0;
        slime.KnockVelocity = 0; slime.Blown = 0; slime.Lured = false;
        slime.Grabbed = true;
        GrabbedId = slime.Id;
        Armed = null;
        grabX = x; grabHeight = Math.Max(0, height);
        gripX = slime.X - x; gripY = slime.Lift - grabHeight;
        grabHeld = 0; grabVX = grabVY = 0;
        if (swingTargetId == slime.Id) CancelSwing();
        events.Add(new(BattleEventKind.Grabbed, slime.X, slime.Id));
        return true;
    }

    public void MoveGrab(double x, double height)
    {
        if (GrabbedId == 0 || Paused || !double.IsFinite(x) || !double.IsFinite(height)) return;
        grabX = x; grabHeight = Math.Max(0, height);
    }

    // The mouse let go: thrown with the cursor's speed (or just dropped when slow).
    public void ReleaseGrab() => LetGo(true);

    private void LetGo(bool fling)
    {
        if (GrabbedId == 0) return;
        var slime = Find(GrabbedId);
        GrabbedId = 0;
        cooldowns[(int)BattleSkill.Grabber] = SkillCooldown(BattleSkill.Grabber);
        if (slime == null) return;
        slime.Grabbed = false;
        if (!slime.Targetable) { slime.Lift = 0; return; }
        double vx = fling ? Math.Clamp(grabVX, -GrabTopSpeed, GrabTopSpeed) : 0, vy = fling ? Math.Clamp(grabVY, -GrabTopSpeed, GrabTopSpeed) : 0;
        slime.Thrown = Math.Sqrt(vx * vx + vy * vy) >= Settings.GrabberThrowSpeed;
        // Too slow: just dropped. Thrown: always a little hop, so one flung along the floor flies an arc.
        if (!slime.Thrown) { vx = 0; vy = Math.Min(0, vy); }
        else vy = Math.Max(vy, ThrowHop);
        slime.Tossed = true;
        slime.TossVX = vx; slime.TossVY = vy;
        events.Add(new(BattleEventKind.GrabReleased, slime.X, slime.Thrown ? 1 : 0));
    }

    // Every step that goes on: the held enemy follows the cursor, the tossed ones fly.
    private void StepGrabber(double dt, bool catHeld)
    {
        if (GrabbedId != 0)
        {
            var slime = Find(GrabbedId);
            if (slime is not { Targetable: true }) { LetGo(false); }
            else if (catHeld) LetGo(false);
            else
            {
                var (minX, maxX) = Bounds(slime);
                double x = Math.Clamp(grabX + gripX, minX, maxX), lift = Math.Clamp(grabHeight + gripY, 0, GrabMaxLift);
                // The cursor's speed over the last few frames (a throw is the last flick, not one frame).
                double k = Math.Min(1, dt / 0.06);
                grabVX += ((x - slime.X) / dt - grabVX) * k;
                grabVY += ((lift - slime.Lift) / dt - grabVY) * k;
                slime.X = x; slime.Lift = lift;
                slime.KnockVelocity = 0;
                grabHeld += dt;
                if (grabHeld >= Settings.GrabberHoldSeconds) LetGo(false);
            }
        }
        int count = slimes.Count;
        for (int i = 0; i < count && !Over; i++)
            if (slimes[i].Tossed) Fly(slimes[i], dt);
    }

    private void Fly(Slime slime, double dt)
    {
        if (!slime.Targetable) { slime.Tossed = slime.Thrown = slime.Launched = false; slime.Lift = 0; return; }
        var (minX, maxX) = Bounds(slime);
        slime.TossVY -= Gravity * dt;
        slime.X += slime.TossVX * dt;
        slime.Lift += slime.TossVY * dt;
        if (slime.X <= minX || slime.X >= maxX)
        {
            slime.X = Math.Clamp(slime.X, minX, maxX);
            if (slime.Thrown && Math.Abs(slime.TossVX) >= Settings.GrabberThrowSpeed * 0.5) GrabHit(slime, null);
            slime.TossVX = 0;
        }
        else if (slime.Thrown && slime.TossVX != 0 && Into(slime) is Slime other) GrabHit(slime, other);
        if (slime.Lift > 0 || !slime.Targetable) return;
        slime.Lift = 0;
        slime.Tossed = false;
        // A thrown bomb slime blows up where it lands (1.5 M11).
        if (slime.Thrown && slime.Type.Fuse != null) { slime.Thrown = false; Blast(slime, true); return; }
        if (slime.Thrown) Daze(slime, Settings.GrabberStun);
        if (slime.Launched) { slime.Launched = false; if (arms.LaunchStun > 0) Daze(slime, arms.LaunchStun); }
        slime.Thrown = false;
    }

    // The first enemy a thrown one flies into (bodies overlapping, the way it goes).
    private Slime? Into(Slime thrown)
    {
        int way = Math.Sign(thrown.TossVX);
        foreach (var o in slimes)
        {
            if (o == thrown || !o.Targetable || o.Handled || o.Riding) continue;
            double ahead = (o.X - thrown.X) * way;
            double bottom = o.Altitude + o.LeapHeight, top = bottom + o.Height;
            if (ahead > -o.HitWidth / 4 && ahead <= (o.HitWidth + thrown.HitWidth) / 2 * 0.8 && thrown.Lift < top && thrown.Lift + thrown.Height > bottom) return o;
        }
        return null;
    }

    private void GrabHit(Slime thrown, Slime? other)
    {
        GrabberHits++;
        double x = other != null ? (thrown.X + other.X) / 2 : thrown.X + Math.Sign(thrown.TossVX) * thrown.Width / 2;
        AddEffect(BattleEffectKind.Combo, x, thrown.Lift + thrown.Height / 2);
        events.Add(new(BattleEventKind.GrabHit, x, thrown.Id));
        thrown.TossVX = 0;
        if (other != null)
        {
            ToolHit(other, Settings.GrabberHitDamage);
            if (other.Targetable) Daze(other, Settings.GrabberStun);
        }
        // A thrown bomb slime blows up where it hits (1.5 M11).
        if (thrown.Type.Fuse != null && thrown.Targetable) { thrown.Tossed = thrown.Thrown = false; thrown.Lift = 0; Blast(thrown, true); return; }
        ToolHit(thrown, Settings.GrabberHitDamage);
    }

    private void MapGrab(double left, double oldWidth, double newWidth)
    {
        if (GrabbedId != 0) grabX = left + (grabX - Left) / oldWidth * newWidth;
    }

    private void ClearGrabber()
    {
        GrabbedId = 0;
        foreach (var s in slimes) { s.Grabbed = s.Tossed = s.Thrown = s.Launched = false; s.Lift = 0; s.TossVX = s.TossVY = 0; }
    }
}
