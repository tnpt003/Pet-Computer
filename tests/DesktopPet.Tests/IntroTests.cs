using DesktopPet;

// The startup intro's clock (M2a): about three seconds, one toss, things in turn, a skip shows all.
internal static class IntroTests
{
    public static void Run(Action<bool, string> check)
    {
        var intro = new PetIntro();
        check(!intro.Active && intro.ItemShown(0) == 1 && intro.SignRise == 1, "With no intro everything is shown");

        intro.Start();
        check(intro.Active && intro.ItemShown(0) == 0 && intro.SignRise == 0, "It starts with the things hidden and the sign in the ground");
        int tosses = 0;
        double tossedAt = -1, time = 0;
        bool ordered = true;
        double lastRise = 0;
        while (intro.Active && time < 10)
        {
            time += 1.0 / 60;
            if (intro.Step(1.0 / 60)) { tosses++; tossedAt = intro.Time; }
            if (intro.Active && intro.SignRise < lastRise - 1e-9) ordered = false;
            if (intro.Active) lastRise = intro.SignRise;
            if (intro.Active && intro.ItemShown(1) > intro.ItemShown(0) + 1e-9) ordered = false;
        }
        check(tosses == 1, $"The cat is tossed out once ({tosses})");
        check(Math.Abs(tossedAt - PetIntro.ThrowAt) < 0.03, $"...after the portal has opened ({tossedAt:F2} s)");
        check(Math.Abs(time - PetIntro.TotalSeconds) < 0.05, $"It lasts about three seconds ({time:F2} s)");
        check(ordered, "The sign only rises and the things appear in turn");
        check(PetIntro.ItemAt(4) + PetIntro.ItemFadeSeconds <= PetIntro.TotalSeconds, "Five things are all shown before it ends");
        check(PetIntro.SignAt + PetIntro.SignSeconds <= PetIntro.TotalSeconds, "The sign is up before it ends");
        check(!intro.Active && intro.ItemShown(3) == 1 && intro.SignRise == 1 && intro.SignOffset == 0, "At the end everything stands still and shown");

        intro.Start();
        intro.Step(0.3);
        intro.Finish();
        check(!intro.Active && intro.Thrown && !intro.Step(1), "A skip shows everything and no toss follows");
        check(intro.ItemShown(0) == 1 && intro.SignRise == 1, "...with every thing shown and the sign up");

        var (vx, vy) = PetIntro.ThrowVelocity(100, 900);
        var (wx, _) = PetIntro.ThrowVelocity(900, 100);
        check(vx > 0 && wx < 0 && vy < 0, "The toss goes up, toward the side with more room");
        check(Math.Sqrt(vx * vx + vy * vy) < PetIntro.RoughThrowSpeed * 0.6, $"It is a gentle throw, not a rough one ({Math.Sqrt(vx * vx + vy * vy):F0} DIP/s)");

        intro.Start();
        intro.Step(PetIntro.SignAt + PetIntro.SignSeconds / 2);
        check(intro.SignRise > 0 && intro.SignRise < 1 && Math.Abs(intro.SignOffset) <= PetIntro.SignShake, "The sign shakes a little while it rises");
    }
}
