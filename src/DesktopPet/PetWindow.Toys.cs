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
        // In the cat's mouth (PetWindow.ToyFetch.cs): its window hidden, its motion still.
        public bool Carried;
        public Point PressAt;
        // M4 (PetWindow.ToyChase.cs): the wand's feather on its string, the mouse's clockwork, and the
        // side the wand's tip or the mouse's nose points to.
        public readonly FeatherString Feather = new();
        public readonly WindUpMouse WindUp = new();
        public int Facing = 1;
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
        StopToyFetch(drop: false); // A ball in the mouth goes back to the basket too.
        StopToyChase();
        StopToyPass();
        if (react == React.SniffWalk) StopToyReact();
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
        // A new mouse counts its winds from zero: the one the cat last chased (another mouse) must not hide its first.
        if (id == DesktopToyRules.Mouse) chaseWind = -1;
        RefreshToyPlace(toy);
        return toy;
    }

    // Takes a toy out of the basket: it hops out towards the middle of the screen.
    public bool TakeToyOut(string id)
    {
        if (paused || !ToysMayShow || toyBasket == null || ToyIsOut(id) || !DesktopToyRules.CanTake(id, desktopToys.Select(toy => toy.Id))) return false;
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
        if (toy.Id == DesktopToyRules.Ball) { StopToyFetch(drop: false); StopToyPass(); }
        if (chaseToy == toy) StopToyChase();
        if (toy.Id == DesktopToyRules.Post) StopPostTrip();
        if (toy.Id == DesktopToyRules.Plant) StopPlantTrip();
        if (sniffToy == toy) StopToyReact();
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
            thing.Feather.Clear(); // Hangs again under the tip in the new DIPs.
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
        else if (thing.Id == DesktopToyRules.Wand)
        {
            // Big enough for the feather swinging on its string while held; only what is drawn takes clicks.
            surface = thing.Art = new ToyWandArt { Cursor = Cursors.Hand, RenderTransformOrigin = new Point(0.5, 0.5) };
            thing.Window = CreateOverlayWindow(surface, "Pet feather wand");
            thing.Window.Width = ToyWandArt.ArtWidth; thing.Window.Height = ToyWandArt.ArtHeight;
        }
        else if (thing.Id == DesktopToyRules.Post)
        {
            surface = thing.Art = new ToyPostArt { Cursor = Cursors.Hand };
            thing.Window = CreateOverlayWindow(surface, "Pet scratching post");
            thing.Window.Width = ToyPostArt.ArtWidth; thing.Window.Height = ToyPostArt.ArtHeight;
        }
        else if (thing.Id == DesktopToyRules.Plant)
        {
            surface = thing.Art = new ToyPlantArt { Cursor = Cursors.Hand, Stage = PlantStage };
            thing.Window = CreateOverlayWindow(surface, "Pet plant pot");
            thing.Window.Width = ToyPlantArt.ArtWidth; thing.Window.Height = ToyPlantArt.ArtHeight;
        }
        else if (thing.Id == DesktopToyRules.Mouse)
        {
            surface = thing.Art = new ToyMouseArt { Cursor = Cursors.Hand };
            thing.Window = CreateOverlayWindow(surface, "Pet wind-up mouse");
            thing.Window.Width = ToyMouseArt.ArtWidth; thing.Window.Height = ToyMouseArt.ArtHeight;
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
        if (thing != toyBasket) AttachToyMenu(thing, surface);
        surface.MouseLeftButtonDown += (_, e) => { PressToy(thing); e.Handled = true; };
        surface.MouseMove += (_, _) => CheckToyDragThreshold(thing);
        surface.MouseLeftButtonUp += (_, _) => EndToyDrag(thing, true);
        surface.LostMouseCapture += (_, _) => EndToyDrag(thing, false);
        thing.Placed = false;
    }

    // Each toy's own right-click menu: what the cat can do with it, then back to the basket. Opening it
    // never touches the cat (no waking, no interaction); a toy in a game, in the cat's mouth, held or
    // with the cat in it has no menu, and Pause shows none.
    private void AttachToyMenu(DesktopThing thing, FrameworkElement surface)
    {
        var menu = new ContextMenu { FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize };
        PetMenuStyle.Apply(menu);
        surface.ContextMenu = menu;
        surface.ContextMenuOpening += (_, e) =>
        {
            if (paused || thing.Held || thing.Press || thing.InGame || thing.Carried || (thing.Id == DesktopToyRules.Box && BoxOccupied)) { e.Handled = true; return; }
            menu.Items.Clear();
            foreach (var item in ToyMenuItems(thing.Id)) menu.Items.Add(item);
        };
    }

    // The rows of a toy's menu (also read by the checks). The cat's own rows come first, a line, then "Put back in the basket".
    public object[] ToyMenuItems(string id)
    {
        var rows = new List<object>();
        if (id == DesktopToyRules.Box)
        {
            var sleep = PetUi.MenuItem(PetUi.Quiet); // The moon, like the cushion's row.
            sleep.Header = L("Ngủ trong hộp", "Sleep in the box");
            sleep.Click += (_, _) => SleepInBoxNow(); // PetWindow.ToyBox.cs
            CatRowState(sleep, BoxNear, PetAction.Sleeping, L("Mèo ở xa hộp quá", "The cat is too far from the box"));
            rows.Add(sleep);
        }
        else if (id == DesktopToyRules.Post)
        {
            var scratch = PetUi.MenuItem(PetUi.Post);
            scratch.Header = L("Cào móng ở đây", "Scratch here");
            scratch.Click += (_, _) => { if (PostNear) DoNow(PetAction.Scratching); }; // Near the post "Scratch" goes there (PetWindow.ToyPost.cs).
            CatRowState(scratch, PostNear, PetAction.Scratching, L("Mèo ở xa trụ quá", "The cat is too far from the post"));
            rows.Add(scratch);
        }
        if (rows.Count > 0) rows.Add(new Separator());
        var stow = PetUi.MenuItem(PetUi.Ball);
        stow.Header = id == DesktopToyRules.Ball ? L("Cất bóng", "Put ball away") : L("Cất vào giỏ", "Put back in the basket");
        stow.Click += (_, _) => StowToy(id);
        rows.Add(stow);
        return rows.ToArray();
    }

    // A row asking the cat for something: greyed out, with the reason, while the cat is far, busy or asleep
    // (the menu of a thing never wakes the cat) or the pet belongs to a battle.
    private void CatRowState(MenuItem row, bool near, PetAction action, string far)
    {
        bool free = !BattleOwnsPet && !dragging && PetBehavior.CanStartManual(pet, action);
        row.IsEnabled = near && free;
        row.ToolTip = !free ? L("Mèo đang ngủ hoặc đang bận", "The cat is asleep or busy") : !near ? far : null;
        ToolTipService.SetShowOnDisabled(row, true);
    }

    private void FillToyBasketMenu()
    {
        if (toyBasketMenu == null) return;
        toyBasketMenu.Items.Clear();
        var rows = ToyBasketMenuItems();
        for (int i = 0; i < rows.Length; i++)
        {
            // The living-corner things (their own limit) under a line of their own.
            if (i > 0 && DesktopToyRules.IsLiving(DesktopToyRules.All[i]) && !DesktopToyRules.IsLiving(DesktopToyRules.All[i - 1])) toyBasketMenu.Items.Add(new Separator());
            toyBasketMenu.Items.Add(rows[i]);
        }
    }

    // One row per toy: ticked while it is out; a click takes it out or puts it back.
    public MenuItem[] ToyBasketMenuItems() => DesktopToyRules.All.Select(id =>
    {
        bool box = id == DesktopToyRules.Box, ballRow = id == DesktopToyRules.Ball;
        bool isOut = ToyIsOut(id), playing = ballRow && toyWindow != null, full = !isOut && !DesktopToyRules.CanTake(id, desktopToys.Select(toy => toy.Id));
        bool sleeping = box && isOut && BoxOccupied;
        bool carried = ballRow && CarryingBall;
        string name = id switch
        {
            DesktopToyRules.Box => L("Thùng carton", "Cardboard box"),
            DesktopToyRules.Wand => L("Cần câu lông vũ", "Feather wand"),
            DesktopToyRules.Mouse => L("Chuột dây cót", "Wind-up mouse"),
            DesktopToyRules.Post => L("Trụ cào", "Scratching post"),
            DesktopToyRules.Plant => L("Chậu cây", "Plant pot"),
            _ => L("Bóng len", "Yarn ball")
        };
        string? why = playing ? L("đang chơi", "in play") : sleeping ? L("mèo đang ngủ", "the cat is asleep in it") : carried ? L("mèo đang ngậm", "in the cat's mouth")
            : full ? (DesktopToyRules.IsLiving(id) ? L("tối đa 2 món góc sống", "2 corner things out at most") : L("tối đa 3 món", "3 out at most")) : null;
        var item = PetUi.MenuItem(id switch { DesktopToyRules.Box => PetUi.Box, DesktopToyRules.Wand => PetUi.Wand, DesktopToyRules.Mouse => PetUi.WindUpMouse, DesktopToyRules.Post => PetUi.Post, DesktopToyRules.Plant => PetUi.Plant, _ => PetUi.Ball });
        // The pass record (M5) is a note, not a reason to grey the row.
        string? note = why ?? (ballRow && preferences.ToyPassBest > 0 ? L("kỷ lục chuyền", "best pass streak") + " " + preferences.ToyPassBest : null);
        if (why == null && id == DesktopToyRules.Plant) note = PlantStageName(PlantStage); // How far it has grown (M2).
        item.Header = note == null ? name : $"{name} ({note})";
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
        if (paused || !ToysMayShow || thing.InGame || thing.Carried || (thing.Id == DesktopToyRules.Box && BoxOccupied) || thing.Surface == null || !thing.Surface.CaptureMouse()) return;
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
        HideSeekThingPicked(thing); // Picking up the box ends hide-and-seek.
        thing.Press = false;
        thing.Held = true;
        thing.Glide = null;
        var cursor = ToyCursor(thing);
        thing.GrabX = cursor.X - thing.Motion.X;
        thing.GrabY = cursor.Y - ToyCentreY(thing);
        thing.Motion.VX = thing.Motion.VY = 0;
        thing.WindUp.Stop(thing.Motion); thing.WindUp.FlipLeft = 0; // Picked up: the mouse stops running.
        thing.Feather.Clear(); // The feather starts hanging from where it lay.
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
            if (HideSeekClickThing(thing)) { thing.Motion.Wobble(); return; } // A guess in hide-and-seek (PetWindow.HideSeek.cs).
            // A click. Nothing here reaches the cat: not awake, not asleep.
            if (thing == toyBasket) OpenToyBasketMenu();
            else if (thing.Id is DesktopToyRules.Box or DesktopToyRules.Wand or DesktopToyRules.Post or DesktopToyRules.Plant) { thing.Motion.Wobble(); PlayPetSound("ball_tap"); }
            // Wound up: it runs (PetWindow.ToyChase.cs); a click while it runs does nothing.
            else if (thing.Id == DesktopToyRules.Mouse) { if (thing.WindUp.Wind(thing.Motion)) PlayPetSound("ball_tap"); }
            else { thing.Motion.Nudge(ToyCursor(thing).X - thing.Motion.X); PlayPetSound("ball_tap"); PassTouched(thing); } // It may roll to the cat (PetWindow.ToyPass.cs).
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
        // A hard throw: the cat may fetch it (PetWindow.ToyFetch.cs) and a pass streak ends; a light one may be a pass (PetWindow.ToyPass.cs).
        if (letGo && !paused && FetchStreak.IsThrow(vx, vy)) StopToyPass();
        else if (letGo && !paused) PassTouched(thing);
        BallThrown(thing, vx, vy);
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
        else if (thing.Id == DesktopToyRules.Wand && thing.Art is ToyWandArt wand)
        {
            double left = motion.X - ToyWandArt.CentreX, top = ToyCentreY(thing) - ToyWandArt.CentreY;
            MoveWindow(thing.Window, left, top, thing.Scale);
            wand.Held = thing.Held && thing.Feather.Ready;
            wand.Facing = thing.Facing;
            wand.Feather = new Point(thing.Feather.X - left, thing.Feather.Y - top);
            wand.Floor = thing.Area.Bottom - top;
            wand.InvalidateVisual();
            wand.RenderTransform = motion.Feedback > 0 ? new RotateTransform(Math.Sin(motion.Feedback * 45) * motion.Feedback * 6) : Transform.Identity;
        }
        else if (thing.Id == DesktopToyRules.Post)
        {
            MoveWindow(thing.Window, motion.X - ToyPostArt.ArtWidth / 2, thing.Area.Bottom - ToyPostArt.ArtHeight - motion.Height, thing.Scale);
            DrawPostScratch(thing); // A click only sways the pompom (PetWindow.ToyPost.cs).
        }
        else if (thing.Id == DesktopToyRules.Plant)
        {
            MoveWindow(thing.Window, motion.X - ToyPlantArt.ArtWidth / 2, thing.Area.Bottom - ToyPlantArt.ArtHeight - motion.Height, thing.Scale);
            DrawPlant(thing); // A click only sways the leaves (PetWindow.ToyPlant.cs).
        }
        else if (thing.Id == DesktopToyRules.Mouse && thing.Art is ToyMouseArt mouse)
        {
            MoveWindow(thing.Window, motion.X - ToyMouseArt.ArtWidth / 2, thing.Area.Bottom - ToyMouseArt.ArtHeight - motion.Height, thing.Scale);
            mouse.Facing = thing.WindUp.Direction;
            mouse.Flipped = thing.WindUp.Flipped;
            mouse.Turn = thing.WindUp.Wheel;
            mouse.InvalidateVisual();
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
        GuardToyFetch(dt); // PetWindow.ToyFetch.cs: a fullscreen app or a battle drops the ball before it hides.
        GuardToyChase(dt); // PetWindow.ToyChase.cs: the same for chasing the wand or the mouse.
        GuardToyPass(dt); // PetWindow.ToyPass.cs: and for passing the ball, with its label.
        GuardFavorite(); // PetWindow.FavoriteToy.cs: and for a trip to the favourite toy.
        if (toyBasket == null) return;
        bool show = ToysMayShow, shown = false;
        foreach (var thing in DesktopThings)
        {
            if (!show || paused) EndToyDrag(thing, false);
            if (!show || thing.InGame || thing.Carried) { if (thing.Window?.IsVisible == true) thing.Window.Hide(); continue; }
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
        foreach (var thing in DesktopThings) if (!thing.Placed && !thing.InGame && !thing.Carried) PositionDesktopThing(thing);
        if (DesktopPost is { Window.IsVisible: true } post) DrawPostScratch(post); // The rope while the cat scratches it.
        if (DesktopPlant is { Window.IsVisible: true } plant) DrawPlant(plant, paused ? 0 : dt); // Its stage, swaying leaves, a new stage's glitter.
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
        if (thing.InGame || thing.Carried) return false;
        var motion = thing.Motion;
        if (thing.Halo != null) thing.Halo.Opacity = Math.Max(motion.Feedback / 0.35, thing.Surface?.IsMouseOver == true ? 0.45 : 0);
        if (thing.Held)
        {
            UpdateToyDrag(thing);
            if (thing.Id == DesktopToyRules.Wand) StepFeather(thing, dt);
            PositionDesktopThing(thing);
            return false;
        }
        if (thing.Feather.Ready) { thing.Feather.Clear(); thing.Placed = false; } // Let go: it lies by the tip again.
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
        if (thing != toyBasket && (!motion.AtRest || motion.Feedback > 0 || thing.WindUp.Busy))
        {
            double left = thing.Area.Left + thing.HalfWidth, right = Math.Max(left, thing.Area.Right - thing.HalfWidth);
            if (thing.Id is DesktopToyRules.Box or DesktopToyRules.Wand or DesktopToyRules.Post or DesktopToyRules.Plant) motion.StepBox(dt, left, right);
            else if (thing.Id == DesktopToyRules.Mouse) thing.WindUp.Step(motion, dt, left, right, thing.WindUp.Running ? ToyObstacles(thing.Monitor) : null, thing.HalfWidth);
            else motion.Step(dt, left, right);
            double ceiling = ToyCeiling(thing);
            if (motion.Height > ceiling) { motion.Height = ceiling; motion.VY = -Math.Abs(motion.VY) * 0.3; }
            PositionDesktopThing(thing);
        }
        if (!motion.AtRest || thing.WindUp.Busy) return false;
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
