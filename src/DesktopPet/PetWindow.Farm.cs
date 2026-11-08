using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace DesktopPet;

// The little field on the desktop (1.5.1 M3): six tiles just above the taskbar. A left click does
// the tile's next step (till, sow, water, harvest); a right click offers to move the whole plot,
// which floats up, is dragged along the floor, and is set down from the menu again. Clicking the
// field never touches the cat: it wakes nothing, and sleep, drag and emotions are not involved.
public sealed partial class PetWindow
{
    private const double FarmCheckSeconds = 1, FarmEffectSeconds = 0.55, FarmGainSeconds = 1.3, FarmChipWidth = 200;

    // Replaceable for UI checks, which must not touch the user's field or wait for real minutes.
    private string farmPath = PetFarm.DefaultPath;
    private Func<DateTime>? farmClock = null;
    private PetFarm? farm;
    private readonly FarmLift farmLift = new();
    private Window? farmWindow;
    private PetFarmArt? farmArt;
    private Border? farmChip, farmGain;
    private TextBlock? farmChipText, farmGainText;
    private Grid? farmSurface;
    private ContextMenu? farmSeedMenu;
    public bool FarmSeedMenuOpen => farmSeedMenu?.IsOpen == true;
    private Area farmArea;
    private double farmScale = 1, farmFraction, farmRestFraction, farmFallFrom, farmFallTo, farmGrab, farmGainLeft;
    private string? farmMonitor;
    private DateTime farmChecked;
    private bool farmDragging, farmEffectsRunning, farmWithCat;
    private int farmHover = -1, farmPressTile = -1;
    private string? farmChipShown;

    private DateTime FarmNow => farmClock?.Invoke() ?? DateTime.UtcNow;
    private double FarmFreeWidth => Math.Max(0, farmArea.Right - farmArea.Left - PetFarmArt.PlotWidth);
    private double FarmPlotLeft => farmArea.Left + farmFraction * FarmFreeWidth;

    public bool FarmVisible => farmWindow?.IsVisible == true;
    public PetFarm? Farm => farm;
    public FarmLiftPhase FarmLiftPhaseNow => farmLift.Phase;
    public double FarmLiftHeight => farmLift.Height;
    public double FarmFraction => farmFraction;
    public bool FarmDragging => farmDragging;

    // Every frame; dt is 0 in Pause, which holds the plot's movement still.
    private void TickFarm(double dt)
    {
        if (!preferences.Farm || context.Fullscreen || BattleActive)
        {
            if (FarmVisible) HideFarm();
            return;
        }
        farm ??= PetFarm.Load(farmPath);
        if (farmWindow == null) CreateFarmWindow();
        if (!FarmVisible) ShowFarm();

        var now = FarmNow;
        if (Math.Abs((now - farmChecked).TotalSeconds) >= FarmCheckSeconds)
        {
            farmChecked = now;
            RefreshFarmArea();
            CheckFarmGrowth(now);
            UpdateFarmChip();
        }
        if (dt > 0) StepFarm(dt);
        if (farmDragging) FollowFarmDrag();
        PositionFarm();
    }

    private void CheckFarmGrowth(DateTime now)
    {
        if (farm == null || farmArt == null) return;
        var grown = farm.Advance(now);
        foreach (int index in grown) StartFarmEffect(index, FarmEffect.Grow);
        // The bar of a hovered tile moves on too; other tiles have nothing new to draw.
        if (grown.Count > 0 || farmHover >= 0) RefreshFarmArt();
    }

    private void StepFarm(double dt)
    {
        if (farmArt == null) return;
        bool lifted = farmLift.Busy;
        if (farmLift.Step(dt)) LandFarm();
        if (farmLift.Phase == FarmLiftPhase.Falling) farmFraction = farmFallFrom + (farmFallTo - farmFallFrom) * farmLift.FallAmount;

        bool effects = false;
        if (farmEffectsRunning)
        {
            for (int i = 0; i < farmArt.Effects.Length; i++)
            {
                if (farmArt.Effects[i] == FarmEffect.None) continue;
                farmArt.EffectAge[i] += dt / FarmEffectSeconds;
                if (farmArt.EffectAge[i] >= 1) farmArt.Effects[i] = FarmEffect.None;
                else effects = true;
            }
        }
        bool bird = farmBird.Present;
        StepFarmBird(dt);
        bool redraw = lifted || farmLift.Busy || farmEffectsRunning || bird;
        farmEffectsRunning = effects;
        if (redraw)
            DrawFarmLift();
        if (farmGainLeft > 0 && farmGain != null)
        {
            farmGainLeft = Math.Max(0, farmGainLeft - dt);
            double k = 1 - farmGainLeft / FarmGainSeconds;
            farmGain.Opacity = k < 0.7 ? 1 : (1 - k) / 0.3;
            ((TranslateTransform)farmGain.RenderTransform).Y = -18 * k;
        }
    }

    private void DrawFarmLift()
    {
        if (farmArt == null) return;
        farmArt.Lift = farmLift.Height;
        farmArt.Dustiness = farmLift.Dust;
        farmArt.ShakeX = farmLift.ShakeX;
        farmArt.Tilt = farmLift.Tilt;
        farmArt.Squash = farmLift.Squash;
        farmArt.Shed = farmLift.Shed;
        farmArt.BirdTile = farmBird.Tile;
        farmArt.BirdSide = farmBird.Side;
        farmArt.BirdAir = farmBird.Air;
        farmArt.BirdTime = farmBird.Time;
        farmArt.BirdFlying = !farmBird.Perched;
        farmArt.InvalidateVisual();
    }

    private void CreateFarmWindow()
    {
        var art = new PetFarmArt { Cursor = Cursors.Hand };
        var chipText = new TextBlock { Foreground = Brushes.White, FontFamily = PetUi.TextFont, FontSize = PetUi.SmallSize };
        var chip = FarmLabel(chipText);
        var gainText = new TextBlock { Foreground = PetUi.Game.Gold, FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold };
        var gain = FarmLabel(gainText);
        gain.RenderTransform = new TranslateTransform();
        var surface = new Grid { Width = PetFarmArt.ArtWidth, Height = PetFarmArt.ArtHeight };
        surface.Children.Add(art);
        var chipRow = new Grid { Width = FarmChipWidth, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        chipRow.Children.Add(chip);
        var gainRow = new Grid { Width = FarmChipWidth, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        gainRow.Children.Add(gain);
        surface.Children.Add(chipRow);
        surface.Children.Add(gainRow);
        farmSurface = surface;

        farmArt = art; farmChip = chip; farmChipText = chipText; farmGain = gain; farmGainText = gainText;
        farmWindow = CreateOverlayWindow(surface, "Pet farm");
        farmWindow.Width = PetFarmArt.ArtWidth; farmWindow.Height = PetFarmArt.ArtHeight;
        art.MouseMove += (_, e) => HoverFarm(PetFarmArt.TileAt(e.GetPosition(art).X));
        art.MouseLeave += (_, _) => HoverFarm(-1);
        art.MouseLeftButtonDown += (_, e) => { e.Handled = true; PressFarm(PetFarmArt.TileAt(e.GetPosition(art).X)); };
        art.MouseLeftButtonUp += (_, e) => { e.Handled = true; ReleaseFarm(PetFarmArt.TileAt(e.GetPosition(art).X)); };
        art.LostMouseCapture += (_, _) => farmDragging = false;
        InitializeFarmMenu(art);
        farmWindow.Closed += (_, _) =>
        {
            farmWindow = null; farmArt = null; farmSurface = null; farmChip = farmGain = null; farmChipText = farmGainText = null;
            farmDragging = false; farmHover = farmPressTile = -1; farmChipShown = null; farmEffectsRunning = false; farmGainLeft = 0;
        };
    }

    private static Border FarmLabel(TextBlock text) => new()
    {
        Background = PetUi.Game.Panel, BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(PetUi.SmallRadius), Padding = new Thickness(8, 2, 8, 3),
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Opacity = 0, Child = text
    };

    private void ShowFarm()
    {
        if (farm == null || farmWindow == null) return;
        // Where it was left; else a quarter across the cat's monitor.
        farmMonitor = farm.Monitor ?? CurrentMonitor().DeviceName;
        farmFraction = farmRestFraction = farm.FractionX ?? PetFarm.FirstFraction;
        RefreshFarmArea();
        farmChecked = FarmNow;
        farm.Advance(farmChecked);
        RefreshFarmArt();
        farmWindow.Show();
        KeepFarmBelowCat();
        PositionFarm();
    }

    // A fullscreen app or a battle takes the screen: a plot in the air goes back where it was.
    private void HideFarm()
    {
        CloseFarmSeedMenu();
        farmArt?.ReleaseMouseCapture();
        farmDragging = false;
        if (farmLift.Phase != FarmLiftPhase.Down) farmFraction = farmRestFraction;
        farmLift.Reset();
        farmBird.Reset();
        DrawFarmLift();
        UpdateFarmCursor();
        HoverFarm(-1);
        farmWindow?.Hide();
    }

    private void CloseFarm() => farmWindow?.Close();

    // The plot's monitor in its own DIPs (unplugged: the primary one, at the same place across it).
    private void RefreshFarmArea()
    {
        var catScreen = CurrentMonitor();
        var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == farmMonitor) ?? Forms.Screen.PrimaryScreen ?? catScreen;
        farmMonitor = screen.DeviceName;
        farmWithCat = screen.DeviceName == catScreen.DeviceName;
        farmScale = screen.DeviceName == catScreen.DeviceName ? scale : MonitorScale(screen);
        var work = screen.WorkingArea;
        farmArea = new Area(work.Left / farmScale, work.Top / farmScale, work.Right / farmScale, work.Bottom / farmScale);
    }

    private void PositionFarm()
    {
        if (farmWindow == null) return;
        MoveWindow(farmWindow, FarmPlotLeft - PetFarmArt.Side, farmArea.Bottom + PetFarmArt.BottomRoom - PetFarmArt.ArtHeight, farmScale);
    }

    // Both are topmost and the field was shown later: the cat walks in front of it, keepsake too.
    private void KeepFarmBelowCat()
    {
        if (farmWindow == null) return;
        var handle = new WindowInteropHelper(farmWindow).Handle;
        var above = new WindowInteropHelper(keepsakeWindow?.IsVisible == true ? keepsakeWindow : this).Handle;
        const uint NoSize = 0x0001, NoMove = 0x0002, NoActivate = 0x0010;
        if (handle != IntPtr.Zero && above != IntPtr.Zero) SetWindowPos(handle, above, 0, 0, 0, 0, NoSize | NoMove | NoActivate);
    }

    private void RefreshFarmArt()
    {
        if (farm == null || farmArt == null) return;
        var now = FarmNow;
        for (int i = 0; i < PetFarm.TileCount; i++)
        {
            var tile = farm.Tiles[i];
            farmArt.Tiles[i] = new FarmTileView(tile.Stage, tile.Crop, tile.GrowsUntil != null, farm.Next(i) == FarmStep.Water, farm.Progress(i, now));
        }
        farmArt.HoverTile = farmHover;
        farmArt.Lift = farmLift.Height;
        farmArt.InvalidateVisual();
    }

    private void StartFarmEffect(int index, FarmEffect effect)
    {
        if (farmArt == null) return;
        farmArt.Effects[index] = effect;
        farmArt.EffectAge[index] = 0;
        farmEffectsRunning = true;
    }

    private void SaveFarm() => farm?.Save(farmPath);

    private string FarmCropName(FarmCrop crop) => crop switch
    {
        FarmCrop.Radish => L("Củ cải", "Radish"),
        FarmCrop.Carrot => L("Cà rốt", "Carrot"),
        FarmCrop.Tomato => L("Cà chua", "Tomato"),
        _ => L("Bí ngô", "Pumpkin")
    };

    private string FarmSeedLabel(FarmCrop crop) => crop switch
    {
        FarmCrop.Radish => L("Củ cải · 5 phút", "Radish · 5 min"),
        FarmCrop.Carrot => L("Cà rốt · 15 phút", "Carrot · 15 min"),
        FarmCrop.Tomato => L("Cà chua · 40 phút", "Tomato · 40 min"),
        _ => L("Bí ngô · 2 giờ", "Pumpkin · 2 hours")
    };

    private static string FarmClock(TimeSpan left)
    {
        int seconds = (int)Math.Ceiling(left.TotalSeconds);
        return seconds >= 3600 ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}" : $"{seconds / 60}:{seconds % 60:00}";
    }

    // What a hovered tile says, null when it has nothing to say.
    public string? FarmTileLabel(int index)
    {
        if (farm == null || index < 0 || index >= PetFarm.TileCount) return null;
        string name = farm.Tiles[index].Crop is FarmCrop crop ? FarmCropName(crop) : "";
        return farm.Next(index) switch
        {
            FarmStep.Till => L("Cày đất", "Till the soil"),
            FarmStep.Plant => L("Gieo hạt", "Sow a seed"),
            FarmStep.Water => name + " · " + L("cần tưới", "needs water"),
            FarmStep.Harvest => name + " · " + L("thu hoạch", "harvest"),
            _ => name + " · " + FarmClock(farm.Remaining(index, FarmNow))
        };
    }

    private void HoverFarm(int index)
    {
        if (farmLift.Phase != FarmLiftPhase.Down) index = -1;
        if (index != farmHover)
        {
            farmHover = index;
            farmChipShown = null; // Two tiles can say the same words: the label still has to move.
            if (farmArt != null) { farmArt.HoverTile = index; RefreshFarmArt(); }
        }
        UpdateFarmChip();
    }

    private void UpdateFarmChip()
    {
        if (farmChip == null || farmChipText == null || farmArt == null || farmChip.Parent is not Grid row) return;
        string? text = paused ? null : FarmTileLabel(farmHover);
        if (text == farmChipShown) return;
        farmChipShown = text;
        if (text == null) { farmChip.Opacity = 0; return; }
        farmChipText.Text = text;
        farmChip.Opacity = 1;
        PlaceFarmLabel(row, farmHover, 26);
    }

    // Above what stands on the tile, kept inside the window at the two ends of the plot.
    private void PlaceFarmLabel(Grid row, int index, double rise)
    {
        if (farmArt == null) return;
        const double overhang = 10; // The window is wider than the plot for flying dirt: the label stays near the plot.
        double inset = PetFarmArt.Side - overhang;
        double left = Math.Clamp(PetFarmArt.TileCenter(index) - FarmChipWidth / 2, inset, PetFarmArt.ArtWidth - FarmChipWidth - inset);
        row.Margin = new Thickness(left, Math.Max(0, farmArt.TopOfTile(index) - rise), 0, 0);
    }

    private void PressFarm(int tile)
    {
        if (paused || farmArt == null) return;
        if (farmLift.Phase is FarmLiftPhase.Rising or FarmLiftPhase.Floating)
        {
            if (!farmArt.CaptureMouse()) return;
            farmDragging = true;
            farmGrab = ScreenCursor().X / farmScale - FarmPlotLeft;
            return;
        }
        farmPressTile = farmLift.Phase == FarmLiftPhase.Down ? tile : -1;
    }

    private void ReleaseFarm(int tile)
    {
        if (farmDragging) { farmDragging = false; farmArt?.ReleaseMouseCapture(); return; }
        int pressed = farmPressTile;
        farmPressTile = -1;
        if (tile >= 0 && tile == pressed) FarmClick(tile);
    }

    // Held in the air: the plot follows the cursor along the floor, never up or down, on its own monitor.
    private void FollowFarmDrag()
    {
        double free = FarmFreeWidth;
        double left = ScreenCursor().X / farmScale - farmGrab;
        farmFraction = free > 0 ? Math.Clamp((left - farmArea.Left) / free, 0, 1) : 0;
    }

    // A left click on a tile: its next step. Harvests and the like work while the cat sleeps, without waking it.
    public void FarmClick(int tile)
    {
        if (farm == null || paused || farmLift.Phase != FarmLiftPhase.Down || tile < 0 || tile >= PetFarm.TileCount) return;
        var now = FarmNow;
        foreach (int grown in farm.Advance(now)) StartFarmEffect(grown, FarmEffect.Grow);
        switch (farm.Next(tile))
        {
            case FarmStep.Till:
                farm.Till(tile);
                StartFarmEffect(tile, FarmEffect.Till);
                PlayPetSound("ball_tap");
                break;
            case FarmStep.Plant:
                OpenFarmSeedMenu(tile);
                return;
            case FarmStep.Water:
                farm.Water(tile, now);
                StartFarmEffect(tile, FarmEffect.Water);
                PlayPetSound("swoosh");
                break;
            case FarmStep.Harvest:
                if (farm.Harvest(tile) is FarmCrop crop)
                {
                    StartFarmEffect(tile, FarmEffect.Harvest);
                    PlayPetSound("sparkle");
                    ShowFarmGain(tile, crop);
                }
                break;
            default:
                return;
        }
        SaveFarm();
        RefreshFarmArt();
        farmChipShown = null;
        UpdateFarmChip();
    }

    public bool FarmPlant(int tile, FarmCrop crop)
    {
        if (farm == null || paused || farmLift.Phase != FarmLiftPhase.Down || !farm.Plant(tile, crop)) return false;
        StartFarmEffect(tile, FarmEffect.Plant);
        PlayPetSound("pop");
        SaveFarm();
        RefreshFarmArt();
        farmChipShown = null;
        UpdateFarmChip();
        return true;
    }

    private void ShowFarmGain(int tile, FarmCrop crop)
    {
        if (farmGain == null || farmGainText == null || farmGain.Parent is not Grid row) return;
        farmGainText.Text = "+1 " + FarmCropName(crop);
        farmGain.Opacity = 1;
        ((TranslateTransform)farmGain.RenderTransform).Y = 0;
        PlaceFarmLabel(row, tile, 30);
        farmGainLeft = FarmGainSeconds;
    }

    // The four seeds, opened just above the tile.
    private void OpenFarmSeedMenu(int tile)
    {
        if (farmArt == null) return;
        CloseFarmSeedMenu();
        var menu = new ContextMenu { FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize };
        PetMenuStyle.Apply(menu);
        foreach (var item in FarmSeedItems(tile)) menu.Items.Add(item);
        menu.PlacementTarget = farmArt;
        menu.Placement = PlacementMode.Top;
        menu.PlacementRectangle = new Rect(PetFarmArt.TileLeft(tile), PetFarmArt.SoilTop, PetFarmArt.TileWidth, PetFarmArt.SoilHeight);
        menu.IsOpen = true;
        farmSeedMenu = menu;
    }

    private void CloseFarmSeedMenu()
    {
        if (farmSeedMenu != null) farmSeedMenu.IsOpen = false;
        farmSeedMenu = null;
    }

    public MenuItem[] FarmSeedItems(int tile) => Enum.GetValues<FarmCrop>().Select(crop =>
    {
        var item = new MenuItem { Header = FarmSeedLabel(crop), Icon = new FarmCropIcon(crop) };
        item.Click += (_, _) => FarmPlant(tile, crop);
        return item;
    }).ToArray();

    private void InitializeFarmMenu(PetFarmArt art)
    {
        var menu = new ContextMenu { FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize };
        PetMenuStyle.Apply(menu);
        art.ContextMenu = menu;
        art.ContextMenuOpening += (_, e) =>
        {
            var items = FarmMenuItems();
            if (items.Length == 0) { e.Handled = true; return; }
            menu.Items.Clear();
            foreach (var item in items) menu.Items.Add(item);
        };
    }

    // On the ground: move it. In the air: set it down here, or put it back. Nothing while it is on the way.
    public MenuItem[] FarmMenuItems()
    {
        if (paused || farmDragging || farmLift.Phase is FarmLiftPhase.Rising or FarmLiftPhase.Falling) return Array.Empty<MenuItem>();
        if (farmLift.Phase == FarmLiftPhase.Floating)
        {
            var drop = new MenuItem { Header = L("Thả xuống đây", "Drop it here") };
            drop.Click += (_, _) => DropFarm();
            var cancel = new MenuItem { Header = L("Hủy", "Cancel") };
            cancel.Click += (_, _) => CancelFarmMove();
            return new[] { drop, cancel };
        }
        var move = new MenuItem { Header = L("Di chuyển mảnh đất farm", "Move the farm plot") };
        move.Click += (_, _) => BeginFarmMove();
        return new[] { move };
    }

    public void BeginFarmMove()
    {
        if (paused || !FarmVisible || farmLift.Phase != FarmLiftPhase.Down) return;
        farmRestFraction = farmFraction;
        HoverFarm(-1);
        farmBird.Scare(); // The ground is about to go.
        farmLift.Begin();
        UpdateFarmCursor();
        PlayPetSound("swoosh");
    }

    public void DropFarm() => SetFarmDown(farmFraction, 0);

    // Back to where it was before it rose: it slides there in the air first, then falls.
    public void CancelFarmMove()
    {
        double way = Math.Abs(farmFraction - farmRestFraction) * FarmFreeWidth;
        SetFarmDown(farmRestFraction, FarmLift.MaxGlideSeconds * Math.Min(1, way / 240));
    }

    private void SetFarmDown(double target, double glide)
    {
        if (paused || !farmLift.Floating) return;
        farmArt?.ReleaseMouseCapture();
        farmDragging = false;
        farmFallFrom = farmFraction;
        farmFallTo = target;
        farmLift.Drop(glide);
        UpdateFarmCursor();
    }

    private void LandFarm()
    {
        farmFraction = farmRestFraction = farmFallTo;
        UpdateFarmCursor();
        PlayPetSound("squish");
        if (farm == null) return;
        farm.Monitor = farmMonitor;
        farm.FractionX = farmFraction;
        SaveFarm();
    }

    private void UpdateFarmCursor()
    {
        if (farmArt != null) farmArt.Cursor = farmLift.Phase == FarmLiftPhase.Down ? Cursors.Hand : Cursors.SizeWE;
    }
}
