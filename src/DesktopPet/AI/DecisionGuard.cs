namespace DesktopPet.AI;

public static class DecisionGuard
{
    public static bool CanApply(long requestedRevision, long currentRevision, double ageSeconds,
        bool paused, PetModel pet) => requestedRevision == currentRevision && ageSeconds <= 2.5 &&
        !paused && pet.Grounded && !PetActionRules.RejectsDecisions(pet.Action);
}
