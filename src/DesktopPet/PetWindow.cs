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
    // English status for the debug view and logs; AiStatusText follows the UI language.
    public string AiStatus => ai?.Status ?? "Python AI: off (local behavior)";
    public string AiStatusText => LocalizeAiStatus(AiStatus, preferences.Language);
    // The whole UI uses one language (preferences.Language). The debug view stays English.
    // Other languages translate the English text (PetText); LF is for interpolated text.
    public string L(string vietnamese, string english) => PetText.Pick(preferences.Language, vietnamese, english);
    public string LF(FormattableString vietnamese, FormattableString english) => PetText.PickFormat(preferences.Language, vietnamese, english);
    // Names, layouts and data text not translated yet: every language but Vietnamese uses English.
    private bool English => PetLanguage.UsesEnglish(preferences.Language);
    public event Action? LanguageChanged;
    private void RaiseLanguageChanged() => LanguageChanged?.Invoke();

    public static string LocalizeAiStatus(string status, string language)
    {
        if (language != PetLanguage.Vietnamese) return status; // Other languages keep the English debug text.
        const string connected = "Python AI: connected (", local = "Local behavior: ";
        if (status.StartsWith(connected, StringComparison.Ordinal) && status.EndsWith(')'))
        {
            string mood = status[connected.Length..^1];
            mood = mood switch { "content" => "thoải mái", "playful" => "ham chơi", "bored" => "chán", "sleepy" => "buồn ngủ", "hungry" => "đói", _ => mood };
            return $"Python AI: đã kết nối ({mood})";
        }
        if (status.StartsWith(local, StringComparison.Ordinal))
            return "Hành vi local: " + status[local.Length..].Replace("timeout", "hết thời gian chờ").Replace("retry after", "thử lại sau");
        return status switch
        {
            "Python AI: off (local behavior)" => "Python AI: tắt (hành vi local)",
            "Python AI: starting on next decision" => "Python AI: khởi động ở lần quyết định tới",
            "Python AI: stopped" => "Python AI: đã dừng",
            _ => status
        };
    }
    public bool AiEnabled => ai != null;

    public PetWindow(PetPreferences? options = null)
    {
        preferences = options ?? PetPreferences.Load();
        persistPreferences = options == null;
        if (persistPreferences) { BattleDataFile = BattleData.UserFilePath; AdventureProgressFile = AdventureProgress.DefaultPath; ExpeditionProgressFile = ExpeditionProgress.DefaultPath; }
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
        behavior.SleepElsewhere = SleepOnCushionInstead; // With the slime cushion on display (PetWindow.KeepsakePlay.cs).
        if (Environment.GetEnvironmentVariable("PET_AI_DISABLED") != "1") ai = new PythonDecisionSource();
        renderer = new PetRenderer(pet);
        Content = renderer;
        var menu = new ContextMenu { FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize };
        PetMenuStyle.Apply(menu);
        ContextMenu = menu;
        InitializeFeatures(menu); // Builds the whole menu (PetWindow.Features.cs).
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

    // Borderless, transparent, topmost tool window that never takes focus: battle panels, the
    // toy ball, the laser dot, the speech bubble, the keepsake. clickThrough lets every click fall
    // to the window below; otherwise it is clickable without stealing keyboard focus.
    private static Window CreateOverlayWindow(UIElement content, string title, bool clickThrough = false)
    {
        var window = new Window
        {
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true,
            Background = Brushes.Transparent, Topmost = true, ShowInTaskbar = false, ShowActivated = false,
            Focusable = false, Content = content, Title = title, Left = -10000, Top = -10000, Width = 10, Height = 10
        };
        window.SourceInitialized += (_, _) =>
        {
            ((HwndSource)PresentationSource.FromVisual(window)).AddHook(WindowHook);
            var handle = new WindowInteropHelper(window).Handle;
            const int ExStyle = -20;
            const long ToolWindow = 0x80, NoActivate = 0x08000000, Transparent = 0x20;
            SetWindowLongPtr(handle, ExStyle, new IntPtr(GetWindowLongPtr(handle, ExStyle).ToInt64() | ToolWindow | NoActivate | (clickThrough ? Transparent : 0)));
        };
        return window;
    }

    private Area WorkArea(Forms.Screen screen)
    {
        var area = screen.WorkingArea;
        return new Area(area.Left / scale, area.Top / scale, area.Right / scale, area.Bottom / scale);
    }

    // Replaceable for UI checks, so they never depend on where the real mouse is: the cursor in
    // screen pixels.
    private Func<Point>? cursorProbe;

    private Point ScreenCursor()
    {
        if (cursorProbe != null) return cursorProbe();
        GetCursorPos(out var point);
        return new Point(point.X, point.Y);
    }

    private Point CursorPosition()
    {
        var point = ScreenCursor();
        return new Point(point.X / scale, point.Y / scale);
    }

    private void SelectMonitor()
    {
        var center = new System.Drawing.Point((int)((pet.X + settings.Width / 2) * scale),
            (int)((pet.Y + settings.Height / 2) * scale));
        bounds = WorkArea(Forms.Screen.FromPoint(center));
        RefreshRestingSpot();
    }

    // Straight to the middle of the main screen (UI checks set the scene with it); the menus use BringHome.
    public void Reset()
    {
        bool keepSleeping = PrepareReset();
        catHop = CatHop.None; ApplyCatHopPose(); // A hop under way ends here.
        MoveHome(keepSleeping);
    }

    // "Bring the pet to the main screen": the cat vanishes in a puff, pops up in the middle of the
    // main screen with another, drops a little and lands surprised (a sleeping cat goes on sleeping,
    // no surprise). Paused or hidden: straight there, so the rescue always works.
    public void BringHome()
    {
        if (CatTeleporting) return;
        bool keepSleeping = PrepareReset();
        if (paused || closed || !IsVisible) { MoveHome(keepSleeping); return; }
        catHopSleeping = keepSleeping;
        catHop = CatHop.Vanish; catHopTime = 0;
        pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
        PlayPetSound("swoosh");
        ApplyCatHopPose();
        Present();
    }

    private enum CatHop { None, Vanish, Appear }
    private CatHop catHop;
    private double catHopTime;
    private bool catHopSleeping;
    public const double CatVanishSeconds = 0.3, CatAppearSeconds = 0.55, CatDropHeight = 36;
    public bool CatTeleporting => catHop != CatHop.None;

    private void TickCatHop(double dt)
    {
        if (catHop == CatHop.None) return;
        catHopTime += dt;
        if (catHop == CatHop.Vanish && catHopTime >= CatVanishSeconds)
        {
            MoveHome(catHopSleeping);
            catHop = CatHop.Appear; catHopTime = 0;
            PlayPetSound("sparkle");
        }
        else if (catHop == CatHop.Appear && catHopTime >= CatAppearSeconds)
        {
            catHop = CatHop.None;
            // Off in manual emotion control (the cat only looks); never for a sleeping cat.
            if (!catHopSleeping && !pet.Emotions.SleepLocked && pet.Action != PetAction.Sleeping) pet.Emotions.Startle();
            System.Diagnostics.Trace.WriteLine("The pet was brought to the main screen.");
        }
        ApplyCatHopPose();
    }

    // The same timing as the keepsake's hop (PetWindow.Keepsake.cs), plus a short fall on arrival.
    private void ApplyCatHopPose()
    {
        double pop = 1, drop = 0, puff = -1, t = catHopTime;
        if (catHop == CatHop.Vanish)
        {
            double v = Math.Min(1, t / CatVanishSeconds);
            pop = v < 0.25 ? 1 + v / 0.25 * 0.18 : 1.18 * (1 - (v - 0.25) / 0.75);
            puff = v * 0.45;
        }
        else if (catHop == CatHop.Appear)
        {
            double u = Math.Min(1, t / 0.32), fall = Math.Min(1, t / 0.4);
            pop = 1 + 2.7 * Math.Pow(u - 1, 3) + 1.7 * Math.Pow(u - 1, 2);
            drop = CatDropHeight * (1 - fall * fall);
            puff = 0.35 + 0.65 * Math.Min(1, t / CatAppearSeconds);
        }
        renderer.TeleportPop = Math.Max(0, pop); renderer.TeleportDrop = drop; renderer.TeleportPuff = puff;
    }

    private bool PrepareReset()
    {
        EndBattle(); CloseBattleHub(); CloseBattlePrep(); CloseAdventureMap(); CloseExpeditionMap(); CloseBattleBag(); CloseBattleSummary(); CloseBattleNotice(); CloseBattleTutorial();
        CancelFeatureActivity();
        bool keepSleeping = pet.Emotions.SleepLocked || pet.Action == PetAction.Sleeping;
        Interact("reset");
        EndDrag(false);
        return keepSleeping;
    }

    private void MoveHome(bool keepSleeping)
    {
        bounds = WorkArea(Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0]);
        pet.X = (bounds.Left + bounds.Right - settings.Width) / 2;
        pet.Y = bounds.Bottom - settings.Height;
        physics.Clamp(pet, bounds);
        pet.VX = pet.VY = 0;
        pet.Grounded = true;
        pet.LedgeId = pet.ClimbId = 0;
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
        // A battle's field and HUD stay clickable while the pet is paused: the battle holds too,
        // so no skill, target or bomb can change it. Un-pausing removes only this reason.
        SetBattlePause(BattlePause.App, value);
    }

    private void BeginDrag(object sender, MouseButtonEventArgs e)
    {
        // A held battle (paused, menu, card pick, bag...) freezes the cat: it cannot be picked up.
        // Nor while it is being brought to the main screen (a puff, under a second).
        if (BattleFreezesPet || CatTeleporting) { e.Handled = true; return; }
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
        pet.LedgeId = pet.ClimbId = 0;
        pet.Action = PetAction.Dragging;
        pet.Pickup.Begin(cursor.X, cursor.Y);
        Present();
        tracker.Clear();
        tracker.Add(clock.Elapsed.TotalSeconds, cursor.X, cursor.Y);
        BattleCatLifted();
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
            // A rescue throw in Battle Mode is not rough handling (no anger, no bond penalty).
            if (Math.Sqrt(pet.VX * pet.VX + pet.VY * pet.VY) >= 1600 && !BattleActive) { pet.Emotions.AggressiveInteraction(); pet.Interactions.RoughThrow(); }
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
        // The real time since the last processed frame is what the frame statistics report;
        // the simulation gets it capped at 0.1 s, so a stall never makes it catch up.
        double elapsed = Math.Max(0, now - previous);
        double dt = Math.Min(elapsed, 0.1);
        previous = now;
        RecordFrame(now, elapsed);
        boundsTimer += dt;
        CheckDragThreshold();
        TickBattleHub(dt);
        TickBattlePrep(dt);
        TickAdventureMap(dt);
        TickExpeditionMap(dt);
        TickBattleBag(dt);
        TickTrophies();
        TickKeepsake(dt); // The keepsake's physics and its meetings with the cat (PetWindow.Keepsake.cs).
        if (!paused)
        {
            TickFeatures(dt);
            TickCommands();
            TickContext(dt);
            TickLedges(dt);
            TickBattleAutostart(dt);
            TickBattle(dt);
            pet.Activity.Step(pet.Action, dt);
            var cursor = CursorPosition();
            // During the laser game the cursor is the toy: no petting strokes or startles from it.
            bool cursorIsToy = LaserActive;
            if (pet.Emotions.Step(pet, dt, cursorIsToy ? null : cursor.X, cursorIsToy ? null : cursor.Y, !cursorIsToy && renderer.IsMouseOver)) Interact("petted");
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
                if (pet.Grounded && pet.LedgeId == 0) pet.Y = bounds.Bottom - settings.Height; // Not when standing on a window.
                SnapPresentation();
            }
            // A paused battle also holds the cat's physics: a throw waits mid-air with its
            // position and velocity and goes on after Resume (one normal frame, no catch-up).
            if (!paused && !pressPending && !BattleFreezesPet && !CatTeleporting)
            {
                double cursorX = CursorPosition().X;
                accumulator += dt;
                while (accumulator >= PhysicsStep)
                {
                    previousX = pet.X; previousY = pet.Y;
                    // Battle Mode owns the pet: no random actions, sleep or AI moves (PetWindow.Battle.cs).
                    if (!FeatureOwnsMovement() && !BattleActive) behavior.Step(pet, PhysicsStep, cursorX);
                    physics.Step(pet, bounds, PhysicsStep, ledges, FeatureOwnsMovement());
                    BounceOnCushion(previousY + settings.Height); // The slime cushion is a trampoline (PetWindow.KeepsakePlay.cs).
                    accumulator -= PhysicsStep;
                }
            }
        }
        if (!paused) TickCatHop(dt);
        // A held battle freezes the cat's poses too (a dazed cat stops swaying), as Pause does.
        if (!paused && (!BattleFreezesPet || dragging)) { pet.AnimationTime += dt; pet.ActionTime += dt; pet.Motion.Step(pet, dt, settings.WalkSpeed); if (pet.Motion.Landing.TakeSound()) PlayPetSound(Landing.Sound); }
        if (!paused) { pet.Thought.Step(pet, dt); pet.Speech.Step(pet, dt); TickPendingSpeech(); }
        Present(interpolate: !dragging && !paused && !pressPending);
        double interval = FramePeriod() * 1000 / 2;
        if (timer.Interval.TotalMilliseconds != interval) timer.Interval = TimeSpan.FromMilliseconds(interval);
    }

    // Frame statistics over windows of at least 1 s: frames per second, and the longest real gap
    // (seconds) between two processed frames in that window, uncapped (a 300 ms stall reads 300 ms).
    private void RecordFrame(double now, double elapsed)
    {
        frameCount++;
        frameMaxGap = Math.Max(frameMaxGap, elapsed);
        if (now - frameStatsStart < 1) return;
        measuredFps = frameCount / (now - frameStatsStart);
        measuredMaxGap = frameMaxGap;
        // Measuring (scripts/perf.ps1 -Battle): one line a second for the script to sum up.
        if (battleAutostart != null && battle != null)
            Trace.WriteLine(FormattableString.Invariant($"Frame stats: {measuredFps:F1} fps, longest gap {measuredMaxGap * 1000:F0} ms, {battle.Slimes.Count} enemies"));
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
        if (!dragging) y -= napLift; // Asleep on the slime cushion: drawn on top of it (PetWindow.KeepsakePlay.cs).
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
                $"Windows: {(!LedgesEnabled ? "off" : pet.Action == PetAction.Climbing ? "climbing" : OnLedge ? "on a window" : "floor")}, {ledges.Count} ledges{(climbPlan != null ? ", going to climb" : ledgeExit != 0 ? ", leaving" : "")}\n" +
                $"Context: {(context.Fullscreen ? "fullscreen app (quiet)" : "normal")}, {(context.Away ? "user away" : "user here")}, idle {context.IdleSeconds:F0} s{(behavior.HoldSleep ? ", away nap" : "")}\n" +
                $"Battle: {(battle == null ? "off" : $"{battle.Phase}, wave {battle.Wave}/{battle.WaveCount}, HP {battle.CatHp:F0}, slimes {battle.Alive}{(battle.Paused ? ", paused" : "")}")}\n" +
                $"Break reminder: {(breaks.Enabled ? $"{breaks.ActiveSeconds / 60:F1} / {breaks.IntervalSeconds / 60:F0} min active{(breaks.Due ? " (due)" : "")}" : "off")}\n" +
                $"Frame: {measuredFps:F0} fps (longest gap {measuredMaxGap * 1000:F0} ms, last 1 s window)\n{MemoryStatus()}\n{AiStatus}";
        }
    }

    // Live heap vs. memory the GC keeps committed: tells uncollected garbage from a real leak.
    private static string MemoryStatus()
    {
        var info = GC.GetGCMemoryInfo();
        return $"Memory: heap {GC.GetTotalMemory(false) / 1048576.0:F0} MB (after last GC {info.HeapSizeBytes / 1048576.0:F0} MB), " +
            $"committed {info.TotalCommittedBytes / 1048576.0:F0} MB, GCs {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}";
    }

    // One SetWindowPos per frame. Setting WPF Left and then Top issues two separate
    // moves, which can briefly show the window offset on only one axis while flying.
    private void MoveWindow(double left, double top) => MoveWindow(this, left, top);

    // atScale: the DPI scale of the monitor the window is on, when that is not the pet's.
    private void MoveWindow(Window target, double left, double top, double? atScale = null)
    {
        var handle = new WindowInteropHelper(target).Handle;
        if (handle == IntPtr.Zero) { target.Left = left; target.Top = top; return; }
        double scale = atScale ?? this.scale;
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
        debugText = new TextBlock { Margin = new Thickness(16), FontSize = PetUi.TitleSize };
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
        behavior.Reset(pet);
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
        specialDay = null; ApplyAccessory(); // Without Python there is no holiday outfit.
    }

    private async void RequestDecision()
    {
        // No requests during a battle: Python never drives the fight.
        if (aiBusy || ai == null || closed || paused || dragging || pressPending || BattleOwnsPet) return;
        aiBusy = true;
        var source = ai;
        long requestedRevision = revision;
        double requestedAt = clock.Elapsed.TotalSeconds;
        string requestedLanguage = preferences.Language;
        var observation = new PetObservation(pet.Action.ToString(), pet.Grounded,
            pet.Emotions.SecondsWithoutAttention, interactionCount, lastEvent, behavior.LastAppliedCommand,
            pet.Emotions.Reported.ToString(), pet.Activity.Snapshot(), preferences.Personality, sessionId, preferences.Tuning?.Normalized(),
            DateTime.Now.Hour + DateTime.Now.Minute / 60.0, pet.Interactions.Snapshot(),
            preferences.Speech, preferences.Language, preferences.Chattiness, preferences.Name, EffectiveQuiet,
            pet.Speech.LastShown is { } shown ? new SpeechReport(shown.Id, Math.Round(shown.Age, 2)) : null, preferences.LearningReset,
            breaks.Due && CanDeliverBreak, ContextSnapshot(), preferences.Birthday, BattleReport);
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
            if (decision.SpecialDay != specialDay) { specialDay = decision.SpecialDay; ApplyAccessory(); }
            // Words are presentation only, so a reply discarded for revision (e.g. the
            // user petted the cat meanwhile) may still speak; PetSpeech enforces safety.
            // A line chosen before a language change is dropped (the data above is still kept).
            if (decision.Speech != null && !paused && !BattleOwnsPet && clock.Elapsed.TotalSeconds - requestedAt < 2.5 &&
                requestedLanguage == preferences.Language) ShowSpeech(decision.Speech);
            if (!pressPending && !BattleOwnsPet && DecisionGuard.CanApply(requestedRevision, revision, clock.Elapsed.TotalSeconds - requestedAt, paused, pet))
            {
                aiEnergy = decision.Energy;
                pet.Emotions.ApplyAiHint(decision.Emotion);
                if (!pet.Speech.Visible && MayAct(AutoKind.Thought) && pet.Thought.TryShow(PetThought.Parse(decision.Thought), pet)) NoteThought(pet.Thought.Kind);
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
