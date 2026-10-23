using System;
using System.Collections.Generic;

namespace DesktopPet;

// One top-level window as Windows reports it (DIPs), in z-order from the top. Candidate:
// an ordinary app window the pet may stand on; every visible window can cover others.
public readonly record struct WindowSnapshot(long Id, double Left, double Top, double Right, double Bottom, bool Candidate);

// Pure geometry for walking on windows: which edges are visible, where the pet stands,
// lands, climbs and jumps. PetWindow.Ledges.cs reads the real windows.
public static class PetLedges
{
    public const double MinWindowWidth = 200, MinWindowHeight = 80, MinLedgeWidth = 80;
    // A window whose bottom is this close to the floor can be climbed from the floor.
    public const double ClimbFloorGap = 100;
    // Jumps: how high (feet to ledge) and how far sideways the pet leaps.
    public const double MinJumpHeight = 60, MaxJumpHeight = 380, MaxJumpReach = 260, JumpApexMargin = 40;
    public const double MaxClimbWalk = 700;

    public static List<Ledge> Build(IReadOnlyList<WindowSnapshot> windows, Area bounds, double petWidth, double petHeight)
    {
        var result = new List<Ledge>();
        double floor = bounds.Bottom;
        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (!w.Candidate || w.Right - w.Left < MinWindowWidth || w.Bottom - w.Top < MinWindowHeight) continue;
            double top = w.Top;
            // Room for the pet above the edge, and clearly above the floor.
            if (top - petHeight < bounds.Top + 4 || top > floor - 30) continue;
            if (w.Left >= bounds.Right || w.Right <= bounds.Left) continue;
            var pieces = new List<(double L, double R)> { (Math.Max(w.Left, bounds.Left), Math.Min(w.Right, bounds.Right)) };
            for (int j = 0; j < i && pieces.Count > 0; j++)
            {
                var o = windows[j];
                if (o.Top <= top && o.Bottom > top) pieces = Subtract(pieces, o.Left, o.Right); // Covers the edge line.
            }
            bool reachesFloor = w.Bottom >= floor - ClimbFloorGap;
            double sideBottom = Math.Min(w.Bottom, floor);
            foreach (var (l, r) in pieces)
            {
                if (r - l < MinLedgeWidth) continue;
                bool climbLeft = reachesFloor && l <= w.Left + 0.5 && w.Left - PetSettings.ClimbOffset - petWidth / 2 >= bounds.Left &&
                    SideClear(windows, i, w.Left, top, sideBottom);
                bool climbRight = reachesFloor && r >= w.Right - 0.5 && w.Right + PetSettings.ClimbOffset + petWidth / 2 <= bounds.Right &&
                    SideClear(windows, i, w.Right, top, sideBottom);
                result.Add(new Ledge(w.Id, top, l, r, w.Left, w.Right, w.Bottom, climbLeft, climbRight));
            }
        }
        return result;
    }

    private static List<(double L, double R)> Subtract(List<(double L, double R)> pieces, double left, double right)
    {
        var next = new List<(double L, double R)>();
        foreach (var (l, r) in pieces)
        {
            if (right <= l || left >= r) { next.Add((l, r)); continue; }
            if (left > l) next.Add((l, left));
            if (right < r) next.Add((right, r));
        }
        return next;
    }

    // No window above this one covers the side line between the edge and the floor.
    private static bool SideClear(IReadOnlyList<WindowSnapshot> windows, int index, double x, double top, double bottom)
    {
        for (int j = 0; j < index; j++)
        {
            var o = windows[j];
            if (o.Left < x + 1 && o.Right > x - 1 && o.Top < bottom && o.Bottom > top) return false;
        }
        return true;
    }

    // The ledge of this window under the pet's centre, if it still holds the pet.
    public static Ledge? Support(IReadOnlyList<Ledge>? ledges, long id, double center)
    {
        if (ledges == null || id == 0) return null;
        foreach (var ledge in ledges)
            if (ledge.Id == id && center >= ledge.Left + PetSettings.LedgeMargin && center <= ledge.Right - PetSettings.LedgeMargin) return ledge;
        return null;
    }

    // The highest ledge the feet crossed while falling during this step.
    public static Ledge? Landing(IReadOnlyList<Ledge>? ledges, double previousFeet, double feet, double center)
    {
        if (ledges == null) return null;
        Ledge? best = null;
        foreach (var ledge in ledges)
        {
            if (previousFeet > ledge.Top + 0.5 || feet < ledge.Top) continue;
            if (center < ledge.Left + PetSettings.LedgeMargin || center > ledge.Right - PetSettings.LedgeMargin) continue;
            if (best == null || ledge.Top < best.Value.Top) best = ledge;
        }
        return best;
    }

    // The window side the pet climbs (side +1: right edge, -1: left edge), while it stays climbable.
    public static Ledge? Wall(IReadOnlyList<Ledge>? ledges, long id, int side)
    {
        if (ledges == null || id == 0) return null;
        foreach (var ledge in ledges)
            if (ledge.Id == id && (side > 0 ? ledge.ClimbRight : ledge.ClimbLeft)) return ledge;
        return null;
    }

    // A leap from where the pet stands onto a higher ledge within reach: launch velocity
    // that peaks a little above the edge and comes down on it.
    public static (Ledge Target, double VX, double VY)? PlanJump(IReadOnlyList<Ledge> ledges, PetModel pet, Area bounds, PetSettings settings)
    {
        double feet = pet.Y + settings.Height, center = pet.X + settings.Width / 2;
        (Ledge Target, double VX, double VY)? best = null;
        double bestScore = double.MaxValue;
        foreach (var ledge in ledges)
        {
            if (ledge.Id == pet.LedgeId) continue;
            double height = feet - ledge.Top;
            if (height < MinJumpHeight || height > MaxJumpHeight) continue;
            if (ledge.Top - settings.Height - JumpApexMargin < bounds.Top) continue; // The head would hit the screen top.
            if (ledge.Right - ledge.Left < MinLedgeWidth + 2 * PetSettings.LedgeMargin) continue;
            double inset = Math.Min(40, (ledge.Right - ledge.Left) / 2);
            double target = Math.Clamp(center, ledge.Left + inset, ledge.Right - inset);
            target = Math.Clamp(target, bounds.Left + settings.Width / 2, bounds.Right - settings.Width / 2);
            double dx = target - center;
            if (Math.Abs(dx) > MaxJumpReach) continue;
            double apex = height + JumpApexMargin;
            double time = Math.Sqrt(2 * apex / settings.Gravity) + Math.Sqrt(2 * JumpApexMargin / settings.Gravity);
            double score = Math.Abs(dx) + height * 0.5;
            if (score >= bestScore) continue;
            bestScore = score;
            best = (ledge, dx / time, -Math.Sqrt(2 * settings.Gravity * apex));
        }
        return best;
    }

    // From the floor: the nearest climbable window side, and where the climb starts.
    public static (Ledge Target, int Side, double StartX)? PlanClimb(IReadOnlyList<Ledge> ledges, PetModel pet, PetSettings settings)
    {
        (Ledge Target, int Side, double StartX)? best = null;
        double bestDistance = MaxClimbWalk;
        foreach (var ledge in ledges)
        {
            foreach (int side in new[] { -1, 1 })
            {
                if (!(side > 0 ? ledge.ClimbRight : ledge.ClimbLeft)) continue;
                double startX = (side > 0 ? ledge.WindowRight + PetSettings.ClimbOffset : ledge.WindowLeft - PetSettings.ClimbOffset) - settings.Width / 2;
                double distance = Math.Abs(startX - pet.X);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = (ledge, side, startX);
            }
        }
        return best;
    }
}
