using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace DesktopPet;

[JsonConverter(typeof(JsonStringEnumConverter<FarmCrop>))]
public enum FarmCrop { Radish, Carrot, Tomato, Pumpkin }

// A tile's life: wild ground, tilled, then the three stages that each wait for water, then ripe.
[JsonConverter(typeof(JsonStringEnumConverter<FarmStage>))]
public enum FarmStage { Wild, Tilled, Seed, Sprout, Young, Ripe }

// What a click on the tile does next.
public enum FarmStep { Till, Plant, Water, Wait, Harvest }

public sealed class FarmTile
{
    public FarmStage Stage { get; set; }
    public FarmCrop? Crop { get; set; }
    // UTC end of the stage that was watered; null: it waits for water.
    public DateTime? GrowsUntil { get; set; }
}

// The little field on the desktop (1.5.1 M3): six tiles, their crops and where the plot was left,
// saved in farm.json. Growth follows the real clock, so it goes on while the app is closed or in
// Pause, but only ever through the one stage that was watered. Nothing wilts: a tile just waits.
// The window owns the art and the mouse (PetWindow.Farm.cs). Gives no strength in battle.
public sealed class PetFarm
{
    public const int CurrentVersion = 1, TileCount = 6, WateredStages = 3;
    public const double FirstFraction = 0.25;

    // Seconds from sowing to ripe when every watering comes on time.
    public static double GrowSeconds(FarmCrop crop) => crop switch
    {
        FarmCrop.Radish => 5 * 60,
        FarmCrop.Carrot => 15 * 60,
        FarmCrop.Tomato => 40 * 60,
        _ => 120 * 60
    };
    public static double StageSeconds(FarmCrop crop) => GrowSeconds(crop) / WateredStages;

    public int Version { get; set; } = CurrentVersion;
    public List<FarmTile> Tiles { get; set; } = new();
    // How many of each crop were ever harvested, by crop name (the store of 1.5.1 M5 starts from this).
    public Dictionary<string, int> Harvested { get; set; } = new();
    // The plot's monitor (device name) and its left edge as a fraction of the free width (null: not placed yet).
    public string? Monitor { get; set; }
    public double? FractionX { get; set; }

    // Written by a newer version of the pet: shown, never overwritten.
    [JsonIgnore] public bool ReadOnly { get; private set; }

    public PetFarm() => Normalize();

    public static string DefaultPath => Path.Combine(PetPreferences.DataDirectory, "farm.json");

    public static PetFarm Load(string path)
    {
        var farm = JsonStore.ReadOrBackup<PetFarm>(path, "Farm");
        if (farm == null) return new();
        farm.ReadOnly = farm.Version > CurrentVersion;
        if (!farm.ReadOnly) farm.Version = CurrentVersion;
        farm.Normalize();
        return farm;
    }

    public bool Save(string path)
    {
        if (ReadOnly) return false;
        return JsonStore.Write(path, this, "Farm", indented: true, backup: true);
    }

    private void Normalize()
    {
        Tiles = (Tiles ?? new()).Where(tile => tile != null).Take(TileCount).ToList();
        while (Tiles.Count < TileCount) Tiles.Add(new FarmTile());
        foreach (var tile in Tiles) Clean(tile);
        Harvested = (Harvested ?? new()).Where(pair => Enum.TryParse<FarmCrop>(pair.Key, out _) && pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);
        if (FractionX is double x) FractionX = double.IsFinite(x) ? Math.Clamp(x, 0, 1) : null;
        if (string.IsNullOrWhiteSpace(Monitor)) Monitor = null;
    }

    private static void Clean(FarmTile tile)
    {
        bool growing = tile.Stage is FarmStage.Seed or FarmStage.Sprout or FarmStage.Young;
        bool hasCrop = tile.Crop is FarmCrop crop && Enum.IsDefined(crop);
        if (!Enum.IsDefined(tile.Stage) || ((growing || tile.Stage == FarmStage.Ripe) && !hasCrop)) tile.Stage = FarmStage.Wild;
        if (tile.Stage is FarmStage.Wild or FarmStage.Tilled) tile.Crop = null;
        if (tile.Stage is not (FarmStage.Seed or FarmStage.Sprout or FarmStage.Young)) tile.GrowsUntil = null;
        else if (tile.GrowsUntil is DateTime until) tile.GrowsUntil = until.ToUniversalTime();
    }

    private bool Valid(int index) => index >= 0 && index < TileCount;

    public FarmStep Next(int index)
    {
        var tile = Tiles[index];
        return tile.Stage switch
        {
            FarmStage.Wild => FarmStep.Till,
            FarmStage.Tilled => FarmStep.Plant,
            FarmStage.Ripe => FarmStep.Harvest,
            _ => tile.GrowsUntil == null ? FarmStep.Water : FarmStep.Wait
        };
    }

    public bool Till(int index)
    {
        if (!Valid(index) || Tiles[index].Stage != FarmStage.Wild) return false;
        Tiles[index].Stage = FarmStage.Tilled;
        return true;
    }

    public bool Plant(int index, FarmCrop crop)
    {
        if (!Valid(index) || Tiles[index].Stage != FarmStage.Tilled || !Enum.IsDefined(crop)) return false;
        Tiles[index].Stage = FarmStage.Seed;
        Tiles[index].Crop = crop;
        Tiles[index].GrowsUntil = null;
        return true;
    }

    public bool Water(int index, DateTime utcNow)
    {
        if (!Valid(index) || Next(index) != FarmStep.Water) return false;
        Tiles[index].GrowsUntil = utcNow.AddSeconds(StageSeconds(Tiles[index].Crop!.Value));
        return true;
    }

    // The crop picked, or null when the tile is not ripe. The tile goes back to wild ground.
    public FarmCrop? Harvest(int index)
    {
        if (!Valid(index) || Tiles[index].Stage != FarmStage.Ripe) return null;
        var crop = Tiles[index].Crop!.Value;
        Tiles[index].Stage = FarmStage.Wild;
        Tiles[index].Crop = null;
        Tiles[index].GrowsUntil = null;
        Harvested[crop.ToString()] = Harvested.GetValueOrDefault(crop.ToString()) + 1;
        return crop;
    }

    public int HarvestedCount(FarmCrop crop) => Harvested.GetValueOrDefault(crop.ToString());

    // Moves on every tile whose watered stage is over. A clock set backwards never makes a stage
    // longer than it should be. Returns the indexes that changed stage.
    public IReadOnlyList<int> Advance(DateTime utcNow)
    {
        List<int>? changed = null;
        for (int i = 0; i < TileCount; i++)
        {
            var tile = Tiles[i];
            if (tile.GrowsUntil is not DateTime until) continue;
            var longest = utcNow.AddSeconds(StageSeconds(tile.Crop!.Value));
            if (until > longest) tile.GrowsUntil = until = longest;
            if (utcNow < until) continue;
            tile.GrowsUntil = null;
            tile.Stage++;
            (changed ??= new()).Add(i);
        }
        return (IReadOnlyList<int>?)changed ?? Array.Empty<int>();
    }

    // Time left of the watered stage (zero when it waits for water).
    public TimeSpan Remaining(int index, DateTime utcNow)
    {
        if (Tiles[index].GrowsUntil is not DateTime until) return TimeSpan.Zero;
        return until > utcNow ? until - utcNow : TimeSpan.Zero;
    }

    // 0 at sowing, 1 when ripe, across the three watered stages.
    public double Progress(int index, DateTime utcNow)
    {
        var tile = Tiles[index];
        if (tile.Stage == FarmStage.Ripe) return 1;
        if (tile.Stage is not (FarmStage.Seed or FarmStage.Sprout or FarmStage.Young)) return 0;
        double within = tile.GrowsUntil == null ? 0 : 1 - Remaining(index, utcNow).TotalSeconds / StageSeconds(tile.Crop!.Value);
        return (tile.Stage - FarmStage.Seed + Math.Clamp(within, 0, 1)) / WateredStages;
    }
}

public enum FarmLiftPhase { Down, Rising, Floating, Falling }

// How the plot moves while the user carries it: it strains on the ground, shaking, is hauled up as
// if by an invisible force, bobs in the air, and on the way back hangs for a moment and slams down.
public sealed class FarmLift
{
    public const double FloatHeight = 44, BobHeight = 3;
    public const double GatherSeconds = 0.5, PullSeconds = 0.8, RiseSeconds = GatherSeconds + PullSeconds;
    public const double HangSeconds = 0.15, DropSeconds = 0.4, FallSeconds = HangSeconds + DropSeconds, MaxGlideSeconds = 0.6;
    public const double BounceSeconds = 0.4, DustSeconds = 0.8, ShedSeconds = 3;
    private const double BobSpeed = 2.3, HangRise = 3, BounceHeight = 7;

    public FarmLiftPhase Phase { get; private set; }
    // 1 on the landing frame, down to 0 (dirt thrown out, dust, the two small bounces).
    public double Dust { get; private set; }
    // Seconds since the plot began to rise (its crumbs are still falling), or -1.
    public double Shed { get; private set; } = -1;
    private double time, fallFrom, glide;

    public bool Busy => Phase != FarmLiftPhase.Down || Dust > 0 || Shed >= 0;
    public bool Floating => Phase == FarmLiftPhase.Floating;
    // How far it has slid towards where it lands, 0 to 1. Dropped on the spot, it is there already.
    public double FallAmount => Phase != FarmLiftPhase.Falling ? 0 : glide > 0 ? Smooth(Math.Clamp(time / glide, 0, 1)) : 1;

    private double SinceLanding => (1 - Dust) * DustSeconds;
    private double Bounce => Math.Clamp(SinceLanding / BounceSeconds, 0, 1);
    private bool Bouncing => Phase == FarmLiftPhase.Down && Dust > 0 && Bounce < 1;
    private double AfterGlide => time - glide;
    private double Pull => Math.Clamp((time - GatherSeconds) / PullSeconds, 0, 1);

    public double Height => Phase switch
    {
        FarmLiftPhase.Rising => RiseHeightAt(time),
        FarmLiftPhase.Floating => FloatHeight + Math.Sin(time * BobSpeed) * BobHeight,
        FarmLiftPhase.Falling when AfterGlide < 0 => fallFrom,
        FarmLiftPhase.Falling when AfterGlide < HangSeconds => fallFrom + Math.Sin(AfterGlide / HangSeconds * Math.PI) * HangRise,
        FarmLiftPhase.Falling => fallFrom * (1 - Math.Pow(Math.Clamp((AfterGlide - HangSeconds) / DropSeconds, 0, 1), 2)),
        _ => Bouncing ? BounceHeight * Math.Abs(Math.Sin(Bounce * Math.PI * 2)) * (1 - Bounce) : 0
    };

    // Sideways, in DIPs: the strain of rising, the moment before it falls, the blow of landing.
    public double ShakeX
    {
        get
        {
            double clock = Phase == FarmLiftPhase.Down ? SinceLanding : time;
            return Tremble * (Math.Sin(clock * 74) + 0.5 * Math.Sin(clock * 117)) / 1.5;
        }
    }

    private double Tremble => Phase switch
    {
        FarmLiftPhase.Rising => time < GatherSeconds ? 0.6 + 2.8 * time / GatherSeconds : 0.3 + 3.1 * (1 - Pull),
        FarmLiftPhase.Falling => AfterGlide >= 0 && AfterGlide < HangSeconds ? 1.2 : 0,
        _ => Bouncing ? 4 * (1 - Bounce) * (1 - Bounce) : 0
    };

    // Degrees: it rocks on its way up.
    public double Tilt => Phase != FarmLiftPhase.Rising ? 0
        : time < GatherSeconds ? Math.Sin(time * 41) * 0.35 * time / GatherSeconds
        : Math.Sin(time * 6.5) * 1.5 * (1 - Pull);

    // 1 as it hits the ground, again (less) at the second touch, 0 otherwise.
    public double Squash => Bouncing ? Math.Pow(Math.Abs(Math.Cos(Bounce * Math.PI * 2)), 3) * (1 - Bounce) * (1 - Bounce) : 0;

    // How high the plot was this long after it began to rise: where a crumb that left it then began.
    public static double RiseHeightAt(double seconds)
    {
        if (seconds <= GatherSeconds) return 0;
        double pull = Math.Min(1, (seconds - GatherSeconds) / PullSeconds);
        return FloatHeight * (Smooth(pull) + 0.05 * Math.Sin(pull * Math.PI * 6) * (1 - pull));
    }

    public void Begin()
    {
        if (Phase != FarmLiftPhase.Down) return;
        Phase = FarmLiftPhase.Rising;
        time = Dust = 0;
        Shed = 0;
    }

    // Glide: seconds it spends sliding along in the air before it falls (going back to its old place).
    public void Drop(double glideSeconds = 0)
    {
        if (Phase is not (FarmLiftPhase.Rising or FarmLiftPhase.Floating)) return;
        fallFrom = Height;
        glide = Math.Max(0, glideSeconds);
        Phase = FarmLiftPhase.Falling;
        time = 0;
    }

    public void Reset()
    {
        Phase = FarmLiftPhase.Down;
        time = Dust = 0;
        Shed = -1;
    }

    // True on the frame the plot touches the ground.
    public bool Step(double dt)
    {
        Dust = Math.Max(0, Dust - dt / DustSeconds);
        if (Shed >= 0) { Shed += dt; if (Shed > ShedSeconds) Shed = -1; }
        if (Phase == FarmLiftPhase.Down) return false;
        time += dt;
        if (Phase == FarmLiftPhase.Rising && time >= RiseSeconds)
        {
            Phase = FarmLiftPhase.Floating;
            time = 0;
        }
        else if (Phase == FarmLiftPhase.Falling && AfterGlide >= FallSeconds)
        {
            Phase = FarmLiftPhase.Down;
            time = 0;
            Dust = 1;
            return true;
        }
        return false;
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);
}
