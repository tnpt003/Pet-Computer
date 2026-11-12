using DesktopPet;

// Mini-games M3 (docs/MINI-GAMES.md): catching fireflies' pure rules — 3–5 flies, never in the cat's body
// and inside the area, each living 4–6 seconds, faster from second 15; taps; the cat's pounce rhythm and its
// four at most; where the area goes; the best.
internal static class FireflyTests
{
    public static void Run(Action<bool, string> check)
    {
        bool count = true, clear = true, inside = true, lifespan = true;
        for (int seed = 0; seed < 20; seed++)
        {
            var random = new Random(seed);
            var swarm = new FireflySwarm();
            swarm.Reset(FireflyRules.AreaWidth, FireflyRules.AreaHeight, random);
            for (int frame = 0; frame < 60 * 30; frame++)
            {
                swarm.Step(1 / 60.0, random);
                count &= swarm.Alive is >= FireflyRules.MinFlies and <= FireflyRules.MaxFlies;
                foreach (var fly in swarm.Flies)
                {
                    clear &= !swarm.InCat(fly.X, fly.Y);
                    inside &= fly.X >= 0 && fly.X <= swarm.Width && fly.Y >= 0 && fly.Y <= swarm.Height;
                    lifespan &= fly.Age < fly.Life && fly.Life is >= FireflyRules.LifeMin and <= FireflyRules.LifeMax;
                }
            }
        }
        check(count, "Always 3–5 fireflies in the air over 30 seconds");
        check(clear, "No firefly ever in the cat's body (with its tap reach around it)");
        check(inside, "...and all inside the play area");
        check(lifespan, "Each lives 4–6 seconds, then goes out");

        var timed = new FireflySwarm();
        var r = new Random(3);
        timed.Reset(FireflyRules.AreaWidth, FireflyRules.AreaHeight, r);
        double slow = timed.SpeedNow;
        for (int i = 0; i < 60 * 16; i++) timed.Step(1 / 60.0, r);
        check(slow == FireflyRules.Speed && timed.SpeedNow == FireflyRules.Speed * FireflyRules.FastScale, "From second 15 they fly a little faster");

        var fly0 = timed.Flies[0];
        check(timed.At(fly0.X + FireflyRules.TapRadius - 1, fly0.Y) == fly0 && timed.At(fly0.X + 40, fly0.Y + 40) != fly0, "A tap within ~14 DIPs catches it, farther misses");
        int before = timed.Alive;
        check(timed.Catch(fly0, byCat: false) && timed.Alive == before - 1 && !timed.Catch(fly0, byCat: false), "A caught fly is gone, once");

        // The cat's pounce: only at a fly low and near it.
        var aim = new FireflySwarm();
        aim.Reset(FireflyRules.AreaWidth, FireflyRules.AreaHeight, new Random(1));
        foreach (var f in aim.Flies) { f.X = 20; f.Y = 20; }
        check(aim.PounceTarget() == null, "No fly near and low: no pounce");
        var low = aim.Flies[0];
        low.X = aim.Width / 2 + 80; low.Y = aim.Height - 60;
        check(aim.PounceTarget() == low, "A fly low beside the cat is its target");

        // Every 6–8 seconds (the first after 3–6), four at most, a miss now and then.
        var cat = new FireflyCat();
        var cr = new Random(7);
        cat.Reset(cr);
        double t = 0;
        while (!cat.Due(0.05)) t += 0.05;
        check(t >= FireflyRules.FirstPounceMin - 0.06 && t <= FireflyRules.PounceEveryMin + 0.06, "The first pounce comes after 3–6 seconds");
        int hits = 0, misses = 0;
        bool rhythm = true;
        for (int i = 0; i < 200; i++)
        {
            if (cat.Pounce(cr)) hits++; else misses++;
            rhythm &= cat.Wait is >= FireflyRules.PounceEveryMin and <= FireflyRules.PounceEveryMax;
        }
        check(rhythm && hits > misses && misses > 20, "Then every 6–8 seconds, mostly a catch, sometimes a miss");
        for (int i = 0; i < 6; i++) cat.Caught();
        check(cat.Catches == FireflyRules.MaxCatCatches && !cat.Due(100), "At most four flies for the cat a game");

        // The area: centred on the cat, on the screen, clear of land and toys; too little room: none.
        var none = new List<(double, double)>();
        check(FireflyRules.Place(800, 56, 0, 1600, 0, 1600, none, 300) == 800, "Room where the cat is: it plays right there");
        double? moved = FireflyRules.Place(800, 56, 0, 1600, 0, 1600, new List<(double, double)> { (900, 1000) }, 300);
        check(moved is double m && (m + FireflyRules.AreaWidth / 2 <= 900 - FireflyRules.Gap || m - FireflyRules.AreaWidth / 2 >= 1000 + FireflyRules.Gap) && Math.Abs(m - 800) <= 300,
            "Farm land or a toy in the way: a spot within reach clear of it");
        check(FireflyRules.Place(100, 56, 0, 1600, 0, 1600, none, 300) is double edge && edge - FireflyRules.AreaWidth / 2 >= 0, "Near the screen's edge: the area stays on the screen");
        check(FireflyRules.Place(150, 56, 0, 300, 0, 300, none, 300) == null, "A screen too narrow: no game");
        check(FireflyRules.Height(1000, 0) == FireflyRules.AreaHeight && FireflyRules.Height(180, 0) == 180 && FireflyRules.Height(100, 0) == 0,
            "High on a window near the top: a lower area, or none");

        var preferences = new PetPreferences();
        check(FireflyRules.RaiseBest(preferences, 6) && !FireflyRules.RaiseBest(preferences, 5) && preferences.FireflyBest == 6, "The best goes up only when beaten");
        FireflyRules.CountPlay(preferences);
        check(preferences.FireflyPlays == 1, "A game played is counted");
    }
}
