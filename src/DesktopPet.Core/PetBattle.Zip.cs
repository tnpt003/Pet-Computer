using System;
using System.Collections.Generic;

namespace DesktopPet;

// 1.5 M17, B1 "Slime Nén" (the .zip slime, chapter 2's boss); redone 2026-11-05 and 2026-11-06 (docs/ZIP-SLIME.md).
// It starts small and quick and swells as it unpacks (ZipSpec.Stages). Each stage has three blows drawn from a bag
// (PetBattle.ZipMoves.cs); two debuffs land apart from them (PetBattle.ZipDebuff.cs). Each stage ends in, or
// holds, a riddle; solving one earns a long stun and extra damage, missing it only costs that (a battle never
// hangs on one):
// - Stage 1, burst the belts (BossPhase.Press): its zipper jammed it slides to a stop shown on the floor; the cat
//   let go on it there pins it and bursts a belt; two pins open it.
// - Stage 2, stuck unpacking (BossPhase.Roped): a shield phase whose helpers are data packets, each tied to the
//   boss; the shield holds while any is left. The cat going through a stream fast enough (held past the drag
//   threshold, or thrown) or a Scissors stroke cuts it; breaking the packet ends it too. Two cuts together stun
//   it longer.
// - Stage 3, the zip bomb (BossPhase.Bomb): it swells behind a shield while a clock runs; the cat through its
//   zipper tab or a Scissors stroke pulls it. Every pull done: it lets its air out and flies round the field.
//   Time out: it bursts over the floor (a cat lifted over the blast is not hit) and arms again later.
// Physics of the cat stay the window's: only its pose is read (PetBattle.Held.cs).
public sealed partial class BattleSim
{
    // How small a deflated Zip Slime gets, and how fast it flies letting its air out.
    public const double DeflatedSize = 0.62;
    private const double DeflateSpeed = 980, TabLockSeconds = 0.5, BurstHeight = 170;
    private Random? zipRandom;

    public int BossRopesCut { get; private set; }
    public int Rolls { get; private set; }
    public int RollHits { get; private set; }
    public int ZipPins { get; private set; }
    public int ZipBursts { get; private set; }

    // The phase a shield tag stands for (null: none).
    private static BossPhase? PhaseOf(Slime boss, int tag) =>
        tag > 0 && boss.Type.Phases is { } phases && tag <= phases.Length ? phases[tag - 1] : null;
    // The shield phase a boss is in now (null: its shield is down).
    public BossPhase? ShieldPhase(Slime boss) => boss.Shielded ? PhaseOf(boss, boss.ShieldTag) : null;

    // The stage a Zip Slime is in (null: another kind).
    public static ZipStage? StageOf(Slime s) => s.Type.Zip is { } zip ? zip.Stages[Math.Clamp(s.ZipStage, 0, zip.Stages.Length - 1)] : null;
    // The share of its plain damage a blow of `damage` is (the level's factors stay on its Damage).
    private static double Share(Slime boss, double damage) => boss.Type.Damage > 0 ? damage / boss.Type.Damage : 0;
    private static double ZipBonus(Slime s) => s.MechBonusClock > 0 ? s.MechBonus : 1;

    // What a riddle shows the view: the bomb's filling (0–1; null: none armed) and its tab.
    public static double? BombFill(Slime s) => s.Type.Zip is { } zip && s.BombClock > 0 ? 1 - s.BombClock / zip.Bomb.Charge : null;
    public static (double X, double Height)? BombTab(Slime s) => s.BombClock > 0 ? (s.TabX, s.TabHeight) : null;
    // The stop of a riddle's roll, while the cat can still be dropped there (null: none).
    public static double? PressSpot(Slime s) => s.PressRolling ? s.RollGoal : s.PressWindow > 0 ? s.X : null;

    private void ZipSize(Slime boss, double scale)
    {
        boss.ZipScale = scale;
        boss.Width = Settings.SlimeWidth * scale;
        boss.HitWidth = Settings.SlimeWidth * scale * boss.Type.Hitbox / boss.Type.Scale;
        boss.Height = Settings.SlimeHeight * scale;
    }

    private double ZipGoalSize(Slime boss) =>
        StageOf(boss)!.Scale * (boss.Deflating > 0 || (boss.BombDone && boss.MechBonusClock > 0) ? DeflatedSize : 1);

    // Just added: its first stage's size and reach.
    private void ZipFit(Slime boss)
    {
        if (StageOf(boss) is not ZipStage stage) return;
        boss.Range = stage.Range;
        ZipSize(boss, stage.Scale);
    }

    // Every step: its size eases to the stage's, its reward and the bomb's rearming run out.
    private void TickZip(Slime boss, double dt)
    {
        if (boss.Type.Zip is not ZipSpec zip) return;
        double goal = ZipGoalSize(boss);
        if (boss.ZipScale != goal)
        {
            double step = StageOf(boss)!.Scale * dt / zip.Grow;
            ZipSize(boss, boss.ZipScale + Math.Clamp(goal - boss.ZipScale, -step, step));
        }
        boss.MechBonusClock = Math.Max(0, boss.MechBonusClock - dt);
        boss.PressWindow = Math.Max(0, boss.PressWindow - dt);
        boss.TabLock = Math.Max(0, boss.TabLock - dt);
        // A stun cut its riddle's roll short: that roll is over (the next one starts from here).
        if (boss.PressRolling && !boss.Rolling) boss.PressRolling = false;
        boss.ZipIdle += dt;
        TickZipDebuff(boss, zip, dt);
        if (boss.BombRearm > 0 && !boss.Shielded && boss.State != SlimeState.Stunned)
        {
            boss.BombRearm = Math.Max(0, boss.BombRearm - dt);
            if (boss.BombRearm == 0) ArmBomb(boss);
        }
    }

    // A new stage: bigger, slower, its blows heavier.
    private void ZipGrow(Slime boss)
    {
        if (boss.Type.Zip is not ZipSpec zip || boss.ZipStage >= zip.Stages.Length - 1) return;
        boss.ZipStage++;
        boss.Range = StageOf(boss)!.Range;
        boss.ZipBag.Clear(); // The new stage's own moves.
        boss.ZipLast = ZipAct.None;
        boss.Cooldown = Math.Max(boss.Cooldown, 1);
        AddEffect(BattleEffectKind.Poof, boss.X, 4, 2);
        events.Add(new(BattleEventKind.ZipGrew, boss.X, boss.ZipStage));
    }

    public double ZipWindup(Slime boss)
    {
        if (boss.Rolling) return boss.RollBounce ? TumbleBackWarn : (boss.PressRolling ? boss.Type.Zip!.Press.Roll : boss.Type.Roll!).Windup;
        return boss.Spitting && boss.Type.Spit is SpitSpec spit ? spit.Windup : StageOf(boss)!.Windup;
    }

    // Its moves in the Moving state; false leaves it to the plain enemy code (a roped phase's slam).
    private bool ZipMove(Slime boss, ZipSpec zip, double dt, double dx, double catX, bool catHeld, double minX, double maxX)
    {
        if (boss.Shielded)
        {
            var phase = PhaseOf(boss, boss.ShieldTag);
            if (phase is { Press: true }) { PressMove(boss, zip.Press, dx, minX, maxX); return true; }
            return phase is { Bomb: true };
        }
        var stage = StageOf(boss)!;
        // Locked in its shell the cat cannot be lifted out of the way: nothing starts until it is out and has a moment.
        if (Grace <= 0 && boss.Cooldown <= 0 && !CatLocked && ZipGuardLeft <= 0)
        {
            var act = NextZipAct(boss, stage, zip, dx);
            if (act != ZipAct.None) { BeginZipAct(boss, act, zip.Move(act)!); return true; }
        }
        if (Math.Abs(dx) > stage.Range * 0.75)
        {
            boss.Facing = dx < 0 ? -1 : 1;
            boss.X = Math.Clamp(boss.X + Math.Sign(dx) * Math.Min(Math.Abs(dx), stage.Speed * level.EnemySpeed * boss.ChillFactor * (CatLifted ? HeldFollow : 1) * dt), minX, maxX);
        }
        return true;
    }

    // Its plain slam landing (a roped phase; called once it is in the Recover state): the share of its damage it deals.
    private double ZipStrike(Slime boss)
    {
        if (StageOf(boss) is not ZipStage stage) return boss.Type.SlamShare;
        boss.Cooldown = stage.Cooldown * (boss.Elite?.AttackCooldown ?? 1);
        return Share(boss, stage.Damage);
    }

    // The time to the next roll runs only while the shield is down.
    private static void TickRoll(Slime boss, double dt)
    {
        if (boss.Type.Roll == null || boss.Shielded) return;
        boss.RollCooldown = Math.Max(0, boss.RollCooldown - dt * boss.ChillFactor);
    }

    // A roll's windup instead of walking up: the cat on the floor between MinRange and Range away.
    private bool StartRoll(Slime boss, RollSpec roll, double dx, bool catHeld)
    {
        if (boss.Shielded || boss.RollCooldown > 0 || boss.Cooldown > 0 || catHeld || CatLifted || Grace > 0) return false;
        if (Math.Abs(dx) < roll.MinRange || Math.Abs(dx) > roll.Range) return false;
        boss.Facing = boss.DashDirection = dx < 0 ? -1 : 1;
        boss.RollDistance = roll.Distance;
        boss.RollGoal = boss.X + boss.DashDirection * roll.Distance;
        boss.Set(SlimeState.Windup);
        boss.Rolling = true;
        lastWindup = Time;
        events.Add(new(BattleEventKind.RollAimed, boss.X, boss.Id));
        return true;
    }

    private void BeginRoll(Slime boss)
    {
        boss.DashLeft = boss.RollDistance;
        boss.RollHit = false;
        if (boss.PressRolling) boss.PressLeft--;
        boss.Set(SlimeState.Dashing); // Rolling stays on (SlimeState.Dashing is the roll).
        Rolls++;
        events.Add(new(BattleEventKind.Rolled, boss.X, boss.Id));
    }

    // The roll itself: its locked lane to the end (or the field's edge), the cat checked along the whole
    // way of this frame, as a dash is.
    private void StepRoll(Slime boss, double dt, double catX, double minX, double maxX)
    {
        var roll = boss.PressRolling ? boss.Type.Zip!.Press.Roll : boss.Type.Roll!;
        double before = boss.X;
        double step = boss.Stuck ? 0 : Math.Min(boss.DashLeft, roll.Speed * level.EnemySpeed * boss.ChillFactor * dt);
        boss.X = Math.Clamp(boss.X + boss.DashDirection * step, minX, maxX);
        boss.DashLeft = boss.X == before ? 0 : boss.DashLeft - step;
        double reach = boss.HitWidth / 2 + CatHalfWidth;
        if (!boss.RollHit && CatInVolume(Math.Min(before, boss.X) - reach, Math.Max(before, boss.X) + reach, 0, boss.Height))
        {
            boss.RollHit = true;
            RollHits++;
            events.Add(new(BattleEventKind.RollHit, catX, boss.Id));
            AddEffect(BattleEffectKind.Thud, catX, 0);
            HurtCat(boss, catX, true, Share(boss, roll.Damage), roll.Stun, crush: true);
        }
        if (boss.DashLeft <= 0 && !Over && boss.RollBack)
        {
            // Back once along the same floor, after a beat on the spot: its lane shows again.
            boss.RollBack = false;
            boss.RollBounce = true;
            boss.DashDirection = boss.Facing = -boss.DashDirection;
            boss.RollDistance = Math.Abs(boss.X - boss.ZipFrom);
            boss.RollGoal = boss.ZipFrom;
            boss.Set(SlimeState.Windup);
            boss.Rolling = true;
            lastWindup = Time;
            events.Add(new(BattleEventKind.RollAimed, boss.X, boss.Id));
            return;
        }
        if (boss.DashLeft <= 0 && !Over)
        {
            bool press = boss.PressRolling;
            boss.RollBounce = false;
            boss.Set(SlimeState.Recover);
            boss.Cooldown = AttackCooldown(boss);
            boss.RollCooldown = boss.Enraged ? roll.EnragedEvery : roll.Every;
            if (StageOf(boss) is ZipStage stage) boss.Cooldown = stage.Cooldown + (MoveSpec(boss, ZipAct.Tumble)?.Recover ?? 0);
            if (press) { boss.PressRolling = false; boss.PressWindow = boss.Type.Zip!.Press.Window; boss.Cooldown = boss.PressWindow; }
            AddEffect(BattleEffectKind.Poof, boss.X, 4, 2);
        }
    }

    // How far a rolling boss has turned (degrees, drawing): its way so far over its half height.
    public static double RollTurn(Slime boss) =>
        boss.Rolling && !boss.PressRolling && boss.State == SlimeState.Dashing
            ? (boss.RollDistance - boss.DashLeft) / Math.Max(1, boss.Height / 2) * 180 / Math.PI * boss.DashDirection : 0;

    // The riddles' turn on the move, in place of the shield phase's run and call; true while one holds.
    private bool StepZipRiddle(Slime boss, double dt, double minX, double maxX)
    {
        if (boss.Type.Zip is not ZipSpec zip || PhaseOf(boss, boss.ShieldTag) is not BossPhase phase) return false;
        if (phase.Press)
        {
            if (boss.PressLeft <= 0 && !boss.PressRolling && boss.PressWindow <= 0 && boss.State != SlimeState.Stunned) EndPress(boss, zip.Press, false);
            return true;
        }
        if (!phase.Bomb) return false;
        if (boss.Deflating > 0) { StepDeflate(boss, zip.Bomb, dt, minX, maxX); return true; }
        boss.BombClock = Math.Max(0, boss.BombClock - dt);
        if (boss.BombClock <= 0) Burst(boss, zip.Bomb);
        return true;
    }

    private void StartPress(Slime boss)
    {
        var press = boss.Type.Zip!.Press;
        boss.Retreating = false;
        boss.PendingCalls = null;
        boss.Pins = 0;
        boss.PressLeft = press.Rolls;
        boss.Cooldown = 1;
        events.Add(new(BattleEventKind.ZipPressed, boss.X, boss.Id));
    }

    // The next slide, at the cat whatever it is doing, its stop fixed now; off an edge too near.
    private void PressMove(Slime boss, ZipPressSpec press, double dx, double minX, double maxX)
    {
        if (boss.Cooldown > 0 || boss.PressLeft <= 0 || boss.PressWindow > 0) return;
        int way = dx < 0 ? -1 : 1;
        double room = way < 0 ? boss.X - minX : maxX - boss.X;
        if (room < press.Roll.Distance * 0.5) way = -way;
        room = way < 0 ? boss.X - minX : maxX - boss.X;
        boss.Facing = boss.DashDirection = way;
        boss.RollDistance = Math.Min(press.Roll.Distance, room);
        boss.RollGoal = boss.X + way * boss.RollDistance;
        boss.Set(SlimeState.Windup);
        boss.Rolling = boss.PressRolling = true;
        lastWindup = Time;
        events.Add(new(BattleEventKind.RollAimed, boss.X, boss.Id));
    }

    // The cat let go after a lift landed at x: a riddle's roll under way or just stopped there is pinned.
    private void CatPressed(double x)
    {
        foreach (var s in slimes.ToArray())
        {
            if (s.Type.Zip is not ZipSpec zip || !s.Targetable || PhaseOf(s, s.ShieldTag) is not { Press: true } || !s.Shielded) continue;
            if (s.PressWindow <= 0) continue; // Only once it has stopped on its gold spot.
            if (Math.Abs(x - s.X) > s.HitWidth / 2 + zip.Press.Reach) continue;
            s.Pins++;
            ZipPins++;
            s.PressRolling = false;
            s.PressWindow = 0;
            s.KnockVelocity = 0;
            AddEffect(BattleEffectKind.Thud, s.X, 0);
            events.Add(new(BattleEventKind.ZipPinned, s.X, s.Pins));
            if (s.Pins >= zip.Press.Pins) { EndPress(s, zip.Press, true); continue; }
            s.StunRemaining = zip.Press.PinStun;
            s.Cooldown = 0.3;
            s.Set(SlimeState.Stunned);
        }
    }

    private void EndPress(Slime boss, ZipPressSpec press, bool broken)
    {
        boss.Shielded = false;
        boss.ShieldTag = 0;
        boss.PressLeft = 0;
        boss.PressRolling = false;
        boss.PressWindow = 0;
        events.Add(new(BattleEventKind.ZipUnlocked, boss.X, broken ? 1 : 0));
        ZipGrow(boss);
        if (!broken) return;
        boss.KnockVelocity = 0;
        boss.StunRemaining = press.Stun;
        boss.Set(SlimeState.Stunned);
        Open(boss, press.Bonus, press.Stun, press.Opening);
    }

    // A solved riddle's opening: more damage for a while, but at most `share` of its whole HP, so that a strong
    // build cannot skip the stage it opens onto (2026-11-06).
    private static void Open(Slime boss, double bonus, double seconds, double share)
    {
        boss.MechBonus = bonus;
        boss.MechBonusClock = seconds;
        boss.ZipOpenFloor = Math.Max(1, boss.Hp - boss.MaxHp * share);
    }

    // The bomb armed (at its phase, again after a burst): the shield up, the clock on, the tab out.
    private void ArmBomb(Slime boss)
    {
        var bomb = boss.Type.Zip!.Bomb;
        boss.Retreating = false;
        boss.PendingCalls = null;
        boss.Shielded = true;
        boss.ShieldTag = boss.BombTag;
        boss.BombPulls = 0;
        boss.BombClock = bomb.Charge;
        if (boss.State is SlimeState.Windup or SlimeState.Dashing or SlimeState.Casting) boss.Set(SlimeState.Moving);
        MoveTab(boss);
        events.Add(new(BattleEventKind.ZipBombArmed, boss.X, boss.Id));
    }

    // The tab swings out to a new spot: the other side from the last, at another height.
    private void MoveTab(Slime boss)
    {
        zipRandom ??= BattleRandom.Stream(Seed, BattleRandom.Zip);
        var (minX, maxX) = Bounds(boss);
        int side = boss.TabX == 0 ? (zipRandom.Next(2) == 0 ? -1 : 1) : boss.TabX > boss.X ? -1 : 1;
        double out_ = boss.Width / 2 + 44 + zipRandom.NextDouble() * 50;
        if (boss.X + side * out_ < Left + 30 || boss.X + side * out_ > Right - 30) side = -side;
        boss.TabX = Math.Clamp(boss.X + side * out_, Left + 30, Right - 30);
        boss.TabHeight = 34 + zipRandom.NextDouble() * 56;
    }

    private void PullTab(Slime boss)
    {
        var bomb = boss.Type.Zip!.Bomb;
        if (boss.TabLock > 0 || boss.BombClock <= 0) return;
        boss.BombPulls++;
        boss.TabLock = TabLockSeconds;
        AddEffect(BattleEffectKind.Spark, boss.TabX, boss.TabHeight);
        events.Add(new(BattleEventKind.ZipTabPulled, boss.TabX, boss.BombPulls));
        if (boss.BombPulls < bomb.Pulls) { MoveTab(boss); return; }
        boss.BombClock = 0;
        boss.BombRearm = 0;
        boss.BombDone = true;
        boss.Deflating = bomb.Flight;
        boss.DeflateVelocity = (boss.X - Left > Right - boss.X ? -1 : 1) * DeflateSpeed;
        events.Add(new(BattleEventKind.ZipDeflated, boss.X));
    }

    // The cat's way since the last step against an armed tab, as against a rope.
    private void CatThroughTab(CatPose from, CatPose cat, double speed)
    {
        foreach (var s in slimes)
        {
            if (s.BombClock <= 0 || s.Type.Zip is not ZipSpec zip || speed < s.Type.RopeCutSpeed) continue;
            if (Distance(s.TabX, s.TabHeight, from.X, from.Height, cat.X, cat.Height) <= zip.Bomb.TabRadius) { PullTab(s); return; }
        }
    }

    // A Scissors cut across [x0, x1] at height y.
    private void SnipTab(double x0, double y, double x1, double band)
    {
        foreach (var s in slimes)
            if (s.BombClock > 0 && s.TabX >= x0 - band && s.TabX <= x1 + band && Math.Abs(s.TabHeight - y) <= band) { PullTab(s); return; }
    }

    // From a point to a segment.
    private static double Distance(double px, double py, double x0, double y0, double x1, double y1)
    {
        double dx = x1 - x0, dy = y1 - y0, length = dx * dx + dy * dy;
        double t = length > 0 ? Math.Clamp(((px - x0) * dx + (py - y0) * dy) / length, 0, 1) : 0;
        double ax = x0 + t * dx - px, ay = y0 + t * dy - py;
        return Math.Sqrt(ax * ax + ay * ay);
    }

    // Its air going out: it shoots along the field off both edges, wobbling, then lies flat and dazed.
    private void StepDeflate(Slime boss, ZipBombSpec bomb, double dt, double minX, double maxX)
    {
        boss.Deflating = Math.Max(0, boss.Deflating - dt);
        boss.X += boss.DeflateVelocity * dt;
        if (boss.X <= minX || boss.X >= maxX) { boss.X = Math.Clamp(boss.X, minX, maxX); boss.DeflateVelocity = -boss.DeflateVelocity; boss.Facing = boss.DeflateVelocity < 0 ? -1 : 1; }
        boss.LeapHeight = 24 + 18 * Math.Sin(boss.Deflating * 17);
        if (boss.Deflating > 0) return;
        boss.LeapHeight = 0;
        boss.Shielded = false;
        boss.ShieldTag = 0;
        AddEffect(BattleEffectKind.Thud, boss.X, 0);
        events.Add(new(BattleEventKind.BossShieldDown, boss.X));
        boss.KnockVelocity = 0;
        boss.StunRemaining = bomb.Stun;
        boss.Set(SlimeState.Stunned);
        Open(boss, bomb.Bonus, bomb.Stun, bomb.Opening);
    }

    // Time out: the blast over the floor; it drops its shield and arms again after a while.
    private void Burst(Slime boss, ZipBombSpec bomb)
    {
        ZipBursts++;
        boss.Shielded = false;
        boss.ShieldTag = 0;
        boss.BombRearm = bomb.Rearm;
        boss.Cooldown = StageOf(boss)!.Cooldown / 2;
        AddEffect(BattleEffectKind.Slam, boss.X, 0, bomb.Radius);
        bool hit = CatInVolume(boss.X - bomb.Radius, boss.X + bomb.Radius, 0, BurstHeight);
        events.Add(new(BattleEventKind.ZipBurst, boss.X, hit ? 1 : 0));
        if (hit) HurtCat(boss, lastCatX, true, Share(boss, bomb.Damage), bomb.CatStun, crush: true);
    }

    // The boss a helper's rope goes to (null: no rope, or the boss is gone).
    public Slime? RopeBoss(Slime helper) =>
        helper.RopedTo != 0 && helper.Targetable && Find(helper.RopedTo) is { Targetable: true } boss ? boss : null;

    // A rope cut: the helper is dazed, the boss's shield holds while any other rope is left.
    private bool CutBossRope(Slime helper)
    {
        if (RopeBoss(helper) is not Slime boss) return false;
        helper.RopedTo = 0;
        if (helper.Type.Packet) Unpacked(helper); // Its stream cut, a data packet only falls apart.
        else Daze(helper, boss.Type.RopeCutStun);
        AddEffect(BattleEffectKind.Spark, (helper.X + boss.X) / 2, Math.Min(helper.Height, boss.Height) * 0.6);
        BossRopesCut++;
        events.Add(new(BattleEventKind.BossRopeCut, helper.X, helper.Id));
        // Two cuts together stun the Zip Slime longer when its shield breaks (2026-11-05).
        if (boss.Type.Zip is ZipSpec zip)
        {
            if (Time - boss.RopeCutAt <= zip.RopeComboWindow && boss.RopeBonusStun == 0)
            {
                boss.RopeBonusStun = zip.RopeComboStun;
                events.Add(new(BattleEventKind.ZipRopeCombo, boss.X));
            }
            boss.RopeCutAt = Time;
        }
        return true;
    }

    // The cat's way since the last step against every rope to a boss (as for a tether's rope).
    private void CatThroughBossRopes(CatPose from, CatPose cat, double speed)
    {
        // Every frame the cat is held: nothing to copy in a battle without ropes.
        bool any = false;
        foreach (var s in slimes) if (s.RopedTo != 0) { any = true; break; }
        if (!any) return;
        foreach (var helper in slimes.ToArray())
        {
            if (RopeBoss(helper) is not Slime boss || speed < boss.Type.RopeCutSpeed) continue;
            if (!RopeCrossed(helper, boss, from.X, from.Height, cat.X, cat.Height, CatHalfWidth * 2 / 3)) continue;
            if (CutBossRope(helper)) CatRopeCuts++;
            if (Over) return;
        }
    }

    // A Scissors cut: every rope to a boss it crosses (within pad of its way); how many it cut.
    private int SnipBossRopes(double x0, double h0, double x1, double h1, double pad)
    {
        int cut = 0;
        foreach (var helper in slimes.ToArray())
            if (RopeBoss(helper) is Slime boss && RopeCrossed(helper, boss, x0, h0, x1, h1, pad) && CutBossRope(helper)) cut++;
        return cut;
    }

    // A roped phase: its helpers still tied to the boss (one still appearing too).
    public int RopesLeft(Slime boss)
    {
        int n = 0;
        foreach (var s in slimes) if (s.RopedTo == boss.Id && s.State != SlimeState.Defeated) n++;
        return n;
    }
}
