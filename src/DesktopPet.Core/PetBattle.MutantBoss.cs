using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

public enum MutantMode { Combat, Bumps, Core, Ping, Absorb, Transition, Roar, AllIn, Dissolve }
public enum MutantShape { Goo, Eye, Hand, Shell, Wave, Fan, Breath, Column, Waterfall }

// Simulation coordinates: X is absolute, height is above the battlefield floor.
public sealed class MutantZone
{
    public double X, H, W, Height, Warn, Life, Age, NextHit;
    public MutantShape Shape;
    public bool Falling, Debuff, Permanent, Horizontal;
    public bool Live => Age >= Warn && Age < Warn + Life;
}
public sealed class MutantBump
{
    public double X, H, SolvedAt = -1;
    public int Side;
    public bool Solved => SolvedAt >= 0;
}
public sealed class MutantBag
{
    public double X, H, Clock, Wet;
    public int Lane;
    public bool Warning => Clock >= 6 && Clock < 7.2;
    public bool Spraying => Clock >= 7.2 && Clock < 9.2;
}
public sealed class MutantBoss
{
    public int Phase = 1, Round, NextBump, Throws, Misses, CoreHits, Move = -1, Turn, AllIn = -1, Beat;
    public double Clock = 2, Age, Deadline, ActionAt, NextAttack = 4, AimX, AimH, FromX, FromH, Flight, ProjectileAge;
    public double FadeAt = -1, TransitionFrom;
    public bool Reflected, Projectile, Final, TerrainCleared, Failed, CoreReady;
    public MutantMode Mode;
    public readonly List<MutantZone> Zones = new();
    public readonly List<MutantBump> Bumps = new();
    public readonly List<MutantBump> FailedCores = new();
    public readonly List<MutantBag> Bags = new();
    internal readonly List<int> Moves = new();
    internal Random Random = new(1);
    public bool Protected => Mode != MutantMode.Combat && Mode != MutantMode.Core;
    public bool Mech => Mode is MutantMode.Bumps or MutantMode.Core or MutantMode.Ping or MutantMode.Absorb or MutantMode.Roar or MutantMode.AllIn or MutantMode.Dissolve;
    public double CoreX, CoreH;
    public double ReturnLeft;
}

public sealed partial class BattleSim
{
    private (double X, double Height)? mutantPreviousCat;
    private double mutantHitUntil, mutantRescueUntil, mutantFallStun;
    public bool MutantForcedFall { get; private set; }
    public double MutantNoiseLeft { get; private set; }
    public double MutantRubProgress { get; private set; }
    private double mutantNoiseAge, mutantRubX = double.NaN, mutantRubLastAt, mutantRubTravel, mutantNoiseImmune;
    private int mutantRubDirection, mutantRubTurns;
    private double mutantFloorSlow = 1;
    private (double X, double H)? mutantPointer;
    public bool MutantNoise => MutantNoiseLeft > 0 && !Over;
    public bool MutantHiccup => MutantNoise && !CatOffFloor && mutantNoiseAge % 1.3 < 0.25;
    public bool MutantRescuing => MutantForcedFall || Time < mutantRescueUntil || Time < mutantHitUntil;
    public bool MutantBumpMech => slimes.Any(s => s.Targetable && s.Mutant?.Mode == MutantMode.Bumps);
    public static bool BossDebuffImmune(Slime s) => s.Mutant is { } m && m.Mode != MutantMode.Combat || s.Type.Zip != null && (s.Shielded || s.MechBonusClock > 0 || s.Deflating > 0);
    public static bool BossPushImmune(Slime s) => BossDebuffImmune(s) || s.Type.Zip != null && (s.ZipStage >= 1 || (s.PhasesDone & 1) != 0);

    // A separate pointer input covers the cat above the strip too. The window supplies screen-mapped DIPs.
    public void SetMutantPointer(double? x, double h = 0) => mutantPointer = x is double p && double.IsFinite(p) && double.IsFinite(h) ? (p, h) : null;
    private void StepMutantCat(double dt)
    {
        mutantFloorSlow = 1;
        mutantNoiseImmune = Math.Max(0, mutantNoiseImmune - dt);
        if (Raging) ClearMutantNoise();
        if (MutantNoise)
        {
            MutantNoiseLeft = Math.Max(0, MutantNoiseLeft - dt);
            mutantNoiseAge += dt;
            var c = CatCentre();
            if (mutantPointer is { } p && Math.Abs(p.X - c.X) <= CatHalfWidth + 14 && p.H >= c.Height - CatFeetBelow && p.H <= c.Height + CatHeadAbove)
            {
                if (!double.IsFinite(mutantRubX)) mutantRubLastAt = Time;
                if (double.IsFinite(mutantRubX))
                {
                    double dx = p.X - mutantRubX;
                    if (Math.Abs(dx) >= 0.1 && Math.Abs(dx) <= 100)
                    {
                        int direction = Math.Sign(dx);
                        mutantRubTravel += Math.Abs(dx);
                        if (direction != mutantRubDirection && mutantRubTravel >= 8)
                        { mutantRubTurns++; mutantRubDirection = direction; mutantRubTravel = 0; }
                        MutantRubProgress += Math.Min(dt, 0.05);
                        mutantRubLastAt = Time;
                    }
                }
                mutantRubX = p.X;
            }
            else mutantRubX = double.NaN;
            if (Time - mutantRubLastAt > 0.3) mutantRubX = double.NaN;
            if (MutantRubProgress >= 2 && mutantRubTurns >= 4 || MutantNoiseLeft <= 0) ClearMutantNoise();
        }
        if (MutantForcedFall)
        {
            wasLifted = false; liftedFor = 0;
            if (!CatOffFloor)
            {
                MutantForcedFall = false;
                CatStunRemaining = Math.Max(CatStunRemaining, mutantFallStun);
                mutantRescueUntil = Time + mutantFallStun + 1.2;
                CancelSwing();
                events.Add(new(BattleEventKind.CatStunned, lastCatX));
            }
        }
    }
    private void ClearMutantNoise()
    {
        if (MutantNoiseLeft > 0) AddEffect(BattleEffectKind.Poof, lastCatX, CatCentre().Height);
        MutantNoiseLeft = MutantRubProgress = 0; mutantRubTurns = 0; mutantRubX = double.NaN;
        mutantNoiseImmune = 2;
    }
    private void ApplyMutantNoise()
    {
        if (Raging || ShieldRemaining > 0 || mutantNoiseImmune > 0 || MutantNoise) return;
        MutantNoiseLeft = 4; mutantNoiseAge = 0; MutantRubProgress = 0; mutantRubTurns = 0; mutantRubDirection = 0;
        mutantRubX = double.NaN; mutantRubTravel = 0;
    }
    private void MutantDrop(double stun)
    {
        if (Raging) return;
        CancelSwing(); CancelHeldSwing();
        mutantFallStun = stun;
        if (CatOffFloor) MutantForcedFall = true;
        else { CatStunRemaining = Math.Max(CatStunRemaining, stun); mutantRescueUntil = Time + stun + 1.2; }
    }
    private bool MutantHurt(double fraction, bool drop = false, bool noise = false)
    {
        if (Over || MutantRescuing) return false;
        mutantHitUntil = Time + 2.5;
        if (!LandHit(CatMaxHp * fraction, lastCatX, out _)) return false;
        if (noise) ApplyMutantNoise();
        if (drop) MutantDrop(1);
        return true;
    }
    private bool MutantTouches(double x, double h, double width, double height)
    {
        var c = CatCentre(); var a = mutantPreviousCat ?? c;
        return Crosses(a.X, a.Height, c.X, c.Height, x - width / 2 - CatHalfWidth, h - CatHeadAbove,
            x + width / 2 + CatHalfWidth, h + height + CatFeetBelow);
    }
    private static double MutationFloor(Slime s) => s.Mutant is not { } m ? 0 : m.Final ? 1 : m.Phase == 1 ? s.MaxHp * 0.66 : m.Phase == 2 ? s.MaxHp * 0.33 : 1;
    private HitResult? MutantDamage(Slime s, DamageSource source)
    {
        if (s.Mutant is not { } m) return null;
        if (m.Mode == MutantMode.Core)
        {
            if (source is not (DamageSource.Weapon or DamageSource.Combo)) return HitResult.Immune;
            s.HitFlash = 0.2;
            AddEffect(BattleEffectKind.Combo, s.X, s.Height * 0.65);
            if (m.Final)
            {
                m.CoreHits++;
                if (m.CoreHits >= 6) { s.Hp = 0; Defeat(s); return HitResult.Landed; }
                // The repulse only moves a grounded cat; a held cat stays under the user's hand.
                if (!CatOffFloor) Shove(lastCatX < s.X ? -1 : 1, 100, 0.3);
                m.Mode = MutantMode.AllIn; m.Clock = 1.2; m.Beat = 0;
                int chosen = m.Random.Next(3); if (chosen == m.AllIn) chosen = (chosen + 1 + m.Random.Next(2)) % 3;
                m.AllIn = chosen;
                s.X = Math.Clamp(Left + (Right - Left) * (0.15 + m.Random.NextDouble() * 0.7), Left + s.Width / 2, Right - s.Width / 2);
                if (chosen == 2) s.X = m.Random.Next(2) == 0 ? Left + s.Width / 2 + 4 : Right - s.Width / 2 - 4;
            }
            else if (m.Phase == 1)
            {
                if (++m.Round >= 3) MutantTransition(s, 2);
                else { m.Mode = MutantMode.Bumps; MakeMutantBumps(s); }
            }
            else MutantTransition(s, 3);
            return HitResult.Landed;
        }
        if (m.Protected) return HitResult.Immune;
        return null;
    }
    private void MutantThreshold(Slime s)
    {
        if (s.Mutant is not { Mode: MutantMode.Combat } m || s.Hp > MutationFloor(s) + 1e-7) return;
        ClearStatus(s); s.KnockVelocity = 0; s.StunRemaining = 0; s.Set(SlimeState.Moving);
        m.Zones.Clear(); m.Moves.Clear(); m.Age = 0;
        if (m.Phase == 1) { m.Mode = MutantMode.Bumps; m.Deadline = Time + 60; m.Round = 0; MakeMutantBumps(s); }
        else if (m.Phase == 2) { m.Mode = MutantMode.Ping; m.Throws = m.Misses = 0; m.Clock = 1.5; m.Projectile = false; }
        else
        {
            m.Final = true; m.Mode = MutantMode.Roar; m.Clock = 5; m.CoreHits = 0;
            // Hazard collisions cease immediately; old positions remain only for the dissolve artwork.
            m.TerrainCleared = true; m.FadeAt = Time; ClearMutantNoise();
        }
        events.Add(new(BattleEventKind.BossShielded, s.X));
    }
    private void MutantTransition(Slime s, int phase)
    {
        var m = s.Mutant!; m.TransitionFrom = m.Phase; m.Phase = phase;
        m.Mode = MutantMode.Transition; m.Clock = 3; m.Age = 0; m.Bumps.Clear(); m.Zones.Clear(); m.Projectile = false;
        ClearStatus(s); s.KnockVelocity = 0;
        if (phase == 3 && m.Bags.Count == 0)
            for (int i = 0; i < 4; i++) m.Bags.Add(new MutantBag { Lane = i, Clock = i * 2.4 });
    }
    private void MakeMutantBumps(Slime s)
    {
        var m = s.Mutant!; m.Bumps.Clear(); m.NextBump = 0; m.NextAttack = Time + 4;
        double span = Right - Left;
        // Stratified floor positions, rejection of boss overlap and sufficient separation.
        var candidates = new List<double>();
        for (double x = Left + 45; x <= Right - 45; x += 90)
            if (Math.Abs(x - s.X) > s.Width / 2 + 50) candidates.Add(x);
        for (int i = candidates.Count - 1; i > 0; i--) { int j = m.Random.Next(i + 1); (candidates[i], candidates[j]) = (candidates[j], candidates[i]); }
        foreach (double x in candidates.Take(3)) m.Bumps.Add(new MutantBump { X = x });
        // A narrow resized layout is rebuilt rather than stacking targets over the boss.
        if (m.Bumps.Count < 3)
        {
            s.X = Left + s.Width / 2 + 8;
            m.Bumps.Clear();
            for (int i = 0; i < 3; i++) m.Bumps.Add(new MutantBump { X = Left + span * (0.45 + i * 0.2) });
        }
        for (int i = 0; i < 3; i++)
        {
            int side = m.Random.Next(2) == 0 ? -1 : 1;
            m.Bumps.Add(new MutantBump { X = side < 0 ? Left + 18 : Right - 18, H = Math.Max(115, SkyHeight * (0.28 + i * 0.23)), Side = side });
        }
        for (int i = m.Bumps.Count - 1; i > 0; i--) { int j = m.Random.Next(i + 1); (m.Bumps[i], m.Bumps[j]) = (m.Bumps[j], m.Bumps[i]); }
    }
    private void MutantZone(MutantBoss m, MutantShape shape, double x, double h, double w, double height, double warn = 1.1, double life = 0.35, bool drop = false)
        => m.Zones.Add(new MutantZone { Shape = shape, X = x, H = h, W = w, Height = height, Warn = warn, Life = life, Falling = drop, Debuff = true });

    private bool StepMutantBoss(Slime s, double dt)
    {
        if (!s.Type.Mutant) return false;
        s.Mutant ??= new MutantBoss { Random = new Random(unchecked(Seed * 397 ^ s.Id * 71 ^ 0x4D5554)) };
        var m = s.Mutant;
        if (!s.Targetable || s.State == SlimeState.Spawning) return false;
        m.Age += dt;
        StepMutantTerrain(s, dt);
        if (Over) return true;
        if (m.Mode == MutantMode.Combat && s.State == SlimeState.Stunned)
        {
            s.StunRemaining = Math.Max(0, s.StunRemaining - dt);
            if (s.StunRemaining <= 0) s.Set(SlimeState.Moving);
            return true;
        }
        m.Clock -= dt * (m.Mode == MutantMode.Combat ? s.ChillFactor : 1);
        if (BossDebuffImmune(s)) { ClearStatus(s); s.WetRemaining = 0; s.KnockVelocity = 0; }
        foreach (var z in m.Zones)
        {
            z.Age += dt;
            if (z.Live && Time >= z.NextHit && MutantTouches(z.X, z.H, z.W, z.Height))
            {
                z.NextHit = Time + 3;
                MutantHurt(m.Final ? (m.AllIn == 0 ? 0.05 : 0.12) : 0.06, z.Falling, z.Debuff);
                if (Over) return true;
            }
        }
        m.Zones.RemoveAll(z => z.Age > z.Warn + z.Life + 0.8);
        switch (m.Mode)
        {
            case MutantMode.Combat:
                if (m.Clock <= 0) MutantMove(s);
                break;
            case MutantMode.Bumps:
                if (Time >= m.Deadline) { m.Failed = true; m.Mode = MutantMode.Absorb; m.Clock = 2; break; }
                StepMutantBumps(s);
                if (Time >= m.NextAttack)
                {
                    var c = CatCentre(); MutantZone(m, MutantShape.Goo, c.X, Math.Max(0, c.Height - 38), 60, 76, 1.4);
                    m.NextAttack = Time + 4;
                }
                break;
            case MutantMode.Ping: StepMutantPing(s, dt); break;
            case MutantMode.Core:
                if (m.Phase == 1 && Time >= m.Deadline) { m.Failed = true; m.Mode = MutantMode.Absorb; m.Clock = 2; }
                break;
            case MutantMode.Absorb:
                if (m.Clock <= 0)
                {
                    double penalty = m.Phase == 1 ? 0.5 : m.Misses == 6 ? 0.4 : m.Misses >= 3 ? 0.2 : 0;
                    if (penalty > 0) LandHit(CatMaxHp * penalty, lastCatX, out _);
                    s.Hp = m.Phase == 1 ? s.MaxHp : Math.Min(s.MaxHp, s.Hp + s.MaxHp * m.Misses / 6.0);
                    m.FailedCores.Clear(); MutantTransition(s, m.Phase + 1);
                }
                break;
            case MutantMode.Transition:
                if (m.Clock <= 0) { m.Mode = MutantMode.Combat; m.Clock = 2; m.Moves.Clear(); }
                break;
            case MutantMode.Roar:
                if (m.Clock <= 0) { m.Bags.Clear(); m.Mode = MutantMode.Core; }
                break;
            case MutantMode.AllIn: StepMutantAllIn(s); break;
            case MutantMode.Dissolve:
                if (m.Clock <= 0) { m.Zones.Clear(); m.Mode = MutantMode.Core; }
                break;
        }
        return true;
    }
    private void StepMutantBumps(Slime s)
    {
        var m = s.Mutant!;
        if (m.NextBump >= m.Bumps.Count) { m.Mode = MutantMode.Core; return; }
        var b = m.Bumps[m.NextBump]; var c = CatCentre();
        bool hit = b.Side == 0 && (catPose is { InHand: false, Flying: true, VY: > 0 } || wasLifted && !CatHeld) && MutantTouches(b.X, 0, 62, 28);
        if (!hit) return;
        SolveMutantBump(s, b);
        mutantBumpBounce = true;
    }
    private void SolveMutantBump(Slime s, MutantBump b)
    {
        var m = s.Mutant!;
        b.SolvedAt = Time; m.NextBump++; m.NextAttack = Math.Max(m.NextAttack, Time + 1.2);
        AddEffect(BattleEffectKind.Combo, b.X, b.H + 18);
        events.Add(new(BattleEventKind.HeldHit, b.X, s.Id));
        if (m.NextBump >= 6) m.Mode = MutantMode.Core;
    }
    private int mutantWallBoss;
    private bool StartMutantWallSwing(CatPose cat)
    {
        foreach (var s in slimes)
        {
            if (!s.Targetable || s.Mutant is not { Mode: MutantMode.Bumps } m || m.NextBump >= m.Bumps.Count) continue;
            var b = m.Bumps[m.NextBump];
            if (b.Side == 0 || Math.Abs(cat.X - b.X) > HeldReach + 45 || Math.Abs(cat.Height - b.H) > 65) continue;
            mutantWallBoss = s.Id; heldTargetId = s.Id; heldSwingTime = 0; heldHit = false;
            heldCooldown = Math.Max(arms.AttackCooldown * CooldownFactor, arms.SwingDuration);
            HeldFacing = b.Side; events.Add(new(BattleEventKind.Swing, cat.X)); return true;
        }
        return false;
    }
    private void HitMutantWallBump(CatPose cat)
    {
        if (Find(mutantWallBoss) is not { Targetable: true, Mutant: { Mode: MutantMode.Bumps } m } s || m.NextBump >= m.Bumps.Count) return;
        var b = m.Bumps[m.NextBump];
        if (b.Side != 0 && Math.Abs(cat.X - b.X) <= HeldReach + 59 && Math.Abs(cat.Height - b.H) <= 65) SolveMutantBump(s, b);
    }
    private bool mutantBumpBounce;
    public bool TakeMutantBumpBounce() { bool value = mutantBumpBounce; mutantBumpBounce = false; return value; }

    private void StepMutantPing(Slime s, double dt)
    {
        var m = s.Mutant!;
        if (m.ReturnLeft > 0)
        {
            m.ReturnLeft = Math.Max(0, m.ReturnLeft - dt);
            double returning = 1 - m.ReturnLeft / 0.55;
            m.CoreX = m.FromX + (s.X - m.FromX) * returning;
            m.CoreH = m.FromH + (s.Height * 0.6 - m.FromH) * returning;
            if (m.ReturnLeft <= 0)
            {
                s.HitFlash = 0.3; s.StunRemaining = 1; s.Set(SlimeState.Stunned);
                AddEffect(BattleEffectKind.Combo, s.X, s.Height * 0.6);
            }
            return;
        }
        if (s.State == SlimeState.Stunned)
        {
            s.StunRemaining = Math.Max(0, s.StunRemaining - dt);
            if (s.StunRemaining <= 0) s.Set(SlimeState.Moving);
            return;
        }
        if (!m.Projectile)
        {
            if (m.Clock > 0) return;
            if (m.Throws >= 6)
            {
                m.Mode = m.Misses == 0 ? MutantMode.Core : MutantMode.Absorb; m.Clock = 3;
                return;
            }
            m.Throws++; m.Reflected = false; m.Projectile = true; m.ProjectileAge = -1.2;
            m.FromX = s.X; m.FromH = s.Height * 0.7;
            double away = s.X < (Left + Right) / 2 ? 1 : -1;
            m.AimX = Math.Clamp(s.X + away * (240 + m.Random.NextDouble() * Math.Max(0, (Right - Left) * 0.5 - 240)), Left + 45, Right - 45);
            m.Flight = Math.Max(1.6, Math.Abs(m.AimX - s.X) / 270); m.CoreX = m.FromX; m.CoreH = m.FromH;
        }
        m.ProjectileAge += dt;
        if (m.ProjectileAge < 0) return;
        double t = Math.Clamp(m.ProjectileAge / m.Flight, 0, 1);
        double oldX = m.CoreX, oldH = m.CoreH;
        m.CoreX = m.FromX + (m.AimX - m.FromX) * t;
        m.CoreH = m.FromH * (1 - t) + Math.Sin(Math.PI * t) * Math.Min(SkyHeight * 0.55, Math.Abs(m.AimX - m.FromX) * 0.4 + 80);
        var c = CatCentre(); var a = mutantPreviousCat ?? c;
        if (catPose is { InHand: false, Flying: true } && PaperCrossesBody(oldX - a.X, oldH - a.Height, m.CoreX - c.X, m.CoreH - c.Height, 26))
        {
            m.Reflected = true; m.Projectile = false; m.Clock = 1.8;
            m.FromX = m.CoreX; m.FromH = m.CoreH; m.ReturnLeft = 0.55;
            AddEffect(BattleEffectKind.Combo, m.CoreX, m.CoreH);
        }
        else if (t >= 1)
        {
            m.Misses++; m.FailedCores.Add(new MutantBump { X = m.AimX });
            m.Projectile = false; m.Clock = 1.4;
            AddEffect(BattleEffectKind.Splat, m.AimX, 0);
        }
    }
    private void MutantMove(Slime s)
    {
        var m = s.Mutant!;
        if (m.Moves.Count == 0)
        {
            var choices = new[] { 0, 1, 2 };
            for (int i = 2; i > 0; i--) { int j = m.Random.Next(i + 1); (choices[i], choices[j]) = (choices[j], choices[i]); }
            if (choices[0] == m.Move) (choices[0], choices[1]) = (choices[1], choices[0]);
            m.Moves.AddRange(choices);
        }
        m.Move = m.Moves[0]; m.Moves.RemoveAt(0); m.Clock = 5.2; m.ActionAt = Time;
        var c = CatCentre(); double x = c.X, h = Math.Max(0, c.Height - 40);
        s.Facing = x < s.X ? -1 : 1;
        if (m.Phase == 1)
        {
            if (m.Move == 0)
            {
                double end = Math.Clamp(s.X + s.Facing * 260, Left + 45, Right - 45);
                MutantZone(m, MutantShape.Wave, (s.X + end) / 2, 0, Math.Abs(end - s.X) + 60, 70);
                MutantZone(m, MutantShape.Shell, s.X, 0, s.Width + 70, 100, 2.3);
            }
            else if (m.Move == 1)
                for (int i = 0; i < 2; i++) MutantZone(m, MutantShape.Eye, Math.Clamp(x + (i * 2 - 1) * 60, Left, Right), h + i * 40, 75, 70, 1.2 + i * 0.8);
            else for (int i = 0; i < 3; i++) MutantZone(m, MutantShape.Goo, Math.Clamp(x + (i - 1) * 90, Left, Right), i == 1 ? h : 0, 65, i == 2 ? 140 : 75, 1.1 + i * 0.65);
        }
        else if (m.Phase == 2)
        {
            if (m.Move == 0)
            {
                MutantZone(m, MutantShape.Shell, s.X, 0, s.Width + 80, s.Height + 55, 1.4);
                s.X = Math.Clamp(s.X + s.Facing * 170, Left + s.Width / 2, Right - s.Width / 2);
                MutantZone(m, MutantShape.Hand, s.X + s.Facing * 100, h, 140, 90, 2.6);
            }
            else if (m.Move == 1)
                for (int i = 0; i < 2; i++) MutantZone(m, MutantShape.Hand, x, i == 0 ? 0 : Math.Min(SkyHeight - 120, h + 100), 200, 90, 1.3 + i * 1.1);
            else for (int i = 0; i < 3; i++) MutantZone(m, MutantShape.Goo, x + (i - 1) * 85, h + i * 55, 65, 75, 1.4 + i * 0.65);
        }
        else
        {
            if (m.Move == 0)
                for (int i = 0; i < 3; i++) MutantZone(m, MutantShape.Fan, Math.Clamp(x + (i - 1) * 170, Left, Right), i * 65, 125, 150, 1.4 + i * 0.6);
            else if (m.Move == 1)
            {
                foreach (var bag in m.Bags.Take(2)) MutantZone(m, MutantShape.Goo, bag.X, bag.H - 75, 150, 150, 1.6, 0.6);
                MutantZone(m, MutantShape.Breath, (Left + Right) / 2, 0, (Right - Left) * 0.65, 80, 3);
            }
            else
                for (int i = 0; i < 3; i++) MutantZone(m, MutantShape.Wave, Math.Clamp(s.X + s.Facing * (120 + i * 120), Left, Right), 0, 95, 140, 1.1 + i * 0.6);
        }
    }
    private void StepMutantAllIn(Slime s)
    {
        var m = s.Mutant!;
        if (m.Clock > 0) return;
        if (m.AllIn == 0)
        {
            if (m.Beat == 6) { m.Mode = MutantMode.Dissolve; m.Clock = 1.2; foreach (var z in m.Zones) z.Life = Math.Max(0, z.Age - z.Warn); return; }
            var c = CatCentre(); bool horizontal = m.Random.Next(2) == 0;
            // Leave a reserved corner untouched so accumulated columns cannot fill the screen.
            double w = horizontal ? Right - Left : Math.Min(75, (Right - Left) * 0.07);
            double height = horizontal ? Math.Min(65, SkyHeight * 0.08) : SkyHeight;
            double x = horizontal ? (Left + Right) / 2 : Math.Clamp(c.X, Left + Math.Min(170, (Right - Left) * 0.4), Right - Math.Min(60, (Right - Left) * 0.15));
            double h = horizontal ? Math.Clamp(c.Height - height / 2, 120, Math.Max(120, SkyHeight - height)) : 0;
            MutantZone(m, MutantShape.Column, x, h, w, height, 2.5, 30, true);
            m.Zones[^1].Horizontal = horizontal;
            m.Beat++; m.Clock = m.Beat == 6 ? 4.5 : 2.7;
        }
        else if (m.AllIn == 1)
        {
            if (m.Beat >= 2) { m.Mode = MutantMode.Dissolve; m.Clock = 1; return; }
            double gap = Math.Min(230, (Right - Left) * 0.3);
            double center = Left + (Right - Left) * (m.Beat == 0 ? 0.3 : 0.7);
            MutantZone(m, MutantShape.Waterfall, (Left + center - gap / 2) / 2, 0, center - gap / 2 - Left, SkyHeight, 2, 2, true);
            MutantZone(m, MutantShape.Waterfall, (center + gap / 2 + Right) / 2, 0, Right - center - gap / 2, SkyHeight, 2, 2, true);
            m.Beat++; m.Clock = 4.5;
        }
        else
        {
            if (m.Beat >= 3) { m.Mode = MutantMode.Dissolve; m.Clock = 1; return; }
            double extent = (Right - Left) * (m.Beat + 1) / 3;
            bool right = s.X < (Left + Right) / 2;
            double x = right ? Left + extent / 2 : Right - extent / 2;
            MutantZone(m, MutantShape.Breath, x, 0, extent, Math.Max(90, SkyHeight * 0.6), m.Beat == 2 ? 2 : 1.2, 1.3, true);
            m.Beat++; m.Clock = m.Beat == 2 ? 2.5 : m.Beat == 3 ? 3.4 : 2.6;
        }
    }
    public static double MutantTerrainClock(MutantBoss m, int index) => (m.Age + index * 3.1) % 13;
    private void StepMutantTerrain(Slime s, double dt)
    {
        var m = s.Mutant!;
        if (m.Phase < 2 || m.TerrainCleared || m.Mode == MutantMode.Transition) return;
        double width = (Right - Left) * (m.Phase == 2 ? 0.125 : 0.25);
        for (int i = 0; i < 4; i++)
        {
            double x = i < 2 ? Left + width * (i + 0.5) / 2 : Right - width * (i - 1.5) / 2;
            if (MutantTerrainClock(m, i) >= 5 && MutantTouches(x, 0, width / 2, 24))
            {
                if (!CatOffFloor && !Raging) mutantFloorSlow = 0.75;
                MutantHurt(0.025, false, true);
            }
        }
        foreach (var bag in m.Bags)
        {
            // Separate nonintersecting lanes; both coordinates drift smoothly, never linearly A→B.
            double laneW = (Right - Left) / 4;
            bag.X = Left + laneW * (bag.Lane + 0.5) + Math.Sin(m.Age * 0.21 + bag.Lane * 2) * Math.Max(0, laneW / 2 - 65);
            bag.H = Math.Max(160, SkyHeight * (0.45 + 0.15 * Math.Sin(m.Age * 0.17 + bag.Lane * 1.8)));
            bag.Clock = (m.Age + bag.Lane * 2.4) % 12;
            bool touch = bag.Spraying && MutantTouches(bag.X, bag.H - 85, 170, 170);
            bag.Wet = touch ? bag.Wet + dt : Math.Max(0, bag.Wet - dt * 2);
            if (touch) MutantHurt(0.025, false, true);
            if (bag.Wet >= 0.6 && !MutantForcedFall && Time >= mutantRescueUntil && !Raging && ShieldRemaining <= 0)
            { MutantDrop(1); ApplyMutantNoise(); bag.Wet = 0; mutantHitUntil = Time + 2.5; }
        }
    }
}
