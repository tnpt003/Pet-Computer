using System;

namespace DesktopPet;

// The keepsake as a cartoon object: picked up, thrown, falls, bounces with squash and stretch,
// gets shoved by the cat. X is the art's left edge and Height the art's bottom above the floor, both
// in DIPs of the keepsake's monitor; VY is up-positive (like ToyBall). Ground: the height it stands on
// (0: the floor; a window's top edge is higher).
public sealed class KeepsakeMotion
{
    public const double Gravity = 1700, EdgeMargin = 24;
    public double X, Height, VX, VY, Ground;
    // Squash: + flattened, − stretched (a damped spring), Tilt in degrees (another spring).
    public double Squash, SquashV, Tilt, TiltV;
    public bool Held;
    // Last landing's impact speed (DIP/s), for the dust and the sound; read and cleared by the caller.
    public double Landed;
    // While held: the tilt the drag pulls toward (the dangling swing).
    public double HeldTilt;

    public bool Grounded => !Held && Height <= Ground && VY == 0;
    public bool Resting => Grounded && Math.Abs(VX) < 1 && Math.Abs(Squash) < 0.02 && Math.Abs(SquashV) < 0.2 && Math.Abs(Tilt) < 0.4 && Math.Abs(TiltV) < 2;
    // The art's scale around its bottom center: squash flattens and widens, a fall stretches it.
    public double ScaleX => Math.Clamp(1 + Squash * 0.6 - Stretch * 0.5, 0.6, 1.4);
    public double ScaleY => Math.Clamp(1 - Squash + Stretch, 0.55, 1.35);
    private double Stretch => Held || Height <= Ground ? 0 : Math.Min(Math.Abs(VY) / 2600, 0.14);

    public static bool NearEdge(double x, double width, double left, double right) =>
        x - left < EdgeMargin || right - (x + width) < EdgeMargin;

    // Dropped or thrown by the user (velocity from ThrowTracker, screen y down → VY up).
    public void Release(double vx, double vyDown)
    {
        Held = false;
        VX = vx; VY = -vyDown;
        TiltV += vx * 0.03;
    }

    // A click without a drag: a springy "boing".
    public void Boing() { SquashV += 5.5; TiltV += (TiltV >= 0 ? 1 : -1) * 40; }

    // The cat's shove: a slide with a little hop, a stretch and a wobble.
    public void Shove(int direction, double speed)
    {
        VX = direction * speed; VY = 150;
        SquashV -= 3.5; TiltV += direction * 90;
    }

    // Knocked off the cat's head: up and to one side.
    public void Rebound(int side, double sideways)
    {
        VY = 430; VX = side * sideways;
        SquashV += 4; TiltV += side * 160;
    }

    // One frame; left/right bound the art's left edge (right already minus its width).
    // Returns true when the keepsake hit the floor this step with some speed.
    public bool Step(double dt, double left, double right)
    {
        if (!double.IsFinite(dt) || dt <= 0) return false;
        bool landed = false;
        int steps = Math.Max(1, (int)Math.Ceiling(Math.Min(dt, 0.1) * 120));
        double h = Math.Min(dt, 0.1) / steps;
        for (int i = 0; i < steps; i++)
        {
            // Springs: squash back to 0, tilt back to upright (or to the dangling angle while held).
            SquashV += (-340 * Squash - 7 * SquashV) * h; Squash += SquashV * h;
            double tiltTarget = Held ? HeldTilt : 0;
            TiltV += (-180 * (Tilt - tiltTarget) - (Held ? 7 : 9) * TiltV) * h; Tilt += TiltV * h;
            Tilt = Math.Clamp(Tilt, -35, 35);
            if (Held) continue;
            X += VX * h;
            if (Height > Ground || VY > 0)
            {
                VY -= Gravity * h; Height += VY * h;
                if (Height <= Ground)
                {
                    double impact = -VY;
                    Height = Ground;
                    // Cartoon landing: a squash that grows with the fall, then one or two small bounces.
                    SquashV += Math.Clamp(impact / 1000 * 5.5, 0, 7.5);
                    VY = impact > 220 ? impact * 0.38 : 0;
                    TiltV += VX * 0.04;
                    if (impact > 120) { Landed = Math.Max(Landed, impact); landed = true; }
                }
            }
            if (X < left || X > right)
            {
                X = Math.Clamp(X, left, Math.Max(left, right));
                VX *= -0.55; SquashV -= 1.5; TiltV += VX * 0.08;
            }
            VX *= Math.Exp(-(Height <= Ground ? 4 : 0.3) * h);
            if (Math.Abs(VX) < 2 && Height <= Ground) VX = 0;
        }
        if (Math.Abs(Squash) < 0.002 && Math.Abs(SquashV) < 0.02) Squash = SquashV = 0;
        if (Math.Abs(Tilt) < 0.05 && Math.Abs(TiltV) < 0.2 && !Held) Tilt = TiltV = 0;
        return landed;
    }
}
