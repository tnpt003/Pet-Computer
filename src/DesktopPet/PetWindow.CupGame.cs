using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DesktopPet;

// Mini-games (docs/MINI-GAMES.md → M1): "Find the fish" under three cups. The user starts it from "Play with
// the cat" (right-click menu and tray); it is the user's request, like "Do now": it runs in Quiet, Focus and the
// wake cooldown, never wakes a sleeping cat (it waits up to 30 seconds for it to wake by itself), and Pause,
// a battle, its screens and a fullscreen app keep it off. While it runs, nothing the cat does on its own, no
// Python command and no automatic emotion comes in (FeatureOwnsMovement, emotions suspended); winning or losing
// is a little scene, never an emotion. The cups are in their own window: only the drawn cups take clicks.
// Esc, a click elsewhere, the menu row again, a click or drag on the cat, or two minutes end it; a fullscreen
// app, a battle, another monitor or DPI end it without counting the game. All C#: Python only hears the
// existing "toy_started" once per game; the best run is kept in the preferences.
public sealed partial class PetWindow
{
    private enum CupStep { None, Wait, Room, Show, Cover, Shuffle, Choose, Go, Sniff, Lift, Win, Puzzled, Reveal, Eat }
    private CupStep cupStep;
    private readonly CupShuffle cupShuffle = new();
    private Window? cupWindow;
    private CupGameArt? cupArt;
    private double cupTime, cupGameTime, cupRowLeft, cupGround, cupSpot, cupScale;
    private int cupRounds, cupSide = 1, cupChosen = -1;
    private bool cupBestRaised, cupSuspended;
    private string? cupMonitor;
    // Replaceable for UI checks: whether a key (virtual-key code) is down now.
    private Func<int, bool>? keyProbe = null;

    public bool CupGameActive => cupStep != CupStep.None;
    public string CupGamePhase => cupStep.ToString();
    public int CupGameRounds => cupRounds;
    public int CupGameFishCup => cupShuffle.FishCup;
    public bool CupGameShuffling => cupShuffle.Shuffling;
    public int CupGameSwaps => cupShuffle.SwapCount;
    public int CupGameSlotOf(int cup) => cupShuffle.SlotOf(cup);

    private bool CupGameBlocked => MiniGameBlocked;
    // One mini-game at a time: the row is greyed out while another runs.
    public bool CupGameMenuEnabled => CupGameActive || (!MiniGameBlocked && !HideSeekActive);
    public string CupGameMenuLabel => preferences.CupGameBest > 0
        ? LF($"Úp cốc tìm cá · kỷ lục {preferences.CupGameBest}", $"Find the fish · best {preferences.CupGameBest}")
        : L("Úp cốc tìm cá", "Find the fish");
    public string CupGameMenuNote => CupGameActive ? "" : MiniGameMenuNote;

    // The menu row: a game now (or once the cat is free); the same row again ends it. False when it may not.
    public bool ToggleCupGame()
    {
        if (CupGameActive) { StopCupGame(count: true); return true; }
        if (CupGameBlocked || HideSeekActive) return false;
        revision++; // Like DoNow: AI replies chosen before this request are dropped.
        StopCall(); StopLesson();
        cupStep = CupStep.Wait; cupTime = 0;
        if (!TrickCatBusy && CanPlay) BeginCupGame();
        return true;
    }

    private void BeginCupGame()
    {
        CancelFeatureActivity(); // Ends the ball, laser or toy game first.
        // The ground the cat is on: the floor, or the top of the window it stands on.
        double left = bounds.Left, right = bounds.Right;
        var taken = new List<(double Left, double Right)>();
        if (pet.LedgeId != 0 && PetLedges.Support(ledges, pet.LedgeId, CatCenter) is Ledge ledge) { left = ledge.Left; right = ledge.Right; }
        else
        {
            string here = CurrentMonitor().DeviceName;
            taken.AddRange(ToyObstacles(here));
            foreach (var thing in DesktopThings)
                if (thing.Window?.IsVisible == true && thing.Monitor == here) taken.Add((thing.Motion.X - thing.HalfWidth, thing.Motion.X + thing.HalfWidth));
        }
        double reach = settings.WalkSpeed * CupGameRules.RoomWalkSeconds;
        if (CupGameRules.Place(CatCenter, settings.Width / 2, left, right, taken, reach) is not { } spot)
        {
            cupStep = CupStep.None;
            ShowTrickLabel(L("Không đủ chỗ bày cốc", "No room for the cups"));
            System.Diagnostics.Trace.WriteLine("Cup game: no room next to the cat.");
            return;
        }
        cupSpot = spot.CatCentre; cupSide = spot.Side;
        cupRounds = 0; cupBestRaised = false; cupChosen = -1;
        cupGameTime = 0; cupTime = 0;
        cupMonitor = CurrentMonitor().DeviceName; cupScale = scale;
        cupGround = pet.Y + settings.Height;
        ResetMiniGameKeys(); // The click that chose the menu row does not end it.
        cupShuffle.Reset();
        cupStep = CupStep.Room;
        pet.Emotions.SetSuspended(true); cupSuspended = true; // No automatic emotion while playing.
        Interact("toy_started"); // Counts as play for Python, once per game.
        System.Diagnostics.Trace.WriteLine("Cup game: started.");
    }

    // Every unpaused frame from TickFeatures, before CanPlay: the wait, what ends a game, Esc and clicks elsewhere.
    private void TickCupClock(double dt)
    {
        if (cupStep == CupStep.None) return;
        if (CupGameBlocked) { StopCupGame(count: false); return; }
        if (cupStep == CupStep.Wait)
        {
            if ((cupTime += dt) > CupGameRules.SleepWaitSeconds) { cupStep = CupStep.None; return; }
            if (!TrickCatBusy && CanPlay) BeginCupGame();
            return;
        }
        // Another monitor, another DPI, or the ground it plays on moved: over, not counted.
        if (CurrentMonitor().DeviceName != cupMonitor || Math.Abs(scale - cupScale) > 0.001 ||
            (pet.Grounded && !dragging && Math.Abs(pet.Y + settings.Height - cupGround) > 3)) { StopCupGame(count: false); return; }
        if ((cupGameTime += dt) > CupGameRules.GameSeconds) { StopCupGame(count: true); return; }
        var (esc, click) = MiniGameKeyPresses();
        if (esc || (click && !OverCups(CursorPosition()))) { StopCupGame(count: true); return; } // Esc, or a click elsewhere.
        PlaceCups();
    }

    private bool OverCups(Point p)
    {
        if (cupWindow == null) return false;
        double left = cupRowLeft - CupGameArt.Pad, top = cupGround - CupGameArt.ArtHeight;
        return p.X >= left && p.X <= left + CupGameArt.ArtWidth && p.Y >= top && p.Y <= cupGround;
    }

    private bool CupOwnsMovement => cupStep is not (CupStep.None or CupStep.Wait);

    // From TickFeatures with the cat free (CanPlay), next to the call and the lesson. True while it owns the cat.
    private bool TickCupGame(double dt)
    {
        if (!CupOwnsMovement) return false;
        cupTime += dt;
        int towardRow = cupSide;
        switch (cupStep)
        {
            case CupStep.Room: // To where there is room for the cups, then they come out.
                if (Math.Abs(CatCenter - cupSpot) > 6 && cupTime < CupGameRules.RoomWalkSeconds) { WalkToward(cupSpot - settings.Width / 2); return true; }
                if (Math.Abs(CatCenter - cupSpot) > 20) { ShowTrickLabel(L("Không đủ chỗ bày cốc", "No room for the cups")); StopCupGame(count: false); return false; }
                cupRowLeft = CupGameRules.RowLeft(CatCenter, cupSide);
                ShowCups();
                NextCupRound();
                return true;
            case CupStep.Show: // The fish in front of the middle cup; the cat looks at it.
                HoldStill(PetAction.Looking, towardRow);
                if (cupTime < CupGameRules.ShowSeconds) return true;
                cupStep = CupStep.Cover; cupTime = 0;
                return true;
            case CupStep.Cover: // The middle cup comes down over the fish.
                HoldStill(PetAction.Looking, towardRow);
                cupArt!.Lift[cupShuffle.CupIn(1)] = Math.Max(0, 1 - cupTime / CupGameRules.CoverSeconds);
                if (cupTime < CupGameRules.CoverSeconds) return true;
                cupArt.FishX = null;
                cupShuffle.Start(cupRounds + 1, featureRandom);
                cupStep = CupStep.Shuffle; cupTime = 0;
                PlayPetSound("ball_tap");
                return true;
            case CupStep.Shuffle: // The cat's head follows the cups.
                HoldStill(PetAction.Looking, towardRow);
                cupShuffle.Step(dt);
                pet.ReactTilt = 6 * Math.Sin(cupTime * 7);
                if (cupShuffle.Shuffling) return true;
                pet.ReactTilt = 0;
                cupStep = CupStep.Choose; cupTime = 0;
                ShowTrickLabel(L("Cá ở cốc nào?", "Which cup is the fish under?"));
                return true;
            case CupStep.Choose: // Sitting, waiting for the user's pick (the game's own time limit only).
                HoldStill(PetAction.Sitting, towardRow);
                trickLabelLeft = Math.Max(trickLabelLeft, 0.3);
                return true;
            case CupStep.Go: // Behind the row to the cup picked, its head over it.
                double target = cupRowLeft + CupGameRules.SlotCentre(cupShuffle.SlotOf(cupChosen));
                if (Math.Abs(CatCenter - target) > 4 && cupTime < CupGameRules.CupWalkSeconds) { WalkToward(target - settings.Width / 2); return true; }
                HoldStill(PetAction.Looking, pet.Facing);
                cupStep = CupStep.Sniff; cupTime = 0;
                return true;
            case CupStep.Sniff: // Nose down to the cup.
                HoldStill(PetAction.Looking, pet.Facing);
                pet.ReactDip = 8 * Math.Sin(Math.PI * Math.Min(1, cupTime / CupGameRules.SniffSeconds));
                if (cupTime < CupGameRules.SniffSeconds) return true;
                pet.ReactDip = 0;
                pet.BatRemaining = 0.3; // The paw tips the cup up.
                PlayPetSound("ball_tap");
                cupStep = CupStep.Lift; cupTime = 0;
                return true;
            case CupStep.Lift:
                HoldStill(PetAction.Looking, pet.Facing);
                cupArt!.Side = pet.Facing;
                cupArt.Lift[cupChosen] = Math.Min(1, cupTime / CupGameRules.LiftSeconds);
                if (cupChosen == cupShuffle.FishCup) cupArt.FishX = CupGameRules.SlotCentre(cupShuffle.SlotOf(cupChosen));
                if (cupTime < CupGameRules.LiftSeconds) return true;
                cupTime = 0;
                if (cupChosen == cupShuffle.FishCup)
                {
                    cupRounds++;
                    if (CupGameRules.RaiseBest(preferences, cupRounds)) { cupBestRaised = true; SavePreferences(); }
                    cupArt.FishX = null; renderer.CarryFish = true; // In the cat's mouth.
                    PlayPetSound("excited");
                    if (cupRounds >= CupGameRules.MaxRounds) { cupStep = CupStep.Eat; ShowTrickLabel($"{cupRounds}/{CupGameRules.MaxRounds}!"); return true; }
                    cupStep = CupStep.Win;
                    ShowTrickLabel("+1");
                }
                else cupStep = CupStep.Puzzled;
                return true;
            case CupStep.Win: // The fish in its mouth, a happy little bob; then the next round.
                HoldStill(PetAction.Sitting, pet.Facing);
                pet.ReactDip = -3 * Math.Abs(Math.Sin(cupTime * 9));
                if (cupTime < CupGameRules.WinSeconds) return true;
                pet.ReactDip = 0;
                NextCupRound();
                return true;
            case CupStep.Puzzled: // An empty cup: a tilt of the head.
                HoldStill(PetAction.Sitting, pet.Facing);
                pet.ReactTilt = 10 * Math.Sin(Math.PI * Math.Min(1, cupTime / CupGameRules.PuzzledSeconds));
                if (cupTime < CupGameRules.PuzzledSeconds) return true;
                pet.ReactTilt = 0;
                int fishCup = cupShuffle.FishCup;
                cupArt!.FishX = CupGameRules.SlotCentre(cupShuffle.SlotOf(fishCup));
                cupStep = CupStep.Reveal; cupTime = 0;
                return true;
            case CupStep.Reveal: // The cup with the fish is turned up to show it; then the game is over.
                HoldStill(PetAction.Looking, pet.Facing);
                cupArt!.Lift[cupShuffle.FishCup] = Math.Min(1, cupTime / CupGameRules.LiftSeconds);
                if (cupTime < CupGameRules.RevealSeconds) return true;
                StopCupGame(count: true);
                return false;
            case CupStep.Eat: // Ten in a row: the cat eats the fish, and that is the game.
                HoldStill(PetAction.Sitting, pet.Facing);
                pet.ReactDip = 3 * Math.Abs(Math.Sin(cupTime * 6));
                if (cupTime < CupGameRules.EatSeconds) return true;
                StopCupGame(count: true);
                return false;
        }
        return false;
    }

    // A new round: the fish goes in front of the middle cup, lifted for the cat to see.
    private void NextCupRound()
    {
        renderer.CarryFish = false;
        var art = cupArt!;
        for (int i = 0; i < CupGameRules.Cups; i++) { art.Lift[i] = 0; art.Rise[i] = 0; art.Front[i] = false; }
        int middle = cupShuffle.CupIn(1);
        art.Lift[middle] = 1; art.Side = cupSide > 0 ? -1 : 1;
        art.FishX = CupGameRules.SlotCentre(1);
        art.Hover = -1;
        cupChosen = -1;
        cupStep = CupStep.Show; cupTime = 0;
    }

    private void ShowCups()
    {
        cupArt = new CupGameArt();
        cupArt.MouseLeftButtonDown += (_, e) => { e.Handled = true; PickCup(e.GetPosition(cupArt).X - CupGameArt.Pad); };
        cupArt.MouseMove += (_, e) => { if (cupArt != null) { int slot = cupStep == CupStep.Choose ? CupGameRules.SlotAt(e.GetPosition(cupArt).X - CupGameArt.Pad) : -1; SetCupHover(slot); } };
        cupArt.MouseLeave += (_, _) => SetCupHover(-1);
        cupWindow = CreateOverlayWindow(cupArt, "Pet cup game");
        cupWindow.Width = CupGameArt.ArtWidth; cupWindow.Height = CupGameArt.ArtHeight;
        cupWindow.Closed += (_, _) => { cupWindow = null; cupArt = null; };
        cupWindow.Left = cupRowLeft - CupGameArt.Pad; cupWindow.Top = cupGround - CupGameArt.ArtHeight;
        cupWindow.Show();
        RaiseCups();
        PlaceCups();
    }

    private void SetCupHover(int slot)
    {
        if (cupArt == null) return;
        int cup = slot >= 0 ? cupShuffle.CupIn(slot) : -1;
        if (cupArt.Hover == cup) return;
        cupArt.Hover = cup;
        cupArt.Cursor = cup >= 0 ? Cursors.Hand : null;
        cupArt.InvalidateVisual();
    }

    // The user's pick (a slot across the row): the cat goes to check that cup.
    public bool PickCup(double x)
    {
        int slot = CupGameRules.SlotAt(x);
        if (cupStep != CupStep.Choose || slot < 0 || paused) return false;
        cupChosen = cupShuffle.CupIn(slot);
        SetCupHover(-1);
        trickLabelLeft = 0;
        cupStep = CupStep.Go; cupTime = 0;
        RaiseCups();
        return true;
    }

    // Each frame from the render step: the cups where the shuffle has them, on the cat's ground.
    private void PlaceCups()
    {
        if (cupWindow == null || cupArt == null) return;
        if (cupStep == CupStep.Shuffle || cupStep == CupStep.Choose || cupStep == CupStep.Go)
            for (int i = 0; i < CupGameRules.Cups; i++)
            {
                var (x, rise, front) = cupShuffle.Pose(i);
                cupArt.X[i] = x; cupArt.Rise[i] = rise; cupArt.Front[i] = front;
            }
        cupArt.InvalidateVisual();
        MoveWindow(cupWindow, cupRowLeft - CupGameArt.Pad, cupGround - CupGameArt.ArtHeight);
    }

    // The cups over the cat, which walks behind the row.
    private void RaiseCups()
    {
        if (cupWindow == null) return;
        var handle = new WindowInteropHelper(cupWindow).Handle;
        if (handle != IntPtr.Zero) SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
    }

    // count: the game ended the user's way, so it counts as played (a fullscreen app, a battle, another
    // monitor or DPI do not count). The best was already raised at each right cup.
    private void StopCupGame(bool count)
    {
        if (cupStep == CupStep.None) return;
        var was = cupStep;
        cupStep = CupStep.None;
        cupTime = 0;
        cupWindow?.Close();
        cupWindow = null; cupArt = null;
        renderer.CarryFish = false;
        pet.ReactTilt = pet.ReactDip = 0;
        if (cupSuspended) { cupSuspended = false; if (battle == null) pet.Emotions.SetSuspended(false); }
        if (was == CupStep.Wait) return;
        if (count && was != CupStep.Room)
        {
            CupGameRules.CountPlay(preferences);
            SavePreferences();
            if (!dragging && (cupRounds > 0 || was is CupStep.Reveal)) ShowTrickLabel(CupGameRules.TellBest(cupBestRaised, cupRounds)
                ? LF($"{cupRounds} vòng · Kỷ lục mới!", $"{cupRounds} in a row · New best!")
                : LF($"{cupRounds} vòng", $"{cupRounds} in a row"));
        }
        if (!dragging && pet.Action is PetAction.Sitting or PetAction.Looking or PetAction.Walking) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
        System.Diagnostics.Trace.WriteLine($"Cup game: over after {cupRounds} right cup(s){(count ? "" : ", not counted")}.");
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
}
