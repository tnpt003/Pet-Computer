using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using DesktopPet.AI;
using static PetPrivate;

// Battle Mode inside the real pet window: lifecycle, ownership of the pet, input, pause,
// Python isolation and cleanup. The real cursor is never moved.
internal static class BattleChecks
{
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
        // A fixed spot with room on its left (spawns can sit at the floor's edge, where a push is
        // clamped; that case has its own check in BattleTests): mid-floor, away from the cat, and
        // every other slime parked well to its right.
        double mid = (Sim().Left + Sim().Right) / 2, catMid = pet.X + 56;
        double spot = Math.Abs(mid - catMid) >= 200 ? mid : mid + (mid >= catMid ? 200 : -200);
        other.X = spot;
        foreach (var s in Sim().Slimes) if (s != other) s.X = Math.Min(spot + 260 + 60 * (s.Id % 3), Sim().Right - 40);
        double otherBefore = other.X;
        check(window.BattleClickSlime(other.Id, 12) && other.State == SlimeState.Stunned && Sim().SelectedId == slime.Id && !Sim().PushArmed,
            "The next slime click pushes and stuns it; the marked target stays");
        Tick(0.3);
        check(other.X < otherBefore - 20, $"Clicked on its right half, it flies left ({otherBefore:F0} → {other.X:F0})");
        check(!window.ArmBattlePush(true) && Sim().PushCooldown > 0, "The skill cools down before it can be chosen again");

        // The game HUD: five skill slots, used with the mouse.
        var gameHud = Field<BattleHud>(window, "battleHud");
        void Press(UIElement element) => element.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        check(BattleHud.SkillOrder.Length == 5 && BattleHud.SkillOrder.All(skill => gameHud.Describe(skill).Length > 10 && gameHud.Slot(skill).ToolTip == null),
            "The HUD has five skill slots, each described in the hint line (no tooltips)");
        string idleHint = gameHud.HintText;
        gameHud.Hover(BattleSkill.Heal);
        string healHint = gameHud.HintText;
        gameHud.Hover(BattleSkill.Bomb);
        string bombHint = gameHud.HintText;
        gameHud.Hover(null);
        check(healHint.StartsWith(gameHud.Describe(BattleSkill.Heal)) && bombHint.StartsWith(gameHud.Describe(BattleSkill.Bomb)) && gameHud.HintText == idleHint,
            "Hovering one slot then another shows each one's text in the same place; leaving restores the hint");
        // The HUD steps aside: over its information part it fades and lets clicks through; over a
        // slot or a button it is solid and clickable; away from it, normal.
        var cursorProbe = FieldOf("cursorProbe");
        var usualCursor = cursorProbe.GetValue(window);
        var hudHandle = new WindowInteropHelper(Field<Window>(window, "battleHudWindow")).Handle;
        bool HudClickThrough() => (GetWindowLongPtr(hudHandle, -20).ToInt64() & Transparent) != 0;
        void CursorAt(Point screen) { cursorProbe.SetValue(window, (Func<Point>)(() => screen)); Call(window, "UpdateBattleHudDodge"); }
        CursorAt(gameHud.PointToScreen(new Point(20, 20)));
        check(window.BattleHudDodging && HudClickThrough() && gameHud.Opacity < 0.5, "Over the HUD's portrait the HUD fades and lets clicks through");
        var slotCentre = gameHud.Slot(BattleSkill.Heal);
        CursorAt(slotCentre.PointToScreen(new Point(slotCentre.ActualWidth / 2, slotCentre.ActualHeight / 2)));
        check(!window.BattleHudDodging && !HudClickThrough() && gameHud.Opacity == 1, "Over a skill slot it is solid and clickable again");
        CursorAt(gameHud.PointToScreen(new Point(20, 20)));
        CursorAt(gameHud.EndButton.PointToScreen(new Point(5, 5)));
        check(!window.BattleHudDodging && !HudClickThrough(), "Over Pause/End too");
        CursorAt(gameHud.PointToScreen(new Point(20, 20)));
        CursorAt(new Point(-30000, -30000));
        check(!window.BattleHudDodging && !HudClickThrough() && gameHud.Opacity == 1, "Away from the HUD it looks and works as before");
        // Moving the HUD by its grip (2026-10-22): it follows the cursor inside the work area above the
        // field, is remembered per monitor, and a double click puts it back at the top centre.
        var hudArea = Field<Area>(window, "battleArea");
        double hudScale = Field<double>(window, "battleScale");
        var hudHome = window.BattleHudPosition;
        check(Math.Abs(hudHome.X - (hudArea.Left + hudArea.Right - Field<Window>(window, "battleHudWindow").ActualWidth) / 2) < 1 && Math.Abs(hudHome.Y - (hudArea.Top + 12)) < 1,
            "The HUD starts centred at the top");
        var gripAt = gameHud.Grip.PointToScreen(new Point(7, 26));
        CursorAt(gripAt);
        check(!window.BattleHudDodging && gameHud.IsInteractiveAt(gameHud.Grip.TranslatePoint(new Point(7, 26), gameHud)), "The gripAt is solid and clickable (no stepping aside over it)");
        Call(window, "StartBattleHudDrag");
        cursorProbe.SetValue(window, (Func<Point>)(() => new Point(gripAt.X - 150 * hudScale, gripAt.Y + 120 * hudScale)));
        Call(window, "PlaceBattleHud");
        check(Math.Abs(window.BattleHudPosition.X - (hudHome.X - 150)) < 1.5 && Math.Abs(window.BattleHudPosition.Y - (hudHome.Y + 120)) < 1.5, "Dragging the gripAt moves the HUD with the cursor");
        Call(window, "EndBattleHudDrag");
        var hudMoved = window.BattleHudPosition;
        check(preferences.BattleHudSpots?.ContainsKey(Field<string>(window, "battleMonitor")) == true, "Let go: the place is remembered for this monitor");
        cursorProbe.SetValue(window, usualCursor);
        Tick(1.0 / 60);
        check(Math.Abs(window.BattleHudPosition.X - hudMoved.X) < 1 && Math.Abs(window.BattleHudPosition.Y - hudMoved.Y) < 1, "It stays there while the battle goes on");
        Call(window, "StartBattleHudDrag");
        cursorProbe.SetValue(window, (Func<Point>)(() => new Point(gripAt.X + 99999, gripAt.Y + 99999)));
        Call(window, "PlaceBattleHud");
        Call(window, "EndBattleHudDrag");
        double hudHeight = Field<Window>(window, "battleHudWindow").ActualHeight;
        check(window.BattleHudPosition.Y <= hudArea.Bottom - BattleFieldView.FieldHeight - hudHeight + 0.5 && window.BattleHudPosition.X <= hudArea.Right + 0.5 - Field<Window>(window, "battleHudWindow").ActualWidth,
            "It never goes off the work hudArea or over the battlefield strip");
        Call(window, "ResetBattleHudSpot");
        check(Math.Abs(window.BattleHudPosition.X - hudHome.X) < 1 && Math.Abs(window.BattleHudPosition.Y - hudHome.Y) < 1 && preferences.BattleHudSpots == null,
            "Double click on the grip: back to the top centre, nothing remembered");
        cursorProbe.SetValue(window, usualCursor);
        Tick(1.0 / 60);
        check(!gameHud.Slot(BattleSkill.Push).IsReady && gameHud.Slot(BattleSkill.Push).Remaining > 0 && gameHud.Slot(BattleSkill.Heal).IsReady,
            "A used skill shows its cooldown; the others are ready");
        // The hit chain on the HUD: ×1, then gold ×2 (the next hit is the combo), hidden once it lapses.
        int savedCombo = Sim().Combo; double savedClock = Sim().comboClock;
        double hudWidth = gameHud.ActualWidth;
        string ShowChain(int hits, double since)
        {
            Sim().Combo = hits; Sim().comboClock = since;
            gameHud.Update(Sim(), false, false, false);
            gameHud.UpdateLayout();
            return gameHud.ComboText.Visibility == Visibility.Visible ? gameHud.ComboText.Text : "";
        }
        string none = ShowChain(0, 0), one = ShowChain(1, 0);
        string two = ShowChain(2, 0.5);
        var twoBrush = (SolidColorBrush)gameHud.ComboText.Foreground;
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string comboReview)
        {
            var comboShot = new RenderTargetBitmap((int)Math.Ceiling(gameHud.ActualWidth + 16), (int)Math.Ceiling(gameHud.ActualHeight + 16), 96, 96, PixelFormats.Pbgra32);
            comboShot.Render(gameHud);
            var comboEncoder = new PngBitmapEncoder(); comboEncoder.Frames.Add(BitmapFrame.Create(comboShot));
            using var comboStream = System.IO.File.Create(System.IO.Path.Combine(comboReview, "battle-hud-combo.png")); comboEncoder.Save(comboStream);
        }
        string lapsed = ShowChain(2, 10);
        check(none == "" && one == "×1" && two == "×2" && twoBrush.Color == Color.FromRgb(0xFF, 0xD2, 0x3F) && lapsed == "" &&
              Math.Abs(gameHud.ActualWidth - hudWidth) < 0.5,
            $"The HUD shows the hit chain (×1, gold ×2 before the combo) and hides it once it lapses, without changing width ({none}|{one}|{two}|{lapsed})");
        Sim().Combo = savedCombo; Sim().comboClock = savedClock;
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
            // A combo burst on the marked slime, a moment after the hit.
            Sim().AddEffect(BattleEffectKind.Combo, slime.X, slime.Height / 2, 0.0);
            Sim().Effects[^1].Age = 0.15;
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
        var aiField = FieldOf("ai");
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
        var windups = Sim().Slimes.Where(s => s.State == SlimeState.Windup).Select(s => (s, s.StateTime)).ToList();
        Tick(0.5);
        check(Sim().CatHp == hpBefore && windups.All(w => w.s.State == SlimeState.Windup && w.s.StateTime == w.StateTime),
            $"A press on the cat (before any drag) already holds slime attacks ({windups.Count} held)");
        FieldOf("pressPosition").SetValue(window, new Point(-100000, -100000));
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

        // The cat runs in a battle (physics' own walking, just faster).
        check(pet.MoveScale == Sim().Settings.CatRunScale && pet.MoveScale > 1, "During a battle the cat runs (walking speed × CatRunScale)");
        var physics = Field<PetPhysics>(window, "physics");
        var runner = new PetModel { X = 100, Y = 0, Grounded = true, Action = PetAction.Walking, Facing = 1, MoveScale = Sim().Settings.CatRunScale };
        var walker = new PetModel { X = 100, Y = 0, Grounded = true, Action = PetAction.Walking, Facing = 1 };
        var floorArea = new Area(0, 0, 4000, 112);
        for (int i = 0; i < 240; i++) { physics.Step(runner, floorArea, 1.0 / 120); physics.Step(walker, floorArea, 1.0 / 120); }
        check(Math.Abs(runner.VX - 72 * Sim().Settings.CatRunScale) < 1 && Math.Abs(walker.VX - 72) < 1,
            $"Running reaches {72 * Sim().Settings.CatRunScale:F0} DIPs/s; normal walking stays 72 ({runner.VX:F0} / {walker.VX:F0})");

        // Carried to another monitor: the battle waits there, paused, and resumes when the cat is back.
        var monitorProbe = FieldOf("battleMonitorProbe");
        var home = (string)FieldOf("battleMonitor").GetValue(window)!;
        monitorProbe.SetValue(window, new Func<string>(() => "\\\\.\\OTHER"));
        double fieldLeft = field!.Left;
        Tick(1.0 / 60);
        var awayNote = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet notice" && w.IsVisible);
        check(Sim().Paused && window.BattleCatAway && awayNote != null && Math.Abs(field.Left - fieldLeft) < 1,
            "On another monitor the battle pauses with a note; the field stays where it was");
        double awayTime = Sim().Time;
        Tick(1);
        check(Sim().Time == awayTime && pet.VX == 0, "While the cat is away nothing in the battle moves");
        monitorProbe.SetValue(window, new Func<string>(() => home));
        Tick(1.0 / 60);
        check(!Sim().Paused && !window.BattleCatAway, "Back on the battle's monitor it goes on by itself");
        monitorProbe.SetValue(window, new Func<string>(() => "\\\\.\\OTHER"));
        Tick(1.0 / 60);
        window.SetBattlePaused(true); // The user pauses while the cat is away...
        monitorProbe.SetValue(window, new Func<string>(() => home));
        Tick(1.0 / 60);
        check(Sim().Paused, "...then a pause the user chose is kept when the cat comes back");
        window.SetBattlePaused(false);
        monitorProbe.SetValue(window, null);

        // End: everything goes away and the pet lives normally again.
        window.EndBattle();
        await Task.Delay(20);
        check(!window.BattleActive && OpenBattleWindows() == 0 && Field<Window?>(window, "battleFieldWindow") == null && !pet.SwordOut && !pet.Emotions.Suspended && pet.MoveScale == 1,
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
        FieldOf("pressPosition").SetValue(window, new Point(-100000, -100000));
        Call(window, "CheckDragThreshold");
        window.EndBattle();
        check(pet.Action == PetAction.Dragging && !window.BattleActive, "Ending mid-drag keeps the drag (no forced Idle in the air)");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        check(pet.Action == PetAction.Thrown, "The throw after it is the normal one");

        // A dash stun in the real window: stars on the standing cat; (2026-10-24) it cannot be picked
        // up until the stun ends, then it can; ending the battle leaves nothing behind.
        window.Reset();
        window.BattleTuning = new BattleSettings
        {
            FirstWaveDelay = 0, CatDamage = 0,
            Enemies = new[] { new EnemyType { Id = "runner", MaxHp = 1000, Damage = 1, Speed = 70, Scale = 0.75, Dash = true } },
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "runner", Count = 1 } } } }
        };
        window.StartBattle();
        for (int i = 0; i < 900 && !Sim().CatStunned; i++) Call(window, "TickBattle", 1.0 / 60);
        Tick(0.1);
        check(Sim().CatStunned && pet.BattleStun > 0 && pet.Action == PetAction.Idle && pet.SwordSwing < 0,
            "A dash stuns the cat in the window: stars, standing still, no swing");
        window.Activate();
        await Task.Delay(20);
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        FieldOf("pressPosition").SetValue(window, new Point(-100000, -100000));
        Call(window, "CheckDragThreshold");
        Tick(0.2);
        check(pet.Action != PetAction.Dragging && Sim().CatStunned && pet.BattleStun > 0, "A stunned cat cannot be dragged; it stays stunned on the floor");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        for (int i = 0; i < 200 && Sim().CatStunned; i++) Call(window, "TickBattle", 1.0 / 60);
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        FieldOf("pressPosition").SetValue(window, new Point(-100000, -100000));
        Call(window, "CheckDragThreshold");
        Tick(0.1);
        check(!Sim().CatStunned && pet.Action == PetAction.Dragging, "Once the stun is over it can be dragged");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        check(pet.Action == PetAction.Thrown, "and thrown as usual");
        window.EndBattle();
        check(!window.BattleActive && pet.BattleStun == 0, "Ending the battle clears the stun (nothing left for Pet Mode)");
        window.Reset();
        window.BattleTuning = new BattleSettings();

        // Losing: a dizzy, harmless end.
        var reportBefore = window.BattleReport;
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
        IEnumerable<System.Windows.Controls.TextBlock> Texts(object node)
        {
            if (node is not DependencyObject element) yield break;
            foreach (object child in LogicalTreeHelper.GetChildren(element))
            {
                if (child is System.Windows.Controls.TextBlock text) yield return text;
                foreach (var inner in Texts(child)) yield return inner;
            }
        }
        string PanelText() => ResultWindow()?.Content is { } panelRoot ? string.Join(" | ", Texts(panelRoot).Select(t => t.Text)) : "";
        check(window.BattleRecords.For(BattleWeapon.Sword, BattleDifficulty.Normal) is { Wins: 3, MostDefeated: 1, BestSeconds: > 0 } && window.BattleRecords.Normal.Empty &&
              window.LastBattleSummary?.Difficulty == BattleDifficulty.Normal && PanelText().Contains("Sword · Normal") &&
              PanelText().Contains("Records (Sword · Normal)") && PanelText().Contains("3 wins") && !PanelText().Contains("Before weapons"),
            $"Each win counts in the sword + Normal records, shown with the loadout on the panel ({PanelText()})");

        // Difficulty: the menu choice goes into the next battle, with its own records.
        window.SetBattleDifficulty(BattleDifficulty.Hard);
        window.Reset();
        window.StartBattle();
        check(window.Battle?.Difficulty == BattleDifficulty.Hard && window.BattleDifficultyChoice == BattleDifficulty.Hard, "The chosen difficulty starts the next battle");
        Tick(0.6);
        if (window.Battle is { } hardBattle && hardBattle.Slimes.Count > 0) hardBattle.Slimes[0].X = pet.X + 56 + 50;
        Tick(1);
        check(window.LastBattleSummary is { Difficulty: BattleDifficulty.Hard, News.Fastest: true } && window.BattleRecords.For(BattleWeapon.Sword, BattleDifficulty.Hard).Wins == 1 &&
              window.BattleRecords.For(BattleWeapon.Sword, BattleDifficulty.Normal).Wins == 3 &&
              PanelText().Contains("New record") && PanelText().Contains("Hard"),
            $"A first Hard win is a new Hard record; Normal's stay apart ({PanelText()})");
        var report = window.BattleReport;
        check(report.Won - reportBefore.Won == 4 && report.Lost - reportBefore.Lost == 1 && report.Records - reportBefore.Records >= 1,
            $"Finished battles are counted for Python (won +{report.Won - reportBefore.Won}, lost +{report.Lost - reportBefore.Lost}, records +{report.Records - reportBefore.Records})");
        window.SetBattleDifficulty(BattleDifficulty.Normal);
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
        // The panel's buttons, found by their text (the layout may change).
        void PressResult(string text)
        {
            var root = ResultWindow()?.Content as DependencyObject;
            var label = root == null ? null : Texts(root).FirstOrDefault(t => t.Text == text);
            DependencyObject? node = label;
            while (node != null && node is not System.Windows.Controls.Border { Cursor: not null }) node = LogicalTreeHelper.GetParent(node);
            (node as UIElement)?.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        }
        check(PanelText().Contains("Play again") && PanelText().Contains("Change gear") && PanelText().Contains("Back to desktop"),
            "The result offers Play again, Change gear and Back to desktop");
        // Play again: the same loadout at once; a double click starts one battle.
        var lastLoadout = window.LastBattleSummary!.Value;
        var reportAtReplay = window.BattleReport;
        var buttonsWindow = ResultWindow();
        PressResult("Play again");
        var replay = window.Battle;
        if (buttonsWindow?.Content is DependencyObject oldPanel)
        {
            var label = Texts(oldPanel).FirstOrDefault(t => t.Text == "Play again");
            DependencyObject? node = label;
            while (node != null && node is not System.Windows.Controls.Border { Cursor: not null }) node = LogicalTreeHelper.GetParent(node);
            (node as UIElement)?.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        }
        await Task.Delay(20);
        check(replay != null && window.Battle == replay && replay.Difficulty == lastLoadout.Difficulty && replay.Weapon.Id == lastLoadout.Weapon &&
              !window.BattlePreparing && ResultWindows() == 0 && window.BattleReport == reportAtReplay && OpenBattleWindows() == 2,
            "Play again starts one battle with the same weapon and difficulty (a second press does nothing, no result counted twice)");
        window.EndBattle();
        check(!window.BattleActive && ResultWindows() == 0, "Ending a battle by hand shows no result panel");
        // Change gear: back to the setup panel with the battle's loadout, no win or loss counted.
        Call(window, "ShowBattleSummary", new PetWindow.BattleSummary(true, 3, 61, 40, 100, Difficulty: BattleDifficulty.Hard, Weapon: BattleWeapon.Hammer, WeaponLabel: "Hammer", WeaponStyle: "hammer"));
        var reportAtGear = window.BattleReport;
        PressResult("Change gear");
        await Task.Delay(20);
        check(ResultWindows() == 0 && !window.BattleActive && window.BattlePreparing && window.BattlePrepWeapon == BattleWeapon.Hammer &&
              window.BattlePrepDifficulty == BattleDifficulty.Hard && window.BattleReport == reportAtGear,
            "Change gear opens the setup panel with that battle's weapon and difficulty");
        window.CancelBattlePrep();
        // Play again when the cat is not free: the setup panel opens with the loadout and the reason; nothing is forced.
        Call(window, "ShowBattleSummary", new PetWindow.BattleSummary(true, 3, 61, 40, 100));
        SetField(window, "dragging", true); pet.Action = PetAction.Dragging; pet.Grounded = false;
        PressResult("Play again");
        await Task.Delay(20);
        check(!window.BattleActive && window.BattlePreparing && !window.BattlePrepReadyEnabled && pet.Action == PetAction.Dragging && ResultWindows() == 0,
            "Play again while the cat is dragged opens the setup panel (Ready waits) and keeps the drag");
        SetField(window, "dragging", false); pet.Action = PetAction.Idle; pet.Grounded = true; pet.VX = pet.VY = 0;
        window.CancelBattlePrep();
        // Back to desktop just closes; the panel no longer closes by itself.
        Call(window, "ShowBattleSummary", new PetWindow.BattleSummary(true, 3, 61, 40, 100));
        check(typeof(PetWindow).GetField("battleSummaryTimer", Flags) == null && window.BattleSummaryOpen, "The result waits for the user (no timer closes it)");
        PressResult("Back to desktop");
        await Task.Delay(20);
        check(ResultWindows() == 0 && !window.BattleActive && !window.BattlePreparing, "Back to desktop just closes the panel");
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
        var slash = Model(); slash.SwordOut = true; slash.SwordSwing = 0.5;
        var goldSlash = Model(); goldSlash.SwordOut = true; goldSlash.SwordSwing = 0.5; goldSlash.SwordCombo = true;
        check(Render(goldSlash, "battle-combo-swing") != Render(slash, "battle-plain-swing"), "The combo swing has its own (gold) slash");
        var restCombo = Model(); restCombo.SwordOut = true; restCombo.SwordCombo = true;
        check(Render(restCombo, "battle-combo-rest") == Render(armed, "battle-sword"), "Outside the slash the combo flag changes nothing");
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
            // A dash stun: stars and dazed eyes on the standing cat. Picked up, the stun and the
            // pickup's own dizziness share one orbit (the stronger one), never two.
            var stunned = Model(facing); stunned.SwordOut = true; stunned.BattleStun = 1;
            check(Render(stunned, $"battle-stunned-{facing}") != Render(armed, "battle-sword"), $"A stunned cat shows stars ({facing})");
            PetModel Lifted(bool shaken, double stun)
            {
                var m = Model(facing); m.SwordOut = true; m.Action = PetAction.Dragging; m.Grounded = false; m.Pickup.Begin(0, 0); m.BattleStun = stun;
                for (int frame = 0; frame < 90; frame++) m.Pickup.Step(shaken ? (frame < 45 ? frame * 40 : (90 - frame) * 40) : 0, 0, 1.0 / 120);
                return m;
            }
            check(Render(Lifted(false, 1), $"battle-stunned-lifted-{facing}") != Render(Lifted(false, 0), $"battle-calm-lifted-{facing}"),
                $"A stunned cat keeps its stars while gently carried ({facing})");
            var shaken = Lifted(true, 0);
            check(shaken.Pickup.Dizziness < 0.75 || Render(Lifted(true, 1), "battle-shaken-stunned") == Render(shaken, "battle-shaken"),
                $"Shaken while stunned: one orbit, not two ({facing}, pickup dizziness {shaken.Pickup.Dizziness:0.00})");
            var shielded = Model(facing); shielded.SwordOut = true; shielded.BattleShield = shielded.BattleBuff = true; shielded.BattleTime = 2.3;
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

        // Every enemy kind from battle.json side by side, for a person to look at.
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string kindsReview)
        {
            var data = BattleData.Settings;
            var lineup = new BattleSim(data, 0, 600, new Random(1));
            double at = 60;
            foreach (var kind in data.EnemyTypes)
            {
                var made = lineup.AddSlime(at + data.SlimeWidth * kind.Scale / 2, kind);
                made.Set(SlimeState.Moving); made.StateTime = 0.4; made.Facing = -1;
                at += data.SlimeWidth * kind.Scale + 50;
            }
            var lineupView = new BattleFieldView(lineup) { OriginX = 0 };
            lineupView.Measure(new Size(600, BattleFieldView.FieldHeight)); lineupView.Arrange(new Rect(0, 0, 600, BattleFieldView.FieldHeight));
            foreach (var (name, back) in new[] { ("light", Colors.WhiteSmoke), ("dark", Color.FromRgb(0x1E, 0x1E, 0x24)) })
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(back), null, new Rect(0, 0, 600, BattleFieldView.FieldHeight));
                    dc.DrawRectangle(new VisualBrush(lineupView) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top,
                        ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 600, BattleFieldView.FieldHeight) },
                        null, new Rect(0, 0, 600, BattleFieldView.FieldHeight));
                }
                var image = new RenderTargetBitmap(600, (int)BattleFieldView.FieldHeight, 96, 96, PixelFormats.Pbgra32); image.Render(visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = System.IO.File.Create(System.IO.Path.Combine(kindsReview, $"battle-kinds-{name}.png")); encoder.Save(stream);
            }
        }

        // The battle's desktop apps (PetWindow.BattleApps.cs): five icon windows on the grid above the
        // strip, faded while the cursor is far; an ad's × on the strip takes the click and the rest of
        // it covers the slime behind. With PET_RENDER_REVIEW, pictures of the icons, flights and strip.
        {
            window.Reset();
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0, Apps = new AppsSpec { StartCharge = 0.4 } };
            check(window.StartBattle(stage: new StageSpec { Id = "ui_apps" }) == null, "A stage battle with desktop apps starts");
            Tick(0.4);
            var sim = Sim();
            double appScale = Field<double>(window, "battleScale");
            var appArea = Field<Area>(window, "battleArea");
            var probe = FieldOf("cursorProbe");
            var probeBefore = probe.GetValue(window);
            check(window.BattleAppWindows.Count == 5 && sim.Desktop.All(a =>
                {
                    var c = window.BattleAppCentre(a);
                    return c.Y < appArea.Bottom - BattleFieldView.FieldHeight && c.Y > appArea.Top && c.X > appArea.Left && c.X < appArea.Right;
                }) && sim.Desktop.Select(a => window.BattleAppCentre(a)).Distinct().Count() == 5,
                "Five app icons stand in their own windows on distinct cells above the strip");
            var first = sim.Desktop[0];
            var centre = window.BattleAppCentre(first);
            probe.SetValue(window, (Func<Point>)(() => new Point((appArea.Left + 2) * appScale, (appArea.Top + 2) * appScale)));
            if (window.BattleAppCentre(first).X < appArea.Left + 400) probe.SetValue(window, (Func<Point>)(() => new Point((appArea.Right - 2) * appScale, (appArea.Top + 2) * appScale)));
            Tick(1);
            double far = window.BattleAppOpacity(first.Id);
            probe.SetValue(window, (Func<Point>)(() => new Point(centre.X * appScale, centre.Y * appScale)));
            Tick(1);
            double near = window.BattleAppOpacity(first.Id);
            probe.SetValue(window, probeBefore);
            check(far < 0.6 && near > 0.97, $"An icon fades back while the cursor is far ({far:0.00}) and is solid with the cursor on it ({near:0.00})");
            // Windows scale changed mid-battle: every icon window is placed again in the new pixels.
            var scaleField = FieldOf("battleScale");
            var iconWindow = Field<System.Collections.IDictionary>(window, "appIcons")[first.Id]!;
            var firstWindow = (Window)iconWindow.GetType().GetField("Window")!.GetValue(iconWindow)!;
            double widthBefore = firstWindow.ActualWidth;
            scaleField.SetValue(window, appScale * 1.5);
            Tick(0.1); firstWindow.UpdateLayout();
            double widthScaled = firstWindow.ActualWidth;
            scaleField.SetValue(window, appScale);
            Tick(0.1); firstWindow.UpdateLayout();
            check(Math.Abs(widthScaled - widthBefore * 1.5) < 2 && Math.Abs(firstWindow.ActualWidth - widthBefore) < 2,
                $"A DPI change mid-battle places the icon windows again in the new pixels ({widthBefore:0} → {widthScaled:0} → {firstWindow.ActualWidth:0})");
            // The bag opened over an icon: that icon moves to a free cell (like the HUD's cells).
            check(window.OpenBattleBag() == null, "The bag opens during the battle");
            var bag = Field<Window>(window, "bagWindow");
            var covered = window.BattleAppCentre(first);
            bag.Left = covered.X - 30; bag.Top = covered.Y - 30;
            Tick(0.2);
            var tile = window.BattleAppCentre(first);
            var bagRect = new Rect(bag.Left, bag.Top, bag.ActualWidth, bag.ActualHeight);
            check(sim.Desktop.All(a => { var c = window.BattleAppCentre(a); return !bagRect.IntersectsWith(new Rect(c.X - 25, c.Y - 25, 50, 50)); }),
                $"An open bag over an icon sends it to a free cell (no icon under the bag; {covered} → {tile})");
            window.CloseBattleBag();
            Tick(0.1);
            check(!window.BattleClickApp(first.Id) && sim.Desktop[0].Charge < 1, "A click on an icon still charging does nothing (a note says so)");
            first.Charge = 1;
            var tabs = sim.Desktop.First(a => a.Kind == BattleAppKind.Tabs);
            tabs.Charge = 1;
            check(window.BattleClickApp(tabs.Id) && sim.Launches.Count == 1, "A click on a charged icon sends its help");
            Tick(0.1);
            check(Field<Window?>(window, "appFlightWindow") is { IsVisible: true }, "What flies is drawn on the flights' canvas");
            window.EndBattle();
            check(window.BattleAppWindows.Count == 0 && Field<Window?>(window, "appFlightWindow") == null, "Ending the battle closes the app windows");
            // Turned off in the setup panel: the stage battle runs without any icon window.
            var appsPrefs = Field<PetPreferences>(window, "preferences");
            appsPrefs.BattleApps = false;
            check(window.StartBattle(stage: new StageSpec { Id = "ui_apps" }) == null, "(setup) a stage battle with the apps turned off");
            Tick(0.4);
            check(!Sim().AppsOn && Sim().Desktop.Count == 0 && window.BattleAppWindows.Count == 0, "With the apps turned off the stage battle has no icons or icon windows");
            window.EndBattle();
            appsPrefs.BattleApps = true;
            // A loading ring thrown where the cat stands slows the cat's running; clicking its spinner closes it.
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0,
                Apps = new AppsSpec { InfectChance = 0, MalwareMinThreat = 0, MalwareEveryMin = 3, MalwareEveryMax = 3, LoadingWeight = 1, LoadingSlow = 0.5 } };
            check(window.StartBattle(stage: new StageSpec { Id = "ui_loading" }) == null, "(setup) a stage battle that throws a loading ring");
            for (int i = 0; i < 600 && Sim().Hazards.Count == 0; i++) Tick(1.0 / 60);
            Tick(0.05);
            var ringSim = Sim();
            check(ringSim.Hazards.Count == 1 && ringSim.LagFactor == 0.5 && Math.Abs(pet.MoveScale - ringSim.Settings.CatRunScale * ringSim.MoveFactor * 0.5) < 1e-9,
                $"A loading ring at the cat halves its running speed (MoveScale {pet.MoveScale:0.00})");
            check(window.BattleCloseHazard(ringSim.Hazards[0].Id) && ringSim.Hazards.Count == 0, "Clicking the ring's spinner closes it");
            Tick(0.05);
            check(Math.Abs(pet.MoveScale - ringSim.Settings.CatRunScale * ringSim.MoveFactor) < 1e-9, "Then the cat runs at full speed again");
            window.EndBattle();
            // With loadingHold (the shipped 2 s) a press starts holding the spinner; the battle's ticks close it.
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0,
                Apps = new AppsSpec { InfectChance = 0, MalwareMinThreat = 0, MalwareEveryMin = 3, MalwareEveryMax = 3, LoadingWeight = 1, LoadingSeconds = 30, LoadingHold = 2 } };
            check(window.StartBattle(stage: new StageSpec { Id = "ui_loading_hold" }) == null, "(setup) a stage battle whose rings are held to close");
            for (int i = 0; i < 600 && Sim().Hazards.Count == 0; i++) Tick(1.0 / 60);
            var holdSim = Sim();
            var holdStrip = Field<BattleFieldView>(window, "battleView");
            check(holdSim.Hazards.Count == 1 && window.BattleCloseHazard(holdSim.Hazards[0].Id) && holdSim.Hazards.Count == 1 && holdSim.HoldingHazardId != 0,
                "Pressing the spinner starts holding it (the ring stays)");
            for (int i = 0; i < 130; i++) Tick(1.0 / 60);
            check(holdSim.Hazards.Count == 0 && holdSim.HoldingHazardId == 0, "Held 2 s, the ring closes");
            // The strip kept the mouse for that press; the ring is gone now: the next frame gives it back.
            holdStrip.CaptureMouse();
            Tick(1.0 / 60);
            check(!holdStrip.IsMouseCaptured, "Once the ring closed under the mouse, the strip gives the mouse back (later clicks reach the card offer)");
            holdStrip.CaptureMouse();
            holdStrip.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            check(!holdStrip.IsMouseCaptured, "Letting go after the ring closed gives the mouse back too");
            window.EndBattle();
            // virus.exe: each copy in its own window on its cell; a click deletes it and its window closes.
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0,
                Apps = new AppsSpec { InfectChance = 0, MalwareMinThreat = 0, MalwareEveryMin = 3, MalwareEveryMax = 3, VirusExeWeight = 1, VirusExeEvery = 30 } };
            check(window.StartBattle(stage: new StageSpec { Id = "ui_virus_exe" }) == null, "(setup) a stage battle that drops virus.exe");
            for (int i = 0; i < 600 && Sim().VirusFiles.Count == 0; i++) Tick(1.0 / 60);
            Tick(0.05);
            var exeSim = Sim();
            check(exeSim.VirusFiles.Count == 1 && window.BattleVirusWindows.Count == 1, "virus.exe opens a window of its own on the desktop grid");
            check(window.BattleDeleteVirusFile(exeSim.VirusFiles[0].Id), "Clicking virus.exe deletes it");
            Tick(0.05);
            check(exeSim.VirusFiles.Count == 0 && window.BattleVirusWindows.Count == 0, "Its window closes with it");
            window.EndBattle();

            // The strip: an ad's × and body (an ad placed on a lone slime).
            var data = BattleData.Settings;
            var appField = new BattleSim(data, 0, 600, stage: new StageSpec { Id = "apps_review" }, seed: 1);
            appField.SetAppGrid(7, 1);
            var behind = appField.AddSlime(470.0, data.EnemyTypes[0]);
            behind.Set(SlimeState.Moving); behind.StateTime = 0.4; behind.Facing = -1;
            var popupList = appField.popups;
            popupList.Add(new BattlePopup { Id = 99, X = 470, Age = 3, Life = 8 });
            var stripView = new BattleFieldView(appField) { OriginX = 0 };
            stripView.Measure(new Size(600, BattleFieldView.FieldHeight)); stripView.Arrange(new Rect(0, 0, 600, BattleFieldView.FieldHeight));
            double appFloor = BattleFieldView.FieldHeight - 2, popupTop = appFloor - 2 - BattleSim.PopupHeight;
            check(stripView.PopupCloseAt(new Point(470 + BattleSim.PopupWidth / 2 - 10, popupTop + 9)) == 99 && stripView.PopupCloseAt(new Point(470, appFloor - 20)) == null &&
                  stripView.OnPopup(new Point(470, appFloor - 20)) && stripView.SlimeAt(new Point(470, appFloor - 20)) == behind.Id,
                "An ad takes clicks only on its ×; the rest of it covers the slime behind (the click goes to neither)");
            // Malware's loading ring (2026-10-24): its spinner up in the strip takes the click.
            appField.hazards.Add(new BattleHazard { Id = 98, Kind = HazardKind.Loading, X = 200, Age = 1, Life = 6 });
            check(stripView.HazardAt(new Point(200, appFloor - 132)) == 98 && stripView.HazardAt(new Point(200, appFloor - 20)) == null,
                "A loading ring takes clicks on its spinner above the cat, not on the floor patch");
            appField.hazards.Add(new BattleHazard { Id = 97, Kind = HazardKind.Update, X = 330, Age = 3, Life = 8, ClicksLeft = 2 });
            check(stripView.HazardAt(new Point(330, appFloor - 15)) == 97 && stripView.HazardAt(new Point(330, appFloor - 60)) == null && stripView.OnWall(new Point(330, appFloor - 60)),
                "A forced update takes clicks on its Later button; the rest of it is a wall clicks do not pass");
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string appsReview)
            {
                void Save(Visual visual, int width, int height, string name)
                {
                    var image = new RenderTargetBitmap(width * 2, height * 2, 192, 192, PixelFormats.Pbgra32); image.Render(visual);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                    using var stream = System.IO.File.Create(System.IO.Path.Combine(appsReview, name)); encoder.Save(stream);
                }
                Visual OnBack(FrameworkElement element, double width, double height, Color back)
                {
                    element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height));
                    var visual = new DrawingVisual();
                    using (var dc = visual.RenderOpen())
                    {
                        dc.DrawRectangle(new SolidColorBrush(back), null, new Rect(0, 0, width, height));
                        dc.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top,
                            ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, width, height) }, null, new Rect(0, 0, width, height));
                    }
                    return visual;
                }
                // virus.exe as the desktop draws it.
                appField.virusFiles.Add(new VirusFile { Id = 96, SlimeId = behind.Id, Col = 0, Row = 0, BornAt = -5 });
                Save(OnBack(new VirusFileIcon(appField, 96), DesktopAppIcon.ViewWidth, DesktopAppIcon.ViewHeight, Color.FromRgb(0x3A, 0x6E, 0xA5)),
                    (int)DesktopAppIcon.ViewWidth, (int)DesktopAppIcon.ViewHeight, "battle-virus-exe.png");
                appField.virusFiles.Clear();
                // The icons in each state: charging, charged, infected (just scrubbed once), just used, hovered.
                var states = appField.Desktop.ToList();
                appField.Time = 10.0;
                states[0].Charge = 0.35;
                states[1].Charge = 1; states[1].ReadyAt = 9.8;
                states[2].InfectedBy = behind.Id; states[2].ScrubsLeft = 2; states[2].InfectedAt = 8; states[2].ScrubbedAt = 9.9;
                states[3].Charge = 0; states[3].UsedAt = 9.85;
                states[4].Charge = 1; states[4].ReadyAt = 2;
                foreach (var (name, back) in new[] { ("light", Colors.WhiteSmoke), ("dark", Color.FromRgb(0x1E, 0x1E, 0x24)) })
                {
                    var row = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                    foreach (var app in states)
                    {
                        var icon = new DesktopAppIcon(appField, app.Id);
                        if (app == states[4]) icon.Hovered = true;
                        row.Children.Add(icon);
                    }
                    Save(OnBack(row, DesktopAppIcon.ViewWidth * 5, DesktopAppIcon.ViewHeight, back), (int)DesktopAppIcon.ViewWidth * 5, (int)DesktopAppIcon.ViewHeight, $"battle-apps-icons-{name}.png");
                }
                // Flights halfway: tabs, bells, the bin's catch, a virus, a bad bell, an ad.
                var launchList = appField.launches;
                var kinds = new[] { (LaunchKind.Tabs, false, 120.0), (LaunchKind.Goo, true, 300.0), (LaunchKind.Trash, true, 380.0), (LaunchKind.BadBell, false, 200.0), (LaunchKind.Popup, false, 520.0), (LaunchKind.Freeze, false, 60.0) };
                int i = 0;
                foreach (var (kind, toIcon, x) in kinds)
                    launchList.Add(new AppLaunch { Id = 200 + i, Kind = kind, AppId = states[i++ % 5].Id, FloorX = x, Height = 40, Duration = 1, Age = 0.5, ToIcon = toIcon, SlimeId = behind.Id,
                        TargetX = new[] { x, x + 40 }, Targets = new[] { -1, -2 }, Colors = new[] { "#7CD36A", "#5CC8FF" } });
                foreach (var (name, back) in new[] { ("light", Colors.WhiteSmoke), ("dark", Color.FromRgb(0x1E, 0x1E, 0x24)) })
                {
                    var flights = new AppFlightView(appField)
                    {
                        IconCentre = id => new Point(60 + (appField.FindApp(id)?.Col ?? 0) * 80, 40),
                        FloorPoint = (x, h) => new Point(x, 300 - h)
                    };
                    Save(OnBack(flights, 600, 300, back), 600, 300, $"battle-apps-flights-{name}.png");
                }
                // The strip: the ad, the tornado around the cat, the virus mark, the effects.
                launchList.Clear();
                states[2].InfectedBy = behind.Id;
                appField.TabsRemaining = 3.0;
                appField.lastCatX = 200.0;
                foreach (var (effect, at, height) in new[] { (BattleEffectKind.Bell, 330.0, 60.0), (BattleEffectKind.EndTask, 560.0, 70.0), (BattleEffectKind.Rewind, 60.0, 60.0) })
                    appField.AddEffect(effect, at, height, 46.0);
                foreach (var e in appField.Effects) e.Age = e.Life * 0.4;
                var strip = new BattleFieldView(appField) { OriginX = 0 };
                foreach (var (name, back) in new[] { ("light", Colors.WhiteSmoke), ("dark", Color.FromRgb(0x1E, 0x1E, 0x24)) })
                    Save(OnBack(strip, 600, BattleFieldView.FieldHeight, back), 600, (int)BattleFieldView.FieldHeight, $"battle-apps-strip-{name}.png");
            }
        }

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
            // Combat Depth milestone 3: the shield phase's bubble, the HUD saying so, and the
            // BLOCK / IMMUNE marks, drawn in the real field (the sim's flags set by hand).
            string FieldShot(string label)
            {
                fieldView.InvalidateVisual(); fieldView.UpdateLayout();
                int fw = (int)fieldView.ActualWidth, fh = (int)fieldView.ActualHeight;
                var shot = new RenderTargetBitmap(fw, fh, 96, 96, PixelFormats.Pbgra32); shot.Render(fieldView);
                var crop = new CroppedBitmap(shot, new Int32Rect((int)Math.Clamp(bossSlime.X - fieldView.OriginX - 200, 0, Math.Max(0, fw - 400)), 0, Math.Min(400, fw), fh));
                var pixels = new byte[crop.PixelWidth * fh * 4]; crop.CopyPixels(pixels, crop.PixelWidth * 4, 0);
                if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review)
                {
                    var visual = new DrawingVisual();
                    using (var dc = visual.RenderOpen()) { dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x24)), null, new Rect(0, 0, 400, fh)); dc.DrawImage(crop, new Rect(0, 0, crop.PixelWidth, fh)); }
                    var image = new RenderTargetBitmap(400, fh, 96, 96, PixelFormats.Pbgra32); image.Render(visual);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                    using var stream = System.IO.File.Create(System.IO.Path.Combine(review, label + ".png")); encoder.Save(stream);
                }
                return Convert.ToBase64String(pixels);
            }
            Sim().Paused = true; // Hold everything still while the pictures are taken.
            string open = FieldShot("battle-boss-open");
            bossSlime.Shielded = true;
            string shielded = FieldShot("battle-boss-shielded");
            check(open != shielded, "A shielded boss is drawn inside a bubble");
            Call(window, "UpdateBattleHud");
            var bossLabel = Field<BattleHud>(window, "battleHud").bossName;
            check(bossLabel.Text.Contains("khiên") || bossLabel.Text.Contains("shield"), $"The HUD's boss bar says the shield is up (\"{bossLabel.Text}\")");
            Sim().AddEffect(BattleEffectKind.Immune, bossSlime.X, bossSlime.Height / 2, bossSlime.Width);
            Sim().AddEffect(BattleEffectKind.Block, bossSlime.X + 120, 30.0, 0.0);
            string marks = FieldShot("battle-block-immune");
            check(marks != shielded, "The IMMUNE and BLOCK marks are drawn");
            bossSlime.Shielded = false;
            Sim().Paused = false;
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
