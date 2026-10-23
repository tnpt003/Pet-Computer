using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopPet;
using static PetPrivate;

// Expedition milestones 2–4 in the real pet window: locked until the Adventure's gate stage is won →
// map → weapon/difficulty → run → step battle with the run's HP and build → result → Continue;
// End leaves a battle (RetreatCost), a battle cut off by closing/reset is charged when the map next
// opens (after a restart too), the boss ends the run, a loss ends it, giving up, custom data kept in
// memory; a won elite's relics, a rest kept over a restart, the relic in the next battle (milestone 3);
// keepsakes given once, the display corner and the cat looking at it (milestone 4).
// A temporary expedition file only; the Adventure progress stays in memory.
internal static class ExpeditionChecks
{
    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, idle)));
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    private struct Rect { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    // Places a window as the pet would at a pretend monitor scale, then reads where it went (screen pixels).
    private static Rect PlacedAt(PetWindow window, Window placed, string place, double testScale)
    {
        var scaleField = FieldOf("scale");
        double realScale = (double)scaleField.GetValue(window)!;
        scaleField.SetValue(window, testScale);
        try
        {
            // Moved off first: MoveWindow skips a window whose Left/Top already match (the test's
            // pretend scale can make them match by chance).
            placed.Left += 37; placed.Top += 23;
            Call(window, place);
            GetWindowRect(new WindowInteropHelper(placed).Handle, out var rect);
            return rect;
        }
        finally { scaleField.SetValue(window, realScale); Call(window, place); }
    }
    private static Window? Find(string title) => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == title && w.IsLoaded);
    private static int Count(string title) => Application.Current.Windows.OfType<Window>().Count(w => w.Title == title && w.IsLoaded);
    private static string Text(DependencyObject node) => node is TextBlock t ? t.Text + "|" : string.Concat(LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().Select(Text));
    private static string ResultText() => Find("Pet result")?.Content is DependencyObject content ? Text(content) : "";
    private static Border? Button(DependencyObject node, string label)
    {
        if (node is Border { Child: StackPanel panel } border && panel.Children.OfType<TextBlock>().Any(t => t.Text == label)) return border;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            if (Button(child, label) is Border found) return found;
        return null;
    }
    private static void Click(UIElement element) =>
        element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
    private static void Shot(string title, string name)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not string review || Find(title)?.Content is not FrameworkElement content) return;
        content.UpdateLayout();
        var shot = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth + 12), (int)Math.Ceiling(content.ActualHeight + 12), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        shot.Render(content);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));
        using var stream = File.Create(Path.Combine(review, name)); encoder.Save(stream);
    }

    public static async Task Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences prefs, Action<bool, string> check)
    {
        void Tick(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickExpeditionMap", 1.0 / 60); }
        void Fight(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattle", 1.0 / 60); }
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
        var campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "test", NameVi = "Chương thử", NameEn = "Test chapter", Stages = new[] { Stage("st_gate", "Gate") } } } };
        var trail = new ExpeditionSpec
        {
            Id = "trail", NameVi = "Đường thử", NameEn = "Test trail", UnlockAfter = "st_gate", RetreatCost = 0.25, WinHeal = 0, StepThreat = 0,
            Steps = new[] { new[] { ExpeditionSpec.Fight, ExpeditionSpec.Elite }, new[] { ExpeditionSpec.Fight, ExpeditionSpec.Fight } },
            Fights = new[] { Stage("x_field", "Field"), Stage("x_hill", "Hill") }, Elites = new[] { Stage("x_fort", "Fort") }, Boss = Stage("x_throne", "Throne")
        };
        // One weak slime per encounter that barely hurts (a quick win), or a cat that falls at the first hit.
        var easy = new BattleSettings { FirstWaveDelay = 0, SlimeMaxHp = 1, SlimeDamage = 1, OutroSeconds = 0.5, Campaign = campaign, Expedition = trail };
        var deadly = new BattleSettings { FirstWaveDelay = 0, SlimeMaxHp = 100000, SlimeDamage = 50, CatMaxHp = 1, LandingGraceSeconds = 0, OutroSeconds = 0.5, Campaign = campaign, Expedition = trail };
        string dir = Path.Combine(Path.GetTempPath(), "pet-expedition-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "expedition.json");
        window.ExpeditionProgressFile = file;
        window.ReloadExpeditionProgress();
        window.ReloadAdventureProgress();
        window.BattleTuning = easy;
        int adventureRuns = window.AdventureProgress.Runs.Count;
        int freeRecords = window.BattleRecords.Loadouts.Values.Sum(r => r.Wins + r.MostDefeated);
        ExpeditionRun? Saved() => ExpeditionProgress.Load(file).Active;
        try
        {
            // Locked until the gate stage is won: the hub's card says so.
            void TickHub() { for (double t = 0; t < 0.3; t += 1.0 / 60) Call(window, "TickBattleHub", 1.0 / 60); }
            check(window.OpenBattleHub() == null, "The hub opens");
            TickHub();
            check(!window.BattleHubModeEnabled(PetWindow.HubExpedition) && window.BattleHubText.Contains("Win the Slime King in the Adventure to open") && window.BattleHubText.Contains("Locked"),
                $"The hub lists the Expedition, locked at first ({window.BattleHubText.Replace('\n', '|')})");
            check(window.OpenExpeditionMap() is string locked && locked.Contains("Slime King") && !window.ExpeditionMapOpen, "A locked Expedition explains and opens nothing");
            window.AdventureProgress.Completed.Add("st_gate");
            TickHub();
            check(window.BattleHubModeEnabled(PetWindow.HubExpedition) && !window.BattleHubText.Contains("Locked"), "Once the gate stage is won the hub opens the Expedition");
            Shot("Pet battle hub", "battle-hub-unlocked-en.png");
            window.CancelBattleHub();

            // The map before a run: one window, never takes focus, holds the pet's free time.
            check(window.OpenExpeditionMap() == null && window.ExpeditionMapOpen && !window.BattleActive, "The Expedition map opens; nothing fights");
            await Task.Delay(30);
            var map = Find("Pet expedition");
            long style = map == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(map).Handle, -20).ToInt64();
            check(map is { ShowActivated: false, ShowInTaskbar: false, IsVisible: true } && (style & 0x08000000) != 0, "The map is clickable, never takes focus and has no taskbar button");
            check(window.OpenExpeditionMap() == null && Count("Pet expedition") == 1 && behavior.Quiet && window.BattleOwnsPet, "Opening it again keeps the one map; the cat's free time belongs to it");
            Tick(0.3);
            string text = window.ExpeditionMapText;
            check(text.Contains("Expedition — Test trail") && text.Contains("No run yet · 3 steps") && text.Contains("Prepare the run") && text.Contains("last time: Sword · Normal") && !text.Contains("Give up"),
                $"Before a run the map offers to prepare it ({text.Replace('\n', '|')})");
            check(!File.Exists(file), "Opening the map saves nothing yet");

            // Prepare: the usual setup panel (weapon cards with stats, difficulty, the cat's preview), for the whole run.
            check(window.ExpeditionPrepare() == null && window.BattlePrepForExpedition && !window.ExpeditionMapOpen && Count("Pet expedition") == 0 && window.BattlePrepCards == easy.WeaponList.Count && window.BattlePrepPreview != null,
                "Prepare opens the shared setup panel in the map's place");
            if (window.BattleTutorialOpen) window.CloseBattleTutorial(finished: true);
            string prep = window.BattlePrepText;
            check(prep.Contains("Expedition — prepare the run") && prep.Contains("Set out") && prep.Contains("‹ Map") && !window.BattleActive, $"It is titled for the run and sets out instead of fighting ({prep.Replace('\n', '|')})");
            Shot("Pet setup", "expedition-setup-en.png");
            window.CancelBattlePrep();
            check(window.ExpeditionMapOpen && !window.BattlePreparing && window.Expedition!.Run == null && !File.Exists(file), "Its Map button comes back to the map; no run starts");
            window.ExpeditionPrepare();
            check(window.BattlePrepChooseWeapon(BattleWeapon.Hammer) && window.BattlePrepChooseDifficulty(BattleDifficulty.Hard), "The run's weapon and difficulty are chosen on the panel");
            check(window.BattlePrepReady() == null && window.ExpeditionMapOpen && !window.BattlePreparing && !window.BattleActive &&
                  window.Expedition!.Run is { Weapon: BattleWeapon.Hammer, Level: BattleDifficulty.Hard } && Saved() != null, "Set out begins the run with that loadout (saved) and comes back to the map");
            check(window.BattlePrepReady() is string twice0 && twice0.Length > 0 && window.Expedition!.Run != null, "A second Set out (double click) finds no panel");

            // A run with the hammer on Hard: saved at once, the first step to fight (seeded again, the same way).
            window.ExpeditionProgress.Active = null; // Not given up: no run is counted.
            check(window.ExpeditionChooseWeapon(BattleWeapon.Hammer) && window.ExpeditionChooseDifficulty(BattleDifficulty.Hard) && window.ExpeditionStart(5) == null, "Start the run begins one");
            var run = window.Expedition!.Run!;
            Tick(0.3);
            text = window.ExpeditionMapText;
            check(run.Weapon == BattleWeapon.Hammer && run.Level == BattleDifficulty.Hard && Saved()?.RunId == run.RunId && prefs.BattleWeapon == BattleWeapon.Hammer,
                "The run keeps its weapon and difficulty and is saved at once");
            check(text.Contains("Step 1/3 · HP 100/100") && text.Contains("Fight") && text.Contains("Give up") && text.Contains("Fight — ") && window.ExpeditionMainEnabled,
                $"The map shows the step, the cat's HP and the chosen node ({text.Replace('\n', '|')})");
            check(window.ExpeditionStart() is string twice && twice.Length > 0 && window.Expedition!.Run!.RunId == run.RunId, "A second Start keeps the run");
            Shot("Pet expedition", "expedition-map-en.png");
            // DPI (milestone 5): top centre of the cat's monitor in its pixels, at its real scale and a pretend 150%.
            var mapArea = (Area)FieldOf("bounds").GetValue(window)!;
            double petScale = (double)FieldOf("scale").GetValue(window)!;
            bool MapPlaced(double at)
            {
                var placed = Find("Pet expedition")!;
                var rect = PlacedAt(window, placed, "PlaceExpeditionMap", at);
                double width = placed.ActualWidth, left = Math.Clamp((mapArea.Left + mapArea.Right - width) / 2, mapArea.Left, Math.Max(mapArea.Left, mapArea.Right - width));
                return Math.Abs(rect.Left - left * at) <= 2 && Math.Abs(rect.Top - (mapArea.Top + 12) * at) <= 2;
            }
            check(MapPlaced(petScale) && MapPlaced(1.5), "The map sits at the top centre of the cat's monitor in its pixels (real scale and 150%)");

            // Step 1: the elite node.
            check(window.ExpeditionSelect(1) && !window.ExpeditionSelect(2), "A node of the current step can be chosen (only those)");
            Tick(0.3);
            check(window.ExpeditionMapText.Contains("Elite — Fort"), "The chosen node's kind and encounter are shown");
            var node = window.Expedition!.Node(1)!.Value;
            check(window.ExpeditionFight() == null && window.BattleActive && window.BattleIsExpedition && window.Battle!.Stage?.Id == "x_fort" && window.Battle.Seed == node.Seed
                  && !window.ExpeditionMapOpen && Count("Pet expedition") == 0 && window.Battle.Weapon.Id == BattleWeapon.Hammer && window.Battle.Difficulty == BattleDifficulty.Hard,
                "Fight starts the node's own battle with the run's gear (the map closes)");
            check(Saved()?.Fighting == 1, "The battle under way is saved before it is fought");
            // Its monitor disconnected (milestone 5): the battle moves to another (150 %) and stays the run's step.
            string lostMonitor = (string)FieldOf("battleMonitor").GetValue(window)!;
            double hpBefore = window.Battle!.CatHp;
            SetField(window, "battleScreenProbe", new Func<string?, (string, Area, double)?>(name => name == lostMonitor ? null : ("Other", mapArea, 1.5)));
            SetField(window, "battleMonitorProbe", new Func<string>(() => "Other"));
            Call(window, "RecoverLostBattleMonitor");
            check(window.BattleActive && window.BattleIsExpedition && window.BattlePauseReasons.HasFlag(PetWindow.BattlePause.User) &&window.Battle!.CatHp == hpBefore && Saved()?.Fighting == 1 && window.Expedition!.Run!.Retreats == 0,
                "A disconnected battle monitor moves the step's battle (paused), still the run's step: nothing is left or charged");
            SetField(window, "battleScreenProbe", null);
            SetField(window, "battleMonitorProbe", null);
            FieldOf("battleMonitor").SetValue(window, lostMonitor);
            FieldOf("battleScale").SetValue(window, petScale);
            window.SetBattlePaused(false);
            Call(window, "CloseBattleNotice");
            // The banner names the step and encounter and always fits the HUD's centre (a long one shrinks; the HUD keeps its width).
            var hud = (BattleHud)FieldOf("battleHud").GetValue(window)!;
            hud.Update(window.Battle!, false, false, false, false);
            hud.UpdateLayout();
            double hudWidth = hud.ActualWidth;
            check(hud.BannerText == "Step 1/3 · Fort" && hud.BannerShownWidth > 0 && hud.BannerShownWidth <= BattleHud.CentreWidth + 0.5,
                $"The step banner is short and fits the HUD ({hud.BannerText}, {hud.BannerShownWidth:0} DIPs)");
            hud.ShowBanner("Expedition · Step 6/6 · The King returns with a much longer name", window.Battle!.Time);
            hud.Update(window.Battle!, false, false, false, false);
            hud.UpdateLayout();
            check(hud.BannerShownWidth > 0 && hud.BannerShownWidth <= BattleHud.CentreWidth + 0.5 && Math.Abs(hud.ActualWidth - hudWidth) < 0.5,
                $"A long banner shrinks to the HUD's centre and the HUD keeps its width ({hud.BannerShownWidth:0} DIPs, HUD {hudWidth:0} → {hud.ActualWidth:0})");
            Shot("Pet battle HUD", "expedition-hud-banner.png");
            Finish();
            await Task.Delay(30);
            string panel = ResultText();
            check(window.LastBattleSummary is { Won: true, Expedition.RunOver: false } && run.Path.SequenceEqual(new[] { 1 }) && Saved()?.Path.Count == 1 && Saved()?.Fighting == null,
                "A won step moves the run on and is saved");
            check(panel.Contains("Expedition · Step 1/3 · Fort") && panel.Contains("Step 1/3 done") && panel.Contains("Continue") && panel.Contains("the run is saved") && !panel.Contains("Play again"),
                $"The step's result says what it changed and offers Continue ({panel})");
            check(window.AdventureProgress.Runs.Count == adventureRuns && window.BattleRecords.Loadouts.Values.Sum(r => r.Wins + r.MostDefeated) == freeRecords,
                "An Expedition step counts in neither the Adventure progress nor the free battle records");
            var continueButton = Button(Find("Pet result")!.Content as DependencyObject ?? new Border(), "Continue")!;
            Click(continueButton); Click(continueButton);
            Tick(0.3);
            check(window.ExpeditionMapOpen && Count("Pet result") == 0 && Count("Pet expedition") == 1 && window.ExpeditionMapText.Contains("Step 2/3") && window.Expedition!.Run!.Step == 1,
                "Continue goes back to the map at the next step (clicked twice: one map, one step)");

            // Step 2 with the HP the run has: End leaves the battle for RetreatCost.
            run.Hp = 60;
            check(window.ExpeditionFight(0) == null && window.Battle!.CatHp == 60, "The step's battle starts with the run's HP");
            window.EndBattleByUser();
            Tick(0.3);
            text = window.ExpeditionMapText;
            check(!window.BattleActive && window.ExpeditionMapOpen && run.Hp == 35 && run.Step == 1 && run.Fighting == null && Saved()?.Hp == 35 && Count("Pet result") == 0,
                "End leaves the step's battle: 25 HP lost, the step stays, back to the map (saved)");
            check(text.Contains("Left the step 2 battle: 25 HP lost") && text.Contains("HP 35/100"), $"The map says what leaving cost ({text.Replace('\n', '|')})");

            // A battle cut off (closing the app, a crash, reset): charged when the map opens again, after a restart too.
            window.ExpeditionFight(0);
            window.EndBattle();
            check(Saved()?.Fighting == 0 && Saved()?.Hp == 35, "A battle cut off leaves the run marked as fighting");
            window.ReloadExpeditionProgress();
            check(window.OpenExpeditionMap() == null && window.Expedition!.Run is { Hp: 10, Fighting: null, Retreats: 2 } && Saved()?.Hp == 10, "Reopened after a restart, the cut-off battle costs the same as leaving it");
            Tick(0.3);
            check(window.ExpeditionMapText.Contains("Left the step 2 battle"), "The map says why HP is lower");
            run = window.Expedition!.Run!;

            // Step 2 won, then the boss: the run is complete.
            window.ExpeditionFight(0);
            Finish();
            await Task.Delay(30);
            Click(Button(Find("Pet result")!.Content as DependencyObject ?? new Border(), "Continue")!);
            Tick(0.3);
            check(window.ExpeditionMapText.Contains("Step 3/3") && window.ExpeditionMapText.Contains("Boss — Throne"), "The last step is the boss");
            window.ExpeditionFight();
            Finish();
            await Task.Delay(30);
            panel = ResultText();
            var record = window.Expedition!.Record(BattleWeapon.Hammer, BattleDifficulty.Hard);
            check(window.LastBattleSummary is { Expedition: { RunOver: true, RunWon: true } } && window.Expedition.Run == null && Saved() == null && record is { Runs: 1, Wins: 1, BestSteps: 3 },
                "Winning the boss completes the run: counted once as won, nothing left to resume");
            check(panel.Contains("Expedition complete!") && panel.Contains("New record: fastest run!") && panel.Contains("Expedition map"), $"The result celebrates the run ({panel})");
            Click(Button(Find("Pet result")!.Content as DependencyObject ?? new Border(), "Expedition map")!);
            Tick(0.3);
            text = window.ExpeditionMapText;
            check(window.ExpeditionMapOpen && text.Contains("No run yet") && text.Contains("1/1 runs won") && text.Contains("furthest 3/3 steps"), $"Back on the map a new run can start; the records show ({text.Replace('\n', '|')})");

            // A loss ends the run where it is.
            window.ExpeditionStart(6);
            window.BattleTuning = deadly;
            window.ExpeditionFight(0);
            Finish();
            await Task.Delay(30);
            panel = ResultText();
            check(window.LastBattleSummary is { Won: false, Expedition: { RunOver: true, RunWon: false } } && window.Expedition!.Run == null && Saved() == null
                  && window.Expedition.Record(BattleWeapon.Hammer, BattleDifficulty.Hard).Runs == 2 && panel.Contains("The run ended at step 1/3"),
                $"A lost step ends the run, counted ({panel})");

            // Giving up: the map asks twice.
            window.BattleTuning = easy;
            window.OpenExpeditionMap();
            window.ExpeditionStart(7);
            Tick(0.3);
            var giveUp = Button(Find("Pet expedition")!.Content as DependencyObject ?? new Border(), "Give up")!;
            Click(giveUp); Tick(0.3);
            check(window.Expedition!.Run != null && window.ExpeditionMapText.Contains("Click again to give up"), "Give up asks once more");
            Click(Button(Find("Pet expedition")!.Content as DependencyObject ?? new Border(), "Click again to give up")!);
            Tick(0.3);
            check(window.Expedition!.Run == null && Saved() == null && window.ExpeditionMapText.Contains("given up at step 1"), "The second click gives the run up");

            // Fullscreen hides the map, Vietnamese, reset closes it, a paused pet does not open it.
            SetContext(window, true, 0); Call(window, "TickContext", 1.1); Tick(0.3);
            check(window.ExpeditionMapOpen && !window.ExpeditionMapVisible, "A fullscreen app hides the map");
            SetContext(window, false, 0); Call(window, "TickContext", 1.1); Tick(0.3);
            check(window.ExpeditionMapVisible, "It comes back when the fullscreen app closes");
            prefs.Language = "vi"; Tick(0.3);
            check(window.ExpeditionMapText.Contains("Viễn chinh — Đường thử") && window.ExpeditionMapText.Contains("Chuẩn bị chuyến"), "The map follows the pet's language");
            Shot("Pet expedition", "expedition-map-vi.png");
            prefs.Language = "ja"; Tick(0.3);
            Shot("Pet expedition", "expedition-map-ja.png");
            prefs.Language = "en";
            window.Reset();
            check(!window.ExpeditionMapOpen && Count("Pet expedition") == 0 && !behavior.Quiet && !window.BattleOwnsPet, "Bringing the pet back closes the map");
            window.SetPaused(true);
            check(window.OpenExpeditionMap() is string paused && paused.Contains("paused") && !window.ExpeditionMapOpen, "A paused pet does not open the map");
            window.SetPaused(false);

            // The user's battle.json: a run kept in memory only, the file untouched.
            string before = File.Exists(file) ? File.ReadAllText(file) : "";
            typeof(PetWindow).GetProperty("BattleDataCustom")!.SetValue(window, true);
            window.OpenExpeditionMap();
            window.ExpeditionStart(8);
            window.ExpeditionFight(0);
            Finish();
            await Task.Delay(30);
            check(window.LastBattleSummary is { Ranked: false, Expedition.RunOver: false } && (File.Exists(file) ? File.ReadAllText(file) : "") == before && ResultText().Contains("Custom data"),
                "With custom battle data the run plays but nothing is written to the file");
            window.Reset();
            typeof(PetWindow).GetProperty("BattleDataCustom")!.SetValue(window, false);

            // Milestone 3: a won elite's relics, a rest, the relic in the next battle.
            var bell = new RelicSpec { Id = "r_bell", Effect = RelicSpec.Counter, Amount = 0.6, Penalty = 3, NameVi = "Chuông", NameEn = "Bell", DescVi = "Chuông phản đòn", DescEn = "Counter after a block" };
            var feather = new RelicSpec { Id = "r_feather", Effect = RelicSpec.Light, Amount = 0.15, Extra = 0.1, Penalty = 15, NameVi = "Lông", NameEn = "Feather", DescVi = "Nhẹ", DescEn = "Lighter" };
            var shrine = new ExpeditionEventSpec { Id = "shrine", Kind = ExpeditionEventSpec.Shrine, Cost = 0.2, NameVi = "Đền", NameEn = "Shrine", DescVi = "Đền cũ", DescEn = "An old shrine" };
            var trail3 = new ExpeditionSpec
            {
                Id = "trail3", NameVi = "Đường ba", NameEn = "Trail three", UnlockAfter = "st_gate", RetreatCost = 0.25, WinHeal = 0, StepThreat = 0, RestHeal = 0.3,
                Steps = new[] { new[] { ExpeditionSpec.Elite }, new[] { ExpeditionSpec.Rest, ExpeditionSpec.Event }, new[] { ExpeditionSpec.Fight } },
                Fights = new[] { Stage("x3_field", "Field") }, Elites = new[] { Stage("x3_fort", "Fort") }, Boss = Stage("x3_throne", "Throne"),
                Events = new[] { shrine }, Relics = new[] { bell, feather }
            };
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, SlimeMaxHp = 1, SlimeDamage = 1, OutroSeconds = 0.5, Campaign = campaign, Expedition = trail3 };
            window.ReloadExpeditionProgress();
            window.OpenExpeditionMap();
            window.ExpeditionStart(9);
            Tick(0.3);
            check(window.ExpeditionMapText.Contains("Relics: none yet (0/3)") && window.ExpeditionMapText.Contains("Win: pick one of 2 relics"), "The map lists the run's relics and says what an elite gives");
            window.ExpeditionFight(0);
            Finish();
            await Task.Delay(30);
            panel = ResultText();
            check(window.LastBattleSummary is { Expedition.Reward: true } && panel.Contains("The elite left relics") && Saved()?.Pending?.Kind == "reward" && Saved()!.Pending!.Relics.Length == 2,
                $"A won elite leaves relics to pick on the map, saved ({panel})");
            Click(Button(Find("Pet result")!.Content as DependencyObject ?? new Border(), "Continue")!);
            Tick(0.3);
            text = window.ExpeditionMapText;
            check(window.ExpeditionHasChoice && text.Contains("Elite reward") && text.Contains("Bell") && text.Contains("Feather") && text.Contains("Counter after a block") && !text.Contains("Fight\n") && !text.Contains("Go here")
                  && !window.ExpeditionOptionEnabled(Expedition.Take) && window.ExpeditionOptionEnabled(Expedition.Leave) && window.ExpeditionFight(0) != null && window.ExpeditionSelect(0) == false,
                $"The map shows the relics with what they do; nothing goes on until one is taken or left ({text.Replace('\n', '|')})");
            check(window.ExpeditionPick(relic: "r_bell") && window.ExpeditionOptionEnabled(Expedition.Take), "Picking a relic enables Take");
            Shot("Pet expedition", "expedition-reward-en.png");
            var takeButton = Button(Find("Pet expedition")!.Content as DependencyObject ?? new Border(), "Take the chosen relic")!;
            Click(takeButton); Click(takeButton); // A double click takes it once (checked below).
            Tick(0.3);
            check(window.Expedition!.Run!.Relics.SequenceEqual(new[] { "r_bell" }) && Saved()!.Relics.SequenceEqual(new[] { "r_bell" }) && Saved()!.Pending == null && !window.ExpeditionHasChoice
                  && window.ExpeditionMapText.Contains("Got the Bell relic") && window.ExpeditionMapText.Contains("Relics: ◈ Bell (1/3)"), "Taking it keeps the relic in the run (saved) and says so");

            // A rest: drawn once, kept over a restart; resolved once.
            check(window.ExpeditionSelect(0) && window.ExpeditionMapText.Contains("Rest") && window.ExpeditionMapText.Contains("Go here"), "A rest node says what it offers and goes there");
            window.Expedition!.Run!.Hp = 50;
            check(window.ExpeditionVisit() == null && Saved()?.Pending?.Kind == "rest" && window.ExpeditionHasChoice && !window.ExpeditionMapText.Contains("Go here") && window.ExpeditionMapText.Contains("Rest — choose one"),
                "Going to the rest opens its choice on the map (saved)");
            var cards = Saved()!.Pending!.Cards;
            Shot("Pet expedition", "expedition-rest-en.png");
            window.CancelExpeditionMap(); window.CancelBattleHub();
            window.ReloadExpeditionProgress();
            window.OpenExpeditionMap();
            Tick(0.3);
            check(window.Expedition!.Run!.Pending!.Cards.SequenceEqual(cards) && window.ExpeditionMapText.Contains("Rest: +30 HP") && window.ExpeditionHasChoice, "Reopened after a restart the rest offers the same cards");
            check(window.ExpeditionChoose(Expedition.Card) is string none3 && none3.Length > 0 && window.ExpeditionHasChoice, "Taking a card without picking one is refused");
            check(window.ExpeditionChoose(Expedition.Heal) == null && window.Expedition.Run!.Hp == 80 && window.Expedition.Run.Step == 2 && Saved()?.Hp == 80 && !window.ExpeditionHasChoice
                  && window.ExpeditionChoose(Expedition.Heal) != null, "Resting gives 30 HP back once and passes the step (saved)");

            // The next battle carries the relic: the HUD lists it, the Shield waits longer.
            check(window.ExpeditionFight(0) == null && window.Battle!.Relics.Count == 1 && window.Battle.SkillCooldown(BattleSkill.Shield) == window.BattleTuning.SkillCooldown(BattleSkill.Shield) + 3,
                "The step's battle has the run's relic");
            hud = (BattleHud)FieldOf("battleHud").GetValue(window)!;
            hud.Update(window.Battle!, false, false, false, false);
            check(hud.UpgradesText.Contains("◈ Bell"), $"The HUD lists the relic ({hud.UpgradesText})");
            window.EndBattle();
            window.Reset();
            check(new BattleSim(window.BattleTuning, 0, 1600).Relics.Count == 0, "A free battle has no relics");
            window.BattleTuning = easy;

            // Milestone 4: a won run gives keepsakes once; the display corner shows them; the cat looks at them.
            var tiny = new ExpeditionSpec
            {
                Id = "tiny", NameVi = "Ngắn", NameEn = "Short", UnlockAfter = "st_gate", WinHeal = 0, StepThreat = 0,
                Steps = new[] { new[] { ExpeditionSpec.Fight } }, Fights = new[] { Stage("x4_field", "Field") }, Elites = Array.Empty<StageSpec>(), Boss = Stage("x4_throne", "Throne"),
                Keepsakes = BattleData.Settings.Expedition!.Keepsakes
            };
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, SlimeMaxHp = 1, SlimeDamage = 1, OutroSeconds = 0.5, Campaign = campaign, Expedition = tiny };
            window.ReloadExpeditionProgress();
            Call(window, "TickTrophies");
            check(!window.AccessoryAvailable("ExpeditionScarf") && window.ShownKeepsakes.Count == 0 && !window.TrophiesVisible, "Without keepsakes: no scarf in Settings, no display corner");
            window.OpenExpeditionMap();
            window.ExpeditionChooseDifficulty(BattleDifficulty.Normal);
            window.ExpeditionStart(10);
            Tick(0.3);
            check(window.ExpeditionMapText.Contains("Keepsakes: 0/5 · win a run at each difficulty to earn them"), $"The map counts the keepsakes ({window.ExpeditionMapText.Replace('\n', '|')})");
            window.ExpeditionFight(0);
            Finish();
            await Task.Delay(30);
            Click(Button(Find("Pet result")!.Content as DependencyObject ?? new Border(), "Continue")!);
            Tick(0.3);
            window.ExpeditionFight();
            Finish();
            await Task.Delay(30);
            panel = ResultText();
            check(panel.Contains("New keepsake: Silver Expedition cup (in the display corner)") && panel.Contains("New keepsake: Expedition scarf (wear it from Settings)") && panel.Contains("New keepsake: Slime cushion"),
                $"The won run's result names its new keepsakes ({panel})");
            check(ExpeditionProgress.Load(file).Keepsakes.OrderBy(k => k).SequenceEqual(new[] { "cup_silver", "expedition_scarf", "slime_cushion" }) && window.AccessoryAvailable("ExpeditionScarf"),
                "The keepsakes are saved with the run; Settings now offers the scarf");
            Click(Button(Find("Pet result")!.Content as DependencyObject ?? new Border(), "Back to desktop")!);
            window.Reset();
            Call(window, "TickTrophies");
            await Task.Delay(30);
            var keepsakeWindow = Find("Pet trophies");
            long keepsakeStyle = keepsakeWindow == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(keepsakeWindow).Handle, -20).ToInt64();
            check(window.TrophiesVisible && keepsakeWindow is { ShowActivated: false, ShowInTaskbar: false, Topmost: true } && (keepsakeStyle & 0x20) == 0 && (keepsakeStyle & 0x08000000) != 0,
                "The display corner shows: clickable (it can be picked up), never takes focus, no taskbar button");
            var area = (Area)FieldOf("bounds").GetValue(window)!;
            var spot = window.TrophyBounds;
            var keepsake = keepsakeWindow?.Content as KeepsakeArt;
            double across = (spot.Left - area.Left) / (area.Right - area.Left - spot.Width);
            check(Math.Abs(across - 0.75) < 0.01 && Math.Abs(spot.Bottom - area.Bottom) < 1 && keepsake != null && !window.KeepsakeHopping
                  && keepsake.Items.Select(k => k.Id).SequenceEqual(new[] { "cup_silver" }) && Math.Abs(spot.Width - KeepsakeArt.WidthFor(keepsake.Items) * PetWindow.DisplayScale) < 0.5
                  && Math.Abs(spot.Height - KeepsakeArt.ArtHeight * PetWindow.DisplayScale) < 0.5 && Math.Abs(keepsakeWindow!.Width - spot.Width - PetWindow.KeepsakeSide * 2) < 0.5,
                $"First shown 3/4 across the cat's monitor (away from the edge), standing on its floor: one keepsake, the best (silver cup over the cushion), drawn 1.8× ({spot}, floor {area.Bottom})");
            // Customize chooses the one on display.
            window.ShowCustomize();
            var settingsForm = (PetSettingsForm)FieldOf("customizeForm").GetValue(window)!;
            check(settingsForm.TrophyIds.SequenceEqual(new[] { "cup_silver", "slime_cushion" }) && settingsForm.Trophy.IsEnabled && settingsForm.Trophy.SelectedIndex == 0,
                "Settings offer the owned keepsakes (cups first), the shown one selected");
            settingsForm.Trophy.SelectedIndex = 1;
            settingsForm.Save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Call(window, "TickTrophies");
            check(prefs.TrophyShown == "slime_cushion" && keepsake!.Items.Select(k => k.Id).SequenceEqual(new[] { "slime_cushion" })
                  && Math.Abs(window.TrophyBounds.Width - KeepsakeArt.WidthFor(keepsake.Items) * PetWindow.DisplayScale) < 0.5 && Math.Abs(window.TrophyBounds.Bottom - area.Bottom) < 1,
                "Choosing the cushion and saving puts the cushion on display instead");
            Shot("Pet trophies", "trophies-cushion.png");
            prefs.TrophyShown = "cup_gold"; Call(window, "TickTrophies");
            check(keepsake!.Items.Select(k => k.Id).SequenceEqual(new[] { "cup_silver" }), "A chosen keepsake that is not owned falls back to the best one");
            prefs.TrophyShown = null; Call(window, "TickTrophies");
            spot = window.TrophyBounds;
            Shot("Pet trophies", "trophies-keepsake.png");
            bool KeepsakePlaced(double at)
            {
                var keepsakeScale = FieldOf("keepsakeScale");
                double real = (double)keepsakeScale.GetValue(window)!;
                keepsakeScale.SetValue(window, at);
                try
                {
                    keepsakeWindow!.Left += 37; keepsakeWindow.Top += 23;
                    Call(window, "PositionKeepsake");
                    GetWindowRect(new WindowInteropHelper(keepsakeWindow).Handle, out var rect);
                    return Math.Abs(rect.Left - (spot.Left - PetWindow.KeepsakeSide) * at) <= 2 && Math.Abs(rect.Top - (spot.Bottom + PetWindow.KeepsakeBottomRoom - keepsakeWindow.Height) * at) <= 2;
                }
                finally { keepsakeScale.SetValue(window, real); Call(window, "PositionKeepsake"); }
            }
            check(keepsakeWindow != null && KeepsakePlaced(petScale) && KeepsakePlaced(1.5), "The display corner is placed in its monitor's pixels (real scale and 150%)");
            SetContext(window, true, 0); Call(window, "TickContext", 1.1); Call(window, "TickTrophies");
            check(!window.TrophiesVisible, "A fullscreen app hides the display corner");
            SetContext(window, false, 0); Call(window, "TickContext", 1.1); Call(window, "TickTrophies");
            prefs.ShowTrophies = false; Call(window, "TickTrophies");
            check(!window.TrophiesVisible && !window.AdmireTrophies(), "Turned off in Settings it hides, and the cat has nothing to look at");
            prefs.ShowTrophies = true; Call(window, "TickTrophies");
            check(window.TrophiesVisible, "Turned on again it shows");
            check(window.StartBattle() == null && window.BattleActive, "A free battle starts");
            Call(window, "TickTrophies");
            check(!window.TrophiesVisible && !window.AdmireTrophies(), "A battle hides the display corner (and the cat stays in it)");
            window.EndBattle();
            window.Reset();
            Call(window, "TickTrophies");

            // The cat looks at its keepsakes: not in Quiet; walks over, looks a few seconds, done.
            prefs.Quiet = true; behavior.Quiet = true;
            check(!window.AdmireTrophies() && !window.Admiring, "In Quiet the cat does not go to look on its own");
            prefs.Quiet = false; behavior.Quiet = false;
            pet.X = Math.Max(area.Left, spot.Left - 400);
            pet.Action = PetAction.Idle; pet.VX = 0;
            check(window.AdmireTrophies() && window.Admiring, "The cat may go to look at its keepsakes");
            Call(window, "TickFeatures", 1.0 / 60);
            check(pet.Action == PetAction.Walking && pet.VX > 0, "It walks toward the keepsake");
            pet.X = spot.Left - 112 * 0.72;
            Call(window, "TickFeatures", 1.0 / 60);
            check(pet.Action == PetAction.Looking && pet.Facing == 1 && pet.VX == 0 && window.Admiring, "Beside it, it stops and looks at the keepsake");
            for (double t = 0; t < PetWindow.AdmireSeconds + 0.2; t += 1.0 / 60) Call(window, "TickFeatures", 1.0 / 60);
            check(!window.Admiring && pet.Action != PetAction.Looking, "After a few seconds it goes back to its own business");
            window.AdmireTrophies();
            prefs.Quiet = true; behavior.Quiet = true;
            Call(window, "TickFeatures", 1.0 / 60);
            check(!window.Admiring, "Quiet turned on during a visit ends it");
            prefs.Quiet = false; behavior.Quiet = false;
            window.AdmireTrophies();
            window.Reset();
            check(!window.Admiring, "Bringing the pet back ends the visit");

            // The keepsake as a toy: falls and lands, bonks the cat, hops back from an edge, gets shoved.
            var motion = window.TrophyMotion;
            void Keepsake(double seconds, bool features = false)
            {
                for (double t = 0; t < seconds; t += 1.0 / 60)
                {
                    Call(window, "TickKeepsake", 1.0 / 60);
                    if (features) Call(window, "TickFeatures", 1.0 / 60);
                    pet.Emotions.Step(pet, 1.0 / 60);
                }
            }
            double middleX = (area.Left + area.Right - spot.Width) / 2;
            motion.X = middleX + 300; motion.Height = 300; motion.VX = motion.VY = 0;
            Keepsake(0.2);
            check(motion.Height > 0 && motion.Height < 300 && window.TrophyBounds.Bottom < area.Bottom, "Let go up high, the keepsake falls");
            Keepsake(3);
            check(motion.Resting && motion.Height == 0 && Math.Abs(window.TrophyBounds.Bottom - area.Bottom) < 1 && !window.KeepsakeHopping, "It lands on the floor and settles there");
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") != null)
            {
                // The poses, for the render review: squashed on landing, dangling while held.
                motion.Squash = 0.3; Call(window, "PositionKeepsake"); Shot("Pet trophies", "trophies-squash.png");
                motion.Squash = 0; motion.Tilt = 22; SetField(window, "keepsakeHeld", true); SetField(window, "keepsakeGrabX", 20.0); SetField(window, "keepsakeGrabY", 40.0);
                Call(window, "PositionKeepsake"); Shot("Pet trophies", "trophies-held.png");
                motion.Tilt = 0; SetField(window, "keepsakeHeld", false); Call(window, "PositionKeepsake");
            }
            SetField(window, "paused", true);
            motion.Height = 200;
            Keepsake(0.5);
            check(motion.Height == 200, "A paused pet freezes the keepsake in the air");
            SetField(window, "paused", false);
            Keepsake(3);

            // Dropped on the cat's head: bonk, stars, the keepsake bounces off to one side.
            window.Reset();
            pet.Action = PetAction.Idle;
            double headTop = pet.Y + 16;
            motion.X = pet.X + 56 - spot.Width / 2; motion.Height = area.Bottom - headTop + 30; motion.VX = 0; motion.VY = -300;
            Keepsake(0.2);
            check(pet.Bonk > 0 && pet.Action == PetAction.Sitting && motion.VY > 0, $"Dropped on the cat's head it bonks it (stars) and bounces up (bonk {pet.Bonk:F2}, VY {motion.VY:F0})");
            Keepsake(PetModel.BonkDuration);
            var landedAt = window.TrophyBounds;
            check(pet.Bonk == 0 && motion.Height == 0 && (landedAt.Right < pet.X + 24 || landedAt.Left > pet.X + 88),
                $"The bonk passes; the keepsake landed beside the cat, not on it ({landedAt.Left:F0}–{landedAt.Right:F0}, cat {pet.X:F0})");
            Keepsake(3);
            window.Reset();
            pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
            motion.X = pet.X + 56 - spot.Width / 2; motion.Height = area.Bottom - headTop + 30; motion.VX = 0; motion.VY = -300;
            Keepsake(0.2);
            check(pet.Bonk > 0 && pet.Action == PetAction.Waking && !pet.Emotions.SleepLocked && pet.Emotions.WakeCooldownRemaining > 25 && motion.VY > 0,
                "Dropped on a sleeping cat it wakes it (waking, then the wake cooldown) dazed with stars; the keepsake bounces off");
            Keepsake(PetModel.BonkDuration);
            check(pet.Bonk == 0 && !pet.Emotions.SleepLocked, "Then the stars are gone and the cat stays awake");
            Keepsake(3);
            window.Reset();
            pet.Action = PetAction.Idle; pet.Emotions.Reset();
            Keepsake(3);

            // Left at the edge (as if the user dropped it there): startled cat, puff, back to the middle.
            pet.X = middleX - 400; pet.Action = PetAction.Idle;
            SetField(window, "keepsakeHeld", true); motion.Held = true;
            motion.X = area.Right - spot.Width - 5; motion.Height = 0;
            Call(window, "EndKeepsakeDrag", false);
            Keepsake(0.1);
            check(window.KeepsakeHopping && pet.Emotions.Target == PetEmotion.Surprised, "Dropped at the screen edge: the cat is startled and the keepsake starts its hop");
            Keepsake(0.5);
            check(window.KeepsakeHopping && Math.Abs(motion.X - (area.Right - spot.Width - 5)) < 1 && pet.Facing == 1 && pet.VX == 0, "It trembles in place while the cat looks at it");
            Keepsake(0.35);
            Shot("Pet trophies", "trophies-puff.png");
            Keepsake(0.25);
            Shot("Pet trophies", "trophies-appear.png");
            Keepsake(1.9);
            var hopped = window.TrophyBounds;
            check(!window.KeepsakeHopping && motion.Resting && !KeepsakeMotion.NearEdge(hopped.Left, hopped.Width, area.Left, area.Right) && Math.Abs(hopped.Left - middleX) < 1,
                $"It vanished in a puff and popped up in the middle ({hopped.Left:F0}, middle {middleX:F0})");
            pet.X = middleX - 40; pet.Action = PetAction.Idle; pet.Emotions.Reset();
            motion.X = area.Left + 3;
            SetField(window, "keepsakeEdgeCheck", true);
            Keepsake(3);
            hopped = window.TrophyBounds;
            check(!window.KeepsakeHopping && (hopped.Right < pet.X + 10 || hopped.Left > pet.X + 102) && !KeepsakeMotion.NearEdge(hopped.Left, hopped.Width, area.Left, area.Right),
                $"With the cat in the middle it lands next to the cat, not on it ({hopped.Left:F0}–{hopped.Right:F0}, cat {pet.X:F0})");
            pet.Emotions.Reset();

            // The cat shoves it along: walks to one side, crouches, pushes, watches it slide.
            window.Reset(); pet.Emotions.Reset();
            motion.X = pet.X + 200; motion.Height = 0; motion.VX = 0;
            Keepsake(0.1);
            prefs.Quiet = true; behavior.Quiet = true;
            check(!window.ShoveKeepsake(1), "In Quiet the cat does not shove the keepsake");
            prefs.Quiet = false; behavior.Quiet = false;
            double keepsakeBefore = motion.X;
            check(window.ShoveKeepsake(1) && window.KeepsakeShoving, "The cat may go to shove its keepsake");
            Call(window, "TickFeatures", 1.0 / 60);
            check(pet.Action == PetAction.Walking && pet.VX > 0, "It walks to the keepsake's left side");
            pet.X = motion.X - 112 * 0.8;
            Keepsake(0.2, features: true);
            check(pet.PlayCrouch > 0 && pet.VX == 0 && pet.Facing == 1 && motion.VX == 0, "Beside it, it crouches facing the keepsake");
            Keepsake(0.4, features: true);
            check(motion.VX > 0 && window.KeepsakeShoving && pet.Action == PetAction.Looking, "Then it shoves: the keepsake slides away and the cat watches");
            Keepsake(2, features: true);
            check(!window.KeepsakeShoving && motion.Resting && motion.X - keepsakeBefore > 50, $"The keepsake slid {motion.X - keepsakeBefore:F0} DIPs; the cat goes back to its business");
            check(window.ShoveKeepsake(1), "(setup) another shove");
            window.Reset();
            check(!window.KeepsakeShoving, "Bringing the pet back ends the shove");
            Keepsake(3);

            // Swapping from the right-click menu: a puff, the new one where the old one stood.
            check(keepsake!.ContextMenu != null, "The keepsake has a right-click menu");
            double centerBefore = window.TrophyBounds.Left + window.TrophyBounds.Width / 2;
            check(window.SwapDisplayed("slime_cushion") && window.KeepsakeHopping && !window.SwapDisplayed("cup_silver"), "Choosing another keepsake starts the puff (one swap at a time)");
            Keepsake(0.2);
            check(keepsake.Items[0].Id == "cup_silver", "During the puff the old one is still there");
            Keepsake(1.5);
            var swapped = window.TrophyBounds;
            check(!window.KeepsakeHopping && keepsake.Items.Select(k => k.Id).SequenceEqual(new[] { "slime_cushion" }) && prefs.TrophyShown == "slime_cushion"
                  && Math.Abs(swapped.Left + swapped.Width / 2 - centerBefore) < 1 && motion.Resting && Math.Abs(swapped.Bottom - area.Bottom) < 1,
                $"The cushion pops up where the cup stood and is remembered ({swapped.Left + swapped.Width / 2:F0} vs {centerBefore:F0})");
            check(!window.SwapDisplayed("slime_cushion") && !window.SwapDisplayed("cup_gold"), "Not the one shown, not one not owned");
            Call(window, "StartKeepsakeHop", false);
            Keepsake(0.1);
            check(window.KeepsakeHopping && pet.Emotions.Target != PetEmotion.Surprised, "\"Back to the middle\": the puff, the cat not startled");
            Keepsake(2);
            check(!window.KeepsakeHopping && (Math.Abs(window.TrophyBounds.Left - (area.Left + area.Right - window.TrophyBounds.Width) / 2) < 1
                  || window.TrophyBounds.Right < pet.X + 10 || window.TrophyBounds.Left > pet.X + 102), "It comes back to the middle (beside the cat if the cat stands there)");

            // The cat naps on the cushion: walks over, sleeps on it (drawn lifted), and it ends with the sleep.
            pet.X = area.Left + 60; pet.Action = PetAction.Idle; pet.Emotions.FinishSleep(0);
            Keepsake(0.5);
            prefs.Quiet = true; behavior.Quiet = true;
            check(!window.GoNapOnCushion(), "In Quiet it does not go to nap on the cushion");
            prefs.Quiet = false; behavior.Quiet = false;
            check(window.GoNapOnCushion() && window.GoingToNap, "With the cushion on display the cat may go to nap on it");
            Call(window, "TickFeatures", 1.0 / 60);
            check(pet.Action == PetAction.Walking && pet.VX > 0, "It walks to the cushion");
            pet.X = window.TrophyBounds.Left + window.TrophyBounds.Width / 2 - 56;
            Call(window, "TickFeatures", 1.0 / 60);
            Keepsake(0.6);
            check(window.NappingOnCushion && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && window.NapLift > 15,
                $"On it, it falls asleep, drawn on top of the cushion (lift {window.NapLift:F1})");
            behavior.WakeByClick(pet);
            Keepsake(0.1);
            check(pet.Action == PetAction.Waking && window.NappingOnCushion, "Woken, it wakes up on the cushion");
            pet.Action = PetAction.Idle;
            Keepsake(0.6);
            check(!window.NappingOnCushion && window.NapLift == 0, "Awake, it steps down");
            pet.Emotions.FinishSleep(0);

            // "Do now → Sleep on the cushion": only close to it; also in Quiet.
            double cushionCenter = window.TrophyBounds.Left + window.TrophyBounds.Width / 2;
            pet.X = Math.Max(area.Left, cushionCenter - 56 - 400); pet.Action = PetAction.Idle;
            check(!window.CushionNear && !window.SleepOnCushionNow(), "Far from the cushion \"Sleep on the cushion\" does nothing (greyed out)");
            pet.X = cushionCenter - 56 - 150;
            prefs.Quiet = true; behavior.Quiet = true;
            check(window.CushionNear && window.SleepOnCushionNow() && window.GoingToNap, "Close to it the cat goes to sleep on the cushion, even in Quiet");
            Call(window, "TickFeatures", 1.0 / 60);
            check(pet.Action == PetAction.Walking && pet.VX > 0, "It walks the few steps to the cushion");
            pet.X = cushionCenter - 56;
            Call(window, "TickFeatures", 1.0 / 60);
            Keepsake(0.3);
            check(window.NappingOnCushion && pet.Action == PetAction.Sleeping, "And sleeps on it");
            prefs.Quiet = false; behavior.Quiet = false;
            behavior.WakeByClick(pet); pet.Action = PetAction.Idle;
            Keepsake(0.6);
            pet.Emotions.FinishSleep(0);
            check(Call(window, "SleepOnCushionInstead", pet) is true && window.GoingToNap, "A sleep the cat picks on its own goes to the cushion instead");
            window.Reset(); pet.Emotions.FinishSleep(0);
            Keepsake(0.5);

            // Thrown onto the slime cushion: a trampoline, a stronger bounce than the floor's, to the side.
            cushionCenter = window.TrophyBounds.Left + window.TrophyBounds.Width / 2;
            double surface = area.Bottom - motion.Height - (KeepsakeArt.StandHeight + KeepsakeArt.CushionHeight - 3) * PetWindow.DisplayScale;
            void ThrowOntoKeepsake()
            {
                pet.X = cushionCenter - 56 + 10; pet.Y = surface - 112 + 2; pet.VY = 600; pet.VX = 0; pet.Grounded = false; pet.Action = PetAction.Thrown;
                motion.SquashV = 0;
                Call(window, "BounceOnCushion", surface - 3);
            }
            ThrowOntoKeepsake();
            check(pet.VY < -600 && pet.VX > 0 && motion.SquashV > 2 && pet.Y + 112 <= surface + 0.01,
                $"Thrown onto the cushion the cat bounces up harder than it fell (floor: ×0.58) and a little aside (VY {pet.VY:F0}, VX {pet.VX:F0})");
            window.Reset(); pet.Emotions.Reset();

            // The ball rolling into the keepsake bounces back and the keepsake wobbles.
            window.ToggleToy();
            var toy = (ToyBall)FieldOf("ball").GetValue(window)!;
            toy.X = window.TrophyBounds.Left - 8; toy.Height = 0; toy.VX = 220; toy.VY = 0;
            motion.TiltV = 0;
            Call(window, "KeepsakeMeetsBall");
            check(toy.VX < 0 && toy.X <= window.TrophyBounds.Left - 12 && motion.TiltV != 0, "The toy ball bounces off the keepsake, which wobbles");
            check(FieldOf("ballArt").GetValue(window) is YarnBallArt, "The toy ball is drawn as a ball of yarn");
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string yarnReview)
            {
                var yarn = new YarnBallArt { Width = 96, Height = 96 };
                yarn.Measure(new Size(96, 96)); yarn.Arrange(new System.Windows.Rect(0, 0, 96, 96));
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(96, 96, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(yarn);
                var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var yarnFile = File.Create(Path.Combine(yarnReview, "yarn-ball.png")); png.Save(yarnFile);
            }
            window.ToggleToy();
            Keepsake(2);

            // Hover wiggle, double click calls the cat over (even in Quiet).
            motion.SquashV = 0;
            Call(window, "HoverKeepsake");
            check(motion.SquashV < 0, "The cursor arriving makes it wiggle");
            Keepsake(1);
            pet.X = Math.Max(area.Left, window.TrophyBounds.Left - 400); pet.Action = PetAction.Idle;
            prefs.Quiet = true; behavior.Quiet = true;
            check(window.CallCatToLook() && window.Admiring, "A double click calls the cat to look, even in Quiet");
            Call(window, "TickFeatures", 1.0 / 60);
            check(window.Admiring && pet.Action == PetAction.Walking, "It walks over to look (Quiet does not stop a look the user asked for)");
            prefs.Quiet = false; behavior.Quiet = false;
            window.Reset();

            // While the user holds it, the cat watches it; landing close by startles the cat.
            prefs.TrophyShown = null; Call(window, "TickTrophies"); Keepsake(1);
            cushionCenter = window.TrophyBounds.Left + window.TrophyBounds.Width / 2;
            surface = area.Bottom - motion.Height - (KeepsakeArt.StandHeight + KeepsakeArt.CushionHeight - 3) * PetWindow.DisplayScale;
            ThrowOntoKeepsake();
            check(pet.VY == 600 && motion.SquashV == 0, "A cup is no trampoline (only the slime cushion)");
            check(Call(window, "SleepOnCushionInstead", pet) is false, "With a cup on display the cat sleeps where it is");
            window.Reset(); pet.Emotions.Reset(); Keepsake(0.5);
            pet.Action = PetAction.Idle; pet.Emotions.Reset();
            motion.X = pet.X + 300; motion.Height = 150;
            // (Not ticked while held: the drag would follow the real cursor.)
            SetField(window, "keepsakeHeld", true);
            Call(window, "WatchHeldKeepsake");
            SetField(window, "keepsakeHeld", false);
            Keepsake(0.1);
            check(pet.Action == PetAction.Looking && pet.Facing == 1 && pet.VX == 0, "Held up on its right, the cat turns and watches it");
            Keepsake(3);
            pet.Emotions.Reset(); pet.Action = PetAction.Idle;
            SetField(window, "keepsakeStartleAt", 0.0);
            motion.X = pet.X + 56 + 60; motion.Height = 250; motion.VX = motion.VY = 0;
            Keepsake(0.6);
            check(pet.Emotions.Target == PetEmotion.Surprised && pet.Bonk == 0, "Dropped hard right beside the cat (not on it): it jumps (Surprised)");
            Keepsake(3);

            // Thrown sideways into the cat: bonk too, and it bounces back.
            window.Reset(); pet.Action = PetAction.Idle; pet.Emotions.Reset();
            motion.X = pet.X - 150; motion.Height = 0; motion.VY = 0; motion.VX = 1200;
            Keepsake(0.4);
            check(pet.Bonk > 0 && motion.VX <= 0, $"Thrown hard into the cat's side it bonks the cat and bounces back (VX {motion.VX:F0})");
            Keepsake(3);
            window.Reset(); pet.Action = PetAction.Idle; pet.Emotions.Reset();

            // On other windows: lands on a window's top edge, rides along, falls when it goes.
            var probe = FieldOf("windowProbe").GetValue(window);
            var rectProbe = FieldOf("windowRectProbe").GetValue(window);
            var appWindow = new WindowSnapshot(4242, middleX - 200, area.Bottom - 300, middleX + 260, area.Bottom - 60, true);
            bool appOpen = true;
            IReadOnlyList<WindowSnapshot> Windows() => appOpen ? new[] { appWindow } : Array.Empty<WindowSnapshot>();
            SetField(window, "windowProbe", new Func<IReadOnlyList<WindowSnapshot>>(Windows));
            SetField(window, "windowRectProbe", new Func<long, WindowSnapshot?>(id => appOpen && id == appWindow.Id ? appWindow : null));
            SetField(window, "ledges", new List<Ledge>());
            SetField(window, "ledgeScan", 0.0);
            pet.X = area.Left + 40;
            Call(window, "TickLedges", 1.0 / 60);
            motion.X = middleX; motion.Height = 420; motion.VX = motion.VY = 0;
            double ledgeSquash = 0;
            bool ledgeBounced = false;
            for (double t = 0; t < 2.5; t += 1.0 / 60)
            {
                Keepsake(1.0 / 60);
                ledgeSquash = Math.Max(ledgeSquash, motion.Squash);
                ledgeBounced |= window.KeepsakeLedgeId == 4242 && motion.VY > 0;
            }
            check(ledgeSquash > 0.1 && ledgeBounced, $"On the window edge it lands with a squash and a bounce, like on the floor (squash {ledgeSquash:F2})");
            check(window.KeepsakeLedgeId == 4242 && Math.Abs(motion.Height - 300) < 1 && motion.Resting && Math.Abs(window.TrophyBounds.Bottom - (area.Bottom - 300)) < 1,
                $"Dropped above an app window it lands on its top edge (height {motion.Height:F0}, ledge {window.KeepsakeLedgeId})");
            // The cat on the same window edge plays with it there; from the floor it does not.
            pet.LedgeId = 4242; pet.Grounded = true; pet.Y = area.Bottom - 300 - 112; pet.X = motion.X - 200; pet.Action = PetAction.Idle; pet.Emotions.FinishSleep(0);
            check(window.AdmireTrophies(), "On the same window as the keepsake the cat may go and look at it");
            Call(window, "StopAdmiring");
            check(window.ShoveKeepsake(1), "...and shove it (off the edge, if it slides that far)");
            Call(window, "StopKeepsakeShove");
            pet.LedgeId = 0; pet.Y = area.Bottom - 112; pet.Action = PetAction.Idle;
            check(!window.AdmireTrophies() && !window.ShoveKeepsake(1), "From the floor it leaves a keepsake up on a window alone");
            double onWindowX = motion.X;
            appWindow = appWindow with { Left = appWindow.Left + 50, Right = appWindow.Right + 50, Top = appWindow.Top - 20, Bottom = appWindow.Bottom - 20 };
            Keepsake(0.1);
            check(Math.Abs(motion.X - onWindowX - 50) < 1 && Math.Abs(motion.Height - 320) < 1, "The window dragged: the keepsake rides along");
            appOpen = false;
            Keepsake(3);
            check(window.KeepsakeLedgeId == 0 && motion.Height == 0 && motion.Resting, "The window closed: the keepsake falls to the floor");
            SetField(window, "windowProbe", probe);
            SetField(window, "windowRectProbe", rectProbe);
            Call(window, "DropLedges");
            window.Reset();
            Keepsake(1);
            window.BattleTuning = easy;

            // The built-in Expedition's map at its tallest (3 relics, a rest's cards, both languages) fits
            // under the top margin of a 1280×720 screen at 150 % (480 DIPs tall).
            window.BattleTuning = BattleData.Settings;
            window.AdventureProgress.Completed.Add("c1_slime_king");
            check(window.OpenExpeditionMap() == null && window.ExpeditionStart(11) == null, "(setup) a run of the built-in Expedition");
            var tall = window.Expedition!.Run!;
            tall.Relics.AddRange(window.Expedition.Spec.Relics.Take(window.Expedition.Spec.MaxRelics).Select(r => r.Id));
            // Spin step by step (passing each on paper) until a step has a rest; the one before the boss always does.
            var drawing = window.Expedition;
            for (int guard = 0; guard < 6; guard++)
            {
                drawing.Roll();
                int rest = Array.IndexOf(tall.Kinds![tall.Step], ExpeditionSpec.Rest);
                if (rest >= 0 && tall.Step >= 2) { drawing.Visit(rest); break; }
                tall.Path.Add(0);
            }
            var boundsField = FieldOf("bounds");
            var realBounds = (Area)boundsField.GetValue(window)!;
            double MapHeight(string language) { prefs.Language = language; Tick(0.3); Call(window, "PlaceExpeditionMap"); var m = Find("Pet expedition")!; m.UpdateLayout(); return m.ActualHeight; }
            double natural = Math.Max(MapHeight("en"), MapHeight("vi"));
            Shot("Pet expedition", "expedition-map-tallest.png");
            boundsField.SetValue(window, realBounds with { Bottom = realBounds.Top + 480 });
            double capped = Math.Max(MapHeight("vi"), MapHeight("en")); // English last: the button is found by its English label.
            var small = Find("Pet expedition")!;
            static IEnumerable<DependencyObject> All(DependencyObject node) => LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().SelectMany(All).Prepend(node);
            var scroller = All(small).OfType<ScrollViewer>().FirstOrDefault();
            var mainButton = Button((DependencyObject)small.Content, "Give up")!; // The bottom row (the main button waits for the rest's choice).
            var buttonBottom = mainButton.TransformToAncestor(small).Transform(new Point(0, mainButton.ActualHeight)).Y;
            check(tall.Pending?.Kind == "rest" && capped <= 480 - 24 + 0.5 && scroller is { ScrollableHeight: > 0 } && buttonBottom <= capped,
                $"On a screen 480 DIPs tall (1280×720 at 150 %) the tallest built-in map is capped ({natural:0} → {capped:0} DIPs): the content scrolls, the buttons stay in view");
            Shot("Pet expedition", "expedition-map-small-screen.png");
            boundsField.SetValue(window, realBounds);
            check(MapHeight("en") is double back && Math.Abs(back - natural) < 1 || realBounds.Bottom - realBounds.Top < natural + 24, "On a big enough screen the map keeps its full height");
            prefs.Language = "en";
            window.Reset();

            // The step gacha and the capsule machine on the map (2026-10-22): a 2-step trail of fights only
            // (the step before the boss gets its rest), weak slimes.
            var gachaTrail = new ExpeditionSpec
            {
                Id = "gacha_trail", NameVi = "Đường quay", NameEn = "Spin trail", UnlockAfter = "st_gate", RetreatCost = 0.25, WinHeal = 0, StepThreat = 0,
                KindGacha = new KindGachaSpec { Steps = 2, MinLanes = 2, MaxLanes = 2, Weights = new() { [ExpeditionSpec.Fight] = 1 }, RerollCost = 0.08 }, Capsules = new CapsuleSpec(),
                Fights = new[] { Stage("g_field", "Field"), Stage("g_hill", "Hill") }, Boss = Stage("g_throne", "Throne")
            };
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, SlimeMaxHp = 1, SlimeDamage = 1, OutroSeconds = 0.5, Campaign = campaign, Expedition = gachaTrail };
            check(window.OpenExpeditionMap() == null && window.ExpeditionStart(21) == null, "(setup) a run of a trail with the step gacha");
            Tick(0.3);
            string spin = window.ExpeditionMapText;
            check(window.Expedition!.NeedsRoll && spin.Contains("Step 1/3 · Step machine") && spin.Contains("Odds per node") && spin.Contains("no elite or rest") && spin.Contains("Spin")
                  && window.ExpeditionMainEnabled && window.ExpeditionFight() is string notYet && notYet.Length > 0,
                $"A new run's map waits for the spin, with its odds; nothing can be fought yet ({spin.Replace('\n', '|')})");
            Shot("Pet expedition", "expedition-spin-en.png");
            check(window.ExpeditionRoll() == null && window.ExpeditionRevealing && !window.ExpeditionMainEnabled && window.ExpeditionRoll() is string twiceSpun && twiceSpun.Length > 0
                  && Saved()?.Kinds?[0].Length == 2, "Spin: the step is drawn and saved at once, the capsule opens (nothing else meanwhile), a second spin draws nothing");
            Tick(0.3);
            check(window.ExpeditionMapText.Contains("Opening the capsule"), "While it opens the map says so");
            window.FinishExpeditionReveal();
            Tick(0.3);
            check(!window.ExpeditionRevealing && window.ExpeditionMapText.Contains("Fight — ") && window.ExpeditionMainEnabled, "Once open, the step's nodes show and can be fought");
            double beforeReroll = window.Expedition!.Run!.Hp;
            check(window.ExpeditionMapText.Contains("↻ Spin this step again · −8 HP"), "A drawn step offers one more spin for 8% of the max HP");
            Shot("Pet expedition", "expedition-reroll-en.png");
            check(window.ExpeditionReroll() == null && window.ExpeditionRevealing && !window.ExpeditionMapText.Contains("Spin this step again")
                  && Saved() is { Rerolled: [0] } saved && saved.Hp == beforeReroll - 8, "Spinning again: paid, saved at once, the capsule opens, the offer is gone");
            window.FinishExpeditionReveal();
            Tick(0.3);
            check(!window.ExpeditionMapText.Contains("Spin this step again") && window.ExpeditionReroll() is string noMore && noMore.Length > 0 && window.ExpeditionMainEnabled,
                "Once a step: no second offer, the new nodes can be fought");
            check(window.ExpeditionFight() == null && window.BattleActive, "(setup) the first fight");
            Finish();
            await Task.Delay(30);
            check(window.LastBattleSummary is { Expedition.Capsule: true } && ResultText().Contains("capsule machine waits"), "A won fight's result says the capsule machine waits on the map");
            Click(Button(Find("Pet result")!.Content as DependencyObject ?? new Border(), "Continue")!);
            Tick(0.3);
            string machine = window.ExpeditionMapText;
            check(window.ExpeditionHasChoice && machine.Contains("Capsule machine") && machine.Contains("Plain capsule · free") && machine.Contains("Gold capsule · −12 HP") && machine.Contains("Skip")
                  && window.ExpeditionOptionEnabled(Expedition.Free) && window.ExpeditionOptionEnabled(Expedition.Gold),
                $"On the map: the plain capsule (free) and the gold one (12% of the max HP), with their odds ({machine.Replace('\n', '|')})");
            Shot("Pet expedition", "expedition-capsules-en.png");
            var capsuleRun = window.Expedition!.Run!;
            string gold = capsuleRun.Pending!.Gold!;
            double capsuleHp = capsuleRun.Hp;
            check(window.ExpeditionChoose(Expedition.Gold) == null && window.ExpeditionRevealing && !window.ExpeditionMapText.Contains("Gold capsule: ") && Saved()?.Pending == null,
                "Taking the gold capsule: the run changes and is saved at once, the prize is said only once it opens");
            window.FinishExpeditionReveal();
            Tick(0.3);
            check(window.ExpeditionMapText.Contains("Gold capsule: ") && window.Expedition!.Run!.Hp < capsuleHp + 30 && window.Expedition.NeedsRoll,
                $"Once open, the map says what it held ({gold}); then step 2 waits for its spin ({window.ExpeditionMapText.Replace('\n', '|')})");
            window.ExpeditionRoll(); window.FinishExpeditionReveal(); Tick(0.3);
            check(window.Expedition!.Run!.Kinds![1].Contains(ExpeditionSpec.Rest), "The step before the boss has its rest");
            window.ExpeditionAbandon(); window.ExpeditionAbandon();
            window.Reset();

            // Data without an Expedition: the menu hides it, opening explains.
            window.BattleTuning = new BattleSettings { Campaign = campaign };
            check(window.OpenExpeditionMap() is string none && none.Contains("no Expedition") && !window.ExpeditionMapOpen, "Battle data without an Expedition: it explains and opens nothing");
        }
        finally
        {
            window.Reset();
            window.ExpeditionProgressFile = null;
            window.ReloadExpeditionProgress();
            window.ReloadAdventureProgress();
            window.BattleTuning = tuningBefore;
            prefs.Language = language; prefs.BattleWeapon = weapon; prefs.BattleDifficulty = level;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
