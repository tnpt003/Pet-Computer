using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// Tools and fertilizer (docs/FARM-GAME.md, stage 4 milestone 5). Better tools only let the cat do more
// tiles on one trip; a click stays one tile. Fertilizer is for one crop, chosen tile by tile.
public sealed partial class PetFarm
{
    public const int MaxToolTier = 2, FertilizerPrice = 15;
    public const double FertilizedTime = 0.75, FertilizedGoldChance = 0.55;

    // 0 bronze, 1 silver, 2 gold.
    public int HoeTier { get; set; }
    public int CanTier { get; set; }
    // Bags given to the farm; used before any coins are.
    public int FertilizerStock { get; set; }

    // The price and the farm level of this tier.
    public static int ToolPrice(int tier) => tier <= 1 ? 800 : 2500;
    public static int ToolLevel(int tier) => tier <= 1 ? 5 : 8;

    public bool BuyTool(bool hoe)
    {
        int next = (hoe ? HoeTier : CanTier) + 1;
        if (next > MaxToolTier || Level < ToolLevel(next) || Coins < ToolPrice(next)) return false;
        Coins -= ToolPrice(next);
        if (hoe) HoeTier = next; else CanTier = next;
        return true;
    }

    // The tiles around this one, nearest first.
    public static IEnumerable<int> ToolReach(int tile) =>
        new[] { tile + 1, tile - 1, tile + 2, tile - 2 }.Where(i => i >= 0 && i < TileCount);

    public bool CanFertilize(int index) => IsOpen(index) && Tiles[index].Stage is FarmStage.Seed or FarmStage.Sprout or FarmStage.Young
        && Tiles[index].Crop != FarmCrop.FocusBloom && !Tiles[index].Fertilized && (FertilizerStock > 0 || Coins >= FertilizerPrice);

    public bool Fertilize(int index, DateTime utcNow)
    {
        if (!CanFertilize(index)) return false;
        if (FertilizerStock > 0) FertilizerStock--;
        else Coins -= FertilizerPrice;
        var tile = Tiles[index];
        tile.Fertilized = true;
        if (tile.GrowsUntil is DateTime until && until > utcNow) tile.GrowsUntil = utcNow.AddSeconds((until - utcNow).TotalSeconds * FertilizedTime);
        return true;
    }

    private static double StageFor(FarmTile tile) => StageSeconds(tile.Crop!.Value) * (tile.Fertilized ? FertilizedTime : 1);

    private void NormalizeTools()
    {
        HoeTier = Math.Clamp(HoeTier, 0, MaxToolTier);
        CanTier = Math.Clamp(CanTier, 0, MaxToolTier);
        FertilizerStock = Math.Max(0, FertilizerStock);
    }
}
