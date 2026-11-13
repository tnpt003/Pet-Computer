using System;
using System.Linq;
using System.Collections.Generic;
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
    private const double FarmCheckSeconds = 1, FarmEffectSeconds = 0.55, FarmGainSeconds = 1.3, FarmLevelUpSeconds = 3, FarmChipWidth = 200, FarmSunSaveSeconds = 30;

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
    private PetFarmArt.LandMagic? farmMagic;
    private ContextMenu? farmSeedMenu;
    public bool FarmSeedMenuOpen => farmSeedMenu?.IsOpen == true;
    private Area farmArea;
    private double farmScale = 1, farmFraction, farmRestFraction, farmFallFrom, farmFallTo, farmGrab, farmGainLeft;
    private double farmHearthFire, farmHearthTime;
    private double farmPondTime;
    private bool farmPondBusy;
    private double farmSkyElapsed, farmRainSeconds, farmSunUnsaved, farmGainTotal = FarmGainSeconds;
    private string? farmMonitor, farmRestMonitor;
    // Replaceable for UI checks, which have one real monitor.
    private Func<IReadOnlyList<FarmMonitor>>? farmMonitorsProbe = null;
    private DateTime farmChecked;
    private bool farmDragging, farmEffectsRunning, farmWithCat;
    private int farmHover = -1, farmPressTile = -1;
    private string? farmChipShown;

    private DateTime FarmNow => farmClock?.Invoke() ?? DateTime.UtcNow;
    private double FarmFreeWidth => Math.Max(0, farmArea.Right - farmArea.Left - PetFarmArt.PlotWidth);
    private double FarmPlotLeft => farmArea.Left + farmFraction * FarmFreeWidth;

    public double FarmPlotStart => FarmPlotLeft;
    public bool FarmVisible => farmWindow?.IsVisible == true;
    public PetFarm? Farm => farm;
    public FarmLiftPhase FarmLiftPhaseNow => farmLift.Phase;
    public double FarmLiftHeight => farmLift.Height;
    public double FarmFraction => farmFraction;
    // The weather that counts for the field: on show, and over the same monitor as the cat.
    public WeatherKind? FarmWeather => weather.Visible && farmWithCat ? weather.Kind : null;
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
        if (farmAchievementsCounted != farm) { CountFarmAchievements(); SyncFarmSeason(); }
        if (farmWindow == null) CreateFarmWindow();
        if (!FarmVisible) ShowFarm();
        WatchFarmShop();
        WatchFarmKitchen();
        WatchFarmStall();
        if (Environment.TickCount64 >= farmOrderDue)
        {
            farmOrderDue = Environment.TickCount64 + FarmOrderMilliseconds;
            KeepFarmBelowCat();
        }
        if (dt > 0) StepFarmSky(dt);

        var now = FarmNow;
        if (Math.Abs((now - farmChecked).TotalSeconds) >= FarmCheckSeconds)
        {
            farmChecked = now;
            RefreshFarmArea();
            RefreshFarmCart();
            RefreshFarmVisitor();
            SyncFarmSeason();
            CheckFarmGrowth(now);
            UpdateFarmChip();
            UpdateFarmStall();
            RefreshFarmDock();
        }
        if (dt > 0) StepFarm(dt);
        if (dt > 0) StepFarmStall(dt);
        if (dt > 0) StepFarmVisitor(dt);
        if (dt > 0) StepFarmDock(dt);
        UpdateFarmTips();
        if (farmDragging) FollowFarmDrag();
        FollowFarmPatchDrags();
        PositionFarm();
        PlaceFarmCart();
        PlaceFarmVisitor();
    }

    // From this computer's month. Everything drawn with grass changes with it.
    private void SyncFarmSeason()
    {
        if (farm == null) return;
        var season = PetFarm.SeasonOf(FarmNow.ToLocalTime().Month);
        bool changed = farm.Season != season || PetFarmArt.CurrentSeason != season;
        farm.Season = season;
        if (!changed) return;
        PetFarmArt.CurrentSeason = season;
        RefreshFarmArt();
        foreach (var patch in farmPatches) DrawFarmPatch(patch);
        UpdateFarmShop();
    }

    private string FarmSeasonName(FarmSeason season) => season switch
    {
        FarmSeason.Spring => L("Xuân", "Spring"),
        FarmSeason.Summer => L("Hạ", "Summer"),
        FarmSeason.Autumn => L("Thu", "Autumn"),
        _ => L("Đông", "Winter")
    };

    private void CheckFarmGrowth(DateTime now)
    {
        if (farm == null || farmArt == null) return;
        var grown = farm.Advance(now);
        foreach (int index in grown) StartFarmEffect(index, FarmEffect.Grow);
        if (farm.LayEggs(now))
        {
            SaveFarm();
            DrawFarmPatch(FarmCoop);
            UpdateFarmShop();
        }
        // The bar of a hovered tile moves on too; other tiles have nothing new to draw.
        if (grown.Count > 0 || farmHover >= 0) RefreshFarmArt();
    }

    // The weather on show reaches the field (1.5.1 M6). Only over the cat's monitor, whose wallpaper
    // it comes from. It leaves the cat alone, so it makes no sound and asks nothing of the cat.
    private void StepFarmSky(double dt)
    {
        if (farm == null) return;
        farmSkyElapsed += Math.Min(dt, FarmCheckSeconds); // A computer waking up was not a long sunny day.
        if (farmSkyElapsed < FarmCheckSeconds) return;
        double seconds = farmSkyElapsed;
        farmSkyElapsed = 0;

        farmRainSeconds = farmWithCat && weather.Raining ? farmRainSeconds + seconds : 0;
        if (farmRainSeconds >= PetFarm.RainSoakSeconds && farmLift.Phase == FarmLiftPhase.Down)
        {
            var watered = farm.RainWater(FarmNow);
            foreach (int tile in watered) StartFarmEffect(tile, FarmEffect.Water);
            if (watered.Count > 0) FarmChanged();
        }
        if (farmWithCat && weather.Shining && farm.Sunshine(seconds) && (farmSunUnsaved += seconds) >= FarmSunSaveSeconds) SaveSunnyFarm();
    }

    private void SaveSunnyFarm()
    {
        farmSunUnsaved = 0;
        SaveFarm();
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
        bool bird = farmBird.Present, basket = farmBasketAge >= 0;
        if (basket)
        {
            farmBasketAge += dt / FarmBasketBounceSeconds;
            if (farmBasketAge >= 1) farmBasketAge = -1;
            farmArt.BasketBounce = farmBasketAge;
        }
        StepFarmBird(dt);
        StepFarmPatches(dt);
        StepFarmPinwheel(dt);
        bool redraw = lifted || farmLift.Busy || farmEffectsRunning || bird || basket;
        farmEffectsRunning = effects;
        if (redraw)
            DrawFarmLift();
        if (farmGainLeft > 0 && farmGain != null)
        {
            farmGainLeft = Math.Max(0, farmGainLeft - dt);
            double k = 1 - farmGainLeft / farmGainTotal;
            farmGain.Opacity = k < 0.7 ? 1 : (1 - k) / 0.3;
            ((TranslateTransform)farmGain.RenderTransform).Y = -FarmGainRise * k;
        }
        // An achievement waits for the label on show (a level up comes with some of them).
        else if (farmGain != null && farmAchievementLabels.TryDequeue(out var achievement))
            ShowFarmLabel(PetFarm.TileCount / 2, L("★ Thành tựu: ", "★ Achievement: ") + achievement.Name(preferences.Language), FarmLevelUpSeconds);
    }

    private PetFarmArt.DecorLayer? farmDecorBack, farmDecorFront;
    private const double FarmBasketBounceSeconds = 0.45;
    private double farmBasketAge = -1;

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
        RefreshFarmDecor();
    }

    private void CreateFarmWindow()
    {
        var art = new PetFarmArt { Cursor = Cursors.Hand };
        var chipText = new TextBlock { Foreground = Brushes.White, FontFamily = PetUi.TextFont, FontSize = PetUi.SmallSize };
        var chip = FarmLabel(chipText);
        var gainText = new TextBlock { Foreground = PetUi.Game.Gold, FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = FarmChipWidth - 24 };
        var gain = FarmLabel(gainText);
        gain.RenderTransform = new TranslateTransform();
        var surface = new Grid { Width = PetFarmArt.ArtWidth, Height = PetFarmArt.ArtHeight };
        farmDecorBack = new PetFarmArt.DecorLayer(FarmDecorPlace.FieldBack, PetFarmArt.ArtWidth, PetFarmArt.Side + PetFarmArt.PlotWidth / 2);
        farmDecorFront = new PetFarmArt.DecorLayer(FarmDecorPlace.FieldFront, PetFarmArt.ArtWidth, PetFarmArt.Side + PetFarmArt.PlotWidth / 2);
        surface.Children.Add(farmDecorBack);
        surface.Children.Add(art);
        surface.Children.Add(farmDecorFront);
        var chipRow = new Grid { Width = FarmChipWidth, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        chipRow.Children.Add(chip);
        var gainRow = new Grid { Width = FarmChipWidth, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        gainRow.Children.Add(gain);
        surface.Children.Add(chipRow);
        surface.Children.Add(gainRow);
        var tipText = new TextBlock { Foreground = PetUi.Game.Gold, FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = FarmChipWidth - 24 };
        var tip = FarmLabel(tipText);
        var tipRow = new Grid { Width = FarmChipWidth, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        tipRow.Children.Add(tip);
        surface.Children.Add(tipRow);
        farmTip = tip; farmTipText = tipText;
        farmSurface = surface;

        farmArt = art; farmChip = chip; farmChipText = chipText; farmGain = gain; farmGainText = gainText;
        farmWindow = CreateOverlayWindow(FarmMagicStage(surface, PetFarmArt.ArtWidth, PetFarmArt.PlotWidth, out var magic), "Pet farm");
        farmMagic = magic;
        farmWindow.Width = PetFarmArt.ArtWidth; farmWindow.Height = PetFarmArt.ArtHeight;
        art.MouseMove += (_, e) => HoverFarm(PetFarmArt.TileAt(e.GetPosition(art).X));
        art.MouseLeave += (_, _) => HoverFarm(-1);
        art.MouseLeftButtonDown += (_, e) => { e.Handled = true; PressFarm(PetFarmArt.TileAt(e.GetPosition(art).X)); };
        art.MouseLeftButtonUp += (_, e) => { e.Handled = true; ReleaseFarm(PetFarmArt.TileAt(e.GetPosition(art).X)); };
        art.LostMouseCapture += (_, _) => farmDragging = false;
        InitializeFarmMenu(art);
        CreateFarmPatchWindows();
        farmWindow.Closed += (_, _) =>
        {
            farmWindow = null; farmArt = null; farmDecorBack = farmDecorFront = null; farmSurface = null; farmMagic = null; farmChip = farmGain = farmTip = null; farmChipText = farmGainText = farmTipText = null; farmTipShown = null;
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
        RestoreFarmFolds();
        SettleFarmPatches();
        farmChecked = FarmNow;
        farm.Advance(farmChecked);
        RefreshFarmArt();
        farmWindow.Show();
        ShowFarmPatches();
        KeepFarmBelowCat();
        PositionFarm();
        RefreshFarmDock();
    }

    // A fullscreen app or a battle takes the screen: a plot in the air goes back where it was.
    private void HideFarm()
    {
        CloseFarmSeedMenu();
        CloseFarmShop();
        CloseFarmKitchen();
        CloseFarmStall();
        ResetFarmCart();
        ResetFarmVisitor();
        farmArt?.ReleaseMouseCapture();
        farmDragging = false;
        if (farmLift.Phase != FarmLiftPhase.Down) { farmFraction = farmRestFraction; farmMonitor = farmRestMonitor ?? farmMonitor; }
        farmLift.Reset();
        farmBird.Reset();
        farmHearthFire = 0;
        farmPondBusy = false;
        HideFarmPatches();
        ResetFarmDock();
        DrawFarmLift();
        UpdateFarmCursor();
        HoverFarm(-1);
        farmWindow?.Hide();
    }

    private void CloseFarm()
    {
        if (farmSunUnsaved > 0) SaveSunnyFarm();
        CloseFarmShop();
        CloseFarmKitchen();
        CloseFarmStall();
        ResetFarmCart();
        ResetFarmVisitor();
        CloseFarmPatches();
        farmWindow?.Close();
    }

    // The plot's monitor in its own DIPs (unplugged: the primary one, at the same place across it).
    private void RefreshFarmArea()
    {
        string? before = farmMonitor;
        var monitor = FindFarmMonitor(farmMonitor);
        farmMonitor = monitor.Name;
        farmWithCat = monitor.Name == CurrentMonitor().DeviceName;
        farmScale = monitor.Scale;
        farmArea = monitor.WorkDips;
        RefreshFarmPatchAreas(before != null && before != farmMonitor);
    }

    private IReadOnlyList<FarmMonitor> FarmMonitors()
    {
        if (farmMonitorsProbe != null) return farmMonitorsProbe();
        string cat = CurrentMonitor().DeviceName;
        return Forms.Screen.AllScreens.Select(s => new FarmMonitor(s.DeviceName,
            new Area(s.Bounds.Left, s.Bounds.Top, s.Bounds.Right, s.Bounds.Bottom),
            new Area(s.WorkingArea.Left, s.WorkingArea.Top, s.WorkingArea.Right, s.WorkingArea.Bottom),
            s.DeviceName == cat ? scale : MonitorScale(s), s.Primary)).ToList();
    }

    // This monitor; unplugged: the primary one, at the same place across it.
    private FarmMonitor FindFarmMonitor(string? name)
    {
        var all = FarmMonitors();
        return all.FirstOrDefault(m => m.Name == name) is { Name: not null } found ? found
            : all.FirstOrDefault(m => m.Primary) is { Name: not null } primary ? primary : all[0];
    }

    // The monitor under the cursor (screen pixels), if any.
    private FarmMonitor? FarmMonitorAt(Point cursor)
    {
        foreach (var monitor in FarmMonitors())
            if (cursor.X >= monitor.Bounds.Left && cursor.X < monitor.Bounds.Right && cursor.Y >= monitor.Bounds.Top && cursor.Y < monitor.Bounds.Bottom) return monitor;
        return null;
    }

    private void PositionFarm()
    {
        if (farmWindow == null) return;
        MoveWindow(farmWindow, FarmPlotLeft - PetFarmArt.Side, farmArea.Bottom + PetFarmArt.BottomRoom - PetFarmArt.ArtHeight, farmScale);
        PositionFarmPatches();
    }

    private const int FarmOrderMilliseconds = 250;
    private long farmOrderDue;

    private IEnumerable<Window> FarmGroundWindows()
    {
        if (farmWindow != null) yield return farmWindow;
        foreach (var patch in farmPatches)
            if (patch.Window != null) yield return patch.Window;
        if (farmCartWindow != null) yield return farmCartWindow;
        if (farmVisitorWindow != null) yield return farmVisitorWindow;
    }

    // What lies above this window in the z-order.
    private static HashSet<IntPtr> WindowsAbove(IntPtr handle)
    {
        const uint Previous = 3;
        var above = new HashSet<IntPtr>();
        for (var next = GetWindow(handle, Previous); next != IntPtr.Zero && above.Add(next); next = GetWindow(next, Previous)) { }
        return above;
    }

    private bool KeepsakeShown => keepsakeWindow?.IsVisible == true;

    public bool FarmGroundAboveCat => FarmGroundAbove(this);
    public bool FarmGroundAboveKeepsake => keepsakeWindow != null && FarmGroundAbove(keepsakeWindow);

    private bool FarmGroundAbove(Window other)
    {
        var above = WindowsAbove(new WindowInteropHelper(other).Handle);
        return FarmGroundWindows().Any(window => above.Contains(new WindowInteropHelper(window).Handle));
    }

    // All are topmost, and WPF applies Topmost again now and then, which brings a window to the top
    // of them: being placed once does not last. The cat walks in front of the land, keepsake too.
    // Only what is out of order is moved, so usually this changes nothing.
    private void KeepFarmBelowCat()
    {
        var cat = new WindowInteropHelper(this).Handle;
        if (cat == IntPtr.Zero) return;
        KeepKeepsakeBelowCat();
        KeepToysInOrder();
        var above = WindowsAbove(new WindowInteropHelper(GroundAnchor).Handle);
        foreach (var window in FarmGroundWindows())
        {
            var handle = new WindowInteropHelper(window).Handle;
            // Issue #86: land that lost "always on top" (placed after a window that was not) would sink behind
            // other apps' windows; it is made topmost again first, then put back under the cat.
            if (handle != IntPtr.Zero && (GetWindowLongPtr(handle, -20).ToInt64() & 0x8) == 0)
            {
                SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
                PlaceBelowCat(window);
            }
            else if (above.Contains(handle)) PlaceBelowCat(window);
        }
    }

    private void PlaceBelowCat(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var above = new WindowInteropHelper(GroundAnchor).Handle;
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
            farmArt.Tiles[i] = new FarmTileView(tile.Stage, tile.Crop, tile.GrowsUntil != null, farm.Next(i) == FarmStep.Water, farm.Progress(i, now), !farm.IsOpen(i), tile.Fertilized);
        }
        farmArt.Scarecrow = farm.Scarecrow;
        farmArt.GoldenScarecrow = farm.GoldenScarecrow;
        int stocked = Enum.GetValues<FarmCrop>().Sum(crop => farm.StockOf(crop));
        farmArt.BasketFill = stocked >= 15 ? 3 : stocked >= 5 ? 2 : stocked >= 1 ? 1 : 0;
        RefreshFarmPot();
        farmArt.HoverTile = farmHover;
        farmArt.Lift = farmLift.Height;
        farmArt.InvalidateVisual();
        RefreshFarmDecor();
    }

    // What is out and how each land is standing; the layers draw again only if that changed.
    private void RefreshFarmDecor()
    {
        if (farm == null) return;
        int shown = farm.DecorMask;
        if (farmArt != null)
        {
            var pose = new FarmLandPose(farmArt.Lift, farmArt.ShakeX, farmArt.Tilt, farmArt.Squash);
            farmDecorBack?.Show(pose, shown);
            farmDecorFront?.Show(pose, shown);
        }
        foreach (var patch in farmPatches)
            if (patch.Art is { } land) patch.Decor?.Show(new FarmLandPose(land.Lift, land.ShakeX, land.Tilt, land.Squash), shown);
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
        FarmCrop.Watermelon => L("Dưa hấu", "Watermelon"),
        FarmCrop.Strawberry => L("Dâu tây", "Strawberry"),
        FarmCrop.Corn => L("Ngô", "Corn"),
        FarmCrop.SweetPotato => L("Khoai lang", "Sweet potato"),
        FarmCrop.NapaCabbage => L("Cải thảo", "Napa cabbage"),
        FarmCrop.FocusBloom => L("Hoa tập trung", "Focus bloom"),
        _ => L("Bí ngô", "Pumpkin")
    };

    private string FarmSeedLabel(FarmCrop crop) => crop switch
    {
        FarmCrop.Radish => L("Củ cải · 5 phút", "Radish · 5 min"),
        FarmCrop.Carrot => L("Cà rốt · 15 phút", "Carrot · 15 min"),
        FarmCrop.Tomato => L("Cà chua · 40 phút", "Tomato · 40 min"),
        FarmCrop.Watermelon => L("Dưa hấu · 4 giờ", "Watermelon · 4 hours"),
        FarmCrop.Strawberry => L("Dâu tây · 25 phút", "Strawberry · 25 min"),
        FarmCrop.Corn => L("Ngô · 50 phút", "Corn · 50 min"),
        FarmCrop.SweetPotato => L("Khoai lang · 70 phút", "Sweet potato · 70 min"),
        FarmCrop.NapaCabbage => L("Cải thảo · 30 phút", "Napa cabbage · 30 min"),
        FarmCrop.FocusBloom => L("Hoa tập trung · 3 phiên", "Focus bloom · 3 sessions"),
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
            FarmStep.Locked => L("Ô khóa", "Locked") + " · " + FarmPriceText(PetFarm.OpenPrice(index)),
            FarmStep.Wait when farm.Tiles[index].Crop == FarmCrop.FocusBloom => name + " · " + L("lớn theo Giờ tập trung", "grows with Focus time"),
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
    // How far a gain label floats up while it fades, and the least room it needs above it so the
    // whole rise stays inside its window (a label starting near the top was cut off as it rose, #77).
    public const double FarmGainRise = 18, FarmGainTop = FarmGainRise + 2;

    private void PlaceFarmLabel(Grid row, int index, double rise, double minTop = 0)
    {
        if (farmArt == null) return;
        const double overhang = 10; // The window is wider than the plot for flying dirt: the label stays near the plot.
        double inset = PetFarmArt.Side - overhang;
        double left = Math.Clamp(PetFarmArt.TileCenter(index) - FarmChipWidth / 2, inset, PetFarmArt.ArtWidth - FarmChipWidth - inset);
        row.Margin = new Thickness(left, Math.Max(minTop, farmArt.TopOfTile(index) - rise), 0, 0);
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

    // Held in the air: the plot follows the cursor along the floor, never up or down, across the monitors.
    private void FollowFarmDrag()
    {
        var cursor = ScreenCursor();
        if (FarmMonitorAt(cursor) is FarmMonitor over && over.Name != farmMonitor)
        {
            farmMonitor = over.Name;
            RefreshFarmArea();
        }
        double free = FarmFreeWidth;
        double left = cursor.X / farmScale - farmGrab;
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
                PlayFarmSound("swoosh");
                break;
            case FarmStep.Harvest:
                if (FarmHarvest(tile, out bool extra) is FarmCrop crop)
                {
                    StartFarmEffect(tile, FarmEffect.Harvest);
                    PlayFarmSound("sparkle");
                    ShowFarmGain(tile, crop, extra);
                }
                break;
            case FarmStep.Locked:
                OpenFarmShop();
                return;
            default:
                return;
        }
        FarmChanged();
    }

    private void FarmChanged()
    {
        if (farm?.TakeLevelsGained() is int gained && gained > 0)
        {
            ShowFarmLevelUp(gained);
            FarmReport = FarmReport with { Levels = FarmReport.Levels + gained };
        }
        SaveFarm();
        CountFarmAchievements();
        RefreshFarmArt();
        farmChipShown = null;
        UpdateFarmChip();
        UpdateFarmShop();
        UpdateFarmKitchen();
        UpdateFarmStall();
        RefreshDiary();
    }

    // This session's farm moments, sent with each observation for Python to choose a line.
    public AI.FarmReport FarmReport { get; private set; } = new(0, 0, 0);

    public bool FarmPlant(int tile, FarmCrop crop)
    {
        if (farm == null || paused || farmLift.Phase != FarmLiftPhase.Down || !farm.Plant(tile, crop, FarmNow)) return false;
        StartFarmEffect(tile, FarmEffect.Plant);
        PlayFarmSound("pop");
        FarmChanged();
        return true;
    }

    // A harvest as the weather on show makes it.
    private FarmCrop? FarmHarvest(int tile, out bool extra)
    {
        var sky = FarmWeather;
        extra = sky == WeatherKind.Sunset && featureRandom.NextDouble() < PetFarm.SunsetExtraChance;
        return farm!.Harvest(tile, extra, sky == WeatherKind.Pollen, farmGoldRandom);
    }

    // What rolls for golden crops. UI checks set it to null, so a harvest there is always an ordinary one.
    private Random? farmGoldRandom = new();

    private void ShowFarmGain(int tile, FarmCrop crop, bool extra = false) => ShowFarmLabel(tile, (extra ? "+2 " : "+1 ") + (farm?.LastHarvestGold == true ? "★ " : "") + FarmCropName(crop), FarmGainSeconds);

    private void ShowFarmLabel(int tile, string text, double seconds)
    {
        if (!farmFieldFold.Showing)
        {
            ShowFarmPatchLabel(FarmDockPatch, text, seconds);
            return;
        }
        if (farmGain == null || farmGainText == null || farmGain.Parent is not Grid row) return;
        farmGainText.Text = text;
        farmGain.Opacity = 1;
        ((TranslateTransform)farmGain.RenderTransform).Y = 0;
        PlaceFarmLabel(row, tile, 30, FarmGainTop);
        farmGainLeft = farmGainTotal = seconds;
    }

    // Over the middle of the field, longer than a harvest's label.
    private void ShowFarmLevelUp(int gained)
    {
        int level = farm!.Level, coins = Enumerable.Range(level - gained + 1, gained).Sum(PetFarm.LevelReward);
        for (int i = 0; i < farm.OpenTiles; i++) StartFarmEffect(i, FarmEffect.Harvest);
        ShowFarmLabel(PetFarm.TileCount / 2, LF($"Lên cấp {level}! +{coins} xu", $"Level {level}! +{coins} coins"), FarmLevelUpSeconds);
    }

    // The seeds, opened just above the tile.
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

    public MenuItem[] FarmSeedItems(int tile) => Enum.GetValues<FarmCrop>().Where(crop => farm == null || farm.InSeason(crop) || farm.OffSeasonOf(crop) > 0).Select(crop =>
    {
        bool locked = farm?.Unlocked(crop) == false, gift = farm?.InSeason(crop) == false;
        var item = new MenuItem { Header = FarmSeedLabel(crop) + " · " + (locked ? FarmLevelText(PetFarm.CropLevel(crop)) : FarmPriceText(gift ? 0 : PetFarm.SeedPrice(crop))), Icon = new FarmCropIcon(crop), IsEnabled = farm == null || (!locked && (gift || farm.Coins >= PetFarm.SeedPrice(crop))) };
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
        var move = new MenuItem { Header = L("Di chuyển mảnh đất farm", "Move the farm plot"), IsEnabled = !FarmLandInAir };
        move.Click += (_, _) => BeginFarmMove();
        if (!FarmShopAllowed) return new[] { move };
        var shop = new MenuItem { Header = L("Kho & cửa hàng", "Store & shop") };
        shop.Click += (_, _) => OpenFarmShop();
        var kitchen = new MenuItem { Header = L("Nấu ăn", "Cook") };
        kitchen.Click += (_, _) => OpenFarmKitchen();
        var stall = new MenuItem { Header = L("Gian hàng", "Stall"), IsEnabled = FarmStallHere };
        stall.Click += (_, _) => OpenFarmStall();
        var fish = new MenuItem { Header = L("Câu cá", "Go fishing"), IsEnabled = FarmCanFish };
        fish.Click += (_, _) => FarmGoFish();
        return new[] { shop, kitchen, stall, fish }.Concat(FarmBulkItems()).Concat(FarmFertilizeItems()).Append(move).ToArray();
    }

    // Over a tile that can take it; a bag that was given is used before coins.
    private IEnumerable<MenuItem> FarmFertilizeItems()
    {
        int tile = farmHover;
        if (farm == null || tile < 0 || !farm.CanFertilize(tile)) yield break;
        var item = new MenuItem { Header = farm.FertilizerStock > 0 ? LF($"Bón phân (còn {farm.FertilizerStock})", $"Fertilize ({farm.FertilizerStock} left)") : LF($"Bón phân · {PetFarm.FertilizerPrice} xu", $"Fertilize · {PetFarm.FertilizerPrice} coins") };
        item.Click += (_, _) => FarmFertilize(tile);
        yield return item;
    }

    public bool FarmFertilize(int tile)
    {
        if (farm == null || paused || farmLift.Phase != FarmLiftPhase.Down || !farm.Fertilize(tile, FarmNow)) return false;
        StartFarmEffect(tile, FarmEffect.Plant);
        PlayPetSound("pop");
        FarmChanged();
        return true;
    }

    public void BeginFarmMove()
    {
        if (paused || !FarmVisible || FarmLandInAir || !farmFieldFold.Showing) return;
        farmRestFraction = farmFraction;
        farmRestMonitor = farmMonitor;
        HoverFarm(-1);
        farmBird.Scare(); // The ground is about to go.
        farmLift.Begin();
        UpdateFarmCursor();
        PlayPetSound("swoosh");
    }

    // Set down here; dropped on other land it first glides to the nearest free place.
    public void DropFarm()
    {
        double free = FarmFreeWidth, spot = FarmLands.FreeSpot(FarmPlotLeft, PetFarmArt.PlotWidth, FarmLandsTaken(farmMonitor, null, withField: false), farmArea.Left, farmArea.Left + free, FarmLandGap);
        double way = Math.Abs(spot - FarmPlotLeft);
        SetFarmDown(free > 0 ? Math.Clamp((spot - farmArea.Left) / free, 0, 1) : 0, way < 0.5 ? 0 : FarmLift.MaxGlideSeconds * Math.Min(1, way / 240));
    }

    // Back to where it was before it rose: it slides there in the air first, then falls.
    public void CancelFarmMove()
    {
        if (farmLift.Floating && farmRestMonitor != null && farmMonitor != farmRestMonitor)
        {
            // Carried to another monitor: it is back in the air over its old place, and falls there.
            farmMonitor = farmRestMonitor;
            farmFraction = farmRestFraction;
            RefreshFarmArea();
            SetFarmDown(farmRestFraction, 0);
            return;
        }
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
