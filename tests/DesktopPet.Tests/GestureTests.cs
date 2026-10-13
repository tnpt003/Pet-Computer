using DesktopPet;
using DesktopPet.AI;

internal static class GestureTests
{
    public static void Run(Action<bool, string> check)
    {
        // Pending speech (a line that arrives while the pet cannot talk).
        var pet = new PetModel { Action = PetAction.Idle, Grounded = true, TreatRemaining = 2 };
        var speech = pet.Speech;
        check(!speech.Offer("treat_thanks:vi:1", "Ngon quá!", pet) && speech.PendingId == "treat_thanks:vi:1" && !speech.Visible,
            "A line that arrives while eating waits instead of being dropped");
        check(!speech.TryShowPending(pet), "The pending line stays hidden while the pet is still busy");
        pet.TreatRemaining = 0;
        speech.Step(pet, 1);
        check(speech.TryShowPending(pet) && speech.Visible && speech.Id == "treat_thanks:vi:1" && speech.PendingId == null,
            "The pending line appears as soon as the pet is free");
        check(!speech.Offer("petted:vi:2", "Purr", pet) && speech.PendingId == "petted:vi:2", "A line during another line waits for the gap");
        speech.Offer("thrown_rough:vi:3", "Đau!", pet);
        check(speech.PendingId == "thrown_rough:vi:3", "The newest pending line replaces an older one");
        speech.Step(pet, PetSpeech.PendingFor + 0.5);
        check(!speech.TryShowPending(pet) && speech.PendingId == null, "A pending line expires after 10 seconds");
        speech.Offer("x:vi:4", "Meo", new PetModel { Action = PetAction.Thrown });
        speech.DropPending();
        check(speech.PendingId == null, "The window can drop a pending line (sleep, fullscreen, setting off)");

        // The greeting survives a short nap; other lines do not get this longer wait.
        var napper = new PetModel { Action = PetAction.Sleeping, Grounded = true };
        var greet = napper.Speech;
        check(!greet.Offer("hello:vi:1", "Chào sen!", napper) && greet.PendingId == "hello:vi:1", "A greeting that arrives as the pet falls asleep waits");
        greet.Step(napper, 11.2);
        check(!greet.TryShowPending(napper) && greet.PendingId == "hello:vi:1", "The greeting stays hidden (and pending) while the pet sleeps");
        greet.Offer("wake_up:vi:2", "Ngáp~", napper);
        check(greet.PendingId == "hello:vi:1", "A wake-up line does not replace the waiting greeting");
        napper.Action = PetAction.Idle;
        greet.Step(napper, 0.5);
        check(greet.TryShowPending(napper) && greet.Id == "hello:vi:1", "The greeting appears once the pet is awake");
        var late = new PetModel { Action = PetAction.Sleeping, Grounded = true };
        late.Speech.Offer("welcome_back:en:1", "Missed you!", late);
        late.Speech.Step(late, PetSpeech.GreetingPendingFor + 0.5);
        late.Action = PetAction.Idle;
        check(!late.Speech.TryShowPending(late) && late.Speech.PendingId == null, "A greeting still expires after 30 seconds (long or away nap)");
        late.Speech.Offer("hello:en:2", "Hi!", new PetModel { Action = PetAction.Thrown });
        late.Speech.Offer("petted:en:3", "Purr", new PetModel { Action = PetAction.Thrown });
        check(late.Speech.PendingId == "petted:en:3", "A newer reaction line still replaces a pending greeting");
        check(PetSpeech.IsGreeting("hello:vi:1") && PetSpeech.IsGreeting("welcome_back:en:2") && !PetSpeech.IsGreeting("wake_up:vi:1") && !PetSpeech.IsGreeting("back:vi:1"),
            "Only hello/welcome_back count as the greeting");

        // Gesture rules.
        var gestures = new[] { PetAction.Kneading, PetAction.RollingOver, PetAction.ChasingTail, PetAction.Scratching, PetAction.Sneezing };
        check(gestures.All(g => PetActionRules.IsGesture(g) && PetActionRules.RejectsDecisions(g) && PetActionRules.IsCancellableLocal(g) &&
                                PetSettings.GestureDuration(g) > 0) && !PetActionRules.IsGesture(PetAction.Grooming),
            "Gestures own their duration, reject AI commands and are cancelled by input");
        check(AiProtocol.IsCommand("ROLL_OVER") && AiProtocol.IsCommand("CHASE_TAIL") && AiProtocol.IsCommand("SCRATCH") && !AiProtocol.IsCommand("KNEAD"),
            "Protocol accepts the three new Python commands; kneading stays a local reaction");

        var settings = new PetSettings();
        var behavior = new PetBehavior(settings, new Random(5));
        double Run(PetModel model, PetAction action)
        {
            double t = 0;
            while (model.Action == action && t < 20) { behavior.Step(model, 1.0 / 60); t += 1.0 / 60; }
            return t;
        }
        AiDecision Decision(string command) => new(command, 9, "playful");
        foreach (var (command, action) in new[] { ("ROLL_OVER", PetAction.RollingOver), ("CHASE_TAIL", PetAction.ChasingTail), ("SCRATCH", PetAction.Scratching) })
        {
            pet = new PetModel { Action = PetAction.Idle, Grounded = true };
            bool applied = behavior.TryApply(pet, Decision(command)) && pet.Action == action && pet.VX == 0;
            double lasted = Run(pet, action);
            check(applied && Math.Abs(lasted - PetSettings.GestureDuration(action)) < 0.05, $"{command} plays its fixed {PetSettings.GestureDuration(action)} s pose, not Python's duration");
        }
        pet = new PetModel { Action = PetAction.RollingOver, Grounded = true };
        check(!behavior.TryApply(pet, Decision("WALK_LEFT")) && pet.Action == PetAction.RollingOver, "AI cannot interrupt a gesture");
        behavior.Quiet = true;
        pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        check(!behavior.TryApply(pet, Decision("CHASE_TAIL")) && !behavior.TryApply(pet, Decision("ROLL_OVER")), "Quiet mode refuses autonomous gestures");
        check(behavior.StartKneading(pet) && pet.Action == PetAction.Kneading, "Petting can start kneading even in Quiet mode");
        check(Math.Abs(Run(pet, PetAction.Kneading) - PetSettings.KneadingDuration) < 0.05 && pet.Action == PetAction.Idle,
            "Kneading lasts its full duration in Quiet mode, then Idle");
        pet = new PetModel { Action = PetAction.Scratching, Grounded = true };
        behavior.Step(pet, 1.0 / 60);
        check(pet.Action == PetAction.Idle, "Turning Quiet on stops an autonomous gesture");
        behavior.Quiet = false;
        var sleeping = new PetModel { Action = PetAction.Sleeping, Grounded = true };
        sleeping.Emotions.EnterSleep();
        var flying = new PetModel { Action = PetAction.Thrown };
        check(!behavior.StartKneading(sleeping) && sleeping.Action == PetAction.Sleeping && !behavior.StartKneading(flying),
            "Kneading never wakes a sleeping pet or starts in the air");
        pet = new PetModel { Action = PetAction.Kneading, Grounded = true };
        behavior.WakeByClick(pet);
        check(PetActionRules.IsCancellableLocal(pet.Action), "Input handlers can cancel kneading back to Idle");

        var seen = new HashSet<PetAction>();
        var local = new PetBehavior(settings, new Random(11)) { Personality = "Playful" };
        var wanderer = new PetModel { Action = PetAction.Idle, Grounded = true, Y = 0 };
        for (int i = 0; i < 120 * 60 * 30; i++)
        {
            local.Step(wanderer, 1.0 / 60);
            seen.Add(wanderer.Action);
            if (wanderer.Action == PetAction.Sleeping) { wanderer.Emotions.FinishSleep(0); wanderer.Action = PetAction.Idle; }
            if (wanderer.Action is PetAction.Jumping) { wanderer.Action = PetAction.Idle; wanderer.Grounded = true; }
        }
        check(seen.Contains(PetAction.ChasingTail) && seen.Contains(PetAction.Scratching) && seen.Contains(PetAction.Sneezing),
            "Without Python the pet still chases its tail, scratches and sneezes now and then");

        var activity = new PetActivity();
        activity.Step(PetAction.ChasingTail, 2); activity.Step(PetAction.Scratching, 1); activity.Step(PetAction.Kneading, 3); activity.Step(PetAction.RollingOver, 1);
        check(activity.Playing == 3 && activity.Resting == 4, "Tail chasing and scratching count as play; kneading and rolling as rest");

        // Diary protocol.
        const string head = "{\"version\":1,\"id\":3,\"type\":\"decision\",\"command\":\"ROLL_OVER\",\"duration_seconds\":3,\"mood\":\"playful\",";
        string habits = "[" + string.Join(",", Enumerable.Range(0, 24).Select(h => h == 20 ? "12.5" : "0")) + "]";
        var withDiary = AiProtocol.Parse(head + "\"diary\":{\"days\":12,\"since\":\"2026-10-01\",\"petted\":40,\"treats\":9,\"play\":7,\"rough_throws\":1," +
            "\"clicks\":30,\"bond\":61.5,\"bond_history\":[50,55.5,61.5],\"habits\":" + habits + "}}", 3);
        check(withDiary.Command == "ROLL_OVER" && withDiary.Diary is { Days: 12, Since: "2026-10-01", Petted: 40, Treats: 9, Play: 7, RoughThrows: 1, Clicks: 30, Bond: 61.5 } d &&
              d.BondHistory.SequenceEqual(new[] { 50, 55.5, 61.5 }) && d.Habits.Length == 24 && d.Habits[20] == 12.5,
            "A decision carries the diary summary");
        foreach (string bad in new[] { "\"diary\":7", "\"diary\":{\"days\":0,\"since\":\"x\"}",
                     "\"diary\":{\"days\":2,\"since\":\"2026-10-01\",\"petted\":1,\"treats\":1,\"play\":1,\"rough_throws\":1,\"clicks\":1,\"bond\":1,\"bond_history\":[1],\"habits\":[1,2]}",
                     "\"diary\":{\"days\":2,\"since\":\"2026-10-01\",\"petted\":-1,\"treats\":1,\"play\":1,\"rough_throws\":1,\"clicks\":1,\"bond\":1,\"bond_history\":[1],\"habits\":" + habits + "}" })
        {
            var parsed = AiProtocol.Parse(head + bad + "}", 3);
            check(parsed.Command == "ROLL_OVER" && parsed.Diary == null, "An invalid diary is ignored without rejecting the decision: " + bad[..Math.Min(30, bad.Length)]);
        }
    }
}
