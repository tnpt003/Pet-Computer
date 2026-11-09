using DesktopPet;

internal static class FocusTests
{
    public static void Run(Action<bool, string> check)
    {
        var focus = new PetFocus();
        check(!focus.Running && focus.Step(5) == FocusEvent.None, "No session: the clock does nothing");
        check(!focus.Start(10) && !focus.Running, "Only 25 and 50 minutes can be started");
        check(focus.Start(25) && focus.Focusing && focus.Remaining == 1500, "A session starts with its whole length");
        check(!focus.Start(50) && focus.Minutes == 25, "A running session cannot be replaced");
        focus.Step(600);
        check(focus.Remaining == 1500, "A gap longer than the limit (the computer slept) is not counted");
        focus.Step(0); focus.Step(double.NaN); focus.Step(-3);
        check(focus.Remaining == 1500, "Pause (0) and broken values leave the clock alone");
        check(focus.Step(10) == FocusEvent.None && focus.Remaining == 1490, "Real time counts down");

        for (int i = 0; i < 148; i++) focus.Step(10);
        check(focus.Focusing && focus.LineWaiting == FocusEvent.None && !focus.ChimeWaiting, "Nothing is announced before the end");
        check(focus.Step(10) == FocusEvent.Done && focus.Phase == FocusPhase.Resting && focus.Remaining == 300, "The end of 25 minutes starts a rest of 5");
        check(focus.LineWaiting == FocusEvent.Done && focus.ChimeWaiting, "The chime and the line wait to be delivered");
        focus.ChimeStep(false, 2);
        check(focus.ChimeWaiting, "The chime keeps trying for a moment");
        focus.ChimeStep(false, PetFocus.ChimeGiveUp);
        check(!focus.ChimeWaiting, "It gives up rather than ring long after");
        focus.Stop(); focus.Start(25);
        for (int i = 0; i < 150; i++) focus.Step(10);
        focus.ChimeStep(true, 1);
        check(!focus.ChimeWaiting, "A chime that rang is done");

        for (int i = 0; i < 29; i++) focus.Step(10);
        check(focus.Phase == FocusPhase.Resting && focus.LineWaiting == FocusEvent.Done, "The news waits through the rest");
        check(focus.Step(10) == FocusEvent.RestOver && !focus.Running && focus.LineWaiting == FocusEvent.RestOver, "The rest ends by itself and its news replaces the older one");
        focus.LineSaid();
        check(focus.LineWaiting == FocusEvent.None, "A line said is done");
        check(!focus.Focusing && focus.Step(10) == FocusEvent.None, "No new session starts by itself");

        check(focus.Start(50) && PetFocus.RestMinutes(50) == 10 && PetFocus.RestMinutes(25) == 5, "A long session earns a longer rest");
        focus.Stop();
        check(!focus.Running && focus.Remaining == 0 && focus.LineWaiting == FocusEvent.None && !focus.ChimeWaiting, "Stopping leaves nothing behind");
        focus.Start(25);
        for (int i = 0; i < 150; i++) focus.Step(10);
        check(focus.Phase == FocusPhase.Resting && focus.Start(25) && focus.Focusing && focus.LineWaiting == FocusEvent.None, "A rest can be cut short by the next session, and its news goes with it");
        focus.Stop();

        foreach (var language in new[] { PetLanguage.Vietnamese, PetLanguage.English, PetLanguage.Japanese })
        foreach (var kind in new[] { FocusEvent.Done, FocusEvent.RestOver })
        {
            var (id, text) = PetFocus.LocalLine(kind, language, "Bông", new Random(1));
            check(PetFocus.IsFocusLine(id) && PetFocus.IsDoneLine(id) == (kind == FocusEvent.Done) && !PetBreaks.IsBreakLine(id) && !text.Contains("{name}") && text.Length > 5, $"The {kind} line in {language} has its own id and a filled name");
        }
        check(!PetFocus.IsFocusLine("break:local:vi:0") && !PetFocus.IsFocusLine(null), "Other lines are not focus lines");
    }
}
