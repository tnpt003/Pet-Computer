using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DesktopPet;

// Golden crops (docs/FARM-GAME.md, stage 4 milestone 2): a crop watered on time each of its three
// times may come up golden. It is only ever a bonus, so a late watering costs nothing but the chance.
public sealed partial class PetFarm
{
    public const double OnTimeSeconds = 600, GoldChance = 0.4;
    public const int GoldPriceFactor = 2;

    // Golden crops in the store, by crop name, apart from the plain ones.
    public Dictionary<string, int> GoldStock { get; set; } = new();
    // Whether the last harvest came up golden.
    [JsonIgnore] public bool LastHarvestGold { get; private set; }

    public int GoldOf(FarmCrop crop) => GoldStock.GetValueOrDefault(crop.ToString());
    // Plain and golden together: what cooking and the stall can use.
    public int HaveOf(FarmCrop crop) => StockOf(crop) + GoldOf(crop);
    public static int GoldSellPrice(FarmCrop crop) => SellPrice(crop) * GoldPriceFactor;

    // Sells one golden crop. The coins it brought, 0 when there was none.
    public int SellGold(FarmCrop crop)
    {
        if (GoldOf(crop) <= 0) return 0;
        TakeGold(crop, 1);
        return Earn(GoldSellPrice(crop));
    }

    private void TakeGold(FarmCrop crop, int count)
    {
        int left = GoldOf(crop) - count;
        if (left <= 0) GoldStock.Remove(crop.ToString());
        else GoldStock[crop.ToString()] = left;
    }

    private static bool WateredOnTime(FarmTile tile, DateTime utcNow) =>
        tile.ThirstySince is not DateTime since || (utcNow - since).TotalSeconds <= OnTimeSeconds;

    // Random null: never golden.
    private bool ComesUpGolden(FarmTile tile, Random? random) => random != null && !tile.LateWater && random.NextDouble() < GoldChance;
}
