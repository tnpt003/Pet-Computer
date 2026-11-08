using DesktopPet;

// The magic portal's clock (1.5.1 M1): when it is due, how it opens and closes.
internal static class PortalTests
{
    public static void Run(Action<bool, string> check)
    {
        var random = new Random(7);
        for (int i = 0; i < 200; i++)
        {
            double delay = PetPortal.NextDelay(random);
            if (delay < 20 * 60 || delay > 40 * 60) check(false, $"A portal is due every 20 to 40 minutes ({delay})");
        }
        check(true, "A portal is due every 20 to 40 minutes");

        var portal = new PetPortal();
        portal.Schedule(1);
        check(!portal.CountDown(0.5) && portal.Due == 0.5, "The countdown runs while no portal is open");
        check(portal.CountDown(0.5), "It says so on the frame the event is due");
        portal.Schedule(PetPortal.RetrySeconds);
        check(portal.Due == 45 && !portal.CountDown(1), "A wrong moment looks again soon");

        portal.Schedule(1);
        portal.Open(PetPortal.EventLifetime);
        check(portal.Phase == PortalPhase.Opening && Math.Abs(portal.Amount) < 1e-9, "It opens from nothing");
        check(!portal.CountDown(5) && portal.Due == 1, "The countdown waits while a portal is open");

        double most = 0;
        for (double t = 0; t < PetPortal.OpeningSeconds + 0.02; t += 1.0 / 60) { portal.Step(1.0 / 60); most = Math.Max(most, portal.Amount); }
        check(portal.Phase == PortalPhase.Open && portal.Amount == 1 && most > 1 && most < 1.2, $"It pops open with a small overshoot ({most:F3})");

        double opened = 0;
        bool finished = false;
        for (double t = 0; t < PetPortal.EventLifetime + 2 && !finished; t += 1.0 / 60)
        {
            finished = portal.Step(1.0 / 60);
            if (portal.Phase == PortalPhase.Open) opened += 1.0 / 60;
        }
        check(finished && portal.Phase == PortalPhase.Closed && !portal.IsOpen, "Left alone it closes by itself");
        check(Math.Abs(opened - PetPortal.EventLifetime) < 0.1, $"It waits its whole lifetime for the click ({opened:F2} s)");

        portal.Open(PetPortal.CastLifetime);
        for (int i = 0; i < 12; i++) portal.Step(1.0 / 60);
        double before = portal.Amount;
        portal.BeginClose();
        check(portal.Phase == PortalPhase.Closing && Math.Abs(portal.Amount - Math.Min(1, before)) < 1e-9, "Closing during the opening starts from where it was, without a jump");
        portal.BeginClose();
        check(portal.Phase == PortalPhase.Closing, "Closing twice changes nothing");
        bool done = false;
        for (double t = 0; t < PetPortal.ClosingSeconds + 0.05 && !done; t += 1.0 / 60) done = portal.Step(1.0 / 60);
        check(done && portal.Amount == 0, "Closing ends at nothing");
        check(!portal.Step(1) && portal.Phase == PortalPhase.Closed, "A closed portal stays closed");
    }
}
