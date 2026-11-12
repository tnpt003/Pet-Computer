using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Effects;

namespace DesktopPet;

// Treasure hunt (docs/MINI-GAMES.md → M5). Finding the map is automatic (like the visitors): every 60–120
// minutes (in RAM, two minutes later when blocked, at most 3 a day) the cat sits down holding a rolled map with
// an X, looks up for about 2 seconds (a little scene, never an emotion), then goes on as usual with a small map
// over its head for 10 minutes (Pause stops that clock; the switch in Customize → Habits takes it away). Only the
// user starts a hunt: a click on that map (its own window, so it is never a click on the cat) or "Treasure hunt"
// in "Play with the cat". The hunt follows the mini-games' rules (the user's request; never wakes the cat; nothing
// automatic, no Python command, no automatic emotion, no visitor while it runs) except that Pause freezes it and a
// click on empty space does not end it. Three faint paw prints hide around the cat in a window under the cat's
// (only the prints and the chest take clicks); each one found, the cat walks under it on its own ground and
// sniffs. Hints at 20 and 40 seconds; after 3 minutes the rest light up by themselves (no losing). Then a chest
// with a puff, the cat beside it; a click (or 30 seconds) and the cat paws it open: a paper flies out with one
// short story, kept for the diary. No items, no new sounds. Python knows nothing of maps or chests.
public sealed partial class PetWindow
{
    private enum TreasureStep { None, Wait, Hunt, Go, Sniff, ToChest, Chest, Paw, Story }
    private TreasureStep treasureStep;
    private readonly TreasureMap treasureMap = new();
    private readonly TreasureHunt treasureHunt = new();
    private Window? treasureWindow, treasureIconWindow, treasureStoryWindow;
    private TreasureArt? treasureArt;
    private TreasureMapIcon? treasureIcon;
    private Area treasureArea;
    private double treasureWait = double.NaN, treasureFind = -1, treasureTime, treasureGround, treasureGroundLeft, treasureGroundRight,
        treasureScale, treasureOrderCheck, treasureGoal, treasureChestX, treasureStand;
    private int treasureStory;
    private bool treasureSuspended, treasureChestClicked, treasurePawed;
    private string? treasureMonitor;
    // Replaceable for UI checks: today's date and time.
    private Func<DateTime>? treasureClock = null;

    public bool TreasureMapHeld => treasureMap.Held;
    public double TreasureMapLeft => treasureMap.Left;
    public bool TreasureFinding => treasureFind >= 0;
    public bool TreasureActive => treasureStep != TreasureStep.None;
    public string TreasurePhase => treasureStep.ToString();
    public TreasureHunt TreasureHuntNow => treasureHunt;
    public double TreasureWait => treasureWait;
    public bool TreasureIconShown => treasureIconWindow?.IsVisible == true;
    public bool TreasureArtShown => treasureWindow != null;
    public bool TreasureStoryShown => treasureStoryWindow != null;
    public int TreasureStoryId => treasureStory;
    public double? TreasureChestX => treasureArt?.ChestX;
    public double TreasureChestFloor => treasureGround;
    public double TreasureLid => treasureArt?.Lid ?? 0;
    public double TreasureGlow(int print) => PrintGlow(print);

    private bool TreasureOwnsMovement => treasureStep is not (TreasureStep.None or TreasureStep.Wait);
    private bool TreasureFindOwnsMovement => treasureFind >= 0;
    private DateTime TreasureNow => treasureClock?.Invoke() ?? DateTime.Now;

    public bool TreasureMenuEnabled => TreasureActive || (!MiniGameBlocked && !MiniGameActive && preferences.Treasure && treasureMap.Held);
    public string TreasureMenuLabel => TreasureActive ? L("Dừng Săn kho báu", "Stop Treasure hunt")
        : treasureMap.Held || !preferences.Treasure ? L("Săn kho báu", "Treasure hunt")
        : L("Săn kho báu · chưa có bản đồ", "Treasure hunt · no map yet");
    public string TreasureMenuNote => TreasureActive ? ""
        : MiniGameBlocked ? MiniGameMenuNote
        : !preferences.Treasure ? L("Săn kho báu đang tắt trong Tùy chỉnh → Thói quen.", "Treasure hunt is off in Customize → Habits.")
        : !treasureMap.Held ? L("Thỉnh thoảng mèo tự tìm thấy một tấm bản đồ; lúc đó bản đồ hiện trên đầu mèo.", "Now and then the pet finds a map; it then shows over the pet's head.")
        : "";

    // The cat finds a map now, if this is a moment it may (also the UiChecks' hook). True when it did.
    public bool TryTreasureFind()
    {
        if (closed || !TreasureRules.MayFind(preferences, TreasureNow) || treasureMap.Held || TreasureActive || treasureFind >= 0
            || !MayAct(AutoKind.TreasureFind) || MiniGameActive || guest.Active || portalWindow != null || pet.CastRemaining > 0
            || !pet.Grounded || pet.Action is not (PetAction.Idle or PetAction.Sitting or PetAction.Looking or PetAction.Walking)) return false;
        TreasureRules.CountFind(preferences, TreasureNow);
        SavePreferences();
        treasureMap.Give();
        treasureFind = 0;
        renderer.CarryMap = true;
        HoldStill(PetAction.Sitting, pet.Facing);
        System.Diagnostics.Trace.WriteLine($"Treasure: the cat found a map ({TreasureRules.MapsToday(preferences, TreasureNow)} today).");
        return true;
    }

    // Every frame from Tick (dt 0 in Pause): the timer, the map's 10 minutes, the icon over the cat.
    private void TickTreasure(double dt)
    {
        if (double.IsNaN(treasureWait)) treasureWait = TreasureRules.NextDelay(featureRandom); // From the app's start, in RAM only.
        if (!preferences.Treasure && (treasureMap.Held || TreasureActive || treasureFind >= 0))
        {
            StopTreasure(); EndTreasureFind(); treasureMap.Take(); // Switched off: the map is gone at once.
        }
        treasureMap.Step(dt); // Its 10 minutes go on during a hunt too; a hunt under way still ends as usual.
        PlaceTreasureIcon(dt);
        if (paused || dt <= 0 || treasureMap.Held || TreasureActive) return; // One map at a time.
        if ((treasureWait -= dt) > 0) return;
        treasureWait = TryTreasureFind() ? TreasureRules.NextDelay(featureRandom) : TreasureRules.RetrySeconds;
    }

    // From TickFeatures with the cat free: the find scene (sitting with the map in its mouth, looking up).
    private bool TickTreasureFind(double dt)
    {
        if (treasureFind < 0) return false;
        treasureFind += dt;
        HoldStill(PetAction.Sitting, pet.Facing);
        pet.ReactTilt = 6 * Math.Sin(Math.PI / 2 * Math.Min(1, treasureFind / 0.4));
        if (treasureFind < TreasureRules.FindSceneSeconds) return true;
        EndTreasureFind();
        return false;
    }

    private void EndTreasureFind()
    {
        if (treasureFind < 0) return;
        treasureFind = -1;
        renderer.CarryMap = false;
        pet.ReactTilt = 0;
        if (!dragging && pet.Action == PetAction.Sitting) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
    }

    // The map over the cat's head: shown while it holds one (not in its find scene, not during a hunt, not
    // while dragged, nor over a fullscreen app or in a battle), fading over its last seconds.
    private void PlaceTreasureIcon(double dt)
    {
        bool show = treasureMap.Held && preferences.Treasure && !TreasureActive && treasureFind < 0 && !dragging && pet.Action != PetAction.Dragging
            && !context.Fullscreen && !BattleActive && !closed;
        if (!show)
        {
            if (treasureIconWindow?.IsVisible == true) treasureIconWindow.Hide();
            if (!treasureMap.Held && treasureIconWindow != null) { treasureIconWindow.Close(); treasureIconWindow = null; treasureIcon = null; }
            return;
        }
        if (treasureIconWindow == null)
        {
            treasureIcon = new TreasureMapIcon { Cursor = System.Windows.Input.Cursors.Hand };
            treasureIcon.ToolTip = L("Săn kho báu cùng mèo", "Go on a treasure hunt with the pet");
            treasureIcon.MouseLeftButtonDown += (_, e) => { e.Handled = true; ClickTreasureMap(); };
            var window = CreateOverlayWindow(treasureIcon, "Pet treasure map");
            window.Width = window.Height = TreasureMapIcon.Size;
            window.Closed += (_, _) => { if (treasureIconWindow == window) { treasureIconWindow = null; treasureIcon = null; } };
            treasureIconWindow = window;
        }
        treasureIcon!.Fade = treasureMap.Fade;
        treasureIcon.Time += dt;
        treasureIcon.InvalidateVisual();
        double top = Math.Max(bounds.Top, pet.Y + 8 - TreasureMapIcon.Size);
        MoveWindow(treasureIconWindow, CatCenter - TreasureMapIcon.Size / 2, top);
        if (!treasureIconWindow.IsVisible) { treasureIconWindow.Show(); KeepTreasureIconAboveCat(); }
        if ((treasureOrderCheck -= dt) <= 0) { treasureOrderCheck = 0.5; KeepTreasureIconAboveCat(); }
    }

    // The icon stays over the cat's window, so a click on it reaches it (and never the cat).
    private void KeepTreasureIconAboveCat()
    {
        var cat = new WindowInteropHelper(this).Handle;
        if (cat == IntPtr.Zero || treasureIconWindow == null) return;
        var handle = new WindowInteropHelper(treasureIconWindow).Handle;
        if (handle == IntPtr.Zero || WindowsAbove(cat).Contains(handle)) return;
        SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
    }

    // A click on the map over the cat's head: the hunt starts (not a click on the cat). True when it did.
    public bool ClickTreasureMap() => !TreasureActive && !paused && ToggleTreasure();

    // The menu row: a hunt now (or once the cat is free); the same row again ends it. False when it may not.
    public bool ToggleTreasure()
    {
        if (TreasureActive) { StopTreasure(); return true; }
        if (MiniGameBlocked || MiniGameActive || !preferences.Treasure || !treasureMap.Held) return false;
        revision++; // Like DoNow: AI replies chosen before this request are dropped.
        StopCall(); StopLesson();
        treasureStep = TreasureStep.Wait; treasureTime = 0;
        if (!TrickCatBusy && CanPlay) BeginTreasure();
        return true;
    }

    private void BeginTreasure()
    {
        CancelFeatureActivity(); // Ends the ball, laser or toy game first, and a find scene.
        double left = bounds.Left, right = bounds.Right, ground = pet.Y + settings.Height;
        var taken = new List<Area>();
        if (pet.LedgeId != 0 && PetLedges.Support(ledges, pet.LedgeId, CatCenter) is Ledge ledge) { left = ledge.Left; right = ledge.Right; }
        else
        {
            string here = CurrentMonitor().DeviceName;
            foreach (var (l, r) in ToyObstacles(here)) taken.Add(new Area(l, bounds.Bottom - 200, r, bounds.Bottom));
            foreach (var thing in DesktopThings)
                if (thing.Window?.IsVisible == true && thing.Monitor == here)
                    taken.Add(new Area(thing.Motion.X - thing.HalfWidth, bounds.Bottom - 170, thing.Motion.X + thing.HalfWidth, bounds.Bottom));
        }
        if (TreasureRules.Place(featureRandom, CatCenter, ground, bounds, taken) is not { } spots)
        {
            treasureStep = TreasureStep.None;
            ShowTrickLabel(L("Không đủ chỗ", "No room"));
            System.Diagnostics.Trace.WriteLine("Treasure: no room for the prints.");
            return;
        }
        treasureHunt.Reset(spots);
        treasureGround = ground; treasureGroundLeft = left; treasureGroundRight = right;
        treasureArea = new Area(Math.Max(bounds.Left, CatCenter - TreasureRules.Spread - 40), Math.Max(bounds.Top, ground - TreasureRules.Rise - 40),
            Math.Min(bounds.Right, CatCenter + TreasureRules.Spread + 40), ground + 2);
        treasureMonitor = CurrentMonitor().DeviceName; treasureScale = scale;
        treasureTime = 0; treasureChestClicked = treasurePawed = false; treasureStory = 0;
        ResetMiniGameKeys(); // The click that chose the row (or the map) does not count.
        treasureStep = TreasureStep.Hunt;
        pet.Emotions.SetSuspended(true); treasureSuspended = true; // No automatic emotion while playing.
        Interact("toy_started"); // Counts as play for Python, once per hunt.
        ShowTreasureArt();
        PlaceTreasureIcon(0); // The map over the head goes as the hunt starts.
        ShowMiniLabel(L("Tìm 3 dấu chân mèo · 0/3", "Find the 3 paw prints · 0/3"), CatCenter, pet.Y + 4, TreasureRules.LabelSeconds + 1);
        System.Diagnostics.Trace.WriteLine("Treasure: the hunt is on.");
    }

    // Every unpaused frame from TickFeatures, before CanPlay: the wait, what ends a hunt, Esc.
    private void TickTreasureClock(double dt)
    {
        if (treasureStep == TreasureStep.None) return;
        if (closed || context.Fullscreen || Blocking(AutoBlock.AnyBattle) != AutoBlock.None) { StopTreasure(); return; }
        if (treasureStep == TreasureStep.Wait)
        {
            if ((treasureTime += dt) > CupGameRules.SleepWaitSeconds || !treasureMap.Held) { treasureStep = TreasureStep.None; return; }
            if (!TrickCatBusy && CanPlay) BeginTreasure();
            return;
        }
        if (dragging) return; // The drag ends it in TickFeatures (CancelFeatureActivity).
        // Another monitor, another DPI, or the ground it plays on moved: over (a chest not opened is not counted).
        if (CurrentMonitor().DeviceName != treasureMonitor || Math.Abs(scale - treasureScale) > 0.001 ||
            (pet.Grounded && Math.Abs(pet.Y + settings.Height - treasureGround) > 3)) { StopTreasure(); return; }
        var (esc, _) = MiniGameKeyPresses(); // A click on empty space does not end it (a wide area, easy to miss).
        if (esc) { StopTreasure(); return; }
        if ((treasureOrderCheck -= dt) <= 0) { treasureOrderCheck = 0.5; KeepTreasureBelowCat(); }
    }

    // From TickFeatures with the cat free (CanPlay), next to the other games. True while it owns the cat.
    private bool TickTreasureHunt(double dt)
    {
        if (!TreasureOwnsMovement) return false;
        treasureTime += dt;
        if (treasureStep is TreasureStep.Hunt or TreasureStep.Go or TreasureStep.Sniff && treasureHunt.Step(dt))
        {
            // Three minutes: the prints left light up; the cat leads the way to the chest.
            ShowMiniLabel(L("Dấu chân tự sáng lên rồi!", "The paw prints light up!"), CatCenter, pet.Y + 4, TreasureRules.LabelSeconds);
            pet.ReactDip = 0;
            BeginChest();
        }
        if (treasureArt != null)
        {
            if (treasureArt.Puff >= 0 && (treasureArt.Puff += dt / TreasureRules.PuffSeconds) >= 1) treasureArt.Puff = -1;
            treasureArt.InvalidateVisual();
        }
        switch (treasureStep)
        {
            case TreasureStep.Hunt:
            {
                int near = treasureHunt.Nearest(CatCenter), hint = treasureHunt.Hint;
                if (near < 0 || hint == 0) { HoldStill(PetAction.Sitting, pet.Facing); pet.ReactTilt = 0; return true; }
                var spot = treasureHunt.Spots[near];
                double under = TreasureRules.Under(spot.X, treasureGroundLeft, treasureGroundRight);
                // The second hint: it walks near that print (at most a few seconds' walk).
                if (hint == 2 && Math.Abs(under - CatCenter) > 30 && treasureHunt.SinceFind < TreasureRules.HintWalk + TreasureRules.WalkSeconds)
                { pet.ReactTilt = 0; WalkToward(under - Math.Sign(under - CatCenter) * 24 - settings.Width / 2); return true; }
                LookToward(spot);
                return true;
            }
            case TreasureStep.Go: // Under the print just found, on its own ground.
                if (Math.Abs(CatCenter - treasureGoal) > 6 && treasureTime < TreasureRules.WalkSeconds) { pet.ReactTilt = 0; WalkToward(treasureGoal - settings.Width / 2); return true; }
                treasureStep = TreasureStep.Sniff; treasureTime = 0;
                return true;
            case TreasureStep.Sniff: // Nose down, about a second.
                HoldStill(PetAction.Looking, pet.Facing);
                pet.ReactTilt = 0;
                pet.ReactDip = 8 * Math.Sin(Math.PI * Math.Min(1, treasureTime / TreasureRules.SniffSeconds));
                if (treasureTime < TreasureRules.SniffSeconds) return true;
                pet.ReactDip = 0;
                if (treasureHunt.AllFound) BeginChest();
                else { treasureStep = TreasureStep.Hunt; treasureTime = 0; }
                return true;
            case TreasureStep.ToChest: // To stand beside the chest (never on it).
                if (Math.Abs(CatCenter - treasureStand) > 6 && treasureTime < TreasureRules.WalkSeconds) { pet.ReactTilt = pet.ReactDip = 0; WalkToward(treasureStand - settings.Width / 2); return true; }
                HoldStill(PetAction.Sitting, treasureChestX < CatCenter ? -1 : 1);
                treasureStep = TreasureStep.Chest; treasureTime = 0;
                if (treasureArt != null) treasureArt.ChestClickable = true;
                if (treasureChestClicked) PawChest();
                return true;
            case TreasureStep.Chest: // Waiting for the user's click; after 30 seconds the cat opens it itself.
                HoldStill(PetAction.Sitting, treasureChestX < CatCenter ? -1 : 1);
                if (treasureTime >= TreasureRules.ChestAutoSeconds) PawChest();
                return true;
            case TreasureStep.Paw: // A pat on the lid, the lid springs open with sparkles, a paper flies out.
            {
                HoldStill(PetAction.Looking, treasureChestX < CatCenter ? -1 : 1);
                if (!treasurePawed && treasureTime >= 0.15) { treasurePawed = true; pet.BatRemaining = 0.3; PlayPetSound("ball_tap"); }
                if (treasureArt != null)
                {
                    double opened = (treasureTime - 0.25) / TreasureRules.LidSeconds;
                    treasureArt.Lid = Math.Clamp(opened, 0, 1);
                    treasureArt.Spark = opened is >= 0 and < 2 ? opened / 2 : -1;
                    double paper = (treasureTime - 0.25 - TreasureRules.LidSeconds) / TreasureRules.PaperSeconds;
                    treasureArt.PaperRise = paper is >= 0 and < 1 ? paper : -1;
                }
                if (treasureTime >= 0.25 && treasureTime - dt < 0.25) PlayPetSound("sparkle"); // The lid springs open.
                if (treasureTime < 0.25 + TreasureRules.LidSeconds + TreasureRules.PaperSeconds) return true;
                if (treasureArt != null) treasureArt.PaperRise = -1;
                ShowTreasureStory();
                treasureStep = TreasureStep.Story; treasureTime = 0;
                return true;
            }
            case TreasureStep.Story:
                HoldStill(PetAction.Sitting, treasureChestX < CatCenter ? -1 : 1);
                if (treasureTime < TreasureRules.StorySeconds) return true;
                StopTreasure();
                return false;
        }
        return false;
    }

    // The first hint: the cat turns its head toward the print (facing it, looking up or down at it).
    private void LookToward(TreasureSpot spot)
    {
        HoldStill(PetAction.Looking, spot.X < CatCenter ? -1 : 1);
        pet.ReactTilt = Math.Clamp((treasureGround - spot.Y - 80) / 14, -8, 8);
    }

    // How bright print i is: faint, brighter near the cursor, a little brighter as the second hint, full once found.
    private double PrintGlow(int print)
    {
        if (print < 0 || print >= treasureHunt.Spots.Count) return 0;
        var spot = treasureHunt.Spots[print];
        var cursor = CursorPosition();
        bool hinted = treasureHunt.Hint == 2 && treasureHunt.Nearest(CatCenter) == print;
        return TreasureRules.Glow(treasureHunt.Found(print), hinted, TreasureRules.Distance(cursor.X, cursor.Y, spot.X, spot.Y));
    }

    // All three found (or lit): the chest comes up with a puff on the cat's ground near the last print.
    private void BeginChest()
    {
        int last = treasureHunt.Last >= 0 ? treasureHunt.Last : 0;
        double lastX = treasureHunt.Spots.Count > 0 ? treasureHunt.Spots[last].X : CatCenter;
        var taken = new List<(double Left, double Right)>();
        if (pet.LedgeId == 0)
        {
            string here = CurrentMonitor().DeviceName;
            taken.AddRange(ToyObstacles(here));
            foreach (var thing in DesktopThings)
                if (thing.Window?.IsVisible == true && thing.Monitor == here) taken.Add((thing.Motion.X - thing.HalfWidth, thing.Motion.X + thing.HalfWidth));
        }
        double groundLeft = Math.Max(treasureGroundLeft, treasureArea.Left), groundRight = Math.Min(treasureGroundRight, treasureArea.Right);
        if (TreasureRules.ChestSpot(lastX, CatCenter, groundLeft, groundRight, taken) is not double chest
            || TreasureRules.StandSpot(chest, CatCenter, groundLeft, groundRight) is not double stand)
        {
            // No room for a chest beside the cat on this ground (a short window edge): no chest is drawn, the
            // cat pats the air where it stands and the story still comes out.
            treasureChestX = treasureStand = CatCenter;
            treasureStep = TreasureStep.Chest; treasureTime = 0;
            PawChest();
            System.Diagnostics.Trace.WriteLine("Treasure: no room for the chest; the story comes at once.");
            return;
        }
        treasureChestX = chest; treasureStand = stand;
        if (treasureArt != null) { treasureArt.ChestX = chest; treasureArt.ChestFloor = treasureGround; treasureArt.Puff = 0; treasureArt.Lid = 0; }
        PlayPetSound("sparkle");
        treasureStep = TreasureStep.ToChest; treasureTime = 0;
    }

    private void PawChest()
    {
        if (treasureStep != TreasureStep.Chest) return;
        if (treasureArt != null) treasureArt.ChestClickable = false;
        treasureStory = TreasureRules.PickStory(preferences, featureRandom);
        TreasureRules.OpenChest(preferences, treasureStory);
        treasureMap.Take(); // The map is used up.
        SavePreferences();
        treasureStep = TreasureStep.Paw; treasureTime = 0; treasurePawed = false;
        System.Diagnostics.Trace.WriteLine($"Treasure: the chest is open (story {treasureStory}).");
    }

    // A click in the hunt's window (screen DIPs): a print under it is found, or the chest is opened. True when it counted.
    public bool ClickTreasureAt(double x, double y)
    {
        if (paused) return false;
        if (treasureStep is TreasureStep.Hunt or TreasureStep.Go or TreasureStep.Sniff)
        {
            int print = treasureHunt.At(x, y);
            if (print < 0 || !treasureHunt.Find(print)) return false;
            int found = treasureHunt.FoundCount;
            ShowMiniLabel($"{found}/{TreasureRules.Prints}", CatCenter, pet.Y + 4, TreasureRules.LabelSeconds);
            PlayPetSound("sparkle");
            treasureGoal = TreasureRules.Under(treasureHunt.Spots[print].X, treasureGroundLeft, treasureGroundRight);
            pet.ReactDip = 0;
            treasureStep = TreasureStep.Go; treasureTime = 0;
            treasureArt?.InvalidateVisual();
            return true;
        }
        if (treasureArt?.ChestX is not double chest || Math.Abs(x - chest) > TreasureRules.ChestHalf + 6
            || y < treasureGround - TreasureRules.ChestHeight - 6 || y > treasureGround + 2) return false;
        if (treasureStep == TreasureStep.ToChest) { treasureChestClicked = true; return true; } // Opened as soon as the cat is there.
        if (treasureStep != TreasureStep.Chest) return false;
        PawChest();
        return true;
    }

    // A click on the story's paper closes it sooner.
    public bool ClickTreasureStory()
    {
        if (treasureStep != TreasureStep.Story || paused) return false;
        StopTreasure();
        return true;
    }

    private void ShowTreasureArt()
    {
        treasureWindow?.Close();
        treasureArt = new TreasureArt(treasureHunt) { Cursor = System.Windows.Input.Cursors.Hand, PrintGlow = PrintGlow, Origin = new Point(treasureArea.Left, treasureArea.Top) };
        double width = treasureArea.Right - treasureArea.Left, height = treasureArea.Bottom - treasureArea.Top;
        treasureArt.Size(width, height);
        treasureArt.MouseLeftButtonDown += (_, e) => { e.Handled = true; var p = e.GetPosition(treasureArt); ClickTreasureAt(p.X + treasureArea.Left, p.Y + treasureArea.Top); };
        var window = CreateOverlayWindow(treasureArt, "Pet treasure hunt");
        window.Width = width; window.Height = height;
        window.Left = treasureArea.Left; window.Top = treasureArea.Top;
        window.Closed += (_, _) => { if (treasureWindow == window) { treasureWindow = null; treasureArt = null; } };
        treasureWindow = window;
        window.Show();
        KeepTreasureBelowCat(force: true);
    }

    // The hunt's window stays under the cat's, so the cat (and a click on it) is always in front.
    private void KeepTreasureBelowCat(bool force = false)
    {
        var cat = new WindowInteropHelper(this).Handle;
        if (cat == IntPtr.Zero || treasureWindow == null) return;
        var handle = new WindowInteropHelper(treasureWindow).Handle;
        if (handle == IntPtr.Zero || (!force && !WindowsAbove(cat).Contains(handle))) return;
        SetWindowPos(handle, cat, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
    }

    // The paper's story in a PetUi card over the chest, about 8 seconds (a click closes it).
    private void ShowTreasureStory()
    {
        treasureStoryWindow?.Close();
        var text = new TextBlock
        {
            Text = TreasureStoryText(treasureStory), TextWrapping = TextWrapping.Wrap, MaxWidth = 260, FontSize = PetUi.BodySize,
            FontFamily = PetUi.TextFont, Foreground = PetUi.Ink, VerticalAlignment = VerticalAlignment.Center
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new PetIllustration(PetMotif.Scroll, 26) { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top });
        row.Children.Add(text);
        var card = new Border
        {
            Background = PetUi.CardBack, BorderBrush = PetUi.ArtInk, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(PetUi.CardRadius),
            Padding = new Thickness(12, 8, 12, 9), Child = row, Margin = new Thickness(6), Cursor = System.Windows.Input.Cursors.Hand,
            Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Direction = 270, Opacity = 0.28 }
        };
        card.MouseLeftButtonDown += (_, e) => { e.Handled = true; ClickTreasureStory(); };
        var window = CreateOverlayWindow(card, "Pet treasure story");
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.Closed += (_, _) => { if (treasureStoryWindow == window) treasureStoryWindow = null; };
        treasureStoryWindow = window;
        window.Show();
        window.UpdateLayout();
        double width = window.ActualWidth, height = window.ActualHeight;
        double left = Math.Clamp(treasureChestX - width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - width));
        double top = Math.Clamp(treasureGround - TreasureRules.ChestHeight - 70 - height, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - height));
        MoveWindow(window, left, top);
    }

    // A hunt ends: Esc, the menu row again, a click or drag on the cat, the story read, a fullscreen app, a battle,
    // another monitor or DPI. The map stays for the rest of its 10 minutes unless the chest was opened.
    private void StopTreasure()
    {
        if (treasureStep == TreasureStep.None) return;
        var was = treasureStep;
        treasureStep = TreasureStep.None;
        treasureTime = 0;
        treasureWindow?.Close(); treasureWindow = null; treasureArt = null;
        treasureStoryWindow?.Close(); treasureStoryWindow = null;
        if (treasureSuspended) { treasureSuspended = false; if (battle == null) pet.Emotions.SetSuspended(false); }
        if (was == TreasureStep.Wait) return;
        HideMiniLabel();
        pet.ReactDip = pet.ReactTilt = 0;
        if (!dragging && pet.Action is PetAction.Sitting or PetAction.Looking or PetAction.Walking) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
        System.Diagnostics.Trace.WriteLine($"Treasure: the hunt is over ({treasureHunt.FoundCount}/{TreasureRules.Prints}{(treasureStory > 0 ? $", story {treasureStory}" : "")}).");
    }

    // The twelve stories in the chests: little adventures of the cat on the desk.
    public string TreasureStoryText(int id) => id switch
    {
        1 => L("Hôm ấy mèo leo lên chồng sách cao nhất bàn và ngắm cả căn phòng như một vị vua nhỏ.",
               "That day the cat climbed the tallest stack of books on the desk and looked over the whole room like a little king."),
        2 => L("Mèo từng đuổi một vệt nắng từ góc bàn tới tận bàn phím, rồi ngủ quên ngay trên phím cách.",
               "The cat once chased a patch of sunlight from the corner of the desk all the way to the keyboard, then fell asleep right on the space bar."),
        3 => L("Dưới chân màn hình có một chiếc kẹp giấy lấp lánh; mèo giấu nó kỹ đến mức chính mèo cũng quên mất chỗ.",
               "Under the monitor lay a shiny paper clip; the cat hid it so well that even the cat forgot where."),
        4 => L("Một đêm mưa, mèo ngồi bên cửa sổ đếm giọt nước trượt xuống kính, đếm tới bảy thì ngáp.",
               "One rainy night the cat sat by the window counting raindrops sliding down the glass, and yawned at seven."),
        5 => L("Mèo phát hiện cốc trà của sen ấm hơn mọi tấm đệm, nên đã nằm cạnh nó suốt buổi chiều.",
               "The cat found that your cup of tea was warmer than any cushion, so it lay beside it all afternoon."),
        6 => L("Có lần cuộn len lăn mất sau tủ, mèo dũng cảm chui vào bóng tối và mang về... một cục bụi to.",
               "Once the yarn ball rolled behind the cupboard; the cat bravely crept into the dark and came back with... a big dust bunny."),
        7 => L("Mèo tin rằng con trỏ chuột là một chú chim nhỏ bay rất nhanh, và một ngày nào đó sẽ bắt được nó.",
               "The cat believes the mouse pointer is a very fast little bird, and that one day it will catch it."),
        8 => L("Sáng nọ mèo dậy sớm hơn mặt trời, ngồi chờ ở mép bàn để là người đầu tiên chào sen.",
               "One morning the cat woke before the sun and waited at the edge of the desk to be the first to say hello."),
        9 => L("Mèo từng vẽ một bức tranh bằng dấu chân dính mực trên tờ giấy nháp, và thấy nó đẹp nhất thế giới.",
               "The cat once painted a picture with inky paw prints on a scrap of paper, and found it the most beautiful in the world."),
        10 => L("Trong ngăn kéo có một chiếc hộp nhỏ thơm mùi gỗ; mèo gọi đó là căn nhà bí mật thứ hai.",
                "In the drawer there is a little box that smells of wood; the cat calls it its second secret house."),
        11 => L("Mèo và một chú slime hiền từng ngồi cạnh nhau ngắm hoàng hôn trên thanh taskbar, chẳng ai nói gì mà vẫn vui.",
                "The cat and a gentle slime once sat side by side watching the sunset over the taskbar; neither said a word, and both were happy."),
        12 => L("Mèo cất tấm bản đồ này để dành cho sen, vì chuyến phiêu lưu nào cũng vui hơn khi có bạn đi cùng.",
                "The cat kept this map for you, because every adventure is more fun with a friend along."),
        _ => ""
    };
}
