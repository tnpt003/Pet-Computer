using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopPet.AI;
using Forms = System.Windows.Forms;

namespace DesktopPet;

public sealed partial class PetWindow : Window
{
    private readonly PetSettings settings = new();
    private readonly PetModel pet = new();
    private readonly PetPhysics physics;
    private readonly PetBehavior behavior;
    private readonly PetRenderer renderer;
    private readonly ThrowTracker tracker = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private Area bounds;
    private double scale = 1, previous, accumulator, boundsTimer;
    private const double PhysicsStep = 1.0 / 120;
    // Last physics state before the newest step; drawing blends toward the current one.
    private double previousX, previousY;
    private double frameStatsStart, frameMaxGap, measuredFps, measuredMaxGap;
    private int frameCount;
    private Point dragOffset;
    private bool dragging, paused;
    private bool pressPending;
    private Point pressPosition;
    private Window? debugWindow;
    private TextBlock? debugText;
    private double? aiEnergy;
    private PetNeeds? aiNeeds;
    private double nextDebugUpdate;
    private readonly bool persistPreferences;
    private IPetDecisionSource? ai;
    private bool aiBusy, closed;
    private long revision, interactionCount;
    private string lastEvent = "started";
    private readonly string sessionId = Guid.NewGuid().ToString("N");
    public string AiStatus => ai?.Status ?? "Python AI: off (local behavior)";
    public bool AiEnabled => ai != null;

    public PetWindow(PetPreferences? options = null)
    {
        preferences = options ?? PetPreferences.Load();
        persistPreferences = options == null;
        Title = "Desktop Pet";
        Icon = AppIcon.WindowImage();
        // Keep one backing surface for every pose; resizing layered windows on
        // mouse-down can expose a partially updated/clipped frame in the compositor.
        Width = settings.Width + PickupAnimation.Padding * 2;
        Height = settings.Height + PickupAnimation.Padding * 2;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        Cursor = Cursors.Hand;
        physics = new PetPhysics(settings);
        behavior = new PetBehavior(settings);
        behavior.DecisionRequested += RequestDecision;
        if (Environment.GetEnvironmentVariable("PET_AI_DISABLED") != "1") ai = new PythonDecisionSource();
        renderer = new PetRenderer(pet);
        Content = renderer;
        var menu = new ContextMenu();
        var reset = new MenuItem { Header = "Reset to primary screen" };
        reset.Click += (_, _) => Reset();
        var exit = new MenuItem { Header = "Exit pet" };
        exit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(reset);
        var debug = new MenuItem { Header = "Behavior debug view" };
        debug.Click += (_, _) => ToggleDebug();
        menu.Items.Add(debug);
        menu.Items.Add(exit);
        ContextMenu = menu;
        InitializeFeatures(menu);
        SourceInitialized += (_, _) =>
        {
            var source = (HwndSource)PresentationSource.FromVisual(this);
            scale = source.CompositionTarget.TransformToDevice.M11;
            source.AddHook(WindowHook);
            Reset();
        };
        MouseLeftButtonDown += BeginDrag;
        MouseMove += (_, _) => CheckDragThreshold();
        MouseRightButtonDown += (_, _) => { CancelFeatureActivity(); behavior.WakeByClick(pet); pet.Emotions.Attention(click: true); Interact("right_clicked"); };
        MouseLeftButtonUp += (_, _) => EndDrag(true);
        LostMouseCapture += (_, _) => EndDrag(false);
        timer.Tick += Tick;
        DpiChanged += (_, e) => ChangeMonitorScale(e.NewDpi.DpiScaleX);
        Loaded += (_, _) => { RestorePosition(); previous = clock.Elapsed.TotalSeconds; timer.Start(); };
        Closed += (_, _) => { SavePosition(); closed = true; timer.Stop(); StopAi(); debugWindow?.Close(); CloseFeatures(); };
    }

    private static IntPtr WindowHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0021) // WM_MOUSEACTIVATE: mouse input without stealing keyboard focus.
        {
            handled = true;
            return new IntPtr(3); // MA_NOACTIVATE
        }
        return IntPtr.Zero;
    }

    private Area WorkArea(Forms.Screen screen)
    {
        var area = screen.WorkingArea;
        return new Area(area.Left / scale, area.Top / scale, area.Right / scale, area.Bottom / scale);
    }

    private Point CursorPosition()
    {
        GetCursorPos(out var point);
        return new Point(point.X / scale, point.Y / scale);
    }

    private void SelectMonitor()
    {
        var center = new System.Drawing.Point((int)((pet.X + settings.Width / 2) * scale),
            (int)((pet.Y + settings.Height / 2) * scale));
        bounds = WorkArea(Forms.Screen.FromPoint(center));
        RefreshRestingSpot();
    }

    public void Reset()
    {
        CancelFeatureActivity();
        bool keepSleeping = pet.Emotions.SleepLocked || pet.Action == PetAction.Sleeping;
        Interact("reset");
        EndDrag(false);
        bounds = WorkArea(Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0]);
        pet.X = (bounds.Left + bounds.Right - settings.Width) / 2;
        pet.Y = bounds.Bottom - settings.Height;
        physics.Clamp(pet, bounds);
        pet.VX = pet.VY = 0;
        pet.Grounded = true;
        pet.Action = PetAction.Idle;
        behavior.Reset();
        pet.Emotions.Reset();
        if (keepSleeping)
        {
            pet.Action = PetAction.Sleeping;
            pet.Emotions.EnterSleep();
        }
        pet.Motion.Reset(pet);
        Present();
    }

    public void SetPaused(bool value)
    {
        Interact(value ? "paused" : "resumed");
        EndDrag(false);
        paused = value;
        accumulator = 0;
    }

    private void BeginDrag(object sender, MouseButtonEventArgs e)
    {
        CancelFeatureActivity();
        behavior.WakeByClick(pet);
        pet.Emotions.Attention(click: true);
        if (!CaptureMouse()) { Interact("left_clicked"); return; }
        Interact("left_clicked");
        pressPending = true;
        pressPosition = CursorPosition();
        if (pet.Grounded && pet.Action != PetAction.Waking)
        {
            pet.Action = PetAction.Idle;
            pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
        }
        e.Handled = true;
    }

    private void CheckDragThreshold()
    {
        if (!pressPending || !IsMouseCaptured) return;
        if ((CursorPosition() - pressPosition).Length < settings.DragThreshold) return;
        StartDrag();
    }

    private void StartDrag()
    {
        pressPending = false;
        Interact("drag_started");
        behavior.Reset();
        dragging = true;
        var cursor = CursorPosition();
        dragOffset = new Point(cursor.X - pet.X, cursor.Y - pet.Y);
        pet.VX = pet.VY = 0;
        pet.Grounded = false;
        pet.Action = PetAction.Dragging;
        pet.Pickup.Begin(cursor.X, cursor.Y);
        Present();
        tracker.Clear();
        tracker.Add(clock.Elapsed.TotalSeconds, cursor.X, cursor.Y);
    }

    private void UpdateDrag(double dt = 0)
    {
        var cursor = CursorPosition();
        if (dt > 0)
        {
            // Ease the original grab into a head anchor instead of snapping on pickup.
            double ease = 1 - Math.Exp(-12 * dt);
            dragOffset = new Point(dragOffset.X + (PickupAnimation.AnchorX - dragOffset.X) * ease,
                dragOffset.Y + (PickupAnimation.AnchorY - dragOffset.Y) * ease);
            if (!paused) pet.Pickup.Step(cursor.X, cursor.Y, dt);
        }
        pet.X = cursor.X - dragOffset.X;
        pet.Y = cursor.Y - dragOffset.Y;
        bounds = WorkArea(Forms.Screen.FromPoint(new System.Drawing.Point((int)(cursor.X * scale), (int)(cursor.Y * scale))));
        var art = pet.Pickup.VisualBounds(settings.Width, settings.Height);
        // Near the bottom edge the head cannot sit exactly under the cursor without
        // losing the feet. Keep all art visible until the cursor provides room.
        pet.X = Math.Clamp(pet.X, bounds.Left - art.Left, Math.Max(bounds.Left - art.Left, bounds.Right - art.Right));
        pet.Y = Math.Clamp(pet.Y, bounds.Top - art.Top, Math.Max(bounds.Top - art.Top, bounds.Bottom - art.Bottom));
        tracker.Add(clock.Elapsed.TotalSeconds, cursor.X, cursor.Y);
    }

    private void EndDrag(bool throwPet)
    {
        if (pressPending)
        {
            pressPending = false;
            ReleaseMouseCapture();
            return; // A click never enters thrown physics.
        }
        if (!dragging) return;
        Interact(throwPet ? "released" : "drag_cancelled");
        UpdateDrag();
        dragging = false; // Set before releasing capture; LostMouseCapture is synchronous.
        ReleaseMouseCapture();
        (pet.VX, pet.VY) = throwPet && !paused ? tracker.Velocity(settings.MaxThrowSpeed) : (0.0, 0.0);
        if (throwPet && !paused)
        {
            if (Math.Sqrt(pet.VX * pet.VX + pet.VY * pet.VY) >= 1600) { pet.Emotions.AggressiveInteraction(); pet.Interactions.RoughThrow(); }
            else pet.Emotions.Attention();
        }
        pet.Action = PetAction.Thrown;
        pet.Pickup.Reset();
        pet.Grounded = false;
        SelectMonitor();
        physics.Clamp(pet, bounds);
        behavior.Reset();
        Present();
    }

    // Target frame period: active ~60, sleeping ~30, paused ~10 FPS.
    private double FramePeriod() => dragging || pressPending ? 1.0 / 60 : paused ? 0.1 : pet.Action == PetAction.Sleeping ? 1.0 / 30 : 1.0 / 60;

    private void Tick(object? sender, EventArgs e)
    {
        double now = clock.Elapsed.TotalSeconds;
        // The timer runs at half the period because Windows' default 15.6 ms timer
        // granularity turns a 16 ms interval into uneven 16/31 ms gaps (~37 FPS).
        // Gating here keeps frames evenly spaced at the intended rate.
        if (now - previous < FramePeriod() * 0.85) return;
        double dt = Math.Clamp(now - previous, 0, 0.1);
        previous = now;
        RecordFrame(now, dt);
        boundsTimer += dt;
        CheckDragThreshold();
        if (!paused)
        {
            TickFeatures(dt);
            TickCommands();
            TickContext(dt);
            pet.Activity.Step(pet.Action, dt);
            var cursor = CursorPosition();
            if (pet.Emotions.Step(pet, dt, cursor.X, cursor.Y, renderer.IsMouseOver)) Interact("petted");
        }
        if (dragging)
        {
            UpdateDrag(dt);
            accumulator = 0;
        }
        else
        {
            // Recover after monitor unplug, taskbar relocation, or work-area changes.
            if (boundsTimer >= 1)
            {
                boundsTimer = 0;
                SelectMonitor();
                physics.Clamp(pet, bounds);
                if (pet.Grounded) pet.Y = bounds.Bottom - settings.Height;
                SnapPresentation();
            }
            if (!paused && !pressPending)
            {
                double cursorX = CursorPosition().X;
                accumulator += dt;
                while (accumulator >= PhysicsStep)
                {
                    previousX = pet.X; previousY = pet.Y;
                    if (!FeatureOwnsMovement()) behavior.Step(pet, PhysicsStep, cursorX);
                    physics.Step(pet, bounds, PhysicsStep);
                    accumulator -= PhysicsStep;
                }
            }
        }
        if (!paused) { pet.AnimationTime += dt; pet.ActionTime += dt; pet.Motion.Step(pet, dt, settings.WalkSpeed); pet.Thought.Step(pet, dt); pet.Speech.Step(pet, dt); TickPendingSpeech(); }
        Present(interpolate: !dragging && !paused && !pressPending);
        double interval = FramePeriod() * 1000 / 2;
        if (timer.Interval.TotalMilliseconds != interval) timer.Interval = TimeSpan.FromMilliseconds(interval);
    }

    private void RecordFrame(double now, double dt)
    {
        frameCount++;
        frameMaxGap = Math.Max(frameMaxGap, dt);
        if (now - frameStatsStart < 1) return;
        measuredFps = frameCount / (now - frameStatsStart);
        measuredMaxGap = frameMaxGap;
        frameStatsStart = now; frameCount = 0; frameMaxGap = 0;
    }

    // Direct position changes (reset, drag, DPI) must not be blended from the old spot.
    private void SnapPresentation() { previousX = pet.X; previousY = pet.Y; }

    private void Present(bool interpolate = false)
    {
        double padding = PickupAnimation.Padding;
        double x = pet.X, y = pet.Y;
        // Presentation only: physics advances in fixed 1/120 s steps, so a frame can
        // contain one or two steps. Blending removes that uneven on-screen stride.
        // A legitimate step moves at most ~18 DIPs; anything larger is a teleport.
        if (interpolate && Math.Abs(pet.X - previousX) < 40 && Math.Abs(pet.Y - previousY) < 40)
        {
            double alpha = Math.Clamp(accumulator / PhysicsStep, 0, 1);
            x = previousX + (pet.X - previousX) * alpha;
            y = previousY + (pet.Y - previousY) * alpha;
        }
        else SnapPresentation();
        MoveWindow(x - padding, y - padding);
        UpdateSpeechWindow(x, y);
        renderer.InvalidateVisual();
        if (debugText != null && clock.Elapsed.TotalSeconds >= nextDebugUpdate)
        {
            nextDebugUpdate = clock.Elapsed.TotalSeconds + 0.25;
            debugText.Text = $"Action: {pet.Action}\nEmotion: {pet.Emotions.Current}{(pet.Emotions.ManualMode ? " (manual control)" : "")}\n" +
                $"Energy: {(aiEnergy.HasValue ? aiEnergy.Value.ToString("F1") + " / 100 (last AI report)" : "unavailable")}\n" +
                $"Sleep: {pet.Emotions.SleepElapsed:F1} / {settings.SleepDurationSeconds:F1} s\n" +
                $"Wake cooldown: {pet.Emotions.WakeCooldownRemaining:F1} s\n" +
                $"Input: {(dragging ? "dragging" : pressPending ? "pressed" : "free")}\nPaused: {paused}\n" +
                $"Needs: {(aiNeeds == null ? "unavailable" : $"hunger {aiNeeds.Hunger:F0}, boredom {aiNeeds.Boredom:F0}, affection {aiNeeds.Affection:F0}, bond {aiNeeds.Bond:F0}")}\n" +
                $"Thought: {(pet.Thought.Visible ? pet.Thought.Kind.ToString() : "none")}\n" +
                $"Speech: {(pet.Speech.Visible ? pet.Speech.Text : "none")}\n" +
                $"Learned likes: {(aiLearned is { Likes.Length: > 0 } ? string.Join("; ", aiLearned.Likes) : "nothing yet")}\n" +
                $"Learned dislikes: {(aiLearned is { Dislikes.Length: > 0 } ? string.Join("; ", aiLearned.Dislikes) : "nothing yet")}\n" +
                $"Context: {(context.Fullscreen ? "fullscreen app (quiet)" : "normal")}, {(context.Away ? "user away" : "user here")}, idle {context.IdleSeconds:F0} s{(behavior.HoldSleep ? ", away nap" : "")}\n" +
                $"Break reminder: {(breaks.Enabled ? $"{breaks.ActiveSeconds / 60:F1} / {breaks.IntervalSeconds / 60:F0} min active{(breaks.Due ? " (due)" : "")}" : "off")}\n" +
                $"Frame: {measuredFps:F0} fps (max gap {measuredMaxGap * 1000:F0} ms)\n{AiStatus}";
        }
    }

    // One SetWindowPos per frame. Setting WPF Left and then Top issues two separate
    // moves, which can briefly show the window offset on only one axis while flying.
    private void MoveWindow(double left, double top) => MoveWindow(this, left, top);

    private void MoveWindow(Window target, double left, double top)
    {
        var handle = new WindowInteropHelper(target).Handle;
        if (handle == IntPtr.Zero) { target.Left = left; target.Top = top; return; }
        int x = (int)Math.Round(left * scale), y = (int)Math.Round(top * scale);
        // WPF keeps Left/Top in sync from WM_MOVE, so an unchanged pixel needs no call.
        if (Math.Abs(target.Left * scale - x) < 0.5 && Math.Abs(target.Top * scale - y) < 0.5) return;
        const uint NoSize = 0x0001, NoZOrder = 0x0004, NoActivate = 0x0010, NoOwnerZOrder = 0x0200;
        if (!SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, NoSize | NoZOrder | NoActivate | NoOwnerZOrder))
        { target.Left = left; target.Top = top; }
    }

    public void ToggleDebug()
    {
        if (debugWindow != null) { debugWindow.Close(); return; }
        debugText = new TextBlock { Margin = new Thickness(16), FontSize = 14 };
        debugWindow = new Window { Title = "Pet behavior", Icon = AppIcon.WindowImage(),
            Width = 520, Height = 400, Content = new ScrollViewer { Content = debugText, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false, Topmost = true };
        debugWindow.Closed += (_, _) => { debugWindow = null; debugText = null; };
        nextDebugUpdate = 0;
        Present();
        debugWindow.Show();
    }

    private void Interact(string kind)
    {
        revision++;
        interactionCount++;
        lastEvent = kind;
        pet.Interactions.Record(kind);
        if (kind is "left_clicked" or "right_clicked") PlayPetSound("meow");
        if (kind == "petted") { PlayPetSound("purr"); MaybeKnead(); }
    }

    public void SetAiEnabled(bool enabled)
    {
        StopAi();
        if (enabled && !closed) ai = new PythonDecisionSource();
        behavior.Reset();
    }

    public void StopAi()
    {
        revision++;
        ai?.Dispose();
        ai = null;
        aiEnergy = null;
        aiNeeds = null;
        aiLearned = null;
        aiDiary = null;
        RefreshDiary();
    }

    private async void RequestDecision()
    {
        if (aiBusy || ai == null || closed || paused || dragging || pressPending) return;
        aiBusy = true;
        var source = ai;
        long requestedRevision = revision;
        double requestedAt = clock.Elapsed.TotalSeconds;
        var observation = new PetObservation(pet.Action.ToString(), pet.Grounded,
            pet.Emotions.SecondsWithoutAttention, interactionCount, lastEvent, behavior.LastAppliedCommand,
            pet.Emotions.Reported.ToString(), pet.Activity.Snapshot(), preferences.Personality, sessionId, preferences.Tuning?.Normalized(),
            DateTime.Now.Hour + DateTime.Now.Minute / 60.0, pet.Interactions.Snapshot(),
            preferences.Speech, preferences.Language, preferences.Chattiness, preferences.Name, EffectiveQuiet,
            pet.Speech.LastShown is { } shown ? new SpeechReport(shown.Id, Math.Round(shown.Age, 2)) : null, preferences.LearningReset,
            breaks.Due && CanDeliverBreak, ContextSnapshot());
        try
        {
            // Even a future cached source that completes synchronously must wait until
            // PetBehavior has finished installing this tick's local fallback action.
            await System.Threading.Tasks.Task.Yield();
            var decision = await source.DecideAsync(observation);
            // The await resumes on WPF's dispatcher. No background task touches the pet.
            if (decision == null || closed || !ReferenceEquals(source, ai)) return;
            if (decision.Needs != null) aiNeeds = decision.Needs;
            if (decision.Learned != null) aiLearned = decision.Learned;
            if (decision.Diary != null) { aiDiary = decision.Diary; RefreshDiary(); }
            // Words are presentation only, so a reply discarded for revision (e.g. the
            // user petted the cat meanwhile) may still speak; PetSpeech enforces safety.
            if (decision.Speech != null && !paused && clock.Elapsed.TotalSeconds - requestedAt < 2.5) ShowSpeech(decision.Speech);
            if (!pressPending && DecisionGuard.CanApply(requestedRevision, revision, clock.Elapsed.TotalSeconds - requestedAt, paused, pet))
            {
                aiEnergy = decision.Energy;
                pet.Emotions.ApplyAiHint(decision.Emotion);
                if (!pet.Speech.Visible && !context.Fullscreen) pet.Thought.TryShow(PetThought.Parse(decision.Thought), pet);
                if (!FeatureOwnsMovement()) behavior.TryApply(pet, decision);
            }
        }
        catch (Exception ex)
        {
            // A future decision-source implementation must also never break the UI loop.
            Trace.WriteLine($"AI decision failed: {ex.Message}");
        }
        finally { aiBusy = false; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
