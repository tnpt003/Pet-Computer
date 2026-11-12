using System;

namespace DesktopPet;

// Visitors (docs/MINI-GAMES.md → M4): now and then a paper butterfly or a gentle slime comes by on its own.
// Pure rules: when one comes, the guest's way across the cat's screen (the butterfly flutters from one edge to
// the other near the cat; the slime crawls on the floor, stops about 80 DIPs from the cat, bounces "hello" and
// goes on), never into the cat's body, and how far the cat may follow. The window (PetWindow.Guests.cs) draws
// and times it. Screen coordinates in DIPs: the guest's X is its middle, Y its middle (butterfly) or the floor
// it sits on (slime).
public enum GuestKind { Butterfly, Slime }

public enum GuestPhase { Coming, Greeting, Going, Gone }

public static class GuestRules
{
    // The first visit counts from the app's start (kept in RAM only); blocked: try again two minutes later.
    public const double VisitMin = 40 * 60, VisitMax = 90 * 60, RetrySeconds = 120;
    // A visit lasts about 8–15 seconds; a safety cap ends one that got stuck.
    public const double VisitSecondsMin = 8, VisitSecondsMax = 15, MaxSeconds = 30, FadeInSeconds = 0.4, FadeOutSeconds = 0.3;
    // The cat's body, from its middle and its ground, kept clear of a guest (with the guest's own half size).
    public const double CatHalfWidth = 46, CatHeight = 104, Clearance = 10;
    public const double ButterflyHalf = 28, ButterflyAboveCat = 62, ButterflyBob = 16, ButterflyEdge = 40;
    public const double SlimeHalf = 20, SlimeHeight = 42, SlimeStop = 80, GreetSeconds = 1.5, HopSeconds = 0.5;
    public const double SpeedMin = 40, ButterflySpeedMax = 260, SlimeSpeedMax = 180, ZoomScale = 2.6, ZoomRise = 90;
    // The cat: about half the visits it follows a little (at most ~150 DIPs or 3 seconds, on its own ground),
    // and with the butterfly it pounces once, in place, when it flutters near.
    public const double ChaseChance = 0.5, ChaseDistance = 150, ChaseSeconds = 3, ChaseFrom = 260, PounceNear = 60, SlimeGap = 50;

    public static double Between(Random random, double low, double high) => low + random.NextDouble() * (high - low);

    public static double NextDelay(Random random) => Between(random, VisitMin, VisitMax);

    public static GuestKind Pick(Random random) => random.NextDouble() < 0.5 ? GuestKind.Butterfly : GuestKind.Slime;

    public static void CountVisit(PetPreferences preferences) => preferences.GuestVisits = Math.Min(999999, preferences.GuestVisits + 1);

    // Where the cat may follow to (its middle): toward the guest, at most ChaseDistance from where it started,
    // inside its ground (the floor or the window edge it stands on), never off it.
    public static double ChaseTarget(double start, double guestX, double groundLeft, double groundRight, double keepAway)
    {
        double low = groundLeft + CatHalfWidth, high = groundRight - CatHalfWidth;
        if (high < low) return start;
        double toward = guestX < start ? guestX + keepAway : guestX - keepAway;
        if (Math.Sign(toward - start) != Math.Sign(guestX - start)) toward = start; // Already close enough.
        double target = Math.Clamp(toward, start - ChaseDistance, start + ChaseDistance);
        return Math.Clamp(target, low, high);
    }
}

// The cat as a guest sees it: its middle, its ground (feet) and whether it stands on the screen's floor.
public readonly record struct GuestCat(double Center, double Ground, bool OnFloor)
{
    public double Top => Ground - GuestRules.CatHeight;
}

// One visit: the guest's way across, its fades, the click reactions. Time only moves in Step.
public sealed class Guest
{
    public GuestKind Kind { get; private set; }
    public GuestPhase Phase { get; private set; } = GuestPhase.Gone;
    public double X { get; private set; }
    public double Y { get; private set; }
    public int Direction { get; private set; } = 1;
    public double Speed { get; private set; }
    public double Time { get; private set; }
    public double PhaseTime { get; private set; }
    public double Fade { get; private set; }
    public double Hop { get; private set; } = -1; // A click's bounce (slime), seconds into it; -1: none.
    public bool Zooming { get; private set; } // Clicked butterfly: off in a hurry.
    public bool Leaving { get; private set; } // Fading out early (sleep, a fullscreen app, a battle...).
    public bool Greeted { get; private set; }
    public bool TurnsBack { get; private set; } // A slime meeting the cat on the floor goes back the way it came.
    public double Left { get; private set; }
    public double Right { get; private set; }
    public double Floor { get; private set; }
    public double Top { get; private set; }
    private double baseline, bobPhase, stopX;

    public bool Active => Phase != GuestPhase.Gone;

    // Starts a visit across the screen [left, right] × [top, floor]. False when there is no way (a slime with
    // no room to stop before the cat on either side, a screen too narrow).
    public bool Start(GuestKind kind, Random random, double left, double right, double top, double floor, GuestCat cat)
    {
        Phase = GuestPhase.Gone;
        if (right - left < 200 || floor - top < 160) return false;
        Kind = kind; Left = left; Right = right; Top = top; Floor = floor;
        Time = PhaseTime = Fade = 0; Hop = -1; Zooming = Leaving = Greeted = TurnsBack = false;
        bobPhase = random.NextDouble() * Math.PI * 2;
        double seconds = GuestRules.Between(random, GuestRules.VisitSecondsMin, GuestRules.VisitSecondsMax);
        // From the side with more room, so it crosses near the cat on the way.
        Direction = cat.Center - left >= right - cat.Center ? -1 : 1;
        if (random.NextDouble() < 0.35) Direction = -Direction;
        if (kind == GuestKind.Butterfly)
        {
            X = Direction > 0 ? left - GuestRules.ButterflyEdge : right + GuestRules.ButterflyEdge;
            baseline = Math.Clamp(cat.Top - GuestRules.ButterflyAboveCat, top + GuestRules.ButterflyHalf + GuestRules.ButterflyBob + 4, floor - 60);
            Y = baseline;
            Speed = Math.Clamp((right - left + 2 * GuestRules.ButterflyEdge) / seconds, GuestRules.SpeedMin, GuestRules.ButterflySpeedMax);
        }
        else
        {
            TurnsBack = cat.OnFloor;
            double reach = GuestRules.CatHalfWidth + GuestRules.SlimeStop + GuestRules.SlimeHalf;
            bool Room(int dir) => dir > 0 ? cat.Center - reach >= left + GuestRules.SlimeHalf + 20 : cat.Center + reach <= right - GuestRules.SlimeHalf - 20;
            if (!Room(Direction)) Direction = -Direction;
            if (!Room(Direction)) return false;
            stopX = cat.Center - Direction * reach;
            X = Direction > 0 ? left - GuestRules.SlimeHalf : right + GuestRules.SlimeHalf;
            Y = floor;
            double way = TurnsBack ? 2 * Math.Abs(stopX - X) : right - left + 2 * GuestRules.SlimeHalf;
            Speed = Math.Clamp(way / Math.Max(3, seconds - GuestRules.GreetSeconds), GuestRules.SpeedMin, GuestRules.SlimeSpeedMax);
        }
        Phase = GuestPhase.Coming;
        return true;
    }

    private double Half => Kind == GuestKind.Butterfly ? GuestRules.ButterflyHalf : GuestRules.SlimeHalf;

    // The guest's box and the cat's body (with the clearance) overlap.
    public bool Touches(double x, double y, GuestCat cat)
    {
        double half = GuestRules.CatHalfWidth + Half + GuestRules.Clearance;
        if (Math.Abs(x - cat.Center) >= half) return false;
        double guestTop = Kind == GuestKind.Butterfly ? y - GuestRules.ButterflyHalf : y - GuestRules.SlimeHeight;
        double guestBottom = Kind == GuestKind.Butterfly ? y + GuestRules.ButterflyHalf : y;
        return guestBottom > cat.Top - GuestRules.Clearance && guestTop < cat.Ground;
    }

    // Time moves on: the way across, the greeting, the fades. The cat's body is never entered.
    public void Step(double dt, GuestCat cat)
    {
        if (!Active || dt <= 0) return;
        Time += dt; PhaseTime += dt;
        if (Hop >= 0 && (Hop += dt) >= GuestRules.HopSeconds) Hop = -1;
        if (Leaving)
        {
            if ((Fade -= dt / GuestRules.FadeOutSeconds) <= 0) { Fade = 0; Phase = GuestPhase.Gone; }
            return;
        }
        Fade = Math.Min(1, Fade + dt / GuestRules.FadeInSeconds);
        if (Time >= GuestRules.MaxSeconds) { Leave(); return; }
        if (Kind == GuestKind.Butterfly) StepButterfly(dt, cat); else StepSlime(dt, cat);
        if (Phase != GuestPhase.Greeting && (X < Left - 60 || X > Right + 60 || Y < Top - 60)) Phase = GuestPhase.Gone;
    }

    private void StepButterfly(double dt, GuestCat cat)
    {
        double speed = Speed * (Zooming ? GuestRules.ZoomScale : 1);
        double nx = X + Direction * speed * dt;
        if (Zooming) baseline -= GuestRules.ZoomRise * dt;
        double ny = baseline + GuestRules.ButterflyBob * Math.Sin(1.8 * Time + bobPhase) + 6 * Math.Sin(4.1 * Time + bobPhase);
        // Over the cat it keeps above its head.
        double ceiling = cat.Top - GuestRules.Clearance - GuestRules.ButterflyHalf - 1;
        if (Math.Abs(nx - cat.Center) < GuestRules.CatHalfWidth + GuestRules.ButterflyHalf + GuestRules.Clearance && ny > ceiling) ny = ceiling;
        ny = Math.Max(ny, Top - 80);
        X = nx; Y = ny;
        if (Phase == GuestPhase.Coming && Direction * (X - cat.Center) > 0) { Phase = GuestPhase.Going; PhaseTime = 0; }
    }

    private void StepSlime(double dt, GuestCat cat)
    {
        if (Phase == GuestPhase.Greeting)
        {
            if (PhaseTime < GuestRules.GreetSeconds) return;
            Greeted = true;
            Phase = GuestPhase.Going; PhaseTime = 0;
            // Meeting the cat on the floor, it goes back the way it came (it never crawls through the cat).
            if (TurnsBack || BlockedAhead(cat)) Direction = -Direction;
            return;
        }
        if (Hop >= 0) return; // A click's bounce: it waits on the spot.
        double nx = X + Direction * Speed * dt;
        if (Phase == GuestPhase.Coming && Direction * (nx - stopX) >= 0) { nx = stopX; Phase = GuestPhase.Greeting; PhaseTime = 0; }
        if (Touches(nx, Y, cat))
        {
            // The cat came in the way (dropped or thrown there): it never crawls in; it greets, or turns away.
            if (Phase == GuestPhase.Coming) { Phase = GuestPhase.Greeting; PhaseTime = 0; }
            else Direction = X < cat.Center ? -1 : 1;
            return;
        }
        X = nx;
    }

    private bool BlockedAhead(GuestCat cat)
    {
        double far = Direction > 0 ? Right + 60 : Left - 60;
        double low = Math.Min(X, far), high = Math.Max(X, far);
        bool lowEnough = cat.Ground > Floor - GuestRules.SlimeHeight - GuestRules.Clearance;
        return lowEnough && cat.Center + GuestRules.CatHalfWidth > low && cat.Center - GuestRules.CatHalfWidth < high;
    }

    // A click on the guest: the butterfly zooms away (with sparkles, drawn by the art); the slime bounces once
    // and goes on. False when nothing happens (already leaving).
    public bool Click()
    {
        if (!Active || Leaving) return false;
        if (Kind == GuestKind.Butterfly)
        {
            if (Zooming) return false;
            Zooming = true;
            if (Phase == GuestPhase.Coming) { Phase = GuestPhase.Going; PhaseTime = 0; }
            return true;
        }
        if (Hop >= 0) return false;
        Hop = 0;
        return true;
    }

    // Fades out at once and is gone (asleep, a fullscreen app, a battle, the switch off...).
    public void Leave() { if (Active) Leaving = true; }

    public void Reset() { Phase = GuestPhase.Gone; Leaving = Zooming = false; Hop = -1; Fade = 0; }
}

// The cat's side of a visit: whether it follows this time, how far it went, its single pounce at a butterfly.
public sealed class GuestWatcher
{
    public bool Follows { get; private set; }
    public bool Chasing { get; private set; }
    public bool Chased { get; private set; }
    public bool Pounced { get; private set; }
    public double ChaseTime { get; private set; }
    public double Start { get; private set; }

    public void Reset(Random random) { Follows = random.NextDouble() < GuestRules.ChaseChance; Chasing = Chased = Pounced = false; ChaseTime = 0; }

    public void ForceFollow(bool follows) => Follows = follows;

    // The guest is close enough: the cat starts following (once a visit).
    public bool BeginChase(double catCenter, double guestX)
    {
        if (!Follows || Chased || Chasing || Math.Abs(guestX - catCenter) > GuestRules.ChaseFrom) return false;
        Chasing = true; Start = catCenter; ChaseTime = 0;
        return true;
    }

    // One frame of the chase: false once it is over (3 seconds, or 150 DIPs walked).
    public bool StepChase(double dt, double catCenter)
    {
        if (!Chasing) return false;
        ChaseTime += dt;
        if (ChaseTime >= GuestRules.ChaseSeconds || Math.Abs(catCenter - Start) >= GuestRules.ChaseDistance) { EndChase(); return false; }
        return true;
    }

    public void EndChase() { if (Chasing) { Chasing = false; Chased = true; } }

    public bool MayPounce(double catCenter, double guestX) => !Pounced && (Chasing || Chased) && Math.Abs(guestX - catCenter) <= GuestRules.PounceNear;

    public void Pounce() { Pounced = true; EndChase(); }
}
