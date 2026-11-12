using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// Mini-games (docs/MINI-GAMES.md → M1): "Find the fish" under three cups. Pure rules: the numbers, where the
// cups go next to the cat, and the shuffle itself. The window (PetWindow.CupGame.cs) draws and times it.
public static class CupGameRules
{
    public const int Cups = 3, MaxRounds = 10, BestNoticeFrom = 3;
    // The cups (DIPs): 36 tall so the face of the cat standing behind the row stays in view, centres 70 apart.
    public const double CupWidth = 40, CupHeight = 36, CupSpacing = 70;
    public const double RowWidth = CupSpacing * (Cups - 1) + CupWidth;
    // Cat centre to the nearest cup centre when the cups are put out, and the gap kept to land, carts and toys.
    public const double CatToRow = 96, Gap = 8;
    // A game lasts two minutes at most; asleep, the request waits 30 seconds for the cat to wake by itself;
    // the walk to a roomier spot takes 5 seconds at most.
    public const double GameSeconds = 120, SleepWaitSeconds = 30, RoomWalkSeconds = 5, CupWalkSeconds = 4;
    // The steps of a round.
    public const double ShowSeconds = 1.0, CoverSeconds = 0.5, SniffSeconds = 0.6, LiftSeconds = 0.45,
        WinSeconds = 1.1, PuzzledSeconds = 1.0, RevealSeconds = 1.4, EatSeconds = 1.6, LabelSeconds = 1.6;

    // Round 1: 3 slow swaps, round 2: 5, from round 3 on: 7 at the fastest speed.
    public static int Swaps(int round) => round <= 1 ? 3 : round == 2 ? 5 : 7;
    public static double SwapSeconds(int round) => round <= 1 ? 0.6 : round == 2 ? 0.45 : 0.35;

    // Slot centre, from the row's left edge.
    public static double SlotCentre(int slot) => CupWidth / 2 + slot * CupSpacing;

    // The row's left edge for a cat centre and the side the row is on (+1: to its right).
    public static double RowLeft(double catCentre, int side) =>
        side > 0 ? catCentre + CatToRow - CupWidth / 2 : catCentre - CatToRow - (Cups - 1) * CupSpacing - CupWidth / 2;

    // Where the cat stands to play (its centre) and on which side the cups go; the nearest spot first, the side
    // with more room first. Null: nowhere on this ground within reach.
    public static (double CatCentre, int Side)? Place(double catCentre, double halfCat, double groundLeft, double groundRight,
        IReadOnlyList<(double Left, double Right)> taken, double reach)
    {
        if (!double.IsFinite(catCentre) || groundRight - groundLeft < 2 * halfCat) return null;
        bool Free(double left, double right) => left >= groundLeft + 4 && right <= groundRight - 4 &&
            taken.All(t => right <= t.Left - Gap || left >= t.Right + Gap);
        int first = groundRight - catCentre >= catCentre - groundLeft ? 1 : -1;
        for (double step = 0; step <= reach + 0.01; step += 20)
            foreach (double centre in step == 0 ? new[] { catCentre } : new[] { catCentre + step * first, catCentre - step * first })
            {
                if (centre - halfCat < groundLeft || centre + halfCat > groundRight) continue;
                foreach (int side in new[] { first, -first })
                {
                    double left = RowLeft(centre, side);
                    if (Free(left, left + RowWidth)) return (centre, side);
                }
            }
        return null;
    }

    // Which slot a point across the row is over (from the row's left edge); -1: between or beyond the cups.
    public static int SlotAt(double x)
    {
        for (int slot = 0; slot < Cups; slot++)
            if (Math.Abs(x - SlotCentre(slot)) <= CupWidth / 2 + 4) return slot;
        return -1;
    }

    // A right cup: the best goes up at once (quitting the app loses nothing). True when it went up.
    public static bool RaiseBest(PetPreferences preferences, int rounds)
    {
        rounds = Math.Clamp(rounds, 0, MaxRounds);
        if (rounds <= preferences.CupGameBest) return false;
        preferences.CupGameBest = rounds;
        return true;
    }

    // A game that ended the user's way (a wrong cup, ten rounds, Esc, a click, time up).
    public static void CountPlay(PetPreferences preferences) => preferences.CupGamePlays = Math.Min(999999, preferences.CupGamePlays + 1);

    // "New best!" at the end only from three rounds.
    public static bool TellBest(bool raised, int rounds) => raised && rounds >= BestNoticeFrom;
}

// The cups changing places. Cup ids stay with the cups; slots are the three places in the row. The fish is
// under one cup (FishCup) and moves with it. Each swap moves two cups past each other: one along an arc over
// the other, so the eye can follow it, never a jump.
public sealed class CupShuffle
{
    private readonly int[] slotOf = { 0, 1, 2 };
    private readonly List<(int A, int B)> swaps = new();
    private double time, swapSeconds = 1;
    private int applied;

    public int FishCup { get; private set; } = 1;
    public int Round { get; private set; }
    public bool Shuffling => applied < swaps.Count;
    public int SwapCount => swaps.Count;

    public void Reset()
    {
        for (int i = 0; i < slotOf.Length; i++) slotOf[i] = i;
        swaps.Clear();
        time = 0; applied = 0; Round = 0;
        FishCup = 1;
    }

    // The fish goes under the cup in the middle slot, then this round's swaps start.
    public void Start(int round, Random random)
    {
        Round = Math.Max(1, round);
        FishCup = CupIn(1);
        swaps.Clear();
        (int, int)[] pairs = { (0, 1), (1, 2), (0, 2) };
        for (int i = 0; i < CupGameRules.Swaps(Round); i++) swaps.Add(pairs[random.Next(pairs.Length)]);
        swapSeconds = CupGameRules.SwapSeconds(Round);
        time = 0; applied = 0;
    }

    public void Step(double dt)
    {
        if (!Shuffling || !double.IsFinite(dt) || dt <= 0) return;
        time += dt;
        while (applied < swaps.Count && time >= (applied + 1) * swapSeconds)
        {
            var (a, b) = swaps[applied];
            int ca = CupIn(a), cb = CupIn(b);
            slotOf[ca] = b; slotOf[cb] = a;
            applied++;
        }
    }

    public int SlotOf(int cup) => slotOf[cup];
    public int CupIn(int slot) => Array.IndexOf(slotOf, slot);

    // Where a cup is drawn now: its centre across the row (from the row's left edge) and how high it rides
    // (DIPs above the floor), and whether it passes in front this moment.
    public (double X, double Rise, bool Front) Pose(int cup)
    {
        double home = CupGameRules.SlotCentre(slotOf[cup]);
        if (!Shuffling) return (home, 0, false);
        var (a, b) = swaps[applied];
        double f = Math.Clamp(time / swapSeconds - applied, 0, 1);
        double ease = f * f * (3 - 2 * f);
        int from = slotOf[cup];
        if (from != a && from != b) return (home, 0, false);
        int to = from == a ? b : a;
        double x = CupGameRules.SlotCentre(from) + (CupGameRules.SlotCentre(to) - CupGameRules.SlotCentre(from)) * ease;
        // The cup from the left slot hops over in front; the other slides along behind with a small lift.
        bool front = from == Math.Min(a, b);
        double arc = Math.Sin(Math.PI * f) * (front ? 16 + 4 * Math.Abs(to - from) : 3);
        return (x, arc, front);
    }
}
