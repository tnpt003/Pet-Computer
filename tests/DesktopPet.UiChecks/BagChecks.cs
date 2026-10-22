using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopPet;

// The Bag and the hub's Rewards in the real pet window, with the built-in data (and a small data
// set for the card offer). Rewards: the hub's button counts new rewards, the page shows tiles,
// records and "New" badges once, saving them as seen. Bag: opens from the Adventure map, the
// Expedition map (not while a capsule opens) and the battle HUD (the battle pauses for it, also over
// a card offer, and goes on after); three tabs; never takes focus; keeps where it was put; scrolls on
// a low screen; hides for a fullscreen app; closes with its battle or map. Temporary files only.
internal static class BagChecks
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static object? Call(PetWindow window, string name, params object[] args) => typeof(PetWindow).GetMethod(name, Flags, null, args.Select(a => a.GetType()).ToArray(), null)!.Invoke(window, args);
    private static T Field<T>(PetWindow window, string name) => (T)typeof(PetWindow).GetField(name, Flags)!.GetValue(window)!;
    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        typeof(PetWindow).GetField("contextProbe", Flags)!.SetValue(window, new Func<(bool, double)>(() => (fullscreen, idle)));
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
        var boundsField = typeof(PetWindow).GetField("bounds", Flags)!;
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
            check(window.OpenBattleHub() == null, "The hub opens");
            await Task.Delay(30);
            var hub = Find("Pet battle hub")!;
            check(Button(hub, "Rewards") != null && Button(hub, "Bag") == null && window.BattleRewardsNew == 0, "The hub has a Rewards button (no Bag) and nothing new yet");
            Click(Button(hub, "Rewards")!);
            await Task.Delay(30);
            string text = window.BattleRewardsText;
            var accessories = data.Campaign!.Chapters.SelectMany(c => c.Stages).Where(s => s.Reward != null).Select(s => PetAccessories.Find(s.Reward)!.Name(true)).ToList();
            check(window.BattleRewardsShown && window.BattleHubText.StartsWith("Rewards") && Button(hub, "‹ Hub") != null && Count("Pet battle hub") == 1 && behavior.Quiet
                && text.Contains("Adventure accessories") && text.Contains("Expedition keepsakes") && accessories.All(a => text.Contains(a) && !text.Contains("✓ " + a))
                && data.Expedition!.Keepsakes.All(k => text.Contains(k.Name(true))) && text.Contains("Records") && text.Contains("not won yet") && !text.Split('\n').Contains("New"),
                $"Rewards in the hub's window: locked tiles, keepsakes, records, no New badge ({text.Replace('\n', '|')})");
            Shot("Pet battle hub", "battle-rewards-empty-en.png");
            Click(Button(hub, "‹ Hub")!);
            await Task.Delay(30);
            check(!window.BattleRewardsShown && window.BattleHubModeEnabled(PetWindow.HubAdventure), "‹ Hub brings the mode cards back");

            // The whole chapter won: two accessories are new, the button counts them, the page marks them.
            var adventure = window.Adventure!;
            var build = new Dictionary<string, int> { ["sharp"] = 2, ["thick_fur"] = 2, ["big_heart"] = 1 };
            int n = 0;
            foreach (var stage in adventure.Chapter()!.Stages) adventure.Apply("bag-" + n++, stage, true, true, BattleWeapon.Sword, BattleDifficulty.Normal, 10, 61, build);
            window.Expedition!.Progress.Keepsakes.Add(data.Expedition!.Keepsakes[0].Id);
            Call(window, "TickBattleHub", 0.3);
            int fresh = window.BattleRewardsNew;
            check(fresh == accessories.Count + 1 && Button(hub, $"Rewards · {fresh} new") != null, $"The button counts new rewards ({fresh})");
            window.ShowBattleRewards(true);
            text = window.BattleRewardsText;
            check(accessories.All(a => text.Contains("✓ " + a)) && text.Contains("✓ " + data.Expedition.Keepsakes[0].Name(true)) && text.Split('\n').Count(l => l == "New") == fresh
                && text.Contains("1:01") && text.Contains("1 win"),
                $"Won rewards are marked, each new one has a badge; records show the fastest win ({text.Replace('\n', '|')})");
            check(window.BattleRewardsNew == 0 && AdventureProgress.Load(adventureFile).RewardsSeen.Count == fresh, "Showing them saves them as seen");
            Shot("Pet battle hub", "battle-rewards-new-en.png");
            window.ShowBattleRewards(false); window.ShowBattleRewards(true);
            check(!window.BattleRewardsText.Split('\n').Contains("New") && Button(hub, "‹ Hub") != null, "Seen once, they are no longer new");
            window.ShowBattleRewards(false);
            Call(window, "TickBattleHub", 0.3);
            check(Button(hub, "Rewards") != null, "With nothing new the button is plain again");
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
            await Task.Delay(10);
            text = window.BattleBagText;
            string sharp = data.UpgradeList.First(u => u.Id == "sharp").Name(true);
            check(window.BattleBagTab == PetWindow.BagCardsTab && text.Contains(sharp) && text.Contains("×2") && text.Contains("Defense 3") && text.Contains("Power 2")
                && sim.Synergies.All(y => text.Contains("★ " + y.Name(true))) && text.Contains("more → ★"),
                $"Cards tab: tiles, ×n, each group's synergy progress ({text.Replace('\n', '|')})");
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
            typeof(PetWindow).GetField("bagPlace", Flags)!.SetValue(window, null);

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
            string feather = data.Expedition.Relic("light_feather")!.Name(true), charm = data.Expedition.Relic("reckless_charm")!.Name(true);
            string hammer = data.Weapon(BattleWeapon.Hammer).Name(true);
            check(text.Contains("Bag · Expedition") && text.Contains(hammer) && text.Contains("Step 1/") && text.Contains("Hard") && text.Contains("HP 20/")
                && text.Contains("Low HP") && text.Contains(feather + ": −15 max HP") && text.Contains(charm + ": always 10% more damage taken"),
                $"Stats: the run's weapon and HP, warnings for low HP and each relic's price ({text.Replace('\n', '|')})");
            window.ShowBattleBagTab(PetWindow.BagRelicsTab);
            text = window.BattleBagText;
            check(text.Contains("Relics 2/3") && text.Contains("◈ " + feather) && text.Contains("▲ +15% running speed") && text.Contains("▼ −15 max HP") && text.Contains("Empty")
                && text.Contains("Run log") && text.Contains("Gold capsule: the ◈ " + feather + " relic") && text.Contains("Fight: won") && text.Contains("−14 HP"),
                $"Relics: slots, gains and prices, the run log latest first ({text.Replace('\n', '|')})");
            Shot("Pet bag", "bag-expedition-relics-en.png");
            // The skills with the relics' changes: the Relief pack makes the fish heal more and recharge slower.
            run.Relics.Add("relief_pack");
            window.ShowBattleBagTab(PetWindow.BagStatsTab);
            text = window.BattleBagText;
            var relief = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Hammer, seed: 0, relics: new[] { "relief_pack" });
            string fish = BattleSkills.Get(BattleSkill.Heal).Name(true);
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
            check(window.PickBattleUpgrade(offered.Id) && window.BattleBagText.Contains("If you take: " + offered.Name(true)) && window.BattleBagText.Contains(offered.Describe(true)),
                "Picking a card shows it in the Bag's Cards tab as a preview");
            window.ShowBattleBagTab(PetWindow.BagStatsTab);
            check(window.BattleBagText.Contains("If you take: " + offered.Name(true)) && window.BattleBagText.Contains(" → ") && window.BattleBagText.Contains("Blue numbers"),
                $"The Stats tab shows → the numbers after the card ({window.BattleBagText.Replace('\n', '|')})");
            Shot("Pet bag", "bag-card-preview-en.png");
            Call(window, "FillBag", true);
            window.ShowBattleBagTab(PetWindow.BagCardsTab);
            check(window.PickBattleUpgrade(offered.Id) && window.ConfirmBattleUpgrade() && window.Battle!.Paused && (window.BattlePauseReasons & PetWindow.BattlePause.Upgrade) == 0,
                "A card picked with the Bag open: the battle stays paused for the Bag");
            TickBag(0.3);
            check(window.BattleBagText.Contains(offered.Name(true)) && !window.BattleBagText.Contains("If you take"), "The Bag shows the card just taken; the preview is gone");
            window.CloseBattleBag();
            check(!window.Battle!.Paused, "Closing it lets the battle go on");
            window.EndBattle();
        }
        finally
        {
            boundsField.SetValue(window, realBounds);
            window.Reset();
            typeof(PetWindow).GetField("bagPlace", Flags)!.SetValue(window, null);
            prefs.Language = language; prefs.BattleWeapon = weapon;
            window.BattleTuning = tuningBefore;
            window.AdventureProgressFile = null; window.ExpeditionProgressFile = null;
            window.ReloadAdventureProgress(); window.ReloadExpeditionProgress();
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
