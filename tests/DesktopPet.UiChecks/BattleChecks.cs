using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using DesktopPet.AI;

// Battle Mode inside the real pet window: lifecycle, ownership of the pet, input, pause,
// Python isolation and cleanup. The real cursor is never moved.
internal static class BattleChecks
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Field<T>(PetWindow window, string name) => (T)typeof(PetWindow).GetField(name, Flags)!.GetValue(window)!;
    private static void SetField(PetWindow window, string name, object? value) => typeof(PetWindow).GetField(name, Flags)!.SetValue(window, value);
    private static object? Call(PetWindow window, string name, params object[] args) => typeof(PetWindow).GetMethod(name, Flags)!.Invoke(window, args);
    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, idle)));
    private static MouseButtonEventArgs MouseEvent(RoutedEvent routedEvent, MouseButton button = MouseButton.Left) =>
        new(Mouse.PrimaryDevice, Environment.TickCount, button) { RoutedEvent = routedEvent };
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    private static Window? ResultWindow() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet result" && w.IsVisible);
    private static int ResultWindows() => Application.Current.Windows.OfType<Window>().Count(w => w.Title == "Pet result" && w.IsVisible);
    private const long NoActivate = 0x08000000, Transparent = 0x20;
    private static int OpenBattleWindows() => Application.Current.Windows.OfType<Window>().Count(w => w.Title.StartsWith("Pet battle") && w.IsLoaded);

    public static async Task Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        void Tick(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattle", 1.0 / 60); }
        BattleSim Sim() => window.Battle!;
        var fast = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 2, 2 }, WaveBreakSeconds = 1 };

        window.Reset();
        check(!window.BattleActive && OpenBattleWindows() == 0 && !pet.SwordOut, "Battle Mode is off until the user starts it");

        // Only from a safe state, and a refusal changes nothing.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        check(window.StartBattle() is string asleep && asleep.Length > 0 && !window.BattleActive && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked,
            "A sleeping cat refuses the battle with a reason and is not woken");
        window.Reset(); behavior.WakeByClick(pet); pet.Emotions.FinishSleep(0); pet.Action = PetAction.Idle;
        pet.Action = PetAction.Thrown; pet.Grounded = false;
        check(window.StartBattle() != null && !window.BattleActive && pet.Action == PetAction.Thrown, "A flying cat refuses the battle");
        window.Reset();
        SetField(window, "dragging", true); pet.Action = PetAction.Dragging; pet.Grounded = false;
        check(window.StartBattle() != null && !window.BattleActive, "A dragged cat refuses the battle");
        SetField(window, "dragging", false);
        window.Reset();
        window.StartLaser();
        check(window.StartBattle() != null && !window.BattleActive && window.LaserActive, "A running laser game refuses the battle (and keeps playing)");
        window.Reset();

        // Start: one floor strip and one HUD, both non-activating; the field takes clicks only on slimes.
        window.BattleTuning = fast;
        check(window.StartBattle() == null && window.BattleActive && pet.SwordOut && pet.Emotions.Suspended, "A calm, awake cat starts the battle with its sword");
        await Task.Delay(30);
        var field = Field<Window?>(window, "battleFieldWindow");
        var hud = Field<Window?>(window, "battleHudWindow");
        long fieldStyle = field == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(field).Handle, -20).ToInt64();
        long hudStyle = hud == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(hud).Handle, -20).ToInt64();
        check(field is { IsVisible: true, ShowActivated: false, ShowInTaskbar: false } && hud is { IsVisible: true, ShowActivated: false } &&
              (fieldStyle & NoActivate) != 0 && (hudStyle & NoActivate) != 0 && (fieldStyle & Transparent) == 0 && OpenBattleWindows() == 2,
            "The battle shows one shared field window and one HUD; neither takes focus");
        var area = Field<Area>(window, "bounds");
        var dpi = VisualTreeHelper.GetDpi(window);
        check(field != null && Math.Abs(field.Left - area.Left) < 2 && Math.Abs(field.Top + field.Height - area.Bottom) < 2 && Math.Abs(field.Width - (area.Right - area.Left)) < 2,
            $"The field covers the floor strip of the cat's work area (field {field?.Left},{field?.Top} {field?.Width}x{field?.Height}, area {area})");

        // While the battle runs it owns the pet.
        check(!window.GiveTreat() && !window.StartLaser() && !window.DoNow(PetAction.Sitting), "Treats, games and Do now wait during the battle");
        window.ToggleToy();
        check(Field<Window?>(window, "toyWindow") == null, "The ball cannot be started during the battle");
        pet.Emotions.Step(pet, 400);
        check(!pet.Emotions.SleepLocked && pet.Emotions.Current == PetEmotion.Neutral, "No automatic sleepiness or sadness during the battle");
        for (int i = 0; i < 5; i++) pet.Emotions.Attention(click: true);
        pet.Emotions.Step(pet, 0.5);
        check(pet.Emotions.Current == PetEmotion.Neutral, "Clicking during the battle does not make the cat angry");
        SetContext(window, false, 400); Call(window, "TickContext", 1.1);
        check(!behavior.HoldSleep && pet.Action != PetAction.Sleeping, "The away nap does not start during the battle");
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);

        // Slimes arrive and the cat goes for them.
        Tick(0.7);
        check(Sim().Wave == 1 && Sim().Slimes.Count > 0, "The first wave arrives");
        var slime = Sim().Slimes[0];
        double catCenter = pet.X + 56;
        check(pet.Action == PetAction.Walking && pet.Facing == (slime.X < catCenter ? -1 : 1), "The cat walks toward a slime (normal walking, no teleport)");

        // Click to target, push skill.
        check(window.BattleClickSlime(slime.Id) && Sim().SelectedId == slime.Id && slime.Hp == slime.MaxHp, "Clicking a slime marks it, without damage");
        check(window.ArmBattlePush(true) && Sim().PushArmed, "The HUD push skill can be chosen");
        var other = Sim().Slimes.FirstOrDefault(s => s.Id != slime.Id && s.Targetable) ?? slime;
        check(window.BattleClickSlime(other.Id) && other.State == SlimeState.Stunned && Sim().SelectedId == slime.Id && !Sim().PushArmed,
            "The next slime click pushes and stuns it; the marked target stays");
        check(!window.ArmBattlePush(true) && Sim().PushCooldown > 0, "The skill cools down before it can be chosen again");

        // The game HUD: five skill slots, used with the mouse.
        var gameHud = Field<BattleHud>(window, "battleHud");
        void Press(UIElement element) => element.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        check(BattleHud.SkillOrder.Length == 5 && BattleHud.SkillOrder.All(skill => gameHud.Slot(skill).ToolTip is string tip && tip.Length > 10),
            "The HUD has five skill slots, each with a tooltip");
        Tick(1.0 / 60);
        check(!gameHud.Slot(BattleSkill.Push).IsReady && gameHud.Slot(BattleSkill.Push).Remaining > 0 && gameHud.Slot(BattleSkill.Heal).IsReady,
            "A used skill shows its cooldown; the others are ready");
        Press(gameHud.Slot(BattleSkill.Heal));
        check(Sim().Cooldown(BattleSkill.Heal) == 0, "The fish slot does nothing at full HP");
        Press(gameHud.Slot(BattleSkill.Shield)); Press(gameHud.Slot(BattleSkill.Cheer));
        Tick(1.0 / 60);
        check(Sim().ShieldRemaining > 0 && Sim().CheerRemaining > 0 && pet.BattleShield && pet.BattleBuff, "Shield and Cheer act at once and show on the cat");
        Press(gameHud.Slot(BattleSkill.Bomb));
        check(Sim().Armed == BattleSkill.Bomb && gameHud.Slot(BattleSkill.Bomb).IsArmed, "The bomb slot is chosen first");
        Press(gameHud.Slot(BattleSkill.Bomb));
        check(Sim().Armed == null, "Clicking it again cancels the choice");
        Press(gameHud.Slot(BattleSkill.Bomb));
        int? markedBefore = Sim().SelectedId;
        var bombTarget = Sim().Slimes.First(s => s.Targetable);
        check(window.BattleClickSlime(bombTarget.Id) && Sim().Bombs.Count == 1 && Sim().SelectedId == markedBefore, "Clicking a slime drops the yarn bomb there; the target stays");
        Press(gameHud.Slot(BattleSkill.Bomb));
        check(Sim().Armed == null, "A cooling skill cannot be chosen");
        check(hud!.ActualWidth < Field<Area>(window, "bounds").Right - Field<Area>(window, "bounds").Left, $"The HUD fits on the screen (width {hud.ActualWidth:F0})");

        // The field: empty pixels are transparent (clicks pass through), slimes are hit-testable.
        var view = Field<BattleFieldView>(window, "battleView");
        Tick(0.6);
        slime = Sim().Slimes.First(s => s.Targetable);
        double localX = slime.X - view.OriginX, floorY = view.ActualHeight - 2;
        int vw = Math.Max(1, (int)view.ActualWidth), vh = Math.Max(1, (int)view.ActualHeight);
        view.InvalidateVisual(); view.UpdateLayout(); // Draw the current state (rendering is otherwise deferred).
        var bitmap = new RenderTargetBitmap(vw, vh, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var pixels = new byte[vw * vh * 4]; bitmap.CopyPixels(pixels, vw * 4, 0);
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string fieldReview && hud?.Content is FrameworkElement hudContent)
        {
            // For a person to look at: the field over light and dark backgrounds, and the HUD.
            System.IO.Directory.CreateDirectory(fieldReview);
            Sim().Select(slime.Id);
            foreach (var s in Sim().Slimes.Where(s => s.Targetable && s.Id != slime.Id)) { s.X = slime.X + 90; s.Set(SlimeState.Windup); s.StateTime = 0.6; }
            view.InvalidateVisual(); view.UpdateLayout();
            var full = new RenderTargetBitmap(vw, vh, 96, 96, PixelFormats.Pbgra32); full.Render(view);
            var crop = new CroppedBitmap(full, new Int32Rect((int)Math.Clamp(localX - 120, 0, Math.Max(0, vw - 400)), 0, Math.Min(400, vw), vh));
            foreach (var (name, back) in new[] { ("light", Colors.WhiteSmoke), ("dark", Color.FromRgb(0x1E, 0x1E, 0x24)) })
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(back), null, new Rect(0, 0, 400, vh));
                    dc.DrawImage(crop, new Rect(0, 0, crop.PixelWidth, vh));
                }
                var shot = new RenderTargetBitmap(400, vh, 96, 96, PixelFormats.Pbgra32); shot.Render(visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(shot));
                using var stream = System.IO.File.Create(System.IO.Path.Combine(fieldReview, $"battle-field-{name}.png")); encoder.Save(stream);
            }
            hudContent.UpdateLayout();
            var hudShot = new RenderTargetBitmap((int)Math.Ceiling(hudContent.ActualWidth + 12), (int)Math.Ceiling(hudContent.ActualHeight + 12), 96, 96, PixelFormats.Pbgra32);
            hudShot.Render(hudContent);
            var hudEncoder = new PngBitmapEncoder(); hudEncoder.Frames.Add(BitmapFrame.Create(hudShot));
            using (var stream = System.IO.File.Create(System.IO.Path.Combine(fieldReview, "battle-hud.png"))) hudEncoder.Save(stream);
        }
        byte Alpha(double x, double y) => pixels[((int)Math.Clamp(y, 0, vh - 1) * vw + (int)Math.Clamp(x, 0, vw - 1)) * 4 + 3];
        double emptyX = localX + (localX < vw / 2.0 ? 300 : -300);
        check(Alpha(emptyX, floorY - 10) == 0 && Alpha(emptyX, 5) == 0 && Alpha(localX, floorY - 12) > 0,
            $"Empty parts of the field are fully transparent; slimes are drawn opaque ({Alpha(emptyX, floorY - 10)}, {Alpha(emptyX, 5)}, {Alpha(localX, floorY - 12)}, view {vw}x{vh}, slime {localX:F0} {slime.State})");
        check(view.SlimeAt(new Point(localX, floorY - 12)) == slime.Id && view.SlimeAt(new Point(emptyX, floorY - 12)) == null,
            "A click on a slime finds it; a click on empty floor finds nothing");

        // Python cannot drive or override the fight.
        var aiField = typeof(PetWindow).GetField("ai", Flags)!;
        var stale = new DelayedSource();
        bool requested = false;
        stale.Requested = () => requested = true;
        aiField.SetValue(window, stale);
        Call(window, "RequestDecision");
        await Task.Delay(20);
        check(!requested, "No Python requests are made during the battle");
        aiField.SetValue(window, null);

        // Lifting the cat: no combat control, no damage, waves wait.
        slime = Sim().Slimes.First(s => s.Targetable);
        slime.X = pet.X + 56 + 40;
        Tick(0.1);
        double hpBefore = Sim().CatHp;
        int toSpawn = Sim().ToSpawn, wave = Sim().Wave;
        window.Activate();
        await Task.Delay(20);
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        Tick(0.05);
        check(Sim().CatHp == hpBefore && Sim().Slimes.All(s => s.State != SlimeState.Windup), "A press on the cat (before any drag) already stops slime attacks");
        typeof(PetWindow).GetField("pressPosition", Flags)!.SetValue(window, new Point(-100000, -100000));
        Call(window, "CheckDragThreshold");
        check(pet.Action == PetAction.Dragging, "Dragging works as always during the battle");
        Tick(3);
        check(Sim().CatHp == hpBefore && Sim().ToSpawn == toSpawn && Sim().Wave == wave && pet.Action == PetAction.Dragging && pet.SwordSwing < 0,
            "While carried the cat takes no damage, does not fight, and no wave progress happens");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        check(pet.Action == PetAction.Thrown, "Letting go uses the normal throw physics");
        double xInAir = pet.X;
        Tick(0.2);
        check(pet.Action == PetAction.Thrown && pet.X == xInAir, "The battle does not steer the cat in the air");
        pet.Grounded = true; pet.VX = pet.VY = 0; pet.Action = PetAction.Idle; pet.Y = Field<Area>(window, "bounds").Bottom - 112;
        Tick(1.0 / 60);
        check(Sim().Grace > 0, "Landing starts the protection time");

        // Pause freezes the simulation; the menu pauses and resumes only its own pause.
        check(window.SetBattlePaused(true) && Sim().Paused, "The battle can be paused");
        double time = Sim().Time, hp = Sim().CatHp;
        Tick(3);
        check(Sim().Time == time && Sim().CatHp == hp && pet.VX == 0 && pet.Action == PetAction.Idle, "Paused: no time, no damage, the cat stands still");
        int drawsPaused = window.BattleFieldDraws;
        Tick(2);
        check(window.BattleFieldDraws == drawsPaused, $"Paused: the field is not redrawn ({window.BattleFieldDraws - drawsPaused} draws in 2 s)");
        window.SetBattlePaused(false);
        int drawsRunning = window.BattleFieldDraws;
        Tick(2);
        int perSecond = (window.BattleFieldDraws - drawsRunning) / 2;
        check(Sim().Slimes.Count > 0 && perSecond >= 25 && perSecond <= 32, $"While slimes move the field redraws ~30 times a second, not 60 ({perSecond}/s)");
        window.SetBattlePaused(true);
        window.ContextMenu!.IsOpen = true;
        window.ContextMenu.IsOpen = false;
        await Task.Delay(20);
        check(Sim().Paused, "Closing the menu does not resume a battle the user paused");
        check(window.SetBattlePaused(false) && !Sim().Paused, "Resume");
        window.ContextMenu.IsOpen = true;
        await Task.Delay(20);
        bool pausedByMenu = Sim().Paused;
        window.ContextMenu.IsOpen = false;
        int waited = 0;
        for (; waited < 40 && Sim().Paused; waited++) await Task.Delay(25);
        check(pausedByMenu && !Sim().Paused, $"Opening the menu pauses a running battle; closing it resumes ({pausedByMenu}, {Sim().Paused}, waited {waited})");

        // Fullscreen: pause and hide; the user resumes after it closes.
        SetContext(window, true, 0); Call(window, "TickContext", 1.1); Tick(1.0 / 60);
        check(Sim().Paused && field is { IsVisible: false } && hud is { IsVisible: false }, "A fullscreen app pauses the battle and hides its windows");
        check(!window.SetBattlePaused(false), "It cannot be resumed while the fullscreen app is open");
        SetContext(window, false, 0); Call(window, "TickContext", 1.1); Tick(1.0 / 60);
        check(Sim().Paused && field is { IsVisible: true }, "After fullscreen the battle is shown again, still paused");
        check(window.SetBattlePaused(false), "The user resumes it");

        // End: everything goes away and the pet lives normally again.
        window.EndBattle();
        await Task.Delay(20);
        check(!window.BattleActive && OpenBattleWindows() == 0 && Field<Window?>(window, "battleFieldWindow") == null && !pet.SwordOut && !pet.Emotions.Suspended,
            "End Battle removes the field, HUD, sword and the emotion lock");
        SetField(window, "treatCooldown", 0.0);
        check(pet.Action == PetAction.Idle && pet.VX == 0 && window.GiveTreat(), "The pet is idle and its normal life (treats, games) works again");
        Call(window, "CancelFeatureActivity");
        Tick(1);
        check(!window.BattleActive, "Nothing restarts the battle by itself");

        // A Python reply chosen before the battle never lands after it.
        window.Reset();
        var before = new DelayedSource();
        aiField.SetValue(window, before);
        Call(window, "RequestDecision");
        await Task.Delay(20);
        window.StartBattle();
        window.EndBattle();
        pet.Action = PetAction.Idle;
        before.Completion.SetResult(new AiDecision("SLEEP", 5, "sleepy", Emotion: "sleepy"));
        await Task.Delay(30);
        check(pet.Action == PetAction.Idle && !pet.Emotions.SleepLocked, "A Python reply from before the battle is dropped (no late command)");
        aiField.SetValue(window, null);

        // Ending while the cat is carried leaves it to input and physics.
        window.Reset();
        window.StartBattle();
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        typeof(PetWindow).GetField("pressPosition", Flags)!.SetValue(window, new Point(-100000, -100000));
        Call(window, "CheckDragThreshold");
        window.EndBattle();
        check(pet.Action == PetAction.Dragging && !window.BattleActive, "Ending mid-drag keeps the drag (no forced Idle in the air)");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        check(pet.Action == PetAction.Thrown, "The throw after it is the normal one");

        // Losing: a dizzy, harmless end.
        window.Reset();
        long roughThrows = pet.Interactions.RoughThrows;
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, CatMaxHp = 10, SlimeDamage = 10, CatDamage = 0, OutroSeconds = 1 };
        window.StartBattle();
        Tick(0.6);
        Sim().Slimes[0].X = pet.X + 56 + 40;
        Tick(1.2);
        check(window.Battle?.Phase == BattlePhase.Lost && pet.BattleDazed && pet.Action == PetAction.Sitting, "At 0 HP the cat sits down seeing stars");
        Tick(1.2);
        check(!window.BattleActive && !pet.BattleDazed && OpenBattleWindows() == 0, "Then the battle ends by itself and cleans up");
        check(ResultWindow() is { IsVisible: true } && window.LastBattleSummary is { Won: false, Defeated: 0 } lost && lost.Hp == 0,
            "A result panel shows the defeat and its numbers");
        pet.Emotions.Step(pet, 0.5);
        check(pet.Emotions.Current is PetEmotion.Neutral or PetEmotion.Happy && pet.Interactions.RoughThrows == roughThrows && !pet.Emotions.SleepLocked,
            "Losing leaves no lasting mark (no sadness, anger or rough-handling count)");

        // Winning, and repeated battles leave nothing behind.
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, Boss = false, SlimeMaxHp = 1, OutroSeconds = 0.5 };
        for (int round = 0; round < 3; round++)
        {
            window.Reset();
            window.StartBattle();
            Tick(0.6);
            if (window.Battle is { } b && b.Slimes.Count > 0) b.Slimes[0].X = pet.X + 56 + 50;
            Tick(1);
        }
        check(!window.BattleActive && OpenBattleWindows() == 0 && !pet.BattleCheer, "Three won battles in a row: each ends by itself and leaves no window");
        check(window.LastBattleSummary is { Won: true, Defeated: 1 } won && won.Seconds > 0 && ResultWindows() == 1,
            "After a victory one result panel shows slimes defeated, time and HP");
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string resultReview && ResultWindow()?.Content is FrameworkElement resultContent)
        {
            resultContent.UpdateLayout();
            var shot = new RenderTargetBitmap((int)Math.Ceiling(resultContent.ActualWidth + 12), (int)Math.Ceiling(resultContent.ActualHeight + 12), 96, 96, PixelFormats.Pbgra32);
            shot.Render(resultContent);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(shot));
            using var stream = System.IO.File.Create(System.IO.Path.Combine(resultReview, "battle-result.png")); encoder.Save(stream);
        }
        long resultStyle = GetWindowLongPtr(new WindowInteropHelper(ResultWindow()!).Handle, -20).ToInt64();
        check((resultStyle & NoActivate) != 0 && ResultWindow()!.ShowActivated == false, "The result panel never takes focus");
        // "Play again" starts the next battle from the panel.
        var buttons = (System.Windows.Controls.Panel)((System.Windows.Controls.Panel)((System.Windows.Controls.Border)ResultWindow()!.Content).Child).Children[2];
        buttons.Children[0].RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        await Task.Delay(20);
        check(window.BattleActive && ResultWindows() == 0, "Play again closes the panel and starts a new battle");
        window.EndBattle();
        check(!window.BattleActive && ResultWindows() == 0, "Ending a battle by hand shows no result panel");
        Call(window, "ShowBattleSummary", new PetWindow.BattleSummary(true, 3, 61, 40, 100));
        buttons = (System.Windows.Controls.Panel)((System.Windows.Controls.Panel)((System.Windows.Controls.Border)ResultWindow()!.Content).Child).Children[2];
        buttons.Children[1].RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        await Task.Delay(20);
        check(ResultWindows() == 0 && !window.BattleActive, "Close just closes the panel");
        Call(window, "ShowBattleSummary", new PetWindow.BattleSummary(true, 3, 61, 40, 100));
        SetContext(window, true, 0); Call(window, "TickContext", 1.1);
        await Task.Delay(20);
        check(ResultWindows() == 0, "A fullscreen app closes the result panel");
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        Call(window, "ShowBattleSummary", new PetWindow.BattleSummary(false, 0, 5, 0, 100));
        window.Reset();
        await Task.Delay(20);
        check(ResultWindows() == 0, "Bringing the pet back closes the result panel");

        // Why a battle cannot start: a small click-through note by the cat, gone after a moment.
        Call(window, "ShowBattleNotice", "The pet is asleep.");
        var note = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet notice" && w.IsVisible);
        long noteStyle = note == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(note).Handle, -20).ToInt64();
        check(note != null && (noteStyle & NoActivate) != 0 && (noteStyle & Transparent) != 0, "A refusal shows a click-through note that never takes focus");
        await Task.Delay(TimeSpan.FromSeconds(PetWindow.BattleNoticeSeconds + 0.4));
        check(!Application.Current.Windows.OfType<Window>().Any(w => w.Title == "Pet notice" && w.IsVisible), "The note disappears by itself");

        // Without Python at all the battle still runs (these checks run with PET_AI_DISABLED).
        window.Reset();
        aiField.SetValue(window, null);
        window.BattleTuning = fast;
        check(window.StartBattle() == null, "Battle Mode starts without Python");
        Tick(1);
        check(window.Battle is { Wave: 1 } && window.Battle.Slimes.Count > 0, "and slimes arrive without Python");
        window.Reset();
        check(!window.BattleActive && OpenBattleWindows() == 0, "Bringing the pet back to the main screen ends the battle");
        window.BattleTuning = new BattleSettings();

        // The sword: drawn in every pose without leaving the window; without it nothing changes.
        bool edgesClear = true;
        string Render(PetModel model, string label)
        {
            const int size = 192;
            var visual = new PetRenderer(model);
            visual.Measure(new Size(size, size)); visual.Arrange(new Rect(0, 0, size, size));
            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32); bmp.Render(visual);
            var px = new byte[size * size * 4]; bmp.CopyPixels(px, size * 4, 0);
            for (int i = 0; i < size; i++)
                edgesClear &= px[i * size * 4 + 3] == 0 && px[(i * size + size - 1) * 4 + 3] == 0 && px[i * 4 + 3] == 0 && px[((size - 1) * size + i) * 4 + 3] == 0;
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review)
            {
                System.IO.Directory.CreateDirectory(review);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
                using var stream = System.IO.File.Create(System.IO.Path.Combine(review, label + ".png")); encoder.Save(stream);
            }
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(px));
        }
        PetModel Model(int facing = 1) => new() { Action = PetAction.Idle, Grounded = true, Facing = facing, AnimationTime = 1, ActionTime = 1 };
        string plain = Render(Model(), "battle-none");
        var armed = Model(); armed.SwordOut = true;
        check(Render(armed, "battle-sword") != plain, "The sword is drawn when the battle gives it to the cat");
        foreach (int facing in new[] { 1, -1 })
        {
            for (double u = 0; u <= 1.0001; u += 0.1)
            {
                var m = Model(facing); m.SwordOut = true; m.SwordSwing = u; m.BattleHurt = u < 0.3 ? 0.3 : 0;
                Render(m, $"battle-swing-{(facing > 0 ? "right" : "left")}-{u:F1}");
            }
            var dragged = Model(facing); dragged.SwordOut = true; dragged.Action = PetAction.Dragging; dragged.Grounded = false; dragged.Pickup.Begin(0, 0);
            for (int frame = 0; frame < 90; frame++) dragged.Pickup.Step(frame < 45 ? frame * 40 : (90 - frame) * 40, 0, 1.0 / 120);
            Render(dragged, $"battle-dragged-{facing}");
            var flying = Model(facing); flying.SwordOut = true; flying.Action = PetAction.Thrown; flying.Grounded = false; flying.VY = -700;
            Render(flying, $"battle-thrown-{facing}");
            var dazed = Model(facing); dazed.SwordOut = true; dazed.BattleDazed = true; dazed.Action = PetAction.Sitting;
            Render(dazed, $"battle-dazed-{facing}");
            var shielded = Model(facing); shielded.SwordOut = true; shielded.BattleShield = shielded.BattleBuff = true; shielded.AnimationTime = 2.3;
            Render(shielded, $"battle-shield-{facing}");
            var heldShield = Model(facing); heldShield.SwordOut = true; heldShield.BattleShield = heldShield.BattleBuff = true;
            heldShield.Action = PetAction.Dragging; heldShield.Grounded = false; heldShield.Pickup.Begin(0, 0);
            for (int frame = 0; frame < 90; frame++) heldShield.Pickup.Step(frame < 45 ? frame * 40 : (90 - frame) * 40, 0, 1.0 / 120);
            Render(heldShield, $"battle-shield-dragged-{facing}");
            var cheer = Model(facing); cheer.SwordOut = true; cheer.BattleCheer = true; cheer.Action = PetAction.Sitting;
            Render(cheer, $"battle-cheer-{facing}");
            var walking = Model(facing); walking.SwordOut = true; walking.Action = PetAction.Walking; walking.VX = 72 * facing;
            Render(walking, $"battle-walk-{facing}");
        }
        check(edgesClear, "The sword, shield bubble and cheer sparkles stay inside the window in every pose, both ways");

        // The Slime King on the field (drawn larger, with a crown and its slam warning).
        window.Reset();
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1, WaveBreakSeconds = 0.2 };
        window.StartBattle();
        Tick(0.6);
        Sim().Slimes[0].X = pet.X + 56 + 50;
        for (int i = 0; i < 400 && Sim().Boss is not { Targetable: true }; i++) Tick(1.0 / 60);
        var bossSlime = Sim().Boss;
        check(bossSlime is { Targetable: true, IsBoss: true } && Field<BattleHud>(window, "battleHud") != null, "The boss wave brings the Slime King");
        var fieldView = Field<BattleFieldView>(window, "battleView");
        if (bossSlime != null)
        {
            bossSlime.X = pet.X + 56 + 90;
            Tick(0.9); // Winding up its slam.
            fieldView.InvalidateVisual(); fieldView.UpdateLayout();
            double bx = bossSlime.X - fieldView.OriginX;
            check(fieldView.SlimeAt(new Point(bx + bossSlime.Width / 2 - 4, fieldView.ActualHeight - 30)) == bossSlime.Id,
                "Clicks reach the whole (wider) boss");
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string bossReview)
            {
                int fw = (int)fieldView.ActualWidth, fh = (int)fieldView.ActualHeight;
                var shot = new RenderTargetBitmap(fw, fh, 96, 96, PixelFormats.Pbgra32); shot.Render(fieldView);
                var crop = new CroppedBitmap(shot, new Int32Rect((int)Math.Clamp(bx - 200, 0, Math.Max(0, fw - 400)), 0, Math.Min(400, fw), fh));
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen()) { dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x24)), null, new Rect(0, 0, 400, fh)); dc.DrawImage(crop, new Rect(0, 0, crop.PixelWidth, fh)); }
                var image = new RenderTargetBitmap(400, fh, 96, 96, PixelFormats.Pbgra32); image.Render(visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using (var stream = System.IO.File.Create(System.IO.Path.Combine(bossReview, "battle-boss.png"))) encoder.Save(stream);
                var hudNow = Field<BattleHud>(window, "battleHud");
                hudNow.UpdateLayout();
                var hudShot = new RenderTargetBitmap((int)Math.Ceiling(hudNow.ActualWidth + 16), (int)Math.Ceiling(hudNow.ActualHeight + 16), 96, 96, PixelFormats.Pbgra32);
                hudShot.Render(hudNow);
                var hudEncoder = new PngBitmapEncoder(); hudEncoder.Frames.Add(BitmapFrame.Create(hudShot));
                using (var stream = System.IO.File.Create(System.IO.Path.Combine(bossReview, "battle-hud-boss.png"))) hudEncoder.Save(stream);
            }
        }
        window.Reset();
        window.BattleTuning = new BattleSettings();
    }

    private sealed class DelayedSource : IPetDecisionSource
    {
        public readonly TaskCompletionSource<AiDecision?> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Action? Requested;
        public string Status => "test";
        public Task<AiDecision?> DecideAsync(PetObservation observation) { Requested?.Invoke(); return Completion.Task; }
        public void Dispose() { }
    }
}
