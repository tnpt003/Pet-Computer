using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopPet;

// Expedition milestones 2–4 in the real pet window: locked until the Adventure's gate stage is won →
// map → weapon/difficulty → run → step battle with the run's HP and build → result → Continue;
// End leaves a battle (RetreatCost), a battle cut off by closing/reset is charged when the map next
// opens (after a restart too), the boss ends the run, a loss ends it, giving up, custom data kept in
// memory; a won elite's relics, a rest kept over a restart, the relic in the next battle (milestone 3);
// keepsakes given once, the display corner and the cat looking at it (milestone 4).
// A temporary expedition file only; the Adventure progress stays in memory.
internal static class ExpeditionChecks
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static object? Call(PetWindow window, string name, params object[] args) => typeof(PetWindow).GetMethod(name, Flags)!.Invoke(window, args);
    private static void SetField(PetWindow window, string name, object? value) => typeof(PetWindow).GetField(name, Flags)!.SetValue(window, value);
    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, idle)));
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
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
            // Locked until the gate stage is won: the menu says so.
            window.ContextMenu!.IsOpen = true;
            await Task.Delay(30);
            var root = window.ContextMenu.Items.OfType<MenuItem>().First(i => i.Header as string == "Battle Mode");
            var item = root.Items.OfType<MenuItem>().FirstOrDefault(i => (i.Header as string ?? "").StartsWith("Expedition"));
            var items = root.Items.OfType<MenuItem>().Where(i => i.Visibility == Visibility.Visible).Select(i => i.Header as string ?? "").ToArray();
            check(items.Length == 3 && items[1] == "Expedition (win the Slime King to open)" && item is { IsEnabled: false }, $"Battle Mode lists the Expedition, locked at first ({string.Join(", ", items)})");
            window.ContextMenu.IsOpen = false;
            await Task.Delay(30);
            check(window.OpenExpeditionMap() is string locked && locked.Contains("Slime King") && !window.ExpeditionMapOpen, "A locked Expedition explains and opens nothing");
            window.AdventureProgress.Completed.Add("st_gate");
            window.ContextMenu.IsOpen = true;
            await Task.Delay(30);
            check(item is { IsEnabled: true } && item.Header as string == "Expedition", "Once the gate stage is won the menu opens the Expedition");
            window.ContextMenu.IsOpen = false;
            await Task.Delay(30);

            // The map before a run: one window, never takes focus, holds the pet's free time.
            check(window.OpenExpeditionMap() == null && window.ExpeditionMapOpen && !window.BattleActive, "The Expedition map opens; nothing fights");
            await Task.Delay(30);
            var map = Find("Pet expedition");
            long style = map == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(map).Handle, -20).ToInt64();
            check(map is { ShowActivated: false, ShowInTaskbar: false, IsVisible: true } && (style & 0x08000000) != 0, "The map is clickable, never takes focus and has no taskbar button");
            check(window.OpenExpeditionMap() == null && Count("Pet expedition") == 1 && behavior.Quiet && window.BattleOwnsPet, "Opening it again keeps the one map; the cat's free time belongs to it");
            Tick(0.3);
            string text = window.ExpeditionMapText;
            check(text.Contains("Expedition — Test trail") && text.Contains("No run yet · 3 steps") && text.Contains("Start the run") && text.Contains("Hammer") && text.Contains("Hard") && !text.Contains("Give up"),
                $"Before a run the map offers the weapon and difficulty for the run ({text.Replace('\n', '|')})");
            check(!File.Exists(file), "Opening the map saves nothing yet");

            // A run with the hammer on Hard: saved at once, the first step to fight.
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

            // Step 1: the elite node.
            check(window.ExpeditionSelect(1) && !window.ExpeditionSelect(2), "A node of the current step can be chosen (only those)");
            Tick(0.3);
            check(window.ExpeditionMapText.Contains("Elite — Fort"), "The chosen node's kind and encounter are shown");
            var node = window.Expedition!.Node(1)!.Value;
            check(window.ExpeditionFight() == null && window.BattleActive && window.BattleIsExpedition && window.Battle!.Stage?.Id == "x_fort" && window.Battle.Seed == node.Seed
                  && !window.ExpeditionMapOpen && Count("Pet expedition") == 0 && window.Battle.Weapon.Id == BattleWeapon.Hammer && window.Battle.Difficulty == BattleDifficulty.Hard,
                "Fight starts the node's own battle with the run's gear (the map closes)");
            check(Saved()?.Fighting == 1, "The battle under way is saved before it is fought");
            // The banner names the step and encounter and always fits the HUD's centre (a long one shrinks; the HUD keeps its width).
            var hud = (BattleHud)typeof(PetWindow).GetField("battleHud", Flags)!.GetValue(window)!;
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
            Click(Button(Find("Pet result")!.Content as DependencyObject ?? new Border(), "Continue")!);
            Tick(0.3);
            check(window.ExpeditionMapOpen && Count("Pet result") == 0 && window.ExpeditionMapText.Contains("Step 2/3"), "Continue goes back to the map at the next step");

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
            check(window.ExpeditionMapText.Contains("Viễn chinh — Đường thử") && window.ExpeditionMapText.Contains("Bắt đầu chuyến"), "The map follows the pet's language");
            Shot("Pet expedition", "expedition-map-vi.png");
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
            Click(Button(Find("Pet expedition")!.Content as DependencyObject ?? new Border(), "Take the chosen relic")!);
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
            window.CancelExpeditionMap();
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
            hud = (BattleHud)typeof(PetWindow).GetField("battleHud", Flags)!.GetValue(window)!;
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
            var shelfWindow = Find("Pet trophies");
            long shelfStyle = shelfWindow == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(shelfWindow).Handle, -20).ToInt64();
            check(window.TrophiesVisible && shelfWindow is { ShowActivated: false, ShowInTaskbar: false, Topmost: true } && (shelfStyle & 0x20) != 0 && (shelfStyle & 0x08000000) != 0,
                "The display corner shows: click-through, never takes focus, no taskbar button");
            var area = (Area)typeof(PetWindow).GetField("bounds", Flags)!.GetValue(window)!;
            var spot = window.TrophyBounds;
            var shelf = shelfWindow?.Content as TrophyShelf;
            check(spot.Right <= area.Right && spot.Right >= area.Right - 20 && Math.Abs(spot.Bottom - area.Bottom) < 1 && shelf != null
                  && shelf.Items.Select(k => k.Id).SequenceEqual(new[] { "slime_cushion", "cup_silver" }) && Math.Abs(spot.Width - TrophyShelf.WidthFor(shelf.Items)) < 0.5,
                $"Bottom right of the cat's monitor, standing on its floor: the cushion, then the silver cup ({spot}, floor {area.Bottom})");
            Shot("Pet trophies", "trophies-shelf.png");
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
            check(pet.Action == PetAction.Walking && pet.VX > 0, "It walks toward the shelf");
            pet.X = spot.Left - 112 * 0.72;
            Call(window, "TickFeatures", 1.0 / 60);
            check(pet.Action == PetAction.Looking && pet.Facing == 1 && pet.VX == 0 && window.Admiring, "Beside it, it stops and looks at the shelf");
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
            window.BattleTuning = easy;

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
