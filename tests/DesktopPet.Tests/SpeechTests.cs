using DesktopPet;
using DesktopPet.AI;

internal static class SpeechTests
{
    public static void Run(Action<bool, string> check)
    {
        const string head = "{\"version\":1,\"id\":7,\"type\":\"decision\",\"command\":\"IDLE\",\"duration_seconds\":2,\"mood\":\"content\",";
        var spoken = AiProtocol.Parse(head + "\"speech\":{\"id\":\"petted:vi:1a2b3c4d\",\"text\":\"Purr~ chỗ đó đó sen!\"}," +
            "\"learned\":{\"likes\":[\"JUMP @day/ok +0.60\"],\"dislikes\":[]}}", 7);
        check(spoken.Speech == new PetLine("petted:vi:1a2b3c4d", "Purr~ chỗ đó đó sen!") &&
              spoken.Learned is { Likes: ["JUMP @day/ok +0.60"], Dislikes: [] }, "Decision carries a Vietnamese line and learned summary");
        var control = AiProtocol.Parse(head + "\"speech\":{\"id\":\"x\",\"text\":\"line\\nbreak\\u001b\"}}", 7);
        check(control.Speech?.Text == "linebreak", "Control characters are stripped from displayed speech");
        var tooLong = AiProtocol.Parse(head + "\"speech\":{\"id\":\"x\",\"text\":\"" + new string('a', 200) + "\"},\"learned\":7}", 7);
        check(tooLong.Command == "IDLE" && tooLong.Speech == null && tooLong.Learned == null, "Oversized or malformed speech extras are ignored, not fatal");

        string json = System.Text.Json.JsonSerializer.Serialize(new PetObservation("Idle", true, 1, 1, "x", "LOCAL",
            SpeechEnabled: true, Language: "en", Chattiness: 2, PetName: "Bơ", Quiet: true,
            LastSpeech: new SpeechReport("hello:en:00ff00ff", 3.25), LearningReset: "r1"), AiProtocol.Json);
        check(json.Contains("\"speech_enabled\":true") && json.Contains("\"language\":\"en\"") && json.Contains("\"chattiness\":2") &&
              json.Contains("\"quiet\":true") && json.Contains("\"last_speech\":{\"id\":\"hello:en:00ff00ff\",\"age\":3.25}") &&
              json.Contains("\"learning_reset\":\"r1\"") && json.Contains("\"pet_name\":"), "Observation sends speech settings and the last shown line");

        var pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        var speech = pet.Speech;
        check(speech.TryShow("a", "Meo~", pet) && speech.Visible && speech.Duration == PetSpeech.DurationFor("Meo~") && speech.Duration < 3,
            "A short line shows briefly, slightly longer than the minimum");
        check(!speech.TryShow("b", "Another", pet), "Only one line is shown at a time");
        speech.Step(pet, speech.Duration + 0.01);
        check(!speech.Visible && speech.LastShown is { Id: "a" }, "The line disappears but is still reported for learning");
        check(!speech.TryShow("b", "Too soon", pet), "Lines are separated by a minimum gap");
        speech.Step(pet, PetSpeech.MinGap);
        check(speech.TryShow("c", new string('x', 100), pet) && speech.Duration == PetSpeech.MaxDuration, "Long lines stay longer, up to a cap");
        pet.Action = PetAction.Dragging; pet.Grounded = false;
        speech.Step(pet, 0.01);
        check(!speech.Visible, "Picking the pet up hides its speech immediately");
        speech.Step(pet, 40);
        check(speech.LastShown == null, "Old lines are no longer reported after 30 seconds");
        pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        pet.Emotions.AggressiveInteraction();
        pet.Emotions.Step(pet, 0.1);
        check(PetSpeech.CanShow(pet), "A short reaction may be spoken while the pet is upset");
        foreach (var action in new[] { PetAction.Sleeping, PetAction.Waking, PetAction.Thrown, PetAction.Jumping, PetAction.PreparingJump })
        {
            pet = new PetModel { Action = action, Grounded = action is not (PetAction.Thrown or PetAction.Jumping) };
            check(!PetSpeech.CanShow(pet), $"No speech while {action}");
        }
        pet = new PetModel { Action = PetAction.Idle, Grounded = true, TreatRemaining = 1 };
        check(!PetSpeech.CanShow(pet), "No speech while eating");
        pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        pet.Emotions.EnterSleep();
        check(!pet.Speech.TryShow("z", "Zzz", pet), "Speech never breaks the sleep lock");

        string file = Path.Combine(Path.GetTempPath(), "pet-prefs-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(file, "{\"Language\":\"fr\",\"Chattiness\":9}");
            var loaded = PetPreferences.Load(file);
            check(loaded.Language == "en" && loaded.Chattiness == 2 && loaded.Speech, "Preferences normalize language (unknown -> English) and chattiness, and default to talking");
            File.WriteAllText(file, "{\"Sounds\":true}");
            loaded = PetPreferences.Load(file);
            check(loaded.Speech && loaded.Language == "en" && loaded.Chattiness == 1 && loaded.LearningReset == "",
                "Older preference files get the speech defaults");
        }
        finally { File.Delete(file); }
    }
}
