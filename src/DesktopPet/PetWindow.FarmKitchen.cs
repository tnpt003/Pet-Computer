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
    private bool farmKitchenReady;

    public bool FarmKitchenOpen => farmKitchenWindow != null;
    private bool FarmCatCanEat => MayAct(AutoKind.Treat) && treatCooldown <= 0;

    private string FarmDishName(FarmDish dish) => dish switch
    {
        FarmDish.RadishSoup => L("Súp củ cải", "Radish soup"),
        FarmDish.CarrotStew => L("Cà rốt hầm", "Carrot stew"),
        FarmDish.StuffedTomato => L("Cà chua nhồi", "Stuffed tomato"),
        _ => L("Bánh bí ngô", "Pumpkin pie")
    };

    // Each frame the field is shown.
    private void WatchFarmKitchen()
    {
        if (farmKitchenWindow == null) return;
        if (!FarmShopAllowed) CloseFarmKitchen();
        else if (farmKitchenReady != (!paused && FarmCatCanEat)) UpdateFarmKitchen();
    }

    public void OpenFarmKitchen()
    {
        if (farm == null || paused || !FarmVisible || !FarmShopAllowed || farmKitchenWindow != null) return;
        CloseFarmShop(); // They open in the same place.
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
        farmKitchenReady = !paused && FarmCatCanEat;
        farmKitchenNote.Text = farmKitchenReady
            ? LF($"Nấu xong là {preferences.Name} được ăn ngay.", $"{preferences.Name} eats it as soon as it is cooked.")
            : LF($"{preferences.Name} chưa ăn được lúc này, chờ một lát nhé.", $"{preferences.Name} can't eat just now, wait a moment.");
        foreach (var (dish, parts, button) in farmKitchenRows)
        {
            parts.Text = farm.Unlocked(dish)
                ? string.Join(" · ", PetFarm.Recipe(dish).Select(part => $"{FarmCropName(part.Crop)} {farm.StockOf(part.Crop)}/{part.Count}"))
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

    // The cat is asked first: when it cannot eat, nothing is used up.
    public bool FarmCook(FarmDish dish)
    {
        if (farm == null || paused || !farm.CanCook(dish) || !GiveTreat(dish)) return false;
        farm.Cook(dish);
        PlayPetSound("pop");
        FarmChanged();
        return true;
    }
}
