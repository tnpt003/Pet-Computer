using System;
using System.Text.Json.Serialization;

namespace DesktopPet;

// The hen coop (docs/FARM-GAME.md, stage 4 milestone 3): land of its own, bought, with up to three
// hens. Eggs come by the real clock, wait in the nest and cost nothing to leave there.
public sealed partial class PetFarm
{
    public const int CoopLevel = 3, CoopPrice = 200, MaxHens = 3, EggsPerHen = 3, EggPrice = 20, EggXp = 2;
    public const double EggSeconds = 45 * 60;
    public const string EggGood = "Egg";

    public int Hens { get; set; }
    // Collected and in the store / lying in the nest.
    public int EggStock { get; set; }
    public int EggsInCoop { get; set; }
    // UTC of the next egg (null: not counting, the nest is full or there are no hens).
    public DateTime? NextEggAt { get; set; }
    public double? CoopFractionX { get; set; }
    public string? CoopMonitor { get; set; }

    [JsonIgnore] public bool HasCoop => Hens > 0;
    [JsonIgnore] public int EggsCap => Hens * EggsPerHen;
    private double EggGap => EggSeconds / Math.Max(1, Hens);

    // The price of the hen after this many.
    public static int HenPrice(int owned) => owned <= 1 ? 150 : 300;
    public static int EggsIn(FarmDish dish) => dish is FarmDish.FriedEgg or FarmDish.EggTart ? 1 : 0;

    public bool BuyCoop(DateTime utcNow)
    {
        if (HasCoop || Level < CoopLevel || Coins < CoopPrice) return false;
        Coins -= CoopPrice;
        Hens = 1;
        NextEggAt = utcNow.AddSeconds(EggGap);
        return true;
    }

    public bool BuyHen()
    {
        if (!HasCoop || Hens >= MaxHens || Coins < HenPrice(Hens)) return false;
        Coins -= HenPrice(Hens);
        Hens++;
        return true;
    }

    // Eggs since last time. True when there are new ones. A clock set backwards never makes the wait longer.
    public bool LayEggs(DateTime utcNow)
    {
        if (!HasCoop) return false;
        var latest = utcNow.AddSeconds(EggGap);
        if (EggsInCoop >= EggsCap) { NextEggAt = null; return false; }
        if (NextEggAt == null || NextEggAt > latest) NextEggAt = latest;
        bool laid = false;
        while (utcNow >= NextEggAt && EggsInCoop < EggsCap)
        {
            EggsInCoop++;
            NextEggAt = NextEggAt.Value.AddSeconds(EggGap);
            laid = true;
        }
        if (EggsInCoop >= EggsCap) NextEggAt = null;
        return laid;
    }

    // Out of the nest and into the store. How many.
    public int CollectEggs()
    {
        int eggs = EggsInCoop;
        if (eggs <= 0) return 0;
        EggsInCoop = 0;
        EggStock += eggs;
        GainXp(eggs * EggXp);
        return eggs;
    }

    public int SellEgg()
    {
        if (EggStock <= 0) return 0;
        EggStock--;
        return Earn(EggPrice);
    }

    private void NormalizeCoop()
    {
        Hens = Math.Clamp(Hens, 0, MaxHens);
        EggStock = Math.Max(0, EggStock);
        EggsInCoop = Math.Clamp(EggsInCoop, 0, EggsCap);
        if (!HasCoop || EggsInCoop >= EggsCap) NextEggAt = null;
        else if (NextEggAt is DateTime next) NextEggAt = next.ToUniversalTime();
        if (CoopFractionX is double x) CoopFractionX = double.IsFinite(x) ? Math.Clamp(x, 0, 1) : null;
        if (string.IsNullOrWhiteSpace(CoopMonitor)) CoopMonitor = null;
    }
}
