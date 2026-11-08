using System;

namespace DesktopPet;

public enum FarmChoreKind { None, Water, Harvest, Bird, Till, Sow, Pour, Stir }
public enum FarmChorePhase { None, Notice, Walk, Work }
public enum FarmBirdPhase { Away, Landing, Perched, Leaving }

// The cat's own trips to the field (1.5.1 M4; rhythm in docs/FARM-GAME.md): when the next is due,
// which tile, and how far along one is. One tile a trip, a short rest between trips and a long one
// after several. It tills and sows again only tiles where something was harvested.
// The window owns the walking, the art and the rules for when the cat may go (PetWindow.FarmChores.cs).
public sealed class FarmChores
{
    public const double RestMinSeconds = 15, RestMaxSeconds = 40, LongRestMinSeconds = 120, LongRestMaxSeconds = 180, FirstVisitSeconds = 30;
    public const int TripsBeforeLongRest = 6;
    public const double BirdMinSeconds = 360, BirdMaxSeconds = 720, ScarecrowBirdFactor = 2;
    // The moment was wrong or there was nothing to do: look again soon, nothing piles up.
    public const double RetrySeconds = 45;
    public const double NoticeSeconds = 0.7, WalkLimitSeconds = 20;
    public const double WaterSeconds = PetModel.WaterDuration, WaterHit = 1.3;
    public const double HarvestSeconds = 0.9, HarvestHit = 0.3;
    public const double PounceSeconds = 1, PounceHit = 0.5;
    public const double TillSeconds = 1.2, TillHit = 0.6, SowSeconds = 1, SowHit = 0.5;
    // At the hearth (docs/FARM-GAME.md). The walk may be longer: the hearth can be a whole screen away.
    public const double PourSeconds = 1.2, PourHit = 0.6, StirSeconds = PetModel.StirDuration, CookWalkLimitSeconds = 30;
    public const double CookMinSeconds = 600, CookMaxSeconds = 900;
    // Trips come often: the cat's own sounds are spaced out. A click by the user always sounds.
    public const double SoundGapSeconds = 60;

    public FarmChoreKind Kind { get; private set; }
    public FarmChorePhase Phase { get; private set; }
    public int Tile { get; private set; } = -1;
    // Where the cat stands to do it (its left edge, DIPs).
    public double Spot { get; private set; }
    // Seconds in this phase.
    public double Time { get; private set; }
    public double VisitDue { get; private set; } = FirstVisitSeconds;
    public double BirdDue { get; private set; } = BirdMinSeconds;
    // Until the cat may cook by itself again.
    public double CookDue { get; private set; } = CookMinSeconds;
    // What is being poured, and whether the user asked for it (then Quiet and the like do not stop it).
    public FarmDish Dish { get; private set; }
    public bool ByUser { get; private set; }
    private bool done;
    private int tripsInRow;

    public bool Active => Kind != FarmChoreKind.None;
    public bool Cooking => Kind is FarmChoreKind.Pour or FarmChoreKind.Stir;
    public bool WalkedTooLong => Phase == FarmChorePhase.Walk && Time >= (Cooking ? CookWalkLimitSeconds : WalkLimitSeconds);
    public bool Finished => Phase == FarmChorePhase.Work && Time >= WorkSeconds;
    private double WorkSeconds => Kind switch { FarmChoreKind.Water => WaterSeconds, FarmChoreKind.Harvest => HarvestSeconds, FarmChoreKind.Till => TillSeconds, FarmChoreKind.Sow => SowSeconds, FarmChoreKind.Pour => PourSeconds, FarmChoreKind.Stir => StirSeconds, _ => PounceSeconds };
    public double HitTime => Kind switch { FarmChoreKind.Water => WaterHit, FarmChoreKind.Harvest => HarvestHit, FarmChoreKind.Till => TillHit, FarmChoreKind.Sow => SowHit, FarmChoreKind.Pour => PourHit, FarmChoreKind.Stir => StirSeconds, _ => PounceHit };

    // The wait after a look for work. It counts only while no trip is on, so a rest begins when the trip ends.
    public void ScheduleAfterLook(bool went, Random random)
    {
        if (!went)
        {
            tripsInRow = 0;
            ScheduleVisit(RetrySeconds);
            return;
        }
        bool tired = ++tripsInRow >= TripsBeforeLongRest;
        if (tired) tripsInRow = 0;
        var (least, most) = tired ? (LongRestMinSeconds, LongRestMaxSeconds) : (RestMinSeconds, RestMaxSeconds);
        ScheduleVisit(least + random.NextDouble() * (most - least));
    }

    public static double NextBird(Random random, bool scarecrow = false) => (BirdMinSeconds + random.NextDouble() * (BirdMaxSeconds - BirdMinSeconds)) * (scarecrow ? ScarecrowBirdFactor : 1);
    public void ScheduleVisit(double seconds) => VisitDue = Math.Max(0, seconds);
    public void ScheduleBird(double seconds) => BirdDue = Math.Max(0, seconds);
    public void ScheduleCook(double seconds) => CookDue = Math.Max(0, seconds);
    public static double NextCook(Random random) => CookMinSeconds + random.NextDouble() * (CookMaxSeconds - CookMinSeconds);
    public void CountDownCook(double dt) => CookDue = Math.Max(0, CookDue - dt);

    // True on the frame the next trip is due: the caller decides, and always calls ScheduleVisit.
    public bool CountDownVisit(double dt) => (VisitDue = Math.Max(0, VisitDue - dt)) <= 0;
    public bool CountDownBird(double dt) => (BirdDue = Math.Max(0, BirdDue - dt)) <= 0;

    public static FarmStep StepOf(FarmChoreKind kind) => kind switch
    {
        FarmChoreKind.Water => FarmStep.Water,
        FarmChoreKind.Till => FarmStep.Till,
        FarmChoreKind.Sow => FarmStep.Plant,
        _ => FarmStep.Harvest
    };

    // What a trip would do: the nearest ripe tile, else thirsty, else one to till or sow again, else nothing.
    public static (FarmChoreKind Kind, int Tile)? Pick(PetFarm farm, Func<int, double> distance, bool resow = true)
    {
        foreach (var kind in new[] { FarmChoreKind.Harvest, FarmChoreKind.Water, FarmChoreKind.Till, FarmChoreKind.Sow })
        {
            bool again = kind is FarmChoreKind.Till or FarmChoreKind.Sow;
            if (again && !resow) continue;
            var step = StepOf(kind);
            int best = -1;
            for (int i = 0; i < PetFarm.TileCount; i++)
                if (farm.Next(i) == step && (!again || farm.ResowCrop(i) != null) && (best < 0 || distance(i) < distance(best))) best = i;
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
        ByUser = false;
    }

    // To the hearth: to pour this dish in, or to stir what the pot already holds.
    public void BeginCook(FarmChoreKind kind, FarmDish dish, double spot, bool byUser)
    {
        Begin(kind, -1, spot);
        Dish = dish;
        ByUser = byUser;
    }

    // Poured: on to the stirring, without leaving the hearth.
    public void StartStirring()
    {
        Kind = FarmChoreKind.Stir;
        Phase = FarmChorePhase.Work;
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
        ByUser = false;
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
