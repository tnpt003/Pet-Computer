using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopPet;
using static PetPrivate;

// Infinity (2026-10-22) in the real pet window: locked until its Adventure stage is won → the hub
// opens the shared setup panel → Ready starts a battle that is never won (the HUD counts waves
// without a total, ten marks), cards every few waves, the bag says Infinity, End gives the result
// with the wave reached and a record (in memory only), Play again, the hub's card shows the best.
internal static class EndlessChecks
{
    private static Window? Find(string title) => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == title && w.IsLoaded);
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
        void Fight(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattle", 1.0 / 60); }
        void TickHub() { for (double t = 0; t < 0.3; t += 1.0 / 60) Call(window, "TickBattleHub", 1.0 / 60); }
        string language = prefs.Language, weapon = prefs.BattleWeapon, level = prefs.BattleDifficulty;
        var tuningBefore = window.BattleTuning;
        prefs.Language = "en"; prefs.BattleWeapon = BattleWeapon.Sword; prefs.BattleDifficulty = "Normal";
        window.Reset();
        var gate = new StageSpec { Id = "inf_gate", NameVi = "Cổng", NameEn = "Gate", Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 } } } } };
        var campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "test", NameVi = "Chương thử", NameEn = "Test chapter", Stages = new[] { gate } } } };
        // One-hit slimes that do not hurt: waves go by quickly; a card after every 2nd wave.
        var infinity = new EndlessSpec { UnlockAfter = "inf_gate", UpgradeEvery = 2, BossEvery = 10, FirstCount = 1, CountPerWave = 0, Kinds = new[] { new EndlessKind { Enemy = "slime", Weight = 1 } } };
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 1, SlimeDamage = 0, WaveBreakSeconds = 0.3, OutroSeconds = 0.5, Campaign = campaign, Endless = infinity };
        window.ReloadAdventureProgress();
        int freeRecords = window.BattleRecords.Loadouts.Count(pair => !pair.Key.StartsWith("endless|"));
        try
        {
            check(window.OpenBattleHub() == null, "The hub opens");
            TickHub();
            check(!window.BattleHubModeEnabled(PetWindow.HubInfinity) && window.BattleHubText.Contains("Infinity") && window.BattleHubText.Contains("Locked")
                  && window.BattleHubChoose(PetWindow.HubInfinity) is string locked && locked.Contains("Slime King") && !window.BattlePreparing,
                $"Infinity is locked until its Adventure stage is won ({window.BattleHubText.Replace('\n', '|')})");
            window.AdventureProgress.Completed.Add("inf_gate");
            TickHub();
            check(window.BattleHubModeEnabled(PetWindow.HubInfinity) && window.BattleHubText.Contains("Not played yet") && !window.BattleHubText.Contains("Coming soon"), "Then it opens; the card says it has not been played");
            Shot("Pet battle hub", "battle-hub-infinity-en.png");

            check(window.BattleHubChoose(PetWindow.HubInfinity) == null && window.BattlePrepForEndless && !window.BattleHubOpen && !window.BattleActive, "Its card opens the setup panel for Infinity");
            if (window.BattleTutorialOpen) window.CloseBattleTutorial(finished: true);
            string prep = window.BattlePrepText;
            check(prep.Contains("Infinity — get ready") && prep.Contains("A card every 2 waves") && prep.Contains("‹ Hub") && prep.Contains("Ready"), $"It says how Infinity goes ({prep.Replace('\n', '|')})");
            Shot("Pet setup", "endless-setup-en.png");
            window.CancelBattlePrep();
            check(!window.BattlePreparing && window.BattleHubOpen, "‹ Hub goes back to the hub");
            window.BattleHubChoose(PetWindow.HubInfinity);
            if (window.BattleTutorialOpen) window.CloseBattleTutorial(finished: true);

            check(window.BattlePrepReady() == null && window.BattleActive && window.Battle is { Endless: true }, "Ready starts an Infinity battle");
            var hud = Field<BattleHud>(window, "battleHud");
            Fight(0.2);
            check(hud.StatusText.StartsWith("Wave 1 ·") && !hud.StatusText.Contains("/") && hud.WaveMarks == BattleHud.EndlessDots, $"The HUD counts waves without a total, ten marks ({hud.StatusText})");
            for (int i = 0; i < 400 && !window.BattleUpgradeOpen && window.BattleActive; i++) { Fight(0.05); if (window.Battle?.Slimes.Count > 0) window.Battle.Slimes[0].X = pet.X + 56 + 50; }
            check(window.BattleUpgradeOpen && window.Battle!.Wave == 2 && window.BattleUpgradeText.Contains("rest of this Infinity run"), $"A card after wave 2, kept for the run ({window.BattleUpgradeText.Replace('\n', '|')})");
            window.Battle!.ChooseUpgrade(window.Battle.OfferCards()![0].Id);
            Fight(0.1);
            for (int i = 0; i < 400 && window.Battle?.Wave < 3; i++) { Fight(0.05); if (window.Battle?.Slimes.Count > 0) window.Battle.Slimes[0].X = pet.X + 56 + 50; }
            check(window.OpenBattleBag() == null && window.BattleBagMode == PetWindow.BagEndless, "The bag opens as Infinity's");
            window.CloseBattleBag();

            // The Cards tab's synergy medals (2026-10-24): all nine tiers shown, each with its picture;
            // in every language each text stays inside its medal and every medal inside the Bag.
            var taken = (Dictionary<string, int>)typeof(BattleSim).GetField("taken", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window.Battle)!;
            var held = new Dictionary<string, int>(taken);
            string tabBefore = Field<string>(window, "bagTab");
            foreach (var (group, n) in new[] { ("defense", 4), ("power", 1), ("speed", 9) })
                taken[window.Battle!.Settings.UpgradeList.First(u => u.Group == group).Id] = n;
            static IEnumerable<DependencyObject> Tree(DependencyObject node)
            {
                yield return node;
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++)
                    foreach (var child in Tree(System.Windows.Media.VisualTreeHelper.GetChild(node, i))) yield return child;
            }
            foreach (string lang in new[] { "vi", "en", "ja" })
            {
                prefs.Language = lang;
                window.OpenBattleBag(PetWindow.BagCardsTab);
                await Task.Delay(30);
                var content = (FrameworkElement)Find("Pet bag")!.Content;
                content.UpdateLayout();
                var scroll = Tree(content).OfType<ScrollViewer>().First();
                var medals = Tree(scroll).OfType<Border>().Where(b => "synergy".Equals(b.Tag)).ToList();
                var outside = new List<string>();
                foreach (var medal in medals)
                    foreach (var line in Tree(medal).OfType<TextBlock>())
                    {
                        var r = line.TransformToAncestor(medal).TransformBounds(new Rect(line.RenderSize));
                        if (r.Left < -0.5 || r.Right > medal.ActualWidth + 0.5 || r.Bottom > medal.ActualHeight + 0.5) outside.Add(line.Text);
                    }
                foreach (var line in Tree(scroll).OfType<TextBlock>().Where(t => t.IsVisible))
                    if (line.TransformToAncestor(scroll).TransformBounds(new Rect(line.RenderSize)).Right > scroll.ViewportWidth + 0.5) outside.Add(line.Text);
                int pictures = medals.Sum(m => Tree(m).OfType<SynergyIcon>().Count());
                check(medals.Count == 12 && pictures == 12 && outside.Count == 0,
                    $"Bag ({lang}): twelve synergy medals (four groups since 1.5 M25) with pictures, no text outside its medal or the Bag ({medals.Count} medals, {pictures} pictures; outside: {string.Join(" / ", outside)})");
                Shot("Pet bag", $"bag-endless-synergies-{lang}.png");
                window.CloseBattleBag();
            }
            prefs.Language = "en";
            SetField(window, "bagTab", tabBefore);
            taken.Clear();
            foreach (var (id, n) in held) taken[id] = n;
            int reached = window.Battle!.Wave;
            Shot("Pet battle HUD", "endless-hud-en.png");

            window.EndBattleByUser();
            Fight(1);
            await Task.Delay(30);
            string result = ResultText();
            check(!window.BattleActive && window.LastBattleSummary is { EndlessWave: var wave, Won: false } && wave == reached && result.Contains($"Reached wave {reached}") && result.Contains("New record")
                  && result.Contains("Play again") && result.Contains("‹ Hub"), $"End gives the result: the wave reached, a new record ({result})");
            Shot("Pet result", "endless-result-en.png");
            var best = window.BattleRecords.ForEndless(infinity, BattleWeapon.Sword, BattleDifficulty.Normal);
            check(best.MostWaves == reached && window.BattleRecords.Loadouts.Count(pair => !pair.Key.StartsWith("endless|")) == freeRecords && window.AdventureProgress.Runs.All(id => id != null),
                "The record is Infinity's own (the free battle's and the Adventure's are untouched)");
            Click(Button(Find("Pet result")!.Content as DependencyObject ?? new Border(), "Play again")!);
            check(window.BattleActive && window.Battle is { Endless: true, Wave: <= 1 }, "Play again starts a new Infinity run");
            window.EndBattleByUser();
            Fight(1);
            Click(Button(Find("Pet result")!.Content as DependencyObject ?? new Border(), "‹ Hub")!);
            TickHub();
            check(window.BattleHubOpen && window.BattleHubText.Contains($"Furthest: wave {reached}") && window.BattleHubText.Contains("Rewards 0/5 · next: clear wave 10"),
                $"The hub's card shows the furthest wave and the milestone rewards ({window.BattleHubText.Replace('\n', '|')})");

            // Milestone rewards (built-in data): 20 waves cleared once earn the collar and the hourglass.
            var builtIn = BattleData.Settings.Endless!;
            string clearedKey = BattleRecords.EndlessKey(builtIn, BattleWeapon.Hammer, BattleDifficulty.Easy);
            window.BattleRecords.Loadouts[clearedKey] = new BattleRecord { MostWaves = 21, MostCleared = 20 };
            FieldOf("endlessEarned").SetValue(window, null);
            TickHub();
            check(window.BattleHubText.Contains("Rewards 2/5 · next: clear wave 30") && window.AccessoryAvailable("InfinityCollar") && !window.AccessoryAvailable("StarHalo") &&
                  window.DisplayableKeepsakes.Any(k => k.Id == "endless_hourglass") && window.DisplayableKeepsakes.All(k => k.Id != "nebula_orb"),
                "Clearing wave 20 earns the Infinity collar (offered in Settings) and the hourglass (displayable); the hub counts 2/5");
            var seenBefore = window.AdventureProgress.RewardsSeen.ToList();
            check(window.ShowBattleRewards(true) == null, "The Rewards page opens");
            string rewards = window.BattleRewardsText;
            check(rewards.Contains("Infinity rewards") && rewards.Contains("✓ Wave 10 · Infinity collar") && rewards.Contains("✓ Wave 20 · Endless hourglass") &&
                  rewards.Contains("Wave 30 · Star halo") && !rewards.Contains("✓ Wave 30") && rewards.Contains("Wave 50 · Galaxy cape") && rewards.Contains("Furthest"),
                $"Rewards lists Infinity's five milestones, two won ({rewards.Replace('\n', '|')})");
            Shot("Pet battle hub", "rewards-infinity-en.png");
            // The two keepsakes as the desktop draws them (PET_RENDER_REVIEW only writes the pictures).
            foreach (var shown in builtIn.Milestones.Where(m => m.Shown))
            {
                var art = new KeepsakeArt { Scale = PetWindow.DisplayScale, Items = new[] { shown } };
                art.Measure(new Size(art.Width, art.Height)); art.Arrange(new Rect(0, 0, art.Width, art.Height));
                if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review)
                {
                    var image = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(art.Width), (int)Math.Ceiling(art.Height), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    image.Render(art);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                    using var stream = File.Create(Path.Combine(review, $"keepsake-{shown.Kind}.png")); encoder.Save(stream);
                }
            }
            window.ShowBattleRewards(false);
            window.CancelBattleHub();
            var collar = builtIn.MilestoneAt(10)!;
            Call(window, "ShowBattleSummary", new PetWindow.BattleSummary(false, 40, 600, 0, 100, WeaponLabel: "Sword", Stage: infinity.Stage(), EndlessWave: 11,
                EndlessUnlocked: new[] { collar }));
            await Task.Delay(30);
            check(ResultText().Contains("Unlocked: Infinity collar (see Hub → Rewards)"), $"The result names a milestone the run unlocked ({ResultText()})");
            Shot("Pet result", "endless-unlocked-en.png");
            Call(window, "CloseBattleSummary");
            window.BattleRecords.Loadouts.Remove(clearedKey);
            FieldOf("endlessEarned").SetValue(window, null);
            window.AdventureProgress.RewardsSeen.Clear(); window.AdventureProgress.RewardsSeen.AddRange(seenBefore);

            // 1.5 M24 "Mutations": a boss every 2 waves; after its cards the panel offers three odd rules
            // (drawback, reward in gold); the pick holds and the HUD says how long.
            var mutating = new EndlessSpec { UpgradeEvery = 2, BossEvery = 2, Boss = "king", FirstCount = 1, CountPerWave = 0, Kinds = new[] { new EndlessKind { Enemy = "slime", Weight = 1 } },
                Mutations = builtIn.Mutations };
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 1, BossMaxHp = 1, SlimeDamage = 0, WaveBreakSeconds = 0.3, OutroSeconds = 0.5, Campaign = campaign, Endless = mutating };
            check(window.OpenBattlePrep(forEndless: true) == null, "Infinity's setup opens again");
            if (window.BattleTutorialOpen) window.CloseBattleTutorial(finished: true);
            check(window.BattlePrepReady() == null && window.Battle is { Endless: true }, "A run with mutations starts");
            hud = Field<BattleHud>(window, "battleHud"); // This run's HUD.
            for (int i = 0; i < 600 && window.BattleActive && !(window.Battle!.ChoosingMutation && window.BattleUpgradeOpen); i++)
            {
                if (window.Battle!.ChoosingUpgrade && !window.Battle.ChoosingMutation) { window.PickBattleUpgrade(window.Battle.UpgradeOffer![0].Id); window.ConfirmBattleUpgrade(); }
                Fight(0.05);
                if (window.Battle?.Slimes.Count > 0) window.Battle.Slimes[0].X = pet.X + 56 + 50;
            }
            var rules = window.Battle?.UpgradeOffer;
            string panel = window.BattleUpgradeText;
            check(window.Battle is { ChoosingMutation: true, Wave: 2 } && rules is { Count: 3 } && panel.Contains("Mutation! Pick an odd rule") && panel.Contains("★ ") && panel.Contains("Until the next boss is beaten")
                  && hud.StatusText.Contains("Pick a mutation"), $"After the boss wave's cards: three mutations with their rewards ({panel.Replace('\n', '|')}; {hud.StatusText})");
            foreach (string lang in new[] { "vi", "ja", "en" })
            {
                prefs.Language = lang;
                Call(window, "CloseBattleUpgrade");
                Fight(0.02);
                Shot("Pet upgrade", $"endless-mutation-{lang}.png");
            }
            string ruleId = rules![1].Id;
            check(window.PickBattleUpgrade(ruleId) && window.ConfirmBattleUpgrade() && window.Battle!.Mutation?.Id == ruleId && window.Battle.Picks == 1 && !window.BattleUpgradeOpen,
                $"Confirming one puts it in force (no card of the build) ({window.Battle?.Mutation?.Id})");
            Fight(0.5);
            check(hud.StatusText.Contains("Mutation ") && (hud.StatusText.Contains("waves left") || hud.StatusText.Contains("wave left")), $"The HUD names the mutation and the waves it holds ({hud.StatusText})");
            window.EndBattleByUser();
            Fight(1);
            Call(window, "CloseBattleSummary");
        }
        finally
        {
            window.Reset();
            window.ReloadAdventureProgress();
            window.BattleTuning = tuningBefore;
            prefs.Language = language; prefs.BattleWeapon = weapon; prefs.BattleDifficulty = level;
        }
    }
}
