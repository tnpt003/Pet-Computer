using System.IO;
using System.Windows;
using System.Windows.Media;
using DesktopPet;
using static PetPrivate;

// Mini-games M3 (docs/MINI-GAMES.md): catching fireflies in the real pet window — the count, 3–5 flies in an
// area around the cat, a tap bursting one, the cat's pounce drawn in place (it never leaves its ground), the
// shared score and the best, 30 seconds, Esc, a click elsewhere or a miss inside the area, a drag on the cat,
// asleep, Pause, Quiet, a narrow screen; and drawings to review.
internal static class FireflyChecks
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
        preferences.FireflyBest = 0; preferences.FireflyPlays = 0;
        void Home() { pet.X = middle - PetSettings.CenterX; pet.Y = bounds.Bottom - catHeight; Free(); }
        // One fly low beside the cat, so its pounce has something to aim at.
        void Lure()
        {
            var swarm = window.FirefliesSwarm;
            if (swarm.Flies.FirstOrDefault(f => !f.Caught) is { } fly) { fly.X = swarm.Width / 2 + 75; fly.Y = swarm.Height - 70; fly.Age = Math.Min(fly.Age, 1); }
        }
        Home();

        // Started: the count, then 3–5 flies in the area around the cat, over its floor.
        check(window.FirefliesMenuEnabled && window.FirefliesMenuLabel == "Catch fireflies", "The fireflies row is ready");
        check(window.ToggleFireflies() && window.FirefliesActive, "Catch fireflies starts");
        check(!window.CupGameMenuEnabled && !window.HideSeekMenuEnabled && !window.ToggleHideSeek(), "One mini-game at a time: the other rows are greyed");
        check(Until(() => window.FirefliesPhase == "Count", 30) && Until(() => window.MiniLabel != null, 2) && window.MiniLabel?.StartsWith("Catch the fireflies!") == true, "\"Catch the fireflies! 3…\"");
        check(pet.Emotions.Suspended && (bool)Call(window, "FeatureOwnsMovement")!, "No automatic emotion or behaviour while playing");
        double catY = pet.Y;
        check(Until(() => window.FirefliesPhase == "Play", 60 * 3) && window.FirefliesAlive is >= 3 and <= 5, "Then 3–5 fireflies come out");
        var area = window.FirefliesArea;
        check(Math.Abs(area.Left + area.Width / 2 - (pet.X + PetSettings.CenterX)) < 1 && Math.Abs(area.Bottom - bounds.Bottom) < 1 && area.Width == FireflyRules.AreaWidth &&
              area.Height == FireflyRules.AreaHeight, "The area is about 360×220, centred on the cat, on its floor");
        for (int i = 0; i < 30; i++) Frame();
        check(window.MiniLabel?.StartsWith("Catch fireflies · ") == true && window.MiniLabel.EndsWith("0 caught"), "The label shows the time left and the count");
        Snapshot(window, "fireflies-play", light: false);
        Snapshot(window, "fireflies-play-light", light: true);

        // A tap on a fly bursts it (+1); a tap in empty air is a miss; neither ends the game.
        var swarmNow = window.FirefliesSwarm;
        var fly = swarmNow.Flies.First(f => !f.Caught);
        check(window.TapFirefly(fly.X, fly.Y) && window.FirefliesUser == 1 && !swarmNow.Flies.Contains(fly), "A tap on a firefly catches it");
        Frame(); Frame();
        Snapshot(window, "fireflies-burst", light: false);
        check(!window.TapFirefly(-50, -50) && window.FirefliesUser == 1, "A tap on nothing is a miss");
        cursor = new Point(area.Left + 20, area.Top + 20);
        keys.Add(0x01); Frame(); keys.Clear(); Frame();
        check(window.FirefliesActive && window.FirefliesAlive >= 3, "A click inside the area does not end it; flies stay 3–5");

        // The cat's pounce: a crouch and a little hop drawn in place, at most ~34 DIPs, on the same ground.
        double highest = 0;
        bool stayed = true, sawPounce = false;
        int catBefore = window.FirefliesCat;
        for (int i = 0; i < 60 * 10 && !(sawPounce && !window.FirefliesPouncing); i++)
        {
            if (!window.FirefliesPouncing) Lure();
            Frame();
            sawPounce |= window.FirefliesPouncing;
            highest = Math.Max(highest, window.FirefliesHop);
            stayed &= Math.Abs(pet.Y - catY) < 0.01 && pet.Grounded && pet.LedgeId == 0;
            if (window.FirefliesPouncing && window.FirefliesHop > FireflyRules.HopHeight - 2 && highest < FireflyRules.HopHeight + 1)
            {
                Snapshot(window, pet.Facing > 0 ? "fireflies-pounce-right" : "fireflies-pounce-left", light: true);
            }
        }
        check(sawPounce && !window.FirefliesPouncing, "Within 6–8 seconds the cat pounces at a fly low beside it");
        check(highest > 20 && highest <= FireflyRules.HopHeight + 0.01 && stayed && window.FirefliesHop == 0, "...a hop of at most ~34 DIPs drawn in place: it lands where it stood");
        check(window.FirefliesCat - catBefore <= 1 && pet.Emotions.Suspended && !pet.Emotions.SleepLocked, "...one fly at most, no emotion, no Dizzy");

        // To the end: the cat never gets more than four; 30 seconds, the flies leave, the summary and the best.
        for (int i = 0; i < 60 * 30 && window.FirefliesPhase == "Play"; i++) { if (!window.FirefliesPouncing) Lure(); Frame(); }
        check(window.FirefliesPhase == "Away" && window.FirefliesCat is >= 1 and <= FireflyRules.MaxCatCatches, "After 30 seconds: over, the cat caught 1–4");
        int total = window.FirefliesUser + window.FirefliesCat;
        check(window.MiniLabel == $"Caught {total} together (you {window.FirefliesUser} · cat {window.FirefliesCat}) · New best!", "The summary: the shared count, you and the cat, and a new best");
        Snapshot(window, "fireflies-away", light: false);
        check(Until(() => !window.FirefliesActive, 60 * 3) && preferences.FireflyBest == total && preferences.FireflyPlays == 1 && !pet.Emotions.Suspended &&
              pet.Action == PetAction.Idle, "Then the cat is itself again; the best and the game are kept");
        check(window.FirefliesMenuLabel == $"Catch fireflies · best {total}", "The row shows the best");

        // Esc, a click elsewhere, the row again, a drag on the cat end it.
        Home();
        window.ToggleFireflies(); Until(() => window.FirefliesPhase == "Play", 60 * 8);
        keys.Add(0x1B); Frame(); keys.Clear();
        check(!window.FirefliesActive && preferences.FireflyPlays == 2 && !pet.Emotions.Suspended, "Esc ends it (counted as played)");
        Home();
        window.ToggleFireflies(); Until(() => window.FirefliesPhase == "Play", 60 * 8);
        cursor = new Point(middle, bounds.Top + 40);
        keys.Add(0x01); Frame(); keys.Clear();
        check(!window.FirefliesActive, "A click elsewhere ends it");
        Home();
        window.ToggleFireflies(); Until(() => window.FirefliesPhase == "Play", 60 * 8);
        check(window.ToggleFireflies() && !window.FirefliesActive, "The row again ends it");
        Home();
        window.ToggleFireflies(); Until(() => window.FirefliesPhase == "Play", 60 * 8);
        pet.Action = PetAction.Dragging; pet.Grounded = false;
        Frame();
        check(!window.FirefliesActive && window.FirefliesHop == 0 && Field<Window?>(window, "flyWindow") == null, "Picking up the cat ends it; the flies are gone");
        Free(); Home();

        // Quiet: still plays. Asleep: not woken, it waits. Pause: greyed.
        preferences.Quiet = true; behavior.Quiet = true;
        check(window.ToggleFireflies() && Until(() => window.FirefliesPhase == "Count", 30), "Quiet mode: the user's game still runs");
        window.ToggleFireflies();
        preferences.Quiet = false; behavior.Quiet = false;
        Home();
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        check(window.ToggleFireflies() && window.FirefliesPhase == "Wait", "Asleep: the game waits");
        for (int i = 0; i < 120; i++) Call(window, "TickFeatures", 1 / 60.0);
        check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && window.FirefliesPhase == "Wait", "...the cat sleeps on");
        window.ToggleFireflies();
        pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();
        window.SetPaused(true);
        check(!window.FirefliesMenuEnabled && !window.ToggleFireflies(), "Paused: the row is greyed");
        window.SetPaused(false);

        // A screen too narrow: no game.
        SetField(window, "bounds", new Area(bounds.Left, bounds.Top, bounds.Left + 250, bounds.Bottom));
        pet.X = bounds.Left;
        check(window.ToggleFireflies() && !window.FirefliesActive && window.TrickLabel == "No room", "No room for the area: \"No room\"");
        SetField(window, "bounds", bounds);

        preferences.FireflyBest = 0; preferences.FireflyPlays = 0;
        Home();
        SetField(window, "cursorProbe", null);
        SetField(window, "keyProbe", null);
        window.Reset();
    }

    // PET_RENDER_REVIEW=<folder>: the cat's window over the fireflies' area, on a light or dark desktop.
    private static void Snapshot(PetWindow window, string name, bool light)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not { Length: > 0 } folder) return;
        Call(window, "Present", false);
        var renderer = Field<PetRenderer>(window, "renderer");
        renderer.InvalidateVisual(); renderer.UpdateLayout();
        var art = Field<FireflyArt?>(window, "flyArt");
        var pet = Field<PetModel>(window, "pet");
        var area = window.FirefliesArea;
        double catLeft = pet.X - PickupAnimation.Padding, catTop = pet.Y - PickupAnimation.Padding;
        double left = Math.Min(catLeft, area.Left) - 10, right = Math.Max(catLeft + renderer.ActualWidth, area.Right) + 10;
        double top = Math.Min(catTop, area.Top) - 10, bottom = area.Bottom + 10;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(light ? Color.FromRgb(0xE9, 0xEE, 0xF2) : Color.FromRgb(0x1F, 0x24, 0x2B)), null, new Rect(0, 0, right - left, bottom - top));
            if (art != null)
            {
                art.InvalidateVisual(); art.UpdateLayout();
                dc.DrawRectangle(new VisualBrush(art) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, area.Width, area.Height) }, null,
                    new Rect(area.Left - left, area.Top - top, area.Width, area.Height));
            }
            dc.DrawRectangle(new VisualBrush(renderer) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, renderer.ActualWidth, renderer.ActualHeight) }, null,
                new Rect(catLeft - left, catTop - top, renderer.ActualWidth, renderer.ActualHeight));
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)((right - left) * 2), (int)((bottom - top) * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }
}
