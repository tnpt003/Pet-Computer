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
        // The first-battle guide is checked on its own (TutorialChecks); elsewhere it would pause every battle.
        var window = new PetWindow(new PetPreferences { BattleTutorialSeen = true });
        // Unattended runs must not depend on the real foreground window or on how long
        // nobody touched the machine (that would trigger the away nap mid-test).
        SetContext(window, false, 0);
        // Nor on the real desktop's windows (the pet would land on them).
        LedgeChecks.SetWindows(window, () => Array.Empty<WindowSnapshot>());
        // Nor on the real mouse: a cursor left on another monitor carried the dragged pet there
        // (and paused battles as "away"). A still cursor in the middle of the primary work area.
        var primary = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        typeof(PetWindow).GetField("cursorProbe", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(window, (Func<Point>)(() => new Point(primary.Left + primary.Width / 2, primary.Top + primary.Height / 2)));
        window.Loaded += async (_, _) =>
        {
            try
            {
                await Run(window);
                window.Close();
                if (failures.Count > 0)
                {
                    Console.WriteLine($"{failures.Count} WPF check(s) FAILED ({checks} passed):");
                    foreach (string failure in failures) Console.WriteLine($"  - {failure}");
                    app.Shutdown(1);
                    return;
                }
                Console.WriteLine($"All {checks} WPF checks passed.");
                app.Shutdown(0);
            }
            catch (Exception ex)
            {
                // 2: the run itself broke (often no interactive desktop), not a failed check (1).
                Console.Error.WriteLine(ex);
                window.Close();
                app.Shutdown(2);
            }
        };
        return app.Run(window);
    }

    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        typeof(PetWindow).GetField("contextProbe", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(window, new Func<(bool, double)>(() => (fullscreen, idle)));

    private static void TickContext(PetWindow window, double dt) =>
        typeof(PetWindow).GetMethod("TickContext", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object[] { dt });

    // Every element of type T under root (logical tree), in document order.
    private static IEnumerable<T> Descendants<T>(object root) where T : DependencyObject
    {
        if (root is not DependencyObject node) yield break;
        foreach (object child in LogicalTreeHelper.GetChildren(node))
        {
            if (child is T match) yield return match;
            foreach (var inner in Descendants<T>(child)) yield return inner;
        }
    }

    private static T Field<T>(PetWindow window, string name) =>
        (T)typeof(PetWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;

    private static readonly List<string> failures = new();

    // Record failures and keep going so one regression does not hide the others.
    private static void Check(bool condition, string description)
    {
        if (!condition)
        {
            failures.Add(description);
            Console.WriteLine($"FAIL: {description}");
            return;
        }
        checks++;
        Console.WriteLine($"PASS: {description}");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

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
        var headers = window.ContextMenu!.Items.OfType<System.Windows.Controls.MenuItem>().Select(item => item.Header as string).ToList();
        var more = window.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>().Single(item => (string)item.Header == "More");
        var moreHeaders = more.Items.OfType<System.Windows.Controls.MenuItem>().Select(item => item.Header as string).ToList();
        Check(headers.Take(3).SequenceEqual(new[] { "Give treat", "Play with ball", "Laser pointer" }) && headers.Contains("Settings…") &&
              !headers.Contains("Start with Windows") && headers[^1] == "Exit pet" && headers[^2] == "More" &&
              moreHeaders.SequenceEqual(new[] { "Bring the pet to the main screen", "Behavior debug view", "About Desktop Pet" }) &&
              window.ContextMenu.Items.OfType<System.Windows.Controls.Separator>().Count() == 3 &&
              !string.IsNullOrEmpty(PetWindow.VersionText) && PetWindow.VersionText != "unknown",
            "Context menu: play items first, groups split by separators, rarely used items under More, Exit last; About has a version");
        int statusIndex = headers.FindIndex(h => h?.EndsWith("'s status", StringComparison.Ordinal) == true);
        var actionBeforeStatus = pet.Action;
        long interactionsBeforeStatus = Field<long>(window, "interactionCount");
        window.ShowStatusCard();
        var statusCard = Field<Window?>(window, "statusWindow");
        var statusRoot = statusCard?.Content as FrameworkElement;
        statusRoot?.UpdateLayout();
        var statusTexts = statusRoot == null ? new List<string>() : Descendants<System.Windows.Controls.TextBlock>(statusRoot).Where(t => t.IsVisible).Select(t => t.Text).ToList();
        Check(statusIndex >= 0 && headers[statusIndex + 1]?.EndsWith("'s diary", StringComparison.Ordinal) == true && statusCard != null &&
              statusTexts.Any(t => t.StartsWith("Doing: ", StringComparison.Ordinal)) && statusTexts.Any(t => t.StartsWith("Feeling: ", StringComparison.Ordinal)) &&
              statusTexts.Any(t => t.Contains("Smart pet (AI)")) && !statusTexts.Any(t => t.EndsWith("/100", StringComparison.Ordinal)) &&
              pet.Action == actionBeforeStatus && Field<long>(window, "interactionCount") == interactionsBeforeStatus,
            "Status card sits before the diary, shows doing/feeling, hides need bars without the AI, and does not touch the pet");
        // The card sits right above the cat, centered, in screen pixels of the pet's monitor scale
        // (also at a pretend 150%), not where WPF's own Left/Top would put a new window.
        bool CardNextToCat(double testScale)
        {
            if (statusCard == null) return false;
            var scaleField = typeof(PetWindow).GetField("scale", BindingFlags.NonPublic | BindingFlags.Instance)!;
            double realScale = (double)scaleField.GetValue(window)!;
            scaleField.SetValue(window, testScale);
            try
            {
                typeof(PetWindow).GetMethod("PlaceStatusCard", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
                GetWindowRect(new System.Windows.Interop.WindowInteropHelper(statusCard).Handle, out var rect);
                var petSettings = Field<PetSettings>(window, "settings");
                var size = ((FrameworkElement)statusCard.Content).DesiredSize;
                double left = (pet.X + petSettings.Width / 2 - size.Width / 2) * testScale, top = (pet.Y + 10 - size.Height) * testScale;
                return size.Width > 200 && Math.Abs(rect.Left - left) <= 2 && Math.Abs(rect.Top - top) <= 2;
            }
            finally { scaleField.SetValue(window, realScale); }
        }
        double petScale = (double)typeof(PetWindow).GetField("scale", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
        Check(CardNextToCat(petScale) && CardNextToCat(1.5), "Status card is placed right above the cat in the pet's monitor scale (real and 150%)");
        // The card's last-words line: the newer of the latest speech and thought bubble.
        List<string> StatusTexts(string method, object arg)
        {
            typeof(PetWindow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new[] { arg });
            typeof(PetWindow).GetMethod("RefreshStatusCard", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            statusRoot!.UpdateLayout();
            return Descendants<System.Windows.Controls.TextBlock>(statusRoot).Where(t => t.IsVisible).Select(t => t.Text).ToList();
        }
        var saidTexts = StatusTexts("NoteWords", "Meow, check line");
        var thoughtTexts = StatusTexts("NoteThought", ThoughtKind.Hungry);
        Check(statusRoot != null && saidTexts.Any(t => t == "Last said: “Meow, check line” · just now") &&
              thoughtTexts.Any(t => t == "Last thought: hungry · just now") && !thoughtTexts.Any(t => t.StartsWith("Last said", StringComparison.Ordinal)),
            "Status card shows the newest of the last words and the last thought bubble");
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string statusReview && statusRoot != null)
        {
            System.IO.Directory.CreateDirectory(statusReview);
            var image = new RenderTargetBitmap((int)Math.Ceiling(statusRoot.ActualWidth), (int)Math.Ceiling(statusRoot.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            image.Render(statusRoot);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = System.IO.File.Create(System.IO.Path.Combine(statusReview, "status-card.png")); encoder.Save(stream);
        }
        window.CloseStatusCard();
        Check(Field<Window?>(window, "statusWindow") == null, "Closing the status card releases it");
        // Self-update (2026-10-22): only the installed app checks GitHub; a build or copied folder does nothing.
        var updates = new PetUpdates();
        Check(!updates.Available && updates.ReadyVersion == null && updates.CurrentVersion == null && !updates.ApplyAndRestart() && updates.Check(true).IsCompleted,
            "Not installed: no update checks, nothing to apply, no error");
        var dialog = PetDialog.Create(null, "Forget learned habits", "The pet will forget which actions and lines you liked.", "Forget", "Cancel", PetDialog.QuestionGlyph);
        dialog.Left = -10000; dialog.Show(); dialog.UpdateLayout();
        var dialogButtons = Descendants<System.Windows.Controls.Button>((FrameworkElement)dialog.Content).ToList();
        Check(dialogButtons.Count == 2 && dialogButtons[0].IsCancel && dialogButtons[1].IsDefault && (string)dialogButtons[1].Content == "Forget" &&
              dialog.Background == PetUi.Surface,
            "The forget-habits question uses the warm dialog: Cancel, then the main button on the right");
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string dialogReview && dialog.Content is FrameworkElement dialogRoot)
        {
            System.IO.Directory.CreateDirectory(dialogReview);
            var image = new RenderTargetBitmap((int)Math.Ceiling(dialogRoot.ActualWidth), (int)Math.Ceiling(dialogRoot.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            image.Render(dialogRoot);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = System.IO.File.Create(System.IO.Path.Combine(dialogReview, "dialog.png")); encoder.Save(stream);
        }
        dialog.Close();
        // First run after installing: tips and "Start with Windows" (ticked); never shown to a built or copied app.
        var welcome = PetWelcome.Create("en", out var welcomeAutostart, out var welcomeLanguage);
        welcome.Left = -10000; welcome.Show(); welcome.UpdateLayout();
        var welcomeButtons = Descendants<System.Windows.Controls.Button>((FrameworkElement)welcome.Content).ToList();
        bool WelcomeSays(string text) => Descendants<System.Windows.Controls.TextBlock>((FrameworkElement)welcome.Content).Any(t => t.Text.Contains(text));
        Check(welcomeAutostart.IsChecked == true && welcomeAutostart.IsVisible && welcomeButtons.Count == 1 && welcomeButtons[0].IsDefault &&
              (string)welcomeButtons[0].Content == "Let's go" && welcomeLanguage.SelectedIndex == 0 && WelcomeSays("tray icon"),
            "The welcome shows tips, the language, one button and a ticked Start with Windows");
        welcomeLanguage.SelectedIndex = 1; welcome.UpdateLayout();
        Check(WelcomeSays("Biểu tượng mèo ở khay") && (string)welcomeButtons[0].Content == "Bắt đầu" && (string)welcomeAutostart.Content == "Khởi động cùng Windows",
            "Choosing Tiếng Việt rewrites the welcome at once");
        Check(PetWelcome.DefaultLanguage(false, "vi") == "vi" && PetWelcome.DefaultLanguage(false, "en") == "en" &&
              PetWelcome.DefaultLanguage(true, "en") == (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? "vi" : "en"),
            "The welcome keeps a returning user's language; a new user starts in Windows' language");
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string welcomeReview && welcome.Content is FrameworkElement welcomeRoot)
        {
            System.IO.Directory.CreateDirectory(welcomeReview);
            var image = new RenderTargetBitmap((int)Math.Ceiling(welcomeRoot.ActualWidth), (int)Math.Ceiling(welcomeRoot.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            image.Render(welcomeRoot);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = System.IO.File.Create(System.IO.Path.Combine(welcomeReview, "welcome.png")); encoder.Save(stream);
        }
        welcome.Close();
        // Lay the closed menu out off-screen to check (and, for review, render) the rounded style.
        var styledMenu = window.ContextMenu;
        styledMenu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.ContextMenu.OpenedEvent)); // Fills labels and states.
        styledMenu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        styledMenu.Arrange(new Rect(styledMenu.DesiredSize));
        styledMenu.UpdateLayout();
        var firstItem = (System.Windows.Controls.MenuItem)styledMenu.Items[0];
        Check(styledMenu.Style != null && firstItem.Template?.FindName("Chrome", firstItem) is System.Windows.Controls.Border { CornerRadius.TopLeft: > 0 } &&
              styledMenu.DesiredSize.Width >= 200,
            "Context menu uses the rounded pet style for the menu and its items");
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string menuReview)
        {
            var image = new RenderTargetBitmap((int)Math.Ceiling(styledMenu.ActualWidth), (int)Math.Ceiling(styledMenu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            var backdrop = new DrawingVisual();
            using (var dc = backdrop.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x9C, 0xB8, 0xD2)), null, new Rect(0, 0, image.Width, image.Height));
                dc.DrawRectangle(new VisualBrush(styledMenu), null, new Rect(0, 0, styledMenu.ActualWidth, styledMenu.ActualHeight));
            }
            image.Render(backdrop);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            System.IO.Directory.CreateDirectory(menuReview);
            using var stream = System.IO.File.Create(System.IO.Path.Combine(menuReview, "context-menu.png")); encoder.Save(stream);
        }
        {
            // The tray menu uses the warm pet renderer; its items and their order stay as built.
            using var tray = new System.Windows.Forms.ContextMenuStrip();
            var trayPause = new System.Windows.Forms.ToolStripMenuItem("Tạm dừng") { Checked = true };
            tray.Items.Add(trayPause);
            tray.Items.Add(new System.Windows.Forms.ToolStripMenuItem("Mèo thông minh (AI)"));
            tray.Items.Add(new System.Windows.Forms.ToolStripMenuItem("AI: đang chạy") { Enabled = false });
            tray.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            tray.Items.Add(new System.Windows.Forms.ToolStripMenuItem("Thoát"));
            PetTrayMenuStyle.Apply(tray);
            Check(tray.Renderer is not System.Windows.Forms.ToolStripProfessionalRenderer && tray.Items.Count == 5 && tray.Items[0] == trayPause,
                "Tray menu uses the warm pet renderer and keeps its items");
            if (Environment.GetEnvironmentVariable("PET_TRAY_REVIEW") is string trayReview)
            {
                // Separate switch: drawing a WinForms menu here disturbs the timed battle checks that follow.
                tray.PerformLayout();
                using var bitmap = new System.Drawing.Bitmap(tray.Width, tray.Height);
                tray.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, tray.Width, tray.Height));
                System.IO.Directory.CreateDirectory(trayReview);
                bitmap.Save(System.IO.Path.Combine(trayReview, "tray-menu.png"));
            }
        }
        {
            // One UI language: every menu label (submenus included) follows preferences.Language.
            var options = Field<PetPreferences>(window, "preferences");
            static bool HasVietnamese(string text) => text.Any(c => "ăâđêôơưĂÂĐÊÔƠƯ".Contains(c) || (c >= 'à' && c <= 'ỹ' && c != '×' && c != '÷' && !"çñüöäßé".Contains(c)));
            List<string> Labels()
            {
                window.ContextMenu!.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.ContextMenu.OpenedEvent));
                var all = new List<string>();
                void Walk(System.Windows.Controls.ItemCollection items)
                {
                    foreach (var item in items.OfType<System.Windows.Controls.MenuItem>()) { all.Add(item.Header as string ?? ""); Walk(item.Items); }
                }
                Walk(window.ContextMenu.Items);
                return all;
            }
            string original = options.Language;
            options.Language = "en";
            var english = Labels();
            options.Language = "vi";
            var vietnamese = Labels();
            options.Language = original;
            Labels();
            Check(english.All(t => t.Length > 0 && !HasVietnamese(t)) && english.Contains("Do now") && english.Contains("Exit pet"),
                "English UI: every context-menu label (submenus included) is English");
            Check(vietnamese.Contains("Làm ngay") && vietnamese.Contains("Thoát mèo") && vietnamese.Contains("Cài đặt…") && vietnamese.Contains("Cảm xúc") &&
                  !vietnamese.Intersect(new[] { "Do now", "Emotions", "Exit pet", "Give treat", "Quiet mode", "Sounds", "Settings…", "More", "About Desktop Pet", "Behavior debug view" }).Any(),
                "Vietnamese UI: no English menu label is left behind");
            Check(PetWindow.LocalizeAiStatus("Python AI: connected (playful)", false) == "Python AI: đã kết nối (ham chơi)" &&
                  PetWindow.LocalizeAiStatus("Local behavior: timeout (retry after 4s)", false) == "Hành vi local: hết thời gian chờ (thử lại sau 4s)" &&
                  PetWindow.LocalizeAiStatus("Python AI: stopped", true) == "Python AI: stopped",
                "AI status follows the UI language (debug view keeps English)");
        }
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
        await Task.Delay(50);
        typeof(PetWindow).GetMethod("ShowSpeech", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, new object[] { new PetLine("hello:vi:test", "Chào sen! Mochi có mặt rồi đây~") });
        await Task.Delay(150);
        var speechWindow = Field<Window>(window, "speechWindow");
        var speechArea = Field<Area>(window, "bounds");
        Check(pet.Speech.Visible && speechWindow.IsVisible &&
              speechWindow.Top + speechWindow.ActualHeight <= pet.Y + 20 && speechWindow.Top >= speechArea.Top - 0.5 &&
              speechWindow.Left >= speechArea.Left - 0.5 && speechWindow.Left + speechWindow.ActualWidth <= speechArea.Right + 0.5 &&
              speechWindow.Left < pet.X + 56 && speechWindow.Left + speechWindow.ActualWidth > pet.X + 56,
            "Speech bubble window appears above the cat's head inside the work area");
        long exStyle = GetWindowLongPtr(new System.Windows.Interop.WindowInteropHelper(speechWindow).Handle, -20).ToInt64();
        Check((exStyle & 0x20) != 0 && (exStyle & 0x08000000) != 0 && !speechWindow.ShowInTaskbar && speechWindow.Topmost &&
              !speechWindow.IsActive, "Speech bubble is click-through, topmost and never takes focus");
        Check(window.Width == 192 && window.Height == 192, "Speaking does not resize the pet window");
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string speechReview)
        {
            void SaveBubble(string label)
            {
                var root = (FrameworkElement)speechWindow.Content;
                var image = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth + 12), (int)Math.Ceiling(root.ActualHeight + 12), 96, 96, PixelFormats.Pbgra32);
                image.Render(root);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                System.IO.Directory.CreateDirectory(speechReview);
                using var stream = System.IO.File.Create(System.IO.Path.Combine(speechReview, label + ".png")); encoder.Save(stream);
            }
            SaveBubble("speech-vi");
            pet.Speech.Hide(); await Task.Delay(50);
            pet.Speech.Step(pet, 20);
            typeof(PetWindow).GetMethod("ShowSpeech", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, new object[] { new PetLine("x:en:test", "What kind of animal is that cursor? Mochi wants to know!") });
            await Task.Delay(150);
            SaveBubble("speech-en-long");
        }
        pet.Action = PetAction.Dragging; pet.Grounded = false;
        await Task.Delay(80);
        Check(!pet.Speech.Visible && !speechWindow.IsVisible, "Speech bubble hides as soon as the pet is picked up");
        window.Reset();
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
        ThoughtVisualChecks.Run(Check);
        AppearanceVisualChecks.Run(Check);
        GestureVisualChecks.Run(Check);
        EnemyVisualChecks.Run(Check);
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
        {
            var toy = Field<Window?>(window, "toyWindow")!;
            var toyBall = Field<ToyBall>(window, "ball");
            double expectedTop = Field<Area>(window, "bounds").Bottom - 40 - toyBall.Height;
            Check(Math.Abs(toy.Left - (toyBall.X - 28)) < 1.5 && Math.Abs(toy.Top - expectedTop) < 1.5,
                "The ball window follows the ball with a single move per frame");
        }
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

        // Laser pointer game (the real cursor is never moved by these checks).
        window.Reset();
        Check(window.StartLaser() && window.LaserActive && Field<Window?>(window, "laserWindow") is { IsVisible: true, ShowActivated: false, Focusable: false, IsHitTestVisible: false },
            "Laser pointer shows a click-through red dot that never takes focus");
        Check((bool)typeof(PetWindow).GetMethod("FeatureOwnsMovement", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null)!,
            "The laser game owns movement (no random actions or AI moves meanwhile)");
        var laserChase = Field<LaserChase>(window, "laserChase");
        laserChase.Reset();
        for (int i = 0; i < 300 && laserChase.Phase != LaserPhase.Chase; i++) laserChase.Step(1.0 / 60, 300, true, 500, "Balanced");
        for (int i = 0; i < 300 && laserChase.Phase != LaserPhase.Pounce; i++) laserChase.Step(1.0 / 60, 30, true, 0, "Balanced");
        pet.Action = PetAction.Jumping; pet.Grounded = false;
        FeatureStep(0.05);
        Check(window.LaserActive && laserChase.Phase == LaserPhase.Pounce, "The game's own pounce keeps the game running while airborne");
        pet.Grounded = true; pet.Action = PetAction.Idle;
        FeatureStep(0.05);
        Check(window.LaserActive && laserChase.Phase == LaserPhase.Recover, "Landing ends the pounce and the game goes on");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        Check(!window.LaserActive && Field<Window?>(window, "laserWindow") == null, "Clicking the cat ends the laser game and removes the dot");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        window.Reset(); window.StartLaser();
        pet.Action = PetAction.Thrown; pet.Grounded = false;
        FeatureStep(0.05);
        Check(!window.LaserActive, "A throw ends the laser game");
        window.Reset(); window.StartLaser();
        FeatureStep(PetWindow.LaserDuration + 0.1);
        Check(!window.LaserActive, "The laser game ends by itself after 60 s");
        window.Reset(); window.StartLaser();
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        FeatureStep(0.1);
        Check(!window.LaserActive && pet.Emotions.SleepLocked, "Sleep ends the laser game without being interrupted");
        window.Reset();
        typeof(PetBehavior).GetMethod("WakeByClick")!.Invoke(Field<PetBehavior>(window, "behavior"), new object[] { pet });
        pet.Emotions.FinishSleep(0); pet.Action = PetAction.Idle;
        bool quietBefore = preferences.Quiet;
        preferences.Quiet = true; behavior.Quiet = true;
        Check(window.StartLaser() && window.LaserActive, "Quiet mode still lets the user start the laser game");
        FeatureStep(0.1);
        Check(window.LaserActive, "Quiet mode does not stop a laser game the user started");
        typeof(PetWindow).GetMethod("StopLaser", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        preferences.Quiet = quietBefore; behavior.Quiet = quietBefore;
        double? oldRestX = preferences.RestX, oldRestY = preferences.RestY;
        preferences.RestX = pet.X - 100; preferences.RestY = pet.Y;
        typeof(PetWindow).GetField("returningHome", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, true);
        FeatureStep(0.1);
        Check(pet.Action == PetAction.Walking && pet.VX < 0, "Favorite spot is approached by walking, not teleporting");
        preferences.RestX = oldRestX; preferences.RestY = oldRestY;
        window.Reset();
        window.ShowPetSettings();
        var customize = Field<Window>(window, "settingsWindow");
        var form = Field<PetSettingsForm>(window, "settingsForm");
        Check(customize.IsVisible, "Settings window opens on demand");
        var settingsArea = Field<Area>(window, "bounds");
        Check(customize.Height <= settingsArea.Bottom - settingsArea.Top && customize.Top >= settingsArea.Top &&
              customize.Top + customize.Height <= settingsArea.Bottom + 0.5 &&
              form.Tabs.Items.OfType<System.Windows.Controls.TabItem>().All(t => t.Content is System.Windows.Controls.ScrollViewer { VerticalScrollBarVisibility: System.Windows.Controls.ScrollBarVisibility.Auto }) &&
              !form.Save.IsDescendantOf(form.Tabs) && !form.Cancel.IsDescendantOf(form.Tabs) && form.Save.IsVisible,
            "Settings window fits the pet's monitor; tabs scroll while Save and Cancel stay visible outside them");
        Check(form.Tabs.Items.OfType<System.Windows.Controls.TabItem>().Select(t => (string)t.Header).SequenceEqual(new[] { "Pet", "Speech", "Sounds", "General" }),
            "Settings are split into Pet, Speech, Sounds and General tabs");
        Check(form.Volume.Value == preferences.Volume && form.PreviewCue.Items.Count == 9 && form.Sounds.IsChecked == preferences.Sounds,
            "Sound settings expose master/category switches, volume and all nine previews");
        bool soundsBefore = form.Sounds.IsChecked == true;
        form.Sounds.IsChecked = false;
        bool kindsOff = !form.InteractionSounds.IsEnabled && !form.EmotionSounds.IsEnabled && !form.SleepSounds.IsEnabled;
        form.Sounds.IsChecked = true;
        bool kindsOn = form.InteractionSounds.IsEnabled;
        form.Sounds.IsChecked = soundsBefore;
        form.Talks.IsChecked = false;
        bool chattinessOff = !form.Chattiness.IsEnabled;
        form.Talks.IsChecked = true;
        Check(kindsOff && kindsOn && chattinessOff && form.Chattiness.IsEnabled && form.BreakInterval.IsEnabled == (form.BreakReminders.IsChecked == true),
            "Dependent settings are greyed out while their switch is off");
        Check(form.Talks.IsChecked == true && form.Language.SelectedIndex == 0 && form.Language.Items.Count == 2 && (string)form.Language.Items[0] == "English" &&
              form.Chattiness.SelectedIndex == 1 && form.Forget != null && form.Autostart != null,
            "Settings default to English; speech defaults to talking, sometimes, with a forget link; startup lives in General");
        // Not installed (a build): the Updates card shows the version, automatic updates on but greyed, no check button.
        Check(form.AutoUpdate.IsChecked == true && !form.AutoUpdate.IsEnabled && !form.CheckUpdates.IsEnabled && (string)form.CheckUpdates.Content == "Check for updates" &&
              System.Text.RegularExpressions.Regex.IsMatch(PetWindow.VersionText, @"^\d+\.\d+\.\d+"),
            "General has Updates: version, automatic updates (off limits when not installed) and Check for updates");
        Check(form.Uninstall.Visibility == Visibility.Collapsed && !new PetUpdates().StartUninstall(true),
            "Not installed: no Uninstall button, and uninstalling does nothing");
        var uninstallQuestion = PetWindow.UninstallQuestion(window.L, out var removeData);
        uninstallQuestion.Left = -10000; uninstallQuestion.Show(); uninstallQuestion.UpdateLayout();
        var uninstallButtons = Descendants<System.Windows.Controls.Button>((FrameworkElement)uninstallQuestion.Content).ToList();
        Check(removeData.IsChecked == false && removeData.IsVisible && uninstallButtons.Count == 2 && uninstallButtons[0].IsCancel && (string)uninstallButtons[1].Content == "Uninstall",
            "Uninstalling asks first; deleting the pet's data is a separate, unticked choice");
        uninstallQuestion.Close();
        // The uninstall hook deletes the data only when the in-app uninstall left its marker.
        string uninstallTest = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PetUninstallCheck-" + Guid.NewGuid().ToString("N"));
        try
        {
            string root = System.IO.Path.Combine(uninstallTest, "PetComputer"), data = System.IO.Path.Combine(uninstallTest, "DesktopPet");
            System.IO.Directory.CreateDirectory(root); System.IO.Directory.CreateDirectory(data);
            System.IO.File.WriteAllText(System.IO.Path.Combine(data, "preferences.json"), "{}");
            bool keptWithoutMarker = !PetUpdates.OnUninstall(root, data, null) && System.IO.File.Exists(System.IO.Path.Combine(data, "preferences.json"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(root, PetUpdates.RemoveDataMarker), "");
            bool removedWithMarker = PetUpdates.OnUninstall(root, data, null) && !System.IO.Directory.Exists(data) &&
                                     !System.IO.File.Exists(System.IO.Path.Combine(root, PetUpdates.RemoveDataMarker));
            System.IO.File.WriteAllText(System.IO.Path.Combine(root, PetUpdates.RemoveDataMarker), "");
            bool refusesInstallFolder = !PetUpdates.OnUninstall(root, root, null) && System.IO.Directory.Exists(root);
            Check(keptWithoutMarker && removedWithMarker && refusesInstallFolder,
                "Uninstalling keeps the data unless asked; when asked it deletes the data folder, never the install folder");
        }
        finally { if (System.IO.Directory.Exists(uninstallTest)) System.IO.Directory.Delete(uninstallTest, true); }
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string settingsReview)
        {
            // Every tab in both languages, for a human look at the layout.
            System.IO.Directory.CreateDirectory(settingsReview);
            string languageBefore = preferences.Language;
            foreach (string language in new[] { "en", "vi" })
            {
                customize.Close();
                preferences.Language = language;
                window.ShowPetSettings();
                var reviewWindow = Field<Window>(window, "settingsWindow");
                var reviewForm = Field<PetSettingsForm>(window, "settingsForm");
                for (int tab = 0; tab < reviewForm.Tabs.Items.Count; tab++)
                {
                    reviewForm.Tabs.SelectedIndex = tab;
                    reviewWindow.UpdateLayout();
                    var root = (FrameworkElement)reviewWindow.Content;
                    var image = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    image.Render(root);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                    using var stream = System.IO.File.Create(System.IO.Path.Combine(settingsReview, $"settings-{language}-{tab}.png")); encoder.Save(stream);
                }
                customize = reviewWindow;
                window.ShowControls();
                var menu = window.ContextMenu!;
                menu.UpdateLayout();
                var menuImage = new RenderTargetBitmap((int)menu.ActualWidth, (int)menu.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                menuImage.Render(menu);
                var menuEncoder = new PngBitmapEncoder(); menuEncoder.Frames.Add(BitmapFrame.Create(menuImage));
                using (var menuStream = System.IO.File.Create(System.IO.Path.Combine(settingsReview, $"menu-{language}.png"))) menuEncoder.Save(menuStream);
                menu.IsOpen = false;
            }
            customize.Close();
            preferences.Language = languageBefore;
            window.ShowPetSettings();
            customize = Field<Window>(window, "settingsWindow");
            form = Field<PetSettingsForm>(window, "settingsForm");
        }
        string nameBefore = preferences.Name;
        double volumeBefore = preferences.Volume;
        form.Name.Text = "Changed"; form.Volume.Value = 0.1;
        form.Cancel.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check(!customize.IsVisible && Field<Window?>(window, "settingsWindow") == null && preferences.Name == nameBefore && preferences.Volume == volumeBefore,
            "Cancel closes settings without changing anything");
        window.ShowPetSettings();
        customize = Field<Window>(window, "settingsWindow");
        var playSound = typeof(PetWindow).GetMethod("PlayPetSound", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Check(!(bool)playSound.Invoke(window, new object?[] { "meow", true, 0.0 })!, "Zero-volume preview stays silent");
        typeof(PetWindow).GetMethod("ShowBehaviorSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        var tuningWindow = Field<Window>(window, "behaviorSettingsWindow");
        var tuningSliders = Descendants<System.Windows.Controls.Slider>(tuningWindow).ToArray();
        System.Windows.Controls.Button TuningButton(bool save) => Descendants<System.Windows.Controls.Button>(tuningWindow)
            .Single(b => save ? b.IsDefault : b.Content is System.Windows.Controls.TextBlock);
        Check(tuningWindow.IsVisible && tuningSliders.Length == 8, "Tuning editor exposes all eight supported JSON values");
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string tuningReview)
        {
            tuningWindow.UpdateLayout();
            var root = (FrameworkElement)tuningWindow.Content;
            var image = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            image.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = System.IO.File.Create(System.IO.Path.Combine(tuningReview, "tuning-window.png")); encoder.Save(stream);
        }
        tuningSliders[0].Value = 0.23;
        TuningButton(true).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(preferences.Tuning?.Playfulness == 0.23, "Tuning save applies draft without writing personal test preferences");
        typeof(PetWindow).GetMethod("ShowBehaviorSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
        tuningWindow = Field<Window>(window, "behaviorSettingsWindow");
        Descendants<System.Windows.Controls.TextBox>(tuningWindow).First().Text = "NaN";
        Check(!TuningButton(true).IsEnabled, "Tuning editor refuses nonfinite input");
        TuningButton(false).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        TuningButton(true).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
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
        // Combinations from review feedback (2026-10-15).
        {
            // Clicks, a Quiet toggle path and an AI toggle during waking keep it at ~1.2 s.
            window.Reset();
            pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
            var rightClick = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right) { RoutedEvent = UIElement.MouseRightButtonDownEvent };
            window.RaiseEvent(rightClick);
            double waking = 0;
            for (; waking < 0.6; waking += 1.0 / 60) behavior.Step(pet, 1.0 / 60);
            window.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right) { RoutedEvent = UIElement.MouseRightButtonDownEvent });
            window.SetAiEnabled(false);
            typeof(PetWindow).GetMethod("CancelFeatureActivity", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null); // Quiet menu path.
            while (pet.Action == PetAction.Waking && waking < 5) { behavior.Step(pet, 1.0 / 60); waking += 1.0 / 60; }
            Check(Math.Abs(waking - PetSettings.WakingDuration) < 0.05,
                $"Right-click, AI toggle and Quiet toggle during waking do not stretch it ({waking:F2} s)");

            // A reply chosen before a language change does not speak in the old language.
            window.Reset();
            pet.Speech.Hide(); pet.Speech.Step(pet, 20);
            string languageBefore = preferences.Language;
            bool speechBefore = preferences.Speech;
            preferences.Speech = true; preferences.Language = "en";
            var requestDecision = typeof(PetWindow).GetMethod("RequestDecision", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var aiField = typeof(PetWindow).GetField("ai", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var staleSource = new DelayedSource();
            aiField.SetValue(window, staleSource);
            requestDecision.Invoke(window, null);
            await Task.Delay(20);
            preferences.Language = "vi"; // The user saved another language meanwhile.
            staleSource.Completion.SetResult(new AiDecision("IDLE", 3, "content", Needs: new PetNeeds(11, 22, 33, 44), Speech: new PetLine("hello:en:test", "Hi human!")));
            await Task.Delay(30);
            Check(!pet.Speech.Visible && pet.Speech.PendingId == null && Field<PetNeeds?>(window, "aiNeeds")?.Hunger == 11,
                "A line chosen before a language change is dropped, while the needs from that reply are kept");
            var freshSource = new DelayedSource();
            aiField.SetValue(window, freshSource);
            requestDecision.Invoke(window, null);
            await Task.Delay(20);
            freshSource.Completion.SetResult(new AiDecision("IDLE", 3, "content", Speech: new PetLine("hello:vi:test", "Chào sen!")));
            await Task.Delay(30);
            Check(pet.Speech.Visible && pet.Speech.Id == "hello:vi:test", "A line in the current language is still spoken");
            pet.Speech.Hide(); pet.Speech.Step(pet, 20);

            // Holiday outfit: Python names the special day, C# picks its accessory; preferences stay as chosen.
            var shown = (PetRenderer)window.Content;
            async Task Reply(string? day)
            {
                var source = new DelayedSource();
                aiField.SetValue(window, source);
                requestDecision.Invoke(window, null);
                await Task.Delay(20);
                source.Completion.SetResult(new AiDecision("IDLE", 3, "content", SpecialDay: day));
                await Task.Delay(30);
            }
            string chosen = preferences.Accessory;
            await Reply("christmas");
            Check(shown.Accessory == "SantaHat" && preferences.Accessory == chosen, "On Christmas the pet wears the Santa hat without changing the saved accessory");
            await Reply(null);
            Check(shown.Accessory == chosen, "The next ordinary day it wears the user's accessory again");
            preferences.HolidayAccessories = false;
            await Reply("halloween");
            Check(shown.Accessory == chosen, "With Dress up for holidays off, special days change nothing");
            preferences.HolidayAccessories = true;
            await Reply("birthday");
            bool party = shown.Accessory == "Hat";
            window.StopAi();
            Check(party && shown.Accessory == chosen, "The user's birthday brings the party hat; turning the AI off takes it off");
            aiField.SetValue(window, null);
            preferences.Language = languageBefore; preferences.Speech = speechBefore;
        }
        ContextChecks(window, pet, behavior, preferences);
        LifeChecks(window, pet, preferences);
        LedgeChecks.Run(window, pet, behavior, preferences, Check);
        FrameChecks.Run(window, pet, Check);
        await BattleChecks.Run(window, pet, behavior, preferences, Check);
        await BattleRulesChecks.Run(window, pet, Check);
        await TutorialChecks.Run(window, pet, Check);
        await WeaponChecks.Run(window, pet, Check);
        await BattlePrepChecks.Run(window, pet, behavior, preferences, Check);
        await AdventureChecks.Run(window, pet, behavior, preferences, Check);
        await ExpeditionChecks.Run(window, pet, behavior, preferences, Check);
        await EndlessChecks.Run(window, pet, behavior, preferences, Check);
        await UpgradeChecks.Run(window, pet, behavior, preferences, Check);
        await PanelHeightChecks.Run(window, pet, preferences, Check);
        await BagChecks.Run(window, pet, behavior, preferences, Check);
        await ContentChecks.Run(window, pet, behavior, preferences, Check);
        await AcceptanceChecks.Run(window, pet, behavior, preferences, Check);
        window.ShowControls();
        Check(window.ContextMenu!.IsOpen, "Existing pet controls can be opened by a second launch notification");
        window.ContextMenu.IsOpen = false;
        preferences.Quiet = true; behavior.Quiet = true;
        window.Reset();
        window.ToggleToy();
        FeatureStep(0.1);
        Check(Field<Window?>(window, "toyWindow")?.IsVisible == true, "Quiet mode still lets the user play with the ball");
        window.ToggleToy();

        // "Do now" menu: immediate actions (even in Quiet), queued after waking, manual emotions.
        var doNow = window.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>().FirstOrDefault(item => item.Header is "Làm ngay" or "Do now");
        var feelings = window.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>().FirstOrDefault(item => item.Header is "Cảm xúc" or "Emotions");
        Check(doNow != null && doNow.Items.OfType<System.Windows.Controls.MenuItem>().Count(item => item.Visibility == Visibility.Visible) == 12 &&
              feelings != null && feelings.Items.OfType<System.Windows.Controls.MenuItem>().Count() == 6,
            "Context menu offers Do now with 12 actions (\"Sleep on the cushion\" hidden without the cushion) and Emotions with the manual switch and 5 emotions");
        window.Reset();
        Check(window.DoNow(PetAction.ChasingTail) && pet.Action == PetAction.ChasingTail, "Do now starts a gesture at once, even in Quiet mode");
        behavior.Step(pet, 0.5);
        Check(pet.Action == PetAction.ChasingTail, "Quiet mode does not cancel the requested gesture");
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        window.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            { RoutedEvent = UIElement.MouseRightButtonDownEvent });
        bool queuedWhileWaking = pet.Action == PetAction.Waking && !window.DoNow(PetAction.Grooming) &&
                                 Field<PetAction?>(window, "queuedCommand") == PetAction.Grooming;
        var tickCommands = typeof(PetWindow).GetMethod("TickCommands", BindingFlags.NonPublic | BindingFlags.Instance)!;
        tickCommands.Invoke(window, null);
        bool waitedForWaking = pet.Action == PetAction.Waking;
        for (int i = 0; i < 80 && pet.Action == PetAction.Waking; i++) behavior.Step(pet, 1.0 / 60);
        tickCommands.Invoke(window, null);
        Check(queuedWhileWaking && waitedForWaking && pet.Action == PetAction.Grooming && Field<PetAction?>(window, "queuedCommand") == null,
            "A request right after the right-click wake waits for waking to finish, then runs");
        window.Reset();
        Check(!window.PerformEmotion(PetEmotion.Angry), "Emotions need the manual-control switch first");
        window.SetManualEmotions(true);
        Check(window.PerformEmotion(PetEmotion.Sad) && pet.Emotions.ManualEmotion == PetEmotion.Sad && pet.Emotions.Reported == PetEmotion.Neutral,
            "With the switch on the chosen emotion is held and Python sees Neutral");
        window.SetManualEmotions(false);
        Check(!pet.Emotions.ManualMode && pet.Emotions.ManualEmotion == PetEmotion.Neutral, "Turning the switch off returns to automatic emotions");
        preferences.Quiet = originalQuiet; behavior.Quiet = originalQuiet;
        window.Reset();
        bool battleAtExit = window.StartBattle() == null;
        window.Close();
        Check(ai.Disposed && !window.IsVisible, "Window/AI shutdown releases the decision source and hides the pet");
        Check(battleAtExit && !window.BattleActive && Application.Current.Windows.OfType<Window>().All(w => !w.Title.StartsWith("Pet battle") || !w.IsVisible),
            "Closing the pet during a battle closes the battle windows too");
    }

    private static void ContextChecks(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences)
    {
        var renderer = (PetRenderer)window.Content;
        window.ShowPetSettings();
        var form = Field<PetSettingsForm>(window, "settingsForm");
        var looks = new[] { form.Coat, form.Accessory };
        var reminder = form.BreakReminders;
        var interval = form.BreakInterval;
        Check(looks.Length == 2 && looks[0].Items.Count == 10 && looks[1].Items.Count == 12 && form.HolidayAccessories.IsChecked == true && looks[0].SelectedIndex == 0 && looks[1].SelectedIndex == 0 &&
              reminder.IsChecked == false && interval.SelectedIndex == 1,
            "Settings offer ten coats, twelve accessories and break reminders (off, 45 minutes)");
        // The preview follows the pickers and moves gently; the real pet waits for Save.
        looks[0].SelectedIndex = 9; looks[1].SelectedIndex = 7;
        double previewTime = form.PreviewPet.AnimationTime;
        var frame = new System.Windows.Threading.DispatcherFrame();
        var wait = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        wait.Tick += (_, _) => { wait.Stop(); frame.Continue = false; };
        wait.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame); // Let the preview timer tick a few times.
        Check(form.PetPreview.Coat == "Siamese" && form.PetPreview.Accessory == "Crown" && renderer.Coat == "Orange" && renderer.Accessory == "None" &&
              form.PreviewPet.AnimationTime > previewTime && form.PreviewTimer.IsEnabled,
            "The settings preview shows the chosen coat and accessory at once and animates, without changing the real pet");
        looks[0].SelectedIndex = 2; looks[1].SelectedIndex = 3; reminder.IsChecked = true; interval.SelectedIndex = 0;
        Check(form.BirthDay.SelectedIndex == 0 && form.BirthMonth.SelectedIndex == 0 && form.BirthDay.Items.Count == 32 && form.BirthMonth.Items.Count == 13,
            "Settings offer an unset birthday (day 1-31, month 1-12)");
        form.BirthDay.SelectedIndex = 31; form.BirthMonth.SelectedIndex = 2; // 31 February.
        form.AutoUpdate.IsChecked = false;
        form.Save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check(!preferences.AutoUpdate && !window.AutoUpdate, "Saving keeps automatic updates off when unticked");
        preferences.AutoUpdate = true;
        // What's new after an update: the first installed start records its version; a later one reports the change.
        Check(window.RecordVersion("1.4.1") == null && window.RecordVersion("1.4.1") == "1.4.1" && window.RecordVersion("1.4.2") == "1.4.1" &&
              preferences.LastVersion == "1.4.2", "The installed version is recorded so an update can say what's new");
        preferences.LastVersion = null;
        // The welcome's language choice switches the whole app like Settings does.
        int languageEvents = 0; void CountLanguage() => languageEvents++;
        window.LanguageChanged += CountLanguage;
        window.SetLanguage("vi"); window.SetLanguage("vi");
        bool languageSet = window.Language == "vi" && languageEvents == 1;
        window.SetLanguage("en"); window.LanguageChanged -= CountLanguage;
        Check(languageSet && window.Language == "en" && languageEvents == 2, "SetLanguage switches the app's language once per change");
        var breaks = Field<PetBreaks>(window, "breaks");
        Check(preferences.Coat == "Black" && preferences.Accessory == "Hat" && renderer.Coat == "Black" && renderer.Accessory == "Hat" &&
              preferences.BreakReminders && preferences.BreakMinutes == 30 && breaks.Enabled && breaks.IntervalSeconds == 1800,
            "Saving applies the coat, accessory and break interval at once");
        Check(preferences.Birthday == "02-29", "A birthday past the month's end is saved as its last day (31 Feb -> 02-29)");
        window.ShowPetSettings();
        form = Field<PetSettingsForm>(window, "settingsForm");
        Check(form.BirthDay.SelectedIndex == 29 && form.BirthMonth.SelectedIndex == 2, "Settings show the saved birthday again");
        form.BirthMonth.SelectedIndex = 0;
        form.Save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check(preferences.Birthday == "", "Clearing the month removes the birthday");
        Check(!form.PreviewTimer.IsEnabled, "Closing settings stops the preview animation");
        renderer.Coat = preferences.Coat = "Orange"; renderer.Accessory = preferences.Accessory = "None";

        bool quiet = preferences.Quiet, sounds = preferences.Sounds;
        preferences.Sounds = true;
        SetContext(window, true, 0);
        TickContext(window, 1.1);
        var playSound = typeof(PetWindow).GetMethod("PlayPetSound", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var showSpeech = typeof(PetWindow).GetMethod("ShowSpeech", BindingFlags.NonPublic | BindingFlags.Instance)!;
        showSpeech.Invoke(window, new object[] { new DesktopPet.AI.PetLine("hello:vi:test", "Chào sen!") });
        Check(behavior.Quiet && preferences.Quiet == quiet && !(bool)playSound.Invoke(window, new object?[] { "meow", false, null })! && !pet.Speech.Visible,
            "A fullscreen app makes the pet quiet and silent without changing the saved Quiet setting");
        window.ToggleToy();
        bool ballBlocked = Field<Window?>(window, "toyWindow") == null;
        if (!ballBlocked) window.ToggleToy();
        Check(ballBlocked && !window.StartLaser() && !window.LaserActive, "A fullscreen app still blocks the ball and the laser game");
        SetContext(window, false, 0);
        TickContext(window, 1.1);
        Check(behavior.Quiet == quiet, "Leaving fullscreen restores the user's Quiet setting");
        preferences.Sounds = sounds;

        SetContext(window, false, 400);
        TickContext(window, 1.1);
        Check(pet.Action == PetAction.Sleeping && behavior.HoldSleep && pet.Emotions.SleepLocked, "Five minutes without input starts the away nap");
        // The window timer is stopped in this phase of the checks, so step the behavior directly.
        for (int i = 0; i < 20 * 120; i++) { pet.Emotions.Step(pet, 1.0 / 120); behavior.Step(pet, 1.0 / 120); }
        Check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepElapsed == 0, "The away nap outlasts the normal 10 s sleep");
        SetContext(window, false, 0.5);
        TickContext(window, 1.1);
        var context = Field<PetContext>(window, "context");
        Check(pet.Action == PetAction.Waking && !behavior.HoldSleep && context.Returned, "Input after being away wakes the pet and flags the return for the AI");
        context.TakeReturned();
        for (int i = 0; i < 2 * 120; i++) { pet.Emotions.Step(pet, 1.0 / 120); behavior.Step(pet, 1.0 / 120); } // Waking finishes.
        pet.Action = PetAction.Idle; // Keep the free pose (a random local jump cannot speak).

        breaks.Configure(true, 1);
        pet.Speech.Hide(); pet.Speech.Step(pet, 20); // Clear the gap left by earlier speech checks.
        TickContext(window, 61);
        Check(pet.Speech.Visible && PetBreaks.IsBreakLine(pet.Speech.Id) && !breaks.Due,
            "With the AI off a due break reminder uses a built-in line");
        pet.Speech.Hide();
        breaks.Configure(false, 45);
        preferences.BreakReminders = false; preferences.BreakMinutes = 45;
    }

    private static void LifeChecks(PetWindow window, PetModel pet, PetPreferences preferences)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        void Call(string name, params object[] args) => typeof(PetWindow).GetMethod(name, flags)!.Invoke(window, args);
        // Pending speech: a line during eating appears right after the meal.
        window.Reset();
        pet.Speech.Hide(); pet.Speech.Step(pet, 20);
        pet.TreatRemaining = 2;
        Call("ShowSpeech", new DesktopPet.AI.PetLine("treat_thanks:vi:test", "Ngon quá sen ơi!"));
        bool waited = !pet.Speech.Visible && pet.Speech.PendingId == "treat_thanks:vi:test";
        pet.TreatRemaining = 0;
        Call("TickPendingSpeech");
        Check(waited && pet.Speech.Visible && pet.Speech.Id == "treat_thanks:vi:test", "A line that arrives while eating is spoken right after");
        pet.Speech.Hide(); pet.Speech.Step(pet, 20);
        pet.TreatRemaining = 2;
        Call("ShowSpeech", new DesktopPet.AI.PetLine("petted:vi:test", "Purr~"));
        pet.TreatRemaining = 0;
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        Call("TickPendingSpeech");
        Check(!pet.Speech.Visible && pet.Speech.PendingId == null, "Falling asleep drops a pending line instead of saying it late");
        Call("ShowSpeech", new DesktopPet.AI.PetLine("hello:vi:test", "Chào sen!"));
        Call("TickPendingSpeech");
        bool heldAsleep = !pet.Speech.Visible && pet.Speech.PendingId == "hello:vi:test";
        window.Reset();
        typeof(PetBehavior).GetMethod("WakeByClick")!.Invoke(Field<PetBehavior>(window, "behavior"), new object[] { pet });
        pet.Emotions.FinishSleep(0); pet.Action = PetAction.Idle;
        Call("TickPendingSpeech");
        Check(heldAsleep && pet.Speech.Visible && pet.Speech.Id == "hello:vi:test",
            "The greeting waits silently through a nap and is said after waking");
        pet.Speech.Hide(); pet.Speech.Step(pet, 20);

        // Two strokes within 10 s start kneading.
        Call("Interact", "petted");
        bool afterOne = pet.Action != PetAction.Kneading;
        Call("Interact", "petted");
        Check(afterOne && pet.Action == PetAction.Kneading, "Sustained petting makes the pet sit and knead");
        pet.Action = PetAction.Idle;

        // Diary window: session numbers with the AI off, full diary from Python's summary.
        string language = preferences.Language;
        preferences.Language = "vi";
        window.ShowDiary();
        var diaryWindow = Field<Window>(window, "diaryWindow");
        var diaryPanel = Field<System.Windows.Controls.StackPanel>(window, "diaryPanel");
        string Texts() => string.Join("\n", Descendants<System.Windows.Controls.TextBlock>(diaryPanel).Select(t => t.Text));
        Check(diaryWindow.IsVisible && Texts().Contains("AI đang tắt") && Texts().Contains("Được vuốt"), "Diary opens and explains it only has this session's numbers when the AI is off");
        var habits = Enumerable.Range(0, 24).Select(h => h is 20 or 21 ? 8.0 : h == 9 ? 3 : 0).ToArray();
        typeof(PetWindow).GetField("aiDiary", flags)!.SetValue(window,
            new DesktopPet.AI.PetDiary(12, "2026-10-01", 40, 9, 7, 1, 30, 61.5, new[] { 50, 55.5, 61.5 }, habits));
        preferences.Language = "en";
        Call("RefreshDiary");
        var charts = Descendants<System.Windows.Controls.Canvas>(diaryPanel).ToArray();
        Check(Texts().Contains("Together for 12 days (since 2026-10-01)") && Texts().Contains("Most often: 20h, 21h, 9h") &&
              Texts().Contains("Bond: 62/100") && charts.Length == 2 &&
              charts[1].Children.OfType<System.Windows.Controls.Border>().Count(b => b.IsHitTestVisible == false) == 3,
            "Diary shows days together, totals, a bond chart and the hours the user plays (in the chosen language)");
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review)
        {
            diaryWindow.UpdateLayout();
            var root = (FrameworkElement)diaryWindow.Content;
            var image = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            image.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = System.IO.File.Create(System.IO.Path.Combine(review, "diary-window.png")); encoder.Save(stream);
        }
        diaryWindow.Close();
        preferences.Language = language;
        typeof(PetWindow).GetField("aiDiary", flags)!.SetValue(window, null);
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
