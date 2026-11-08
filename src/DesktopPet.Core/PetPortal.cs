using System;

namespace DesktopPet;

public enum PortalPhase { Closed, Opening, Open, Closing }

// The magic portal's clock (1.5.1 M1): when the next one is due, and how far the open one is.
// The window owns the art, the placement and the rules for when one may appear (PetWindow.Portal.cs).
public sealed class PetPortal
{
    public const double OpeningSeconds = 0.7, ClosingSeconds = 0.45;
    // How long a portal waits for its click: one that came by itself, one the cat summoned.
    public const double EventLifetime = 30, CastLifetime = 45;
    public const double EventMinSeconds = 20 * 60, EventMaxSeconds = 40 * 60;
    // The moment was wrong (asleep, Quiet, playing...): look again soon, nothing piles up.
    public const double RetrySeconds = 45;

    public PortalPhase Phase { get; private set; }
    // Seconds since it began to open (the art's swirl).
    public double Time { get; private set; }
    public double Due { get; private set; } = EventMinSeconds;
    private double phaseTime, lifetime, closingFrom = 1;

    public bool IsOpen => Phase != PortalPhase.Closed;

    // 0 closed, 1 fully open; overshoots a little as it pops open.
    public double Amount => Phase switch
    {
        PortalPhase.Opening => BackOut(Math.Clamp(phaseTime / OpeningSeconds, 0, 1)),
        PortalPhase.Open => 1,
        PortalPhase.Closing => closingFrom * (1 - Smooth(Math.Clamp(phaseTime / ClosingSeconds, 0, 1))),
        _ => 0
    };

    public void Schedule(double seconds) => Due = Math.Max(0, seconds);

    public static double NextDelay(Random random) => EventMinSeconds + random.NextDouble() * (EventMaxSeconds - EventMinSeconds);

    // Counts down only while no portal is open. True on the frame the next event is due: the
    // caller decides whether the moment is right, and always calls Schedule.
    public bool CountDown(double dt)
    {
        if (IsOpen) return false;
        Due = Math.Max(0, Due - dt);
        return Due <= 0;
    }

    public void Open(double seconds)
    {
        Phase = PortalPhase.Opening;
        Time = phaseTime = 0;
        lifetime = seconds;
    }

    public void BeginClose()
    {
        if (Phase is PortalPhase.Closed or PortalPhase.Closing) return;
        closingFrom = Math.Min(1, Amount);
        Phase = PortalPhase.Closing;
        phaseTime = 0;
    }

    public void Reset()
    {
        Phase = PortalPhase.Closed;
        Time = phaseTime = 0;
    }

    // True on the frame it has finished closing.
    public bool Step(double dt)
    {
        if (!IsOpen) return false;
        Time += dt;
        phaseTime += dt;
        switch (Phase)
        {
            case PortalPhase.Opening when phaseTime >= OpeningSeconds:
                Phase = PortalPhase.Open;
                phaseTime = 0;
                break;
            case PortalPhase.Open when phaseTime >= lifetime:
                BeginClose();
                break;
            case PortalPhase.Closing when phaseTime >= ClosingSeconds:
                Reset();
                return true;
        }
        return false;
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);
    private static double BackOut(double t) => 1 + 2.7 * Math.Pow(t - 1, 3) + 1.7 * Math.Pow(t - 1, 2);
}
