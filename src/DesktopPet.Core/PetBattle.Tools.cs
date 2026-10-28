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
    public double FanSteam { get; private set; } = 1;
    public double ToolDamage { get; private set; }
    public int FanBumps { get; private set; }
    private double laserClock;

    // A spot skill (the Laser, chosen first) at x on the floor.
    public bool UseAt(BattleSkill skill, double x)
    {
        if (BattleSkills.Get(skill).Aim != SkillAim.Point || Armed != skill || !Ready(skill) || !double.IsFinite(x)) return false;
        LaserX = Math.Clamp(x, Left, Right);
        LaserRemaining = Settings.LaserSeconds;
        laserClock = 0;
        // At once, so no slime near the dot winds up in the frame it appears.
        foreach (var s in slimes) s.Lured = Lurable(s) && Math.Abs(s.X - LaserX.Value) <= Settings.LaserLureRange;
        cooldowns[(int)skill] = SkillCooldown(skill);
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
        if (fresh) events.Add(new(BattleEventKind.FanOn, FanX.Value));
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
                    if (s.Targetable && !s.Airborne && Math.Abs(s.X - dot) <= s.HitWidth / 2 + 6) ToolHit(s, Settings.LaserDps * run);
            }
            if (ended)
            {
                LaserX = null;
                foreach (var s in slimes) s.Lured = false;
                events.Add(new(BattleEventKind.LaserOff, dot));
            }
        }
        if (catHeld) { FanX = null; return; }
        int fan = (int)BattleSkill.Fan;
        if (FanX is double from && Armed == BattleSkill.Fan && cooldowns[fan] <= 0)
        {
            FanSteam = Math.Max(0, FanSteam - dt / Settings.FanSteamSeconds);
            Blow(from, dt);
            if (FanSteam <= 0)
            {
                FanX = null;
                Armed = null;
                cooldowns[fan] = SkillCooldown(BattleSkill.Fan);
                events.Add(new(BattleEventKind.FanSpent, from));
            }
        }
        else
        {
            FanX = null;
            // Spent: the air comes back with the cooldown; otherwise at the same pace.
            double refill = Math.Max(0.01, SkillCooldown(BattleSkill.Fan));
            FanSteam = cooldowns[fan] > 0 ? 1 - cooldowns[fan] / refill : Math.Min(1, FanSteam + dt / refill);
        }
        Bumps();
    }

    // A lured enemy walks to the dot (its own speed) and waits on it; no attack meanwhile.
    private void Lure(Slime slime, double dot, double dt, double minX, double maxX)
    {
        double gap = dot - slime.X;
        if (Math.Abs(gap) > 1) slime.Facing = gap < 0 ? -1 : 1;
        if (Math.Abs(gap) > 4) slime.X = Math.Clamp(slime.X + Math.Sign(gap) * Math.Min(Math.Abs(gap), Speed(slime) * dt), minX, maxX);
    }

    private static bool Lurable(Slime s) => s.Targetable && !s.IsBoss && !s.Riding && !s.Airborne && !s.Stuck && s.LeapHeight <= 0
        && s.State is not (SlimeState.Casting or SlimeState.Spawning) && !s.Shielded;

    private void Blow(double from, double dt)
    {
        double range = Settings.FanRange;
        foreach (var s in slimes)
        {
            if (!s.Targetable || s.IsBoss || s.Stuck || s.Riding || s.Airborne || s.LeapHeight > 0 || s.Shielded) continue;
            double d = s.X - from;
            if (Math.Abs(d) > range + s.Width / 2) continue;
            int away = Math.Abs(d) >= 1 ? Math.Sign(d) : s.X >= lastCatX ? 1 : -1;
            double strength = Math.Clamp(1 - Math.Max(0, Math.Abs(d) - s.Width / 2) / range, 0.25, 1);
            double push = Settings.FanSpeed * strength * s.Type.PushFactor * (s.Elite is { PushImmune: true } ? FanImmuneShare : 1);
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
            if (s.Blown <= 0 || !s.Targetable || Math.Abs(s.KnockVelocity) < BumpSpeed) continue;
            int way = Math.Sign(s.KnockVelocity);
            var (minX, maxX) = Bounds(s);
            Slime? into = null;
            foreach (var o in slimes)
            {
                if (o == s || !o.Targetable || o.Airborne || o.Riding) continue;
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
        foreach (var s in slimes) { s.Lured = false; s.Blown = 0; }
    }

    private void MapTools(double left, double oldWidth, double newWidth)
    {
        if (LaserX is double dot) LaserX = left + (dot - Left) / oldWidth * newWidth;
        if (FanX is double fan) FanX = left + (fan - Left) / oldWidth * newWidth;
    }
}
