using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopPet;

// The battle hub's Bag in the real pet window, with the built-in data: it opens from the hub's
// button in the same window (the cards make way, ‹ Hub brings them back), shows the numbers, the
// Adventure build carried on, the Expedition run's cards and relics and the rewards won or to win,
// follows the language, scrolls on a low screen and never takes focus. Temporary progress files only.
internal static class InventoryChecks
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static object? Call(PetWindow window, string name, params object[] args) => typeof(PetWindow).GetMethod(name, Flags)!.Invoke(window, args);
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    private static Window? Find(string title) => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == title && w.IsLoaded);
    private static int Count(string title) => Application.Current.Windows.OfType<Window>().Count(w => w.Title == title && w.IsLoaded);
    private static Border? Button(DependencyObject node, string label)
    {
        if (node is Border { Child: StackPanel panel } border && panel.Children.OfType<TextBlock>().Any(t => t.Text == label)) return border;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            if (Button(child, label) is Border found) return found;
        return null;
    }
    private static void Click(UIElement element) =>
        element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
    private static void Shot(string name)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not string review || Find("Pet battle hub")?.Content is not FrameworkElement content) return;
        content.UpdateLayout();
        var shot = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth + 12), (int)Math.Ceiling(content.ActualHeight + 12), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        shot.Render(content);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));
        using var stream = File.Create(Path.Combine(review, name)); encoder.Save(stream);
    }

    public static async Task Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences prefs, Action<bool, string> check)
    {
        void TickHub() { for (double t = 0; t < 0.3; t += 1.0 / 60) Call(window, "TickBattleHub", 1.0 / 60); }
        string language = prefs.Language, weapon = prefs.BattleWeapon;
        var tuningBefore = window.BattleTuning;
        var data = BattleData.Settings;
        string dir = Path.Combine(Path.GetTempPath(), "pet-inventory-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var boundsField = typeof(PetWindow).GetField("bounds", Flags)!;
        var realBounds = (Area)boundsField.GetValue(window)!;
        prefs.Language = "en"; prefs.BattleWeapon = BattleWeapon.Sword;
        window.Reset();
        window.BattleTuning = data;
        window.AdventureProgressFile = Path.Combine(dir, "adventure-progress.json");
        window.ExpeditionProgressFile = Path.Combine(dir, "expedition.json");
        window.ReloadAdventureProgress(); window.ReloadExpeditionProgress();
        try
        {
            // Nothing won yet: the hub has a Bag button; it shows the base numbers, no cards, the
            // rewards still to win and no Expedition (locked).
            check(window.OpenBattleHub() == null, "The hub opens");
            await Task.Delay(30);
            var hub = Find("Pet battle hub")!;
            var bag = Button(hub, "Bag");
            check(bag != null && !window.BattleInventoryShown, "The hub has a Bag button and starts on its mode cards");
            Click(bag!);
            await Task.Delay(30);
            string text = window.BattleInventoryText;
            long style = GetWindowLongPtr(new WindowInteropHelper(hub).Handle, -20).ToInt64();
            check(window.BattleInventoryShown && Count("Pet battle hub") == 1 && window.BattleHubOpen && behavior.Quiet && window.BattleOwnsPet
                && hub is { ShowActivated: false, ShowInTaskbar: false } && (style & 0x08000000) != 0,
                "The Bag opens in the hub's own window: no new window, no focus, the pet's free time still held");
            check(window.BattleHubText.StartsWith("Bag") && Button(hub, "‹ Hub") != null && Button(hub, "Close") != null, "Its title says Bag; ‹ Hub and Close are below");
            string sword = data.Weapon(BattleWeapon.Sword).Name(true);
            check(text.Contains("Stats") && text.Contains("Base · " + sword) && text.Contains("Max HP") && text.Contains("Damage per hit") && text.Contains("Damage taken")
                && text.Contains($"{data.CatMaxHp:0}") && text.Contains("Adventure build") && text.Contains("No cards yet (win a stage") && !text.Split('\n').Contains("Expedition run"),
                $"Before any win: base numbers, no cards, no Expedition section ({text.Replace('\n', '|')})");
            var rewardStages = data.Campaign!.Chapters.SelectMany(c => c.Stages).Where(s => s.Reward != null).ToList();
            check(rewardStages.All(s => text.Contains("? " + PetAccessories.Find(s.Reward)!.Name(true)) && text.Contains("Win the stage " + s.Name(true)))
                && data.Expedition!.Keepsakes.All(k => text.Contains("? " + k.Name(true))),
                "Rewards not won show what wins them (each Adventure accessory and Expedition keepsake)");
            Shot("battle-bag-empty-en.png");
            Click(Button(hub, "‹ Hub")!);
            await Task.Delay(30);
            check(!window.BattleInventoryShown && window.BattleHubModeEnabled(PetWindow.HubAdventure) && Button(hub, "Bag") != null && window.BattleHubText.StartsWith("Battle Mode"),
                "‹ Hub brings the mode cards back");

            // The whole chapter won with a build: its cards, synergy, changed numbers and rewards.
            var adventure = window.Adventure!;
            var build = new Dictionary<string, int> { ["sharp"] = 2, ["thick_fur"] = 2, ["big_heart"] = 1 };
            int n = 0;
            foreach (var stage in adventure.Chapter()!.Stages) adventure.Apply("bag-" + n++, stage, true, true, BattleWeapon.Sword, BattleDifficulty.Normal, 10, 60, build);
            var sim = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Sword, build: build, seed: 0);
            check(window.ShowBattleInventory(true) == null, "The Bag opens from the API too");
            text = window.BattleInventoryText;
            string sharp = data.UpgradeList.First(u => u.Id == "sharp").Name(true);
            check(text.Contains(sharp + " ×2") && text.Contains("Power 2") && text.Contains("Defense 3") && text.Contains($"{sim.CatMaxHp:0}") && text.Contains($"{sim.SwingDamage:0.#}")
                && text.Contains("The build that won " + adventure.Chapter()!.Stages[^1].Name(true)),
                $"The Adventure build: its cards by group and the numbers it gives ({text.Replace('\n', '|')})");
            check(sim.Synergies.All(y => text.Contains("★ " + y.Name(true))) && sim.Synergies.Count > 0, "Synergies the build switched on are listed");
            check(rewardStages.All(s => text.Contains("✓ " + PetAccessories.Find(s.Reward)!.Name(true))), "Won accessories are marked and say where to wear them");
            check(text.Split('\n').Contains("Expedition run") && text.Contains("No run under way"), "Once open, the Expedition section says no run is under way");

            // A run under way: its weapon, HP, cards and relics.
            window.ShowBattleInventory(false);
            var x = window.Expedition!;
            var run = x.Start(BattleWeapon.Hammer, BattleDifficulty.Hard, 7, "bag-run");
            run.Build["fast_paws"] = 1;
            run.Relics.Add("light_feather");
            window.ShowBattleInventory(true);
            text = window.BattleInventoryText;
            string feather = data.Expedition!.Relic("light_feather")!.Name(true);
            check(text.Contains("Expedition · " + data.Weapon(BattleWeapon.Hammer).Name(true)) && text.Contains("Step 1/") && text.Contains("Hard")
                && text.Contains("Relics (1/3)") && text.Contains("◈ " + feather) && text.Contains(data.UpgradeList.First(u => u.Id == "fast_paws").Name(true)),
                $"The run's column, cards and relics are shown ({text.Replace('\n', '|')})");
            // Gold marks what cards or relics changed from that column's own weapon, not the hammer itself.
            static IEnumerable<TextBlock> Texts(DependencyObject node) => node is TextBlock t ? new[] { t } : LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().SelectMany(Texts);
            var gold = Texts(hub).Where(t => t.Foreground == PetUi.Game.Gold).Select(t => t.Text).ToList();
            var hammerPlain = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Hammer, seed: 0);
            var hammerRun = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Hammer, build: run.Build, seed: 0, relics: run.Relics);
            check(!gold.Contains($"{hammerPlain.SwingDamage:0.#}") && gold.Contains($"{hammerRun.CatMaxHp:0}") && gold.Contains("115%"),
                $"Gold marks only what the run's cards and relics changed ({string.Join(", ", gold)})");
            Shot("battle-bag-full-en.png");

            // The language follows the pet's.
            prefs.Language = "vi";
            TickHub();
            text = window.BattleInventoryText;
            check(window.BattleHubText.StartsWith("Túi đồ") && text.Contains("Chỉ số") && text.Contains("HP tối đa") && text.Contains("Di vật (1/3)") && Button(hub, "‹ Sảnh") != null,
                "The Bag follows the language");
            Shot("battle-bag-full-vi.png");

            // A low screen: the hub is capped, the Bag scrolls, Close stays in view.
            boundsField.SetValue(window, realBounds with { Bottom = realBounds.Top + 480 });
            window.ShowBattleInventory(false); window.ShowBattleInventory(true);
            await Task.Delay(30);
            hub.UpdateLayout();
            static IEnumerable<DependencyObject> All(DependencyObject node) => LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().SelectMany(All).Prepend(node);
            var scroller = All(hub).OfType<ScrollViewer>().FirstOrDefault();
            var close = Button(hub, "Đóng")!;
            double closeBottom = close.TransformToAncestor(hub).Transform(new Point(0, close.ActualHeight)).Y;
            check(hub.ActualHeight <= 456.5 && scroller is { ScrollableHeight: > 0 } && closeBottom <= hub.ActualHeight,
                $"On a 480-DIP screen the Bag scrolls inside the hub and Close stays in view ({hub.ActualHeight:0} DIPs)");
            boundsField.SetValue(window, realBounds);

            // Closing the hub forgets the Bag; the next hub starts on its cards. A paused pet can still look.
            window.CancelBattleHub();
            check(!window.BattleInventoryShown && Count("Pet battle hub") == 0 && !behavior.Quiet, "Closing the hub closes the Bag and gives the pet back");
            window.OpenBattleHub();
            check(!window.BattleInventoryShown && window.ShowBattleInventory(true) == null && window.BattleInventoryShown, "A new hub starts on its cards; the Bag opens again");
            check(window.ShowBattleInventory(true) == null && Count("Pet battle hub") == 1, "Opening the Bag twice keeps one");
            window.CancelBattleHub();
            check(window.ShowBattleInventory(true) is string closed && closed.Length > 0, "Without the hub the Bag does not open");
        }
        finally
        {
            boundsField.SetValue(window, realBounds);
            window.Reset();
            prefs.Language = language; prefs.BattleWeapon = weapon;
            window.BattleTuning = tuningBefore;
            window.AdventureProgressFile = null; window.ExpeditionProgressFile = null;
            window.ReloadAdventureProgress(); window.ReloadExpeditionProgress();
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
