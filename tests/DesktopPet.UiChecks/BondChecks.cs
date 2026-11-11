using System.IO;
using System.Windows;
using DesktopPet;
using DesktopPet.AI;
using static PetPrivate;

// Bond milestones (docs/LIVING-CORNER.md → M3) in the real pet window: nothing without Python, the
// greeting back, the after-play routine, the hearts of a new milestone and napping snug against a thing.
internal static class BondChecks
{
    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        void FeatureStep(double dt = 1 / 60.0) => Call(window, "TickFeatures", dt);
        void Frame()
        {
            Call(window, "TickDesktopToys", 1 / 60.0); FeatureStep();
            if (!(bool)Call(window, "FeatureOwnsMovement")!) behavior.Step(pet, 1 / 60.0);
            pet.ActionTime += 1 / 60.0;
        }
        bool Until(Func<bool> done, int frames = 900) { for (int i = 0; i < frames && !done(); i++) Frame(); return done(); }
        var cooldown = typeof(PetBehavior).GetField("selfCareCooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        void CooldownOver() => cooldown.SetValue(behavior, 0.0);
        void Level(int level) => Call(window, "ApplyBondDecision", new AiDecision("IDLE", 3, "content", BondLevel: level));
        void Return(double away) { SetField(window, "awayPeak", away); Call(window, "WatchReturnForBond", true); }
        void ReadyAgain() { SetField(window, "greetReadyAt", double.NegativeInfinity); SetField(window, "afterPlayReadyAt", double.NegativeInfinity); }
        void Free() { pet.Action = PetAction.Idle; pet.VX = 0; pet.Grounded = true; }
        var bounds = Field<Area>(window, "bounds");
        double scale = Field<double>(window, "scale");
        double middle = (bounds.Left + bounds.Right) / 2;
        Point cursor = new((middle + 300) * scale, (bounds.Bottom - 60) * scale);
        SetField(window, "cursorProbe", new Func<Point>(() => cursor));
        window.Reset(); pet.Emotions.FinishSleep(0); pet.Emotions.Reset();
        preferences.Quiet = false; behavior.Quiet = false;
        pet.X = middle - PetSettings.CenterX; Free();
        Call(window, "ForgetBond");

        // Without Python (no milestone): nothing opens.
        Return(700);
        check(window.BondLevel == 0 && !window.GreetPending, "Without a milestone from Python the cat does not greet");
        Call(window, "PlayEnded");
        check(!window.AfterPlayPending, "...nor groom after play");

        // Familiar: a glance hello after ten minutes away, once, then not again for 30 minutes.
        Level(2);
        Return(300);
        check(window.BondLevel == 2 && !window.GreetPending, "Away five minutes: no greeting");
        Return(700);
        check(window.GreetPending, "Away more than ten minutes: a greeting waits");
        preferences.Quiet = true; behavior.Quiet = true;
        FeatureStep();
        check(window.BondActPhase == "None" && window.GreetPending, "In Quiet it waits (the cat's own doing)");
        preferences.Quiet = false; behavior.Quiet = false;
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        FeatureStep();
        check(window.BondActPhase == "None" && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "Asleep: not woken for it");
        behavior.WakeByClick(pet); pet.Emotions.FinishSleep(30); Free();
        FeatureStep();
        check(window.BondActPhase == "Glance" && pet.Action == PetAction.Looking && pet.Facing == 1, "Awake: a glance toward the user");
        check(Until(() => window.BondActPhase == "None") && pet.Action == PetAction.Idle, "...for a moment, then free");
        Return(700);
        check(!window.GreetPending, "Not again within 30 minutes");
        pet.Emotions.FinishSleep(0);

        // Very close: it comes to meet the user, then waves.
        Level(4); ReadyAgain(); Free();
        Return(700);
        FeatureStep();
        check(window.BondActPhase == "GreetWalk" && pet.Action == PetAction.Walking && pet.Facing == 1, "Very close: it walks toward the user");
        SetField(window, "pressPending", true);
        FeatureStep();
        SetField(window, "pressPending", false);
        check(window.BondActPhase == "None", "A click ends it at once");
        ReadyAgain(); Free();
        Return(700);
        FeatureStep();
        pet.X = middle + 300 - PetSettings.CenterX;
        FeatureStep();
        check(window.BondActPhase == "GreetWave" && pet.Action == PetAction.SeekingAttention, "Close to the user: a wave");
        check(Until(() => window.BondActPhase == "None"), "...then free");

        // Close: grooming, a stretch and a rest after a game ends by itself; not below that milestone.
        Free(); CooldownOver();
        Level(2);
        Call(window, "ForgetBond"); Level(2);
        Call(window, "PlayEnded");
        check(!window.AfterPlayPending, "Familiar: no after-play routine yet");
        Level(3);
        Call(window, "PlayEnded");
        check(window.AfterPlayPending, "Good friends: a game ending by itself starts the routine");
        FeatureStep();
        check(window.BondActPhase == "Groom" && pet.Action == PetAction.Grooming, "It grooms");
        check(Until(() => window.BondActPhase != "Groom") && window.BondActPhase == "Stretch" && pet.Action == PetAction.Stretching, "...then stretches");
        check(Until(() => window.BondActPhase is "RestSit" or "RestWalk"), "...then rests");
        check(Until(() => window.BondActPhase == "None") && pet.Action == PetAction.Idle, "...and is free again");
        Call(window, "PlayEnded");
        check(!window.AfterPlayPending, "Not again within five minutes");

        // A new milestone: hearts once the cat is free, not while asleep.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        Call(window, "ApplyBondDecision", new AiDecision("IDLE", 3, "content", BondLevel: 4, BondUp: 4));
        FeatureStep();
        check(window.HeartsTime < 0 && window.BondLevel == 4, "Asleep: the hearts wait");
        behavior.WakeByClick(pet); pet.Emotions.FinishSleep(0); Free();
        FeatureStep();
        check(window.HeartsTime >= 0, "Awake: a few hearts over its head");
        for (int i = 0; i < 20; i++) FeatureStep();
        Call(window, "Present", false);
        var renderer = Field<PetRenderer>(window, "renderer");
        check(renderer.HeartsTime >= 0, "The cat's drawing has them");
        Snapshot(window, "bond-hearts");
        for (int i = 0; i < 90; i++) FeatureStep();
        check(window.HeartsTime < 0, "...for a moment and a half");

        // Soulmates: a sleep it picks itself, snug against a toy nearby.
        window.ToggleToyBasket();
        window.TakeToyOut(DesktopToyRules.Box);
        for (int i = 0; i < 240; i++) Call(window, "TickDesktopToys", 1 / 60.0);
        var box = window.ToyMotionOf(DesktopToyRules.Box)!;
        box.X = middle + 150; box.VX = box.VY = 0; box.Height = 0;
        for (int i = 0; i < 120; i++) Call(window, "TickDesktopToys", 1 / 60.0);
        Free(); pet.X = middle - PetSettings.CenterX; pet.Emotions.FinishSleep(0);
        check(!(bool)Call(window, "GoLeanNap")!, "Best friends: no snug nap yet");
        Level(5);
        check((bool)Call(window, "GoLeanNap")! && window.GoingToNap, "Soulmates: the cat goes to nap by a thing nearby");
        pet.X = Field<double>(window, "leanX");
        FeatureStep();
        check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && !window.NappingOnCushion && !window.NappingInBox && pet.Facing == 1,
            "There it falls asleep right by it, facing it: an ordinary sleep");
        behavior.WakeByClick(pet);
        check(pet.Action == PetAction.Waking, "A click wakes it as always");
        pet.Emotions.FinishSleep(0); Free();
        Call(window, "ForgetBond");
        check(window.BondLevel == 0 && !(bool)Call(window, "GoLeanNap")!, "Python gone: nothing open any more");
        window.ToggleToyBasket();
        SetField(window, "cursorProbe", null);
        window.Reset();
    }

    private static void Snapshot(PetWindow window, string name)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not { Length: > 0 } folder) return;
        var renderer = Field<PetRenderer>(window, "renderer");
        renderer.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)renderer.ActualWidth * 2, (int)renderer.ActualHeight * 2, 192, 192, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(renderer);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }
}
