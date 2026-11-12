using System.IO;
using System.Windows;
using System.Windows.Media;
using DesktopPet;
using static PetPrivate;

// Mini-games M2 (docs/MINI-GAMES.md): hide-and-seek in the real pet window — the count and the puff, the cat
// sunk behind one of three places, its ears peeking now and then, wrong guesses fading a place, a press on the
// hidden cat found right (and the game going on unless it becomes a drag), three wrong guesses or a minute
// bringing it out, no speech while hidden, Esc, Pause, asleep; and drawings to review.
internal static class HideSeekChecks
{
    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        void Frame()
        {
            Call(window, "TickDesktopToys", 1 / 60.0); Call(window, "TickFeatures", 1 / 60.0);
            if (!(bool)Call(window, "FeatureOwnsMovement")!) behavior.Step(pet, 1 / 60.0);
            pet.ActionTime += 1 / 60.0;
        }
        bool Until(Func<bool> done, int frames = 900) { for (int i = 0; i < frames && !done(); i++) Frame(); return done(); }
        void Free() { pet.Action = PetAction.Idle; pet.VX = 0; pet.Grounded = true; }
        var bounds = Field<Area>(window, "bounds");
        double scale = Field<double>(window, "scale");
        double middle = (bounds.Left + bounds.Right) / 2;
        double catHeight = Field<PetSettings>(window, "settings").Height;
        Point cursor = new(middle, bounds.Top + 40);
        var keys = new HashSet<int>();
        SetField(window, "cursorProbe", new Func<Point>(() => new Point(cursor.X * scale, cursor.Y * scale)));
        SetField(window, "keyProbe", new Func<int, bool>(keys.Contains));
        window.Reset(); pet.Emotions.FinishSleep(0); pet.Emotions.Reset();
        preferences.Quiet = false; behavior.Quiet = false;
        preferences.HideSeekFound = 0; preferences.HideSeekFirstTry = 0;
        void Home() { pet.X = middle - PetSettings.CenterX; pet.Y = bounds.Bottom - catHeight; Free(); }
        Home();
        int Wrong() => Enumerable.Range(0, window.HideSeekSpotCount).First(i => i != window.HideSeekCatSpot && !window.HideSeekSpotWrong(i));

        // Started: three places, the count, the puff, and the cat already behind one of them.
        check(window.HideSeekMenuEnabled && window.HideSeekMenuLabel == "Hide and seek", "The hide-and-seek row is ready");
        check(window.ToggleHideSeek() && window.HideSeekPhase == "Count" && window.HideSeekSpotCount == 3 && window.MiniLabel?.StartsWith("Close your eyes!") == true,
            "Hide and seek: three hiding places and \"Close your eyes! 3…\"");
        check(!window.CupGameMenuEnabled && !window.ToggleCupGame(), "One mini-game at a time: the cup game's row is greyed");
        for (int i = 0; i < 20; i++) Frame();
        Snapshot(window, "seek-vanish", light: true);
        check(Until(() => window.HideSeekSink > 0, 60) && Math.Abs(pet.X + PetSettings.CenterX - window.HideSeekSpotX(window.HideSeekCatSpot)) < 1 &&
              Math.Abs(pet.Y + catHeight - bounds.Bottom) < 1, "In a puff the cat is behind its hiding place, on the floor, sunk low");
        Snapshot(window, "seek-hidden", light: true);
        check(Until(() => window.HideSeekPhase == "Hide") && window.MiniLabel == "Where is the cat hiding?", "Then: \"Where is the cat hiding?\"");
        check(!(bool)Call(window, "SpeechAllowed", "greeting")!, "A hidden cat says nothing (no bubble gives it away)");
        double hiddenSink = window.HideSeekSink;
        check(Until(() => window.HideSeekSink < hiddenSink - 5, 60 * 6), "Now and then its ear tips peek over");
        for (int i = 0; i < 14; i++) Frame(); // The peek at its highest.
        Snapshot(window, "seek-peek", light: false);
        Snapshot(window, "seek-peek-light", light: true);

        // A wrong guess fades that place and costs a guess; the cat's place is right.
        int wrong = Wrong();
        check(window.GuessSpot(wrong) && window.HideSeekSpotWrong(wrong) && window.HideSeekGuessesLeft == 2 && window.MiniLabel == "Not here" &&
              !window.GuessSpot(wrong), "A wrong place: \"Not here\", it fades and cannot be picked again");
        check(window.GuessSpot(window.HideSeekCatSpot) && window.HideSeekFound && window.HideSeekPhase == "Pop" && preferences.HideSeekFound == 1 &&
              preferences.HideSeekFirstTry == 0 && window.MiniLabel == "Found!", "The cat's place: found (not on the first try)");
        check(Until(() => window.HideSeekPhase == "Out") && window.HideSeekSink == 0, "It pops out in front of its place");
        for (int i = 0; i < 15; i++) Frame();
        Snapshot(window, "seek-out", light: true);
        check(Until(() => !window.HideSeekActive) && !pet.Emotions.Suspended && pet.Action == PetAction.Idle && window.HideSeekMenuLabel == "Hide and seek · found 1",
            "Then the bushes wither away and the cat is itself again; the row shows the finds");

        // A press on the hidden cat: the right guess on the first try; the click goes on to the pop-out.
        Home();
        window.ToggleHideSeek(); Until(() => window.HideSeekPhase == "Hide");
        check((bool)Call(window, "HideSeekPressCat")! && window.HideSeekFound && preferences.HideSeekFirstTry == 1, "A press on the hidden cat finds it, first try");
        SetField(window, "pressPending", true);
        Frame();
        check(window.HideSeekActive, "...a press that is not (yet) a drag does not end the game");
        SetField(window, "pressPending", false);
        check(Until(() => !window.HideSeekActive, 60 * 3), "...released, the pop-out finishes");

        // Wrong guesses use up the three; a minute: the cat comes out by itself.
        Home();
        window.ToggleHideSeek(); Until(() => window.HideSeekPhase == "Hide");
        window.GuessSpot(Wrong()); window.GuessSpot(Wrong());
        check(window.HideSeekGuessesLeft == 1 && window.HideSeekPhase == "Hide" &&
              Enumerable.Range(0, 3).Count(i => !window.HideSeekSpotWrong(i)) == 1, "Two wrong guesses: one left, and only the cat's place is still open");
        window.ToggleHideSeek(); Free();
        Home();
        window.ToggleHideSeek(); Until(() => window.HideSeekPhase == "Hide");
        for (int i = 0; i < 60 * 61 && window.HideSeekPhase == "Hide"; i++) Frame();
        check(window.HideSeekPhase == "Pop" && !window.HideSeekFound && window.MiniLabel == "Here I am!", "A minute without finding it: \"Here I am!\"");
        Until(() => !window.HideSeekActive, 60 * 4);
        check(preferences.HideSeekFound == 2, "Games where the cat came out by itself are not finds");

        // Esc and a click elsewhere end it; the cat shows again where it was hiding.
        Home();
        window.ToggleHideSeek(); Until(() => window.HideSeekPhase == "Hide");
        keys.Add(0x1B); Frame(); keys.Clear();
        check(!window.HideSeekActive && window.HideSeekSink == 0 && !pet.Emotions.Suspended, "Esc ends it; the cat is visible again");
        Home();
        window.ToggleHideSeek(); Until(() => window.HideSeekPhase == "Hide");
        cursor = new Point(window.HideSeekSpotX(Wrong()), bounds.Bottom - 20);
        keys.Add(0x01); Frame(); keys.Clear(); Frame();
        check(window.HideSeekActive, "A click on a hiding place does not end it");
        cursor = new Point(middle, bounds.Top + 40);
        keys.Add(0x01); Frame(); keys.Clear();
        check(!window.HideSeekActive, "A click elsewhere ends it");

        // Asleep: not woken, it waits; Pause: greyed.
        Home();
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        check(window.ToggleHideSeek() && window.HideSeekPhase == "Wait", "Asleep: the game waits");
        for (int i = 0; i < 120; i++) Call(window, "TickFeatures", 1 / 60.0);
        check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && window.HideSeekPhase == "Wait", "...the cat sleeps on");
        window.ToggleHideSeek();
        pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();
        window.SetPaused(true);
        check(!window.HideSeekMenuEnabled && !window.ToggleHideSeek(), "Paused: the row is greyed");
        window.SetPaused(false);

        // A floor too narrow: no game.
        SetField(window, "bounds", new Area(bounds.Left, bounds.Top, bounds.Left + 250, bounds.Bottom));
        pet.X = bounds.Left;
        check(window.ToggleHideSeek() && !window.HideSeekActive && window.TrickLabel == "No room to hide", "No room for three places: \"No room to hide\"");
        SetField(window, "bounds", bounds);

        preferences.HideSeekFound = 0; preferences.HideSeekFirstTry = 0;
        Home();
        SetField(window, "cursorProbe", null);
        SetField(window, "keyProbe", null);
        window.Reset();
    }

    // PET_RENDER_REVIEW=<folder>: the cat's window over the floor with the bush windows, light or dark.
    private static void Snapshot(PetWindow window, string name, bool light)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not { Length: > 0 } folder) return;
        Call(window, "Present", false);
        var renderer = Field<PetRenderer>(window, "renderer");
        renderer.InvalidateVisual(); renderer.UpdateLayout();
        var pet = Field<PetModel>(window, "pet");
        var bounds = Field<Area>(window, "bounds");
        var spots = Field<System.Collections.IList>(window, "seekSpots");
        double catLeft = pet.X - PickupAnimation.Padding, catTop = pet.Y - PickupAnimation.Padding;
        double left = catLeft - 260, right = catLeft + renderer.ActualWidth + 260, top = bounds.Bottom - 180, bottom = bounds.Bottom + 6;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(light ? Color.FromRgb(0xE9, 0xEE, 0xF2) : Color.FromRgb(0x1F, 0x24, 0x2B)), null, new Rect(0, 0, right - left, bottom - top));
            foreach (var spot in spots)
            {
                var art = (BushArt?)spot.GetType().GetField("Art")!.GetValue(spot);
                var host = (Window?)spot.GetType().GetField("Window")!.GetValue(spot);
                if (art == null || host == null) continue;
                art.InvalidateVisual(); art.UpdateLayout();
                dc.PushOpacity(host.Opacity);
                dc.DrawRectangle(new VisualBrush(art) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, BushArt.ArtWidth, BushArt.ArtHeight) }, null,
                    new Rect(host.Left - left, host.Top - top, BushArt.ArtWidth, BushArt.ArtHeight));
                dc.Pop();
            }
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, right - left, bounds.Bottom - top)));
            dc.DrawRectangle(new VisualBrush(renderer) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, renderer.ActualWidth, renderer.ActualHeight) }, null,
                new Rect(catLeft - left, catTop - top, renderer.ActualWidth, renderer.ActualHeight));
            dc.Pop();
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)((right - left) * 2), (int)((bottom - top) * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }
}
