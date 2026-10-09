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
