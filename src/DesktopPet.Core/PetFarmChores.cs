using System;

namespace DesktopPet;

public enum FarmChoreKind { None, Water, Harvest, Bird }
public enum FarmChorePhase { None, Notice, Walk, Work }
public enum FarmBirdPhase { Away, Landing, Perched, Leaving }

// The cat's own trips to the field (1.5.1 M4): when the next is due, which tile, and how far along
// one is. It waters and harvests one tile a trip, and never tills or sows: those stay the user's.
// The window owns the walking, the art and the rules for when the cat may go (PetWindow.FarmChores.cs).
public sealed class FarmChores
{
    public const double VisitMinSeconds = 120, VisitMaxSeconds = 240, BirdMinSeconds = 360, BirdMaxSeconds = 720, ScarecrowBirdFactor = 2;
    // The moment was wrong or there was nothing to do: look again soon, nothing piles up.
    public const double RetrySeconds = 45;
    public const double NoticeSeconds = 0.7, WalkLimitSeconds = 20;
    public const double WaterSeconds = PetModel.WaterDuration, WaterHit = 1.3;
    public const double HarvestSeconds = 0.9, HarvestHit = 0.3;
    public const double PounceSeconds = 1, PounceHit = 0.5;

    public FarmChoreKind Kind { get; private set; }
    public FarmChorePhase Phase { get; private set; }
    public int Tile { get; private set; } = -1;
    // Where the cat stands to do it (its left edge, DIPs).
    public double Spot { get; private set; }
    // Seconds in this phase.
    public double Time { get; private set; }
    public double VisitDue { get; private set; } = VisitMinSeconds;
    public double BirdDue { get; private set; } = BirdMinSeconds;
    private bool done;

    public bool Active => Kind != FarmChoreKind.None;
    public bool WalkedTooLong => Phase == FarmChorePhase.Walk && Time >= WalkLimitSeconds;
    public bool Finished => Phase == FarmChorePhase.Work && Time >= WorkSeconds;
    private double WorkSeconds => Kind switch { FarmChoreKind.Water => WaterSeconds, FarmChoreKind.Harvest => HarvestSeconds, _ => PounceSeconds };
    private double HitTime => Kind switch { FarmChoreKind.Water => WaterHit, FarmChoreKind.Harvest => HarvestHit, _ => PounceHit };

    public static double NextVisit(Random random) => VisitMinSeconds + random.NextDouble() * (VisitMaxSeconds - VisitMinSeconds);
    public static double NextBird(Random random, bool scarecrow = false) => (BirdMinSeconds + random.NextDouble() * (BirdMaxSeconds - BirdMinSeconds)) * (scarecrow ? ScarecrowBirdFactor : 1);
    public void ScheduleVisit(double seconds) => VisitDue = Math.Max(0, seconds);
    public void ScheduleBird(double seconds) => BirdDue = Math.Max(0, seconds);

    // True on the frame the next trip is due: the caller decides, and always calls ScheduleVisit.
    public bool CountDownVisit(double dt) => (VisitDue = Math.Max(0, VisitDue - dt)) <= 0;
    public bool CountDownBird(double dt) => (BirdDue = Math.Max(0, BirdDue - dt)) <= 0;

    // What a trip would do: the nearest ripe tile, else the nearest thirsty one, else nothing.
    public static (FarmChoreKind Kind, int Tile)? Pick(PetFarm farm, Func<int, double> distance)
    {
        foreach (var (step, kind) in new[] { (FarmStep.Harvest, FarmChoreKind.Harvest), (FarmStep.Water, FarmChoreKind.Water) })
        {
            int best = -1;
            for (int i = 0; i < PetFarm.TileCount; i++)
                if (farm.Next(i) == step && (best < 0 || distance(i) < distance(best))) best = i;
            if (best >= 0) return (kind, best);
        }
        return null;
    }

    // A tile with a crop on it for a bird to come to, or -1.
    public static int BirdTile(PetFarm farm, Random random)
    {
        int found = 0, chosen = -1;
        for (int i = 0; i < PetFarm.TileCount; i++)
            if (farm.Tiles[i].Crop != null && random.Next(++found) == 0) chosen = i;
        return chosen;
    }

    public void Begin(FarmChoreKind kind, int tile, double spot)
    {
        Kind = kind;
        Tile = tile;
        Spot = spot;
        Phase = kind == FarmChoreKind.Bird ? FarmChorePhase.Notice : FarmChorePhase.Walk;
        Time = 0;
        done = false;
    }

    public void Arrive()
    {
        if (Phase != FarmChorePhase.Walk) return;
        Phase = FarmChorePhase.Work;
        Time = 0;
    }

    public void Stop()
    {
        Kind = FarmChoreKind.None;
        Phase = FarmChorePhase.None;
        Tile = -1;
        Time = 0;
        done = false;
    }

    // True on the one frame the work lands on the tile.
    public bool Step(double dt)
    {
        if (Phase == FarmChorePhase.None) return false;
        Time += dt;
        if (Phase == FarmChorePhase.Notice && Time >= NoticeSeconds)
        {
            Phase = FarmChorePhase.Walk;
            Time = 0;
            return false;
        }
        if (Phase != FarmChorePhase.Work || done || Time < HitTime) return false;
        done = true;
        return true;
    }
}

// A paper bird on a tile. It does no harm: it pecks for a while, and leaves when the cat pounces or
// when it has had enough. It comes only when the cat could chase it.
public sealed class FarmBird
{
    public const double LandSeconds = 0.6, LeaveSeconds = 0.7, StaySeconds = 30;

    public FarmBirdPhase Phase { get; private set; }
    public int Tile { get; private set; } = -1;
    // The side of the crop it stands on, -1 left or +1 right.
    public int Side { get; private set; } = 1;
    // Seconds since it came (its wings and its pecking).
    public double Time { get; private set; }
    private double phaseTime;

    public bool Present => Phase != FarmBirdPhase.Away;
    public bool Perched => Phase == FarmBirdPhase.Perched;
    // 0 standing on the tile, 1 up and away.
    public double Air => Phase switch
    {
        FarmBirdPhase.Landing => 1 - Smooth(Math.Clamp(phaseTime / LandSeconds, 0, 1)),
        FarmBirdPhase.Leaving => Smooth(Math.Clamp(phaseTime / LeaveSeconds, 0, 1)),
        _ => 0
    };

    public void Land(int tile, int side)
    {
        Phase = FarmBirdPhase.Landing;
        Tile = tile;
        Side = side < 0 ? -1 : 1;
        Time = phaseTime = 0;
    }

    public void Scare()
    {
        if (Phase is not (FarmBirdPhase.Landing or FarmBirdPhase.Perched)) return;
        Phase = FarmBirdPhase.Leaving;
        phaseTime = 0;
    }

    public void Reset()
    {
        Phase = FarmBirdPhase.Away;
        Tile = -1;
        Time = phaseTime = 0;
    }

    public void Step(double dt)
    {
        if (!Present) return;
        Time += dt;
        phaseTime += dt;
        if (Phase == FarmBirdPhase.Landing && phaseTime >= LandSeconds) { Phase = FarmBirdPhase.Perched; phaseTime = 0; }
        else if (Phase == FarmBirdPhase.Perched && phaseTime >= StaySeconds) Scare();
        else if (Phase == FarmBirdPhase.Leaving && phaseTime >= LeaveSeconds) Reset();
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);
}
