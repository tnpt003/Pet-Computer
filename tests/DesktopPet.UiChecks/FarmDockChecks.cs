using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using static PetPrivate;

// The farm sign in the real pet window (docs/FARM-GAME.md, Biển nông trại): folding land, guests
// waiting on the sign, its menu and what is remembered. The cat's trips are in FarmChoreChecks.
internal static class FarmDockChecks
{
    private static string[] Headers(MenuItem[] items) => items.Select(item => (string)item.Header).ToArray();

    public static void Run(PetWindow window, PetFarm farm, string path, Action<bool, string> check, Action<double> frames)
    {
        var dock = window.FarmDockPatch;
        var art = dock.Art!;
        void Put() => frames(FarmLandFold.Seconds + 0.05);
        var field = window.FarmFoldOf(FarmLandSlot.Field);
        var hearth = window.FarmFoldOf(FarmLandSlot.Hearth);
        var pond = window.FarmFoldOf(FarmLandSlot.Pond);
        var fieldSurface = Field<Grid?>(window, "farmSurface")!;

        check(dock.Window?.IsVisible == true && dock.Width == PetFarmArt.DockLandWidth, "The sign stands on the floor");
        double left = window.FarmPatchLeft(dock), right = left + dock.Width;
        check(new[] { window.FarmHearth, window.FarmPond }.All(p => right <= window.FarmPatchLeft(p) || left >= window.FarmPatchLeft(p) + p.Width)
            && (dock.Monitor != window.FarmHearth.Monitor || right <= window.FarmPlotStart || left >= window.FarmPlotStart + PetFarmArt.PlotWidth), "...on ground of its own");
        check(farm.HasCoop || art.DockSlots[3].Locked, "A coop not bought is a locked board");

        // The boards are where the clicks land.
        check(art.DockTargetAt(new Point(60, 94)) == 0 && art.DockTargetAt(new Point(95, 94)) == 1 && art.DockTargetAt(new Point(130, 94)) == 2 && art.DockTargetAt(new Point(165, 94)) == 3, "Each board takes its own clicks");
        check(art.DockTargetAt(new Point(100, 30)) == -1 && art.DockTargetAt(new Point(62, 62)) == -1, "Nothing above them while no guest is waiting");
        check(Enumerable.Range(0, 4).All(i => window.FarmDockLabel(i)!.Length > 0) && window.FarmDockLabel(-1) == null, "Each board says what it is");

        // One land.
        check(field.Showing && field.Amount == 1 && fieldSurface.Visibility == Visibility.Visible, "At first all the land is out");
        window.FarmDockClick(0);
        check(!field.Showing && farm.FieldFolded && PetFarm.Load(path).FieldFolded, "A click on its board folds the field, and it is saved");
        frames(FarmLandFold.Seconds / 2);
        check(field.Amount > 0 && field.Amount < 1 && fieldSurface.Opacity < 1, "It sinks away");
        Put();
        check(field.Amount == 0 && fieldSurface.Visibility == Visibility.Collapsed && window.FarmVisible, "...and is gone, with the farm still running");
        check(!art.DockSlots[0].Open && art.DockSlots[1].Open, "The sign shows which land is out");

        // Where it was stays free for others, and it finds room when it comes back.
        window.FarmDockClick(0);
        Put();
        check(field.Amount == 1 && !farm.FieldFolded && fieldSurface.Visibility == Visibility.Visible, "A second click brings it back");

        // Labels of folded land are read on the sign.
        window.FarmDockClick(0);
        Put();
        dock.Gain!.Opacity = 0;
        Call(window, "ShowFarmLabel", 0, "+1", 1.0);
        check(dock.Gain.Opacity == 1, "What the folded field has to say is on the sign");
        frames(1.1);
        window.FarmDockClick(0);
        Put();

        // Everything.
        check(window.FarmSetAllLands(false) && window.FarmDockCompact, "Fold all");
        Put();
        check(window.FarmHearth.Surface!.Visibility == Visibility.Collapsed && window.FarmPond.Surface!.Visibility == Visibility.Collapsed && fieldSurface.Visibility == Visibility.Collapsed, "...and only the sign is left");
        check(window.FarmHearth.Window!.IsVisible && window.FarmPond.Window!.IsVisible, "The windows stay; only what is in them goes");
        check(Headers(window.FarmPatchItems(dock)).Take(2).SequenceEqual(new[] { "Open all land", "Fold all land" }) && window.FarmPatchItems(dock)[0].IsEnabled && !window.FarmPatchItems(dock)[1].IsEnabled, "The sign's menu offers what is left to do");
        check(Headers(window.FarmPatchItems(dock)).Last() == "Move the sign", "...and moves the sign");
        check(Headers(window.FarmPatchItems(dock)).Contains("Store & shop") && Headers(window.FarmPatchItems(dock)).Contains("Cook") && Headers(window.FarmPatchItems(dock)).Contains("Go fishing"), "The farm's jobs are all on it");

        // Nothing while paused or with land in the air.
        SetField(window, "paused", true);
        check(!window.FarmToggleLand(FarmLandSlot.Pond) && window.FarmPatchItems(dock).Length == 0, "Pause holds the land where it is");
        SetField(window, "paused", false);

        // Guests wait on the sign.
        var stall = Field<FarmStall>(window, "farmStall");
        stall.Reset();
        check(window.TryFarmStall() && window.FarmStallHere && Field<Window?>(window, "farmCartWindow") == null, "With all land folded the cart stays off the floor");
        Call(window, "RefreshFarmDock");
        check(art.DockCart && art.DockTargetAt(new Point(98, 62)) == PetFarmArt.Land.DockCartTarget, "...and waits on the sign");
        window.FarmDockClick(PetFarmArt.Land.DockCartTarget);
        check(window.FarmStallOpen, "A click browses it");
        window.CloseFarmStall();
        frames(FarmStall.RollSeconds + 0.1);
        var visitor = window.FarmVisitorNow;
        visitor.Reset();
        farm.Stock["Radish"] = 3;
        check(window.TryFarmVisitor() && visitor.Here && window.FarmVisitorWindow == null, "The neighbour's cat too");
        Call(window, "RefreshFarmDock");
        check(art.DockVisitor && window.FarmDockLabel(PetFarmArt.Land.DockVisitorTarget)!.Length > 0, "...and says what it wants");
        Render(dock.Window!, "farm-dock-guests.png", Brushes.WhiteSmoke);
        Render(dock.Window!, "farm-dock-guests-dark.png", new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x3A)));
        frames(FarmVisitor.RollSeconds + 0.1);

        check(window.FarmToggleLand(FarmLandSlot.Pond) && Field<Window?>(window, "farmCartWindow") != null && window.FarmVisitorWindow != null, "Land opened: they come down to the floor");
        check(window.FarmToggleLand(FarmLandSlot.Pond) && Field<Window?>(window, "farmCartWindow") == null && window.FarmVisitorWindow == null && stall.Here && visitor.Here, "Folded again: back on the sign, the visit goes on");
        Call(window, "ResetFarmCart");
        Call(window, "ResetFarmVisitor");
        Call(window, "RefreshFarmDock");
        check(!art.DockCart && !art.DockVisitor, "Gone when they leave");
        Put();

        // A fullscreen app and back.
        Call(window, "HideFarm");
        frames(0.02);
        check(window.FarmVisible && !field.Showing && !hearth.Showing && !pond.Showing, "Back from a fullscreen app it is still folded");

        var loaded = PetFarm.Load(path);
        check(loaded.FieldFolded && loaded.HearthFolded && loaded.PondFolded && loaded.DockFractionX != null, "All of it is remembered");

        Render(dock.Window!, "farm-dock-folded.png", Brushes.WhiteSmoke);
        Render(dock.Window!, "farm-dock-folded-dark.png", new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x3A)));
        check(window.FarmSetAllLands(true) && !window.FarmDockCompact, "Open all");
        Put();
        Call(window, "RefreshFarmDock");
        check(field.Amount == 1 && hearth.Amount == 1 && pond.Amount == 1, "...and the land is back");
        Render(dock.Window!, "farm-dock-open.png", Brushes.WhiteSmoke);
    }

    private static void Render(Window source, string name, Brush background)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not string review) return;
        source.UpdateLayout();
        var element = (FrameworkElement)source.Content;
        var scene = new DrawingVisual();
        using (var dc = scene.RenderOpen())
        {
            var box = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
            dc.DrawRectangle(background, null, box);
            dc.DrawRectangle(new VisualBrush(element), null, box);
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(scene);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(review);
        using var stream = File.Create(Path.Combine(review, name));
        encoder.Save(stream);
    }
}
