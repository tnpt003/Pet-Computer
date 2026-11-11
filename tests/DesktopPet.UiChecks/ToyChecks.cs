using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using static PetPrivate;

// The toy basket and the toys left out, in the real pet window (docs/TOY-PLAY.md, M1). Nothing
// here reads the real cursor: drags are started and ended directly.
internal static class ToyChecks
{
    private const string Ball = DesktopToyRules.Ball, Box = DesktopToyRules.Box;

    private static void Context(PetWindow window, bool fullscreen)
    {
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, 0.0)));
        Call(window, "TickContext", 1.1);
    }

    private static void Tick(PetWindow window, double seconds = 1 / 60.0)
    {
        for (double t = 0; t < seconds; t += 1 / 60.0) Call(window, "TickDesktopToys", 1 / 60.0);
    }

    private static void Settle(PetWindow window, ToyBall motion)
    {
        for (int i = 0; i < 900 && !motion.AtRest; i++) Tick(window);
        Tick(window);
    }

    private static object Thing(PetWindow window, string id) => Call(window, "DesktopToy", id)!;
    private static void SetThing(object thing, string field, object? value) => thing.GetType().GetField(field)!.SetValue(thing, value);
    private static T GetThing<T>(object thing, string field) => (T)thing.GetType().GetField(field)!.GetValue(thing)!;

    private static void Render(string folder, Action<bool, string> check)
    {
        foreach (var (name, hasBall) in new[] { ("toy-basket-full", true), ("toy-basket-empty", false) })
        {
            var art = new ToyBasketArt { HasBall = hasBall };
            var host = new Grid { Width = 240, Height = 120, Background = new LinearGradientBrush(Color.FromRgb(0xFF, 0xF4, 0xE0), Color.FromRgb(0x2B, 0x2F, 0x3A), 0) };
            host.Children.Add(new Viewbox { Width = 204, Height = 168, Child = art, VerticalAlignment = VerticalAlignment.Bottom });
            host.Height = 180;
            host.Measure(new Size(240, 180)); host.Arrange(new Rect(0, 0, 240, 180)); host.UpdateLayout();
            var bitmap = new RenderTargetBitmap(240, 180, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(folder, name + ".png"));
            encoder.Save(stream);
        }
        check(File.Exists(Path.Combine(folder, "toy-basket-full.png")), "The basket is rendered for review");
    }

    private static VisualBrush Exact(Visual visual, double width, double height) =>
        new(visual) { Stretch = Stretch.None, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, width, height), AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };

    // The cat's window over the box's, where they stand on the screen (PET_RENDER_REVIEW only).
    private static void Snapshot(PetWindow window, Window box, string name)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not { Length: > 0 } folder) return;
        Call(window, "Present", false);
        var renderer = Field<PetRenderer>(window, "renderer");
        renderer.UpdateLayout();
        double left = Math.Min(window.Left, box.Left) - 10, top = Math.Min(window.Top, box.Top) - 10;
        int width = (int)(Math.Max(window.Left + window.Width, box.Left + box.Width) - left + 10), height = (int)(Math.Max(window.Top + window.Height, box.Top + box.Height) - top + 10);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x7A, 0x9C, 0xB8)), null, new Rect(0, 0, width, height));
            dc.DrawRectangle(Exact((Visual)box.Content, box.Width, box.Height), null, new Rect(box.Left - left, box.Top - top, box.Width, box.Height));
            dc.DrawRectangle(Exact(renderer, window.Width, window.Height), null, new Rect(window.Left - left, window.Top - top, window.Width, window.Height));
        }
        var bitmap = new RenderTargetBitmap(width * 2, height * 2, 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }

    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        void FeatureStep(double dt) => Call(window, "TickFeatures", dt);
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is { Length: > 0 } review) Render(review, check);
        window.Reset();
        preferences.Quiet = false; behavior.Quiet = false;
        preferences.ToysSniffed = DesktopToyRules.All.ToList(); // The first sniff (M5) is checked on its own in RunReact.

        // The basket.
        Tick(window);
        check(!window.ToyBasketShown && window.ToyBasketWindow == null, "No basket until it is asked for");
        check(window.ToyBasketMenuLabel == "Toy basket" && window.ToyBasketMenuEnabled, "The menu offers the basket");
        var play = window.ContextMenu!.Items.OfType<MenuItem>().Single(item => (string)item.Header == "Play");
        check(play.Items.OfType<MenuItem>().Any(item => (string)item.Header == "Toy basket"), "It is in the Play group");
        window.ToggleToyBasket();
        Tick(window);
        check(window.ToyBasketShown && window.ToyBasketWindow?.IsVisible == true && preferences.ToyBasketShown, "The basket comes out and is remembered");
        check(window.ToyBasketMenuLabel == "Quick tidy", "The same row now tidies up");
        var basketArt = Field<ToyBasketArt?>(window, "toyBasketArt")!;
        check(basketArt.HasBall, "The ball is in the basket");
        var row = window.ToyBasketMenuItems().First();
        check((string)row.Header == "Yarn ball" && row.IsEnabled && !row.IsChecked, "The list shows the ball");

        // Out of the basket.
        row.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(window.ToyIsOut(Ball), "Choosing it takes it out");
        Tick(window);
        var motion = window.ToyMotionOf(Ball)!;
        var toyWindow = window.ToyWindowOf(Ball)!;
        check(toyWindow.IsVisible && !basketArt.HasBall, "It is on the desktop, no longer in the basket");
        check(!window.TakeToyOut(Ball), "A toy is out only once");
        Settle(window, motion);
        check(motion.AtRest && preferences.ToysOut is { Count: 1 } && preferences.ToysOut[0].Id == Ball, "Once at rest it is saved");
        var area = GetThing<Area>(Thing(window, Ball), "Area");
        check(Math.Abs(toyWindow.Top - (area.Bottom - 40)) < 1.5 && Math.Abs(toyWindow.Left - (motion.X - 28)) < 1.5, "Its window lies on the floor at the ball");
        check(window.ToyBasketMenuItems().First().IsChecked, "The list ticks what is out");
        check(!window.ToysAboveCat, "The cat stays in front of the toys");

        // Resting costs no window moves.
        double left = toyWindow.Left;
        var placed = Thing(window, Ball);
        Tick(window, 0.2);
        check(toyWindow.Left == left && GetThing<bool>(placed, "Placed"), "A toy at rest stays put");

        // Left alone by the cat, asleep or awake.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        double before = pet.Emotions.Time;
        Call(window, "StartToyDrag", placed);
        check(window.ToyHeld(Ball), "A toy can be held while the cat sleeps");
        motion.X = pet.X + PetSettings.CenterX; motion.Height = 300;
        Call(window, "EndToyDrag", placed, false);
        Settle(window, motion);
        check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && pet.Bonk == 0, "Dropped on the sleeping cat: it sleeps on, no bonk");
        double x = motion.X;
        Call(window, "EndToyDrag", placed, true);
        check(motion.X == x, "Letting go of nothing does nothing");
        behavior.WakeByClick(pet); pet.Action = PetAction.Idle; pet.Emotions.Reset();
        var mood = pet.Emotions.Current;
        Call(window, "StartToyDrag", placed);
        motion.X = pet.X + PetSettings.CenterX; motion.Height = 300;
        Call(window, "EndToyDrag", placed, false);
        Settle(window, motion);
        check(pet.Bonk == 0 && pet.Emotions.Current == mood && pet.Action == PetAction.Idle, "Dropped on the awake cat: no bonk, no reaction");

        // A throw carries the speed of the hand.
        Call(window, "StartToyDrag", placed);
        check(motion.VX == 0 && motion.VY == 0, "Picked up: it stops");
        var tracker = GetThing<ThrowTracker>(placed, "Tracker");
        tracker.Clear(); tracker.Add(1000, 100, 100); tracker.Add(1000.05, 130, 90);
        motion.X = 600; motion.Height = 100;
        Call(window, "EndToyDrag", placed, true);
        check(window.ToyIsOut(Ball) && motion.VX > 0 && motion.VY > 0, "Let go: it flies with the hand's speed");
        Settle(window, motion);

        // Pause holds it.
        motion.VX = 100; motion.VY = 100; motion.Height = 50;
        Tick(window);
        window.SetPaused(true);
        Tick(window);
        double frozenX = motion.X, frozenH = motion.Height;
        Tick(window, 0.2);
        check(motion.X == frozenX && motion.Height == frozenH, "Pause holds a toy in the air");
        Call(window, "StartToyDrag", placed);
        Tick(window);
        check(!window.ToyHeld(Ball), "Pause lets go of a held toy");
        check(!window.ToyBasketMenuEnabled, "Pause greys the basket's row");
        window.SetPaused(false);
        Settle(window, motion);

        // Quiet does not get in the way.
        preferences.Quiet = true;
        Call(window, "StartToyDrag", placed);
        check(window.ToyHeld(Ball), "Quiet still lets the user hold a toy");
        Call(window, "EndToyDrag", placed, false);
        preferences.Quiet = false;
        Settle(window, motion);

        // Fullscreen hides everything and keeps its place.
        double place = motion.X;
        Context(window, true);
        Tick(window);
        check(toyWindow.IsVisible == false && window.ToyBasketWindow!.IsVisible == false && !window.ToyBasketMenuEnabled, "A fullscreen app hides the basket and the toys");
        Context(window, false);
        Tick(window);
        check(toyWindow.IsVisible && window.ToyBasketWindow!.IsVisible && motion.X == place, "...and they come back where they were");

        // The game takes the ball from where it lies, and leaves it where it ends.
        window.Reset();
        motion.X = place;
        window.ToggleToy();
        Tick(window);
        var gameBall = Field<ToyBall>(window, "ball");
        var bounds = Field<Area>(window, "bounds");
        check(Field<Window?>(window, "toyWindow") != null && !toyWindow.IsVisible, "The game takes over the ball lying out");
        check(Math.Abs(gameBall.X - Math.Clamp(place, bounds.Left + 12, bounds.Right - 12)) < 0.01, "...from where it lies");
        check(window.ToyBasketMenuItems().First().IsEnabled == false && ((string)window.ToyBasketMenuItems().First().Header).Contains("in play"), "The list says it is in play");
        gameBall.X = 700; gameBall.Height = 0; gameBall.VX = 0; gameBall.VY = 0;
        Call(window, "CancelFeatureActivity");
        Tick(window);
        check(Field<Window?>(window, "toyWindow") == null && window.ToyIsOut(Ball) && toyWindow.IsVisible && Math.Abs(motion.X - 700) < 0.5, "Stopped: it lies where the game left it");

        window.ToggleToy();
        Tick(window);
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        gameBall.X = 640; gameBall.Height = 0; gameBall.VX = 0; gameBall.VY = 0;
        FeatureStep(0.1);
        check(Field<Window?>(window, "toyWindow") == null && pet.Emotions.SleepLocked && window.ToyIsOut(Ball), "Sleep stops the game; the ball stays out");
        behavior.WakeByClick(pet); pet.Action = PetAction.Idle; pet.Emotions.Reset();
        Tick(window);

        // Another monitor than the cat's: the ball comes to the cat.
        window.Reset();
        var thing = Thing(window, Ball);
        SetThing(thing, "Monitor", "\\\\.\\ELSEWHERE");
        var catBall = pet.X + 145;
        window.ToggleToy();
        Tick(window);
        check(Math.Abs(gameBall.X - Math.Clamp(catBall, bounds.Left + 12, bounds.Right - 12)) < 0.01, "On another monitor the game starts beside the cat");
        gameBall.VX = 0; gameBall.VY = 0;
        Call(window, "CancelFeatureActivity");
        Tick(window);
        check(GetThing<string>(Thing(window, Ball), "Monitor") != "\\\\.\\ELSEWHERE" && window.ToyIsOut(Ball), "...and stays on the cat's monitor");

        // "Put ball away" during that game puts it in the basket.
        window.Reset();
        window.ToggleToy();
        Tick(window);
        window.ToggleToy();
        check(Field<Window?>(window, "toyWindow") == null && !window.ToyIsOut(Ball), "\"Put ball away\" puts a ball that lay out back in the basket");
        Tick(window);

        // The game out of the basket is what it always was.
        window.Reset();
        window.ToggleToy();
        Tick(window);
        check(!basketArt.HasBall, "In the game it is not in the basket");
        Call(window, "CancelFeatureActivity");
        Tick(window);
        check(!window.ToyIsOut(Ball) && basketArt.HasBall, "A ball from the basket goes back to it");

        // Over the basket.
        window.TakeToyOut(Ball);
        Tick(window);
        motion = window.ToyMotionOf(Ball)!;
        Settle(window, motion);
        placed = Thing(window, Ball);
        Call(window, "StartToyDrag", placed);
        motion.X = window.ToyBasketWindow!.Left + ToyBasketArt.ArtWidth / 2; motion.Height = 20;
        Call(window, "EndToyDrag", placed, true);
        check(!window.ToyIsOut(Ball) && preferences.ToysOut == null, "Let go over the basket: it is put away");

        // Monitors: DPI and unplugging, with made-up monitors.
        window.TakeToyOut(Ball);
        Tick(window);
        motion = window.ToyMotionOf(Ball)!;
        Settle(window, motion);
        placed = Thing(window, Ball);
        string name = GetThing<string>(placed, "Monitor");
        var work = GetThing<Area>(placed, "Area");
        double oldScale = GetThing<double>(placed, "Scale");
        FarmMonitor Monitor(string id, double scale, bool primary, double leftPixels = 0)
        {
            var pixels = new Area(leftPixels, 0, leftPixels + (work.Right - work.Left) * oldScale, (work.Bottom - work.Top) * oldScale);
            return new FarmMonitor(id, pixels, pixels, scale, primary);
        }
        double along = DesktopToyRules.Fraction(motion.X, DesktopToyRules.BallHalfWidth, work);
        SetThing(placed, "GrabX", 10.0);
        tracker = GetThing<ThrowTracker>(placed, "Tracker");
        tracker.Add(5, 1, 1); tracker.Add(5.05, 40, 1);
        double xAtOld = motion.X;
        SetField(window, "farmMonitorsProbe", new Func<IReadOnlyList<FarmMonitor>>(() => new[] { Monitor(name, oldScale * 2, true) }));
        Call(window, "RefreshToyPlace", placed);
        check(Math.Abs(motion.X - xAtOld / 2) < 0.6 && GetThing<double>(placed, "GrabX") == 5 && tracker.Velocity(2200) == (0, 0), "A new DPI keeps position and grab in step and drops old throw samples");
        SetField(window, "farmMonitorsProbe", new Func<IReadOnlyList<FarmMonitor>>(() => new[] { Monitor("MAIN", oldScale, true), Monitor("SIDE", oldScale, false, 5000) }));
        Call(window, "RefreshToyPlace", placed);
        var moved = GetThing<Area>(placed, "Area");
        check(GetThing<string>(placed, "Monitor") == "MAIN" && Math.Abs(DesktopToyRules.Fraction(motion.X, DesktopToyRules.BallHalfWidth, moved) - along) < 0.01, "Its monitor unplugged: the main one, at the same place across");
        SetField(window, "farmMonitorsProbe", null);
        Call(window, "RefreshToyPlace", placed);
        Tick(window, 1.1);

        // Quick tidy; the battle hides them; the cat is left alone.
        double catX = pet.X;
        window.ToggleToy();
        Tick(window);
        window.ToggleToyBasket();
        check(!window.ToyBasketShown && !window.ToyIsOut(Ball) && Field<Window?>(window, "toyWindow") == null, "Quick tidy puts everything away and stops the game");
        check(!preferences.ToyBasketShown && preferences.ToysOut == null && pet.X == catX, "...remembered, with the cat still there");
        check(window.ToyBasketMenuLabel == "Toy basket", "The menu offers the basket again");
        RunBox(window, pet, behavior, preferences, check);
        RunFetch(window, pet, behavior, preferences, check);
        RunWand(window, pet, behavior, preferences, check);
        RunMouse(window, pet, behavior, preferences, check);
        RunPass(window, pet, behavior, preferences, check);
        RunReact(window, pet, behavior, preferences, check);
        preferences.ToysSniffed = null;
        preferences.ToyBasketMonitor = null; preferences.ToyBasketFractionX = null;
        window.Reset();
    }

    // M2: the cardboard box, and the cat napping in it as an ordinary sleep.
    private static void RunBox(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        void FeatureStep(double dt) => Call(window, "TickFeatures", dt);
        var renderer = Field<PetRenderer>(window, "renderer");
        double FrontDrop() { Call(window, "Present", false); return renderer.BoxFrontDrop; }
        window.Reset(); pet.Emotions.FinishSleep(0);
        window.ToggleToyBasket();
        Tick(window);
        var rows = window.ToyBasketMenuItems();
        check(rows.Length == 4 && (string)rows[1].Header == "Cardboard box" && rows[1].IsEnabled && !rows[1].IsChecked, "The basket lists the cardboard box");
        check((string)rows[2].Header == "Feather wand" && (string)rows[3].Header == "Wind-up mouse", "...and the feather wand and the wind-up mouse (M4)");
        check(window.TakeToyOut(Box), "It comes out of the basket");
        Tick(window);
        var motion = window.ToyMotionOf(Box)!;
        var boxWindow = window.ToyWindowOf(Box)!;
        Settle(window, motion);
        var thing = Thing(window, Box);
        var area = GetThing<Area>(thing, "Area");
        check(boxWindow.IsVisible && Math.Abs(boxWindow.Top - (area.Bottom - ToyBoxArt.ArtHeight)) < 1.5 && Math.Abs(boxWindow.Left - (motion.X - ToyBoxArt.ArtWidth / 2)) < 1.5,
            "It stands on the floor");
        check(preferences.ToysOut!.Any(toy => toy.Id == Box) && GetThing<double>(thing, "HalfWidth") == DesktopToyRules.BoxHalfWidth, "...and is saved, about 100 DIPs wide");
        check(!window.ToysAboveCat, "The cat stays in front of the box");

        // A click shakes it; a throw lands low and short, never turning.
        double x = motion.X;
        SetThing(thing, "Press", true);
        Call(window, "EndToyDrag", thing, true);
        check(motion.Feedback > 0 && motion.X == x && motion.AtRest, "A click shakes the box where it stands");
        Tick(window, 0.5);
        check(motion.Feedback == 0 && motion.X == x, "...and it settles");
        Call(window, "StartToyDrag", thing);
        var tracker = GetThing<ThrowTracker>(thing, "Tracker");
        tracker.Clear(); tracker.Add(1000, 100, 100); tracker.Add(1000.05, 130, 90);
        motion.X = (area.Left + area.Right) / 2; motion.Height = 150;
        Call(window, "EndToyDrag", thing, true);
        check(motion.VX > 0 && motion.VY > 0, "Let go: it flies with the hand's speed");
        double peak = 0; bool landed = false;
        for (int i = 0; i < 900 && !motion.AtRest; i++)
        {
            Tick(window);
            if (motion.Height == 0) landed = true; else if (landed) peak = Math.Max(peak, motion.Height);
            if (motion.Rotation != 0) { check(false, "A box never turns"); break; }
        }
        check(motion.AtRest && peak < 20, $"It lands with a low bounce ({peak:F1} DIPs) and stops");
        Tick(window, 1.1); // Off the land, if it fell on one.
        Settle(window, motion);

        // The cat on the same floor, 300 DIPs away.
        var bounds = Field<Area>(window, "bounds");
        double CatCentre() => pet.X + PetSettings.CenterX;
        void Away(double distance)
        {
            double side = motion.X - bounds.Left > distance + 120 ? -1 : 1;
            pet.X = motion.X + side * distance - PetSettings.CenterX; pet.Action = PetAction.Idle; pet.VX = 0;
        }
        void Arrive() { pet.X = motion.X - PetSettings.CenterX; FeatureStep(1 / 60.0); Tick(window, 0.1); }
        Away(300);
        preferences.Quiet = true; behavior.Quiet = true;
        check(!window.GoNapInBox(), "In Quiet the cat does not go to the box on its own");
        preferences.Quiet = false; behavior.Quiet = false;
        check(window.GoNapInBox() && window.GoingToBox, "With the box out the cat may go to nap in it");
        FeatureStep(1 / 60.0);
        check(pet.Action == PetAction.Walking, "It walks to the box");
        SetThing(thing, "Held", true);
        FeatureStep(1 / 60.0);
        check(!window.GoingToBox && pet.Action != PetAction.Walking, "The box picked up on the way: the trip is off");
        SetThing(thing, "Held", false);
        check(!window.BoxNear && !window.SleepInBoxNow(), "Far from the box \"Sleep in the box\" does nothing (greyed out)");

        check(window.GoNapInBox(), "Off again");
        Arrive();
        check(window.NappingInBox && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && window.BoxLift > PetWindow.BoxFloor + 5,
            $"In the box it falls asleep, after a little hop in (lift {window.BoxLift:F1})");
        Tick(window, 0.4);
        check(window.BoxLift == PetWindow.BoxFloor && FrontDrop() == PetWindow.BoxFloor, "Asleep in it, behind its front panel drawn by the cat's window");
        Snapshot(window, boxWindow, "toy-box-asleep");
        check(!window.StowToy(Box) && window.ToyBasketMenuItems()[1] is { IsEnabled: false } asleepRow && ((string)asleepRow.Header).Contains("asleep"), "The box cannot be put away while the cat is in it");
        double sleptBefore = pet.Emotions.SleepElapsed;
        window.SetPaused(true);
        Tick(window, 0.5);
        check(window.NappingInBox && pet.Emotions.SleepElapsed == sleptBefore, "Pause holds the nap in the box");
        window.SetPaused(false);

        // Woken (a click): wakes in the box, then hops out of it.
        behavior.WakeByClick(pet);
        Tick(window, 0.1);
        check(pet.Action == PetAction.Waking && window.NappingInBox && window.BoxFrontShown, "Woken, it wakes up in the box");
        Snapshot(window, boxWindow, "toy-box-waking");
        pet.Action = PetAction.Idle;
        Tick(window);
        check(!window.NappingInBox && window.BoxFrontShown && window.BoxLift > 0, "Awake, it hops out");
        Tick(window, 0.1);
        Snapshot(window, boxWindow, "toy-box-hop-out");
        Tick(window, 0.4);
        check(!window.BoxFrontShown && window.BoxLift == 0 && FrontDrop() < 0, "...and stands in front of the box");
        Snapshot(window, boxWindow, "toy-box-out");
        pet.Emotions.FinishSleep(0);

        // "Do now → Sleep in the box": close by, even in Quiet and in the wake cooldown.
        Away(150);
        preferences.Quiet = true; behavior.Quiet = true;
        check(window.BoxNear && window.SleepInBoxNow() && window.GoingToBox, "Close to it \"Sleep in the box\" works even in Quiet");
        Arrive();
        check(window.NappingInBox && pet.Action == PetAction.Sleeping, "...and the cat sleeps in it");
        preferences.Quiet = false; behavior.Quiet = false;

        // Picked up: straight out of the box, no hop.
        SetField(window, "dragging", true); pet.Action = PetAction.Dragging; pet.Grounded = false;
        Tick(window);
        check(!window.NappingInBox && !window.BoxFrontShown && window.BoxLift == 0 && FrontDrop() < 0, "Picked up, it is lifted straight out");
        SetField(window, "dragging", false); window.Reset(); pet.Emotions.FinishSleep(0);
        Tick(window);

        // Quick tidy with the cat in the box: the box goes, the cat sleeps on.
        Away(100);
        check(window.SleepInBoxNow(), "Back to the box");
        Arrive();
        check(window.NappingInBox, "...asleep in it");
        window.ToggleToyBasket();
        Tick(window);
        check(!window.ToyBasketShown && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && !window.NappingInBox && window.BoxLift == 0,
            "Quick tidy: the box goes, the cat sleeps on where it is");
        behavior.WakeByClick(pet); pet.Action = PetAction.Idle; pet.Emotions.FinishSleep(0);

        // A fullscreen app hides the box: the cat sleeps on there too.
        window.ToggleToyBasket();
        window.TakeToyOut(Box);
        Tick(window);
        motion = window.ToyMotionOf(Box)!;
        Settle(window, motion); Tick(window, 1.1); Settle(window, motion);
        thing = Thing(window, Box);
        Away(100);
        check(window.SleepInBoxNow(), "Into the box again");
        Arrive();
        Context(window, true);
        Tick(window);
        check(pet.Action == PetAction.Sleeping && !window.NappingInBox && !window.ToyWindowOf(Box)!.IsVisible, "A fullscreen app hides the box; the cat sleeps on");
        Context(window, false);
        behavior.WakeByClick(pet); pet.Action = PetAction.Idle; pet.Emotions.FinishSleep(0);
        Tick(window, 0.5);
        window.ToggleToyBasket();
        check(!window.ToyBasketShown, "Tidied away");
    }

    // M3: fetch with the yarn ball, and the cat bringing it on its own.
    private static void RunFetch(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        void FeatureStep(double dt = 1 / 60.0) => Call(window, "TickFeatures", dt);
        void Frame() { Tick(window); FeatureStep(); }
        var renderer = Field<PetRenderer>(window, "renderer");
        double scale = Field<double>(window, "scale");
        var bounds = Field<Area>(window, "bounds");
        double cursorX = bounds.Left + 300;
        SetField(window, "cursorProbe", new Func<Point>(() => new Point(cursorX * scale, (bounds.Bottom - 200) * scale)));
        window.Reset(); pet.Emotions.FinishSleep(0); pet.Emotions.Reset();
        window.FetchStreak.Step(1000); window.FetchStreak.Break();
        window.ToggleToyBasket();
        Tick(window);
        check(window.TakeToyOut(Ball), "The ball comes out");
        Tick(window);
        var motion = window.ToyMotionOf(Ball)!;
        var ballWindow = window.ToyWindowOf(Ball)!;
        Settle(window, motion);
        var thing = Thing(window, Ball);
        var tracker = GetThing<ThrowTracker>(thing, "Tracker");
        double CatCentre() => pet.X + PetSettings.CenterX;
        void Throw(double fromX, double speed)
        {
            Call(window, "StartToyDrag", thing);
            tracker.Clear(); tracker.Add(1000, 100, 100); tracker.Add(1000.05, 100 + speed * 0.05, 100);
            motion.X = fromX; motion.Height = 120;
            Call(window, "EndToyDrag", thing, true);
        }
        bool Until(Func<bool> done, int frames = 900) { for (int i = 0; i < frames && !done(); i++) Frame(); return done(); }
        void Free() { pet.Action = PetAction.Idle; pet.VX = 0; }
        double start = (bounds.Left + bounds.Right) / 2;
        pet.X = start - PetSettings.CenterX; Free();

        // A soft let-go is no throw.
        Throw(start + 100, 200);
        check(!window.Fetching, "A gentle toss: the cat does not fetch");
        Settle(window, motion);

        // A throw: watch, run, carry to the cursor, drop, wait.
        Throw(start + 50, 900);
        check(window.FetchPhase == "Watch" && pet.Action == PetAction.Looking && pet.Facing == 1, "A hard throw: the cat watches the ball");
        check((bool)Call(window, "FeatureOwnsMovement")!, "...and nothing else of its own starts meanwhile (emotions and naps wait)");
        Frame();
        check(window.FetchPhase == "Watch", "...while it is in the air");
        check(Until(() => window.FetchPhase == "Run"), "Once it has landed and slowed down, the cat goes for it");
        Frame();
        var settings = Field<PetSettings>(window, "settings");
        check(pet.Action == PetAction.Walking && Math.Abs(pet.VX) > settings.WalkSpeed * 1.2, "...at a run");
        pet.Facing = motion.X < CatCentre() ? -1 : 1;
        pet.X = motion.X - PetSettings.CenterX - pet.Facing * PetWindow.FetchMouthAhead;
        Frame();
        check(window.CarryingBall && !ballWindow.IsVisible, "At the ball it takes it in its mouth; the ball's window hides");
        Call(window, "Present", false);
        check(renderer.CarryBall, "The cat's window draws the ball in its mouth");
        // Review: the ball in the mouth through the poses (PET_RENDER_REVIEW only).
        int facingNow = pet.Facing;
        foreach (var (action, facing) in new[] { (PetAction.Walking, 1), (PetAction.Walking, -1), (PetAction.Sitting, 1), (PetAction.Looking, -1), (PetAction.Idle, 1) })
        {
            pet.Action = action; pet.Facing = facing;
            Snapshot(window, ballWindow, $"fetch-carry-{action.ToString().ToLowerInvariant()}-{(facing > 0 ? "right" : "left")}");
        }
        pet.Facing = facingNow; pet.Action = PetAction.Walking;
        check(window.ToyBasketMenuItems()[0] is { IsEnabled: false } row && ((string)row.Header).Contains("mouth"), "The basket's ball row is greyed: in the cat's mouth");
        Frame();
        check(pet.Action == PetAction.Walking && pet.Facing == Math.Sign(cursorX - CatCentre()), "It carries the ball towards the cursor");
        pet.X = cursorX - PetSettings.CenterX + 2;
        Frame();
        check(window.FetchPhase == "Wait" && !window.CarryingBall && pet.Action == PetAction.Sitting, "Under the cursor it drops the ball and sits waiting");
        Tick(window);
        check(ballWindow.IsVisible && motion.Height > 20 && Math.Abs(motion.X - (CatCentre() + pet.Facing * PetWindow.FetchMouthAhead)) < 2, "The ball falls from its mouth");
        Snapshot(window, ballWindow, "fetch-drop");
        Call(window, "Present", false);
        check(!renderer.CarryBall, "...and is no longer drawn on the cat");
        Settle(window, motion);
        check(preferences.ToysOut!.Single(t => t.Id == Ball) is var saved && Math.Abs(DesktopToyRules.FromFraction(saved.FractionX, 12, GetThing<Area>(thing, "Area")) - motion.X) < 1,
            "Once still it is saved where it lies");
        check(window.FetchStreak.InARow == 1, "One fetch counted"); check(window.FetchPhase == "Wait", "...and it still waits");

        // Thrown again while it waits: another round, even in Quiet and the wake cooldown.
        preferences.Quiet = true; behavior.Quiet = true;
        pet.Emotions.FinishSleep(30);
        Throw(start - 200, 900);
        check(window.FetchPhase == "Watch", "Thrown again while it waits, in Quiet and the wake cooldown: it watches again");
        check(Until(() => window.FetchPhase == "Run"), "...and goes for it");
        // Picked up on the way: the trip is off, nothing more.
        Call(window, "StartToyDrag", thing);
        Frame();
        check(!window.Fetching && pet.Action != PetAction.Walking, "The ball picked up while the cat runs: the trip is off");
        Call(window, "EndToyDrag", thing, false);
        Settle(window, motion);
        check(!window.InviteToFetch(), "In Quiet the cat does not bring the ball on its own");
        preferences.Quiet = false; behavior.Quiet = false;
        check(!window.InviteToFetch(), "...nor during the wake cooldown");
        pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();

        // A click on the cat (left or right) while it carries the ball: it falls from the mouth.
        Throw(start, 900);
        check(Until(() => window.FetchPhase == "Run"), "Another throw");
        pet.Facing = motion.X < CatCentre() ? -1 : 1;
        pet.X = motion.X - PetSettings.CenterX - pet.Facing * PetWindow.FetchMouthAhead;
        Frame();
        check(window.CarryingBall, "In the mouth again");
        Call(window, "CancelFeatureActivity"); // What a press on the cat or its right-click does first.
        Tick(window);
        check(!window.Fetching && ballWindow.IsVisible && motion.Height > 20, "A click on the cat: it lets the ball fall");
        Settle(window, motion);

        // Five in a row, then two minutes of only watching.
        window.FetchStreak.Break();
        for (int i = 0; i < FetchStreak.MaxInARow; i++) window.FetchStreak.TryFetch();
        Free();
        Throw(start + 50, 900);
        check(window.FetchPhase == "Watch", "After five in a row it still watches the throw");
        bool walked = false;
        for (int i = 0; i < 900 && window.Fetching; i++) { Frame(); walked |= pet.Action == PetAction.Walking; }
        check(!walked && !window.Fetching, "...but rests: no run for two minutes");
        window.FetchStreak.Step(121);
        Settle(window, motion);

        // Asleep: it sleeps on.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        Throw(start + 50, 900);
        check(!window.Fetching && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "A throw does not wake the sleeping cat");
        Settle(window, motion);
        behavior.WakeByClick(pet); pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();

        // Pause holds a trip.
        Throw(start + 50, 900);
        Until(() => window.FetchPhase == "Run");
        window.SetPaused(true);
        double frozen = pet.X;
        for (int i = 0; i < 20; i++) Tick(window);
        check(window.FetchPhase == "Run" && pet.X == frozen, "Pause holds the trip");
        window.SetPaused(false);

        // A fullscreen app while it carries: dropped where it is, then hidden.
        pet.Facing = motion.X < CatCentre() ? -1 : 1;
        pet.X = motion.X - PetSettings.CenterX - pet.Facing * PetWindow.FetchMouthAhead;
        Frame();
        check(window.CarryingBall, "Carrying it once more");
        Context(window, true);
        Tick(window);
        check(!window.Fetching && !ballWindow.IsVisible && motion.Height > 0, "A fullscreen app: the ball falls from the mouth and hides");
        Context(window, false);
        Tick(window); Settle(window, motion);
        check(ballWindow.IsVisible && motion.AtRest, "...and is back on the floor afterwards");

        // The cat's own invitation: it walks over, brings it and waits; no throw, it goes off.
        Free();
        pet.X = Math.Clamp(motion.X + 250, bounds.Left, bounds.Right - settings.Width) - PetSettings.CenterX;
        check(window.InviteToFetch() && window.FetchingInvite, "Now and then it goes to bring the ball");
        Frame();
        check(pet.Action == PetAction.Walking && Math.Abs(Math.Abs(pet.VX) - settings.WalkSpeed * pet.Emotions.WalkScale) < 1, "...at a walk");
        pet.Facing = motion.X < CatCentre() ? -1 : 1;
        pet.X = motion.X - PetSettings.CenterX - pet.Facing * PetWindow.FetchMouthAhead;
        Frame();
        check(window.CarryingBall, "It takes it in its mouth");
        preferences.Quiet = true; behavior.Quiet = true;
        Frame();
        check(!window.Fetching && !window.CarryingBall, "Quiet turned on: its own invitation stops, the ball falls");
        preferences.Quiet = false; behavior.Quiet = false;
        Tick(window); Settle(window, motion); Free();
        check(window.InviteToFetch(), "Off again");
        pet.Facing = motion.X < CatCentre() ? -1 : 1;
        pet.X = motion.X - PetSettings.CenterX - pet.Facing * PetWindow.FetchMouthAhead;
        Frame();
        pet.X = cursorX - PetSettings.CenterX;
        Frame();
        check(window.FetchPhase == "Wait" && window.FetchingInvite, "It brings it to the cursor and waits");
        Settle(window, motion);
        Until(() => !window.Fetching, 60 * 21);
        check(!window.Fetching, "No throw in 20 seconds: it goes off");

        // Quick tidy while it carries: the ball goes back to the basket.
        Free();
        check(window.InviteToFetch(), "Once more");
        pet.Facing = motion.X < CatCentre() ? -1 : 1;
        pet.X = motion.X - PetSettings.CenterX - pet.Facing * PetWindow.FetchMouthAhead;
        Frame();
        check(window.CarryingBall, "In the mouth");
        window.ToggleToyBasket();
        Frame();
        Call(window, "Present", false);
        check(!window.ToyBasketShown && !window.Fetching && !renderer.CarryBall && !window.ToyIsOut(Ball), "Quick tidy: the trip ends, the ball is put away");
        SetField(window, "cursorProbe", null);
        window.Reset();
    }

    // M4: the feather wand. The cat chases the feather only while the user holds the wand.
    private static void RunWand(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        void FeatureStep(double dt = 1 / 60.0) => Call(window, "TickFeatures", dt);
        void Frame() { Tick(window); FeatureStep(); }
        bool Until(Func<bool> done, int frames = 900) { for (int i = 0; i < frames && !done(); i++) Frame(); return done(); }
        double scale = Field<double>(window, "scale");
        var bounds = Field<Area>(window, "bounds");
        var settings = Field<PetSettings>(window, "settings");
        Point cursor = new(bounds.Left + 300, bounds.Bottom - 150);
        SetField(window, "cursorProbe", new Func<Point>(() => new Point(cursor.X * scale, cursor.Y * scale)));
        window.Reset(); pet.Emotions.FinishSleep(0); pet.Emotions.Reset();
        window.ToyChaseStreak.Step(1000, false);
        window.ToggleToyBasket();
        Tick(window);
        check(window.TakeToyOut(DesktopToyRules.Wand), "The feather wand comes out of the basket");
        Tick(window);
        var motion = window.ToyMotionOf(DesktopToyRules.Wand)!;
        var wandWindow = window.ToyWindowOf(DesktopToyRules.Wand)!;
        Settle(window, motion);
        var thing = Thing(window, DesktopToyRules.Wand);
        var area = GetThing<Area>(thing, "Area");
        check(wandWindow.IsVisible && motion.Rotation == 0 && Math.Abs(wandWindow.Top - (area.Bottom - DesktopToyRules.WandHeight / 2 - ToyWandArt.CentreY)) < 1.5,
            "It lies flat on the floor, not rolling");
        check(preferences.ToysOut!.Any(toy => toy.Id == DesktopToyRules.Wand), "...and is saved");
        check(!window.ToysAboveCat, "The cat stays in front of it");
        double CatCentre() => pet.X + PetSettings.CenterX;
        void Free() { pet.Action = PetAction.Idle; pet.VX = 0; pet.Grounded = true; }
        var feather = window.FeatherOf(DesktopToyRules.Wand)!;
        void Hold() { Call(window, "StartToyDrag", thing); SetThing(thing, "GrabX", 0.0); SetThing(thing, "GrabY", 0.0); }
        void LetGo() => Call(window, "EndToyDrag", thing, false);
        double middle = (bounds.Left + bounds.Right) / 2;
        pet.X = middle - PetSettings.CenterX; Free();
        Snapshot(window, wandWindow, "wand-lying");

        // Held, in Quiet and the wake cooldown: the feather hangs, the cat watches it.
        preferences.Quiet = true; behavior.Quiet = true;
        pet.Emotions.FinishSleep(30);
        cursor = new Point(middle + 250, bounds.Bottom - 150);
        Hold();
        Frame();
        check(feather.Ready && window.ChaseToyId == DesktopToyRules.Wand && window.ChasePhase == "Wand", "Held, in Quiet and the wake cooldown: the cat goes for the feather");
        check((bool)Call(window, "FeatureOwnsMovement")!, "...and nothing else of its own starts meanwhile (emotions and naps wait)");
        for (int i = 0; i < 120; i++) Frame();
        check(Math.Abs(feather.X - (cursor.X + ToyWandArt.HeldTipX)) < 3 && Math.Abs(feather.Y - (cursor.Y - ToyWandArt.HeldTipY + FeatherString.Length)) < 3, "The feather hangs on its string under the tip");
        Snapshot(window, wandWindow, "wand-held");
        bool walked = false;
        for (int i = 0; i < 90; i++) { cursor = new Point(middle + 250 + Math.Sin(i / 6.0) * 60, cursor.Y); Frame(); walked |= pet.Action == PetAction.Walking; }
        check(walked && pet.Facing == 1, "Swung about out of reach: the cat runs after it");
        Snapshot(window, wandWindow, "wand-swing");

        // Held still near the cat: it stalks and pounces with its normal jump; landing under it, it holds the feather.
        cursor = new Point(CatCentre() + 20 - ToyWandArt.HeldTipX, bounds.Bottom - 150);
        check(Until(() => window.ChasePhase == "Pounce", 600) && pet.Action == PetAction.Jumping && pet.VY < 0, "Still near the cat: it pounces");
        Free();
        Frame();
        check(window.ChasePhase == "Hold" && feather.Pinned, "Landing under it: the feather is in its paws");
        Snapshot(window, wandWindow, "wand-caught");
        check(Until(() => window.ChasePhase == "Wand", 60) && !feather.Pinned, "...for under a second, then it lets go");

        // Still and out of reach for 8 seconds: bored, it only looks.
        cursor = new Point(middle + 300, bounds.Bottom - 150);
        bool bored = Until(() => window.ChaseBored, 60 * 14);
        check(bored && pet.Action == PetAction.Looking, $"Held still for 8 seconds: the cat loses interest and watches ({window.ChasePhase}, {feather.Speed:0}, {pet.Action})");
        for (int i = 0; i < 20; i++) { cursor = new Point(middle + 300 + (i % 2) * 40, cursor.Y); Frame(); }
        check(!window.ChaseBored && window.ChasePhase == "Wand", "It moves again: the chase goes on");

        // Pause holds it all.
        window.SetPaused(true);
        double fx = feather.X, catX = pet.X;
        for (int i = 0; i < 20; i++) Tick(window);
        check(feather.X == fx && pet.X == catX && window.ChasePhase == "Wand", "Pause holds the feather and the chase");
        window.SetPaused(false);

        // A click on the cat: the chase stops (the drag goes on as usual).
        Call(window, "CancelFeatureActivity");
        check(window.ChasePhase == "None", "A click on the cat stops the chase");
        LetGo();
        Frame();
        check(window.ChasePhase == "None" && !feather.Ready, "Let go, the wand drops and the feather lies by its tip");
        Settle(window, motion);
        check(motion.AtRest && motion.Rotation == 0, "...it lands flat");

        // Asleep: it sleeps on.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        Hold();
        for (int i = 0; i < 30; i++) Frame();
        check(window.ChasePhase == "None" && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "The wand held over a sleeping cat does not wake it");
        LetGo(); Settle(window, motion);
        behavior.WakeByClick(pet); pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();
        preferences.Quiet = false; behavior.Quiet = false;

        // About 90 seconds of chasing, then two minutes of only watching.
        window.ToyChaseStreak.Wand(ToyChaseStreak.WandSeconds + 1);
        Hold();
        Frame();
        check(window.ChasePhase == "Look", "Resting after 90 seconds: it only watches the feather");
        walked = false;
        for (int i = 0; i < 60; i++) { cursor = new Point(middle + 250 + Math.Sin(i / 6.0) * 60, cursor.Y); Frame(); walked |= pet.Action == PetAction.Walking; }
        check(!walked && pet.Action == PetAction.Looking, "...without running");
        window.ToyChaseStreak.Step(121, false);
        LetGo(); Frame(); Free();

        // A fullscreen app: the chase ends and the wand hides.
        Hold();
        Frame();
        check(window.ChasePhase == "Wand", "Held once more");
        Context(window, true);
        Tick(window);
        check(window.ChasePhase == "None" && !wandWindow.IsVisible, "A fullscreen app: the chase stops and the wand hides");
        Context(window, false);
        Tick(window); Settle(window, motion);
        check(wandWindow.IsVisible, "...and is back afterwards");
        window.ToggleToyBasket(); // Quick tidy.
        SetField(window, "cursorProbe", null);
        window.Reset();
    }

    // M4: the wind-up mouse. A click winds it; the cat chases it while it runs.
    private static void RunMouse(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        void FeatureStep(double dt = 1 / 60.0) => Call(window, "TickFeatures", dt);
        void Frame() { Tick(window); FeatureStep(); }
        bool Until(Func<bool> done, int frames = 900) { for (int i = 0; i < frames && !done(); i++) Frame(); return done(); }
        var bounds = Field<Area>(window, "bounds");
        var settings = Field<PetSettings>(window, "settings");
        double scale = Field<double>(window, "scale");
        SetField(window, "cursorProbe", new Func<Point>(() => new Point((bounds.Left + 200) * scale, (bounds.Bottom - 60) * scale)));
        window.Reset(); pet.Emotions.FinishSleep(0); pet.Emotions.Reset();
        window.ToyChaseStreak.Step(1000, false);
        window.ToggleToyBasket();
        Tick(window);
        check(window.TakeToyOut(DesktopToyRules.Mouse), "The wind-up mouse comes out of the basket");
        Tick(window);
        var motion = window.ToyMotionOf(DesktopToyRules.Mouse)!;
        var mouseWindow = window.ToyWindowOf(DesktopToyRules.Mouse)!;
        Settle(window, motion);
        var thing = Thing(window, DesktopToyRules.Mouse);
        var area = GetThing<Area>(thing, "Area");
        var windUp = window.WindUpOf(DesktopToyRules.Mouse)!;
        check(mouseWindow.IsVisible && !windUp.Busy && Math.Abs(mouseWindow.Top - (area.Bottom - ToyMouseArt.ArtHeight)) < 1.5, "It sits on the floor, still");
        Snapshot(window, mouseWindow, "mouse-still");
        double CatCentre() => pet.X + PetSettings.CenterX;
        void Free() { pet.Action = PetAction.Idle; pet.VX = 0; pet.Grounded = true; }
        void Click() { SetThing(thing, "Press", true); Call(window, "EndToyDrag", thing, true); }
        void Place(double x, int direction) { motion.X = x; windUp.Direction = direction; }
        double middle = (bounds.Left + bounds.Right) / 2;
        pet.X = middle - PetSettings.CenterX; Free();

        // A click winds it up; the cat runs after it, in Quiet too.
        preferences.Quiet = true; behavior.Quiet = true;
        Place(middle + 300, 1);
        Click();
        check(windUp.Running, "A click winds the mouse up");
        Frame();
        check(window.ChaseToyId == DesktopToyRules.Mouse && window.ChasePhase == "Run", "In Quiet too, the cat goes after the running mouse");
        Frame();
        check(pet.Action == PetAction.Walking && Math.Abs(pet.VX) > settings.WalkSpeed * 1.8 && pet.Facing == 1, "...at a run");
        Snapshot(window, mouseWindow, "mouse-run");
        Click();
        check(windUp.Winds == 1, "A click while it runs does nothing");

        // Close by: a pounce; landing on it, the cat pins it and the mouse stops.
        motion.X = CatCentre() + 60;
        Frame();
        check(window.ChasePhase == "Pounce" && pet.Action == PetAction.Jumping, "Close by, the cat pounces");
        Free(); motion.X = CatCentre() + 10;
        Frame();
        check(window.ChasePhase == "Hold" && !windUp.Running, "Landing on it: caught, the mouse stops");
        check(window.ToyChaseStreak.MiceInARow == 1, "One mouse counted");
        check(Until(() => window.ChasePhase == "None", 90) && pet.Action == PetAction.Sitting, "After about a second the cat sits");
        Settle(window, motion);
        check(preferences.ToysOut!.Any(toy => toy.Id == DesktopToyRules.Mouse), "Once still the mouse is saved where it lies");

        // A miss: the chase goes on; the mouse running out ends it.
        Free();
        Place(CatCentre() + 300, -1);
        Click();
        check(Until(() => window.ChasePhase == "Run", 5), "Wound again");
        motion.X = CatCentre() + 60;
        Frame();
        check(window.ChasePhase == "Pounce", "...a pounce");
        Free(); motion.X = CatCentre() + 200;
        Frame();
        check(window.ChasePhase == "Run" && windUp.Running, "A miss: it chases on");
        check(window.ReactPhase == "Miss", "...after a puzzled second (M5)");
        windUp.RunLeft = 0;
        check(Until(() => window.ChasePhase == "None", 120) && window.ReactPhase == "None", "The mouse stops: the chase ends");
        Settle(window, motion);

        // The same winding is chased once: a click on the cat ends it for good.
        Free();
        Place(CatCentre() + 300, 1);
        Click();
        Frame();
        check(window.ChasePhase == "Run", "Another round");
        Call(window, "CancelFeatureActivity");
        Frame(); Frame();
        check(window.ChasePhase == "None" && windUp.Running, "A click on the cat stops the chase; that run is not chased again");
        windUp.RunLeft = 0; Settle(window, motion);

        // Picked up while it runs: it stops, and so does the chase.
        Free();
        Place(CatCentre() + 300, 1);
        Click();
        Frame();
        Call(window, "StartToyDrag", thing);
        Frame();
        check(window.ChasePhase == "None" && !windUp.Running, "Picked up while it runs: the mouse and the chase stop");
        Call(window, "EndToyDrag", thing, false);
        Settle(window, motion);

        // Asleep: it sleeps on.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        Click();
        for (int i = 0; i < 20; i++) Frame();
        check(window.ChasePhase == "None" && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "A running mouse does not wake the sleeping cat");
        windUp.RunLeft = 0; Settle(window, motion);
        behavior.WakeByClick(pet); pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();
        preferences.Quiet = false; behavior.Quiet = false;

        // Five in a row, then two minutes of only watching.
        window.ToyChaseStreak.Step(1000, false);
        for (int i = 0; i < ToyChaseStreak.MouseInARow; i++) window.ToyChaseStreak.TryMouse();
        Place(CatCentre() + 300, 1);
        Click();
        Frame();
        check(window.ChasePhase == "Look", "After five in a row it only watches the mouse");
        bool walked = false;
        for (int i = 0; i < 30; i++) { Frame(); walked |= pet.Action == PetAction.Walking; }
        check(!walked, "...without running");
        windUp.RunLeft = 0; Frame();
        check(window.ChasePhase == "None", "...until it stops");
        window.ToyChaseStreak.Step(121, false);
        Settle(window, motion);

        // Three out: the fourth is greyed. Quick tidy puts everything away mid-chase.
        check(window.TakeToyOut(Ball) && window.TakeToyOut(DesktopToyRules.Wand), "The ball and the wand come out too");
        check(window.ToyBasketMenuItems()[1] is { IsEnabled: false } boxRow && ((string)boxRow.Header).Contains("3"), "Three out: the box's row is greyed");
        Free();
        Place(CatCentre() + 300, 1);
        Click();
        Frame();
        check(window.ChasePhase == "Run", "Running once more");
        window.ToggleToyBasket();
        Frame();
        check(!window.ToyBasketShown && window.ChasePhase == "None" && !window.ToyIsOut(DesktopToyRules.Mouse), "Quick tidy: the chase ends, everything is put away");
        SetField(window, "cursorProbe", null);
        window.Reset();
    }

    // M5: passing the yarn ball lying out.
    private static void RunPass(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        void FeatureStep(double dt = 1 / 60.0) => Call(window, "TickFeatures", dt);
        void Frame() { Tick(window); FeatureStep(); }
        bool Until(Func<bool> done, int frames = 900) { for (int i = 0; i < frames && !done(); i++) Frame(); return done(); }
        var bounds = Field<Area>(window, "bounds");
        var settings = Field<PetSettings>(window, "settings");
        double scale = Field<double>(window, "scale");
        double cursorX = bounds.Left + 200;
        SetField(window, "cursorProbe", new Func<Point>(() => new Point(cursorX * scale, (bounds.Bottom - 60) * scale)));
        window.Reset(); pet.Emotions.FinishSleep(0); pet.Emotions.Reset();
        window.ToyChaseStreak.Step(1000, false);
        preferences.ToyPassBest = 0; SetField(window, "passCount", null);
        window.ToggleToyBasket();
        Tick(window);
        check(window.TakeToyOut(Ball), "The yarn ball comes out for passing");
        Tick(window);
        var motion = window.ToyMotionOf(Ball)!;
        var ballWindow = window.ToyWindowOf(Ball)!;
        Settle(window, motion);
        var thing = Thing(window, Ball);
        double CatCentre() => pet.X + PetSettings.CenterX;
        void Free() { pet.Action = PetAction.Idle; pet.VX = 0; pet.Grounded = true; }
        void Roll(double from, double vx) { motion.X = CatCentre() + from; motion.Height = 0; motion.VY = 0; motion.VX = vx; Call(window, "PassTouched", thing); }
        double middle = (bounds.Left + bounds.Right) / 2;
        pet.X = middle - PetSettings.CenterX; Free();

        // A click on the ball rolls it away from the click: from its far side, toward the cat. In Quiet too.
        preferences.Quiet = true; behavior.Quiet = true;
        motion.X = CatCentre() + 150;
        cursorX = motion.X + 20;
        SetThing(thing, "Press", true); Call(window, "EndToyDrag", thing, true);
        check(window.PassCount.Touched && motion.VX < 0, "A click rolls the ball toward the cat and counts as the user's touch");
        Frame();
        check(window.PassPhase == "Ready", "In Quiet too, the cat gets ready for the ball");
        for (int i = 0; i < 12; i++) Frame();
        check(pet.PlayCrouch > 0 || pet.Action == PetAction.Walking, "...crouching, or stepping up to meet a ball that would stop short");
        Snapshot(window, ballWindow, "pass-ready");
        cursorX = bounds.Right - 200; // Behind the cat's other side now.
        bool batted = false;
        check(Until(() => { batted |= pet.BatRemaining > 0; return window.PassPhase == "Wait"; }, 300) && batted, "The ball reaches its paws: the cat bats it back");
        check(window.PassCount.Count == 1 && motion.VX > 0 && motion.VY > 0, "One pass: the ball goes back toward the cursor");
        double stops = ToyPassRules.StopsAt(motion.X, motion.VX);
        check(Math.Abs(motion.VX) <= ToyPassRules.MaxBat && stops > CatCentre(), "...as hard as it takes to get there, within limits");

        // A light roll back: a second pass, and the count shows by the ball.
        Roll(250, -280);
        check(Until(() => window.PassCount.Count == 2, 300) && window.PassLabel == "2", "The second pass shows its count by the ball");
        var passWindow = Field<Window?>(window, "passWindow");
        check(passWindow?.IsVisible == true && passWindow.IsHitTestVisible == false, "The label is a click-through overlay");
        // Rolling back on its own (no touch): the cat watches, no pass.
        motion.X = CatCentre() + 200; motion.VX = -250; motion.Height = 0;
        for (int i = 0; i < 120; i++) Frame();
        check(window.PassCount.Count == 2 && window.PassPhase == "Wait", "A roll the user did not make is no pass");
        check(Until(() => window.PassLabel == null, 200), "The count fades after a moment");
        // Six seconds without a touch: the streak ends; the record is kept and shown in the basket.
        Settle(window, motion);
        check(Until(() => window.PassPhase == "None", (int)(60 * (ToyPassRules.IdleSeconds + 1))) && preferences.ToyPassBest == 2, "Six seconds untouched: the streak ends, its record kept");
        var ballRow = window.ToyBasketMenuItems()[0];
        check(((string)ballRow.Header).Contains("best pass streak 2") && ballRow.IsEnabled, "The basket shows the record without greying the row");

        // Five in a row beats the record out loud.
        Free(); pet.X = middle - PetSettings.CenterX;
        for (int n = 0; n < 5; n++)
        {
            int before = window.PassCount.Count;
            Roll(220, -300);
            Until(() => window.PassCount.Count > before, 300);
        }
        check(window.PassCount.Count == 5 && preferences.ToyPassBest == 5, "Five passes in a row: the record goes up at once");
        Settle(window, motion);
        check(Until(() => window.PassPhase == "None", 60 * 8) && window.PassLabel == "New record!", "When it ends: \"New record!\"");

        // Held: the streak waits; a hard throw would be a fetch.
        Free();
        Roll(220, -300);
        Until(() => window.PassCount.Count == 1, 300);
        Call(window, "StartToyDrag", thing);
        Frame();
        check(window.PassPhase == "Wait" && pet.Action == PetAction.Looking, "The ball in the user's hand: the cat waits, looking");
        Call(window, "EndToyDrag", thing, false);
        Settle(window, motion);

        // A ball that rolls past it: the streak ends, and a puzzled second.
        Free();
        Roll(120, -300);
        Frame();
        check(window.PassPhase == "Ready", "A ball rolls in");
        motion.VX = -500; motion.Height = 80; motion.VY = 150; // Bouncing over its head, out of reach.
        check(Until(() => window.PassPhase == "None", 300) && window.ReactPhase == "Miss", "A ball that rolls past: the streak ends, the cat looks puzzled");
        for (int i = 0; i < 12; i++) Frame();
        Snapshot(window, ballWindow, "pass-miss");
        check(pet.ReactTilt != 0 || window.ReactPhase == "None", "...head on one side");
        check(Until(() => window.ReactPhase == "None", 120) && pet.ReactTilt == 0, "...for about a second");
        Settle(window, motion);

        // A click on the cat stops the streak.
        Free();
        Roll(220, -300);
        Frame();
        check(window.PassPhase == "Ready", "Another streak");
        Call(window, "CancelFeatureActivity");
        Frame();
        check(window.PassPhase == "None", "A click on the cat stops it");
        Settle(window, motion);

        // Asleep: it sleeps on.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        Roll(220, -300);
        for (int i = 0; i < 60; i++) Frame();
        check(window.PassPhase == "None" && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "A ball rolled to the sleeping cat does not wake it");
        behavior.WakeByClick(pet); pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();
        Settle(window, motion);
        preferences.Quiet = false; behavior.Quiet = false;

        // The rest is shared with the wand and the mouse.
        window.ToyChaseStreak.Step(1000, false);
        for (int i = 0; i < ToyChaseStreak.MouseInARow; i++) window.ToyChaseStreak.TryMouse();
        Free();
        Roll(220, -300);
        Frame();
        check(window.PassPhase == "Look", "Rested from chasing the mouse: it only watches the pass");
        bool moved = false;
        for (int i = 0; i < 60; i++) { Frame(); moved |= pet.Action == PetAction.Walking || pet.BatRemaining > 0; }
        check(!moved, "...without stepping up or batting");
        Settle(window, motion);
        Until(() => window.PassPhase == "None", 600);
        window.ToyChaseStreak.Step(1000, false);

        window.ToggleToyBasket();
        check(!window.ToyBasketShown && window.PassPhase == "None", "Quick tidy ends any streak");
        preferences.ToyPassBest = 0; SetField(window, "passCount", null);
        SetField(window, "cursorProbe", null);
        window.Reset();
    }

    // M5 reactions: the first sniff of a toy, and a new accessory. The miss is checked with the pass and the mouse.
    private static void RunReact(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        void FeatureStep(double dt = 1 / 60.0) => Call(window, "TickFeatures", dt);
        void Frame() { Tick(window); FeatureStep(); }
        bool Until(Func<bool> done, int frames = 900) { for (int i = 0; i < frames && !done(); i++) Frame(); return done(); }
        var bounds = Field<Area>(window, "bounds");
        var settings = Field<PetSettings>(window, "settings");
        double scale = Field<double>(window, "scale");
        SetField(window, "cursorProbe", new Func<Point>(() => new Point((bounds.Left + 200) * scale, (bounds.Bottom - 60) * scale)));
        window.Reset(); pet.Emotions.FinishSleep(0); pet.Emotions.Reset();
        window.ToyChaseStreak.Step(1000, false);
        double CatCentre() => pet.X + PetSettings.CenterX;
        void Free() { pet.Action = PetAction.Idle; pet.VX = 0; pet.Grounded = true; }
        double middle = (bounds.Left + bounds.Right) / 2;
        preferences.ToysSniffed = new() { Ball, Box, DesktopToyRules.Wand };
        window.ToggleToyBasket();
        Tick(window);
        check(window.TakeToyOut(DesktopToyRules.Mouse), "A toy the cat never met comes out");
        Tick(window);
        var motion = window.ToyMotionOf(DesktopToyRules.Mouse)!;
        Settle(window, motion);
        var mouseWindow = window.ToyWindowOf(DesktopToyRules.Mouse)!;
        var thing0 = Thing(window, DesktopToyRules.Mouse);
        pet.X = middle - PetSettings.CenterX; Free();
        motion.X = CatCentre() + 300;
        Tick(window);

        // Quiet: no visit of its own.
        preferences.Quiet = true; behavior.Quiet = true;
        SetField(window, "reactSniffCheck", 0.0);
        for (int i = 0; i < 30; i++) Frame();
        check(window.ReactPhase == "None" && !window.ToysSniffed.Contains(DesktopToyRules.Mouse), "In Quiet the cat does not go to sniff on its own");
        preferences.Quiet = false; behavior.Quiet = false;
        // Asleep: it sleeps on.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        SetField(window, "reactSniffCheck", 0.0);
        for (int i = 0; i < 30; i++) Frame();
        check(window.ReactPhase == "None" && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "A new toy does not wake the sleeping cat");
        behavior.WakeByClick(pet); pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();

        // Awake and free: it walks over slowly, sniffs, tries it with a paw, once.
        check(window.SniffNewToy() && window.ReactPhase == "SniffWalk" && window.SniffToyId == DesktopToyRules.Mouse, "Awake and free, the cat goes to sniff the new mouse");
        Frame();
        check(pet.Action == PetAction.Walking && Math.Abs(pet.VX) < settings.WalkSpeed * 0.75 && pet.Facing == 1, "...slowly");
        pet.X = motion.X - 40 - PetSettings.CenterX; // (No physics in these checks: the walk is put where it leads.)
        check(Until(() => window.ReactPhase == "Sniff", 30), "It gets there and sniffs");
        check(window.ToysSniffed.Contains(DesktopToyRules.Mouse) && preferences.ToysSniffed!.Contains(DesktopToyRules.Mouse), "...and remembers it has met the mouse");
        for (int i = 0; i < 30; i++) Frame();
        Call(window, "PositionDesktopThing", thing0);
        Snapshot(window, mouseWindow, "sniff");
        check(pet.PlayCrouch > 0.5 && pet.ReactDip > 4 && pet.Action == PetAction.Sitting, "Nose down at it");
        bool pawed = false;
        check(Until(() => { pawed |= pet.BatRemaining > 0; return window.ReactPhase == "None"; }, 120) && pawed, "...a paw tries it, then it is done");
        SetField(window, "reactSniffCheck", 0.0);
        for (int i = 0; i < 30; i++) Frame();
        check(window.ReactPhase == "None", "Each toy is sniffed once");

        // A first game with a toy it never met: a quick sniff, then the game.
        preferences.ToysSniffed = new() { Ball, Box, DesktopToyRules.Wand };
        var thing = Thing(window, DesktopToyRules.Mouse);
        var windUp = window.WindUpOf(DesktopToyRules.Mouse)!;
        Free();
        motion.X = CatCentre() + 300; windUp.Direction = 1;
        SetThing(thing, "Press", true); Call(window, "EndToyDrag", thing, true);
        Frame();
        check(window.ReactPhase == "Sniff" && window.ToysSniffed.Contains(DesktopToyRules.Mouse), "The first game with a new toy: a quick sniff first");
        check(Until(() => window.ChasePhase == "Run", 60), "...then the chase");
        Call(window, "CancelFeatureActivity");
        windUp.RunLeft = 0; Settle(window, motion);

        // A new accessory: a look, a head shake, a paw over the ears. In Quiet too; never asleep.
        preferences.Quiet = true; behavior.Quiet = true;
        Free();
        Call(window, "ReactToAccessory");
        check(window.ReactPhase == "Accessory" && pet.Action == PetAction.Looking, "A new accessory: the cat looks itself over, in Quiet too");
        Frame(); Frame(); Frame();
        check(pet.ReactTilt != 0, "...shaking its head");
        check(Until(() => pet.Action == PetAction.Grooming, 60), "...then a paw over its ears");
        for (int i = 0; i < 30; i++) Frame();
        Snapshot(window, mouseWindow, "accessory");
        check(Until(() => window.ReactPhase == "None", 120) && pet.ReactTilt == 0, "About two seconds in all");
        Free();
        Call(window, "ReactToAccessory");
        Frame();
        Call(window, "CancelFeatureActivity");
        check(window.ReactPhase == "None" && pet.ReactTilt == 0, "A click on the cat ends a reaction at once");
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        Call(window, "ReactToAccessory");
        for (int i = 0; i < 10; i++) Frame();
        check(window.ReactPhase == "None" && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "Asleep, a new accessory does not wake it");
        behavior.WakeByClick(pet); pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();
        preferences.Quiet = false; behavior.Quiet = false;

        preferences.ToysSniffed = DesktopToyRules.All.ToList();
        window.ToggleToyBasket();
        SetField(window, "cursorProbe", null);
        window.Reset();
    }
}
