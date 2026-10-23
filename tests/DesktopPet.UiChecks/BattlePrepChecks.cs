using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopPet;
using static PetPrivate;

// Battle Mode's setup panel in the real pet window: menu → Start battle → setup → Ready → fight.
// Opening, cancelling, Ready (also twice), the cat changing while the panel is open, pause and
// fullscreen, the weapon/difficulty choice reaching the battle, and the user's battle.json with
// its fallback. In-memory preferences and temporary files only.
internal static class BattlePrepChecks
{
    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, idle)));
    private static MouseButtonEventArgs MouseEvent(RoutedEvent routedEvent) => new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = routedEvent };
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    private static int SetupWindows() => Application.Current.Windows.OfType<Window>().Count(w => w.Title == "Pet setup" && w.IsLoaded);
    private static Window? SetupWindow() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet setup" && w.IsLoaded);
    private static int BattleWindows() => Application.Current.Windows.OfType<Window>().Count(w => w.Title.StartsWith("Pet battle") && w.IsLoaded);

    public static async Task Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences prefs, Action<bool, string> check)
    {
        void Prep(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattlePrep", 1.0 / 60); }
        string language = prefs.Language, weapon = prefs.BattleWeapon, level = prefs.BattleDifficulty;
        prefs.Language = "en";
        prefs.BattleWeapon = BattleWeapon.Sword; prefs.BattleDifficulty = "Normal";
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        window.Reset();
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 2 }, MaxAlive = 2, SpawnInterval = 0, SlimeMaxHp = 1000, SlimeDamage = 0 };
        int Records() => window.BattleRecords.Loadouts.Values.Sum(r => r.Wins + r.MostDefeated) + window.BattleRecords.Normal.Wins + window.BattleRecords.Hard.Wins;
        var records = Records();
        var report = window.BattleReport;

        // Start battle opens the panel only: no battle, slime, timer or record yet; one panel only.
        check(window.OpenBattlePrep() == null && window.BattlePreparing && !window.BattleActive && BattleWindows() == 0 && !pet.SwordOut,
            "Start battle opens the setup panel; nothing fights yet");
        await Task.Delay(30);
        var setup = SetupWindow();
        long style = setup == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(setup).Handle, -20).ToInt64();
        check(setup is { ShowActivated: false, ShowInTaskbar: false, IsVisible: true } && (style & 0x08000000) != 0 && (style & 0x20) == 0,
            "The panel is clickable but never takes focus (no keys from other apps) and has no taskbar button");
        check(window.OpenBattlePrep() == null && SetupWindows() == 1, "Starting again while it is open keeps the one panel");
        check(window.BattlePrepWeapon == BattleWeapon.Sword && window.BattlePrepDifficulty == BattleDifficulty.Normal && window.BattlePrepReadyEnabled &&
              window.BattlePrepText.Contains("Get ready") && window.BattlePrepText.Contains("Sword") && window.BattlePrepText.Contains("Hammer") && window.BattlePrepText.Contains("Hard"),
            $"It offers the data's weapons and the difficulties, with the remembered choice ({window.BattlePrepText.Replace('\n', '|')})");
        check(behavior.Quiet && window.BattleOwnsPet, "While it is open the cat's free time belongs to it (no random actions, AI, games or chatter)");

        // Cards come from the data; each shows the weapon's real numbers.
        var tuning = window.BattleTuning;
        var hammerStats = PetWindow.WeaponStats(tuning.Weapon(BattleWeapon.Hammer));
        check(window.BattlePrepCards == tuning.WeaponList.Count && window.BattlePrepText.Contains(hammerStats.Knock.ToString("0")) &&
              window.BattlePrepText.Contains(hammerStats.Speed.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "/s") &&
              window.BattlePrepText.Contains("Close") && window.BattlePrepText.Contains("Get ready"),
            $"One card per weapon in the data, with its own damage/speed/reach/knock-back ({window.BattlePrepCards} cards)");
        // The preview is its own model: choosing and trying a weapon never touches the real cat.
        var preview = window.BattlePrepPreview;
        double realX = pet.X, realY = pet.Y;
        var realAction = pet.Action;
        check(preview != null && preview != pet && preview.SwordOut && !preview.Hammer && !pet.SwordOut, "The preview shows this cat holding the chosen weapon (the real cat holds nothing)");
        window.BattlePrepChooseWeapon(BattleWeapon.Hammer);
        check(preview!.Hammer && Math.Abs(preview.SwordHitAt - tuning.Weapon(BattleWeapon.Hammer).HitFraction) < 1e-9, "Choosing the hammer updates the preview at once");
        check(window.BattlePrepTryWeapon() && window.BattlePrepPreviewPlaying, "Try it swings the preview");
        var hammerSpec = tuning.Weapon(BattleWeapon.Hammer);
        window.StepPreviewSwing(hammerSpec.HitTime * 0.5, hammerSpec);
        double midSwing = preview.SwordSwing;
        window.StepPreviewSwing(hammerSpec.SwingDuration + 0.3, hammerSpec);
        check(midSwing > 0 && midSwing < 1 && preview.SwordSwing < 0 && !window.BattlePrepPreviewPlaying && !window.BattleActive &&
              pet.X == realX && pet.Y == realY && pet.Action == realAction && !pet.Hammer && pet.SwordSwing < 0,
            "The swing plays at the weapon's pace and stops by itself; no battle starts and the real cat is untouched");
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review && SetupWindow()?.Content is FrameworkElement setupContent)
        {
            foreach (var (shotLanguage, shotFile) in new[] { ("en", "battle-setup-en.png"), ("vi", "battle-setup-vi.png"), ("ja", "battle-setup-ja.png") })
            {
                prefs.Language = shotLanguage; Prep(0.3); setupContent.UpdateLayout();
                var shot = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(setupContent.ActualWidth + 12), (int)Math.Ceiling(setupContent.ActualHeight + 12), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                shot.Render(setupContent);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));
                using var stream = File.Create(Path.Combine(review, shotFile)); encoder.Save(stream);
            }
            prefs.Language = "en"; Prep(0.3);
        }
        window.BattlePrepTryWeapon();
        window.CancelBattlePrep();
        check(!window.BattlePrepPreviewPlaying && window.BattlePrepPreview == null, "Closing the panel stops the preview (no timer left)");
        window.OpenBattlePrep();
        prefs.Language = "vi";
        Prep(0.3);
        check(window.BattlePrepText.Contains("Chuẩn bị trận") && window.BattlePrepText.Contains("Kiếm") && window.BattlePrepText.Contains("Đóng"), "The panel follows the pet's language");
        prefs.Language = "en";
        Prep(0.3);

        // Cancel: nothing counted, nothing remembered, the pet's own life comes back.
        window.BattlePrepChooseWeapon(BattleWeapon.Hammer);
        window.BattlePrepChooseDifficulty(BattleDifficulty.Hard);
        window.CancelBattlePrep();
        await Task.Delay(20);
        check(!window.BattlePreparing && !window.BattleActive && SetupWindows() == 0 && !behavior.Quiet && !window.BattleOwnsPet &&
              prefs.BattleWeapon == BattleWeapon.Sword && prefs.BattleDifficulty == "Normal" &&
              Records() == records && window.BattleReport == report,
            "Cancel leaves no window, no win or loss, no saved choice, and gives the pet back");

        // A sleeping cat: the panel opens, the cat sleeps on, Ready waits.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        check(window.OpenBattlePrep() == null && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "The panel opens over a sleeping cat without waking it");
        Prep(0.3);
        check(!window.BattlePrepReadyEnabled && window.BattlePrepText.Contains("asleep") && window.BattlePrepReady() is string sleeping && sleeping.Length > 0 && !window.BattleActive,
            "Ready waits while the cat sleeps, and says why");
        window.CancelBattlePrep();
        window.Reset(); behavior.WakeByClick(pet); pet.Emotions.FinishSleep(0); pet.Action = PetAction.Idle;

        // No away nap while the panel is open.
        window.OpenBattlePrep();
        SetContext(window, false, 400); Call(window, "TickContext", 1.1);
        check(pet.Action != PetAction.Sleeping && !behavior.HoldSleep, "Leaving the computer does not start an away nap during the setup");
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);

        // A drag during the setup: Ready waits until the cat is down.
        SetField(window, "dragging", true); pet.Action = PetAction.Dragging; pet.Grounded = false;
        Prep(0.3);
        check(!window.BattlePrepReadyEnabled && window.BattlePrepReady() != null && !window.BattleActive && window.BattlePreparing, "Ready waits while the cat is dragged");
        SetField(window, "dragging", false); pet.Action = PetAction.Idle; pet.Grounded = true; pet.VX = pet.VY = 0;
        Prep(0.3);
        check(window.BattlePrepReadyEnabled, "and is back once the cat is down");

        // Pause and fullscreen: Ready waits; a fullscreen app hides the panel until it closes.
        window.SetPaused(true);
        Prep(0.3);
        check(window.BattlePreparing && !window.BattlePrepReadyEnabled && window.BattlePrepReady() != null && !window.BattleActive, "Ready waits while the pet is paused");
        window.SetPaused(false);
        SetContext(window, true, 0); Call(window, "TickContext", 1.1); Prep(0.3);
        check(window.BattlePreparing && !window.BattlePrepVisible && window.BattlePrepReady() != null && !window.BattleActive, "A fullscreen app hides the panel and Ready waits");
        SetContext(window, false, 0); Call(window, "TickContext", 1.1); Prep(0.3);
        check(window.BattlePrepVisible && window.BattlePrepReadyEnabled, "It comes back when the fullscreen app closes");
        window.CancelBattlePrep();
        window.SetPaused(true);
        check(window.OpenBattlePrep() is string pausedReason && pausedReason.Contains("paused") && !window.BattlePreparing, "A paused pet does not open the panel");
        window.SetPaused(false);

        // Ready: the choice goes into the battle and is remembered; a double click makes one battle.
        window.OpenBattlePrep();
        window.BattlePrepChooseWeapon(BattleWeapon.Hammer);
        window.BattlePrepChooseDifficulty(BattleDifficulty.Hard);
        var ready = Field<Border>(window, "prepReady");
        ready.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        var first = window.Battle;
        ready.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        check(window.BattlePrepReady() != null && window.Battle == first && first != null, "A second Ready (double click) starts nothing more");
        await Task.Delay(30);
        check(window.Battle is { Difficulty: BattleDifficulty.Hard } b && b.Weapon.Id == BattleWeapon.Hammer && pet.Hammer && !window.BattlePreparing &&
              SetupWindows() == 0 && BattleWindows() == 2 && prefs.BattleWeapon == BattleWeapon.Hammer && prefs.BattleDifficulty == "Hard",
            "Ready starts one battle with the chosen hammer and Hard, closes the panel and remembers the choice");
        check(window.OpenBattlePrep() is string running && running.Length > 0 && !window.BattlePreparing, "No setup while a battle runs");
        window.EndBattle();
        check(window.OpenBattlePrep() == null && window.BattlePrepWeapon == BattleWeapon.Hammer && window.BattlePrepDifficulty == BattleDifficulty.Hard,
            "The next setup starts from the last choice");
        window.Reset();
        check(!window.BattlePreparing && SetupWindows() == 0 && !behavior.Quiet, "Bringing the pet back closes the panel");

        // The app icons' chip (2026-10-24): not for a free battle; elsewhere a choice only Ready keeps.
        check(window.OpenBattlePrep() == null && Field<TextBlock>(window, "prepAppsHint").Visibility == Visibility.Collapsed && !window.BattlePrepChooseApps(false),
            "A free battle's setup has no app chip (it never has apps)");
        window.CancelBattlePrep();
        bool appsBefore = prefs.BattleApps;
        var prepTuning = window.BattleTuning;
        window.BattleTuning = BattleData.Settings; // The test data has no Expedition.
        string? expeditionPrep = window.OpenBattlePrep(forExpedition: true);
        check(expeditionPrep == null && window.BattlePrepApps == appsBefore && Field<TextBlock>(window, "prepAppsHint").Visibility == Visibility.Visible
              && Field<TextBlock>(window, "prepAppsLabel").Text == "On the screen (whole run)", $"A new Expedition run's setup has the app chip, for the whole run ({expeditionPrep ?? Field<TextBlock>(window, "prepAppsLabel").Text})");
        check(window.BattlePrepChooseApps(false) && !window.BattlePrepApps && Field<TextBlock>(window, "prepAppsHint").Text.Contains("Records are kept apart"),
            "Clicking the chip turns the apps off and says the records are kept apart");
        window.Reset();
        window.BattleTuning = prepTuning;
        check(prefs.BattleApps == appsBefore, "Leaving the panel without Ready keeps the remembered choice");

        // The user's battle.json: read when the panel opens, used if valid, shown if not.
        string dir = Path.Combine(Path.GetTempPath(), "pet-prep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "battle.json");
        try
        {
            SetField(window, "battleTuningFixed", false);
            window.BattleDataFile = file;
            File.WriteAllText(file, """
                { "schemaVersion": 1,
                  "weapons": [ { "id": "spear", "nameVi": "Giáo", "nameEn": "Spear", "style": "sword", "damage": 18, "range": 110, "attackCooldown": 0.9, "swingDuration": 0.4, "hitTime": 0.2, "knock": 0 } ],
                  "enemies": [ { "id": "stone_slime", "maxHp": 120, "scale": 1.35 } ] }
                """);
            window.OpenBattlePrep();
            check(window.BattleDataCustom && window.BattleDataError == null && window.BattlePrepText.Contains("Spear") && window.BattlePrepText.Contains("custom"),
                $"A valid file is used: its new weapon is on the panel ({window.BattleDataError})");
            window.BattlePrepChooseWeapon("spear");
            check(window.BattlePrepReady() == null && window.Battle?.Weapon.Id == "spear" && !pet.Hammer && prefs.BattleWeapon == "spear", "The custom weapon goes into the battle");
            var started = window.Battle!.Settings;
            File.WriteAllText(file, """{ "schemaVersion": 1, "enemies": [ { "id": "slime", "maxHp": 5 } ] }""");
            check(window.Battle!.Settings == started && started.Enemy("slime").MaxHp == BattleData.Settings.Enemy("slime").MaxHp,
                "Editing the file mid-battle does not change the running battle");
            window.EndBattle();
            File.WriteAllText(file, """{ "schemaVersion": 1, "enemies": [ { "id": "slime", "maxHp": -5 } ] }""");
            window.OpenBattlePrep();
            check(window.BattleDataError is string error && error.Contains("maxHp") && window.BattlePrepText.Contains("built-in battle is used") &&
                  window.BattleTuning == BattleData.Settings && window.BattlePrepWeapon == BattleWeapon.Sword && window.BattlePrepReadyEnabled,
                $"A broken file falls back to the built-in battle, says what is wrong, and an old choice no longer there reads as the first weapon ({window.BattleDataError})");
            window.CancelBattlePrep();
        }
        finally
        {
            window.BattleDataFile = null;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        prefs.Language = language; prefs.BattleWeapon = weapon; prefs.BattleDifficulty = level;
        window.Reset();
        window.BattleTuning = new BattleSettings();
    }
}
