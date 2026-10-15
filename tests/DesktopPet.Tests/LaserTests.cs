using DesktopPet;

internal static class LaserTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        var chase = new LaserChase();
        chase.Reset();
        bool Steps(double seconds, double dx, bool reachable, double speed, string personality = "Balanced")
        {
            bool pounced = false;
            for (double t = 0; t < seconds; t += dt) pounced |= chase.Step(dt, dx, reachable, speed, personality);
            return pounced;
        }

        check(!Steps(0.3, 300, true, 500) && chase.Phase == LaserPhase.Watch, "The cat first watches the dot");
        Steps(0.4, 300, true, 500);
        check(chase.Phase == LaserPhase.Chase, "Then it chases a moving dot");
        check(!Steps(2, 30, true, 500) && chase.Phase == LaserPhase.Chase, "A dot that keeps moving is chased, never pounced");
        Steps(0.35, 30, true, 0);
        check(chase.Phase == LaserPhase.Stalk && chase.CrouchBlend > 0, "A dot resting nearby makes the cat crouch and stalk");
        Steps(0.05, 30, true, 400);
        check(chase.Phase == LaserPhase.Chase && chase.CrouchBlend == 0, "If the dot moves away during the stalk, the chase resumes");
        Steps(0.35, 30, true, 0);
        bool pounced = Steps(0.7, 30, true, 0);
        check(pounced && chase.Phase == LaserPhase.Pounce, "After the stalk the cat pounces once");
        check(!Steps(1, 30, true, 0) && chase.Phase == LaserPhase.Pounce, "No second pounce until the first one has landed");
        chase.Landed();
        check(chase.Phase == LaserPhase.Recover, "Landing starts a short recovery");
        Steps(1.1, 300, true, 500);
        check(chase.Phase == LaserPhase.Chase, "After recovering the chase continues");
        check(!Steps(3, 20, false, 0) && chase.Phase == LaserPhase.Watch, "A dot high above or on another monitor is only watched");

        var playful = new LaserChase(); playful.Reset();
        var calm = new LaserChase(); calm.Reset();
        double Until(LaserChase c, string personality)
        {
            double t = 0;
            while (c.Phase == LaserPhase.Watch && t < 5) { c.Step(dt, 300, true, 500, personality); t += dt; }
            return t;
        }
        check(Until(playful, "Playful") < Until(calm, "Curious"), "Playful cats start chasing sooner than curious ones");
        check(!new LaserChase().Step(double.NaN, 0, true, 0, "Balanced"), "Invalid time steps are ignored");
    }
}
