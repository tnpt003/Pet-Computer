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
    public double LandingSquash { get; private set; }
    public int Facing { get; private set; } = 1;
    public double FacingScale => Facing * (turnTime < TurnDuration ? 1 - 0.14 * Math.Sin(Math.PI * turnTime / TurnDuration) : 1);
    private const double TurnDuration = 0.18;
    private double turnTime = TurnDuration, lagVelocity, landingTime = 1, landingStrength;
    private int targetFacing = 1;
    private static double Ease(double current, double target, double rate, double dt) => current + (target - current) * (1 - Math.Exp(-rate * dt));
    private static double RestTarget(PetModel pet) => pet.Action is PetAction.Sitting or PetAction.Sleeping or PetAction.Grooming || pet.Emotions.SleepLocked ? 1 : 0;
    public void Reset(PetModel pet)
    {
        Ready = true; WalkPhase = WalkBlend = HeadTilt = TailLag = LandingSquash = lagVelocity = 0;
        RestBlend = RestTarget(pet); Facing = targetFacing = pet.Facing < 0 ? -1 : 1;
        turnTime = TurnDuration; landingTime = 1; landingStrength = 0;
    }
    public void Contact(double speed)
    {
        if (!double.IsFinite(speed) || speed <= 40) return;
        landingTime = 0; landingStrength = Math.Clamp(speed / 700, 0.12, 1);
    }
    public void Step(PetModel pet, double dt, double walkSpeed = 72)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, 0.1);
        if (!Ready) Reset(pet);
        if (pet.Action == PetAction.Dragging) return; // Pickup eases this frozen pose away.
        bool sleeping = pet.Emotions.SleepLocked || pet.Action == PetAction.Sleeping;
        bool walking = pet.Action == PetAction.Walking && !pet.Emotions.BlocksAutonomy && pet.Grounded;
        double speed = walking ? Math.Abs(pet.VX) : 0;
        double ratio = Math.Clamp(speed / Math.Max(1, walkSpeed), 0, 1.3);
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
        landingTime += dt;
        LandingSquash = !sleeping && landingTime < 0.22 ? landingStrength * Math.Sin(Math.PI * landingTime / 0.22) : 0;
        if (sleeping) { WalkBlend = 0; HeadTilt = TailLag = lagVelocity = 0; landingStrength = 0; }
    }
}
