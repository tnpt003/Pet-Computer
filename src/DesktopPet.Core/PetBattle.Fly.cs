using System;
using System.Collections.Generic;

namespace DesktopPet;

// 1.5 M14, S5 the flying slime (EnemyType.Fly), docs/UPDATE-1.5.md and docs/APPROVED-EXCEPTIONS.md →
// Battle Mode: it flies from spot to spot anywhere over the battle's screen (SetSky: how high the screen
// goes over the floor and the windows it keeps clear of), out of reach from the floor (Altitude > 0:
// the cat on the floor never goes for it; the cat in the user's hand swings at it, PetBattle.Held.cs).
// Every so often it hovers still and aims at the cat, then shoots drops of sticky goo straight at where
// the cat was. A drop reaching the cat hurts it even in the user's hand or under landing protection
// (the one exception to "no hit while held"; the Shield and the Turtle shell still take it): never a
// stun and never out of the hand; on the floor the cat is slowed a moment. Stunned (Push, the
// Grabber's yank, enough held hits in a row, any daze) it falls to the floor, lies dazed there taking
// more from every hit, crouches and flies up again. The cat's physics are only read (its pose).
// Its own random stream: battles without flyers play exactly as before.
public sealed partial class BattleSim
{
    // How high the screen goes over the battle floor (DIPs) when the window has not said.
    public const double DefaultSky = 700, SkyMargin = 30, CatStandHeight = 34, ShotGap = 0.3, ShotOverrun = 1.2;
    private readonly List<BattleShot> shots = new();
    private readonly List<(double Left, double Bottom, double Right, double Top)> skyBlockers = new();
    private Random? skyRandom;
    // The cat in the user's hand this step (null: on the floor), for the flyers' aim and drops.
    private CatPose? catPose;
    private double catSlow = 1;
    public IReadOnlyList<BattleShot> Shots => shots;
    public double SkyHeight { get; private set; } = DefaultSky;
    // Seconds the cat stays slowed by a drop of goo (on the floor only).
    public double CatSlowRemaining { get; private set; }
    public int FlyShots { get; private set; }
    public int FlyShotHits { get; private set; }
    public int FlyersKnocked { get; private set; }

    // The window: how high its screen goes over the floor (DIPs) and the battle windows over it (the HUD,
    // the card offer, the bag) in battle x and heights over the floor. A flyer never picks a spot in them.
    public void SetSky(double height, IEnumerable<(double Left, double Bottom, double Right, double Top)>? blockers = null)
    {
        if (double.IsFinite(height)) SkyHeight = Math.Max(Settings.SlimeHeight * 2, height);
        skyBlockers.Clear();
        if (blockers != null) skyBlockers.AddRange(blockers);
        foreach (var s in slimes)
        {
            if (s.Type.Fly == null) continue;
            double top = Math.Max(1, SkyHeight - s.Height - SkyMargin);
            s.Altitude = Math.Min(s.Altitude, top);
            s.FlyToHeight = Math.Min(s.FlyToHeight, top);
        }
    }

    // Where the cat's centre is now (battle x, height over the floor): in the user's hand, or standing.
    public (double X, double Height) CatCentre() => catPose is CatPose cat ? (cat.X, cat.Height) : (lastCatX, CatStandHeight);

    // A flyer coming into the battle: at a height in its sky, its first spot picked.
    private void JoinSky(Slime s, FlySpec fly)
    {
        skyRandom ??= BattleRandom.Stream(Seed, BattleRandom.Sky);
        var (low, high) = SkyBand(s, fly);
        s.Fly = FlyPhase.Aloft;
        s.Altitude = low + skyRandom.NextDouble() * (high - low);
        s.FlyToX = s.X; s.FlyToHeight = s.Altitude;
        s.FlyClock = 0.6;
        s.ShotClock = fly.Cooldown / 2;
    }

    // The heights its bottom may fly at: MinHeight up to the top of the screen (a low screen: lower).
    private (double Low, double High) SkyBand(Slime s, FlySpec fly)
    {
        double high = Math.Max(1, SkyHeight - s.Height - SkyMargin);
        return (Math.Min(fly.MinHeight, high), high);
    }

    private void PickSpot(Slime s, FlySpec fly)
    {
        skyRandom ??= BattleRandom.Stream(Seed, BattleRandom.Sky);
        var (low, high) = SkyBand(s, fly);
        double minX = Left + s.Width / 2, maxX = Math.Max(minX, Right - s.Width / 2);
        double x = s.X, h = s.Altitude;
        for (int tries = 0; tries < 12; tries++)
        {
            x = minX + skyRandom.NextDouble() * (maxX - minX);
            h = low + skyRandom.NextDouble() * (high - low);
            double far = Math.Abs(x - s.X) + Math.Abs(h - s.Altitude);
            if ((far >= 160 || tries >= 8) && !InBlocker(x, h, s)) break;
        }
        s.FlyToX = x; s.FlyToHeight = h;
        s.FlyClock = fly.Move * (0.8 + 0.4 * skyRandom.NextDouble());
    }

    private bool InBlocker(double x, double h, Slime s)
    {
        foreach (var (left, bottom, right, top) in skyBlockers)
            if (x + s.Width / 2 + 12 > left && x - s.Width / 2 - 12 < right && h + s.Height + 12 > bottom && h - 12 < top) return true;
        return false;
    }

    // Every step of a flyer that goes on (from StepSlimes). True: its step is done here (aloft, falling,
    // crouching to rise); false: it is on the floor and steps as any slime (dazed, or hit back at).
    private bool StepFlyer(Slime s, FlySpec fly, double dt)
    {
        switch (s.Fly)
        {
            case FlyPhase.Aloft:
                if (s.State == SlimeState.Spawning) return false;
                if (s.State == SlimeState.Stunned) { KnockDown(s, false); return true; }
                if (s.State != SlimeState.Moving) s.Set(SlimeState.Moving);
                double act = dt * s.ChillFactor;
                if (!s.Aiming) FlyAbout(s, fly, dt);
                Shoot(s, fly, act);
                return true;
            case FlyPhase.Falling:
                s.FallSpeed += Gravity * dt;
                s.Altitude = Math.Max(0, s.Altitude - s.FallSpeed * dt);
                if (s.Altitude > 0) return true;
                s.Fly = FlyPhase.Downed;
                s.FallSpeed = 0;
                s.Set(SlimeState.Stunned);
                s.StunRemaining = fly.Downed;
                AddEffect(BattleEffectKind.Thud, s.X, 0);
                events.Add(new(BattleEventKind.FlyerDowned, s.X, s.Id));
                return true;
            case FlyPhase.Downed:
                if (s.State != SlimeState.Moving) return false;
                s.Fly = FlyPhase.Rising;
                s.FlyClock = fly.Rise;
                return true;
            default: // Rising: a crouch on the floor (it can still be hit there), then up.
                if (s.State == SlimeState.Stunned) { s.Fly = FlyPhase.Downed; return false; }
                if (s.State != SlimeState.Moving) return false;
                s.FlyClock -= dt * s.ChillFactor;
                if (s.FlyClock > 0) return true;
                s.Fly = FlyPhase.Aloft;
                s.Altitude = 1;
                s.HeldHitCount = 0;
                s.ShotClock = fly.Cooldown / 2;
                PickSpot(s, fly);
                if (SelectedId == s.Id) SelectedId = null;
                events.Add(new(BattleEventKind.FlyerRose, s.X, s.Id));
                return true;
        }
    }

    private void FlyAbout(Slime s, FlySpec fly, double dt)
    {
        s.FlyClock -= dt;
        if (s.FlyClock <= 0) PickSpot(s, fly);
        double dx = s.FlyToX - s.X, dh = s.FlyToHeight - s.Altitude, gap = Math.Sqrt(dx * dx + dh * dh);
        if (Math.Abs(dx) > 1) s.Facing = dx < 0 ? -1 : 1;
        if (gap < 0.5) return;
        double step = Math.Min(gap, fly.FlySpeed * s.ChillFactor * s.AuraSpeed * dt);
        s.X += dx / gap * step;
        s.Altitude = Math.Clamp(s.Altitude + dh / gap * step, 1, Math.Max(1, SkyHeight - s.Height - SkyMargin));
    }

    // Its shots: a wait, an aim (hovering still), then its drops one after another.
    private void Shoot(Slime s, FlySpec fly, double act)
    {
        if (s.Aiming)
        {
            s.AimLeft = Math.Max(0, s.AimLeft - act);
            if (s.AimLeft > 0) return;
            s.ShotsLeft = s.Elite != null ? fly.EliteShots : 1;
            s.ShotClock = 0;
        }
        s.ShotClock -= act;
        if (s.ShotsLeft > 0)
        {
            if (s.ShotClock > 0) return;
            Fire(s, fly);
            s.ShotsLeft--;
            s.ShotClock = s.ShotsLeft > 0 ? ShotGap : fly.Cooldown;
            return;
        }
        if (s.ShotClock > 0) return;
        var (catX, _) = CatCentre();
        s.Facing = catX < s.X ? -1 : 1;
        s.AimLeft = fly.Aim;
        events.Add(new(BattleEventKind.FlyAimed, s.X, s.Id));
    }

    // Where its drop flies from: its centre.
    public static (double X, double Height) Muzzle(Slime s) => (s.X, s.Altitude + s.Height / 2);

    private void Fire(Slime s, FlySpec fly)
    {
        var (fromX, fromH) = Muzzle(s);
        var (toX, toH) = CatCentre();
        double dx = toX - fromX, dh = toH - fromH, d = Math.Max(1, Math.Sqrt(dx * dx + dh * dh));
        shots.Add(new BattleShot
        {
            OwnerId = s.Id, X = fromX, Height = fromH, VX = dx / d * fly.ShotSpeed, VY = dh / d * fly.ShotSpeed,
            Remaining = d / fly.ShotSpeed + ShotOverrun, Damage = s.Damage, Radius = fly.Radius, Slow = fly.Slow, SlowSeconds = fly.SlowSeconds, Tint = s.Type.Color
        });
        FlyShots++;
        events.Add(new(BattleEventKind.FlyShot, s.X, s.Id));
    }

    // Before the slimes move: drops in flight go on and hit the cat in their way (a segment, so a fast
    // drop cannot skip over it), splat on the floor or fly off the screen.
    private void StepShots(double dt)
    {
        CatSlowRemaining = Math.Max(0, CatSlowRemaining - dt);
        if (shots.Count == 0) return;
        var (catX, catH) = CatCentre();
        for (int i = 0; i < shots.Count; i++)
        {
            var shot = shots[i];
            double x0 = shot.X, h0 = shot.Height;
            shot.X += shot.VX * dt; shot.Height += shot.VY * dt;
            shot.Remaining -= dt;
            if (Near(x0, h0, shot.X, shot.Height, catX, catH) <= shot.Radius)
            {
                shots.RemoveAt(i--);
                ShotHit(shot, catX);
                if (Over) return;
                continue;
            }
            if (shot.Height <= 0)
            {
                shots.RemoveAt(i--);
                AddEffect(BattleEffectKind.Splat, shot.X, 4);
                effects[^1].Tint = shot.Tint;
                continue;
            }
            if (shot.Remaining <= 0 || shot.X < Left - 200 || shot.X > Right + 200 || shot.Height > SkyHeight + 200) shots.RemoveAt(i--);
        }
    }

    // The distance from (px, ph) to the segment (x0, h0)–(x1, h1).
    internal static double Near(double x0, double h0, double x1, double h1, double px, double ph)
    {
        double dx = x1 - x0, dh = h1 - h0, length = dx * dx + dh * dh;
        double t = length < 1e-9 ? 0 : Math.Clamp(((px - x0) * dx + (ph - h0) * dh) / length, 0, 1);
        double ex = x0 + dx * t - px, eh = h0 + dh * t - ph;
        return Math.Sqrt(ex * ex + eh * eh);
    }

    private void ShotHit(BattleShot shot, double catX)
    {
        FlyShotHits++;
        bool held = catPose != null || CatLifted;
        bool through = ShieldRemaining <= 0 && !ShellReady;
        events.Add(new(BattleEventKind.FlyShotHit, catX, held ? 1 : 0));
        AddEffect(BattleEffectKind.Splat, catX, CatCentre().Height);
        effects[^1].Tint = shot.Tint;
        // Its flyer still there: a hit like its own (Thorns sends some back), never a stun; gone, the drop still hurts.
        if (Find(shot.OwnerId) is { Targetable: true, Damage: > 0 } owner) HurtCat(owner, catX, false, shot.Damage / owner.Damage);
        else LandHit(shot.Damage, catX, out _);
        if (through && !held && shot.SlowSeconds > 0) { CatSlowRemaining = Math.Max(CatSlowRemaining, shot.SlowSeconds); catSlow = shot.Slow; }
    }

    // The cat's speed factor from the goo (1: not slowed; never while lifted).
    private double GooSlow => CatSlowRemaining > 0 && !CatLifted ? catSlow : 1;

    // Stunned in the air (or yanked down by the Grabber): it falls, and lies dazed where it lands.
    private bool KnockDown(Slime s, bool yanked)
    {
        if (s.Type.Fly == null || s.Fly != FlyPhase.Aloft || !s.Targetable) return false;
        s.Fly = FlyPhase.Falling;
        s.FallSpeed = 0;
        s.AimLeft = 0; s.ShotsLeft = 0;
        s.HeldHitCount = 0;
        if (s.State != SlimeState.Stunned) s.Set(SlimeState.Stunned);
        s.StunRemaining = Math.Max(s.StunRemaining, 0.1);
        FlyersKnocked++;
        AddEffect(BattleEffectKind.Push, s.X, s.Altitude + s.Height / 2);
        events.Add(new(BattleEventKind.FlyerKnocked, s.X, yanked ? 1 : 0));
        return true;
    }

    // A held hit landed on it (PetBattle.Held.cs): enough of them within HeldWindow s knock it down.
    private void FlyerHeldHit(Slime s)
    {
        if (s.Type.Fly is not FlySpec fly || s.Fly != FlyPhase.Aloft || !s.Targetable) return;
        if (s.HeldHitCount == 0 || Time - s.HeldHitClock > fly.HeldWindow) { s.HeldHitCount = 0; s.HeldHitClock = Time; }
        if (++s.HeldHitCount >= (s.Elite != null ? fly.EliteHeldHits : fly.HeldHits)) KnockDown(s, false);
    }

    // A flyer down on the floor takes DownedDamage × every hit.
    private static double DownedFactor(Slime s) => s.Type.Fly is FlySpec fly && s.Fly == FlyPhase.Downed ? fly.DownedDamage : 1;

    // A flyer up in the air (or falling): out of the floor's reach and of everything on it.
    public static bool InSky(Slime s) => s.Type.Fly != null && s.Fly is FlyPhase.Aloft or FlyPhase.Falling;

    private void ClearSky() { shots.Clear(); CatSlowRemaining = 0; }

    private void MapSky(double left, double oldWidth, double newWidth)
    {
        foreach (var shot in shots) shot.X = left + (shot.X - Left) / oldWidth * newWidth;
        foreach (var s in slimes) if (s.Type.Fly != null) s.FlyToX = left + (s.FlyToX - Left) / oldWidth * newWidth;
    }
}
