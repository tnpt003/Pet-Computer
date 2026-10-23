namespace DesktopPet;

// The one gate for "may the cat do this now": the table is PetAutonomy.BlockedBy (Core);
// this file only reads the window's state for the conditions a kind asks for.
public sealed partial class PetWindow
{
    private bool MayAct(AutoKind kind) => Blocking(PetAutonomy.BlockedBy(kind)) == AutoBlock.None;

    // Only the asked conditions are worked out (Feature reads many fields; Play needs none of them).
    private AutoBlock Blocking(AutoBlock asked)
    {
        var now = AutoBlock.None;
        if (Has(asked, AutoBlock.Paused) && paused) now |= AutoBlock.Paused;
        if (Has(asked, AutoBlock.Held) && (dragging || pressPending)) now |= AutoBlock.Held;
        if (Has(asked, AutoBlock.Unsettled) && (!PetActionRules.IsSettled(pet) || pet.Emotions.BlocksAutonomy)) now |= AutoBlock.Unsettled;
        if (Has(asked, AutoBlock.Quiet) && preferences.Quiet) now |= AutoBlock.Quiet;
        if (Has(asked, AutoBlock.Fullscreen) && context.Fullscreen) now |= AutoBlock.Fullscreen;
        if (Has(asked, AutoBlock.Battle) && BattleActive) now |= AutoBlock.Battle;
        if (Has(asked, AutoBlock.BattleScreens) && (battleHubOpen || battlePreparing || adventureMapOpen || expeditionMapOpen)) now |= AutoBlock.BattleScreens;
        if (Has(asked, AutoBlock.Away) && context.Away) now |= AutoBlock.Away;
        if (Has(asked, AutoBlock.WakeCooldown) && pet.Emotions.WakeCooldownRemaining > 0) now |= AutoBlock.WakeCooldown;
        if (Has(asked, AutoBlock.Feature) && now == AutoBlock.None && FeatureOwnsMovement()) now |= AutoBlock.Feature;
        return now;
    }

    private static bool Has(AutoBlock set, AutoBlock flag) => (set & flag) != 0;
}
