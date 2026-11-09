using System;
using System.Text.Json.Serialization;

namespace DesktopPet;

public enum FarmSeason { Spring, Summer, Autumn, Winter }

// The seasons (docs/FARM-GAME.md, stage 4 milestone 4): by the month on this computer, northern
// style. Each has one crop that can only be sown in it. What is already growing goes on growing.
public sealed partial class PetFarm
{
    public static FarmSeason SeasonOf(int month) => month switch
    {
        3 or 4 or 5 => FarmSeason.Spring,
        6 or 7 or 8 => FarmSeason.Summer,
        9 or 10 or 11 => FarmSeason.Autumn,
        _ => FarmSeason.Winter
    };

    // The one season this crop can be sown in; null: any time.
    public static FarmSeason? CropSeason(FarmCrop crop) => crop switch
    {
        FarmCrop.Strawberry => FarmSeason.Spring,
        FarmCrop.Corn => FarmSeason.Summer,
        FarmCrop.SweetPotato => FarmSeason.Autumn,
        FarmCrop.NapaCabbage => FarmSeason.Winter,
        _ => null
    };

    // Set by the window from its clock; not saved.
    [JsonIgnore] public FarmSeason Season { get; set; } = SeasonOf(DateTime.Now.Month);

    public bool InSeason(FarmCrop crop) => CropSeason(crop) is not FarmSeason season || season == Season;
}
