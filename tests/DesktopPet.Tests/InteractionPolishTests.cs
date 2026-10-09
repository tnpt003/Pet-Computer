using DesktopPet;
internal static class InteractionPolishTests
{
    public static void Run(Action<bool, string> check)
    {
        var behavior = new PetBehavior(new PetSettings());
        var pet = new PetModel { Grounded = true };
        PetAction? previous = null;
        bool varied = true;
        for (int i = 0; i < 200; i++)
        {
            pet.Action = PetAction.Idle; pet.Grounded = true; pet.Emotions.Reset();
            behavior.Reset(); behavior.Step(pet, 3);
            varied &= pet.Action != previous; previous = pet.Action;
        }
        check(varied, "Offline choices avoid consecutive repeated activities across resets");
        pet.Action = PetAction.Idle; pet.Grounded = true; pet.Emotions.Reset();
        check(behavior.TryApply(pet, new("SEEK_ATTENTION", 2, "bored"))
            && pet.Action == PetAction.SeekingAttention && pet.Grounded && pet.VX == 0 && pet.VY == 0,
            "Attention gesture stays planted instead of jumping");
        check(!behavior.TryApply(pet, new("WALK_LEFT", 2, "content")), "Delayed AI cannot replace an attention gesture");
        behavior.Step(pet, 2.5);
        check(pet.Action != PetAction.SeekingAttention, "Attention gesture completes without getting stuck");
        pet.Action = PetAction.Idle; pet.Grounded = true; behavior.Reset();
        check(!behavior.TryApply(pet, new("SEEK_ATTENTION", 2, "bored")), "Attention cooldown survives behavior reset");
        pet.Emotions.Reset(); pet.Action = PetAction.Idle;
        bool petted = false;
        for (int i = 0; i < 12; i++) petted |= pet.Emotions.Step(pet, 0.1, 70 + (i % 2) * 3, 45, true);
        check(petted && pet.Emotions.PettingLean > 0 && pet.Emotions.Current == PetEmotion.Happy,
            "Gentle strokes produce happy expression and a directional head lean");
        pet.Action = PetAction.Dragging; pet.Emotions.Step(pet, 0.1, 70, 45, true);
        check(pet.Emotions.PettingLean == 0, "Pickup clears petting pose immediately");
        pet.Emotions.EnterSleep();
        check(!pet.Emotions.Step(pet, 0.1, 80, 45, true) && pet.Emotions.PettingLean == 0,
            "Petting does not bypass sleep lock");
        foreach (string cue in new[] { "meow", "purr", "sleep", "excited", "surprised", "annoyed" })
        {
            byte[] first = PetSounds.CreateWave(cue); Array.Clear(first);
            check(PetSounds.CreateWave(cue)[0] == (byte)'R', "Volume buffers do not mutate cached " + cue);
        }
    }
}
