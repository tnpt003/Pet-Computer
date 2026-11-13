using System.IO;
using System.Windows;
using System.Windows.Media;
using DesktopPet;
using static PetPrivate;

// Mini-games M1 (docs/MINI-GAMES.md): the cup game in the real pet window — a whole round won and one lost,
// the best and the count kept, the shuffle getting quicker, Quiet, asleep (not woken), Esc, a click elsewhere,
// a click on the cat, Pause, nothing automatic while it runs, Python told it is busy; and drawings to review.
internal static class CupGameChecks
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
        Point cursor = new(middle, bounds.Top + 40);
        var keys = new HashSet<int>();
        SetField(window, "cursorProbe", new Func<Point>(() => new Point(cursor.X * scale, cursor.Y * scale)));
        SetField(window, "keyProbe", new Func<int, bool>(keys.Contains));
        window.Reset(); pet.Emotions.FinishSleep(0); pet.Emotions.Reset();
        preferences.Quiet = false; behavior.Quiet = false;
        preferences.CupGameBest = 0; preferences.CupGamePlays = 0;
        pet.X = middle - PetSettings.CenterX; pet.Y = bounds.Bottom - Field<PetSettings>(window, "settings").Height; Free();
        double RowLeft() => Field<double>(window, "cupRowLeft");
        double FishSlotX() => CupGameRules.SlotCentre(window.CupGameSlotOf(window.CupGameFishCup));
        double WrongSlotX() => CupGameRules.SlotCentre(Enumerable.Range(0, 3).First(s => s != window.CupGameSlotOf(window.CupGameFishCup)));

        // Started from the menu: the cups come out beside the cat and the fish is shown.
        check(window.CupGameMenuEnabled && window.CupGameMenuLabel == "Find the fish", "The menu row is ready, no best yet");
        check(window.ToggleCupGame() && window.CupGameActive && Until(() => window.CupGamePhase == "Show", 30) && Field<Window?>(window, "cupWindow") != null,
            "Find the fish: the cups come out next to the cat and the fish is shown");
        check(pet.Emotions.Suspended && (bool)Call(window, "FeatureOwnsMovement")!, "While it runs no automatic emotion or behaviour comes in");
        Snapshot(window, "cups-show", light: true);
        check(Until(() => window.CupGamePhase == "Shuffle") && window.CupGameSwaps == 3, "Round 1: three swaps");
        for (int i = 0; i < 20; i++) Frame();
        Snapshot(window, "cups-shuffle", light: true);
        check(Until(() => window.CupGamePhase == "Choose") && !window.PickCup(-50), "Shuffled: it waits for a pick; a click beside the cups picks nothing");
        Snapshot(window, "cups-choose", light: false);

        // The right cup: the cat goes there, sniffs, tips it up and has the fish.
        double fishX = RowLeft() + FishSlotX();
        check(window.PickCup(FishSlotX()) && Until(() => window.CupGamePhase == "Sniff") && Math.Abs(CatCentre() - fishX) <= 6,
            "Picked: the cat goes behind the row to that cup and sniffs it");
        check(Until(() => window.CupGamePhase == "Lift", 60), "...then tips it up with a paw");
        for (int i = 0; i < 20; i++) Frame();
        Snapshot(window, "cups-lift", light: true);
        check(Until(() => window.CupGamePhase == "Win") && window.CupGameRounds == 1 && preferences.CupGameBest == 1 && Field<PetRenderer>(window, "renderer").CarryFish,
            "The fish is there: +1, the best goes up at once and the cat holds the fish");
        Snapshot(window, "cups-win", light: false);
        check(Until(() => window.CupGamePhase == "Shuffle") && window.CupGameSwaps == 5 && !Field<PetRenderer>(window, "renderer").CarryFish, "Round 2: five quicker swaps");

        // A wrong cup: puzzled, the right cup is shown, the game is over and counted.
        Until(() => window.CupGamePhase == "Choose");
        check(window.PickCup(WrongSlotX()) && Until(() => window.CupGamePhase == "Puzzled"), "An empty cup: the cat tilts its head");
        check(Until(() => window.CupGamePhase == "Reveal"), "...then the cup with the fish is turned up");
        for (int i = 0; i < 30; i++) Frame();
        Snapshot(window, "cups-reveal", light: true);
        check(Until(() => !window.CupGameActive) && preferences.CupGamePlays == 1 && preferences.CupGameBest == 1 && Field<Window?>(window, "cupWindow") == null &&
              !pet.Emotions.Suspended && window.TrickLabel == "1 in a row", "Game over: counted, the cups go away, emotions are back, \"1 in a row\"");
        check(window.CupGameMenuLabel == "Find the fish · best 1", "The menu row shows the best");

        // Quiet: still the user's game. Esc ends it; a click on the cups does not; a click elsewhere does.
        preferences.Quiet = true; behavior.Quiet = true; Free();
        check(window.ToggleCupGame() && Until(() => window.CupGamePhase == "Shuffle"), "In Quiet the game still runs");
        cursor = new Point(RowLeft() + CupGameRules.SlotCentre(1), bounds.Bottom - 20);
        keys.Add(0x01); Frame(); keys.Clear(); Frame();
        check(window.CupGameActive, "A click on the cups does not end it");
        keys.Add(0x1B); Frame(); keys.Clear();
        check(!window.CupGameActive && preferences.CupGamePlays == 2, "Esc ends it (counted)");
        preferences.Quiet = false; behavior.Quiet = false; Free();
        window.ToggleCupGame(); Until(() => window.CupGamePhase == "Shuffle");
        cursor = new Point(middle, bounds.Top + 40);
        keys.Add(0x01); Frame(); keys.Clear();
        check(!window.CupGameActive, "A click elsewhere ends it");
        Free();
        keys.Add(0x01); window.ToggleCupGame(); Until(() => window.CupGamePhase == "Show", 30); Frame(); keys.Clear(); Frame();
        check(window.CupGameActive, "The click that chose the menu row does not end it");
        check(window.ToggleCupGame() && !window.CupGameActive, "The menu row again ends it");
        Free();
        window.ToggleCupGame(); Until(() => window.CupGamePhase == "Shuffle");
        Call(window, "CancelFeatureActivity");
        check(!window.CupGameActive && pet.Action == PetAction.Idle && !pet.Emotions.Suspended, "A click or drag on the cat ends it");
        Free();

        // Python sees the game as busy.
        window.ToggleCupGame(); Until(() => window.CupGamePhase == "Shuffle");
        check((bool)Call(window, "get_CupOwnsMovement")!, "While the cups are out the cat is reported busy (MiniGame) to Python");
        window.ToggleCupGame(); Free();

        // Asleep: not woken; it starts once the cat wakes by itself, or is dropped after 30 seconds.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        check(window.ToggleCupGame() && window.CupGamePhase == "Wait", "Asleep: the game waits");
        for (int i = 0; i < 120; i++) Call(window, "TickFeatures", 1 / 60.0);
        check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && window.CupGamePhase == "Wait", "...the cat sleeps on");
        pet.Emotions.FinishSleep(0); Free();
        check(Until(() => window.CupGamePhase == "Show", 30), "Awake by itself: the game starts");
        window.ToggleCupGame(); Free();
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        window.ToggleCupGame();
        for (int i = 0; i < 60 * 31; i++) Call(window, "TickFeatures", 1 / 60.0);
        check(!window.CupGameActive && pet.Action == PetAction.Sleeping, "Still asleep after 30 seconds: dropped");
        pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();

        // Pause: no game; pausing mid-game freezes it (no frames run), and the row is greyed.
        window.SetPaused(true);
        check(!window.CupGameMenuEnabled && !window.ToggleCupGame() && window.CupGameMenuNote.Length > 0, "Paused: the row is greyed and says why");
        window.SetPaused(false);

        // Two minutes at most.
        int plays = preferences.CupGamePlays;
        window.ToggleCupGame(); Until(() => window.CupGamePhase == "Choose");
        for (int i = 0; i < 60 * 125 && window.CupGameActive; i++) Frame();
        check(!window.CupGameActive && preferences.CupGamePlays == plays + 1, "Two minutes without a pick: the game ends (counted)");
        Free();

        // Real clicks (Issue #85): each of the three cups takes a click through Windows' own hit test.
        for (int slot = 0; slot < CupGameRules.Cups; slot++)
        {
            Free();
            window.ToggleCupGame(); Until(() => window.CupGamePhase == "Choose");
            Call(window, "Present", false);
            Call(window, "PlaceCups");
            Pump(300);
            double x = RowLeft() + CupGameRules.SlotCentre(slot), y = Field<double>(window, "cupGround") - 18;
            var at = new NativePoint { X = (int)Math.Round(x * scale), Y = (int)Math.Round(y * scale) };
            var under = GetAncestor(WindowFromPoint(at), 2);
            var title = new System.Text.StringBuilder(256); GetWindowText(under, title, 256);
            GetCursorPos(out var was);
            SetCursorPos(at.X, at.Y);
            mouse_event(0x0002, 0, 0, 0, 0); mouse_event(0x0004, 0, 0, 0, 0);
            Pump(300);
            SetCursorPos(was.X, was.Y);
            int chosen = Field<int>(window, "cupChosen");
            check(window.CupGamePhase is "Go" or "Sniff" or "Lift" && chosen >= 0 && window.CupGameSlotOf(chosen) == slot,
                $"A real click on cup {slot + 1} picks it (window under it: {title}, phase {window.CupGamePhase})");
            if (window.CupGameActive) window.ToggleCupGame();
        }
        Free();

        preferences.CupGameBest = 0; preferences.CupGamePlays = 0;
        SetField(window, "cursorProbe", null);
        SetField(window, "keyProbe", null);
        window.Reset();
    }

    private struct NativePoint { public int X, Y; }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int max);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern void mouse_event(uint flags, int dx, int dy, int data, IntPtr extra);

    // Lets WPF draw and Windows deliver input for a while.
    private static void Pump(int milliseconds)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var wait = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        wait.Tick += (_, _) => { wait.Stop(); frame.Continue = false; };
        wait.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    // PET_RENDER_REVIEW=<folder>: the cat and the cups as they stand on the floor, on a light or dark desktop.
    private static void Snapshot(PetWindow window, string name, bool light)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not { Length: > 0 } folder) return;
        Call(window, "Present", false);
        var renderer = Field<PetRenderer>(window, "renderer");
        renderer.InvalidateVisual(); renderer.UpdateLayout();
        var art = Field<CupGameArt?>(window, "cupArt");
        var pet = Field<PetModel>(window, "pet");
        double rowLeft = Field<double>(window, "cupRowLeft"), ground = Field<double>(window, "cupGround");
        double catLeft = pet.X - PickupAnimation.Padding, catTop = pet.Y - PickupAnimation.Padding; // The cat's window has room around the cat.
        double left = Math.Min(catLeft, rowLeft - CupGameArt.Pad) - 10, right = Math.Max(catLeft + renderer.ActualWidth, rowLeft - CupGameArt.Pad + CupGameArt.ArtWidth) + 10;
        double top = Math.Min(catTop, ground - CupGameArt.ArtHeight) - 10, bottom = ground + 10;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(light ? Color.FromRgb(0xE9, 0xEE, 0xF2) : Color.FromRgb(0x1F, 0x24, 0x2B)), null, new Rect(0, 0, right - left, bottom - top));
            dc.DrawRectangle(new VisualBrush(renderer) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, renderer.ActualWidth, renderer.ActualHeight) }, null, new Rect(catLeft - left, catTop - top, renderer.ActualWidth, renderer.ActualHeight));
            if (art != null)
            {
                art.InvalidateVisual(); art.UpdateLayout();
                dc.DrawRectangle(new VisualBrush(art) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, CupGameArt.ArtWidth, CupGameArt.ArtHeight) }, null, new Rect(rowLeft - CupGameArt.Pad - left, ground - CupGameArt.ArtHeight - top, CupGameArt.ArtWidth, CupGameArt.ArtHeight));
            }
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)((right - left) * 2), (int)((bottom - top) * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }
}
