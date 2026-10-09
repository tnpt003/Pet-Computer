using DesktopPet;

internal static class EmotionTests
{
    public static void Run(Action<bool, string> check)
    {
        PetModel Pet() => new() { Action = PetAction.Idle, Grounded = true };
        void Advance(PetModel pet, double seconds)
        {
            for (int i = 0; i < (int)Math.Ceiling(seconds * 60); i++) pet.Emotions.Step(pet, 1.0 / 60);
        }
        var pet = Pet();
        pet.Emotions.Attention(click: true);
        Advance(pet, 0.5);
        check(pet.Emotions.Current == PetEmotion.Happy && pet.Emotions.Strength > 0.9, "One click produces a smooth Happy reaction");
        Advance(pet, 3.5);
        check(pet.Emotions.Current == PetEmotion.Neutral, "Happy returns to neutral after its duration");
        pet = Pet();
        for (int i = 0; i < 4; i++) { pet.Emotions.Attention(click: true); Advance(pet, 0.2); }
        Advance(pet, 0.4);
        check(pet.Emotions.Current == PetEmotion.Angry, "Four clicks in two seconds produce playful anger");
        for (int i = 0; i < 10; i++) { pet.Emotions.Attention(click: true); Advance(pet, 0.1); }
        Advance(pet, 3.5);
        check(pet.Emotions.Current == PetEmotion.Neutral, "Repeated clicks during anger cooldown do not extend it forever");
        pet = Pet();
        pet.Emotions.Step(pet, 121);
        Advance(pet, 0.5);
        check(pet.Emotions.Current == PetEmotion.Sleepy, "Inactivity produces Sleepy without random emotion selection");
        pet.Emotions.Step(pet, 181);
        Advance(pet, 0.5);
        check(pet.Emotions.Current == PetEmotion.Sleepy, "Prolonged neglect cannot replace locked sleep");
        pet = Pet();
        pet.Emotions.Step(pet, 301);
        Advance(pet, 0.5);
        var behavior = new PetBehavior(new PetSettings());
        behavior.TryApply(pet, new("WALK_RIGHT", 4, "content"));
        check(pet.VX < 50 && pet.VX > 40, "Sadness slows walking rather than changing physics");
        pet.Emotions.Attention();
        pet.Emotions.ApplyAiHint("sad");
        Advance(pet, 0.5);
        check(pet.Emotions.Current == PetEmotion.Happy, "Fresh attention clears sadness and rejects stale Python hints");
        pet = Pet();
        pet.Emotions.Step(pet, 0.1, -200, 50);
        pet.Emotions.Step(pet, 0.05, 56, 50);
        Advance(pet, 0.4);
        check(pet.Emotions.Current == PetEmotion.Surprised, "Fast cursor arrival near the pet triggers surprise");
        Advance(pet, 1.3);
        pet.Emotions.Step(pet, 0.05, -50, 50);
        Advance(pet, 0.4);
        check(pet.Emotions.Current == PetEmotion.Neutral, "Surprise cooldown prevents rapid repeated startling");
        pet = Pet();
        pet.Emotions.Step(pet, 0.1, 1000, 1000);
        pet.Emotions.Step(pet, 0.01, 2000, 1000);
        Advance(pet, 0.5);
        check(pet.Emotions.Current == PetEmotion.Neutral, "Fast movement far from the pet has no emotional effect");
        bool petted = false;
        for (int i = 0; i < 90; i++) petted |= pet.Emotions.Step(pet, 1.0 / 60,
            50 + (i % 40 <= 20 ? i % 40 : 40 - i % 40), 50, true);
        Advance(pet, 0.5);
        check(petted && pet.Emotions.Current == PetEmotion.Happy, "Gentle sustained strokes register as positive attention");
        pet = Pet();
        pet.Emotions.Step(pet, 0.1, 50, 50, true);
        for (int i = 0; i < 90; i++) pet.Emotions.Step(pet, 1.0 / 60, 50, 50, true);
        check(pet.Emotions.Current == PetEmotion.Neutral, "A parked cursor is not repeatedly counted as petting");
        pet.Action = PetAction.Dragging;
        pet.Pickup.Begin(0, 0);
        pet.Emotions.Attention();
        for (int i = 1; i <= 120; i++)
        {
            pet.Pickup.Step(i * 8, 0, 1.0 / 120);
            pet.Emotions.Step(pet, 1.0 / 120, i * 8, 0, true);
        }
        check(pet.Emotions.Current == PetEmotion.Neutral && pet.Action == PetAction.Dragging,
            "Normal dragging suppresses other emotions and does not cause dizziness");
        for (int i = 1; i <= 120; i++)
        {
            pet.Pickup.Step(960 + i * 20, 0, 1.0 / 120);
            pet.Emotions.Step(pet, 1.0 / 120);
        }
        check(pet.Emotions.Current == PetEmotion.Dizzy && pet.Action == PetAction.Dragging,
            "Only the existing high-speed trigger produces the Dizzy emotion during dragging");
        pet.Action = PetAction.Thrown;
        pet.VX = 1800; pet.VY = -900;
        Advance(pet, 0.5);
        check(pet.Emotions.Current == PetEmotion.Neutral && pet.VX == 1800 && pet.VY == -900,
            "Emotion update never changes thrown velocities or interrupts physics");
        pet.Emotions.Reset();
        pet.Action = PetAction.Sleeping;
        Advance(pet, 0.5);
        check(pet.Emotions.Current == PetEmotion.Sleepy, "Uninterrupted sleep uses the Sleepy expression");
        pet.Emotions.Reset();
        check(pet.Emotions.Current == PetEmotion.Neutral && pet.Emotions.SecondsWithoutAttention == 0,
            "Reset clears emotion, cooldown history, and neglect timer");
    }
}
