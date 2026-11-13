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
    private const double FarmCartRoll = 150, FarmCartGap = 4, FarmCartDragStart = 6;

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
    // Where this visit stands: chosen when the cart comes, and it stays there whatever land is moved.
    private double farmCartX, farmCartRoom = FarmCartRoll, farmCartScale = 1;
    private int farmCartSide = 1;
    // The user's hand on the cart (feedback 13/11): where the press was, where the cart stood then, and
    // whether it has moved past the click's slack. The place they chose is tried first next visit, this session only.
    private double? farmCartPress;
    private double farmCartPressX;
    private bool farmCartDragging;
    private double? farmCartChosenX;
    private string? farmCartChosenMonitor;
    private Border? farmStallLeave;
    // The wheels turn with every move the user makes (feedback 13/11): the distance rolled by hand, and a
    // short roll to the nearest gap when it is let go over land (from, to, 0–1; null: none).
    private const double FarmCartGlideSeconds = 0.3;
    private double farmCartWheel, farmCartGlideFrom, farmCartGlideAge;
    private double? farmCartGlideTo;
    private string? farmCartMonitor;
    private Area farmCartArea;

    public bool FarmStallHere => farmStall.Here;
    public bool FarmStallOpen => farmStallWindow != null;

    // One visit now, if this is a moment for it. The cat has no part in it, so a sleeping cat does not matter.
    public bool TryFarmStall()
    {
        if (farm == null || farmStall.Phase != FarmStallPhase.Away || !FarmVisible || FarmLandInAir
            || Blocking(AutoBlock.Paused | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Away) != AutoBlock.None
            || !PickFarmCartPlace(FindFarmMonitor(farmMonitor))) return false;
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
        if (farmCartArt != null && (rolling || farmStall.Rolling)) ShowFarmCartWheels();
        StepFarmCartGlide(dt);
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
        farmCartArt = art; farmCartLabel = label; farmCartRolled = farmCartWheel = 0; farmCartGlideTo = null;
        farmCartWindow = CreateOverlayWindow(surface, "Pet farm cart");
        farmCartWindow.Width = PetFarmStallArt.ArtWidth; farmCartWindow.Height = PetFarmStallArt.ArtHeight;
        farmCartWindow.MouseLeftButtonDown += (_, e) => { e.Handled = true; PressFarmCart(); };
        farmCartWindow.MouseMove += (_, _) => MoveFarmCart();
        farmCartWindow.MouseLeftButtonUp += (_, e) => { e.Handled = true; ReleaseFarmCart(click: true); };
        farmCartWindow.LostMouseCapture += (_, _) => ReleaseFarmCart(click: false);
        farmCartWindow.Closed += (_, _) => { farmCartWindow = null; farmCartArt = null; farmCartLabel = null; farmCartPress = null; farmCartDragging = false; farmCartGlideTo = null; };
        art.Ready = FarmStallReady;
        PlaceFarmCart();
        farmCartWindow.Show();
        PlaceBelowCat(farmCartWindow);
        PlaceFarmCart();
    }

    // Somewhere free on this monitor, a new place each visit. False when no gap is wide enough.
    private bool PickFarmCartPlace(FarmMonitor monitor)
    {
        double width = PetFarmStallArt.ArtWidth;
        var area = monitor.WorkDips;
        var taken = FarmLandsTaken(monitor.Name, null, withCart: false);
        double max = area.Right - width;
        double? chosen = farmCartChosenMonitor == monitor.Name && farmCartChosenX is double at && at >= area.Left && at <= max
            && Math.Abs(FarmLands.FreeSpot(at, width, taken, area.Left, max, FarmCartGap) - at) < 0.5 ? at : null;
        if ((chosen ?? FarmLands.RandomFreeSpot(featureRandom, width, taken, area.Left, max, FarmCartGap)) is not double x) return false;
        (farmCartSide, farmCartRoom) = FarmLands.RollWay(x, width, taken, area.Left, max, FarmCartGap, FarmCartRoll);
        farmCartX = x;
        StoreFarmCartMonitor(monitor);
        return true;
    }

    private void StoreFarmCartMonitor(FarmMonitor monitor)
    {
        farmCartMonitor = monitor.Name;
        farmCartArea = monitor.WorkDips;
        farmCartScale = monitor.Scale;
    }

    // Once a second: the screen may have changed under it. It goes elsewhere only if it has to, and leaves if nowhere is free.
    private void RefreshFarmCart()
    {
        if (farmCartWindow == null || FarmLandInAir) return;
        var monitor = FindFarmMonitor(farmCartMonitor);
        var area = monitor.WorkDips;
        double right = farmCartX + PetFarmStallArt.ArtWidth;
        bool clear = farmCartX >= area.Left && right <= area.Right
            && FarmLandsTaken(monitor.Name, null, withCart: false).All(land => right <= land.Left || farmCartX >= land.Right);
        if (clear) StoreFarmCartMonitor(monitor);
        else if (!PickFarmCartPlace(monitor)) ResetFarmCart();
    }

    // Every frame, for the roll in and out. The land does not matter to it.
    private void PlaceFarmCart()
    {
        if (farmCartWindow == null) return;
        double shown = Math.Clamp(farmStall.Amount, 0, 1);
        farmCartWindow.Opacity = shown;
        MoveWindow(farmCartWindow, farmCartX + farmCartSide * (1 - shown) * farmCartRoom, farmCartArea.Bottom + PetFarmArt.BottomRoom - PetFarmStallArt.ArtHeight, farmCartScale);
    }

    private void CloseFarmCart() => farmCartWindow?.Close();

    // A press is a click until the mouse goes past the slack; then the cart slides along the floor of its screen.
    private void PressFarmCart()
    {
        if (farmCartWindow == null) return;
        farmCartPress = ScreenCursor().X / farmCartScale;
        farmCartPressX = farmCartX;
        farmCartDragging = false;
        farmCartWindow.CaptureMouse();
    }

    private void MoveFarmCart()
    {
        if (farmCartPress is not double press || farmCartWindow == null) return;
        double moved = ScreenCursor().X / farmCartScale - press;
        if (!farmCartDragging)
        {
            if (Math.Abs(moved) < FarmCartDragStart || paused || farmStall.Phase != FarmStallPhase.Open || farmCartGlideTo != null) return;
            farmCartDragging = true;
        }
        double was = farmCartX;
        farmCartX = Math.Clamp(farmCartPressX + moved, farmCartArea.Left, Math.Max(farmCartArea.Left, farmCartArea.Right - PetFarmStallArt.ArtWidth));
        RollFarmCartWheels(farmCartX - was);
        PlaceFarmCart();
    }

    private void ReleaseFarmCart(bool click)
    {
        if (farmCartPress == null) return;
        farmCartPress = null;
        bool dragged = farmCartDragging;
        farmCartDragging = false;
        farmCartWindow?.ReleaseMouseCapture();
        if (dragged) SettleFarmCart();
        else if (click) OpenFarmStall();
    }

    // Let go over land: to the nearest gap; back where it stood when no gap is wide enough.
    private void SettleFarmCart()
    {
        var monitor = FindFarmMonitor(farmCartMonitor);
        var area = monitor.WorkDips;
        double width = PetFarmStallArt.ArtWidth, max = area.Right - width;
        var taken = FarmLandsTaken(monitor.Name, null, withCart: false);
        double x = FarmLands.FreeSpot(farmCartX, width, taken, area.Left, max, FarmCartGap);
        bool free = taken.All(land => x + width <= land.Left - FarmCartGap + 0.5 || x >= land.Right + FarmCartGap - 0.5);
        double to = free ? x : farmCartPressX;
        (farmCartSide, farmCartRoom) = FarmLands.RollWay(to, width, taken, area.Left, max, FarmCartGap, FarmCartRoll);
        farmCartChosenX = to;
        farmCartChosenMonitor = monitor.Name;
        // Over land: it rolls there instead of jumping.
        if (Math.Abs(to - farmCartX) > 0.5) { farmCartGlideFrom = farmCartX; farmCartGlideTo = to; farmCartGlideAge = 0; }
        else { farmCartX = to; PlaceFarmCart(); }
    }

    // Each unpaused frame of a roll to the gap.
    private void StepFarmCartGlide(double dt)
    {
        if (farmCartGlideTo is not double to || farmCartWindow == null) return;
        farmCartGlideAge = Math.Min(1, farmCartGlideAge + dt / FarmCartGlideSeconds);
        double t = farmCartGlideAge, eased = t * t * (3 - 2 * t), was = farmCartX;
        farmCartX = farmCartGlideFrom + (to - farmCartGlideFrom) * eased;
        RollFarmCartWheels(farmCartX - was);
        PlaceFarmCart();
        if (farmCartGlideAge >= 1) { farmCartX = to; farmCartGlideTo = null; }
    }

    // Wheels turn by the distance the cart went, either way.
    private void RollFarmCartWheels(double distance)
    {
        if (distance == 0) return;
        farmCartWheel += distance;
        ShowFarmCartWheels();
    }

    private void ShowFarmCartWheels()
    {
        if (farmCartArt == null) return;
        farmCartArt.Rolled = farmCartWheel + farmCartRolled * farmCartRoom / FarmStall.RollSeconds;
        farmCartArt.InvalidateVisual();
    }

    // "Send it off" in the panel: the visit ends now, with whatever was still offered; nothing is lost.
    public bool FarmSendStallAway()
    {
        if (paused || !farmStall.Leave()) return false;
        if (farmStall.Due < FarmStall.MinSeconds) farmStall.Schedule(FarmStall.NextDelay(featureRandom));
        CloseFarmStall();
        PlayPetSound("swoosh");
        return true;
    }

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
        var (leave, leaveText, _) = HudButton(() => FarmSendStallAway(), plain: true);
        leave.Margin = new Thickness(0);
        leaveText.Text = L("Cho xe đi", "Send it off");
        leave.ToolTip = L("Xe rời đi ngay, lời mua còn lại bỏ qua", "The stall leaves now; offers left are dropped");
        farmStallLeave = leave;

        var window = CreateGamePanel(body, PanelActions(new UIElement[] { leave }, new UIElement[] { close }), "Pet farm stall", scene: PetSceneKind.Home);
        window.Closed += (_, _) => { if (farmStallWindow == window) { farmStallWindow = null; farmStallNote = farmStallDecorTitle = null; farmStallLeave = null; farmStallRows.Clear(); farmStallDecorRows.Clear(); } };
        farmStallWindow = window;
        UpdateFarmStall();
        window.Show();
        PlaceGamePanel(window);
    }

    private bool FarmStallReady => farm != null && farmStall.Offers.Any(farm.CanDeliver);

    private string FarmGoodName(string good) =>
        Enum.TryParse<FarmCrop>(good, out var crop) ? FarmCropName(crop) : Enum.TryParse<FarmFish>(good, out var fish) ? FarmFishName(fish) : good == PetFarm.EggGood ? L("Trứng", "Eggs") : good;

    private void UpdateFarmStall()
    {
        if (farmCartArt != null && farmCartArt.Ready != FarmStallReady)
        {
            farmCartArt.Ready = FarmStallReady;
            farmCartArt.InvalidateVisual();
        }
        if (farmStallWindow == null || farm == null || farmStallNote == null) return;
        farmStallPaused = paused;
        if (farmStallLeave != null)
        {
            farmStallLeave.Visibility = farmStall.Here ? Visibility.Visible : Visibility.Collapsed;
            ShowFarmShopButton(farmStallLeave, !paused && farmStall.Here);
        }
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
        FarmDecor.Pinwheel => L("Chong chóng", "Pinwheel"),
        FarmDecor.StoneFrog => L("Ếch đá", "Stone frog"),
        FarmDecor.TeaPot => L("Ấm trà", "Tea pot"),
        _ => L("Mái che", "Hearth roof")
    };

    private string FarmDecorSpot(FarmDecor decor) => decor switch
    {
        FarmDecor.WaterLily or FarmDecor.WoodBridge or FarmDecor.StoneFrog => L("Bày ở hồ", "Goes on the pond"),
        FarmDecor.Awning or FarmDecor.TeaPot => L("Bày ở bếp", "Goes on the hearth"),
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
