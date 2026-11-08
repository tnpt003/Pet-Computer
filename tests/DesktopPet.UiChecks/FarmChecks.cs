using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using static PetPrivate;

// The field in the real pet window (1.5.1 M3): where it stands, the steps of a click, moving the
// plot, what hides it, and that it leaves the cat alone. Uses a temporary file and a false clock.
internal static class FarmChecks
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, idle)));
    private static Window? Overlay() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet farm" && w.IsLoaded);

    public static void Run(PetWindow window, PetModel pet, PetPreferences prefs, Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "pet-farm-ui-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "farm.json");
        var now = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        var cursor = new Point(0, 0);
        var realCursor = FieldOf("cursorProbe").GetValue(window);
        void Tick(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickFarm", 1.0 / 60); }
        void Wait(double seconds) { now = now.AddSeconds(seconds); Call(window, "TickFarm", 1.0 / 60); }
        SetField(window, "farmPath", path);
        SetField(window, "farmClock", new Func<DateTime>(() => now));
        SetField(window, "cursorProbe", new Func<Point>(() => cursor));
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        window.Reset();
        try
        {
            Tick(0.05);
            check(!window.FarmVisible && Overlay() == null && !File.Exists(path), "With the setting off there is no field and no file");
            prefs.Farm = true;
            Tick(0.05);
            check(window.FarmVisible && Overlay() != null && window.Farm != null, "Turned on, it appears");

            var overlay = Overlay()!;
            var handle = new WindowInteropHelper(overlay).Handle;
            var area = Field<Area>(window, "farmArea");
            check(Math.Abs(overlay.Top + PetFarmArt.Floor - area.Bottom) < 1.5, $"Its soil stands on the top of the taskbar ({overlay.Top + PetFarmArt.Floor:F0} on {area.Bottom:F0})");
            double free = area.Right - area.Left - PetFarmArt.PlotWidth;
            check(Math.Abs(overlay.Left + PetFarmArt.Side - (area.Left + free * 0.25)) < 1.5, "At first it is a quarter across the monitor");
            long style = GetWindowLongPtr(handle, -20).ToInt64();
            check((style & 0x20) == 0 && (style & 0x08000000) != 0 && (style & 0x80) != 0, "It takes clicks but no focus, and has no taskbar button");
            check(overlay.Topmost && !overlay.ShowInTaskbar && !overlay.ShowActivated, "An overlay like the others");
            check(Below(new WindowInteropHelper(window).Handle, handle), "The cat walks in front of it");

            // The steps of one tile, by clicks.
            var farm = window.Farm!;
            check(window.FarmTileLabel(0)!.Length > 0 && window.FarmTileLabel(6) == null, "Each tile has a label");
            check(farm.Next(0) == FarmStep.Till, "A new tile needs tilling");
            window.FarmClick(0);
            check(farm.Tiles[0].Stage == FarmStage.Tilled && File.Exists(path), "A click tills it and saves");
            check(farm.Tiles[1].Stage == FarmStage.Wild, "Only that tile");
            window.FarmClick(0);
            check(window.FarmSeedMenuOpen && farm.Tiles[0].Stage == FarmStage.Tilled, "A click on tilled ground opens the seed menu and sows nothing yet");
            Call(window, "CloseFarmSeedMenu");
            check(!window.FarmSeedMenuOpen, "The menu closes");
            var seeds = window.FarmSeedItems(0);
            check(seeds.Length == 4 && seeds.All(s => s.Icon is FarmCropIcon && ((string)s.Header).Length > 0), "The seed menu has four crops with their pictures");
            check(window.FarmPlant(0, FarmCrop.Radish) && farm.Next(0) == FarmStep.Water, "A seed is sown");
            check(!window.FarmPlant(1, FarmCrop.Radish), "Not on wild ground");
            check(window.FarmTileLabel(0)!.Contains("Radish") || window.FarmTileLabel(0)!.Contains("Củ cải"), "The label names the crop");

            double stage = PetFarm.StageSeconds(FarmCrop.Radish);
            Wait(stage * 5);
            check(farm.Tiles[0].Stage == FarmStage.Seed, "An unwatered seed stays a seed");
            foreach (var next in new[] { FarmStage.Sprout, FarmStage.Young, FarmStage.Ripe })
            {
                window.FarmClick(0);
                check(farm.Next(0) == FarmStep.Wait, $"Watered, it grows towards {next}");
                window.FarmClick(0);
                check(farm.Next(0) == FarmStep.Wait && farm.Tiles[0].Stage != next, "Clicking again while it grows does nothing");
                Wait(stage + 1);
                check(farm.Tiles[0].Stage == next, $"The clock brings it to {next}");
            }
            window.FarmClick(0);
            check(farm.HarvestedCount(FarmCrop.Radish) == 1 && farm.Tiles[0].Stage == FarmStage.Wild, "A click harvests it and counts it");
            var gain = (Border)Field<Border?>(window, "farmGain")!;
            check(gain.Opacity == 1 && ((TextBlock)gain.Child).Text.StartsWith("+1 "), "\"+1\" rises over the tile");
            Tick(FarmGainWait);
            check(gain.Opacity < 0.05, "...and fades");

            var reload = PetFarm.Load(path);
            check(reload.HarvestedCount(FarmCrop.Radish) == 1 && reload.Tiles[0].Stage == FarmStage.Wild, "The harvest is in the file");

            // Clicks leave the cat alone, asleep or awake.
            pet.Emotions.Reset(); pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
            window.FarmClick(2);
            Tick(0.2);
            check(pet.Emotions.SleepLocked && pet.Action == PetAction.Sleeping && farm.Tiles[2].Stage == FarmStage.Tilled, "The field works while the cat sleeps, and does not wake it");
            pet.Emotions.Reset(); pet.Action = PetAction.Idle;
            double vx = pet.VX, x = pet.X, y = pet.Y; var action = pet.Action; var emotion = pet.Emotions.Current;
            window.FarmClick(3);
            Tick(1);
            check(pet.VX == vx && pet.X == x && pet.Y == y && pet.Action == action && pet.Emotions.Current == emotion, "It never touches the cat");

            // Pause: no clicks, nothing moves; growth follows the clock.
            farm.Plant(2, FarmCrop.Radish); farm.Water(2, now);
            SetField(window, "paused", true);
            window.FarmClick(4);
            check(farm.Tiles[4].Stage == FarmStage.Wild, "Pause refuses clicks");
            check(window.FarmMenuItems().Length == 0, "...and the menu");
            window.BeginFarmMove();
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Down, "...and moving the plot");
            now = now.AddSeconds(stage + 1);
            Call(window, "TickFarm", 0.0);
            check(farm.Tiles[2].Stage == FarmStage.Sprout, "A crop still grows by the clock in Pause");
            SetField(window, "paused", false);

            // Moving the plot.
            var ground = window.FarmMenuItems();
            check(ground.Length == 1 && ((string)ground[0].Header).Length > 0, "On the ground the menu offers to move it");
            double topBefore = Overlay()!.Top, restFraction = window.FarmFraction;
            ground[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Rising, "It begins to rise");
            check(window.FarmMenuItems().Length == 0, "No menu while it is on the way up");
            Tick(FarmLift.RiseSeconds + 0.05);
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Floating && Math.Abs(window.FarmLiftHeight - FarmLift.FloatHeight) < FarmLift.BobHeight + 0.5, $"It floats ({window.FarmLiftHeight:F1} DIPs up)");
            check(Math.Abs(Overlay()!.Top - topBefore) < 0.5, "The art lifts inside the window, its shadow stays on the floor");
            int stageBefore = (int)farm.Tiles[4].Stage;
            window.FarmClick(4);
            check((int)farm.Tiles[4].Stage == stageBefore, "In the air a click does not farm");
            var air = window.FarmMenuItems();
            check(air.Length == 2, "In the air the menu offers to drop or cancel");

            double plotLeft() => area.Left + window.FarmFraction * free;
            double scaleNow = Field<double>(window, "farmScale");
            cursor = new Point((plotLeft() + 100) * scaleNow, (area.Top + 40) * scaleNow);
            Call(window, "PressFarm", 0);
            // Mouse capture needs a real press on some machines; the drag itself is what is checked here.
            bool held = window.FarmDragging;
            if (!held) { SetField(window, "farmDragging", true); SetField(window, "farmGrab", cursor.X / scaleNow - plotLeft()); }
            double before = plotLeft();
            cursor = new Point(cursor.X + 150 * scaleNow, (area.Top + 5) * scaleNow);
            Tick(0.05);
            check(Math.Abs(plotLeft() - before - 150) < 1.5, $"It follows the cursor along the floor ({plotLeft() - before:F0} DIPs)");
            check(Math.Abs(Overlay()!.Top - topBefore) < 0.5, "...and only sideways, wherever the cursor goes up or down");
            cursor = new Point(-100000, cursor.Y);
            Tick(0.05);
            check(window.FarmFraction == 0, "It stops at the monitor's left edge");
            cursor = new Point(100000, cursor.Y);
            Tick(0.05);
            check(window.FarmFraction == 1, "...and at its right edge");
            cursor = new Point((area.Left + free * 0.6 + 100) * scaleNow, cursor.Y);
            Tick(0.05);
            Call(window, "ReleaseFarm", 0);
            if (!held) SetField(window, "farmDragging", false);
            check(!window.FarmDragging && window.FarmLiftPhaseNow == FarmLiftPhase.Floating, "Let go, it stays in the air");

            string savedBefore = File.ReadAllText(path);
            window.FarmMenuItems()[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Falling, "Cancel sets it down");
            Tick(FarmLift.FallSeconds + 0.05);
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Down && Math.Abs(window.FarmFraction - restFraction) < 1e-6, "...back where it was");

            window.BeginFarmMove();
            Tick(FarmLift.RiseSeconds + 0.05);
            SetField(window, "farmFraction", 0.6);
            window.FarmMenuItems()[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Tick(FarmLift.FallSeconds / 2);
            check(window.FarmLiftHeight > 1 && window.FarmLiftHeight < FarmLift.FloatHeight, $"It comes down slowly ({window.FarmLiftHeight:F1} DIPs up half way)");
            Tick(FarmLift.FallSeconds / 2 + 0.1);
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Down && Math.Abs(window.FarmFraction - 0.6) < 1e-6, "It lands where it was held");
            var moved = PetFarm.Load(path);
            check(moved.FractionX == window.FarmFraction && moved.Monitor != null && File.ReadAllText(path) != savedBefore, "Its place and monitor are saved");
            check(Math.Abs(Overlay()!.Left + PetFarmArt.Side - (area.Left + free * 0.6)) < 1.5, "The window stands there");

            // Fullscreen and battle hide it; a plot in the air goes back.
            window.BeginFarmMove();
            Tick(FarmLift.RiseSeconds + 0.05);
            SetField(window, "farmFraction", 0.1);
            SetContext(window, true, 0); Call(window, "TickContext", 1.1);
            Tick(0.02);
            check(!window.FarmVisible, "A fullscreen app hides it");
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Down && Math.Abs(window.FarmFraction - 0.6) < 1e-6, "A plot in the air goes back to its place");
            SetContext(window, false, 0); Call(window, "TickContext", 1.1);
            Tick(0.02);
            check(window.FarmVisible && Math.Abs(window.FarmFraction - 0.6) < 1e-6, "It comes back after the fullscreen app");

            check(window.StartBattle() == null && window.BattleActive, "A battle starts");
            Tick(0.02);
            check(!window.FarmVisible, "A battle hides it");
            window.EndBattle();
            Tick(0.02);
            check(window.FarmVisible, "It comes back after the battle");
            pet.Action = PetAction.Idle;

            prefs.Farm = false;
            Tick(0.02);
            check(!window.FarmVisible, "The setting hides it");
            prefs.Farm = true;
            Tick(0.02);
            check(window.FarmVisible && window.Farm!.HarvestedCount(FarmCrop.Radish) == 1, "Turned on again, the field is as it was");
        }
        finally
        {
            Call(window, "CloseFarm");
            prefs.Farm = false;
            SetField(window, "farmClock", null);
            SetField(window, "farmPath", PetFarm.DefaultPath);
            SetField(window, "farm", null);
            SetField(window, "paused", false);
            SetField(window, "cursorProbe", realCursor);
            SetContext(window, false, 0); Call(window, "TickContext", 1.1);
            window.Reset();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        Art(check);
    }

    private const double FarmGainWait = 1.35;

    private static bool Below(IntPtr above, IntPtr target)
    {
        for (var next = GetWindow(above, 2); next != IntPtr.Zero; next = GetWindow(next, 2))
            if (next == target) return true;
        return false;
    }

    // Every stage of every crop, on a light and a dark wallpaper, resting and in the air.
    private static void Art(Action<bool, string> check)
    {
        int width = (int)PetFarmArt.ArtWidth, height = (int)PetFarmArt.ArtHeight;
        string Render(Action<PetFarmArt> set, Color back, string? name)
        {
            var art = new PetFarmArt();
            set(art);
            art.Measure(new Size(width, height));
            art.Arrange(new Rect(0, 0, width, height));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(back), null, new Rect(0, 0, width, height));
                dc.DrawRectangle(new VisualBrush(art), null, new Rect(0, 0, width, height));
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            if (name != null && Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review)
            {
                Directory.CreateDirectory(review);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(review, name + ".png")); encoder.Save(stream);
            }
            var pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);
            return Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(pixels));
        }

        var light = Color.FromRgb(0xF1, 0xEE, 0xE6);
        var dark = Color.FromRgb(0x1C, 0x22, 0x30);
        void Steps(PetFarmArt art)
        {
            art.Tiles[0] = new(FarmStage.Wild, null, false, false, 0);
            art.Tiles[1] = new(FarmStage.Tilled, null, false, false, 0);
            art.Tiles[2] = new(FarmStage.Seed, FarmCrop.Carrot, false, true, 0);
            art.Tiles[3] = new(FarmStage.Sprout, FarmCrop.Carrot, true, false, 0.4);
            art.Tiles[4] = new(FarmStage.Young, FarmCrop.Tomato, true, false, 0.8);
            art.Tiles[5] = new(FarmStage.Young, FarmCrop.Pumpkin, false, true, 0.67);
            art.HoverTile = 3;
        }
        void Ripe(PetFarmArt art)
        {
            var crops = Enum.GetValues<FarmCrop>();
            for (int i = 0; i < 6; i++) art.Tiles[i] = new(FarmStage.Ripe, crops[i % 4], false, false, 1);
        }
        foreach (var (back, tone) in new[] { (light, "light"), (dark, "dark") })
        {
            Render(Steps, back, $"farm-steps-{tone}");
            Render(Ripe, back, $"farm-ripe-{tone}");
            Render(art => { Steps(art); art.Lift = FarmLift.FloatHeight; }, back, $"farm-floating-{tone}");
            Render(art => { Steps(art); art.Dustiness = 0.6; }, back, $"farm-landing-{tone}");
        }
        foreach (var (effect, name) in new[] { (FarmEffect.Till, "till"), (FarmEffect.Water, "water"), (FarmEffect.Harvest, "harvest") })
            Render(art => { Steps(art); art.Effects[1] = effect; art.EffectAge[1] = 0.4; }, dark, $"farm-effect-{name}");

        var seen = new HashSet<string>();
        foreach (var stage in new[] { FarmStage.Seed, FarmStage.Sprout })
            seen.Add(Render(art => art.Tiles[0] = new(stage, FarmCrop.Carrot, false, false, 0), dark, null));
        foreach (var crop in Enum.GetValues<FarmCrop>())
        {
            seen.Add(Render(art => art.Tiles[0] = new(FarmStage.Ripe, crop, false, false, 1), dark, null));
            seen.Add(Render(art => art.Tiles[0] = new(FarmStage.Young, crop, false, false, 0.5), dark, null));
        }
        // Radish and carrot share their young leaves' shape, but not their color.
        check(seen.Count == 10, $"Every stage of every crop looks different ({seen.Count} of 10)");
        string dry = Render(art => art.Tiles[0] = new(FarmStage.Seed, FarmCrop.Carrot, false, false, 0), dark, null);
        check(dry != Render(art => art.Tiles[0] = new(FarmStage.Seed, FarmCrop.Carrot, true, false, 0), dark, null), "Wet soil is not dry soil");
        check(dry != Render(art => art.Tiles[0] = new(FarmStage.Seed, FarmCrop.Carrot, false, true, 0), dark, null), "A thirsty seed shows a drop");
        string still = Render(art => { }, dark, null);
        check(still != Render(art => art.Lift = FarmLift.FloatHeight, dark, null), "A floating plot is drawn in the air");
        check(PetFarmArt.TileAt(PetFarmArt.TileLeft(0) + 1) == 0 && PetFarmArt.TileAt(PetFarmArt.TileLeft(5) + 53) == 5 && PetFarmArt.TileAt(1) == -1 && PetFarmArt.TileAt(PetFarmArt.ArtWidth - 1) == -1, "Clicks find their tiles; the rim is none");
    }
}
