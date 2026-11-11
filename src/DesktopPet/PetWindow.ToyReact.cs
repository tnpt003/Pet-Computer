using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// Short reactions (docs/TOY-PLAY.md → M5): a miss, a first sniff of a new toy, a new accessory.
// Only actions with poses the cat already has (Looking, Sitting, Grooming) and a head tilt; never an
// emotion, no mood, no IPC. Never asleep, waking, held or in flight: CanPlay ends them, so a click,
// a drag or an emotion that owns the cat wins at once. Pause freezes them with the frame loop.
public sealed partial class PetWindow
{
    private enum React { None, Miss, SniffWalk, Sniff, Accessory }
    private React react;
    private double reactTime, reactLength, reactMissGap, reactSniffCheck;
    private int reactFacing = 1;
    private bool reactPawed;
    private DesktopThing? sniffToy;

    public string ReactPhase => react.ToString();
    public string? SniffToyId => sniffToy?.Id;
    public IReadOnlyList<string> ToysSniffed => preferences.ToysSniffed ?? (IReadOnlyList<string>)Array.Empty<string>();

    private bool Sniffed(string id) => preferences.ToysSniffed?.Contains(id) == true;

    private void MarkSniffed(string id)
    {
        if (Sniffed(id)) return;
        (preferences.ToysSniffed ??= new List<string>()).Add(id);
        SavePreferences();
    }

    private void StartReaction(React kind, double length, int facing)
    {
        react = kind;
        reactTime = 0; reactLength = length; reactFacing = facing; reactPawed = false;
        pet.PlayCrouch = 0; pet.ReactTilt = pet.ReactDip = 0;
        HoldStill(kind == React.Sniff ? PetAction.Sitting : PetAction.Looking, facing);
    }

    // The user's first game with a toy the cat never sniffed: a quick sniff, then the game (TryStartPass, TryStartChase).
    private bool SniffFirst(DesktopThing toy)
    {
        if (Sniffed(toy.Id)) return false;
        MarkSniffed(toy.Id);
        sniffToy = toy;
        StartReaction(React.Sniff, ToyReactRules.QuickSniff, toy.Motion.X < CatCenter ? -1 : 1);
        return true;
    }

    // A pounce that caught nothing, or a pass that rolled by: a puzzled second, at most once every ten.
    private void MissReaction()
    {
        if (reactMissGap > 0 || react != React.None || !CanPlay || !CatOnFloor) return;
        reactMissGap = ToyReactRules.MissGap;
        StartReaction(React.Miss, ToyReactRules.MissSeconds, pet.Facing);
    }

    // Saved in Customize with another accessory on: it looks itself over, shakes its head and fixes the hat.
    private void ReactToAccessory()
    {
        if (react != React.None || !MayAct(AutoKind.ToyReact) || !CatOnFloor
            || pet.Action is not (PetAction.Idle or PetAction.Walking or PetAction.Sitting or PetAction.Looking)) return;
        StartReaction(React.Accessory, ToyReactRules.AccessorySeconds, pet.Facing);
    }

    // For checks: the cat goes to sniff its next new toy now, as its own check would. False when it may not.
    public bool SniffNewToy()
    {
        if (react != React.None || !MayAct(AutoKind.ToySniff) || !CatOnFloor
            || pet.Action is not (PetAction.Idle or PetAction.Sitting or PetAction.Walking or PetAction.Looking)) return false;
        string monitor = CurrentMonitor().DeviceName;
        var toy = desktopToys.FirstOrDefault(t => !Sniffed(t.Id) && ToysMayShow && t.Window?.IsVisible == true && t.Monitor == monitor
            && !t.Held && !t.Press && !t.Carried && !t.InGame && t.Motion.AtRest && !(t.Id == DesktopToyRules.Box && BoxOccupied));
        if (toy == null) return false;
        if (WeighNewToys(toy)) return true; // Two new toys close together: it looks at both first (PetWindow.FavoriteToy.cs).
        StartSniffWalk(toy);
        return true;
    }

    private void StartSniffWalk(DesktopThing toy)
    {
        sniffToy = toy;
        StartReaction(React.SniffWalk, ToyReactRules.SniffWalkSeconds, toy.Motion.X < CatCenter ? -1 : 1);
        System.Diagnostics.Trace.WriteLine($"The cat goes to sniff the new {toy.Id}.");
    }

    // From TickFeatures, before the chase and the pass, so a miss holds them a moment. True while it owns the cat.
    private bool TickToyReact(double dt)
    {
        reactMissGap = Math.Max(0, reactMissGap - dt);
        if (react == React.None)
        {
            if ((reactSniffCheck -= dt) > 0 || !CanPlay) return false;
            reactSniffCheck = 2;
            return desktopToys.Any(t => !Sniffed(t.Id)) && SniffNewToy();
        }
        if (!CanPlay) { StopToyReact(); return false; }
        reactTime += dt;
        double t = reactTime;
        switch (react)
        {
            case React.Miss: // Head on one side, a glance back over the shoulder.
                pet.ReactTilt = 11 * Math.Sin(Math.PI * Math.Clamp(t / reactLength, 0, 1));
                HoldStill(PetAction.Looking, t < reactLength * 0.55 ? reactFacing : -reactFacing);
                break;
            case React.SniffWalk:
                if (sniffToy is not { } toy || !desktopToys.Contains(toy) || toy.Held || toy.Window?.IsVisible != true
                    || Blocking(PetAutonomy.BlockedBy(AutoKind.ToySniff) & ~AutoBlock.Feature) != AutoBlock.None || t > reactLength)
                { StopToyReact(); return false; }
                double gap = toy.Motion.X - CatCenter;
                if (Math.Abs(gap) <= toy.HalfWidth + ToyReactRules.SniffReach)
                {
                    MarkSniffed(toy.Id);
                    StartReaction(React.Sniff, ToyReactRules.SniffSeconds, gap < 0 ? -1 : 1);
                    return true;
                }
                double spot = Math.Clamp(toy.Motion.X - Math.Sign(gap) * (toy.HalfWidth + ToyReactRules.SniffReach - 8) - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
                WalkToward(spot);
                pet.VX *= 0.6; // Slowly: something new.
                return true;
            case React.Sniff: // Nose down at it, then one paw tries it.
                pet.Action = PetAction.Sitting; pet.VX = pet.SmoothedWalkVelocity = 0; pet.Facing = reactFacing;
                pet.PlayCrouch = Math.Min(1, t / 0.2) * Math.Min(1, (reactLength - t) / 0.15 + 0.2);
                pet.ReactDip = 9 * pet.PlayCrouch; pet.ReactTilt = 8 * reactFacing * pet.PlayCrouch; // Nose down toward it.
                if (!reactPawed && t >= reactLength - 0.35) { reactPawed = true; pet.BatRemaining = 0.3; }
                break;
            case React.Accessory: // A look and a head shake, then a paw over the ears.
                if (t < ToyReactRules.AccessoryLook)
                {
                    pet.ReactTilt = 9 * Math.Sin(t * 18) * (1 - t / ToyReactRules.AccessoryLook);
                    HoldStill(PetAction.Looking, reactFacing);
                }
                else
                {
                    pet.ReactTilt = 0;
                    if (pet.Action != PetAction.Grooming) { HoldStill(PetAction.Grooming, reactFacing); pet.ActionTime = 1.1; } // The face-washing middle of the grooming pose.
                }
                break;
        }
        if (t >= reactLength) { StopToyReact(); return false; }
        return true;
    }

    private void StopToyReact()
    {
        if (react == React.None) return;
        react = React.None;
        sniffToy = null;
        pet.ReactTilt = pet.ReactDip = 0; pet.PlayCrouch = 0;
        reactTime = reactLength = 0;
        // Free again: what it was doing was only the reaction's pose.
        if (!dragging && pet.Action is PetAction.Walking or PetAction.Sitting or PetAction.Looking or PetAction.Grooming) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
    }
}
