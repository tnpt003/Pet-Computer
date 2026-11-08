using System;
using System.Collections.Generic;
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
    private TextBlock? farmShopCoins, farmShopTileName, farmShopTileNote, farmShopTilePrice, farmShopScareNote, farmShopScarePrice, farmShopSellAllText;
    private Border? farmShopTileButton, farmShopScareButton, farmShopSellAll;
    private readonly List<(FarmCrop Crop, TextBlock Count, Border Button, TextBlock Price)> farmShopRows = new();
    private bool farmShopPaused;

    public bool FarmShopOpen => farmShopWindow != null;
    // A battle panel sits in the same place: the shop gives way to it.
    private bool FarmShopAllowed => Blocking(AutoBlock.BattleScreens) == AutoBlock.None;

    private string FarmPriceText(int price) => price == 0 ? L("miễn phí", "free") : LF($"{price} xu", $"{price} coins");

    // Each frame the field is shown.
    private void WatchFarmShop()
    {
        if (farmShopWindow == null) return;
        if (!FarmShopAllowed) CloseFarmShop();
        else if (farmShopPaused != paused) UpdateFarmShop();
    }

    public void OpenFarmShop()
    {
        if (farm == null || paused || !FarmVisible || !FarmShopAllowed || farmShopWindow != null) return;
        var title = PanelLine(PetUi.DisplaySize, true, FarmShopWidth);
        title.Text = L("Kho & cửa hàng", "Store & shop");
        farmShopCoins = PanelLine(PetUi.TitleSize, true, FarmShopWidth);
        farmShopCoins.Foreground = PetUi.Game.Gold;
        farmShopCoins.Margin = new Thickness(0, 2, 0, 0);

        var body = new StackPanel { Width = FarmShopWidth, Children = { PanelTitle(title), farmShopCoins } };
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
        body.Children.Add(FarmShopRow(new FarmScarecrowIcon(30), L("Bù nhìn", "Scarecrow"), farmShopScareNote, farmShopScareButton));

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

    private static Border FarmShopRow(FrameworkElement icon, string name, TextBlock note, Border button)
    {
        icon.Margin = new Thickness(0, 0, 10, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        var nameText = PanelLine(PetUi.BodySize, true, 170);
        nameText.Text = name;
        button.Margin = new Thickness(8, 0, 0, 0);
        button.MinWidth = 120;
        button.VerticalAlignment = VerticalAlignment.Center;
        var row = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(button, Dock.Right);
        row.Children.Add(icon);
        row.Children.Add(button);
        row.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { nameText, note } });
        return new Border { Background = PrepCardBack, CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 6, 8, 6), Margin = new Thickness(0, 6, 0, 0), Child = row };
    }

    private static TextBlock FarmShopRowName(Border row) => (TextBlock)((StackPanel)((DockPanel)row.Child).Children[2]).Children[0];

    private static void ShowFarmShopButton(Border button, bool enabled)
    {
        button.IsEnabled = enabled;
        button.Opacity = enabled ? 1 : 0.5;
    }

    private void UpdateFarmShop()
    {
        if (farmShopWindow == null || farm == null || farmShopCoins == null) return;
        farmShopPaused = paused;
        farmShopCoins.Text = LF($"Xu nông trại: {farm.Coins}", $"Farm coins: {farm.Coins}");
        foreach (var (crop, count, button, price) in farmShopRows)
        {
            count.Text = LF($"Trong kho: {farm.StockOf(crop)}", $"In store: {farm.StockOf(crop)}");
            price.Text = LF($"Bán · +{PetFarm.SellPrice(crop)} xu", $"Sell · +{PetFarm.SellPrice(crop)}");
            ShowFarmShopButton(button, !paused && farm.StockOf(crop) > 0);
        }

        int next = farm.NextLockedTile;
        farmShopTileName!.Text = next >= 0 ? LF($"Mở ô thứ {next + 1}", $"Open tile {next + 1}") : L("Mở thêm ô", "More tiles");
        farmShopTileNote!.Text = LF($"Đã mở {farm.OpenTiles}/{PetFarm.TileCount} ô", $"{farm.OpenTiles} of {PetFarm.TileCount} open");
        farmShopTilePrice!.Text = next >= 0 ? FarmPriceText(PetFarm.OpenPrice(next)) : L("Đã mở hết", "All open");
        ShowFarmShopButton(farmShopTileButton!, !paused && next >= 0 && farm.Coins >= PetFarm.OpenPrice(next));

        farmShopScareNote!.Text = L("Chim giấy đến thưa gấp đôi", "Paper birds come half as often");
        farmShopScarePrice!.Text = farm.Scarecrow ? L("Đã có", "Owned") : FarmPriceText(PetFarm.ScarecrowPrice);
        ShowFarmShopButton(farmShopScareButton!, !paused && !farm.Scarecrow && farm.Coins >= PetFarm.ScarecrowPrice);

        farmShopSellAllText!.Text = LF($"Bán hết · +{farm.StockValue} xu", $"Sell all · +{farm.StockValue}");
        ShowFarmShopButton(farmShopSellAll!, !paused && farm.StockValue > 0);
    }

    public void CloseFarmShop() => farmShopWindow?.Close();

    private void ClearFarmShop()
    {
        farmShopWindow = null;
        farmShopCoins = farmShopTileName = farmShopTileNote = farmShopTilePrice = farmShopScareNote = farmShopScarePrice = farmShopSellAllText = null;
        farmShopTileButton = farmShopScareButton = farmShopSellAll = null;
        farmShopRows.Clear();
    }

    public int FarmSell(FarmCrop crop) => FarmSold(farm == null || paused ? 0 : farm.Sell(crop));
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
