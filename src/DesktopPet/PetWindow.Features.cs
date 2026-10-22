using System;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopPet;

public sealed partial class PetWindow
{
    private readonly PetPreferences preferences;
    private Window? toyWindow, settingsWindow;
    private readonly ToyBall ball = new();
    private readonly ToyPlaySequence toyPlay = new();
    private readonly Random featureRandom = new();
    private double ballTime, treatCooldown, restVisit = 35, soundAfter;
    private double treatSatisfiedRemaining;
    private FrameworkElement? ballArt;
    private Ellipse? ballHalo;
    private PetEmotion lastSoundEmotion;
    private readonly System.Collections.Generic.Dictionary<string, double> soundCooldowns = new();
    private bool returningHome;
    private PetAction lastSoundAction;
    private SoundPlayer? sound;
    private MemoryStream? soundStream;

    private bool CanPlay => !paused && !dragging && !pressPending && PetActionRules.IsSettled(pet) && !pet.Emotions.BlocksAutonomy;

    private void InitializeFeatures(ContextMenu menu)
    {
        Title = preferences.Name + " — Desktop Pet";
        InitializeContext();
        InitializeLedges();
        ApplyQuiet();
        renderer.Coat = preferences.Coat;
        ApplyAccessory();
        behavior.Personality = preferences.Personality;
        // Menu groups: play with the cat · quick switches · the cat's pages · rarely used · exit.
        var treat = PetUi.MenuItem(PetUi.Treat);
        treat.Click += (_, _) => GiveTreat();
        var toy = PetUi.MenuItem(PetUi.Ball, Brushes.Coral);
        toy.Click += (_, _) => ToggleToy();
        var laser = PetUi.MenuItem(PetUi.Laser, Brushes.Red);
        laser.Click += (_, _) => StartLaser();
        var home = PetUi.MenuItem(PetUi.Home);
        var remember = new MenuItem();
        remember.Click += (_, _) => { preferences.RestX = pet.X; preferences.RestY = pet.Y;
            preferences.RestMonitor = CurrentMonitor().DeviceName;
            preferences.RestFractionX = Math.Clamp((pet.X - bounds.Left) / Math.Max(1, bounds.Right - bounds.Left - settings.Width), 0, 1);
            SavePreferences(); };
        var visit = new MenuItem();
        visit.Click += (_, _) => { if (CanPlay && !BattleOwnsPet && !EffectiveQuiet && preferences.RestX.HasValue) { CancelFeatureActivity(); returningHome = true; Interact("rest_requested"); } };
        var forget = new MenuItem();
        forget.Click += (_, _) => { preferences.RestX = preferences.RestY = null; preferences.RestMonitor = null; returningHome = false; SavePreferences(); };
        home.Items.Add(remember); home.Items.Add(visit); home.Items.Add(forget);
        var quiet = new MenuItem { IsCheckable = true, IsChecked = preferences.Quiet };
        quiet.Click += (_, _) => { preferences.Quiet = quiet.IsChecked; ApplyQuiet(); CancelFeatureActivity(); Interact("quiet_changed"); SavePreferences(); };
        var sounds = new MenuItem { IsCheckable = true, IsChecked = preferences.Sounds };
        sounds.Click += (_, _) => { preferences.Sounds = sounds.IsChecked; if (!preferences.Sounds) sound?.Stop(); SavePreferences(); };
        var customize = PetUi.MenuItem(PetUi.Settings);
        customize.Click += (_, _) => ShowPetSettings();
        var status = PetUi.MenuItem(PetUi.Status, PetUi.Accent);
        // Opened after the menu has closed, so the menu taking focus back cannot dismiss the card.
        status.Click += (_, _) => Dispatcher.BeginInvoke(ShowStatusCard, System.Windows.Threading.DispatcherPriority.Background);
        var diary = PetUi.MenuItem(PetUi.Diary);
        diary.Click += (_, _) => ShowDiary();
        // Rarely used: back to the primary screen, the debug view, About.
        var more = PetUi.MenuItem(PetUi.More);
        var reset = new MenuItem();
        reset.Click += (_, _) => Reset();
        var debug = PetUi.MenuItem(PetUi.Debug);
        debug.Click += (_, _) => ToggleDebug();
        var about = PetUi.MenuItem(PetUi.About);
        about.Click += (_, _) => ShowAbout();
        more.Items.Add(reset); more.Items.Add(debug); more.Items.Add(about);
        var exit = PetUi.MenuItem(PetUi.Exit);
        exit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(treat); menu.Items.Add(toy); menu.Items.Add(laser);
        InitializeBattle(menu); // "Battle Mode" (PetWindow.Battle.cs).
        InitializeCommands(menu); // "Do now" and "Emotions".
        menu.Items.Add(new Separator());
        menu.Items.Add(quiet); menu.Items.Add(sounds); menu.Items.Add(home);
        menu.Items.Add(new Separator());
        menu.Items.Add(status); menu.Items.Add(diary); menu.Items.Add(customize); menu.Items.Add(more);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        void Localize()
        {
            customize.Header = L("Cài đặt…", "Settings…");
            status.Header = L("Trạng thái của " + preferences.Name, preferences.Name + "'s status");
            diary.Header = L("Nhật ký của " + preferences.Name, preferences.Name + "'s diary");
            more.Header = L("Khác", "More");
            reset.Header = L("Gọi mèo về màn hình chính", "Bring the pet to the main screen");
            debug.Header = L("Xem debug hành vi", "Behavior debug view");
            exit.Header = L("Thoát mèo", "Exit pet");
            treat.Header = L("Cho ăn", "Give treat");
            toy.Header = toyWindow == null ? L("Chơi bóng", "Play with ball") : L("Cất bóng", "Put ball away");
            laser.Header = L("Chơi đèn laser", "Laser pointer");
            home.Header = L("Chỗ nghỉ yêu thích", "Favorite resting spot");
            remember.Header = L("Nhớ chỗ này", "Remember this spot");
            visit.Header = L("Đến chỗ nghỉ", "Go to resting spot");
            forget.Header = L("Quên chỗ nghỉ", "Forget resting spot");
            quiet.Header = L("Chế độ yên lặng", "Quiet mode");
            quiet.ToolTip = L("Mèo không tự đi lại hay tự làm gì; bạn vẫn cho ăn và chơi được.", "The pet stays put and does nothing on its own; you can still feed it and play.");
            sounds.Header = L("Âm thanh", "Sounds");
            about.Header = L("Giới thiệu Desktop Pet", "About Desktop Pet");
        }
        Localize();
        menu.Opened += (_, _) =>
        {
            Localize();
            sounds.IsChecked = preferences.Sounds;
            treat.IsEnabled = CanPlay && treatCooldown <= 0 && !BattleOwnsPet;
            // Quiet only stops what the cat does on its own; games the user starts still run.
            // A fullscreen app (game, video, slides) still keeps the cat still.
            toy.IsEnabled = toyWindow != null || (CanPlay && !context.Fullscreen && !BattleOwnsPet);
            laser.IsEnabled = CanPlay && !context.Fullscreen && !BattleOwnsPet; // Right-clicking the cat already ended a running game.
            remember.IsEnabled = pet.Grounded && pet.LedgeId == 0; // Resting spots are on the floor.
            visit.IsEnabled = CanPlay && !EffectiveQuiet && preferences.RestX.HasValue && !BattleOwnsPet;
            forget.IsEnabled = preferences.RestX.HasValue;
        };
    }

    private readonly StartupRegistration startup = new();

    public static string VersionText => PetLog.BuildVersion.Split('+')[0];

    // The About box also names the commit the published build came from (scripts/publish.ps1).
    private void ShowAbout() => PetDialog.Show(null, L("Giới thiệu Desktop Pet", "About Desktop Pet"),
        $"Desktop Pet {PetLog.VersionLabel(PetLog.BuildVersion, preferences.Language == "en")}\n\n{L("Dữ liệu và log", "Data and logs")}: {PetPreferences.DataDirectory}\n{AiStatusText}",
        L("Đóng", "Close"));

    private void SavePreferences()
    {
        if (persistPreferences && !preferences.Save()) PetDialog.Show(settingsWindow, "Desktop Pet", L("Cài đặt có hiệu lực trong phiên này nhưng không lưu được.", "The setting works for this session, but could not be saved."), "OK", glyph: PetDialog.WarningGlyph);
    }

    public bool GiveTreat()
    {
        if (!CanPlay || treatCooldown > 0 || BattleOwnsPet) return false;
        CancelFeatureActivity();
        pet.TreatRemaining = PetModel.TreatDuration;
        treatCooldown = 20;
        pet.Emotions.Attention();
        pet.Action = PetAction.Sitting;
        pet.VX = pet.SmoothedWalkVelocity = 0;
        Interact("treat");
        return true;
    }

    public void ToggleToy()
    {
        if (toyWindow != null) { CancelFeatureActivity(); return; }
        if (!CanPlay || context.Fullscreen || BattleOwnsPet) return; // Allowed in Quiet mode: the user asked for it.
        CancelFeatureActivity();
        Interact("toy_started");
        toyPlay.Reset();
        ball.X = Math.Clamp(pet.X + 145, bounds.Left + 12, Math.Max(bounds.Left + 12, bounds.Right - 12));
        ball.Height = 0; ball.VX = 45; ball.VY = 100; ball.BatCooldown = 0; ballTime = 45;
        var surface = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), Cursor = System.Windows.Input.Cursors.Hand };
        ballHalo = new Ellipse { Width = 36, Height = 36, Stroke = YarnBallArt.Halo, StrokeThickness = 2, Opacity = 0 };
        var art = new YarnBallArt { Width = 24, Height = 24, RenderTransformOrigin = new Point(0.5, 0.5) };
        surface.Children.Add(ballHalo); surface.Children.Add(art); ballArt = art;
        toyWindow = CreateOverlayWindow(surface, "Pet toy");
        toyWindow.Width = 56; toyWindow.Height = 56; toyWindow.Left = ball.X - 28; toyWindow.Top = bounds.Bottom - 40;
        toyWindow.MouseLeftButtonDown += (_, e) =>
        {
            if (paused || !CanPlay) return;
            ball.Push(e.GetPosition(surface).X - 28, 0.9 + featureRandom.NextDouble() * 0.2);
            pet.Emotions.Attention();
            pet.Facing = ball.X < pet.X + PetSettings.CenterX ? -1 : 1;
            Interact("ball_clicked"); PlayPetSound("ball_tap"); e.Handled = true;
        };
        toyWindow.Closed += (_, _) => toyWindow = null;
        toyWindow.Show();
        PlayPetSound("excited");
    }

    private void CancelFeatureActivity()
    {
        StopLaser();
        toyWindow?.Close();
        returningHome = false;
        admiring = false; admireRemaining = 0;
        shelfShove = ShelfShove.None; shelfWatch = 0; pet.Bonk = 0; cushionNap = CushionNap.None; userAsked &= ~UserAsked.Nap;
        climbPlan = null; ledgeExit = 0;
        PetPhysics.LetGo(pet); // A climbing pet lets go and drops.
        pet.TreatRemaining = 0;
        treatSatisfiedRemaining = 0;
        pet.BatRemaining = pet.PlayCrouch = 0;
        toyPlay.Reset();
        if (PetActionRules.IsCancellableLocal(pet.Action)) pet.Action = PetAction.Idle;
        behavior.Reset(pet);
    }

    // Also lets a walk carry the pet past a window ledge's end (it drops off).
    private bool FeatureOwnsMovement() => CanPlay && (pet.TreatRemaining > 0 || treatSatisfiedRemaining > 0 || toyWindow != null || returningHome || admiring ||
        laserWindow != null || climbPlan != null || ledgeExit != 0 || shelfShove != ShelfShove.None || shelfWatch > 0 || pet.Bonk > 0 || cushionNap == CushionNap.Walk);

    private void TickFeatures(double dt)
    {
        treatCooldown = Math.Max(0, treatCooldown - dt);
        pet.BatRemaining = Math.Max(0, pet.BatRemaining - dt);
        if (lastSoundEmotion != pet.Emotions.Current)
        {
            _ = pet.Emotions.Current switch
            {
                PetEmotion.Angry => PlayPetSound("annoyed"),
                PetEmotion.Surprised => PlayPetSound("surprised"),
                _ => true
            };
            // Transition cues expire immediately when muted, busy or cooling down.
            lastSoundEmotion = pet.Emotions.Current;
        }
        if (lastSoundAction != pet.Action)
        {
            if (pet.Action == PetAction.Sleeping) PlayPetSound("sleep");
            if (pet.Action == PetAction.Waking) PlayPetSound("wake");
            if (pet.Action == PetAction.SeekingAttention) PlayPetSound("meow");
            lastSoundAction = pet.Action;
        }
        if (BattleActive) return; // The battle owns the pet; games, treats and rest visits wait.
        if (TickLaser(dt)) return;
        if (!CanPlay)
        {
            if (toyWindow != null || returningHome || admiring || shelfShove != ShelfShove.None || cushionNap == CushionNap.Walk || pet.TreatRemaining > 0 || treatSatisfiedRemaining > 0) CancelFeatureActivity();
            return;
        }
        if (pet.TreatRemaining > 0)
        {
            pet.TreatRemaining = Math.Max(0, pet.TreatRemaining - dt);
            pet.VX = 0; pet.Action = PetAction.Sitting;
            if (pet.TreatRemaining == 0)
            {
                pet.Activity.GiveTreat();
                treatSatisfiedRemaining = 1.2;
                pet.Emotions.Attention();
                Interact("treat_consumed"); PlayPetSound("satisfied"); RequestDecision();
            }
            else if (pet.TreatRemaining < 2.7 && pet.TreatRemaining + dt >= 2.7) PlayPetSound("purr");
            return;
        }
        if (treatSatisfiedRemaining > 0)
        {
            treatSatisfiedRemaining = Math.Max(0, treatSatisfiedRemaining - dt);
            pet.Action = PetAction.Sitting; pet.VX = 0;
            return;
        }
        if (context.Fullscreen) return;
        if (toyWindow != null)
        {
            ballTime -= dt;
            if (ballTime <= 0) { CancelFeatureActivity(); return; }
            ball.Step(dt, bounds.Left + 12, Math.Max(bounds.Left + 12, bounds.Right - 12));
            ShelfMeetsBall(); // The keepsake on display is in the way (PetWindow.TrophyPlay.cs).
            double delta = ball.X - (pet.X + PetSettings.CenterX);
            // Only from the floor: the ball never rolls on a window's edge.
            if (toyPlay.Step(dt, Math.Abs(delta) < 49 && ball.Height < 24 && ball.BatCooldown <= 0 && pet.LedgeId == 0, preferences.Personality))
            {
                pet.Facing = delta < 0 ? -1 : 1;
                ball.Bat(pet.Facing, 0.9 + featureRandom.NextDouble() * 0.2); pet.Interactions.BallHit();
                pet.BatRemaining = 0.3; PlayPetSound("ball_tap");
            }
            pet.PlayCrouch = toyPlay.CrouchBlend;
            if (toyPlay.Phase != ToyPlayPhase.Chase)
            {
                pet.Facing = delta < 0 ? -1 : 1;
                pet.Action = toyPlay.Phase == ToyPlayPhase.Observe ? PetAction.Looking : PetAction.Sitting;
                pet.VX = pet.SmoothedWalkVelocity = 0;
            }
            else
            {
                double target = Math.Clamp(ball.X + ball.VX * 0.22 - 56, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
                if (Math.Abs(target - pet.X) < 12) { pet.Action = PetAction.Idle; pet.VX = 0; }
                else WalkToward(target);
            }
            MoveWindow(toyWindow, ball.X - 28, bounds.Bottom - 40 - ball.Height); // One move, not Left then Top.
            if (ballArt != null) ballArt.RenderTransform = new RotateTransform(ball.Rotation);
            if (ballHalo != null) ballHalo.Opacity = Math.Max(ball.Feedback / 0.35, toyWindow.IsMouseOver ? 0.45 : 0);
            return;
        }
        // Walking to the resting spot or the keepsakes on its own is autonomous; a look the user
        // asked for (double click on the keepsake) still happens in Quiet.
        // Walking to the cushion the user asked for ("Do now") also goes on in Quiet.
        if (EffectiveQuiet) { StopShelfShove(); if (!Asked(UserAsked.Nap)) StopCushionNap(); if (!Asked(UserAsked.Admire)) StopAdmiring(); }
        if (TickAdmiring(dt)) return; // PetWindow.Trophies.cs
        if (TickCushionNap(dt)) return; // Napping on the slime cushion (PetWindow.TrophyPlay.cs).
        if (EffectiveQuiet) return;
        if (TickShelfShove(dt)) return; // Shoving the keepsake shelf along (PetWindow.Trophies.cs).
        restVisit -= dt;
        if (restVisit <= 0 && preferences.RestX.HasValue && pet.Action is PetAction.Idle or PetAction.Sitting && climbPlan == null && ledgeExit == 0)
        { returningHome = true; restVisit = 45; }
        if (returningHome && preferences.RestX.HasValue)
        {
            // Stay on the current monitor; do not teleport through other windows/screens.
            if (preferences.RestY < bounds.Top || preferences.RestY >= bounds.Bottom || preferences.RestX < bounds.Left || preferences.RestX >= bounds.Right)
            { returningHome = false; return; }
            double target = Math.Clamp(preferences.RestX.Value, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
            if (OnLedge && Math.Abs(target - pet.X) < 8)
            {
                // Right above the spot on a window: walk off the nearer end first.
                double center = pet.X + settings.Width / 2;
                if (PetLedges.Support(ledges, pet.LedgeId, center) is Ledge here)
                    WalkToward(center - here.Left < here.Right - center ? here.Left - settings.Width : here.Right);
            }
            else if (Math.Abs(target - pet.X) < 8) { returningHome = false; pet.Action = PetAction.Sitting; pet.VX = 0; behavior.Reset(); }
            else WalkToward(target);
        }
    }

    private readonly System.Collections.Generic.Queue<double> recentPetting = new();
    private double kneadReady;

    // Two strokes within 10 s mean sustained petting: the pet sits down and kneads.
    private void MaybeKnead()
    {
        double now = clock.Elapsed.TotalSeconds;
        recentPetting.Enqueue(now);
        while (recentPetting.Count > 0 && now - recentPetting.Peek() > 10) recentPetting.Dequeue();
        if (recentPetting.Count < 2 || now < kneadReady || !CanPlay || FeatureOwnsMovement() || BattleActive) return;
        if (!behavior.StartKneading(pet)) return;
        kneadReady = now + 20;
        recentPetting.Clear();
    }

    private void WalkToward(double target)
    {
        pet.Facing = target < pet.X ? -1 : 1;
        pet.VX = pet.Facing * settings.WalkSpeed * pet.Emotions.WalkScale * pet.MoveScale;
        pet.Action = PetAction.Walking;
    }

    private bool PlayPetSound(string kind, bool preview = false, double? previewVolume = null)
    {
        bool category = kind is "sleep" or "wake" ? preferences.SleepSounds : kind is "annoyed" or "surprised" ? preferences.EmotionSounds : preferences.InteractionSounds;
        // A fullscreen app (game, video, slides) gets no pet sounds; previews still play.
        if (!preview && (context.Fullscreen || !preferences.Sounds || !category || paused || clock.Elapsed.TotalSeconds < soundAfter)) return false;
        double volume = Math.Clamp(previewVolume ?? preferences.Volume, 0, 1);
        if (volume <= 0) { if (preview) sound?.Stop(); return false; }
        double now = clock.Elapsed.TotalSeconds;
        if (!preview && soundCooldowns.TryGetValue(kind, out double until) && now < until) return false;
        soundAfter = now + 0.7;
        // Battle foley is short and frequent (a swing every ~0.75 s): its own short cooldown.
        bool foley = PetSounds.IsBattleEffect(kind);
        soundCooldowns[kind] = now + (PetSounds.IsEliteCue(kind) ? 1 : foley ? 0.25 : kind == "ball_tap" ? 0.7 : kind is "purr" or "sleep" ? 6 : 3.5);
        try
        {
            sound?.Stop(); sound?.Dispose(); soundStream?.Dispose();
            byte[] recording = PetSounds.CreateWave(kind);
            for (int i = 44; i + 1 < recording.Length; i += 2)
            {
                short sample = (short)(BitConverter.ToInt16(recording, i) * volume);
                recording[i] = (byte)sample; recording[i + 1] = (byte)(sample >> 8);
            }
            // Let the cartoon cue finish before another cue can replace it.
            soundAfter = now + (recording.Length - 44) / 44100.0 + (foley ? 0.02 : 0.15);
            soundStream = new MemoryStream(recording);
            soundStream.Position = 0; sound = new SoundPlayer(soundStream); sound.Play();
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.TimeoutException) { soundAfter = now + 10; return false; }
    }

    // Today's special day from Python (ai/calendar.json), or null.
    private string? specialDay;

    // The chosen accessory, or on a special day (with "Dress up for holidays" on) the one made for it.
    // Saved preferences never change; the settings preview shows the user's own choice.
    private void ApplyAccessory() =>
        renderer.Accessory = preferences.HolidayAccessories && PetAccessories.ForEvent(specialDay) is { } outfit ? outfit.Id : preferences.Accessory;

    private void CloseFeatures() { CloseTrophies(); EndBattle(); CloseBattleHub(); CloseBattlePrep(); CloseAdventureMap(); CloseExpeditionMap(); CloseBattleBag(); CloseBattleSummary(); CloseBattleNotice(); CloseBattleTutorial(); laserWindow?.Close(); diaryWindow?.Close(); CloseStatusCard(); behaviorSettingsWindow?.Close(); toyWindow?.Close(); settingsWindow?.Close(); speechWindow?.Close(); sound?.Stop(); sound?.Dispose(); soundStream?.Dispose(); }
}
