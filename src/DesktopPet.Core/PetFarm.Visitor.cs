using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

public enum FarmVisitorPhase { Away, Arriving, Open, Leaving }
public enum FarmGiftKind { Coins, Fertilizer, Seed, Decor }
public sealed record FarmGift(FarmGiftKind Kind, int Amount = 0, FarmCrop? Crop = null, FarmDecor? Decor = null);

// The neighbour's cat (docs/FARM-GAME.md, stage 4 milestone 6): its clock and what it asks for.
// It comes, stands a while, and goes. The window owns the art and when it may come.
public sealed class FarmVisitor
{
    public const double FirstSeconds = 900, MinSeconds = 1800, MaxSeconds = 3600, StaySeconds = 180, RetrySeconds = 45, RollSeconds = 1;

    public FarmVisitorPhase Phase { get; private set; }
    public double Due { get; private set; } = FirstSeconds;
    public double Left { get; private set; }
    public Dictionary<string, int> Items { get; private set; } = new();
    private double rolled;

    public bool Here => Phase is FarmVisitorPhase.Arriving or FarmVisitorPhase.Open;
    public bool Rolling => Phase is FarmVisitorPhase.Arriving or FarmVisitorPhase.Leaving;
    public double Amount => Phase switch
    {
        FarmVisitorPhase.Arriving => Smooth(Math.Clamp(rolled / RollSeconds, 0, 1)),
        FarmVisitorPhase.Open => 1,
        FarmVisitorPhase.Leaving => 1 - Smooth(Math.Clamp(rolled / RollSeconds, 0, 1)),
        _ => 0
    };

    public static double NextDelay(Random random) => MinSeconds + random.NextDouble() * (MaxSeconds - MinSeconds);
    public void Schedule(double seconds) => Due = Math.Max(0, seconds);

    public bool CountDown(double dt)
    {
        if (Phase != FarmVisitorPhase.Away) return false;
        Due = Math.Max(0, Due - dt);
        return Due <= 0;
    }

    public void Arrive(Dictionary<string, int> items)
    {
        Items = items;
        Phase = FarmVisitorPhase.Arriving;
        Left = StaySeconds;
        rolled = 0;
    }

    public void Leave()
    {
        if (!Here) return;
        Phase = FarmVisitorPhase.Leaving;
        rolled = 0;
    }

    public void Reset()
    {
        Phase = FarmVisitorPhase.Away;
        rolled = Left = 0;
    }

    // True on the frame it has gone.
    public bool Step(double dt)
    {
        if (Phase == FarmVisitorPhase.Away) return false;
        rolled += dt;
        if (Phase == FarmVisitorPhase.Arriving && rolled >= RollSeconds) { Phase = FarmVisitorPhase.Open; rolled = 0; }
        else if (Phase == FarmVisitorPhase.Open && (Left = Math.Max(0, Left - dt)) <= 0) { Phase = FarmVisitorPhase.Leaving; rolled = 0; }
        else if (Phase == FarmVisitorPhase.Leaving && rolled >= RollSeconds) { Reset(); return true; }
        return false;
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);
}

public sealed partial class PetFarm
{
    public const int VisitorMaxItems = 2, GiftFertilizerBags = 2;

    // Gifts for sowing another season's crop once, by crop name.
    public Dictionary<string, int> OffSeasonSeeds { get; set; } = new();
    public int OffSeasonOf(FarmCrop crop) => OffSeasonSeeds.GetValueOrDefault(crop.ToString());

    private IEnumerable<string> GoodsInStore() => Enum.GetValues<FarmCrop>().Where(crop => crop != FarmCrop.FocusBloom && HaveOf(crop) > 0).Select(crop => crop.ToString())
        .Concat(new[] { nameof(FarmFish.Minnow), nameof(FarmFish.Carp) }.Where(fish => GoodsOf(fish) > 0))
        .Concat(EggStock > 0 ? new[] { EggGood } : Array.Empty<string>());

    // One or two of something the store has; null when it has nothing, and then nobody comes to ask.
    public Dictionary<string, int>? NewVisitorRequest(Random random)
    {
        var goods = GoodsInStore().ToList();
        if (goods.Count == 0) return null;
        var good = goods[random.Next(goods.Count)];
        return new() { [good] = Math.Min(GoodsOf(good), 1 + random.Next(VisitorMaxItems)) };
    }

    public bool CanGive(Dictionary<string, int> items) => items.Count > 0 && items.All(item => GoodsOf(item.Key) >= item.Value);

    // Hands it over and takes what the visitor leaves behind; null when the store is short.
    public FarmGift? GiveVisitor(Dictionary<string, int> items, Random random)
    {
        if (!CanGive(items)) return null;
        TakeGoods(items);
        var gift = RollGift(random);
        switch (gift.Kind)
        {
            case FarmGiftKind.Coins: Earn(gift.Amount); break;
            case FarmGiftKind.Fertilizer: FertilizerStock += gift.Amount; break;
            case FarmGiftKind.Seed: OffSeasonSeeds[gift.Crop!.Value.ToString()] = OffSeasonOf(gift.Crop.Value) + 1; break;
            default: Decors.Add(gift.Decor!.Value.ToString()); break;
        }
        return gift;
    }

    private FarmGift RollGift(Random random)
    {
        double roll = random.NextDouble();
        var coins = new FarmGift(FarmGiftKind.Coins, 40 + Level * 10);
        var bags = new FarmGift(FarmGiftKind.Fertilizer, GiftFertilizerBags);
        if (roll < 0.4) return coins;
        if (roll < 0.7) return bags;
        if (roll < 0.9)
        {
            var other = Enum.GetValues<FarmCrop>().Where(crop => crop != FarmCrop.FocusBloom && !InSeason(crop)).ToList();
            return other.Count == 0 ? coins : new FarmGift(FarmGiftKind.Seed, 1, other[random.Next(other.Count)]);
        }
        var decors = GiftDecors.Where(decor => !HasDecor(decor)).ToList();
        return decors.Count == 0 ? bags : new FarmGift(FarmGiftKind.Decor, 1, null, decors[random.Next(decors.Count)]);
    }

    private void NormalizeVisitor() =>
        OffSeasonSeeds = (OffSeasonSeeds ?? new()).Where(pair => Enum.TryParse<FarmCrop>(pair.Key, out var crop) && (CropSeason(crop) != null || crop == FarmCrop.FocusBloom) && pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value);
}
