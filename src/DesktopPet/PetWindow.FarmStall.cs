using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DesktopPet;

// The traveling stall (docs/FARM-GAME.md, milestone 4): a cart rolls in beside the field now and
// then, buys what it asks for, and rolls away after a few minutes. Like the rest of the field it
// never touches the cat. Its offers end with the visit; nothing is lost when one is missed.
public sealed partial class PetWindow
{
    private const double FarmCartRoll = 150, FarmCartGap = 4;

    private sealed record FarmStallRow(Border Icon, TextBlock Name, TextBlock Note, Border Deliver);
    private sealed record FarmDecorRow(Border Row, Border Icon, TextBlock Name, TextBlock Note, Border Buy, TextBlock Price);

    private readonly FarmStall farmStall = new();
    private Window? farmCartWindow, farmStallWindow;
    private PetFarmStallArt? farmCartArt;
    private FrameworkElement? farmCartLabel;
    private TextBlock? farmStallNote, farmStallDecorTitle;
    private readonly List<FarmStallRow> farmStallRows = new();
    private readonly List<FarmDecorRow> farmStallDecorRows = new();
    private bool farmStallPaused;
    private double farmCartRolled;

    public bool FarmStallHere => farmStall.Here;
    public bool FarmStallOpen => farmStallWindow != null;

    // One visit now, if this is a moment for it. The cat has no part in it, so a sleeping cat does not matter.
    public bool TryFarmStall()
    {
        if (farm == null || farmStall.Phase != FarmStallPhase.Away || !FarmVisible || FarmLandInAir
            || Blocking(AutoBlock.Paused | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Away) != AutoBlock.None) return false;
        farmStall.Arrive(farm.NewOffers(featureRandom), farm.NewDecors(featureRandom));
        ShowFarmCart();
        ShowFarmCartTip();
        PlayPetSound("sparkle");
        return true;
    }

    // Each frame the clock moves.
    private void StepFarmStall(double dt)
    {
        if (farmStall.Phase == FarmStallPhase.Away)
        {
            if (farmStall.CountDown(dt)) farmStall.Schedule(TryFarmStall() ? FarmStall.NextDelay(featureRandom) : FarmStall.RetrySeconds);
            return;
        }
        bool rolling = farmStall.Rolling;
        if (rolling) farmCartRolled += dt;
        if (farmStall.Step(dt)) { CloseFarmCart(); UpdateFarmStall(); return; }
        if (farmCartArt != null && (rolling || farmStall.Rolling))
        {
            farmCartArt.Rolled = farmCartRolled * FarmCartRoll / FarmStall.RollSeconds;
            farmCartArt.InvalidateVisual();
        }
        if (farmCartLabel != null) farmCartLabel.Opacity = farmStall.Phase == FarmStallPhase.Open ? 1 : 0;
    }

    private void ShowFarmCart()
    {
        var art = new PetFarmStallArt();
        var label = new Border
        {
            Background = PetUi.Game.Panel, BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(PetUi.SmallRadius), Padding = new Thickness(8, 2, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Opacity = 0,
            Child = new TextBlock { Text = L("Bấm để xem hàng", "Click to browse"), Foreground = System.Windows.Media.Brushes.White, FontFamily = PetUi.TextFont, FontSize = PetUi.SmallSize }
        };
        var surface = new Grid { Width = PetFarmStallArt.ArtWidth, Height = PetFarmStallArt.ArtHeight, Cursor = Cursors.Hand };
        surface.Children.Add(art);
        surface.Children.Add(label);
        farmCartArt = art; farmCartLabel = label; farmCartRolled = 0;
        farmCartWindow = CreateOverlayWindow(surface, "Pet farm cart");
        farmCartWindow.Width = PetFarmStallArt.ArtWidth; farmCartWindow.Height = PetFarmStallArt.ArtHeight;
        farmCartWindow.MouseLeftButtonDown += (_, e) => { e.Handled = true; OpenFarmStall(); };
        farmCartWindow.Closed += (_, _) => { farmCartWindow = null; farmCartArt = null; farmCartLabel = null; };
        art.Ready = FarmStallReady;
        PlaceFarmCart();
        farmCartWindow.Show();
        PlaceBelowCat(farmCartWindow);
        PlaceFarmCart();
    }

    // Left of the field if there is room, otherwise the free place nearest to that; never on the land or over the screen's edge.
    private (double X, int Side) FarmCartPlace
    {
        get
        {
            double width = PetFarmStallArt.ArtWidth, wanted = FarmPlotLeft - width - FarmCartGap;
            double spot = FarmLands.FreeSpot(wanted, width, FarmLandsTaken(farmMonitor, null), farmArea.Left, Math.Max(farmArea.Left, farmArea.Right - width), FarmCartGap);
            return (spot, spot < FarmPlotLeft ? -1 : 1);
        }
    }

    // Every frame: it stands beside the plot, wherever that has been moved to.
    private void PlaceFarmCart()
    {
        if (farmCartWindow == null) return;
        var (x, side) = FarmCartPlace;
        double shown = Math.Clamp(farmStall.Amount, 0, 1);
        farmCartWindow.Opacity = shown;
        MoveWindow(farmCartWindow, x + side * (1 - shown) * FarmCartRoll, farmArea.Bottom + PetFarmArt.BottomRoom - PetFarmStallArt.ArtHeight, farmScale);
    }

    private void CloseFarmCart() => farmCartWindow?.Close();

    // A fullscreen app or a battle takes the screen: the visit is over.
    private void ResetFarmCart()
    {
        farmStall.Reset();
        CloseFarmCart();
    }

    // Each frame the field is shown.
    private void WatchFarmStall()
    {
        if (farmStallWindow == null) return;
        if (!FarmShopAllowed) CloseFarmStall();
        else if (farmStallPaused != paused) UpdateFarmStall();
    }

    public void OpenFarmStall()
    {
        if (farm == null || paused || !FarmVisible || !FarmShopAllowed || farmStallWindow != null || !farmStall.Here) return;
        CloseFarmShop(); // They all open in the same place.
        CloseFarmKitchen();
        var title = PanelLine(PetUi.DisplaySize, true, FarmShopWidth);
        title.Text = L("Gian hàng lưu động", "Traveling stall");
        farmStallNote = PanelLine(PetUi.SmallSize, maxWidth: FarmShopWidth);
        farmStallNote.Foreground = PetUi.Game.Soft;
        farmStallNote.Margin = new Thickness(0, 2, 0, 4);

        var body = new StackPanel { Width = FarmShopWidth, Children = { PanelTitle(title), farmStallNote } };
        farmStallRows.Clear();
        for (int i = 0; i < PetFarm.MaxOfferSlots; i++)
        {
            int slot = i;
            var line = PanelLine(PetUi.SmallSize, maxWidth: 190);
            line.Foreground = PetUi.Game.Soft;
            var (deliver, deliverText, _) = HudButton(() => FarmDeliver(slot), plain: true);
            deliverText.Text = L("Giao", "Deliver");
            var icon = new Border { Width = 30, Height = 30 };
            var row = FarmShopRow(icon, "", line, deliver);
            deliver.MinWidth = 88;
            body.Children.Add(row);
            farmStallRows.Add(new FarmStallRow(icon, FarmShopRowName(row), line, deliver));
        }

        farmStallDecorTitle = PanelLine(PetUi.BodySize, true, FarmShopWidth);
        farmStallDecorTitle.Text = L("Đồ trang trí", "Decorations");
        farmStallDecorTitle.Margin = new Thickness(0, 12, 0, 0);
        body.Children.Add(farmStallDecorTitle);
        farmStallDecorRows.Clear();
        for (int i = 0; i < PetFarm.DecorsPerVisit; i++)
        {
            int slot = i;
            var line = PanelLine(PetUi.SmallSize, maxWidth: 190);
            line.Foreground = PetUi.Game.Soft;
            var (buy, price, _) = HudButton(() => FarmBuyDecor(slot), plain: true);
            var icon = new Border { Width = 30, Height = 30 };
            var row = FarmShopRow(icon, "", line, buy);
            buy.MinWidth = 88;
            body.Children.Add(row);
            farmStallDecorRows.Add(new FarmDecorRow(row, icon, FarmShopRowName(row), line, buy, price));
        }

        var (close, closeText, _) = HudButton(CloseFarmStall, plain: true);
        close.Margin = new Thickness(0);
        closeText.Text = L("Đóng", "Close");

        var window = CreateGamePanel(body, PanelActions(Array.Empty<UIElement>(), new UIElement[] { close }), "Pet farm stall", scene: PetSceneKind.Home);
        window.Closed += (_, _) => { if (farmStallWindow == window) { farmStallWindow = null; farmStallNote = farmStallDecorTitle = null; farmStallRows.Clear(); farmStallDecorRows.Clear(); } };
        farmStallWindow = window;
        UpdateFarmStall();
        window.Show();
        PlaceGamePanel(window);
    }

    private bool FarmStallReady => farm != null && farmStall.Offers.Any(farm.CanDeliver);

    private string FarmGoodName(string good) =>
        Enum.TryParse<FarmCrop>(good, out var crop) ? FarmCropName(crop) : Enum.TryParse<FarmFish>(good, out var fish) ? FarmFishName(fish) : good;

    private void UpdateFarmStall()
    {
        if (farmCartArt != null && farmCartArt.Ready != FarmStallReady)
        {
            farmCartArt.Ready = FarmStallReady;
            farmCartArt.InvalidateVisual();
        }
        if (farmStallWindow == null || farm == null || farmStallNote == null) return;
        farmStallPaused = paused;
        farmStallNote.Text = farmStall.Here
            ? LF($"Xe rời đi sau {FarmClock(TimeSpan.FromSeconds(farmStall.Left))}.", $"Leaves in {FarmClock(TimeSpan.FromSeconds(farmStall.Left))}.")
            : L("Xe đã đi. Giao nốt rồi đóng bảng nhé.", "The stall has left. Deliver what you can, then close this.");
        for (int i = 0; i < farmStallRows.Count; i++)
        {
            var row = farmStallRows[i];
            var offer = i < farmStall.Offers.Count ? farmStall.Offers[i] : null;
            row.Deliver.Visibility = offer is { Done: false } ? Visibility.Visible : Visibility.Collapsed;
            if (offer == null)
            {
                ShowFarmStallIcon(row, null);
                row.Name.Text = LF($"Lời mua thứ {i + 1}", $"Offer {i + 1}");
                row.Note.Text = FarmLevelText(PetFarm.SlotLevel(i));
                continue;
            }
            ShowFarmStallIcon(row, offer.Items.Keys.First());
            if (offer.Done)
            {
                row.Name.Text = L("Đã giao", "Delivered");
                row.Note.Text = "";
                continue;
            }
            row.Name.Text = string.Join(" · ", offer.Items.Select(item => $"{FarmGoodName(item.Key)} {farm.GoodsOf(item.Key)}/{item.Value}"));
            row.Note.Text = LF($"+{PetFarm.OfferCoins(offer)} xu · +{PetFarm.OfferXp(offer)} XP", $"+{PetFarm.OfferCoins(offer)} coins · +{PetFarm.OfferXp(offer)} XP");
            ShowFarmShopButton(row.Deliver, !paused && farm.CanDeliver(offer));
        }
        UpdateFarmStallDecors();
    }

    private string FarmDecorName(FarmDecor decor) => decor switch
    {
        FarmDecor.FlowerBed => L("Luống hoa", "Flower bed"),
        FarmDecor.WaterLily => L("Hoa súng", "Water lilies"),
        FarmDecor.BambooFence => L("Hàng rào tre", "Bamboo fence"),
        FarmDecor.Lantern => L("Đèn lồng", "Lantern"),
        FarmDecor.WoodBridge => L("Cầu gỗ", "Wooden bridge"),
        _ => L("Mái che", "Hearth roof")
    };

    private string FarmDecorSpot(FarmDecor decor) => decor switch
    {
        FarmDecor.WaterLily or FarmDecor.WoodBridge => L("Bày ở hồ", "Goes on the pond"),
        FarmDecor.Awning => L("Bày ở bếp", "Goes on the hearth"),
        _ => L("Bày ở ruộng", "Goes on the field")
    };

    private void UpdateFarmStallDecors()
    {
        if (farm == null || farmStallDecorTitle == null) return;
        var decors = farmStall.Decors;
        farmStallDecorTitle.Visibility = decors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        for (int i = 0; i < farmStallDecorRows.Count; i++)
        {
            var row = farmStallDecorRows[i];
            row.Row.Visibility = i < decors.Count ? Visibility.Visible : Visibility.Collapsed;
            if (i >= decors.Count) continue;
            var decor = decors[i];
            if (!Equals(row.Icon.Tag, decor)) { row.Icon.Tag = decor; row.Icon.Child = new FarmDecorIcon(decor, 30); }
            row.Name.Text = FarmDecorName(decor);
            bool owned = farm.HasDecor(decor);
            int price = PetFarm.DecorPrice(decor), missing = price - farm.Coins;
            row.Note.Text = owned || missing <= 0 ? FarmDecorSpot(decor) : LF($"Còn thiếu {missing} xu", $"{missing} coins short");
            row.Price.Text = owned ? L("Đã mua", "Bought") : FarmPriceText(price);
            ShowFarmShopButton(row.Buy, !paused && farm.CanBuyDecor(decor));
        }
    }

    // Like delivering: it may be finished in a panel still open after the stall has left.
    public bool FarmBuyDecor(int slot)
    {
        if (farm == null || paused || slot < 0 || slot >= farmStall.Decors.Count || (!farmStall.Here && farmStallWindow == null)) return false;
        var decor = farmStall.Decors[slot];
        if (!farm.BuyDecor(decor)) return false;
        ShowFarmLabel(PetFarm.TileCount / 2, LF($"Đã mua {FarmDecorName(decor)}!", $"Bought: {FarmDecorName(decor)}!"), FarmGainSeconds);
        PlayPetSound("sparkle");
        FarmChanged();
        return true;
    }

    // Only when the good changes: this runs every second.
    private static void ShowFarmStallIcon(FarmStallRow row, string? good)
    {
        if (Equals(row.Icon.Tag, good)) return;
        row.Icon.Tag = good;
        row.Icon.Child = Enum.TryParse<FarmCrop>(good, out var crop) ? new FarmCropIcon(crop, 30)
            : Enum.TryParse<FarmFish>(good, out var fish) ? new FarmFishIcon(fish, 30) : null;
    }

    public void CloseFarmStall() => farmStallWindow?.Close();

    // The coins it brought, 0 when it could not be delivered.
    public int FarmDeliver(int slot)
    {
        if (farm == null || paused || slot < 0 || slot >= farmStall.Offers.Count || (!farmStall.Here && farmStallWindow == null)) return 0;
        int coins = farm.Deliver(farmStall.Offers[slot]);
        if (coins <= 0) return 0;
        FarmReport = FarmReport with { Deliveries = FarmReport.Deliveries + 1 };
        ShowFarmLabel(PetFarm.TileCount / 2, LF($"+{coins} xu", $"+{coins} coins"), FarmGainSeconds);
        PlayPetSound("sparkle");
        FarmChanged();
        return coins;
    }
}
