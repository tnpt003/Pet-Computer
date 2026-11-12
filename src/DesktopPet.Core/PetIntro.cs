using System;

namespace DesktopPet;

// The startup intro (M2a, docs/APPROVED-EXCEPTIONS.md → Cổng phép): about three seconds each time the
// app opens. A decorative portal opens where the cat was saved, the cat is tossed out of it with an
// ordinary throw, the farm sign rises out of the ground and the keepsake, the basket and the toys pop
// in with smoke and sparkles. This is only the clock; the window owns the art and the windows
// (PetWindow.Intro.cs). Nothing here is an emotion or an interaction, and nothing goes to Python.
public sealed class PetIntro
{
    public const double ThrowAt = 0.6, SignAt = 0.9, SignSeconds = 1.0, ItemsAt = 1.0, ItemStagger = 0.22,
        ItemFadeSeconds = 0.35, FxSeconds = 0.7, PortalLifetime = 1.6, TotalSeconds = 3.0;
    // The toss: about 750 DIP/s, far under the 1600 that counts as a rough throw (anger). Dizzy only
    // ever comes from swinging the cat by the hand, never from a throw.
    public const double ThrowSpeedX = 430, ThrowSpeedY = -620, RoughThrowSpeed = 1600;
    // How far the sign shakes from side to side while it rises, in DIPs.
    public const double SignShake = 1.6;

    public bool Active { get; private set; }
    public bool Thrown { get; private set; }
    public double Time { get; private set; }

    public void Start()
    {
        Active = true;
        Thrown = false;
        Time = 0;
    }

    // One frame. True on the frame the cat should be tossed out (once).
    public bool Step(double dt)
    {
        if (!Active) return false;
        Time += Math.Max(0, dt);
        if (Time >= TotalSeconds) Active = false;
        if (Thrown || Time < ThrowAt) return false;
        Thrown = true;
        return true;
    }

    // A click on the cat or the portal, a fullscreen app, a battle: everything at once.
    public void Finish()
    {
        Active = false;
        Thrown = true;
        Time = TotalSeconds;
    }

    // When the n-th thing (keepsake, basket, toys, in that order) begins to appear.
    public static double ItemAt(int index) => ItemsAt + Math.Max(0, index) * ItemStagger;

    // 0 hidden, 1 fully shown.
    public double ItemShown(int index) => !Active ? 1 : Math.Clamp((Time - ItemAt(index)) / ItemFadeSeconds, 0, 1);

    // How much of the sign is out of the ground, 0 to 1 (eases out as it arrives).
    public double SignRise => !Active ? 1 : EaseOut(Math.Clamp((Time - SignAt) / SignSeconds, 0, 1));

    // The sideways shake while it rises; none before or after.
    public double SignOffset
    {
        get
        {
            double rise = SignRise;
            return rise <= 0 || rise >= 1 ? 0 : SignShake * Math.Sin(Time * 62) * (1 - rise * 0.6);
        }
    }

    // The toss goes toward the side with more room.
    public static (double VX, double VY) ThrowVelocity(double roomLeft, double roomRight) =>
        (roomRight >= roomLeft ? ThrowSpeedX : -ThrowSpeedX, ThrowSpeedY);

    private static double EaseOut(double t) => 1 - Math.Pow(1 - t, 3);
}
