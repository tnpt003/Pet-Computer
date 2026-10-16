using DesktopPet;
using DesktopPet.AI;

// Regression checks for features interacting (review feedback 2026-10-15).
internal static class CombinationTests
{
    public static void Run(Action<bool, string> check)
    {
        var settings = new PetSettings();
        AiDecision Decision(string command) => new(command, 4, "content");

        // The shared start condition matches the conditions it replaced in every state.
        bool sameRules = true;
        foreach (PetAction action in Enum.GetValues<PetAction>())
            foreach (bool grounded in new[] { true, false })
                foreach (bool asleep in new[] { true, false })
                {
                    var state = new PetModel { Action = action, Grounded = grounded };
                    if (asleep) state.Emotions.EnterSleep();
                    bool old = grounded && !asleep && action is not (PetAction.Sleeping or PetAction.Waking or PetAction.Dragging or
                        PetAction.Thrown or PetAction.Jumping or PetAction.PreparingJump);
                    sameRules &= PetActionRules.IsSettled(state) == old &&
                        PetActionRules.IsInFlight(action) == (action is PetAction.Thrown or PetAction.Jumping or PetAction.PreparingJump) &&
                        PetBehavior.CanStartManual(state, PetAction.Kneading) == (old && !state.Emotions.BlocksAutonomy) &&
                        PetSpeech.CanShow(state) == (old && state.TreatRemaining <= 0);
                }
        check(sameRules, "Shared start rules (IsSettled/IsInFlight) match the old per-feature conditions for every action");

        // Quiet keeps Python informed but never moves the cat.
        var quiet = new PetBehavior(settings, new Random(8)) { Quiet = true };
        int requests = 0;
        quiet.DecisionRequested += () => requests++;
        var pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        bool stayedStill = true;
        for (int i = 0; i < 60 * 60; i++)
        {
            quiet.Step(pet, 1.0 / 60);
            stayedStill &= pet.Action == PetAction.Idle && pet.VX == 0;
        }
        check(requests >= 8 && stayedStill, $"60 s of Quiet still sends observations to Python ({requests}) while the cat stays still");
        check(new[] { "SIT", "SLEEP", "LOOK", "IDLE", "WALK_LEFT", "JUMP" }.All(c => !quiet.TryApply(pet, Decision(c))) && pet.Action == PetAction.Idle,
            "While Quiet, no AI command is applied (the cat does nothing on its own)");
        quiet.Quiet = false;
        check(quiet.TryApply(pet, Decision("SIT")) && pet.Action == PetAction.Sitting, "Without Quiet, AI commands apply again");

        // Waking keeps its 1.2 s even if the behavior is reset meanwhile (AI toggle, click, Quiet).
        double WakingLength(Action<PetBehavior, PetModel> midway)
        {
            var behavior = new PetBehavior(settings, new Random(9));
            var sleeper = new PetModel { Action = PetAction.Sleeping, Grounded = true };
            sleeper.Emotions.EnterSleep();
            behavior.WakeByClick(sleeper);
            double t = 0;
            bool done = false;
            while (sleeper.Action == PetAction.Waking && t < 5)
            {
                if (!done && t >= 0.8) { midway(behavior, sleeper); done = true; }
                behavior.Step(sleeper, 1.0 / 60); t += 1.0 / 60;
            }
            return t;
        }
        double plain = WakingLength((_, _) => { });
        double reset = WakingLength((b, p) => b.Reset(p));
        check(Math.Abs(plain - PetSettings.WakingDuration) < 0.05 && Math.Abs(reset - plain) < 0.05,
            $"A reset during waking (AI toggle, click, Quiet) does not stretch it ({plain:F2} s vs {reset:F2} s)");
        var idle = new PetBehavior(settings, new Random(10));
        var awake = new PetModel { Action = PetAction.Idle, Grounded = true };
        idle.Reset(awake);
        double firstChoice = 0;
        while (awake.Action == PetAction.Idle && firstChoice < 5) { idle.Step(awake, 1.0 / 60); firstChoice += 1.0 / 60; }
        check(firstChoice < 2.2, "Outside waking, a reset still schedules the next action within ~2 s");
    }
}
