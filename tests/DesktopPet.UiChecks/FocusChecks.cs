using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using static PetPrivate;

// The focus timer in the real pet window: what a session quiets, what the end may and may not do.
internal static class FocusChecks
{
    private static void Context(PetWindow window, bool fullscreen, double idle)
    {
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, idle)));
        Call(window, "TickContext", 1.1);
    }

    // The reward in the window, on a temporary field file: the real farm.json is never touched.
    private static void Bloom(PetWindow window, Action<bool, string> check, Action finish)
    {
        const FarmCrop bloom = FarmCrop.FocusBloom;
        string folder = Path.Combine(Path.GetTempPath(), "pet-focus-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "farm.json");
        SetField(window, "farmPath", path);
        var field = new PetFarm();
        SetField(window, "farm", field);
        try
        {
            field.Till(0);
            field.OffSeasonSeeds[bloom.ToString()] = 1;
            var seeds = window.FarmSeedItems(0);
            var item = seeds.Single(entry => ((string)entry.Header).StartsWith("Focus bloom"));
            check(seeds.Length == Enum.GetValues<FarmCrop>().Count(crop => field.InSeason(crop)) + 1 && item.IsEnabled, "With a seed the tile's menu offers the bloom");
            item.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            check(field.Tiles[0].Crop == bloom && field.OffSeasonOf(bloom) == 0, "Choosing it sows it and uses the seed");
            check(window.FarmTileLabel(0) == "Focus bloom · grows with Focus time", "Hovering says what it waits for");
            window.StartFocus(25);
            finish();
            check(field.Tiles[0].Stage == FarmStage.Sprout && field.OffSeasonOf(bloom) == 1 && PetFarm.Load(path).OffSeasonOf(bloom) == 1, "A finished session grows it, gives a seed and is saved at once");
            window.StopFocus();
            window.StartFocus(25);
            window.StopFocus();
            check(field.Tiles[0].Stage == FarmStage.Sprout && field.OffSeasonOf(bloom) == 1, "Stopping early gives nothing and takes nothing");
            field.Tiles[0].Stage = FarmStage.Young;
            field.Till(1);
            check(!window.FarmSeedItems(1).Any(entry => ((string)entry.Header).StartsWith("Focus bloom") && field.OffSeasonOf(bloom) == 0), "No seed, no entry");

            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review)
            {
                foreach (var (name, back) in new[] { ("light", Brushes.Beige), ("dark", new SolidColorBrush(Color.FromRgb(0x2E, 0x3A, 0x4A))) })
                {
                    var visual = new DrawingVisual();
                    using (var dc = visual.RenderOpen())
                    {
                        dc.DrawRectangle(back, null, new Rect(0, 0, 320, 110));
                        double x = 40;
                        foreach (var stage in new[] { FarmStage.Seed, FarmStage.Sprout, FarmStage.Young, FarmStage.Ripe })
                        {
                            dc.PushTransform(new TranslateTransform(x, 90));
                            PetFarmArt.DrawPlant(dc, stage, bloom);
                            dc.Pop();
                            x += 80;
                        }
                    }
                    var picture = new RenderTargetBitmap(320, 110, 96, 96, PixelFormats.Pbgra32);
                    picture.Render(visual);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(picture));
                    using var file = File.Create(Path.Combine(review, $"focus-bloom-{name}.png"));
                    encoder.Save(file);
                }
            }
        }
        finally
        {
            window.StopFocus();
            SetField(window, "farm", null);
            SetField(window, "farmPath", PetFarm.DefaultPath);
            Directory.Delete(folder, true);
        }
    }

    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences prefs, Action<bool, string> check)
    {
        var focus = Field<PetFocus>(window, "focus");
        var breaks = Field<PetBreaks>(window, "breaks");
        bool quietBefore = prefs.Quiet, remindersBefore = prefs.BreakReminders, speechBefore = prefs.Speech, soundsBefore = prefs.Sounds;
        int minutesBefore = prefs.BreakMinutes;
        void Free()
        {
            pet.Emotions.Reset(); behavior.Reset(); pet.Speech.Hide(); pet.Speech.DropPending();
            pet.Action = PetAction.Idle; pet.Grounded = true; pet.LedgeId = 0; pet.VX = pet.VY = 0;
        }
        void Finish() { var phase = focus.Phase; while (focus.Phase == phase) Call(window, "TickFocus", 25.0); }
        void Lines() { for (int i = 0; i < 60; i++) pet.Speech.Step(pet, 1); pet.Speech.Hide(); }
        prefs.Quiet = false; prefs.Speech = true; prefs.Sounds = true;
        Context(window, false, 0);
        Free();

        // Starting a session.
        window.SetBreakReminders(true);
        prefs.BreakMinutes = 30; breaks.Configure(true, 30);
        check(window.StartFocus(25) && focus.Focusing && behavior.Quiet && !prefs.Quiet, "A session quiets the cat without changing the saved Quiet setting");
        check(!window.StartFocus(50) && focus.Minutes == 25, "A running session cannot be replaced from the menu");
        Call(window, "Present", false);
        var label = Field<Window?>(window, "focusWindow");
        var text = Field<System.Windows.Controls.TextBlock?>(window, "focusText");
        check(label?.IsVisible == true && text?.Text == "25:00", "The label shows the time left");
        check(label != null && label.Title == "Pet focus timer" && label.ActualWidth < 140 && label.ActualHeight < 60, "The label is small");

        // Quiet during the session.
        var showSpeech = MethodOf("ShowSpeech");
        showSpeech.Invoke(window, new object[] { new DesktopPet.AI.PetLine("hello:vi:test", "Chào sen!") });
        check(!pet.Speech.Visible && pet.Speech.PendingId == null, "No chatter during a session");
        for (int i = 0; i < 3000; i++) Call(window, "TickContext", 1.1); // Well past the 30-minute interval.
        check(!breaks.Due && !pet.Speech.Visible, "No break reminder in the middle of a session");

        // Input still works and Pause stops the clock.
        double before = focus.Remaining;
        Call(window, "TickFocus", 0.0); // What Tick passes in Pause.
        check(focus.Remaining == before, "Pause freezes the clock");

        // A sleeping cat is not woken.
        pet.Action = PetAction.Sleeping;
        Finish();
        check(focus.Phase == FocusPhase.Resting && !behavior.Quiet && focus.LineWaiting == FocusEvent.Done, "The session ends and the cat is free again");
        for (int i = 0; i < 3; i++) Context(window, false, 0);
        check(pet.Action == PetAction.Sleeping && !pet.Speech.Visible && focus.LineWaiting == FocusEvent.Done, "A sleeping cat is not woken: the line waits");
        Call(window, "Present", false);
        check(text?.Text.StartsWith("Done") == true, "The label says it is done even while the cat sleeps");
        Free();
        check(breaks.ActiveSeconds < 5, "The break clock starts over after a session");
        Context(window, false, 0);
        check(pet.Speech.Visible && focus.LineWaiting == FocusEvent.None && PetFocus.IsFocusLine(pet.Speech.Id), "Awake, the cat says the line");
        check(pet.Action == PetAction.Stretching, "And stretches along with the user");
        Free(); Lines();

        // The end of the rest.
        Finish();
        check(!focus.Running && focus.LineWaiting == FocusEvent.RestOver, "The rest ends by itself");
        Context(window, false, 0);
        check(pet.Speech.Visible && focus.LineWaiting == FocusEvent.None && pet.Action != PetAction.Stretching, "The rest's line has no stretch");
        Call(window, "Present", false);
        check(label?.IsVisible == false, "The label goes when nothing runs");
        Free(); Lines();

        // A fullscreen app holds the news; chatter off does not.
        prefs.Speech = false;
        window.StartFocus(25);
        Context(window, true, 0);
        Call(window, "Present", false);
        check(label?.IsVisible == false, "A fullscreen app hides the label");
        Finish();
        Context(window, true, 0);
        check(focus.ChimeWaiting && focus.LineWaiting == FocusEvent.Done && !pet.Speech.Visible, "Over a fullscreen app the chime and the line wait");
        Context(window, false, 0);
        check(pet.Speech.Visible && focus.LineWaiting == FocusEvent.None, "They come after it closes, even with chatter off");
        Free(); Lines();
        window.StopFocus();
        check(!focus.Running && !behavior.Quiet, "Stopping ends it at once");

        // The menu.
        var menu = window.ContextMenu;
        menu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.ContextMenu.OpenedEvent));
        var root = menu.Items.OfType<System.Windows.Controls.MenuItem>().First(item => (string)item.Header == "Focus time");
        var items = root.Items.OfType<System.Windows.Controls.MenuItem>().ToList();
        check(items.Count == 4 && (string)items[0].Header == "Start 25 minutes" && items[0].IsEnabled && items[2].Visibility == Visibility.Collapsed && items[3].IsCheckable && items[3].IsChecked,
            "The menu offers 25 and 50 minutes and the reminders switch; Stop only when something runs");
        items[3].IsChecked = false;
        items[3].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
        check(!prefs.BreakReminders && !breaks.Enabled, "The menu switch turns the reminders off at once");

        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string folder)
        {
            foreach (var (name, setup) in new (string, Action)[] { ("focusing", () => { }), ("resting", () => Finish()) })
            {
                if (name == "focusing") window.StartFocus(25);
                setup();
                Call(window, "Present", false);
                if (label?.Content is FrameworkElement content)
                {
                    content.UpdateLayout();
                    var picture = new RenderTargetBitmap((int)content.ActualWidth + 20, (int)content.ActualHeight + 20, 96, 96, PixelFormats.Pbgra32);
                    picture.Render(content);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(picture));
                    Directory.CreateDirectory(folder);
                    using var file = File.Create(Path.Combine(folder, $"focus-label-{name}.png"));
                    encoder.Save(file);
                }
            }
            window.StopFocus();
        }

        Bloom(window, check, Finish);

        Free();
        prefs.Quiet = quietBefore; prefs.Speech = speechBefore; prefs.Sounds = soundsBefore; prefs.BreakMinutes = minutesBefore;
        window.SetBreakReminders(remindersBefore);
        breaks.Configure(prefs.BreakReminders, prefs.BreakMinutes);
        Call(window, "ApplyQuiet");
    }
}
