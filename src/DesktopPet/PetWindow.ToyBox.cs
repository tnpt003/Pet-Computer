using System;

namespace DesktopPet;

// Napping in the cardboard box (docs/TOY-PLAY.md → M2). The same naps as on the slime cushion
// (PetWindow.KeepsakePlay.cs: the walk, the timers, what blocks them) and an ordinary sleep once there:
// its timer, wake cooldown, Pause and every click stay the sleep's own. What is new is only drawn:
// a little hop in, the box's front panel over the cat (drawn in the cat's own window, so clicks still
// reach the cat), and a little hop out after a wake-up in the box.
public sealed partial class PetWindow
{
    // The nap under way (cushionNap) is in the box, not on the cushion.
    private bool napBox;
    // The hops in and out (seconds since they began; negative: none), and how far up they draw the cat.
    private double boxHopIn = -1, boxHopOut = -1, boxLift;
    public const double BoxHopInSeconds = 0.35, BoxHopOutSeconds = 0.3, BoxHopHeight = 32, BoxFloor = 3;

    private DesktopThing? DesktopBox => DesktopToy(DesktopToyRules.Box);
    private double BoxCenter => DesktopBox?.Motion.X ?? 0;

    public bool NappingInBox => cushionNap == CushionNap.Asleep && napBox;
    public bool GoingToBox => cushionNap == CushionNap.Walk && napBox;
    // The cat is in it (asleep, waking, peeking out or hopping out): the box cannot be picked up or put away from the basket.
    private bool BoxOccupied => NappingInBox || BoxPeeking || boxHopOut >= 0;
    public bool BoxFrontShown => !dragging && (NappingInBox || BoxPeeking || (boxHopOut >= 0 && boxHopOut < BoxHopOutSeconds * 0.5));
    public double BoxLift => boxLift;

    // The box is out, shown and still on the floor of the cat's monitor, nobody holds it, and the cat
    // stands on that floor (not on a window) with room to stand where the box is.
    private bool BoxShownWithCat => DesktopBox is { } box && ToysMayShow && box.Window?.IsVisible == true && !box.Held && !box.Press
        && box.Monitor == CurrentMonitor().DeviceName;
    private bool BoxReady => BoxShownWithCat && DesktopBox is { } box && box.Glide == null && box.Motion.AtRest
        && pet.Grounded && pet.LedgeId == 0 && CatCanStand(box.Motion.X - settings.Width / 2) != null;
    // Close enough for "Do now → Sleep in the box".
    public bool BoxNear => BoxReady && Math.Abs(BoxCenter - CatCenter) <= CushionNearDistance;
    // For "Do now": the box is out (the item shows; it may still be greyed out).
    public bool BoxOut => DesktopBox != null && ToysMayShow;

    // For checks (and GoNap): go and nap in the box now. False when it may not.
    public bool GoNapInBox()
    {
        if (!MayAct(AutoKind.CushionNap) || !BoxReady || Math.Abs(BoxCenter - CatCenter) > CushionReach) return false;
        cushionNap = CushionNap.Walk;
        napBox = true; napLean = false;
        napTime = 0;
        return true;
    }

    // "Do now → Sleep in the box": only near it; like every "Do now" also in Quiet and during the wake
    // cooldown (the sleep rules themselves stay). Not while the pet cannot act (asleep, held, flying).
    public bool SleepInBoxNow()
    {
        if (paused || closed || dragging || BattleOwnsPet || !BoxNear || !PetBehavior.CanStartManual(pet, PetAction.Sleeping)) return false;
        revision++; // Like DoNow: AI replies chosen before this request are dropped.
        CancelFeatureActivity();
        cushionNap = CushionNap.Walk;
        napBox = true; napLean = false;
        userAsked |= UserAsked.Nap;
        napTime = 0;
        return true;
    }

    // Every frame (TickDesktopToys), frozen by Pause. The nap ends with the sleep (a wake-up in the box
    // hops out of it), when the cat is lifted out, or when the box is gone from under it (tidied away,
    // hidden for a fullscreen app or the battle, its monitor unplugged): then the cat sleeps on where it is.
    private void TickBoxNap(double dt)
    {
        if (paused) return;
        if (NappingInBox)
        {
            bool under = BoxShownWithCat && Math.Abs(BoxCenter - CatCenter) < 24;
            bool asleep = pet.Action is PetAction.Sleeping or PetAction.Waking;
            if (!asleep || !under || !pet.Grounded || dragging)
            {
                bool hopOut = !asleep && under && pet.Grounded && !dragging && !PetActionRules.IsInFlight(pet.Action);
                cushionNap = CushionNap.None;
                userAsked &= ~UserAsked.Nap;
                boxHopIn = -1;
                boxHopOut = hopOut ? 0 : -1;
            }
        }
        if (boxHopIn >= 0 && (boxHopIn += dt) >= BoxHopInSeconds) boxHopIn = -1;
        if (boxHopOut >= 0 && ((boxHopOut += dt) >= BoxHopOutSeconds || dragging || !pet.Grounded)) boxHopOut = -1;
        if (NappingInBox || BoxPeeking) boxLift = BoxFloor + (boxHopIn >= 0 ? Math.Sin(Math.PI * boxHopIn / BoxHopInSeconds) * BoxHopHeight : 0);
        else if (boxHopOut >= 0) boxLift = (1 - boxHopOut / BoxHopOutSeconds) * BoxFloor + Math.Sin(Math.PI * boxHopOut / BoxHopOutSeconds) * BoxHopHeight;
        else boxLift = 0;
    }

    // Living corner M2 (docs/LIVING-CORNER.md): hiding in the box awake, head over the edge, for 8–15
    // seconds every 8–12 minutes (blocked like a nap in the box). The same hops and front panel as the nap,
    // so every click still reaches the cat. It owns the cat's movement while it lasts: a click, a drag or an
    // emotion that owns the cat ends it (CancelFeatureActivity), with the little hop out unless lifted.
    // Its time for a nap comes while in the box: an ordinary sleep in the box. The wind-up mouse or the
    // feather the user plays with going by: a short crouch, then a pounce out and the M4 chase.
    private enum BoxPeek { None, Walk, In, Stalk }
    private BoxPeek boxPeek;
    private double peekVisit = BoxPeekRules.VisitMin, peekTime, peekLength;
    private DesktopThing? peekLure;

    public string BoxPeekPhase => boxPeek.ToString();
    private bool BoxPeeking => boxPeek is BoxPeek.In or BoxPeek.Stalk;

    // For checks (and the timer): go and hide in the box now. False when it may not.
    public bool GoPeekInBox()
    {
        if (boxPeek != BoxPeek.None || cushionNap != CushionNap.None || !MayAct(AutoKind.BoxPeek) || !BoxReady
            || Math.Abs(BoxCenter - CatCenter) > CushionReach || pet.Action is not (PetAction.Idle or PetAction.Sitting or PetAction.Looking or PetAction.Walking)) return false;
        boxPeek = BoxPeek.Walk;
        peekTime = 0;
        peekLength = BoxPeekRules.PeekLength(featureRandom.NextDouble());
        System.Diagnostics.Trace.WriteLine("The cat goes to hide in the cardboard box.");
        return true;
    }

    // The toy the user plays with, close enough to the box to pounce at; null: none.
    private DesktopThing? PeekLure()
    {
        if (ChaseLure() is not { } lure || !ChaseToyWithCat(lure)) return null;
        var target = ChaseTarget(lure);
        return Math.Abs(target.X - BoxCenter) <= BoxPeekRules.LureReach && target.Y >= pet.Y - LaserReachAbove ? lure : null;
    }

    // From TickFeatures, next to the scratching post. True while it owns the cat.
    private bool TickBoxPeek(double dt)
    {
        peekVisit -= dt;
        if (boxPeek == BoxPeek.None)
        {
            if (peekVisit > 0) return false;
            peekVisit = BoxPeekRules.NextVisit(featureRandom.NextDouble());
            if (!GoPeekInBox()) return false;
        }
        peekTime += dt;
        if (!CanPlay) { StopBoxPeek(); return false; }
        if (boxPeek == BoxPeek.Walk)
        {
            if (!BoxReady || peekTime > NapWalkSeconds) { StopBoxPeek(); return false; }
            double target = Math.Clamp(BoxCenter - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
            if (Math.Abs(target - pet.X) >= 4) { WalkToward(target); return true; }
            pet.X = target;
            HoldStill(PetAction.Looking, pet.Facing);
            boxPeek = BoxPeek.In;
            peekTime = 0;
            boxHopIn = 0; // The nap's little hop in (drawn only).
            System.Diagnostics.Trace.WriteLine("The cat peeks out of the cardboard box.");
            return true;
        }
        // The box gone from under it (tidied away, hidden, its monitor unplugged): out, no hop.
        if (!BoxShownWithCat || Math.Abs(BoxCenter - CatCenter) >= 24 || !pet.Grounded) { StopBoxPeek(hop: false); return false; }
        if (boxPeek == BoxPeek.Stalk)
        {
            if (peekLure is not { } lure || PeekLure() != lure) { boxPeek = BoxPeek.In; pet.PlayCrouch = 0; return true; }
            pet.PlayCrouch = Math.Min(1, peekTime / 0.2);
            HoldStill(PetAction.Sitting, ChaseTarget(lure).X < CatCenter ? -1 : 1);
            if (peekTime < BoxPeekRules.StalkSeconds) return true;
            StopBoxPeek(hop: false);
            return PounceFromBox(lure); // Out of the box in a pounce, then the M4 chase.
        }
        // Peeking out: looking one way, then the other.
        if (PeekLure() is { } near)
        {
            int toward = ChaseTarget(near).X < CatCenter ? -1 : 1;
            if (!toyChaseStreak.Resting) { boxPeek = BoxPeek.Stalk; peekLure = near; peekTime = 0; HoldStill(PetAction.Sitting, toward); return true; }
            HoldStill(PetAction.Looking, toward); // Resting from M4: it only turns its head after it.
            return true;
        }
        HoldStill(PetAction.Looking, (int)(peekTime / 2.6) % 2 == 0 ? pet.Facing : -pet.Facing);
        // Its time for a nap (the cushion's timer, or nearly sleepy from being left alone): it sleeps in the box.
        napVisit -= dt;
        bool sleepy = pet.Emotions.SecondsWithoutAttention >= PetEmotions.SleepyAfter - NapBeforeSleepy && pet.Emotions.SecondsWithoutAttention < PetEmotions.SleepyAfter;
        if ((napVisit <= 0 || sleepy) && pet.Emotions.WakeCooldownRemaining <= 0)
        {
            napVisit = NapEvery + featureRandom.NextDouble() * 180;
            boxPeek = BoxPeek.None;
            if (behavior.StartManual(pet, PetAction.Sleeping))
            {
                cushionNap = CushionNap.Asleep; napBox = true; // The ordinary nap in the box (TickBoxNap), lift already there.
                System.Diagnostics.Trace.WriteLine("The cat falls asleep in the cardboard box.");
                return true;
            }
            boxHopOut = 0;
            return false;
        }
        if (peekTime < peekLength) return true;
        StopBoxPeek();
        return false;
    }

    // Out of the box: with the little hop, unless lifted, flying or the box is gone.
    private void StopBoxPeek(bool hop = true)
    {
        if (boxPeek == BoxPeek.None) return;
        bool inside = BoxPeeking;
        boxPeek = BoxPeek.None;
        peekLure = null;
        peekVisit = Math.Max(peekVisit, BoxPeekRules.VisitMin);
        pet.PlayCrouch = 0;
        if (inside)
        {
            boxHopIn = -1;
            if (hop && !dragging && pet.Grounded && BoxShownWithCat) boxHopOut = 0;
        }
        if (!dragging && pet.Action is PetAction.Walking or PetAction.Looking or PetAction.Sitting) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
    }
}
