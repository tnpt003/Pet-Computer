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

    private bool CanPlay => !paused && !dragging && !pressPending && pet.Grounded &&
        !pet.Emotions.SleepLocked && !pet.Emotions.BlocksAutonomy &&
        pet.Action is not (PetAction.Sleeping or PetAction.Waking or PetAction.Thrown or PetAction.Jumping or PetAction.PreparingJump);

    private void InitializeFeatures(ContextMenu menu)
    {
        Title = preferences.Name + " — Desktop Pet";
        behavior.Quiet = preferences.Quiet;
        behavior.Personality = preferences.Personality;
        var treat = new MenuItem { Header = "Give treat" };
        treat.Click += (_, _) => GiveTreat();
        var toy = new MenuItem { Header = "Play with ball / put away" };
        toy.Click += (_, _) => ToggleToy();
        var home = new MenuItem { Header = "Favorite resting spot" };
        var remember = new MenuItem { Header = "Remember this spot" };
        remember.Click += (_, _) => { preferences.RestX = pet.X; preferences.RestY = pet.Y;
            preferences.RestMonitor = CurrentMonitor().DeviceName;
            preferences.RestFractionX = Math.Clamp((pet.X - bounds.Left) / Math.Max(1, bounds.Right - bounds.Left - settings.Width), 0, 1);
            SavePreferences(); };
        var visit = new MenuItem { Header = "Go to resting spot" };
        visit.Click += (_, _) => { if (CanPlay && !preferences.Quiet && preferences.RestX.HasValue) { CancelFeatureActivity(); returningHome = true; Interact("rest_requested"); } };
        var forget = new MenuItem { Header = "Forget resting spot" };
        forget.Click += (_, _) => { preferences.RestX = preferences.RestY = null; preferences.RestMonitor = null; returningHome = false; SavePreferences(); };
        home.Items.Add(remember); home.Items.Add(visit); home.Items.Add(forget);
        var quiet = new MenuItem { Header = "Quiet mode", IsCheckable = true, IsChecked = preferences.Quiet };
        quiet.Click += (_, _) => { preferences.Quiet = quiet.IsChecked; behavior.Quiet = preferences.Quiet; CancelFeatureActivity(); Interact("quiet_changed"); SavePreferences(); };
        var sounds = new MenuItem { Header = "Pet sounds", IsCheckable = true, IsChecked = preferences.Sounds };
        sounds.Click += (_, _) => { preferences.Sounds = sounds.IsChecked; if (!preferences.Sounds) sound?.Stop(); SavePreferences(); };
        var customize = new MenuItem { Header = "Name and personality…" };
        customize.Click += (_, _) => ShowPetSettings();
        menu.Items.Insert(0, customize); menu.Items.Insert(1, treat); menu.Items.Insert(2, toy);
        menu.Items.Insert(3, home); menu.Items.Insert(4, quiet); menu.Items.Insert(5, sounds);
        // Off unless the user enables it; the registry value is read fresh each time.
        var autostart = new MenuItem { Header = "Start with Windows", IsCheckable = true };
        autostart.Click += (_, _) =>
        {
            if (Environment.ProcessPath is not string exe || !startup.SetEnabled(autostart.IsChecked, exe))
                MessageBox.Show("Windows startup could not be changed.", "Desktop Pet");
        };
        var about = new MenuItem { Header = "About Desktop Pet" };
        about.Click += (_, _) => ShowAbout();
        menu.Items.Insert(6, autostart);
        menu.Items.Insert(menu.Items.Count - 1, about); // Just above Exit.
        menu.Opened += (_, _) =>
        {
            autostart.IsChecked = Environment.ProcessPath is string exe && startup.IsEnabled(exe);
            customize.Header = preferences.Name + " — name and personality…";
            sounds.IsChecked = preferences.Sounds;
            toy.Header = toyWindow == null ? "Play with ball" : "Put ball away";
            treat.IsEnabled = CanPlay && treatCooldown <= 0;
            toy.IsEnabled = toyWindow != null || (CanPlay && !preferences.Quiet);
            remember.IsEnabled = pet.Grounded;
            visit.IsEnabled = CanPlay && !preferences.Quiet && preferences.RestX.HasValue;
            forget.IsEnabled = preferences.RestX.HasValue;
        };
    }

    private readonly StartupRegistration startup = new();

    public static string VersionText =>
        (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(PetWindow).Assembly)
            ?.InformationalVersion ?? "unknown").Split('+')[0];

    private void ShowAbout() => MessageBox.Show(
        $"Desktop Pet {VersionText}\n\nData and logs: {PetPreferences.DataDirectory}\n{AiStatus}",
        "About Desktop Pet");

    private void SavePreferences()
    {
        if (persistPreferences && !preferences.Save()) MessageBox.Show("The setting works for this session, but could not be saved.", "Desktop Pet");
    }

    public bool GiveTreat()
    {
        if (!CanPlay || treatCooldown > 0) return false;
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
        if (!CanPlay || preferences.Quiet) return;
        CancelFeatureActivity();
        Interact("toy_started");
        toyPlay.Reset();
        ball.X = Math.Clamp(pet.X + 145, bounds.Left + 12, Math.Max(bounds.Left + 12, bounds.Right - 12));
        ball.Height = 0; ball.VX = 45; ball.VY = 100; ball.BatCooldown = 0; ballTime = 45;
        var surface = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), Cursor = System.Windows.Input.Cursors.Hand };
        ballHalo = new Ellipse { Width = 36, Height = 36, Stroke = Brushes.White, StrokeThickness = 2, Opacity = 0 };
        var art = new Grid { Width = 24, Height = 24, RenderTransformOrigin = new Point(0.5, 0.5) };
        art.Children.Add(new Ellipse { Fill = Brushes.Coral, Stroke = Brushes.White, StrokeThickness = 2,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Color = Colors.Black } });
        art.Children.Add(new Ellipse { Width = 8, Height = 20, Stroke = Brushes.DarkSlateBlue, StrokeThickness = 2 });
        surface.Children.Add(ballHalo); surface.Children.Add(art); ballArt = art;
        toyWindow = new Window { Width = 56, Height = 56, WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, AllowsTransparency = true, Background = Brushes.Transparent,
            ShowInTaskbar = false, ShowActivated = false, Topmost = true, Content = surface,
            Left = ball.X - 28, Top = bounds.Bottom - 40 };
        toyWindow.SourceInitialized += (_, _) =>
            ((System.Windows.Interop.HwndSource)PresentationSource.FromVisual(toyWindow)).AddHook(WindowHook);
        toyWindow.MouseLeftButtonDown += (_, e) =>
        {
            if (paused || !CanPlay) return;
            ball.Push(e.GetPosition(surface).X - 28, 0.9 + featureRandom.NextDouble() * 0.2);
            pet.Emotions.Attention();
            pet.Facing = ball.X < pet.X + 56 ? -1 : 1;
            Interact("ball_clicked"); PlayPetSound("ball_tap"); e.Handled = true;
        };
        toyWindow.Closed += (_, _) => toyWindow = null;
        toyWindow.Show();
        PlayPetSound("excited");
    }

    private void CancelFeatureActivity()
    {
        toyWindow?.Close();
        returningHome = false;
        pet.TreatRemaining = 0;
        treatSatisfiedRemaining = 0;
        pet.BatRemaining = pet.PlayCrouch = 0;
        toyPlay.Reset();
        if (PetActionRules.IsCancellableLocal(pet.Action)) pet.Action = PetAction.Idle;
        behavior.Reset();
    }

    private bool FeatureOwnsMovement() => CanPlay && (pet.TreatRemaining > 0 || treatSatisfiedRemaining > 0 || toyWindow != null || returningHome);

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
        if (!CanPlay)
        {
            if (toyWindow != null || returningHome || pet.TreatRemaining > 0 || treatSatisfiedRemaining > 0) CancelFeatureActivity();
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
        if (preferences.Quiet) return;
        if (toyWindow != null)
        {
            ballTime -= dt;
            if (ballTime <= 0) { CancelFeatureActivity(); return; }
            ball.Step(dt, bounds.Left + 12, Math.Max(bounds.Left + 12, bounds.Right - 12));
            double delta = ball.X - (pet.X + 56);
            if (toyPlay.Step(dt, Math.Abs(delta) < 49 && ball.Height < 24 && ball.BatCooldown <= 0, preferences.Personality))
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
            toyWindow.Left = ball.X - 28; toyWindow.Top = bounds.Bottom - 40 - ball.Height;
            if (ballArt != null) ballArt.RenderTransform = new RotateTransform(ball.Rotation);
            if (ballHalo != null) ballHalo.Opacity = Math.Max(ball.Feedback / 0.35, toyWindow.IsMouseOver ? 0.45 : 0);
            return;
        }
        restVisit -= dt;
        if (restVisit <= 0 && preferences.RestX.HasValue && pet.Action is PetAction.Idle or PetAction.Sitting)
        { returningHome = true; restVisit = 45; }
        if (returningHome && preferences.RestX.HasValue)
        {
            // Stay on the current monitor; do not teleport through other windows/screens.
            if (preferences.RestY < bounds.Top || preferences.RestY >= bounds.Bottom || preferences.RestX < bounds.Left || preferences.RestX >= bounds.Right)
            { returningHome = false; return; }
            double target = Math.Clamp(preferences.RestX.Value, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
            if (Math.Abs(target - pet.X) < 8) { returningHome = false; pet.Action = PetAction.Sitting; pet.VX = 0; behavior.Reset(); }
            else WalkToward(target);
        }
    }

    private void WalkToward(double target)
    {
        pet.Facing = target < pet.X ? -1 : 1;
        pet.VX = pet.Facing * settings.WalkSpeed * pet.Emotions.WalkScale;
        pet.Action = PetAction.Walking;
    }

    private void ShowPetSettings()
    {
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        var panel = new StackPanel { Margin = new Thickness(16) };
        var name = new TextBox { Text = preferences.Name, MaxLength = 24, Margin = new Thickness(0, 4, 0, 12) };
        var personality = new ComboBox { ItemsSource = new[] { "Balanced", "Playful", "Curious", "Calm" }, SelectedItem = preferences.Personality };
        var save = new Button { Content = "Save", Margin = new Thickness(0, 16, 0, 0) };
        panel.Children.Add(new TextBlock { Text = "Pet name" }); panel.Children.Add(name);
        panel.Children.Add(new TextBlock { Text = "Personality (Python AI)" }); panel.Children.Add(personality);
        var tuning = new Button { Content = "Behavior tuning...", Margin = new Thickness(0, 6, 0, 0) };
        tuning.Click += (_, _) => ShowBehaviorSettings(); panel.Children.Add(tuning);
        // Speech bubbles (needs Python AI): language, chattiness and forgetting learned habits.
        var talks = new CheckBox { Content = "Pet talks (speech bubbles)", IsChecked = preferences.Speech, Margin = new Thickness(0, 4, 0, 4) };
        var language = new ComboBox { ItemsSource = new[] { "Tiếng Việt", "English" }, SelectedIndex = preferences.Language == "en" ? 1 : 0, Margin = new Thickness(0, 2, 0, 4) };
        var chattiness = new ComboBox { ItemsSource = new[] { "Rarely — events only", "Sometimes", "Often" }, SelectedIndex = preferences.Chattiness };
        var forget = new Button { Content = "Forget what the pet learned…", Margin = new Thickness(0, 8, 0, 2) };
        forget.Click += (_, _) =>
        {
            if (MessageBox.Show("The pet will forget which actions and lines you liked. Needs and bond are kept.",
                    "Forget learned habits", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            preferences.LearningReset = Guid.NewGuid().ToString("N"); // Python applies it on its next decision.
            SavePreferences();
        };
        var speechPanel = new StackPanel { Margin = new Thickness(6) };
        speechPanel.Children.Add(talks); speechPanel.Children.Add(new TextBlock { Text = "Language" }); speechPanel.Children.Add(language);
        speechPanel.Children.Add(new TextBlock { Text = "How often" }); speechPanel.Children.Add(chattiness); speechPanel.Children.Add(forget);
        panel.Children.Add(new GroupBox { Header = "Speech (Python AI)", Content = speechPanel, Margin = new Thickness(0, 10, 0, 0) });
        var master = new CheckBox { Content = "Enable sounds", IsChecked = preferences.Sounds, Margin = new Thickness(0, 12, 0, 4) };
        var interaction = new CheckBox { Content = "Interaction sounds", IsChecked = preferences.InteractionSounds };
        var emotion = new CheckBox { Content = "Emotion sounds", IsChecked = preferences.EmotionSounds };
        var sleep = new CheckBox { Content = "Sleep sounds", IsChecked = preferences.SleepSounds };
        var volume = new Slider { Minimum = 0, Maximum = 1, Value = preferences.Volume, TickFrequency = 0.1, Margin = new Thickness(0, 6, 0, 10) };
        panel.Children.Add(master); panel.Children.Add(interaction); panel.Children.Add(emotion); panel.Children.Add(sleep);
        panel.Children.Add(new TextBlock { Text = "Volume" }); panel.Children.Add(volume);
        panel.Children.Add(new TextBlock { Text = "Preview (also works while muted)" });
        var previews = new WrapPanel();
        foreach (string cue in new[] { "meow", "purr", "excited", "surprised", "annoyed", "sleep", "ball_tap", "satisfied", "wake" })
        {
            var button = new Button { Content = cue, Margin = new Thickness(2), Padding = new Thickness(6, 3, 6, 3) };
            button.Click += (_, _) => PlayPetSound(cue, true, volume.Value); previews.Children.Add(button);
        }
        panel.Children.Add(previews); panel.Children.Add(save);
        settingsWindow = new Window { Title = "Customize pet", Icon = AppIcon.WindowImage(), Width = 380, Height = 760,
            Content = panel, ResizeMode = ResizeMode.NoResize, Topmost = true, ShowInTaskbar = false };
        save.Click += (_, _) => { preferences.Name = string.IsNullOrWhiteSpace(name.Text) ? "Mochi" : name.Text.Trim();
            preferences.Personality = personality.SelectedItem as string ?? "Balanced"; Title = preferences.Name + " — Desktop Pet";
            behavior.Personality = preferences.Personality;
            preferences.Volume = volume.Value; preferences.Sounds = master.IsChecked == true;
            preferences.InteractionSounds = interaction.IsChecked == true; preferences.EmotionSounds = emotion.IsChecked == true; preferences.SleepSounds = sleep.IsChecked == true;
            preferences.Speech = talks.IsChecked == true; preferences.Language = language.SelectedIndex == 1 ? "en" : "vi";
            preferences.Chattiness = Math.Clamp(chattiness.SelectedIndex, 0, 2);
            if (!preferences.Speech) pet.Speech.Hide();
            sound?.Stop(); Interact("preferences_changed"); SavePreferences(); settingsWindow?.Close(); };
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
    }

    private bool PlayPetSound(string kind, bool preview = false, double? previewVolume = null)
    {
        bool category = kind is "sleep" or "wake" ? preferences.SleepSounds : kind is "annoyed" or "surprised" ? preferences.EmotionSounds : preferences.InteractionSounds;
        if (!preview && (!preferences.Sounds || !category || paused || clock.Elapsed.TotalSeconds < soundAfter)) return false;
        double volume = Math.Clamp(previewVolume ?? preferences.Volume, 0, 1);
        if (volume <= 0) { if (preview) sound?.Stop(); return false; }
        double now = clock.Elapsed.TotalSeconds;
        if (!preview && soundCooldowns.TryGetValue(kind, out double until) && now < until) return false;
        soundAfter = now + 0.7;
        soundCooldowns[kind] = now + (kind == "ball_tap" ? 0.7 : kind is "purr" or "sleep" ? 6 : 3.5);
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
            soundAfter = now + (recording.Length - 44) / 44100.0 + 0.15;
            soundStream = new MemoryStream(recording);
            soundStream.Position = 0; sound = new SoundPlayer(soundStream); sound.Play();
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.TimeoutException) { soundAfter = now + 10; return false; }
    }

    private void CloseFeatures() { behaviorSettingsWindow?.Close(); toyWindow?.Close(); settingsWindow?.Close(); speechWindow?.Close(); sound?.Stop(); sound?.Dispose(); soundStream?.Dispose(); }
}
