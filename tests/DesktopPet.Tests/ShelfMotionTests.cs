using DesktopPet;

// The keepsake shelf as a cartoon object (ShelfMotion) and the emotions it gives the cat.
internal static class ShelfMotionTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60, left = 0, right = 1700;
        (double MaxSquash, int Landings, double Seconds) Drop(ShelfMotion s, double limit = 6)
        {
            double max = 0, t = 0;
            int landings = 0;
            while (t < limit && !(t > 0.1 && s.Resting)) { if (s.Step(dt, left, right)) landings++; max = Math.Max(max, s.Squash); t += dt; }
            return (max, landings, t);
        }

        var high = new ShelfMotion { X = 500, Height = 400 };
        var (highSquash, highLandings, highTime) = Drop(high);
        check(high.Resting && high.Height == 0 && high.X == 500 && highTime < 4, $"A dropped shelf falls, lands and comes to rest ({highTime:F2} s)");
        check(highLandings >= 2 && highSquash > 0.15, $"A high drop squashes it and bounces ({highLandings} landings, squash {highSquash:F2})");
        var low = new ShelfMotion { X = 500, Height = 60 };
        var (lowSquash, lowLandings, _) = Drop(low);
        check(lowSquash < highSquash && lowSquash > 0.03 && lowLandings < highLandings, $"A low drop squashes less and bounces less ({lowSquash:F2}, {lowLandings})");
        var tiny = new ShelfMotion { X = 500, Height = 0.5 };
        check(Drop(tiny).Landings == 0 && tiny.Resting, "Set down from almost nothing it only settles");
        var landed = new ShelfMotion { X = 500, Height = 300 };
        while (!landed.Step(dt, left, right)) { }
        check(landed.Landed > 500 && landed.VY > 0, $"The landing reports its impact speed and the bounce goes up ({landed.Landed:F0})");

        var falling = new ShelfMotion { X = 500, Height = 300, VY = -600 };
        falling.Step(dt, left, right);
        check(falling.ScaleY > 1 && falling.ScaleX < 1, "Falling fast it stretches");

        foreach (var (speed, min, max) in new[] { (200.0, 50.0, 110.0), (400.0, 110.0, 190.0) })
        {
            var shoved = new ShelfMotion { X = 500 };
            shoved.Shove(1, speed);
            check(shoved.VY > 0, "A shove gives a little hop");
            Drop(shoved);
            check(shoved.Resting && shoved.X - 500 >= min && shoved.X - 500 <= max, $"A shove at {speed} slides it {shoved.X - 500:F0} DIPs");
        }
        var back = new ShelfMotion { X = 500 };
        back.Shove(-1, 400);
        Drop(back);
        check(back.X < 500, "A shove to the left slides it left");

        var wall = new ShelfMotion { X = 40, Height = 100 };
        wall.Release(-2000, 0);
        bool inside = true;
        for (int i = 0; i < 240; i++) { wall.Step(dt, left, right); inside &= wall.X >= left && wall.X <= right; }
        check(inside && wall.X > left, "Thrown hard into the screen edge it stays inside and bounces back off it");

        var held = new ShelfMotion { X = 500, Height = 200, Held = true, HeldTilt = 20 };
        for (int i = 0; i < 120; i++) held.Step(dt, left, right);
        check(held.X == 500 && held.Height == 200 && Math.Abs(held.Tilt - 20) < 2, $"Held, it stays at the hand and swings to the dangling angle ({held.Tilt:F1}°)");
        held.Release(300, -200);
        check(!held.Held && held.VX == 300 && held.VY == 200, "Thrown upward it keeps the throw's velocity (up-positive)");

        var boing = new ShelfMotion { X = 500 };
        boing.Boing();
        double most = 0, least = 0;
        for (int i = 0; i < 30; i++) { boing.Step(dt, left, right); most = Math.Max(most, boing.Squash); least = Math.Min(least, boing.Squash); }
        check(most > 0.1 && least < -0.02 && boing.Height == 0 && boing.X == 500, "A click makes it boing in place (squash, then stretch)");
        Drop(boing);
        check(boing.Resting, "The boing dies out");

        var bonk = new ShelfMotion { X = 500, Height = 96 };
        bonk.Rebound(-1, 300);
        double top = bonk.Height;
        for (int i = 0; i < 20; i++) { bonk.Step(dt, left, right); top = Math.Max(top, bonk.Height); }
        Drop(bonk);
        check(top > 130 && bonk.X < 500 - 150, $"Knocked off the head it pops up and lands off to the side ({500 - bonk.X:F0} DIPs away)");

        check(ShelfMotion.NearEdge(10, 100, 0, 1700) && ShelfMotion.NearEdge(1590, 100, 0, 1700) && !ShelfMotion.NearEdge(800, 100, 0, 1700)
              && !ShelfMotion.NearEdge(30, 100, 0, 1700), "Near the edge: closer than 24 DIPs to either side");
        var still = new ShelfMotion { X = 500 };
        still.Step(double.NaN, left, right);
        check(still.X == 500 && still.Resting, "Invalid time steps are ignored");

        // Emotions the shelf gives: startled (Surprised) and a short happy moment, not in manual control or asleep.
        var pet = new PetModel { Grounded = true };
        void Run(double seconds) { for (double t = 0; t < seconds; t += dt) pet.Emotions.Step(pet, dt); }
        Run(0.5);
        check(pet.Emotions.Startle(), "The shelf at the edge startles the cat");
        Run(0.4);
        check(pet.Emotions.Current == PetEmotion.Surprised && pet.Emotions.BlocksAutonomy, "Startled: Surprised (it stops what it does)");
        Run(1.5);
        check(pet.Emotions.Current != PetEmotion.Surprised, "The startle passes");
        pet.Emotions.Cheer();
        Run(0.4);
        check(pet.Emotions.Current == PetEmotion.Happy && !pet.Emotions.BlocksAutonomy, "A shove it liked: briefly Happy");
        pet.Emotions.SetManualMode(true);
        check(!pet.Emotions.Startle(), "In manual emotion control the shelf does not startle it");
        pet.Emotions.SetManualMode(false);
        pet.Emotions.EnterSleep();
        check(!pet.Emotions.Startle(), "Asleep it is not startled");
    }
}
