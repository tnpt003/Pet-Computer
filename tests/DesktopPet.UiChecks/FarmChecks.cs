using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using static PetPrivate;

// The field in the real pet window (1.5.1 M3): where it stands, the steps of a click, moving the
// plot, what hides it, and that it leaves the cat alone. Uses a temporary file and a false clock.
internal static class FarmChecks
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, idle)));
    private static Window? Overlay() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet farm" && w.IsLoaded);

    public static void Run(PetWindow window, PetModel pet, PetPreferences prefs, Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "pet-farm-ui-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "farm.json");
        var now = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        var cursor = new Point(0, 0);
        var realCursor = FieldOf("cursorProbe").GetValue(window);
        void Tick(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickFarm", 1.0 / 60); }
        void Wait(double seconds) { now = now.AddSeconds(seconds); Call(window, "TickFarm", 1.0 / 60); }
        SetField(window, "farmPath", path);
        SetField(window, "farmClock", new Func<DateTime>(() => now));
        SetField(window, "cursorProbe", new Func<Point>(() => cursor));
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        window.Reset();
        try
        {
            Tick(0.05);
            check(!window.FarmVisible && Overlay() == null && !File.Exists(path), "With the setting off there is no field and no file");
            prefs.Farm = true;
            Tick(0.05);
            check(window.FarmVisible && Overlay() != null && window.Farm != null, "Turned on, it appears");

            var overlay = Overlay()!;
            var handle = new WindowInteropHelper(overlay).Handle;
            var area = Field<Area>(window, "farmArea");
            check(Math.Abs(overlay.Top + PetFarmArt.Floor - area.Bottom) < 1.5, $"Its soil stands on the top of the taskbar ({overlay.Top + PetFarmArt.Floor:F0} on {area.Bottom:F0})");
            double free = area.Right - area.Left - PetFarmArt.ZoneWidth;
            check(Math.Abs(overlay.Left + PetFarmArt.Side - (area.Left + free * 0.25)) < 1.5, "At first it is a quarter across the monitor");
            long style = GetWindowLongPtr(handle, -20).ToInt64();
            check((style & 0x20) == 0 && (style & 0x08000000) != 0 && (style & 0x80) != 0, "It takes clicks but no focus, and has no taskbar button");
            check(overlay.Topmost && !overlay.ShowInTaskbar && !overlay.ShowActivated, "An overlay like the others");
            check(Below(new WindowInteropHelper(window).Handle, handle), "The cat walks in front of it");

            // The steps of one tile, by clicks.
            var farm = window.Farm!;
            Shop(window, farm, path, check, () => Tick(0.02));
            Kitchen(window, pet, farm, path, check, () => Tick(0.02));
            Sky(window, farm, check, () => now);
            Bulk(window, pet, farm, path, check, () => now, seconds => now = now.AddSeconds(seconds));
            Levels(window, farm, path, check, () => Tick(0.02));
            Orders(window, pet, farm, path, check, Wait, () => Tick(0.02));
            farm.OpenTiles = 6;
            check(window.FarmTileLabel(0)!.Length > 0 && window.FarmTileLabel(6) == null, "Each tile has a label");
            check(farm.Next(0) == FarmStep.Till, "A new tile needs tilling");
            window.FarmClick(0);
            check(farm.Tiles[0].Stage == FarmStage.Tilled && File.Exists(path), "A click tills it and saves");
            check(farm.Tiles[1].Stage == FarmStage.Wild, "Only that tile");
            window.FarmClick(0);
            check(window.FarmSeedMenuOpen && farm.Tiles[0].Stage == FarmStage.Tilled, "A click on tilled ground opens the seed menu and sows nothing yet");
            Call(window, "CloseFarmSeedMenu");
            check(!window.FarmSeedMenuOpen, "The menu closes");
            var seeds = window.FarmSeedItems(0);
            check(seeds.Length == 4 && seeds.All(s => s.Icon is FarmCropIcon && ((string)s.Header).Length > 0), "The seed menu has four crops with their pictures");
            check(window.FarmPlant(0, FarmCrop.Radish) && farm.Next(0) == FarmStep.Water, "A seed is sown");
            check(!window.FarmPlant(1, FarmCrop.Radish), "Not on wild ground");
            check(window.FarmTileLabel(0)!.Contains("Radish") || window.FarmTileLabel(0)!.Contains("Củ cải"), "The label names the crop");

            double stage = PetFarm.StageSeconds(FarmCrop.Radish);
            Wait(stage * 5);
            check(farm.Tiles[0].Stage == FarmStage.Seed, "An unwatered seed stays a seed");
            foreach (var next in new[] { FarmStage.Sprout, FarmStage.Young, FarmStage.Ripe })
            {
                window.FarmClick(0);
                check(farm.Next(0) == FarmStep.Wait, $"Watered, it grows towards {next}");
                window.FarmClick(0);
                check(farm.Next(0) == FarmStep.Wait && farm.Tiles[0].Stage != next, "Clicking again while it grows does nothing");
                Wait(stage + 1);
                check(farm.Tiles[0].Stage == next, $"The clock brings it to {next}");
            }
            window.FarmClick(0);
            check(farm.HarvestedCount(FarmCrop.Radish) == 1 && farm.Tiles[0].Stage == FarmStage.Wild, "A click harvests it and counts it");
            var gain = (Border)Field<Border?>(window, "farmGain")!;
            check(gain.Opacity == 1 && ((TextBlock)gain.Child).Text.StartsWith("+1 "), "\"+1\" rises over the tile");
            Tick(FarmGainWait);
            check(gain.Opacity < 0.05, "...and fades");

            var reload = PetFarm.Load(path);
            check(reload.HarvestedCount(FarmCrop.Radish) == 1 && reload.Tiles[0].Stage == FarmStage.Wild, "The harvest is in the file");

            // Clicks leave the cat alone, asleep or awake.
            pet.Emotions.Reset(); pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
            window.FarmClick(2);
            Tick(0.2);
            check(pet.Emotions.SleepLocked && pet.Action == PetAction.Sleeping && farm.Tiles[2].Stage == FarmStage.Tilled, "The field works while the cat sleeps, and does not wake it");
            pet.Emotions.Reset(); pet.Action = PetAction.Idle;
            double vx = pet.VX, x = pet.X, y = pet.Y; var action = pet.Action; var emotion = pet.Emotions.Current;
            window.FarmClick(3);
            Tick(1);
            check(pet.VX == vx && pet.X == x && pet.Y == y && pet.Action == action && pet.Emotions.Current == emotion, "It never touches the cat");

            // Pause: no clicks, nothing moves; growth follows the clock.
            farm.Plant(2, FarmCrop.Radish); farm.Water(2, now);
            SetField(window, "paused", true);
            window.FarmClick(4);
            check(farm.Tiles[4].Stage == FarmStage.Wild, "Pause refuses clicks");
            check(window.FarmMenuItems().Length == 0, "...and the menu");
            window.BeginFarmMove();
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Down, "...and moving the plot");
            now = now.AddSeconds(stage + 1);
            Call(window, "TickFarm", 0.0);
            check(farm.Tiles[2].Stage == FarmStage.Sprout, "A crop still grows by the clock in Pause");
            SetField(window, "paused", false);

            // Moving the plot.
            var ground = window.FarmMenuItems();
            check(ground.Length >= 3 && ground.All(item => ((string)item.Header).Length > 0), "On the ground the menu offers the shop, the kitchen and to move it");
            double topBefore = Overlay()!.Top, restFraction = window.FarmFraction;
            ground[^1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Rising, "It begins to rise");
            check(window.FarmMenuItems().Length == 0, "No menu while it is on the way up");
            Tick(FarmLift.RiseSeconds + 0.05);
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Floating && Math.Abs(window.FarmLiftHeight - FarmLift.FloatHeight) < FarmLift.BobHeight + 0.5, $"It floats ({window.FarmLiftHeight:F1} DIPs up)");
            check(Math.Abs(Overlay()!.Top - topBefore) < 0.5, "The art lifts inside the window, its shadow stays on the floor");
            int stageBefore = (int)farm.Tiles[4].Stage;
            window.FarmClick(4);
            check((int)farm.Tiles[4].Stage == stageBefore, "In the air a click does not farm");
            var air = window.FarmMenuItems();
            check(air.Length == 2, "In the air the menu offers to drop or cancel");

            double plotLeft() => area.Left + window.FarmFraction * free;
            double scaleNow = Field<double>(window, "farmScale");
            cursor = new Point((plotLeft() + 100) * scaleNow, (area.Top + 40) * scaleNow);
            Call(window, "PressFarm", 0);
            // Mouse capture needs a real press on some machines; the drag itself is what is checked here.
            bool held = window.FarmDragging;
            if (!held) { SetField(window, "farmDragging", true); SetField(window, "farmGrab", cursor.X / scaleNow - plotLeft()); }
            double before = plotLeft();
            cursor = new Point(cursor.X + 150 * scaleNow, (area.Top + 5) * scaleNow);
            Tick(0.05);
            check(Math.Abs(plotLeft() - before - 150) < 1.5, $"It follows the cursor along the floor ({plotLeft() - before:F0} DIPs)");
            check(Math.Abs(Overlay()!.Top - topBefore) < 0.5, "...and only sideways, wherever the cursor goes up or down");
            cursor = new Point(-100000, cursor.Y);
            Tick(0.05);
            check(window.FarmFraction == 0, "It stops at the monitor's left edge");
            cursor = new Point(100000, cursor.Y);
            Tick(0.05);
            check(window.FarmFraction == 1, "...and at its right edge");
            cursor = new Point((area.Left + free * 0.6 + 100) * scaleNow, cursor.Y);
            Tick(0.05);
            Call(window, "ReleaseFarm", 0);
            if (!held) SetField(window, "farmDragging", false);
            check(!window.FarmDragging && window.FarmLiftPhaseNow == FarmLiftPhase.Floating, "Let go, it stays in the air");

            string savedBefore = File.ReadAllText(path);
            window.FarmMenuItems()[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Falling, "Cancel sets it down");
            Tick(FarmLift.MaxGlideSeconds + FarmLift.FallSeconds + 0.05);
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Down && Math.Abs(window.FarmFraction - restFraction) < 1e-6, "...back where it was");
            var farmArt = Field<PetFarmArt>(window, "farmArt");
            check(farmArt.Dustiness > 0, "Dirt flies as it lands");

            window.BeginFarmMove();
            Tick(FarmLift.RiseSeconds + 0.05);
            SetField(window, "farmFraction", 0.6);
            window.FarmMenuItems()[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Tick(FarmLift.FallSeconds / 2);
            check(window.FarmLiftHeight > 1 && window.FarmLiftHeight < FarmLift.FloatHeight, $"It is on its way down ({window.FarmLiftHeight:F1} DIPs up half way)");
            Tick(FarmLift.FallSeconds / 2 + 0.1);
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Down && Math.Abs(window.FarmFraction - 0.6) < 1e-6, "It lands where it was held");
            var moved = PetFarm.Load(path);
            check(moved.FractionX == window.FarmFraction && moved.Monitor != null && File.ReadAllText(path) != savedBefore, "Its place and monitor are saved");
            check(Math.Abs(Overlay()!.Left + PetFarmArt.Side - (area.Left + free * 0.6)) < 1.5, "The window stands there");

            // Fullscreen and battle hide it; a plot in the air goes back.
            window.BeginFarmMove();
            Tick(FarmLift.RiseSeconds + 0.05);
            SetField(window, "farmFraction", 0.1);
            SetContext(window, true, 0); Call(window, "TickContext", 1.1);
            Tick(0.02);
            check(!window.FarmVisible, "A fullscreen app hides it");
            check(window.FarmLiftPhaseNow == FarmLiftPhase.Down && Math.Abs(window.FarmFraction - 0.6) < 1e-6, "A plot in the air goes back to its place");
            check(farmArt.Shed < 0 && farmArt.ShakeX == 0 && farmArt.Tilt == 0 && farmArt.Lift == 0 && farmArt.Squash == 0, "...and no shaking or crumbs stay on it");
            SetContext(window, false, 0); Call(window, "TickContext", 1.1);
            Tick(0.02);
            check(window.FarmVisible && Math.Abs(window.FarmFraction - 0.6) < 1e-6, "It comes back after the fullscreen app");

            check(window.StartBattle() == null && window.BattleActive, "A battle starts");
            Tick(0.02);
            check(!window.FarmVisible, "A battle hides it");
            window.EndBattle();
            Tick(0.02);
            check(window.FarmVisible, "It comes back after the battle");
            pet.Action = PetAction.Idle;

            prefs.Farm = false;
            Tick(0.02);
            check(!window.FarmVisible, "The setting hides it");
            prefs.Farm = true;
            Tick(0.02);
            check(window.FarmVisible && window.Farm!.HarvestedCount(FarmCrop.Radish) == 1, "Turned on again, the field is as it was");
        }
        finally
        {
            Call(window, "CloseFarm");
            prefs.Farm = false;
            SetField(window, "farmClock", null);
            SetField(window, "farmPath", PetFarm.DefaultPath);
            SetField(window, "farm", null);
            SetField(window, "paused", false);
            SetField(window, "cursorProbe", realCursor);
            SetContext(window, false, 0); Call(window, "TickContext", 1.1);
            window.Reset();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        Art(check);
    }

    private const double FarmGainWait = 1.35;

    // The store and shop in the real window: locked tiles, seeds at a price, selling, buying, what closes it.
    private static void Shop(PetWindow window, PetFarm farm, string path, Action<bool, string> check, Action tick)
    {
        Window? Panel() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet farm shop" && w.IsLoaded);
        check(window.FarmTileLabel(3)!.Contains("40"), "A locked tile says what it costs");
        window.FarmClick(3);
        check(window.FarmShopOpen && Panel() != null && farm.Tiles[3].Stage == FarmStage.Wild, "Clicking a locked tile opens the shop and tills nothing");
        var panel = Panel()!;
        check(panel.Topmost && !panel.ShowInTaskbar && !panel.ShowActivated, "The shop takes no focus and has no taskbar button");
        var area = Field<Area>(window, "bounds");
        check(panel.Left >= area.Left - 1 && panel.Left + panel.ActualWidth <= area.Right + 1 && panel.Top + panel.ActualHeight <= area.Bottom + 1, $"The shop fits on the monitor ({panel.ActualWidth:F0}x{panel.ActualHeight:F0})");
        double width = panel.ActualWidth;

        farm.Coins = 5;
        var seeds = window.FarmSeedItems(0);
        check(seeds[0].IsEnabled && seeds[1].IsEnabled && !seeds[2].IsEnabled && !seeds[3].IsEnabled, "Seeds too dear for the purse are greyed");
        check(((string)seeds[1].Header).Contains("4") , "A seed shows its price");
        farm.Coins = 0;
        farm.Stock["Carrot"] = 2; farm.Stock["Radish"] = 1;
        check(window.FarmSell(FarmCrop.Carrot) == 13 && farm.Coins == 13 && farm.StockOf(FarmCrop.Carrot) == 1, "Selling pays");
        check(PetFarm.Load(path).Coins == 13, "...and is saved");
        check(window.FarmSellAll() == 16 && farm.Coins == 29 && farm.Stock.Count == 0, "Selling all empties the store");
        check(!window.FarmOpenTile() && farm.OpenTiles == 3, "A tile too dear is refused");
        farm.Coins = 500;
        check(window.FarmOpenTile() && farm.OpenTiles == 4 && farm.Next(3) == FarmStep.Till, "A tile is opened");
        check(window.FarmBuyScarecrow() && farm.Scarecrow && farm.Coins == 310 && !window.FarmBuyScarecrow(), "The scarecrow is bought once");
        check(Field<PetFarmArt>(window, "farmArt").Scarecrow, "It stands on the plot");
        var reload = PetFarm.Load(path);
        check(reload.OpenTiles == 4 && reload.Scarecrow && reload.Coins == 310, "Tile and scarecrow are in the file");
        panel.UpdateLayout();
        check(Math.Abs(panel.ActualWidth - width) < 1, "The panel does not change width as its labels change");

        SetField(window, "paused", true);
        tick();
        int coins = farm.Coins;
        farm.Stock["Radish"] = 1;
        check(window.FarmSell(FarmCrop.Radish) == 0 && window.FarmSellAll() == 0 && !window.FarmOpenTile() && farm.Coins == coins, "Pause refuses buying and selling");
        SetField(window, "paused", false);
        farm.Stock.Clear();
        tick();

        Render(panel);
        window.CloseFarmShop();
        tick();
        check(!window.FarmShopOpen && Panel() == null, "The shop closes");
        window.FarmClick(4);
        check(window.FarmShopOpen, "A locked tile opens it again");
        SetField(window, "battleHubOpen", true);
        tick();
        check(!window.FarmShopOpen, "A battle panel closes it");
        check(window.FarmMenuItems().Length == 1, "...and takes it off the menu");
        SetField(window, "battleHubOpen", false);
        window.OpenFarmShop();
        SetContext(window, true, 0); Call(window, "TickContext", 1.1);
        tick();
        check(!window.FarmShopOpen, "A fullscreen app closes it");
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        tick();
        farm.Scarecrow = false;
    }

    // The kitchen in the real window: what it asks of the store and of the cat, and what closes it.
    private static void Kitchen(PetWindow window, PetModel pet, PetFarm farm, string path, Action<bool, string> check, Action tick)
    {
        Window? Panel() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet farm kitchen" && w.IsLoaded);
        void Free() { pet.Emotions.Reset(); pet.Action = PetAction.Idle; pet.TreatRemaining = 0; SetField(window, "treatCooldown", 0.0); SetField(window, "treatSatisfiedRemaining", 0.0); }
        Free();
        window.OpenFarmShop();
        check(window.FarmMenuItems().Length >= 3, "The menu has the kitchen too");
        window.FarmMenuItems()[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(window.FarmKitchenOpen && Panel() != null && !window.FarmShopOpen, "The kitchen opens, in place of the shop");
        window.OpenFarmShop();
        check(window.FarmShopOpen && !window.FarmKitchenOpen, "...and the shop in place of the kitchen");
        window.OpenFarmKitchen();
        var panel = Panel()!;
        check(panel.Topmost && !panel.ShowInTaskbar && !panel.ShowActivated, "The kitchen takes no focus and has no taskbar button");
        var area = Field<Area>(window, "bounds");
        check(panel.Left >= area.Left - 1 && panel.Left + panel.ActualWidth <= area.Right + 1 && panel.Top + panel.ActualHeight <= area.Bottom + 1, $"The kitchen fits on the monitor ({panel.ActualWidth:F0}x{panel.ActualHeight:F0})");
        double width = panel.ActualWidth;
        Button(window).ForEach(b => check(!b.IsEnabled, "With an empty store nothing can be cooked"));

        check(!window.FarmCook(FarmDish.RadishSoup) && pet.TreatRemaining == 0, "Without ingredients the cat gets nothing");
        farm.Stock["Radish"] = 2; farm.Stock["Tomato"] = 1; farm.Stock["Pumpkin"] = 1; farm.Stock["Carrot"] = 1;
        Call(window, "UpdateFarmKitchen");
        Render(panel, "farm-kitchen.png");

        pet.Emotions.Reset(); pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        tick();
        check(!Button(window)[0].IsEnabled, "A sleeping cat greys the kitchen");
        check(!window.FarmCook(FarmDish.RadishSoup) && farm.StockOf(FarmCrop.Radish) == 2 && pet.Emotions.SleepLocked && pet.Action == PetAction.Sleeping, "Cooking is refused and neither wakes the cat nor uses anything up");
        Free();
        tick();
        check(Button(window)[0].IsEnabled, "Awake, the kitchen is open again");

        SetField(window, "paused", true);
        tick();
        check(!Button(window)[0].IsEnabled && !window.FarmCook(FarmDish.RadishSoup) && farm.StockOf(FarmCrop.Radish) == 2, "Pause refuses cooking");
        SetField(window, "paused", false);
        tick();

        int coins = farm.Coins;
        pet.LedgeId = 7; // Up on a window it cannot walk to the hearth: it eats where it is (the hearth: FarmChoreChecks).
        check(window.FarmCook(FarmDish.RadishSoup) && pet.TreatRemaining > 0 && pet.TreatDish == FarmDish.RadishSoup, "A dish is cooked and the cat eats it");
        pet.LedgeId = 0;
        check(farm.StockOf(FarmCrop.Radish) == 0 && farm.Coins == coins && PetFarm.Load(path).StockOf(FarmCrop.Radish) == 0, "Its ingredients are gone, also from the file");
        check(!Button(window)[0].IsEnabled && !window.FarmCook(FarmDish.StuffedTomato) && farm.StockOf(FarmCrop.Tomato) == 1, "Eating, the cat cannot be fed again and nothing is used up");
        double treats = pet.Activity.Treats;
        for (double t = 0; t < PetModel.TreatDuration + 0.1; t += 1.0 / 30) Call(window, "TickFeatures", 1.0 / 30);
        check(pet.TreatRemaining == 0 && pet.Activity.Treats == treats + 1, "It counts as one treat");
        pet.Emotions.Step(pet, 4, -200, 50);
        check(pet.Emotions.Target == PetEmotion.Happy, "A dish makes the cat happy for longer than a treat does");
        Free();
        check(window.GiveTreat() && pet.TreatDish == null, "The usual treat is still the fish");
        Free();
        panel.UpdateLayout();
        check(Math.Abs(panel.ActualWidth - width) < 1, "The kitchen does not change width as its labels change");

        SetField(window, "battleHubOpen", true);
        tick();
        check(!window.FarmKitchenOpen, "A battle panel closes the kitchen");
        SetField(window, "battleHubOpen", false);
        window.OpenFarmKitchen();
        SetContext(window, true, 0); Call(window, "TickContext", 1.1);
        tick();
        check(!window.FarmKitchenOpen, "A fullscreen app closes the kitchen");
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        tick();
        farm.Stock.Clear();
        Free();
    }

    // The "all" items in the real window: only what has work, and the same result as clicking each tile.
    private static void Bulk(PetWindow window, PetModel pet, PetFarm farm, string path, Action<bool, string> check, Func<DateTime> now, Action<double> pass)
    {
        string[] Headers() => window.FarmMenuItems().Select(item => (string)item.Header).ToArray();
        MenuItem Item(string start) => window.FarmMenuItems().First(item => ((string)item.Header).StartsWith(start));
        void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        int openBefore = farm.OpenTiles, coinsBefore = farm.Coins;
        farm.OpenTiles = 3;
        for (int i = 0; i < PetFarm.TileCount; i++) farm.Tiles[i] = new FarmTile();
        farm.Coins = 9;

        check(Headers().Count(h => h.StartsWith("Till all (3")) == 1 && !Headers().Any(h => h.StartsWith("Water all") || h.StartsWith("Harvest all") || h.StartsWith("Sow all")), "Only the work there is, with its count");
        Click(Item("Till all"));
        check(farm.TilesAt(FarmStep.Plant).Count == 3 && farm.Tiles[3].Stage == FarmStage.Wild, "Till all tills the open tiles and no locked one");
        check(PetFarm.Load(path).TilesAt(FarmStep.Plant).Count == 3, "...and saves");

        var sow = Item("Sow all (3");
        var seeds = sow.Items.OfType<MenuItem>().ToArray();
        check(seeds.Length == 4 && seeds.All(s => s.Icon is FarmCropIcon), "Sow all offers the four crops with their pictures");
        check(seeds[0].IsEnabled && seeds[1].IsEnabled && !seeds[3].IsEnabled, "A seed the purse cannot pay for even once is greyed");
        check(((string)seeds[1].Header).Contains("2 tiles") && ((string)seeds[1].Header).Contains("8"), $"It says how many tiles and what that costs ({seeds[1].Header})");
        Click(seeds[1]);
        check(farm.TilesAt(FarmStep.Water).Count == 2 && farm.Coins == 1 && farm.Next(2) == FarmStep.Plant, "Sowing stops when the coins run out");
        check(window.FarmSowAll(FarmCrop.Radish) == 1, "The last tile takes a free seed");

        pet.Emotions.Reset(); pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        check(window.FarmDoAll(FarmStep.Water) == 3 && farm.TilesAt(FarmStep.Wait).Count == 3, "Water all waters them, also while the cat sleeps");
        check(pet.Emotions.SleepLocked && pet.Action == PetAction.Sleeping, "...and does not wake it");
        pet.Emotions.Reset(); pet.Action = PetAction.Idle;
        check(window.FarmDoAll(FarmStep.Water) == 0, "Nothing is left to water");

        foreach (var tile in farm.Tiles.Take(3)) { tile.Stage = FarmStage.Ripe; tile.GrowsUntil = null; }
        int harvested = farm.HarvestedCount(FarmCrop.Carrot), carrots = farm.StockOf(FarmCrop.Carrot);
        SetField(window, "paused", true);
        check(window.FarmDoAll(FarmStep.Harvest) == 0 && window.FarmSowAll(FarmCrop.Radish) == 0 && farm.Tiles[0].Stage == FarmStage.Ripe, "Pause refuses it");
        SetField(window, "paused", false);
        window.BeginFarmMove();
        check(window.FarmDoAll(FarmStep.Harvest) == 0 && farm.Tiles[0].Stage == FarmStage.Ripe, "A plot on the move refuses it");
        Call(window, "HideFarm");
        Call(window, "TickFarm", 1.0 / 60);

        Click(Item("Harvest all (3"));
        check(farm.HarvestedCount(FarmCrop.Carrot) == harvested + 2 && farm.StockOf(FarmCrop.Carrot) == carrots + 2 && farm.TilesAt(FarmStep.Till).Count == 3, "Harvest all fills the store and clears the tiles");
        var gain = Field<Border?>(window, "farmGain")!;
        check(((TextBlock)gain.Child).Text.StartsWith("+3 "), $"One label says how many ({((TextBlock)gain.Child).Text})");
        check(farm.Harvested.Keys.Count == 2, "Each crop counts as itself");

        Call(window, "FarmChanged");
        for (int i = 0; i < PetFarm.TileCount; i++) farm.Tiles[i] = new FarmTile();
        farm.Harvested.Clear(); farm.Stock.Clear();
        farm.OpenTiles = openBefore; farm.Coins = coinsBefore;
        Call(window, "FarmChanged");
        double wait = FarmGainWait;
        for (double t = 0; t < wait; t += 1.0 / 60) Call(window, "TickFarm", 1.0 / 60);
    }

    // Levels in the real window: locked seeds and dishes say what they need, a level is announced and paid.
    private static void Levels(PetWindow window, PetFarm farm, string path, Action<bool, string> check, Action tick)
    {
        Window? Shop() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet farm shop" && w.IsLoaded);
        int coinsBefore = farm.Coins, openBefore = farm.OpenTiles;
        farm.Xp = 0; farm.Coins = 500; farm.OpenTiles = 3;
        var seeds = window.FarmSeedItems(0);
        check(seeds[0].IsEnabled && seeds[1].IsEnabled && !seeds[2].IsEnabled && !seeds[3].IsEnabled, "At level 1 the tomato and pumpkin are greyed, however rich");
        check(((string)seeds[2].Header).Contains("level 2") && ((string)seeds[3].Header).Contains("level 4"), $"They say the level they need ({seeds[2].Header})");

        window.OpenFarmShop();
        tick();
        var level = Field<TextBlock?>(window, "farmShopLevel")!;
        var next = Field<TextBlock?>(window, "farmShopNextUnlock")!;
        check(level.Text.Contains("Level 1") && level.Text.Contains("0/25"), $"The shop shows the level and XP ({level.Text})");
        check(next.Text.Contains("Tomato") && next.Text.Contains("Carrot stew"), $"...and what the next level opens ({next.Text})");
        var fill = Field<Border?>(window, "farmShopXpFill")!;
        check(fill.Width == 0, "The XP bar starts empty");

        farm.Xp = 23;
        farm.Tiles[0] = new FarmTile { Stage = FarmStage.Ripe, Crop = FarmCrop.Radish };
        int coins = farm.Coins;
        window.FarmClick(0);
        var gain = Field<Border?>(window, "farmGain")!;
        check(farm.Level == 2 && farm.Coins == coins + 40, "A harvest reaches level 2 and pays");
        check(((TextBlock)gain.Child).Text.Contains("Level 2") && ((TextBlock)gain.Child).Text.Contains("40"), $"The field says so ({((TextBlock)gain.Child).Text})");
        check(level.Text.Contains("Level 2") && fill.Width == 0, "The open shop follows at once");
        check(PetFarm.Load(path).Xp == 25, "XP is in the file");
        check(window.FarmSeedItems(0)[2].IsEnabled && !window.FarmSeedItems(0)[3].IsEnabled, "The tomato opens, not the pumpkin");
        for (double t = 0; t < 2; t += 1.0 / 60) Call(window, "TickFarm", 1.0 / 60);
        check(gain.Opacity > 0.5, "A new level stays up longer than a harvest's label");
        for (double t = 0; t < 1.1; t += 1.0 / 60) Call(window, "TickFarm", 1.0 / 60);
        check(gain.Opacity < 0.05, "...and fades");

        double width = Shop()!.ActualWidth;
        farm.Xp = 450;
        Call(window, "UpdateFarmShop");
        check(next.Text.Contains("140"), $"A level that opens nothing tells its coins ({next.Text})");
        farm.Xp = 5000;
        Call(window, "UpdateFarmShop");
        Shop()!.UpdateLayout();
        check(farm.Level == 10 && fill.Width == 400 && !level.Text.Contains("/"), $"The top level is full ({level.Text})");
        check(Math.Abs(Shop()!.ActualWidth - width) < 1, "The panel keeps its width");
        var area = Field<Area>(window, "bounds");
        check(Shop()!.Top + Shop()!.ActualHeight <= area.Bottom + 1, $"The taller shop still fits on the monitor ({Shop()!.ActualHeight:F0})");
        farm.Xp = 70;
        Call(window, "UpdateFarmShop");
        Render(Shop()!, "farm-shop-level.png");
        window.CloseFarmShop();

        farm.Xp = 0;
        window.OpenFarmKitchen();
        tick();
        var rows = (System.Collections.IEnumerable)FieldOf("farmKitchenRows").GetValue(window)!;
        var notes = rows.Cast<ValueTuple<FarmDish, TextBlock, Border>>().ToArray();
        check(notes[0].Item2.Text.Contains("Radish") && notes[1].Item2.Text.Contains("level 2") && notes[3].Item2.Text.Contains("level 5"), $"The kitchen says which level a dish needs ({notes[3].Item2.Text})");
        window.CloseFarmKitchen();
        tick();

        farm.Tiles[0] = new FarmTile();
        farm.Harvested.Clear(); farm.Stock.Clear();
        farm.Xp = 0; farm.Coins = coinsBefore; farm.OpenTiles = openBefore;
        Call(window, "FarmChanged");
    }

    // The order board in the real window: orders arrive by the clock, deliver, skip, what closes it.
    private static void Orders(PetWindow window, PetModel pet, PetFarm farm, string path, Action<bool, string> check, Action<double> wait, Action tick)
    {
        Window? Panel() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet farm orders" && w.IsLoaded);
        int coinsBefore = farm.Coins;
        var art = Field<PetFarmArt>(window, "farmArt");
        farm.Xp = 0; farm.Orders.Clear(); farm.Stock.Clear(); farm.Coins = 0;

        wait(2);
        check(farm.Orders.Count == 1 && farm.Orders[0].Items != null, "An order arrives by itself");
        check(Field<double>(window, "farmMailLeft") > 0 && art.OrderSign, "A paper bird brings it to the board on the plot");
        check(PetFarm.Load(path).Orders.Count == 1, "...and it is saved");
        for (double t = 0; t < PetFarmArt.MailSeconds + 0.1; t += 1.0 / 30) Call(window, "TickFarm", 1.0 / 30);
        check(Field<double>(window, "farmMailLeft") == 0 && art.MailTime < 0, "The bird goes away again");

        farm.Orders[0].Items = new() { ["Radish"] = 2 };
        farm.Stock["Radish"] = 1;
        Call(window, "FarmChanged");
        check(!art.OrderReady, "Short of crops the board shows no mark");

        window.FarmMenuItems()[2].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(window.FarmOrdersOpen && Panel() != null, "The menu opens the orders");
        var panel = Panel()!;
        check(panel.Topmost && !panel.ShowInTaskbar && !panel.ShowActivated, "It takes no focus and has no taskbar button");
        var area = Field<Area>(window, "bounds");
        check(panel.Left >= area.Left - 1 && panel.Left + panel.ActualWidth <= area.Right + 1 && panel.Top + panel.ActualHeight <= area.Bottom + 1, $"It fits on the monitor ({panel.ActualWidth:F0}x{panel.ActualHeight:F0})");
        double width = panel.ActualWidth;
        window.OpenFarmShop();
        check(window.FarmShopOpen && !window.FarmOrdersOpen, "The shop opens in its place");
        window.OpenFarmOrders();
        check(window.FarmOrdersOpen && !window.FarmShopOpen, "...and the orders in the shop's place");
        window.OpenFarmKitchen();
        check(window.FarmKitchenOpen && !window.FarmOrdersOpen, "The kitchen too");
        window.OpenFarmOrders();
        check(window.FarmOrdersOpen && !window.FarmKitchenOpen, "...and back");
        panel = Panel()!;

        object?[] Rows() => ((System.Collections.IEnumerable)FieldOf("farmOrderRows").GetValue(window)!).Cast<object?>().ToArray();
        T Part<T>(int row, string name) => (T)Rows()[row]!.GetType().GetProperty(name)!.GetValue(Rows()[row])!;
        check(Rows().Length == 3, "Three places on the board");
        check(Part<TextBlock>(0, "Name").Text.Contains("1/2") && Part<TextBlock>(0, "Note").Text.Contains("9") && Part<TextBlock>(0, "Note").Text.Contains("4 XP"), $"An order says what it needs and pays ({Part<TextBlock>(0, "Name").Text}, {Part<TextBlock>(0, "Note").Text})");
        check(!Part<Border>(0, "Deliver").IsEnabled && Part<Border>(0, "Skip").IsEnabled, "Short of crops it cannot be delivered, but it can be skipped");
        check(Part<Border>(1, "Deliver").Visibility == Visibility.Collapsed && Part<TextBlock>(1, "Note").Text.Contains("level 3") && Part<TextBlock>(2, "Note").Text.Contains("level 5"), "A closed place says which level opens it");
        check(window.FarmDeliver(0) == 0, "Delivery is refused while crops are short");

        pet.Emotions.Reset(); pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        farm.Stock["Radish"] = 3;
        Call(window, "FarmChanged");
        check(art.OrderReady && Part<Border>(0, "Deliver").IsEnabled, "With enough crops the board shows its mark and the button lights");
        SetField(window, "paused", true);
        tick();
        check(!Part<Border>(0, "Deliver").IsEnabled && !Part<Border>(0, "Skip").IsEnabled && window.FarmDeliver(0) == 0 && !window.FarmSkipOrder(0) && farm.Orders[0].Items != null, "Pause refuses both");
        SetField(window, "paused", false);
        tick();
        Render(panel, "farm-orders.png");
        check(window.FarmDeliver(0) == 9 && farm.Coins == 9 && farm.Xp == 4 && farm.StockOf(FarmCrop.Radish) == 1, "Delivering pays, and takes what it asked for");
        check(pet.Emotions.SleepLocked && pet.Action == PetAction.Sleeping, "It does not wake the cat");
        pet.Emotions.Reset(); pet.Action = PetAction.Idle;
        check(PetFarm.Load(path).Coins == 9 && !art.OrderReady, "It is saved and the mark goes");
        check(((TextBlock)Field<Border?>(window, "farmGain")!.Child).Text.Contains("9"), "The field says what it brought");
        check(Part<Border>(0, "Deliver").Visibility == Visibility.Collapsed && Part<TextBlock>(0, "Note").Text.StartsWith("20:00"), $"The place counts down to its next order ({Part<TextBlock>(0, "Note").Text})");
        wait(600);
        check(farm.Orders[0].Items == null && Part<TextBlock>(0, "Note").Text.StartsWith("10:00"), $"Ten minutes on it still waits ({Part<TextBlock>(0, "Note").Text})");
        wait(601);
        check(farm.Orders[0].Items != null && Part<Border>(0, "Deliver").Visibility == Visibility.Visible, "After twenty minutes the next order is on the board");
        check(window.FarmSkipOrder(0) && farm.Orders[0].Items == null && farm.Coins == 9, "Skipping costs nothing");

        farm.Xp = 70;
        wait(2);
        check(farm.Orders.Count == 2 && farm.Orders[1].Items != null && Part<Border>(1, "Skip").Visibility == Visibility.Visible, "Level 3 opens the second place at once");
        panel.UpdateLayout();
        check(Math.Abs(panel.ActualWidth - width) < 1, "The panel keeps its width");

        // The widest order there can be still fits its row.
        farm.Orders[1].Items = new() { ["Pumpkin"] = 2, ["Tomato"] = 3 };
        Call(window, "UpdateFarmOrders");
        panel.UpdateLayout();
        var button = Part<Border>(1, "Deliver");
        var corner = button.TransformToAncestor((Visual)panel.Content).Transform(new Point(button.ActualWidth, 0));
        check(corner.X <= ((FrameworkElement)panel.Content).ActualWidth + 1 && Part<TextBlock>(1, "Name").ActualWidth > 40, $"A long order keeps its buttons inside the panel ({corner.X:F0})");
        Render(panel, "farm-orders-long.png");

        SetField(window, "battleHubOpen", true);
        tick();
        check(!window.FarmOrdersOpen, "A battle panel closes it");
        SetField(window, "battleHubOpen", false);
        window.OpenFarmOrders();
        SetContext(window, true, 0); Call(window, "TickContext", 1.1);
        tick();
        check(!window.FarmOrdersOpen, "A fullscreen app closes it");
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        tick();

        farm.Xp = 0; farm.Orders.Clear(); farm.Stock.Clear(); farm.Coins = coinsBefore;
        Call(window, "FarmChanged");
        double fade = FarmGainWait;
        for (double t = 0; t < fade; t += 1.0 / 60) Call(window, "TickFarm", 1.0 / 60);
    }

    private static List<Border> Button(PetWindow window)
    {
        var rows = (System.Collections.IEnumerable)FieldOf("farmKitchenRows").GetValue(window)!;
        return rows.Cast<ValueTuple<FarmDish, TextBlock, Border>>().Select(row => row.Item3).ToList();
    }

    // The weather reaching the field in the real window, through the same frame step the app runs.
    private static void Sky(PetWindow window, PetFarm farm, Action<bool, string> check, Func<DateTime> now)
    {
        var weather = Field<PetWeather>(window, "weather");
        var random = new Random(3);
        void Show(WeatherKind kind) { weather.HideNow(); weather.SetKind(kind); weather.Step(PetWeather.FadeSeconds, true, random); }
        void Pass(double seconds) { for (double t = 0; t < seconds; t += 0.5) Call(window, "StepFarmSky", 0.5); }
        void Sown(int tile) { farm.Tiles[tile] = new FarmTile { Stage = FarmStage.Seed, Crop = FarmCrop.Radish }; }
        var withCat = Field<bool>(window, "farmWithCat");
        SetField(window, "farmWithCat", true);
        Sown(0);

        Show(WeatherKind.Rain);
        Pass(PetFarm.RainSoakSeconds - 2);
        check(farm.Next(0) == FarmStep.Water, "A short shower does not water yet");
        Pass(3);
        check(farm.Next(0) == FarmStep.Wait && PetFarm.Load(Field<string>(window, "farmPath")).Next(0) == FarmStep.Wait, "Rain that lasts waters the tile and saves it");
        Sown(0);
        weather.HideNow();
        Pass(1);
        Show(WeatherKind.Rain);
        Pass(3);
        check(farm.Next(0) == FarmStep.Water, "When the rain stops the wait starts over");

        SetField(window, "farmWithCat", false);
        Pass(PetFarm.RainSoakSeconds + 2);
        check(farm.Next(0) == FarmStep.Water, "Rain on the cat's monitor does not reach a field on another");
        SetField(window, "farmWithCat", true);
        Pass(PetFarm.RainSoakSeconds + 2);
        check(farm.Next(0) == FarmStep.Wait, "Back on its monitor the field is watered");

        Show(WeatherKind.Sunny);
        var growing = farm.Tiles[0].GrowsUntil!.Value;
        Pass(10);
        check(Math.Abs((growing - farm.Tiles[0].GrowsUntil!.Value).TotalSeconds - 10 * PetFarm.SunBonus) < 0.6, "The sun brings the end of the stage closer");
        growing = farm.Tiles[0].GrowsUntil!.Value;
        Call(window, "TickFarm", 0.0);
        check(farm.Tiles[0].GrowsUntil == growing, "Pause holds the sun's help still");
        SetField(window, "farmWithCat", false);
        Pass(10);
        check(farm.Tiles[0].GrowsUntil == growing, "No sun for a field on another monitor");
        SetField(window, "farmWithCat", withCat);

        weather.HideNow();
        weather.SetKind(WeatherKind.Sunny);
        SetField(window, "farmRainSeconds", 0.0);
        SetField(window, "farmSunUnsaved", 0.0);
        farm.Tiles[0] = new FarmTile();
        Call(window, "FarmChanged");
    }

    private static void Render(Window panel, string name = "farm-shop.png")
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not string review) return;
        panel.UpdateLayout();
        var element = (FrameworkElement)panel.Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(review);
        using var stream = File.Create(Path.Combine(review, name)); encoder.Save(stream);
    }

    private static bool Below(IntPtr above, IntPtr target)
    {
        for (var next = GetWindow(above, 2); next != IntPtr.Zero; next = GetWindow(next, 2))
            if (next == target) return true;
        return false;
    }

    // Every stage of every crop, on a light and a dark wallpaper, resting and in the air.
    private static void Art(Action<bool, string> check)
    {
        int width = (int)PetFarmArt.ArtWidth, height = (int)PetFarmArt.ArtHeight;
        string Render(Action<PetFarmArt> set, Color back, string? name)
        {
            var art = new PetFarmArt();
            set(art);
            art.Measure(new Size(width, height));
            art.Arrange(new Rect(0, 0, width, height));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
                dc.DrawRectangle(new SolidColorBrush(back), null, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Render(art); // Directly: a VisualBrush would stretch what is drawn to fill the frame.
            if (name != null && Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review)
            {
                Directory.CreateDirectory(review);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(review, name + ".png")); encoder.Save(stream);
            }
            var pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);
            return Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(pixels));
        }

        var light = Color.FromRgb(0xF1, 0xEE, 0xE6);
        var dark = Color.FromRgb(0x1C, 0x22, 0x30);
        void Steps(PetFarmArt art)
        {
            art.Tiles[0] = new(FarmStage.Wild, null, false, false, 0);
            art.Tiles[1] = new(FarmStage.Tilled, null, false, false, 0);
            art.Tiles[2] = new(FarmStage.Seed, FarmCrop.Carrot, false, true, 0);
            art.Tiles[3] = new(FarmStage.Sprout, FarmCrop.Carrot, true, false, 0.4);
            art.Tiles[4] = new(FarmStage.Young, FarmCrop.Tomato, true, false, 0.8);
            art.Tiles[5] = new(FarmStage.Young, FarmCrop.Pumpkin, false, true, 0.67);
            art.HoverTile = 3;
        }
        void Ripe(PetFarmArt art)
        {
            var crops = Enum.GetValues<FarmCrop>();
            for (int i = 0; i < 6; i++) art.Tiles[i] = new(FarmStage.Ripe, crops[i % 4], false, false, 1);
        }
        foreach (var (back, tone) in new[] { (light, "light"), (dark, "dark") })
        {
            Render(Steps, back, $"farm-steps-{tone}");
            Render(Ripe, back, $"farm-ripe-{tone}");
            Render(art => { Steps(art); art.Lift = FarmLift.FloatHeight; }, back, $"farm-floating-{tone}");
            Render(art => { Steps(art); art.Dustiness = 0.6; }, back, $"farm-landing-{tone}");
            Render(art => { Steps(art); art.OrderSign = true; art.OrderReady = true; art.MailTime = 2; }, back, $"farm-orders-board-{tone}");
            Render(art => { Steps(art); art.Lift = FarmLift.RiseHeightAt(0.9); art.ShakeX = 2; art.Tilt = 1.2; art.Shed = 1.0; }, back, $"farm-rising-{tone}");
            Render(art => { Steps(art); art.Lift = FarmLift.FloatHeight; art.Shed = 1.9; }, back, $"farm-shedding-{tone}");
            Render(art => { Steps(art); art.Dustiness = 0.95; art.Squash = 0.9; art.ShakeX = 2.5; }, back, $"farm-impact-{tone}");
            Render(art => { Steps(art); art.Dustiness = 0.65; }, back, $"farm-impact-late-{tone}");
            Render(art => { Steps(art); for (int i = 3; i < 6; i++) art.Tiles[i] = new(FarmStage.Wild, null, false, false, 0, true); art.HoverTile = 4; art.Scarecrow = true; }, back, $"farm-locked-{tone}");
        }
        foreach (var (effect, name) in new[] { (FarmEffect.Till, "till"), (FarmEffect.Water, "water"), (FarmEffect.Harvest, "harvest") })
            Render(art => { Steps(art); art.Effects[1] = effect; art.EffectAge[1] = 0.4; }, dark, $"farm-effect-{name}");

        var seen = new HashSet<string>();
        foreach (var stage in new[] { FarmStage.Seed, FarmStage.Sprout })
            seen.Add(Render(art => art.Tiles[0] = new(stage, FarmCrop.Carrot, false, false, 0), dark, null));
        foreach (var crop in Enum.GetValues<FarmCrop>())
        {
            seen.Add(Render(art => art.Tiles[0] = new(FarmStage.Ripe, crop, false, false, 1), dark, null));
            seen.Add(Render(art => art.Tiles[0] = new(FarmStage.Young, crop, false, false, 0.5), dark, null));
        }
        // Radish and carrot share their young leaves' shape, but not their color.
        check(seen.Count == 10, $"Every stage of every crop looks different ({seen.Count} of 10)");
        string dry = Render(art => art.Tiles[0] = new(FarmStage.Seed, FarmCrop.Carrot, false, false, 0), dark, null);
        check(dry != Render(art => art.Tiles[0] = new(FarmStage.Seed, FarmCrop.Carrot, true, false, 0), dark, null), "Wet soil is not dry soil");
        check(dry != Render(art => art.Tiles[0] = new(FarmStage.Seed, FarmCrop.Carrot, false, true, 0), dark, null), "A thirsty seed shows a drop");
        string still = Render(art => { }, dark, null);
        check(still != Render(art => art.Lift = FarmLift.FloatHeight, dark, null), "A floating plot is drawn in the air");
        check(still != Render(art => art.Shed = 1.0, dark, null), "Crumbs are drawn as it tears loose");
        check(still != Render(art => art.Dustiness = 0.65, dark, null), "Dirt is drawn as it lands");
        check(still != Render(art => art.ShakeX = 3, dark, null), "The plot shakes");
        check(still != Render(art => art.Tiles[3] = new(FarmStage.Wild, null, false, false, 0, true), dark, null), "A locked tile has its fence");
        check(still != Render(art => art.Scarecrow = true, dark, null), "The scarecrow is drawn");
        string board = Render(art => art.OrderSign = true, dark, null);
        check(still != board, "The order board is drawn");
        check(board != Render(art => { art.OrderSign = true; art.OrderReady = true; }, dark, null), "A delivered-ready order puts a mark on it");
        check(board != Render(art => { art.OrderSign = true; art.MailTime = 2; }, dark, null), "The paper bird sits on it");
        check(PetFarmArt.TileAt(PetFarmArt.TileLeft(0) + 1) == 0 && PetFarmArt.TileAt(PetFarmArt.TileLeft(5) + 53) == 5 && PetFarmArt.TileAt(1) == -1 && PetFarmArt.TileAt(PetFarmArt.ArtWidth - 1) == -1, "Clicks find their tiles; the rim is none");
        Dishes(check);
    }

    // The cat at each dish, as big as it is drawn, on a light and a dark wallpaper.
    private static void Dishes(Action<bool, string> check)
    {
        const int size = 180;
        string Render(FarmDish? dish, double eaten, Color back, string? name)
        {
            var pet = new PetModel { Action = PetAction.Sitting, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1, TreatRemaining = PetModel.TreatDuration - eaten, TreatDish = dish };
            var cat = new PetRenderer(pet);
            cat.Measure(new Size(size, size)); cat.Arrange(new Rect(0, 0, size, size));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(back), null, new Rect(0, 0, size, size));
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Render(cat);
            if (name != null && Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review)
            {
                Directory.CreateDirectory(review);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(review, name + ".png")); encoder.Save(stream);
            }
            var pixels = new byte[size * size * 4];
            bitmap.CopyPixels(pixels, size * 4, 0);
            return Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(pixels));
        }
        var light = Color.FromRgb(0xF1, 0xEE, 0xE6);
        var dark = Color.FromRgb(0x1C, 0x22, 0x30);
        var seen = new HashSet<string> { Render(null, 1, dark, null) };
        foreach (var dish in Enum.GetValues<FarmDish>())
        {
            seen.Add(Render(dish, 1, dark, $"dish-{dish}-dark"));
            Render(dish, 1, light, $"dish-{dish}-light");
            Render(dish, 0.4, dark, $"dish-{dish}-start");
        }
        check(seen.Count == 5, $"Each dish is drawn differently, and none like the fish ({seen.Count} of 5)");
    }
}
