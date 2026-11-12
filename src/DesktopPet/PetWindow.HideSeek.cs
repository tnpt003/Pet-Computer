using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;

namespace DesktopPet;

// Mini-games (docs/MINI-GAMES.md → M2): hide-and-seek. Started from "Play with the cat" like the cup game and
// under the same rules (the user's request; never wakes the cat; nothing automatic while it runs; Pause, a
// battle, a fullscreen app, another monitor or DPI end it). Three hiding places on the cat's floor: the
// cardboard box when it is out there, and bushes drawn for the game only. While the user counts, the cat
// vanishes in a puff and is already behind one of them, sunk low so only its ear tips can peek over now and
// then; the cat's own window draws that place's cover over it, so a click there reaches the cat (a right
// guess). Three guesses, 60 seconds; then the cat pops out by itself. Dragging the cat or picking up the box
// ends the game. All C#: Python hears "toy_started" once and sees the "MiniGame" state.
public sealed partial class PetWindow
{
    private enum SeekStep { None, Wait, Count, Hide, Pop, Out }
    private sealed class SeekSpot
    {
        public double X, Height;
        public bool Box, Wrong;
        public Window? Window;
        public BushArt? Art;
        public double Decoy;
    }
    private SeekStep seekStep;
    private readonly List<SeekSpot> seekSpots = new();
    private int seekCat = -1, seekGuesses;
    private double seekTime, seekSearch, seekSignal, seekPeek = -1, seekFloor, seekScale, seekStartX, seekOrderCheck;
    private bool seekFound, seekFirstTry, seekSuspended, seekMoved;
    private string? seekMonitor;

    public bool HideSeekActive => seekStep != SeekStep.None;
    public string HideSeekPhase => seekStep.ToString();
    public int HideSeekSpotCount => seekSpots.Count;
    public int HideSeekCatSpot => seekCat;
    public double HideSeekSpotX(int i) => seekSpots[i].X;
    public bool HideSeekSpotIsBox(int i) => seekSpots[i].Box;
    public bool HideSeekSpotWrong(int i) => seekSpots[i].Wrong;
    public int HideSeekGuessesLeft => seekGuesses;
    public bool HideSeekFound => seekFound;
    public double HideSeekSink => renderer.HideSink;

    public bool HideSeekMenuEnabled => HideSeekActive || (!MiniGameBlocked && !MiniGameActive);
    public string HideSeekMenuLabel => preferences.HideSeekFound > 0
        ? LF($"Trốn tìm · tìm thấy {preferences.HideSeekFound}", $"Hide and seek · found {preferences.HideSeekFound}")
        : L("Trốn tìm", "Hide and seek");
    public string HideSeekMenuNote => HideSeekActive ? "" : MiniGameMenuNote;

    // The cat is out of sight behind its cover: no speech or thought bubble may give it away.
    private bool CatHiding => seekStep is SeekStep.Count or SeekStep.Hide && seekMoved;
    private bool SeekOwnsMovement => seekStep is not (SeekStep.None or SeekStep.Wait);

    // The menu row: a game now (or once the cat is free); the same row again ends it. False when it may not.
    public bool ToggleHideSeek()
    {
        if (HideSeekActive) { StopHideSeek(); return true; }
        if (MiniGameBlocked || MiniGameActive) return false;
        revision++; // Like DoNow: AI replies chosen before this request are dropped.
        StopCall(); StopLesson();
        seekStep = SeekStep.Wait; seekTime = 0;
        if (!TrickCatBusy && CanPlay) BeginHideSeek();
        return true;
    }

    private void BeginHideSeek()
    {
        CancelFeatureActivity(); // Ends the ball, laser or toy game first.
        string here = CurrentMonitor().DeviceName;
        var box = DesktopToy(DesktopToyRules.Box);
        bool boxHere = box != null && box.Window?.IsVisible == true && box.Monitor == here && !box.Held && !box.Press && box.Motion.Height < 1 && !BoxOccupied;
        var taken = new List<(double Left, double Right)>(ToyObstacles(here));
        foreach (var thing in DesktopThings)
            if (thing.Window?.IsVisible == true && thing.Monitor == here && !(boxHere && thing == box))
                taken.Add((thing.Motion.X - thing.HalfWidth, thing.Motion.X + thing.HalfWidth));
        if (HideSeekRules.Place(CatCenter, bounds.Left, bounds.Right, taken, boxHere ? box!.Motion.X : null, featureRandom) is not { } places)
        {
            seekStep = SeekStep.None;
            ShowTrickLabel(L("Không đủ chỗ để trốn", "No room to hide"));
            System.Diagnostics.Trace.WriteLine("Hide and seek: no room for three hiding places.");
            return;
        }
        seekFloor = bounds.Bottom;
        seekSpots.Clear();
        foreach (double x in places)
        {
            bool isBox = boxHere && Math.Abs(x - box!.Motion.X) < 0.5;
            seekSpots.Add(new SeekSpot { X = x, Box = isBox, Height = isBox ? DesktopToyRules.BoxHeight : HideSeekRules.BushHeight, Decoy = HideSeekRules.NextDecoy(featureRandom) });
        }
        seekCat = featureRandom.Next(seekSpots.Count);
        foreach (var spot in seekSpots) if (!spot.Box) OpenBush(spot, grow: 0);
        seekGuesses = HideSeekRules.Guesses;
        seekFound = seekFirstTry = seekMoved = false;
        seekTime = seekSearch = 0; seekPeek = -1; seekOrderCheck = 0;
        seekStartX = CatCenter;
        seekMonitor = here; seekScale = scale;
        ResetMiniGameKeys(); // The click that chose the menu row does not end it.
        seekStep = SeekStep.Count;
        pet.Emotions.SetSuspended(true); seekSuspended = true; // No automatic emotion while playing.
        pet.Speech.Hide(); pet.Thought.Clear();
        Interact("toy_started"); // Counts as play for Python, once per game.
        ShowMiniLabel(L("Nhắm mắt nhé! 3…", "Close your eyes! 3…"), CatCenter, pet.Y + PetSettings.SpeechAnchorY, 1.2);
        System.Diagnostics.Trace.WriteLine("Hide and seek: started.");
    }

    private void OpenBush(SeekSpot spot, double grow)
    {
        var art = new BushArt { Grow = grow, Cursor = System.Windows.Input.Cursors.Hand };
        var window = CreateOverlayWindow(art, "Pet hide-and-seek bush");
        window.Width = BushArt.ArtWidth; window.Height = BushArt.ArtHeight;
        window.Left = spot.X - BushArt.ArtWidth / 2; window.Top = seekFloor - BushArt.ArtHeight + 2;
        art.MouseLeftButtonDown += (_, e) => { e.Handled = true; GuessSpot(seekSpots.IndexOf(spot)); };
        window.Closed += (_, _) => { if (spot.Window == window) { spot.Window = null; spot.Art = null; } };
        spot.Window = window; spot.Art = art;
        window.Show();
        MoveWindow(window, spot.X - BushArt.ArtWidth / 2, seekFloor - BushArt.ArtHeight + 2);
        KeepBushesBelowCat(force: true);
    }

    // The bushes stay under the cat, so the cat steps out in front of its own.
    private void KeepBushesBelowCat(bool force = false)
    {
        var cat = new WindowInteropHelper(this).Handle;
        if (cat == IntPtr.Zero) return;
        var above = WindowsAbove(cat);
        const uint NoSize = 0x0001, NoMove = 0x0002, NoActivate = 0x0010;
        foreach (var spot in seekSpots)
            if (spot.Window != null && new WindowInteropHelper(spot.Window).Handle is var handle && handle != IntPtr.Zero && (force || above.Contains(handle)))
                SetWindowPos(handle, cat, 0, 0, 0, 0, NoSize | NoMove | NoActivate);
    }

    // Every unpaused frame from TickFeatures, before CanPlay: the wait, what ends a game, Esc and clicks elsewhere.
    private void TickHideSeekClock(double dt)
    {
        if (seekStep == SeekStep.None) return;
        if (MiniGameBlocked) { StopHideSeek(); return; }
        if (seekStep == SeekStep.Wait)
        {
            if ((seekTime += dt) > CupGameRules.SleepWaitSeconds) { seekStep = SeekStep.None; return; }
            if (!TrickCatBusy && CanPlay) BeginHideSeek();
            return;
        }
        if (dragging) return; // The drag ends it in TickFeatures (CancelFeatureActivity).
        if (CurrentMonitor().DeviceName != seekMonitor || Math.Abs(scale - seekScale) > 0.001) { StopHideSeek(); return; }
        var (esc, click) = MiniGameKeyPresses();
        if (esc || (click && !OverSeekPlaces(CursorPosition()))) { StopHideSeek(); return; } // Esc, or a click elsewhere.
        if ((seekOrderCheck -= dt) <= 0) { seekOrderCheck = 0.5; KeepBushesBelowCat(); }
    }

    private bool OverSeekPlaces(Point p)
    {
        if (p.X >= pet.X - 10 && p.X <= pet.X + settings.Width + 10 && p.Y >= pet.Y - 10 && p.Y <= pet.Y + settings.Height + 10) return true;
        return seekSpots.Any(s => Math.Abs(p.X - s.X) <= (s.Box ? DesktopToyRules.BoxHalfWidth + 20 : BushArt.ArtWidth / 2) && p.Y <= seekFloor + 4 && p.Y >= seekFloor - s.Height - 30);
    }

    // From TickFeatures with the cat free (CanPlay), next to the cup game. True while it owns the cat.
    private bool TickHideSeek(double dt)
    {
        if (!SeekOwnsMovement) return false;
        seekTime += dt;
        foreach (var spot in seekSpots)
            if (spot.Art is { } art && art.Rustle >= 0) { art.Rustle += dt; if (art.Rustle > HideSeekRules.SignalSeconds) art.Rustle = -1; art.InvalidateVisual(); }
        switch (seekStep)
        {
            case SeekStep.Count:
                HoldStill(PetAction.Sitting, pet.Facing);
                int left = Math.Max(1, (int)Math.Ceiling(HideSeekRules.CountSeconds - seekTime));
                if (seekTime < HideSeekRules.CountSeconds)
                    ShowMiniLabel(LF($"Nhắm mắt nhé! {left}…", $"Close your eyes! {left}…"), seekStartX, seekFloor - settings.Height + PetSettings.SpeechAnchorY, 0.3);
                foreach (var spot in seekSpots)
                    if (spot.Art is { } bush) { bush.Grow = Math.Min(1, seekTime / HideSeekRules.GrowSeconds); bush.InvalidateVisual(); }
                // Gone in a puff, then already behind the place it picked.
                double v = Math.Min(1, (seekTime - HideSeekRules.VanishAt) / CatVanishSeconds);
                if (!seekMoved && v >= 0)
                {
                    renderer.TeleportPop = Math.Max(0, v < 0.25 ? 1 + v / 0.25 * 0.18 : 1.18 * (1 - (v - 0.25) / 0.75));
                    renderer.TeleportPuff = v * 0.45;
                    if (v >= 1) HideCat();
                }
                if (seekTime < HideSeekRules.CountSeconds) return true;
                seekStep = SeekStep.Hide; seekTime = 0;
                seekSignal = HideSeekRules.NextSignal(featureRandom);
                ShowSeekPrompt();
                return true;
            case SeekStep.Hide:
                HoldStill(PetAction.Sitting, pet.Facing);
                if ((seekSearch += dt) > HideSeekRules.SearchSeconds) { PopOut(found: false); return true; }
                // Now and then the ear tips peek over (and a bush the cat is in rustles); the other places stir less often.
                if (seekPeek < 0 && (seekSignal -= dt) <= 0) { seekPeek = 0; seekSignal = HideSeekRules.NextSignal(featureRandom) + HideSeekRules.SignalSeconds; }
                double sink = HideSeekRules.Sink(seekSpots[seekCat].Height);
                if (seekPeek >= 0)
                {
                    seekPeek += dt;
                    sink -= HideSeekRules.Peek * Math.Sin(Math.PI * Math.Min(1, seekPeek / HideSeekRules.SignalSeconds));
                    renderer.HideRustle = seekSpots[seekCat].Box ? -1 : seekPeek;
                    if (seekPeek >= HideSeekRules.SignalSeconds) { seekPeek = -1; renderer.HideRustle = -1; }
                }
                renderer.HideSink = sink;
                for (int i = 0; i < seekSpots.Count; i++)
                {
                    var spot = seekSpots[i];
                    if (i == seekCat || spot.Wrong || (spot.Decoy -= dt) > 0) continue;
                    spot.Decoy = HideSeekRules.NextDecoy(featureRandom);
                    StirSpot(spot);
                }
                ShowSeekPrompt();
                return true;
            case SeekStep.Pop: // Up out of the hiding place.
                HoldStill(PetAction.Sitting, pet.Facing);
                double f = Math.Min(1, seekTime / HideSeekRules.PopSeconds);
                renderer.HideSink = HideSeekRules.Sink(seekSpots[seekCat].Height) * (1 - f * f);
                if (f < 1) return true;
                // Out in front: its own bush comes back in its own window under the cat; a little hop.
                var own = seekSpots[seekCat];
                if (!own.Box) OpenBush(own, grow: 1);
                renderer.HideSink = 0; renderer.HideCover = 0; renderer.HideRustle = -1;
                seekStep = SeekStep.Out; seekTime = 0;
                return true;
            case SeekStep.Out: // A happy little bounce, then the bushes wither away.
                HoldStill(PetAction.Sitting, pet.Facing);
                renderer.TeleportDrop = 14 * Math.Max(0, Math.Sin(Math.PI * Math.Min(1, seekTime / 0.4)));
                pet.ReactDip = seekTime > 0.4 ? -3 * Math.Abs(Math.Sin(seekTime * 9)) : 0;
                double fade = (seekTime - (HideSeekRules.OutSeconds - HideSeekRules.FadeSeconds)) / HideSeekRules.FadeSeconds;
                if (fade > 0)
                    foreach (var spot in seekSpots)
                        if (spot.Art is { } bush) { bush.Grow = Math.Max(0, 1 - fade); bush.InvalidateVisual(); }
                if (seekTime < HideSeekRules.OutSeconds) return true;
                StopHideSeek();
                return false;
        }
        return false;
    }

    // At the end of the puff: behind the hiding place, on the floor, sunk low under its cover.
    private void HideCat()
    {
        var spot = seekSpots[seekCat];
        PetPhysics.LetGo(pet);
        pet.LedgeId = pet.ClimbId = 0;
        pet.X = spot.X - settings.Width / 2;
        pet.Y = seekFloor - settings.Height;
        pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
        pet.Grounded = true;
        pet.Motion.Reset(pet);
        renderer.TeleportPop = 1; renderer.TeleportPuff = -1;
        renderer.HideCover = spot.Box ? 2 : 1;
        renderer.HideHalf = spot.Box ? DesktopToyRules.BoxHalfWidth - 2 : HideSeekRules.BushHalfWidth - 8;
        renderer.HideSink = HideSeekRules.Sink(spot.Height);
        pet.Facing = featureRandom.Next(2) == 0 ? -1 : 1;
        seekMoved = true;
        PlayPetSound("sparkle");
    }

    private void ShowSeekPrompt()
    {
        double middle = (seekSpots.First().X + seekSpots.Last().X) / 2;
        ShowMiniLabel(L("Mèo trốn ở đâu?", "Where is the cat hiding?"), middle, seekFloor - 110, 0.3);
    }

    // A place without the cat stirs too, so a stir alone does not give it away.
    private void StirSpot(SeekSpot spot)
    {
        if (spot.Art is { } bush) { bush.Rustle = 0; bush.InvalidateVisual(); }
        else if (spot.Box && DesktopToy(DesktopToyRules.Box) is { } box) box.Motion.Wobble();
    }

    // A guess on a place (its index); the cat's own place or the cat itself is right.
    public bool GuessSpot(int index)
    {
        if (seekStep != SeekStep.Hide || paused || index < 0 || index >= seekSpots.Count || seekSpots[index].Wrong) return false;
        if (index == seekCat) { PopOut(found: true); return true; }
        var spot = seekSpots[index];
        spot.Wrong = true;
        StirSpot(spot);
        if (spot.Window != null) spot.Window.Opacity = 0.45;
        else if (spot.Box && DesktopToy(DesktopToyRules.Box)?.Window is { } boxWindow) boxWindow.Opacity = 0.5;
        PlayPetSound("ball_tap");
        if (--seekGuesses <= 0) { PopOut(found: false); return true; }
        ShowMiniLabel(L("Không có ở đây", "Not here"), spot.X, seekFloor - spot.Height - 6, HideSeekRules.WrongSeconds);
        return true;
    }

    private void PopOut(bool found)
    {
        seekFound = found;
        seekFirstTry = found && seekGuesses == HideSeekRules.Guesses;
        if (found) { HideSeekRules.RecordFound(preferences, seekFirstTry); SavePreferences(); PlayPetSound("excited"); }
        seekPeek = -1;
        seekStep = SeekStep.Pop; seekTime = 0;
        double x = seekSpots[seekCat].X, top = seekFloor - settings.Height + PetSettings.SpeechAnchorY;
        ShowMiniLabel(found ? (seekFirstTry ? L("Tìm thấy rồi! Ngay lượt đầu!", "Found! First try!") : L("Tìm thấy rồi!", "Found!")) : L("Ở đây nè!", "Here I am!"),
            x, top, HideSeekRules.PopSeconds + HideSeekRules.OutSeconds);
        System.Diagnostics.Trace.WriteLine(found ? $"Hide and seek: found{(seekFirstTry ? " with the first guess" : "")}." : "Hide and seek: the cat came out by itself.");
    }

    // A press on the hidden cat (its peeking ears, or the cover its window draws): the right guess. True when
    // the press was that guess, so the press goes on as a normal press (a drag still picks the cat up).
    private bool HideSeekPressCat() => seekStep == SeekStep.Hide && GuessSpot(seekCat);

    // While the found cat pops out, holding the mouse on it is not yet a drag: the game waits.
    private bool SeekRidesPress => pressPending && !dragging && seekStep is SeekStep.Pop or SeekStep.Out;

    // A click on the box during a game is a guess; picking it up ends the game.
    private bool HideSeekClickThing(DesktopThing thing)
    {
        if (seekStep == SeekStep.None || thing.Id != DesktopToyRules.Box) return false;
        int index = seekSpots.FindIndex(s => s.Box);
        if (index < 0) return false;
        GuessSpot(index);
        return true;
    }

    private void HideSeekThingPicked(DesktopThing thing)
    {
        if (seekStep != SeekStep.None && thing.Id == DesktopToyRules.Box && seekSpots.Any(s => s.Box)) StopHideSeek();
    }

    private void StopHideSeek()
    {
        if (seekStep == SeekStep.None) return;
        var was = seekStep;
        seekStep = SeekStep.None;
        seekTime = 0;
        foreach (var spot in seekSpots) { spot.Window?.Close(); spot.Window = null; spot.Art = null; }
        if (DesktopToy(DesktopToyRules.Box)?.Window is { } boxWindow) boxWindow.Opacity = 1;
        seekSpots.Clear();
        renderer.HideSink = 0; renderer.HideCover = 0; renderer.HideRustle = -1;
        renderer.TeleportPop = 1; renderer.TeleportDrop = 0; renderer.TeleportPuff = -1;
        pet.ReactDip = 0;
        if (seekSuspended) { seekSuspended = false; if (battle == null) pet.Emotions.SetSuspended(false); }
        if (was is SeekStep.Count or SeekStep.Hide) HideMiniLabel();
        if (was != SeekStep.Wait && !dragging && pet.Action is PetAction.Sitting or PetAction.Looking) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
        if (was != SeekStep.Wait) System.Diagnostics.Trace.WriteLine("Hide and seek: over.");
    }
}
