using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopPet;

// The neighbour's cat (docs/FARM-GAME.md, stage 4 milestone 6). Comes to a free place like the
// stall does, asks for something, and leaves a small gift for it. Like the stall it never touches
// the cat, so a sleeping cat stays asleep. A visit missed costs nothing.
public sealed partial class PetWindow
{
    private const double FarmVisitorRoll = 120, FarmVisitorGap = 4;

    private readonly FarmVisitor farmVisitor = new();
    private Window? farmVisitorWindow;
    private PetFarmVisitorArt? farmVisitorArt;
    private TextBlock? farmVisitorText;
    private double farmVisitorX, farmVisitorRoom = FarmVisitorRoll, farmVisitorScale = 1, farmVisitorClock, farmVisitorTime;
    private int farmVisitorSide = 1;
    private string? farmVisitorMonitor;
    private Area farmVisitorArea;

    public FarmVisitor FarmVisitorNow => farmVisitor;
    public Window? FarmVisitorWindow => farmVisitorWindow;

    public bool TryFarmVisitor()
    {
        if (farm == null || farmVisitor.Phase != FarmVisitorPhase.Away || !FarmVisible || FarmLandInAir
            || Blocking(AutoBlock.Paused | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Away) != AutoBlock.None
            || farm.NewVisitorRequest(featureRandom) is not { } items
            || !PickFarmVisitorPlace(FindFarmMonitor(farmMonitor))) return false;
        farmVisitor.Arrive(items);
        ShowFarmVisitor();
        PlayPetSound("sparkle");
        return true;
    }

    private void StepFarmVisitor(double dt)
    {
        if (farmVisitor.Phase == FarmVisitorPhase.Away)
        {
            if (farmVisitor.CountDown(dt)) farmVisitor.Schedule(TryFarmVisitor() ? FarmVisitor.NextDelay(featureRandom) : FarmVisitor.RetrySeconds);
            return;
        }
        if (farmVisitor.Step(dt)) { CloseFarmVisitor(); return; }
        farmVisitorClock += dt;
        if (farmVisitorClock >= FarmHenFrameSeconds && farmVisitorArt != null)
        {
            farmVisitorTime += farmVisitorClock;
            farmVisitorClock = 0;
            farmVisitorArt.Time = farmVisitorTime;
            farmVisitorArt.InvalidateVisual();
        }
        UpdateFarmVisitorLabel();
    }

    private void UpdateFarmVisitorLabel()
    {
        if (farmVisitorText == null || farm == null) return;
        if (farmVisitorText.Parent is FrameworkElement label) label.Opacity = farmVisitor.Phase == FarmVisitorPhase.Open ? 1 : 0;
        string wants = string.Join(" · ", farmVisitor.Items.Select(item => $"{FarmGoodName(item.Key)} {farm.GoodsOf(item.Key)}/{item.Value}"));
        if (farmVisitorText.Text != wants) farmVisitorText.Text = wants;
    }

    private void ShowFarmVisitor()
    {
        var art = new PetFarmVisitorArt();
        var text = new TextBlock { Foreground = Brushes.White, FontFamily = PetUi.TextFont, FontSize = PetUi.SmallSize };
        var label = new Border
        {
            Background = PetUi.Game.Panel, BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(PetUi.SmallRadius), Padding = new Thickness(8, 2, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Opacity = 0, Child = text,
            ToolTip = L("Bấm để tặng", "Click to give")
        };
        var surface = new Grid { Width = PetFarmVisitorArt.ArtWidth, Height = PetFarmVisitorArt.ArtHeight, Cursor = Cursors.Hand };
        surface.Children.Add(art);
        surface.Children.Add(label);
        farmVisitorArt = art; farmVisitorText = text; farmVisitorClock = farmVisitorTime = 0;
        farmVisitorWindow = CreateOverlayWindow(surface, "Pet farm visitor");
        farmVisitorWindow.Width = PetFarmVisitorArt.ArtWidth; farmVisitorWindow.Height = PetFarmVisitorArt.ArtHeight;
        farmVisitorWindow.MouseLeftButtonDown += (_, e) => { e.Handled = true; FarmGiveVisitor(); };
        farmVisitorWindow.Closed += (_, _) => { farmVisitorWindow = null; farmVisitorArt = null; farmVisitorText = null; };
        PlaceFarmVisitor();
        farmVisitorWindow.Show();
        PlaceBelowCat(farmVisitorWindow);
        PlaceFarmVisitor();
        UpdateFarmVisitorLabel();
    }

    private bool PickFarmVisitorPlace(FarmMonitor monitor)
    {
        double width = PetFarmVisitorArt.ArtWidth;
        var area = monitor.WorkDips;
        var taken = FarmLandsTaken(monitor.Name, null, withVisitor: false);
        double max = area.Right - width;
        if (FarmLands.RandomFreeSpot(featureRandom, width, taken, area.Left, max, FarmVisitorGap) is not double x) return false;
        (farmVisitorSide, farmVisitorRoom) = FarmLands.RollWay(x, width, taken, area.Left, max, FarmVisitorGap, FarmVisitorRoll);
        farmVisitorX = x;
        StoreFarmVisitorMonitor(monitor);
        return true;
    }

    private void StoreFarmVisitorMonitor(FarmMonitor monitor)
    {
        farmVisitorMonitor = monitor.Name;
        farmVisitorArea = monitor.WorkDips;
        farmVisitorScale = monitor.Scale;
    }

    // Once a second, as for the stall.
    private void RefreshFarmVisitor()
    {
        if (farmVisitorWindow == null || FarmLandInAir) return;
        var monitor = FindFarmMonitor(farmVisitorMonitor);
        var area = monitor.WorkDips;
        double right = farmVisitorX + PetFarmVisitorArt.ArtWidth;
        bool clear = farmVisitorX >= area.Left && right <= area.Right
            && FarmLandsTaken(monitor.Name, null, withVisitor: false).All(land => right <= land.Left || farmVisitorX >= land.Right);
        if (clear) StoreFarmVisitorMonitor(monitor);
        else if (!PickFarmVisitorPlace(monitor)) ResetFarmVisitor();
    }

    private void PlaceFarmVisitor()
    {
        if (farmVisitorWindow == null) return;
        double shown = Math.Clamp(farmVisitor.Amount, 0, 1);
        farmVisitorWindow.Opacity = shown;
        MoveWindow(farmVisitorWindow, farmVisitorX + farmVisitorSide * (1 - shown) * farmVisitorRoom, farmVisitorArea.Bottom + PetFarmArt.BottomRoom - PetFarmVisitorArt.ArtHeight, farmVisitorScale);
    }

    private void CloseFarmVisitor() => farmVisitorWindow?.Close();

    private void ResetFarmVisitor()
    {
        farmVisitor.Reset();
        CloseFarmVisitor();
    }

    // What the click does: hands it over, and the visitor goes off happy. Null when the store is short.
    public FarmGift? FarmGiveVisitor()
    {
        if (farm == null || paused || !farmVisitor.Here || !FarmShopAllowed) return null;
        if (farm.GiveVisitor(farmVisitor.Items, featureRandom) is not { } gift) return null;
        farmVisitor.Leave();
        ShowFarmNotice(FarmGiftText(gift));
        PlayPetSound("sparkle");
        FarmChanged();
        return gift;
    }

    private string FarmGiftText(FarmGift gift) => gift.Kind switch
    {
        FarmGiftKind.Coins => LF($"Mèo hàng xóm tặng {gift.Amount} xu", $"The neighbour's cat left {gift.Amount} coins"),
        FarmGiftKind.Fertilizer => LF($"Mèo hàng xóm tặng {gift.Amount} túi phân", $"The neighbour's cat left {gift.Amount} bags of fertilizer"),
        FarmGiftKind.Seed => LF($"Mèo hàng xóm tặng hạt {FarmCropName(gift.Crop!.Value)}", $"The neighbour's cat left a seed: {FarmCropName(gift.Crop!.Value)}"),
        _ => LF($"Mèo hàng xóm tặng {FarmDecorName(gift.Decor!.Value)}", $"The neighbour's cat left: {FarmDecorName(gift.Decor!.Value)}")
    };
}
