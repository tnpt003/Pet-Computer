using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// 1.5 M05, docs/UPDATE-1.5.md (K) and docs/BATTLE-DATA.md → Đèn laser và Quạt: the two skills used
// with the mouse on the field.
// - Laser (SkillAim.Point): chosen, then a click on a spot of the field puts a red dot there for
//   LaserSeconds. Every enemy within LaserLureRange of it that is free to walk (not a boss, not riding,
//   in the air, stuck or healing) goes for the dot instead of the cat and starts no attack meanwhile
//   (one already wound up still lands). One standing on the dot loses LaserDps HP a second, dealt every
//   ToolTick s. Then the usual cooldown.
// - Fan and Scissors (SkillAim.Screen, 2026-11-04): aimed anywhere on the battle's screen, see below.
// 1.5 M06 (docs/BATTLE-DATA.md → Kẹp gắp, Kéo cắt, Bình xịt nước; the Grabber: PetBattle.Grabber.cs):
// - Spray (SkillAim.Hold): one press starts it, see StartSpray: it soaks every enemy within SprayRange of
//   its spot: wet for SpraySoakSeconds (drawn smaller; ice and spikes go soft with it from M15/M16),
//   chilled by SprayChill and SprayDps HP a second, both every ToolTick s. Its water lasts
//   SprayTankSeconds, then SprayRefillSeconds.
// Their damage is a skill's (DamageSource.Tool): never blocked by a rock, Execute applies, a defeat is
// the cat's; a boss takes BossToolShare. Like every skill they wait while the cat is in the user's hands
// (no air comes back, no wind); Pause and the frozen guide freeze them (time runs only in Step).
public sealed partial class BattleSim
{
    public const double ToolTick = 0.5, BossToolShare = 0.5;
    // The laser's dot (null: none) and its time left.
    public double? LaserX { get; private set; }
    public double LaserRemaining { get; private set; }
    // The spray's cursor while spraying (battle x and height over the floor; null: not spraying) and its
    // water (1: full); the strokes of the Scissors and the ropes they cut.
    public double? SprayX { get; private set; }
    public double SprayHeight { get; private set; }
    public double SprayTank => sprayTank;
    private double sprayTank = 1, sprayClock;
    public int Snips { get; private set; }
    public int RopesCut { get; private set; }
    public double ToolDamage { get; private set; }
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

    // ── 2026-11-04 (docs/GOPY-C-2026-11-04.md D4): the Scissors and the Fan aimed anywhere on the screen ──

    // A cut across the screen: at height Y, from From to To (the scissors' way), Age since the click.
    public sealed class ScissorCut { public double Y, From, To, Age; }
    // The lowest a cut runs, so the big scissors stay whole over the floor, centred on it. A cut this low
    // reaches down to the floor: a click at the feet still cuts whatever stands there.
    public const double CutFloorHeight = 42;
    public ScissorCut? Cut { get; private set; }
    private readonly HashSet<int> cutHit = new();
    private readonly Random cutRandom;
    public int Cuts { get; private set; }
    // The Fan's column: its middle (battle x; null: none) and its time left.
    public double? ColumnX { get; private set; }
    public double ColumnRemaining { get; private set; }
    public double ColumnWidth => Settings.FanColumnWidth * (1 + fanBonus);
    public int ColumnKnocks { get; private set; }

    // A click anywhere on the battle's screen (battle x, height over the floor) with the Scissors or the
    // Fan chosen.
    public bool UseOnScreen(BattleSkill skill, double x, double height)
    {
        if (Armed != skill || !Ready(skill) || !double.IsFinite(x) || !double.IsFinite(height)) return false;
        if (skill == BattleSkill.Scissors)
        {
            double half = Settings.ScissorsLength / 2, mid = Math.Clamp(x, Left + half, Math.Max(Left + half, Right - half));
            bool fromLeft = cutRandom.Next(2) == 0;
            Cut = new ScissorCut { Y = Math.Max(CutFloorHeight, height), From = fromLeft ? mid - half : mid + half, To = fromLeft ? mid + half : mid - half };
            cutHit.Clear();
            events.Add(new(BattleEventKind.CutStarted, mid));
        }
        else if (skill == BattleSkill.Fan)
        {
            ColumnX = Math.Clamp(x, Left, Right);
            ColumnRemaining = Settings.FanColumnSeconds;
            events.Add(new(BattleEventKind.FanOn, ColumnX.Value));
        }
        else return false;
        Recharge(skill);
        Armed = null;
        SkillUsed();
        return true;
    }

    // The cut's scissors run along it; each enemy whose body meets the band behind them is hit once: it
    // loses ScissorsDamage and everything tied to it is cut (a rope, a ride, a bubble, a drum's count).
    private void StepCut(double dt)
    {
        if (Cut is not ScissorCut cut) return;
        cut.Age += dt;
        if (cut.Age < Settings.ScissorsPreview) return;
        double t = Math.Clamp((cut.Age - Settings.ScissorsPreview) / Settings.ScissorsSweep, 0, 1), at = cut.From + (cut.To - cut.From) * t;
        double lo = Math.Min(cut.From, at), hi = Math.Max(cut.From, at), band = Settings.ScissorsBand / 2, under = cut.Y <= CutFloorHeight ? 0 : cut.Y - band;
        // Ropes the blade has crossed (between two enemies, or to a boss).
        foreach (var holder in slimes.ToArray())
            if (Is(holder, EnemyLinks.Tether) && LinkPartner(holder) is Slime ally && RopeCrossed(holder, ally, lo, cut.Y, hi, cut.Y, band) && CutLink(holder.Id)) RopesCut++;
        RopesCut += SnipBossRopes(lo, cut.Y, hi, cut.Y, band);
        foreach (var s in slimes.ToArray())
        {
            if (cutHit.Contains(s.Id) || !s.Targetable || s.Shielded || s.Burrowed) continue;
            var (left, bottom, right, top) = Body(s);
            if (right < lo || left > hi || top < under || bottom > cut.Y + band) continue;
            cutHit.Add(s.Id);
            if (s.Bubbled) { PopBubble(s); continue; }
            LeafCut(s);
            // A bomb flying back to its mount drops where it is, dazed.
            if (s.RecoilLeft > 0) { s.RecoilLeft = 0; s.LeapHeight = 0; Daze(s, RecoilFallStun); }
            if (s.LinkedTo != 0 || slimes.Any(o => o.LinkedTo == s.Id)) { if (CutLink(s.Id)) RopesCut++; }
            if (s.RopedTo != 0 && CutBossRope(s)) RopesCut++;
            if (Is(s, EnemyLinks.Rhythm)) { s.BeatCount = 0; s.BeatClock = 0; }
            AddEffect(BattleEffectKind.Spark, s.X, bottom + s.Height / 2);
            ToolHit(s, ScissorsDamage);
            if (Over) return;
        }
        if (t < 1) return;
        Cuts++;
        Snips++;
        events.Add(new(BattleEventKind.Snipped, (cut.From + cut.To) / 2, cutHit.Count));
        Cut = null;
    }

    // The column of wind: slimes on the floor in it stand still (their blows lighter, HurtCat); a climber
    // up its edge or a flyer in the air in it is blown down; puddles under it dry.
    private void StepColumn(double dt)
    {
        foreach (var s in slimes) s.Rooted = false;
        if (ColumnX is not double x) return;
        ColumnRemaining = Math.Max(0, ColumnRemaining - dt);
        double half = ColumnWidth / 2;
        foreach (var s in slimes.ToArray())
        {
            if (!s.Targetable || s.IsBoss || s.Handled || s.Burrowed || Math.Abs(s.BodyX - x) > half + s.HitWidth / 2) continue;
            if (s.OnWall && s.Type.Cling != null && s.State != SlimeState.Spawning) { if (KnockOff(s, false)) ColumnKnocks++; continue; }
            if (s.Type.Fly != null && s.Fly == FlyPhase.Aloft && s.State != SlimeState.Spawning) { if (KnockDown(s, false)) ColumnKnocks++; continue; }
            if (s.Airborne) continue;
            s.Rooted = true;
            s.KnockVelocity = 0;
            // The wind throws a drummer off its count.
            if (Is(s, EnemyLinks.Rhythm)) { s.BeatCount = 0; s.BeatClock = 0; }
        }
        if (puddles.Count > 0) DryPuddles(x);
        if (ColumnRemaining > 0) return;
        ColumnX = null;
        foreach (var s in slimes) s.Rooted = false;
        events.Add(new(BattleEventKind.FanSpent, x));
    }

    // Every frame (not paused): the dot's time, the lure flags, the wind and the air.
    private void StepTools(double dt, bool catHeld)
    {
        StepCut(dt);
        if (Over) return;
        StepColumn(dt);
        if (Over) return;
        foreach (var s in slimes)
        {
            s.Lured = false;
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
        if (catHeld) { SprayX = null; SprayTargetId = 0; return; }
        if (SprayX is double && Holding(BattleSkill.Spray))
        {
            FollowSprayTarget();
            double at = SprayX!.Value;
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
        else { SprayX = null; SprayTargetId = 0; sprayClock = 0; Refill(BattleSkill.Spray, ref sprayTank, dt); }
    }

    // The Spray works this step: pressed on the field, still chosen, not cooling down.
    private bool Holding(BattleSkill skill) => Armed == skill && cooldowns[(int)skill] <= 0;
    // Used up: put away and cooling down (its tank refills with the cooldown).
    private void Spend(BattleSkill skill) { Armed = null; Recharge(skill); }
    // Not in use: spent, the tank comes back with the cooldown; otherwise at the same pace.
    private void Refill(BattleSkill skill, ref double tank, double dt)
    {
        double refill = Math.Max(0.01, SkillCooldown(skill)), left = cooldowns[(int)skill];
        tank = left > 0 ? 1 - left / refill : Math.Min(1, tank + dt / refill);
    }

    // 2026-11-04, the hot-water spray: one click on the field starts it; it locks on the slime there (or the
    // nearest one within SprayRange x 2 of the click) and keeps spraying it by itself until the tank is dry,
    // the mouse free. Its slime gone, it moves to the nearest other one (none: it keeps to that spot).
    // The skill put away (another chosen, Pause, the cat lifted) stops it.
    public int SprayTargetId { get; private set; }
    public int SprayMelts { get; private set; }
    public int SprayKills { get; private set; }

    public bool StartSpray(double x, double height = 20)
    {
        if (Armed != BattleSkill.Spray || !Ready(BattleSkill.Spray) || !double.IsFinite(x) || !double.IsFinite(height)) return false;
        if (SprayX != null) return true; // Already spraying: the click changes nothing.
        SprayX = Math.Clamp(x, Left, Right);
        SprayHeight = Math.Max(0, height);
        SprayTargetId = SprayCandidate(SprayX.Value, SprayHeight, Settings.SprayRange * 2)?.Id ?? 0;
        FollowSprayTarget();
        events.Add(new(BattleEventKind.SprayOn, SprayX.Value));
        SkillUsed();
        return true;
    }
    public bool Spraying => SprayX != null;

    // The slime nearest (x, height) a spray can lock on (within `within`; none: null).
    private Slime? SprayCandidate(double x, double height, double within)
    {
        Slime? best = null;
        double bestGap = within * within;
        foreach (var s in slimes)
        {
            if (!s.Targetable || s.Shielded || InSky(s) || s.Burrowed) continue;
            var (left, bottom, right, top) = Body(s);
            double dx = Math.Max(0, Math.Max(left - x, x - right)), dy = Math.Max(0, Math.Max(bottom - height, height - top));
            if (dx * dx + dy * dy <= bestGap) { best = s; bestGap = dx * dx + dy * dy; }
        }
        return best;
    }

    // The spray stays on its slime (the middle of its body); gone, on the nearest other one.
    private void FollowSprayTarget()
    {
        if (SprayX is not double x) return;
        if (SprayTargetId != 0 && Find(SprayTargetId) is not { Targetable: true })
            SprayTargetId = SprayCandidate(x, SprayHeight, double.PositiveInfinity)?.Id ?? 0;
        if (SprayTargetId == 0 || Find(SprayTargetId) is not Slime s) return;
        var (_, bottom, _, top) = Body(s);
        SprayX = Math.Clamp(s.X, Left, Right);
        SprayHeight = (bottom + top) / 2;
    }

    // An enemy defeated within the spray's reach while it sprays: more water (2026-11-04).
    private void SprayKill(Slime s)
    {
        if (SprayX is not double x || Settings.SprayKillSeconds <= 0) return;
        var (left, bottom, right, top) = Body(s);
        double dx = Math.Max(0, Math.Max(left - x, x - right)), dy = Math.Max(0, Math.Max(bottom - SprayHeight, SprayHeight - top));
        double range = Settings.SprayRange;
        if (s.Id != SprayTargetId && dx * dx + dy * dy > range * range) return;
        sprayTank = Math.Min(Settings.SprayTankMax, sprayTank + Settings.SprayKillSeconds / SprayTankSeconds);
        SprayKills++;
        events.Add(new(BattleEventKind.SprayRefilled, s.X, s.Id));
    }

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
            // Hot water: an ice shell sprayed long enough melts away (2026-11-04).
            if (s.Iced)
            {
                s.MeltClock += dt;
                if (s.MeltClock >= Settings.SprayMeltSeconds) { s.MeltClock = 0; SprayMelts++; BreakIce(s); }
            }
            if (fresh || tick) ApplyStatus(s, SlimeStatus.Chill, Settings.SpraySoakSeconds, Settings.SprayChill);
            if (tick) ToolHit(s, Settings.SprayDps * run);
            if (Over) return;
        }
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
        ColumnX = null; ColumnRemaining = 0; Cut = null;
        SprayX = null; sprayClock = 0;
        foreach (var s in slimes) { s.Lured = false; s.Rooted = false; s.WetRemaining = 0; }
        ClearGrabber();
    }

    private void MapTools(double left, double oldWidth, double newWidth)
    {
        if (LaserX is double dot) LaserX = left + (dot - Left) / oldWidth * newWidth;
        if (ColumnX is double column) ColumnX = left + (column - Left) / oldWidth * newWidth;
        if (SprayX is double spray) SprayX = left + (spray - Left) / oldWidth * newWidth;
        MapGrab(left, oldWidth, newWidth);
    }
}
