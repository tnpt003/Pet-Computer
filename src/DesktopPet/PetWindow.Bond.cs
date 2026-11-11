using System;
using System.Linq;

namespace DesktopPet;

// Bond milestones (docs/LIVING-CORNER.md → M3). Python sends the highest milestone reached (1–5) and,
// once, a milestone just reached; C# opens one behaviour per milestone and owns its timing and walking:
// a glance hello (Familiar) or coming to meet the user (Very close) when they come back after ten minutes
// away, grooming and a stretch after a game the user started ends by itself (Close), and a sleep it picks
// slept snug against a thing nearby (Soulmates). All are the cat's own doing: Quiet, Focus, a fullscreen
// app, the battle, Pause, being busy or another feature stop them; a click, a drag or an emotion that owns
// the cat ends them. Without Python nothing is open (bondLevel 0).
public sealed partial class PetWindow
{
    private enum BondAct { None, Glance, GreetWalk, GreetWave, Groom, Stretch, RestWalk, RestSit }
    private BondAct bondAct;
    private int bondLevel;
    private double bondTime, bondUpWait = -1, heartsTime = -1, greetWait = -1, afterPlayWait = -1, awayPeak;
    private double greetReadyAt = double.NegativeInfinity, afterPlayReadyAt = double.NegativeInfinity;
    private bool chaseWasResting;
    private int bondFacing = 1;

    public int BondLevel => bondLevel;
    public string BondActPhase => bondAct.ToString();
    public double HeartsTime => heartsTime;
    public bool GreetPending => greetWait > 0;
    public bool AfterPlayPending => afterPlayWait > 0;

    // From each decision: the milestone (dropped values leave it as it was), and a new one to celebrate.
    private void ApplyBondDecision(AI.AiDecision decision)
    {
        if (decision.BondLevel is int level && PetBondRules.Valid(level)) bondLevel = level;
        if (decision.BondUp is int up && PetBondRules.Valid(up)) { bondLevel = Math.Max(bondLevel, up); bondUpWait = PetBondRules.BondUpWaitSeconds; }
    }

    // Python off, failing or just stopped: nothing is open until it says so again.
    private void ForgetBond()
    {
        bondLevel = 0;
        greetWait = afterPlayWait = bondUpWait = -1;
        StopBondAct();
        ForgetFavorite(); // Nor a favourite toy (PetWindow.FavoriteToy.cs, M4).
    }

    // From TickContext, once a second: how long the user has been away, and a greeting when they come back.
    private void WatchReturnForBond(bool cameBack)
    {
        if (context.Away) awayPeak = Math.Max(awayPeak, context.IdleSeconds);
        if (!cameBack) return;
        if (awayPeak >= PetBondRules.GreetAwaySeconds && bondLevel >= PetBondRules.Familiar && clock.Elapsed.TotalSeconds >= greetReadyAt)
            greetWait = PetBondRules.GreetWaitSeconds;
        awayPeak = 0;
    }

    // A game the user started ended by itself (the ball game's time up, no more throws, no more passes,
    // the cat resting from the wand and the mouse): the after-play routine may follow.
    private void PlayEnded()
    {
        if (bondLevel >= PetBondRules.Close && clock.Elapsed.TotalSeconds >= afterPlayReadyAt) afterPlayWait = PetBondRules.AfterPlayWaitSeconds;
    }

    // Every unpaused frame from TickFeatures: the waits, and the hearts of a new milestone once the cat is free.
    private void TickBondClock(double dt)
    {
        bool resting = toyChaseStreak.Resting;
        if (resting && !chaseWasResting) PlayEnded(); // The wand, the mouse or passing: rested after a good round.
        chaseWasResting = resting;
        if (greetWait > 0 && (greetWait -= dt) <= 0) greetWait = -1;
        if (afterPlayWait > 0 && (afterPlayWait -= dt) <= 0) afterPlayWait = -1;
        if (heartsTime >= 0 && (heartsTime += dt) >= PetBondRules.HeartsSeconds) heartsTime = -1;
        if (bondUpWait <= 0) return;
        bondUpWait -= dt;
        if (PetActionRules.IsSettled(pet) && !dragging && !BattleActive && !context.Fullscreen) { heartsTime = 0; bondUpWait = -1; }
        else if (bondUpWait <= 0) bondUpWait = -1; // Only in the diary then.
    }

    private bool BondFree => pet.Action is PetAction.Idle or PetAction.Sitting or PetAction.Looking or PetAction.Walking && pet.Grounded;

    // From TickFeatures with the other trips. True while it owns the cat's movement this frame.
    private bool TickBondAct(double dt)
    {
        double now = clock.Elapsed.TotalSeconds;
        if (bondAct == BondAct.None)
        {
            if (greetWait > 0 && BondFree && MayAct(AutoKind.BondAct))
            {
                greetWait = -1;
                greetReadyAt = now + PetBondRules.GreetGapSeconds;
                bondAct = bondLevel >= PetBondRules.VeryClose && pet.LedgeId == 0 ? BondAct.GreetWalk : BondAct.Glance;
                bondTime = 0;
                System.Diagnostics.Trace.WriteLine($"Bond {bondLevel}: the cat greets the user back ({bondAct}).");
            }
            else if (afterPlayWait > 0 && BondFree && behavior.SelfCareReady && MayAct(AutoKind.BondAct))
            {
                afterPlayWait = -1;
                afterPlayReadyAt = now + PetBondRules.AfterPlayGapSeconds;
                if (!behavior.StartManual(pet, PetAction.Grooming)) return false;
                bondAct = BondAct.Groom;
                bondTime = 0;
                System.Diagnostics.Trace.WriteLine($"Bond {bondLevel}: grooming after play.");
                return true;
            }
            else return false;
        }
        if (!CanPlay) { StopBondAct(); return false; }
        bondTime += dt;
        double cursor = CursorPosition().X;
        int toward = cursor < CatCenter ? -1 : 1;
        switch (bondAct)
        {
            case BondAct.Glance: // Head turned to the user, a little tilt.
                HoldStill(PetAction.Looking, toward);
                pet.ReactTilt = 6 * toward * Math.Sin(Math.PI * Math.Min(1, bondTime / PetBondRules.GlanceSeconds));
                if (bondTime < PetBondRules.GlanceSeconds) return true;
                StopBondAct();
                return false;
            case BondAct.GreetWalk: // To the user along the floor, then a wave.
                if (Math.Abs(cursor - CatCenter) > PetBondRules.GreetNear && bondTime < PetBondRules.GreetWalkSeconds)
                {
                    WalkToward(Math.Clamp(cursor - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width)));
                    return true;
                }
                HoldStill(PetAction.Idle, toward);
                if (!behavior.StartManual(pet, PetAction.SeekingAttention)) { StopBondAct(); return false; }
                pet.Facing = toward;
                bondAct = BondAct.GreetWave;
                bondTime = 0;
                return true;
            case BondAct.GreetWave:
                if (pet.Action == PetAction.SeekingAttention) return true;
                StopBondAct();
                return false;
            case BondAct.Groom:
                if (pet.Action == PetAction.Grooming) return true;
                if (bondTime >= PetSettings.GroomingDuration - 0.1 && pet.Action == PetAction.Idle && behavior.StartManual(pet, PetAction.Stretching))
                { bondAct = BondAct.Stretch; bondTime = 0; return true; }
                StopBondAct();
                return false;
            case BondAct.Stretch:
                if (pet.Action == PetAction.Stretching) return true;
                if (bondTime < PetSettings.StretchingDuration - 0.1 || pet.Action != PetAction.Idle) { StopBondAct(); return false; }
                bondAct = RestSpotHere() is not null ? BondAct.RestWalk : BondAct.RestSit;
                bondTime = 0;
                return true;
            case BondAct.RestWalk: // Back to the resting spot the user set, on this floor.
                if (RestSpotHere() is not double spot || bondTime > PetBondRules.RestWalkSeconds) { bondAct = BondAct.RestSit; bondTime = 0; return true; }
                if (Math.Abs(spot - pet.X) >= 6) { WalkToward(spot); return true; }
                bondAct = BondAct.RestSit; bondTime = 0;
                return true;
            case BondAct.RestSit:
                HoldStill(PetAction.Sitting, pet.Facing);
                if (bondTime < PetBondRules.RestSitSeconds) return true;
                StopBondAct();
                return false;
        }
        return false;
    }

    // The resting spot's left edge when it is on the floor of the cat's monitor (the cat on that floor too).
    private double? RestSpotHere()
    {
        if (preferences.RestX is not double x || preferences.RestY is not double y || preferences.RestMonitor != CurrentMonitor().DeviceName
            || pet.LedgeId != 0 || Math.Abs(y - pet.Y) > 8 || x < bounds.Left || x > bounds.Right - settings.Width) return null;
        return x;
    }

    private void StopBondAct()
    {
        if (bondAct == BondAct.None) return;
        var was = bondAct;
        bondAct = BondAct.None;
        pet.ReactTilt = 0;
        bool own = was switch
        {
            BondAct.Glance => pet.Action == PetAction.Looking,
            BondAct.GreetWalk or BondAct.RestWalk => pet.Action == PetAction.Walking,
            BondAct.RestSit => pet.Action == PetAction.Sitting,
            _ => false
        };
        if (own && !dragging) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
    }

    private bool BondOwnsMovement => bondAct is BondAct.Glance or BondAct.GreetWalk or BondAct.RestWalk or BondAct.RestSit;

    // Soulmates: a sleep the cat picks itself is slept snug against the nearest thing within reach
    // (a toy or living-corner thing out on its floor, or the keepsake on display). An ordinary sleep.
    private bool napLean;
    private double leanX;
    private DesktopThing? leanThing;

    private bool LeanTargetUsable => leanThing is { } thing
        ? desktopToys.Contains(thing) && ToysMayShow && thing.Window?.IsVisible == true && !thing.Held && !thing.Press && !thing.Carried && !thing.InGame
            && thing.Motion.AtRest && thing.Glide == null && thing.Monitor == CurrentMonitor().DeviceName
        : TrophiesVisible && KeepsakeWithCat && KeepsakeOnFloor && keepsake.Resting && !keepsakeHeld && keepsakeHop == KeepsakeHop.None;

    private bool GoLeanNap()
    {
        if (bondLevel < PetBondRules.Soulmate || !MayAct(AutoKind.CushionNap) || !pet.Grounded || pet.LedgeId != 0) return false;
        string monitor = CurrentMonitor().DeviceName;
        var spots = desktopToys.Where(t => ToysMayShow && t.Window?.IsVisible == true && !t.Held && !t.Press && !t.Carried && !t.InGame && t.Motion.AtRest
                && t.Glide == null && t.Monitor == monitor && !(t.Id == DesktopToyRules.Box && BoxOccupied))
            .Select(t => ((DesktopThing?)t, t.Motion.X, t.HalfWidth)).ToList();
        if (TrophiesVisible && KeepsakeWithCat && KeepsakeOnFloor && DisplayedKeepsake?.Kind != KeepsakeSpec.Cushion)
            spots.Add((null, KeepsakeCenter, KeepsakeArtWidth / 2));
        double best = double.MaxValue;
        foreach (var (thing, centre, half) in spots)
        {
            double distance = Math.Abs(centre - CatCenter);
            if (distance > PetBondRules.LeanReach || distance >= best) continue;
            int side = CatCenter < centre ? -1 : 1;
            foreach (int s in new[] { side, -side })
            {
                if (CatCanStand(centre + s * (half + PetBondRules.LeanGap) - settings.Width / 2) is not double left) continue;
                best = distance; leanThing = thing; leanX = left; bondFacing = -s;
                break;
            }
        }
        if (best == double.MaxValue) { leanThing = null; return false; }
        cushionNap = CushionNap.Walk;
        napBox = false; napLean = true;
        napTime = 0;
        System.Diagnostics.Trace.WriteLine($"Bond {bondLevel}: the cat goes to nap snug against the {leanThing?.Id ?? "keepsake"}.");
        return true;
    }

    // Arrived (from TickCushionNap): an ordinary sleep right there, facing the thing.
    private void StartLeanSleep()
    {
        napLean = false; leanThing = null;
        cushionNap = CushionNap.None;
        HoldStill(PetAction.Idle, bondFacing);
        if (behavior.StartManual(pet, PetAction.Sleeping)) System.Diagnostics.Trace.WriteLine("The cat naps snug against its things.");
    }
}
