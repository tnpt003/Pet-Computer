using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// Mini-games (docs/MINI-GAMES.md → M2): hide-and-seek. Pure rules: the numbers, where the three hiding
// places go on the floor, and what a game adds to the preferences. The window (PetWindow.HideSeek.cs)
// draws and times it. Hiding places are the cardboard box when it is out on that floor, and bushes the
// app draws for the game only (the user chose 12/11: the plant pot and the scratching post are too narrow
// to hide the cat's head).
public static class HideSeekRules
{
    public const int Spots = 3, Guesses = 3;
    // Spot centres at least this far apart; bushes are about 76 wide and 56 tall.
    public const double MinApart = 150, BushHalfWidth = 38, BushHeight = 56, Gap = 8, Reach = 700;
    // Counting before the search, the search's time limit, how often the cat gives itself away and for how long.
    public const double CountSeconds = 3, VanishAt = 0.3, SearchSeconds = 60, SignalMin = 3, SignalMax = 5, SignalSeconds = 0.6,
        DecoyMin = 5, DecoyMax = 8, WrongSeconds = 1.0, PopSeconds = 0.35, OutSeconds = 1.5, GrowSeconds = 0.5, FadeSeconds = 0.4;
    // The cat sinks behind its cover until its ear tips (about 10 DIPs below the top of its 112-DIP drawing)
    // are just under the cover's top; a signal lifts them this much above it.
    public const double Drawing = 112, EarTip = 10, Peek = 22;

    public static double Sink(double coverHeight) => Math.Max(0, Drawing - EarTip - coverHeight + 4);

    // Three spot centres on a floor from left to right (with the box's centre among them when it is given),
    // clear of what is taken (farm land, the cart, other toys); null when three do not fit.
    public static List<double>? Place(double catCentre, double floorLeft, double floorRight, IReadOnlyList<(double Left, double Right)> taken,
        double? box, Random random)
    {
        double left = Math.Max(floorLeft + BushHalfWidth + 4, catCentre - Reach), right = Math.Min(floorRight - BushHalfWidth - 4, catCentre + Reach);
        var spots = new List<double>();
        if (box is double b && double.IsFinite(b)) spots.Add(b);
        bool Fits(double x) => taken.All(t => x + BushHalfWidth <= t.Left - Gap || x - BushHalfWidth >= t.Right + Gap)
            && spots.All(s => Math.Abs(s - x) >= MinApart);
        for (int tries = 0; tries < 400 && spots.Count < Spots && right > left; tries++)
        {
            double x = left + random.NextDouble() * (right - left);
            if (Fits(x)) spots.Add(x);
        }
        // A last sweep in steps, so a crowded floor still finds room when there is some.
        for (double x = left; spots.Count < Spots && x <= right; x += 10)
            if (Fits(x)) spots.Add(x);
        if (spots.Count < Spots) return null;
        spots.Sort();
        return spots;
    }

    public static double NextSignal(Random random) => SignalMin + random.NextDouble() * (SignalMax - SignalMin);
    public static double NextDecoy(Random random) => DecoyMin + random.NextDouble() * (DecoyMax - DecoyMin);

    // A game where the user found the cat (firstTry: with the first guess).
    public static void RecordFound(PetPreferences preferences, bool firstTry)
    {
        preferences.HideSeekFound = Math.Min(999999, preferences.HideSeekFound + 1);
        if (firstTry) preferences.HideSeekFirstTry = Math.Min(999999, preferences.HideSeekFirstTry + 1);
    }
}
