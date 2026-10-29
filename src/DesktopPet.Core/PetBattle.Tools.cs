using System;

namespace DesktopPet;

// 1.5 M05, docs/UPDATE-1.5.md (K) and docs/BATTLE-DATA.md → Đèn laser và Quạt: the two skills used
// with the mouse on the field.
// - Laser (SkillAim.Point): chosen, then a click on a spot of the field puts a red dot there for
//   LaserSeconds. Every enemy within LaserLureRange of it that is free to walk (not a boss, not riding,
//   in the air, stuck or healing) goes for the dot instead of the cat and starts no attack meanwhile
//   (one already wound up still lands). One standing on the dot loses LaserDps HP a second, dealt every
//   ToolTick s. Then the usual cooldown.
// - Fan (SkillAim.Hold): chosen, then held on the field it blows every enemy within FanRange away from
//   the cursor (stronger near it; its pushFactor; an elite that ignores Push barely moves; never a boss,
//   a stuck, riding or flying one). One blown into another or the edge (a wall too) loses FanBumpDamage
//   (each at most once a BumpSeconds); a rhythm's beat count is lost in the wind. Its air lasts
//   FanSteamSeconds of blowing and comes back while it is not; spent, it waits FanRefillSeconds (its
//   cooldown) before blowing again. It stays chosen after the mouse is let go.
// 1.5 M06 (docs/BATTLE-DATA.md → Kẹp gắp, Kéo cắt, Bình xịt nước; the Grabber: PetBattle.Grabber.cs):
// - Scissors (SkillAim.Swipe): chosen, then a stroke across the field (at least ScissorsMinLength DIPs)
//   cuts every tether rope it crosses (CutLink: the ally is dazed) and hurts every enemy whose body it
//   crosses by ScissorsDamage, once each. A shorter stroke does nothing (still chosen). Then the cooldown.
// - Spray (SkillAim.Hold, like the Fan): held on the field it soaks every enemy within SprayRange of the
//   cursor: wet for SpraySoakSeconds (drawn smaller; ice and spikes go soft with it from M15/M16),
//   chilled by SprayChill and SprayDps HP a second, both every ToolTick s. Its water lasts
//   SprayTankSeconds and refills like the Fan's air (SprayRefillSeconds once spent).
// Their damage is a skill's (DamageSource.Tool): never blocked by a rock, Execute applies, a defeat is
// the cat's; a boss takes BossToolShare. Like every skill they wait while the cat is in the user's hands
// (no air comes back, no wind); Pause and the frozen guide freeze them (time runs only in Step).
public sealed partial class BattleSim
{
    public const double ToolTick = 0.5, BumpSeconds = 1, BossToolShare = 0.5, FanImmuneShare = 0.2, BumpSpeed = 60;
    // The laser's dot (null: none) and its time left; the fan's cursor while blowing (null: not blowing)
    // and its air (1: full, 0: spent).
    public double? LaserX { get; private set; }
    public double LaserRemaining { get; private set; }
    public double? FanX { get; private set; }
    public double FanSteam => fanSteam;
    private double fanSteam = 1;
    // The spray's cursor while spraying (battle x and height over the floor; null: not spraying) and its
    // water (1: full); the strokes of the Scissors and the ropes they cut.
    public double? SprayX { get; private set; }
    public double SprayHeight { get; private set; }
    public double SprayTank => sprayTank;
    private double sprayTank = 1, sprayClock;
    public int Snips { get; private set; }
    public int RopesCut { get; private set; }
    public double ToolDamage { get; private set; }
    public int FanBumps { get; private set; }
    private double laserClock;

    // A spot skill (the Laser, chosen first) at x on the floor.
    public bool UseAt(BattleSkill skill, double x)
    {
        if (BattleSkills.Get(skill).Aim != SkillAim.Point || Armed != skill || !Ready(skill) || !double.IsFinite(x)) return false;
        LaserX = Math.Clamp(x, Left, Right);
        LaserRemaining = LaserSeconds;
        laserClock = 0;
        // At once, so no slime near the dot winds up in the frame it appears.
        foreach (var s in slimes) s.Lured = Lurable(s) && Math.Abs(s.X - LaserX.Value) <= Settings.LaserLureRange;
        Recharge(skill);
        SkillUsed();
        if (Armed == skill) Armed = null;
        events.Add(new(BattleEventKind.LaserOn, LaserX.Value));
        return true;
    }

    // The laser's dot lights up a spot this wide around x (for enemies that hide: M13).
    public bool LaserShows(double x, double halfWidth) => LaserX is double dot && Math.Abs(dot - x) <= halfWidth;

    // The mouse pressed (or moved) on the field with the Fan chosen: it blows from x.
    public bool StartFan(double x)
    {
        if (Armed != BattleSkill.Fan || !Ready(BattleSkill.Fan) || !double.IsFinite(x)) return false;
        bool fresh = FanX == null;
        FanX = Math.Clamp(x, Left, Right);
        if (fresh) { events.Add(new(BattleEventKind.FanOn, FanX.Value)); SkillUsed(); }
        return true;
    }
    public void MoveFan(double x) { if (FanX != null && double.IsFinite(x)) FanX = Math.Clamp(x, Left, Right); }
    public void StopFan() => FanX = null;
    public bool FanBlowing => FanX != null;

    // Every frame (not paused): the dot's time, the lure flags, the wind and the air.
    private void StepTools(double dt, bool catHeld)
    {
        foreach (var s in slimes)
        {
            s.Lured = false;
            s.Blown = Math.Max(0, s.Blown - dt);
            s.BumpCooldown = Math.Max(0, s.BumpCooldown - dt);
            s.WetRemaining = Math.Max(0, s.WetRemaining - dt);
        }
        if (LaserX is double dot)
        {
            LaserRemaining = Math.Max(0, LaserRemaining - dt);
            foreach (var s in slimes) s.Lured = Lurable(s) && Math.Abs(s.X - dot) <= Settings.LaserLureRange;
            laserClock += dt;
            bool ended = LaserRemaining <= 0;
            if (laserClock >= ToolTick - 1e-9 || ended)
            {
                double run = laserClock;
                laserClock = 0;
                foreach (var s in slimes.ToArray())
                    if (s.Targetable && !s.Airborne && !s.Burrowed && Math.Abs(s.X - dot) <= s.HitWidth / 2 + 6) ToolHit(s, Settings.LaserDps * run);
            }
            if (ended)
            {
                LaserX = null;
                foreach (var s in slimes) s.Lured = false;
                events.Add(new(BattleEventKind.LaserOff, dot));
            }
        }
        if (catHeld) { FanX = null; SprayX = null; return; }
        if (FanX is double from && Holding(BattleSkill.Fan))
        {
            fanSteam = Math.Max(0, fanSteam - dt / Settings.FanSteamSeconds);
            Blow(from, dt);
            if (puddles.Count > 0) DryPuddles(from);
            if (fanSteam <= 0)
            {
                FanX = null;
                Spend(BattleSkill.Fan);
                events.Add(new(BattleEventKind.FanSpent, from));
            }
        }
        else { FanX = null; Refill(BattleSkill.Fan, ref fanSteam, dt); }
        if (SprayX is double at && Holding(BattleSkill.Spray))
        {
            sprayTank = Math.Max(0, sprayTank - dt / SprayTankSeconds);
            Soak(at, SprayHeight, dt);
            if (Over) return;
            if (sprayTank <= 0)
            {
                SprayX = null;
                Spend(BattleSkill.Spray);
                events.Add(new(BattleEventKind.SpraySpent, at));
            }
        }
        else { SprayX = null; sprayClock = 0; Refill(BattleSkill.Spray, ref sprayTank, dt); }
        Bumps();
    }

    // A hold skill (Fan, Spray) works this step: pressed on the field, still chosen, not cooling down.
    private bool Holding(BattleSkill skill) => Armed == skill && cooldowns[(int)skill] <= 0;
    // Used up: put away and cooling down (its tank refills with the cooldown).
    private void Spend(BattleSkill skill) { Armed = null; Recharge(skill); }
    // Not in use: spent, the tank comes back with the cooldown; otherwise at the same pace.
    private void Refill(BattleSkill skill, ref double tank, double dt)
    {
        double refill = Math.Max(0.01, SkillCooldown(skill)), left = cooldowns[(int)skill];
        tank = left > 0 ? 1 - left / refill : Math.Min(1, tank + dt / refill);
    }

    // The mouse pressed (or moved) on the field with the Spray chosen: it sprays at (x, height).
    public bool StartSpray(double x, double height = 20)
    {
        if (Armed != BattleSkill.Spray || !Ready(BattleSkill.Spray) || !double.IsFinite(x) || !double.IsFinite(height)) return false;
        bool fresh = SprayX == null;
        SprayX = Math.Clamp(x, Left, Right);
        SprayHeight = Math.Max(0, height);
        if (fresh) { events.Add(new(BattleEventKind.SprayOn, SprayX.Value)); SkillUsed(); }
        return true;
    }
    public void MoveSpray(double x, double height)
    {
        if (SprayX == null || !double.IsFinite(x) || !double.IsFinite(height)) return;
        SprayX = Math.Clamp(x, Left, Right);
        SprayHeight = Math.Max(0, height);
    }
    public void StopSpray() => SprayX = null;
    public bool Spraying => SprayX != null;

    // Where an enemy's body is: its edges (battle x) and its bottom and top over the floor.
    private static (double Left, double Bottom, double Right, double Top) Body(Slime s)
    {
        double bottom = s.Altitude + s.LeapHeight + s.RideHeight + s.Lift;
        return (s.X - s.HitWidth / 2, bottom, s.X + s.HitWidth / 2, bottom + s.Height);
    }

    // Soaking every enemy near the cursor; its chill and damage every ToolTick.
    private void Soak(double x, double height, double dt)
    {
        double range = Settings.SprayRange;
        sprayClock += dt;
        bool tick = sprayClock >= ToolTick - 1e-9;
        double run = sprayClock;
        if (tick) sprayClock = 0;
        foreach (var s in slimes.ToArray())
        {
            if (!s.Targetable || s.Shielded) continue;
            var (left, bottom, right, top) = Body(s);
            double dx = Math.Max(0, Math.Max(left - x, x - right)), dy = Math.Max(0, Math.Max(bottom - height, height - top));
            if (dx * dx + dy * dy > range * range) continue;
            bool fresh = !s.Wet;
            s.WetRemaining = Settings.SpraySoakSeconds;
            if (fresh || tick) ApplyStatus(s, SlimeStatus.Chill, Settings.SpraySoakSeconds, Settings.SprayChill);
            if (tick) ToolHit(s, Settings.SprayDps * run);
            if (Over) return;
        }
    }

    // A stroke of the Scissors from (x0, h0) to (x1, h1) (battle x, height over the floor).
    public bool Snip(double x0, double h0, double x1, double h1)
    {
        if (Armed != BattleSkill.Scissors || !Ready(BattleSkill.Scissors) || !double.IsFinite(x0) || !double.IsFinite(h0) || !double.IsFinite(x1) || !double.IsFinite(h1)) return false;
        if (Math.Sqrt((x1 - x0) * (x1 - x0) + (h1 - h0) * (h1 - h0)) < Settings.ScissorsMinLength) return false;
        int done = 0;
        // Ropes first (the cut below may defeat a holder, and its rope with it).
        foreach (var holder in slimes.ToArray())
            if (Is(holder, EnemyLinks.Tether) && LinkPartner(holder) is Slime ally && RopeCrossed(holder, ally, x0, h0, x1, h1) && CutLink(holder.Id)) { RopesCut++; done++; }
        // A boss's ropes to its helpers (1.5 M17).
        int bossRopes = SnipBossRopes(x0, h0, x1, h1);
        RopesCut += bossRopes; done += bossRopes;
        foreach (var s in slimes.ToArray())
        {
            if (!s.Targetable || s.Shielded) continue;
            var (left, bottom, right, top) = Body(s);
            if (!Crosses(x0, h0, x1, h1, left - 4, bottom - 4, right + 4, top + 4)) continue;
            // A bubble the stroke goes through bursts (no hurt; 1.5 M13).
            if (s.Bubbled) { PopBubble(s); done++; continue; }
            AddEffect(BattleEffectKind.Spark, s.X, bottom + s.Height / 2);
            ToolHit(s, ScissorsDamage);
            done++;
            if (Over) break;
        }
        Snips++;
        Spend(BattleSkill.Scissors);
        SkillUsed();
        events.Add(new(BattleEventKind.Snipped, (x0 + x1) / 2, done));
        return true;
    }

    // The rope as drawn (PetBattleView.Links.cs, without its sway): from body to body at 45% of their
    // height, sagging in the middle (never below the floor line: a stroke along the floor still meets
    // it); crossed when the stroke comes within `pad` DIPs of it.
    private static bool RopeCrossed(Slime holder, Slime ally, double x0, double h0, double x1, double h1, double pad = 6)
    {
        double ax = holder.X, ay = Body(holder).Bottom + holder.Height * 0.45, bx = ally.X, by = Body(ally).Bottom + ally.Height * 0.45;
        double sag = Math.Min(28, Math.Abs(bx - ax) * 0.18);
        double px = ax, py = ay;
        for (int k = 1; k <= 10; k++)
        {
            double t = k / 10.0, qx = ax + (bx - ax) * t, qy = Math.Max(2, ay + (by - ay) * t - sag * 4 * t * (1 - t));
            if (SegmentGap(x0, h0, x1, h1, px, py, qx, qy) <= pad) return true;
            px = qx; py = qy;
        }
        return false;
    }

    // The shortest distance between two segments (0: they cross).
    internal static double SegmentGap(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
    {
        static double Cross(double ox, double oy, double px, double py, double qx, double qy) => (px - ox) * (qy - oy) - (py - oy) * (qx - ox);
        double d1 = Cross(cx, cy, dx, dy, ax, ay), d2 = Cross(cx, cy, dx, dy, bx, by), d3 = Cross(ax, ay, bx, by, cx, cy), d4 = Cross(ax, ay, bx, by, dx, dy);
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return 0;
        static double ToSegment(double px, double py, double sx, double sy, double ex, double ey)
        {
            double vx = ex - sx, vy = ey - sy, len = vx * vx + vy * vy;
            double t = len <= 1e-12 ? 0 : Math.Clamp(((px - sx) * vx + (py - sy) * vy) / len, 0, 1);
            double qx = sx + vx * t - px, qy = sy + vy * t - py;
            return Math.Sqrt(qx * qx + qy * qy);
        }
        return Math.Min(Math.Min(ToSegment(ax, ay, cx, cy, dx, dy), ToSegment(bx, by, cx, cy, dx, dy)),
                        Math.Min(ToSegment(cx, cy, ax, ay, bx, by), ToSegment(dx, dy, ax, ay, bx, by)));
    }

    // A lured enemy walks to the dot (its own speed) and waits on it; no attack meanwhile.
    private void Lure(Slime slime, double dot, double dt, double minX, double maxX)
    {
        double gap = dot - slime.X;
        if (Math.Abs(gap) > 1) slime.Facing = gap < 0 ? -1 : 1;
        if (Math.Abs(gap) > 4) slime.X = Math.Clamp(slime.X + Math.Sign(gap) * Math.Min(Math.Abs(gap), Speed(slime) * dt), minX, maxX);
    }

    private static bool Lurable(Slime s) => s.Targetable && !s.IsBoss && !s.Riding && !s.Airborne && !s.Handled && !s.Stuck && !s.Burrowed && s.LeapHeight <= 0
        && s.State is not (SlimeState.Casting or SlimeState.Spawning) && !s.Shielded;

    private void Blow(double from, double dt)
    {
        double range = FanRange;
        foreach (var s in slimes)
        {
            if (!s.Targetable || s.IsBoss || s.Stuck || s.Riding || s.Airborne || s.Handled || s.Burrowed || s.LeapHeight > 0 || s.Shielded) continue;
            double d = s.X - from;
            if (Math.Abs(d) > range + s.Width / 2) continue;
            int away = Math.Abs(d) >= 1 ? Math.Sign(d) : s.X >= lastCatX ? 1 : -1;
            double strength = Math.Clamp(1 - Math.Max(0, Math.Abs(d) - s.Width / 2) / range, 0.25, 1);
            double push = FanSpeed * strength * s.Type.PushFactor * (s.Elite is { PushImmune: true } ? FanImmuneShare : 1);
            if (push <= 0) continue;
            if (s.KnockVelocity * away < push) s.KnockVelocity = away * push;
            s.Blown = 0.3;
            // The wind throws a drummer off its count.
            if (Is(s, EnemyLinks.Rhythm)) { s.BeatCount = 0; s.BeatClock = 0; }
        }
    }

    // A blown enemy going fast into another or the edge (a wall counts) is hurt, and so is the other.
    private void Bumps()
    {
        int count = slimes.Count;
        for (int i = 0; i < count; i++)
        {
            var s = slimes[i];
            if (s.Blown <= 0 || !s.Targetable || s.Handled || Math.Abs(s.KnockVelocity) < BumpSpeed) continue;
            int way = Math.Sign(s.KnockVelocity);
            var (minX, maxX) = Bounds(s);
            Slime? into = null;
            foreach (var o in slimes)
            {
                if (o == s || !o.Targetable || o.Airborne || o.Riding || o.Handled || o.Burrowed) continue;
                double ahead = (o.X - s.X) * way;
                // Not one going along with it as fast (two blown the same way do not bump).
                if (ahead > 0 && ahead <= (o.Width + s.Width) / 2 * 0.7 + 4 && (s.KnockVelocity - o.KnockVelocity) * way >= BumpSpeed) { into = o; break; }
            }
            bool edge = way > 0 ? s.X >= maxX - 0.5 : s.X <= minX + 0.5;
            if (into == null && !edge) continue;
            if (s.BumpCooldown <= 0)
            {
                s.BumpCooldown = BumpSeconds;
                FanBumps++;
                AddEffect(BattleEffectKind.Spark, s.X + way * s.Width / 2, s.Height * 0.5);
                events.Add(new(BattleEventKind.FanBump, s.X, s.Id));
                ToolHit(s, Settings.FanBumpDamage);
            }
            if (into is { Targetable: true, BumpCooldown: <= 0 })
            {
                into.BumpCooldown = BumpSeconds;
                ToolHit(into, Settings.FanBumpDamage);
            }
            s.KnockVelocity = 0;
            if (Over) return;
        }
    }

    private void ToolHit(Slime s, double amount)
    {
        if (amount <= 0 || !s.Targetable || Over) return;
        if (s.IsBoss) amount *= BossToolShare;
        if (executeBonus > 0 && s.Hp < s.MaxHp * ExecuteBelow) { amount *= 1 + executeBonus; ExecuteHits++; }
        ToolDamage += Math.Min(amount, s.Hp);
        Damage(s, amount, DamageSource.Tool);
    }

    // The battle is over or the field moved (SetArea maps the spots).
    private void ClearTools()
    {
        LaserX = null; LaserRemaining = 0; laserClock = 0;
        FanX = null;
        SprayX = null; sprayClock = 0;
        foreach (var s in slimes) { s.Lured = false; s.Blown = 0; s.WetRemaining = 0; }
        ClearGrabber();
    }

    private void MapTools(double left, double oldWidth, double newWidth)
    {
        if (LaserX is double dot) LaserX = left + (dot - Left) / oldWidth * newWidth;
        if (FanX is double fan) FanX = left + (fan - Left) / oldWidth * newWidth;
        if (SprayX is double spray) SprayX = left + (spray - Left) / oldWidth * newWidth;
        MapGrab(left, oldWidth, newWidth);
    }
}
