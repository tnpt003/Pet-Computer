using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// Treasure hunt (docs/MINI-GAMES.md → M5). Two halves: the cat finding a map now and then on its own (an
// automatic behaviour like the visitors: its own 60–120 minute timer in RAM, at most 3 maps a day, the map kept
// 10 minutes), and the hunt itself, only ever started by the user: three faint paw prints hidden around the cat,
// then a little chest with one short story in it. No losing: after 3 minutes the prints left light up by
// themselves. Pure rules here; the window (PetWindow.Treasure.cs) draws and times it. Screen coordinates in DIPs.
public static class TreasureRules
{
    // Finding a map: from the app's start, 60–120 minutes; blocked, two minutes later; at most 3 a day.
    public const double FindMin = 60 * 60, FindMax = 120 * 60, RetrySeconds = 120;
    public const int MapsPerDay = 3, Prints = 3, StoryCount = 12;
    // The find scene (the cat sits holding the rolled map, looking up), then the map over its head for 10
    // minutes (Pause stops that clock), fading out over its last seconds.
    public const double FindSceneSeconds = 2, MapSeconds = 10 * 60, MapFadeSeconds = 20;
    // The hunt: about 3 minutes to find the prints, hints at 20 and 40 seconds without a new one, a 1 second
    // sniff under each print found, the chest opening by itself after 30 seconds, the story 8 seconds.
    public const double HuntSeconds = 180, HintTurn = 20, HintWalk = 40, SniffSeconds = 1, WalkSeconds = 10, ChestAutoSeconds = 30,
        StorySeconds = 8, PuffSeconds = 0.6, LidSeconds = 0.5, PaperSeconds = 0.8, LabelSeconds = 1.6;
    // Where the prints go: ±400 DIPs across from the cat, up to 300 above its ground, 80 apart, inside the work
    // area, never on the cat's body or on what stands on the floor (the farm, toys, the corner things).
    public const double Spread = 400, Rise = 300, PrintGap = 80, PrintRadius = 16, Margin = 20, LowestPrint = 24;
    public const double CatHalfWidth = 46, CatHeight = 104, Clearance = 8;
    // How bright a print is: faint (about 20 %), brighter as the cursor comes within 50 DIPs, a little brighter
    // as the 40 second hint, full once found.
    public const double Faint = 0.2, HintFaint = 0.4, NearCursor = 50, NearGlow = 0.75;
    public const double ChestHalf = 22, ChestHeight = 36, ChestGap = 10;

    public static double Between(Random random, double low, double high) => low + random.NextDouble() * (high - low);

    public static double NextDelay(Random random) => Between(random, FindMin, FindMax);

    // Maps found today (the computer's date); another day starts again at 0.
    public static int MapsToday(PetPreferences preferences, DateTime now) =>
        preferences.TreasureMapDay is DateTime day && day.Date == now.Date ? Math.Clamp(preferences.TreasureMapsToday, 0, MapsPerDay) : 0;

    public static bool MayFind(PetPreferences preferences, DateTime now) => preferences.Treasure && MapsToday(preferences, now) < MapsPerDay;

    public static void CountFind(PetPreferences preferences, DateTime now)
    {
        int today = MapsToday(preferences, now);
        preferences.TreasureMapDay = now.Date;
        preferences.TreasureMapsToday = Math.Min(MapsPerDay, today + 1);
    }

    // Three prints at random around the cat (centre catCenter, feet at ground), inside the work area, apart
    // from each other, off the cat's body and off what is taken. Null: no room.
    public static TreasureSpot[]? Place(Random random, double catCenter, double ground, Area work, IReadOnlyList<Area> taken)
    {
        double left = Math.Max(work.Left + Margin, catCenter - Spread), right = Math.Min(work.Right - Margin, catCenter + Spread);
        double top = Math.Max(work.Top + Margin, ground - Rise), bottom = Math.Min(work.Bottom - Margin / 2, ground - LowestPrint);
        if (right - left < 2 * PrintGap || bottom - top < 40) return null;
        var cat = new Area(catCenter - CatHalfWidth - Clearance, ground - CatHeight - Clearance, catCenter + CatHalfWidth + Clearance, ground + Clearance);
        bool Clear(double x, double y) => !Overlaps(cat, x, y) && taken.All(t => !Overlaps(t, x, y));
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var spots = new List<TreasureSpot>();
            for (int tries = 0; tries < 400 && spots.Count < Prints; tries++)
            {
                double x = Between(random, left, right), y = Between(random, top, bottom);
                if (!Clear(x, y) || spots.Any(s => Distance(s.X, s.Y, x, y) < PrintGap)) continue;
                spots.Add(new TreasureSpot(x, y));
            }
            if (spots.Count == Prints) return spots.ToArray();
        }
        return null;
    }

    private static bool Overlaps(Area a, double x, double y) =>
        x + PrintRadius > a.Left && x - PrintRadius < a.Right && y + PrintRadius > a.Top && y - PrintRadius < a.Bottom;

    public static double Distance(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    // How bright a print is now (0–1).
    public static double Glow(bool found, bool hinted, double cursorDistance)
    {
        if (found) return 1;
        double low = hinted ? HintFaint : Faint;
        if (!(cursorDistance < NearCursor)) return low;
        double near = 1 - Math.Max(0, cursorDistance) / NearCursor;
        return low + (NearGlow - low) * near;
    }

    // Where the cat stands to be "under" x: on its own ground (the floor or its window edge), never off it.
    // How long the cat may walk to a print or the chest: at least WalkSeconds, more when it is far. Two prints
    // can be ~800 DIPs apart (±Spread around where the hunt began), longer than 10 s at the usual 72 DIP/s.
    public static double WalkLimit(double distance, double speed) =>
        speed > 0 && double.IsFinite(distance) ? Math.Max(WalkSeconds, Math.Abs(distance) / speed + 2) : WalkSeconds;

    public static double Under(double x, double groundLeft, double groundRight)
    {
        double low = groundLeft + CatHalfWidth, high = groundRight - CatHalfWidth;
        return high < low ? (groundLeft + groundRight) / 2 : Math.Clamp(x, low, high);
    }

    // Where the chest goes on the cat's ground (its middle), near the last print, off the cat's body and off
    // what is taken on that ground when it can be. Null: no room on this ground at all.
    public static double? ChestSpot(double lastX, double catCenter, double groundLeft, double groundRight, IReadOnlyList<(double Left, double Right)> taken)
    {
        double low = groundLeft + ChestHalf, high = groundRight - ChestHalf;
        // Room for the chest and the cat beside it.
        if (high - low < 2 * (CatHalfWidth + ChestHalf + ChestGap)) return null;
        bool OffCat(double x) => Math.Abs(x - catCenter) >= CatHalfWidth + ChestHalf + ChestGap;
        bool Free(double x) => taken.All(t => x + ChestHalf <= t.Left || x - ChestHalf >= t.Right);
        bool Beside(double x) => StandSpot(x, catCenter, groundLeft, groundRight) is not null;
        double start = Math.Clamp(lastX, low, high);
        for (int pass = 0; pass < 2; pass++)
            for (double step = 0; step <= high - low; step += 10)
                foreach (double x in step == 0 ? new[] { start } : new[] { start + step, start - step })
                    if (x >= low && x <= high && OffCat(x) && Beside(x) && (pass == 1 || Free(x))) return x;
        return null;
    }

    // Where the cat stands beside the chest (its middle): on the side nearer the cat, else the other; on its ground.
    public static double? StandSpot(double chestX, double catCenter, double groundLeft, double groundRight)
    {
        double reach = CatHalfWidth + ChestHalf + ChestGap;
        int near = catCenter < chestX ? -1 : 1;
        foreach (int side in new[] { near, -near })
        {
            double x = chestX + side * reach;
            if (x - CatHalfWidth >= groundLeft && x + CatHalfWidth <= groundRight) return x;
        }
        return null;
    }

    // The story in the chest: one not opened yet, else the one opened longest ago (so none repeats until all
    // have been read, then they come round again in the same order).
    public static int PickStory(PetPreferences preferences, Random random)
    {
        var opened = Normalize(preferences.TreasureStories) ?? new List<int>();
        var fresh = Enumerable.Range(1, StoryCount).Where(id => !opened.Contains(id)).ToList();
        return fresh.Count > 0 ? fresh[random.Next(fresh.Count)] : opened[0];
    }

    // A chest opened: counted, and its story kept (the most recent last).
    public static void OpenChest(PetPreferences preferences, int story)
    {
        preferences.TreasureChests = Math.Min(999999, preferences.TreasureChests + 1);
        var opened = Normalize(preferences.TreasureStories) ?? new List<int>();
        opened.Remove(story);
        if (story is >= 1 and <= StoryCount) opened.Add(story);
        preferences.TreasureStories = opened;
    }

    // Only known ids, each once (an older or edited file reads fine). Null when there is none.
    public static List<int>? Normalize(List<int>? stories)
    {
        if (stories == null) return null;
        var clean = new List<int>();
        foreach (int id in stories) if (id is >= 1 and <= StoryCount && !clean.Contains(id)) clean.Add(id);
        return clean.Count > 0 ? clean : null;
    }
}

public readonly record struct TreasureSpot(double X, double Y);

// The map the cat holds: 10 minutes, its clock stopped in Pause (dt 0). Not saved.
public sealed class TreasureMap
{
    public double Left { get; private set; }
    public bool Held => Left > 0;
    // 1, fading to 0 over the last seconds.
    public double Fade => Math.Clamp(Left / TreasureRules.MapFadeSeconds, 0, 1);

    public void Give() => Left = TreasureRules.MapSeconds;
    public void Step(double dt) { if (Left > 0 && dt > 0) Left = Math.Max(0, Left - dt); }
    public void Take() => Left = 0;
}

// One hunt: where the prints are, which are found, the clock and the hints. Time only moves in Step.
public sealed class TreasureHunt
{
    private TreasureSpot[] spots = Array.Empty<TreasureSpot>();
    private bool[] found = Array.Empty<bool>();

    public IReadOnlyList<TreasureSpot> Spots => spots;
    public double Time { get; private set; }
    public double SinceFind { get; private set; }
    public int FoundCount => found.Count(f => f);
    public bool AllFound => spots.Length > 0 && found.All(f => f);
    public bool TimedOut { get; private set; }
    public int Last { get; private set; } = -1; // The print found last (or lit last when time ran out).

    public bool Found(int index) => index >= 0 && index < found.Length && found[index];

    public void Reset(TreasureSpot[] places)
    {
        spots = places; found = new bool[places.Length];
        Time = SinceFind = 0; TimedOut = false; Last = -1;
    }

    // 0: no hint; 1: the cat turns its head to the nearest print; 2: it walks near it and that print glows a little.
    public int Hint => AllFound ? 0 : SinceFind >= TreasureRules.HintWalk ? 2 : SinceFind >= TreasureRules.HintTurn ? 1 : 0;

    // True when time ran out this step: the prints left light up and the hunt is done (no losing).
    public bool Step(double dt)
    {
        if (spots.Length == 0 || AllFound || dt <= 0) return false;
        Time += dt; SinceFind += dt;
        if (Time < TreasureRules.HuntSeconds) return false;
        for (int i = 0; i < found.Length; i++) if (!found[i]) { found[i] = true; Last = i; }
        TimedOut = true;
        return true;
    }

    // A click found print index. False when it was found already (or there is none).
    public bool Find(int index)
    {
        if (index < 0 || index >= found.Length || found[index]) return false;
        found[index] = true; Last = index; SinceFind = 0;
        return true;
    }

    // The print under (x, y) within reach, not yet found. -1: none.
    public int At(double x, double y, double reach = TreasureRules.PrintRadius + 6)
    {
        int best = -1; double bestDistance = reach;
        for (int i = 0; i < spots.Length; i++)
        {
            if (found[i]) continue;
            double d = TreasureRules.Distance(spots[i].X, spots[i].Y, x, y);
            if (d <= bestDistance) { best = i; bestDistance = d; }
        }
        return best;
    }

    // The unfound print nearest to x (across). -1: none.
    public int Nearest(double x)
    {
        int best = -1; double bestDistance = double.MaxValue;
        for (int i = 0; i < spots.Length; i++)
            if (!found[i] && Math.Abs(spots[i].X - x) < bestDistance) { best = i; bestDistance = Math.Abs(spots[i].X - x); }
        return best;
    }
}
