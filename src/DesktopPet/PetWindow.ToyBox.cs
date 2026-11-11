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
    // The cat is in it (asleep, waking, or hopping out): the box cannot be picked up or put away from the basket.
    private bool BoxOccupied => NappingInBox || boxHopOut >= 0;
    public bool BoxFrontShown => !dragging && (NappingInBox || (boxHopOut >= 0 && boxHopOut < BoxHopOutSeconds * 0.5));
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
        napBox = true;
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
        napBox = true;
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
        if (NappingInBox) boxLift = BoxFloor + (boxHopIn >= 0 ? Math.Sin(Math.PI * boxHopIn / BoxHopInSeconds) * BoxHopHeight : 0);
        else if (boxHopOut >= 0) boxLift = (1 - boxHopOut / BoxHopOutSeconds) * BoxFloor + Math.Sin(Math.PI * boxHopOut / BoxHopOutSeconds) * BoxHopHeight;
        else boxLift = 0;
    }
}
