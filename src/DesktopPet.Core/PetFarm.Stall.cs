using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DesktopPet;

// One thing the traveling stall wants to buy: crop and fish names to how many.
public sealed class FarmOffer
{
    public Dictionary<string, int> Items { get; init; } = new();
    public bool Done { get; set; }
}

// What the traveling stall asks for (docs/FARM-GAME.md, milestone 4). Offers last only as long as
// the visit, are not saved, and cost nothing to leave alone.
public sealed partial class PetFarm
{
    public const int MaxOfferSlots = 3, MinOfferItems = 2, MaxOfferItems = 6;
    public const double OfferPayFactor = 1.5;

    [JsonIgnore] public int OfferSlots => Level >= SlotLevel(2) ? 3 : Level >= SlotLevel(1) ? 2 : 1;
    // The farm level that opens this offer.
    public static int SlotLevel(int slot) => slot switch { 0 => 1, 1 => 3, _ => 5 };

    private static int OfferCap(string good) => good switch
    {
        nameof(FarmCrop.Watermelon) => 1,
        nameof(FarmCrop.Pumpkin) => 2,
        nameof(FarmCrop.Tomato) or nameof(FarmFish.Carp) or EggGood => 3,
        _ => MaxOfferItems
    };

    private static int GoodPrice(string good) => Enum.TryParse<FarmCrop>(good, out var crop) ? SellPrice(crop) : Enum.TryParse<FarmFish>(good, out var fish) ? SellPrice(fish) : good == EggGood ? EggPrice : 0;
    private static int GoodXp(string good) => Enum.TryParse<FarmCrop>(good, out var crop) ? HarvestXp(crop) : Enum.TryParse<FarmFish>(good, out var fish) ? FishXp(fish) : good == EggGood ? EggXp : 0;
    public int GoodsOf(string good) => Enum.TryParse<FarmCrop>(good, out var crop) ? HaveOf(crop) : Enum.TryParse<FarmFish>(good, out var fish) ? FishOf(fish) : good == EggGood ? EggStock : 0;

    public static int OfferCoins(FarmOffer offer) => (int)Math.Round(offer.Items.Sum(item => item.Value * GoodPrice(item.Key)) * OfferPayFactor, MidpointRounding.AwayFromZero);
    public static int OfferXp(FarmOffer offer) => offer.Items.Sum(item => item.Value * GoodXp(item.Key));

    public List<FarmOffer> NewOffers(Random random) => Enumerable.Range(0, OfferSlots).Select(_ => NewOffer(random)).ToList();

    // One or two goods the farm can already produce; the golden fish is never asked for.
    private FarmOffer NewOffer(Random random)
    {
        var goods = Enum.GetValues<FarmCrop>().Where(crop => Unlocked(crop)).Select(crop => crop.ToString())
            .Concat(new[] { nameof(FarmFish.Minnow), nameof(FarmFish.Carp) }).Concat(HasCoop ? new[] { EggGood } : Array.Empty<string>()).ToArray();
        int total = random.Next(MinOfferItems, Math.Min(MaxOfferItems, Level + 2) + 1);
        var first = goods[random.Next(goods.Length)];
        var offer = new FarmOffer();
        void Ask(string good, int count) => offer.Items[good] = Math.Min(count, OfferCap(good));
        var others = goods.Where(good => good != first).ToArray();
        if (random.Next(2) == 0)
        {
            int part = random.Next(1, total);
            Ask(first, part);
            Ask(others[random.Next(others.Length)], total - part);
        }
        else
        {
            Ask(first, total);
            // An offer is at least two goods, and a watermelon comes one at a time: it needs company.
            if (offer.Items[first] < MinOfferItems) Ask(others[random.Next(others.Length)], total - offer.Items[first]);
        }
        return offer;
    }

    public bool CanDeliver(FarmOffer offer) => !offer.Done && offer.Items.All(item => GoodsOf(item.Key) >= item.Value);

    // Takes the goods from the store and pays. The coins it brought, 0 when it could not be delivered.
    public int Deliver(FarmOffer offer)
    {
        if (!CanDeliver(offer)) return 0;
        int coins = OfferCoins(offer), xp = OfferXp(offer);
        foreach (var (good, count) in offer.Items)
        {
            if (Enum.TryParse<FarmCrop>(good, out var crop)) Take(crop, count);
            else if (Enum.TryParse<FarmFish>(good, out var fish)) for (int i = 0; i < count; i++) TakeFish(fish);
            else if (good == EggGood) EggStock -= count;
        }
        Earn(coins);
        GainXp(xp);
        offer.Done = true;
        return coins;
    }
}

public enum FarmStallPhase { Away, Arriving, Open, Leaving }

// The traveling stall's clock: when it comes, how long it stays and how far it has rolled.
// The window owns the art, the place it stands and the rules for when it may come (PetWindow.FarmStall.cs).
public sealed class FarmStall
{
    public const double FirstSeconds = 600, MinSeconds = 1200, MaxSeconds = 2400, StaySeconds = 300, RetrySeconds = 45, RollSeconds = 1.2;

    public FarmStallPhase Phase { get; private set; }
    public double Due { get; private set; } = FirstSeconds;
    // Seconds of the visit left.
    public double Left { get; private set; }
    private double rolled;
    // Kept after it leaves: a panel still open may finish delivering.
    public List<FarmOffer> Offers { get; private set; } = new();
    // What this visit has for sale; kept as long as the offers are.
    public List<FarmDecor> Decors { get; private set; } = new();

    public bool Here => Phase is FarmStallPhase.Arriving or FarmStallPhase.Open;
    public bool Rolling => Phase is FarmStallPhase.Arriving or FarmStallPhase.Leaving;
    // 0 out of sight, 1 standing in its place.
    public double Amount => Phase switch
    {
        FarmStallPhase.Arriving => Smooth(Math.Clamp(rolled / RollSeconds, 0, 1)),
        FarmStallPhase.Open => 1,
        FarmStallPhase.Leaving => 1 - Smooth(Math.Clamp(rolled / RollSeconds, 0, 1)),
        _ => 0
    };

    public static double NextDelay(Random random) => MinSeconds + random.NextDouble() * (MaxSeconds - MinSeconds);
    public void Schedule(double seconds) => Due = Math.Max(0, seconds);

    // Counts down only while it is away. True on the frame the next visit is due: the caller decides, and always calls Schedule.
    public bool CountDown(double dt)
    {
        if (Phase != FarmStallPhase.Away) return false;
        Due = Math.Max(0, Due - dt);
        return Due <= 0;
    }

    public void Arrive(List<FarmOffer> offers, List<FarmDecor>? decors = null)
    {
        Offers = offers;
        Decors = decors ?? new();
        Phase = FarmStallPhase.Arriving;
        Left = StaySeconds;
        rolled = 0;
    }

    public void Reset()
    {
        Phase = FarmStallPhase.Away;
        rolled = Left = 0;
    }

    // True on the frame it has rolled away.
    public bool Step(double dt)
    {
        if (Phase == FarmStallPhase.Away) return false;
        rolled += dt;
        if (Phase == FarmStallPhase.Arriving && rolled >= RollSeconds) { Phase = FarmStallPhase.Open; rolled = 0; }
        else if (Phase == FarmStallPhase.Open && (Left = Math.Max(0, Left - dt)) <= 0) { Phase = FarmStallPhase.Leaving; rolled = 0; }
        else if (Phase == FarmStallPhase.Leaving && rolled >= RollSeconds) { Reset(); return true; }
        return false;
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);
}
