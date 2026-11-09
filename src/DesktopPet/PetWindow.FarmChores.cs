using System;

namespace DesktopPet;

// The cat works the field by itself (1.5.1 M4): every few minutes it walks over and waters or
// harvests one tile, and now and then a paper bird lands and the cat chases it off. Local only, so
// it goes on without Python, and it is not attention: it leaves the sleepiness clock alone.
// Any sleep, drag, Quiet, Pause and the like ends a trip at once (AutoKind.FarmChore).
public sealed partial class PetWindow
{
    // From the cat's middle to the tile's middle while it works, so the crop stays in view.
    private const double FarmChoreGap = 58, FarmBasketGap = 40, FarmFishGap = 100, FarmStalkSpeed = 0.6, FarmStalkCrouch = 0.45, FarmLeanDown = 0.3;

    private readonly FarmChores farmChores = new();
    private readonly FarmBird farmBird = new();
    private bool farmCatHands, treatOwnMeal, farmCropInPaws;
    private double farmChoreSoundWait;

    public FarmChoreKind FarmChoreNow => farmChores.Kind;
    public FarmChorePhase FarmChorePhaseNow => farmChores.Phase;
    public FarmBirdPhase FarmBirdPhaseNow => farmBird.Phase;
    public int FarmChoreTile => farmChores.Tile;
    public double FarmChoreSpot => farmChores.Spot;

    private double FarmTileMid(int tile) => FarmPlotLeft - PetFarmArt.Side + PetFarmArt.TileCenter(tile);

    private double FarmBasketMid => FarmPlotLeft - PetFarmArt.Side + PetFarmArt.BasketX;
    private double FarmHearthMid => FarmPatchLeft(FarmHearth) + FarmHearth.Width / 2;
    private double FarmPondMid => FarmPatchLeft(FarmPond) + FarmPond.Width / 2;

    // The cat can walk to the field from where it is.
    private bool FarmAtHand => preferences.Farm && FarmVisible && farm != null && pet.LedgeId == 0;
    // The cat goes only to land on the ground on its own monitor; each piece of land answers for itself.
    private bool FarmFieldReady => farmLift.Phase == FarmLiftPhase.Down && farmWithCat;
    private bool FarmHearthReady => FarmHearth.OnGround && FarmHearth.WithCat;
    private bool FarmPondReady => FarmPond.OnGround && FarmPond.WithCat;
    private bool FarmChoreLandDown => farmChores.Cooking ? FarmHearthReady : farmChores.Kind == FarmChoreKind.Fishing ? FarmPondReady : FarmFieldReady;
    private bool FarmHelpOn => preferences.FarmHelper && FarmAtHand;

    // Everything that starts a trip stops one, except the trip itself owning the cat. What the user
    // asked to have cooked stops like a treat; a pot with something in it does not depend on the helper setting.
    private bool FarmChoreMayGoOn => FarmChoreLandDown && (farmChores.ByUser
        ? FarmAtHand && MayAct(AutoKind.Treat)
        : (farmChores.Kind == FarmChoreKind.Stir ? FarmAtHand : FarmHelpOn) && Blocking(PetAutonomy.BlockedBy(AutoKind.FarmChore) & ~AutoBlock.Feature) == AutoBlock.None);

    // Each unpaused frame, before anything else may take the cat.
    private void WatchFarmChores(double dt)
    {
        if (farmChores.Active && !FarmChoreMayGoOn) StopFarmChore();
        farmChoreSoundWait = Math.Max(0, farmChoreSoundWait - dt);
        farmChores.CountDownCook(dt);
        farmChores.CountDownFish(dt);
        if (!farmChores.Active && farmChores.CountDownVisit(dt)) farmChores.ScheduleAfterLook(TryFarmVisit(), featureRandom);
        if (farmChores.CountDownBird(dt)) farmChores.ScheduleBird(TryFarmBird() ? FarmChores.NextBird(featureRandom, farm?.Scarecrow == true) : FarmChores.RetrySeconds);
    }

    private bool FarmChoreMayStart => FarmHelpOn && MayAct(AutoKind.FarmChore);

    // One trip now, if this is a moment for it and a tile needs it. True when the cat set off.
    public bool TryFarmVisit()
    {
        // Something left in the pot comes first, and is not the helper setting's business.
        if (FarmAtHand && FarmHearthReady && MayAct(AutoKind.FarmChore) && FarmCatCanEat && farm!.PotDish is FarmDish waiting)
        {
            farmChores.BeginCook(FarmChoreKind.Stir, waiting, FarmSpotBeside(FarmHearthMid), byUser: false);
            return true;
        }
        if (!FarmChoreMayStart) return false;
        if (FarmFieldReady && FarmChores.Pick(farm!, i => Math.Abs(FarmTileMid(i) - CatCenter), preferences.FarmResow) is var (kind, tile))
        {
            farmChores.Begin(kind, tile, FarmChoreSpotFor(tile));
            return true;
        }
        if (preferences.FarmAutoFish && FarmPondReady && farmChores.FishDue <= 0)
        {
            BeginFarmFishing(byUser: false);
            farmChores.ScheduleFish(FarmChores.NextFish(featureRandom));
            return true;
        }
        if (!preferences.FarmAutoCook || !FarmHearthReady || farmChores.CookDue > 0 || !FarmCatCanEat || farm!.AutoDish() is not FarmDish dish) return false;
        farmChores.BeginCook(FarmChoreKind.Pour, dish, FarmSpotBeside(FarmHearthMid), byUser: false);
        farmChores.ScheduleCook(FarmChores.NextCook(featureRandom));
        return true;
    }

    // One bird now, if the cat could chase it. True when it came.
    public bool TryFarmBird()
    {
        if (!FarmChoreMayStart || !FarmFieldReady || farmBird.Present) return false;
        int tile = FarmChores.BirdTile(farm!, featureRandom);
        if (tile < 0) return false;
        farmBird.Land(tile, CatCenter < FarmTileMid(tile) ? 1 : -1); // Beyond the crop: the cat will not stand on it.
        farmChores.Begin(FarmChoreKind.Bird, tile, FarmChoreSpotFor(tile));
        return true;
    }

    // Beside the tile on the side the cat comes from; the other side when the screen ends there.
    private double FarmChoreSpotFor(int tile) => FarmSpotBeside(FarmTileMid(tile));

    // The user's "Go fishing": like a treat it works in Quiet and is refused while the cat is asleep or eating.
    public bool FarmCanFish => !paused && FarmAtHand && FarmPondReady && MayAct(AutoKind.Treat) && pet.TreatRemaining <= 0 && treatSatisfiedRemaining <= 0
        && !farmChores.Cooking && farmChores.Kind != FarmChoreKind.Fishing && farmChores.FishAgain <= 0;

    public bool FarmGoFish()
    {
        if (!FarmCanFish) return false;
        CancelFeatureActivity();
        BeginFarmFishing(byUser: true);
        return true;
    }

    private WeatherKind? FarmPondWeather => weather.Visible && FarmPond.WithCat ? weather.Kind : null;

    private void BeginFarmFishing(bool byUser)
    {
        // From the far bank, so the cat does not sit in front of the hearth; the near one when the screen ends there.
        double half = settings.Width / 2, right = Math.Max(bounds.Left, bounds.Right - settings.Width);
        double spot = FarmPondMid + FarmFishGap - half;
        if (spot > right) spot = Math.Clamp(FarmPondMid - FarmFishGap - half, bounds.Left, right);
        farmChores.BeginFishing(spot, spot + half < FarmPondMid ? -1 : 1, FarmChores.NextBite(featureRandom, FarmPondWeather == WeatherKind.Night), farm!.RollFish(featureRandom, FarmPondWeather), byUser);
    }

    private double FarmSpotBeside(double mid, double gap = FarmChoreGap)
    {
        double half = settings.Width / 2, left = bounds.Left, right = Math.Max(bounds.Left, bounds.Right - settings.Width);
        double spot = mid + (CatCenter < mid ? -gap : gap) - half;
        if (spot < left || spot > right) spot = mid + (CatCenter < mid ? gap : -gap) - half;
        return Math.Clamp(spot, left, right);
    }

    // From TickFeatures. True while the trip owns the cat this frame.
    private bool TickFarmChore(double dt)
    {
        if (!farmChores.Active || farm == null) return false;
        int tile = farmChores.Tile;
        var kind = farmChores.Kind;
        // Done by hand meanwhile, or the bird has gone: nothing left to go for.
        if (farmChores.Phase is FarmChorePhase.Notice or FarmChorePhase.Walk && !FarmChoreWanted(kind, tile))
        {
            StopFarmChore();
            return false;
        }
        bool hit = farmChores.Step(dt);
        int facing = (farmChores.Cooking ? FarmHearthMid : kind == FarmChoreKind.Fishing ? FarmPondMid : FarmTileMid(tile)) < CatCenter ? -1 : 1;
        switch (farmChores.Phase)
        {
            case FarmChorePhase.Notice:
                HoldStill(PetAction.Looking, facing);
                return true;
            case FarmChorePhase.Walk:
                return WalkToFarmSpot(kind, dt);
            case FarmChorePhase.Carry:
                ShowFarmTool(kind, tile);
                return WalkToFarmSpot(kind, dt);
            case FarmChorePhase.Drop:
                DropInFarmBasket(tile, hit);
                return true;
        }
        DoFarmWork(kind, tile, facing, hit);
        if (farmChores.Finished) FinishFarmWork(kind);
        return true;
    }

    private bool FarmChoreWanted(FarmChoreKind kind, int tile) => kind switch
    {
        FarmChoreKind.Bird => FarmBirdThere(tile),
        FarmChoreKind.Pour => farm!.PotDish == null && farm.CanCook(farmChores.Dish),
        FarmChoreKind.Stir => farm!.PotDish != null,
        FarmChoreKind.Fishing => true,
        _ => farm!.Next(tile) == FarmChores.StepOf(kind)
    };

    // Already in the store since it was picked: this is only the walk and the drop.
    private void DropInFarmBasket(int tile, bool hit)
    {
        ShowFarmTool(FarmChoreKind.Harvest, tile);
        HoldStill(PetAction.Idle, FarmBasketMid < CatCenter ? -1 : 1);
        if (hit)
        {
            farmBasketAge = 0;
            farmCatHands = true;
            PlayFarmSound("pop");
            farmCatHands = false;
        }
        if (farmChores.Finished) StopFarmChore();
    }

    private void FinishFarmWork(FarmChoreKind kind)
    {
        if (kind == FarmChoreKind.Harvest && farmCropInPaws)
        {
            farmChores.StartCarry(FarmSpotBeside(FarmBasketMid, FarmBasketGap));
            return;
        }
        if (kind == FarmChoreKind.Pour && farm?.PotDish != null) { farmChores.StartStirring(); return; }
        if (kind == FarmChoreKind.Fishing) farmChores.ScheduleFishAgain(FarmChores.FishAgainSeconds);
        bool byUser = farmChores.ByUser;
        StopFarmChore();
        if (kind == FarmChoreKind.Stir && farm?.PotDish is FarmDish dish && GiveTreat(dish, byUser))
        {
            farm.TakePot();
            FarmChanged();
        }
    }

    private bool FarmBirdThere(int tile) => farmBird.Tile == tile && farmBird.Phase is FarmBirdPhase.Landing or FarmBirdPhase.Perched;

    private bool WalkToFarmSpot(FarmChoreKind kind, double dt)
    {
        if (farmChores.WalkedTooLong) { StopFarmChore(); return false; }
        double spot = farmChores.Spot;
        if (Math.Abs(spot - pet.X) < 4)
        {
            pet.X = spot;
            pet.VX = pet.SmoothedWalkVelocity = 0;
            pet.Action = PetAction.Idle;
            farmChores.Arrive();
            if (farmChores.Phase == FarmChorePhase.Work && kind is FarmChoreKind.Harvest or FarmChoreKind.Till or FarmChoreKind.Sow or FarmChoreKind.Pour) pet.BatRemaining = farmChores.HitTime;
            return true;
        }
        WalkToward(spot);
        if (kind == FarmChoreKind.Bird) { pet.VX *= FarmStalkSpeed; pet.PlayCrouch = Math.Min(FarmStalkCrouch, pet.PlayCrouch + dt * 2); } // On tiptoe.
        return true;
    }

    private void DoFarmWork(FarmChoreKind kind, int tile, int facing, bool hit)
    {
        farmCatHands = !farmChores.ByUser;
        ShowFarmTool(kind, tile);
        DoFarmWorkStep(kind, tile, facing, hit);
        if (kind is FarmChoreKind.Till or FarmChoreKind.Harvest) pet.PlayCrouch = FarmLeanDown * Math.Max(0, 1 - Math.Abs(farmChores.Time - farmChores.HitTime) / 0.25); // Into the blow, and up again.
        farmCatHands = false;
    }

    private void ShowFarmTool(FarmChoreKind kind, int tile)
    {
        pet.FarmTool = farmChores.Phase == FarmChorePhase.Carry ? FarmToolPose.Carry : farmChores.Phase == FarmChorePhase.Drop ? FarmToolPose.Drop : kind switch
        {
            FarmChoreKind.Till => FarmToolPose.Till,
            FarmChoreKind.Sow => FarmToolPose.Sow,
            FarmChoreKind.Harvest => FarmToolPose.Harvest,
            FarmChoreKind.Pour => FarmToolPose.Pour,
            FarmChoreKind.Bird => FarmToolPose.Pounce,
            _ => FarmToolPose.None
        };
        pet.FarmToolTime = farmChores.Time;
        pet.FarmToolHit = farmChores.HitTime;
        pet.FarmToolLength = farmChores.WorkLength;
        pet.FarmToolHold = farmCropInPaws;
        pet.FarmToolGold = farmCropInPaws && farm!.LastHarvestGold;
        if (kind == FarmChoreKind.Harvest && farm!.Tiles[tile].Crop is FarmCrop crop) pet.FarmToolCrop = crop; // Kept once the tile is empty: it is in the cat's paws now.
    }

    // Trips come often: what the cat does by itself sounds once a minute at most.
    private void PlayFarmSound(string sound)
    {
        if (farmCatHands)
        {
            if (farmChoreSoundWait > 0) return;
            farmChoreSoundWait = FarmChores.SoundGapSeconds;
        }
        PlayPetSound(sound);
    }

    private void DoFarmWorkStep(FarmChoreKind kind, int tile, int facing, bool hit)
    {
        switch (kind)
        {
            case FarmChoreKind.Pour:
                HoldStill(PetAction.Idle, facing);
                if (hit && farm!.Pour(farmChores.Dish))
                {
                    PlayFarmSound("pop");
                    FarmChanged();
                }
                break;
            case FarmChoreKind.Stir:
                HoldStill(PetAction.Sitting, facing);
                pet.StirringRemaining = Math.Max(0, FarmChores.StirSeconds - farmChores.Time);
                break;
            case FarmChoreKind.Fishing:
                HoldStill(PetAction.Sitting, facing);
                pet.FishingTime = farmChores.Time;
                pet.FishingBiteAt = farmChores.BiteAt;
                if (hit)
                {
                    farm!.Catch(farmChores.Catch);
                    if (farmChores.Catch == FarmFish.Goldfish) FarmReport = FarmReport with { Goldfish = FarmReport.Goldfish + 1 };
                    ShowFarmFishGain(farmChores.Catch);
                    PlayFarmSound("pop");
                    FarmChanged();
                }
                break;
            case FarmChoreKind.Till:
                HoldStill(PetAction.Idle, facing);
                if (hit && farm!.Next(tile) == FarmStep.Till && farm.ResowCrop(tile) != null && farm.Till(tile, keepLastCrop: true))
                {
                    StartFarmEffect(tile, FarmEffect.Till);
                    PlayFarmSound("ball_tap");
                    FarmChanged();
                }
                break;
            case FarmChoreKind.Sow:
                HoldStill(PetAction.Idle, facing);
                if (hit && farm!.Next(tile) == FarmStep.Plant && farm.ResowCrop(tile) is FarmCrop crop) FarmPlant(tile, crop);
                break;
            case FarmChoreKind.Water:
                HoldStill(PetAction.Sitting, facing);
                pet.WateringRemaining = Math.Max(0, FarmChores.WaterSeconds - farmChores.Time);
                if (hit && farm!.Next(tile) == FarmStep.Water) FarmClick(tile);
                break;
            case FarmChoreKind.Harvest:
                HoldStill(PetAction.Idle, facing);
                if (hit && farm!.Next(tile) == FarmStep.Harvest)
                {
                    FarmClick(tile);
                    farmCropInPaws = true;
                }
                break;
            default:
                HoldStill(PetAction.Sitting, facing);
                pet.PlayCrouch = farmChores.Time < FarmChores.PounceHit ? Math.Max(FarmStalkCrouch, Math.Min(1, farmChores.Time / 0.3)) : 0;
                if (hit)
                {
                    farmBird.Scare();
                    pet.BatRemaining = 0.3;
                    PlayPetSound("ball_tap");
                }
                break;
        }
    }

    private void StopFarmChore()
    {
        if (!farmChores.Active) return;
        farmChores.Stop();
        pet.WateringRemaining = pet.StirringRemaining = pet.PlayCrouch = 0;
        pet.FishingTime = -1;
        pet.FarmTool = FarmToolPose.None;
        pet.FarmToolCrop = null;
        pet.FarmToolHold = pet.FarmToolGold = farmCropInPaws = false;
        if (pet.Action is not (PetAction.Walking or PetAction.Sitting or PetAction.Looking or PetAction.Idle)) return; // Held, flying or asleep: not ours to change.
        pet.Action = PetAction.Idle;
        pet.VX = pet.SmoothedWalkVelocity = 0;
        behavior.Reset(pet);
    }

    // With the plot's own frame: the bird flies, and goes if its tile has been emptied under it.
    private void StepFarmBird(double dt)
    {
        if (farmBird.Present && farm != null && farm.Tiles[farmBird.Tile].Crop == null) farmBird.Scare();
        farmBird.Step(dt);
    }
}
