using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopPet;
using static PetPrivate;

// 1.5 M22: chapter 2's look and rewards. Drawn for real: the Download Dump scene (map banner, hub card),
// the map's new landmarks (folder, download, page), the Zip Slime plush on its plank, the Folder hat on
// the cat, and the backdrop behind chapter 2's battles (heaps at both ends, the middle left clear). In
// the pet window: the map's banner follows the chapter shown; the hub's Adventure card shows the latest
// open chapter; a chapter 2 battle has its click-through backdrop window under the strip (chapter 1 and
// the free battle none; gone when the battle ends, hidden for a fullscreen app); the plush, once won,
// stands on the display corner, falls and lands like the other keepsakes; the Rewards page lists it.
// PET_RENDER_REVIEW=<folder> saves scene-dump.png, landmarks-dump.png, keepsake-plush.png,
// accessory-folder-hat.png and battle-scenery-dump.png (dark and light backgrounds).
internal static class ChapterArtChecks
{
    private static readonly Color FolderYellow = Color.FromRgb(0xF2, 0xC6, 0x5C), Felt = Color.FromRgb(0xF2, 0xC6, 0x5C);

    private static int Inked(byte[] px, int w, int h, int x0, int x1, int y0, int y1)
    {
        int n = 0;
        for (int y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(w, x1); x++)
                if (px[(y * w + x) * 4 + 3] > 8) n++;
        return n;
    }

    private sealed class SceneBox : FrameworkElement
    {
        public PetSceneKind Kind { get; init; }
        protected override void OnRender(DrawingContext dc) => PetScene.Draw(dc, Kind, ActualWidth, ActualHeight);
    }

    public static void Run(Action<bool, string> check)
    {
        // The scene: its own picture (not the meadow), folder yellow in the part a mode card keeps.
        var dump = SkyVisualChecks.Render(new SceneBox { Kind = PetSceneKind.Dump }, 480, 120, "scene-dump");
        var meadow = SkyVisualChecks.Render(new SceneBox { Kind = PetSceneKind.Adventure }, 480, 120);
        int folders = SkyVisualChecks.Count(dump, 480, 120, FolderYellow, 0, 480, 0, 120), middle = SkyVisualChecks.Count(dump, 480, 120, Color.FromRgb(0xE8, 0xB8, 0x4A), 152, 328, 0, 120);
        check(!dump.AsSpan().SequenceEqual(meadow) && folders > 300 && middle > 300,
            $"The Download Dump scene has its own picture: folders ({folders} pixels) and the Zip Slime in the middle a mode card keeps ({middle})");
        check(PetScene.ForChapter("dump") == PetSceneKind.Dump && PetScene.ForChapter(null) == PetSceneKind.Adventure, "A chapter's scene picks its picture (none: the meadow)");

        // The map's landmarks for the Download Dump.
        var marks = Enumerable.Range(0, 5).Select(i => PetWindow.StageLandmark(i, 5, ChapterSpec.Dump)).ToArray();
        check(marks.SequenceEqual(new[] { PetMotif.Download, PetMotif.Folder, PetMotif.Fish, PetMotif.File, PetMotif.Crown })
              && Enumerable.Range(0, 5).Select(i => PetWindow.StageLandmark(i, 5)).SequenceEqual(new[] { PetMotif.Flag, PetMotif.Sword, PetMotif.Shield, PetMotif.Mushroom, PetMotif.Crown }),
            "Chapter 2's map has its own landmarks (download, folder, fish, page, crown); chapter 1's are unchanged");
        var row = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Background = Brushes.Transparent };
        foreach (var m in new[] { PetMotif.Download, PetMotif.Folder, PetMotif.File }) row.Children.Add(new PetIllustration(m, 64));
        var icons = SkyVisualChecks.Render(row, 192, 64, "landmarks-dump");
        bool drawn = Enumerable.Range(0, 3).All(i => Inked(icons, 192, 64, i * 64 + 8, i * 64 + 56, 8, 56) > 900);
        check(drawn && SkyVisualChecks.Count(icons, 192, 64, FolderYellow, 64, 128, 0, 64) > 150, "The folder, download and page icons are drawn (the folder in folder yellow)");

        // The plush on its plank, as the display corner and the Rewards tile draw it.
        var plush = BattleData.Settings.Campaign!.Chapters[1].Stages[4].Keepsake!;
        var art = new KeepsakeArt { Scale = PetWindow.DisplayScale, Items = new[] { plush } };
        int aw = (int)Math.Ceiling(art.Width), ah = (int)Math.Ceiling(art.Height);
        var px = SkyVisualChecks.Render(art, aw, ah, "keepsake-plush");
        int felt = SkyVisualChecks.Count(px, aw, ah, Felt, 0, aw, 0, ah), teeth = SkyVisualChecks.Count(px, aw, ah, Color.FromRgb(0x8C, 0x6A, 0x2E), 0, aw, 0, ah, 30);
        check(Math.Abs(KeepsakeArt.WidthFor(new[] { plush }) - (KeepsakeArt.PlushWidth + KeepsakeArt.Pad * 2)) < 0.01 && felt > 400 && teeth > 6,
            $"The Zip Slime plush stands on its plank: felt ({felt} pixels) with the zipper down its front ({teeth})");

        // The Folder hat on the cat (Settings' preview cat).
        var hat = AccessoryOn("FolderHat"); var bare = AccessoryOn("None");
        check(!hat.AsSpan().SequenceEqual(bare) && SkyVisualChecks.Count(hat, 192, 192, Color.FromRgb(0xFF, 0xD8, 0x73), 0, 192, 0, 192) > 60,
            "The Folder hat sits on the cat's head");

        // The battle backdrop: heaps at both ends, the middle clear (pages only), nothing takes the mouse.
        var view = new BattleSceneryView();
        int w = 1600, h = (int)BattleFieldView.FieldHeight;
        var strip = SkyVisualChecks.Render(view, w, h, "battle-scenery-dump");
        int left = Inked(strip, w, h, 0, 170, 0, h), right = Inked(strip, w, h, w - 170, w, 0, h), middleInk = Inked(strip, w, h, 300, w - 300, 0, h - 20);
        check(left > 3000 && right > 3000 && middleInk == 0 && Inked(strip, w, h, 0, w, 0, h - 110) == 0 && !view.IsHitTestVisible,
            $"The backdrop has heaps at both ends ({left}, {right} pixels), the middle above the floor clear ({middleInk}), low on the strip, and takes no clicks");
    }

    private static byte[] AccessoryOn(string accessory)
    {
        var renderer = new PetRenderer(new PetModel()) { Accessory = accessory };
        return SkyVisualChecks.Render(renderer, 192, 192, accessory == "FolderHat" ? "accessory-folder-hat" : null);
    }

    public static async Task RunWindow(PetWindow window, PetModel pet, PetPreferences prefs, Action<bool, string> check)
    {
        var tuningBefore = window.BattleTuning;
        string language = prefs.Language;
        string? shownBefore = prefs.TrophyShown;
        bool trophiesBefore = prefs.ShowTrophies;
        var progress = window.AdventureProgress;
        var completedBefore = progress.Completed.ToList();
        var rewardsBefore = progress.Rewards.ToList();
        var seenBefore = progress.RewardsSeen.ToList();
        void ToGround() { pet.Grounded = true; pet.VX = pet.VY = 0; pet.Action = PetAction.Idle; pet.Y = Field<Area>(window, "bounds").Bottom - 112; }
        Window? Find(string title) => Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.Title == title && x.IsLoaded);
        try
        {
            prefs.Language = "en";
            window.BattleTuning = BattleData.Settings;
            window.Reset();

            // The map's banner and the hub card follow the chapter.
            check(window.OpenAdventureMap() == null && window.AdventureMapChapter == "slime_invasion" && window.AdventureMapScene == PetSceneKind.Adventure,
                $"Chapter 1's map has the meadow banner ({window.AdventureMapScene})");
            Call(window, "CloseAdventureMap");
            progress.Completed.Add(PetWindow.StageSlimeKing);
            check(window.OpenAdventureMap(PetWindow.StageDownloadGate) == null && window.AdventureMapChapter == "download_dump" && window.AdventureMapScene == PetSceneKind.Dump,
                $"Chapter 2's map has the Download Dump banner ({window.AdventureMapScene})");
            check(window.AdventureShowChapter(-1) && window.AdventureMapScene == PetSceneKind.Adventure && window.AdventureShowChapter(1) && window.AdventureMapScene == PetSceneKind.Dump,
                "Turning the chapters with ‹ › turns the banner too");
            Call(window, "CloseAdventureMap");
            check(window.OpenBattleHub() == null, "(setup) the hub opens");
            for (double t = 0; t < 0.3; t += 1.0 / 60) Call(window, "TickBattleHub", 1.0 / 60);
            check(window.BattleHubAdventureScene == PetSceneKind.Dump, $"The hub's Adventure card shows the latest open chapter's scene ({window.BattleHubAdventureScene})");
            window.CancelBattleHub();

            // A chapter 2 battle has the backdrop under its strip; chapter 1 and the free battle none.
            ToGround();
            check(window.StartBattle(stage: window.Adventure!.Stage(PetWindow.StageDownloadGate)) == null, "(setup) a chapter 2 battle starts");
            Call(window, "TickBattle", 1.0 / 60);
            await Task.Delay(30);
            var scenery = Find("Pet battle scenery");
            var field = Find("Pet battle");
            long style = scenery == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(scenery).Handle, -20).ToInt64();
            bool same = scenery != null && field != null && GetWindowRect(new WindowInteropHelper(scenery).Handle, out var a) && GetWindowRect(new WindowInteropHelper(field).Handle, out var b)
                        && a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
            check(window.BattleSceneryShown && window.BattleSceneryKind == ChapterSpec.Dump && (style & 0x20) != 0 && (style & 0x08000000) != 0 && scenery is { ShowInTaskbar: false } && same,
                "A chapter 2 battle shows the Download Dump backdrop: click-through, never takes focus, no taskbar button, the strip's place and size");
            check(scenery != null && field != null && Below(new WindowInteropHelper(scenery).Handle, new WindowInteropHelper(field).Handle),
                "The backdrop is under the strip (the slimes are drawn over it)");
            SetField(window, "contextProbe", new Func<(bool, double)>(() => (true, 0)));
            Call(window, "TickContext", 1.1); Call(window, "TickBattle", 1.0 / 60);
            check(!window.BattleSceneryShown, "A fullscreen app hides the backdrop with the strip");
            SetField(window, "contextProbe", new Func<(bool, double)>(() => (false, 0)));
            Call(window, "TickContext", 1.1); Call(window, "TickBattle", 1.0 / 60);
            check(window.BattleSceneryShown, "and it comes back with the strip");
            window.EndBattle();
            check(!window.BattleSceneryShown && Find("Pet battle scenery") == null, "Ending the battle closes the backdrop");
            window.Reset(); ToGround();
            check(window.StartBattle(stage: window.Adventure!.Stage(PetWindow.StageFirstSteps)) == null && !window.BattleSceneryShown, "A chapter 1 battle has no backdrop");
            window.EndBattle();
            window.Reset(); ToGround();
            check(window.StartBattle() == null && !window.BattleSceneryShown, "The free battle has no backdrop");
            window.EndBattle();
            window.Reset();

            // The plush: won with the Zip Slime stage, on the display corner, a keepsake with physics.
            check(!window.ShownKeepsakes.Any(k => k.Id == "zip_plush"), "Before the Zip Slime is beaten the plush is not on the display corner");
            progress.Completed.Add(PetWindow.StageFishBasket); progress.Completed.Add(PetWindow.StageZipSlime);
            check(window.Adventure!.GrantRewards().Contains("zip_plush") && window.AccessoryAvailable("FolderHat"), "(setup) the chapter 2 rewards are given");
            check(window.ShownKeepsakes.Any(k => k.Id == "zip_plush") && window.DisplayableKeepsakes.Any(k => k.Id == "zip_plush"),
                "Won, the plush is a keepsake the display corner and Settings offer");
            prefs.ShowTrophies = true; prefs.TrophyShown = "zip_plush";
            Call(window, "TickTrophies");
            await Task.Delay(30);
            var corner = Find("Pet trophies")?.Content as KeepsakeArt;
            var area = Field<Area>(window, "bounds");
            check(window.TrophiesVisible && corner != null && corner.Items.Select(k => k.Id).SequenceEqual(new[] { "zip_plush" })
                  && Math.Abs(window.TrophyBounds.Width - KeepsakeArt.WidthFor(corner.Items) * PetWindow.DisplayScale) < 0.5,
                "Chosen, the plush stands on the display corner");
            var motion = window.TrophyMotion;
            motion.X = (area.Left + area.Right) / 2 + 200; motion.Height = 300; motion.VX = motion.VY = 0;
            for (double t = 0; t < 0.2; t += 1.0 / 60) Call(window, "TickKeepsake", 1.0 / 60);
            check(motion.Height > 0 && motion.Height < 300, "Let go up high, the plush falls");
            for (double t = 0; t < 3; t += 1.0 / 60) Call(window, "TickKeepsake", 1.0 / 60);
            check(motion.Resting && motion.Height == 0 && Math.Abs(window.TrophyBounds.Bottom - area.Bottom) < 1, "and lands on the floor like the other keepsakes");
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review && Find("Pet trophies")?.Content is FrameworkElement shown)
            {
                shown.UpdateLayout();
                var image = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(shown.ActualWidth), (int)Math.Ceiling(shown.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                image.Render(shown);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                using var stream = System.IO.File.Create(System.IO.Path.Combine(review, "trophies-plush.png")); encoder.Save(stream);
            }

            // The Rewards page lists the plush and the hat.
            check(window.OpenBattleHub() == null && window.ShowBattleRewards(true) == null, "(setup) the Rewards page opens");
            string rewards = window.BattleRewardsText;
            check(rewards.Contains("Adventure keepsakes") && rewards.Contains("✓ Zip Slime plush") && rewards.Contains("✓ Folder hat"),
                $"Rewards lists the Folder hat and the Zip Slime plush, both won ({rewards.Replace('\n', '|')})");
            window.ShowBattleRewards(false);
            window.CancelBattleHub();
        }
        finally
        {
            window.EndBattle();
            progress.Completed.Clear(); progress.Completed.AddRange(completedBefore);
            progress.Rewards.Clear(); progress.Rewards.AddRange(rewardsBefore);
            progress.RewardsSeen.Clear(); progress.RewardsSeen.AddRange(seenBefore);
            prefs.TrophyShown = shownBefore; prefs.ShowTrophies = trophiesBefore;
            Call(window, "TickTrophies");
            Call(window, "CloseAdventureMap");
            window.Reset();
            window.BattleTuning = tuningBefore;
            prefs.Language = language;
        }
    }

    // a is somewhere below b in the z-order (GetWindow(GW_HWNDNEXT) walks down from b).
    private static bool Below(IntPtr a, IntPtr b)
    {
        for (IntPtr next = GetWindow(b, 2); next != IntPtr.Zero; next = GetWindow(next, 2))
            if (next == a) return true;
        return false;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    private struct Rect { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
}
