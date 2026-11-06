using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

public enum MotherMode { Combat, HangingEggs, Cradle, Lullaby, Stroke, LastEgg, Sleeping }
public enum MotherArt { Hand, Egg, Hug, Child, Ribbon, Flood, Shell, Heartbeat, Cry }
public sealed class MotherZone
{
    public MotherArt Art;
    public double X, H, Width, Height, Warn, Life, Age, Damage = 1, FromX, FromH, ToX, ToH;
    public bool Moving, Hit, Slow, Thread, Jump;
    public int ActorId;
    public bool Live => Age >= Warn && Age < Warn + Life;
}
public sealed class MotherToken
{
    public double X, H, AnchorX, AnchorH;
    public int Hits;
    public bool Falling, Bad, Carried;
    private bool done;
    public double DoneAge;
    public bool Done { get => done; set { if (value && !done) DoneAge = 0; done = value; } }
}
public sealed class MotherBoss
{
    public int Phase = 1, Move = -1, LastMove = -1, Thresholds, Progress, Beat, StrokeDirection = 1;
    private MotherMode mode;
    public MotherMode Mode { get => mode; set { if (mode != value) { PreviousMode = mode; mode = value; ModeAge = 0; } } }
    public MotherMode PreviousMode { get; private set; }
    public double ModeAge, Velocity, ActionAge = 100, ActionEnd, ActionWarn = 1.1;
    public int PoseMove = -1;
    public double Age, Clock = 3, Deadline, Soothe, NextWrap = 10, NextBeat, HitAt, SwingAt, RingX, StrokeTravel, PointerX = double.NaN;
    public double FromX, ToX, TravelAge, TravelTime;
    public bool Slower;
    public bool BellyOpen => Mode == MotherMode.LastEgg && Zones.Exists(z => z.Art == MotherArt.Heartbeat && z.Age >= z.Warn + z.Life && z.Age < z.Warn + z.Life + 1.8);
    public readonly List<MotherZone> Zones = new();
    public readonly List<MotherToken> Tokens = new();
    internal readonly List<int> Bag = new();
    internal Random Random = new(1);
    public bool Protected => Mode != MotherMode.Combat;
    public bool SoothePhase => Phase == 3;
}

public sealed partial class BattleSim
{
    private (double X, double Height)? motherPreviousCat;
    private (double X, double H)? motherPointer;
    private double motherWrapLeft;
    private int motherWrapClicks, motherWrapOwner;
    private double motherSwingStarted = -10;
    private bool MotherSwinging => Time >= motherSwingStarted && Time < motherSwingStarted + arms.SwingDuration;
    public double MotherWrapLeft => Over ? 0 : motherWrapLeft;
    public int MotherWrapClicks => motherWrapClicks;
    private static bool IsMother(Slime s) => s.Type.Mother;
    public void SetMotherPointer(double? x, double h = 0) => motherPointer = x is double p && double.IsFinite(p) && double.IsFinite(h) ? (p, h) : null;
    public bool ClickMotherWrap(double? pointerX = null, double pointerH = 0)
    {
        if (Paused || ChoosingUpgrade || Over || motherWrapLeft <= 0) return false;
        var c = CatCentre();
        if (pointerX is double x && (Math.Abs(x - c.X) > 33 || Math.Abs(pointerH - c.Height) > 20)) return false;
        if (++motherWrapClicks >= 3) ClearMotherWrap();
        return true;
    }
    private void ClearMotherWrap() { motherWrapLeft = 0; motherWrapClicks = motherWrapOwner = 0; }
    private void StepMotherCat(double dt)
    {
        motherWrapLeft = Math.Max(0, motherWrapLeft - dt);
        if (Raging || motherWrapLeft <= 0 || Find(motherWrapOwner) is not { Targetable: true, Mother.Mode: MotherMode.Combat }) ClearMotherWrap();
    }
    private CatOrder? MotherPull(double dt, double catX)
    {
        if (motherWrapLeft <= 0 || CatOffFloor || Raging || CatStunned || Find(motherWrapOwner) is not Slime s) return null;
        return new(CatCommand.Shove, Math.Clamp(catX + Math.Sign(s.X - catX) * Math.Min(Math.Abs(s.X - catX), 32 * dt), Left, Right), s.X < catX ? -1 : 1);
    }
    private CatOrder? MotherStrokeOrder(double catX)
    {
        foreach (var s in slimes)
            if (s.Targetable && s.Mother is { Mode: MotherMode.Stroke } m)
                return Math.Abs(catX - m.RingX) <= 25 ? new(CatCommand.Stand, catX) : new(CatCommand.Move, m.RingX, catX < m.RingX ? 1 : -1);
        return null;
    }
    private bool MotherTouches(double x, double h, double w, double height)
    {
        var c = CatCentre(); var a = motherPreviousCat ?? c;
        return Crosses(a.X, a.Height, c.X, c.Height, x - w / 2 - CatHalfWidth, h - CatHeadAbove, x + w / 2 + CatHalfWidth, h + height + CatFeetBelow);
    }
    private void MotherCancel(Slime s)
    {
        var m = s.Mother!;
        motherSwingStarted = -10;
        m.ActionEnd = Math.Min(m.ActionEnd, m.ActionAge);
        foreach (var zone in m.Zones)
            if (zone.ActorId != 0 && Find(zone.ActorId) is Slime child) { child.LeapHeight = 0; child.Set(SlimeState.Moving); }
        m.Zones.Clear(); m.TravelTime = 0; m.Move = -1; m.Clock = 2;
        s.Special = BossMove.None; s.BroodSpots = null; s.NurseCord = 0; s.LeapHeight = 0;
        ClearMotherWrap();
    }
    private void MotherEnter(Slime s, MotherMode mode, double seconds)
    {
        MotherCancel(s);
        var m = s.Mother!; m.Mode = mode; m.Deadline = Time + seconds; m.Clock = seconds; m.Progress = m.Beat = 0;
        m.NextBeat = Time + 2; m.PointerX = double.NaN; m.StrokeTravel = 0; m.Tokens.Clear();
        s.Shielded = mode != MotherMode.Combat;
        ClearStatus(s); s.WetRemaining = 0; s.StunRemaining = s.KnockVelocity = 0; s.Set(SlimeState.Moving);
    }
    private HitResult? MotherDamage(Slime s, double amount, DamageSource source)
    {
        if (!IsMother(s)) return null;
        EnsureMother(s); var m = s.Mother!;
        if (m.Protected) return HitResult.Immune;
        s.HitFlash = 0.18;
        if (m.Phase == 3)
        {
            // Only a direct weapon/skill hit soothes. Passive cards, apps, DOT, reflect and execute cannot win.
            if (source is not (DamageSource.Weapon or DamageSource.Combo or DamageSource.Bomb or DamageSource.Tool)) return HitResult.Immune;
            m.Soothe = Math.Min(100, m.Soothe + Math.Min(amount, s.MaxHp * 0.33) / (s.MaxHp * 0.33) * 100);
            AddEffect(BattleEffectKind.Heart, s.X, s.Height * 0.65);
            MotherSootheThreshold(s);
            return HitResult.Landed;
        }
        double floor = s.MaxHp * (m.Phase == 1 ? 0.66 : 0.33);
        double actual = Math.Min(amount, Math.Max(0, s.Hp - floor));
        s.Hp = Math.Max(floor, s.Hp - amount);
        AddEffect(BattleEffectKind.Number, s.X, s.Height + 10, Math.Round(actual));
        if (s.Hp <= floor + 0.00001)
        {
            if (m.Phase == 1)
            {
                MotherEnter(s, MotherMode.HangingEggs, 40);
                for (int i = 0; i < 5; i++)
                {
                    double x = Left + (Right - Left) * (i + 1) / 6;
                    double h = Math.Min(SkyHeight - 80, 100 + i % 2 * Math.Min(180, SkyHeight * 0.25));
                    m.Tokens.Add(new() { X = x, H = h, AnchorX = x, AnchorH = h + 90 });
                }
            }
            else
            {
                MotherEnter(s, MotherMode.Cradle, 45);
                m.RingX = (Left + Right) / 2;
                for (int i = 0; i < 4; i++) m.Tokens.Add(new() { X = Left + (Right - Left) * (i + 1) / 5, H = 20 });
            }
        }
        return HitResult.Landed;
    }
    private void EnsureMother(Slime s)
    {
        s.Mother ??= new MotherBoss { Random = new Random(unchecked(Seed * 397 ^ s.Id * 71 ^ 0x4D4F54)) };
    }
    private void MotherSootheThreshold(Slime s)
    {
        var m = s.Mother!;
        if (m.Soothe >= 33 && (m.Thresholds & 1) == 0)
        {
            m.Thresholds |= 1; MotherEnter(s, MotherMode.Lullaby, 18);
            for (int i = 0; i < 8; i++) m.Tokens.Add(new() { X = Left + (Right - Left) * (i + 1) / 9,
                H = Math.Min(SkyHeight - 70, 120 + i % 3 * Math.Min(120, SkyHeight * 0.18)), Bad = i is 2 or 5 or 7 });
        }
        else if (m.Soothe >= 66 && (m.Thresholds & 2) == 0)
        {
            m.Thresholds |= 2; MotherEnter(s, MotherMode.Stroke, 15); m.RingX = Left + (Right - Left) * 0.25;
        }
        else if (m.Soothe >= 100) { MotherEnter(s, MotherMode.LastEgg, 30); m.HitAt = 0; }
    }
    private void MotherEventEnd(Slime s, bool success)
    {
        var m = s.Mother!; var mode = m.Mode;
        if (mode == MotherMode.HangingEggs)
        {
            foreach (var token in m.Tokens.Where(t => !t.Done))
            {
                string fused = Settings.Enemy(s.Type.Brood!.Enemy).Link?.Into ?? s.Type.Brood.Enemy;
                SpawnHelpers(s, new List<(string Kind, double X)> { (fused, token.X) }, 0);
                s.Hp = Math.Min(s.MaxHp, s.Hp + s.MaxHp * 0.05);
            }
            m.Phase = 2;
        }
        else if (mode == MotherMode.Cradle)
        {
            if (!success) LandHit(CatMaxHp * 0.30, lastCatX, out _);
            m.Phase = 3; m.Soothe = 0;
        }
        else if (mode == MotherMode.LastEgg && success)
        {
            MotherEnter(s, MotherMode.Sleeping, 2.5);
            AddEffect(BattleEffectKind.Heart, s.X, s.Height); return;
        }
        else if (success) { m.Slower = true; m.Soothe = Math.Min(100, m.Soothe + 10); }
        else m.Soothe = Math.Max(0, m.Soothe - 15);
        MotherEnter(s, MotherMode.Combat, 3);
        if (!success && mode is MotherMode.Lullaby or MotherMode.Stroke or MotherMode.LastEgg)
            MotherAdd(m, MotherArt.Cry, (Left + Right) / 2, 0, Right - Left, 55, 1.2, 0.5, 0.25);
        s.BroodClock = 4; m.NextWrap = Time + 8;
        if (m.Phase == 3) MotherSootheThreshold(s);
    }
    private static MotherZone MotherAdd(MotherBoss m, MotherArt art, double x, double h, double w, double height, double warn, double life = 0.3, double damage = 1)
    {
        var z = new MotherZone { Art = art, X = x, H = h, Width = w, Height = height, Warn = warn, Life = life, Damage = damage };
        m.Zones.Add(z); return z;
    }
    private static (double X, double H) MotherZonePosition(MotherZone z, double age)
    {
        double k = Math.Clamp((age - z.Warn) / Math.Max(.001, z.Life), 0, 1);
        return (z.FromX + (z.ToX - z.FromX) * k,
            z.FromH + (z.ToH - z.FromH) * k + (z.Jump || z.Art is MotherArt.Egg or MotherArt.Child ? 160 * 4 * k * (1 - k) : 0));
    }
    private bool MotherMovingContact(MotherZone z, double oldAge)
    {
        var c = CatCentre(); var a = motherPreviousCat ?? c;
        int steps = 8;
        for (int i = 0; i < steps; i++)
        {
            double t0 = i / (double)steps, t1 = (i + 1) / (double)steps;
            double age0 = oldAge + (z.Age - oldAge) * t0, age1 = oldAge + (z.Age - oldAge) * t1;
            if (age1 < z.Warn || age0 >= z.Warn + z.Life) continue;
            var p = MotherZonePosition(z, age0); var q = MotherZonePosition(z, age1);
            if (Crosses(a.X + (c.X - a.X) * t0 - p.X, a.Height + (c.Height - a.Height) * t0 - p.H,
                a.X + (c.X - a.X) * t1 - q.X, a.Height + (c.Height - a.Height) * t1 - q.H,
                -z.Width / 2 - CatHalfWidth, -CatHeadAbove, z.Width / 2 + CatHalfWidth, z.Height + CatFeetBelow)) return true;
        }
        return false;
    }
    private bool StepMotherBoss(Slime s, double dt)
    {
        if (!IsMother(s)) return false;
        EnsureMother(s); var m = s.Mother!;
        if (!s.Targetable || s.State == SlimeState.Spawning) return false;
        m.Age += dt;
        m.ModeAge += dt;
        m.ActionAge += dt;
        foreach (var token in m.Tokens) if (token.Done) token.DoneAge += dt;
        double visualFrom = s.X;
        if (m.Protected) { ClearStatus(s); s.WetRemaining = s.KnockVelocity = 0; }
        if (!m.Protected && (s.State == SlimeState.Stunned || s.Handled))
        {
            MotherCancel(s);
            if (!s.Handled) { s.StunRemaining -= dt; if (s.StunRemaining <= 0) s.Set(SlimeState.Moving); }
            return true;
        }
        foreach (var z in m.Zones)
        {
            double oldX = z.X, oldH = z.H, oldAge = z.Age;
            z.Age += dt;
            bool active = oldAge < z.Warn + z.Life && z.Age >= z.Warn;
            if (z.Moving && active)
            {
                (z.X, z.H) = MotherZonePosition(z, z.Age);
                if (oldAge < z.Warn) { oldX = z.FromX; oldH = z.FromH; }
            }
            if (z.Jump)
            {
                if (z.Live) { s.X = z.X; s.LeapHeight = z.H; }
                else if (z.Age >= z.Warn + z.Life) s.LeapHeight = 0;
            }
            if (z.ActorId != 0 && Find(z.ActorId) is Slime escort)
            {
                if (!escort.Targetable || escort.Handled || escort.State == SlimeState.Stunned) { z.Hit = true; z.ActorId = 0; }
                else if (active) { escort.X = z.X; escort.LeapHeight = z.H; }
                else if (z.Age >= z.Warn + z.Life)
                { escort.LeapHeight = 0; escort.StunRemaining = 1; escort.Set(SlimeState.Stunned); z.ActorId = 0; }
            }
            var cat = CatCentre(); var previous = motherPreviousCat ?? cat;
            bool contact = Crosses(previous.X - oldX, previous.Height - oldH, cat.X - z.X, cat.Height - z.H,
                -z.Width / 2 - CatHalfWidth, -CatHeadAbove, z.Width / 2 + CatHalfWidth, z.Height + CatFeetBelow);
            if (z.Moving && active) contact = MotherMovingContact(z, oldAge);
            if (z.Thread) contact = Crosses(z.FromX, z.FromH, z.ToX, z.ToH,
                cat.X - CatHalfWidth - 9, cat.Height - CatFeetBelow - 9, cat.X + CatHalfWidth + 9, cat.Height + CatHeadAbove + 9);
            if (active && !z.Hit && Time >= m.HitAt && contact)
            {
                z.Hit = true; m.HitAt = Time + 0.7;
                if (z.Damage > 0) LandHit(s.Damage * z.Damage, lastCatX, out _);
            }
            if (z.Slow && z.Age >= z.Warn + z.Life) { z.Slow = false;
                AddPuddle(new BattleGoo { OwnerId = s.Id, X = z.X, PuddleSeconds = 4, PuddleRadius = 45, PuddleSlow = 0.75, DashDistance = 1, DashSpeed = 1 }); }
            if (Over) return true;
        }
        m.Zones.RemoveAll(z => z.Age > z.Warn + z.Life + (z.Art == MotherArt.Heartbeat ? 2.0 : 0.4));
        if (m.Mode == MotherMode.Sleeping)
        {
            if (Time >= m.Deadline) { s.Hp = 0; s.Shielded = false; m.Tokens.Clear(); m.Zones.Clear(); Defeat(s); }
            return true;
        }
        if (m.Mode != MotherMode.Combat) { StepMotherMech(s, dt); return true; }
        MotherSootheThreshold(s);
        if (m.Mode != MotherMode.Combat) return true;
        if (m.Phase < 3)
        {
            StepBrood(s, dt);
            s.CordCooldown = Math.Max(0, s.CordCooldown - dt);
            if (s.NurseCord != 0) { s.CordLeft -= dt; if (s.CordLeft <= 0 || Find(s.NurseCord) is not { Targetable: true, Nursing: true }) s.NurseCord = 0; }
            if (s.NurseCord == 0 && s.CordCooldown <= 0)
                foreach (var child in slimes)
                    if (child.SummonedBy == s.Id && child.Targetable && child.Nursing && child.Hp < child.MaxHp * 0.5)
                    { s.NurseCord = child.Id; s.CordLeft = 6; s.CordCooldown = 14; break; }
        }
        if (m.Phase >= 2 && Time >= m.NextWrap)
        {
            m.NextWrap = Time + 12;
            if (!CatOffFloor && !Raging && ShieldRemaining <= 0)
            { motherWrapLeft = 4; motherWrapOwner = s.Id; motherWrapClicks = 0; }
        }
        if (m.TravelTime > 0)
        {
            m.TravelAge += dt;
            double k = Math.Clamp(m.TravelAge / m.TravelTime, 0, 1);
            s.X = m.FromX + (m.ToX - m.FromX) * k;
            if (k >= 1) m.TravelTime = 0;
        }
        m.Clock -= dt * s.ChillFactor;
        if (m.Clock <= 0) MotherMove(s);
        else if (m.Zones.Count == 0 && m.TravelTime <= 0)
        {
            double dx = lastCatX - s.X;
            if (Math.Abs(dx) > s.Range * 0.8) s.X += Math.Sign(dx) * s.Type.Speed * dt * s.ChillFactor;
        }
        s.X = Math.Clamp(s.X, Left + s.Width / 2, Math.Max(Left + s.Width / 2, Right - s.Width / 2));
        m.Velocity += ((s.X - visualFrom) / Math.Max(.001, dt) - m.Velocity) * (1 - Math.Exp(-12 * dt));
        s.Facing = lastCatX < s.X ? -1 : 1;
        return true;
    }
    private void MotherMove(Slime s)
    {
        var m = s.Mother!;
        if (m.Bag.Count == 0)
        {
            m.Bag.AddRange(new[] { 0, 1, 2 });
            for (int i = 2; i > 0; i--) { int j = m.Random.Next(i + 1); (m.Bag[i], m.Bag[j]) = (m.Bag[j], m.Bag[i]); }
            if (m.Bag[^1] == m.LastMove) (m.Bag[0], m.Bag[^1]) = (m.Bag[^1], m.Bag[0]);
        }
        int move = m.Bag[^1]; m.Bag.RemoveAt(m.Bag.Count - 1); m.LastMove = move;
        m.Move = (m.Phase - 1) * 3 + move;
        double slow = m.Slower ? 1.25 : 1;
        double warn = 1.1 * slow, span = Right - Left;
        var c = CatCentre();
        switch (m.Move)
        {
            case 0:
                MotherAdd(m, MotherArt.Hand, c.X - 55, Math.Max(0, c.Height - 45), 80, 90, warn);
                MotherAdd(m, MotherArt.Hand, c.X + 55, Math.Max(0, c.Height - 45), 80, 90, warn + 0.5 * slow); break;
            case 1:
                for (int i = -1; i <= 1; i++)
                {
                    double x = Math.Clamp(c.X + i * 110, Left + 30, Right - 30);
                    var z = MotherAdd(m, MotherArt.Egg, x, 0, 34, 42, warn, 1.2 * slow);
                    z.Moving = z.Slow = true; z.FromX = s.X; z.FromH = s.Height * 0.6; z.ToX = x; z.ToH = 0;
                }
                break;
            case 2:
                double to = Math.Clamp(c.X, Left + s.Width / 2, Math.Max(Left + s.Width / 2, Right - s.Width / 2));
                var hug = MotherAdd(m, MotherArt.Hug, s.X, 0, s.Width + 25, s.Height, warn, 0.65 * slow);
                hug.Moving = true; hug.FromX = s.X; hug.ToX = to;
                m.FromX = s.X; m.ToX = to; m.TravelAge = -warn; m.TravelTime = hug.Life; break;
            case 3:
                var escorts = slimes.Where(child => child.SummonedBy == s.Id && child.Targetable && !child.BroodNest && !child.Handled && child.State != SlimeState.Stunned).Take(3).ToArray();
                for (int i = -1; i <= 1; i++)
                {
                    double aim = Math.Clamp(c.X + i * 100, Left + 25, Right - 25);
                    var z = MotherAdd(m, MotherArt.Child, aim, Math.Max(0, c.Height - 40), 60, 80, warn + Math.Abs(i) * 0.25, .8 * slow);
                    z.Moving = true; z.FromX = s.X; z.FromH = s.Height * .5; z.ToX = aim; z.ToH = Math.Max(0, c.Height - 40);
                    if (i + 1 < escorts.Length) { var child = escorts[i + 1]; z.ActorId = child.Id; z.FromX = child.X; z.FromH = child.LeapHeight; }
                }
                break;
            case 4:
                // Two ribbons sweep the full field at different heights with a body-sized middle gap.
                MotherAdd(m, MotherArt.Ribbon, (Left + Right) / 2, 0, span, 45, warn, 0.6);
                MotherAdd(m, MotherArt.Ribbon, (Left + Right) / 2, Math.Max(220, SkyHeight * 0.6), span, 45, warn + 0.6, 0.6); break;
            case 5:
                for (int side = -1; side <= 1; side += 2)
                {
                    var z = MotherAdd(m, MotherArt.Flood, s.X, 0, 65, 75, warn, 2 * slow);
                    z.Moving = true; z.FromX = s.X; z.ToX = side < 0 ? Left : Right;
                    var shell = MotherAdd(m, MotherArt.Shell, s.X, 0, 45, 65, warn + 0.5, 2 * slow);
                    shell.Moving = true; shell.FromX = s.X; shell.ToX = side < 0 ? Left : Right;
                }
                break;
            case 6:
                int safeRay = m.Random.Next(2, 5);
                for (int ray = 0; ray < 7; ray++)
                {
                    if (Math.Abs(ray - safeRay) <= 1) continue;
                    double endX = Left + span * ray / 6;
                    var ribbon = MotherAdd(m, MotherArt.Ribbon, (s.X + endX) / 2, 0, Math.Abs(endX - s.X) + 18, SkyHeight * .8, warn, .7);
                    ribbon.Thread = true; ribbon.FromX = s.X; ribbon.FromH = s.Height * .8; ribbon.ToX = endX; ribbon.ToH = SkyHeight * .85;
                }
                break;
            case 7:
                for (int beat = 0; beat < 2; beat++)
                {
                    double safe = Left + span * (beat == 0 ? 0.3 : 0.7);
                    for (double x = Left + 35; x < Right; x += 100)
                        if (Math.Abs(x - safe) > 140) MotherAdd(m, MotherArt.Shell, x, 0, 65, SkyHeight, warn + beat * 1.4 * slow, 0.5);
                }
                break;
            case 8:
                double from = s.X;
                for (int i = 0; i < 3; i++)
                {
                    double aim = Math.Clamp(c.X + (i - 1) * 140, Left + s.Width / 2, Math.Max(Left + s.Width / 2, Right - s.Width / 2));
                    var jump = MotherAdd(m, MotherArt.Hug, aim, 0, s.Width + 20, s.Height, warn + i * .9 * slow, .6 * slow);
                    jump.Moving = jump.Jump = true; jump.FromX = from; jump.ToX = aim; from = aim;
                }
                break;
        }
        m.Clock = m.Zones.Count == 0 ? 4 : m.Zones.Max(z => z.Warn + z.Life) + (s.Type.BossTactics?.Opening ?? 3.5) * slow;
        m.PoseMove = m.Move; m.ActionAge = 0; m.ActionWarn = warn;
        m.ActionEnd = m.Zones.Count == 0 ? warn : m.Zones.Max(z => z.Warn + z.Life);
        BossSpecials++;
    }
    private void StepMotherMech(Slime s, double dt)
    {
        var m = s.Mother!; var c = CatCentre();
        if (m.Mode == MotherMode.HangingEggs)
        {
            foreach (var t in m.Tokens.Where(t => !t.Done))
            {
                if (!t.Falling) t.X = t.AnchorX + Math.Sin(m.Age * 1.8 + t.AnchorX) * 25;
                else t.H = Math.Max(18, t.H - 150 * dt);
                bool reach = Math.Abs(c.X - t.X) <= HeldReach + 22 && Math.Abs(c.Height - t.H) <= HeldReach + 25;
                if (reach && (catPose?.InHand == true || t.Falling && t.H <= 20 && !CatOffFloor) && Time >= m.SwingAt)
                {
                    m.SwingAt = Time + Math.Max(0.3, arms.AttackCooldown * CooldownFactor);
                    motherSwingStarted = Time; HeldFacing = t.X < c.X ? -1 : 1;
                    AddEffect(BattleEffectKind.Spark, t.X, t.H);
                    events.Add(new(BattleEventKind.Swing, c.X));
                    if (!t.Falling) t.Falling = true;
                    else if (++t.Hits >= 1) t.Done = true;
                }
            }
            if (m.Tokens.All(t => t.Done)) { MotherEventEnd(s, true); return; }
        }
        else if (m.Mode == MotherMode.Cradle)
        {
            double cradleH = Math.Min(SkyHeight - 90, 300);
            bool carrying = m.Tokens.Any(t => t.Carried);
            foreach (var t in m.Tokens.Where(t => !t.Done))
            {
                if (t.Carried)
                {
                    t.X = c.X; t.H = c.Height;
                    if (catPose?.InHand != true) { t.Carried = false; t.H = 20; }
                    else if (Math.Abs(c.X - m.RingX) < 65 && Math.Abs(c.Height - cradleH) < 65)
                    { t.Done = true; t.Carried = false; m.Progress++; }
                }
                else
                {
                    t.X = Math.Clamp(t.X + Math.Sin(m.Age * 2 + t.AnchorX) * 55 * dt, Left + 25, Right - 25);
                    if (!carrying && catPose?.InHand == true && MotherTouches(t.X, 0, 32, 35)) { t.Carried = true; carrying = true; }
                }
            }
            if (Time >= m.NextBeat)
            {
                m.NextBeat = Time + 4;
                MotherAdd(m, MotherArt.Ribbon, c.X, Math.Max(0, c.Height - 35), 90, 75, 1.1, 0.4, 0);
            }
            if (m.Zones.Any(z => z.Live && MotherTouches(z.X, z.H, z.Width, z.Height)))
                foreach (var t in m.Tokens.Where(t => t.Carried)) { t.Carried = false; t.H = 20; }
            if (m.Progress >= 4) { MotherEventEnd(s, true); return; }
        }
        else if (m.Mode == MotherMode.Lullaby)
        {
            foreach (var t in m.Tokens.Where(t => !t.Done))
            {
                t.H += Math.Sin(m.Age * 2 + t.X) * 12 * dt;
                if (catPose?.InHand != true || !MotherTouches(t.X, t.H - 12, 26, 26)) continue;
                t.Done = true;
                if (t.Bad) LandHit(s.Damage * 0.3, c.X, out _); else { m.Progress++; AddEffect(BattleEffectKind.Heart, t.X, t.H); }
            }
            if (m.Progress >= 5) { MotherEventEnd(s, true); return; }
        }
        else if (m.Mode == MotherMode.Stroke)
        {
            bool onRing = !CatOffFloor && Math.Abs(lastCatX - m.RingX) <= 65;
            if (onRing && motherPointer is { } p && p.H >= s.Height * 0.55 && p.H <= s.Height + 30 && Math.Abs(p.X - s.X) < s.Width * 0.6)
            {
                if (double.IsFinite(m.PointerX))
                {
                    double dx = p.X - m.PointerX;
                    if (Math.Abs(dx) < 70 && dx * m.StrokeDirection > 0) m.StrokeTravel += Math.Abs(dx);
                    else if (dx * m.StrokeDirection < -3) m.StrokeTravel = 0;
                }
                m.PointerX = p.X;
                if (m.StrokeTravel >= s.Width * 0.5)
                {
                    m.Progress++; m.StrokeDirection *= -1; m.StrokeTravel = 0; m.PointerX = double.NaN;
                    m.RingX = Left + (Right - Left) * (m.Progress % 2 == 0 ? 0.25 : 0.75);
                    AddEffect(BattleEffectKind.Heart, s.X, s.Height);
                }
            }
            else { m.PointerX = double.NaN; m.StrokeTravel = 0; }
            if (m.Progress >= 3) { MotherEventEnd(s, true); return; }
        }
        else if (m.Mode == MotherMode.LastEgg)
        {
            if (Time >= m.NextBeat)
            {
                m.NextBeat = Time + 4.2; m.Beat++;
                MotherAdd(m, MotherArt.Heartbeat, (Left + Right) / 2, 0, Right - Left, 65, 1.0, 0.35, 0.5);
            }
            var wave = m.Zones.LastOrDefault(z => z.Art == MotherArt.Heartbeat);
            bool open = wave != null && wave.Age >= wave.Warn + wave.Life && wave.Age < wave.Warn + wave.Life + 1.8;
            if (open && !CatStunned && catPose?.Flying != true && Math.Abs(c.X - s.X) < HeldReach + s.Width / 2 && Math.Abs(c.Height - s.Height * 0.4) < HeldReach + 35 && Time >= m.SwingAt)
            {
                m.SwingAt = Time + Math.Max(0.25, arms.AttackCooldown * CooldownFactor); m.Progress++;
                motherSwingStarted = Time; HeldFacing = s.X < c.X ? -1 : 1;
                AddEffect(BattleEffectKind.Heart, s.X, s.Height * 0.5); events.Add(new(BattleEventKind.Swing, c.X));
                if (m.Progress >= 5) { MotherEventEnd(s, true); return; }
            }
        }
        if (Time >= m.Deadline) MotherEventEnd(s, false);
    }
    private int CutMotherRopes(double x0, double h0, double x1, double h1, double pad)
    {
        int n = 0;
        if (motherWrapLeft > 0 && Find(motherWrapOwner) is Slime owner && SegmentGap(x0, h0, x1, h1, lastCatX, CatStandHeight, owner.X, owner.Height * 0.5) <= pad)
        { ClearMotherWrap(); n++; }
        foreach (var s in slimes)
            if (s.Targetable && s.Mother is { Mode: MotherMode.HangingEggs } m)
                foreach (var t in m.Tokens)
                    if (!t.Done && !t.Falling && SegmentGap(x0, h0, x1, h1, t.X, t.H, t.AnchorX, t.AnchorH) <= pad) { t.Falling = true; n++; }
        return n;
    }
}
