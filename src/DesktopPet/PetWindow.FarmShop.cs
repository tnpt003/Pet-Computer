using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopPet;

// The farm's store and shop (1.5.1 M5): what was harvested, selling it for farm coins, opening more
// tiles, the scarecrow. Like the field itself it never touches the cat. Farm coins are not battle coins.
public sealed partial class PetWindow
{
    private const double FarmShopWidth = 400;

    private Window? farmShopWindow;
    private TextBlock? farmShopLevel, farmShopNextUnlock;
    private Border? farmShopXpFill;
    private FarmScarecrowIcon? farmShopScareIcon;
    private TextBlock? farmShopCoins, farmShopTileName, farmShopTileNote, farmShopTilePrice, farmShopScareName, farmShopScareNote, farmShopScarePrice, farmShopSellAllText;
    private Border? farmShopTileButton, farmShopScareButton, farmShopSellAll;
    private readonly List<(FarmCrop Crop, TextBlock Count, Border Button, TextBlock Price)> farmShopRows = new();
    private readonly List<(FarmFish Fish, TextBlock Count, Border Button, TextBlock Price)> farmShopFishRows = new();
    private bool farmShopPaused;
    private TextBlock? farmShopWeather;
    private WeatherKind? farmShopSky;
    private sealed record FarmDecorToggle(FarmDecor Decor, Border Button, FrameworkElement Icon, UIElement Slash);
    private readonly List<FarmDecorToggle> farmShopDecors = new();
    private TextBlock? farmShopDecorNote;

    public bool FarmShopOpen => farmShopWindow != null;
    // A battle panel sits in the same place: the shop gives way to it.
    private bool FarmShopAllowed => Blocking(AutoBlock.BattleScreens) == AutoBlock.None;

    private string FarmPriceText(int price) => price == 0 ? L("miễn phí", "free") : LF($"{price} xu", $"{price} coins");
    private string FarmLevelText(int level) => LF($"cần cấp {level}", $"needs level {level}");

    // What the next level opens, for the shop's line; empty when it opens nothing.
    private string FarmNextUnlockNames(int level)
    {
        var crops = Enum.GetValues<FarmCrop>().Where(crop => PetFarm.CropLevel(crop) == level).Select(FarmCropName);
        var dishes = Enum.GetValues<FarmDish>().Where(dish => PetFarm.DishLevel(dish) == level).Select(FarmDishName);
        var fish = Enum.GetValues<FarmFish>().Where(kind => PetFarm.FishLevel(kind) == level && level > 1).Select(FarmFishName);
        var scarecrow = level == PetFarm.GoldenScarecrowLevel ? new[] { L("Bù nhìn vàng", "Golden scarecrow") } : Array.Empty<string>();
        return string.Join(", ", crops.Concat(fish).Concat(dishes).Concat(scarecrow));
    }

    // Each frame the field is shown.
    private void WatchFarmShop()
    {
        if (farmShopWindow == null) return;
        if (!FarmShopAllowed) CloseFarmShop();
        else if (farmShopPaused != paused || farmShopSky != FarmWeather) UpdateFarmShop();
    }

    public void OpenFarmShop()
    {
        if (farm == null || paused || !FarmVisible || !FarmShopAllowed || farmShopWindow != null) return;
        CloseFarmKitchen(); // They open in the same place.
        CloseFarmStall();
        var title = PanelLine(PetUi.DisplaySize, true, FarmShopWidth);
        title.Text = L("Kho & cửa hàng", "Store & shop");
        farmShopCoins = PanelLine(PetUi.TitleSize, true, FarmShopWidth);
        farmShopCoins.Foreground = PetUi.Game.Gold;
        farmShopCoins.Margin = new Thickness(0, 2, 0, 0);

        farmShopLevel = PanelLine(PetUi.BodySize, true, FarmShopWidth);
        farmShopLevel.Margin = new Thickness(0, 6, 0, 3);
        farmShopXpFill = new Border { Background = PetUi.Game.Gold, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left };
        var xpBar = new Border { Background = PrepCardBack, CornerRadius = new CornerRadius(3), Height = 6, Child = farmShopXpFill };
        farmShopNextUnlock = PanelLine(PetUi.SmallSize, maxWidth: FarmShopWidth);
        farmShopNextUnlock.Foreground = PetUi.Game.Soft;
        farmShopNextUnlock.Margin = new Thickness(0, 3, 0, 0);

        farmShopWeather = PanelLine(PetUi.SmallSize, maxWidth: FarmShopWidth);
        farmShopWeather.Foreground = PetUi.Game.Gold;
        farmShopWeather.Margin = new Thickness(0, 3, 0, 0);

        var body = new StackPanel { Width = FarmShopWidth, Children = { PanelTitle(title), farmShopCoins, farmShopLevel, xpBar, farmShopNextUnlock, farmShopWeather } };
        farmShopRows.Clear();
        foreach (var crop in Enum.GetValues<FarmCrop>())
        {
            var count = PanelLine(PetUi.SmallSize, maxWidth: 170);
            count.Foreground = PetUi.Game.Soft;
            var chosen = crop;
            var (button, price, _) = HudButton(() => FarmSell(chosen), plain: true);
            body.Children.Add(FarmShopRow(new FarmCropIcon(crop, 30), FarmCropName(crop), count, button));
            farmShopRows.Add((crop, count, button, price));
        }
        farmShopFishRows.Clear();
        foreach (var fish in Enum.GetValues<FarmFish>())
        {
            var count = PanelLine(PetUi.SmallSize, maxWidth: 170);
            count.Foreground = PetUi.Game.Soft;
            var chosen = fish;
            var (button, price, _) = HudButton(() => FarmSell(chosen), plain: true);
            body.Children.Add(FarmShopRow(new FarmFishIcon(fish, 30), FarmFishName(fish), count, button));
            farmShopFishRows.Add((fish, count, button, price));
        }

        (farmShopTileButton, farmShopTilePrice, _) = HudButton(() => FarmOpenTile(), plain: true);
        farmShopTileNote = PanelLine(PetUi.SmallSize, maxWidth: 170);
        farmShopTileNote.Foreground = PetUi.Game.Soft;
        var tileRow = FarmShopRow(new Path { Data = LockMark, Stroke = PetUi.Game.Gold, StrokeThickness = 2, Width = 30, Height = 24, Stretch = Stretch.Uniform }, "", farmShopTileNote, farmShopTileButton);
        farmShopTileName = FarmShopRowName(tileRow);
        tileRow.Margin = new Thickness(0, 14, 0, 0);
        body.Children.Add(tileRow);

        (farmShopScareButton, farmShopScarePrice, _) = HudButton(() => FarmBuyScarecrow(), plain: true);
        farmShopScareNote = PanelLine(PetUi.SmallSize, maxWidth: 170);
        farmShopScareNote.Foreground = PetUi.Game.Soft;
        farmShopScareIcon = new FarmScarecrowIcon(30);
        var scareRow = FarmShopRow(farmShopScareIcon, "", farmShopScareNote, farmShopScareButton);
        farmShopScareName = FarmShopRowName(scareRow);
        body.Children.Add(scareRow);

        body.Children.Add(FarmDecorPanel());

        var (sellAll, sellAllText, _) = HudButton(() => FarmSellAll(), plain: true);
        farmShopSellAll = sellAll; farmShopSellAllText = sellAllText;
        var (close, closeText, _) = HudButton(CloseFarmShop, plain: true);
        close.Margin = new Thickness(0);
        closeText.Text = L("Đóng", "Close");

        var window = CreateGamePanel(body, PanelActions(new UIElement[] { sellAll }, new UIElement[] { close }), "Pet farm shop", scene: PetSceneKind.Home);
        window.Closed += (_, _) => { if (farmShopWindow == window) ClearFarmShop(); };
        farmShopWindow = window;
        UpdateFarmShop();
        window.Show();
        PlaceGamePanel(window);
    }

    // One row for all six: more rows would not fit on a low screen.
    private Border FarmDecorPanel()
    {
        var name = PanelLine(PetUi.BodySize, true, FarmShopWidth - 18);
        name.Text = L("Đồ trang trí", "Decorations");
        name.HorizontalAlignment = HorizontalAlignment.Left;
        farmShopDecorNote = PanelLine(PetUi.SmallSize, maxWidth: FarmShopWidth - 18);
        farmShopDecorNote.Foreground = PetUi.Game.Soft;
        farmShopDecorNote.HorizontalAlignment = HorizontalAlignment.Left;
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Margin = new Thickness(0, 5, 0, 0) };
        farmShopDecors.Clear();
        foreach (var decor in Enum.GetValues<FarmDecor>())
        {
            var chosen = decor;
            var (button, _, _) = HudButton(() => FarmToggleDecor(chosen), plain: true);
            var icon = new FarmDecorIcon(decor, 34);
            var slash = new Path { Data = Geometry.Parse("M5,33 L33,5"), Stroke = PetUi.Game.Soft, StrokeThickness = 3, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Width = 38, Height = 38, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            button.Child = new Grid { Children = { icon, slash } };
            button.MinWidth = 0;
            button.Padding = new Thickness(3);
            button.Margin = new Thickness(2, 0, 2, 0);
            button.BorderThickness = new Thickness(1.5);
            ToolTipService.SetShowOnDisabled(button, true);
            grid.Children.Add(button);
            farmShopDecors.Add(new FarmDecorToggle(decor, button, icon, slash));
        }
        return new Border { Background = PrepCardBack, CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 6, 8, 6), Margin = new Thickness(0, 6, 0, 0), Child = new StackPanel { Children = { name, farmShopDecorNote, grid } } };
    }

    private void UpdateFarmShopDecors()
    {
        if (farm == null || farmShopDecorNote == null) return;
        int owned = farmShopDecors.Count(item => farm.HasDecor(item.Decor));
        farmShopDecorNote.Text = owned > 0
            ? LF($"Có {owned}/{farmShopDecors.Count} · bấm để cất hoặc bày lại", $"{owned} of {farmShopDecors.Count} · click to put away or bring out")
            : L("Xe hàng lưu động có bán", "Sold by the traveling stall");
        foreach (var item in farmShopDecors)
        {
            bool has = farm.HasDecor(item.Decor), shown = farm.ShowsDecor(item.Decor);
            item.Button.ToolTip = has ? FarmDecorName(item.Decor) : $"{FarmDecorName(item.Decor)} · {FarmPriceText(PetFarm.DecorPrice(item.Decor))}";
            item.Button.BorderBrush = shown ? PetUi.Game.Gold : Brushes.Transparent;
            item.Icon.Opacity = shown ? 1 : has ? 0.5 : 0.7;
            item.Slash.Visibility = has && !shown ? Visibility.Visible : Visibility.Collapsed;
            ShowFarmShopButton(item.Button, !paused && has);
        }
    }

    // Puts it away or brings it back. False when the farm does not have it.
    public bool FarmToggleDecor(FarmDecor decor)
    {
        if (farm == null || paused || !farm.ShowDecor(decor, !farm.ShowsDecor(decor))) return false;
        PlayPetSound("pop");
        FarmChanged();
        return true;
    }

    private static Border FarmShopRow(FrameworkElement icon, string name, TextBlock note, Border button, Border? second = null)
    {
        icon.Margin = new Thickness(0, 0, 10, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        var nameText = PanelLine(PetUi.BodySize, true, 170);
        nameText.Text = name;
        nameText.HorizontalAlignment = note.HorizontalAlignment = HorizontalAlignment.Left; // A row without buttons has room to spare.
        button.Margin = new Thickness(8, 0, 0, 0);
        button.MinWidth = 120;
        button.VerticalAlignment = VerticalAlignment.Center;
        var row = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(button, Dock.Right);
        row.Children.Add(icon);
        row.Children.Add(button);
        if (second != null)
        {
            second.Margin = new Thickness(6, 0, 0, 0);
            second.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(second, Dock.Right);
            row.Children.Add(second);
        }
        row.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { nameText, note } });
        return new Border { Background = PrepCardBack, CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 6, 8, 6), Margin = new Thickness(0, 6, 0, 0), Child = row };
    }

    private static TextBlock FarmShopRowName(Border row) => (TextBlock)((DockPanel)row.Child).Children.OfType<StackPanel>().Single().Children[0];

    private static void ShowFarmShopButton(Border button, bool enabled)
    {
        button.IsEnabled = enabled;
        button.Opacity = enabled ? 1 : 0.5;
    }

    private void UpdateFarmShop()
    {
        if (farmShopWindow == null || farm == null || farmShopCoins == null) return;
        farmShopPaused = paused;
        farmShopSky = FarmWeather;
        farmShopWeather!.Text = farmShopSky is WeatherKind sky ? FarmWeatherText(sky) : "";
        farmShopWeather.Visibility = farmShopSky == null ? Visibility.Collapsed : Visibility.Visible;
        farmShopCoins.Text = LF($"Xu nông trại: {farm.Coins}", $"Farm coins: {farm.Coins}");
        farmShopLevel!.Text = farm.Level >= PetFarm.MaxLevel
            ? LF($"Cấp {farm.Level} · {farm.Xp} XP", $"Level {farm.Level} · {farm.Xp} XP")
            : LF($"Cấp {farm.Level} · {farm.Xp}/{farm.NextLevelXp} XP", $"Level {farm.Level} · {farm.Xp}/{farm.NextLevelXp} XP");
        farmShopXpFill!.Width = FarmShopWidth * farm.LevelProgress;
        string unlocks = farm.Level >= PetFarm.MaxLevel ? "" : FarmNextUnlockNames(farm.Level + 1);
        farmShopNextUnlock!.Text = farm.Level >= PetFarm.MaxLevel ? L("Đã đạt cấp cao nhất", "Top level reached")
            : unlocks.Length > 0 ? LF($"Cấp sau mở: {unlocks}", $"Next level opens: {unlocks}")
            : LF($"Cấp sau thưởng {PetFarm.LevelReward(farm.Level + 1)} xu", $"Next level pays {PetFarm.LevelReward(farm.Level + 1)} coins");
        foreach (var (crop, count, button, price) in farmShopRows)
        {
            count.Text = LF($"Trong kho: {farm.StockOf(crop)}", $"In store: {farm.StockOf(crop)}");
            price.Text = LF($"Bán · +{PetFarm.SellPrice(crop)} xu", $"Sell · +{PetFarm.SellPrice(crop)}");
            ShowFarmShopButton(button, !paused && farm.StockOf(crop) > 0);
        }
        foreach (var (fish, count, button, price) in farmShopFishRows)
        {
            count.Text = LF($"Trong kho: {farm.FishOf(fish)}", $"In store: {farm.FishOf(fish)}");
            price.Text = LF($"Bán · +{PetFarm.SellPrice(fish)} xu", $"Sell · +{PetFarm.SellPrice(fish)}");
            ShowFarmShopButton(button, !paused && farm.FishOf(fish) > 0);
        }

        int next = farm.NextLockedTile;
        farmShopTileName!.Text = next >= 0 ? LF($"Mở ô thứ {next + 1}", $"Open tile {next + 1}") : L("Mở thêm ô", "More tiles");
        farmShopTileNote!.Text = LF($"Đã mở {farm.OpenTiles}/{PetFarm.TileCount} ô", $"{farm.OpenTiles} of {PetFarm.TileCount} open");
        farmShopTilePrice!.Text = next >= 0 ? FarmPriceText(PetFarm.OpenPrice(next)) : L("Đã mở hết", "All open");
        ShowFarmShopButton(farmShopTileButton!, !paused && next >= 0 && farm.Coins >= PetFarm.OpenPrice(next));

        farmShopScareIcon!.Golden = farm.GoldenScarecrow;
        farmShopScareName!.Text = farm.GoldenScarecrow ? L("Bù nhìn vàng", "Golden scarecrow") : L("Bù nhìn", "Scarecrow");
        farmShopScareNote!.Text = L("Chim giấy đến thưa gấp đôi", "Paper birds come half as often");
        farmShopScarePrice!.Text = farm.Scarecrow ? L("Đã có", "Owned") : FarmPriceText(PetFarm.ScarecrowPrice);
        ShowFarmShopButton(farmShopScareButton!, !paused && !farm.Scarecrow && farm.Coins >= PetFarm.ScarecrowPrice);

        UpdateFarmShopDecors();

        farmShopSellAllText!.Text = LF($"Bán hết · +{farm.StockValue} xu", $"Sell all · +{farm.StockValue}");
        ShowFarmShopButton(farmShopSellAll!, !paused && farm.StockValue > 0);
    }

    public void CloseFarmShop() => farmShopWindow?.Close();

    private void ClearFarmShop()
    {
        farmShopWindow = null;
        farmShopScareIcon = null;
        farmShopCoins = farmShopTileName = farmShopTileNote = farmShopTilePrice = farmShopScareName = farmShopScareNote = farmShopScarePrice = farmShopSellAllText = null;
        farmShopTileButton = farmShopScareButton = farmShopSellAll = null;
        farmShopLevel = farmShopNextUnlock = farmShopWeather = null;
        farmShopXpFill = null;
        farmShopRows.Clear();
        farmShopFishRows.Clear();
        farmShopDecors.Clear();
        farmShopDecorNote = null;
    }

    public int FarmSell(FarmCrop crop) => FarmSold(farm == null || paused ? 0 : farm.Sell(crop));
    public int FarmSell(FarmFish fish) => FarmSold(farm == null || paused ? 0 : farm.Sell(fish));
    public int FarmSellAll() => FarmSold(farm == null || paused ? 0 : farm.SellAll());

    private int FarmSold(int coins)
    {
        if (coins <= 0) return 0;
        PlayPetSound("pop");
        FarmChanged();
        return coins;
    }

    public bool FarmOpenTile()
    {
        if (farm == null || paused) return false;
        int tile = farm.NextLockedTile;
        if (!farm.OpenNextTile()) return false;
        StartFarmEffect(tile, FarmEffect.Harvest);
        PlayPetSound("sparkle");
        FarmChanged();
        return true;
    }

    public bool FarmBuyScarecrow()
    {
        if (farm == null || paused || !farm.BuyScarecrow()) return false;
        PlayPetSound("sparkle");
        FarmChanged();
        return true;
    }
}
