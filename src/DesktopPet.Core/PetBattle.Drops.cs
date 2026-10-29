using System;
using System.Collections.Generic;

namespace DesktopPet;

// 1.5 M19 (docs/UPDATE-1.5.md M4, M5; docs/BATTLE-DATA.md → Đồ rơi và xu): what a defeated slime may
// leave on the floor. Fish: some HP back; coin: the Expedition run's coins (only there); potion: the
// carried skill with the longest cooldown left is ready at once. A click picks one up; left alone it
// fades and is gone. Its clock runs only in Step: Pause, a card offer and the frozen guide freeze it.
public enum BattleDropKind { Fish, Coin, Potion }

// Which drops a battle has: none (Adventure chapter 1, the free battle), fish and potions (Infinity),
// all three (an Expedition step: its coins go to the run).
public enum BattleLoot { None, Items, Coins }

public sealed class BattleDrop
{
    public int Id;
    public BattleDropKind Kind;
    public double X, Age, Life;
    // A coin's worth (1 for a slime's, more for an elite's or a boss's); unused by the others.
    public int Value;

    // How far it shows (1: fully; it fades out over the last `fade` seconds, blinking in the last 0.6 s).
    public double Opacity(double fade)
    {
        double left = Life - Age;
        if (left >= fade || fade <= 0) return 1;
        double k = Math.Clamp(left / fade, 0, 1);
        return left < 0.6 && (int)(left * 10) % 2 == 0 ? k * 0.35 : 0.35 + 0.65 * k;
    }
}

// battle.json "drops" (null: no drops anywhere). Chance: a slime's (eliteChance: an elite's) chance to
// leave one; a boss leaves bossDrops coins. Weights: of each kind ("fish", "coin", "potion"; coins only
// in an Expedition step). Life: seconds on the floor (the last fade of them fading); at most maxOnFloor
// at once (a new one replaces the oldest). fishHeal: HP; coin, eliteCoin, bossCoin: a coin's worth.
public sealed class DropSpec
{
    public double Chance { get; init; } = 0.2;
    public double EliteChance { get; init; } = 0.6;
    public int BossDrops { get; init; } = 3;
    public double Life { get; init; } = 8;
    public double Fade { get; init; } = 1.5;
    public int MaxOnFloor { get; init; } = 6;
    public Dictionary<string, double> Weights { get; init; } = new() { ["fish"] = 3, ["coin"] = 5, ["potion"] = 2 };
    public double FishHeal { get; init; } = 10;
    public int Coin { get; init; } = 1;
    public int EliteCoin { get; init; } = 2;
    public int BossCoin { get; init; } = 3;

    public static readonly string[] Kinds = { "fish", "coin", "potion" };
    public static BattleDropKind? Parse(string? id) => id switch
    {
        "fish" => BattleDropKind.Fish,
        "coin" => BattleDropKind.Coin,
        "potion" => BattleDropKind.Potion,
        _ => null
    };

    // What the battle data may hold (InvalidDataException messages name "drops").
    public static void Validate(DropSpec d)
    {
        void Within(double value, double min, double max, string name)
        {
            if (!double.IsFinite(value) || value < min || value > max)
                throw new System.IO.InvalidDataException($"drops.{name} must be {min.ToString(System.Globalization.CultureInfo.InvariantCulture)}–{max.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
        Within(d.Chance, 0, 1, "chance"); Within(d.EliteChance, 0, 1, "eliteChance");
        Within(d.BossDrops, 0, 10, "bossDrops");
        Within(d.Life, 2, 60, "life"); Within(d.Fade, 0, d.Life, "fade");
        Within(d.MaxOnFloor, 1, 20, "maxOnFloor");
        Within(d.FishHeal, 0, 200, "fishHeal");
        Within(d.Coin, 1, 100, "coin"); Within(d.EliteCoin, 1, 100, "eliteCoin"); Within(d.BossCoin, 1, 100, "bossCoin");
        if (d.Weights == null || d.Weights.Count == 0) throw new System.IO.InvalidDataException("drops.weights must name at least one kind");
        foreach (var (kind, weight) in d.Weights)
        {
            if (Parse(kind) == null) throw new System.IO.InvalidDataException($"drops.weights: \"{kind}\" must be one of: {string.Join(", ", Kinds)}");
            Within(weight, 0, 100, "weights." + kind);
        }
    }
}

public sealed partial class BattleSim
{
    private readonly List<BattleDrop> drops = new();
    public IReadOnlyList<BattleDrop> Drops => drops;
    public BattleLoot Loot { get; }
    private readonly Random dropRandom;
    private int nextDropId = 1;
    // Coins picked up this battle (a won one also gathers the coins still on the floor).
    public int Coins { get; private set; }
    public int DropsPicked { get; private set; }
    public const double DropHalfWidth = 14, DropPickHeight = 34;

    private DropSpec? DropRules => Loot != BattleLoot.None ? Settings.Drops : null;

    // A defeated enemy's drop (if the roll says so). A boss: its coins, where there are coins.
    private void DropLoot(Slime slime)
    {
        if (DropRules is not DropSpec d || slime.Decoy) return;
        if (slime.IsBoss)
        {
            if (Loot != BattleLoot.Coins) return;
            for (int i = 0; i < d.BossDrops; i++) AddDrop(BattleDropKind.Coin, slime.X + (i - (d.BossDrops - 1) / 2.0) * 30, d.BossCoin);
            return;
        }
        // One roll for whether, one for which (both always drawn, so the stream never depends on the result).
        double roll = dropRandom.NextDouble(), pick = dropRandom.NextDouble();
        // 1.5 M23b: a coin magnet makes a drop likelier.
        if (roll >= Math.Min(1, (slime.Elite != null ? d.EliteChance : d.Chance) * (1 + magnetBonus) * (1 + MutationDrops) * (1 + dropBonus))) return; // 1.5 M24: and a drops mutation; M25: Keen nose.
        if (PickDrop(d, pick) is not BattleDropKind kind) return;
        AddDrop(kind, slime.X, kind == BattleDropKind.Coin ? (slime.Elite != null ? d.EliteCoin : d.Coin) : 0);
    }

    // The kind for a roll in [0, 1): by weight, in a fixed order (coins left out where there are none).
    private BattleDropKind? PickDrop(DropSpec d, double pick)
    {
        double total = 0;
        foreach (var id in DropSpec.Kinds) total += DropWeight(d, id);
        if (total <= 0) return null;
        double at = pick * total;
        foreach (var id in DropSpec.Kinds)
        {
            double w = DropWeight(d, id);
            if (w <= 0) continue;
            if (at < w) return DropSpec.Parse(id);
            at -= w;
        }
        return null;
    }

    // 1.5 M23b: no potions with a spare battery.
    private double DropWeight(DropSpec d, string id) =>
        id == "coin" && Loot != BattleLoot.Coins || id == "potion" && NoPotions || id == "fish" && Hungry ? 0 : d.Weights.TryGetValue(id, out double w) ? w : 0;

    // How long a drop lies on the floor (1.5 M23b: shorter with a coin magnet, never under the fade).
    public double DropLife => Settings.Drops is not DropSpec d ? 0 : magnetLife > 0 ? Math.Max(d.Fade, Math.Min(d.Life, magnetLife)) : d.Life;

    internal void AddDrop(BattleDropKind kind, double x, int value)
    {
        if (DropRules is not DropSpec d) return;
        if (drops.Count >= d.MaxOnFloor) drops.RemoveAt(0);
        var drop = new BattleDrop { Id = nextDropId++, Kind = kind, X = Math.Clamp(x, Left + DropHalfWidth, Right - DropHalfWidth), Life = DropLife, Value = value };
        drops.Add(drop);
        events.Add(new(BattleEventKind.DropAppeared, drop.X, drop.Id));
    }

    // Each Step: the drops age; a drop past its life is gone.
    private void StepDrops(double dt)
    {
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            drops[i].Age += dt;
            if (drops[i].Age < drops[i].Life) continue;
            events.Add(new(BattleEventKind.DropFaded, drops[i].X, drops[i].Id));
            drops.RemoveAt(i);
        }
    }

    public BattleDrop? FindDrop(int id) { foreach (var d in drops) if (d.Id == id) return d; return null; }

    // A click on a drop: it is picked up (not while paused, choosing a card or once the battle is over).
    public bool PickUp(int id)
    {
        if (Over || Paused || ChoosingUpgrade || DropRules is not DropSpec d || FindDrop(id) is not BattleDrop drop) return false;
        drops.Remove(drop);
        DropsPicked++;
        switch (drop.Kind)
        {
            case BattleDropKind.Fish:
                double healed = Math.Min(d.FishHeal + fishCardHeal, CatMaxHp - CatHp); // 1.5 M25: Fat fish.
                if (healed > 0)
                {
                    CatHp += healed;
                    AddEffect(BattleEffectKind.Number, lastCatX, 115, -Math.Round(healed));
                }
                break;
            case BattleDropKind.Coin:
                Coins += drop.Value;
                break;
            case BattleDropKind.Potion:
                // The carried skill with the longest cooldown left (none cooling down: nothing to do).
                int longest = -1;
                foreach (var skill in carried)
                    if (cooldowns[(int)skill] > 0 && (longest < 0 || cooldowns[(int)skill] > cooldowns[longest])) longest = (int)skill;
                if (longest >= 0) cooldowns[longest] = 0;
                break;
        }
        events.Add(new(BattleEventKind.DropPicked, drop.X, (int)drop.Kind));
        return true;
    }

    // The battle ends: a win gathers the coins still on the floor; every drop is gone.
    private void ClearDrops(bool won)
    {
        if (won) foreach (var d in drops) if (d.Kind == BattleDropKind.Coin) Coins += d.Value;
        drops.Clear();
    }

    private void MapDrops(double left, double oldWidth, double newWidth)
    {
        foreach (var d in drops) d.X = left + (d.X - Left) / oldWidth * newWidth;
    }
}
