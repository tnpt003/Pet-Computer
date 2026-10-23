using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopPet;
using static PetPrivate;

// Adventure milestone 3 in the real pet window: the upgrade panel between waves (pause reason of
// its own, pick + Confirm, double click, other pauses kept, fullscreen, ending the battle), the
// HUD's upgrade line and a survive stage's clock. In-memory progress only.
internal static class UpgradeChecks
{
    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, idle)));
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    private static Window? Find(string title) => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == title && w.IsLoaded);
    private static string Text(DependencyObject node) => node is TextBlock t ? t.Text + "|" : string.Concat(LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().Select(Text));

    public static async Task Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences prefs, Action<bool, string> check)
    {
        void Fight(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattle", 1.0 / 60); }
        // Fights (the slime brought next to the cat) until the upgrade panel opens or the battle ends.
        void UntilOffer()
        {
            for (int i = 0; i < 400 && window.BattleActive && !window.BattleUpgradeOpen; i++)
            {
                if (window.Battle is { } b) foreach (var s in b.Slimes) if (s.Targetable) s.X = pet.X + 56 + 50;
                Fight(0.05);
            }
        }
        string language = prefs.Language;
        var tuningBefore = window.BattleTuning;
        prefs.Language = "en";
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        window.Reset();
        WaveSpec One() => new() { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 } } };
        var stage = new StageSpec { Id = "up", NameVi = "Thử", NameEn = "Trial", Upgrades = true, Waves = new[] { One(), One(), One() } };
        var hold = new StageSpec { Id = "hold", NameVi = "Giữ", NameEn = "Hold", Objective = "survive", SurviveSeconds = 30, Waves = new[] { One() } };
        var later = new StageSpec { Id = "later", NameVi = "Sau", NameEn = "Later", Upgrades = true, Waves = new[] { One(), One(), One() } };
        // rareChance: 0 keeps the offers to the common cards these checks expect (a rare card may
        // otherwise take a group's place); the rare Defense card has its own check below.
        BattleSettings Tuning(double rare) => new() { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 1, SlimeDamage = 0, WaveBreakSeconds = 0.5, OutroSeconds = 0.5, RareChance = rare,
            Campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "c", NameVi = "C", NameEn = "C", Stages = new[] { stage, hold, later } } } } };
        var tuning = Tuning(0);
        window.BattleTuning = tuning;
        window.ReloadAdventureProgress();
        try
        {
            check(window.StartBattle(stage: stage) == null && window.Battle?.Stage == stage, "A stage with upgrades starts");
            UntilOffer();
            await Task.Delay(30);
            var panel = Find("Pet upgrade");
            long style = panel == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(panel).Handle, -20).ToInt64();
            check(window.BattleUpgradeOpen && panel is { ShowActivated: false, ShowInTaskbar: false, IsVisible: true } && (style & 0x08000000) != 0,
                "After wave 1 the upgrade panel opens; it never takes focus and has no taskbar button");
            var b = window.Battle!;
            check(b.ChoosingUpgrade && b.Paused && window.BattlePauseReasons == PetWindow.BattlePause.Upgrade && !window.BattleUpgradeConfirmEnabled,
                "Choosing pauses the battle for that reason only; Confirm waits for a pick");
            var offer = b.UpgradeOffer!;
            check(offer.Count == 3 && offer.All(u => window.BattleUpgradeText.Contains(u.Name(true)) && window.BattleUpgradeText.Contains(u.Describe(true))) &&
                  window.BattleUpgradeText.Contains("Pick an upgrade"), $"Three cards with name and effect ({window.BattleUpgradeText.Replace('\n', '|')})");
            check(offer.Select(u => u.Group).OrderBy(g => g).SequenceEqual(new[] { "defense", "power", "speed" }) &&
                  new[] { "Defense 0 → 1/3", "Power 0 → 1/3", "Speed 0 → 1/3" }.All(window.BattleUpgradeText.Contains) && window.BattleUpgradeText.Contains("kept for the next stage"),
                "One card per group, each showing its progress toward the group's synergy");
            double time = b.Time;
            Fight(1);
            check(b.Time == time && window.BattleUpgradeOpen, "Nothing runs behind the panel");
            check(!window.ConfirmBattleUpgrade() && !window.PickBattleUpgrade("nope"), "Confirm without a pick, or a card not on offer, does nothing");

            // A user pause on top: confirming removes only the upgrade reason.
            window.SetBattlePaused(true);
            string pick = offer[0].Id;
            check(window.PickBattleUpgrade(pick) && window.BattleUpgradeConfirmEnabled && window.BattleUpgradePick == pick, "Clicking a card picks it");
            var confirm = Field<Border>(window, "upgradeConfirm");
            var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
            confirm.RaiseEvent(click);
            check(!window.ConfirmBattleUpgrade() && b.Stacks(pick) == 1, "Confirm takes it once (a second click does nothing)");
            await Task.Delay(30);
            check(!window.BattleUpgradeOpen && Find("Pet upgrade") == null && window.BattlePauseReasons == PetWindow.BattlePause.User && b.Paused,
                "The panel closes; the user's own pause stays");
            window.SetBattlePaused(false);
            check(!b.Paused, "Resuming starts the battle again");
            var hud = Field<BattleHud>(window, "battleHud");
            Fight(0.1);
            check(hud.UpgradesText.Contains(BuildGroups.Name(offer[0].Group!, true) + " 1/3"), $"The HUD shows the build's group points ({hud.UpgradesText})");

            // Quick feet changes the cat's running speed for this run only.
            UntilOffer();
            var quick = window.Battle!.UpgradeOffer?.FirstOrDefault(u => u.Effect == "moveSpeed") ?? window.Battle!.UpgradeOffer![0];
            window.PickBattleUpgrade(quick.Id); window.ConfirmBattleUpgrade();
            Fight(0.1);
            check(Math.Abs(pet.MoveScale - window.Battle!.Settings.CatRunScale * window.Battle.MoveFactor) < 1e-9, "The cat runs at this run's speed (upgrades included)");

            // Fullscreen while choosing: the panel hides, comes back; the user must resume after it.
            UntilOffer();
            check(window.BattleUpgradeOpen || !window.BattleActive, "The next offer comes after the next wave");
            window.EndBattle();
            check(!window.BattleUpgradeOpen && Find("Pet upgrade") == null && pet.MoveScale == 1 && window.BattlePauseReasons == PetWindow.BattlePause.None,
                "Ending the battle while choosing closes the panel and resets the run");
            check(window.StartBattle(stage: stage) == null && window.Battle!.UpgradesTaken.Count == 0, "A new run starts without the old upgrades");
            UntilOffer();
            SetContext(window, true, 0); Call(window, "TickContext", 1.1); Fight(0.1);
            check(window.BattleUpgradeOpen && !window.BattleUpgradeVisible && (window.BattlePauseReasons & PetWindow.BattlePause.Upgrade) != 0, "A fullscreen app hides the panel; the choice waits");
            SetContext(window, false, 0); Call(window, "TickContext", 1.1); Fight(0.1);
            check(window.BattleUpgradeVisible, "It comes back when the fullscreen app closes");
            prefs.Language = "vi"; Call(window, "UpdateBattleUpgrade");
            check(window.BattleUpgradeText.Contains("Chọn một nâng cấp") && window.BattleUpgradeText.Contains("Xác nhận"), "The panel follows the pet's language");
            prefs.Language = "en";
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review && Find("Pet upgrade")?.Content is FrameworkElement content)
            {
                window.PickBattleUpgrade(window.Battle!.UpgradeOffer![1].Id);
                content.UpdateLayout();
                var shot = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth + 12), (int)Math.Ceiling(content.ActualHeight + 12), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                shot.Render(content);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));
                using var stream = File.Create(Path.Combine(review, "adventure-upgrade-en.png")); encoder.Save(stream);
            }
            window.Reset();
            check(!window.BattleActive && !window.BattleUpgradeOpen && Find("Pet upgrade") == null, "Bringing the pet back closes the battle and the panel");

            // The carried build: the stage before's winning build shows on the setup panel, the run
            // starts with it, and the card that completes a group announces its synergy.
            var progress = window.AdventureProgress;
            progress.Completed.AddRange(new[] { "up", "hold" });
            progress.Builds["hold"] = new Dictionary<string, int> { ["thick_fur"] = 2 };
            check(window.OpenBattlePrep(stage: later) == null && window.BattlePrepText.Contains("Build brought in: Defense 2"), $"The setup panel shows the build brought in ({window.BattlePrepText.Replace('\n', '|')})");
            check(window.StartBattle(stage: later) == null && window.Battle!.Stacks("thick_fur") == 2 && window.Battle.CatMaxHp == 100, "The stage starts with that build");
            hud = Field<BattleHud>(window, "battleHud");
            Fight(0.1);
            check(hud.UpgradesText.Contains("Defense 2/3"), $"The HUD shows the carried points ({hud.UpgradesText})");
            UntilOffer();
            var heart = window.Battle!.UpgradeOffer?.FirstOrDefault(u => u.Group == "defense");
            check(heart?.Id == "big_heart" && window.BattleUpgradeText.Contains("Defense 2 → 3/3 · Thorns!"), "The Defense card says it completes Thorns (Thick fur is at its limit)");
            window.PickBattleUpgrade(heart!.Id); window.ConfirmBattleUpgrade();
            check(window.Battle.Synergies.Any(y => y.Id == "thorns") && Text(hud).Contains("★ THORNS!") && hud.UpgradesText.Contains("★ Thorns") && window.Battle.CatMaxHp == 120,
                $"Taking it switches Thorns on with a banner; the HUD keeps a star for it ({hud.UpgradesText})");
            window.Reset();
            // The rare Defense card (Turtle shell) in its place completes the group just the same.
            window.BattleTuning = Tuning(1);
            check(window.StartBattle(stage: later) == null && window.Battle!.Stacks("thick_fur") == 2, "With rare cards always offered the stage starts with the same build");
            hud = Field<BattleHud>(window, "battleHud");
            Fight(0.1);
            UntilOffer();
            var shell = window.Battle!.UpgradeOffer?.FirstOrDefault(u => u.Group == "defense");
            check(shell?.Id == "turtle_shell" && window.BattleUpgradeText.Contains("Defense 2 → 3/3 · Thorns!"), "The rare Defense card also says it completes Thorns");
            window.PickBattleUpgrade(shell!.Id); window.ConfirmBattleUpgrade();
            check(window.Battle.Synergies.Any(y => y.Id == "thorns") && Text(hud).Contains("★ THORNS!") && hud.UpgradesText.Contains("★ Thorns") && hud.UpgradesText.Contains("◆ Turtle shell") && window.Battle.CatMaxHp == 100,
                $"Taking it switches Thorns on too; the HUD shows the star and the rare card ({hud.UpgradesText})");
            window.Reset();
            window.BattleTuning = tuning;
            progress.Completed.RemoveAll(id => id is "up" or "hold");
            progress.Builds.Clear();

            // A survive stage: the HUD leads with the time left; carrying the cat stops it.
            check(window.StartBattle(stage: hold) == null && window.Battle!.Survival, "A survive stage starts");
            hud = Field<BattleHud>(window, "battleHud");
            Fight(1.2);
            string status = Text(hud);
            check(status.Contains("Survive: 0:2") && window.Battle.SurviveRemaining < 30, $"The HUD shows the survive clock ({status})");
            double leftNow = window.Battle.SurviveRemaining;
            SetField(window, "pressPending", true);
            Fight(1);
            SetField(window, "pressPending", false);
            check(window.Battle.SurviveRemaining == leftNow, "Holding the cat stops the survive clock");
            window.EndBattle();
        }
        finally
        {
            window.Reset();
            window.BattleTuning = tuningBefore;
            window.ReloadAdventureProgress();
            prefs.Language = language;
        }
    }
}
