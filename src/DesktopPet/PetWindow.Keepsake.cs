using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopPet.AI;
using Forms = System.Windows.Forms;

namespace DesktopPet;

// Expedition milestone 4: the display corner. One keepsake a won run gave (a cup or the slime
// cushion; chosen in Settings → Pet or its right-click menu, else the best one) stands, drawn 1.2×
// (about half the cat), on a small wooden base on the floor of a monitor or on a window's top edge
// (PetWindow.KeepsakePlay.cs). On once the first keepsake is earned; Settings → Pet turns it off. Never takes focus, no taskbar button; hidden while a
// fullscreen app runs or a battle is on.
// The keepsake is a cartoon object (KeepsakeMotion): the user picks it up (past the drag threshold), it
// dangles, is thrown, falls, lands with a squash and a bounce; dropped on the cat's head it bonks the
// cat (stars) and bounces off to one side. Now and then the cat shoves it along. Left at a screen
// edge, the cat is startled and the keepsake vanishes in a puff and pops up in the middle.
// Now and then (not in Quiet, asleep, held, in a battle) the cat walks over and looks at it for a
// few seconds. All C#; Python is not involved.
public sealed partial class PetWindow
{
    private Window? keepsakeWindow;
    private KeepsakeArt? keepsakeArt;
    private string? trophyShownId;
    // Seconds until the cat next goes to look at the keepsake, and how long it still looks.
    private double admireVisit = 300, admireRemaining;
    private bool admiring;
    public const double AdmireSeconds = 4, AdmireEvery = 600;
    public const double KeepsakeSide = 56, KeepsakeTopRoom = 64, KeepsakeBottomRoom = 36, DisplayScale = 1.2;

    // The keepsake's place: its monitor (in that monitor's DIPs) and its motion.
    private readonly KeepsakeMotion keepsake = new();
    private string? keepsakeMonitor;
    private Area keepsakeArea;
    private double keepsakeScale = 1, keepsakeAreaTimer;
    private bool keepsakePlaced, keepsakeEdgeCheck;
    // Input: pressed (not yet past the drag threshold) or held; the grab point from the art's left
    // edge and up from its bottom.
    private bool keepsakePress, keepsakeHeld;
    private Point keepsakePressAt;
    private double keepsakeGrabX, keepsakeGrabY;
    private readonly ThrowTracker keepsakeTracker = new();
    // Effects: the landing's dust and sound, the same rule as the cat's (PetImpact.cs).
    private readonly Landing keepsakeLanding = new();

    // Back to the middle from a screen edge: the alarm (shakes, the cat startled), the vanish (a
    // puff) and the appear (pops up in the middle, drops, lands).
    private enum KeepsakeHop { None, Alarm, Vanish, Appear }
    private KeepsakeHop keepsakeHop;
    private double keepsakeHopTime, keepsakeHopTo;
    public const double KeepsakeAlarmSeconds = 0.8, KeepsakeVanishSeconds = 0.3, KeepsakeAppearSeconds = 0.55;
    // Seconds the cat still watches the keepsake (the hop); while it does it stands still.
    private double keepsakeWatch;
    // The hourglass keepsake: the share of its sand that has run down (full on top when it is put on
    // display) and, after the user carried it and let go, how far it is into turning over (seconds;
    // negative: upright). Turned over, what had run down is on top again.
    private double hourglassSand, hourglassFlip = -1;
    public const double HourglassRunSeconds = 60, HourglassFlipSeconds = 0.35;

    // The cat's shove: walk to one side, crouch, push, watch it slide.
    private enum KeepsakeShove { None, Approach, Crouch, Watch }
    private KeepsakeShove keepsakeShove;
    private double shoveVisit = 200, shoveTime;
    private int shoveDirection;
    public const double ShoveCrouchSeconds = 0.45, ShoveWatchSeconds = 1.6;

    // The keepsakes in the saved Expedition progress (never the in-memory one of custom data), read
    // with the built-in definitions.
    private ExpeditionProgress SavedExpeditionProgress =>
        expeditionProgress ??= ExpeditionProgressFile != null ? ExpeditionProgress.Load(ExpeditionProgressFile) : new();
    // The Expedition's keepsakes, then Infinity's milestone rewards (earned from the battle records),
    // then (1.5 M22) the Adventure stages' (given with the stage's reward, in the saved progress).
    private static IEnumerable<KeepsakeSpec> AdventureKeepsakes =>
        BattleData.Settings.Campaign?.Chapters.SelectMany(c => c.Stages).Select(s => s.Keepsake).OfType<KeepsakeSpec>() ?? Enumerable.Empty<KeepsakeSpec>();
    private static IReadOnlyList<KeepsakeSpec> KeepsakeList =>
        (BattleData.Settings.Expedition?.Keepsakes ?? Array.Empty<KeepsakeSpec>()).Concat(EndlessRewards?.Milestones ?? Array.Empty<KeepsakeSpec>()).Concat(AdventureKeepsakes).ToList();
    public IReadOnlyList<string> Keepsakes => SavedExpeditionProgress.Keepsakes.Concat(EndlessEarned.Select(m => m.Id))
        .Concat(AdventureKeepsakes.Where(k => AdventureProgress.Rewards.Contains(k.Id)).Select(k => k.Id)).ToList();
    // Shown on the keepsake: the cushion first, then the cups bronze → gold, the Adventure's plush, then Infinity's (by wave).
    public IReadOnlyList<KeepsakeSpec> ShownKeepsakes => KeepsakeList.Where(k => k.Shown && Keepsakes.Contains(k.Id))
        .OrderBy(k => k.Kind == KeepsakeSpec.Cushion ? -1 : k.Wave > 0 ? 10 + k.Wave : k.Kind == KeepsakeSpec.Plush ? 5 : Array.IndexOf(KeepsakeSpec.Tiers, k.Tier)).ToList();
    // The one on display: the chosen one if owned, else the best (gold → silver → bronze → cushion).
    public KeepsakeSpec? DisplayedKeepsake
    {
        get
        {
            var owned = ShownKeepsakes;
            return owned.FirstOrDefault(k => k.Id == preferences.TrophyShown) ?? owned.LastOrDefault(k => k.Kind == KeepsakeSpec.Cup) ?? owned.FirstOrDefault();
        }
    }
    private bool KeepsakeAccessory(string id) => KeepsakeList.Any(k => k.Accessory == id && Keepsakes.Contains(k.Id));

    public bool TrophiesVisible => keepsakeWindow?.IsVisible == true;
    private double KeepsakeArtWidth => keepsakeArt == null ? 0 : KeepsakeArt.WidthFor(keepsakeArt.Items) * DisplayScale;
    private static double KeepsakeArtHeight => KeepsakeArt.ArtHeight * DisplayScale;
    private double KeepsakeFloor => keepsakeArea.Bottom;
    // The keepsake's art (not the room around it), in its monitor's DIPs.
    public Rect TrophyBounds => keepsakeWindow == null ? Rect.Empty
        : new Rect(keepsake.X, KeepsakeFloor - keepsake.Height - KeepsakeArtHeight, KeepsakeArtWidth, KeepsakeArtHeight);
    public bool Admiring => admiring;
    public KeepsakeMotion TrophyMotion => keepsake;
    public bool KeepsakeHeld => keepsakeHeld;
    public bool KeepsakeHopping => keepsakeHop != KeepsakeHop.None;
    public bool KeepsakeShoving => keepsakeShove != KeepsakeShove.None;
    // The cat and the keepsake are on the same monitor (same DIPs): only then do they meet.
    private bool KeepsakeBesideCat => TrophiesVisible && keepsakeMonitor == CurrentMonitor().DeviceName && Math.Abs(keepsakeScale - scale) < 0.01;

    // Every frame, paused or not (cheap: only a list count and a position).
    private void TickTrophies()
    {
        var displayed = preferences.ShowTrophies && !context.Fullscreen && !BattleActive ? DisplayedKeepsake : null;
        if (displayed == null)
        {
            if (keepsakeWindow?.IsVisible == true)
            {
                EndKeepsakeDrag(false);
                FinishKeepsakeHop();
                keepsakeShove = KeepsakeShove.None; keepsakeWatch = 0;
                LeaveKeepsakeLedge(); // Shown again: on the floor.
                keepsakeWindow.Hide();
            }
            return;
        }
        if (keepsakeWindow == null)
        {
            keepsakeArt = new KeepsakeArt { Side = KeepsakeSide, TopRoom = KeepsakeTopRoom, BottomRoom = KeepsakeBottomRoom, Scale = DisplayScale, Cursor = Cursors.Hand };
            // Clickable on the art (the transparent room passes clicks through).
            keepsakeWindow = CreateOverlayWindow(keepsakeArt, "Pet trophies");
            keepsakeArt.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount >= 2) CallCatToLook(); // PetWindow.KeepsakePlay.cs
                else PressKeepsake();
                e.Handled = true;
            };
            keepsakeArt.MouseEnter += (_, _) => HoverKeepsake();
            keepsakeArt.MouseMove += (_, _) => CheckKeepsakeDragThreshold();
            keepsakeArt.MouseLeftButtonUp += (_, _) => EndKeepsakeDrag(true);
            keepsakeArt.LostMouseCapture += (_, _) => EndKeepsakeDrag(false);
            keepsakeWindow.Closed += (_, _) => { keepsakeWindow = null; keepsakeArt = null; trophyShownId = null; keepsakeHeld = keepsakePress = false; };
            trophyShownId = null;
            InitializeKeepsakeMenu();
        }
        if (trophyShownId != displayed.Id)
        {
            trophyShownId = displayed.Id;
            keepsakeArt!.Items = new[] { displayed };
            hourglassSand = 0; hourglassFlip = -1;
            keepsakeWindow.Width = keepsakeArt.Width;
            keepsakeWindow.Height = keepsakeArt.Height;
            if (keepsakePlaced) keepsake.X = Math.Clamp(keepsake.X, keepsakeArea.Left, Math.Max(keepsakeArea.Left, keepsakeArea.Right - KeepsakeArtWidth));
        }
        if (!keepsakePlaced) PlaceKeepsake();
        if (!keepsakeWindow.IsVisible) { keepsakeWindow.Show(); KeepCatAboveKeepsake(); }
        PositionKeepsake();
    }

    // First show: where it was left (its monitor, across the free width), else 3/4 across the
    // cat's monitor; never at an edge (straight to the middle, no show).
    private void PlaceKeepsake()
    {
        keepsakePlaced = true;
        keepsakeMonitor = preferences.TrophyMonitor;
        RefreshKeepsakeArea(keepPlace: false);
        double free = Math.Max(0, keepsakeArea.Right - keepsakeArea.Left - KeepsakeArtWidth);
        bool remembered = preferences.TrophyMonitor == keepsakeMonitor && preferences.TrophyFractionX.HasValue;
        keepsake.X = keepsakeArea.Left + (remembered ? preferences.TrophyFractionX!.Value : 0.75) * free;
        if (KeepsakeMotion.NearEdge(keepsake.X, KeepsakeArtWidth, keepsakeArea.Left, keepsakeArea.Right)) keepsake.X = keepsakeArea.Left + free / 2;
        keepsake.Height = keepsake.VX = keepsake.VY = 0;
    }

    // The keepsake's monitor (gone: the cat's) in its own DIPs. The cat's monitor uses the cat's scale.
    private void RefreshKeepsakeArea(bool keepPlace = true)
    {
        var catScreen = CurrentMonitor();
        var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == keepsakeMonitor) ?? catScreen;
        bool moved = screen.DeviceName != keepsakeMonitor;
        keepsakeMonitor = screen.DeviceName;
        double at = screen.DeviceName == catScreen.DeviceName ? scale : MonitorScale(screen);
        if (keepPlace && Math.Abs(at - keepsakeScale) > 0.001)
        {
            double ratio = keepsakeScale / at;
            keepsake.X *= ratio; keepsake.Height *= ratio; keepsake.VX *= ratio; keepsake.VY *= ratio;
            keepsakeTracker.Clear();
        }
        keepsakeScale = at;
        var work = screen.WorkingArea;
        var old = keepsakeArea;
        keepsakeArea = new Area(work.Left / at, work.Top / at, work.Right / at, work.Bottom / at);
        if (!keepPlace) return;
        // Its monitor was unplugged: the same spot across the new one.
        if (moved) keepsake.X = keepsakeArea.Left + Math.Clamp((keepsake.X - old.Left) / Math.Max(1, old.Right - old.Left - KeepsakeArtWidth), 0, 1) * Math.Max(0, keepsakeArea.Right - keepsakeArea.Left - KeepsakeArtWidth);
        keepsake.X = Math.Clamp(keepsake.X, keepsakeArea.Left, Math.Max(keepsakeArea.Left, keepsakeArea.Right - KeepsakeArtWidth));
        keepsake.Height = Math.Clamp(keepsake.Height, 0, Math.Max(0, KeepsakeFloor - keepsakeArea.Top - KeepsakeArtHeight));
    }

    private void PositionKeepsake()
    {
        if (keepsakeWindow == null || keepsakeArt == null) return;
        // The room below the art hangs past the floor (over the taskbar): transparent, clicks pass through.
        MoveWindow(keepsakeWindow, keepsake.X - KeepsakeSide, KeepsakeFloor - keepsake.Height + KeepsakeBottomRoom - keepsakeWindow.Height, keepsakeScale);
        double pop = 1, shake = 0, puff = -1, t = keepsakeHopTime;
        switch (keepsakeHop)
        {
            case KeepsakeHop.Alarm:
                // Trembles harder and harder, with a nervous little pulse.
                shake = Math.Sin(t * 70) * 2.6 * Math.Min(1, t / KeepsakeAlarmSeconds + 0.2);
                pop = 1 + 0.05 * Math.Sin(t * 28);
                break;
            case KeepsakeHop.Vanish:
                double v = t / KeepsakeVanishSeconds;
                pop = v < 0.25 ? 1 + v / 0.25 * 0.18 : 1.18 * (1 - (v - 0.25) / 0.75);
                puff = v * 0.45;
                break;
            case KeepsakeHop.Appear:
                double u = Math.Min(1, t / 0.32);
                // Ease out with an overshoot: pops up a bit too big, then settles.
                pop = 1 + 2.7 * Math.Pow(u - 1, 3) + 1.7 * Math.Pow(u - 1, 2);
                puff = 0.35 + 0.65 * Math.Min(1, t / KeepsakeAppearSeconds);
                break;
        }
        pop *= 1 + 0.06 * keepsakeHoverBlend;
        Point? pivot = keepsakeHeld ? new Point(keepsakeGrabX / DisplayScale, (KeepsakeArtHeight - keepsakeGrabY) / DisplayScale) : null;
        keepsakeArt.SetPose(keepsake.ScaleX, keepsake.ScaleY, keepsake.Tilt, pivot, Math.Max(0, pop), shake, keepsakeLanding, puff);
    }

    // Keepsake below the cat: both are topmost, and the keepsake was shown later.
    private void KeepCatAboveKeepsake()
    {
        if (keepsakeWindow == null) return;
        var keepsakeHandle = new WindowInteropHelper(keepsakeWindow).Handle;
        var catHandle = new WindowInteropHelper(this).Handle;
        const uint NoSize = 0x0001, NoMove = 0x0002, NoActivate = 0x0010;
        if (keepsakeHandle != IntPtr.Zero && catHandle != IntPtr.Zero) SetWindowPos(keepsakeHandle, catHandle, 0, 0, 0, 0, NoSize | NoMove | NoActivate);
    }

    private Point KeepsakeCursor()
    {
        var point = ScreenCursor();
        return new Point(point.X / keepsakeScale, point.Y / keepsakeScale);
    }

    private void PressKeepsake()
    {
        if (paused || keepsakeHop != KeepsakeHop.None || BattleActive || keepsakeArt == null || !keepsakeArt.CaptureMouse()) return;
        keepsakePress = true;
        keepsakePressAt = KeepsakeCursor();
    }

    private void CheckKeepsakeDragThreshold()
    {
        if (!keepsakePress || keepsakeArt?.IsMouseCaptured != true) return;
        if ((KeepsakeCursor() - keepsakePressAt).Length < settings.DragThreshold) return;
        StartKeepsakeDrag();
    }

    private void StartKeepsakeDrag()
    {
        keepsakePress = false;
        keepsakeHeld = true;
        StopKeepsakeShove();
        if (admiring) StopAdmiring();
        var cursor = KeepsakeCursor();
        keepsakeGrabX = Math.Clamp(cursor.X - keepsake.X, 0, KeepsakeArtWidth);
        keepsakeGrabY = Math.Clamp(KeepsakeFloor - keepsake.Height - cursor.Y, 0, KeepsakeArtHeight);
        keepsake.Held = true;
        LeaveKeepsakeLedge();
        keepsake.VX = keepsake.VY = 0;
        keepsake.SquashV -= 3; // Lifted: a little stretch.
        keepsakeTracker.Clear();
        keepsakeTracker.Add(clock.Elapsed.TotalSeconds, cursor.X, cursor.Y);
        PlayPetSound("swoosh");
    }

    // Test hook (and the drag's own frame): carry the held keepsake to the cursor.
    private void UpdateKeepsakeDrag()
    {
        var point = ScreenCursor();
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)point.X, (int)point.Y));
        if (screen.DeviceName != keepsakeMonitor)
        {
            // Carried onto another monitor: its DIPs from now on, and no throw across the two.
            keepsakeMonitor = screen.DeviceName;
            RefreshKeepsakeArea();
        }
        var cursor = KeepsakeCursor();
        keepsakeTracker.Add(clock.Elapsed.TotalSeconds, cursor.X, cursor.Y);
        double w = KeepsakeArtWidth;
        keepsake.X = Math.Clamp(cursor.X - keepsakeGrabX, keepsakeArea.Left, Math.Max(keepsakeArea.Left, keepsakeArea.Right - w));
        keepsake.Height = Math.Clamp(KeepsakeFloor - (cursor.Y + keepsakeGrabY), 0, Math.Max(0, KeepsakeFloor - keepsakeArea.Top - KeepsakeArtHeight));
        // Dangles: swings back against the way it is carried.
        keepsake.HeldTilt = Math.Clamp(keepsakeTracker.Velocity(3000).X * 0.018, -28, 28);
    }

    private void EndKeepsakeDrag(bool throwIt)
    {
        if (keepsakePress)
        {
            keepsakePress = false;
            keepsakeArt?.ReleaseMouseCapture();
            // A click: boing.
            keepsake.Boing();
            PlayPetSound("squish");
            return;
        }
        if (!keepsakeHeld) return;
        keepsakeHeld = false; // Before releasing capture; LostMouseCapture is synchronous.
        keepsakeArt?.ReleaseMouseCapture();
        var (vx, vy) = throwIt && !paused ? keepsakeTracker.Velocity(2200) : (0.0, 0.0);
        keepsake.Release(vx, vy);
        if (HourglassShown && hourglassFlip < 0) hourglassFlip = 0;
        keepsakeEdgeCheck = true;
        KeepCatAboveKeepsake();
    }

    // From Tick, every frame: the keepsake's physics, the bonk, the hop and the cat's watching.
    // A paused pet freezes all of it (the keepsake hangs in the air with its velocity).
    private void TickKeepsake(double dt)
    {
        if (!TrophiesVisible && (cushionNap != CushionNap.None || napLift > 0)) { cushionNap = CushionNap.None; napLift = 0; } // The cushion is gone.
        if (paused || !TrophiesVisible) return;
        if (pet.Bonk > 0)
        {
            pet.Bonk = Math.Max(0, pet.Bonk - dt);
            if (PetActionRules.IsSettled(pet)) { pet.VX = pet.SmoothedWalkVelocity = 0; if (pet.Action is PetAction.Idle or PetAction.Walking or PetAction.Looking) pet.Action = PetAction.Sitting; }
        }
        if (keepsakeWatch > 0)
        {
            keepsakeWatch = Math.Max(0, keepsakeWatch - dt);
            if (PetActionRules.IsSettled(pet) && KeepsakeBesideCat) HoldStill(PetAction.Looking, FacingKeepsake);
            if (keepsakeWatch == 0 && pet.Action == PetAction.Looking) { pet.Action = PetAction.Idle; behavior.Reset(pet); }
        }
        keepsakeAreaTimer += dt;
        if (keepsakeAreaTimer >= 1 && !keepsakeHeld) { keepsakeAreaTimer = 0; RefreshKeepsakeArea(); }
        if (keepsakeHeld) UpdateKeepsakeDrag();
        WatchHeldKeepsake(); // PetWindow.KeepsakePlay.cs
        FollowKeepsakeLedge();
        keepsakeHoverBlend += ((keepsakeArt?.IsMouseOver == true && !keepsakeHeld ? 1 : 0) - keepsakeHoverBlend) * (1 - Math.Exp(-dt * 12));
        double before = KeepsakeFloor - keepsake.Height;
        keepsake.Step(dt, keepsakeArea.Left, Math.Max(keepsakeArea.Left, keepsakeArea.Right - KeepsakeArtWidth));
        LandKeepsakeOnLedge(before);
        double ceiling = Math.Max(0, KeepsakeFloor - keepsakeArea.Top - KeepsakeArtHeight);
        if (keepsake.Height > ceiling) { keepsake.Height = ceiling; keepsake.VY = -Math.Abs(keepsake.VY) * 0.3; }
        keepsakeLanding.Step(dt);
        if (keepsake.Landed > 0)
        {
            keepsakeLanding.Hit(keepsake.Landed);
            if (keepsakeLanding.TakeSound()) PlayPetSound(Landing.Sound);
            if (keepsake.Landed > Landing.SoundSpeed) NoticeKeepsakeLanding();
            keepsake.Landed = 0;
        }
        CheckKeepsakeBonk(before);
        TickKeepsakeHop(dt);
        TickHourglass(dt);
        TickNapLift(dt);
        if (keepsakeEdgeCheck && keepsake.Resting && !keepsakeHeld && keepsakeHop == KeepsakeHop.None)
        {
            keepsakeEdgeCheck = false;
            if (KeepsakeMotion.NearEdge(keepsake.X, KeepsakeArtWidth, keepsakeArea.Left, keepsakeArea.Right)) StartKeepsakeHop();
            else SaveKeepsakePlace();
        }
        PositionKeepsake();
    }

    private bool HourglassShown => keepsakeArt?.Items.Any(k => k.Kind == KeepsakeSpec.Hourglass) == true;

    // The sand runs down (a paused pet stops it with everything else); a turn over ends with the sand
    // that had run down on top. The art is drawn again only when the level moves a visible step.
    private void TickHourglass(double dt)
    {
        if (!HourglassShown || keepsakeArt == null) return;
        double flip = 0;
        if (hourglassFlip >= 0)
        {
            hourglassFlip += dt;
            if (hourglassFlip >= HourglassFlipSeconds) { hourglassFlip = -1; hourglassSand = 1 - hourglassSand; }
            else flip = hourglassFlip / HourglassFlipSeconds;
        }
        else hourglassSand = Math.Min(1, hourglassSand + dt / HourglassRunSeconds);
        keepsakeArt.SetHourglass(Math.Round(hourglassSand * 60) / 60, flip);
    }

    // The keepsake hits the cat: falling onto the top of its head, or thrown fast into its side. Bonk:
    // stars, and the keepsake bounces off. A sleeping cat is woken by it (approved exception to the
    // sleep lock, as a click: waking, then the wake cooldown) and sees stars too.
    private void CheckKeepsakeBonk(double bottomBefore)
    {
        if (keepsake.Held || pet.Bonk > 0 || keepsakeHop != KeepsakeHop.None || !KeepsakeBesideCat || cushionNap == CushionNap.Asleep) return;
        if (!pet.Grounded || pet.Action is PetAction.Dragging or PetAction.Climbing || PetActionRules.IsInFlight(pet.Action)) return;
        double w = KeepsakeArtWidth, bottom = KeepsakeFloor - keepsake.Height, top = bottom - KeepsakeArtHeight;
        double headLeft = pet.X + PetSettings.HeadLeft, headRight = pet.X + PetSettings.HeadRight, headTop = pet.Y + PetSettings.HeadTop;
        bool onHead = keepsake.VY <= -150 && keepsake.X + w >= headLeft && keepsake.X <= headRight && bottomBefore <= headTop + 4 && bottom >= headTop;
        double bodyLeft = pet.X + PetSettings.BodyLeft, bodyRight = pet.X + PetSettings.BodyRight, bodyTop = pet.Y + PetSettings.BodyTop, bodyBottom = pet.Y + settings.Height;
        bool side = !onHead && Math.Abs(keepsake.VX) >= 260 && Math.Sign(keepsake.VX) == Math.Sign(CatCenter - KeepsakeCenter)
            && keepsake.X + w > bodyLeft && keepsake.X < bodyRight && bottom > bodyTop && top < bodyBottom;
        if (!onHead && !side) return;
        pet.Facing = KeepsakeCenter < CatCenter ? -1 : 1;
        if (onHead)
        {
            keepsake.Height = KeepsakeFloor - headTop;
            int away = featureRandom.NextDouble() < 0.5 ? -1 : 1;
            if (away < 0 && CatCenter - settings.Width / 2 - w < keepsakeArea.Left) away = 1;
            else if (away > 0 && CatCenter + settings.Width / 2 + w > keepsakeArea.Right) away = -1;
            keepsake.Rebound(away, 250 + featureRandom.NextDouble() * 90);
        }
        else
        {
            // Bounces back off the cat's side.
            int back = keepsake.VX > 0 ? -1 : 1;
            keepsake.X = Math.Clamp(back < 0 ? bodyLeft - w : bodyRight, keepsakeArea.Left, Math.Max(keepsakeArea.Left, keepsakeArea.Right - w));
            keepsake.Rebound(back, Math.Abs(keepsake.VX) * 0.35 + 60);
            keepsake.VY = 260;
        }
        LeaveKeepsakeLedge();
        keepsakeEdgeCheck = true;
        PlayPetSound("clank");
        if (pet.Action is PetAction.Sleeping or PetAction.Waking || pet.Emotions.SleepLocked)
        {
            behavior.WakeByClick(pet);
            pet.Bonk = PetModel.BonkDuration;
            System.Diagnostics.Trace.WriteLine("The keepsake fell on the sleeping cat: it wakes up dazed.");
            return;
        }
        CancelFeatureActivity();
        pet.Bonk = PetModel.BonkDuration;
        pet.Action = PetAction.Sitting;
        pet.VX = pet.SmoothedWalkVelocity = 0;
        System.Diagnostics.Trace.WriteLine(onHead ? "The keepsake bonked the cat on the head." : "The keepsake was thrown into the cat.");
    }

    // Left at an edge: the cat startles, the keepsake vanishes in a puff and pops up in the middle.
    // alarm false (the menu's "Back to the middle"): straight to the puff, the cat not startled.
    private void StartKeepsakeHop(bool alarm = true)
    {
        keepsakeHop = alarm ? KeepsakeHop.Alarm : KeepsakeHop.Vanish;
        keepsakeHopTime = 0;
        if (!alarm) PlayPetSound("swoosh");
        double w = KeepsakeArtWidth, middle = (keepsakeArea.Left + keepsakeArea.Right - w) / 2;
        bool beside = KeepsakeBesideCat;
        if (beside && middle < pet.X + 102 && middle + w > pet.X + 10)
            // The cat is in the middle: land next to it, toward the middle.
            middle = pet.X + settings.Width / 2 < (keepsakeArea.Left + keepsakeArea.Right) / 2 ? pet.X + 102 + 24 : pet.X + 10 - 24 - w;
        double margin = KeepsakeMotion.EdgeMargin + 12;
        keepsakeHopTo = Math.Clamp(middle, keepsakeArea.Left + margin, Math.Max(keepsakeArea.Left + margin, keepsakeArea.Right - w - margin));
        if (alarm && beside && PetActionRules.IsSettled(pet) && pet.Bonk <= 0 && !BattleActive)
        {
            StopKeepsakeShove();
            if (admiring) StopAdmiring();
            pet.Emotions.Startle(); // Off in manual control: the cat only looks.
            keepsakeWatch = KeepsakeAlarmSeconds + KeepsakeVanishSeconds + KeepsakeAppearSeconds + 0.3;
        }
        System.Diagnostics.Trace.WriteLine("The keepsake was at the screen edge: back to the middle.");
    }

    private void TickKeepsakeHop(double dt)
    {
        if (keepsakeHop == KeepsakeHop.None) return;
        keepsakeHopTime += dt;
        if (keepsakeHop == KeepsakeHop.Alarm && keepsakeHopTime >= KeepsakeAlarmSeconds) { keepsakeHop = KeepsakeHop.Vanish; keepsakeHopTime = 0; PlayPetSound("swoosh"); }
        else if (keepsakeHop == KeepsakeHop.Vanish && keepsakeHopTime >= KeepsakeVanishSeconds)
        {
            keepsakeHop = KeepsakeHop.Appear; keepsakeHopTime = 0;
            if (keepsakeSwapTo != null) { ApplyKeepsakeSwap(); keepsake.Height = keepsake.Ground + 24; }
            else { keepsake.X = keepsakeHopTo; LeaveKeepsakeLedge(); keepsake.Height = 70; }
            keepsake.VX = keepsake.VY = 0; keepsake.Tilt = keepsake.TiltV = 0;
            PlayPetSound("sparkle");
        }
        else if (keepsakeHop == KeepsakeHop.Appear && keepsakeHopTime >= KeepsakeAppearSeconds && keepsake.Grounded) FinishKeepsakeHop();
    }

    private void FinishKeepsakeHop()
    {
        if (keepsakeHop == KeepsakeHop.None) return;
        if (keepsakeHop != KeepsakeHop.Appear)
        {
            if (keepsakeSwapTo != null) { ApplyKeepsakeSwap(); keepsake.Height = keepsake.Ground; }
            else { keepsake.X = keepsakeHopTo; LeaveKeepsakeLedge(); keepsake.Height = 0; }
            keepsake.VX = keepsake.VY = 0;
        }
        keepsakeHop = KeepsakeHop.None;
        keepsakeHopTime = 0;
        keepsakeEdgeCheck = false;
        SaveKeepsakePlace();
    }

    private void SaveKeepsakePlace()
    {
        preferences.TrophyMonitor = keepsakeMonitor;
        preferences.TrophyFractionX = Math.Clamp((keepsake.X - keepsakeArea.Left) / Math.Max(1, keepsakeArea.Right - keepsakeArea.Left - KeepsakeArtWidth), 0, 1);
        SavePreferences();
    }

    // Where the cat stands to look (just left of the keepsake, facing it); null: nothing to look at here.
    private double? TrophySpot()
    {
        if (!KeepsakeWithCat || keepsakeHeld || keepsakeHop != KeepsakeHop.None || !keepsake.Grounded) return null;
        return CatCanStand(keepsake.X - settings.Width * 0.72);
    }

    // For checks: send the cat to look now (as the timer would). False when it may not.
    public bool AdmireTrophies()
    {
        if (!MayAct(AutoKind.Admire) || TrophySpot() == null) return false;
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
            if (!returningHome && climbPlan == null && ledgeExit == 0 && pet.Action is PetAction.Idle or PetAction.Sitting) AdmireTrophies();
        }
        if (!admiring) return false;
        if (TrophySpot() is not double target) { StopAdmiring(); return false; }
        if (admireRemaining > 0)
        {
            admireRemaining = Math.Max(0, admireRemaining - dt);
            HoldStill(PetAction.Looking, 1);
            if (admireRemaining <= 0) StopAdmiring();
            return true;
        }
        if (Math.Abs(target - pet.X) < 8)
        {
            admireRemaining = AdmireSeconds;
            HoldStill(PetAction.Looking, 1);
            System.Diagnostics.Trace.WriteLine("The cat looks at its keepsakes.");
        }
        else WalkToward(target);
        return true;
    }

    private void StopAdmiring()
    {
        if (!admiring) return;
        admiring = false; userAsked &= ~UserAsked.Admire;
        admireRemaining = 0;
        if (pet.Action is PetAction.Looking or PetAction.Walking) pet.Action = PetAction.Idle;
        pet.VX = 0;
        behavior.Reset(pet);
    }

    // Where the cat stands to shove the keepsake in this direction (null: no room on that side).
    private double? ShoveSpot(int direction)
    {
        return CatCanStand(direction > 0 ? keepsake.X - settings.Width * 0.8 : keepsake.X + KeepsakeArtWidth - settings.Width * 0.2);
    }

    // For checks: the cat goes to shove the keepsake now (as the timer would). False when it may not.
    public bool ShoveKeepsake(int? direction = null)
    {
        if (!MayAct(AutoKind.ShoveKeepsake) || !KeepsakeWithCat
            || keepsakeHeld || keepsakeHop != KeepsakeHop.None || !keepsake.Resting) return false;
        int dir = direction ?? (featureRandom.NextDouble() < 0.5 ? -1 : 1);
        if (ShoveSpot(dir) == null) dir = -dir;
        if (ShoveSpot(dir) == null) return false;
        shoveDirection = dir;
        keepsakeShove = KeepsakeShove.Approach;
        shoveTime = 0;
        return true;
    }

    // From TickFeatures (the pet free, not in Quiet). True while the shove owns the cat this frame.
    private bool TickKeepsakeShove(double dt)
    {
        shoveVisit -= dt;
        if (keepsakeShove == KeepsakeShove.None && shoveVisit <= 0)
        {
            double every = 180 + featureRandom.NextDouble() * 180;
            shoveVisit = every * (preferences.Personality switch { PetPersonality.Playful => 0.6, PetPersonality.Calm => 1.6, _ => 1 });
            if (!returningHome && climbPlan == null && ledgeExit == 0 && pet.Action is PetAction.Idle or PetAction.Sitting) ShoveKeepsake();
        }
        if (keepsakeShove == KeepsakeShove.None) return false;
        shoveTime += dt;
        if (!KeepsakeBesideCat || keepsakeHeld || keepsakeHop != KeepsakeHop.None) { StopKeepsakeShove(); return false; }
        switch (keepsakeShove)
        {
            case KeepsakeShove.Approach:
                if (ShoveSpot(shoveDirection) is not double target || shoveTime > 12) { StopKeepsakeShove(); return false; }
                if (Math.Abs(target - pet.X) < 6) { keepsakeShove = KeepsakeShove.Crouch; shoveTime = 0; pet.VX = pet.SmoothedWalkVelocity = 0; }
                else WalkToward(target);
                break;
            case KeepsakeShove.Crouch:
                // Wiggles down, eyes on the keepsake, then the push.
                HoldStill(PetAction.Sitting, shoveDirection);
                pet.PlayCrouch = Math.Min(1, shoveTime / 0.3);
                if (shoveTime >= ShoveCrouchSeconds)
                {
                    pet.PlayCrouch = 0;
                    pet.BatRemaining = 0.3;
                    keepsake.Shove(shoveDirection, 200 + featureRandom.NextDouble() * 200);
                    keepsakeEdgeCheck = true;
                    PlayPetSound("swoosh");
                    keepsakeShove = KeepsakeShove.Watch; shoveTime = 0;
                    System.Diagnostics.Trace.WriteLine("The cat shoves its keepsake.");
                }
                break;
            case KeepsakeShove.Watch:
                HoldStill(PetAction.Looking, FacingKeepsake);
                if (shoveTime >= 0.3 && shoveTime - dt < 0.3) pet.Emotions.Cheer();
                if (shoveTime >= ShoveWatchSeconds) { StopKeepsakeShove(); return false; }
                break;
        }
        return true;
    }

    private void StopKeepsakeShove()
    {
        if (keepsakeShove == KeepsakeShove.None) return;
        keepsakeShove = KeepsakeShove.None;
        pet.PlayCrouch = 0;
        if (pet.Action is PetAction.Looking or PetAction.Walking or PetAction.Sitting) pet.Action = PetAction.Idle;
        pet.VX = 0;
        behavior.Reset(pet);
    }

    private void CloseTrophies() { keepsakeWindow?.Close(); keepsakeWindow = null; }
}
