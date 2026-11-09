using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// One click for the whole field: till, sow, water or harvest every tile that is ready for it.
// Like a click on a tile it never touches the cat.
public sealed partial class PetWindow
{
    private bool FarmHandsFree => farm != null && !paused && farmLift.Phase == FarmLiftPhase.Down;

    // Only what there is work for.
    private IEnumerable<MenuItem> FarmBulkItems()
    {
        CheckFarmGrowth(FarmNow); // A crop that has just ripened counts.
        foreach (var step in new[] { FarmStep.Harvest, FarmStep.Water, FarmStep.Till })
        {
            int count = farm!.TilesAt(step).Count;
            if (count == 0) continue;
            var item = new MenuItem { Header = FarmBulkLabel(step, count) };
            var chosen = step;
            item.Click += (_, _) => FarmDoAll(chosen);
            yield return item;
        }
        int tilled = farm!.TilesAt(FarmStep.Plant).Count;
        if (tilled == 0) yield break;
        var sow = new MenuItem { Header = LF($"Gieo tất cả ({tilled} ô)", $"Sow all ({tilled} tiles)") };
        foreach (var crop in Enum.GetValues<FarmCrop>()) sow.Items.Add(FarmSowItem(crop));
        yield return sow;
    }

    private string FarmBulkLabel(FarmStep step, int count) => step switch
    {
        FarmStep.Harvest => LF($"Gặt tất cả ({count} ô)", $"Harvest all ({count} tiles)"),
        FarmStep.Water => LF($"Tưới tất cả ({count} ô)", $"Water all ({count} tiles)"),
        _ => LF($"Cày tất cả ({count} ô)", $"Till all ({count} tiles)")
    };

    // Says how many tiles the coins will really cover, and what that costs.
    private MenuItem FarmSowItem(FarmCrop crop)
    {
        int count = farm!.CanSow(crop);
        string name = FarmCropName(crop), price = FarmPriceText(count * PetFarm.SeedPrice(crop));
        string header = farm.Unlocked(crop) ? LF($"{name} · {count} ô · {price}", $"{name} · {count} tiles · {price}") : name + " · " + FarmLevelText(PetFarm.CropLevel(crop));
        var item = new MenuItem { Header = header, Icon = new FarmCropIcon(crop), IsEnabled = count > 0 };
        item.Click += (_, _) => FarmSowAll(crop);
        return item;
    }

    // Till, Water or Harvest on every tile ready for it. How many tiles were done.
    public int FarmDoAll(FarmStep step)
    {
        if (!FarmHandsFree) return 0;
        var now = FarmNow;
        foreach (int grown in farm!.Advance(now)) StartFarmEffect(grown, FarmEffect.Grow);
        var tiles = farm.TilesAt(step);
        if (tiles.Count == 0 || step is not (FarmStep.Till or FarmStep.Water or FarmStep.Harvest)) return 0;

        FarmCrop? picked = null;
        int extras = 0, golden = 0;
        foreach (int tile in tiles)
        {
            if (step == FarmStep.Till) farm.Till(tile);
            else if (step == FarmStep.Water) farm.Water(tile, now);
            else
            {
                picked = FarmHarvest(tile, out bool extra);
                if (extra) extras++;
                if (farm.LastHarvestGold) golden++;
            }
            StartFarmEffect(tile, step == FarmStep.Till ? FarmEffect.Till : step == FarmStep.Water ? FarmEffect.Water : FarmEffect.Harvest);
        }
        PlayPetSound(step == FarmStep.Till ? "ball_tap" : step == FarmStep.Water ? "swoosh" : "sparkle");
        if (picked is FarmCrop crop) ShowFarmBulkGain(tiles, crop, extras, golden);
        FarmChanged();
        return tiles.Count;
    }

    // How many tiles were sown.
    public int FarmSowAll(FarmCrop crop)
    {
        if (!FarmHandsFree) return 0;
        var tiles = farm!.SowAll(crop, FarmNow);
        if (tiles.Count == 0) return 0;
        foreach (int tile in tiles) StartFarmEffect(tile, FarmEffect.Plant);
        PlayPetSound("pop");
        FarmChanged();
        return tiles.Count;
    }

    // One label rises from the middle of what was picked; several crops have no one name.
    private void ShowFarmBulkGain(IReadOnlyList<int> tiles, FarmCrop last, int extras, int golden)
    {
        int total = tiles.Count + extras;
        string stars = golden > 0 ? $" (★{golden})" : "";
        ShowFarmLabel(tiles[tiles.Count / 2], (tiles.Count > 1 ? LF($"+{total} nông sản", $"+{total} crops") : $"+{total} " + FarmCropName(last)) + stars, FarmGainSeconds);
    }
}
