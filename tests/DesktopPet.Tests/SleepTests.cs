using DesktopPet;
using DesktopPet.AI;

internal static class SleepTests
{
    public static void Run(Action<bool, string> check)
    {
        var settings = new PetSettings { SleepDurationSeconds = 8, WakeCooldownSeconds = 0 };
        var physics = new PetPhysics(settings);
        var behavior = new PetBehavior(settings);
        var pet = new PetModel { X = 300, Y = 928, Grounded = true, VX = 72 };
        var bounds = new Area(0, 0, 1920, 1040);
        int requests = 0;
        behavior.DecisionRequested += () => requests++;
        check(behavior.TryApply(pet, new("SLEEP", 8, "sleepy")), "Sleep command enters the sleep lifecycle");
        for (int i = 0; i < 700; i++)
        {
            behavior.Step(pet, 0.01, 1500);
            physics.Step(pet, bounds, 0.01);
        }
        check(pet.Action == PetAction.Sleeping && pet.X == 300 && pet.Y == 928 && pet.VX == 0 && pet.VY == 0,
            "Sleeping stays planted and ignores cursor-follow movement");
        check(requests == 0 && !behavior.TryApply(pet, new("WALK_LEFT", 2, "content")) &&
            !DecisionGuard.CanApply(1, 1, 0.1, false, pet), "Random and delayed AI decisions cannot interrupt sleep");
        behavior.Step(pet, 1.1);
        check(pet.Action == PetAction.Waking && !pet.Emotions.SleepLocked, "Configured sleep duration automatically wakes and releases emotion lock");
        pet.Emotions.Step(pet, 0.1);
        check(pet.Emotions.Current == PetEmotion.Neutral, "Automatic wake clears Zs and resets inactivity");
        check(!behavior.TryApply(pet, new("SLEEP", 8, "sleepy")), "Waking completes before another AI action");
        behavior.Step(pet, PetSettings.WakingDuration + 0.01);
        check(pet.Action == PetAction.Idle && requests == 0, "Waking returns to idle without immediately walking");
        behavior.Step(pet, 2.1);
        check(requests == 1, "Normal random/AI decisions resume after waking and idle");

        pet.Action = PetAction.Idle;
        pet.Grounded = true;
        behavior.TryApply(pet, new("SLEEP", 8, "sleepy"));
        behavior.WakeByClick(pet);
        check(pet.Action == PetAction.Waking, "User interaction immediately wakes a sleeping pet");
        pet.Action = PetAction.Dragging;
        behavior.Reset();
        behavior.Step(pet, 20);
        check(pet.Action == PetAction.Dragging, "Dragging takes priority over all sleep timers");
        pet.Action = PetAction.Thrown;
        pet.Grounded = false;
        pet.VX = 300;
        pet.VY = -500;
        physics.Step(pet, bounds, 1.0 / 120);
        check(pet.X > 300 && pet.Y < 928 && pet.Action == PetAction.Thrown, "Throw after sleep uses the existing velocity and gravity");
    }
}
