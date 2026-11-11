using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// Passing the yarn ball (docs/TOY-PLAY.md → M5): the user rolls it to the cat (a click or a light
// throw), the cat bats it back toward the cursor. The cat's part is in PetWindow.ToyPass.cs.
public static class ToyPassRules
{
    // The ball counts when rolling toward the cat within Reach; the cat steps up to StepUp to meet it
    // and walks to a ball that stopped short within ShortStop. Six seconds untouched: the streak ends.
    public const double Reach = 400, StepUp = 150, ShortStop = 120, IdleSeconds = 6, LabelSeconds = 1.5, OverheadBand = 60;
    public const double BatReach = 49, BatHeight = 24, MovingSpeed = 20, MinBat = 120, MaxBat = 300, BatLift = 125;
    public const int RecordAtLeast = 5;
    // Ground friction of ToyBall.Step: a ball rolling at v stops about v / 1.25 DIPs on.
    public const double Friction = 1.25;

    // The roll that stops the ball about distance DIPs away.
    public static double BatSpeed(double distance) => double.IsFinite(distance) ? Math.Clamp(Math.Abs(distance) * Friction, MinBat, MaxBat) : MinBat;

    // Toward the cursor; with the cursor right above the cat, back the way the ball came (cameFrom: -1 left, 1 right).
    public static int BatDirection(double cursorX, double catX, int cameFrom)
    {
        double dx = cursorX - catX;
        if (!double.IsFinite(dx) || Math.Abs(dx) < OverheadBand) return cameFrom < 0 ? -1 : 1;
        return dx < 0 ? -1 : 1;
    }

    // Where a ball rolling on the floor stops, before walls.
    public static double StopsAt(double x, double vx) => x + vx / Friction;
}

// The passes of one streak and the record. A pass counts only when the user touched the ball since the cat's last bat.
public sealed class ToyPassCount
{
    public int Count { get; private set; }
    public int Best { get; private set; }
    public bool Touched { get; private set; }
    private bool beaten;

    public ToyPassCount(int best = 0) { Best = Math.Max(0, best); }

    public void Touch() => Touched = true;

    // The cat bats the ball back; true when that was a pass (the record follows at once, so nothing is lost on exit).
    public bool Bat()
    {
        if (!Touched) return false;
        Touched = false;
        Count++;
        if (Count > Best) { Best = Count; if (Count >= ToyPassRules.RecordAtLeast) beaten = true; }
        return true;
    }

    // The streak is over; true when it set a new record of at least five.
    public bool End()
    {
        bool record = beaten;
        Count = 0; Touched = false; beaten = false;
        return record;
    }
}

// Short reactions (docs/TOY-PLAY.md → M5): only actions, never emotions.
public static class ToyReactRules
{
    public const double MissSeconds = 1.0, MissGap = 10, SniffSeconds = 1.5, QuickSniff = 0.5, AccessorySeconds = 2.0, AccessoryLook = 0.6;
    public const double SniffWalkSeconds = 20, SniffReach = 30;

    // Known toys only, each once.
    public static List<string>? Normalize(List<string>? sniffed)
    {
        var known = sniffed?.Where(id => id != null && DesktopToyRules.All.Contains(id)).Distinct().ToList();
        return known is { Count: > 0 } ? known : null;
    }

    // The first toy out that the cat has never sniffed, or null.
    public static string? NextToSniff(IEnumerable<string> outIds, IReadOnlyCollection<string>? sniffed) =>
        outIds.FirstOrDefault(id => sniffed == null || !sniffed.Contains(id));
}
