using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopPet;
using static PetPrivate;

// Adventure milestone 2 in the real pet window: menu → Adventure → map → stage setup → battle →
// result → progress saved → next stage open; loss, cancel, ending a run, custom data, closing and
// reopening (the progress read again from its file). A temporary progress file only.
internal static class AdventureChecks
{
    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, idle)));
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    private static Window? Find(string title) => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == title && w.IsLoaded);
    private static int Count(string title) => Application.Current.Windows.OfType<Window>().Count(w => w.Title == title && w.IsLoaded);
    private static string Text(DependencyObject node) => node is TextBlock t ? t.Text + "|" : string.Concat(LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().Select(Text));
    private static string ResultText() => Find("Pet result")?.Content is DependencyObject content ? Text(content) : "";
    // A panel button (a Border holding its label) found by its label.
    private static Border? Button(DependencyObject node, string label)
    {
        if (node is Border { Child: StackPanel panel } border && panel.Children.OfType<TextBlock>().Any(t => t.Text == label)) return border;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            if (Button(child, label) is Border found) return found;
        return null;
    }
    // PET_RENDER_REVIEW: a picture of a panel, to look at its layout.
    private static void Shot(string title, string name)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not string review || Find(title)?.Content is not FrameworkElement content) return;
        content.UpdateLayout();
        var shot = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth + 12), (int)Math.Ceiling(content.ActualHeight + 12), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        shot.Render(content);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));
        using var stream = File.Create(Path.Combine(review, name)); encoder.Save(stream);
    }
    private static void Click(UIElement element) =>
        element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });

    public static async Task Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences prefs, Action<bool, string> check)
    {
        void Tick(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) { Call(window, "TickBattlePrep", 1.0 / 60); Call(window, "TickAdventureMap", 1.0 / 60); } }
        void Fight(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattle", 1.0 / 60); }
        // Brings the stage's one slime next to the cat and fights until the battle is over.
        void Finish()
        {
            Fight(0.6);
            if (window.Battle is { } b && b.Slimes.Count > 0) b.Slimes[0].X = pet.X + 56 + 50;
            for (int i = 0; i < 600 && window.BattleActive; i++) Fight(0.05);
        }
        string language = prefs.Language, weapon = prefs.BattleWeapon, level = prefs.BattleDifficulty;
        var tuningBefore = window.BattleTuning;
        prefs.Language = "en"; prefs.BattleWeapon = BattleWeapon.Sword; prefs.BattleDifficulty = "Normal";
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        window.Reset();
        WaveSpec[] OneSlime() => new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 } } } };
        StageSpec Stage(string id, string name) => new() { Id = id, NameVi = name, NameEn = name, GoalVi = "Mục tiêu " + name, GoalEn = "Goal " + name, Waves = OneSlime() };
        var campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "test", NameVi = "Chương thử", NameEn = "Test chapter",
            Stages = new[] { Stage("st_one", "Meadow"), Stage("st_two", "Bridge"), Stage("st_three", "Castle") } } } };
        // One weak slime per stage (a quick win), or a cat that falls at the first hit (a loss).
        var easy = new BattleSettings { FirstWaveDelay = 0, SlimeMaxHp = 1, OutroSeconds = 0.5, Campaign = campaign };
        var deadly = new BattleSettings { FirstWaveDelay = 0, SlimeMaxHp = 100000, SlimeDamage = 50, CatMaxHp = 1, LandingGraceSeconds = 0, OutroSeconds = 0.5, Campaign = campaign };
        string dir = Path.Combine(Path.GetTempPath(), "pet-adventure-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "adventure-progress.json");
        window.AdventureProgressFile = file;
        window.ReloadAdventureProgress();
        window.BattleTuning = easy;
        int FreeRecords() => window.BattleRecords.Loadouts.Values.Sum(r => r.Wins + r.MostDefeated);
        int freeRecords = FreeRecords();
        try
        {
            // The menu: Battle Mode is one item (no submenu, no free battle) that opens the hub.
            window.ContextMenu!.IsOpen = true;
            await Task.Delay(30);
            var root = window.ContextMenu.Items.OfType<MenuItem>().First(i => i.Header as string == "Battle Mode");
            int items = root.Items.Count;
            bool enabled = root.IsEnabled;
            window.ContextMenu.IsOpen = false;
            await Task.Delay(30);
            check(items == 0 && enabled, $"Battle Mode is one item without a submenu ({items} items)");
            root.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, root));
            await Task.Delay(30);
            var hub = Find("Pet battle hub");
            long hubStyle = hub == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(hub).Handle, -20).ToInt64();
            check(window.BattleHubOpen && hub is { ShowActivated: false, ShowInTaskbar: false, IsVisible: true } && (hubStyle & 0x08000000) != 0 && !window.BattleActive,
                "Clicking it opens the hub: clickable, never takes focus, no taskbar button; nothing fights");
            check(window.OpenBattleHub() == null && Count("Pet battle hub") == 1 && behavior.Quiet && window.BattleOwnsPet, "Opening it again keeps the one hub; the cat's free time belongs to it");
            for (double t = 0; t < 0.3; t += 1.0 / 60) Call(window, "TickBattleHub", 1.0 / 60);
            string hubText = window.BattleHubText;
            check(hubText.Contains("Battle Mode") && hubText.Contains("Adventure") && hubText.Contains("Test chapter · 0/3 stages") && hubText.Contains("Infinity") && hubText.Contains("Not available"),
                $"The hub shows the modes and where the user is ({hubText.Replace('\n', '|')})");
            check(window.BattleHubModeEnabled(PetWindow.HubAdventure) && !window.BattleHubModeEnabled(PetWindow.HubInfinity) && window.BattleHubChoose(PetWindow.HubInfinity) is string soon && soon.Length > 0 && window.BattleHubOpen,
                "Adventure can be opened; Infinity (not in this data) opens nothing");
            Shot("Pet battle hub", "battle-hub-en.png");
            window.CancelBattleHub();
            await Task.Delay(20);
            check(!window.BattleHubOpen && Count("Pet battle hub") == 0 && !behavior.Quiet && !window.BattleOwnsPet, "The hub's Close leaves no window and gives the pet back");
            window.SetPaused(true);
            window.ContextMenu.IsOpen = true;
            await Task.Delay(30);
            string pausedHeader = root.Header as string ?? "";
            bool pausedEnabled = root.IsEnabled;
            window.ContextMenu.IsOpen = false;
            await Task.Delay(30);
            window.SetPaused(false);
            check(!pausedEnabled && pausedHeader.StartsWith("Battle Mode (") && window.OpenBattleHub() is null && window.BattleHubOpen, $"A paused pet: the item says why and is off ({pausedHeader})");

            // The map: one window, never takes focus, holds the pet's free time, starts nothing.
            check(window.BattleHubChoose(PetWindow.HubAdventure) == null && window.AdventureMapOpen && !window.BattleHubOpen && Count("Pet battle hub") == 0 && !window.BattleActive && !window.BattlePreparing,
                "The hub's Adventure opens the chapter map in its place; nothing fights");
            await Task.Delay(30);
            var map = Find("Pet adventure");
            long style = map == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(map).Handle, -20).ToInt64();
            check(map is { ShowActivated: false, ShowInTaskbar: false, IsVisible: true } && (style & 0x08000000) != 0, "The map is clickable, never takes focus and has no taskbar button");
            check(window.OpenAdventureMap() == null && Count("Pet adventure") == 1, "Opening it again keeps the one map");
            check(behavior.Quiet && window.BattleOwnsPet, "While the map is open the cat's free time belongs to it");
            Tick(0.3);
            check(window.AdventureSelectedStage == "st_one" && window.AdventureEnterEnabled && window.AdventureMapText.Contains("Test chapter") &&
                  window.AdventureMapText.Contains("Stage 1 · Meadow") && window.AdventureMapText.Contains("Goal Meadow") && window.AdventureMapText.Contains("Castle"),
                $"The map shows the chapter's stages; stage 1 is chosen and open ({window.AdventureMapText.Replace('\n', '|')})");
            window.AdventureSelectStage("st_two"); Tick(0.3);
            check(!window.AdventureEnterEnabled && window.AdventureMapText.Contains("Locked") && window.AdventureEnterStage("st_two") is string locked && locked.Length > 0 && !window.BattlePreparing,
                "A locked stage says what opens it and cannot be entered");
            window.CancelAdventureMap();
            await Task.Delay(20);
            check(!window.AdventureMapOpen && Count("Pet adventure") == 0 && window.BattleHubOpen && behavior.Quiet, "The map's back button goes back to the hub");
            window.CancelBattleHub();
            check(!behavior.Quiet && !window.BattleOwnsPet, "and the hub's Close gives the pet back");

            // Stage → setup → back to the map, and setup → battle.
            window.OpenAdventureMap();
            check(window.AdventureEnterStage() == null && window.BattlePreparing && window.BattlePrepStage?.Id == "st_one" && !window.AdventureMapOpen && Count("Pet adventure") == 0,
                "Play stage opens the setup panel for that stage (the map closes)");
            Tick(0.3);
            check(window.BattlePrepText.Contains("Stage 1 · Meadow") && window.BattlePrepText.Contains("Map") && behavior.Quiet, "The setup panel names the stage; its close button says Map");
            check(!window.BattlePrepHasSkillChoice && !window.BattlePrepText.Contains("Skills to bring") && !window.BattlePrepToggleSkill(BattleSkill.Bomb),
                "A stage of a chapter without skillChoice (chapter 1) has no skill choice: it keeps the five first skills");
            window.CancelBattlePrep();
            check(window.AdventureMapOpen && !window.BattlePreparing && window.AdventureSelectedStage == "st_one", "Map on the setup panel goes back to the map");

            // A lost run: counted, opens nothing, nothing in the free battle records.
            window.BattleTuning = deadly;
            var report = window.BattleReport;
            window.AdventureEnterStage();
            check(window.BattlePrepReady() == null && window.Battle?.Stage?.Id == "st_one" && window.Battle.WaveCount == 1 && !window.AdventureMapOpen,
                "Ready starts the stage's own battle");
            Finish();
            await Task.Delay(30);
            var progress = window.AdventureProgress;
            string panel = ResultText();
            check(window.LastBattleSummary is { Won: false, Stage.Id: "st_one" } && !progress.Completed.Contains("st_one") && progress.Runs.Count == 1 &&
                  window.Adventure?.IsUnlocked("st_two") == false && FreeRecords() == freeRecords && window.BattleReport.Lost == report.Lost + 1 && File.Exists(file),
                "A lost stage is counted once and saved, opens nothing and stays out of the free battle records");
            check(panel.Contains("Stage 1 · Meadow") && panel.Contains("Play again") && panel.Contains("Map") && panel.Contains("Back to desktop") && !panel.Contains("Next stage"),
                $"The result after a loss offers Play again, Map and Back to desktop ({panel})");

            // Play again (the main button) with an easier stage: a win opens stage 2.
            window.BattleTuning = easy;
            var result = Find("Pet result");
            Click(Button(result!.Content as DependencyObject ?? new Border(), "Play again")!);
            check(window.Battle?.Stage?.Id == "st_one" && Count("Pet result") == 0, "Play again starts the same stage at once");
            Finish();
            await Task.Delay(30);
            panel = ResultText();
            check(window.LastBattleSummary is { Won: true } summary && summary.Outcome.Unlocked?.Id == "st_two" && progress.Completed.Contains("st_one") &&
                  window.Adventure!.IsUnlocked("st_two") && !window.Adventure.IsUnlocked("st_three") && FreeRecords() == freeRecords,
                "Winning stage 1 completes it and opens stage 2 (free battle records untouched)");
            check(panel.Contains("Unlocked: Stage 2 · Bridge") && panel.Contains("Next stage") && panel.Contains("Play again") && panel.Contains("Map"),
                $"The result says what opened and offers Next stage first ({panel})");
            // The same run counted again (a result shown twice) changes nothing.
            int wins = progress.Records.Values.Sum(r => r.Wins);
            var again = window.Adventure!.Apply(Field<string>(window, "battleRunId"), window.Adventure.Stage("st_one")!, true, true, BattleWeapon.Sword, BattleDifficulty.Normal, 1, 1);
            check(!again.Counted && progress.Records.Values.Sum(r => r.Wins) == wins, "The same run is never counted twice");
            Shot("Pet result", "adventure-result-en.png");

            // Next stage: the setup panel for stage 2 with the same gear; a double click opens one panel.
            result = Find("Pet result");
            var next = Button(result!.Content as DependencyObject ?? new Border(), "Next stage")!;
            Click(next); Click(next);
            await Task.Delay(30);
            check(window.BattlePreparing && window.BattlePrepStage?.Id == "st_two" && Count("Pet setup") == 1 && !window.BattleActive && Count("Pet result") == 0,
                "Next stage opens one setup panel for stage 2 (double click included)");

            // Closing the app mid-run: nothing is counted; reopening reads the saved progress.
            window.BattlePrepReady();
            int runs = progress.Runs.Count;
            window.EndBattle();
            check(!window.BattleActive && Count("Pet result") == 0 && progress.Runs.Count == runs && !progress.Completed.Contains("st_two"),
                "Ending a run (End, Exit, reset) counts nothing and shows no result");
            window.ReloadAdventureProgress();
            check(window.Adventure!.IsCompleted("st_one") && window.Adventure.IsUnlocked("st_two") && !window.Adventure.IsCompleted("st_two") && !window.Adventure.IsUnlocked("st_three"),
                "After a restart the saved progress is read back: stage 1 won, stage 2 open, stage 3 locked");

            // Custom battle data: playable, but the run saves no progress.
            window.OpenAdventureMap("st_two");
            window.AdventureEnterStage();
            window.BattlePrepReady();
            SetField(window, "battleCustomData", true);
            Finish();
            await Task.Delay(30);
            check(window.LastBattleSummary is { Won: true, Ranked: false } && !window.AdventureProgress.Completed.Contains("st_two") && ResultText().Contains("Custom data"),
                "A win with custom battle data is shown but saves no progress");

            // Stage 2 then the last stage: the chapter is complete and there is no next stage.
            window.BattleReplay(window.LastBattleSummary!.Value);
            Finish();
            window.OpenAdventureMap("st_three");
            window.AdventureEnterStage(); window.BattlePrepReady();
            Finish();
            await Task.Delay(30);
            panel = ResultText();
            check(window.AdventureProgress.Completed.Count == 3 && panel.Contains("Chapter complete!") && !panel.Contains("Next stage") && panel.Contains("Play again"),
                $"Winning the last stage completes the chapter; no Next stage ({panel})");
            result = Find("Pet result");
            Click(Button(result!.Content as DependencyObject ?? new Border(), "Map")!);
            Tick(0.3);
            check(window.AdventureMapOpen && window.AdventureSelectedStage == "st_three" && window.AdventureMapText.Contains("Completed") && window.AdventureMapText.Contains("Won 1 time"),
                "Map on the result goes back to the map at that stage, shown as completed");
            window.AdventureSelectStage("st_two"); Tick(0.3); Shot("Pet adventure", "adventure-map-en.png"); window.AdventureSelectStage("st_three");

            // Fullscreen hides the map, pause keeps it from opening, reset closes it; Vietnamese.
            SetContext(window, true, 0); Call(window, "TickContext", 1.1); Tick(0.3);
            check(window.AdventureMapOpen && !window.AdventureMapVisible, "A fullscreen app hides the map");
            SetContext(window, false, 0); Call(window, "TickContext", 1.1); Tick(0.3);
            check(window.AdventureMapVisible, "It comes back when the fullscreen app closes");
            prefs.Language = "vi"; Tick(0.3);
            Shot("Pet adventure", "adventure-map-vi.png");
            check(window.AdventureMapText.Contains("Chương 1: Chương thử") && window.AdventureMapText.Contains("Màn 3") && window.AdventureMapText.Contains("Vào màn"), "The map follows the pet's language");
            prefs.Language = "ja"; Tick(0.3);
            Shot("Pet adventure", "adventure-map-ja.png");
            prefs.Language = "en";
            window.Reset();
            check(!window.AdventureMapOpen && Count("Pet adventure") == 0 && !behavior.Quiet, "Bringing the pet back closes the map");
            window.SetPaused(true);
            check(window.OpenAdventureMap() is string paused && paused.Contains("paused") && !window.AdventureMapOpen, "A paused pet does not open the map");
            window.SetPaused(false);

            // A sleeping cat: the map opens without waking it; no away nap while it is open.
            pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
            check(window.OpenAdventureMap() == null && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "The map opens over a sleeping cat without waking it");
            window.CancelAdventureMap(); window.CancelBattleHub();
            window.Reset(); behavior.WakeByClick(pet); pet.Emotions.FinishSleep(0); pet.Action = PetAction.Idle;
            window.OpenAdventureMap();
            SetContext(window, false, 400); Call(window, "TickContext", 1.1);
            check(pet.Action != PetAction.Sleeping && !behavior.HoldSleep, "Leaving the computer does not start an away nap while the map is open");
            SetContext(window, false, 0); Call(window, "TickContext", 1.1);
            window.CancelAdventureMap(); window.CancelBattleHub();

            // Data without a campaign: Adventure says so.
            window.BattleTuning = new BattleSettings();
            check(window.OpenAdventureMap() is string none && none.Contains("no Adventure") && !window.AdventureMapOpen, "Battle data without a campaign: Adventure explains and opens nothing");
        }
        finally
        {
            window.Reset();
            window.AdventureProgressFile = null;
            window.ReloadAdventureProgress();
            window.BattleTuning = tuningBefore;
            prefs.Language = language; prefs.BattleWeapon = weapon; prefs.BattleDifficulty = level;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
