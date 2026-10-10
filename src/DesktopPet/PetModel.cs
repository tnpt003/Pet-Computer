using System;

namespace DesktopPet;

public enum PetAction { Idle, Walking, Jumping, Sitting, Sleeping, Looking, Dragging, Thrown, Waking, SeekingAttention, Grooming, Stretching, PreparingJump }

public static class PetActionRules
{
    // Actions that own their whole duration: AI commands/hints cannot replace them.
    // Keep in sync with BUSY_STATES in ai/service.py.
    public static bool RejectsDecisions(PetAction action) => action is PetAction.Dragging or PetAction.Thrown or
        PetAction.Jumping or PetAction.PreparingJump or PetAction.Sleeping or PetAction.Waking or
        PetAction.Grooming or PetAction.Stretching or PetAction.SeekingAttention;

    // Local poses that direct input or feature changes cancel straight back to Idle.
    public static bool IsCancellableLocal(PetAction action) => action is PetAction.Grooming or PetAction.Stretching or PetAction.PreparingJump;
}

public readonly record struct Area(double Left, double Top, double Right, double Bottom);

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
    public double Width { get; init; } = 112;
    public double Height { get; init; } = 112;
    public double Gravity { get; init; } = 1500;
    public double Bounce { get; init; } = 0.58;
    public double WalkSpeed { get; init; } = 72;
    public double JumpSpeed { get; init; } = 580;
    public double MaxThrowSpeed { get; init; } = 2200;
}

public sealed class PetModel
{
    public double X, Y, VX, VY;
    public double SmoothedWalkVelocity;
    public double TreatRemaining;
    public double BatRemaining;
    public double PlayCrouch;
    public const double TreatDuration = 4.2;
    public int Facing = 1;
    public bool Grounded;
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
            case PetAction.Jumping: Playing += dt; break;
            case PetAction.Sleeping: Sleeping += dt; break;
            case PetAction.Idle:
            case PetAction.Sitting:
            case PetAction.PreparingJump:
            case PetAction.Grooming:
            case PetAction.Stretching:
            case PetAction.SeekingAttention:
            case PetAction.Looking:
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

    public void Step(PetModel pet, Area bounds, double dt)
    {
        if (pet.Action == PetAction.Dragging) return;
        if (pet.Grounded && pet.Action == PetAction.Walking)
        {
            double rightEdge = Math.Max(bounds.Left, bounds.Right - settings.Width);
            int direction = pet.Facing < 0 ? -1 : 1;
            double distance = direction > 0 ? rightEdge - pet.X : pet.X - bounds.Left;
            if (distance <= 4)
            {
                direction = -direction;
                pet.SmoothedWalkVelocity = 0;
                distance = direction > 0 ? rightEdge - pet.X : pet.X - bounds.Left;
            }
            pet.Facing = direction;
            double target = direction * Math.Min(settings.WalkSpeed * pet.Emotions.WalkScale,
                Math.Sqrt(2 * settings.WalkAcceleration * Math.Max(0, distance - 4)));
            pet.SmoothedWalkVelocity += Math.Clamp(target - pet.SmoothedWalkVelocity,
                -settings.WalkAcceleration * dt, settings.WalkAcceleration * dt);
            pet.VX = pet.SmoothedWalkVelocity;
        }
        else pet.SmoothedWalkVelocity = 0;
        if (!pet.Grounded) pet.VY += settings.Gravity * dt;
        pet.X += pet.VX * dt;
        pet.Y += pet.VY * dt;
        double right = Math.Max(bounds.Left, bounds.Right - settings.Width);
        double floor = Math.Max(bounds.Top, bounds.Bottom - settings.Height);
        if (pet.X < bounds.Left || pet.X > right)
        {
            pet.X = Math.Clamp(pet.X, bounds.Left, right);
            pet.VX = -pet.VX * (pet.Action == PetAction.Walking ? 1 : settings.Bounce);
            if (Math.Abs(pet.VX) > 1) pet.Facing = Math.Sign(pet.VX);
        }
        if (pet.Y < bounds.Top)
        {
            pet.Y = bounds.Top;
            pet.VY = Math.Abs(pet.VY) * settings.Bounce;
        }
        if (pet.Y >= floor)
        {
            pet.Y = floor;
            if (!pet.Grounded) pet.Motion.Contact(pet.VY);
            if (pet.VY > 130)
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
}
