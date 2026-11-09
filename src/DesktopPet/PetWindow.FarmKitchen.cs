using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DesktopPet;

// The farm's kitchen (1.5.1 M6): a dish cooked from the store is eaten at once, by way of the usual
// treat, so everything that stops a treat stops the cooking, before anything is used up.
public sealed partial class PetWindow
{
    private const double FarmDishHappySeconds = 6;

    private Window? farmKitchenWindow;
    private TextBlock? farmKitchenNote;
    private readonly List<(FarmDish Dish, TextBlock Parts, Border Button)> farmKitchenRows = new();
    private bool farmKitchenReady, farmKitchenCooking;

    public bool FarmKitchenOpen => farmKitchenWindow != null;
    private bool FarmCatCanEat => MayAct(AutoKind.Treat) && treatCooldown <= 0;
    // One dish at a time: on its way to the pot, or in it.
    private bool FarmPotBusy => farmChores.Cooking || farm?.PotDish != null;
    private bool FarmKitchenCanCook => !paused && !FarmPotBusy && FarmHearth.OnGround && FarmCatCanEat;

    private string FarmDishName(FarmDish dish) => dish switch
    {
        FarmDish.RadishSoup => L("Súp củ cải", "Radish soup"),
        FarmDish.CarrotStew => L("Cà rốt hầm", "Carrot stew"),
        FarmDish.StuffedTomato => L("Cà chua nhồi", "Stuffed tomato"),
        FarmDish.GrilledFish => L("Cá nướng", "Grilled fish"),
        FarmDish.CarpTomato => L("Cá chép kho cà chua", "Carp with tomato"),
        FarmDish.GoldfishHotpot => L("Lẩu cá vàng", "Golden fish hotpot"),
        FarmDish.ChilledWatermelon => L("Dưa hấu ướp lạnh", "Chilled watermelon"),
        FarmDish.FriedEgg => L("Trứng ốp la", "Fried egg"),
        FarmDish.EggTart => L("Bánh bí trứng", "Pumpkin egg tart"),
        _ => L("Bánh bí ngô", "Pumpkin pie")
    };

    private string FarmFishName(FarmFish fish) => fish switch
    {
        FarmFish.Minnow => L("Cá nhỏ", "Small fish"),
        FarmFish.Carp => L("Cá chép", "Carp"),
        _ => L("Cá vàng quý", "Golden fish")
    };

    private string FarmDishParts(FarmDish dish)
    {
        var parts = PetFarm.Recipe(dish).Select(part => $"{FarmCropName(part.Crop)} {farm!.HaveOf(part.Crop)}/{part.Count}").ToList();
        if (PetFarm.FishIn(dish) is FarmFish fish) parts.Insert(0, $"{FarmFishName(fish)} {farm!.FishOf(fish)}/1");
        if (PetFarm.EggsIn(dish) is int eggs and > 0) parts.Insert(0, $"{L("Trứng", "Eggs")} {farm!.EggStock}/{eggs}");
        return string.Join(" · ", parts);
    }

    // Each frame the field is shown.
    private void WatchFarmKitchen()
    {
        if (farmKitchenWindow == null) return;
        if (!FarmShopAllowed) CloseFarmKitchen();
        else if (farmKitchenReady != FarmKitchenCanCook || farmKitchenCooking != FarmPotBusy) UpdateFarmKitchen();
    }

    public void OpenFarmKitchen()
    {
        if (farm == null || paused || !FarmVisible || !FarmShopAllowed || farmKitchenWindow != null) return;
        CloseFarmShop(); // They open in the same place.
        CloseFarmStall();
        var title = PanelLine(PetUi.DisplaySize, true, FarmShopWidth);
        title.Text = L("Nấu ăn", "Cook");
        farmKitchenNote = PanelLine(PetUi.SmallSize, maxWidth: FarmShopWidth);
        farmKitchenNote.Foreground = PetUi.Game.Soft;
        farmKitchenNote.Margin = new Thickness(0, 2, 0, 4);

        var body = new StackPanel { Width = FarmShopWidth, Children = { PanelTitle(title), farmKitchenNote } };
        farmKitchenRows.Clear();
        foreach (var dish in Enum.GetValues<FarmDish>())
        {
            var parts = PanelLine(PetUi.SmallSize, maxWidth: 170);
            parts.Foreground = PetUi.Game.Soft;
            var chosen = dish;
            var (button, text, _) = HudButton(() => FarmCook(chosen), plain: true);
            text.Text = L("Nấu", "Cook");
            body.Children.Add(FarmShopRow(new FarmDishIcon(dish, 32), FarmDishName(dish), parts, button));
            farmKitchenRows.Add((dish, parts, button));
        }

        var (close, closeText, _) = HudButton(CloseFarmKitchen, plain: true);
        close.Margin = new Thickness(0);
        closeText.Text = L("Đóng", "Close");

        var window = CreateGamePanel(body, PanelActions(Array.Empty<UIElement>(), new UIElement[] { close }), "Pet farm kitchen", scene: PetSceneKind.Home);
        window.Closed += (_, _) => { if (farmKitchenWindow == window) ClearFarmKitchen(); };
        farmKitchenWindow = window;
        UpdateFarmKitchen();
        window.Show();
        PlaceGamePanel(window);
    }

    private void UpdateFarmKitchen()
    {
        if (farmKitchenWindow == null || farm == null || farmKitchenNote == null) return;
        farmKitchenReady = FarmKitchenCanCook;
        farmKitchenCooking = FarmPotBusy;
        farmKitchenNote.Text = farm.PotDish != null && !FarmHearth.Fold.Showing && !farmChores.Cooking
            ? LF($"Còn món trong nồi. Mở Bếp trên Biển nông trại để {preferences.Name} nấu tiếp.", $"A dish is still in the pot. Open the Hearth on the farm sign so {preferences.Name} can finish it.")
            : farmKitchenCooking
            ? LF($"{preferences.Name} đang lo nồi trên bếp, xong món này mới nấu tiếp được.", $"{preferences.Name} has a pot on the hearth; the next dish has to wait for it.")
            : farmKitchenReady
            ? LF($"Nấu xong là {preferences.Name} được ăn ngay.", $"{preferences.Name} eats it as soon as it is cooked.")
            : LF($"{preferences.Name} chưa ăn được lúc này, chờ một lát nhé.", $"{preferences.Name} can't eat just now, wait a moment.");
        foreach (var (dish, parts, button) in farmKitchenRows)
        {
            parts.Text = farm.Unlocked(dish)
                ? FarmDishParts(dish)
                : FarmLevelText(PetFarm.DishLevel(dish));
            ShowFarmShopButton(button, farmKitchenReady && farm.CanCook(dish));
        }
    }

    public void CloseFarmKitchen() => farmKitchenWindow?.Close();

    private void ClearFarmKitchen()
    {
        farmKitchenWindow = null;
        farmKitchenNote = null;
        farmKitchenRows.Clear();
    }

    // The cat is asked first: when it cannot eat, nothing is used up. It goes to the hearth and the
    // store pays when it pours; from a window or another monitor it eats where it is, at once.
    public bool FarmCook(FarmDish dish)
    {
        if (farm == null || !FarmKitchenCanCook || !farm.CanCook(dish)) return false;
        if (FarmAtHand && FarmHearthUsable)
        {
            BorrowFarmLand(FarmLandSlot.Hearth);
            CancelFeatureActivity();
            farmChores.BeginCook(FarmChoreKind.Pour, dish, FarmSpotBeside(FarmHearthMid), byUser: true);
            UpdateFarmKitchen();
            return true;
        }
        if (!GiveTreat(dish)) return false;
        farm.Cook(dish);
        PlayPetSound("pop");
        FarmChanged();
        return true;
    }
}
