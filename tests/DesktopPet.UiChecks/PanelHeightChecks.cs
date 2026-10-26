using System.Reflection;
using System.Windows;
using DesktopPet;
using static PetPrivate;

// The game panels at their tallest with the built-in data, in both languages, against a screen
// 480 DIPs tall (1280×720 at 150 %): each opens 12 DIPs below the top, so it has 456 DIPs.
// Measurement first (the Expedition map was found too tall in milestone 5). In-memory progress only.
internal static class PanelHeightChecks
{
    private static Window? Find(string title) => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == title && w.IsLoaded);
    public const double Room = 480 - 24;

    private static double Height(string title)
    {
        if (Find(title) is not Window w) return -1;
        w.UpdateLayout();
        return w.ActualHeight;
    }

    public static async Task Run(PetWindow window, PetModel pet, PetPreferences prefs, Action<bool, string> check)
    {
        string language = prefs.Language;
        var tuningBefore = window.BattleTuning;
        var data = BattleData.Settings;
        window.Reset();
        window.BattleTuning = data;
        window.ReloadAdventureProgress();
        try
        {
            // Every stage won with records, so the map and setup show all they can.
            var stages = data.Campaign!.Chapters.SelectMany(c => c.Stages).ToList();
            var adventure = window.Adventure!;
            int run = 0;
            foreach (var stage in stages)
                foreach (var level in new[] { BattleDifficulty.Easy, BattleDifficulty.Normal, BattleDifficulty.Hard })
                    adventure.Apply("height-" + run++, stage, true, true, BattleWeapon.Sword, level, 42, 300, new Dictionary<string, int> { ["sharp"] = 2, ["big_heart"] = 1, ["quick_feet"] = 1, ["thorns"] = 1, ["turtle_shell"] = 1 });

            var tallest = new Dictionary<string, (double Height, string Where)>();
            void Note(string panel, string where)
            {
                double h = Height(panel);
                if (panel is "Pet adventure" or "Pet result" && Find(panel) is Window shown)
                {
                    var field = FieldOf("bounds"); var area = (Area)field.GetValue(window)!;
                    try
                    {
                        field.SetValue(window, area with { Bottom = area.Top + 480 });
                        Call(window, "PlaceGamePanel", shown); shown.UpdateLayout();
                        var frame = (System.Windows.Controls.Border)shown.Content;
                        var dock = (System.Windows.Controls.DockPanel)frame.Child;
                        var actions = (FrameworkElement)dock.Children[0];
                        double bottom = actions.TransformToAncestor(shown).Transform(new Point(0, actions.ActualHeight)).Y;
                        var scroll = dock.Children.OfType<System.Windows.Controls.ScrollViewer>().Single();
                        check(bottom <= shown.ActualHeight && shown.ActualHeight <= Room + 0.5 && (scroll.ExtentHeight <= scroll.ViewportHeight + 0.5 || scroll.ScrollableHeight > 0),
                            panel + " keeps actions visible and tall content scrolls on a 480-DIP screen: " + where);
                        h = Height(panel);
                        var banner = (PetScene)shown.Resources["PetBanner"];
                        bool none = banner.Visibility == Visibility.Collapsed;
                        field.SetValue(window, area with { Bottom = area.Top + 640 });
                        Call(window, "PlaceGamePanel", shown);
                        check(none && banner.Visibility == Visibility.Visible && banner.Height == PetWindow.ShortBannerHeight,
                            panel + ": no banner on a 480-DIP screen, a short one on 640 DIPs");
                    }
                    finally { field.SetValue(window, area); Call(window, "PlaceGamePanel", shown); }
                }
                if (!tallest.TryGetValue(panel, out var best) || h > best.Height) tallest[panel] = (h, where);
            }
            foreach (var lang in new[] { "en", "vi" })
            {
                prefs.Language = lang;
                // The Adventure map, each stage selected.
                foreach (var stage in stages)
                {
                    window.OpenAdventureMap(stage.Id);
                    for (int i = 0; i < 20; i++) Call(window, "TickAdventureMap", 1.0 / 60);
                    await Task.Delay(10);
                    Note("Pet adventure", $"{lang} {stage.Id}");
                }
                window.Reset();
                // The setup panel: the free battle and every stage.
                foreach (var stage in stages.Prepend(null))
                {
                    window.OpenBattlePrep(stage: stage);
                    await Task.Delay(10);
                    Note("Pet setup", $"{lang} {stage?.Id ?? "free"}");
                    window.Reset();
                }
                // The result panel at its fullest: a first stage win (records, opened stage, reward,
                // a save that failed), a won Expedition run (keepsakes, records), a free battle record.
                var last = stages[^1];
                var rewards = PetAccessories.All.Where(a => a.Reward != null).Select(a => a.Id).Take(2).ToList();
                var summaries = new (string Where, PetWindow.BattleSummary Summary)[]
                {
                    ("stage", new(true, 30, 312, 64, 120, Boss: true, BossName: "Slime King", Stage: stages[^2], Upgrades: "Sharp edge ×2 · Big heart · Quick paws",
                        Outcome: new AdventureOutcome(true, true, last, new BattleRecordNews(true, true), rewards), News: new BattleRecordNews(true, true))),
                    ("expedition", new(true, 30, 312, 64, 120, Boss: true, BossName: "Slime King", Stage: data.Expedition!.Boss, Upgrades: "Sharp edge ×2 · Big heart · Quick paws",
                        Expedition: new ExpeditionStepOutcome(true, 5, true, true, true, 64, 120, 0, true, true, false, data.Expedition.Keepsakes.Select(k => k.Id).ToList()))),
                    ("free", new(true, 30, 312, 64, 120, Boss: true, BossName: "Slime King", News: new BattleRecordNews(true, true))),
                };
                FieldOf("adventureSaveFailed").SetValue(window, true);
                foreach (var (where, summary) in summaries)
                {
                    Call(window, "ShowBattleSummary", summary);
                    await Task.Delay(10);
                    Note("Pet result", $"{lang} {where}");
                    Call(window, "CloseBattleSummary");
                }
                FieldOf("adventureSaveFailed").SetValue(window, false);
            }
            foreach (var panel in new[] { "Pet adventure", "Pet result" })
                check(tallest[panel].Height > 100 && tallest[panel].Height <= Room,
                    $"{panel} at its tallest fits a 480-DIP screen ({tallest[panel].Height:0} of {Room:0} DIPs, {tallest[panel].Where})");

            // The setup panel is taller (a stage's goal, tip and build): on that screen it is capped,
            // its content scrolls and Ready stays in view; on a big enough one it keeps its height.
            var (natural, at) = tallest["Pet setup"];
            var setupStage = stages.FirstOrDefault(s => at.EndsWith(" " + s.Id));
            prefs.Language = at[..2];
            var boundsField = FieldOf("bounds");
            var realBounds = (Area)boundsField.GetValue(window)!;
            boundsField.SetValue(window, realBounds with { Bottom = realBounds.Top + 480 });
            window.OpenBattlePrep(stage: setupStage);
            await Task.Delay(10);
            double capped = Height("Pet setup");
            var setup = Find("Pet setup")!;
            static IEnumerable<DependencyObject> All(DependencyObject node) => LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().SelectMany(All).Prepend(node);
            var scroller = All(setup).OfType<System.Windows.Controls.ScrollViewer>().FirstOrDefault();
            var ready = (FrameworkElement)FieldOf("prepReady").GetValue(window)!;
            double readyBottom = ready.TransformToAncestor(setup).Transform(new Point(0, ready.ActualHeight)).Y;
            check(capped <= Room + 0.5 && scroller is { ScrollableHeight: > 0 } && readyBottom <= capped,
                $"On a 480-DIP screen the tallest setup panel is capped ({natural:0} → {capped:0} DIPs, {at}): the content scrolls, Ready stays in view");
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review && setup.Content is FrameworkElement content)
            {
                var shot = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth + 12), (int)Math.Ceiling(content.ActualHeight + 12), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                shot.Render(content);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));
                using var stream = System.IO.File.Create(System.IO.Path.Combine(review, "battle-setup-small-screen.png")); encoder.Save(stream);
            }
            window.Reset();
            boundsField.SetValue(window, realBounds);
            window.OpenBattlePrep(stage: setupStage);
            await Task.Delay(10);
            double back = Height("Pet setup");
            check(Math.Abs(back - natural) < 1 || realBounds.Bottom - realBounds.Top < natural + 24, $"On a big enough screen the setup panel keeps its full height ({back:0} DIPs)");
        }
        finally
        {
            window.Reset();
            prefs.Language = language;
            window.BattleTuning = tuningBefore;
            window.ReloadAdventureProgress();
        }
    }
}
