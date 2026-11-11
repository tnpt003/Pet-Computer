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
        check(rows.Length == 2 && (string)rows[1].Header == "Cardboard box" && rows[1].IsEnabled && !rows[1].IsChecked, "The basket lists the cardboard box");
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
}
