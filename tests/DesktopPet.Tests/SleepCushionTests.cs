using DesktopPet;

// The slime cushion's pull on sleep (PetBehavior.SleepElsewhere), and a bonked cat not startled by the cursor.
internal static class SleepCushionTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        var settings = new PetSettings();
        var physics = new PetPhysics(settings);
        var bounds = new Area(0, 0, 1920, 1040);

        // Ten minutes of local behavior (attention every minute so it never gets sleepy): how often it
        // picks a sleep of its own, with and without somewhere else to sleep.
        (int Asked, bool SleptHere) Live(bool elsewhere)
        {
            var pet = new PetModel { X = 900, Y = bounds.Bottom - settings.Height, Grounded = true, Action = PetAction.Idle };
            var behavior = new PetBehavior(settings);
            int asked = 0;
            bool slept = false;
            if (elsewhere) behavior.SleepElsewhere = _ => { asked++; return true; };
            for (double t = 0; t < 600; t += dt)
            {
                if (t % 60 < dt) pet.Emotions.Attention();
                pet.Emotions.Step(pet, dt);
                behavior.Step(pet, dt, null);
                physics.Step(pet, bounds, dt);
                slept |= pet.Action == PetAction.Sleeping;
                if (pet.Action == PetAction.Sleeping) { behavior.WakeByClick(pet); }
            }
            return (asked, slept);
        }
        var plain = Live(false);
        check(plain.SleptHere, "Without a cushion the cat now and then falls asleep where it stands");
        var cushion = Live(true);
        check(cushion.Asked > 0 && !cushion.SleptHere, $"With somewhere else to sleep, each sleep of its own goes there instead ({cushion.Asked} times)");

        // Bonked: a fast cursor right by the cat does not startle it.
        PetEmotion Swipe(double bonk)
        {
            var pet = new PetModel { X = 500, Y = 500, Grounded = true, Action = PetAction.Sitting, Bonk = bonk };
            for (int i = 0; i < 5; i++) pet.Emotions.Step(pet, dt, 556, 550);
            for (int i = 0; i < 30; i++) pet.Emotions.Step(pet, dt, 556 + (i % 2 == 0 ? 60 : -60), 550);
            return pet.Emotions.Target;
        }
        check(Swipe(0) == PetEmotion.Surprised, "(control) a fast cursor next to the cat startles it");
        check(Swipe(PetModel.BonkDuration) != PetEmotion.Surprised, "While bonked (seeing stars) a fast cursor does not startle it");
    }
}
