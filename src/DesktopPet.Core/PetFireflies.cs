using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// Mini-games (docs/MINI-GAMES.md → M3): catch the fireflies. Pure rules: the numbers, where the play area
// goes, and the swarm itself (3–5 glowing flies drifting on curves, never into the cat's body, each living
// 4–6 seconds; faster from second 15; the cat's own pounces, at most 4 catches a game). The window
// (PetWindow.Fireflies.cs) draws and times it. Area coordinates: x from the area's left, y down from its
// top, the cat's ground at y = Height.
public static class FireflyRules
{
    // The area around the cat (DIPs), and the least height it needs on a ledge high up the screen.
    public const double AreaWidth = 360, AreaHeight = 220, MinHeight = 160, Gap = 8;
    // The cat's body inside the area, kept clear of every fly (by the fly's tap radius too).
    public const double CatHalfWidth = 46, CatHeight = 104, TapRadius = 14, EdgeMargin = 16;
    public const int MinFlies = 3, MaxFlies = 5, MaxCatCatches = 4;
    // The count before, the game, and the flies leaving at the end.
    public const double CountSeconds = 2, GameSeconds = 30, FasterFrom = 15, AwaySeconds = 1.4, SummarySeconds = 3.5;
    public const double LifeMin = 4, LifeMax = 6, FadeSeconds = 0.9, BornSeconds = 0.35, RespawnMin = 0.3, RespawnMax = 0.9;
    public const double Speed = 42, FastScale = 1.35, TurnMax = 2.2, TurnEveryMin = 0.7, TurnEveryMax = 1.5;
    // The cat's pounce: every 6–8 seconds at a fly low and near; a short crouch and a hop of at most ~34 DIPs
    // (drawn, not physics: the cat lands where it stood), the swipe at the top; sometimes a miss.
    public const double PounceEveryMin = 6, PounceEveryMax = 8, PounceRetry = 1, PounceReach = 120, PounceLow = 150,
        CrouchSeconds = 0.35, HopSeconds = 0.45, HopHeight = 34, MissChance = 0.3, FirstPounceMin = 3;

    public static double Between(Random random, double low, double high) => low + random.NextDouble() * (high - low);

    // Where the cat plays (its centre): the nearest spot within reach on its ground whose area, centred on
    // the cat, stays on the screen and clear of what is taken on the floor. Null: nowhere.
    public static double? Place(double catCentre, double halfCat, double groundLeft, double groundRight, double screenLeft, double screenRight,
        IReadOnlyList<(double Left, double Right)> taken, double reach)
    {
        if (!double.IsFinite(catCentre) || groundRight - groundLeft < 2 * halfCat || screenRight - screenLeft < AreaWidth) return null;
        bool Free(double centre) => centre - halfCat >= groundLeft && centre + halfCat <= groundRight &&
            centre - AreaWidth / 2 >= screenLeft && centre + AreaWidth / 2 <= screenRight &&
            taken.All(t => centre + AreaWidth / 2 <= t.Left - Gap || centre - AreaWidth / 2 >= t.Right + Gap);
        int first = groundRight - catCentre >= catCentre - groundLeft ? 1 : -1;
        for (double step = 0; step <= reach + 0.01; step += 20)
            foreach (double centre in step == 0 ? new[] { catCentre } : new[] { catCentre + step * first, catCentre - step * first })
                if (Free(centre)) return centre;
        return null;
    }

    // The area's height on this ground: the full height, less when the screen's top is near; 0: too low.
    public static double Height(double ground, double screenTop)
    {
        double room = Math.Min(AreaHeight, ground - screenTop);
        return room >= MinHeight ? room : 0;
    }

    // A game's best (users' and the cat's catches together) goes up at once. True when it went up.
    public static bool RaiseBest(PetPreferences preferences, int total)
    {
        total = Math.Clamp(total, 0, 9999);
        if (total <= preferences.FireflyBest) return false;
        preferences.FireflyBest = total;
        return true;
    }

    public static void CountPlay(PetPreferences preferences) => preferences.FireflyPlays = Math.Min(999999, preferences.FireflyPlays + 1);
}

public sealed class Firefly
{
    public int Id;
    public double X, Y, Heading, Turn, TurnLeft, Age, Life, Phase;
    public bool Caught, ByCat;
    public double Fade => Math.Clamp((Life - Age) / FireflyRules.FadeSeconds, 0, 1) * Math.Clamp(Age / FireflyRules.BornSeconds, 0, 1);
}

// The flies in the area: spawns, drift, ages, catches. Time only moves in Step.
public sealed class FireflySwarm
{
    private readonly List<Firefly> flies = new();
    private int nextId, target = 4;
    private double respawn;

    public double Width { get; private set; } = FireflyRules.AreaWidth;
    public double Height { get; private set; } = FireflyRules.AreaHeight;
    public double Time { get; private set; }
    public bool Leaving { get; private set; }
    public IReadOnlyList<Firefly> Flies => flies;
    public int Alive => flies.Count(f => !f.Caught);

    public void Reset(double width, double height, Random random)
    {
        flies.Clear();
        Width = width; Height = height; Time = 0; Leaving = false; respawn = 0;
        target = random.Next(FireflyRules.MinFlies, FireflyRules.MaxFlies + 1);
        while (Alive < FireflyRules.MinFlies) Spawn(random);
    }

    public double SpeedNow => FireflyRules.Speed * (Time >= FireflyRules.FasterFrom ? FireflyRules.FastScale : 1);

    // The cat's body in area coordinates, kept clear with the tap radius around it.
    public bool InCat(double x, double y)
    {
        double half = FireflyRules.CatHalfWidth + FireflyRules.TapRadius, top = Height - FireflyRules.CatHeight - FireflyRules.TapRadius;
        return Math.Abs(x - Width / 2) < half && y > top;
    }

    private bool Inside(double x, double y) => x >= FireflyRules.EdgeMargin && x <= Width - FireflyRules.EdgeMargin &&
        y >= FireflyRules.EdgeMargin && y <= Height - FireflyRules.EdgeMargin;

    public Firefly Spawn(Random random)
    {
        double x = Width / 2, y = FireflyRules.EdgeMargin;
        for (int tries = 0; tries < 60; tries++)
        {
            x = FireflyRules.Between(random, FireflyRules.EdgeMargin, Width - FireflyRules.EdgeMargin);
            y = FireflyRules.Between(random, FireflyRules.EdgeMargin, Height - FireflyRules.EdgeMargin);
            if (!InCat(x, y)) break;
            y = FireflyRules.EdgeMargin; // A last resort above the cat is always clear.
        }
        var fly = new Firefly
        {
            Id = nextId++, X = x, Y = y, Heading = random.NextDouble() * Math.PI * 2,
            Turn = FireflyRules.Between(random, -FireflyRules.TurnMax, FireflyRules.TurnMax),
            TurnLeft = FireflyRules.Between(random, FireflyRules.TurnEveryMin, FireflyRules.TurnEveryMax),
            Life = FireflyRules.Between(random, FireflyRules.LifeMin, FireflyRules.LifeMax), Phase = random.NextDouble() * Math.PI * 2
        };
        flies.Add(fly);
        return fly;
    }

    // The game's time moves on: flies drift on their curves, turn away from the edges and the cat, age and
    // go out; new ones keep 3–5 in the air. Leaving: they all drift up and away, no new ones.
    public void Step(double dt, Random random)
    {
        if (dt <= 0) return;
        Time += dt;
        double speed = SpeedNow;
        foreach (var fly in flies)
        {
            if (fly.Caught) continue;
            fly.Age += dt;
            if (Leaving)
            {
                fly.Heading = -Math.PI / 2 + (fly.X < Width / 2 ? -0.5 : 0.5);
                fly.X += Math.Cos(fly.Heading) * speed * 1.6 * dt;
                fly.Y += Math.Sin(fly.Heading) * speed * 1.6 * dt;
                continue;
            }
            if ((fly.TurnLeft -= dt) <= 0)
            {
                fly.Turn = FireflyRules.Between(random, -FireflyRules.TurnMax, FireflyRules.TurnMax);
                fly.TurnLeft = FireflyRules.Between(random, FireflyRules.TurnEveryMin, FireflyRules.TurnEveryMax);
            }
            fly.Heading += fly.Turn * dt;
            double nx = fly.X + Math.Cos(fly.Heading) * speed * dt, ny = fly.Y + Math.Sin(fly.Heading) * speed * dt;
            if (!Inside(nx, ny) || InCat(nx, ny))
            {
                // Turned back toward the open middle above the cat, so it never slips in.
                fly.Heading = Math.Atan2(Height * 0.3 - fly.Y, Width / 2 + (fly.X < Width / 2 ? -80 : 80) - fly.X);
                fly.Turn = -fly.Turn;
                nx = fly.X + Math.Cos(fly.Heading) * speed * dt; ny = fly.Y + Math.Sin(fly.Heading) * speed * dt;
                if (!Inside(nx, ny) || InCat(nx, ny)) { nx = fly.X; ny = fly.Y; }
            }
            fly.X = nx; fly.Y = ny;
        }
        flies.RemoveAll(f => !f.Caught && f.Age >= f.Life);
        if (Leaving) { DropGone(); return; }
        if (Alive < FireflyRules.MinFlies) respawn = Math.Min(respawn, 0);
        if (Alive < target && (respawn -= dt) <= 0)
        {
            Spawn(random);
            respawn = FireflyRules.Between(random, FireflyRules.RespawnMin, FireflyRules.RespawnMax);
            target = random.Next(FireflyRules.MinFlies, FireflyRules.MaxFlies + 1);
        }
        while (Alive < FireflyRules.MinFlies) Spawn(random);
    }

    public void Leave() => Leaving = true;

    // The live fly under a tap (area coordinates), the nearest within the tap radius; null: a miss.
    public Firefly? At(double x, double y) => flies.Where(f => !f.Caught && f.Age < f.Life && Math.Sqrt((f.X - x) * (f.X - x) + (f.Y - y) * (f.Y - y)) <= FireflyRules.TapRadius)
        .OrderBy(f => (f.X - x) * (f.X - x) + (f.Y - y) * (f.Y - y)).FirstOrDefault();

    // A fly worth the cat's pounce: low and near the cat, the nearest; null: none.
    public Firefly? PounceTarget() => flies.Where(f => !f.Caught && f.Life - f.Age > 0.8 && Math.Abs(f.X - Width / 2) <= FireflyRules.PounceReach &&
            Height - f.Y <= FireflyRules.PounceLow).OrderBy(f => Math.Abs(f.X - Width / 2)).FirstOrDefault();

    // Caught (by the user or the cat): it bursts into sparks and is no longer in play.
    public bool Catch(Firefly fly, bool byCat)
    {
        if (fly.Caught || !flies.Contains(fly)) return false;
        fly.Caught = true; fly.ByCat = byCat;
        flies.Remove(fly);
        return true;
    }

    // Drawn flies that have left the area once leaving.
    public void DropGone() => flies.RemoveAll(f => f.Y < -20 || f.X < -20 || f.X > Width + 20);
}

// The cat's turn: pounce every 6–8 seconds (the first after 3), at most four catches, a miss now and then.
public sealed class FireflyCat
{
    public int Catches { get; private set; }
    public double Wait { get; private set; }

    public void Reset(Random random) { Catches = 0; Wait = FireflyRules.Between(random, FireflyRules.FirstPounceMin, FireflyRules.PounceEveryMin); }

    // True when it is time to look for a fly to pounce at.
    public bool Due(double dt) => Catches < FireflyRules.MaxCatCatches && (Wait -= dt) <= 0;

    // No fly in reach: look again in a second.
    public void Retry() => Wait = FireflyRules.PounceRetry;

    // A pounce goes off: whether it catches, and the wait to the next.
    public bool Pounce(Random random)
    {
        Wait = FireflyRules.Between(random, FireflyRules.PounceEveryMin, FireflyRules.PounceEveryMax);
        return random.NextDouble() >= FireflyRules.MissChance;
    }

    public void Caught() => Catches = Math.Min(FireflyRules.MaxCatCatches, Catches + 1);
}
