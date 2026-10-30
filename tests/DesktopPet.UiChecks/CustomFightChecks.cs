using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopPet;
using static PetPrivate;

// 1.5 M29a (docs/UPDATE-1.5.md F1): the Custom Fight in the real pet window: the hub's button, the setup
// panel's presets (Default is the old free battle), the builder (locks from the Monster Book, edits saved,
// fullscreen), a preset's battle (no records, no achievements, no Adventure progress) and Play again.
// In-memory preferences, book and presets; temporary files only.
internal static class CustomFightChecks
{
    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        FieldOf("contextProbe").SetValue(window, new Func<(bool, double)>(() => (fullscreen, idle)));
    private static Window? Find(string title) => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == title && w.IsLoaded);
    private static int Count(string title) => Application.Current.Windows.OfType<Window>().Count(w => w.Title == title && w.IsLoaded);
    private static Border? Button(DependencyObject node, string label)
    {
        if (node is Border { Child: StackPanel panel } border && panel.Children.OfType<TextBlock>().Any(t => t.Text == label)) return border;
        if (node is Border { Child: TextBlock text } plain && text.Text == label) return plain;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            if (Button(child, label) is Border found) return found;
        return null;
    }
    private static void Click(UIElement element) =>
        element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
    private static string Text(DependencyObject node) => string.Join("|", Texts(node));
    private static IEnumerable<string> Texts(DependencyObject node)
    {
        if (node is TextBlock { Visibility: Visibility.Visible } t && t.Text.Length > 0) yield return t.Text;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            foreach (var s in Texts(child)) yield return s;
    }
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
        void Prep(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattlePrep", 1.0 / 60); }
        void Fight(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattle", 1.0 / 60); }
        void Finish()
        {
            for (int i = 0; i < 800 && window.BattleActive; i++)
            {
                if (window.Battle is { } b) foreach (var s in b.Slimes) s.X = pet.X + 106;
                Fight(0.05);
            }
        }
        string language = prefs.Language, weapon = prefs.BattleWeapon, level = prefs.BattleDifficulty;
        var tuningBefore = window.BattleTuning;
        string dir = Path.Combine(Path.GetTempPath(), "pet-custom-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        prefs.Language = "en"; prefs.BattleWeapon = BattleWeapon.Sword; prefs.BattleDifficulty = "Normal";
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        window.Reset();
        // Two kinds with Monster Book pages (one met), one-hit slimes that never hurt the cat.
        EnemyType Kind(string id, string name) => new() { Id = id, NameVi = name, NameEn = name, MaxHp = 1, Damage = 0, Speed = 40, TipVi = "t", TipEn = "t", Color = "#7CD36A" };
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, OutroSeconds = 0.5, MaxAlive = 3, Enemies = new[] { Kind("slime", "Slime"), Kind("runner", "Quick slime") } };
        window.AdventureProgressFile = Path.Combine(dir, "adventure-progress.json");
        window.ReloadAdventureProgress();
        var book = new MonsterBook { Seeded = true, AppsSeeded = true };
        book.Add(window.BattleTuning, new[] { "slime" }, new Dictionary<string, int>(), Array.Empty<string>(), out _);
        SetField(window, "monsterBook", book);
        var fights = new CustomFightBook();
        SetField(window, "customFights", fights);
        var trophies = new AchievementBook();
        SetField(window, "achievementBook", trophies);
        int Records() => window.BattleRecords.Loadouts.Values.Sum(r => r.Wins + r.MostDefeated) + window.BattleRecords.Normal.Wins + window.BattleRecords.Hard.Wins;
        try
        {
            // The hub's Custom Fight button opens the free battle's setup panel with the presets.
            check(window.OpenBattleHub() == null, "The hub opens");
            await Task.Delay(30);
            var hub = Find("Pet battle hub")!;
            check(Button(hub, "Custom Fight") is { IsVisible: true } customButton, "The hub has a Custom Fight button");
            Click(Button(hub, "Custom Fight")!);
            await Task.Delay(30);
            Prep(0.3);
            check(window.BattlePreparing && !window.BattleHubOpen && window.BattlePrepText.Contains("Get ready to battle") && window.ChosenFight == null
                  && Text(Find("Pet setup")!).Contains("Custom Fight|Default|The old free battle") && Text(Find("Pet setup")!).Contains("Build a fight…"),
                $"It opens the setup panel: Default chosen, what it is, Build a fight ({Text(Find("Pet setup")!)})");
            Shot("Pet setup", "custom-fight-setup-en.png");

            // Default is the old free battle: no stage, its records as before.
            int records = Records();
            check(window.BattlePrepReady() == null && window.Battle is { Stage: null }, "Ready with Default starts the old free battle (no stage)");
            Finish();
            await Task.Delay(30);
            check(window.LastBattleSummary is { Won: true, Stage: null } && Records() > records, "Default's win goes to the free battle records as before");
            Call(window, "CloseBattleSummary");

            // The builder: Default cannot change; + New set copies it with only what the book has met.
            check(window.OpenBattlePrep() == null && window.OpenFightBuilder() == null, "Build a fight opens the builder");
            await Task.Delay(30);
            Prep(0.2);
            check(window.FightBuilderVisible && !window.BattlePrepVisible && Count("Pet custom fight") == 1 && window.FightBuilderText.Contains("The Default set cannot be changed"),
                "The builder stands in the setup panel's place; Default is read-only");
            check(window.FightCycleEnemy("slime") == CustomFightBlock.Locked && window.ChosenFight == null, "Default's slimes cannot be changed");
            var set = window.FightAdd();
            string text = window.FightBuilderText;
            check(set is { Name: "Set 1", Boss: null } && set.Enemies.Keys.SequenceEqual(new[] { "slime" }) && window.ChosenFight == set && fights.Presets.Count == 1
                  && text.Contains("Quick slime") == false && text.Contains("???") && text.Contains("No boss") && text.Contains("Waves"),
                $"+ New set: a copy of Default with only the met slime (the rest ??? tiles) ({text.Replace('\n', '|')})");
            check(window.FightCycleEnemy("runner") == CustomFightBlock.Locked && window.FightNote!.Contains("Not met yet"), "A ??? tile says it is not met yet");
            check(window.FightCycleEnemy("slime") == CustomFightBlock.None && set.Enemies["slime"] == 3 && window.FightCycleEnemy("slime") == CustomFightBlock.None && !set.Enemies.ContainsKey("slime"),
                "A slime tile goes Some → Many → Off");
            Prep(0.2);
            check(window.BattlePrepReady() is string empty && empty.Contains("no enemy") && !window.BattleActive, "A set with nothing to fight: Ready says why");
            window.FightCycleEnemy("slime");
            check(window.FightStep(PetWindow.FightWaves, -1) && set.Waves == 2 && window.FightStep(PetWindow.FightPerWave, -1) && set.PerWave == 2
                  && window.FightStep(PetWindow.FightPerWave, -1) && set.PerWave == 2 && window.FightStep(PetWindow.FightElites, 1) && set.Elites == 10 && window.FightToggleCards() && set.Cards,
                "The numbers step within their limits; cards turn on");
            check(!window.FightToggleIcon("tabs") && !set.Icons.Contains("tabs"), "An app not met cannot be chosen");
            book.AddApps(new[] { "tabs", "ads" }, out _);
            check(window.FightToggleIcon("tabs") && window.FightCycleMalware("ads") && set.Malware["ads"] == 1 && window.FightBuilderText.Contains("Ads: Few"), "Met apps can be chosen");
            window.FightToggleIcon("tabs"); window.FightCycleMalware("ads"); window.FightCycleMalware("ads"); window.FightCycleMalware("ads");
            Shot("Pet custom fight", "custom-fight-builder-en.png");

            // Fullscreen hides the builder; it comes back after.
            SetContext(window, true, 0); Call(window, "TickContext", 1.1); Prep(0.2);
            check(!window.FightBuilderVisible && !window.BattlePrepVisible && window.BattlePreparing, "A fullscreen app hides the builder (and the panel)");
            SetContext(window, false, 0); Call(window, "TickContext", 1.1); Prep(0.2);
            check(window.FightBuilderVisible && !window.BattlePrepVisible, "It comes back when the fullscreen app closes");
            window.CloseFightBuilder();
            Prep(0.2);
            check(!window.FightBuilderOpen && window.BattlePrepVisible && Text(Find("Pet setup")!).Contains("2 waves · 5 enemies (1 kind)"),
                $"Done: back to the setup panel, the set's summary under its chip ({Text(Find("Pet setup")!)})");

            // A preset's battle: a Custom stage; no records, no achievements, no Adventure progress.
            records = Records();
            var report = window.BattleReport;
            check(window.BattlePrepReady() == null && window.Battle is { Stage: { Custom: true, Upgrades: true } stage } && stage.Waves.Length == 2 && window.Battle.Skills.Count == 5,
                "Ready starts the chosen set's battle (its waves, cards, five skills)");
            int earnedBefore = trophies.Earned.Count;
            double totalsBefore = trophies.Totals.Values.Sum();
            Finish();
            for (int i = 0; i < 20 && window.Battle?.ChoosingUpgrade == true; i++) { window.Battle.ChooseUpgrade(window.Battle.UpgradeOffer![0].Id); Finish(); }
            await Task.Delay(30);
            string panel = Find("Pet result") is { } result ? Text(result) : "";
            check(window.LastBattleSummary is { Won: true, Stage.Custom: true } && Records() == records && trophies.Earned.Count == earnedBefore && trophies.Totals.Values.Sum() == totalsBefore
                  && window.AdventureProgress.Runs.Count == 0 && window.BattleReport.Won == report.Won + 1,
                $"A Custom Fight's win: no records, no achievements, no Adventure progress (won {window.LastBattleSummary?.Won}, custom {window.LastBattleSummary?.Stage?.Custom}, records {Records()}/{records}, "
                + $"achievements {trophies.Earned.Count}/{earnedBefore} totals {trophies.Totals.Values.Sum()}/{totalsBefore}, runs {window.AdventureProgress.Runs.Count}, report {window.BattleReport.Won}/{report.Won})");
            check(panel.Contains("Custom Fight · Set 1") && panel.Contains("no records") && panel.Contains("Play again") && !panel.Contains("Records ("),
                $"Its result names the set and says nothing is kept ({panel})");
            Click(Button(Find("Pet result")!, "Play again")!);
            check(window.Battle is { Stage: { Custom: true, Id: var again } } && again == "custom:" + set.Id, "Play again starts the same set");
            window.EndBattleByUser();
            check(!window.BattleActive && !window.AdventureMapOpen, "End goes back to the desktop (no map)");

            // Delete: Default chosen again.
            window.OpenBattlePrep(); window.OpenFightBuilder();
            check(window.FightDelete() && window.ChosenFight == null && fights.Presets.Count == 0 && window.FightBuilderText.Contains("cannot be changed"), "Deleting the only set chooses Default");
            window.CancelBattlePrep();
            check(!window.BattlePreparing && Count("Pet custom fight") == 0 && Count("Pet setup") == 0 && !window.BattleOwnsPet, "Close removes the panel and the builder");
        }
        finally
        {
            window.EndBattle(); Call(window, "CloseBattleSummary"); window.CancelBattlePrep(); window.CancelBattleHub();
            window.BattleTuning = tuningBefore;
            SetField(window, "monsterBook", null); SetField(window, "customFights", null); SetField(window, "achievementBook", null);
            prefs.Language = language; prefs.BattleWeapon = weapon; prefs.BattleDifficulty = level;
            window.Reset();
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
