using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;

namespace DesktopPet;

// Mini-games (docs/MINI-GAMES.md → M3): catch the fireflies. Started from "Play with the cat" like the other
// games and under the same rules (the user's request; never wakes the cat; nothing automatic while it runs;
// Pause, a battle, a fullscreen app, another monitor or DPI end it). A play area about 360×220 around the cat,
// over its floor or the window it stands on, in its own window under the cat's: 3–5 fireflies drift there for
// 30 seconds, never into the cat's body, so a click on a fly and a click on the cat never mix up. A click on a
// fly bursts it into sparks; the cat pounces too every 6–8 seconds, a short hop drawn in place (it lands where
// it stood, no physics, never off its ledge), at most four flies a game, sometimes a miss. One shared score.
// All C#: Python hears "toy_started" once and sees the "MiniGame" state.
public sealed partial class PetWindow
{
    private enum FlyStep { None, Wait, Room, Count, Play, Away }
    private FlyStep flyStep;
    private readonly FireflySwarm flySwarm = new();
    private readonly FireflyCat flyCat = new();
    private Window? flyWindow;
    private FireflyArt? flyArt;
    private Firefly? flyTarget;
    private double flyTime, flySpot, flyGround, flyLeft, flyTop, flyHeight, flyScale, flyOrderCheck, flyPounce = -1;
    private int flyUser, flyByCat;
    private bool flyBestRaised, flySuspended, flyPounceHits, flyPounceDone;
    private string? flyMonitor;

    public bool FirefliesActive => flyStep != FlyStep.None;
    public string FirefliesPhase => flyStep.ToString();
    public int FirefliesUser => flyUser;
    public int FirefliesCat => flyByCat;
    public int FirefliesAlive => flySwarm.Alive;
    public double FirefliesTime => flySwarm.Time;
    public bool FirefliesPouncing => flyPounce >= 0;
    public double FirefliesHop => renderer.TeleportDrop;
    public Rect FirefliesArea => new(flyLeft, flyTop, FireflyRules.AreaWidth, flyHeight);
    public FireflySwarm FirefliesSwarm => flySwarm;

    public bool FirefliesMenuEnabled => FirefliesActive || (!MiniGameBlocked && !MiniGameActive);
    public string FirefliesMenuLabel => preferences.FireflyBest > 0
        ? LF($"Bắt đom đóm · kỷ lục {preferences.FireflyBest}", $"Catch fireflies · best {preferences.FireflyBest}")
        : L("Bắt đom đóm", "Catch fireflies");
    public string FirefliesMenuNote => FirefliesActive ? "" : MiniGameMenuNote;

    private bool FlyOwnsMovement => flyStep is not (FlyStep.None or FlyStep.Wait);

    // The menu row: a game now (or once the cat is free); the same row again ends it. False when it may not.
    public bool ToggleFireflies()
    {
        if (FirefliesActive) { StopFireflies(count: true); return true; }
        if (MiniGameBlocked || MiniGameActive) return false;
        revision++; // Like DoNow: AI replies chosen before this request are dropped.
        StopCall(); StopLesson();
        flyStep = FlyStep.Wait; flyTime = 0;
        if (!TrickCatBusy && CanPlay) BeginFireflies();
        return true;
    }

    private void BeginFireflies()
    {
        CancelFeatureActivity(); // Ends the ball, laser or toy game first.
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
        double ground = pet.Y + settings.Height;
        flyHeight = FireflyRules.Height(ground, bounds.Top);
        double reach = settings.WalkSpeed * CupGameRules.RoomWalkSeconds;
        if (flyHeight <= 0 || FireflyRules.Place(CatCenter, settings.Width / 2, left, right, bounds.Left, bounds.Right, taken, reach) is not { } spot)
        {
            flyStep = FlyStep.None;
            ShowTrickLabel(L("Không đủ chỗ", "No room"));
            System.Diagnostics.Trace.WriteLine("Fireflies: no room next to the cat.");
            return;
        }
        flySpot = spot;
        flyGround = ground;
        flyUser = flyByCat = 0; flyBestRaised = false;
        flyTime = 0; flyPounce = -1; flyTarget = null;
        flyMonitor = CurrentMonitor().DeviceName; flyScale = scale;
        ResetMiniGameKeys(); // The click that chose the menu row does not end it.
        flyStep = FlyStep.Room;
        pet.Emotions.SetSuspended(true); flySuspended = true; // No automatic emotion while playing.
        Interact("toy_started"); // Counts as play for Python, once per game.
        System.Diagnostics.Trace.WriteLine("Fireflies: started.");
    }

    // Every unpaused frame from TickFeatures, before CanPlay: the wait, what ends a game, Esc and clicks elsewhere.
    private void TickFirefliesClock(double dt)
    {
        if (flyStep == FlyStep.None) return;
        if (MiniGameBlocked) { StopFireflies(count: false); return; }
        if (flyStep == FlyStep.Wait)
        {
            if ((flyTime += dt) > CupGameRules.SleepWaitSeconds) { flyStep = FlyStep.None; return; }
            if (!TrickCatBusy && CanPlay) BeginFireflies();
            return;
        }
        if (dragging) return; // The drag ends it in TickFeatures (CancelFeatureActivity).
        if (CurrentMonitor().DeviceName != flyMonitor || Math.Abs(scale - flyScale) > 0.001 ||
            (pet.Grounded && Math.Abs(pet.Y + settings.Height - flyGround) > 3)) { StopFireflies(count: false); return; }
        var (esc, click) = MiniGameKeyPresses();
        if (esc || (click && !OverFlyArea(CursorPosition()))) { StopFireflies(count: true); return; } // Esc, or a click elsewhere.
        if ((flyOrderCheck -= dt) <= 0) { flyOrderCheck = 0.5; KeepFliesBelowCat(); }
    }

    // A click inside the area (a miss among the flies included) or on the cat is part of the game.
    private bool OverFlyArea(Point p)
    {
        if (p.X >= pet.X - 10 && p.X <= pet.X + settings.Width + 10 && p.Y >= pet.Y - 10 && p.Y <= pet.Y + settings.Height + 10) return true;
        return flyWindow != null && p.X >= flyLeft && p.X <= flyLeft + FireflyRules.AreaWidth && p.Y >= flyTop && p.Y <= flyTop + flyHeight;
    }

    // From TickFeatures with the cat free (CanPlay), next to the other games. True while it owns the cat.
    private bool TickFireflies(double dt)
    {
        if (!FlyOwnsMovement) return false;
        flyTime += dt;
        switch (flyStep)
        {
            case FlyStep.Room: // To where the area fits, then the count.
                if (Math.Abs(CatCenter - flySpot) > 6 && flyTime < CupGameRules.RoomWalkSeconds) { WalkToward(flySpot - settings.Width / 2); return true; }
                if (Math.Abs(CatCenter - flySpot) > 20) { ShowTrickLabel(L("Không đủ chỗ", "No room")); StopFireflies(count: false); return false; }
                HoldStill(PetAction.Sitting, pet.Facing);
                flyLeft = CatCenter - FireflyRules.AreaWidth / 2;
                flyTop = flyGround - flyHeight;
                flySwarm.Reset(FireflyRules.AreaWidth, flyHeight, featureRandom);
                flyCat.Reset(featureRandom);
                flyStep = FlyStep.Count; flyTime = 0;
                return true;
            case FlyStep.Count: // "Catch the fireflies! 3… 2… 1…", then they come out.
                HoldStill(PetAction.Sitting, pet.Facing);
                int left = Math.Max(1, 3 - (int)(flyTime / (FireflyRules.CountSeconds / 3)));
                ShowMiniLabel(LF($"Bắt đom đóm nào! {left}…", $"Catch the fireflies! {left}…"), CatCenter, flyTop - 2, 0.3);
                if (flyTime < FireflyRules.CountSeconds) return true;
                ShowFlies();
                PlayPetSound("sparkle");
                flyStep = FlyStep.Play; flyTime = 0;
                return true;
            case FlyStep.Play:
                flySwarm.Step(dt, featureRandom);
                flyArt?.Step(dt);
                TickFlyCat(dt);
                int seconds = Math.Max(0, (int)Math.Ceiling(FireflyRules.GameSeconds - flySwarm.Time)), total = flyUser + flyByCat;
                ShowMiniLabel(LF($"Bắt đom đóm · còn {seconds} giây · {total} đốm", $"Catch fireflies · {seconds}s left · {total} caught"), CatCenter, flyTop - 2, 0.3);
                PlaceFlies();
                if (flySwarm.Time < FireflyRules.GameSeconds) return true;
                // Time: the flies left fly away, the cat sits watching them go.
                EndPounce();
                flySwarm.Leave();
                flyStep = FlyStep.Away; flyTime = 0;
                ShowFlySummary();
                return true;
            case FlyStep.Away:
                HoldStill(PetAction.Looking, pet.Facing);
                flySwarm.Step(dt, featureRandom);
                flyArt?.Step(dt);
                PlaceFlies();
                if (flyTime < FireflyRules.AwaySeconds) return true;
                StopFireflies(count: true);
                return false;
        }
        return false;
    }

    // The cat's own turn: now and then a fly low and near, a crouch, a little hop in place with a swipe.
    private void TickFlyCat(double dt)
    {
        if (flyPounce < 0)
        {
            var near = flySwarm.Flies.Where(f => !f.Caught).OrderBy(f => Math.Abs(f.X - FireflyRules.AreaWidth / 2)).FirstOrDefault();
            HoldStill(PetAction.Sitting, near == null ? pet.Facing : near.X < FireflyRules.AreaWidth / 2 ? -1 : 1);
            pet.ReactTilt = near == null ? 0 : Math.Clamp((flyHeight - near.Y - 80) / 12, -8, 8);
            if (!flyCat.Due(dt)) return;
            if (flySwarm.PounceTarget() is not { } target) { flyCat.Retry(); return; }
            flyTarget = target;
            flyPounceHits = flyCat.Pounce(featureRandom);
            flyPounce = 0; flyPounceDone = false;
            return;
        }
        flyPounce += dt;
        HoldStill(PetAction.Looking, flyTarget == null || flyTarget.Caught ? pet.Facing : flyTarget.X < FireflyRules.AreaWidth / 2 ? -1 : 1);
        pet.ReactTilt = 0;
        if (flyPounce < FireflyRules.CrouchSeconds) { pet.ReactDip = 6 * Math.Sin(Math.PI / 2 * flyPounce / FireflyRules.CrouchSeconds); return; }
        double hop = (flyPounce - FireflyRules.CrouchSeconds) / FireflyRules.HopSeconds;
        pet.ReactDip = 0;
        renderer.TeleportDrop = FireflyRules.HopHeight * Math.Sin(Math.PI * Math.Min(1, hop));
        if (!flyPounceDone && hop >= 0.15) { pet.BatRemaining = 0.3; PlayPetSound("ball_tap"); flyPounceDone = true; }
        if (hop >= 0.5 && flyTarget != null)
        {
            if (flyPounceHits && flySwarm.Catch(flyTarget, byCat: true)) { flyByCat++; flyCat.Caught(); FlyCaught(flyTarget, byCat: true); }
            flyTarget = null;
        }
        if (hop >= 1) EndPounce();
    }

    private void EndPounce()
    {
        flyPounce = -1; flyTarget = null;
        renderer.TeleportDrop = 0;
        pet.ReactDip = pet.ReactTilt = 0;
    }

    private void FlyCaught(Firefly fly, bool byCat)
    {
        flyArt?.Bursts.Add(new FireflyArt.Burst { X = fly.X, Y = fly.Y, ByCat = byCat });
        if (FireflyRules.RaiseBest(preferences, flyUser + flyByCat)) { flyBestRaised = true; SavePreferences(); }
        PlayPetSound("sparkle");
    }

    // A click in the area (its own coordinates): a fly under it bursts. True when one was caught.
    public bool TapFirefly(double x, double y)
    {
        if (flyStep != FlyStep.Play || paused || flySwarm.At(x, y) is not { } fly) return false;
        if (fly == flyTarget) flyTarget = null; // The cat's pounce goes on, at nothing.
        flySwarm.Catch(fly, byCat: false);
        flyUser++;
        FlyCaught(fly, byCat: false);
        return true;
    }

    private void ShowFlies()
    {
        flyArt = new FireflyArt(flySwarm) { Cursor = System.Windows.Input.Cursors.Hand };
        flyArt.Size(FireflyRules.AreaWidth, flyHeight);
        flyArt.MouseLeftButtonDown += (_, e) => { e.Handled = true; var p = e.GetPosition(flyArt); TapFirefly(p.X, p.Y); };
        flyWindow = CreateOverlayWindow(flyArt, "Pet fireflies");
        flyWindow.Width = FireflyRules.AreaWidth; flyWindow.Height = flyHeight;
        flyWindow.Left = flyLeft; flyWindow.Top = flyTop;
        flyWindow.Closed += (_, _) => { flyWindow = null; flyArt = null; };
        flyWindow.Show();
        KeepFliesBelowCat(force: true);
        PlaceFlies();
    }

    private void PlaceFlies()
    {
        if (flyWindow == null || flyArt == null) return;
        flyArt.InvalidateVisual();
        MoveWindow(flyWindow, flyLeft, flyTop);
    }

    // The flies' window stays under the cat's, so the cat (and a click on it) is always in front.
    private void KeepFliesBelowCat(bool force = false)
    {
        var cat = new WindowInteropHelper(this).Handle;
        if (cat == IntPtr.Zero || flyWindow == null) return;
        var handle = new WindowInteropHelper(flyWindow).Handle;
        if (handle == IntPtr.Zero || (!force && !WindowsAbove(cat).Contains(handle))) return;
        SetWindowPos(handle, cat, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
    }

    private void ShowFlySummary()
    {
        int total = flyUser + flyByCat;
        string words = flyBestRaised
            ? LF($"Cùng bắt được {total} đốm (bạn {flyUser} · mèo {flyByCat}) · Kỷ lục mới!", $"Caught {total} together (you {flyUser} · cat {flyByCat}) · New best!")
            : LF($"Cùng bắt được {total} đốm (bạn {flyUser} · mèo {flyByCat})", $"Caught {total} together (you {flyUser} · cat {flyByCat})");
        ShowMiniLabel(words, CatCenter, flyTop - 2, FireflyRules.SummarySeconds);
        if (flyBestRaised) PlayPetSound("excited");
    }

    // count: the game ended the user's way, so it counts as played (a fullscreen app, a battle, another
    // monitor or DPI do not count). The best was already raised at each catch.
    private void StopFireflies(bool count)
    {
        if (flyStep == FlyStep.None) return;
        var was = flyStep;
        flyStep = FlyStep.None;
        flyTime = 0;
        flyWindow?.Close();
        flyWindow = null; flyArt = null;
        EndPounce();
        if (flySuspended) { flySuspended = false; if (battle == null) pet.Emotions.SetSuspended(false); }
        if (was == FlyStep.Wait) return;
        if (was is FlyStep.Count or FlyStep.Play) HideMiniLabel();
        if (count && was is FlyStep.Play or FlyStep.Away)
        {
            FireflyRules.CountPlay(preferences);
            SavePreferences();
            if (was == FlyStep.Play && !dragging) ShowFlySummary();
        }
        if (!dragging && pet.Action is PetAction.Sitting or PetAction.Looking or PetAction.Walking) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
        System.Diagnostics.Trace.WriteLine($"Fireflies: over, {flyUser} by the user and {flyByCat} by the cat{(count ? "" : ", not counted")}.");
    }
}
