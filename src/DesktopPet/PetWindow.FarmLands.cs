using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopPet;

// A monitor as the farm's land sees it: rectangles in screen pixels, and its DPI scale.
public readonly record struct FarmMonitor(string Name, Area Bounds, Area Work, double Scale, bool Primary)
{
    public Area WorkDips => new(Work.Left / Scale, Work.Top / Scale, Work.Right / Scale, Work.Bottom / Scale);
}

// One of the two small patches of land beside the field (docs/FARM-GAME.md): where it stands, its
// own window and art, and the same lift the field has.
public sealed class FarmPatch
{
    public FarmPatch(FarmPatchKind kind) => Kind = kind;

    public FarmPatchKind Kind { get; }
    public double Width => Kind == FarmPatchKind.Hearth ? PetFarmArt.HearthLandWidth : PetFarmArt.PondLandWidth;
    public double ArtWidth => Width + PetFarmArt.Side * 2;
    public FarmLift Lift { get; } = new();
    public Window? Window { get; internal set; }
    public PetFarmArt.Land? Art { get; internal set; }
    public Border? Gain { get; internal set; }
    public string? Monitor { get; set; }
    public string? RestMonitor { get; set; }
    public Area Area { get; set; }
    public double Scale { get; set; } = 1;
    public bool WithCat { get; set; }
    public double Fraction { get; set; }
    public double RestFraction { get; set; }
    public double Grab { get; set; }
    public bool Dragging { get; set; }
    internal double FallFrom, FallTo, GainLeft;
    public bool OnGround => Lift.Phase == FarmLiftPhase.Down;
}

// The hearth and the pond are land of their own: each is lifted, carried along the floor and set
// down exactly like the field, one patch at a time (PetWindow.Farm.cs has the field's own copy).
public sealed partial class PetWindow
{
    private const double FarmLandGap = 6;

    private readonly FarmPatch[] farmPatches = { new(FarmPatchKind.Hearth), new(FarmPatchKind.Pond) };
    public FarmPatch FarmHearth => farmPatches[0];
    public FarmPatch FarmPond => farmPatches[1];

    // Only one piece of land is in the air at a time.
    public bool FarmLandInAir => farmLift.Phase != FarmLiftPhase.Down || farmPatches.Any(patch => !patch.OnGround);

    private double FarmPatchFree(FarmPatch patch) => Math.Max(0, patch.Area.Right - patch.Area.Left - patch.Width);
    public double FarmPatchLeft(FarmPatch patch) => patch.Area.Left + patch.Fraction * FarmPatchFree(patch);

    // What stands on this monitor. Land on another one is no obstacle.
    private List<(double Left, double Right)> FarmLandsTaken(string? monitor, FarmPatch? except, bool withField = true)
    {
        var taken = new List<(double Left, double Right)>();
        if (withField && farmMonitor == monitor) taken.Add((FarmPlotLeft, FarmPlotLeft + PetFarmArt.PlotWidth));
        foreach (var patch in farmPatches)
            if (patch != except && patch.Monitor == monitor) taken.Add((FarmPatchLeft(patch), FarmPatchLeft(patch) + patch.Width));
        return taken;
    }

    private void ApplyFarmPatchMonitor(FarmPatch patch)
    {
        var monitor = FindFarmMonitor(patch.Monitor);
        patch.Monitor = monitor.Name;
        patch.Scale = monitor.Scale;
        patch.Area = monitor.WorkDips;
        patch.WithCat = monitor.Name == CurrentMonitor().DeviceName;
    }

    // Once a second with the field's own. A monitor unplugged may have put land on top of land.
    private void RefreshFarmPatchAreas(bool fieldMoved)
    {
        bool moved = fieldMoved;
        foreach (var patch in farmPatches)
        {
            if (patch.Monitor == null) continue;
            string? before = patch.Monitor;
            ApplyFarmPatchMonitor(patch);
            moved |= before != patch.Monitor;
        }
        if (!moved) return;
        foreach (var patch in farmPatches)
        {
            if (patch.Monitor == null || !patch.OnGround) continue;
            double free = FarmPatchFree(patch), left = FarmPatchLeft(patch);
            double spot = FarmLands.FreeSpot(left, patch.Width, FarmLandsTaken(patch.Monitor, patch), patch.Area.Left, patch.Area.Left + free, FarmLandGap);
            patch.Fraction = patch.RestFraction = free > 0 ? Math.Clamp((spot - patch.Area.Left) / free, 0, 1) : 0;
        }
    }

    private void CreateFarmPatchWindows()
    {
        foreach (var patch in farmPatches)
        {
            if (patch.Window != null) continue;
            var art = new PetFarmArt.Land(patch.Kind);
            var surface = new Grid { Width = patch.ArtWidth, Height = PetFarmArt.ArtHeight };
            surface.Children.Add(art);
            if (patch.Kind == FarmPatchKind.Pond)
            {
                var text = new TextBlock { Foreground = PetUi.Game.Gold, FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold };
                var gain = FarmLabel(text);
                gain.RenderTransform = new TranslateTransform();
                gain.Margin = new Thickness(0, PetFarmArt.SoilTop - 40, 0, 0);
                gain.IsHitTestVisible = false;
                surface.Children.Add(gain);
                patch.Gain = gain;
            }
            patch.Art = art;
            patch.Window = CreateOverlayWindow(surface, patch.Kind == FarmPatchKind.Hearth ? "Pet farm hearth" : "Pet farm pond");
            patch.Window.Width = patch.ArtWidth; patch.Window.Height = PetFarmArt.ArtHeight;
            art.MouseLeftButtonDown += (_, e) => { e.Handled = true; PressFarmPatch(patch); };
            art.MouseLeftButtonUp += (_, e) => { e.Handled = true; ReleaseFarmPatch(patch); };
            art.LostMouseCapture += (_, _) => patch.Dragging = false;
            InitializeFarmPatchMenu(patch, art);
            patch.Window.Closed += (_, _) => { patch.Window = null; patch.Art = null; patch.Gain = null; patch.Dragging = false; patch.GainLeft = 0; };
        }
    }

    // Where each patch was left. A new one stands to the right of what is already there, and never on top of anything.
    private void SettleFarmPatches()
    {
        if (farm == null) return;
        var taken = new List<(string Monitor, double Left, double Right)> { (farmMonitor!, FarmPlotLeft, FarmPlotLeft + PetFarmArt.PlotWidth) };
        foreach (var patch in farmPatches)
        {
            bool hearth = patch.Kind == FarmPatchKind.Hearth;
            double? saved = hearth ? farm.HearthFractionX : farm.PondFractionX;
            patch.Monitor = (hearth ? farm.HearthMonitor : farm.PondMonitor) ?? farmMonitor;
            ApplyFarmPatchMonitor(patch);
            double free = FarmPatchFree(patch);
            var here = taken.Where(land => land.Monitor == patch.Monitor).Select(land => (land.Left, land.Right)).ToList();
            double wanted = saved is double fraction ? patch.Area.Left + fraction * free : here.Max(land => land.Right) + FarmLandGap;
            double spot = FarmLands.FreeSpot(wanted, patch.Width, here, patch.Area.Left, patch.Area.Left + free, FarmLandGap);
            patch.Fraction = patch.RestFraction = free > 0 ? Math.Clamp((spot - patch.Area.Left) / free, 0, 1) : 0;
            if (saved == null) StoreFarmPatch(patch);
            taken.Add((patch.Monitor!, spot, spot + patch.Width));
        }
    }

    private void StoreFarmPatch(FarmPatch patch)
    {
        if (farm == null) return;
        if (patch.Kind == FarmPatchKind.Hearth) { farm.HearthFractionX = patch.Fraction; farm.HearthMonitor = patch.Monitor; }
        else { farm.PondFractionX = patch.Fraction; farm.PondMonitor = patch.Monitor; }
    }

    private void ShowFarmPatches()
    {
        foreach (var patch in farmPatches)
        {
            patch.Window?.Show();
            if (patch.Window != null) PlaceBelowCat(patch.Window);
            DrawFarmPatch(patch);
        }
    }

    private void HideFarmPatches()
    {
        foreach (var patch in farmPatches)
        {
            patch.Art?.ReleaseMouseCapture();
            patch.Dragging = false;
            if (!patch.OnGround) { patch.Fraction = patch.RestFraction; patch.Monitor = patch.RestMonitor ?? patch.Monitor; }
            patch.Lift.Reset();
            patch.GainLeft = 0;
            if (patch.Gain != null) patch.Gain.Opacity = 0;
            patch.Window?.Hide();
            UpdateFarmPatchCursor(patch);
        }
    }

    private void CloseFarmPatches()
    {
        foreach (var patch in farmPatches) patch.Window?.Close();
    }

    private void PositionFarmPatches()
    {
        foreach (var patch in farmPatches)
            if (patch.Window != null) MoveWindow(patch.Window, FarmPatchLeft(patch) - PetFarmArt.Side, patch.Area.Bottom + PetFarmArt.BottomRoom - PetFarmArt.ArtHeight, patch.Scale);
    }

    private void FollowFarmPatchDrags()
    {
        foreach (var patch in farmPatches)
        {
            if (!patch.Dragging) continue;
            var cursor = ScreenCursor();
            if (FarmMonitorAt(cursor) is FarmMonitor over && over.Name != patch.Monitor)
            {
                patch.Monitor = over.Name;
                ApplyFarmPatchMonitor(patch);
            }
            double free = FarmPatchFree(patch), left = cursor.X / patch.Scale - patch.Grab;
            patch.Fraction = free > 0 ? Math.Clamp((left - patch.Area.Left) / free, 0, 1) : 0;
        }
    }

    // The fire takes and dies down; only then does the hearth need drawing every frame.
    private bool StepFarmHearth(double dt)
    {
        bool cooking = farmChores.Cooking && farmChores.Phase == FarmChorePhase.Work;
        if (!cooking && farmHearthFire <= 0) return false;
        farmHearthFire = Math.Clamp(farmHearthFire + (cooking ? dt * 3 : -dt * 2), 0, 1);
        farmHearthTime += dt;
        return true;
    }

    private bool StepFarmPond(double dt)
    {
        bool fishing = farmChores.Phase == FarmChorePhase.Work && farmChores.Kind == FarmChoreKind.Fishing, was = farmPondBusy;
        farmPondBusy = fishing;
        if (!fishing && !was) return false;
        farmPondTime += dt;
        return true;
    }

    private void StepFarmPatches(double dt)
    {
        bool fire = StepFarmHearth(dt), water = StepFarmPond(dt);
        foreach (var patch in farmPatches)
        {
            bool lifted = patch.Lift.Busy;
            if (patch.Lift.Step(dt)) LandFarmPatch(patch);
            if (patch.Lift.Phase == FarmLiftPhase.Falling) patch.Fraction = patch.FallFrom + (patch.FallTo - patch.FallFrom) * patch.Lift.FallAmount;
            if (lifted || patch.Lift.Busy || (patch.Kind == FarmPatchKind.Hearth ? fire : water)) DrawFarmPatch(patch);
            if (patch.GainLeft > 0 && patch.Gain != null)
            {
                patch.GainLeft = Math.Max(0, patch.GainLeft - dt);
                double k = 1 - patch.GainLeft / FarmGainSeconds;
                patch.Gain.Opacity = k < 0.7 ? 1 : (1 - k) / 0.3;
                ((TranslateTransform)patch.Gain.RenderTransform).Y = -18 * k;
            }
        }
    }

    private void ShowFarmLands()
    {
        bool working = farmChores.Phase == FarmChorePhase.Work;
        if (FarmHearth.Art is { } hearth)
        {
            hearth.HearthFire = farmHearthFire;
            hearth.HearthTime = farmHearthTime;
            hearth.PotDish = farm?.PotDish;
            hearth.Stirring = working && farmChores.Kind == FarmChoreKind.Stir;
            hearth.PourDish = working && farmChores.Kind == FarmChoreKind.Pour ? farmChores.Dish : null;
            hearth.PourAge = working ? farmChores.Time / FarmChores.PourSeconds : 0;
        }
        if (FarmPond.Art is { } pond)
        {
            bool fishing = working && farmChores.Kind == FarmChoreKind.Fishing;
            double sinceBite = fishing ? farmChores.Time - farmChores.BiteAt : -1;
            pond.HearthTime = farmPondTime;
            pond.Fishing = fishing;
            pond.FishSide = farmChores.FishSide;
            pond.CaughtFish = sinceBite >= 0 ? farmChores.Catch : null;
            pond.CaughtAge = Math.Clamp(sinceBite / (FarmChores.ReelSeconds + FarmChores.FishShowSeconds), 0, 1);
        }
    }

    private void DrawFarmPatch(FarmPatch patch)
    {
        if (patch.Art == null) return;
        ShowFarmLands();
        patch.Art.Lift = patch.Lift.Height;
        patch.Art.Dustiness = patch.Lift.Dust;
        patch.Art.ShakeX = patch.Lift.ShakeX;
        patch.Art.Tilt = patch.Lift.Tilt;
        patch.Art.Squash = patch.Lift.Squash;
        patch.Art.Shed = patch.Lift.Shed;
        patch.Art.InvalidateVisual();
    }

    // The pot's contents changed: nothing else about the land did.
    private void RefreshFarmPot()
    {
        if (FarmHearth.Art is { } hearth && hearth.PotDish != farm?.PotDish) DrawFarmPatch(FarmHearth);
    }

    private void ShowFarmFishGain(FarmFish fish)
    {
        if (FarmPond.Gain == null) return;
        ((TextBlock)FarmPond.Gain.Child).Text = "+1 " + FarmFishName(fish);
        FarmPond.Gain.Opacity = 1;
        ((TranslateTransform)FarmPond.Gain.RenderTransform).Y = 0;
        FarmPond.GainLeft = FarmGainSeconds;
    }

    private void PressFarmPatch(FarmPatch patch)
    {
        if (paused || patch.Art == null || patch.Lift.Phase is not (FarmLiftPhase.Rising or FarmLiftPhase.Floating)) return;
        if (!patch.Art.CaptureMouse()) return;
        patch.Dragging = true;
        patch.Grab = ScreenCursor().X / patch.Scale - FarmPatchLeft(patch);
    }

    private void ReleaseFarmPatch(FarmPatch patch)
    {
        if (!patch.Dragging) return;
        patch.Dragging = false;
        patch.Art?.ReleaseMouseCapture();
    }

    private void InitializeFarmPatchMenu(FarmPatch patch, FrameworkElement art)
    {
        var menu = new ContextMenu { FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize };
        PetMenuStyle.Apply(menu);
        art.ContextMenu = menu;
        art.ContextMenuOpening += (_, e) =>
        {
            var items = FarmPatchItems(patch);
            if (items.Length == 0) { e.Handled = true; return; }
            menu.Items.Clear();
            foreach (var item in items) menu.Items.Add(item);
        };
    }

    // On the ground: its own job, and move it. In the air: set it down, or put it back.
    public MenuItem[] FarmPatchItems(FarmPatch patch)
    {
        if (paused || patch.Dragging || patch.Lift.Phase is FarmLiftPhase.Rising or FarmLiftPhase.Falling) return Array.Empty<MenuItem>();
        if (patch.Lift.Phase == FarmLiftPhase.Floating)
        {
            var drop = new MenuItem { Header = L("Thả xuống đây", "Drop it here") };
            drop.Click += (_, _) => DropFarmPatch(patch);
            var cancel = new MenuItem { Header = L("Hủy", "Cancel") };
            cancel.Click += (_, _) => CancelFarmPatchMove(patch);
            return new[] { drop, cancel };
        }
        var move = new MenuItem { Header = patch.Kind == FarmPatchKind.Hearth ? L("Di chuyển bếp", "Move the hearth") : L("Di chuyển hồ", "Move the pond"), IsEnabled = !FarmLandInAir };
        move.Click += (_, _) => BeginFarmPatchMove(patch);
        if (!FarmShopAllowed) return new[] { move };
        MenuItem job;
        if (patch.Kind == FarmPatchKind.Hearth)
        {
            job = new MenuItem { Header = L("Nấu ăn", "Cook") };
            job.Click += (_, _) => OpenFarmKitchen();
        }
        else
        {
            job = new MenuItem { Header = L("Câu cá", "Go fishing"), IsEnabled = FarmCanFish };
            job.Click += (_, _) => FarmGoFish();
        }
        return new[] { job, move };
    }

    public void BeginFarmPatchMove(FarmPatch patch)
    {
        if (paused || !FarmVisible || FarmLandInAir) return;
        patch.RestFraction = patch.Fraction;
        patch.RestMonitor = patch.Monitor;
        if (patch.Kind == FarmPatchKind.Hearth) CloseFarmKitchen();
        patch.Lift.Begin();
        UpdateFarmPatchCursor(patch);
        PlayPetSound("swoosh");
    }

    // Set down here; dropped on other land it first glides to the nearest free place.
    public void DropFarmPatch(FarmPatch patch)
    {
        double left = FarmPatchLeft(patch), free = FarmPatchFree(patch);
        double spot = FarmLands.FreeSpot(left, patch.Width, FarmLandsTaken(patch.Monitor, patch), patch.Area.Left, patch.Area.Left + free, FarmLandGap);
        double way = Math.Abs(spot - left);
        SetFarmPatchDown(patch, free > 0 ? Math.Clamp((spot - patch.Area.Left) / free, 0, 1) : 0, way < 0.5 ? 0 : FarmLift.MaxGlideSeconds * Math.Min(1, way / 240));
    }

    public void CancelFarmPatchMove(FarmPatch patch)
    {
        if (patch.Lift.Floating && patch.RestMonitor != null && patch.Monitor != patch.RestMonitor)
        {
            // Carried to another monitor: it is back in the air over its old place, and falls there.
            patch.Monitor = patch.RestMonitor;
            ApplyFarmPatchMonitor(patch);
            patch.Fraction = patch.RestFraction;
            SetFarmPatchDown(patch, patch.RestFraction, 0);
            return;
        }
        double way = Math.Abs(patch.Fraction - patch.RestFraction) * FarmPatchFree(patch);
        SetFarmPatchDown(patch, patch.RestFraction, FarmLift.MaxGlideSeconds * Math.Min(1, way / 240));
    }

    private void SetFarmPatchDown(FarmPatch patch, double target, double glide)
    {
        if (paused || !patch.Lift.Floating) return;
        patch.Art?.ReleaseMouseCapture();
        patch.Dragging = false;
        patch.FallFrom = patch.Fraction;
        patch.FallTo = target;
        patch.Lift.Drop(glide);
        UpdateFarmPatchCursor(patch);
    }

    private void LandFarmPatch(FarmPatch patch)
    {
        patch.Fraction = patch.RestFraction = patch.FallTo;
        UpdateFarmPatchCursor(patch);
        PlayPetSound("squish");
        if (farm == null) return;
        StoreFarmPatch(patch);
        SaveFarm();
    }

    private void UpdateFarmPatchCursor(FarmPatch patch)
    {
        if (patch.Art != null) patch.Art.Cursor = patch.OnGround ? Cursors.Arrow : Cursors.SizeWE;
    }
}
