using System;
using System.Collections.Generic;

namespace DesktopPet;

// 1.5 M09, Q1 the spitting slime (EnemyType.Spit): from afar it winds up a spit at the cat's spot (an
// orange ring marks it from the start), then a drop of goo flies there in an arc. Landing on a cat on
// the floor (not lifted, not under landing protection) it hurts like the spitter's hit, without a stun;
// the Bubble shield takes it. Where it lands a puddle stays a while: a dash through it goes further and
// faster (the dasher's "link" with the spitter, not a link kind: it never counts against the two kinds).
// The fan dries puddles. A push or a stun during the windup cancels the spit.
public sealed partial class BattleSim
{
    private readonly List<BattleGoo> goos = new();
    private readonly List<BattlePuddle> puddles = new();
    public IReadOnlyList<BattleGoo> Goos => goos;
    public IReadOnlyList<BattlePuddle> Puddles => puddles;
    // Puddles on the floor at once: a new one past this dries the oldest.
    public const int MaxPuddles = 6;
    public int SpitHits { get; private set; }
    public int PuddleBoosts { get; private set; }
    public int PuddlesDried { get; private set; }

    // Where a spitter winding up will land its drops (the first on the cat's spot then; an elite's
    // others further on, away from it), kept on the floor.
    public IEnumerable<double> SpitTargets(Slime slime)
    {
        if (!slime.Spitting || slime.Type.Spit is not SpitSpec spit) yield break;
        int drops = slime.Elite != null ? spit.EliteDrops : 1, away = slime.SpitAt >= slime.X ? 1 : -1;
        for (int i = 0; i < drops; i++) yield return Math.Clamp(slime.SpitAt + away * spit.EliteSpread * i, Left, Right);
    }

    // On the move: a spit at the cat when it is in range on the floor (never while the user has it,
    // under landing protection, or with the spit still recovering).
    private bool StartSpit(Slime slime, double catX, bool catHeld)
    {
        if (slime.Type.Spit is not SpitSpec spit || slime.SpitCooldown > 0 || catHeld || Grace > 0 || slime.Airborne || slime.Handled) return false;
        double distance = Math.Abs(catX - slime.X);
        if (distance < spit.MinRange || distance > spit.Range) return false;
        slime.Facing = catX < slime.X ? -1 : 1;
        slime.Set(SlimeState.Windup);
        slime.Spitting = true;
        slime.SpitAt = catX;
        events.Add(new(BattleEventKind.SpitAimed, catX, slime.Id));
        return true;
    }

    // The windup is over: its drops fly.
    private void Spit(Slime slime)
    {
        var spit = slime.Type.Spit!;
        var targets = new List<double>(SpitTargets(slime));
        slime.Set(SlimeState.Recover);
        slime.SpitCooldown = spit.Cooldown;
        double puddle = spit.PuddleSeconds * (slime.Elite != null ? spit.ElitePuddle : 1);
        foreach (double x in targets)
            goos.Add(new BattleGoo
            {
                OwnerId = slime.Id, FromX = slime.X, X = x, Flight = spit.Flight, Remaining = spit.Flight, Damage = slime.Damage, Radius = spit.Radius,
                PuddleSeconds = puddle, PuddleRadius = spit.PuddleRadius, DashDistance = spit.DashDistance, DashSpeed = spit.DashSpeed, Tint = slime.Type.Color
            });
    }

    // Before the slimes move: drops in flight land, puddles dry up.
    private void StepSpit(double dt)
    {
        for (int i = puddles.Count - 1; i >= 0; i--)
        {
            puddles[i].Remaining -= dt;
            if (puddles[i].Remaining <= 0) puddles.RemoveAt(i);
        }
        for (int i = 0; i < goos.Count; i++)
        {
            var goo = goos[i];
            goo.Remaining -= dt;
            if (goo.Remaining > 0) continue;
            goos.RemoveAt(i--);
            Land(goo);
            if (Over) return;
        }
    }

    private void Land(BattleGoo goo)
    {
        // A wall climber's spike (1.5 M15) sparks on the floor instead.
        AddEffect(goo.Spike ? BattleEffectKind.Spark : BattleEffectKind.Splat, goo.X, 4);
        effects[^1].Tint = goo.Tint;
        bool hit = !CatLifted && Grace <= 0 && Math.Abs(lastCatX - goo.X) <= goo.Radius;
        if (goo.PuddleSeconds > 0) AddPuddle(goo);
        events.Add(new(BattleEventKind.GooLanded, goo.X, hit ? 1 : 0));
        if (!hit) return;
        if (goo.Spike) SpikeHits++; else SpitHits++;
        // Its spitter still there: a hit like its own (Thorns sends some back); gone, the goo still lands.
        if (Find(goo.OwnerId) is { Targetable: true, Damage: > 0 } owner) HurtCat(owner, lastCatX, false, goo.Damage / owner.Damage);
        else LandHit(goo.Damage, lastCatX, out _);
    }

    // A drop landing on a puddle freshens it rather than adding another on the same spot.
    private void AddPuddle(BattleGoo goo)
    {
        foreach (var p in puddles)
            if (Math.Abs(p.X - goo.X) < p.Radius / 2)
            {
                p.Remaining = p.Life = Math.Max(p.Remaining, goo.PuddleSeconds);
                return;
            }
        if (puddles.Count >= MaxPuddles) puddles.RemoveAt(0);
        puddles.Add(new BattlePuddle { X = goo.X, Radius = goo.PuddleRadius, Remaining = goo.PuddleSeconds, Life = goo.PuddleSeconds, DashDistance = goo.DashDistance, DashSpeed = goo.DashSpeed });
    }

    public BattlePuddle? PuddleAt(double x)
    {
        foreach (var p in puddles) if (Math.Abs(p.X - x) <= p.Radius) return p;
        return null;
    }

    // A dasher in a puddle: the rest of its dash goes DashDistance × as far in all and DashSpeed × faster.
    private void BoostDash(Slime slime, BattlePuddle puddle)
    {
        slime.DashBoost = puddle.DashSpeed;
        slime.DashLeft += slime.Type.DashDistance * (puddle.DashDistance - 1);
        PuddleBoosts++;
        events.Add(new(BattleEventKind.PuddleBoost, slime.X, slime.Id));
    }

    // The fan's wind dries every puddle it reaches.
    private void DryPuddles(double from)
    {
        for (int i = puddles.Count - 1; i >= 0; i--)
        {
            if (Math.Abs(puddles[i].X - from) > Settings.FanRange + puddles[i].Radius) continue;
            PuddlesDried++;
            events.Add(new(BattleEventKind.PuddleDried, puddles[i].X));
            puddles.RemoveAt(i);
        }
    }

    private void ClearSpit() { goos.Clear(); puddles.Clear(); }

    private void MapSpit(double left, double oldWidth, double newWidth)
    {
        foreach (var g in goos) { g.X = left + (g.X - Left) / oldWidth * newWidth; g.FromX = left + (g.FromX - Left) / oldWidth * newWidth; }
        foreach (var p in puddles) p.X = left + (p.X - Left) / oldWidth * newWidth;
        foreach (var s in slimes) if (s.Spitting) s.SpitAt = left + (s.SpitAt - Left) / oldWidth * newWidth;
    }
}
