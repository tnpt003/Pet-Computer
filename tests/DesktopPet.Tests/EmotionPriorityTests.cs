using DesktopPet;

internal static class EmotionPriorityTests
{
    public static void Run(Action<bool, string> check)
    {
        var pet = new PetModel { Grounded = true, Action = PetAction.Walking, X = 300, Y = 928, VX = 72 };
        var behavior = new PetBehavior(new PetSettings());
        var physics = new PetPhysics(new PetSettings());
        var bounds = new Area(0, 0, 1920, 1040);
        void Step(double seconds)
        {
            for (int i = 0; i < Math.Ceiling(seconds * 120); i++)
            {
                pet.Emotions.Step(pet, 1.0 / 120);
                behavior.Step(pet, 1.0 / 120, 1500);
                physics.Step(pet, bounds, 1.0 / 120);
            }
        }
        pet.Emotions.Step(pet, 121);
        Step(0.5);
        check(pet.Action == PetAction.Sleeping && pet.VX == 0 && pet.X == 300 && pet.Emotions.Current == PetEmotion.Sleepy,
            "Sleepy stops existing walking immediately and enters Sleeping");
        check(!behavior.TryApply(pet, new("WALK_RIGHT", 3, "content")), "AI cannot restart walking under a sleepy expression");
        Step(5);
        check(pet.X == 300 && pet.Action == PetAction.Sleeping, "Random behavior cannot move a sleepy pet");

        pet.Emotions.Reset();
        behavior.Reset();
        pet.Action = PetAction.Idle;
        behavior.TryApply(pet, new("SLEEP", 1, "sleepy"));
        Step(0.3);
        pet.Emotions.Step(pet, 0.1, pet.X - 200, pet.Y + 50);
        pet.Emotions.Step(pet, 0.05, pet.X + 56, pet.Y + 50);
        pet.Emotions.Attention();
        pet.Emotions.AggressiveInteraction();
        pet.Emotions.ApplyAiHint("sad");
        Step(1);
        check(pet.Emotions.Current == PetEmotion.Sleepy && pet.Emotions.SleepLocked && pet.Action == PetAction.Sleeping,
            "Cursor surprise, attention, anger, hints and long timers cannot interrupt sleep");
        bool petted = false;
        for (int i = 0; i < 120; i++)
            petted |= pet.Emotions.Step(pet, 1.0 / 60, pet.X + 50 + i % 20, pet.Y + 50, true);
        check(!petted && pet.Emotions.SleepLocked, "Hover petting cannot wake sleeping pet");
        behavior.Reset();
        Step(1);
        check(pet.Action == PetAction.Sleeping && pet.X == 300, "Resetting AI behavior timers cannot wake sleep");
        behavior.WakeByClick(pet);
        pet.Emotions.Attention(click: true);
        check(!pet.Emotions.SleepLocked && pet.Emotions.Current == PetEmotion.Neutral && pet.Action == PetAction.Waking,
            "Click clears sleep and Zs immediately before processing its reaction");
        Step(1.6);
        check(pet.Emotions.Current == PetEmotion.Happy, "Click reaction runs after waking");
        pet.Action = PetAction.Idle;
        pet.Emotions.Reset(); behavior.Reset();
        pet.Emotions.Step(pet, 121); Step(0.3);
        pet.Emotions.AggressiveInteraction(); Step(1);
        check(pet.Emotions.Current == PetEmotion.Sleepy && pet.Action == PetAction.Sleeping && pet.VX == 0,
            "Inactivity Sleepy is locked against anger and neglect timers too");
        Step(9);
        check(pet.Action == PetAction.Waking && !pet.Emotions.SleepLocked,
            "Inactivity sleep automatically wakes at the configured duration");
        Step(1.5);
        check(pet.Action == PetAction.Idle && pet.Emotions.Current == PetEmotion.Neutral,
            "Automatic wake returns to normal without immediately becoming sleepy again");
        pet.Action = PetAction.Dragging; pet.Grounded = false;
        Step(0.3);
        check(pet.Action == PetAction.Dragging && pet.Emotions.Current == PetEmotion.Neutral,
            "Click wakes inactivity Sleepy before normal dragging");
    }
}
