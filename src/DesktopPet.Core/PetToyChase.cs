using System;
using System.Collections.Generic;

namespace DesktopPet;

// The feather wand and the wind-up mouse (docs/TOY-PLAY.md → M4): the cat chases them only while the
// user plays (holds the wand, or has wound the mouse up). The chase itself is in PetWindow.ToyChase.cs.
// Up to about 90 seconds of chasing the wand, or 5 mice in a row, then two minutes of only watching.
public sealed class ToyChaseStreak
{
    public const double WandSeconds = 90, RestSeconds = 120, BreakSeconds = 20, BoredSeconds = 8, MouseWalkSeconds = 20;
    public const int MouseInARow = 5, PassesInARow = 20;
    private double wand, passing, idle, rest;
    private int mice, passes;

    public bool Resting => rest > 0;
    public double RestLeft => rest;
    public double WandPlayed => wand;
    public double PassPlayed => passing;
    public int MiceInARow => mice;
    public int PassesMade => passes;

    // M5 shares the rest: a frame of passing the ball (the 90th second rests), and each pass (the 20th rests).
    public void Pass(double dt)
    {
        if (rest > 0 || !double.IsFinite(dt) || dt <= 0) return;
        idle = 0;
        if ((passing += dt) >= WandSeconds) StartRest();
    }

    public void Passed()
    {
        if (rest > 0) return;
        idle = 0;
        if (++passes >= PassesInARow) StartRest();
    }

    // A frame of chasing the wand; the 90th second starts the rest.
    public void Wand(double dt)
    {
        if (rest > 0 || !double.IsFinite(dt) || dt <= 0) return;
        idle = 0;
        if ((wand += dt) >= WandSeconds) StartRest();
    }

    // The cat sets off after a wound-up mouse; false while it rests (it only watches). The fifth starts the rest.
    public bool TryMouse()
    {
        if (rest > 0) return false;
        idle = 0;
        if (++mice >= MouseInARow) StartRest();
        return true;
    }

    private void StartRest() { wand = passing = 0; mice = passes = 0; rest = RestSeconds; }

    // playing: the cat chases one of them this frame. Twenty seconds without: the next chase starts afresh.
    public void Step(double dt, bool playing)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        rest = Math.Max(0, rest - dt);
        if (playing) { idle = 0; return; }
        if ((idle += dt) >= BreakSeconds) { wand = passing = 0; mice = passes = 0; }
    }
}

// The feather on its string under the wand's tip, in screen DIPs (Y goes down). A point on a rope:
// it swings and lags behind the tip, never farther than Length from it, and drags along the floor.
public sealed class FeatherString
{
    public const double Length = 90, Gravity = 900, Damping = 0.985;
    public double X, Y;
    private double prevX, prevY, tipX, tipY;
    public bool Ready { get; private set; }
    public double Speed { get; private set; }
    // Held in the cat's paws: it stays at PinX, PinY until the tip pulls the string tight.
    public bool Pinned;
    public double PinX, PinY;

    public void Place(double x, double y, double tipX, double tipY)
    {
        X = prevX = x; Y = prevY = y;
        this.tipX = tipX; this.tipY = tipY;
        Ready = true; Speed = 0;
    }

    public void Clear() { Ready = false; Pinned = false; Speed = 0; }

    public void Pin(double x, double y) { Pinned = true; PinX = x; PinY = y; }

    public void Step(double dt, double newTipX, double newTipY, double floorY)
    {
        if (!double.IsFinite(newTipX) || !double.IsFinite(newTipY)) return;
        if (!Ready) Place(newTipX, Math.Min(newTipY + Length, floorY), newTipX, newTipY);
        if (!double.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, 0.1);
        int steps = Math.Max(1, (int)Math.Ceiling(dt * 120));
        double h = dt / steps, startX = X, startY = Y;
        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps, ax = tipX + (newTipX - tipX) * t, ay = tipY + (newTipY - tipY) * t;
            if (Pinned)
            {
                // Pulled tight: the paws let go.
                if (Math.Sqrt((PinX - ax) * (PinX - ax) + (PinY - ay) * (PinY - ay)) > Length + 12) Pinned = false;
                else { X = prevX = PinX; Y = prevY = PinY; continue; }
            }
            double vx = (X - prevX) * Damping, vy = (Y - prevY) * Damping;
            prevX = X; prevY = Y;
            X += vx; Y += vy + Gravity * h * h;
            double dx = X - ax, dy = Y - ay, d = Math.Sqrt(dx * dx + dy * dy);
            if (d > Length) { X = ax + dx * Length / d; Y = ay + dy * Length / d; }
            if (Y > floorY) { Y = floorY; X = prevX + (X - prevX) * 0.6; } // Drags along the floor.
        }
        tipX = newTipX; tipY = newTipY;
        Speed = Math.Sqrt((X - startX) * (X - startX) + (Y - startY) * (Y - startY)) / dt;
    }
}

// The wind-up mouse on the floor: a click winds it, it runs about 5 seconds, turns at the screen's
// edges and at the farm's lands, then slows down and stops. A hard landing turns it over for a second.
public sealed class WindUpMouse
{
    public const double RunSeconds = 5, Speed = 180, SlowSeconds = 1, FlipSeconds = 1, HardLanding = 380, Gravity = 1400;
    public double RunLeft, FlipLeft, Wheel;
    public int Direction = 1;
    // How many times it has been wound: one chase per winding.
    public int Winds { get; private set; }

    public bool Running => RunLeft > 0;
    public bool Flipped => FlipLeft > 0;
    public bool Busy => Running || Flipped;

    // Only on the floor, upright and still; a click while it runs does nothing.
    public bool Wind(ToyBall motion)
    {
        if (Busy || motion.Height > 0 || motion.VY != 0) return false;
        RunLeft = RunSeconds;
        motion.Feedback = 0.35;
        Winds++;
        return true;
    }

    // Caught or picked up.
    public void Stop(ToyBall motion) { RunLeft = 0; motion.VX = 0; }

    public void Step(ToyBall m, double dt, double left, double right, IReadOnlyList<(double Left, double Right)>? taken, double halfWidth)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        m.Feedback = Math.Max(0, m.Feedback - dt);
        dt = Math.Min(dt, 0.1);
        int steps = Math.Max(1, (int)Math.Ceiling(dt * 120));
        double h = dt / steps;
        right = Math.Max(left, right);
        for (int i = 0; i < steps; i++)
        {
            if (m.Height > 0 || m.VY > 0)
            {
                m.X += m.VX * h;
                m.VY -= Gravity * h; m.Height += m.VY * h;
                m.VX *= Math.Exp(-0.4 * h);
                if (m.Height <= 0)
                {
                    m.Height = 0;
                    if (m.VY < -HardLanding) { FlipLeft = FlipSeconds; RunLeft = 0; m.VX *= 0.3; }
                    m.VY = Math.Abs(m.VY) > 160 ? -m.VY * 0.2 : 0;
                    if (m.VX != 0) Direction = m.VX < 0 ? -1 : 1;
                }
            }
            else if (FlipLeft > 0)
            {
                FlipLeft = Math.Max(0, FlipLeft - h);
                m.VX = 0;
                Wheel += 900 * h;
            }
            else if (RunLeft > 0)
            {
                RunLeft = Math.Max(0, RunLeft - h);
                if (m.X <= left && Direction < 0) Direction = 1;
                else if (m.X >= right && Direction > 0) Direction = -1;
                else if (WouldEnter(m.X, Direction * Speed * h + Direction * 2, halfWidth, taken)) Direction = -Direction;
                m.VX = RunLeft > 0 ? Direction * Speed * Math.Min(1, RunLeft / SlowSeconds) : 0;
                m.X += m.VX * h;
                Wheel += m.VX * h * 6;
            }
            else
            {
                // Sliding to a stop after a throw.
                m.X += m.VX * h;
                m.VX *= Math.Exp(-8 * h);
                if (Math.Abs(m.VX) < 4) m.VX = 0;
            }
            if (m.X < left || m.X > right) { m.X = Math.Clamp(m.X, left, right); if (RunLeft <= 0) m.VX *= -0.3; }
        }
        m.Rotation = 0;
    }

    // Moving by step from x it would start to overlap a taken stretch it is not on now.
    private static bool WouldEnter(double x, double step, double halfWidth, IReadOnlyList<(double Left, double Right)>? taken)
    {
        if (taken == null) return false;
        foreach (var (l, r) in taken)
        {
            bool now = x + halfWidth > l && x - halfWidth < r;
            double next = x + step;
            if (!now && next + halfWidth > l && next - halfWidth < r) return true;
        }
        return false;
    }
}
