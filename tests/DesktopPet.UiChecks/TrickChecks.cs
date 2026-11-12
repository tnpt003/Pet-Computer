using System.IO;
using System.Windows;
using DesktopPet;
using static PetPrivate;

// Calling the cat and teaching it tricks (docs/LIVING-CORNER.md → M5) in the real pet window: the call along
// the floor, from another monitor, asleep and in Quiet; a lesson counted only with a stroke, the daily limit,
// learned tricks in "Do now", and the drawings of the three tricks.
internal static class TrickChecks
{
    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        void Frame()
        {
            Call(window, "TickDesktopToys", 1 / 60.0); Call(window, "TickFeatures", 1 / 60.0);
            if (!(bool)Call(window, "FeatureOwnsMovement")!) behavior.Step(pet, 1 / 60.0);
            if (pet.Action == PetAction.Walking) pet.X += pet.VX / 60.0; // The walk the physics step would make.
            pet.ActionTime += 1 / 60.0;
        }
        bool Until(Func<bool> done, int frames = 900) { for (int i = 0; i < frames && !done(); i++) Frame(); return done(); }
        void Free() { pet.Action = PetAction.Idle; pet.VX = 0; pet.Grounded = true; }
        var bounds = Field<Area>(window, "bounds");
        double scale = Field<double>(window, "scale");
        double middle = (bounds.Left + bounds.Right) / 2;
        double CatCentre() => pet.X + PetSettings.CenterX;
        Point cursor = new(middle + 300, bounds.Bottom - 60);
        SetField(window, "cursorProbe", new Func<Point>(() => new Point(cursor.X * scale, cursor.Y * scale)));
        window.Reset(); pet.Emotions.FinishSleep(0); pet.Emotions.Reset();
        preferences.Quiet = false; behavior.Quiet = false;
        preferences.TrickLessons = null; preferences.TrickDay = null; preferences.TrickToday = null;
        pet.X = middle - PetSettings.CenterX; Free();

        // Called: a glance, then along the floor to under the cursor, following it; then sitting looking up.
        check(window.CallMenuEnabled && window.CallCat() && window.CallPhase == "Turn" && pet.Action == PetAction.Looking && pet.Facing == 1,
            "Called: the cat first looks toward the cursor");
        check(Until(() => window.CallPhase == "Walk") && Until(() => pet.Action == PetAction.Walking, 5), "...then sets off");
        cursor = new Point(middle + 200, cursor.Y);
        check(Until(() => window.CallPhase == "Sit") && Math.Abs(CatCentre() - cursor.X) <= TrickRules.CallNear + 4 && pet.Action == PetAction.Sitting,
            "It follows the cursor and sits right under it");
        check(Until(() => window.CallPhase == "None", 60 * 4) && pet.Action == PetAction.Idle, "...looks up a while, then is free");

        // In Quiet (the user's request) it still comes; a click ends it.
        preferences.Quiet = true; behavior.Quiet = true;
        cursor = new Point(middle - 300, cursor.Y);
        check(window.CallCat() && Until(() => window.CallPhase == "Walk"), "In Quiet a call still works");
        Call(window, "CancelFeatureActivity");
        check(window.CallPhase == "None", "A click on the cat ends it");
        preferences.Quiet = false; behavior.Quiet = false; Free();

        // Asleep: not woken; it comes once it wakes by itself, or gives up after 30 seconds.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        check(window.CallCat() && window.CallPhase == "Wait", "Called while asleep: the call waits");
        for (int i = 0; i < 120; i++) Frame();
        check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && window.CallPhase == "Wait", "...the cat sleeps on");
        pet.Emotions.FinishSleep(0); Free();
        check(Until(() => window.CallPhase == "Turn", 5), "Awake by itself: it comes");
        Call(window, "CancelFeatureActivity"); Free();
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        window.CallCat();
        for (int i = 0; i < 60 * 31; i++) Call(window, "TickFeatures", 1 / 60.0);
        check(window.CallPhase == "None" && pet.Action == PetAction.Sleeping, "Still asleep after 30 seconds: the call is dropped");
        pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();

        // The cursor on another monitor: only a look that way. Pause: no call.
        cursor = new Point(bounds.Left - 20000, cursor.Y);
        pet.X = middle - PetSettings.CenterX;
        check(window.CallCat() && window.CallPhase == "Look" && pet.Facing == -1, "The cursor on another monitor: the cat only looks that way");
        check(Until(() => window.CallPhase == "None", 120) && Math.Abs(CatCentre() - middle) < 1, "...without walking");
        Free();
        window.SetPaused(true);
        check(!window.CallMenuEnabled && !window.CallCat(), "Paused: no call");
        window.SetPaused(false);
        cursor = new Point(middle + 150, bounds.Bottom - 60);

        // A lesson: a clumsy try, then a stroke within six seconds counts one.
        pet.X = middle - PetSettings.CenterX; Free();
        check(!window.DoNow(PetAction.Spinning), "Not learned yet: no spin from Do now");
        check(window.TeachTrick(TrickRules.Spin) && window.LessonPhase == "Try" && pet.Action == PetAction.Spinning && pet.TrickClumsy,
            "Teaching the spin: the cat tries it, clumsily");
        for (int i = 0; i < 25; i++) Frame();
        Snapshot(window, "trick-spin-clumsy");
        check(Until(() => window.LessonPhase == "Pet") && pet.Action == PetAction.Sitting && !pet.TrickClumsy, "...then sits waiting for a stroke");
        Call(window, "Interact", "petted");
        Frame();
        check(window.LessonPhase == "None" && TrickRules.Lessons(preferences, TrickRules.Spin) == 1 && window.TrickLabel == "1/5", "A stroke counts one lesson: 1/5 over its head");
        check(!window.TeachTrick(TrickRules.Spin), "The next lesson waits 30 seconds");
        SetField(window, "lessonReadyAt", double.NegativeInfinity); Free();
        check(window.TeachTrick(TrickRules.Spin) && Until(() => window.LessonPhase == "Pet"), "Another lesson");
        check(Until(() => window.LessonPhase == "None", 60 * 7) && TrickRules.Lessons(preferences, TrickRules.Spin) == 1, "No stroke in six seconds: not counted");
        SetField(window, "lessonReadyAt", double.NegativeInfinity); Free();
        check(window.TeachTrick(TrickRules.Spin), "Once more");
        Call(window, "CancelFeatureActivity");
        check(window.LessonPhase == "None" && pet.Action == PetAction.Idle && !pet.TrickClumsy, "A click ends the lesson");
        SetField(window, "lessonReadyAt", double.NegativeInfinity); Free();

        // Three a day; asleep it waits like "Do now" and does not wake the cat.
        for (int lesson = 0; lesson < 2; lesson++)
        {
            SetField(window, "lessonReadyAt", double.NegativeInfinity); Free();
            window.TeachTrick(TrickRules.Spin); Until(() => window.LessonPhase == "Pet");
            Call(window, "Interact", "petted"); Frame();
        }
        SetField(window, "lessonReadyAt", double.NegativeInfinity); Free();
        check(TrickRules.Lessons(preferences, TrickRules.Spin) == 3 && !window.TeachTrick(TrickRules.Spin), "Three lessons today: no more until tomorrow");
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        check(window.TeachTrick(TrickRules.Beg) && window.LessonPhase == "Queued", "Asleep: the lesson waits");
        for (int i = 0; i < 60 * 4; i++) Call(window, "TickFeatures", 1 / 60.0);
        check(window.LessonPhase == "None" && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "...not waking it, and lapses like Do now");
        pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();

        // Learned: in Do now, done properly.
        preferences.TrickLessons = new() { [TrickRules.Spin] = 5, [TrickRules.Beg] = 5, [TrickRules.HighFive] = 5 };
        check(window.DoNow(PetAction.Spinning) && pet.Action == PetAction.Spinning && !pet.TrickClumsy, "Learned: the spin from Do now, done properly");
        pet.ActionTime = 0.7; Snapshot(window, "trick-spin");
        Free();
        check(window.DoNow(PetAction.Begging) && pet.Action == PetAction.Begging, "Sitting up and begging from Do now");
        pet.ActionTime = 1; Snapshot(window, "trick-beg");
        pet.TrickClumsy = true; Snapshot(window, "trick-beg-clumsy"); pet.TrickClumsy = false;
        Free();
        cursor = new Point(CatCentre() + 30, pet.Y + 42);
        check(window.DoNow(PetAction.HighFive) && pet.Action == PetAction.HighFive, "A high five from Do now");
        pet.ActionTime = 0.7;
        check(window.HighFiveMet, "The cursor by the raised paw meets it");
        Snapshot(window, "trick-highfive");
        pet.TrickClumsy = true; check(!window.HighFiveMet, "...not a clumsy try"); Snapshot(window, "trick-highfive-clumsy"); pet.TrickClumsy = false;
        pet.Facing = -1; cursor = new Point(CatCentre() - 30, pet.Y + 42); Snapshot(window, "trick-highfive-left");
        check(window.HighFiveMet, "Facing left, the paw is on the left");

        preferences.TrickLessons = null; preferences.TrickDay = null; preferences.TrickToday = null;
        Free();
        SetField(window, "cursorProbe", null);
        window.Reset();
    }

    // PET_RENDER_REVIEW=<folder>: the cat's own window as drawn.
    private static void Snapshot(PetWindow window, string name)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not { Length: > 0 } folder) return;
        Call(window, "Present", false);
        var renderer = Field<PetRenderer>(window, "renderer");
        renderer.InvalidateVisual(); renderer.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)renderer.ActualWidth * 2, (int)renderer.ActualHeight * 2, 192, 192, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(renderer);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }
}
