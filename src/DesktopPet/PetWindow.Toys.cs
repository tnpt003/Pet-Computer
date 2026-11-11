using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopPet;

// The toy basket and the toys left out on the desktop (docs/TOY-PLAY.md). They never touch the cat:
// no bonk, no emotion, no waking it. The 45-second ball game keeps its own window (PetWindow.Features.cs);
// a ball lying out only hands its place over to the game and takes it back afterwards.
public sealed partial class PetWindow
{
    // The basket or a toy: its monitor, in that monitor's DIPs. Motion.X is the centre; the basket never leaves the floor.
    private sealed class DesktopThing
    {
        public readonly ToyBall Motion = new();
        public readonly ThrowTracker Tracker = new();
        public string Id = "";
        public double HalfWidth;
        public Window? Window;
        public FrameworkElement? Surface, Art;
        public Ellipse? Halo;
        public string? Monitor;
        public Area Area;
        public double Scale = 1, GrabX, GrabY;
        public double? Glide;
        public bool Press, Held, InGame, Unsaved, Placed;
        public Point PressAt;
    }

    private const double ToySide = 56;
    private DesktopThing? toyBasket;
    private readonly List<DesktopThing> desktopToys = new();
    private DesktopThing? gameToy;
    private ToyBasketArt? toyBasketArt;
    private ContextMenu? toyBasketMenu;
    private bool toysLoaded, toyGameStowing;
    private double toysAreaTimer;
    private long toysOrderDue;

    private bool ToysMayShow => toyBasket != null && !context.Fullscreen && !BattleActive;
    private IEnumerable<DesktopThing> DesktopThings => toyBasket == null ? desktopToys : desktopToys.Append(toyBasket);
    private DesktopThing? DesktopToy(string id) => desktopToys.FirstOrDefault(toy => toy.Id == id);

    public bool ToyBasketShown => toyBasket != null;
    public Window? ToyBasketWindow => toyBasket?.Window;
    public bool ToyIsOut(string id) => DesktopToy(id) != null;
    public Window? ToyWindowOf(string id) => DesktopToy(id)?.Window;
    public ToyBall? ToyMotionOf(string id) => DesktopToy(id)?.Motion;
    public bool ToyHeld(string id) => DesktopToy(id)?.Held == true;
    public bool ToysAboveCat => DesktopThings.Any(thing => thing.Window?.IsVisible == true && WindowsAbove(new WindowInteropHelper(this).Handle).Contains(new WindowInteropHelper(thing.Window).Handle));

    // The menu's and the tray's one entry: the basket comes out, or everything goes away.
    public string ToyBasketMenuLabel => ToyBasketShown ? L("Cất nhanh", "Quick tidy") : L("Giỏ đồ chơi", "Toy basket");
    public bool ToyBasketMenuEnabled => !paused && !BattleActive && !context.Fullscreen;

    public void ToggleToyBasket()
    {
        if (!ToyBasketMenuEnabled) return;
        if (!toysLoaded) LoadDesktopToys();
        if (ToyBasketShown) QuickTidy();
        else
        {
            preferences.ToyBasketShown = true;
            CreateToyBasket();
            SavePreferences();
            PlayPetSound("pop");
        }
    }

    // Everything back in the basket and the basket away; the cat stays.
    private void QuickTidy()
    {
        toyGameStowing = true;
        if (toyWindow != null || laserWindow != null) CancelFeatureActivity();
        toyGameStowing = false;
        foreach (var toy in desktopToys) CloseDesktopThing(toy);
        desktopToys.Clear();
        WriteToyPlaces();
        if (toyBasket != null) CloseDesktopThing(toyBasket);
        toyBasket = null; toyBasketArt = null; toyBasketMenu = null;
        preferences.ToyBasketShown = false;
        preferences.ToysOut = null;
        SavePreferences();
        PlayPetSound("pop");
    }

    private void CreateToyBasket()
    {
        toyBasket = new DesktopThing { Id = "basket", HalfWidth = DesktopToyRules.BasketHalfWidth, Monitor = preferences.ToyBasketMonitor ?? CurrentMonitor().DeviceName };
        RefreshToyPlace(toyBasket);
        toyBasket.Motion.X = DesktopToyRules.FromFraction(preferences.ToyBasketFractionX ?? 0.25, toyBasket.HalfWidth, toyBasket.Area);
        toyBasket.Unsaved = true;
    }

    private void LoadDesktopToys()
    {
        toysLoaded = true;
        if (!preferences.ToyBasketShown) return;
        CreateToyBasket();
        toyBasket!.Unsaved = false;
        foreach (var saved in preferences.ToysOut ?? new List<SavedToy>())
        {
            var toy = NewToy(saved.Id, saved.Monitor);
            toy.Motion.X = DesktopToyRules.FromFraction(saved.FractionX, toy.HalfWidth, toy.Area);
            desktopToys.Add(toy);
        }
    }

    private DesktopThing NewToy(string id, string? monitor)
    {
        var toy = new DesktopThing { Id = id, HalfWidth = DesktopToyRules.HalfWidthOf(id), Monitor = monitor };
        RefreshToyPlace(toy);
        return toy;
    }

    // Takes a toy out of the basket: it hops out towards the middle of the screen.
    public bool TakeToyOut(string id)
    {
        if (paused || !ToysMayShow || toyBasket == null || ToyIsOut(id) || !DesktopToyRules.CanTake(desktopToys.Count)) return false;
        if (id == DesktopToyRules.Ball && toyWindow != null) return false; // It is in the game.
        var toy = NewToy(id, toyBasket.Monitor);
        toy.Motion.X = toyBasket.Motion.X;
        toy.Motion.Height = DesktopToyRules.BasketHeight - 8;
        toy.Motion.VX = DesktopToyRules.Fraction(toyBasket.Motion.X, toyBasket.HalfWidth, toyBasket.Area) < 0.5 ? 120 : -120;
        toy.Motion.VY = 190;
        toy.Unsaved = true;
        desktopToys.Add(toy);
        PlayPetSound("pop");
        return true;
    }

    public bool StowToy(string id)
    {
        if (paused || DesktopToy(id) is not { } toy || toy.InGame || (toy.Id == DesktopToyRules.Box && BoxOccupied)) return false;
        StowToy(toy);
        return true;
    }

    private void StowToy(DesktopThing toy)
    {
        CloseDesktopThing(toy);
        desktopToys.Remove(toy);
        WriteToyPlaces();
        SavePreferences();
        PlayPetSound("pop");
    }

    private static void CloseDesktopThing(DesktopThing thing)
    {
        thing.Press = thing.Held = false;
        thing.Surface?.ReleaseMouseCapture();
        thing.Window?.Close();
        thing.Window = null; thing.Surface = thing.Art = null; thing.Halo = null;
    }

    private void WriteToyPlaces()
    {
        if (toyBasket != null)
        {
            preferences.ToyBasketMonitor = toyBasket.Monitor;
            preferences.ToyBasketFractionX = DesktopToyRules.Fraction(toyBasket.Motion.X, toyBasket.HalfWidth, toyBasket.Area);
        }
        preferences.ToysOut = desktopToys.Count == 0 ? null : desktopToys.Select(toy => new SavedToy
        {
            Id = toy.Id, Monitor = toy.Monitor, FractionX = DesktopToyRules.Fraction(toy.Motion.X, toy.HalfWidth, toy.Area)
        }).ToList();
    }

    // Its monitor (unplugged: the main one, at the same place across) and DPI, kept in step.
    private void RefreshToyPlace(DesktopThing thing)
    {
        var monitor = FindFarmMonitor(thing.Monitor);
        var motion = thing.Motion;
        bool moved = monitor.Name != thing.Monitor;
        double fraction = DesktopToyRules.Fraction(motion.X, thing.HalfWidth, thing.Area);
        var area = monitor.WorkDips;
        if (Math.Abs(monitor.Scale - thing.Scale) > 0.001)
        {
            double ratio = thing.Scale / monitor.Scale;
            motion.X *= ratio; motion.Height *= ratio; motion.VX *= ratio; motion.VY *= ratio;
            thing.GrabX *= ratio; thing.GrabY *= ratio;
            if (thing.Glide is double glide) thing.Glide = glide * ratio;
            thing.Tracker.Clear();
            thing.Placed = false;
        }
        if (moved || !thing.Area.Equals(area)) thing.Placed = false;
        thing.Monitor = monitor.Name;
        thing.Scale = monitor.Scale;
        thing.Area = area;
        if (moved) { motion.X = DesktopToyRules.FromFraction(fraction, thing.HalfWidth, area); thing.Glide = null; }
        motion.X = Math.Clamp(motion.X, area.Left + thing.HalfWidth, Math.Max(area.Left + thing.HalfWidth, area.Right - thing.HalfWidth));
        motion.Height = Math.Clamp(motion.Height, 0, ToyCeiling(thing));
    }

    private static double ToyCeiling(DesktopThing thing) => Math.Max(0, thing.Area.Bottom - thing.Area.Top - 2 * DesktopToyRules.HalfHeightOf(thing.Id));

    private void EnsureToyWindow(DesktopThing thing)
    {
        if (thing.Window != null) return;
        FrameworkElement surface;
        if (thing == toyBasket)
        {
            toyBasketArt = new ToyBasketArt { Cursor = Cursors.Hand };
            surface = thing.Art = toyBasketArt;
            thing.Window = CreateOverlayWindow(surface, "Pet toy basket");
            thing.Window.Width = ToyBasketArt.ArtWidth; thing.Window.Height = ToyBasketArt.ArtHeight;
            toyBasketMenu = new ContextMenu { FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize };
            PetMenuStyle.Apply(toyBasketMenu);
            surface.ContextMenu = toyBasketMenu;
            surface.ContextMenuOpening += (_, e) => { if (paused || thing.Held) e.Handled = true; else FillToyBasketMenu(); };
        }
        else if (thing.Id == DesktopToyRules.Box)
        {
            // Only the drawn box takes clicks; it shakes from its bottom middle.
            surface = thing.Art = new ToyBoxArt { Cursor = Cursors.Hand, RenderTransformOrigin = new Point(0.5, 1) };
            thing.Window = CreateOverlayWindow(surface, "Pet cardboard box");
            thing.Window.Width = ToyBoxArt.ArtWidth; thing.Window.Height = ToyBoxArt.ArtHeight;
        }
        else
        {
            var grid = new Grid { Width = ToySide, Height = ToySide, Cursor = Cursors.Hand };
            // Only a small circle around the ball takes clicks; the rest of the window lets them through.
            grid.Children.Add(new Ellipse { Width = 36, Height = 36, Fill = PetUi.Frozen(1, 0, 0, 0) });
            thing.Halo = new Ellipse { Width = 36, Height = 36, Stroke = YarnBallArt.Halo, StrokeThickness = 2, Opacity = 0, IsHitTestVisible = false };
            thing.Art = new YarnBallArt { Width = 24, Height = 24, RenderTransformOrigin = new Point(0.5, 0.5), IsHitTestVisible = false };
            grid.Children.Add(thing.Halo); grid.Children.Add(thing.Art);
            surface = grid;
            thing.Window = CreateOverlayWindow(surface, "Pet desktop toy");
            thing.Window.Width = ToySide; thing.Window.Height = ToySide;
        }
        thing.Surface = surface;
        surface.MouseLeftButtonDown += (_, e) => { PressToy(thing); e.Handled = true; };
        surface.MouseMove += (_, _) => CheckToyDragThreshold(thing);
        surface.MouseLeftButtonUp += (_, _) => EndToyDrag(thing, true);
        surface.LostMouseCapture += (_, _) => EndToyDrag(thing, false);
        thing.Placed = false;
    }

    private void FillToyBasketMenu()
    {
        if (toyBasketMenu == null) return;
        toyBasketMenu.Items.Clear();
        foreach (var item in ToyBasketMenuItems()) toyBasketMenu.Items.Add(item);
    }

    // One row per toy: ticked while it is out; a click takes it out or puts it back.
    public MenuItem[] ToyBasketMenuItems() => DesktopToyRules.All.Select(id =>
    {
        bool box = id == DesktopToyRules.Box;
        bool isOut = ToyIsOut(id), playing = id == DesktopToyRules.Ball && toyWindow != null, full = !isOut && !DesktopToyRules.CanTake(desktopToys.Count);
        bool sleeping = box && isOut && BoxOccupied;
        string name = box ? L("Thùng carton", "Cardboard box") : L("Bóng len", "Yarn ball");
        string? why = playing ? L("đang chơi", "in play") : sleeping ? L("mèo đang ngủ", "the cat is asleep in it") : full ? L("tối đa 3 món", "3 out at most") : null;
        var item = PetUi.MenuItem(box ? PetUi.Box : PetUi.Ball);
        item.Header = why == null ? name : $"{name} ({why})";
        item.IsCheckable = true; item.IsChecked = isOut; item.IsEnabled = why == null;
        item.Click += (_, _) => { if (ToyIsOut(id)) StowToy(id); else TakeToyOut(id); };
        return item;
    }).ToArray();

    private void OpenToyBasketMenu()
    {
        if (toyBasketMenu == null || toyBasket?.Surface == null) return;
        FillToyBasketMenu();
        toyBasketMenu.PlacementTarget = toyBasket.Surface;
        toyBasketMenu.Placement = PlacementMode.Top;
        toyBasketMenu.IsOpen = true;
    }

    private Point ToyCursor(DesktopThing thing)
    {
        var point = ScreenCursor();
        return new Point(point.X / thing.Scale, point.Y / thing.Scale);
    }

    private void PressToy(DesktopThing thing)
    {
        if (paused || !ToysMayShow || thing.InGame || (thing.Id == DesktopToyRules.Box && BoxOccupied) || thing.Surface == null || !thing.Surface.CaptureMouse()) return;
        thing.Press = true;
        thing.PressAt = ToyCursor(thing);
        thing.Glide = null;
    }

    private void CheckToyDragThreshold(DesktopThing thing)
    {
        if (!thing.Press || thing.Surface?.IsMouseCaptured != true) return;
        if ((ToyCursor(thing) - thing.PressAt).Length < settings.DragThreshold) return;
        StartToyDrag(thing);
    }

    private double ToyCentreY(DesktopThing thing) => thing.Area.Bottom - DesktopToyRules.HalfHeightOf(thing.Id) - thing.Motion.Height;

    private void StartToyDrag(DesktopThing thing)
    {
        thing.Press = false;
        thing.Held = true;
        thing.Glide = null;
        var cursor = ToyCursor(thing);
        thing.GrabX = cursor.X - thing.Motion.X;
        thing.GrabY = cursor.Y - ToyCentreY(thing);
        thing.Motion.VX = thing.Motion.VY = 0;
        thing.Tracker.Clear();
        thing.Tracker.Add(clock.Elapsed.TotalSeconds, cursor.X, cursor.Y);
        PlayPetSound("swoosh");
    }

    private void UpdateToyDrag(DesktopThing thing)
    {
        if (FarmMonitorAt(ScreenCursor()) is { } over && over.Name != thing.Monitor)
        {
            // Carried onto another monitor: its DIPs from now on, and no throw across the two.
            thing.Monitor = over.Name;
            RefreshToyPlace(thing);
            thing.Tracker.Clear();
        }
        var cursor = ToyCursor(thing);
        thing.Tracker.Add(clock.Elapsed.TotalSeconds, cursor.X, cursor.Y);
        var motion = thing.Motion;
        motion.X = Math.Clamp(cursor.X - thing.GrabX, thing.Area.Left + thing.HalfWidth, Math.Max(thing.Area.Left + thing.HalfWidth, thing.Area.Right - thing.HalfWidth));
        if (thing != toyBasket) motion.Height = Math.Clamp(thing.Area.Bottom - DesktopToyRules.HalfHeightOf(thing.Id) - (cursor.Y - thing.GrabY), 0, ToyCeiling(thing));
        thing.Placed = false;
    }

    private void EndToyDrag(DesktopThing thing, bool letGo)
    {
        if (thing.Press)
        {
            thing.Press = false;
            thing.Surface?.ReleaseMouseCapture();
            if (!letGo || paused) return;
            // A click. Nothing here reaches the cat: not awake, not asleep.
            if (thing == toyBasket) OpenToyBasketMenu();
            else if (thing.Id == DesktopToyRules.Box) { thing.Motion.Wobble(); PlayPetSound("ball_tap"); }
            else { thing.Motion.Nudge(ToyCursor(thing).X - thing.Motion.X); PlayPetSound("ball_tap"); }
            return;
        }
        if (!thing.Held) return;
        thing.Held = false; // Before releasing capture; LostMouseCapture is synchronous.
        thing.Surface?.ReleaseMouseCapture();
        thing.Unsaved = true;
        if (thing == toyBasket) return;
        if (letGo && !paused && toyBasket != null && toyBasket.Monitor == thing.Monitor && DesktopToyRules.OverBasket(thing.Motion.X, thing.Motion.Height, toyBasket.Motion.X))
        {
            StowToy(thing);
            return;
        }
        var (vx, vy) = letGo && !paused ? thing.Tracker.Velocity(2200) : (0.0, 0.0);
        thing.Motion.VX = vx;
        thing.Motion.VY = -vy; // Height goes up, the screen goes down.
    }

    // "Play with ball" with the ball lying out: the game takes over from where it is. On another
    // monitor than the cat's it comes to the cat, as it would from the basket.
    private void GameTakesLyingBall()
    {
        if (DesktopToy(DesktopToyRules.Ball) is not { } lying) return;
        lying.Glide = null;
        lying.InGame = true;
        lying.Window?.Hide();
        gameToy = lying;
        if (lying.Monitor != CurrentMonitor().DeviceName) return;
        ball.X = Math.Clamp(lying.Motion.X, bounds.Left + 12, Math.Max(bounds.Left + 12, bounds.Right - 12));
        ball.Height = lying.Motion.Height;
    }

    // Whatever ended the game: the ball that lay out lies where the game left it ("Put ball away" and Quick tidy stow it).
    private void FinishToyGame()
    {
        if (gameToy is not { } toy) return;
        gameToy = null;
        toy.InGame = false;
        if (toyGameStowing) { StowToy(toy); return; }
        SyncGameToy(toy);
    }

    private void SyncGameToy(DesktopThing toy)
    {
        var motion = toy.Motion;
        motion.X = ball.X; motion.Height = ball.Height; motion.VX = ball.VX; motion.VY = ball.VY; motion.Rotation = ball.Rotation;
        // The game ran in the cat's DIPs.
        toy.Monitor = CurrentMonitor().DeviceName;
        toy.Scale = FindFarmMonitor(toy.Monitor).Scale;
        RefreshToyPlace(toy);
        toy.Placed = false;
        toy.Unsaved = true;
    }

    private List<(double Left, double Right)> ToyObstacles(string? monitor) => FarmVisible ? FarmLandsTaken(monitor, null) : new();

    private bool SlideOffLand(DesktopThing thing)
    {
        if (FarmLandInAir || DesktopToyRules.FreeCentre(thing.Motion.X, thing.HalfWidth, ToyObstacles(thing.Monitor), thing.Area) is not double spot) return false;
        thing.Glide = spot;
        return true;
    }

    private void PositionDesktopThing(DesktopThing thing)
    {
        if (thing.Window == null) return;
        var motion = thing.Motion;
        if (thing == toyBasket) MoveWindow(thing.Window, motion.X - ToyBasketArt.ArtWidth / 2, thing.Area.Bottom - ToyBasketArt.ArtHeight, thing.Scale);
        else if (thing.Id == DesktopToyRules.Box)
        {
            MoveWindow(thing.Window, motion.X - ToyBoxArt.ArtWidth / 2, thing.Area.Bottom - ToyBoxArt.ArtHeight - motion.Height, thing.Scale);
            // A click's little shake, dying out with Feedback.
            if (thing.Art != null) thing.Art.RenderTransform = motion.Feedback > 0 ? new RotateTransform(Math.Sin(motion.Feedback * 45) * motion.Feedback * 9) : Transform.Identity;
        }
        else
        {
            MoveWindow(thing.Window, motion.X - ToySide / 2, thing.Area.Bottom - 40 - motion.Height, thing.Scale);
            if (thing.Art != null) thing.Art.RenderTransform = new RotateTransform(motion.Rotation);
        }
        thing.Placed = true;
    }

    // Under the cat and the keepsake, over the land; the farm's own order (PetWindow.Farm.cs) goes under the last of these.
    private IEnumerable<Window> ShownToyWindows => DesktopThings.Where(thing => thing.Window?.IsVisible == true).Select(thing => thing.Window!);
    private Window GroundAnchor => ShownToyWindows.LastOrDefault() ?? (KeepsakeShown ? keepsakeWindow! : this);

    private void KeepToysInOrder()
    {
        var previous = new WindowInteropHelper(KeepsakeShown ? keepsakeWindow! : this).Handle;
        const uint NoSize = 0x0001, NoMove = 0x0002, NoActivate = 0x0010;
        foreach (var window in ShownToyWindows)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero || previous == IntPtr.Zero) continue;
            if (WindowsAbove(previous).Contains(handle)) SetWindowPos(handle, previous, 0, 0, 0, 0, NoSize | NoMove | NoActivate);
            previous = handle;
        }
    }

    // Every frame, paused or not: a resting toy costs a few field reads and never moves its window.
    private void TickDesktopToys(double dt)
    {
        if (!toysLoaded) LoadDesktopToys();
        TickBoxNap(dt); // PetWindow.ToyBox.cs: also when the basket has just been tidied away.
        if (toyBasket == null) return;
        bool show = ToysMayShow, shown = false;
        foreach (var thing in DesktopThings)
        {
            if (!show || paused) EndToyDrag(thing, false);
            if (!show || thing.InGame) { if (thing.Window?.IsVisible == true) thing.Window.Hide(); continue; }
            EnsureToyWindow(thing);
            if (thing.Window!.IsVisible) continue;
            thing.Window.Show();
            thing.Placed = false;
            shown = true;
        }
        if (!show) return;
        if (shown || Environment.TickCount64 >= toysOrderDue)
        {
            toysOrderDue = Environment.TickCount64 + FarmOrderMilliseconds;
            KeepToysInOrder();
        }
        if (toyBasketArt != null) toyBasketArt.HasBall = !ToyIsOut(DesktopToyRules.Ball) && toyWindow == null;
        foreach (var thing in DesktopThings) if (!thing.Placed && !thing.InGame) PositionDesktopThing(thing);
        if (paused) return;
        toysAreaTimer += dt;
        bool checkPlaces = toysAreaTimer >= 1;
        if (checkPlaces) toysAreaTimer = 0;
        bool save = false;
        foreach (var thing in DesktopThings.ToList()) save |= StepDesktopThing(thing, dt, checkPlaces);
        if (save) { WriteToyPlaces(); SavePreferences(); }
    }

    // True when it has just come to rest somewhere new.
    private bool StepDesktopThing(DesktopThing thing, double dt, bool checkPlaces)
    {
        if (thing.InGame) return false;
        var motion = thing.Motion;
        if (thing.Halo != null) thing.Halo.Opacity = Math.Max(motion.Feedback / 0.35, thing.Surface?.IsMouseOver == true ? 0.45 : 0);
        if (thing.Held) { UpdateToyDrag(thing); PositionDesktopThing(thing); return false; }
        if (thing.Press) return false;
        if (checkPlaces) RefreshToyPlace(thing);
        if (thing.Glide is double to)
        {
            double step = Math.Min(Math.Abs(to - motion.X), DesktopToyRules.GlideSpeed * dt);
            double signed = Math.Sign(to - motion.X) * step;
            motion.X += signed;
            if (thing != toyBasket) motion.Rotation += signed / 11 * 180 / Math.PI;
            thing.Placed = false;
            if (Math.Abs(to - motion.X) < 0.01) { thing.Glide = null; thing.Unsaved = true; }
            PositionDesktopThing(thing);
            return false;
        }
        if (thing != toyBasket && (!motion.AtRest || motion.Feedback > 0))
        {
            double left = thing.Area.Left + thing.HalfWidth, right = Math.Max(left, thing.Area.Right - thing.HalfWidth);
            if (thing.Id == DesktopToyRules.Box) motion.StepBox(dt, left, right); else motion.Step(dt, left, right);
            double ceiling = ToyCeiling(thing);
            if (motion.Height > ceiling) { motion.Height = ceiling; motion.VY = -Math.Abs(motion.VY) * 0.3; }
            PositionDesktopThing(thing);
        }
        if (!motion.AtRest) return false;
        // The box with the cat in it stays put until the cat is out.
        if ((thing.Unsaved || checkPlaces) && !(thing.Id == DesktopToyRules.Box && BoxOccupied) && SlideOffLand(thing)) return false;
        if (!thing.Unsaved) return false;
        thing.Unsaved = false;
        return true;
    }

    private void CloseDesktopToys()
    {
        if (toyBasket == null) return;
        bool unsaved = DesktopThings.Any(thing => thing.Unsaved);
        if (unsaved) { WriteToyPlaces(); SavePreferences(); }
        foreach (var thing in DesktopThings) CloseDesktopThing(thing);
    }
}
