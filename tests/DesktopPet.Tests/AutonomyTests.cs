using DesktopPet;

internal static class AutonomyTests
{
    public static void Run(Action<bool, string> check)
    {
        bool Blocks(AutoKind kind, AutoBlock block) => !PetAutonomy.MayAct(kind, block);
        var kinds = Enum.GetValues<AutoKind>();
        foreach (var kind in kinds) _ = PetAutonomy.BlockedBy(kind); // Every kind has a row (no throw).
        check(kinds.All(k => PetAutonomy.MayAct(k, AutoBlock.None)), "Nothing blocks any kind when no condition holds");

        // What the cat does on its own stops in Quiet and with a fullscreen app (AGENTS.md).
        var own = new[] { AutoKind.LocalBehavior, AutoKind.RestVisit, AutoKind.Admire, AutoKind.ShoveKeepsake, AutoKind.CushionNap, AutoKind.ClimbLedge };
        check(own.All(k => Blocks(k, AutoBlock.Quiet) && Blocks(k, AutoBlock.Fullscreen)), "Autonomous kinds stop in Quiet and with a fullscreen app");
        // The user asked: Quiet does not stop it; a fullscreen app still stops the games.
        var asked = new[] { AutoKind.Treat, AutoKind.UserGame, AutoKind.CallToLook };
        check(asked.All(k => !Blocks(k, AutoBlock.Quiet)), "Feeding, ball/laser and calling the cat to look still work in Quiet");
        check(Blocks(AutoKind.UserGame, AutoBlock.Fullscreen) && !Blocks(AutoKind.Treat, AutoBlock.Fullscreen), "Fullscreen stops ball/laser, not feeding (unchanged)");

        // A running fight owns the cat: every kind but PetBehavior's (not ticked in a fight) and thoughts stops.
        check(kinds.Where(k => k is not (AutoKind.Play or AutoKind.LocalBehavior or AutoKind.Stretch or AutoKind.ClimbLedge or AutoKind.Thought))
            .All(k => Blocks(k, AutoBlock.Battle)), "A running battle blocks pet features");
        check(Blocks(AutoKind.LocalBehavior, AutoBlock.BattleScreens) && !Blocks(AutoKind.LocalBehavior, AutoBlock.Battle),
            "The battle hub/maps keep local behavior still, as ApplyQuiet did");

        // Being held, paused or asleep stops everything the cat would start itself.
        foreach (var block in new[] { AutoBlock.Paused, AutoBlock.Held, AutoBlock.Unsettled })
            check(kinds.Where(k => k is not (AutoKind.LocalBehavior or AutoKind.Ledges or AutoKind.Speech or AutoKind.Thought or AutoKind.AwayNap or AutoKind.BreakReminder))
                .All(k => Blocks(k, block)), $"{block} stops every play kind");
        check(Blocks(AutoKind.AwayNap, AutoBlock.Held) && !Blocks(AutoKind.AwayNap, AutoBlock.Unsettled) && !Blocks(AutoKind.AwayNap, AutoBlock.Paused),
            "Away nap waits for the drag only (it may start while settled or not, as before)");
        check(Blocks(AutoKind.BreakReminder, AutoBlock.Away) && !Blocks(AutoKind.BreakReminder, AutoBlock.Quiet), "Break reminders wait for the user, not for Quiet");
        check(Blocks(AutoKind.CushionNap, AutoBlock.WakeCooldown) && !Blocks(AutoKind.Admire, AutoBlock.WakeCooldown), "Only the cushion nap waits for the wake cooldown");
        check(Blocks(AutoKind.Admire, AutoBlock.Feature) && !Blocks(AutoKind.CallToLook, AutoBlock.Feature), "Admiring on its own waits for other features; a double click does not");
        check(Blocks(AutoKind.Ledges, AutoBlock.Battle) && !Blocks(AutoKind.Ledges, AutoBlock.Quiet), "Ledges are ground in Quiet, not in a battle");
    }
}
