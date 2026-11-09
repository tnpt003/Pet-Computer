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

// The rules of the toys left out on the desktop; the windows are in PetWindow.Toys.cs.
public static class DesktopToyRules
{
    public const string Ball = "ball";
    public const int MaxOut = 3;
    public const double BallHalfWidth = 12, BasketHalfWidth = 34, BasketHeight = 46, Gap = 4, GlideSpeed = 220;
    public static readonly IReadOnlyList<string> All = new[] { Ball };

    public static bool CanTake(int outCount) => outCount < MaxOut;

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
