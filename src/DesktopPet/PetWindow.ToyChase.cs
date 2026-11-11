using System;
using System.Windows;

namespace DesktopPet;

// Chasing the feather wand and the wind-up mouse (docs/TOY-PLAY.md → M4). Only while the user plays:
// the wand held (the feather swings on its string) or the mouse wound up and running. The cat watches,
// runs after it on the floor and pounces with its normal jump; a catch is the cat's own doing, the toys
// never touch it. Allowed in Quiet and the wake cooldown since the user started it; a sleeping cat sleeps on.
// All C#: Python only hears the existing "toy_started" once per round, like the laser.
public sealed partial class PetWindow
{
    private enum Chase { None, Look, Wand, Run, Pounce, Hold }
    private Chase chase;
    private DesktopThing? chaseToy;
    private int chaseWind = -1;
    private double chaseTime, chaseStill, chaseHold, chaseCooldown;
    private bool chaseBored;
    private readonly LaserChase wandRhythm = new();
    private readonly ToyChaseStreak toyChaseStreak = new();
    // The feather's plume hangs this far below its quill; the cat pounces within these reaches.
    public const double FeatherPlume = 32, FeatherPawAhead = 42, ChaseRunScale = 2.2, WandCatchReach = 50, MouseCatchReach = 45, MousePounceNear = 25, MousePounceFar = 85;

    public string ChasePhase => chase.ToString();
    public string? ChaseToyId => chaseToy?.Id;
    public bool ChaseBored => chaseBored;
    public ToyChaseStreak ToyChaseStreak => toyChaseStreak;
    public FeatherString? FeatherOf(string id) => DesktopToy(id)?.Feather;
    public WindUpMouse? WindUpOf(string id) => DesktopToy(id)?.WindUp;

    private Point WandTip(DesktopThing wand) => new(wand.Motion.X + wand.Facing * ToyWandArt.HeldTipX, ToyCentreY(wand) - ToyWandArt.HeldTipY);

    private void StepFeather(DesktopThing wand, double dt)
    {
        var tip = WandTip(wand);
        wand.Feather.Step(dt, tip.X, tip.Y, wand.Area.Bottom - FeatherPlume);
    }

    // What the cat goes for: the middle of the plume, or the mouse.
    private Point ChaseTarget(DesktopThing toy) => toy.Id == DesktopToyRules.Wand
        ? new Point(toy.Feather.X, toy.Feather.Y + FeatherPlume / 2)
        : new Point(toy.Motion.X, ToyCentreY(toy));

    private bool ChaseToyWithCat(DesktopThing toy) => ToysMayShow && toy.Window?.IsVisible == true && toy.Monitor == CurrentMonitor().DeviceName;

    // The toy the user is playing with right now, if any: the wand in hand, or a mouse wound up and not chased yet.
    private DesktopThing? ChaseLure()
    {
        if (DesktopToy(DesktopToyRules.Wand) is { Held: true } wand && wand.Feather.Ready) return wand;
        if (DesktopToy(DesktopToyRules.Mouse) is { } mouse && mouse.WindUp.Running && !mouse.Held && !mouse.Press && mouse.WindUp.Winds != chaseWind) return mouse;
        return null;
    }

    // Every frame from TickDesktopToys, before the toys move: the rest timer, and what ends a chase whatever
    // the cat is doing (a battle, a fullscreen app, the toy put away, hidden or on another monitor).
    private void GuardToyChase(double dt)
    {
        if (paused) return;
        toyChaseStreak.Step(dt, chase is Chase.Wand or Chase.Run or Chase.Pounce or Chase.Hold);
        if (chase == Chase.None) return;
        if (chaseToy is not { } toy || !desktopToys.Contains(toy) || BattleActive || !ChaseToyWithCat(toy)) StopToyChase();
    }

    private bool TryStartChase()
    {
        if (ChaseLure() is not { } lure) return false;
        var asked = PetAutonomy.BlockedBy(AutoKind.ToyChase);
        bool waiting = fetch == Fetch.Wait; // Sitting after a fetch: the new toy wins.
        if (waiting) asked &= ~AutoBlock.Feature;
        if (Blocking(asked) != AutoBlock.None || !CatOnFloor || !ChaseToyWithCat(lure)) return false;
        if (!waiting && pet.Action is not (PetAction.Idle or PetAction.Walking)) return false;
        if (waiting) StopToyFetch(drop: false);
        bool mouse = lure.Id == DesktopToyRules.Mouse;
        if (mouse) chaseWind = lure.WindUp.Winds;
        bool may = mouse ? toyChaseStreak.TryMouse() : !toyChaseStreak.Resting;
        chaseToy = lure;
        chase = !may ? Chase.Look : mouse ? Chase.Run : Chase.Wand;
        chaseTime = chaseStill = chaseHold = chaseCooldown = 0;
        chaseBored = false;
        wandRhythm.Reset();
        if (may) Interact("toy_started"); // Counts as play for Python, like the laser.
        HoldStill(PetAction.Looking, ChaseTarget(lure).X < CatCenter ? -1 : 1);
        return true;
    }

    // From TickFeatures, right after the laser (a pounce is in the air, where CanPlay is false). True while it owns the cat.
    private bool TickToyChase(double dt)
    {
        if (chase == Chase.None) return CanPlay && TryStartChase();
        var toy = chaseToy!;
        if (chase != Chase.Pounce && !CanPlay) { StopToyChase(); return false; }
        bool mouse = toy.Id == DesktopToyRules.Mouse;
        bool over = mouse ? toy.Held || toy.Press || (!toy.WindUp.Running && chase is Chase.Run or Chase.Look) : !toy.Held;
        if (over) { StopToyChase(); return false; }
        chaseTime += dt;
        var target = ChaseTarget(toy);
        double dx = target.X - CatCenter;
        int toward = dx < 0 ? -1 : 1;
        switch (chase)
        {
            case Chase.Look: // Resting: it only watches.
                HoldStill(PetAction.Looking, toward);
                return true;
            case Chase.Pounce:
                if (!pet.Grounded || pet.Action == PetAction.Jumping) return true;
                if (!mouse) wandRhythm.Landed();
                bool caught = Math.Abs(dx) < (mouse ? MouseCatchReach : WandCatchReach) && (mouse ? toy.Motion.Height < 8 : target.Y >= pet.Y - 30);
                HoldStill(PetAction.Sitting, pet.Facing);
                if (caught)
                {
                    chase = Chase.Hold;
                    chaseHold = mouse ? 1.0 : 0.8;
                    if (mouse) toy.WindUp.Stop(toy.Motion);
                    else toy.Feather.Pin(CatCenter + pet.Facing * FeatherPawAhead, pet.Y + settings.Height - FeatherPlume - 2); // By its front paws, not behind the body.
                    PlayPetSound("ball_tap");
                }
                else { chase = mouse ? Chase.Run : Chase.Wand; chaseCooldown = 0.6; }
                return true;
            case Chase.Hold: // Paws on the feather, or pinning the mouse down.
                pet.Action = PetAction.Sitting; pet.VX = pet.SmoothedWalkVelocity = 0;
                pet.PlayCrouch = mouse ? 1 : 0.6;
                if (!mouse && !toy.Feather.Pinned) chaseHold = 0; // Pulled out of its paws.
                if ((chaseHold -= dt) > 0) return true;
                pet.PlayCrouch = 0;
                toy.Feather.Pinned = false;
                if (mouse) { StopToyChase(); HoldStill(PetAction.Sitting, toward); return true; }
                chase = Chase.Wand; wandRhythm.Reset(); chaseStill = 0;
                return true;
            case Chase.Wand:
                toyChaseStreak.Wand(dt);
                if (toyChaseStreak.Resting) { pet.PlayCrouch = 0; chase = Chase.Look; return true; }
                double speed = toy.Feather.Speed;
                chaseStill = speed < 30 ? chaseStill + dt : 0;
                if (chaseStill >= ToyChaseStreak.BoredSeconds) chaseBored = true;
                else if (speed > 80) chaseBored = false;
                if (chaseBored) { pet.PlayCrouch = 0; wandRhythm.Reset(); HoldStill(PetAction.Looking, toward); return true; }
                bool reachable = target.Y >= pet.Y - LaserReachAbove;
                bool pounce = wandRhythm.Step(dt, dx, reachable, speed, preferences.Personality);
                pet.PlayCrouch = wandRhythm.CrouchBlend;
                if (Math.Abs(dx) > 4) pet.Facing = toward;
                if (pounce) { Pounce(1); return true; }
                switch (wandRhythm.Phase)
                {
                    case LaserPhase.Chase:
                        double spot = Math.Clamp(target.X - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
                        if (Math.Abs(spot - pet.X) < 12) { pet.Action = PetAction.Idle; pet.VX = pet.SmoothedWalkVelocity = 0; }
                        else WalkToward(spot);
                        break;
                    case LaserPhase.Stalk:
                        pet.Action = PetAction.Sitting; pet.VX = pet.SmoothedWalkVelocity = 0;
                        break;
                    default:
                        pet.Action = wandRhythm.Phase == LaserPhase.Watch ? PetAction.Looking : PetAction.Sitting;
                        pet.VX = pet.SmoothedWalkVelocity = 0;
                        break;
                }
                return true;
            case Chase.Run:
                if (chaseTime > ToyChaseStreak.MouseWalkSeconds) { StopToyChase(); return false; }
                chaseCooldown = Math.Max(0, chaseCooldown - dt);
                if (chaseCooldown <= 0 && Math.Abs(dx) > MousePounceNear && Math.Abs(dx) < MousePounceFar && toy.Motion.Height <= 0 && pet.Grounded)
                {
                    pet.Facing = toward;
                    Pounce(ChaseRunScale);
                    return true;
                }
                WalkToward(Math.Clamp(target.X - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width)));
                pet.VX *= ChaseRunScale; // A mouse is chased at a run.
                return true;
        }
        return false;
    }

    // The same impulse as a normal jump, a little faster across for the mouse.
    private void Pounce(double across)
    {
        pet.PlayCrouch = 0;
        pet.Action = PetAction.Jumping; pet.Grounded = false;
        pet.VY = -settings.JumpSpeed; pet.VX = pet.Facing * settings.WalkSpeed * across;
        chase = Chase.Pounce;
    }

    private void StopToyChase()
    {
        if (chase == Chase.None) return;
        if (chaseToy != null) chaseToy.Feather.Pinned = false;
        pet.PlayCrouch = 0;
        if (pet.Action == PetAction.Walking && !dragging) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
        chase = Chase.None;
        chaseToy = null;
        chaseTime = chaseStill = chaseHold = chaseCooldown = 0;
        chaseBored = false;
    }
}
