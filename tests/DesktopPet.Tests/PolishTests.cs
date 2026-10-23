using DesktopPet;
using DesktopPet.AI;

internal static class PolishTests
{
    public static void Run(Action<bool, string> check)
    {
        var settings = new PetSettings();
        var behavior = new PetBehavior(settings);
        var physics = new PetPhysics(settings);
        var pet = new PetModel { Grounded = true, X = 300, Y = 928 };
        var bounds = new Area(0, 0, 1920, 1040);
        behavior.TryApply(pet, new("WALK_RIGHT", 6, "content"));
        physics.Step(pet, bounds, 1.0 / 120);
        check(pet.VX > 0 && pet.VX < 5, "Walking accelerates gently from rest");
        for (int i = 0; i < 120; i++)
        {
            behavior.Step(pet, 1.0 / 120);
            physics.Step(pet, bounds, 1.0 / 120);
        }
        check(Math.Abs(pet.VX - settings.WalkSpeed) < 0.01, "Walking reaches its intended cruising speed");
        pet.X = bounds.Right - settings.Width - 18;
        double startSpeed = pet.VX;
        bool slowed = false, turned = false, inside = true;
        for (int i = 0; i < 180; i++)
        {
            behavior.Step(pet, 1.0 / 120);
            physics.Step(pet, bounds, 1.0 / 120);
            slowed |= pet.VX > 0 && pet.VX < startSpeed / 2;
            turned |= pet.VX < 0;
            inside &= pet.X <= bounds.Right - settings.Width;
        }
        check(slowed && turned && inside, "Walking slows before turning inward at the edge");

        pet.Action = PetAction.Sleeping;
        pet.Emotions.EnterSleep();
        behavior.WakeByClick(pet);
        behavior.Step(pet, 2);
        check(!behavior.TryApply(pet, new("SLEEP", 8, "sleepy")), "Wake cooldown blocks Python sleep recommendations");
        bool stayedAwake = true;
        for (int i = 0; i < 100; i++)
        {
            pet.Action = PetAction.Idle;
            pet.Grounded = true;
            behavior.Reset();
            behavior.Step(pet, 3);
            stayedAwake &= pet.Action != PetAction.Sleeping;
        }
        check(stayedAwake, "Local random choices respect the same wake cooldown");
        pet.Action = PetAction.Idle;
        pet.Grounded = true; // A preceding random jump may have left this test fixture airborne.
        pet.Emotions.Step(pet, settings.WakeCooldownSeconds + 0.1);
        check(behavior.TryApply(pet, new("SLEEP", 8, "sleepy")), "Sleep is available again after wake cooldown expires");

        var reply = AiProtocol.Parse("{\"version\":1,\"id\":1,\"type\":\"decision\",\"command\":\"IDLE\",\"duration_seconds\":2,\"mood\":\"content\",\"energy\":72.5}", 1);
        check(reply.Energy == 72.5, "Debug energy is read from optional AI telemetry");
        pet.Emotions.Reset(); pet.Action = PetAction.Idle; pet.Grounded = true;
        behavior.Quiet = true;
        check(!behavior.TryApply(pet, new("JUMP", 2, "playful")) && !behavior.TryApply(pet, new("WALK_LEFT", 3, "content")),
            "Quiet mode rejects AI locomotion");
        pet.Action = PetAction.Walking; pet.VX = 72;
        behavior.Step(pet, 4);
        check(pet.Action == PetAction.Idle && pet.VX == 0, "Quiet mode stops grounded autonomous walking");
        pet.Action = PetAction.Thrown; pet.Grounded = false; pet.VX = 200;
        behavior.Step(pet, 1);
        check(pet.Action == PetAction.Thrown && pet.VX == 200, "Quiet mode preserves user throw physics");
        var directory = Path.Combine(Path.GetTempPath(), "PetPreferences-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "preferences.json");
            var preferences = new PetPreferences { Name = "Luna", Personality = "Curious", Quiet = true, RestX = -400, RestY = 900,
                Tuning = new PersonalityTuning { Playfulness = 0.24, AttentionAfterSeconds = 95 },
                Volume = 0.35, EmotionSounds = false, PositionMonitor = "test-monitor", PositionX = 0.25, RestMonitor = "test-monitor", RestFractionX = 0.6 };
            check(preferences.Save(file), "Preferences can be saved atomically");
            var loaded = PetPreferences.Load(file);
            check(loaded.Name == "Luna" && loaded.Personality == "Curious" && loaded.Quiet && loaded.RestX == -400 && !loaded.Sounds,
                "Name, personality, quiet and resting spot survive restart; sounds default off");
            check(loaded.Tuning?.Playfulness == 0.24 && loaded.Tuning.AttentionAfterSeconds == 95, "Personality tuning persists without editing packaged JSON");
            File.WriteAllText(file, "broken");
            check(loaded.Volume == 0.35 && !loaded.EmotionSounds && loaded.InteractionSounds && loaded.SleepSounds
                && loaded.PositionMonitor == "test-monitor" && loaded.PositionX == 0.25 && loaded.RestFractionX == 0.6,
                "Sound categories, volume and monitor-relative positions survive restart");
            check(PetPreferences.Load(file).Name == "Mochi", "Corrupt preferences recover with safe defaults");
        }
        finally { Directory.Delete(directory, true); }
        var cues = new[] { "purr", "meow", "sleep", "excited", "surprised", "annoyed" }.Select(kind => PetSounds.CreateWave(kind, 10)).ToArray();
        check(cues.All(wave => wave.Length > 8000 && wave.Length < 140000 && BitConverter.ToInt32(wave, 40) == wave.Length - 44 && System.Text.Encoding.ASCII.GetString(wave, 0, 4) == "RIFF" && BitConverter.ToInt32(wave, 24) == 22050),
            "All six cartoon cues produce valid short PCM audio from embedded recordings");
        check(!cues[0].SequenceEqual(cues[1]) && !cues[1].SequenceEqual(cues[2]), "Purr, meow and sleepy cues have distinct waveforms");
        check(!PetSounds.CreateWave("meow", 1).SequenceEqual(PetSounds.CreateWave("meow", 2)), "Cartoon cues vary voice excerpt, pitch and volume");
        check(cues.All(wave => wave.Length <= 44 + 22050 * 2 * 1.2 && BitConverter.ToInt16(wave, 44) == 0 && BitConverter.ToInt16(wave, wave.Length - 2) == 0),
            "Every cartoon cue stays under 1.2 seconds with smooth silent endpoints");
        check(PetSounds.Gain(0) == 0 && PetSounds.Gain(-1) == 0 && PetSounds.Gain(double.NaN) == 0 && PetSounds.Gain(1) == 1 && PetSounds.Gain(0.5) <= 0.3,
            "The volume curve: 0 is silent, 100% is full, half way is clearly quieter (not half the amplitude)");
        check(cues.Concat(new[] { PetSounds.CreateWave("swoosh", 1) }).All(wave => Math.Abs(PetSounds.Seconds(wave) - (wave.Length - 44) / 44100.0) < 1e-9),
            "A cue lasts its data over the 22,050 Hz byte rate (the next cue waits for all of it)");
        var ball = new ToyBall { X = 300 };
        ball.Push(18); check(ball.VX < 0, "Click right of ball pushes left");
        double strong = Math.Abs(ball.VX);
        ball.Push(5); check(Math.Abs(ball.VX) < strong, "Off-center distance controls horizontal strength");
        ball.Push(-18); check(ball.VX > 0, "Click left of ball pushes right");
        ball.Push(0); check(ball.VX == 0 && ball.VY > 0, "Centered click gives a centered upward bounce");
        ball.Bat(1);
        bool bounced = false; double lastVY = ball.VY;
        for (int i = 0; i < 1800; i++)
        {
            ball.Step(1.0 / 120, 0, 400);
            bounced |= lastVY < 0 && ball.VY > 0; lastVY = ball.VY;
        }
        check(bounced && ball.Height == 0 && ball.VX == 0 && ball.X >= 0 && ball.X <= 400 && ball.Rotation != 0,
            "Ball bounces, rolls, collides and eventually settles");
        var eating = new PetModel { Grounded = true, Action = PetAction.Sitting, TreatRemaining = 4 };
        eating.Emotions.Step(eating, 121, -200, 50);
        eating.Emotions.Step(eating, 0.05, 56, 50);
        check(eating.Emotions.Current == PetEmotion.Neutral && !eating.Emotions.SleepLocked,
            "Eating suppresses cursor surprise and inactivity sleep until its interaction finishes");
    }
}
