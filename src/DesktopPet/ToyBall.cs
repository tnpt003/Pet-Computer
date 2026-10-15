using System;

namespace DesktopPet;

// Center coordinates in desktop DIPs; height is measured above the floor.
public sealed class ToyBall
{
    public double X, Height, VX, VY, Rotation, Feedback, BatCooldown;
    public void Push(double clickOffset, double variation = 1)
    {
        double offset = Math.Clamp(clickOffset / 24, -1, 1);
        VX = -offset * 250 * Math.Clamp(variation, 0.9, 1.1);
        VY = 160 + (1 - Math.Abs(offset)) * 95;
        Feedback = 0.35;
        BatCooldown = 0.35;
    }
    public void Bat(int direction, double variation = 1)
    {
        VX = direction * 180 * Math.Clamp(variation, 0.9, 1.1);
        VY = 125; Feedback = 0.35; BatCooldown = 0.65;
    }
    public void Step(double dt, double left, double right)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        Feedback = Math.Max(0, Feedback - dt); BatCooldown = Math.Max(0, BatCooldown - dt);
        int steps = Math.Max(1, (int)Math.Ceiling(Math.Min(dt, 0.1) * 120));
        double step = Math.Min(dt, 0.1) / steps;
        for (int i = 0; i < steps; i++)
        {
            X += VX * step; Rotation += VX * step / 11 * 180 / Math.PI;
            VY -= 850 * step; Height += VY * step;
            if (Height < 0) { Height = 0; VY = Math.Abs(VY) > 45 ? -VY * 0.48 : 0; }
            if (X < left || X > right) { X = Math.Clamp(X, left, Math.Max(left, right)); VX *= -0.7; Feedback = 0.2; }
            VX *= Math.Exp(-(Height <= 0 ? 1.25 : 0.25) * step);
            if (Math.Abs(VX) < 2) VX = 0;
        }
    }
}

public enum ToyPlayPhase { Observe, Crouch, Chase, Recover }
public sealed class ToyPlaySequence
{
    public ToyPlayPhase Phase { get; private set; }
    public double Elapsed { get; private set; }
    public void Reset() { Phase = ToyPlayPhase.Observe; Elapsed = 0; }
    public double CrouchBlend => Phase == ToyPlayPhase.Crouch ? Math.Clamp(Elapsed / 0.25, 0, 1) : 0;
    public bool Step(double dt, bool withinReach, string personality)
    {
        if (!double.IsFinite(dt) || dt <= 0) return false;
        Elapsed += dt;
        double observe = personality == "Curious" ? 1.4 : personality == "Playful" ? 0.4 : 0.8;
        double prepare = personality == "Playful" ? 0.35 : 0.55;
        double recover = personality == "Calm" ? 1.8 : personality == "Playful" ? 0.7 : 1.1;
        var next = Phase;
        bool bat = false;
        if (Phase == ToyPlayPhase.Observe && Elapsed >= observe) next = ToyPlayPhase.Crouch;
        else if (Phase == ToyPlayPhase.Crouch && Elapsed >= prepare) next = ToyPlayPhase.Chase;
        else if (Phase == ToyPlayPhase.Chase && withinReach) { next = ToyPlayPhase.Recover; bat = true; }
        else if (Phase == ToyPlayPhase.Chase && Elapsed >= 6) next = ToyPlayPhase.Observe;
        else if (Phase == ToyPlayPhase.Recover && Elapsed >= recover) next = ToyPlayPhase.Observe;
        if (next != Phase) { Phase = next; Elapsed = 0; }
        return bat;
    }
}

public enum LaserPhase { Watch, Chase, Stalk, Pounce, Recover }

// Laser-pointer game rhythm (presentation and pacing only; the window moves the pet).
// Watch the dot, chase it along the floor, stalk when it rests nearby, then pounce.
public sealed class LaserChase
{
    public const double StillSpeed = 60, StillBeforeStalk = 0.3, StalkReach = 70, EscapeReach = 110;
    public LaserPhase Phase { get; private set; }
    public double Elapsed { get; private set; }
    private double still;
    public void Reset() { Phase = LaserPhase.Watch; Elapsed = 0; still = 0; }
    public double CrouchBlend => Phase == LaserPhase.Stalk ? Math.Clamp(Elapsed / 0.25, 0, 1) : 0;

    // dx: dot X minus the cat's center; reachable: the dot is on this monitor and low enough
    // to pounce at. Returns true on the frame the cat should pounce.
    public bool Step(double dt, double dx, bool reachable, double dotSpeed, string personality)
    {
        if (!double.IsFinite(dt) || dt <= 0 || Phase == LaserPhase.Pounce) return false;
        Elapsed += dt;
        still = double.IsFinite(dotSpeed) && dotSpeed < StillSpeed ? still + dt : 0;
        double observe = personality == "Curious" ? 1.2 : personality == "Playful" ? 0.3 : 0.6;
        double prepare = personality == "Playful" ? 0.35 : 0.55;
        double recover = personality == "Calm" ? 1.6 : personality == "Playful" ? 0.6 : 1.0;
        var next = Phase;
        bool pounce = false;
        if (!reachable) next = LaserPhase.Watch;
        else if (Phase == LaserPhase.Watch && Elapsed >= observe) next = LaserPhase.Chase;
        else if (Phase == LaserPhase.Chase && Math.Abs(dx) < StalkReach && still >= StillBeforeStalk) next = LaserPhase.Stalk;
        else if (Phase == LaserPhase.Stalk && (Math.Abs(dx) > EscapeReach || still == 0)) next = LaserPhase.Chase;
        else if (Phase == LaserPhase.Stalk && Elapsed >= prepare) { next = LaserPhase.Pounce; pounce = true; }
        else if (Phase == LaserPhase.Recover && Elapsed >= recover) next = LaserPhase.Chase;
        if (next != Phase) { Phase = next; Elapsed = 0; }
        return pounce;
    }

    // The window calls this once the pounce has landed.
    public void Landed() { if (Phase == LaserPhase.Pounce) { Phase = LaserPhase.Recover; Elapsed = 0; } }
}
