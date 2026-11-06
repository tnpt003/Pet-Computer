using System;
using System.Collections.Generic;

namespace DesktopPet;

// 2026-11-06, the Zip Slime's own blows (docs/ZIP-SLIME.md). Each stage has three moves (ZipSpec.Moves), drawn
// from a bag shuffled from the battle's seed: all the valid ones show before it is shaken again, the last one
// never straight away. Every move runs warn → lock → strike → recover: what was announced does not change
// after the lock, and a stun, the Grabber or a shield ends a move at once, with no late hit.
// Its hits look at where the cat really is (CatInVolume): the floor moves miss a cat lifted over them, the moves
// marked Air are aimed at it wherever it is. A hit never makes the hand let go, stuns a cat in it or changes a
// throw (HurtCat). Physics of the cat stay the window's.
public enum ZipAct { None, Clamp, Bolt, Tumble, Tree, Chase, Faulty, Overflow, Overwrite, Tear }
public enum ZipBeat { None, Warn, Lock, Strike, Recover }
public enum ZipBlowKind { Branch, Mark, Belt, Canopy }
public enum ZipVisual { Clamp, Bolt, Tumble, Tree, Chase, Faulty, Overflow, Overwrite, Tear, Repulse, Burst, Belt }

// One blow waiting on a marked spot: Left s to go, then it hits once in its box and shows Life s more.
public sealed class ZipBlow
{
    public int OwnerId, Order, Variant;
    public double X, Radius, Bottom, Top, Left, Warn, Life, Damage, Stun;
    public double FromX, ToX, Travel, Elapsed;
    public ZipBlowKind Kind;
    public bool Done;
    public bool Hit;
    public double Front => FromX + (ToX - FromX) * Math.Clamp(Travel > 0 ? Elapsed / Travel : 1, 0, 1);
}

public sealed partial class BattleSim
{
    private const double CatHalfHeight = 24, TumbleBackWarn = 0.6, RepeatAfter = 2.5, ShellLife = 0.25;
    private readonly List<ZipBlow> zipBlows = new();
    public IReadOnlyList<ZipBlow> ZipBlows => zipBlows;
    public int ZipMovesUsed { get; private set; }
    public int ZipPacketBlasts { get; private set; }
    public int ZipHits { get; private set; }

    public static ZipMoveSpec? MoveSpec(Slime s, ZipAct act) => s.Type.Zip?.Move(act);

    // The cat's box against a box of the field: never during a landing's grace; a cat lifted with no pose
    // known is out of reach.
    private bool CatInVolume(double x0, double x1, double bottom, double top)
    {
        if (Grace > 0 || (CatLifted && catPose == null)) return false;
        var (x, h) = CatCentre();
        return x >= x0 && x <= x1 && h + CatHalfHeight >= bottom && h - CatHalfHeight <= top;
    }

    // True while the cat is off the floor in the user's hand, or in flight after a throw.
    private bool CatOffFloor => CatLifted || catPose is { Flying: true };

    private bool ZipHit(Slime boss, double damage, double stun, bool crush = false)
    {
        var (x, _) = CatCentre();
        ZipHits++;
        return HurtCat(boss, x, stun > 0, Share(boss, damage), stun, crush: crush);
    }

    // The next move for this stage, or None to go on walking.
    private ZipAct NextZipAct(Slime boss, ZipStage stage, ZipSpec zip, double dx)
    {
        bool Fits(ZipAct act)
        {
            if (zip.Move(act) is not ZipMoveSpec m) return false;
            double far = Math.Abs(dx);
            return far >= m.MinRange && far <= m.MaxRange && (m.Air || !CatOffFloor);
        }
        zipRandom ??= BattleRandom.Stream(Seed, BattleRandom.Zip);
        var moves = stage.Acts;
        if (boss.ZipBag.Count == 0)
        {
            boss.ZipBag.AddRange(moves);
            for (int i = boss.ZipBag.Count - 1; i > 0; i--)
            {
                int j = zipRandom.Next(i + 1);
                (boss.ZipBag[i], boss.ZipBag[j]) = (boss.ZipBag[j], boss.ZipBag[i]);
            }
            if (boss.ZipBag.Count > 1 && boss.ZipBag[0] == boss.ZipLast) (boss.ZipBag[0], boss.ZipBag[^1]) = (boss.ZipBag[^1], boss.ZipBag[0]);
        }
        for (int i = 0; i < boss.ZipBag.Count; i++)
            if (boss.ZipBag[i] != boss.ZipLast && Fits(boss.ZipBag[i]))
            {
                var act = boss.ZipBag[i];
                boss.ZipBag.RemoveAt(i);
                return act;
            }
        // Nothing left in the bag fits: any other move that does, and the same one only after a while.
        ZipAct other = ZipAct.None;
        int ways = 0;
        foreach (var act in moves)
            if (act != boss.ZipLast && Fits(act) && zipRandom.Next(++ways) == 0) other = act;
        if (other != ZipAct.None) return other;
        return boss.ZipIdle >= RepeatAfter && Fits(boss.ZipLast) ? boss.ZipLast : ZipAct.None;
    }

    private void BeginZipAct(Slime boss, ZipAct act, ZipMoveSpec m)
    {
        var (catX, catH) = CatCentre();
        boss.ZipAct = boss.ZipLast = act;
        boss.ZipIdle = 0;
        boss.ZipBeat = ZipBeat.Warn;
        boss.ZipLeft = boss.ZipSeconds = m.Warn;
        boss.ZipFrom = boss.X;
        boss.ZipAimX = Math.Clamp(catX, Left + 24, Right - 24);
        boss.ZipAimHeight = catH;
        boss.ZipHitMask = 0;
        boss.ZipCount = 0;
        boss.ZipTear = 0;
        boss.Facing = boss.ZipAimX < boss.X ? -1 : 1;
        boss.ZipHeadX = boss.X;
        boss.ZipHeadHeight = boss.Height * 0.7;
        ZipMovesUsed++;
        lastWindup = Time;
        events.Add(new(BattleEventKind.ZipMoveWarned, boss.X, (int)act));
        switch (act)
        {
            case ZipAct.Tumble: BeginTumble(boss); break;
            case ZipAct.Tree: PlantTree(boss, m); break;
            case ZipAct.Overflow:
                double room = m.Radius + 40;
                boss.ZipAimX = Math.Clamp(boss.ZipAimX, Left + room, Math.Max(Left + room, Right - room));
                break;
        }
    }

    private void BeginTumble(Slime boss)
    {
        boss.ZipCount = 1;
        AimZipRoll(boss, boss.ZipAimX);
        boss.RollBack = false;
        boss.ZipBeat = ZipBeat.Strike;
        boss.Set(SlimeState.Windup);
        boss.Rolling = true;
        events.Add(new(BattleEventKind.RollAimed, boss.X, boss.Id));
    }

    private void AimZipRoll(Slime boss, double catX)
    {
        var (minX, maxX) = Bounds(boss);
        int way = catX < boss.X ? -1 : 1;
        double room = way < 0 ? boss.X - minX : maxX - boss.X;
        if (room < 1) { way = -way; room = way < 0 ? boss.X - minX : maxX - boss.X; }
        boss.Facing = boss.DashDirection = way;
        boss.RollDistance = Math.Min(boss.Type.Roll!.Distance, room);
        boss.RollGoal = boss.X + way * boss.RollDistance;
    }

    // The branches, nearest first on both sides in turn, each with its gap to the next and one more left out.
    private void PlantTree(Slime boss, ZipMoveSpec m)
    {
        zipRandom ??= BattleRandom.Stream(Seed, BattleRandom.Zip);
        int skip = zipRandom.Next(m.Count), order = 0;
        for (int i = 0; i < m.Count; i++)
        {
            int side = i % 2 == 0 ? 1 : -1, step = i / 2 + 1;
            double x = boss.X + side * step * m.Spacing;
            if (i == skip || x < Left + 24 || x > Right - 24) continue;
            AddZipBlow(boss, x, m.Radius, 0, m.Height, m.Warn + order * m.Delay, m, ZipBlowKind.Branch, ++order);
        }
    }

    private void AddZipBlow(Slime boss, double x, double radius, double bottom, double top, double left, ZipMoveSpec m, ZipBlowKind kind, int order) =>
        zipBlows.Add(new ZipBlow { OwnerId = boss.Id, X = x, Radius = radius, Bottom = bottom, Top = top, Left = left, Warn = left, Life = kind == ZipBlowKind.Branch ? 0.65 : ShellLife, Damage = m.Damage, Stun = m.Stun, Kind = kind, Order = order, FromX = boss.X, ToX = 2 * x - boss.X, Travel = kind == ZipBlowKind.Belt ? 0.35 : 0, Variant = (zipRandom ??= BattleRandom.Stream(Seed, BattleRandom.Zip)).Next(3) });

    // Called once a step for every enemy before its state machine; true: this boss does nothing else now.
    private bool StepZipAct(Slime boss, ZipSpec zip, double dt)
    {
        if (boss.ZipAct == ZipAct.None) return false;
        if (boss.Shielded || boss.State == SlimeState.Stunned || boss.Handled) { EndZipAct(boss); return false; }
        if (boss.ZipAct == ZipAct.Tumble)
        {
            if (!boss.Rolling) EndZipAct(boss);
            return false; // The roll's own states (StepRoll) carry it.
        }
        var m = zip.Move(boss.ZipAct)!;
        boss.ZipLeft = Math.Max(0, boss.ZipLeft - dt * boss.ChillFactor);
        double done = boss.ZipSeconds > 0 ? 1 - boss.ZipLeft / boss.ZipSeconds : 1;
        if (boss.ZipBeat == ZipBeat.Warn) WarnBeat(boss, m, done);
        else if (boss.ZipBeat == ZipBeat.Strike) StrikeBeat(boss, m, done);
        if (Over || !boss.Targetable) return true;
        if (boss.ZipLeft > 0) return true;
        switch (boss.ZipBeat)
        {
            case ZipBeat.Warn when m.Lock > 0:
                NextBeat(boss, ZipBeat.Lock, m.Lock);
                break;
            case ZipBeat.Warn:
            case ZipBeat.Lock:
                NextBeat(boss, ZipBeat.Strike, StrikeSeconds(m));
                Landed(boss, m);
                break;
            case ZipBeat.Strike:
                FinishStrike(boss, m);
                if (!Over) NextBeat(boss, ZipBeat.Recover, m.Recover);
                break;
            default:
                boss.Cooldown = StageOf(boss)!.Cooldown * (boss.Elite?.AttackCooldown ?? 1);
                EndZipAct(boss);
                break;
        }
        return true;
    }

    private static void NextBeat(Slime boss, ZipBeat beat, double seconds)
    {
        boss.ZipBeat = beat;
        boss.ZipLeft = boss.ZipSeconds = seconds;
    }

    // Tree and marks strike on their own clocks: the beat only waits for the last of them.
    private static double StrikeSeconds(ZipMoveSpec m) => m.Act switch
    {
        ZipAct.Tree or ZipAct.Overwrite => m.Count * m.Delay + ShellLife,
        _ => Math.Max(0.05, m.Strike)
    };

    private void EndZipAct(Slime boss)
    {
        boss.ZipAct = ZipAct.None;
        boss.ZipBeat = ZipBeat.None;
        boss.ZipLeft = boss.ZipSeconds = 0;
        boss.RollBack = boss.RollBounce = false;
        boss.ZipTear = 0;
        zipBlows.RemoveAll(b => b.OwnerId == boss.Id);
    }

    private void WarnBeat(Slime boss, ZipMoveSpec m, double done)
    {
        var (catX, catH) = CatCentre();
        switch (m.Act)
        {
            case ZipAct.Clamp:
            case ZipAct.Overflow:
                // It slides to the marked spot, in the first part of the warning.
                var (minX, maxX) = Bounds(boss);
                double to = Math.Clamp(boss.ZipAimX, minX, maxX), most = m.Speed * boss.ZipSeconds * Math.Min(1, done * 2);
                boss.X = boss.ZipFrom + Math.Clamp(to - boss.ZipFrom, -most, most);
                break;
            case ZipAct.Bolt:
                boss.ZipAimX = catX; boss.ZipAimHeight = catH; // Follows until the lock.
                break;
            case ZipAct.Overwrite:
                // Three marks in turn, each where the cat is at that moment and staying there.
                int due = Math.Min(m.Count, (int)(done * m.Count) + 1);
                while (boss.ZipCount < due)
                {
                    double high = catH > CatStandHeight + 12 ? catH : 0;
                    double wait = boss.ZipLeft + m.Lock + boss.ZipCount * m.Delay;
                    AddZipBlow(boss, Math.Clamp(catX, Left + 24, Right - 24), m.Radius, high > 0 ? high - m.Radius : 0, high > 0 ? high + m.Radius : m.Radius + 40, wait, m, ZipBlowKind.Mark, ++boss.ZipCount);
                    events.Add(new(BattleEventKind.ZipMarked, catX, boss.ZipCount));
                }
                break;
        }
    }

    // What happens the moment the strike starts.
    private void Landed(Slime boss, ZipMoveSpec m)
    {
        events.Add(new(BattleEventKind.ZipMoveLanded, boss.X, (int)m.Act));
        switch (m.Act)
        {
            case ZipAct.Clamp:
                AddZipEffect(ZipVisual.Clamp, boss.ZipAimX, 0, m.Radius);
                if (CatInVolume(boss.ZipAimX - m.Radius, boss.ZipAimX + m.Radius, 0, m.Height)) ZipHit(boss, m.Damage, m.Stun);
                break;
            case ZipAct.Chase:
                ThrowFiles(boss, m);
                break;
            case ZipAct.Faulty:
                ThrowPackets(boss, m);
                break;
        }
    }

    private void StrikeBeat(Slime boss, ZipMoveSpec m, double done)
    {
        switch (m.Act)
        {
            case ZipAct.Bolt:
            {
                // Out to the locked spot and back along the same line, dangerous both ways.
                double fromX = boss.X, fromH = boss.Height * 0.7, k = done < 0.5 ? done * 2 : (1 - done) * 2;
                double x0 = boss.ZipHeadX, h0 = boss.ZipHeadHeight;
                boss.ZipHeadX = fromX + (boss.ZipAimX - fromX) * k;
                boss.ZipHeadHeight = fromH + (boss.ZipAimHeight - fromH) * k;
                var (catX, catH) = CatCentre();
                if (boss.ZipHitMask == 0 && Grace <= 0 && !(CatLifted && catPose == null)
                    && Near(x0, h0, boss.ZipHeadX, boss.ZipHeadHeight, catX, catH) <= m.Radius)
                {
                    AddZipEffect(ZipVisual.Bolt, catX, catH, m.Radius);
                    boss.ZipHitMask = 1; // One hit a shot: going out and coming back are two chances at it.
                    ZipHit(boss, m.Damage, m.Stun);
                }
                break;
            }
            case ZipAct.Overflow:
                boss.ZipTear = Math.Clamp(done, 0, 1);
                var (l, r) = OverflowEdges(boss, m, boss.ZipTear);
                if ((boss.ZipHitMask & 1) == 0 && (CatInVolume(Left, l, 0, m.Height) || CatInVolume(r, Right, 0, m.Height)))
                {
                    boss.ZipHitMask |= 1;
                    ZipHit(boss, m.Damage * 0.5, 0);
                }
                break;
            case ZipAct.Tear:
            {
                // Out, a beat at the far end, back in: one hit on each sweep at most.
                const double hold = 0.14;
                double before = boss.ZipTear;
                double k = done < 0.5 - hold ? done / (0.5 - hold) : done > 0.5 + hold ? (1 - done) / (0.5 - hold) : 1;
                boss.ZipTear = Math.Clamp(k, 0, 1);
                double a = Math.Min(before, boss.ZipTear) * m.Spacing, b = Math.Max(before, boss.ZipTear) * m.Spacing;
                int way = done <= 0.5 ? 1 : 2;
                if ((boss.ZipHitMask & way) == 0
                    && (CatInVolume(boss.X - b - m.Radius, boss.X - a + m.Radius, 0, m.Height) || CatInVolume(boss.X + a - m.Radius, boss.X + b + m.Radius, 0, m.Height)))
                {
                    boss.ZipHitMask |= way;
                    AddZipEffect(ZipVisual.Tear, lastCatX, 0, m.Radius);
                    ZipHit(boss, m.Damage, m.Stun);
                }
                break;
            }
        }
    }

    private void FinishStrike(Slime boss, ZipMoveSpec m)
    {
        if (m.Act != ZipAct.Overflow) { boss.ZipTear = 0; return; }
        // The burst: all the floor outside the gap at once, up to the top of the strip.
        var (l, r) = OverflowEdges(boss, m, 1);
        AddZipEffect(ZipVisual.Overflow, (Left + l) / 2, 0, Math.Max(10, (l - Left) / 2));
        AddZipEffect(ZipVisual.Overflow, (r + Right) / 2, 0, Math.Max(10, (Right - r) / 2));
        boss.ZipTear = 0;
        if (CatInVolume(Left, l, 0, m.BurstHeight) || CatInVolume(r, Right, 0, m.BurstHeight)) ZipHit(boss, m.Damage, m.Stun);
    }

    // The spilling edges' reach after `share` of the way: the safe gap stays round the marked spot.
    public (double Left, double Right) OverflowEdges(Slime boss, ZipMoveSpec m, double share)
    {
        double left = boss.ZipAimX - m.Radius, right = boss.ZipAimX + m.Radius;
        return (Left + (left - Left) * share, Right - (Right - right) * share);
    }

    private void ThrowFiles(Slime boss, ZipMoveSpec m)
    {
        double damage = boss.Damage * Share(boss, m.Damage);
        foreach (int side in new[] { -1, 0, 1 })
            shots.Add(new BattleShot
            {
                OwnerId = boss.Id, X = boss.X + side * boss.Width * 0.3, Height = boss.Height * 0.8, VX = side * m.Speed * 0.55, VY = m.Speed * 0.75,
                Remaining = 4, Damage = damage, Radius = m.Radius, Tint = "#FFFBF2", Paper = true, LockIn = m.Delay, Speed = m.Speed
            });
    }

    // Papers curve up before locking, then pursue at constant speed with a bounded angular velocity.
    private void StepPaper(BattleShot shot, double dt)
    {
        if (shot.LockIn > 0)
        {
            shot.LockIn -= dt;
            shot.VY -= shot.Speed * 1.1 * dt;
            if (shot.LockIn > 0) return;
            events.Add(new(BattleEventKind.ZipFileLocked, shot.X));
        }
        var (x, h) = CatCentre();
        double dx = x - shot.X, dh = h - shot.Height;
        double angle = Math.Atan2(shot.VY, shot.VX), target = Math.Atan2(dh, dx);
        double turn = Math.Atan2(Math.Sin(target - angle), Math.Cos(target - angle));
        angle += Math.Clamp(turn, -2.8 * dt, 2.8 * dt);
        shot.VX = Math.Cos(angle) * shot.Speed;
        shot.VY = Math.Sin(angle) * shot.Speed;
    }

    private void ThrowPackets(Slime boss, ZipMoveSpec m)
    {
        var kind = Settings.Enemy("zip_packet");
        zipRandom ??= BattleRandom.Stream(Seed, BattleRandom.Zip);
        int live = zipRandom.Next(m.Count);
        double middle = Math.Clamp((boss.X + lastCatX) / 2, Left + m.Spacing + 30, Math.Max(Left + m.Spacing + 30, Right - m.Spacing - 30));
        for (int i = 0; i < m.Count; i++)
        {
            var packet = AddSlime(middle + (i - (m.Count - 1) / 2.0) * m.Spacing, kind);
            packet.SummonedBy = boss.Id;
            packet.Facing = -1;
            packet.PacketLeft = m.Life;
            packet.PacketFuse = i == live;
        }
    }

    // A packet thrown out by Faulty unpacking, on its clock.
    private void StepPacket(Slime s, double dt)
    {
        if (s.PacketLeft <= 0) return;
        if (PointerOn(s)) s.PacketRevealed = true;
        s.PacketLeft = Math.Max(0, s.PacketLeft - dt);
        if (s.PacketLeft > 0) return;
        if (!s.PacketFuse) return; // Decoys must not clear the live bomb before its own turn.
        if (s.PacketFuse && Find(s.SummonedBy) is { Targetable: true } boss && MoveSpec(boss, ZipAct.Faulty) is ZipMoveSpec m)
        {
            ZipPacketBlasts++;
            AddZipEffect(ZipVisual.Faulty, s.X, 0, m.Radius);
            events.Add(new(BattleEventKind.ZipPacketBurst, s.X));
            if (CatInVolume(s.X - m.Radius, s.X + m.Radius, 0, m.Height)) ZipHit(boss, m.Damage, m.Stun);
        }
        ClearPackets(s.SummonedBy);
    }

    private void Unpacked(Slime s)
    {
        AddEffect(BattleEffectKind.Poof, s.X, 12);
        effects[^1].Tint = s.Type.Color;
        s.Hp = 0;
        s.Set(SlimeState.Defeated);
        s.Escaped = true; // No kill, no reward: it is only paper now.
        s.RopedTo = 0;
    }

    // Every thrown packet of a boss goes (those on a rope stay).
    private void ClearPackets(int bossId)
    {
        foreach (var s in slimes)
            if (s.SummonedBy == bossId && s.Type.Packet && s.ZipProp == ZipProp.None && s.State != SlimeState.Defeated && s.RopedTo == 0) Unpacked(s);
    }

    private void StepZipBlows(double dt)
    {
        for (int i = zipBlows.Count - 1; i >= 0; i--)
        {
            var b = zipBlows[i];
            if (!b.Done) { b.Left -= dt; continue; }
            b.Elapsed += dt;
            b.Life -= dt;
            if (b.Life <= 0) zipBlows.RemoveAt(i);
        }
        if (zipBlows.Count == 0) return; // Every frame of every battle: nothing to copy.
        // In order, oldest first: a hit can end the battle.
        foreach (var b in zipBlows.ToArray())
        {
            if (b.Kind == ZipBlowKind.Canopy)
            {
                if (!b.Done && b.Left <= 0) b.Done = true;
                if (b.Done && !b.Hit && Find(b.OwnerId) is { Targetable: true } owner && CatInVolume(b.X - b.Radius, b.X + b.Radius, 0, b.Top))
                {
                    b.Hit = true;
                    ZipHit(owner, b.Damage, b.Stun);
                }
                if (Over) return;
                continue;
            }
            if (b.Kind == ZipBlowKind.Belt)
            {
                if (!b.Done && b.Left <= 0)
                {
                    b.Done = true;
                    b.Life = 0.55;
                    events.Add(new(BattleEventKind.ZipBlowLanded, b.FromX, b.Order));
                }
                if (b.Done && !b.Hit && b.Elapsed > 0 && b.Elapsed - dt < b.Travel)
                {
                    double before = b.FromX + (b.ToX - b.FromX) * Math.Clamp((b.Elapsed - dt) / b.Travel, 0, 1);
                    if (Find(b.OwnerId) is { Targetable: true } owner && CatInVolume(Math.Min(before, b.Front) - 12, Math.Max(before, b.Front) + 12, b.Bottom, b.Top))
                    {
                        b.Hit = true;
                        ZipHit(owner, b.Damage, b.Stun);
                    }
                }
                if (Over) return;
                continue;
            }
            if (b.Done || b.Left > 0) continue;
            b.Done = true;
            if (b.Kind != ZipBlowKind.Branch) AddZipEffect(b.Kind == ZipBlowKind.Belt ? ZipVisual.Belt : ZipVisual.Overwrite, b.X, b.Bottom, b.Radius);
            events.Add(new(BattleEventKind.ZipBlowLanded, b.X, b.Order));
            if (Find(b.OwnerId) is { Targetable: true } boss && CatInVolume(b.X - b.Radius, b.X + b.Radius, b.Bottom, b.Top)) ZipHit(boss, b.Damage, b.Stun);
            if (Over) return;
        }
    }

    // A slime fell: the Zip Slime leaves nothing on the field; a live packet takes its batch with it.
    private void ZipFallen(Slime s)
    {
        if (s.Type.Packet && s.PacketFuse) { ClearPackets(s.SummonedBy); return; }
        if (s.Type.Zip == null) return;
        EndZipAct(s);
        ClearZipProps(s);
        foreach (var other in slimes)
            if (other.SummonedBy == s.Id && other.Type.Packet && other.State != SlimeState.Defeated) Unpacked(other);
        ClearZipDebuff(false);
    }

    // What a floor spot risks in the moves under way (the balance player's eyes; the same shapes as the hits).
    public bool ZipThreatens(Slime s, double x)
    {
        if (s.Type.Zip == null) return false;
        foreach (var b in zipBlows) if (!b.Done && b.Bottom <= CatStandHeight + 30 && Math.Abs(b.X - x) <= b.Radius) return true;
        if (s.ZipAct == ZipAct.None || s.Type.Zip.Move(s.ZipAct) is not ZipMoveSpec m) return false;
        return s.ZipAct switch
        {
            ZipAct.Clamp => s.ZipBeat is ZipBeat.Warn or ZipBeat.Lock && Math.Abs(x - s.ZipAimX) <= m.Radius,
            ZipAct.Overflow => s.ZipBeat is ZipBeat.Warn or ZipBeat.Lock or ZipBeat.Strike && Math.Abs(x - s.ZipAimX) > m.Radius,
            ZipAct.Tear => s.ZipBeat is ZipBeat.Warn or ZipBeat.Lock or ZipBeat.Strike && Math.Abs(x - s.X) <= m.Spacing + m.Radius,
            ZipAct.Tumble => s.Rolling && x >= Math.Min(s.X, s.RollGoal) - s.Width && x <= Math.Max(s.X, s.RollGoal) + s.Width,
            _ => false
        };
    }
}
