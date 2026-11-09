using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DesktopPet;

public enum FarmDecor { FlowerBed, WaterLily, BambooFence, Lantern, WoodBridge, Awning }

// What the traveling stall sells (docs/FARM-GAME.md, finishing milestone B): six things to look at.
// Each is bought once and can be put away and brought back from the store. They give nothing but looks.
// Saved by name, so a name from a newer version is dropped instead of breaking the file.
public sealed partial class PetFarm
{
    public const int DecorsPerVisit = 2;

    public static int DecorPrice(FarmDecor decor) => decor switch
    {
        FarmDecor.FlowerBed => 500,
        FarmDecor.WaterLily => 700,
        FarmDecor.BambooFence or FarmDecor.Lantern => 800,
        FarmDecor.WoodBridge => 1200,
        _ => 1400
    };

    // What was bought, and which of those are put away, by name.
    public List<string> Decors { get; set; } = new();
    public List<string> DecorsHidden { get; set; } = new();

    public bool HasDecor(FarmDecor decor) => Decors.Contains(decor.ToString());
    public bool ShowsDecor(FarmDecor decor) => HasDecor(decor) && !DecorsHidden.Contains(decor.ToString());

    // One bit for each decoration that is out, for the art.
    [JsonIgnore] public int DecorMask
    {
        get
        {
            int mask = 0;
            foreach (var name in Decors)
                if (!DecorsHidden.Contains(name) && Enum.TryParse<FarmDecor>(name, out var decor)) mask |= 1 << (int)decor;
            return mask;
        }
    }

    // Up to two the farm does not have yet, for one visit of the stall.
    public List<FarmDecor> NewDecors(Random random) =>
        Enum.GetValues<FarmDecor>().Where(decor => !HasDecor(decor)).OrderBy(_ => random.Next()).Take(DecorsPerVisit).ToList();

    public bool CanBuyDecor(FarmDecor decor) => Enum.IsDefined(decor) && !HasDecor(decor) && Coins >= DecorPrice(decor);

    public bool BuyDecor(FarmDecor decor)
    {
        if (!CanBuyDecor(decor)) return false;
        Coins -= DecorPrice(decor);
        Decors.Add(decor.ToString());
        return true;
    }

    // Puts one away or brings it back. False when the farm does not have it.
    public bool ShowDecor(FarmDecor decor, bool shown)
    {
        if (!HasDecor(decor)) return false;
        DecorsHidden.Remove(decor.ToString());
        if (!shown) DecorsHidden.Add(decor.ToString());
        return true;
    }

    private void NormalizeDecors()
    {
        Decors = (Decors ?? new()).Where(name => Enum.TryParse<FarmDecor>(name, out var decor) && Enum.IsDefined(decor)).Distinct().ToList();
        DecorsHidden = (DecorsHidden ?? new()).Where(Decors.Contains).Distinct().ToList();
    }
}
