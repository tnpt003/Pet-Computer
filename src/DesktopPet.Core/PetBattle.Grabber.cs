using System;
using System.Linq;

namespace DesktopPet;

// 1.5 M06 (docs/BATTLE-DATA.md → Kẹp gắp), redone 2026-11-04 (docs/GOPY-C-2026-11-04.md D4, "grab – hold – slam"):
// chosen, a click on a small enemy (never a boss or a rock; a kind's scale at most GrabberMaxScale) and the
// claw lifts it out of the fight (GrabLift DIPs up over GrabRise s): it does nothing (a windup is lost), is
// no target of the cat even chosen, shoves no one, its links break and a timer's clock waits. The next
// click on the field carries it there (GrabCarrySpeed) and slams it down: it loses GrabberSlamDamage and
// is dazed GrabberStun s; every other enemy on the floor within GrabberSplash loses GrabberHitDamage; an
// ice shell on either breaks (2026-11-05; set down when the hold runs out, it stays whole). A bomb
// slime in the claw is the user's bomb: it blows up where it lands, hurting enemies only. Held
// GrabberHoldSeconds without a click, it is put down on the side away from the cat, dazed. A thief is never
// slammed: held to the end it shakes out what it stole (the skill's cooldown, the icon's charge; nothing
// stolen: a coin in a run with coins, else GrabberThiefCharge on the emptiest icon) and is gone, the cat's
// defeat. A flyer up in the air or a climber on its edge is taken too (2026-11-05, it used to be only knocked
// down): the claw goes up to it and draws it down at GrabPull to where it holds enemies, and from there it is
// like any other. The cooldown starts when it is put down. Lifting the cat lets it drop (no slam, a flyer or a
// climber still up there falls as if knocked down); Pause and the frozen guide
// freeze it; the battle ending or the field moving puts it down.
public sealed partial class BattleSim
{
    public const double Gravity = 1800, GrabTopSpeed = 1600, GrabMaxLift = 100, ThrowHop = 260;
    public const double GrabLift = 90, GrabRise = 0.35, GrabCarrySpeed = 1100, SlamSpeed = 900, GrabSplash = 75, GrabPull = 900;
    // Where the claw carries the held enemy (NaN: it hangs where it was lifted), and slams that hit.
    private double grabTo = double.NaN;
    public int GrabSlams { get; private set; }
    public int ThievesShaken { get; private set; }
    public bool GrabCarrying => GrabbedId != 0 && !double.IsNaN(grabTo);
    public double GrabHeldSeconds => grabHeld;
    // The enemy in the claw (0: none).
    public int GrabbedId { get; private set; }
    // The time held.
    private double grabHeld;
    public bool Grabbing => GrabbedId != 0;
    // The Grabber is in use: an enemy in the claw or one of its throws still flying.
    public bool GrabberBusy { get { if (GrabbedId != 0) return true; foreach (var s in slimes) if (s.Tossed && !s.Launched) return true; return false; } }

    public bool Grabbable(Slime s) => s.Targetable && !s.IsBoss && !s.Type.Blocker && s.Type.Scale <= Settings.GrabberMaxScale + 1e-9
        && !s.Handled && !s.Airborne && (s.LeapHeight <= 0 || s.RecoilLeft > 0) && !s.Shielded && !s.Faded && !s.Bubbled;

    // A press on an enemy (battle x, height over the floor) with the Grabber chosen.
    public bool Grab(int id, double x, double height)
    {
        if (Armed != BattleSkill.Grabber || !Ready(BattleSkill.Grabber) || GrabbedId != 0 || !double.IsFinite(x) || !double.IsFinite(height)) return false;
        if (Find(id) is not Slime slime) return false;
        // A flyer up in the air or a climber on its edge: taken where it is (2026-11-05).
        bool high = OutOfHand(slime) && slime.State != SlimeState.Spawning;
        if (!high && !Grabbable(slime)) return false;
        if (high) { slime.AimLeft = 0; slime.ShotsLeft = 0; slime.ClingShifting = false; slime.FallSpeed = 0; }
        // A bomb flying back to its mount is caught in the air (2026-11-04).
        if (slime.RecoilLeft > 0) { slime.RecoilLeft = 0; slime.Lift = slime.LeapHeight; slime.LeapHeight = 0; }
        // A burrower is pulled up from under the floor (1.5 M14).
        PullUp(slime);
        // A mirage's copy vanishes in the claw (the Grabber is spent; 1.5 M13).
        if (slime.Decoy) { Dispel(slime); Spend(BattleSkill.Grabber); return true; }
        if (slime.Riding) Fall(slime, 0);
        foreach (var s in slimes)
            if (s.Riding && s.LinkedTo == slime.Id) Fall(s, 0);
        // Its links break (a tether's rope, 2026-11-04).
        if (Is(slime, EnemyLinks.Tether)) CutLink(slime.Id);
        // Its windup, heal or dash is lost; it hangs still.
        if (slime.State is not (SlimeState.Moving or SlimeState.Stunned)) slime.Set(SlimeState.Moving);
        if (slime.MendingId != 0 && Find(slime.MendingId) is Slime mended) mended.Mended = false;
        slime.MendingId = 0;
        slime.KnockVelocity = 0; slime.Lured = false;
        slime.Grabbed = true;
        GrabbedId = slime.Id;
        Armed = null;
        grabTo = double.NaN;
        grabHeld = 0;
        if (swingTargetId == slime.Id) CancelSwing();
        events.Add(new(BattleEventKind.Grabbed, slime.X, slime.Id));
        SkillUsed();
        return true;
    }

    // Not on the floor yet: a flyer in the air or a climber on its edge, out of the usual hold.
    public static bool OutOfHand(Slime s) => s.Type.Fly != null && s.Fly == FlyPhase.Aloft || s.Type.Cling != null && s.OnWall;
    // In the claw and still being drawn down (the sky's canvas draws the claw while it is in the sky).
    public static bool Reeling(Slime s) => s.Grabbed && OutOfHand(s);

    // Drawn down to the hold's height, it counts as knocked down: on the floor's rules, hanging in the claw.
    private void Reeled(Slime s)
    {
        s.Lift = s.Altitude;
        s.Altitude = 0;
        if (s.Type.Fly != null)
        {
            s.Fly = FlyPhase.Downed;
            FlyersKnocked++;
            events.Add(new(BattleEventKind.FlyerKnocked, s.X, 1));
            return;
        }
        if (s.Type.Cling is not ClingSpec c) return;
        var (minX, maxX) = Bounds(s);
        s.X = Math.Clamp(s.X - s.WallSide * s.Width, minX, maxX);
        s.Cling = ClingPhase.Ground;
        s.WallSide = 0;
        s.ClingClock = c.Ground;
        ClingKnocks++;
        events.Add(new(BattleEventKind.ClingKnocked, s.X, 1));
    }

    // A click on the field while an enemy hangs in the claw: carried there and slammed down. A thief is
    // never slammed (false).
    public bool GrabDrop(double x)
    {
        if (GrabbedId == 0 || Paused || Over || !double.IsFinite(x) || !double.IsNaN(grabTo) || Find(GrabbedId) is not Slime slime) return false;
        if (slime.Type.Steal != null) { events.Add(new(BattleEventKind.GrabRefused, slime.X, slime.Id)); return false; }
        var (minX, maxX) = Bounds(slime);
        grabTo = Math.Clamp(x, minX, maxX);
        return true;
    }

    // Put down: slammed (from a click, a bomb is the user's), or set down dazed (the hold ran out).
    private void LetGo(bool slam)
    {
        if (GrabbedId == 0) return;
        var slime = Find(GrabbedId);
        GrabbedId = 0;
        grabTo = double.NaN;
        Recharge(BattleSkill.Grabber);
        if (slime == null) return;
        slime.Grabbed = false;
        if (!slime.Targetable) { slime.Lift = 0; return; }
        // Still up there (the cat lifted, say): it falls as if knocked down.
        if (OutOfHand(slime)) { if (slime.Type.Fly != null) KnockDown(slime, true); else KnockOff(slime, true); return; }
        slime.Slammed = slam;
        slime.Tossed = true;
        slime.TossVX = 0; slime.TossVY = slam ? -SlamSpeed : 0;
        events.Add(new(BattleEventKind.GrabReleased, slime.X, slam ? 1 : 0));
    }

    // The hold ran out on a thief: what it stole falls out of it and it is gone (the cat's defeat).
    private void ShakeThief(Slime thief)
    {
        GrabbedId = 0;
        grabTo = double.NaN;
        Recharge(BattleSkill.Grabber);
        thief.Grabbed = false;
        if (thief.Loot == StolenKind.None)
        {
            // Nothing stolen yet: a little something still drops out.
            if (Loot == BattleLoot.Coins) Coins++;
            else if (AppRules != null && desktop.Where(a => !a.Infected).OrderBy(a => a.Charge).FirstOrDefault() is DesktopApp app) Charge(app, Settings.GrabberThiefCharge);
        }
        ThievesShaken++;
        events.Add(new(BattleEventKind.ThiefShaken, thief.X, thief.Id));
        thief.Lift = 0;
        Defeat(thief); // GiveBack in Defeat returns what it carried.
    }

    // Every step that goes on: the held enemy follows the cursor, the tossed ones fly.
    private void StepGrabber(double dt, bool catHeld)
    {
        if (GrabbedId != 0)
        {
            var slime = Find(GrabbedId);
            if (slime is not { Targetable: true }) { LetGo(false); }
            else if (catHeld) LetGo(false);
            else if (OutOfHand(slime))
            {
                slime.KnockVelocity = 0;
                slime.Altitude = Math.Max(Math.Min(slime.Altitude, GrabLift), slime.Altitude - GrabPull * dt);
                if (slime.Altitude <= GrabLift) Reeled(slime);
            }
            else
            {
                // Up it goes; carried to the spot clicked, then slammed down.
                slime.Lift = Math.Min(GrabLift, slime.Lift + GrabLift / GrabRise * dt);
                slime.KnockVelocity = 0;
                if (!double.IsNaN(grabTo))
                {
                    double gap = grabTo - slime.X;
                    slime.X += Math.Sign(gap) * Math.Min(Math.Abs(gap), GrabCarrySpeed * dt);
                    if (Math.Abs(grabTo - slime.X) < 0.5 && slime.Lift >= GrabLift - 0.5) LetGo(true);
                }
                else
                {
                    grabHeld += dt;
                    if (grabHeld >= Settings.GrabberHoldSeconds)
                    {
                        if (slime.Type.Steal != null) ShakeThief(slime);
                        else
                        {
                            // Put down on the side away from the cat.
                            var (minX, maxX) = Bounds(slime);
                            slime.X = Math.Clamp(slime.X + (slime.X >= lastCatX ? 1 : -1) * 120, minX, maxX);
                            LetGo(false);
                        }
                    }
                }
            }
        }
        int count = slimes.Count;
        for (int i = 0; i < count && !Over; i++)
            if (slimes[i].Tossed) Fly(slimes[i], dt);
    }

    private void Fly(Slime slime, double dt)
    {
        if (!slime.Targetable) { slime.Tossed = slime.Launched = slime.Slammed = false; slime.Lift = 0; return; }
        var (minX, maxX) = Bounds(slime);
        slime.TossVY -= Gravity * dt;
        slime.X += slime.TossVX * dt;
        slime.Lift += slime.TossVY * dt;
        if (slime.X <= minX || slime.X >= maxX)
        {
            slime.X = Math.Clamp(slime.X, minX, maxX);
            slime.TossVX = 0;
        }
        if (slime.Lift > 0 || !slime.Targetable) return;
        slime.Lift = 0;
        slime.Tossed = false;
        // Slammed down by the claw (2026-11-04).
        if (slime.Slammed) { slime.Slammed = false; Slam(slime); return; }
        if (slime.Launched) { slime.Launched = false; if (arms.LaunchStun > 0) Daze(slime, arms.LaunchStun); }
        // Put down when the hold ran out: dazed on landing.
        else Daze(slime, GrabberStun);
    }

    // The claw's slam: a bomb slime is the user's bomb; anything else is hurt and dazed, and so is the floor
    // round it (the others lose the splash).
    private void Slam(Slime slime)
    {
        GrabSlams++;
        AddEffect(BattleEffectKind.Slam, slime.X, 0, GrabSplash);
        events.Add(new(BattleEventKind.GrabHit, slime.X, slime.Id));
        if (slime.Type.Fuse != null)
        {
            // In the user's hands it is the user's bomb: it hurts enemies only, shield or no shield.
            slime.LeafShield = false;
            slime.ByUser = true;
            Blast(slime, true);
            return;
        }
        foreach (var o in slimes.ToArray())
        {
            if (o == slime || !o.Targetable || o.Airborne || o.OnWall || o.Burrowed || o.Handled || Math.Abs(o.X - slime.X) > GrabSplash + o.HitWidth / 2) continue;
            if (o.Iced) BreakIce(o);
            ToolHit(o, GrabberHitDamage);
            if (Over) return;
        }
        if (slime.Iced) BreakIce(slime);
        ToolHit(slime, GrabberSlamDamage);
        if (!Over && slime.Targetable) Daze(slime, GrabberStun);
    }

    private void MapGrab(double left, double oldWidth, double newWidth)
    {
        if (!double.IsNaN(grabTo)) grabTo = left + (grabTo - Left) / oldWidth * newWidth;
    }

    private void ClearGrabber()
    {
        GrabbedId = 0;
        grabTo = double.NaN;
        foreach (var s in slimes) { s.Grabbed = s.Tossed = s.Launched = s.Slammed = false; s.Lift = 0; s.TossVX = s.TossVY = 0; }
    }
}
