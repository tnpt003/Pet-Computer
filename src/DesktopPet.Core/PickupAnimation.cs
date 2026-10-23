using System;

namespace DesktopPet;

// Presentation-only spring. Never changes the pet's position or throw velocity.
public sealed class PickupAnimation
{
    public const double AnchorX = 56;
    public const double AnchorY = 20;
    public const double Padding = 40;
    public const double MaxAngle = 26;
    // DIPs/second; separate entry/exit thresholds prevent threshold chatter.
    public const double DizzyStartSpeed = 1800;
    public const double DizzyStopSpeed = 1200;
    public const double DizzyRequiredIntensity = 0.18;
    public double PoseBlend => 1 - Math.Exp(-12 * Time);
    public double Angle { get; private set; }
    public double Dizziness { get; private set; }
    public double Time { get; private set; }
    private double lastX, lastY, velocityX, velocityY, angularVelocity, dizzyHold;
    private double movementSpeed, movementIntensity;
    private bool vigorousMovement;
    // No dizziness however the cat is shaken (Battle Mode, 2026-10-24: its stars mean a stun there).
    public bool NoDizzy { get; set; }

    public void Begin(double x, double y)
    {
        Reset();
        lastX = x;
        lastY = y;
    }

    public void Reset()
    {
        Angle = Dizziness = Time = velocityX = velocityY = angularVelocity = dizzyHold = 0;
        movementSpeed = movementIntensity = 0;
        vigorousMovement = false;
    }

    public void Step(double x, double y, double dt)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        double rawX = Math.Clamp((x - lastX) / dt, -4000, 4000);
        double rawY = Math.Clamp((y - lastY) / dt, -4000, 4000);
        lastX = x;
        lastY = y;
        dt = Math.Min(dt, 0.1);
        // Substeps keep the damped spring stable even on a late render frame.
        int steps = (int)Math.Ceiling(dt / (1.0 / 120));
        double h = dt / steps;
        for (int i = 0; i < steps; i++)
        {
            double oldVelocity = velocityX;
            double smoothing = 1 - Math.Exp(-14 * h);
            velocityX += (rawX - velocityX) * smoothing;
            velocityY += (rawY - velocityY) * smoothing;
            double targetAngle = Math.Clamp(velocityX * 0.01, -18, 18);
            double impulse = Math.Clamp((velocityX - oldVelocity) / h * 0.004, -120, 120);
            angularVelocity += (85 * (targetAngle - Angle) - 6.5 * angularVelocity + impulse) * h;
            Angle += angularVelocity * h;
            if (Math.Abs(Angle) > MaxAngle)
            {
                Angle = Math.Clamp(Angle, -MaxAngle, MaxAngle);
                angularVelocity *= 0.4;
            }
            // Smooth scalar speed independently: strong back-and-forth movement
            // must not cancel itself as it would in the directional swing velocity.
            movementSpeed += (Math.Sqrt(rawX * rawX + rawY * rawY) - movementSpeed) * smoothing;
            if (vigorousMovement)
            {
                if (movementSpeed < DizzyStopSpeed)
                {
                    vigorousMovement = false;
                    movementIntensity = 0;
                }
            }
            else
            {
                if (movementSpeed >= DizzyStartSpeed)
                    movementIntensity += h * Math.Clamp(0.5 + (movementSpeed - DizzyStartSpeed) / 600, 0.5, 2);
                else
                    movementIntensity = Math.Max(0, movementIntensity - 2 * h);
                vigorousMovement = movementIntensity >= DizzyRequiredIntensity;
            }
            if (NoDizzy) { vigorousMovement = false; movementIntensity = 0; }
            double targetDizziness = vigorousMovement ? Math.Clamp(movementSpeed / 2200, 0.65, 1) : 0;
            if (vigorousMovement) dizzyHold = 1.2;
            else dizzyHold = Math.Max(0, dizzyHold - h);
            if (targetDizziness > Dizziness)
                Dizziness += (targetDizziness - Dizziness) * (1 - Math.Exp(-6 * h));
            else if (dizzyHold == 0)
                Dizziness += (targetDizziness - Dizziness) * (1 - Math.Exp(-0.9 * h));
            Time += h;
        }
    }

    // Conservative rotated art bounds, separate from the transparent host padding.
    public Area VisualBounds(double width, double height)
    {
        double radians = Angle * Math.PI / 180;
        double cos = Math.Cos(radians), sin = Math.Sin(radians);
        double left = double.PositiveInfinity, top = double.PositiveInfinity;
        double right = double.NegativeInfinity, bottom = double.NegativeInfinity;
        double artTop = Dizziness > 0.02 ? -12 : 0;
        foreach (double x in new[] { 0.0, width })
        foreach (double y in new[] { artTop, height + PoseBlend })
        {
            double rx = AnchorX + (x - AnchorX) * cos - (y - AnchorY) * sin;
            double ry = AnchorY + (x - AnchorX) * sin + (y - AnchorY) * cos;
            left = Math.Min(left, rx); right = Math.Max(right, rx);
            top = Math.Min(top, ry); bottom = Math.Max(bottom, ry);
        }
        return new Area(left, top, right, bottom);
    }
}
