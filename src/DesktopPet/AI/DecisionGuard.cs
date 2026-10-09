namespace DesktopPet.AI;

public static class DecisionGuard
{
    public static bool CanApply(long requestedRevision, long currentRevision, double ageSeconds,
        bool paused, PetModel pet) => requestedRevision == currentRevision && ageSeconds <= 2.5 &&
        !paused && pet.Grounded && pet.Action is not (PetAction.Dragging or PetAction.Thrown or PetAction.Jumping or PetAction.PreparingJump or PetAction.Sleeping or PetAction.Waking or PetAction.Grooming or PetAction.Stretching or PetAction.SeekingAttention);
}
