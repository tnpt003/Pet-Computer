using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace DesktopPet;

[JsonConverter(typeof(JsonStringEnumConverter<FarmCrop>))]
public enum FarmCrop { Radish, Carrot, Tomato, Pumpkin, Watermelon, Strawberry, Corn, SweetPotato, NapaCabbage, FocusBloom }

// A tile's life: wild ground, tilled, then the three stages that each wait for water, then ripe.
[JsonConverter(typeof(JsonStringEnumConverter<FarmStage>))]
public enum FarmStage { Wild, Tilled, Seed, Sprout, Young, Ripe }

// What a click on the tile does next.
public enum FarmStep { Till, Plant, Water, Wait, Harvest, Locked }

// Cooked from the store on the hearth and eaten there (1.5.1 M6, docs/FARM-GAME.md); only the pot keeps one.
[JsonConverter(typeof(JsonStringEnumConverter<FarmDish>))]
public enum FarmDish { RadishSoup, CarrotStew, StuffedTomato, PumpkinPie, GrilledFish, CarpTomato, GoldfishHotpot, ChilledWatermelon, FriedEgg, EggTart }

// Caught in the pond (docs/FARM-GAME.md); kept in the store beside the crops.
[JsonConverter(typeof(JsonStringEnumConverter<FarmFish>))]
public enum FarmFish { Minnow, Carp, Goldfish }

public sealed class FarmTile
{
    public FarmStage Stage { get; set; }
    public FarmCrop? Crop { get; set; }
    // UTC end of the stage that was watered; null: it waits for water.
    public DateTime? GrowsUntil { get; set; }
    // What was last harvested here: the cat may till and sow it again. Tilling by hand makes the tile the user's.
    public FarmCrop? LastCrop { get; set; }
    // UTC since when it has waited for water, and whether any watering came too late for a golden crop.
    public DateTime? ThirstySince { get; set; }
    public bool LateWater { get; set; }
    public bool Fertilized { get; set; }
}

// The little field on the desktop (1.5.1 M3): six tiles, their crops and where the plot was left,
// saved in farm.json. Growth follows the real clock, so it goes on while the app is closed or in
// Pause, but only ever through the one stage that was watered. Nothing wilts: a tile just waits.
// The window owns the art and the mouse (PetWindow.Farm.cs). Gives no strength in battle.
// Its economy (1.5.1 M5): harvests go to a store, are sold for farm coins, which are not battle
// coins, and the coins buy seeds, more tiles and a scarecrow. Radish seed is free, so nobody gets stuck.
public sealed partial class PetFarm
{
    public const int CurrentVersion = 6, TileCount = 6, WateredStages = 3, MaxLevel = 10, MaxXp = 999_999;
    public const int StartingTiles = 3, StartingCoins = 20, MaxCoins = 999_999, ScarecrowPrice = 150;
    public const double FirstFraction = 0.25;
    // What the cat leaves in the store of each ingredient when it cooks by itself.
    public const int CookKeep = 4;
    // 1.5.1 M6. Rain has to last this long before it waters; each second of sun counts this much extra.
    public const double RainSoakSeconds = 15, SunBonus = 0.25;
    // The first-time hints (docs/FARM-GAME.md).
    public const string TipField = "field", TipHearth = "hearth", TipPond = "pond", TipCart = "cart";

    // Seconds from sowing to ripe when every watering comes on time.
    public static double GrowSeconds(FarmCrop crop) => crop switch
    {
        FarmCrop.Radish => 5 * 60,
        FarmCrop.Carrot => 15 * 60,
        FarmCrop.Tomato => 40 * 60,
        FarmCrop.Watermelon => 240 * 60,
        FarmCrop.Strawberry => 25 * 60,
        FarmCrop.Corn => 50 * 60,
        FarmCrop.SweetPotato => 70 * 60,
        FarmCrop.NapaCabbage => 30 * 60,
        FarmCrop.FocusBloom => 75 * 60, // Three sessions of 25 minutes; it does not grow by the clock.
        _ => 120 * 60
    };
    public static double StageSeconds(FarmCrop crop) => GrowSeconds(crop) / WateredStages;

    public static int SeedPrice(FarmCrop crop) => crop switch
    {
        FarmCrop.Radish => 0,
        FarmCrop.Carrot => 4,
        FarmCrop.Tomato => 10,
        FarmCrop.Watermelon => 50,
        FarmCrop.Strawberry => 8,
        FarmCrop.Corn => 14,
        FarmCrop.SweetPotato => 18,
        FarmCrop.NapaCabbage => 9,
        FarmCrop.FocusBloom => 0,
        _ => 25
    };

    public static int SellPrice(FarmCrop crop) => crop switch
    {
        FarmCrop.Radish => 3,
        FarmCrop.Carrot => 13,
        FarmCrop.Tomato => 30,
        FarmCrop.Watermelon => 200,
        FarmCrop.Strawberry => 22,
        FarmCrop.Corn => 40,
        FarmCrop.SweetPotato => 55,
        FarmCrop.NapaCabbage => 26,
        FarmCrop.FocusBloom => 40,
        _ => 85
    };

    // Farm levels (docs/FARM-GAME.md): the XP that reaches level 1, 2, ... 10.
    private static readonly int[] LevelXp = { 0, 25, 70, 150, 280, 450, 700, 1000, 1400, 2000 };

    public static int HarvestXp(FarmCrop crop) => crop switch
    {
        FarmCrop.Radish => 2,
        FarmCrop.Carrot => 5,
        FarmCrop.Tomato => 12,
        FarmCrop.Watermelon => 60,
        FarmCrop.Strawberry => 9,
        FarmCrop.Corn => 16,
        FarmCrop.SweetPotato => 22,
        FarmCrop.NapaCabbage => 10,
        FarmCrop.FocusBloom => 12,
        _ => 30
    };

    public static int CropLevel(FarmCrop crop) => crop switch
    {
        FarmCrop.Tomato => 2,
        FarmCrop.Pumpkin => 4,
        FarmCrop.Watermelon => 8,
        FarmCrop.Strawberry or FarmCrop.Corn or FarmCrop.SweetPotato or FarmCrop.NapaCabbage => 3,
        _ => 1
    };

    public static int DishLevel(FarmDish dish) => dish switch
    {
        FarmDish.CarrotStew => 2,
        FarmDish.StuffedTomato => 3,
        FarmDish.PumpkinPie => 5,
        FarmDish.CarpTomato => 4,
        FarmDish.GoldfishHotpot => 7,
        FarmDish.ChilledWatermelon => 9,
        FarmDish.FriedEgg => 3,
        FarmDish.EggTart => 6,
        _ => 1
    };

    public const int GoldfishLevel = 6, GoldenScarecrowLevel = MaxLevel;
    public const double GoldfishChance = 0.05, CarpChance = 0.25;
    // What the weather on show gives (docs/FARM-GAME.md, milestone 5).
    public const double SnowCarpChance = 0.5, NightGoldfishChance = 0.1, SunsetExtraChance = 0.25, PollenXpFactor = 1.5;
    public static int SellPrice(FarmFish fish) => fish switch { FarmFish.Minnow => 5, FarmFish.Carp => 15, _ => 60 };
    public static int FishXp(FarmFish fish) => fish switch { FarmFish.Minnow => 3, FarmFish.Carp => 8, _ => 25 };
    public static int FishLevel(FarmFish fish) => fish == FarmFish.Goldfish ? GoldfishLevel : 1;

    // The one fish a dish takes, or null.
    public static FarmFish? FishIn(FarmDish dish) => dish switch
    {
        FarmDish.GrilledFish => FarmFish.Minnow,
        FarmDish.CarpTomato => FarmFish.Carp,
        FarmDish.GoldfishHotpot => FarmFish.Goldfish,
        _ => null
    };

    public static int LevelReward(int level) => level * 20;

    // What opening this tile costs (the fourth, fifth and sixth).
    public static int OpenPrice(int index) => index switch { 3 => 40, 4 => 120, _ => 300 };

    public static (FarmCrop Crop, int Count)[] Recipe(FarmDish dish) => dish switch
    {
        FarmDish.RadishSoup => new[] { (FarmCrop.Radish, 2) },
        FarmDish.CarrotStew => new[] { (FarmCrop.Carrot, 2) },
        FarmDish.StuffedTomato => new[] { (FarmCrop.Tomato, 1), (FarmCrop.Radish, 1) },
        FarmDish.GrilledFish => Array.Empty<(FarmCrop, int)>(),
        FarmDish.CarpTomato => new[] { (FarmCrop.Tomato, 1) },
        FarmDish.GoldfishHotpot => new[] { (FarmCrop.Pumpkin, 1) },
        FarmDish.ChilledWatermelon => new[] { (FarmCrop.Watermelon, 1), (FarmCrop.Radish, 1) },
        FarmDish.FriedEgg => new[] { (FarmCrop.Radish, 1) },
        FarmDish.EggTart => new[] { (FarmCrop.Pumpkin, 1) },
        _ => new[] { (FarmCrop.Pumpkin, 1), (FarmCrop.Carrot, 1) }
    };

    public int Version { get; set; } = CurrentVersion;
    public List<FarmTile> Tiles { get; set; } = new();
    // How many of each crop were ever harvested, by crop name. Selling does not lower it.
    public Dictionary<string, int> Harvested { get; set; } = new();
    // What is in the store now, by crop name.
    public Dictionary<string, int> Stock { get; set; } = new();
    // The fish in the store, by name.
    public Dictionary<string, int> Fish { get; set; } = new();
    public int Coins { get; set; } = StartingCoins;
    // Tiles open from the left; a tile with anything on it is always open.
    public int OpenTiles { get; set; } = StartingTiles;
    // The top level gives the golden one, bought or not; it keeps the birds away no better.
    public bool Scarecrow { get => scarecrowBought || GoldenScarecrow; set => scarecrowBought = value; }
    private bool scarecrowBought;
    [JsonIgnore] public bool GoldenScarecrow => Level >= GoldenScarecrowLevel;
    public int Xp { get; set; }
    // In the pot, its ingredients already used: it waits there until the cat has stirred and eaten it.
    public FarmDish? PotDish { get; set; }
    // The plot's monitor (device name) and its left edge as a fraction of the free width (null: not placed yet).
    public string? Monitor { get; set; }
    public double? FractionX { get; set; }
    // Where the hearth and the pond were left, as fractions of their own free width (null: not placed yet).
    public double? HearthFractionX { get; set; }
    public double? PondFractionX { get; set; }
    // Their monitors (device names; null: the one the field is on).
    public string? HearthMonitor { get; set; }
    public string? PondMonitor { get; set; }
    // The first-time hints already given (TipField and the others): each is given once.
    public List<string> TipsSeen { get; set; } = new();

    // Written by a newer version of the pet: shown, never overwritten.
    [JsonIgnore] public bool ReadOnly { get; private set; }

    public bool TipPending(string tip) => !ReadOnly && !TipsSeen.Contains(tip);

    // True when this was the first time.
    public bool MarkTip(string tip)
    {
        if (!TipPending(tip)) return false;
        TipsSeen.Add(tip);
        return true;
    }

    public PetFarm() => Normalize();

    public static string DefaultPath => Path.Combine(PetPreferences.DataDirectory, "farm.json");

    public static PetFarm Load(string path)
    {
        var farm = JsonStore.ReadOrBackup<PetFarm>(path, "Farm");
        if (farm == null) return new();
        farm.ReadOnly = farm.Version > CurrentVersion;
        // A field from before the store: what it had grown so far goes in.
        if (farm.Version < 2) farm.Stock = new(farm.Harvested ?? new());
        bool beforeLevels = farm.Version < 3;
        if (!farm.ReadOnly) farm.Version = CurrentVersion;
        farm.Normalize();
        // A field from before the levels: what it has grown counts, without the rewards.
        if (beforeLevels) farm.Xp = Math.Min(MaxXp, Enum.GetValues<FarmCrop>().Sum(crop => farm.HarvestedCount(crop) * HarvestXp(crop)));
        // Someone who has already grown something needs no showing how.
        if (farm.Harvested.Count > 0 || farm.Tiles.Any(tile => tile.Stage is not (FarmStage.Wild or FarmStage.Tilled))) farm.MarkTip(TipField);
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
        Harvested = CleanCounts(Harvested);
        Stock = CleanCounts(Stock);
        GoldStock = CleanCounts(GoldStock);
        NormalizeCoop();
        NormalizeTools();
        NormalizeVisitor();
        Fish =(Fish ?? new()).Where(pair => Enum.TryParse<FarmFish>(pair.Key, out _) && pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);
        Coins = Math.Clamp(Coins, 0, MaxCoins);
        Xp = Math.Clamp(Xp, 0, MaxXp);
        if (PotDish is FarmDish pot && !Enum.IsDefined(pot)) PotDish = null;
        OpenTiles = Math.Clamp(Math.Max(OpenTiles, Tiles.FindLastIndex(tile => tile.Stage != FarmStage.Wild) + 1), StartingTiles, TileCount);
        if (FractionX is double x) FractionX = double.IsFinite(x) ? Math.Clamp(x, 0, 1) : null;
        if (HearthFractionX is double hx) HearthFractionX = double.IsFinite(hx) ? Math.Clamp(hx, 0, 1) : null;
        if (PondFractionX is double px) PondFractionX = double.IsFinite(px) ? Math.Clamp(px, 0, 1) : null;
        if (string.IsNullOrWhiteSpace(Monitor)) Monitor = null;
        if (string.IsNullOrWhiteSpace(HearthMonitor)) HearthMonitor = null;
        if (string.IsNullOrWhiteSpace(PondMonitor)) PondMonitor = null;
        TipsSeen = (TipsSeen ?? new()).Where(tip => !string.IsNullOrWhiteSpace(tip)).Distinct().ToList();
        NormalizeDecors();
    }

    private static Dictionary<string, int> CleanCounts(Dictionary<string, int>? counts) =>
        (counts ?? new()).Where(pair => Enum.TryParse<FarmCrop>(pair.Key, out _) && pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);

    private static void Clean(FarmTile tile)
    {
        bool growing = tile.Stage is FarmStage.Seed or FarmStage.Sprout or FarmStage.Young;
        bool hasCrop = tile.Crop is FarmCrop crop && Enum.IsDefined(crop);
        if (!Enum.IsDefined(tile.Stage) || ((growing || tile.Stage == FarmStage.Ripe) && !hasCrop)) tile.Stage = FarmStage.Wild;
        if (tile.LastCrop is FarmCrop last && !Enum.IsDefined(last)) tile.LastCrop = null;
        if (tile.Stage is FarmStage.Wild or FarmStage.Tilled) tile.Crop = null;
        if (tile.Stage is not (FarmStage.Seed or FarmStage.Sprout or FarmStage.Young)) tile.GrowsUntil = null;
        else if (tile.GrowsUntil is DateTime until) tile.GrowsUntil = until.ToUniversalTime();
        if (tile.Stage is not (FarmStage.Seed or FarmStage.Sprout or FarmStage.Young)) tile.ThirstySince = null;
        else if (tile.ThirstySince is DateTime since) tile.ThirstySince = since.ToUniversalTime();
    }

    public bool IsOpen(int index) => index >= 0 && index < OpenTiles;

    [JsonIgnore] public int Level { get { int xp = Xp; return Array.FindLastIndex(LevelXp, need => xp >= need) + 1; } }
    // How far from this level to the next, 0 to 1; 1 at the top level.
    [JsonIgnore] public double LevelProgress => Level >= MaxLevel ? 1 : (Xp - LevelXp[Level - 1]) / (double)(LevelXp[Level] - LevelXp[Level - 1]);
    // The XP the next level takes (at the top level, that of the top level).
    [JsonIgnore] public int NextLevelXp => LevelXp[Math.Min(Level, MaxLevel - 1)];
    public bool Unlocked(FarmCrop crop) => Level >= CropLevel(crop);
    public bool Unlocked(FarmDish dish) => Level >= DishLevel(dish);
    public bool Unlocked(FarmFish fish) => Level >= FishLevel(fish);

    // Levels reached since the window last asked. Each has paid its coins already.
    private int levelsGained;
    public int TakeLevelsGained()
    {
        int gained = levelsGained;
        levelsGained = 0;
        return gained;
    }

    private void GainXp(int amount)
    {
        int before = Level;
        Xp = Math.Min(MaxXp, Xp + amount);
        for (int level = before + 1; level <= Level; level++)
        {
            Earn(LevelReward(level));
            levelsGained++;
        }
    }

    public FarmStep Next(int index)
    {
        if (!IsOpen(index)) return FarmStep.Locked;
        var tile = Tiles[index];
        return tile.Stage switch
        {
            FarmStage.Wild => FarmStep.Till,
            FarmStage.Tilled => FarmStep.Plant,
            FarmStage.Ripe => FarmStep.Harvest,
            _ => tile.GrowsUntil == null && tile.Crop != FarmCrop.FocusBloom ? FarmStep.Water : FarmStep.Wait
        };
    }

    // Every tile whose next step this is, from the left.
    public IReadOnlyList<int> TilesAt(FarmStep step) => Enumerable.Range(0, TileCount).Where(i => Next(i) == step).ToList();

    // How many tilled tiles the coins at hand can sow with this crop.
    public int CanSow(FarmCrop crop)
    {
        if (!Unlocked(crop) || !InSeason(crop)) return 0;
        int tilled = TilesAt(FarmStep.Plant).Count, price = SeedPrice(crop);
        return price == 0 ? tilled : Math.Min(tilled, Coins / price);
    }

    // Sows from the left until the tiles or the coins run out. Returns the tiles sown.
    public IReadOnlyList<int> SowAll(FarmCrop crop, DateTime? utcNow = null) => TilesAt(FarmStep.Plant).Where(i => Plant(i, crop, utcNow)).ToList();

    public bool Till(int index, bool keepLastCrop = false)
    {
        if (!IsOpen(index) || Tiles[index].Stage != FarmStage.Wild) return false;
        Tiles[index].Stage = FarmStage.Tilled;
        if (!keepLastCrop) Tiles[index].LastCrop = null;
        return true;
    }

    // The crop the cat would sow here again, or null: never grown here, locked, or too dear right now.
    public FarmCrop? ResowCrop(int index) =>
        IsOpen(index) && Tiles[index].LastCrop is FarmCrop crop && Unlocked(crop) && InSeason(crop) && Coins >= SeedPrice(crop) ? crop : null;

    public bool Plant(int index, FarmCrop crop, DateTime? utcNow = null)
    {
        if (!IsOpen(index) || Tiles[index].Stage != FarmStage.Tilled || !Enum.IsDefined(crop) || !Unlocked(crop)) return false;
        if (InSeason(crop))
        {
            if (Coins < SeedPrice(crop)) return false;
            Coins -= SeedPrice(crop);
        }
        else
        {
            // Out of season only with a seed that was given, which costs nothing and is used up.
            if (OffSeasonOf(crop) <= 0) return false;
            if (OffSeasonOf(crop) == 1) OffSeasonSeeds.Remove(crop.ToString());
            else OffSeasonSeeds[crop.ToString()] = OffSeasonOf(crop) - 1;
        }
        Tiles[index].Stage = FarmStage.Seed;
        Tiles[index].Crop = crop;
        Tiles[index].GrowsUntil = null;
        Tiles[index].ThirstySince = utcNow;
        Tiles[index].LateWater = Tiles[index].Fertilized = false;
        return true;
    }

    public bool Water(int index, DateTime utcNow)
    {
        if (!IsOpen(index) || Next(index) != FarmStep.Water) return false;
        var tile = Tiles[index];
        tile.LateWater |= !WateredOnTime(tile, utcNow);
        tile.ThirstySince = null;
        tile.GrowsUntil = utcNow.AddSeconds(StageFor(tile));
        return true;
    }

    // The crop picked, or null when the tile is not ripe. The tile goes back to wild ground.
    // Extra: one more for the store (a sunset's gift). Pollen: more XP for it. Random: rolls for a golden crop.
    public FarmCrop? Harvest(int index, bool extra = false, bool pollen = false, Random? random = null)
    {
        if (!IsOpen(index) || Tiles[index].Stage != FarmStage.Ripe) return null;
        var crop = Tiles[index].Crop!.Value;
        LastHarvestGold = ComesUpGolden(Tiles[index], random);
        Tiles[index].Stage = FarmStage.Wild;
        Tiles[index].Crop = null;
        Tiles[index].GrowsUntil = Tiles[index].ThirstySince = null;
        Tiles[index].LateWater = Tiles[index].Fertilized = false;
        Tiles[index].LastCrop = crop;
        Harvested[crop.ToString()] = Harvested.GetValueOrDefault(crop.ToString()) + 1;
        if (LastHarvestGold) GoldStock[crop.ToString()] = GoldOf(crop) + 1;
        int plain = (extra ? 2 : 1) - (LastHarvestGold ? 1 : 0);
        if (plain > 0) Stock[crop.ToString()] = StockOf(crop) + plain;
        GainXp(pollen ? (int)Math.Ceiling(HarvestXp(crop) * PollenXpFactor) : HarvestXp(crop));
        return crop;
    }

    public int HarvestedCount(FarmCrop crop) => Harvested.GetValueOrDefault(crop.ToString());
    public int StockOf(FarmCrop crop) => Stock.GetValueOrDefault(crop.ToString());
    public int FishOf(FarmFish fish) => Fish.GetValueOrDefault(fish.ToString());
    public int StockValue => Enum.GetValues<FarmCrop>().Sum(crop => StockOf(crop) * SellPrice(crop) + GoldOf(crop) * GoldSellPrice(crop)) + Enum.GetValues<FarmFish>().Sum(fish => FishOf(fish) * SellPrice(fish)) + EggStock * EggPrice;
    // The next tile to open, or -1 when all are.
    public int NextLockedTile => OpenTiles < TileCount ? OpenTiles : -1;

    // Sells one of this crop. The coins it brought, 0 when there was none.
    public int Sell(FarmCrop crop)
    {
        if (StockOf(crop) <= 0) return 0;
        Take(crop, 1);
        return Earn(SellPrice(crop));
    }

    public int Sell(FarmFish fish)
    {
        if (FishOf(fish) <= 0) return 0;
        TakeFish(fish);
        return Earn(SellPrice(fish));
    }

    // What this cast brings up. Before the golden fish is opened, it would have been a carp.
    public FarmFish RollFish(Random random, WeatherKind? weather = null)
    {
        double roll = random.NextDouble(), gold = weather == WeatherKind.Night ? NightGoldfishChance : GoldfishChance, carp = weather == WeatherKind.Snow ? SnowCarpChance : CarpChance;
        if (roll < gold) return Unlocked(FarmFish.Goldfish) ? FarmFish.Goldfish : FarmFish.Carp;
        return roll < gold + carp ? FarmFish.Carp : FarmFish.Minnow;
    }

    public void Catch(FarmFish fish)
    {
        Fish[fish.ToString()] = FishOf(fish) + 1;
        GainXp(FishXp(fish));
    }

    private void TakeFish(FarmFish fish)
    {
        int left = FishOf(fish) - 1;
        if (left <= 0) Fish.Remove(fish.ToString());
        else Fish[fish.ToString()] = left;
    }

    // The plain ones first; golden ones only for what is still missing.
    private void Take(FarmCrop crop, int count)
    {
        int plain = StockOf(crop), left = plain - count;
        if (left <= 0) Stock.Remove(crop.ToString());
        else Stock[crop.ToString()] = left;
        if (count > plain) TakeGold(crop, count - plain);
    }

    public bool CanCook(FarmDish dish) => Enum.IsDefined(dish) && Unlocked(dish) && Recipe(dish).All(part => HaveOf(part.Crop) >= part.Count)
        && (FishIn(dish) is not FarmFish fish || FishOf(fish) >= 1) && EggStock >= EggsIn(dish);

    // Uses up the ingredients, all of them or none.
    public bool Cook(FarmDish dish)
    {
        if (!CanCook(dish)) return false;
        foreach (var (crop, count) in Recipe(dish)) Take(crop, count);
        if (FishIn(dish) is FarmFish fish) TakeFish(fish);
        EggStock -= EggsIn(dish);
        return true;
    }

    // Into the pot, if the pot is empty and the store has it all.
    public bool Pour(FarmDish dish)
    {
        if (PotDish != null || !Cook(dish)) return false;
        PotDish = dish;
        return true;
    }

    public FarmDish? TakePot()
    {
        var dish = PotDish;
        PotDish = null;
        return dish;
    }

    // What the cat would cook by itself: the best dish that still leaves CookKeep of each ingredient, or null.
    // The golden fish is the user's to spend.
    public FarmDish? AutoDish() => Enum.GetValues<FarmDish>().OrderByDescending(DishLevel)
        .Where(dish => Unlocked(dish) && FishIn(dish) != FarmFish.Goldfish && (FishIn(dish) is not FarmFish fish || FishOf(fish) - 1 >= CookKeep)
            && EggStock - EggsIn(dish) >= (EggsIn(dish) > 0 ? CookKeep : 0) && Recipe(dish).All(part => StockOf(part.Crop) - part.Count >= CookKeep))
        .Cast<FarmDish?>().FirstOrDefault();

    // Empties the store. The coins it brought.
    public int SellAll()
    {
        int value = StockValue;
        Stock.Clear();
        GoldStock.Clear();
        Fish.Clear();
        EggStock = 0;
        return Earn(value);
    }

    private int Earn(int amount)
    {
        Coins = Math.Min(MaxCoins, Coins + amount);
        return amount;
    }

    public bool OpenNextTile()
    {
        if (NextLockedTile < 0 || Coins < OpenPrice(NextLockedTile)) return false;
        Coins -= OpenPrice(NextLockedTile);
        OpenTiles++;
        return true;
    }

    public bool BuyScarecrow()
    {
        if (Scarecrow || Coins < ScarecrowPrice) return false;
        Coins -= ScarecrowPrice;
        Scarecrow = true;
        return true;
    }

    // Rain does what the watering can does, on every open tile waiting for it. Returns those tiles.
    public IReadOnlyList<int> RainWater(DateTime utcNow) => Enumerable.Range(0, TileCount).Where(i => Water(i, utcNow)).ToList();

    // Brings every growing stage's end closer. Seconds of sun seen, not of the clock. False when nothing grows.
    public bool Sunshine(double seconds)
    {
        bool any = false;
        foreach (var tile in Tiles)
        {
            if (tile.GrowsUntil is not DateTime until) continue;
            tile.GrowsUntil = until.AddSeconds(-seconds * SunBonus);
            any = true;
        }
        return any;
    }

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
            tile.ThirstySince = tile.Stage == FarmStage.Ripe ? null : until;
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
        double within = tile.GrowsUntil == null ? 0 : 1 - Remaining(index, utcNow).TotalSeconds / StageFor(tile);
        return (tile.Stage - FarmStage.Seed + Math.Clamp(within, 0, 1)) / WateredStages;
    }
}

// Pieces of land share one floor and do not stand on each other.
public static class FarmLands
{
    // The nearest place to x, between min and max, that leaves this gap from all that is taken. x itself when nothing fits.
    public static double FreeSpot(double x, double width, IReadOnlyList<(double Left, double Right)> taken, double min, double max, double gap)
    {
        bool Free(double at) => at >= min - 0.01 && at <= max + 0.01 && taken.All(land => at + width <= land.Left - gap + 0.5 || at >= land.Right + gap - 0.5);
        double wanted = Math.Clamp(x, min, Math.Max(min, max));
        if (Free(wanted)) return wanted;
        var spots = taken.SelectMany(land => new[] { land.Left - gap - width, land.Right + gap }).Where(Free).ToList();
        return spots.Count == 0 ? wanted : spots.OrderBy(at => Math.Abs(at - wanted)).First();
    }

    // Where something this wide may stand: each free stretch as the range of its left edge.
    private static List<(double From, double To)> FreeRanges(double width, IReadOnlyList<(double Left, double Right)> taken, double min, double max, double gap)
    {
        var open = new List<(double From, double To)>();
        double from = min;
        foreach (var land in taken.OrderBy(land => land.Left))
        {
            double to = Math.Min(max, land.Left - gap - width);
            if (to >= from) open.Add((from, to));
            from = Math.Max(from, land.Right + gap);
        }
        if (max >= from) open.Add((from, max));
        return open;
    }

    // Anywhere with room, each free stretch as likely as its length; null when nothing fits.
    public static double? RandomFreeSpot(Random random, double width, IReadOnlyList<(double Left, double Right)> taken, double min, double max, double gap)
    {
        var open = FreeRanges(width, taken, min, max, gap);
        if (open.Count == 0) return null;
        double pick = random.NextDouble() * open.Sum(range => range.To - range.From);
        foreach (var (from, to) in open)
        {
            if (pick <= to - from) return from + pick;
            pick -= to - from;
        }
        return open[^1].To;
    }

    // The way in to a place: from the roomier side, and no farther than that room, so it never passes over land.
    public static (int Side, double Room) RollWay(double x, double width, IReadOnlyList<(double Left, double Right)> taken, double min, double max, double gap, double roll)
    {
        double low = taken.Where(land => land.Right <= x).Select(land => land.Right + gap).DefaultIfEmpty(min).Max();
        double high = taken.Where(land => land.Left >= x + width).Select(land => land.Left - gap - width).DefaultIfEmpty(max).Min();
        double left = Math.Max(0, x - Math.Max(min, low)), right = Math.Max(0, Math.Min(max, high) - x);
        return right >= left ? (1, Math.Min(roll, right)) : (-1, Math.Min(roll, left));
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
