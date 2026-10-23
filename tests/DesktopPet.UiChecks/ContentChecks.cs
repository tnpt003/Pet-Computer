using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using DesktopPet;
using static PetPrivate;

// Adventure milestone 4 in the real pet window: reward accessories (hidden in Settings until
// received, offered after, never put on by themselves), the result panel's reward line, End
// going back to the map, and the herb slime drawn while it heals. In-memory progress only.
internal static class ContentChecks
{
    private static Window? Find(string title) => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == title && w.IsLoaded);
    private static string Text(DependencyObject node) => node is TextBlock t ? t.Text + "|" : string.Concat(LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().Select(Text));

    public static async Task Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences prefs, Action<bool, string> check)
    {
        void Fight(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattle", 1.0 / 60); }
        string language = prefs.Language, accessory = prefs.Accessory;
        bool holiday = prefs.HolidayAccessories;
        var tuningBefore = window.BattleTuning;
        prefs.Language = "en"; prefs.Accessory = "None"; prefs.HolidayAccessories = false;
        window.Reset();
        window.ReloadAdventureProgress();
        var renderer = (PetRenderer)window.Content;
        try
        {
            // Settings: no reward accessory before it is received.
            string[] Offered()
            {
                window.ShowPetSettings();
                var form = Field<PetSettingsForm>(window, "settingsForm");
                var ids = form.AccessoryIds.ToArray();
                form.Cancel.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                return ids;
            }
            var before = Offered();
            check(!before.Contains("SlimeHat") && !before.Contains("SlimeKingCrown") && before.Length == 12, "Settings do not offer the Adventure rewards before they are received");

            // A stage giving the Slime hat: one weak slime, then the result names the reward.
            var stage = new StageSpec { Id = "reward_stage", NameVi = "Thưởng", NameEn = "Prize", Reward = "SlimeHat",
                Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 } } } } };
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, SlimeMaxHp = 1, OutroSeconds = 0.5,
                Campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "c", NameVi = "C", NameEn = "C", Stages = new[] { stage } } } } };
            check(window.StartBattle(stage: stage) == null, "The reward stage starts");
            Fight(0.6);
            if (window.Battle is { } b && b.Slimes.Count > 0) b.Slimes[0].X = pet.X + 56 + 50;
            for (int i = 0; i < 200 && window.BattleActive; i++) Fight(0.05);
            await Task.Delay(30);
            string panel = Find("Pet result")?.Content is DependencyObject content ? Text(content) : "";
            check(window.AdventureProgress.Rewards.SequenceEqual(new[] { "SlimeHat" }) && panel.Contains("New reward: Slime hat") && panel.Contains("Settings → Cat"),
                $"Winning gives the Slime hat once and the result says where to wear it ({panel})");
            check(prefs.Accessory == "None" && renderer.Accessory == "None", "The cat does not put the reward on by itself");

            // Settings now offer it; choosing it by its id works (the list is not the full accessory list).
            window.ShowPetSettings();
            var form = Field<PetSettingsForm>(window, "settingsForm");
            int index = Array.IndexOf(form.AccessoryIds, "SlimeHat");
            check(index > 0 && form.AccessoryIds.Length == 13 && !form.AccessoryIds.Contains("SlimeKingCrown"), "After the win Settings offer the Slime hat (and not the crown yet)");
            form.Accessory.SelectedIndex = index;
            check(form.PetPreview.Accessory == "SlimeHat", "The settings preview shows the Slime hat");
            form.Save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            check(prefs.Accessory == "SlimeHat" && renderer.Accessory == "SlimeHat", "Saving puts it on the cat");
            // Progress lost (a new file): the worn reward stays listed and worn.
            window.ReloadAdventureProgress();
            var worn = Offered();
            check(worn.Contains("SlimeHat") && !worn.Contains("SlimeKingCrown") && prefs.Accessory == "SlimeHat", "A worn reward stays offered even without its progress");

            // End during a stage: back to the map; during a free battle: back to the desktop.
            check(window.StartBattle(stage: stage) == null, "The stage starts again");
            window.EndBattleByUser();
            check(!window.BattleActive && window.AdventureMapOpen && window.AdventureSelectedStage == "reward_stage" && Find("Pet result") == null,
                "End during a stage goes back to the map (nothing counted)");
            Tick(window, 0.3);
            check(window.AdventureMapText.Contains("Reward: Slime hat"), $"The map shows the stage's reward ({window.AdventureMapText.Replace('\n', '|')})");
            window.CancelAdventureMap(); window.CancelBattleHub();
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, SlimeMaxHp = 1000 };
            check(window.StartBattle() == null, "A free battle starts");
            window.EndBattleByUser();
            check(!window.BattleActive && !window.AdventureMapOpen, "End during a free battle goes back to the desktop as before");

            // The herb slime winding up a heal, drawn (PET_RENDER_REVIEW only writes the picture).
            var heal = new StageSpec { Id = "heal_stage", NameVi = "H", NameEn = "H",
                Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 }, new WaveGroup { Enemy = "mender", Count = 1 } } } } };
            window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, CatDamage = 0, Enemies = BattleData.Settings.EnemyTypes.ToArray(),
                Campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "c", NameVi = "C", NameEn = "C", Stages = new[] { heal } } } } };
            window.StartBattle(stage: heal);
            Fight(1.5);
            var sim = window.Battle!;
            var healer = sim.Slimes.First(s => s.Type.Id == "mender");
            var friend = sim.Slimes.First(s => s.Type.Id == "slime");
            friend.X = healer.X + 50; friend.Hp = 10;
            for (int i = 0; i < 200 && healer.State != SlimeState.Casting; i++) { friend.X = healer.X + 50; Fight(0.05); }
            check(healer.State == SlimeState.Casting && sim.Healing(healer) == friend, "In the real window the herb slime winds up a heal on its friend");
            var view = Field<BattleFieldView>(window, "battleView");
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review)
            {
                view.UpdateLayout();
                double left = Math.Max(0, Math.Min(healer.X, friend.X) - view.OriginX - 90);
                var full = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)BattleFieldView.FieldHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                full.Render(view);
                var crop = new System.Windows.Media.Imaging.CroppedBitmap(full, new Int32Rect((int)left, 0, Math.Min(320, (int)view.ActualWidth - (int)left), (int)BattleFieldView.FieldHeight));
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(crop));
                using var stream = File.Create(Path.Combine(review, "battle-mender-casting.png")); encoder.Save(stream);
            }
            window.EndBattle();
        }
        finally
        {
            window.Reset();
            window.BattleTuning = tuningBefore;
            window.ReloadAdventureProgress();
            prefs.Language = language; prefs.Accessory = accessory; prefs.HolidayAccessories = holiday;
            Call(window, "ApplyAccessory");
        }
    }

    private static void Tick(PetWindow window, double seconds)
    {
        for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickAdventureMap", 1.0 / 60);
    }
}
