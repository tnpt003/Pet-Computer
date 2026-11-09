using System;
using System.Collections.Generic;

namespace DesktopPet;

// The focus bloom (docs/FARM-GAME.md): the one crop that grows by finished focus sessions instead
// of water and the clock. Its seeds come only from sessions and are kept with the given seeds
// (OffSeasonSeeds); it counts as never in season, so shops, bulk sowing and resowing all pass it by.
public sealed partial class PetFarm
{
    public const int FocusSeedsMax = 99;

    // Stages and seeds a finished session gives: one for 25 minutes, two for 50.
    public static int FocusStages(int minutes) => minutes >= PetFocus.LongMinutes ? 2 : 1;

    // Returns the tiles whose bloom moved on.
    public IReadOnlyList<int> CompleteFocus(int minutes)
    {
        int stages = FocusStages(minutes);
        OffSeasonSeeds[nameof(FarmCrop.FocusBloom)] = Math.Min(FocusSeedsMax, OffSeasonOf(FarmCrop.FocusBloom) + stages);
        var grown = new List<int>();
        for (int i = 0; i < TileCount; i++)
        {
            var tile = Tiles[i];
            if (tile.Crop != FarmCrop.FocusBloom || tile.Stage is not (FarmStage.Seed or FarmStage.Sprout or FarmStage.Young)) continue;
            tile.Stage = (FarmStage)Math.Min((int)FarmStage.Ripe, (int)tile.Stage + stages);
            grown.Add(i);
        }
        return grown;
    }
}
