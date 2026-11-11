using System;

namespace DesktopPet;

// Fetch with the yarn ball (docs/TOY-PLAY.md → M3). A hard throw: the cat watches it land, runs to it,
// carries it in its mouth to under the cursor, drops it and sits waiting for the next throw. Now and
// then it also brings the ball on its own to ask for a game. All C#: no Python, no IPC event.
// While carried the ball's window is hidden and the cat's own window draws it (PetRenderer.CarryBall),
// so every click still reaches the cat; any click, drag or cancelled activity drops it from the mouth.
public sealed partial class PetWindow
{
    private enum Fetch { None, Watch, Run, Carry, Wait }
    private Fetch fetch;
    // This trip is the cat's own invitation (no throw yet); it stops in Quiet like the cat's other ideas.
    private bool fetchInvite;
    // Resting after five in a row: it only watches this throw.
    private bool fetchLookOnly;
    private double fetchTime, fetchInviteDue = -1;
    private readonly FetchStreak fetchStreak = new();
    // From the ball's centre to the cat's mouth, and the mouth above the cat's floor line (DIPs).
    public const double FetchMouthAhead = 4, FetchMouthHeight = 44, FetchRunScale = 1.6, FetchReach = 10;

    private DesktopThing? FetchBall => DesktopToy(DesktopToyRules.Ball);
    public bool CarryingBall => fetch == Fetch.Carry;
    public bool Fetching => fetch != Fetch.None;
    public bool FetchingInvite => fetch != Fetch.None && fetchInvite;
    public string FetchPhase => fetch.ToString();
    public FetchStreak FetchStreak => fetchStreak;

    // The ball lies out, shown, on the cat's monitor, not in the 45-second game.
    private bool FetchBallWithCat => FetchBall is { } ball && ToysMayShow && !ball.InGame && toyWindow == null
        && (ball.Carried || ball.Window?.IsVisible == true) && ball.Monitor == CurrentMonitor().DeviceName;
    private bool CatOnFloor => pet.Grounded && pet.LedgeId == 0;
    private double MouthX => CatCenter + pet.Facing * FetchMouthAhead;

    // From EndToyDrag: the ball was let go at this speed (its DIPs per second).
    private void BallThrown(DesktopThing thing, double vx, double vy)
    {
        if (!FetchStreak.IsThrow(vx, vy) || thing.Id != DesktopToyRules.Ball) return;
        bool ours = fetch is Fetch.Wait or Fetch.Watch;
        if (fetch != Fetch.None && !ours) return;
        var asked = PetAutonomy.BlockedBy(AutoKind.ToyFetch);
        if (ours) asked &= ~AutoBlock.Feature; // Waiting for this very throw.
        if (Blocking(asked) != AutoBlock.None || !CatOnFloor || !FetchBallWithCat) return;
        if (!ours && pet.Action is not (PetAction.Idle or PetAction.Walking)) return;
        fetch = Fetch.Watch;
        fetchInvite = fetchLookOnly = false;
        fetchTime = 0;
        HoldStill(PetAction.Looking, thing.Motion.X < CatCenter ? -1 : 1);
    }

    // For checks: the cat brings the ball now, as its invitation timer would. False when it may not.
    public bool InviteToFetch()
    {
        if (fetch != Fetch.None || !MayAct(AutoKind.ToyInvite) || !CatOnFloor || !FetchBallWithCat || FetchBall is not { } ball
            || ball.Held || ball.Press || !ball.Motion.AtRest || pet.Action is not (PetAction.Idle or PetAction.Sitting or PetAction.Walking)) return false;
        fetch = Fetch.Run;
        fetchInvite = true; fetchLookOnly = false;
        fetchTime = 0;
        System.Diagnostics.Trace.WriteLine("The cat goes to bring the yarn ball.");
        return true;
    }

    // Every frame from TickDesktopToys, before the toys move: the rest timer, and what ends a trip
    // whatever the cat is doing (a battle, a fullscreen app, the ball gone, put away or in the game).
    private void GuardToyFetch(double dt)
    {
        if (paused) return;
        fetchStreak.Step(dt);
        if (fetch == Fetch.None) return;
        if (FetchBall == null) { StopToyFetch(drop: false); return; }
        if (BattleActive || !FetchBallWithCat) StopToyFetch(drop: true);
    }

    // From TickFeatures (a free cat, after the ball game). True while the trip owns the cat.
    private bool TickToyFetch(double dt)
    {
        if (fetch == Fetch.None)
        {
            if (fetchInviteDue < 0) fetchInviteDue = FetchStreak.NextInvite(featureRandom.NextDouble());
            if ((fetchInviteDue -= dt) > 0) return false;
            fetchInviteDue = FetchStreak.NextInvite(featureRandom.NextDouble());
            if (!InviteToFetch()) return false;
        }
        if (fetchInvite && EffectiveQuiet) { StopToyFetch(drop: true); return false; }
        var ball = FetchBall!;
        var motion = ball.Motion;
        fetchTime += dt;
        switch (fetch)
        {
            case Fetch.Watch:
                if (ball.Held || ball.Press) { StopToyFetch(drop: false); return false; }
                HoldStill(PetAction.Looking, motion.X < CatCenter ? -1 : 1);
                if (fetchLookOnly)
                {
                    if (motion.AtRest || fetchTime > 8) StopToyFetch(drop: false);
                    return fetch != Fetch.None;
                }
                // It has landed and slowed down (or bounces on for long): off it goes, unless resting.
                if (fetchTime < 0.3 || motion.Height > 0.5 || (Math.Abs(motion.VX) > 160 && fetchTime < 6)) return true;
                if (!fetchStreak.TryFetch()) { fetchLookOnly = true; fetchTime = 0; return true; }
                fetch = Fetch.Run; fetchTime = 0;
                return true;
            case Fetch.Run:
                if (ball.Held || ball.Press || fetchTime > FetchStreak.WalkSeconds) { StopToyFetch(drop: false); return false; }
                if (Math.Abs(motion.X - MouthX) <= FetchReach && motion.Height < 24) { PickUpBall(ball); return true; }
                WalkToward(Math.Clamp(motion.X - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width)));
                if (!fetchInvite) pet.VX *= FetchRunScale; // A throw is chased at a run.
                return true;
            case Fetch.Carry:
                double target = Math.Clamp(CursorPosition().X - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
                if (Math.Abs(target - pet.X) < 8 || fetchTime > FetchStreak.WalkSeconds)
                {
                    HoldStill(PetAction.Sitting, pet.Facing);
                    DropCarriedBall();
                    fetch = Fetch.Wait; fetchTime = 0;
                    return true;
                }
                WalkToward(target);
                return true;
            case Fetch.Wait:
                if (fetchTime > FetchStreak.WaitSeconds) { fetchStreak.Break(); StopToyFetch(drop: false); PlayEnded(); return false; } // No more throws (bond M3).
                if (pet.Action != PetAction.Sitting) HoldStill(PetAction.Sitting, pet.Facing); // Sits looking up, waiting.
                return true;
        }
        return false;
    }

    private void PickUpBall(DesktopThing ball)
    {
        ball.Carried = true;
        ball.Glide = null;
        ball.Motion.VX = ball.Motion.VY = 0; ball.Motion.Height = 0;
        ball.Window?.Hide();
        fetch = Fetch.Carry; fetchTime = 0;
        PlayPetSound("ball_tap");
    }

    // The ball falls from the cat's mouth where it is drawn, in the cat's monitor's DIPs (as after the game).
    private void DropCarriedBall()
    {
        if (FetchBall is not { Carried: true } ball) return;
        ball.Carried = false;
        ball.Monitor = CurrentMonitor().DeviceName;
        ball.Scale = FindFarmMonitor(ball.Monitor).Scale;
        RefreshToyPlace(ball);
        var motion = ball.Motion;
        motion.X = Math.Clamp(MouthX, ball.Area.Left + ball.HalfWidth, Math.Max(ball.Area.Left + ball.HalfWidth, ball.Area.Right - ball.HalfWidth));
        double mouth = pet.Y + settings.Height - FetchMouthHeight;
        motion.Height = Math.Clamp(ball.Area.Bottom - DesktopToyRules.BallHalfWidth - mouth, 0, ToyCeiling(ball));
        motion.VX = pet.Facing * 30; motion.VY = 0;
        ball.Placed = false;
        ball.Unsaved = true; // Saved once it lies still.
        // Shown now, not next frame (a hidden ball would end the wait); a fullscreen app or a battle hides it again.
        if (ToysMayShow && ball.Window != null) { PositionDesktopThing(ball); ball.Window.Show(); }
        PlayPetSound("ball_tap");
    }

    // drop: a carried ball falls from the mouth; otherwise it was put away with the basket.
    private void StopToyFetch(bool drop)
    {
        if (fetch == Fetch.None) return;
        if (drop) DropCarriedBall();
        else if (FetchBall is { Carried: true } ball) ball.Carried = false;
        if (pet.Action == PetAction.Walking && !dragging) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
        fetch = Fetch.None;
        fetchInvite = fetchLookOnly = false;
        fetchTime = 0;
    }
}
