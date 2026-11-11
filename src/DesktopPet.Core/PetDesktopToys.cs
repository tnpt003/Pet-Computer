using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// A toy left out on the desktop, as remembered in preferences.json (docs/TOY-PLAY.md).
public sealed class SavedToy
{
    public string Id { get; set; } = "";
    public string? Monitor { get; set; }
    public double FractionX { get; set; } = 0.5;
}

// Fetch with the yarn ball (docs/TOY-PLAY.md → M3): what counts as a throw, and five fetches in a row
// before two minutes of only watching. The walking and carrying are in PetWindow.ToyFetch.cs.
public sealed class FetchStreak
{
    public const int MaxInARow = 5;
    public const double ThrowSpeed = 400, RestSeconds = 120, WaitSeconds = 20, WalkSeconds = 20, InviteMin = 480, InviteMax = 900;
    private int count;
    private double rest;

    public static bool IsThrow(double vx, double vy) => double.IsFinite(vx) && double.IsFinite(vy) && Math.Sqrt(vx * vx + vy * vy) > ThrowSpeed;
    // random in [0, 1): the next invitation, 8–15 minutes away.
    public static double NextInvite(double random) => InviteMin + Math.Clamp(random, 0, 1) * (InviteMax - InviteMin);

    public int InARow => count;
    public bool Resting => rest > 0;
    public double RestLeft => rest;

    // The cat sets off after a throw; false while it rests (it only watches). The fifth in a row starts the rest.
    public bool TryFetch()
    {
        if (rest > 0) return false;
        if (++count >= MaxInARow) { count = 0; rest = RestSeconds; }
        return true;
    }

    // No throw while it waited: the next fetch starts a new streak.
    public void Break() => count = 0;

    public void Step(double dt) { if (double.IsFinite(dt) && dt > 0) rest = Math.Max(0, rest - dt); }
}

// The rules of the toys left out on the desktop; the windows are in PetWindow.Toys.cs.
public static class DesktopToyRules
{
    public const string Ball = "ball", Box = "box", Wand = "wand", Mouse = "mouse";
    public const int MaxOut = 3;
    public const double BallHalfWidth = 12, BasketHalfWidth = 34, BasketHeight = 46, Gap = 4, GlideSpeed = 220;
    // The cardboard box (M2): about 100 DIPs wide, its body this tall above the floor.
    public const double BoxHalfWidth = 50, BoxHeight = 44;
    // M4: the feather wand's handle (about 90 DIPs) lying on the floor, and the wind-up mouse.
    public const double WandHalfWidth = 45, WandHeight = 10, MouseHalfWidth = 18, MouseHeight = 16;
    public static readonly IReadOnlyList<string> All = new[] { Ball, Box, Wand, Mouse };

    public static bool CanTake(int outCount) => outCount < MaxOut;

    public static double HalfWidthOf(string id) => id switch { Box => BoxHalfWidth, Wand => WandHalfWidth, Mouse => MouseHalfWidth, _ => BallHalfWidth };
    // Half its height: the ball's radius, half the box's body, half the handle, half the mouse.
    public static double HalfHeightOf(string id) => id switch { Box => BoxHeight / 2, Wand => WandHeight / 2, Mouse => MouseHeight / 2, _ => BallHalfWidth };

    // What is left of a saved list: known toys, each once, no more than fit. No basket: nothing is out.
    public static List<SavedToy>? Normalize(IEnumerable<SavedToy>? saved, bool basketShown, IReadOnlyCollection<string>? known = null)
    {
        if (!basketShown || saved == null) return null;
        var kept = saved.Where(toy => toy != null && (known ?? All).Contains(toy.Id)).GroupBy(toy => toy.Id).Select(same => same.First()).Take(MaxOut).ToList();
        foreach (var toy in kept) toy.FractionX = double.IsFinite(toy.FractionX) ? Math.Clamp(toy.FractionX, 0, 1) : 0.5;
        return kept.Count == 0 ? null : kept;
    }

    // Across the room a centre has on this monitor's floor.
    public static double Fraction(double x, double halfWidth, Area area) =>
        Math.Clamp((x - area.Left - halfWidth) / Math.Max(1, area.Right - area.Left - 2 * halfWidth), 0, 1);

    public static double FromFraction(double fraction, double halfWidth, Area area) =>
        area.Left + halfWidth + Math.Clamp(fraction, 0, 1) * Math.Max(0, area.Right - area.Left - 2 * halfWidth);

    public static bool OverBasket(double x, double height, double basketX) =>
        Math.Abs(x - basketX) <= BasketHalfWidth + 6 && height <= BasketHeight + 10;

    // The centre to move to so as not to lie on what is taken; null: it is fine here, or nowhere is free.
    public static double? FreeCentre(double x, double halfWidth, IReadOnlyList<(double Left, double Right)> taken, Area area)
    {
        double left = x - halfWidth;
        double spot = FarmLands.FreeSpot(left, 2 * halfWidth, taken, area.Left, Math.Max(area.Left, area.Right - 2 * halfWidth), Gap);
        return Math.Abs(spot - left) > 0.5 ? spot + halfWidth : null;
    }
}
