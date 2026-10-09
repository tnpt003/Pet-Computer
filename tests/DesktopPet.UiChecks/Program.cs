using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using DesktopPet.AI;

internal static class Program
{
    private static int checks;

    [STAThread]
    private static int Main()
    {
        Environment.SetEnvironmentVariable("PET_AI_DISABLED", "1");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new PetWindow(new PetPreferences());
        window.Loaded += async (_, _) =>
        {
            try
            {
                await Run(window);
                Console.WriteLine($"All {checks} WPF checks passed.");
                window.Close();
                app.Shutdown(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                window.Close();
                app.Shutdown(1);
            }
        };
        return app.Run(window);
    }

    private static T Field<T>(PetWindow window, string name) =>
        (T)typeof(PetWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
        Console.WriteLine($"PASS: {description}");
    }

    private static MouseButtonEventArgs MouseEvent(RoutedEvent routedEvent) =>
        new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = routedEvent };

    private static async Task Run(PetWindow window)
    {
        await Task.Delay(50);
        Check(window.Icon is BitmapSource icon && icon.PixelWidth == 256, "Window uses the full-resolution custom pet icon");
        using (var stream = Application.GetResourceStream(new Uri("pack://application:,,,/DesktopPet;component/Assets/Pet.ico"))!.Stream)
        {
            var frames = new IconBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames;
            Check(frames.Select(frame => frame.PixelWidth).SequenceEqual(new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 }),
                "Embedded icon contains all nine native Windows resolutions");
        }
        using (var trayIcon = AppIcon.TrayImage())
            Check(trayIcon.Width >= 16, "Tray icon loads from the same multi-resolution pet artwork");
        Check(window.AllowsTransparency && window.Topmost && !window.ShowInTaskbar &&
              window.WindowStyle == WindowStyle.None, "WPF desktop host retains transparent/topmost/no-taskbar configuration");
        var pet = Field<PetModel>(window, "pet");
        var behavior = Field<PetBehavior>(window, "behavior");
        var ai = new DelayedSource();
        typeof(PetWindow).GetField("ai", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, ai);
        window.Reset();
        behavior.Step(pet, 3); // Starts a pending Python-equivalent decision, with immediate local fallback.
        window.Activate(); // Synthetic routed input needs an active presentation source.
        await Task.Delay(30);
        // CaptureMouse can pump dispatcher messages. Isolate the pickup transition
        // from an unrelated autonomous movement tick during this assertion.
        var timer = Field<System.Windows.Threading.DispatcherTimer>(window, "timer");
        timer.Stop();
        var surfaceBeforePickup = new Rect(window.Left, window.Top, window.Width, window.Height);
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        var dpi = VisualTreeHelper.GetDpi(window);
        Check(surfaceBeforePickup.Width == window.Width && surfaceBeforePickup.Height == window.Height &&
              Math.Abs(surfaceBeforePickup.Left - window.Left) < 1 / dpi.DpiScaleX &&
              Math.Abs(surfaceBeforePickup.Top - window.Top) < 1 / dpi.DpiScaleY,
            "Mouse-down preserves the transparent surface within native pixel rounding");
        timer.Start();
        Check(pet.Action != PetAction.Dragging && window.IsMouseCaptured, "Press gives attention without starting a drag");
        CrossDragThreshold(window);
        Check(pet.Action == PetAction.Dragging && window.IsMouseCaptured,
            $"Mouse-down captures input and enters dragging (state={pet.Action}, captured={window.IsMouseCaptured})");
        pet.X += 50;
        pet.Y -= 50;
        await Task.Delay(70);
        // The real user's cursor may move during the test. Compare a fresh update
        // with its current global position rather than assuming a stationary mouse.
        typeof(PetWindow).GetMethod("UpdateDrag", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, new object[] { 1.0 / 120 });
        var cursorNow = (Point)typeof(PetWindow).GetMethod("CursorPosition", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null)!;
        var easedOffset = Field<Point>(window, "dragOffset");
        var workArea = Field<Area>(window, "bounds");
        var artBounds = pet.Pickup.VisualBounds(112, 112);
        double expectedX = Math.Clamp(cursorNow.X - easedOffset.X, workArea.Left - artBounds.Left, workArea.Right - artBounds.Right);
        double expectedY = Math.Clamp(cursorNow.Y - easedOffset.Y, workArea.Top - artBounds.Top, workArea.Bottom - artBounds.Bottom);
        Check(pet.Action == PetAction.Dragging && Math.Abs(pet.X - expectedX) < 3 && Math.Abs(pet.Y - expectedY) < 3,
            "Drag follows the cursor with full-body screen-edge constraints");
        Check(pet.X + artBounds.Left >= workArea.Left - 0.01 && pet.X + artBounds.Right <= workArea.Right + 0.01 &&
            pet.Y + artBounds.Top >= workArea.Top - 0.01 && pet.Y + artBounds.Bottom <= workArea.Bottom + 0.01,
            "Full swinging artwork remains inside the monitor work area");
        Check(window.Width == 192 && window.Height == 192,
            "Picked-up window provides transparent padding for swinging artwork");
        ai.Completion.SetResult(new AiDecision("SLEEP", 8, "sleepy"));
        await Task.Delay(30);
        Check(pet.Action == PetAction.Dragging, "Delayed AI response does not interrupt captured drag");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        Check(!window.IsMouseCaptured && pet.Action == PetAction.Thrown, "Mouse release hands control back to throw physics");
        Check(window.Width == 192 && window.Height == 192 && pet.Pickup.Angle == 0 && pet.Pickup.Dizziness == 0,
            "Release keeps the same surface dimensions and clears pickup effects");
        window.SetAiEnabled(false);
        window.Reset();
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        Check(!window.IsMouseCaptured && pet.Action != PetAction.Thrown && pet.Action != PetAction.Dragging,
            "Quick click releases capture without creating a throw");
        window.ToggleDebug();
        var debugWindow = Field<Window>(window, "debugWindow");
        Check(debugWindow.IsVisible && Field<System.Windows.Controls.TextBlock>(window, "debugText").Text.Contains("Energy:") &&
              Field<System.Windows.Controls.TextBlock>(window, "debugText").Text.Contains("Frame:"),
            "Optional debug view shows live behavior and energy status");
        window.ToggleDebug();
        Check(!debugWindow.IsVisible && window.IsVisible, "Closing debug view leaves the pet running");
        window.Reset();
        behavior.TryApply(pet, new AiDecision("WALK_RIGHT", 5, "content"));
        double oldX = pet.X;
        await Task.Delay(100);
        Check(pet.X > oldX && pet.Action == PetAction.Walking, "WPF timer executes walking and updates animation state");
        window.SetPaused(true);
        oldX = pet.X;
        double oldTime = pet.AnimationTime;
        double oldActivity = pet.Activity.Walking;
        await Task.Delay(100);
        Check(pet.X == oldX && pet.AnimationTime == oldTime, "Pause freezes motion and animation");
        Check(pet.Activity.Walking == oldActivity, "Pause does not accumulate energy activity telemetry");
        window.SetPaused(false);
        await Task.Delay(80);
        Check(pet.X > oldX && pet.AnimationTime > oldTime, "Resume restores the update loop");
        window.Reset();
        var resetDpi = VisualTreeHelper.GetDpi(window);
        Check(Math.Abs(window.Left - (pet.X - PickupAnimation.Padding)) <= 1 / resetDpi.DpiScaleX &&
              Math.Abs(window.Top - (pet.Y - PickupAnimation.Padding)) <= 1 / resetDpi.DpiScaleY,
            "Reset snaps the window to the pet without blending from the old position");
        pet.Action = PetAction.Thrown; pet.Grounded = false; pet.VX = 900; pet.VY = -600;
        double flightStart = pet.X;
        await Task.Delay(120);
        double presentedX = window.Left + PickupAnimation.Padding;
        Check(pet.X > flightStart && presentedX >= flightStart - 1 && presentedX <= pet.X + 1,
            "Thrown flight presents an interpolated position between physics states");
        window.Reset();
        window.Reset();
        pet.Emotions.Step(pet, 31); // Initial randomized sleep may have started a wake cooldown earlier.
        behavior.TryApply(pet, new AiDecision("SLEEP", 8, "sleepy"));
        oldX = pet.X;
        double oldY = pet.Y;
        await Task.Delay(100);
        Check(pet.Action == PetAction.Sleeping && pet.X == oldX && pet.Y == oldY && pet.ActionTime > 0,
            "Sleep animation clock runs while the window remains planted");
        window.SetPaused(true);
        window.SetPaused(false);
        window.SetAiEnabled(false);
        Check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked,
            "Pause, resume and AI toggles preserve sleep lock");
        window.Reset();
        Check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked,
            "Repositioning to the primary screen preserves sleep until click");
        window.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            { RoutedEvent = UIElement.MouseRightButtonDownEvent });
        Check(pet.Action == PetAction.Waking && !pet.Emotions.SleepLocked && pet.Emotions.Current != PetEmotion.Sleepy,
            "Right-click immediately wakes sleeping pet and clears Zs");
        window.Reset();
        pet.Emotions.Step(pet, 31);
        behavior.TryApply(pet, new AiDecision("SLEEP", 8, "sleepy"));
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        Check(pet.Action == PetAction.Waking && !pet.Emotions.SleepLocked, "Left press wakes before drag threshold");
        CrossDragThreshold(window);
        Check(pet.Action == PetAction.Dragging && window.IsMouseCaptured, "Dragging interrupts sleep immediately");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        Check(pet.Action == PetAction.Thrown, "Releasing a pet picked up during sleep enters throw physics");
        window.SetPaused(true);

        string Frame(PetAction action, double time)
        {
            var visual = new PetRenderer(new PetModel { Action = action, AnimationTime = time, ActionTime = time });
            visual.Measure(new Size(192, 192));
            visual.Arrange(new Rect(0, 0, 192, 192));
            var bitmap = new RenderTargetBitmap(192, 192, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            string? review = Environment.GetEnvironmentVariable("PET_RENDER_REVIEW");
            if (review != null && action is PetAction.Grooming or PetAction.Stretching or PetAction.SeekingAttention)
            {
                System.IO.Directory.CreateDirectory(review);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = System.IO.File.Create(System.IO.Path.Combine(review, action + "-" + time.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png"));
                encoder.Save(output);
            }
            var bytes = new byte[192 * 192 * 4];
            bitmap.CopyPixels(bytes, 192 * 4, 0);
            Check(bytes[3] == 0 && bytes.Where((_, i) => i % 4 == 3).Any(alpha => alpha != 0),
                $"{action} renderer has transparent background and visible character pixels");
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        Check(Frame(PetAction.SeekingAttention, 0.2) != Frame(PetAction.SeekingAttention, 1.2),
            "Attention gesture progresses from looking to a raised paw");
        Check(Frame(PetAction.Grooming, 0.8) != Frame(PetAction.Grooming, 2.2), "Grooming progresses from licking to face washing");
        Check(Frame(PetAction.Stretching, 0.1) != Frame(PetAction.Stretching, 1.2), "Self-care stretch changes pose while planted");
        string idle = Frame(PetAction.Idle, 1);
        Check(idle != Frame(PetAction.Idle, 2), "Idle animation changes across frames");
        Check(Frame(PetAction.Walking, 0.3) != Frame(PetAction.Walking, 0.5), "Walking animation changes across frames");
        Check(idle != Frame(PetAction.Sleeping, 1), "Sleeping renders a distinct pose");
        Check(Frame(PetAction.Sleeping, 0.3) != Frame(PetAction.Sleeping, 1.5), "Sleeping continuously animates breathing and floating Zs");
        Check(Frame(PetAction.Waking, 0.1) != Frame(PetAction.Waking, 0.8), "Waking renders a progressing stretch and open eyes");
        string PickupFrame(PetAction action, double speed, double holdTime = 0)
        {
            var model = new PetModel { Action = action, AnimationTime = 1, ActionTime = 1 };
            model.Pickup.Begin(0, 0);
            for (int i = 1; i <= 120; i++) model.Pickup.Step(speed * i / 120, 0, 1.0 / 120);
            for (int i = 0; i < holdTime * 120; i++) model.Pickup.Step(speed, 0, 1.0 / 120);
            var visual = new PetRenderer(model);
            int size = 192;
            visual.Measure(new Size(size, size));
            visual.Arrange(new Rect(0, 0, size, size));
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var bytes = new byte[size * size * 4];
            bitmap.CopyPixels(bytes, size * 4, 0);
            if (action == PetAction.Dragging)
            {
                bool clearEdges = true;
                for (int i = 0; i < size; i++)
                    clearEdges &= bytes[i * 4 + 3] == 0 && bytes[((size - 1) * size + i) * 4 + 3] == 0
                        && bytes[(i * size) * 4 + 3] == 0 && bytes[(i * size + size - 1) * 4 + 3] == 0;
                Check(clearEdges, "Swing and circling stars fit inside the padded transparent window");
            }
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        Check(PickupFrame(PetAction.Dragging, 100) != PickupFrame(PetAction.Dragging, 2200),
            "Fast pickup renders a visibly stronger dizzy/dangling pose");
        Check(PickupFrame(PetAction.Dragging, 2200) != PickupFrame(PetAction.Dragging, 2200, 3),
            "Holding the cursor still eases the rendered pickup pose");
        Check(PickupFrame(PetAction.Idle, 2200) == idle, "Pickup presentation never affects idle rendering");
        Check(PickupFrame(PetAction.Walking, 2200) == Frame(PetAction.Walking, 1),
            "Pickup presentation never affects walking rendering");
        VisualReviewChecks.Run(Check);
        EmotionVisualChecks.Run(Check);
        MotionVisualChecks.Run(Check);
        window.SetPaused(false);
        window.Reset();
        void AttentionClick() => window.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            { RoutedEvent = UIElement.MouseRightButtonDownEvent });
        AttentionClick();
        await Task.Delay(450);
        Check(pet.Emotions.Current == PetEmotion.Happy, "Actual WPF click handler triggers Happy");
        for (int i = 0; i < 3; i++) { AttentionClick(); await Task.Delay(80); }
        await Task.Delay(350);
        Check(pet.Emotions.Current == PetEmotion.Angry && pet.Action != PetAction.Dragging,
            "Repeated WPF clicks trigger anger without changing movement state");
        window.SetPaused(true);
        double emotionAge = pet.Emotions.Time;
        await Task.Delay(100);
        Check(pet.Emotions.Time == emotionAge, "Pause freezes emotional animation and timing");
        window.SetPaused(false);
        window.Reset();
        timer.Stop();
        var preferences = Field<PetPreferences>(window, "preferences");
        bool originalQuiet = preferences.Quiet;
        preferences.Quiet = false; behavior.Quiet = false;
        Check(window.GiveTreat() && pet.TreatRemaining > 0 && pet.Activity.Treats == 0,
            "Treat appears before consumption or energy reward");
        Check(!window.GiveTreat(), "Treat cooldown prevents repeated feeding");
        void FeatureStep(double dt) => typeof(PetWindow).GetMethod("TickFeatures", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object[] { dt });
        FeatureStep(0.2);
        Check(pet.Action == PetAction.Sitting && pet.VX == 0, "Eating stays planted");
        for (int i = 0; i < 50; i++) { FeatureStep(0.1); pet.Emotions.Step(pet, 0.1); }
        Check(pet.TreatRemaining == 0 && pet.Activity.Treats == 1 && pet.Emotions.Current == PetEmotion.Happy,
            "Completed eating gives one energy reward and a satisfied reaction");
        Check((bool)typeof(PetWindow).GetMethod("FeatureOwnsMovement", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null)!,
            "Satisfied pause protects the happy reaction from immediate AI movement");
        window.Reset();
        window.ToggleToy();
        Check(Field<Window?>(window, "toyWindow")?.IsVisible == true, "Toy mode displays a ball");
        FeatureStep(0.1);
        Check(pet.Action == PetAction.Looking && pet.VX == 0, "Toy mode observes before chasing");
        for (int i = 0; i < 15; i++) FeatureStep(0.1);
        Check(pet.Action == PetAction.Walking && pet.VX != 0, "Toy mode chases after preparation through C# movement");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        Check(Field<Window?>(window, "toyWindow") == null, "Direct pickup interaction stops toy play");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        window.Reset(); window.ToggleToy();
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        FeatureStep(0.1);
        Check(Field<Window?>(window, "toyWindow") == null && pet.Emotions.SleepLocked,
            "Sleep stops toy play without being interrupted");
        Check(!window.GiveTreat(), "Treat command cannot interrupt a sleep lock");
        behavior.WakeByClick(pet); pet.Action = PetAction.Idle; pet.Emotions.Reset();
        double? oldRestX = preferences.RestX, oldRestY = preferences.RestY;
        preferences.RestX = pet.X - 100; preferences.RestY = pet.Y;
        typeof(PetWindow).GetField("returningHome", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, true);
        FeatureStep(0.1);
        Check(pet.Action == PetAction.Walking && pet.VX < 0, "Favorite spot is approached by walking, not teleporting");
        preferences.RestX = oldRestX; preferences.RestY = oldRestY;
        window.Reset();
        typeof(PetWindow).GetMethod("ShowPetSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        var customize = Field<Window>(window, "settingsWindow");
        Check(customize.IsVisible, "Name and personality editor opens on demand");
        var controls = ((System.Windows.Controls.StackPanel)customize.Content).Children;
        Check(controls.OfType<System.Windows.Controls.CheckBox>().Count() == 4
            && controls.OfType<System.Windows.Controls.Slider>().Single().Value == preferences.Volume
            && controls.OfType<System.Windows.Controls.WrapPanel>().Single().Children.Count == 9,
            "Sound settings expose master/category switches, volume and all nine previews");
        var playSound = typeof(PetWindow).GetMethod("PlayPetSound", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Check(!(bool)playSound.Invoke(window, new object?[] { "meow", true, 0.0 })!, "Zero-volume preview stays silent");
        typeof(PetWindow).GetMethod("ShowBehaviorSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        var tuningWindow = Field<Window>(window, "behaviorSettingsWindow");
        var tuningPanel = (System.Windows.Controls.StackPanel)((System.Windows.Controls.ScrollViewer)tuningWindow.Content).Content;
        var tuningSliders = tuningPanel.Children.OfType<System.Windows.Controls.Slider>().ToArray();
        Check(tuningWindow.IsVisible && tuningSliders.Length == 8, "Tuning editor exposes all eight supported JSON values");
        tuningSliders[0].Value = 0.23;
        tuningPanel.Children.OfType<System.Windows.Controls.Button>().Single(b => (string)b.Content == "Save behavior tuning")
            .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(preferences.Tuning?.Playfulness == 0.23, "Tuning save applies draft without writing personal test preferences");
        typeof(PetWindow).GetMethod("ShowBehaviorSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        tuningWindow = Field<Window>(window, "behaviorSettingsWindow");
        tuningPanel = (System.Windows.Controls.StackPanel)((System.Windows.Controls.ScrollViewer)tuningWindow.Content).Content;
        tuningPanel.Children.OfType<System.Windows.Controls.TextBox>().First().Text = "NaN";
        Check(!tuningPanel.Children.OfType<System.Windows.Controls.Button>().Last().IsEnabled, "Tuning editor refuses nonfinite input");
        tuningPanel.Children.OfType<System.Windows.Controls.Button>().First().RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        tuningPanel.Children.OfType<System.Windows.Controls.Button>().Last().RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(preferences.Tuning == null, "Restore defaults clears the user override");
        customize.Close();
        preferences.PositionMonitor = "disconnected-monitor";
        preferences.PositionX = 0.3; preferences.PositionY = 1;
        preferences.RestMonitor = "disconnected-monitor"; preferences.RestFractionX = 0.7;
        typeof(PetWindow).GetMethod("RestorePosition", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        var restoredBounds = Field<Area>(window, "bounds");
        Check(pet.Grounded && pet.X >= restoredBounds.Left && pet.X <= restoredBounds.Right - 112
            && preferences.RestX >= restoredBounds.Left && preferences.RestX <= restoredBounds.Right - 112,
            "Unavailable saved monitor restores pet and favorite spot inside the primary work area");
        window.ShowControls();
        Check(window.ContextMenu!.IsOpen, "Existing pet controls can be opened by a second launch notification");
        window.ContextMenu.IsOpen = false;
        preferences.Quiet = true; behavior.Quiet = true;
        window.ToggleToy();
        Check(Field<Window?>(window, "toyWindow") == null, "Quiet mode blocks toy movement");
        preferences.Quiet = originalQuiet; behavior.Quiet = originalQuiet;
        window.Close();
        Check(ai.Disposed && !window.IsVisible, "Window/AI shutdown releases the decision source and hides the pet");
    }

    private static void CrossDragThreshold(PetWindow window)
    {
        var cursor = (Point)typeof(PetWindow).GetMethod("CursorPosition", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null)!;
        typeof(PetWindow).GetField("pressPosition", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, new Point(cursor.X - 10, cursor.Y));
        typeof(PetWindow).GetMethod("CheckDragThreshold", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
    }

    private sealed class DelayedSource : IPetDecisionSource
    {
        public readonly TaskCompletionSource<AiDecision?> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed;
        public string Status => "test";
        public Task<AiDecision?> DecideAsync(PetObservation observation) => Completion.Task;
        public void Dispose() => Disposed = true;
    }
}
