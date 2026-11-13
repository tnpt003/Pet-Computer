using System;

namespace DesktopPet;

// Small life on the farm that changes nothing in the game: the pinwheel turns in the wind and a fish
// jumps in the pond now and then. Nothing is drawn again while they rest; Pause stops them (dt is 0).
public sealed partial class PetWindow
{
    private const double FarmGustSeconds = 1.6, FarmGustMinSeconds = 30, FarmGustMaxSeconds = 90;
    private const double FarmWindySpin = 300, FarmGustSpin = 720, FarmPinFrameSeconds = 1 / 30.0;
    public const double FarmLeapSeconds = 1.1, FarmLeapMinSeconds = 40, FarmLeapMaxSeconds = 90;

    private double farmPinTurn, farmPinSpeed, farmPinClock, farmGustAge = -1, farmGustDue = FarmGustMinSeconds;
    private double farmLeapAge = -1, farmLeapDue = 20, farmLeapX;
    private int farmLeapSide = 1;

    // The pinwheel turns steadily in windy weather (pollen, sunset leaves) and fast in a passing puff of wind.
    private void StepFarmPinwheel(double dt)
    {
        var layer = farmDecorBack;
        if (layer == null || farm?.ShowsDecor(FarmDecor.Pinwheel) != true) return;
        bool windy = FarmWeather is WeatherKind.Pollen or WeatherKind.Sunset;
        farmGustDue -= dt;
        if (farmGustAge < 0 && farmGustDue <= 0)
        {
            farmGustAge = 0;
            farmGustDue = FarmGustMinSeconds + featureRandom.NextDouble() * (FarmGustMaxSeconds - FarmGustMinSeconds);
        }
        if (farmGustAge >= 0 && (farmGustAge += dt / FarmGustSeconds) >= 1) farmGustAge = -1;
        double target = farmGustAge >= 0 ? FarmGustSpin : windy ? FarmWindySpin : 0;
        // Quick to catch the wind, slow to wind down.
        farmPinSpeed += (target - farmPinSpeed) * Math.Min(1, dt * (target > farmPinSpeed ? 3 : 0.7));
        if (target == 0 && farmPinSpeed < 4) farmPinSpeed = 0;
        farmPinTurn = (farmPinTurn + farmPinSpeed * dt) % 360;
        if (farmPinSpeed == 0 && farmGustAge < 0 && layer.Gust < 0) return;
        farmPinClock += dt;
        if (farmPinClock < FarmPinFrameSeconds && (farmGustAge >= 0) == (layer.Gust >= 0)) return;
        farmPinClock = 0;
        layer.Blow(farmPinTurn, farmGustAge);
    }

    // True while a fish is in the air (and the frame it lands), when the pond needs drawing.
    private bool StepFarmLeap(double dt, bool fishing)
    {
        if (farmLeapAge >= 0)
        {
            if ((farmLeapAge += dt / FarmLeapSeconds) >= 1) farmLeapAge = -1;
            return true;
        }
        if (fishing || FarmPond.Art == null || FarmPond.Lift.Busy) return false;
        if ((farmLeapDue -= dt) > 0) return false;
        farmLeapDue = FarmLeapMinSeconds + featureRandom.NextDouble() * (FarmLeapMaxSeconds - FarmLeapMinSeconds);
        farmLeapAge = 0;
        farmLeapSide = featureRandom.NextDouble() < 0.5 ? -1 : 1;
        farmLeapX = -28 + featureRandom.NextDouble() * 40;
        return true;
    }
}
