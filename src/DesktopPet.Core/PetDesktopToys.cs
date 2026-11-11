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
    public const string Ball = "ball", Box = "box", Wand = "wand", Mouse = "mouse", Post = "post", Plant = "plant";
    // Toys: 3 out at most. Living-corner things (docs/LIVING-CORNER.md: the scratching post) have their own 2.
    public const int MaxOut = 3, MaxLivingOut = 2;
    public const double BallHalfWidth = 12, BasketHalfWidth = 34, BasketHeight = 46, Gap = 4, GlideSpeed = 220;
    // The cardboard box (M2): about 100 DIPs wide, its body this tall above the floor.
    public const double BoxHalfWidth = 50, BoxHeight = 44;
    // M4: the feather wand's handle (about 90 DIPs) lying on the floor, and the wind-up mouse.
    public const double WandHalfWidth = 45, WandHeight = 10, MouseHalfWidth = 18, MouseHeight = 16;
    // Đợt 2 M1: the scratching post, a wooden base about 60 DIPs wide and 110 tall to the top of its disc.
    public const double PostHalfWidth = 30, PostHeight = 110;
    // M2: the plant pot, a clay pot about 50 DIPs wide; its height is the pot's (the leaves only draw above it).
    public const double PlantHalfWidth = 25, PlantHeight = 36;
    public static readonly IReadOnlyList<string> All = new[] { Ball, Box, Wand, Mouse, Post, Plant };

    public static bool IsLiving(string id) => id is Post or Plant;
    public static int LimitOf(string id) => IsLiving(id) ? MaxLivingOut : MaxOut;

    public static bool CanTake(int outCount) => outCount < MaxOut;
    // Room for this one among what is out: each group counts only its own.
    public static bool CanTake(string id, IEnumerable<string> outIds) => outIds.Count(other => IsLiving(other) == IsLiving(id)) < LimitOf(id);

    public static double HalfWidthOf(string id) => id switch { Box => BoxHalfWidth, Wand => WandHalfWidth, Mouse => MouseHalfWidth, Post => PostHalfWidth, Plant => PlantHalfWidth, _ => BallHalfWidth };
    // Half its height: the ball's radius, half the box's body, half the handle, half the mouse, half the post.
    public static double HalfHeightOf(string id) => id switch { Box => BoxHeight / 2, Wand => WandHeight / 2, Mouse => MouseHeight / 2, Post => PostHeight / 2, Plant => PlantHeight / 2, _ => BallHalfWidth };

    // What is left of a saved list: known toys, each once, no more than fit. No basket: nothing is out.
    public static List<SavedToy>? Normalize(IEnumerable<SavedToy>? saved, bool basketShown, IReadOnlyCollection<string>? known = null)
    {
        if (!basketShown || saved == null) return null;
        var unique = saved.Where(toy => toy != null && (known ?? All).Contains(toy.Id)).GroupBy(toy => toy.Id).Select(same => same.First()).ToList();
        var kept = unique.Where(toy => !IsLiving(toy.Id)).Take(MaxOut).Concat(unique.Where(toy => IsLiving(toy.Id)).Take(MaxLivingOut)).ToList();
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

// The scratching post (docs/LIVING-CORNER.md → M1): how often the cat goes on its own, how near it
// must be for a scratch it picked anyway or the user's "Do now", and where it stands to scratch.
public static class PostScratchRules
{
    public const double VisitMin = 360, VisitMax = 600, WalkSeconds = 15, CommandReach = 400, NearDistance = 200;
    // Cat centre to post centre while scratching: the scratching paws (about 29 DIPs ahead of the cat's
    // centre in its 112-DIP drawing) land on the near side of the rope.
    public const double StandOff = 34;

    // random in [0, 1): the next visit of its own, 6–10 minutes away.
    public static double NextVisit(double random) => VisitMin + Math.Clamp(double.IsFinite(random) ? random : 0, 0, 1) * (VisitMax - VisitMin);

    // Where the cat's centre stands to scratch: on the side it comes from, else the other; null: no room.
    public static double? StandAt(double postX, double catX, double left, double right)
    {
        int side = catX < postX ? -1 : 1;
        foreach (int s in new[] { side, -side })
        {
            double spot = postX + s * StandOff;
            if (spot >= left && spot <= right) return spot;
        }
        return null;
    }
}

// The plant pot (docs/LIVING-CORNER.md → M2): the cat's visits, and the plant growing with them. It never
// wilts: points only go up, and only a whole visit earns one, at most one every ten minutes.
public static class PlantRules
{
    public const double VisitMin = 480, VisitMax = 720, WalkSeconds = 15, PointGapSeconds = 600, Reach = 30;
    public const double SniffSeconds = 1.5, PawSeconds = 1.2, SitSeconds = 4;
    public static readonly int[] StageAt = { 0, 5, 15, 30 }; // Sprout, small plant, leafy, in flower.
    public const int MaxPoints = 9999;

    public enum Visit { Sniff, Paw, Sit }

    public static double NextVisit(double random) => VisitMin + Math.Clamp(double.IsFinite(random) ? random : 0, 0, 1) * (VisitMax - VisitMin);

    public static int StageOf(int points)
    {
        int stage = 0;
        for (int i = 0; i < StageAt.Length; i++) if (points >= StageAt[i]) stage = i;
        return stage;
    }

    public static double SecondsOf(Visit visit) => visit switch { Visit.Sniff => SniffSeconds, Visit.Paw => PawSeconds, _ => SitSeconds };

    // One of the three, never the last one again (random in [0, 1)).
    public static Visit NextKind(Visit? last, double random)
    {
        var kinds = new[] { Visit.Sniff, Visit.Paw, Visit.Sit }.Where(k => k != last).ToArray();
        return kinds[Math.Clamp((int)(Math.Clamp(double.IsFinite(random) ? random : 0, 0, 0.999999) * kinds.Length), 0, kinds.Length - 1)];
    }

    // A whole visit: one more point unless the last one came less than ten minutes ago (a clock set
    // back counts as long enough). True when it earned one.
    public static bool Earn(ref int points, ref DateTime? lastPoint, DateTime now)
    {
        if (lastPoint is DateTime last && now >= last && (now - last).TotalSeconds < PointGapSeconds) return false;
        points = Math.Min(MaxPoints, Math.Max(0, points) + 1);
        lastPoint = now;
        return true;
    }
}

// The cat hiding in the cardboard box awake, its head over the edge (docs/LIVING-CORNER.md → M2), and
// pouncing out at the wind-up mouse or the feather the user plays with going by.
public static class BoxPeekRules
{
    public const double VisitMin = 480, VisitMax = 720, PeekMin = 8, PeekMax = 15, LureReach = 200, StalkSeconds = 0.5;

    public static double NextVisit(double random) => VisitMin + Math.Clamp(double.IsFinite(random) ? random : 0, 0, 1) * (VisitMax - VisitMin);
    public static double PeekLength(double random) => PeekMin + Math.Clamp(double.IsFinite(random) ? random : 0, 0, 1) * (PeekMax - PeekMin);
}
