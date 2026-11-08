using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DesktopPet;

// One place on the order board: an order, or the time the next one comes.
public sealed class FarmOrderSlot
{
    // Crop name to how many; null: the place is empty.
    public Dictionary<string, int>? Items { get; set; }
    // UTC; null on an empty place: the next look fills it.
    public DateTime? NextAt { get; set; }
}

// Orders brought by the paper birds (docs/FARM-GAME.md, milestone C). They never expire and cost
// nothing to skip; a place filled by delivering or skipping gets a new order some time later.
public sealed partial class PetFarm
{
    public const int MaxOrderSlots = 3, MinOrderItems = 2, MaxOrderItems = 6;
    public const double OrderRefillSeconds = 20 * 60, OrderPayFactor = 1.5;

    public List<FarmOrderSlot> Orders { get; set; } = new();

    [JsonIgnore] public int OrderSlots => Level >= SlotLevel(2) ? 3 : Level >= SlotLevel(1) ? 2 : 1;
    // The farm level that opens this place on the board.
    public static int SlotLevel(int slot) => slot switch { 0 => 1, 1 => 3, _ => 5 };

    private static int OrderCap(FarmCrop crop) => crop switch
    {
        FarmCrop.Pumpkin => 2,
        FarmCrop.Tomato => 3,
        _ => MaxOrderItems
    };

    private void CleanOrders()
    {
        Orders = (Orders ?? new()).Where(slot => slot != null).Take(MaxOrderSlots).ToList();
        foreach (var slot in Orders)
        {
            if (slot.Items != null) slot.Items = CleanCounts(slot.Items);
            if (slot.Items?.Count == 0) slot.Items = null;
            slot.NextAt = slot.Items != null ? null : slot.NextAt?.ToUniversalTime();
        }
    }

    public static IEnumerable<(FarmCrop Crop, int Count)> OrderItems(FarmOrderSlot slot) =>
        Enum.GetValues<FarmCrop>().Where(crop => slot.Items?.ContainsKey(crop.ToString()) == true).Select(crop => (crop, slot.Items![crop.ToString()]));

    public static int OrderCoins(FarmOrderSlot slot) => (int)Math.Round(OrderItems(slot).Sum(item => item.Count * SellPrice(item.Crop)) * OrderPayFactor, MidpointRounding.AwayFromZero);
    public static int OrderXp(FarmOrderSlot slot) => OrderItems(slot).Sum(item => item.Count * HarvestXp(item.Crop));

    // Fills every empty place whose time has come. True when a new order arrived.
    public bool RefreshOrders(DateTime utcNow, Random random)
    {
        while (Orders.Count < OrderSlots) Orders.Add(new FarmOrderSlot());
        bool arrived = false;
        foreach (var slot in Orders)
        {
            if (slot.Items != null) continue;
            if (slot.NextAt is DateTime next)
            {
                // A clock set backwards never makes the wait longer than it should be.
                var longest = utcNow.AddSeconds(OrderRefillSeconds);
                if (next > longest) slot.NextAt = next = longest;
                if (utcNow < next) continue;
            }
            slot.Items = NewOrder(random);
            slot.NextAt = null;
            arrived = true;
        }
        return arrived;
    }

    // One or two crops the field can already grow.
    private Dictionary<string, int> NewOrder(Random random)
    {
        var crops = Enum.GetValues<FarmCrop>().Where(crop => Unlocked(crop)).ToArray();
        int total = random.Next(MinOrderItems, Math.Min(MaxOrderItems, Level + 2) + 1);
        var first = crops[random.Next(crops.Length)];
        var order = new Dictionary<string, int>();
        void Ask(FarmCrop crop, int count) => order[crop.ToString()] = Math.Min(count, OrderCap(crop));
        if (crops.Length > 1 && random.Next(2) == 0)
        {
            var others = crops.Where(crop => crop != first).ToArray();
            int part = random.Next(1, total);
            Ask(first, part);
            Ask(others[random.Next(others.Length)], total - part);
        }
        else Ask(first, total);
        return order;
    }

    public bool CanDeliver(int slot) => slot >= 0 && slot < Orders.Count && Orders[slot].Items is { } items && items.All(item => Stock.GetValueOrDefault(item.Key) >= item.Value);
    [JsonIgnore] public bool AnyOrderReady => Enumerable.Range(0, Orders.Count).Any(CanDeliver);

    // Takes the crops from the store and pays. The coins it brought, 0 when it could not be delivered.
    public int Deliver(int slot, DateTime utcNow)
    {
        if (!CanDeliver(slot)) return 0;
        var order = Orders[slot];
        int coins = OrderCoins(order), xp = OrderXp(order);
        foreach (var (crop, count) in OrderItems(order).ToList()) Take(crop, count);
        Earn(coins);
        GainXp(xp);
        EmptyOrder(order, utcNow);
        return coins;
    }

    public bool SkipOrder(int slot, DateTime utcNow)
    {
        if (slot < 0 || slot >= Orders.Count || Orders[slot].Items == null) return false;
        EmptyOrder(Orders[slot], utcNow);
        return true;
    }

    private static void EmptyOrder(FarmOrderSlot slot, DateTime utcNow)
    {
        slot.Items = null;
        slot.NextAt = utcNow.AddSeconds(OrderRefillSeconds);
    }
}
