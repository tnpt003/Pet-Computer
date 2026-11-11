using System;

namespace DesktopPet;

// The plant pot (docs/LIVING-CORNER.md → M2). Every 8–12 minutes the cat walks over on its own (blocked
// like the scratching post) and sniffs the leaves, paws at them or sits by it a while, never the same as
// last time. A whole visit earns the plant a point (at most one every ten minutes); it grows through four
// stages and never wilts. All C#: no Python, no needs, bond or learning. The whole visit owns the cat's
// movement, so a click, a drag or an emotion that owns the cat ends it (CancelFeatureActivity).
public sealed partial class PetWindow
{
    private enum PlantTrip { None, Walk, Visit }
    private PlantTrip plantTrip;
    private PlantRules.Visit plantKind;
    private PlantRules.Visit? plantLastKind;
    private double plantVisit = PlantRules.VisitMin, plantTime, plantSparkle = -1;
    private int plantFacing = 1;
    private bool plantPawed;

    public string PlantTripPhase => plantTrip.ToString();
    public string PlantVisitKind => plantKind.ToString();
    public int PlantStage => PlantRules.StageOf(preferences.PlantPoints);
    private DesktopThing? DesktopPlant => DesktopToy(DesktopToyRules.Plant);

    private bool PlantUsable(DesktopThing plant) => ToysMayShow && plant.Window?.IsVisible == true && !plant.Held && !plant.Press
        && plant.Glide == null && plant.Motion.AtRest && plant.Monitor == CurrentMonitor().DeviceName;
    private bool PlantReady => DesktopPlant is { } plant && PlantUsable(plant) && pet.Grounded && pet.LedgeId == 0 && PlantStand(plant) != null;

    // Where the cat's left edge stands beside the pot (the side it comes from, else the other).
    private double? PlantStand(DesktopThing plant)
    {
        double half = settings.Width / 2, off = DesktopToyRules.PlantHalfWidth + PlantRules.Reach;
        int side = CatCenter < plant.Motion.X ? -1 : 1;
        foreach (int s in new[] { side, -side })
            if (CatCanStand(plant.Motion.X + s * off - half) is double left) return left;
        return null;
    }

    // For checks (and the timer): go and visit the plant now. False when it may not.
    public bool GoVisitPlant()
    {
        if (plantTrip != PlantTrip.None || !MayAct(AutoKind.PlantVisit) || !behavior.SelfCareReady || !PlantReady
            || pet.Action is not (PetAction.Idle or PetAction.Sitting or PetAction.Looking or PetAction.Walking)) return false;
        plantTrip = PlantTrip.Walk;
        plantTime = 0;
        System.Diagnostics.Trace.WriteLine("The cat goes to the plant pot.");
        return true;
    }

    // From TickFeatures, next to the scratching post. True while it owns the cat.
    private bool TickPlantTrip(double dt)
    {
        plantVisit -= dt;
        if (plantTrip == PlantTrip.None)
        {
            if (plantVisit > 0) return false;
            plantVisit = PlantRules.NextVisit(featureRandom.NextDouble());
            if (!GoVisitPlant()) return false;
        }
        plantTime += dt;
        if (DesktopPlant is not { } plant || !PlantUsable(plant) || !CanPlay) { StopPlantTrip(); return false; }
        if (plantTrip == PlantTrip.Walk)
        {
            if (plantTime > PlantRules.WalkSeconds || PlantStand(plant) is not double target) { StopPlantTrip(); return false; }
            if (Math.Abs(target - pet.X) >= 4) { WalkToward(target); return true; }
            pet.X = target;
            plantFacing = plant.Motion.X < CatCenter ? -1 : 1;
            plantKind = PlantRules.NextKind(plantLastKind, featureRandom.NextDouble());
            plantLastKind = plantKind;
            plantTrip = PlantTrip.Visit;
            plantTime = 0;
            plantPawed = false;
            System.Diagnostics.Trace.WriteLine($"The cat visits the plant: {plantKind}.");
        }
        double t = plantTime, length = PlantRules.SecondsOf(plantKind);
        HoldStill(PetAction.Sitting, plantFacing);
        switch (plantKind)
        {
            case PlantRules.Visit.Sniff: // Nose down to the leaves, as it sniffs a new toy.
                pet.PlayCrouch = Math.Min(1, t / 0.2) * Math.Min(1, (length - t) / 0.15 + 0.2);
                pet.ReactDip = 9 * pet.PlayCrouch; pet.ReactTilt = 8 * plantFacing * pet.PlayCrouch;
                break;
            case PlantRules.Visit.Paw: // One paw at the leaves; they sway.
                if (!plantPawed && t >= 0.3) { plantPawed = true; pet.BatRemaining = 0.3; plant.Motion.Wobble(); }
                break;
            default: // Sitting by it, looking up at it now and then.
                pet.ReactTilt = 5 * plantFacing * Math.Max(0, Math.Sin(t * 1.6));
                break;
        }
        if (t < length) return true;
        // A whole visit: maybe a point, maybe a new stage.
        int stage = PlantStage, points = preferences.PlantPoints;
        DateTime? last = preferences.PlantPointAt;
        if (PlantRules.Earn(ref points, ref last, DateTime.UtcNow))
        {
            preferences.PlantPoints = points; preferences.PlantPointAt = last;
            if (PlantStage > stage) plantSparkle = 0;
            SavePreferences();
        }
        StopPlantTrip();
        return false;
    }

    private void StopPlantTrip()
    {
        if (plantTrip == PlantTrip.None) return;
        var was = plantTrip;
        plantTrip = PlantTrip.None;
        plantVisit = Math.Max(plantVisit, PlantRules.VisitMin);
        pet.PlayCrouch = pet.ReactDip = pet.ReactTilt = 0;
        if (!dragging && (was == PlantTrip.Walk ? pet.Action == PetAction.Walking : pet.Action == PetAction.Sitting))
        { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
    }

    // The pot's drawing every frame it shows: its stage, the leaves swaying, the glitter of a new stage.
    private void DrawPlant(DesktopThing plant, double dt = 0)
    {
        if (plantSparkle >= 0 && (plantSparkle += dt) >= ToyPlantArt.SparkleSeconds) plantSparkle = -1;
        if (plant.Art is not ToyPlantArt art) return;
        double rustle = plant.Motion.Feedback > 0 ? Math.Sin(plant.Motion.Feedback * 30) * plant.Motion.Feedback * 30 : 0;
        double sparkle = plantSparkle;
        int stage = PlantStage;
        if (art.Stage == stage && art.Rustle == rustle && art.Sparkle == sparkle) return;
        art.Stage = stage; art.Rustle = rustle; art.Sparkle = sparkle;
        art.InvalidateVisual();
    }

    private string PlantStageName(int stage) => stage switch
    {
        0 => L("mầm", "sprout"),
        1 => L("cây nhỏ", "small plant"),
        2 => L("sum suê", "leafy"),
        _ => L("ra hoa", "in flower")
    };
}
