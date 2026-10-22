using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace DesktopPet;

// Expedition milestone 4: the display corner. The keepsakes a won run gave that are shown (cups, a
// slime cushion) stand on a small shelf on the floor of a monitor. On once the first keepsake is
// earned; Settings → Pet turns it off. Never takes focus, no taskbar button; hidden while a
// fullscreen app runs or a battle is on.
// The shelf is a cartoon object (ShelfMotion): the user picks it up (past the drag threshold), it
// dangles, is thrown, falls, lands with a squash and a bounce; dropped on the cat's head it bonks the
// cat (stars) and bounces off to one side. Now and then the cat shoves it along. Left at a screen
// edge, the cat is startled and the shelf vanishes in a puff and pops up in the middle.
// Now and then (not in Quiet, asleep, held, in a battle) the cat walks over and looks at it for a
// few seconds. All C#; Python is not involved.
public sealed partial class PetWindow
{
    private Window? trophyWindow;
    private TrophyShelf? trophyShelf;
    private int trophyCount = -1;
    // Seconds until the cat next goes to look at the shelf, and how long it still looks.
    private double admireVisit = 300, admireRemaining;
    private bool admiring;
    public const double AdmireSeconds = 4, AdmireEvery = 600;
    public const double ShelfSide = 48, ShelfTopRoom = 56, ShelfBottomRoom = 36;

    // The shelf's place: its monitor (in that monitor's DIPs) and its motion.
    private readonly ShelfMotion shelf = new();
    private string? shelfMonitor;
    private Area shelfArea;
    private double shelfScale = 1, shelfAreaTimer;
    private bool shelfPlaced, shelfEdgeCheck;
    // Input: pressed (not yet past the drag threshold) or held; the grab point from the art's left
    // edge and up from its bottom.
    private bool shelfPress, shelfHeld;
    private Point shelfPressAt;
    private double shelfGrabX, shelfGrabY;
    private readonly ThrowTracker shelfTracker = new();
    // Effects: landing dust (progress 0..1, negative: none) and its strength.
    private double shelfDust = -1, shelfDustStrength;

    // Back to the middle from a screen edge: the alarm (shakes, the cat startled), the vanish (a
    // puff) and the appear (pops up in the middle, drops, lands).
    private enum ShelfHop { None, Alarm, Vanish, Appear }
    private ShelfHop shelfHop;
    private double shelfHopTime, shelfHopTo;
    public const double ShelfAlarmSeconds = 0.8, ShelfVanishSeconds = 0.3, ShelfAppearSeconds = 0.55;
    // Seconds the cat still watches the shelf (the hop); while it does it stands still.
    private double shelfWatch;

    // The cat's shove: walk to one side, crouch, push, watch it slide.
    private enum ShelfShove { None, Approach, Crouch, Watch }
    private ShelfShove shelfShove;
    private double shoveVisit = 200, shoveTime;
    private int shoveDirection;
    public const double ShoveCrouchSeconds = 0.45, ShoveWatchSeconds = 1.6;

    // The keepsakes in the saved Expedition progress (never the in-memory one of custom data), read
    // with the built-in definitions.
    private ExpeditionProgress SavedExpeditionProgress =>
        expeditionProgress ??= ExpeditionProgressFile != null ? ExpeditionProgress.Load(ExpeditionProgressFile) : new();
    private static IReadOnlyList<KeepsakeSpec> KeepsakeList => BattleData.Settings.Expedition?.Keepsakes ?? Array.Empty<KeepsakeSpec>();
    public IReadOnlyList<string> Keepsakes => SavedExpeditionProgress.Keepsakes;
    // Shown on the shelf: the cushion first, then the cups bronze → gold.
    public IReadOnlyList<KeepsakeSpec> ShownKeepsakes => KeepsakeList.Where(k => k.Shown && Keepsakes.Contains(k.Id))
        .OrderBy(k => k.Kind == KeepsakeSpec.Cushion ? -1 : Array.IndexOf(KeepsakeSpec.Tiers, k.Tier)).ToList();
    private bool KeepsakeAccessory(string id) => KeepsakeList.Any(k => k.Accessory == id && Keepsakes.Contains(k.Id));

    public bool TrophiesVisible => trophyWindow?.IsVisible == true;
    private double ShelfArtWidth => trophyShelf == null ? 0 : TrophyShelf.WidthFor(trophyShelf.Items);
    private double ShelfFloor => shelfArea.Bottom;
    // The shelf's art (not the room around it), in its monitor's DIPs.
    public Rect TrophyBounds => trophyWindow == null ? Rect.Empty
        : new Rect(shelf.X, ShelfFloor - shelf.Height - TrophyShelf.ArtHeight, ShelfArtWidth, TrophyShelf.ArtHeight);
    public bool Admiring => admiring;
    public ShelfMotion TrophyMotion => shelf;
    public bool ShelfHeld => shelfHeld;
    public bool ShelfHopping => shelfHop != ShelfHop.None;
    public bool ShelfShoving => shelfShove != ShelfShove.None;
    // The cat and the shelf are on the same monitor (same DIPs): only then do they meet.
    private bool ShelfBesideCat => TrophiesVisible && shelfMonitor == CurrentMonitor().DeviceName && Math.Abs(shelfScale - scale) < 0.01;

    // Every frame, paused or not (cheap: only a list count and a position).
    private void TickTrophies()
    {
        var shown = preferences.ShowTrophies && !context.Fullscreen && !BattleActive ? ShownKeepsakes : Array.Empty<KeepsakeSpec>();
        if (shown.Count == 0)
        {
            if (trophyWindow?.IsVisible == true)
            {
                EndShelfDrag(false);
                FinishShelfHop();
                shelfShove = ShelfShove.None; shelfWatch = 0;
                trophyWindow.Hide();
            }
            return;
        }
        if (trophyWindow == null)
        {
            trophyShelf = new TrophyShelf { Side = ShelfSide, TopRoom = ShelfTopRoom, BottomRoom = ShelfBottomRoom, Cursor = Cursors.Hand };
            trophyWindow = new Window
            {
                Title = "Pet trophies", WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true, Background = Brushes.Transparent,
                ShowInTaskbar = false, ShowActivated = false, Topmost = true, Focusable = false, SizeToContent = SizeToContent.Manual, Content = trophyShelf
            };
            trophyWindow.SourceInitialized += (_, _) =>
            {
                // Clickable on the art (the transparent room passes clicks through), a tool window
                // that never activates.
                ((HwndSource)PresentationSource.FromVisual(trophyWindow)).AddHook(WindowHook);
                const int ExStyle = -20;
                const long ToolWindow = 0x80, NoActivate = 0x08000000;
                var handle = new WindowInteropHelper(trophyWindow).Handle;
                SetWindowLongPtr(handle, ExStyle, new IntPtr(GetWindowLongPtr(handle, ExStyle).ToInt64() | ToolWindow | NoActivate));
            };
            trophyShelf.MouseLeftButtonDown += (_, e) => { PressShelf(); e.Handled = true; };
            trophyShelf.MouseMove += (_, _) => CheckShelfDragThreshold();
            trophyShelf.MouseLeftButtonUp += (_, _) => EndShelfDrag(true);
            trophyShelf.LostMouseCapture += (_, _) => EndShelfDrag(false);
            trophyWindow.Closed += (_, _) => { trophyWindow = null; trophyShelf = null; trophyCount = -1; shelfHeld = shelfPress = false; };
            trophyCount = -1;
        }
        if (trophyCount != shown.Count)
        {
            trophyCount = shown.Count;
            trophyShelf!.Items = shown;
            trophyWindow.Width = trophyShelf.Width;
            trophyWindow.Height = trophyShelf.Height;
            if (shelfPlaced) shelf.X = Math.Clamp(shelf.X, shelfArea.Left, Math.Max(shelfArea.Left, shelfArea.Right - ShelfArtWidth));
        }
        if (!shelfPlaced) PlaceShelf();
        if (!trophyWindow.IsVisible) { trophyWindow.Show(); KeepCatAboveShelf(); }
        PositionShelf();
    }

    // First show: where it was left (its monitor, across the free width), else 3/4 across the
    // cat's monitor; never at an edge (straight to the middle, no show).
    private void PlaceShelf()
    {
        shelfPlaced = true;
        shelfMonitor = preferences.TrophyMonitor;
        RefreshShelfArea(keepPlace: false);
        double free = Math.Max(0, shelfArea.Right - shelfArea.Left - ShelfArtWidth);
        bool remembered = preferences.TrophyMonitor == shelfMonitor && preferences.TrophyFractionX.HasValue;
        shelf.X = shelfArea.Left + (remembered ? preferences.TrophyFractionX!.Value : 0.75) * free;
        if (ShelfMotion.NearEdge(shelf.X, ShelfArtWidth, shelfArea.Left, shelfArea.Right)) shelf.X = shelfArea.Left + free / 2;
        shelf.Height = shelf.VX = shelf.VY = 0;
    }

    // The shelf's monitor (gone: the cat's) in its own DIPs. The cat's monitor uses the cat's scale.
    private void RefreshShelfArea(bool keepPlace = true)
    {
        var catScreen = CurrentMonitor();
        var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == shelfMonitor) ?? catScreen;
        bool moved = screen.DeviceName != shelfMonitor;
        shelfMonitor = screen.DeviceName;
        double at = screen.DeviceName == catScreen.DeviceName ? scale : MonitorScale(screen);
        if (keepPlace && Math.Abs(at - shelfScale) > 0.001)
        {
            double ratio = shelfScale / at;
            shelf.X *= ratio; shelf.Height *= ratio; shelf.VX *= ratio; shelf.VY *= ratio;
            shelfTracker.Clear();
        }
        shelfScale = at;
        var work = screen.WorkingArea;
        var old = shelfArea;
        shelfArea = new Area(work.Left / at, work.Top / at, work.Right / at, work.Bottom / at);
        if (!keepPlace) return;
        // Its monitor was unplugged: the same spot across the new one.
        if (moved) shelf.X = shelfArea.Left + Math.Clamp((shelf.X - old.Left) / Math.Max(1, old.Right - old.Left - ShelfArtWidth), 0, 1) * Math.Max(0, shelfArea.Right - shelfArea.Left - ShelfArtWidth);
        shelf.X = Math.Clamp(shelf.X, shelfArea.Left, Math.Max(shelfArea.Left, shelfArea.Right - ShelfArtWidth));
        shelf.Height = Math.Clamp(shelf.Height, 0, Math.Max(0, ShelfFloor - shelfArea.Top - TrophyShelf.ArtHeight));
    }

    private void PositionShelf()
    {
        if (trophyWindow == null || trophyShelf == null) return;
        // The room below the art hangs past the floor (over the taskbar): transparent, clicks pass through.
        MoveWindow(trophyWindow, shelf.X - ShelfSide, ShelfFloor - shelf.Height + ShelfBottomRoom - trophyWindow.Height, shelfScale);
        double pop = 1, shake = 0, puff = -1, t = shelfHopTime;
        switch (shelfHop)
        {
            case ShelfHop.Alarm:
                // Trembles harder and harder, with a nervous little pulse.
                shake = Math.Sin(t * 70) * 2.6 * Math.Min(1, t / ShelfAlarmSeconds + 0.2);
                pop = 1 + 0.05 * Math.Sin(t * 28);
                break;
            case ShelfHop.Vanish:
                double v = t / ShelfVanishSeconds;
                pop = v < 0.25 ? 1 + v / 0.25 * 0.18 : 1.18 * (1 - (v - 0.25) / 0.75);
                puff = v * 0.45;
                break;
            case ShelfHop.Appear:
                double u = Math.Min(1, t / 0.32);
                // Ease out with an overshoot: pops up a bit too big, then settles.
                pop = 1 + 2.7 * Math.Pow(u - 1, 3) + 1.7 * Math.Pow(u - 1, 2);
                puff = 0.35 + 0.65 * Math.Min(1, t / ShelfAppearSeconds);
                break;
        }
        Point? pivot = shelfHeld ? new Point(shelfGrabX, TrophyShelf.ArtHeight - shelfGrabY) : null;
        trophyShelf.SetPose(shelf.ScaleX, shelf.ScaleY, shelf.Tilt, pivot, Math.Max(0, pop), shake, shelfDust, shelfDustStrength, puff);
    }

    // Shelf below the cat: both are topmost, and the shelf was shown later.
    private void KeepCatAboveShelf()
    {
        if (trophyWindow == null) return;
        var shelfHandle = new WindowInteropHelper(trophyWindow).Handle;
        var catHandle = new WindowInteropHelper(this).Handle;
        const uint NoSize = 0x0001, NoMove = 0x0002, NoActivate = 0x0010;
        if (shelfHandle != IntPtr.Zero && catHandle != IntPtr.Zero) SetWindowPos(shelfHandle, catHandle, 0, 0, 0, 0, NoSize | NoMove | NoActivate);
    }

    private Point ShelfCursor()
    {
        GetCursorPos(out var point);
        return new Point(point.X / shelfScale, point.Y / shelfScale);
    }

    private void PressShelf()
    {
        if (paused || shelfHop != ShelfHop.None || BattleActive || trophyShelf == null || !trophyShelf.CaptureMouse()) return;
        shelfPress = true;
        shelfPressAt = ShelfCursor();
    }

    private void CheckShelfDragThreshold()
    {
        if (!shelfPress || trophyShelf?.IsMouseCaptured != true) return;
        if ((ShelfCursor() - shelfPressAt).Length < settings.DragThreshold) return;
        StartShelfDrag();
    }

    private void StartShelfDrag()
    {
        shelfPress = false;
        shelfHeld = true;
        StopShelfShove();
        if (admiring) StopAdmiring();
        var cursor = ShelfCursor();
        shelfGrabX = Math.Clamp(cursor.X - shelf.X, 0, ShelfArtWidth);
        shelfGrabY = Math.Clamp(ShelfFloor - shelf.Height - cursor.Y, 0, TrophyShelf.ArtHeight);
        shelf.Held = true;
        shelf.VX = shelf.VY = 0;
        shelf.SquashV -= 3; // Lifted: a little stretch.
        shelfTracker.Clear();
        shelfTracker.Add(clock.Elapsed.TotalSeconds, cursor.X, cursor.Y);
        PlayPetSound("swoosh");
    }

    // Test hook (and the drag's own frame): carry the held shelf to the cursor.
    private void UpdateShelfDrag()
    {
        GetCursorPos(out var point);
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y));
        if (screen.DeviceName != shelfMonitor)
        {
            // Carried onto another monitor: its DIPs from now on, and no throw across the two.
            shelfMonitor = screen.DeviceName;
            RefreshShelfArea();
        }
        var cursor = ShelfCursor();
        shelfTracker.Add(clock.Elapsed.TotalSeconds, cursor.X, cursor.Y);
        double w = ShelfArtWidth;
        shelf.X = Math.Clamp(cursor.X - shelfGrabX, shelfArea.Left, Math.Max(shelfArea.Left, shelfArea.Right - w));
        shelf.Height = Math.Clamp(ShelfFloor - (cursor.Y + shelfGrabY), 0, Math.Max(0, ShelfFloor - shelfArea.Top - TrophyShelf.ArtHeight));
        // Dangles: swings back against the way it is carried.
        shelf.HeldTilt = Math.Clamp(shelfTracker.Velocity(3000).X * 0.018, -28, 28);
    }

    private void EndShelfDrag(bool throwIt)
    {
        if (shelfPress)
        {
            shelfPress = false;
            trophyShelf?.ReleaseMouseCapture();
            // A click: boing.
            shelf.Boing();
            PlayPetSound("squish");
            return;
        }
        if (!shelfHeld) return;
        shelfHeld = false; // Before releasing capture; LostMouseCapture is synchronous.
        trophyShelf?.ReleaseMouseCapture();
        var (vx, vy) = throwIt && !paused ? shelfTracker.Velocity(2200) : (0.0, 0.0);
        shelf.Release(vx, vy);
        shelfEdgeCheck = true;
        KeepCatAboveShelf();
    }

    // From Tick, every frame: the shelf's physics, the bonk, the hop and the cat's watching.
    // A paused pet freezes all of it (the shelf hangs in the air with its velocity).
    private void TickShelf(double dt)
    {
        if (paused || !TrophiesVisible) return;
        if (pet.Bonk > 0)
        {
            pet.Bonk = Math.Max(0, pet.Bonk - dt);
            if (PetActionRules.IsSettled(pet)) { pet.VX = pet.SmoothedWalkVelocity = 0; if (pet.Action is PetAction.Idle or PetAction.Walking or PetAction.Looking) pet.Action = PetAction.Sitting; }
        }
        if (shelfWatch > 0)
        {
            shelfWatch = Math.Max(0, shelfWatch - dt);
            if (PetActionRules.IsSettled(pet) && ShelfBesideCat)
            {
                pet.VX = pet.SmoothedWalkVelocity = 0;
                pet.Action = PetAction.Looking;
                pet.Facing = TrophyBounds.Left + ShelfArtWidth / 2 < pet.X + settings.Width / 2 ? -1 : 1;
            }
            if (shelfWatch == 0 && pet.Action == PetAction.Looking) { pet.Action = PetAction.Idle; behavior.Reset(pet); }
        }
        shelfAreaTimer += dt;
        if (shelfAreaTimer >= 1 && !shelfHeld) { shelfAreaTimer = 0; RefreshShelfArea(); }
        if (shelfHeld) UpdateShelfDrag();
        double before = ShelfFloor - shelf.Height;
        shelf.Step(dt, shelfArea.Left, Math.Max(shelfArea.Left, shelfArea.Right - ShelfArtWidth));
        double ceiling = Math.Max(0, ShelfFloor - shelfArea.Top - TrophyShelf.ArtHeight);
        if (shelf.Height > ceiling) { shelf.Height = ceiling; shelf.VY = -Math.Abs(shelf.VY) * 0.3; }
        if (shelf.Landed > 0)
        {
            shelfDust = 0; shelfDustStrength = Math.Clamp(shelf.Landed / 1200, 0.2, 1);
            if (shelf.Landed > 300) PlayPetSound("squish");
            shelf.Landed = 0;
        }
        if (shelfDust >= 0 && (shelfDust += dt / 0.45) >= 1) shelfDust = -1;
        CheckShelfBonk(before);
        TickShelfHop(dt);
        if (shelfEdgeCheck && shelf.Resting && !shelfHeld && shelfHop == ShelfHop.None)
        {
            shelfEdgeCheck = false;
            if (ShelfMotion.NearEdge(shelf.X, ShelfArtWidth, shelfArea.Left, shelfArea.Right)) StartShelfHop();
            else SaveShelfPlace();
        }
        PositionShelf();
    }

    // A falling shelf meets the top of the cat's head: bonk. The cat (awake, standing) sees stars;
    // a sleeping or waking cat sleeps on (the sleep lock). Either way the shelf bounces off sideways.
    private void CheckShelfBonk(double bottomBefore)
    {
        if (shelf.Held || shelf.VY > -150 || pet.Bonk > 0 || shelfHop == ShelfHop.Appear || !ShelfBesideCat) return;
        if (!pet.Grounded || pet.Action is PetAction.Dragging or PetAction.Climbing || PetActionRules.IsInFlight(pet.Action)) return;
        double headLeft = pet.X + 24, headRight = pet.X + 88, headTop = pet.Y + 16;
        double bottom = ShelfFloor - shelf.Height;
        if (shelf.X + ShelfArtWidth < headLeft || shelf.X > headRight || bottomBefore > headTop + 4 || bottom < headTop) return;
        shelf.Height = ShelfFloor - headTop;
        double catCenter = pet.X + settings.Width / 2, w = ShelfArtWidth;
        int side = featureRandom.NextDouble() < 0.5 ? -1 : 1;
        if (side < 0 && catCenter - settings.Width / 2 - w < shelfArea.Left) side = 1;
        else if (side > 0 && catCenter + settings.Width / 2 + w > shelfArea.Right) side = -1;
        shelf.Rebound(side, 250 + featureRandom.NextDouble() * 90);
        shelfEdgeCheck = true;
        PlayPetSound("clank");
        if (pet.Action is PetAction.Sleeping or PetAction.Waking || pet.Emotions.SleepLocked) return;
        CancelFeatureActivity();
        pet.Bonk = PetModel.BonkDuration;
        pet.Action = PetAction.Sitting;
        pet.VX = pet.SmoothedWalkVelocity = 0;
        System.Diagnostics.Trace.WriteLine("The keepsake shelf bonked the cat on the head.");
    }

    // Left at an edge: the cat startles, the shelf vanishes in a puff and pops up in the middle.
    private void StartShelfHop()
    {
        shelfHop = ShelfHop.Alarm;
        shelfHopTime = 0;
        double w = ShelfArtWidth, middle = (shelfArea.Left + shelfArea.Right - w) / 2;
        bool beside = ShelfBesideCat;
        if (beside && middle < pet.X + 102 && middle + w > pet.X + 10)
            // The cat is in the middle: land next to it, toward the middle.
            middle = pet.X + settings.Width / 2 < (shelfArea.Left + shelfArea.Right) / 2 ? pet.X + 102 + 24 : pet.X + 10 - 24 - w;
        double margin = ShelfMotion.EdgeMargin + 12;
        shelfHopTo = Math.Clamp(middle, shelfArea.Left + margin, Math.Max(shelfArea.Left + margin, shelfArea.Right - w - margin));
        if (beside && PetActionRules.IsSettled(pet) && pet.Bonk <= 0 && !BattleActive)
        {
            StopShelfShove();
            if (admiring) StopAdmiring();
            pet.Emotions.Startle(); // Off in manual control: the cat only looks.
            shelfWatch = ShelfAlarmSeconds + ShelfVanishSeconds + ShelfAppearSeconds + 0.3;
        }
        System.Diagnostics.Trace.WriteLine("The keepsake shelf was at the screen edge: back to the middle.");
    }

    private void TickShelfHop(double dt)
    {
        if (shelfHop == ShelfHop.None) return;
        shelfHopTime += dt;
        if (shelfHop == ShelfHop.Alarm && shelfHopTime >= ShelfAlarmSeconds) { shelfHop = ShelfHop.Vanish; shelfHopTime = 0; PlayPetSound("swoosh"); }
        else if (shelfHop == ShelfHop.Vanish && shelfHopTime >= ShelfVanishSeconds)
        {
            shelfHop = ShelfHop.Appear; shelfHopTime = 0;
            shelf.X = shelfHopTo; shelf.Height = 70; shelf.VX = shelf.VY = 0; shelf.Tilt = shelf.TiltV = 0;
            PlayPetSound("sparkle");
        }
        else if (shelfHop == ShelfHop.Appear && shelfHopTime >= ShelfAppearSeconds && shelf.Grounded) FinishShelfHop();
    }

    private void FinishShelfHop()
    {
        if (shelfHop == ShelfHop.None) return;
        if (shelfHop != ShelfHop.Appear) { shelf.X = shelfHopTo; shelf.Height = 0; shelf.VX = shelf.VY = 0; }
        shelfHop = ShelfHop.None;
        shelfHopTime = 0;
        shelfEdgeCheck = false;
        SaveShelfPlace();
    }

    private void SaveShelfPlace()
    {
        preferences.TrophyMonitor = shelfMonitor;
        preferences.TrophyFractionX = Math.Clamp((shelf.X - shelfArea.Left) / Math.Max(1, shelfArea.Right - shelfArea.Left - ShelfArtWidth), 0, 1);
        SavePreferences();
    }

    // Where the cat stands to look (just left of the shelf, facing it); null: nothing to look at here.
    private double? TrophySpot()
    {
        if (!ShelfBesideCat || shelfHeld || shelfHop != ShelfHop.None || !shelf.Grounded) return null;
        double target = shelf.X - settings.Width * 0.72;
        return target < bounds.Left ? null : target;
    }

    // For checks: send the cat to look now (as the timer would). False when it may not.
    public bool AdmireTrophies()
    {
        if (!CanPlay || EffectiveQuiet || BattleActive || FeatureOwnsMovement() || TrophySpot() == null || OnLedge) return false;
        admiring = true;
        admireRemaining = 0;
        return true;
    }

    // From TickFeatures (the pet free, not in Quiet). True while the visit owns the cat this frame.
    private bool TickAdmiring(double dt)
    {
        admireVisit -= dt;
        if (!admiring && admireVisit <= 0)
        {
            admireVisit = AdmireEvery + featureRandom.NextDouble() * 300;
            if (!returningHome && climbPlan == null && ledgeExit == 0 && !OnLedge && pet.Action is PetAction.Idle or PetAction.Sitting) AdmireTrophies();
        }
        if (!admiring) return false;
        if (TrophySpot() is not double target) { StopAdmiring(); return false; }
        if (admireRemaining > 0)
        {
            admireRemaining = Math.Max(0, admireRemaining - dt);
            pet.Action = PetAction.Looking; pet.VX = pet.SmoothedWalkVelocity = 0; pet.Facing = 1;
            if (admireRemaining <= 0) StopAdmiring();
            return true;
        }
        if (Math.Abs(target - pet.X) < 8)
        {
            admireRemaining = AdmireSeconds;
            pet.Action = PetAction.Looking; pet.VX = pet.SmoothedWalkVelocity = 0; pet.Facing = 1;
            System.Diagnostics.Trace.WriteLine("The cat looks at its keepsakes.");
        }
        else WalkToward(target);
        return true;
    }

    private void StopAdmiring()
    {
        if (!admiring) return;
        admiring = false;
        admireRemaining = 0;
        if (pet.Action is PetAction.Looking or PetAction.Walking) pet.Action = PetAction.Idle;
        pet.VX = 0;
        behavior.Reset(pet);
    }

    // Where the cat stands to shove the shelf in this direction (null: no room on that side).
    private double? ShoveSpot(int direction)
    {
        double target = direction > 0 ? shelf.X - settings.Width * 0.8 : shelf.X + ShelfArtWidth - settings.Width * 0.2;
        return target < bounds.Left || target > bounds.Right - settings.Width ? null : target;
    }

    // For checks: the cat goes to shove the shelf now (as the timer would). False when it may not.
    public bool ShoveShelf(int? direction = null)
    {
        if (!CanPlay || EffectiveQuiet || BattleActive || FeatureOwnsMovement() || OnLedge || pet.LedgeId != 0 || !ShelfBesideCat
            || shelfHeld || shelfHop != ShelfHop.None || !shelf.Resting) return false;
        int dir = direction ?? (featureRandom.NextDouble() < 0.5 ? -1 : 1);
        if (ShoveSpot(dir) == null) dir = -dir;
        if (ShoveSpot(dir) == null) return false;
        shoveDirection = dir;
        shelfShove = ShelfShove.Approach;
        shoveTime = 0;
        return true;
    }

    // From TickFeatures (the pet free, not in Quiet). True while the shove owns the cat this frame.
    private bool TickShelfShove(double dt)
    {
        shoveVisit -= dt;
        if (shelfShove == ShelfShove.None && shoveVisit <= 0)
        {
            double every = 180 + featureRandom.NextDouble() * 180;
            shoveVisit = every * (preferences.Personality switch { "Playful" => 0.6, "Calm" => 1.6, _ => 1 });
            if (!returningHome && climbPlan == null && ledgeExit == 0 && pet.Action is PetAction.Idle or PetAction.Sitting) ShoveShelf();
        }
        if (shelfShove == ShelfShove.None) return false;
        shoveTime += dt;
        if (!ShelfBesideCat || shelfHeld || shelfHop != ShelfHop.None) { StopShelfShove(); return false; }
        switch (shelfShove)
        {
            case ShelfShove.Approach:
                if (ShoveSpot(shoveDirection) is not double target || shoveTime > 12) { StopShelfShove(); return false; }
                if (Math.Abs(target - pet.X) < 6) { shelfShove = ShelfShove.Crouch; shoveTime = 0; pet.VX = pet.SmoothedWalkVelocity = 0; }
                else WalkToward(target);
                break;
            case ShelfShove.Crouch:
                // Wiggles down, eyes on the shelf, then the push.
                pet.Action = PetAction.Sitting; pet.Facing = shoveDirection; pet.VX = pet.SmoothedWalkVelocity = 0;
                pet.PlayCrouch = Math.Min(1, shoveTime / 0.3);
                if (shoveTime >= ShoveCrouchSeconds)
                {
                    pet.PlayCrouch = 0;
                    pet.BatRemaining = 0.3;
                    shelf.Shove(shoveDirection, 200 + featureRandom.NextDouble() * 200);
                    shelfEdgeCheck = true;
                    PlayPetSound("swoosh");
                    shelfShove = ShelfShove.Watch; shoveTime = 0;
                    System.Diagnostics.Trace.WriteLine("The cat shoves its keepsake shelf.");
                }
                break;
            case ShelfShove.Watch:
                pet.Action = PetAction.Looking; pet.VX = pet.SmoothedWalkVelocity = 0;
                pet.Facing = shelf.X + ShelfArtWidth / 2 < pet.X + settings.Width / 2 ? -1 : 1;
                if (shoveTime >= 0.3 && shoveTime - dt < 0.3) pet.Emotions.Cheer();
                if (shoveTime >= ShoveWatchSeconds) { StopShelfShove(); return false; }
                break;
        }
        return true;
    }

    private void StopShelfShove()
    {
        if (shelfShove == ShelfShove.None) return;
        shelfShove = ShelfShove.None;
        pet.PlayCrouch = 0;
        if (pet.Action is PetAction.Looking or PetAction.Walking or PetAction.Sitting) pet.Action = PetAction.Idle;
        pet.VX = 0;
        behavior.Reset(pet);
    }

    private void CloseTrophies() { trophyWindow?.Close(); trophyWindow = null; }
}
