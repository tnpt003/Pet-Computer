using System;

namespace DesktopPet;

public enum PetAction { Idle, Walking, Jumping, Sitting, Sleeping, Looking, Dragging, Thrown, Waking, SeekingAttention, Grooming, Stretching, PreparingJump,
    Kneading, RollingOver, ChasingTail, Scratching, Sneezing, Climbing }

public static class PetActionRules
{
    // Actions that own their whole duration: AI commands/hints cannot replace them.
    // Keep in sync with BUSY_STATES in ai/service.py.
    public static bool RejectsDecisions(PetAction action) => action is PetAction.Dragging or PetAction.Thrown or
        PetAction.Jumping or PetAction.PreparingJump or PetAction.Sleeping or PetAction.Waking or
        PetAction.Grooming or PetAction.Stretching or PetAction.SeekingAttention or PetAction.Climbing || IsGesture(action);

    // Local poses that direct input or feature changes cancel straight back to Idle.
    public static bool IsCancellableLocal(PetAction action) => action is PetAction.Grooming or PetAction.Stretching or PetAction.PreparingJump || IsGesture(action);

    // Short stationary poses that own a fixed duration (PetSettings.GestureDuration).
    public static bool IsGesture(PetAction action) => action is PetAction.Kneading or PetAction.RollingOver or
        PetAction.ChasingTail or PetAction.Scratching or PetAction.Sneezing;

    // In the air or about to be: a throw, a jump or its crouch.
    public static bool IsInFlight(PetAction action) => action is PetAction.Thrown or PetAction.Jumping or PetAction.PreparingJump;

    // Sleep, waking, input and flight own the body: nothing new starts over them.
    public static bool HoldsBody(PetAction action) => action is PetAction.Sleeping or PetAction.Waking or PetAction.Dragging || IsInFlight(action);

    // Standing on the floor or a window, awake and free of input and flight: the shared
    // precondition for play, "Do now", speech and thought bubbles.
    public static bool IsSettled(PetModel pet) => pet.Grounded && !pet.Emotions.SleepLocked && !HoldsBody(pet.Action);
}

public readonly record struct Area(double Left, double Top, double Right, double Bottom);

// The visible part of an app window's top edge (DIPs on the pet's monitor). Other windows
// can split one edge into several ledges with the same Id. ClimbLeft/ClimbRight: that
// side of the window reaches the floor and is uncovered, so the pet can climb it.
public readonly record struct Ledge(long Id, double Top, double Left, double Right,
    double WindowLeft, double WindowRight, double WindowBottom, bool ClimbLeft = false, bool ClimbRight = false)
{
    public Ledge Shifted(double dx, double dy) => this with
    {
        Top = Top + dy, Left = Left + dx, Right = Right + dx,
        WindowLeft = WindowLeft + dx, WindowRight = WindowRight + dx, WindowBottom = WindowBottom + dy
    };
}

public sealed class PetSettings
{
    // Applies to inactivity, local random sleep, and Python SLEEP commands.
    public double SleepDurationSeconds { get; init; } = 10;
    public double WakeCooldownSeconds { get; init; } = 30;
    public double WalkAcceleration { get; init; } = 220;
    public double DragThreshold { get; init; } = 6;
    public const double WakingDuration = 1.2;
    public const double JumpPreparationDuration = 0.16;
    public const double GroomingDuration = 3.6;
    public const double StretchingDuration = 2.4;
    public const double SelfCareCooldown = 45;
    public const double AttentionGestureDuration = 2.4;
    public const double AttentionCooldown = 45;
    public const double KneadingDuration = 3.2;
    public const double RollOverDuration = 3.5;
    public const double TailChaseDuration = 3;
    public const double ScratchingDuration = 2.4;
    public const double SneezeDuration = 1;
    public static double GestureDuration(PetAction action) => action switch
    {
        PetAction.Kneading => KneadingDuration,
        PetAction.RollingOver => RollOverDuration,
        PetAction.ChasingTail => TailChaseDuration,
        PetAction.Scratching => ScratchingDuration,
        PetAction.Sneezing => SneezeDuration,
        _ => 0
    };
    public double Width { get; init; } = 112;
    public double Height { get; init; } = 112;
    public double Gravity { get; init; } = 1500;
    public double Bounce { get; init; } = 0.58;
    public double WalkSpeed { get; init; } = 72;
    public double JumpSpeed { get; init; } = 580;
    public double MaxThrowSpeed { get; init; } = 2200;
    public double ClimbSpeed { get; init; } = 110;
    // The pet's centre must stay this far inside a ledge to stand on it.
    public const double LedgeMargin = 8;
    // Faster landings on a ledge bounce (the floor keeps its own 130 threshold).
    public const double LedgeBounceSpeed = 400;
    // While climbing, the pet's centre is this far outside the window's side.
    public const double ClimbOffset = 30;
    // Where the drawn cat sits inside its 112×112 frame (DIPs from the frame's top-left),
    // for hit tests and anchors outside the renderer.
    public const double CenterX = 56;
    public const double HeadLeft = 24, HeadRight = 88, HeadTop = 16;
    public const double BodyLeft = 18, BodyRight = 94, BodyTop = 24;
    public const double ChestY = 50;
    public const double SpeechAnchorY = 12;
}

public sealed class PetModel
{
    public double X, Y, VX, VY;
    public double SmoothedWalkVelocity;
    public double TreatRemaining;
    public double BatRemaining;
    public double PlayCrouch;
    // Battle Mode, presentation only (the window copies them from BattleSim each frame):
    // the wooden sword, the swing's progress (0..1, negative when not swinging), a short
    // flinch after a hit, and the end-of-battle pose.
    public bool SwordOut;
    // The battle's weapon is the hammer (drawn instead of the sword, same poses).
    public bool Hammer;
    public double SwordSwing = -1;
    // Where in the swing the weapon's hit lands (0..1, WeaponSpec.HitFraction): the slash or
    // slam is drawn reaching its target at that moment. 0.44 is the sword's.
    public double SwordHitAt = 0.44;
    // The swing under way finishes a combo (a gold slash).
    public bool SwordCombo;
    public double BattleHurt;
    public bool BattleDazed, BattleCheer;
    // Seconds of a battle stun still to go (stars over the head; 0: not stunned).
    public double BattleStun;
    // Seconds of the bonk from a keepsake dropped on the head still to go (stars, dazed eyes).
    public double Bonk;
    public const double BonkDuration = 2.2;
    // The user's skills on the cat: a protective bubble (Shield) and a golden glow (Cheer).
    public bool BattleShield, BattleBuff;
    // The battle's clock for those effects (stops while the battle is paused).
    public double BattleTime;
    // Battle Mode runs: walking speed and acceleration multiplier (1 outside a battle).
    public double MoveScale = 1;
    public const double TreatDuration = 4.2;
    public int Facing = 1;
    public bool Grounded;
    // The window the pet stands on (0: the screen floor) and, while Climbing, the window
    // and side (+1 right edge, -1 left edge) it climbs.
    public long LedgeId, ClimbId;
    public int ClimbSide;
    private PetAction action = PetAction.Idle;
    public PetAction Action
    {
        get => action;
        set { if (action != value) { action = value; ActionTime = 0; } }
    }
    public double ActionTime;
    public double AnimationTime;
    public PickupAnimation Pickup { get; } = new();
    public PetMotion Motion { get; } = new();
    public PetEmotions Emotions { get; } = new();
    public PetActivity Activity { get; } = new();
    public PetInteractions Interactions { get; } = new();
    public PetThought Thought { get; } = new();
    public PetSpeech Speech { get; } = new();
}

// Cumulative per-session counts of how the user treated the pet. Python turns the
// differences into needs and bond; counting never changes behavior here.
public sealed class PetInteractions
{
    public long Petted { get; private set; }
    public long Clicked { get; private set; }
    public long Treats { get; private set; }
    public long Toy { get; private set; }
    public long BallClicks { get; private set; }
    public long BallHits { get; private set; }
    public long RoughThrows { get; private set; }
    public void Record(string kind)
    {
        switch (kind)
        {
            case "petted": Petted++; break;
            case "left_clicked": Clicked++; break;
            case "treat_consumed": Treats++; break;
            case "toy_started": Toy++; break;
            case "ball_clicked": BallClicks++; break;
        }
    }
    public void BallHit() => BallHits++;
    public void RoughThrow() => RoughThrows++;
    public PetInteractions Snapshot() => (PetInteractions)MemberwiseClone();
}

// Observation only: Python owns energy and policy. Totals survive UI resets so
// delayed decisions and AI restarts never mistake a reset for negative activity.
public sealed class PetActivity
{
    public double Walking { get; private set; }
    public double Playing { get; private set; }
    public double Sleeping { get; private set; }
    public double Resting { get; private set; }
    public double Treats { get; private set; }
    public void GiveTreat() => Treats++;
    public PetActivity Snapshot() => new() { Walking = Walking, Playing = Playing, Sleeping = Sleeping, Resting = Resting, Treats = Treats };
    public void Step(PetAction action, double dt)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        switch (action)
        {
            case PetAction.Walking: Walking += dt; break;
            case PetAction.Jumping:
            case PetAction.ChasingTail:
            case PetAction.Scratching: Playing += dt; break;
            case PetAction.Sleeping: Sleeping += dt; break;
            case PetAction.Idle:
            case PetAction.Sitting:
            case PetAction.PreparingJump:
            case PetAction.Grooming:
            case PetAction.Stretching:
            case PetAction.SeekingAttention:
            case PetAction.Looking:
            case PetAction.Kneading:
            case PetAction.RollingOver:
            case PetAction.Sneezing:
            case PetAction.Waking: Resting += dt; break;
            // User dragging/throwing never counts as autonomous exertion.
        }
    }
}

public sealed class PetPhysics(PetSettings settings)
{
    public void Clamp(PetModel pet, Area bounds)
    {
        pet.X = Math.Clamp(pet.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
        pet.Y = Math.Clamp(pet.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - settings.Height));
    }

    // Ledges are window edges the pet may stand on; mayLeaveLedge lets a walk (ball, laser,
    // resting spot) carry it past a ledge's end, where it drops. Without ledges this is the
    // original floor-only physics.
    public void Step(PetModel pet, Area bounds, double dt, System.Collections.Generic.IReadOnlyList<Ledge>? ledges = null, bool mayLeaveLedge = false)
    {
        if (pet.Action == PetAction.Dragging) return;
        Ledge? standing = null;
        if (pet.LedgeId != 0)
        {
            standing = pet.Grounded ? PetLedges.Support(ledges, pet.LedgeId, pet.X + settings.Width / 2) : null;
            if (standing == null) LeaveLedge(pet); // Window closed, moved away, covered, or the pet walked off.
        }
        if (pet.Action == PetAction.Climbing) { StepClimb(pet, ledges, dt); return; }
        if (pet.Grounded && pet.Action == PetAction.Walking)
        {
            double leftEdge = bounds.Left, rightEdge = Math.Max(bounds.Left, bounds.Right - settings.Width);
            if (standing is Ledge ledge && !mayLeaveLedge)
            {
                // Turn at a ledge's ends like at the screen's edges.
                leftEdge = Math.Max(leftEdge, ledge.Left + PetSettings.LedgeMargin - settings.Width / 2);
                rightEdge = Math.Max(leftEdge, Math.Min(rightEdge, ledge.Right - PetSettings.LedgeMargin - settings.Width / 2));
            }
            int direction = pet.Facing < 0 ? -1 : 1;
            double distance = direction > 0 ? rightEdge - pet.X : pet.X - leftEdge;
            if (distance <= 4)
            {
                direction = -direction;
                pet.SmoothedWalkVelocity = 0;
                distance = direction > 0 ? rightEdge - pet.X : pet.X - leftEdge;
            }
            pet.Facing = direction;
            double acceleration = settings.WalkAcceleration * pet.MoveScale;
            double target = direction * Math.Min(settings.WalkSpeed * pet.Emotions.WalkScale * pet.MoveScale,
                Math.Sqrt(2 * acceleration * Math.Max(0, distance - 4)));
            pet.SmoothedWalkVelocity += Math.Clamp(target - pet.SmoothedWalkVelocity, -acceleration * dt, acceleration * dt);
            pet.VX = pet.SmoothedWalkVelocity;
        }
        else pet.SmoothedWalkVelocity = 0;
        if (!pet.Grounded) pet.VY += settings.Gravity * dt;
        double previousFeet = pet.Y + settings.Height;
        pet.X += pet.VX * dt;
        pet.Y += pet.VY * dt;
        double right = Math.Max(bounds.Left, bounds.Right - settings.Width);
        double floor = Math.Max(bounds.Top, bounds.Bottom - settings.Height);
        if (pet.X < bounds.Left || pet.X > right)
        {
            pet.X = Math.Clamp(pet.X, bounds.Left, right);
            pet.VX = -pet.VX * (pet.Action == PetAction.Walking ? 1 : settings.Bounce);
            if (Math.Abs(pet.VX) > 1) pet.Facing = Math.Sign(pet.VX);
            if (!pet.Grounded) pet.Motion.Wall(pet.VX);
        }
        if (pet.Y < bounds.Top)
        {
            pet.Y = bounds.Top;
            pet.VY = Math.Abs(pet.VY) * settings.Bounce;
        }
        if (standing is Ledge onLedge)
        {
            pet.Y = onLedge.Top - settings.Height;
            pet.VY = 0;
        }
        else if (!pet.Grounded && pet.VY > 0 && PetLedges.Landing(ledges, previousFeet, pet.Y + settings.Height, pet.X + settings.Width / 2) is Ledge landing)
        {
            // Only from above: rising through a window's edge never stops the pet.
            pet.Y = landing.Top - settings.Height;
            pet.Motion.Contact(pet.VY, pet);
            if (pet.VY > PetSettings.LedgeBounceSpeed && pet.Action != PetAction.Sleeping)
            {
                pet.VY = -pet.VY * settings.Bounce;
                pet.VX *= 0.82;
            }
            else
            {
                pet.VY = 0;
                pet.Grounded = true;
                pet.LedgeId = landing.Id;
            }
        }
        else if (pet.Y >= floor)
        {
            pet.Y = floor;
            if (!pet.Grounded) pet.Motion.Contact(pet.VY, pet);
            // A sleeping pet (its window vanished) lands softly and keeps sleeping.
            if (pet.VY > 130 && pet.Action != PetAction.Sleeping)
            {
                pet.VY = -pet.VY * settings.Bounce;
                pet.VX *= 0.82;
                pet.Grounded = false;
            }
            else
            {
                pet.VY = 0;
                pet.Grounded = true;
            }
        }
        else pet.Grounded = false;

        if (pet.Grounded && pet.Action != PetAction.Walking)
        {
            pet.VX *= Math.Exp(-9 * dt);
            if (Math.Abs(pet.VX) < 3) pet.VX = 0;
        }
        if (pet.Grounded && pet.VX == 0 &&
            (pet.Action == PetAction.Thrown || pet.Action == PetAction.Jumping))
            pet.Action = PetAction.Idle;
    }

    // The ledge is gone: fall. Sleep and waking keep their pose (nothing in the
    // background may wake the pet); anything else becomes a plain fall.
    public static void LeaveLedge(PetModel pet)
    {
        pet.LedgeId = 0;
        if (!pet.Grounded) return;
        pet.Grounded = false;
        pet.VY = 0;
        if (pet.Action is not (PetAction.Sleeping or PetAction.Waking or PetAction.Thrown or PetAction.Jumping))
            pet.Action = PetAction.Jumping;
    }

    // Let go of a wall (click, right-click, Quiet...): drop straight down.
    public static void LetGo(PetModel pet)
    {
        if (pet.Action != PetAction.Climbing) return;
        pet.Action = PetAction.Jumping;
        pet.ClimbId = 0;
        pet.Grounded = false;
        pet.VX = pet.VY = 0;
    }

    // Up the window's side at a steady pace, then a small hop onto its top edge.
    private void StepClimb(PetModel pet, System.Collections.Generic.IReadOnlyList<Ledge>? ledges, double dt)
    {
        var wall = PetLedges.Wall(ledges, pet.ClimbId, pet.ClimbSide);
        if (wall is not Ledge ledge) { LetGo(pet); return; }
        pet.Grounded = false;
        pet.Facing = -pet.ClimbSide; // Facing the window.
        pet.X = (pet.ClimbSide > 0 ? ledge.WindowRight + PetSettings.ClimbOffset : ledge.WindowLeft - PetSettings.ClimbOffset) - settings.Width / 2;
        pet.VX = 0;
        pet.VY = -settings.ClimbSpeed;
        pet.Y += pet.VY * dt;
        if (pet.Y + settings.Height > ledge.Top) return;
        // Over the top: the hop lands about 40 DIPs inside the edge, softly (no bounce).
        pet.Y = ledge.Top - settings.Height;
        pet.Action = PetAction.Jumping;
        pet.ClimbId = 0;
        pet.VY = -230;
        pet.VX = pet.Facing * 230;
    }
}
