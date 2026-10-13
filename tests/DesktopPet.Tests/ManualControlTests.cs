using DesktopPet;

internal static class ManualControlTests
{
    public static void Run(Action<bool, string> check)
    {
        var settings = new PetSettings();

        // "Do now" actions skip cooldowns, bond and chance.
        var behavior = new PetBehavior(settings, new Random(3));
        var pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        check(behavior.StartManual(pet, PetAction.RollingOver) && pet.Action == PetAction.RollingOver,
            "Roll over starts at once without the bond or petting condition");
        double t = 0;
        while (pet.Action == PetAction.RollingOver && t < 10) { behavior.Step(pet, 1.0 / 60); t += 1.0 / 60; }
        check(pet.Action != PetAction.RollingOver && Math.Abs(t - PetSettings.GestureDuration(PetAction.RollingOver)) < 0.1,
            "A manual gesture keeps its normal length, then ends");
        check(behavior.StartManual(pet, PetAction.Grooming) && behavior.StartManual(pet, PetAction.Sneezing) && pet.Action == PetAction.Sneezing,
            "Self-care starts inside the shared cooldown, and a newer request replaces the running one");
        pet.Action = PetAction.Idle;
        check(behavior.StartManual(pet, PetAction.Stretching) && pet.Action == PetAction.Stretching,
            "Back-to-back self-care ignores the 45 s cooldown");
        check(!behavior.StartManual(new PetModel { Action = PetAction.Idle, Grounded = true }, PetAction.Walking),
            "Only listed actions can be requested");

        // Quiet mode does not cancel what the user asked for.
        var quiet = new PetBehavior(settings, new Random(4)) { Quiet = true };
        var still = new PetModel { Action = PetAction.Idle, Grounded = true };
        quiet.StartManual(still, PetAction.ChasingTail);
        quiet.Step(still, 0.5);
        check(still.Action == PetAction.ChasingTail, "Quiet mode keeps a requested gesture");
        still.Action = PetAction.Idle;
        quiet.StartManual(still, PetAction.Sitting);
        quiet.Step(still, 1);
        bool sat = still.Action == PetAction.Sitting;
        quiet.Step(still, PetBehavior.ManualPoseDuration);
        check(sat && still.Action == PetAction.Idle, "Quiet mode keeps a requested pose, then returns to Idle (no random action)");
        quiet.StartManual(still, PetAction.PreparingJump);
        quiet.Step(still, 0.05);
        bool crouched = still.Action == PetAction.PreparingJump;
        quiet.Step(still, PetSettings.JumpPreparationDuration);
        check(crouched && still.Action == PetAction.Jumping && !still.Grounded, "Quiet mode lets a requested jump take off");

        // Sleep lock, waking and flight are never cut short.
        var sleeper = new PetModel { Action = PetAction.Idle, Grounded = true };
        var sleepBehavior = new PetBehavior(settings, new Random(5));
        sleeper.Emotions.FinishSleep(30);
        check(sleepBehavior.StartManual(sleeper, PetAction.Sleeping) && sleeper.Emotions.SleepLocked,
            "Manual sleep skips the 30 s wake cooldown and uses the sleep lock");
        check(!sleepBehavior.StartManual(sleeper, PetAction.Grooming) && sleeper.Action == PetAction.Sleeping,
            "No request runs during the sleep lock");
        for (int i = 0; i < 60 * 11; i++) sleepBehavior.Step(sleeper, 1.0 / 60);
        check(sleeper.Action == PetAction.Waking && !sleeper.Emotions.SleepLocked, "Manual sleep still wakes by itself after ~10 s");
        check(!sleepBehavior.StartManual(sleeper, PetAction.Kneading) && sleeper.Action == PetAction.Waking,
            "Waking is not cut short by a request");
        check(!behavior.StartManual(new PetModel { Action = PetAction.Thrown, Grounded = false }, PetAction.Sitting) &&
              !behavior.StartManual(new PetModel { Action = PetAction.Dragging, Grounded = false }, PetAction.Sitting),
            "Requests never interrupt drag or flight");

        // Manual emotion control.
        var face = new PetModel { Action = PetAction.Idle, Grounded = true };
        var emotions = face.Emotions;
        check(!emotions.Perform(PetEmotion.Angry), "Emotions can only be chosen after turning on manual control");
        emotions.SetManualMode(true);
        check(emotions.Perform(PetEmotion.Angry) && !emotions.Perform(PetEmotion.Dizzy) && !emotions.Perform(PetEmotion.Sleepy),
            "Manual control offers Happy/Angry/Sad/Surprised/Normal, not Dizzy or Sleepy");
        for (int i = 0; i < 60; i++) emotions.Step(face, 1.0 / 60);
        check(emotions.Current == PetEmotion.Angry && emotions.Reported == PetEmotion.Neutral && !emotions.BlocksAutonomy,
            "A chosen emotion shows, is reported to Python as Neutral and does not freeze the pet");
        for (int i = 0; i < 60 * 400; i++) emotions.Step(face, 1.0 / 60);
        check(emotions.Current == PetEmotion.Angry && !emotions.SleepLocked,
            "The chosen emotion is held: no automatic sadness or sleepiness after minutes alone");
        for (int i = 0; i < 6; i++) emotions.Attention(click: true);
        emotions.Step(face, 0.1);
        check(emotions.ManualEmotion == PetEmotion.Angry && emotions.Target == PetEmotion.Angry,
            "Petting and click bursts do not override the chosen emotion");
        emotions.Perform(PetEmotion.Happy);
        face.Action = PetAction.Dragging;
        emotions.Step(face, 0.1);
        check(emotions.Target == PetEmotion.Neutral, "Dragging still takes priority over the chosen emotion");
        face.Action = PetAction.Sleeping;
        emotions.Step(face, 0.1);
        check(emotions.SleepLocked && emotions.Reported == PetEmotion.Sleepy, "Sleep is still Sleepy for Python under manual control");
        emotions.FinishSleep(30); face.Action = PetAction.Idle;
        for (int i = 0; i < 60; i++) emotions.Step(face, 1.0 / 60);
        check(emotions.ManualMode && emotions.Current == PetEmotion.Happy, "Manual control and the chosen emotion survive a nap");
        emotions.SetManualMode(false);
        for (int i = 0; i < 60; i++) emotions.Step(face, 1.0 / 60);
        check(emotions.Current == PetEmotion.Neutral && emotions.SecondsWithoutAttention < 2 && emotions.SecondsWithoutUser < 2,
            "Turning manual control off clears the emotion and restarts the alone clocks (no instant sadness or sleep)");
        emotions.Attention(click: true);
        for (int i = 0; i < 30; i++) emotions.Step(face, 1.0 / 60);
        check(emotions.Current == PetEmotion.Happy, "Automatic reactions are back after turning manual control off");
    }
}
