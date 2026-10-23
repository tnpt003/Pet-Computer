using System;

namespace DesktopPet;

// The cartoon squash and wobble everything that falls shares (the cat, the keepsake): two damped
// springs, Squash (+ flattened, − stretched) and Tilt (degrees). Presentation only: never touches
// position or velocity. Each owner maps Squash to its own scale (the cat's face squashes less).
public sealed class ImpactSpring
{
    public double Squash, SquashV, Tilt, TiltV;

    // One small step (h ≤ ~1/120 s); while held the tilt swings toward the dangling angle instead of upright.
    public void Step(double h, double tiltTarget = 0, double tiltDamping = 9)
    {
        SquashV += (-340 * Squash - 7 * SquashV) * h; Squash += SquashV * h;
        TiltV += (-180 * (Tilt - tiltTarget) - tiltDamping * TiltV) * h; Tilt += TiltV * h;
        Tilt = Math.Clamp(Tilt, -35, 35);
    }

    // Hitting the ground at `impact` DIP/s while moving `vx` sideways: a squash that grows with the
    // fall (then rebounds into a little stretch) and a wobble toward the motion.
    public void Land(double impact, double vx) { SquashV += Landing.SquashKick(impact); TiltV += vx * 0.04; }
    // Hitting a side (vx: the velocity after the bounce): squeezed sideways, tipped back.
    public void Wall(double vx) { SquashV -= 1.5; TiltV += vx * 0.08; }

    public void Settle(bool held = false)
    {
        if (Math.Abs(Squash) < 0.002 && Math.Abs(SquashV) < 0.02) Squash = SquashV = 0;
        if (Math.Abs(Tilt) < 0.05 && Math.Abs(TiltV) < 0.2 && !held) Tilt = TiltV = 0;
    }
    public void Reset() => Squash = SquashV = Tilt = TiltV = 0;
}

// One landing rule for the cat and the keepsake: how hard a hit must be for dust and for the
// sound and how long the dust lasts (LandingDust draws it, PetImpactArt.cs). X/Y: where it landed, in the owner's coordinates.
public sealed class Landing
{
    public const double DustSpeed = 120, SoundSpeed = 300, DustSeconds = 0.45, SoundGap = 0.4;
    public const string Sound = "squish";

    public static double SquashKick(double impact) => Math.Clamp(impact / 1000 * 5.5, 0, 7.5);

    // Dust progress 0..1 (negative: none) and its strength 0.2..1.
    public double Dust { get; private set; } = -1;
    public double Strength { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    private double sinceSound = SoundGap;
    private bool soundDue;

    // A bounce series sounds once per SoundGap (the first, hardest hit), the dust every time.
    public void Hit(double impact, double x = 0, double y = 0, bool quiet = false)
    {
        if (!double.IsFinite(impact) || impact <= DustSpeed) return;
        Dust = 0; Strength = Math.Clamp(impact / 1200, 0.2, 1); X = x; Y = y;
        if (!quiet && impact > SoundSpeed && sinceSound >= SoundGap) { soundDue = true; sinceSound = 0; }
    }

    // True once after a hit loud enough for the sound; the owner plays Sound.
    public bool TakeSound() { bool due = soundDue; soundDue = false; return due; }

    public void Step(double dt)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        sinceSound += dt;
        if (Dust >= 0 && (Dust += dt / DustSeconds) >= 1) Dust = -1;
    }
    public void Clear() { Dust = -1; soundDue = false; }
}
