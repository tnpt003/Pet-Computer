using System;
using System.Collections.Generic;

namespace DesktopPet;

// 1.5 M20 (docs/UPDATE-1.5.md T1; docs/BATTLE-DATA.md → Bảo vệ giỏ cá): a stage with a fish basket. The
// basket stands on the floor with its fish; every n-th plain slime that comes in goes for it instead of the
// cat, takes a fish (a moment at the basket) and runs with it for the edge away from the cat: there it gets
// away and the fish is lost. Defeated, dazed or grabbed on the way, it drops the fish: a click puts it back,
// left alone it hops back after a while (another taker may pick it up first). The cat goes for a slime
// carrying a fish first. Every fish lost loses the battle, whatever HP the cat has; a won battle keeps the
// fish still carried or lying. No random stream is used; a stage without a basket plays as before.
public sealed class BasketSpec
{
    // Fish in the basket; where it stands (0–1 across the strip); every n-th slime that may take fish does.
    public int Fish { get; init; } = 5;
    public double At { get; init; } = 0.5;
    public int Every { get; init; } = 2;
    // Seconds at the basket to take a fish (a fish lying on the floor: half of it); the speed (× its own)
    // of a slime carrying one; seconds a dropped fish lies before it hops back by itself.
    public double Grab { get; init; } = 1.2;
    public double Carry { get; init; } = 0.8;
    public double Back { get; init; } = 5;

    public static void Validate(BasketSpec b, string where)
    {
        void Within(double value, double min, double max, string name)
        {
            if (!double.IsFinite(value) || value < min || value > max)
                throw new System.IO.InvalidDataException($"{where}.basket.{name} must be {min.ToString(System.Globalization.CultureInfo.InvariantCulture)}–{max.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
        Within(b.Fish, 1, 20, "fish"); Within(b.At, 0.15, 0.85, "at"); Within(b.Every, 1, 6, "every");
        Within(b.Grab, 0.2, 5, "grab"); Within(b.Carry, 0.3, 2, "carry"); Within(b.Back, 1, 30, "back");
    }
}

// A fish a slime dropped on the floor (its clock: seconds lying there).
public sealed class StrayFish
{
    public int Id;
    public double X, Age;
}

public sealed partial class BattleSim
{
    public BasketSpec? Basket => Stage?.Basket;
    // Where the basket stands (NaN: no basket), the fish in it, every fish of the stage and those lost.
    public double BasketX { get; private set; } = double.NaN;
    public int FishInBasket { get; private set; }
    public int FishTotal => Basket?.Fish ?? 0;
    public int FishLost { get; private set; }
    // Fish still the cat's: in the basket, lying on the floor or carried off but not yet away.
    public int FishLeft => FishTotal - FishLost;
    // The battle was lost because every fish was carried off (not the cat's HP).
    public bool FishGone { get; private set; }
    public int FishTaken { get; private set; }
    public int FishReturned { get; private set; }
    private readonly List<StrayFish> strayFish = new();
    public IReadOnlyList<StrayFish> StrayFish => strayFish;
    private int nextFishId = 1, takersCounted;
    public const double FishHalfWidth = 13, FishPickHeight = 30, BasketHalfWidth = 26;

    private void PlaceBasket()
    {
        if (Basket is not BasketSpec b) return;
        BasketX = Left + (Right - Left) * b.At;
        FishInBasket = b.Fish;
    }

    // A plain slime with no job of its own may take fish (bosses, supports, rocks, traps, flyers, linked
    // and trick slimes keep theirs).
    public static bool MayTakeFish(EnemyType t) => !t.Boss && !t.Support && !t.Blocker && !t.Still && t.Hover <= 0 && t.Link == null
        && t.Spit == null && t.Spring == null && t.Fuse == null && t.Fly == null && t.Burrow == null && t.Cling == null && t.Steal == null
        && t.Ghost == null && t.Bubble == null && t.Mirage == null && t.Sulk == null && t.Ice == null && t.Quills == null && t.Clock == null;

    // From AddSlime: every n-th slime that may take fish does.
    private void MarkTaker(Slime slime)
    {
        if (Basket is not BasketSpec b || !MayTakeFish(slime.Type)) return;
        takersCounted++;
        slime.FishTaker = takersCounted % b.Every == 0;
    }

    // From the Moving step of a taker: to the nearest fish lying on the floor, else to the basket; with a
    // fish, to its edge. False: nothing left to take, it goes for the cat as any slime.
    private bool TakeFish(Slime s, double dt, double minX, double maxX)
    {
        if (Basket is not BasketSpec b || s.Decoy) return false;
        if (s.CarriesFish)
        {
            double edge = s.FleeSide < 0 ? minX : maxX, gap = edge - s.X;
            s.Facing = s.FleeSide;
            if (Math.Abs(gap) > 1) s.X = Math.Clamp(s.X + Math.Sign(gap) * Math.Min(Math.Abs(gap), Speed(s) * b.Carry * dt), minX, maxX);
            else CarryOff(s);
            return true;
        }
        StrayFish? stray = null;
        foreach (var f in strayFish) if (stray == null || Math.Abs(f.X - s.X) < Math.Abs(stray.X - s.X)) stray = f;
        if (stray == null && FishInBasket <= 0) { s.FishClock = 0; return false; }
        double goal = stray?.X ?? BasketX, dx = goal - s.X;
        if (Math.Abs(dx) > 8)
        {
            s.Facing = dx < 0 ? -1 : 1;
            s.X = Math.Clamp(s.X + Math.Sign(dx) * Math.Min(Math.Abs(dx), Speed(s) * dt), minX, maxX);
            s.FishClock = 0;
            // Kept off the goal by the field's edge or a wall: it fights instead.
            return Math.Abs(goal - s.X) <= 8 || s.X > minX && s.X < maxX;
        }
        s.FishClock += dt;
        if (s.FishClock < (stray != null ? b.Grab / 2 : b.Grab)) return true;
        if (stray != null) strayFish.Remove(stray);
        else FishInBasket--;
        s.FishClock = 0;
        s.CarriesFish = true;
        s.FleeSide = s.X >= lastCatX ? 1 : -1; // Away from the cat.
        FishTaken++;
        events.Add(new(BattleEventKind.FishTaken, s.X, s.Id));
        return true;
    }

    // At its edge with a fish: away with it (gone, not a defeat of the cat's); the last fish loses the battle.
    private void CarryOff(Slime s)
    {
        s.CarriesFish = false;
        s.Set(SlimeState.Defeated);
        s.Escaped = true;
        s.KnockVelocity = 0;
        ClearStatus(s);
        if (SelectedId == s.Id) SelectedId = null;
        LinkFallen(s, false);
        FishLost++;
        AddEffect(BattleEffectKind.Poof, s.X, s.Height / 2, 1);
        effects[^1].Tint = s.Type.Color;
        events.Add(new(BattleEventKind.FishLost, s.X, FishLeft));
        if (FishLeft <= 0 && !Over)
        {
            FishGone = true;
            Finish(BattlePhase.Lost);
        }
    }

    // A carrier defeated, dazed or grabbed lets its fish fall where it is.
    private void DropFish(Slime s)
    {
        if (!s.CarriesFish) return;
        s.CarriesFish = false;
        var fish = new StrayFish { Id = nextFishId++, X = Math.Clamp(s.X, Left + FishHalfWidth, Right - FishHalfWidth) };
        strayFish.Add(fish);
        events.Add(new(BattleEventKind.FishDropped, fish.X, fish.Id));
    }

    // Each Step: a carrier that cannot carry on drops its fish; a fish left lying long enough hops back.
    private void StepBasket(double dt)
    {
        if (Basket is not BasketSpec b) return;
        foreach (var s in slimes)
            if (s.CarriesFish && (s.State is SlimeState.Stunned or SlimeState.Defeated || s.Handled || s.Decoy)) DropFish(s);
        for (int i = strayFish.Count - 1; i >= 0; i--)
        {
            strayFish[i].Age += dt;
            if (strayFish[i].Age >= b.Back) ReturnFish(i, false);
        }
    }

    private void ReturnFish(int index, bool clicked)
    {
        var fish = strayFish[index];
        strayFish.RemoveAt(index);
        FishInBasket++;
        FishReturned++;
        events.Add(new(BattleEventKind.FishReturned, fish.X, clicked ? 1 : 0));
    }

    public StrayFish? FindFish(int id) { foreach (var f in strayFish) if (f.Id == id) return f; return null; }

    // A click on a fish lying on the floor: back in the basket at once (not while paused, choosing a card or over).
    public bool PickUpFish(int id)
    {
        if (Over || Paused || ChoosingUpgrade) return false;
        for (int i = 0; i < strayFish.Count; i++)
            if (strayFish[i].Id == id) { ReturnFish(i, true); return true; }
        return false;
    }

    // The battle ends: a win keeps every fish not yet away (lying or still carried go back to the basket).
    private void ClearBasket(bool won)
    {
        if (Basket == null) return;
        foreach (var s in slimes)
            if (s.CarriesFish) { s.CarriesFish = false; if (won) FishInBasket++; }
        if (won) FishInBasket += strayFish.Count;
        strayFish.Clear();
    }

    private void MapBasket(double left, double oldWidth, double newWidth)
    {
        if (double.IsFinite(BasketX)) BasketX = left + (BasketX - Left) / oldWidth * newWidth;
        foreach (var f in strayFish) f.X = left + (f.X - Left) / oldWidth * newWidth;
    }
}
