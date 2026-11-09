using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// The farm sign (docs/FARM-GAME.md, Biển nông trại): land can be folded away so the farm takes little
// of the screen. A folded land keeps its place and its windows, only with nothing to see or click,
// and its clock goes on. The cat does no work of its own there; what the user asks for opens the
// land for that job and folds it again. Clicking the sign never touches the cat.
public sealed partial class PetWindow
{
    private static readonly FarmLandSlot[] FarmLandSlots = Enum.GetValues<FarmLandSlot>();

    private readonly FarmLandFold farmFieldFold = new();
    private Border? farmDockChip;
    private TextBlock? farmDockChipText;
    private int farmDockPress = -1, farmDockHover = -1;

    public FarmPatch FarmDockPatch => farmPatches[3];

    private FarmPatch FarmPatchOf(FarmLandSlot slot) => slot == FarmLandSlot.Hearth ? FarmHearth : slot == FarmLandSlot.Pond ? FarmPond : FarmCoop;
    public FarmLandFold FarmFoldOf(FarmLandSlot slot) => slot == FarmLandSlot.Field ? farmFieldFold : FarmPatchOf(slot).Fold;
    private FrameworkElement? FarmSurfaceOf(FarmLandSlot slot) => slot == FarmLandSlot.Field ? farmSurface : FarmPatchOf(slot).Surface;
    private bool FarmLandOwned(FarmLandSlot slot) => slot != FarmLandSlot.Coop || farm?.HasCoop == true;
    private IEnumerable<FarmLandSlot> FarmOwnedLands => FarmLandSlots.Where(FarmLandOwned);

    // Everything is folded: the cart and the neighbour's cat wait on the sign instead of on the floor.
    public bool FarmDockCompact => farm != null && FarmOwnedLands.All(slot => FarmFoldOf(slot).Folded);

    private void AddFarmDockChip(Grid surface)
    {
        var text = new TextBlock { Foreground = Brushes.White, FontFamily = PetUi.TextFont, FontSize = PetUi.SmallSize, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = FarmDockPatch.ArtWidth - 32 };
        var chip = FarmLabel(text);
        chip.Margin = new Thickness(0, 22, 0, 0);
        chip.IsHitTestVisible = false;
        surface.Children.Add(chip);
        farmDockChip = chip;
        farmDockChipText = text;
    }

    // Each land as it was left. Before anything looks for free ground.
    private void RestoreFarmFolds()
    {
        if (farm == null) return;
        foreach (var slot in FarmLandSlots)
        {
            FarmFoldOf(slot).Restore(farm.IsFolded(slot));
            ApplyFarmFold(slot);
        }
    }

    private void ApplyFarmFold(FarmLandSlot slot)
    {
        if (FarmSurfaceOf(slot) is not { } surface) return;
        double amount = FarmFoldOf(slot).Amount;
        surface.Visibility = amount > 0 ? Visibility.Visible : Visibility.Collapsed;
        surface.Opacity = amount;
        surface.RenderTransformOrigin = new Point(0.5, 1);
        surface.RenderTransform = amount >= 1 ? Transform.Identity : new ScaleTransform(0.85 + 0.15 * amount, amount);
    }

    private bool FarmLandWorking(FarmLandSlot slot) => farmChores.Active && slot == (farmChores.Cooking ? FarmLandSlot.Hearth
        : farmChores.Kind == FarmChoreKind.Fishing ? FarmLandSlot.Pond : farmChores.Kind == FarmChoreKind.Hen ? FarmLandSlot.Coop : FarmLandSlot.Field);

    private void StepFarmDock(double dt)
    {
        foreach (var slot in FarmLandSlots)
        {
            var fold = FarmFoldOf(slot);
            if (fold.WatchBorrow(FarmLandWorking(slot), dt)) RefreshFarmDock();
            if (fold.Step(dt)) ApplyFarmFold(slot);
        }
    }

    public bool FarmToggleLand(FarmLandSlot slot)
    {
        if (farm == null || paused || FarmLandInAir || !FarmLandOwned(slot)) return false;
        SetFarmLandOpen(slot, !FarmFoldOf(slot).Showing);
        FarmDockChanged();
        return true;
    }

    public bool FarmSetAllLands(bool open)
    {
        if (farm == null || paused || FarmLandInAir) return false;
        var changing = FarmOwnedLands.Where(slot => FarmFoldOf(slot).Showing != open || FarmFoldOf(slot).Borrowed).ToList();
        if (changing.Count == 0) return false;
        foreach (var slot in changing) SetFarmLandOpen(slot, open);
        FarmDockChanged();
        return true;
    }

    private void SetFarmLandOpen(FarmLandSlot slot, bool open)
    {
        var fold = FarmFoldOf(slot);
        farm!.SetFolded(slot, !open);
        if (open)
        {
            fold.Open();
            MakeRoomForFarmLand(slot);
            return;
        }
        fold.Fold();
        if (slot == FarmLandSlot.Field)
        {
            farmArt?.ReleaseMouseCapture();
            HoverFarm(-1);
            CloseFarmSeedMenu();
            farmBird.Scare(); // The ground is about to go.
        }
        else FarmPatchOf(slot).Art?.ReleaseMouseCapture();
    }

    private void FarmDockChanged()
    {
        PlayPetSound("pop");
        SaveFarm();
        SyncFarmGuests();
        RefreshFarmDock();
    }

    // Open for the job the user asked for; it folds again when the cat is done.
    private void BorrowFarmLand(FarmLandSlot slot)
    {
        if (!FarmFoldOf(slot).Borrow()) return;
        MakeRoomForFarmLand(slot);
        SaveFarm();
        RefreshFarmDock();
    }

    // A folded land keeps no ground: what stands there now stays, and the land takes the nearest free place.
    private void MakeRoomForFarmLand(FarmLandSlot slot)
    {
        if (farm == null) return;
        if (slot == FarmLandSlot.Field)
        {
            double free = FarmFreeWidth;
            double spot = FarmLands.FreeSpot(FarmPlotLeft, PetFarmArt.PlotWidth, FarmLandsTaken(farmMonitor, null, withField: false), farmArea.Left, farmArea.Left + free, FarmLandGap);
            farmFraction = farmRestFraction = free > 0 ? Math.Clamp((spot - farmArea.Left) / free, 0, 1) : 0;
            farm.FractionX = farmFraction;
            farm.Monitor = farmMonitor;
            return;
        }
        var patch = FarmPatchOf(slot);
        double room = FarmPatchFree(patch);
        double place = FarmLands.FreeSpot(FarmPatchLeft(patch), patch.Width, FarmLandsTaken(patch.Monitor, patch), patch.Area.Left, patch.Area.Left + room, FarmLandGap);
        patch.Fraction = patch.RestFraction = room > 0 ? Math.Clamp((place - patch.Area.Left) / room, 0, 1) : 0;
        StoreFarmPatch(patch);
    }

    // Guests stay on the sign while everything is folded and stand on the floor otherwise.
    private void SyncFarmGuests()
    {
        if (farm == null || FarmLandInAir) return;
        bool compact = FarmDockCompact;
        if (compact)
        {
            CloseFarmCart();
            CloseFarmVisitor();
            return;
        }
        if (farmCartWindow == null && farmStall.Phase == FarmStallPhase.Open && PickFarmCartPlace(FindFarmMonitor(farmMonitor))) ShowFarmCart();
        if (farmVisitorWindow == null && farmVisitor.Phase == FarmVisitorPhase.Open && PickFarmVisitorPlace(FindFarmMonitor(farmMonitor))) ShowFarmVisitor();
    }

    private void RefreshFarmDock()
    {
        if (farm == null || FarmDockPatch.Art is not { } art) return;
        bool changed = false;
        foreach (var slot in FarmLandSlots)
        {
            bool owned = FarmLandOwned(slot);
            var view = new FarmDockSlotView(owned && FarmFoldOf(slot).Showing, owned ? farm.LandWork(slot) : 0, !owned);
            if (art.DockSlots[(int)slot] == view) continue;
            art.DockSlots[(int)slot] = view;
            changed = true;
        }
        bool cart = farmStall.Here && farmCartWindow == null, visitor = farmVisitor.Here && farmVisitorWindow == null;
        if (art.DockCart != cart || art.DockVisitor != visitor || art.DockHover != farmDockHover) changed = true;
        art.DockCart = cart;
        art.DockVisitor = visitor;
        art.DockHover = farmDockHover;
        if (changed) art.InvalidateVisual();
        UpdateFarmDockChip();
    }

    private void UpdateFarmDockChip()
    {
        if (farmDockChip == null || farmDockChipText == null) return;
        string? text = paused || FarmDockPatch.GainLeft > 0 ? null : FarmDockLabel(farmDockHover);
        farmDockChip.Opacity = text == null ? 0 : 1;
        if (text != null && farmDockChipText.Text != text) farmDockChipText.Text = text;
    }

    public string? FarmDockLabel(int target)
    {
        if (farm == null) return null;
        if (target == PetFarmArt.Land.DockCartTarget) return L("Xe hàng · bấm để xem hàng", "Stall · click to browse");
        if (target == PetFarmArt.Land.DockVisitorTarget) return L("Mèo hàng xóm cần: ", "The neighbour's cat wants: ") + FarmVisitorWants();
        if (target < 0 || target >= FarmLandSlots.Length) return null;
        var slot = (FarmLandSlot)target;
        int work = FarmLandOwned(slot) ? farm.LandWork(slot) : 0;
        return slot switch
        {
            FarmLandSlot.Field => work > 0 ? LF($"Ruộng · {work} ô cần chăm", $"Field · {work} tiles need you") : L("Ruộng", "Field"),
            FarmLandSlot.Hearth => work > 0 ? L("Bếp · còn món trong nồi", "Hearth · a dish in the pot") : L("Bếp", "Hearth"),
            FarmLandSlot.Pond => L("Hồ câu", "Pond"),
            _ => !FarmLandOwned(slot) ? L("Chuồng gà · mua ở Kho", "Coop · buy it in the Store") : work > 0 ? LF($"Chuồng gà · {work} trứng", $"Coop · {work} eggs") : L("Chuồng gà", "Coop")
        };
    }

    private string FarmVisitorWants() => farm == null ? "" : string.Join(" · ", farmVisitor.Items.Select(item => $"{FarmGoodName(item.Key)} {farm.GoodsOf(item.Key)}/{item.Value}"));

    private void HoverFarmDock(int target)
    {
        if (paused || !FarmDockPatch.OnGround) target = -1;
        if (target == farmDockHover) return;
        farmDockHover = target;
        UpdateFarmPatchCursor(FarmDockPatch);
        RefreshFarmDock();
    }

    private void PressFarmDock(Point point)
    {
        farmDockPress = !paused && FarmDockPatch.OnGround && FarmDockPatch.Art is { } art ? art.DockTargetAt(point) : -1;
    }

    private void ReleaseFarmDock(Point point)
    {
        int pressed = farmDockPress;
        farmDockPress = -1;
        if (pressed >= 0 && FarmDockPatch.Art?.DockTargetAt(point) == pressed) FarmDockClick(pressed);
    }

    // What a click on the sign does: a board opens or folds its land, a guest is received.
    public void FarmDockClick(int target)
    {
        if (farm == null || paused) return;
        if (target == PetFarmArt.Land.DockCartTarget)
        {
            OpenFarmStall();
            return;
        }
        if (target == PetFarmArt.Land.DockVisitorTarget)
        {
            if (FarmGiveVisitor() == null) ShowFarmPatchLabel(FarmDockPatch, L("Chưa đủ đồ để tặng", "Not enough to give yet"), FarmGainSeconds);
            RefreshFarmDock();
            return;
        }
        if (target < 0 || target >= FarmLandSlots.Length) return;
        var slot = (FarmLandSlot)target;
        if (!FarmLandOwned(slot)) ShowFarmPatchLabel(FarmDockPatch, L("Mua chuồng gà ở Kho", "Buy the coop in the Store"), FarmGainSeconds);
        else FarmToggleLand(slot);
    }

    // A dish left in a folded hearth: the cat goes back to it only when asked.
    public bool FarmCanResumePot => !paused && farm?.PotDish != null && !farmChores.Cooking && FarmAtHand && FarmHearthUsable && !FarmHearth.Fold.Showing && FarmCatCanEat;

    public bool FarmResumePot()
    {
        if (!FarmCanResumePot) return false;
        BorrowFarmLand(FarmLandSlot.Hearth);
        CancelFeatureActivity();
        farmChores.BeginCook(FarmChoreKind.Stir, farm!.PotDish!.Value, FarmSpotBeside(FarmHearthMid), byUser: true);
        return true;
    }

    private MenuItem[] FarmDockItems(MenuItem move)
    {
        var openAll = new MenuItem { Header = L("Mở tất cả vùng đất", "Open all land"), IsEnabled = !FarmLandInAir && FarmOwnedLands.Any(slot => !FarmFoldOf(slot).Showing) };
        openAll.Click += (_, _) => FarmSetAllLands(true);
        var foldAll = new MenuItem { Header = L("Gập tất cả vùng đất", "Fold all land"), IsEnabled = !FarmLandInAir && FarmOwnedLands.Any(slot => FarmFoldOf(slot).Showing) };
        foldAll.Click += (_, _) => FarmSetAllLands(false);
        var items = new List<MenuItem> { openAll, foldAll };
        if (FarmShopAllowed)
        {
            var shop = new MenuItem { Header = L("Kho & cửa hàng", "Store & shop") };
            shop.Click += (_, _) => OpenFarmShop();
            var kitchen = new MenuItem { Header = L("Nấu ăn", "Cook") };
            kitchen.Click += (_, _) => OpenFarmKitchen();
            items.Add(shop);
            items.Add(kitchen);
            if (farm?.PotDish != null && !FarmHearth.Fold.Showing)
            {
                var resume = new MenuItem { Header = L("Nấu tiếp món trong nồi", "Finish the dish in the pot"), IsEnabled = FarmCanResumePot };
                resume.Click += (_, _) => FarmResumePot();
                items.Add(resume);
            }
            var stall = new MenuItem { Header = L("Gian hàng", "Stall"), IsEnabled = FarmStallHere };
            stall.Click += (_, _) => OpenFarmStall();
            var fish = new MenuItem { Header = L("Câu cá", "Go fishing"), IsEnabled = FarmCanFish };
            fish.Click += (_, _) => FarmGoFish();
            items.Add(stall);
            items.Add(fish);
            if (farm?.HasCoop == true)
            {
                int eggs = farm.EggsInCoop;
                var collect = new MenuItem { Header = LF($"Thu trứng ({eggs})", $"Collect eggs ({eggs})"), IsEnabled = eggs > 0 };
                collect.Click += (_, _) => FarmCollectEggs();
                items.Add(collect);
            }
            items.AddRange(FarmBulkItems());
        }
        items.Add(move);
        return items.ToArray();
    }

    private void ResetFarmDock()
    {
        farmDockPress = farmDockHover = -1;
        if (FarmDockPatch.Art is { } art) art.DockHover = -1;
        if (farmDockChip != null) farmDockChip.Opacity = 0;
    }
}
