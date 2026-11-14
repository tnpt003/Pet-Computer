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
        void Put() => frames(FarmLandFold.OpenSeconds + FarmLandFold.StaggerSeconds * 3 + 0.05);
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
        frames(FarmLandFold.FoldSeconds * 0.75);
        var fieldMagic = Field<PetFarmArt.LandMagic?>(window, "farmMagic")!;
        check(field.Moving && fieldSurface.Opacity < 1 && fieldMagic.Visibility == Visibility.Visible && fieldMagic.Amount == field.Amount, "It sinks into the spell");
        Put();
        check(field.Amount == 0 && fieldSurface.Visibility == Visibility.Collapsed && window.FarmVisible, "...and is gone, with the farm still running");
        check(fieldMagic.Visibility == Visibility.Collapsed, "The spell is over and draws nothing");
        check(!art.DockSlots[0].Open && art.DockSlots[1].Open, "The sign shows which land is out");

        // Where it was stays free for others, and it finds room when it comes back.
        window.FarmDockClick(0);
        frames(FarmLandFold.OpenSeconds * 0.15);
        check(field.Moving && fieldSurface.Visibility == Visibility.Collapsed && fieldMagic.Visibility == Visibility.Visible, "Opening starts with the ring, before any land");
        Put();
        check(field.Amount == 1 && !farm.FieldFolded && fieldSurface.Visibility == Visibility.Visible && fieldSurface.RenderTransform == Transform.Identity, "A second click brings it back, exactly as it was");

        // Labels of folded land are read on the sign.
        window.FarmDockClick(0);
        Put();
        dock.Gain!.Opacity = 0;
        Call(window, "ShowFarmLabel", 0, "+1", 1.0);
        check(dock.Gain.Opacity == 1, "What the folded field has to say is on the sign");
        Call(window, "ShowFarmLabel", 0, "Level 5! Watermelon unlocked, 50 coins", 1.0); // Two lines on the narrow sign.
        frames(0.95);
        dock.Gain.UpdateLayout();
        var gainTop = dock.Gain.TranslatePoint(new Point(0, 0), dock.Window!.Content as UIElement ?? dock.Gain);
        check(dock.Gain.Margin.Top >= PetWindow.FarmGainRise && gainTop.Y >= 0,
            $"The sign's label stays inside its window all the way up (top {gainTop.Y:F1} DIPs near the end; it was cut off, #77)");
        frames(0.2);

        // A level up of the folded field: its card on the sign, a star on the field's board until it is out.
        Call(window, "ShowFarmLevelUp", 1);
        var levelCard = LevelCard(window, "farmLevelDock");
        check(levelCard.Opacity == 1 && window.FarmLevelUnseen && art.DockSlots[0].Star && !art.DockSlots[1].Star,
            "A level up of the folded field shows its card on the sign and a star on the field's board");
        frames(1);
        levelCard.UpdateLayout();
        var cardTop = levelCard.TranslatePoint(new Point(0, 0), dock.Surface!);
        check(cardTop.X >= 0 && cardTop.X + levelCard.ActualWidth <= dock.Surface!.Width + 0.5 && cardTop.Y >= 0,
            $"...inside the sign's window ({cardTop.X:F0}, {levelCard.ActualWidth:F0} of {dock.Surface!.Width:F0})");
        Render(dock.Window!, "farm-level-dock.png", Brushes.WhiteSmoke);
        Render(dock.Window!, "farm-level-dock-dark.png", new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x3A)));
        frames(PetWindow.FarmLevelCardSeconds);
        check(levelCard.Opacity < 0.05 && !window.FarmLevelCardShowing && art.DockSlots[0].Star, "The card goes; the star stays");
        window.FarmDockClick(0);
        Put();
        check(!art.DockSlots[0].Star && !window.FarmLevelUnseen, "...until the field is out again");

        // Everything.
        check(window.FarmSetAllLands(false), "Fold all");
        frames(0.02);
        check(new[] { field, hearth, pond }.Count(fold => fold.Moving) == 1, "...one land after another");
        Put();
        check(window.FarmHearth.Surface!.Visibility == Visibility.Collapsed && window.FarmPond.Surface!.Visibility == Visibility.Collapsed && fieldSurface.Visibility == Visibility.Collapsed, "...and only the sign is left");
        check(window.FarmHearth.Window!.IsVisible && window.FarmPond.Window!.IsVisible, "The windows stay; only what is in them goes");
        check(Headers(window.FarmPatchItems(dock)).Take(2).SequenceEqual(new[] { "Open all land", "Fold all land" }) && window.FarmPatchItems(dock)[0].IsEnabled && !window.FarmPatchItems(dock)[1].IsEnabled, "The sign's menu offers what is left to do");
        check(Headers(window.FarmPatchItems(dock)).Last() == "Move the sign", "...and moves the sign");
        check(Headers(window.FarmPatchItems(dock)).SequenceEqual(new[] { "Open all land", "Fold all land", "Move the sign" }),
            $"Only those three: the farm's jobs are on the menus of their own land ({string.Join(", ", Headers(window.FarmPatchItems(dock)))})");

        // Nothing while paused or with land in the air.
        SetField(window, "paused", true);
        check(!window.FarmToggleLand(FarmLandSlot.Pond) && window.FarmPatchItems(dock).Length == 0, "Pause holds the land where it is");
        SetField(window, "paused", false);

        // Guests come to the floor as ever, folded land or not.
        var stall = Field<FarmStall>(window, "farmStall");
        stall.Reset();
        check(window.TryFarmStall() && window.FarmStallHere && Field<Window?>(window, "farmCartWindow") != null, "With all land folded the cart still comes to the floor");
        var visitor = window.FarmVisitorNow;
        visitor.Reset();
        farm.Stock["Radish"] = 3;
        check(window.TryFarmVisitor() && visitor.Here && window.FarmVisitorWindow != null, "...and the neighbour's cat");
        Call(window, "ResetFarmCart");
        Call(window, "ResetFarmVisitor");
        Put();

        // A fullscreen app and back.
        Call(window, "HideFarm");
        frames(0.02);
        check(window.FarmVisible && !field.Showing && !hearth.Showing && !pond.Showing, "Back from a fullscreen app it is still folded");

        var loaded = PetFarm.Load(path);
        check(loaded.FieldFolded && loaded.HearthFolded && loaded.PondFolded && loaded.DockFractionX != null, "All of it is remembered");

        Render(dock.Window!, "farm-dock-folded.png", Brushes.WhiteSmoke);
        Render(dock.Window!, "farm-dock-folded-dark.png", new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x3A)));
        check(window.FarmSetAllLands(true), "Open all");
        frames(0.02);
        check(new[] { field, hearth, pond }.Count(fold => fold.Moving) == 1, "...one after another too");
        foreach (var (at, name) in new[] { (0.22, "ring"), (0.5, "rising"), (0.75, "stars") })
        {
            hearth.Restore(true);
            hearth.Open();
            frames(FarmLandFold.OpenSeconds * at);
            Render(window.FarmHearth.Window!, $"farm-magic-{name}.png", Brushes.WhiteSmoke);
            Render(window.FarmHearth.Window!, $"farm-magic-{name}-dark.png", new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x3A)));
        }
        Put();
        Call(window, "RefreshFarmDock");
        check(field.Amount == 1 && hearth.Amount == 1 && pond.Amount == 1, "...and the land is back");
        Render(dock.Window!, "farm-dock-open.png", Brushes.WhiteSmoke);
    }

    // The Border of one of the farm's level cards (a private holder in PetWindow).
    internal static Border LevelCard(PetWindow window, string name)
    {
        var holder = Field<object?>(window, name)!;
        return (Border)holder.GetType().GetProperty("Card")!.GetValue(holder)!;
    }

    // Ends a level card an earlier check left up, and drops the labels waiting for it.
    internal static void EndLevelCard(PetWindow window)
    {
        if (window.FarmLevelCardShowing)
        {
            SetField(window, "farmLevelLeft", 1e-6);
            Call(window, "StepFarmLevelCard", 1.0);
        }
        Field<Queue<string>>(window, "farmNotices").Clear();
    }

    internal static string LevelCardText(PetWindow window, string name)
    {
        var panel = (StackPanel)LevelCard(window, name).Child;
        return string.Join(" / ", panel.Children.OfType<TextBlock>().Where(t => t.Visibility == Visibility.Visible).Select(t => t.Text));
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
