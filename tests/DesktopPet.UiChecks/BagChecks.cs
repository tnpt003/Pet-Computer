using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopPet;
using static PetPrivate;

// The Bag and the hub's Rewards in the real pet window, with the built-in data (and a small data
// set for the card offer). Rewards: the hub's button counts new rewards, the page shows tiles,
// records and "New" badges once, saving them as seen. Bag: opens from the Adventure map, the
// Expedition map (not while a capsule opens) and the battle HUD (the battle pauses for it, also over
// a card offer, and goes on after); three tabs; never takes focus; keeps where it was put; scrolls on
// a low screen; hides for a fullscreen app; closes with its battle or map. Temporary files only.
internal static class BagChecks
{
    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        FieldOf("contextProbe").SetValue(window, new Func<(bool, double)>(() => (fullscreen, idle)));
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
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
    // 2026-10-26 (user's screenshots): card tiles and relic slots fill the card's width, no empty strip on the right.
    private static bool TilesFillWidth(out string detail)
    {
        detail = "no tile grid";
        if (Find("Pet bag")?.Content is not FrameworkElement content) return false;
        content.UpdateLayout();
        var grids = Descendants(content).OfType<System.Windows.Controls.Primitives.UniformGrid>().Where(g => g.Children.Count > 0).ToList();
        if (grids.Count == 0) return false;
        foreach (var grid in grids)
        {
            var last = (FrameworkElement)grid.Children[Math.Min(grid.Columns, grid.Children.Count) - 1];
            double right = last.TranslatePoint(new Point(last.ActualWidth + last.Margin.Right, 0), grid).X;
            var holder = (FrameworkElement)VisualTreeHelper.GetParent(grid);
            detail = $"row right {right:F0}, grid {grid.ActualWidth:F0}, holder {holder.ActualWidth:F0}";
            if (grid.Children.Count >= grid.Columns && right < grid.ActualWidth - 1 || grid.ActualWidth < holder.ActualWidth - 1) return false;
        }
        return true;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
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
        void TickBag(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattleBag", 1.0 / 60); }
        void Fight(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) { Call(window, "TickBattle", 1.0 / 60); Call(window, "TickBattleBag", 1.0 / 60); } }
        string language = prefs.Language, weapon = prefs.BattleWeapon;
        var tuningBefore = window.BattleTuning;
        var data = BattleData.Settings;
        string dir = Path.Combine(Path.GetTempPath(), "pet-bag-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var boundsField = FieldOf("bounds");
        var realBounds = (Area)boundsField.GetValue(window)!;
        prefs.Language = "en"; prefs.BattleWeapon = BattleWeapon.Sword;
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        window.Reset();
        window.BattleTuning = data;
        string adventureFile = Path.Combine(dir, "adventure-progress.json");
        window.AdventureProgressFile = adventureFile;
        window.ExpeditionProgressFile = Path.Combine(dir, "expedition.json");
        window.ReloadAdventureProgress(); window.ReloadExpeditionProgress();
        try
        {
            // ── Rewards on the hub ──────────────────────────────────────────────────────────
            // Earlier checks fought battles in this window: start with no achievements (1.5 M28).
            FieldOf("achievementBook").SetValue(window, new AchievementBook());
            check(window.OpenBattleHub() == null, "The hub opens");
            await Task.Delay(30);
            var hub = Find("Pet battle hub")!;
            check(Button(hub, "Rewards") != null && Button(hub, "Bag") == null && window.BattleRewardsNew == 0, "The hub has a Rewards button (no Bag) and nothing new yet");
            Click(Button(hub, "Rewards")!);
            await Task.Delay(30);
            string text = window.BattleRewardsText;
            var accessories = data.Campaign!.Chapters.SelectMany(c => c.Stages).Where(s => s.Reward != null).Select(s => PetAccessories.Find(s.Reward)!.Name("en")).ToList();
            check(window.BattleRewardsShown && window.BattleHubText.StartsWith("Rewards") && Button(hub, "‹ Hub") != null && Count("Pet battle hub") == 1 && behavior.Quiet
                && text.Contains("Adventure accessories") && text.Contains("Expedition keepsakes") && accessories.All(a => text.Contains(a) && !text.Contains("✓ " + a))
                && data.Expedition!.Keepsakes.All(k => text.Contains(k.Name("en"))) && text.Contains("Records") && text.Contains("not won yet") && !text.Split('\n').Contains("New"),
                $"Rewards in the hub's window: locked tiles, keepsakes, records, no New badge ({text.Replace('\n', '|')})");
            Shot("Pet battle hub", "battle-rewards-empty-en.png");
            Click(Button(hub, "‹ Hub")!);
            await Task.Delay(30);
            check(!window.BattleRewardsShown && window.BattleHubModeEnabled(PetWindow.HubAdventure), "‹ Hub brings the mode cards back");

            // The whole chapter won: two accessories are new, the button counts them, the page marks them
            // (chapter 2's hat and plush stay locked, 1.5 M22).
            var adventure = window.Adventure!;
            var chapterOne = adventure.Chapter()!.Stages.Where(s => s.Reward != null).Select(s => PetAccessories.Find(s.Reward)!.Name("en")).ToList();
            var build = new Dictionary<string, int> { ["sharp"] = 2, ["thick_fur"] = 2, ["big_heart"] = 1 };
            int n = 0;
            foreach (var stage in adventure.Chapter()!.Stages) adventure.Apply("bag-" + n++, stage, true, true, BattleWeapon.Sword, BattleDifficulty.Normal, 10, 61, build);
            window.Expedition!.Progress.Keepsakes.Add(data.Expedition!.Keepsakes[0].Id);
            Call(window, "TickBattleHub", 0.3);
            int fresh = window.BattleRewardsNew;
            check(fresh == chapterOne.Count + 1 && Button(hub, $"Rewards · {fresh} new") != null, $"The button counts new rewards ({fresh})");
            window.ShowBattleRewards(true);
            text = window.BattleRewardsText;
            check(chapterOne.All(a => text.Contains("✓ " + a)) && text.Contains("✓ " + data.Expedition.Keepsakes[0].Name("en")) && text.Split('\n').Count(l => l == "New") == fresh
                && text.Contains("Adventure keepsakes") && text.Contains("Zip Slime plush") && !text.Contains("✓ Zip Slime plush") && !text.Contains("✓ Folder hat")
                && text.Contains("1:01") && text.Contains("1 win"),
                $"Won rewards are marked, each new one has a badge; records show the fastest win ({text.Replace('\n', '|')})");
            check(window.BattleRewardsNew == 0 && AdventureProgress.Load(adventureFile).RewardsSeen.Count == fresh, "Showing them saves them as seen");
            Shot("Pet battle hub", "battle-rewards-new-en.png");
            window.ShowBattleRewards(false); window.ShowBattleRewards(true);
            check(!window.BattleRewardsText.Split('\n').Contains("New") && Button(hub, "‹ Hub") != null, "Seen once, they are no longer new");
            window.ShowBattleRewards(false);
            Call(window, "TickBattleHub", 0.3);
            check(Button(hub, "Rewards") != null, "With nothing new the button is plain again");

            // ── 1.5 M27: the Monster Book on the hub ────────────────────────────────────────
            var book = new MonsterBook { Seeded = true };
            FieldOf("monsterBook").SetValue(window, book);
            Call(window, "TickBattleHub", 0.3);
            check(Button(hub, "Monster Book") != null && window.BattleBookNew == 0, "The hub has a Monster Book button, nothing new in an empty book");
            Click(Button(hub, "Monster Book")!);
            await Task.Delay(30);
            text = window.BattleBookText;
            int slimes = data.EnemyTypes.Count(e => e.TipVi != null && !e.Boss);
            check(window.BattleBookShown && window.BattleHubText.StartsWith("Monster Book") && Button(hub, "‹ Hub") != null && Button(hub, "Rewards") is not { IsVisible: true }
                && text.Contains($"Slimes · 0/{slimes}") && text.Contains("Bosses · 0/4") && text.Contains($"Formations · 0/{data.FormationList.Count}")
                && text.Contains("No enemy met yet") && text.Split('\n').Count(l => l == "???") == slimes + 4 + data.FormationList.Count,
                $"An empty book: every page a ??? shape, in place of the cards ({text.Replace('\n', '|')})");
            Click(Button(hub, "‹ Hub")!);
            await Task.Delay(30);
            check(!window.BattleBookShown && Button(hub, "Rewards") is { IsVisible: true } && window.BattleHubModeEnabled(PetWindow.HubAdventure), "‹ Hub brings the cards and the Rewards button back");
            book.Add(data, new[] { "slime", "spitter", "zipshard" }, new Dictionary<string, int> { ["slime"] = 4 }, new[] { "fortress" }, out _);
            Call(window, "TickBattleHub", 0.3);
            check(window.BattleBookNew == 3 && Button(hub, "Monster Book · 3 new") != null, $"Three new pages counted on the button ({window.BattleBookNew})");
            window.ShowMonsterBook(true);
            text = window.BattleBookText;
            var plainSlime = data.Enemy("slime");
            check(text.Contains($"Slimes · 2/{slimes}") && text.Contains("Formations · 1/") && text.Split('\n').Count(l => l == "New") == 3 && text.Contains("× 4")
                && window.BattleBookSelected == "slime" && text.Contains(plainSlime.TipEn!) && text.Contains("Defeated: 4") && text.Contains("Good skills: ") && text.Contains("Elite: "),
                $"Met pages drawn with their count and New; the first new one is open with its tip ({text.Replace('\n', '|')})");
            check(window.BattleBookNew == 0 && book.Seen.Count == 3, "Opening the book marks them seen");
            Shot("Pet battle hub", "battle-book-en.png");
            check(!window.SelectBookPage("rock") && window.BattleBookSelected == "slime", "A page not met cannot be opened");
            check(window.SelectBookPage(MonsterBook.FormationPage("fortress")) && window.BattleBookText.Contains("Members: ") && window.BattleBookText.Contains(data.Formation("fortress")!.TipEn!),
                "A formation's page: its members and its tip");
            Shot("Pet battle hub", "battle-book-formation-en.png");
            var flyerPortrait = (System.Windows.Media.Imaging.BitmapSource)Call(window, "BookPortrait", data.Enemy("flyer"), false)!;
            var flyerPixels = new byte[flyerPortrait.PixelWidth * flyerPortrait.PixelHeight * 4];
            flyerPortrait.CopyPixels(flyerPixels, flyerPortrait.PixelWidth * 4, 0);
            check(Enumerable.Range(0, flyerPixels.Length / 4).Count(i => flyerPixels[i * 4 + 3] > 100) > 200,
                "The flying slime's Monster Book portrait contains visible artwork");
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string portraitFolder)
            {
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(flyerPortrait));
                using var file = File.Create(Path.Combine(portraitFolder, "book-flyer.png")); encoder.Save(file);
            }
            window.ShowMonsterBook(false);
            // Counted once per battle; custom battle data counts nothing.
            var met = new BattleSim(data, 0, 1000, seed: 1);
            var rock = met.AddSlime(500, data.Enemy("rock"));
            rock.Set(SlimeState.Moving);
            met.Step(1.0 / 60, 200, true);
            var pages = (List<string>)PetPrivate.Call(window, "CountMonsterBook", met)!;
            var again = (List<string>)PetPrivate.Call(window, "CountMonsterBook", met)!;
            check(pages.SequenceEqual(new[] { "rock" }) && again.Count == 0 && book.Knows("rock"), $"A battle's meetings go in the book once ({string.Join(",", pages)})");

            // ── 1.5 M28: achievements on the Rewards page, the result panel, Customize ─────────
            var trophies = new AchievementBook();
            FieldOf("achievementBook").SetValue(window, trophies);
            window.ShowBattleRewards(true);
            text = window.BattleRewardsText;
            check(text.Contains("Achievements · 0/20") && text.Contains("First victory") && text.Contains("0/500 · reward: Accessory Bandana") && text.Contains("reward: Coat Mint")
                && !text.Split('\n').Contains("New") && !window.CoatAvailable("Mint") && window.CoatAvailable("Gray") && !window.AccessoryAvailable("Bandana"),
                $"Achievements head the Rewards page with their progress and rewards; their coats and accessories are locked ({text.Replace('\n', '|')})");
            window.ShowBattleRewards(false);
            var clean = new BattleSim(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 2 }, Boss = false, SlimeMaxHp = 1, SlimeDamage = 0, CatMaxHp = 1000 }, 0, 1600, seed: 3);
            for (double t = 0; t < 60 && !clean.Over; t += 1.0 / 60) clean.Step(1.0 / 60, 800, false);
            FieldOf("battleCustomData").SetValue(window, true);
            var custom = (List<AchievementSpec>)PetPrivate.Call(window, "CountAchievements", clean)!;
            check(custom.Count == 0 && trophies.Earned.Count == 0 && trophies.Totals.Count == 0, "Custom battle data counts nothing");
            FieldOf("battleCustomData").SetValue(window, false);
            var otherBattle = new BattleSim(new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 2 }, Boss = false, SlimeMaxHp = 1, SlimeDamage = 0, CatMaxHp = 1000 }, 0, 1600, seed: 3);
            for (double t = 0; t < 60 && !otherBattle.Over; t += 1.0 / 60) otherBattle.Step(1.0 / 60, 800, false);
            var earned = (List<AchievementSpec>)PetPrivate.Call(window, "CountAchievements", otherBattle)!;
            var earnedAgain = (List<AchievementSpec>)PetPrivate.Call(window, "CountAchievements", otherBattle)!;
            check(earned.Select(a => a.Id).Take(2).SequenceEqual(new[] { "first_win", "flawless" }) && earnedAgain.Count == 0 && trophies.Totals["defeated"] == 2
                && window.CoatAvailable("Mint") && window.AchievementReward("Mint") && !window.CoatAvailable("Golden"),
                $"A clean win earns First victory and Not a scratch once, and its Mint coat ({string.Join(",", earned.Select(a => a.Id))})");
            Call(window, "TickBattleHub", 0.3);
            check(window.BattleRewardsNew == earned.Count && Button(hub, $"Rewards · {earned.Count} new") != null, $"The Rewards button counts the new achievements ({window.BattleRewardsNew})");
            window.ShowBattleRewards(true);
            text = window.BattleRewardsText;
            check(text.Contains($"Achievements · {earned.Count}/20") && text.Contains("✓ Not a scratch") && text.Contains("Done · reward: Coat Mint") && text.Split('\n').Count(l => l == "New") == earned.Count,
                $"Earned ones are marked with New ({text.Replace('\n', '|')})");
            Shot("Pet battle hub", "battle-achievements-en.png");
            window.ShowBattleRewards(false);
            check(window.BattleRewardsNew == 0, "Seen once, they are no longer new");
            Call(window, "ShowBattleSummary", new PetWindow.BattleSummary(true, 2, 30, 1000, 1000, Achieved: earned));
            await Task.Delay(10);
            string resultText = Find("Pet result")?.Content is DependencyObject resultContent ? string.Join("|", Descendants(resultContent).OfType<TextBlock>().Select(t => t.Text)) : "";
            check(resultText.Contains("★ Achievement: First victory") && resultText.Contains("★ Achievement: Not a scratch · unlocked Coat Mint"), $"The result panel names each achievement and what it unlocked ({resultText})");
            Shot("Pet result", "battle-result-achievement-en.png");
            Call(window, "CloseBattleSummary");
            window.ShowCustomize();
            var looks = PetPrivate.Field<PetSettingsForm>(window, "customizeForm");
            check(looks.CoatIds.Contains("Mint") && !looks.CoatIds.Contains("Golden") && looks.Coat.Items.Count == 11, $"Customize offers the earned Mint coat only ({string.Join(",", looks.CoatIds)})");
            PetPrivate.Field<Window>(window, "customizeWindow").Close();
            window.CancelBattleHub();

            // ── The Bag from the Adventure map ──────────────────────────────────────────────
            var stages = adventure.Chapter()!.Stages;
            check(window.OpenBattleBag() is string none && none.Length > 0 && !window.BattleBagOpen, "Without a battle or map the Bag does not open");
            check(window.OpenAdventureMap(stages[1].Id) == null, "The Adventure map opens");
            await Task.Delay(30);
            var map = Find("Pet adventure")!;
            Click(Button(map, "Bag")!);
            await Task.Delay(30);
            var bag = Find("Pet bag");
            long style = bag == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(bag).Handle, -20).ToInt64();
            check(window.BattleBagOpen && bag is { ShowActivated: false, ShowInTaskbar: false, IsVisible: true } && (style & 0x08000000) != 0 && window.BattleBagMode == PetWindow.BagAdventure,
                "The map's Bag button opens a floating Bag: no focus, no taskbar button");
            text = window.BattleBagText;
            var sim = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Sword, build: build, seed: 0);
            check(window.BattleBagTab == PetWindow.BagStatsTab && text.Contains("Bag · Adventure") && text.Contains("Build carried into Stage 2") && text.Contains("Stats") && text.Contains("Cards") && text.Contains("Relics")
                && text.Contains("Max HP") && text.Contains("Damage taken") && text.Contains("Healing fish") && text.Contains($"{sim.CatMaxHp:0}") && text.Contains("+20"),
                $"Stats tab: the numbers with the build carried into the chosen stage ({text.Replace('\n', '|')})");
            Shot("Pet bag", "bag-adventure-stats-en.png");
            Click(Button(bag!, "Cards")!);
            // Assert the initial hint before pumping real mouse input: a cursor over a medal
            // legitimately replaces it with that synergy's description.
            text = window.BattleBagText;
            string sharp = data.UpgradeList.First(u => u.Id == "sharp").Name("en");
            check(window.BattleBagTab == PetWindow.BagCardsTab && text.Contains(sharp) && text.Contains("×2") && text.Contains("Defense 3") && text.Contains("Power 2")
                && sim.Synergies.All(y => text.Contains(y.Name("en"))) && text.Contains("On") && text.Contains("1 more") && text.Contains("Hover a synergy"),
                $"Cards tab: tiles, ×n, each group's synergy progress ({text.Replace('\n', '|')})");
            check(TilesFillWidth(out var cardsFill), $"Card tiles fill the Bag's width ({cardsFill})");
            Shot("Pet bag", "bag-adventure-cards-en.png");
            Click(Button(bag!, "Relics")!);
            await Task.Delay(10);
            text = window.BattleBagText;
            check(text.Contains("Relics belong to the Expedition") && text.Contains("✓ Stage 1"), "Relics tab outside the Expedition: says where relics come from, shows the chapter");

            // Where it was put is kept; it comes back there on the cat's screen.
            typeof(PetWindow).GetMethod("MoveWindow", Flags, null, new[] { typeof(Window), typeof(double), typeof(double), typeof(double?) }, null)!
                .Invoke(window, new object?[] { bag!, realBounds.Left + 40.0, realBounds.Top + 60.0, null });
            await Task.Delay(10);
            var place = Field<Point?>(window, "bagPlace");
            Click(Button(bag!, "×")!);
            check(!window.BattleBagOpen && Count("Pet bag") == 0 && window.AdventureMapOpen, "× closes the Bag; the map stays");
            window.OpenBattleBag();
            await Task.Delay(10);
            bag = Find("Pet bag")!;
            check(place is Point p && Math.Abs(bag.Left - p.X) < 2 && Math.Abs(bag.Top - p.Y) < 2 && window.BattleBagTab == PetWindow.BagRelicsTab,
                $"Opened again it is where it was put, on the tab left open ({place})");
            FieldOf("bagPlace").SetValue(window, null);

            // A fullscreen app hides it; it comes back after.
            SetContext(window, true, 0); Call(window, "TickContext", 1.1);
            TickBag(0.1);
            check(window.BattleBagOpen && !window.BattleBagVisible, "A fullscreen app hides the Bag");
            SetContext(window, false, 0); Call(window, "TickContext", 1.1);
            TickBag(0.1);
            check(window.BattleBagVisible, "It comes back after");
            window.CancelAdventureMap();
            TickBag(0.1);
            check(!window.BattleBagOpen && Count("Pet bag") == 0, "Closing the map closes its Bag");

            // ── The Bag from the Expedition map ─────────────────────────────────────────────
            var x = window.Expedition!;
            var run = x.Start(BattleWeapon.Hammer, BattleDifficulty.Hard, 7, "bag-run");
            run.Build["fast_paws"] = 1;
            run.Relics.Add("light_feather"); run.Relics.Add("reckless_charm");
            run.Hp = 20;
            run.Log.Add(new ExpeditionLogEntry { Step = 0, Kind = ExpeditionSpec.Fight, Hp = -12 });
            run.Log.Add(new ExpeditionLogEntry { Step = 0, Kind = ExpeditionChoice.CapsuleKind, Option = Expedition.Gold, Prize = "relic:light_feather", Hp = -14 });
            check(window.OpenExpeditionMap() == null && window.OpenBattleBag(PetWindow.BagStatsTab) == null && window.BattleBagMode == PetWindow.BagExpedition, "The Expedition map's Bag opens on the run");
            await Task.Delay(10);
            text = window.BattleBagText;
            string feather = data.Expedition.Relic("light_feather")!.Name("en"), charm = data.Expedition.Relic("reckless_charm")!.Name("en");
            string hammer = data.Weapon(BattleWeapon.Hammer).Name("en");
            check(text.Contains("Bag · Expedition") && text.Contains(hammer) && text.Contains("Step 1/") && text.Contains("Hard") && text.Contains("HP 20/")
                && text.Contains("Low HP") && text.Contains(feather + ": −15 max HP") && text.Contains(charm + ": always 10% more damage taken"),
                $"Stats: the run's weapon and HP, warnings for low HP and each relic's price ({text.Replace('\n', '|')})");
            window.ShowBattleBagTab(PetWindow.BagRelicsTab);
            text = window.BattleBagText;
            check(text.Contains("Relics 2/3") && text.Contains("◈ " + feather) && text.Contains("▲ +15% running speed") && text.Contains("▼ −15 max HP") && text.Contains("Empty")
                && text.Contains("Run log") && text.Contains("Gold capsule: the ◈ " + feather + " relic") && text.Contains("Fight: won") && text.Contains("−14 HP"),
                $"Relics: slots, gains and prices, the run log latest first ({text.Replace('\n', '|')})");
            check(TilesFillWidth(out var relicsFill), $"Relic slots fill the Bag's width ({relicsFill})");
            Shot("Pet bag", "bag-expedition-relics-en.png");
            // The skills with the relics' changes: the Relief pack makes the fish heal more and recharge slower.
            run.Relics.Add("relief_pack");
            window.ShowBattleBagTab(PetWindow.BagStatsTab);
            text = window.BattleBagText;
            var relief = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Hammer, seed: 0, relics: new[] { "relief_pack" });
            string fish = BattleSkills.Get(BattleSkill.Heal).Name("en");
            check(text.Contains("Skills") && text.Contains(fish) && text.Contains($"{relief.HealAmount:0} HP") && text.Contains("every " + relief.SkillCooldown(BattleSkill.Heal).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s")
                && text.Contains("every " + data.SkillCooldown(BattleSkill.Shield).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s") && !text.Contains("This battle"),
                $"Skills: cooldowns and the fish's heal after the relics; no battle numbers outside a battle ({text.Replace('\n', '|')})");
            Shot("Pet bag", "bag-expedition-skills-en.png");
            run.Relics.Remove("relief_pack");
            // Not while a capsule opens (its prize stays secret until it pops).
            window.CloseBattleBag();
            if (window.ExpeditionRoll() == null)
            {
                check(window.ExpeditionRevealing && window.OpenBattleBag() is string wait && wait.Contains("capsule") && !window.BattleBagOpen, "While a capsule opens the Bag waits");
                window.FinishExpeditionReveal();
            }
            check(window.OpenBattleBag() == null, "After it, the Bag opens");
            prefs.Language = "vi";
            window.OpenBattleBag(PetWindow.BagStatsTab);
            check(window.BattleBagText.Contains("Túi đồ · Viễn chinh") && window.BattleBagText.Contains("Chỉ số") && window.BattleBagText.Contains("HP tối đa"), "The Bag follows the language");
            Shot("Pet bag", "bag-expedition-stats-vi.png");
            prefs.Language = "ja"; window.OpenBattleBag(PetWindow.BagStatsTab);
            Shot("Pet bag", "bag-expedition-stats-ja.png");
            window.ShowBattleBagTab(PetWindow.BagRelicsTab); Shot("Pet bag", "bag-expedition-relics-ja.png");
            window.ShowBattleBagTab(PetWindow.BagCardsTab); Shot("Pet bag", "bag-expedition-cards-ja.png");
            window.ShowBattleBagTab(PetWindow.BagStatsTab);
            prefs.Language = "en";

            // A low screen: at most the screen's height less 24, the content scrolls.
            window.ShowBattleBagTab(PetWindow.BagRelicsTab);
            boundsField.SetValue(window, realBounds with { Bottom = realBounds.Top + 480 });
            window.CloseBattleBag(); window.OpenBattleBag();
            await Task.Delay(30);
            bag = Find("Pet bag")!;
            bag.UpdateLayout();
            static IEnumerable<DependencyObject> All(DependencyObject node) => LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().SelectMany(All).Prepend(node);
            check(bag.ActualHeight <= 456.5 && All(bag).OfType<ScrollViewer>().First() is { ScrollableHeight: > 0 }, $"On a 480-DIP screen the Bag is capped and scrolls ({bag.ActualHeight:0} DIPs)");
            boundsField.SetValue(window, realBounds);
            window.CancelExpeditionMap();
            TickBag(0.1);
            check(!window.BattleBagOpen, "Closing the Expedition map closes its Bag");
            x.Abandon();

            // ── The Bag in a battle: it pauses the battle (small data: no stage tip in the way) ─
            WaveSpec One() => new() { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 } } };
            var plain = new StageSpec { Id = "bag_plain", NameVi = "Đồng", NameEn = "Field", Waves = new[] { One(), One(), One() } };
            var up = new StageSpec { Id = "bag_up", NameVi = "Thử", NameEn = "Trial", Upgrades = true, Waves = new[] { One(), One(), One() } };
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 100000, SlimeDamage = 0, WaveBreakSeconds = 0.5, OutroSeconds = 0.5, RareChance = 0,
                Campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "c", NameVi = "C", NameEn = "C", Stages = new[] { plain, up } } } } };
            check(window.StartBattle(stage: plain) == null && window.BattleActive && window.BattlePauseReasons == PetWindow.BattlePause.None, "An Adventure battle starts");
            Fight(0.5);
            var hud = Field<BattleHud>(window, "battleHud");
            double before = window.Battle!.Time;
            Click(hud.BagButton);
            await Task.Delay(10);
            check(window.BattleBagOpen && window.BattleBagMode == PetWindow.BagAdventure && (window.BattlePauseReasons & PetWindow.BattlePause.Bag) != 0 && window.Battle!.Paused,
                "The HUD's Bag button opens the Bag and pauses the battle");
            Fight(1);
            check(window.Battle!.Time == before, "Nothing runs while the Bag is open");
            check(window.BattleBagText.Contains("HP " + Math.Ceiling(window.Battle.CatHp) + "/") && window.BattleBagText.Contains("Stage 1 · Field"), "In a battle it shows the battle's own HP and stage");
            window.ShowBattleBagTab(PetWindow.BagStatsTab);
            check(window.BattleBagText.Contains("This battle · ") && window.BattleBagText.Contains("Enemies defeated") && window.BattleBagText.Contains("Skills")
                && !window.BattleBagText.Contains("If you take"),
                $"In a battle: what this battle has done so far, the skills, no card preview without an offer ({window.BattleBagText.Replace('\n', '|')})");
            window.SetBattlePaused(true);
            Click(hud.BagButton);
            check(!window.BattleBagOpen && window.BattlePauseReasons == PetWindow.BattlePause.User, "Closing it removes only its own pause (the user's stays)");
            window.SetBattlePaused(false);
            Fight(0.5);
            check(window.Battle!.Time > before, "The battle goes on after");
            window.OpenBattleBag();
            window.EndBattle();
            TickBag(0.1);
            check(!window.BattleBagOpen && Count("Pet bag") == 0, "The battle's end closes its Bag");

            // Over a card offer: the Bag opens, picking still works, the build shows the new card.
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 1, SlimeDamage = 0, WaveBreakSeconds = 0.5, OutroSeconds = 0.5, RareChance = 0,
                Campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "c", NameVi = "C", NameEn = "C", Stages = new[] { plain, up } } } } };
            check(window.StartBattle(stage: up) == null, "A stage with cards starts");
            for (int i = 0; i < 400 && window.BattleActive && !window.BattleUpgradeOpen; i++)
            {
                if (window.Battle is { } b) foreach (var s in b.Slimes) if (s.Targetable) s.X = pet.X + 56 + 50;
                Fight(0.05);
            }
            check(window.BattleUpgradeOpen && window.OpenBattleBag(PetWindow.BagCardsTab) == null && (window.BattlePauseReasons & PetWindow.BattlePause.Bag) != 0,
                "Over a card offer the Bag opens too");
            // A card that changes the numbers: picked (not confirmed) the Bag previews it on both tabs.
            var offered = window.Battle!.UpgradeOffer!.FirstOrDefault(u => u.Effect is "damage" or "maxHp" or "armor" or "moveSpeed" or "attackSpeed" or "knock") ?? window.Battle!.UpgradeOffer![0];
            check(window.PickBattleUpgrade(offered.Id) && window.BattleBagText.Contains("If you take: " + offered.Name("en")) && window.BattleBagText.Contains(offered.Describe("en")),
                "Picking a card shows it in the Bag's Cards tab as a preview");
            window.ShowBattleBagTab(PetWindow.BagStatsTab);
            // The offer is random: only a card that changes a number has "→" and the blue-number note.
            bool changesNumbers = offered.Effect is "damage" or "maxHp" or "armor" or "moveSpeed" or "attackSpeed" or "knock";
            check(window.BattleBagText.Contains("If you take: " + offered.Name("en")) && (!changesNumbers || window.BattleBagText.Contains(" → ") && window.BattleBagText.Contains("Blue numbers")),
                $"The Stats tab shows → the numbers after the card ({window.BattleBagText.Replace('\n', '|')})");
            Shot("Pet bag", "bag-card-preview-en.png");
            Call(window, "FillBag", true);
            window.ShowBattleBagTab(PetWindow.BagCardsTab);
            check(window.PickBattleUpgrade(offered.Id) && window.ConfirmBattleUpgrade() && window.Battle!.Paused && (window.BattlePauseReasons & PetWindow.BattlePause.Upgrade) == 0,
                "A card picked with the Bag open: the battle stays paused for the Bag");
            TickBag(0.3);
            check(window.BattleBagText.Contains(offered.Name("en")) && !window.BattleBagText.Contains("If you take"), "The Bag shows the card just taken; the preview is gone");
            window.CloseBattleBag();
            check(!window.Battle!.Paused, "Closing it lets the battle go on");
            window.EndBattle();
        }
        finally
        {
            boundsField.SetValue(window, realBounds);
            window.Reset();
            FieldOf("bagPlace").SetValue(window, null);
            prefs.Language = language; prefs.BattleWeapon = weapon;
            window.BattleTuning = tuningBefore;
            window.AdventureProgressFile = null; window.ExpeditionProgressFile = null;
            window.ReloadAdventureProgress(); window.ReloadExpeditionProgress();
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
