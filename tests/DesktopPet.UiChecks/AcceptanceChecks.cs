using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using DesktopPet;
using static PetPrivate;

// Adventure milestone 5 in the real pet window: a stage's tip (once, pauses the battle, seen is
// saved, again from the setup panel), pauses stacking (tip + upgrade + user), the HUD naming the
// stage, and progress that cannot be saved (map and result say so). Temporary files only.
internal static class AcceptanceChecks
{
    private static Window? Find(string title) => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == title && w.IsLoaded);
    private static string Text(DependencyObject node) => node is TextBlock t ? t.Text + "|" : string.Concat(LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().Select(Text));

    public static async Task Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences prefs, Action<bool, string> check)
    {
        void Fight(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattle", 1.0 / 60); }
        void Win()
        {
            Fight(0.6);
            for (int i = 0; i < 400 && window.BattleActive; i++)
            {
                if (window.Battle is { } b)
                {
                    if (b.ChoosingUpgrade) { window.PickBattleUpgrade(b.UpgradeOffer![0].Id); window.ConfirmBattleUpgrade(); }
                    foreach (var s in b.Slimes) if (s.Targetable) s.X = pet.X + 56 + 50;
                }
                Fight(0.05);
            }
        }
        string language = prefs.Language;
        var tuningBefore = window.BattleTuning;
        prefs.Language = "en";
        window.Reset();
        string dir = Path.Combine(Path.GetTempPath(), "pet-accept-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "adventure-progress.json");
        WaveSpec One() => new() { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 } } };
        var first = new StageSpec { Id = "a_one", NameVi = "Một", NameEn = "One", Waves = new[] { One() } };
        var tipped = new StageSpec { Id = "a_two", NameVi = "Hai", NameEn = "Two", Upgrades = true, TipVi = "Mẹo thử", TipEn = "A test tip", Waves = new[] { One(), One() } };
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, SlimeMaxHp = 1, SlimeDamage = 0, WaveBreakSeconds = 0.5, OutroSeconds = 0.5,
            Campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "c", NameVi = "C", NameEn = "C", Stages = new[] { first, tipped } } } } };
        window.AdventureProgressFile = file;
        window.ReloadAdventureProgress();
        try
        {
            // The HUD names the stage; a short banner shows it as the stage starts.
            check(window.StartBattle(stage: first) == null, "Stage 1 starts");
            var hud = Field<BattleHud>(window, "battleHud");
            check(Text(hud).Contains("· Stage 1"), $"The HUD names the stage next to the cat ({Text(hud)})");
            Win();
            check(window.Adventure!.IsUnlocked("a_two"), "Winning stage 1 opens stage 2");

            // The tip (what the stage teaches) no longer opens by itself (2026-10-24: the Adventure
            // Guide teaches in the battle); from the setup panel it pauses the battle; Done marks it seen.
            check(window.StartBattle(stage: tipped) == null && !window.BattleTutorialOpen && !window.Battle!.Paused, "A stage's tip does not open by itself");
            check(window.ShowStageTip(tipped) && window.BattleTipStage == "a_two" && window.BattleTutorialText.Contains("A test tip") && window.BattleTutorialText.StartsWith("Lessons · "),
                "Opened, the tip shows what the stage teaches");
            check(window.Battle!.Paused && (window.BattlePauseReasons & PetWindow.BattlePause.Tutorial) != 0, "The battle waits while the tip is open");
            window.SetBattlePaused(true);
            window.BattleTutorialNext(); // Done: one card.
            check(!window.BattleTutorialOpen && window.AdventureProgress.Seen.Contains("a_two") && File.ReadAllText(file).Contains("a_two") &&
                  window.BattlePauseReasons == PetWindow.BattlePause.User && window.Battle.Paused,
                "Done closes the tip, saves it as seen and keeps the user's own pause");
            window.SetBattlePaused(false);
            // A tip during an upgrade choice: both reasons hold the battle; each goes on its own.
            Fight(0.6);
            for (int i = 0; i < 200 && !window.BattleUpgradeOpen; i++) { foreach (var s in window.Battle!.Slimes) if (s.Targetable) s.X = pet.X + 56 + 50; Fight(0.05); }
            window.ShowStageTip(tipped);
            check(window.BattleUpgradeOpen && window.BattleTutorialOpen && window.BattlePauseReasons == (PetWindow.BattlePause.Upgrade | PetWindow.BattlePause.Tutorial),
                "An upgrade choice and a tip together: both hold the battle");
            window.PickBattleUpgrade(window.Battle!.UpgradeOffer![0].Id); window.ConfirmBattleUpgrade();
            check(window.BattlePauseReasons == PetWindow.BattlePause.Tutorial && window.Battle.Paused, "Confirming the upgrade leaves the tip's pause");
            window.CloseBattleTutorial(finished: true);
            check(window.BattlePauseReasons == PetWindow.BattlePause.None && !window.Battle.Paused, "Closing the tip lets the battle run");
            window.EndBattleByUser();
            window.CancelAdventureMap(); window.CancelBattleHub();
            // Again from the setup panel.
            window.OpenAdventureMap("a_two");
            window.AdventureEnterStage();
            await Task.Delay(30);
            check(window.BattlePrepText.Contains("What you learn here"), "The stage's setup panel offers its tip");
            check(window.ShowStageTip(window.BattlePrepStage!) && window.BattleTutorialText.Contains("A test tip"), "and shows it");
            window.CloseBattleTutorial(finished: true);
            window.CancelBattlePrep();
            window.CancelAdventureMap(); window.CancelBattleHub();

            // Progress that cannot be saved: the folder is a file. The map and the result say so.
            string blocker = Path.Combine(dir, "blocked");
            File.WriteAllText(blocker, "not a folder");
            window.AdventureProgressFile = Path.Combine(blocker, "adventure-progress.json");
            window.ReloadAdventureProgress();
            check(window.StartBattle(stage: first) == null, "A stage starts with an unwritable progress file");
            Win();
            await Task.Delay(30);
            string result = Find("Pet result")?.Content is DependencyObject content ? Text(content) : "";
            check(window.AdventureSaveFailed && result.Contains("could not be saved") && window.LastBattleSummary is { Won: true },
                $"Saving fails without a crash; the result says progress was not saved ({result})");
            window.OpenAdventureMap();
            for (double t = 0; t < 0.3; t += 1.0 / 60) Call(window, "TickAdventureMap", 1.0 / 60);
            check(window.AdventureMapText.Contains("could not be saved"), "The map says so too");
            window.CancelAdventureMap(); window.CancelBattleHub();

            // A progress file from a newer version: read only, said on the map.
            string newer = Path.Combine(dir, "newer.json");
            File.WriteAllText(newer, "{ \"Version\": 9, \"Completed\": [\"a_one\"] }");
            window.AdventureProgressFile = newer;
            window.ReloadAdventureProgress();
            window.OpenAdventureMap();
            for (double t = 0; t < 0.3; t += 1.0 / 60) Call(window, "TickAdventureMap", 1.0 / 60);
            check(window.AdventureMapText.Contains("newer version") && window.Adventure!.IsUnlocked("a_two"), "A newer version's progress is read, and the map says it is read only");
            window.CancelAdventureMap(); window.CancelBattleHub();
            check(File.ReadAllText(newer).Contains("\"Version\": 9"), "and the file is left as it was");
        }
        finally
        {
            window.Reset();
            window.AdventureProgressFile = null;
            window.ReloadAdventureProgress();
            window.BattleTuning = tuningBefore;
            prefs.Language = language;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
