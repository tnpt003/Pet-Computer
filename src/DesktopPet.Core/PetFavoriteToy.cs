using System;

namespace DesktopPet;

// The favourite toy (docs/LIVING-CORNER.md → M4). Python keeps the scores of the rounds the user played
// with the yarn ball, the feather wand and the wind-up mouse and sends the favourite; C# opens a visit to it,
// an invitation with it and a choice between two toys lying close together. Without Python: none.
public static class FavoriteToyRules
{
    // A visit every 10–15 minutes: a sniff or a paw at it, then sitting by it a while. The toy never moves.
    public const double VisitMin = 600, VisitMax = 900, GreetSeconds = 1.2, SitSeconds = 5;
    // An invitation with the wand or the mouse (on the shared 8–15 minute invitation timer): sitting by it,
    // looking at the user and patting it now and then, for 20 seconds.
    public const double InviteSeconds = 20, PatEvery = 6, PatAt = 0.5;
    // Two toys within 250 DIPs of each other: it stops between them and looks at each for about a second.
    public const double ChoiceReach = 250, LookSeconds = 1, WalkSeconds = 12, Reach = 30;

    public static double NextVisit(double random) => VisitMin + Math.Clamp(random, 0, 1) * (VisitMax - VisitMin);

    // Only the three toys the user plays with count (not the laser, the box or the living-corner things).
    public static bool IsPlayToy(string? id) => id is DesktopToyRules.Ball or DesktopToyRules.Wand or DesktopToyRules.Mouse;
}
