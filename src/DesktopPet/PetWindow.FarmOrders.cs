using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DesktopPet;

// The farm's order board (docs/FARM-GAME.md, milestone C): what the paper birds bring in, delivered
// from the store for coins and XP. Like the rest of the field it never touches the cat.
public sealed partial class PetWindow
{
    private sealed record FarmOrderRow(Border Icon, TextBlock Name, TextBlock Note, Border Deliver, Border Skip);

    private Window? farmOrdersWindow;
    private readonly List<FarmOrderRow> farmOrderRows = new();
    private bool farmOrdersPaused;
    private double farmMailLeft;

    public bool FarmOrdersOpen => farmOrdersWindow != null;

    // Once a second while the clock moves. A new order comes with a bird, in silence: nobody asked for it.
    private void CheckFarmOrders(DateTime now)
    {
        if (farm == null) return;
        if (farm.RefreshOrders(now, featureRandom))
        {
            farmMailLeft = PetFarmArt.MailSeconds;
            FarmChanged();
        }
        else UpdateFarmOrders();
    }

    // Each frame the field is shown.
    private void WatchFarmOrders()
    {
        if (farmOrdersWindow == null) return;
        if (!FarmShopAllowed) CloseFarmOrders();
        else if (farmOrdersPaused != paused) UpdateFarmOrders();
    }

    public void OpenFarmOrders()
    {
        if (farm == null || paused || !FarmVisible || !FarmShopAllowed || farmOrdersWindow != null) return;
        CloseFarmShop(); // They all open in the same place.
        CloseFarmKitchen();
        var title = PanelLine(PetUi.DisplaySize, true, FarmShopWidth);
        title.Text = L("Đơn hàng", "Orders");
        var note = PanelLine(PetUi.SmallSize, maxWidth: FarmShopWidth);
        note.Foreground = PetUi.Game.Soft;
        note.Margin = new Thickness(0, 2, 0, 4);
        note.Text = L("Chim giấy mang đơn tới. Giao từ kho để nhận xu và XP.", "Paper birds bring the orders. Deliver from the store for coins and XP.");

        var body = new StackPanel { Width = FarmShopWidth, Children = { PanelTitle(title), note } };
        farmOrderRows.Clear();
        for (int i = 0; i < PetFarm.MaxOrderSlots; i++)
        {
            int slot = i;
            var line = PanelLine(PetUi.SmallSize, maxWidth: 190);
            line.Foreground = PetUi.Game.Soft;
            var (deliver, deliverText, _) = HudButton(() => FarmDeliver(slot), plain: true);
            deliverText.Text = L("Giao", "Deliver");
            var (skip, skipText, _) = HudButton(() => FarmSkipOrder(slot), plain: true);
            skipText.Text = L("Bỏ", "Skip");
            var icon = new Border { Width = 30, Height = 30 };
            var row = FarmShopRow(icon, "", line, deliver, skip);
            deliver.MinWidth = 88;
            skip.MinWidth = 44;
            body.Children.Add(row);
            farmOrderRows.Add(new FarmOrderRow(icon, FarmShopRowName(row), line, deliver, skip));
        }

        var (close, closeText, _) = HudButton(CloseFarmOrders, plain: true);
        close.Margin = new Thickness(0);
        closeText.Text = L("Đóng", "Close");

        var window = CreateGamePanel(body, PanelActions(Array.Empty<UIElement>(), new UIElement[] { close }), "Pet farm orders", scene: PetSceneKind.Home);
        window.Closed += (_, _) => { if (farmOrdersWindow == window) { farmOrdersWindow = null; farmOrderRows.Clear(); } };
        farmOrdersWindow = window;
        UpdateFarmOrders();
        window.Show();
        PlaceGamePanel(window);
    }

    private void UpdateFarmOrders()
    {
        if (farmOrdersWindow == null || farm == null) return;
        farmOrdersPaused = paused;
        var now = FarmNow;
        for (int i = 0; i < farmOrderRows.Count; i++)
        {
            var row = farmOrderRows[i];
            var slot = i < farm.Orders.Count && i < farm.OrderSlots ? farm.Orders[i] : null;
            bool order = slot?.Items != null;
            row.Deliver.Visibility = row.Skip.Visibility = order ? Visibility.Visible : Visibility.Collapsed;
            if (order)
            {
                var items = PetFarm.OrderItems(slot!).ToList();
                ShowFarmOrderIcon(row, items[0].Crop);
                row.Name.Text = string.Join(" · ", items.Select(item => $"{FarmCropName(item.Crop)} {farm.StockOf(item.Crop)}/{item.Count}"));
                row.Note.Text = LF($"+{PetFarm.OrderCoins(slot!)} xu · +{PetFarm.OrderXp(slot!)} XP", $"+{PetFarm.OrderCoins(slot!)} coins · +{PetFarm.OrderXp(slot!)} XP");
                ShowFarmShopButton(row.Deliver, !paused && farm.CanDeliver(i));
                ShowFarmShopButton(row.Skip, !paused);
                continue;
            }
            ShowFarmOrderIcon(row, null);
            if (i >= farm.OrderSlots)
            {
                row.Name.Text = LF($"Ô đơn thứ {i + 1}", $"Order place {i + 1}");
                row.Note.Text = FarmLevelText(PetFarm.SlotLevel(i));
            }
            else
            {
                row.Name.Text = L("Đơn mới sắp tới", "A new order is on its way");
                row.Note.Text = slot?.NextAt is DateTime next && next > now ? FarmClock(next - now) : "";
            }
        }
    }

    // Only when the crop changes: this runs every second.
    private static void ShowFarmOrderIcon(FarmOrderRow row, FarmCrop? crop)
    {
        if (Equals(row.Icon.Tag, crop)) return;
        row.Icon.Tag = crop;
        row.Icon.Child = crop is FarmCrop shown ? new FarmCropIcon(shown, 30) : null;
    }

    public void CloseFarmOrders() => farmOrdersWindow?.Close();

    // The coins it brought, 0 when it could not be delivered.
    public int FarmDeliver(int slot)
    {
        if (farm == null || paused) return 0;
        int coins = farm.Deliver(slot, FarmNow);
        if (coins <= 0) return 0;
        ShowFarmLabel(PetFarm.TileCount / 2, LF($"+{coins} xu", $"+{coins} coins"), FarmGainSeconds);
        PlayPetSound("sparkle");
        FarmChanged();
        return coins;
    }

    public bool FarmSkipOrder(int slot)
    {
        if (farm == null || paused || !farm.SkipOrder(slot, FarmNow)) return false;
        PlayPetSound("pop");
        FarmChanged();
        return true;
    }
}
