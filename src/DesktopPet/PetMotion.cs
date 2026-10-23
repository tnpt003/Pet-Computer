using System;
namespace DesktopPet;

// Presentation-only history. Never writes action, position, velocity or input.
public sealed class PetMotion
{
    public bool Ready { get; private set; }
    public double WalkPhase { get; private set; }
    public double WalkBlend { get; private set; }
    public double RestBlend { get; private set; }
    public double HeadTilt { get; private set; }
    public double TailLag { get; private set; }
    // The landing's squash (+ flattened, − the rebound's stretch) and wobble: the springs the keepsake
    // uses too (PetImpact.cs); the renderer scales them down for the cat. Landing: its dust and sound.
    public double LandingSquash => spring.Squash;
    public double LandingTilt => spring.Tilt;
    public Landing Landing { get; } = new();
    private readonly ImpactSpring spring = new();
    public int Facing { get; private set; } = 1;
    public double FacingScale => Facing * (turnTime < TurnDuration ? 1 - 0.14 * Math.Sin(Math.PI * turnTime / TurnDuration) : 1);
    private const double TurnDuration = 0.18;
    private double turnTime = TurnDuration, lagVelocity;
    private int targetFacing = 1;
    private static double Ease(double current, double target, double rate, double dt) => current + (target - current) * (1 - Math.Exp(-rate * dt));
    private static double RestTarget(PetModel pet) => pet.Action is PetAction.Sitting or PetAction.Sleeping or PetAction.Grooming or PetAction.Kneading || pet.Emotions.SleepLocked ? 1 : 0;
    public void Reset(PetModel pet)
    {
        Ready = true; WalkPhase = WalkBlend = HeadTilt = TailLag = lagVelocity = 0;
        spring.Reset(); Landing.Clear();
        RestBlend = RestTarget(pet); Facing = targetFacing = pet.Facing < 0 ? -1 : 1;
        turnTime = TurnDuration;
    }
    // Touching down at `speed` DIP/s (the physics' observation; nothing here changes the bounce).
    // With the pet: its sideways speed tips the wobble, the dust stays where it landed, and a
    // sleeping pet lands softly (no squash, dust or sound).
    public void Contact(double speed, PetModel? pet = null)
    {
        if (!double.IsFinite(speed) || speed <= 40) return;
        if (pet != null && (pet.Action == PetAction.Sleeping || pet.Emotions.SleepLocked)) return;
        spring.Land(speed, pet?.VX ?? 0);
        Landing.Hit(speed, pet?.X ?? 0, pet?.Y ?? 0);
    }
    // Bouncing off a screen side mid-air (vx: after the bounce).
    public void Wall(double vx) { if (double.IsFinite(vx) && Math.Abs(vx) > 60) spring.Wall(vx); }
    public void Step(PetModel pet, double dt, double walkSpeed = 72)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, 0.1);
        if (!Ready) Reset(pet);
        Landing.Step(dt);
        bool sleeping = pet.Emotions.SleepLocked || pet.Action == PetAction.Sleeping;
        // Held or asleep: no squash left over (the pickup has its own pose).
        if (pet.Action == PetAction.Dragging || sleeping) spring.Reset();
        else
        {
            int steps = (int)Math.Ceiling(dt * 120);
            for (int i = 0; i < steps; i++) spring.Step(dt / steps);
            spring.Settle();
        }
        if (pet.Action == PetAction.Dragging) return; // Pickup eases this frozen pose away.
        bool walking = pet.Action == PetAction.Walking && !pet.Emotions.BlocksAutonomy && pet.Grounded;
        double speed = walking ? Math.Abs(pet.VX) : 0;
        // Up to 2x: Battle Mode's run (normal walking never exceeds 1).
        double ratio = Math.Clamp(speed / Math.Max(1, walkSpeed), 0, 2);
        WalkPhase = (WalkPhase + ratio * 13 * dt) % (Math.PI * 2);
        WalkBlend = Ease(WalkBlend, Math.Min(1, ratio), 15, dt);
        RestBlend = Ease(RestBlend, RestTarget(pet), pet.Action == PetAction.Waking ? 4 : 9, dt);
        double velocity = sleeping || pet.Action == PetAction.Waking ? 0 : Math.Clamp(pet.VX, -144, 144);
        lagVelocity = Ease(lagVelocity, velocity, 5, dt);
        double lag = (lagVelocity - velocity) / Math.Max(1, walkSpeed);
        HeadTilt = Ease(HeadTilt, Math.Clamp(lag * 5, -5, 5), 12, dt);
        TailLag = Ease(TailLag, Math.Clamp(lag * 8, -8, 8), 6, dt);
        int desired = walking && Math.Abs(pet.VX) > 8 ? Math.Sign(pet.VX) : pet.Facing < 0 ? -1 : 1;
        if (desired != targetFacing) { targetFacing = desired; turnTime = 0; }
        turnTime = Math.Min(TurnDuration, turnTime + dt);
        if (turnTime >= TurnDuration / 2) Facing = targetFacing;
        if (sleeping) { WalkBlend = 0; HeadTilt = TailLag = lagVelocity = 0; }
    }
}
