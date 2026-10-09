using DesktopPet;

internal static class PickupTests
{
    public static void Run(Action<bool, string> check)
    {
        PickupAnimation Move(double speedX, double speedY, int frames = 120)
        {
            var animation = new PickupAnimation();
            animation.Begin(0, 0);
            for (int i = 1; i <= frames; i++) animation.Step(speedX * i / frames, speedY * i / frames, 1.0 / frames);
            return animation;
        }
        var slow = Move(100, 0);
        var medium = Move(1400, 0);
        check(medium.Dizziness == 0 && medium.Angle > 10, "Normal movement gives strong dangling without dizziness");
        var belowThreshold = Move(1750, 0);
        check(belowThreshold.Dizziness == 0, "Movement below the raised entry threshold never starts dizziness");
        var burst = new PickupAnimation();
        burst.Begin(0, 0);
        burst.Step(33, 0, 1.0 / 120);
        for (int i = 0; i < 120; i++) burst.Step(33, 0, 1.0 / 120);
        check(burst.Dizziness == 0, "A single fast cursor spike does not trigger dizziness");
        var brief = new PickupAnimation();
        brief.Begin(0, 0);
        for (int i = 1; i <= 12; i++) brief.Step(i * 2200.0 / 120, 0, 1.0 / 120);
        check(brief.Dizziness == 0, "Fast movement needs accumulated intensity before it starts dizziness");
        var shaking = new PickupAnimation();
        shaking.Begin(0, 0);
        for (int i = 1; i <= 120; i++) shaking.Step(i % 2 == 0 ? 0 : 22, 0, 1.0 / 120);
        check(shaking.Dizziness > 0.9, "Aggressive direction reversals accumulate speed without cancelling out");
        var fast = Move(2200, 0);
        check(slow.Dizziness < 0.01 && fast.Dizziness > 0.9, "Dizziness scales with cursor speed, not distance alone");
        check(fast.Angle > 15 && fast.Angle > slow.Angle && fast.Angle <= PickupAnimation.MaxAngle,
            "Fast horizontal movement produces a stronger but bounded trailing tilt");
        check(Move(-2200, 0).Angle < 0, "Leftward movement reverses the dangling tilt");
        check(Move(0, 2200).Dizziness > 0.9, "Fast vertical movement also causes dizziness");
        fast.Step(2200, 0, 1.0 / 60);
        check(Math.Abs(fast.Angle) > 1, "Stopping the cursor does not instantly snap the swing upright");
        for (int i = 0; i < 120; i++) fast.Step(2200, 0, 1.0 / 120);
        check(fast.Dizziness > 0.85, "Dizziness remains clearly visible for a hold after fast movement stops");
        for (int i = 0; i < 120; i++) fast.Step(2200, 0, 1.0 / 120);
        check(fast.Dizziness > 0.3 && fast.Dizziness < 0.85, "Dizziness fades gradually after the hold");
        for (int i = 0; i < 1200; i++) fast.Step(2200, 0, 1.0 / 120);
        check(Math.Abs(fast.Angle) < 0.01 && fast.Dizziness < 0.001, "Stationary cursor lets pendulum and dizziness settle smoothly");
        var lowFps = Move(1500, 0, 30);
        var highFps = Move(1500, 0, 120);
        check(Math.Abs(lowFps.Angle - highFps.Angle) < 0.2 && Math.Abs(lowFps.Dizziness - highFps.Dizziness) < 0.02,
            "Spring and speed response remain consistent at different frame rates");
        check(Math.Abs(Move(2200, 0, 30).Dizziness - Move(2200, 0, 120).Dizziness) < 0.02,
            "Dizzy activation remains consistent across frame rates");
        var hysteresis = Move(2200, 0);
        double position = 2200;
        for (int i = 0; i < 360; i++)
        {
            position += (i % 2 == 0 ? 1700 : 1900) / 120.0;
            hysteresis.Step(position, 0, 1.0 / 120);
        }
        check(hysteresis.Dizziness > 0.9, "Speed oscillating near entry threshold does not toggle active dizziness");
        fast.Begin(10000, -3000);
        fast.Step(10000, -3000, 0);
        fast.Step(10000, -3000, 0.016);
        check(fast.Angle == 0 && fast.Dizziness == 0, "Repicking resets history without a false cursor-speed spike");
        var bounded = Move(1000000, 1000000);
        check(double.IsFinite(bounded.Angle) && Math.Abs(bounded.Angle) <= PickupAnimation.MaxAngle && bounded.Dizziness <= 1,
            "Extreme cursor movement cannot exaggerate or destabilize the effect");
        bounded.Reset();
        check(bounded.Angle == 0 && bounded.Dizziness == 0 && bounded.Time == 0, "Release clears all pickup presentation state");
        var rest = bounded.VisualBounds(112, 112);
        check(rest == new Area(0, 0, 112, 112), "First pickup frame has the same world bounds as idle");
    }
}
