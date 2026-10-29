using System;
using System.Collections.Generic;

namespace DesktopPet;

// BattleSim: links between enemies (1.5 "Slime phản công"; LinkSpec in PetBattleData.cs). Each link is
// seen (a rope, a ring, a bar) and can be broken, and breaking it is rewarded. A battle lets in at most
// EnemyLinks.MaxKinds kinds of link, first come; a later kind's enemies fight without theirs. No random
// stream is used here, so battles without links play exactly as before.
public sealed partial class BattleSim
{
    private readonly List<string> linkKinds = new(EnemyLinks.MaxKinds);
    private readonly List<Slime> linkScratch = new();
    // The kinds of link let into this battle so far.
    public IReadOnlyList<string> LinkKinds => linkKinds;
    private bool HasLink(string kind) => linkKinds.Contains(kind);

    // At spawn: its kind's link, if this battle still has room for that kind.
    private void JoinLinks(Slime slime)
    {
        if (slime.Type.ActiveLink is not LinkSpec link) return;
        if (!linkKinds.Contains(link.Kind))
        {
            if (linkKinds.Count >= EnemyLinks.MaxKinds) return;
            linkKinds.Add(link.Kind);
        }
        slime.Link = link;
    }

    private static bool Is(Slime slime, string kind) => slime.Link?.Kind == kind;

    // A rhythm's count up to its strike, a tether's share of its ally's hits: an elite's own when it has one (1.5 M10).
    public static int Beats(Slime slime) => slime.Link is not LinkSpec link ? 0 : slime.Elite != null && link.EliteBeats > 0 ? link.EliteBeats : link.Beats;
    // An aura's speed-up: an elite's own when it has one (1.5 M12).
    public static double AuraBoost(Slime slime) => slime.Link is not LinkSpec link ? 1 : slime.Elite != null && link.EliteSpeed > 0 ? link.EliteSpeed : link.Speed;
    public static double Share(Slime holder) => holder.Link is not LinkSpec link ? 0 : holder.Elite != null && link.EliteShare > 0 ? link.EliteShare : link.Share;

    // The holder of the tether on this ally (null: none); `after`: the next holder after that one
    // (an ally takes up to EnemyLinks.MaxTethers ropes, 1.5 M12).
    public Slime? TetherHolder(Slime ally, Slime? after = null)
    {
        if (!HasLink(EnemyLinks.Tether)) return null;
        bool past = after == null;
        foreach (var s in slimes)
        {
            if (s.LinkedTo != ally.Id || !Is(s, EnemyLinks.Tether) || !s.Targetable) continue;
            if (past) return s;
            past = s == after;
        }
        return null;
    }

    private int Tethers(Slime ally)
    {
        int n = 0;
        for (var h = TetherHolder(ally); h != null; h = TetherHolder(ally, h)) n++;
        return n;
    }

    // The ally a tether holder is tied to, or the mount a rider is on (null: none).
    public Slime? LinkPartner(Slime slime) => slime.LinkedTo != 0 && Find(slime.LinkedTo) is { Targetable: true } partner ? partner : null;

    private bool Ridden(Slime mount)
    {
        foreach (var s in slimes) if (s.LinkedTo == mount.Id && Is(s, EnemyLinks.Ride)) return true;
        return false;
    }

    // A few times a second (the guards' decision tick): free holders tie themselves, free riders mount.
    private void FormLinks(double catX)
    {
        if (linkKinds.Count == 0) return;
        foreach (var slime in slimes)
        {
            if (slime.Link is not LinkSpec link || slime.LinkedTo != 0 || slime.State != SlimeState.Moving || slime.Handled || Time < slime.RelinkAt) continue;
            Slime? best = null;
            if (link.Kind == EnemyLinks.Tether)
            {
                // The nearest ally without a rope; none: the nearest with room for one more (1.5 M12).
                int bestRopes = 0;
                foreach (var s in slimes)
                {
                    if (s == slime || !s.Targetable || s.IsBoss || s.Handled || s.Decoy || s.Burrowed || s.Type.Fly != null || s.Type.Cling != null || Is(s, EnemyLinks.Tether) || s.Riding || Math.Abs(s.X - slime.X) > link.Range) continue;
                    int ropes = Tethers(s);
                    if (ropes >= EnemyLinks.MaxTethers) continue;
                    if (best == null || ropes < bestRopes || ropes == bestRopes && Math.Abs(s.X - slime.X) < Math.Abs(best.X - slime.X)) { best = s; bestRopes = ropes; }
                }
            }
            else if (link.Kind == EnemyLinks.Ride)
            {
                // Never mounting right next to the cat (it would hop off at once).
                if (Math.Abs(slime.X - catX) <= link.DropRange * 1.5) continue;
                foreach (var s in slimes)
                    if (s != slime && s.Targetable && !s.Handled && !s.Decoy && s.Type.Id == link.Mount && !s.Riding && !Ridden(s) &&
                        Math.Abs(s.X - slime.X) <= link.Range && (best == null || Math.Abs(s.X - slime.X) < Math.Abs(best.X - slime.X))) best = s;
            }
            if (best == null) continue;
            slime.LinkedTo = best.Id;
            events.Add(new(BattleEventKind.LinkFormed, slime.X, slime.Id));
        }
    }

    // Every frame, before the enemies move: auras, riders, beats, merges and ropes stretched too far.
    private void StepLinks(double dt, double catX, bool catHeld)
    {
        bool aura = HasLink(EnemyLinks.Aura);
        if (aura) foreach (var s in slimes) s.AuraSpeed = 1;
        for (int i = 0; i < slimes.Count; i++)
        {
            var slime = slimes[i];
            if (slime.Link is not LinkSpec link || !slime.Targetable) continue;
            switch (link.Kind)
            {
                case EnemyLinks.Aura:
                    double speed = AuraBoost(slime);
                    foreach (var s in slimes)
                        if (s != slime && s.Targetable && Math.Abs(s.X - slime.X) <= link.Range) s.AuraSpeed = Math.Max(s.AuraSpeed, speed);
                    break;
                case EnemyLinks.Tether:
                    if (slime.LinkedTo == 0) break;
                    if (Find(slime.LinkedTo) is not { Targetable: true } ally) { Unlink(slime); break; }
                    if (Math.Abs(ally.X - slime.X) > link.Range * 1.5)
                    {
                        Unlink(slime);
                        events.Add(new(BattleEventKind.LinkSnapped, slime.X, slime.Id));
                    }
                    break;
                case EnemyLinks.Ride:
                    if (slime.LinkedTo == 0) break;
                    if (Find(slime.LinkedTo) is not { Targetable: true } mount) { Fall(slime, link.BreakStun); break; }
                    slime.X = mount.X;
                    slime.Facing = mount.Facing;
                    slime.KnockVelocity = 0;
                    slime.RideHeight = mount.Height * 0.8;
                    if (Math.Abs(mount.X - catX) <= link.DropRange) Fall(slime, 0);
                    break;
                case EnemyLinks.Rhythm:
                    // Stunned (a push, a daze): the count starts again.
                    if (slime.State == SlimeState.Stunned) { slime.BeatCount = 0; slime.BeatClock = 0; break; }
                    slime.BeatClock += dt;
                    if (slime.BeatClock < link.Beat) break;
                    slime.BeatClock -= link.Beat;
                    if (++slime.BeatCount < Beats(slime)) { events.Add(new(BattleEventKind.LinkBeat, slime.X, slime.Id)); break; }
                    slime.BeatCount = 0;
                    StrikeTogether(slime, link, catX, catHeld);
                    break;
                case EnemyLinks.Merge:
                    if (slime.State == SlimeState.Stunned || slime.Riding || Huddle(slime, link) < link.Count) { slime.MergeClock = 0; break; }
                    slime.MergeClock += dt;
                    if (slime.MergeClock >= link.MergeSeconds && MergeAround(slime, link)) i = -1; // The list changed: from the start.
                    break;
            }
        }
    }

    // After the enemies moved: riders stay on their mounts this very frame (never a frame behind).
    private void CarryRiders()
    {
        if (!HasLink(EnemyLinks.Ride)) return;
        foreach (var s in slimes)
            if (s.Riding && Find(s.LinkedTo) is { } mount) { s.X = mount.X; s.Facing = mount.Facing; }
    }

    // A rhythm's strike: every ally within its range (and itself) that could hit the cat now winds up,
    // attack turns or not (never a support, a spring trap, a boss, a rider or one in a panic).
    private void StrikeTogether(Slime holder, LinkSpec link, double catX, bool catHeld)
    {
        int strikers = 0;
        if (!catHeld && Grace <= 0)
            foreach (var s in slimes)
            {
                if (!s.Targetable || s.State != SlimeState.Moving || s.IsBoss || s.Type.Support || s.Type.Spring != null || s.Type.Fuse != null || s.Riding || s.PanicRemaining > 0 || s.Shielded || s.Faded || s.Airborne || s.Burrowed || s.Type.Fly != null) continue;
                if (Math.Abs(s.X - holder.X) > link.Range) continue;
                double dx = catX - s.X, reach = s.Type.Dash ? s.Type.DashRange : s.Range;
                if (Math.Abs(dx) > reach || WallBetween(s.X, catX) != null) continue;
                s.Facing = s.DashDirection = dx < 0 ? -1 : 1;
                s.Set(SlimeState.Windup);
                strikers++;
            }
        if (strikers > 0) lastWindup = Time;
        events.Add(new(BattleEventKind.LinkStrike, holder.X, strikers));
    }

    // One of a merging slime's kind ready to merge with it (healed since it came, for AfterHeal).
    private static bool Merging(Slime s, Slime slime, LinkSpec link) =>
        s.Type == slime.Type && s.Link != null && s.Targetable && s.State != SlimeState.Stunned && !s.Riding && !s.Handled
        && (!link.AfterHeal || s.Healed) && Math.Abs(s.X - slime.X) <= link.Range;

    // How many of a merging slime's kind (itself included) stand within its range, ready to merge.
    private int Huddle(Slime slime, LinkSpec link)
    {
        int n = 0;
        foreach (var s in slimes)
            if (Merging(s, slime, link)) n++;
        return n;
    }

    // The merge: this one and its kind's nearest Count - 1 become one Into enemy, their HP added up.
    private bool MergeAround(Slime slime, LinkSpec link)
    {
        var group = linkScratch;
        group.Clear();
        foreach (var s in slimes)
            if (Merging(s, slime, link)) group.Add(s);
        if (group.Count < link.Count) return false;
        group.Sort((a, b) => Math.Abs(a.X - slime.X).CompareTo(Math.Abs(b.X - slime.X)));
        group.RemoveRange(link.Count, group.Count - link.Count);
        double x = 0, hp = 0, maxHp = 0;
        foreach (var s in group) { x += s.X; hp += s.Hp; maxHp += s.MaxHp; }
        x /= group.Count;
        foreach (var s in group) Remove(s);
        var merged = AddSlime(x, Settings.Enemy(link.Into!));
        merged.Hp = hp; merged.MaxHp = maxHp;
        merged.Facing = lastCatX < x ? -1 : 1;
        merged.Set(SlimeState.Moving);
        AddEffect(BattleEffectKind.Poof, x, merged.Height / 2, 1);
        effects[^1].Tint = slime.Type.Color;
        events.Add(new(BattleEventKind.Merged, x, merged.Id));
        group.Clear();
        return true;
    }

    // A slime leaving the field without being defeated (merged): nothing may point at it any more.
    private void Remove(Slime slime)
    {
        LinkFallen(slime, false);
        slimes.Remove(slime);
        if (SelectedId == slime.Id) SelectedId = null;
        if (autoTargetId == slime.Id) autoTargetId = 0;
        if (swingTargetId == slime.Id) swingTargetId = 0;
    }

    private void Unlink(Slime slime)
    {
        slime.LinkedTo = 0;
        slime.RideHeight = 0;
        slime.RelinkAt = Time + (slime.Link?.Relink ?? 0);
    }

    // A rider off its mount: hopping off near the cat (no stun) or falling (dazed `stun` s; a bomb's
    // short fuse lights as it falls, 1.5 M11).
    private void Fall(Slime rider, double stun)
    {
        Unlink(rider);
        events.Add(new(BattleEventKind.Dismounted, rider.X, rider.Id));
        if (stun > 0 && rider.Type.Fuse is FuseSpec fuse) Light(rider, fuse.DropFuse);
        if (stun > 0) Daze(rider, stun);
    }

    // A slime defeated (rewarded) or removed: the links it was part of end. A tether holder's ally and
    // a fallen mount's rider are dazed; an aura holder's allies panic.
    private void LinkFallen(Slime slime, bool rewarded)
    {
        if (linkKinds.Count == 0) return;
        if (slime.Link is LinkSpec link)
        {
            if (link.Kind == EnemyLinks.Tether && LinkPartner(slime) is Slime ally)
            {
                Unlink(slime);
                events.Add(new(rewarded ? BattleEventKind.LinkCut : BattleEventKind.LinkSnapped, slime.X, slime.Id));
                if (rewarded) Daze(ally, link.BreakStun);
            }
            if (link.Kind == EnemyLinks.Aura && rewarded && link.BreakStun > 0)
            {
                bool any = false;
                foreach (var s in slimes)
                    if (s != slime && s.Targetable && !s.IsBoss) { s.PanicRemaining = link.BreakStun; any = true; }
                if (any) events.Add(new(BattleEventKind.Panicked, slime.X, slime.Id));
            }
            slime.LinkedTo = 0;
            slime.RideHeight = 0;
        }
        foreach (var s in slimes)
        {
            if (s.LinkedTo != slime.Id || s == slime) continue;
            if (Is(s, EnemyLinks.Ride)) Fall(s, rewarded ? s.Link!.BreakStun : 0);
            else Unlink(s); // A tether's ally gone: the holder looks for another later.
        }
    }

    // The user breaks a link (the cat thrown through a rope, the Scissors; 1.5 M03/M06): a tether by
    // its holder or its ally, a rider off its mount (by either). The partner is dazed BreakStun s.
    // False: no link to cut there.
    public bool CutLink(int id)
    {
        if (Over || Paused || Find(id) is not { Targetable: true } slime) return false;
        Slime? holder = slime.LinkedTo != 0 && slime.Link?.Kind is EnemyLinks.Tether or EnemyLinks.Ride ? slime : null;
        if (holder == null)
            foreach (var s in slimes)
                if (s.LinkedTo == slime.Id && s.Link?.Kind is EnemyLinks.Tether or EnemyLinks.Ride) { holder = s; break; }
        if (holder?.Link is not LinkSpec link || LinkPartner(holder) is not Slime partner) return false;
        if (link.Kind == EnemyLinks.Ride) { Fall(holder, link.BreakStun); return true; }
        Unlink(holder);
        Daze(partner, link.BreakStun);
        AddEffect(BattleEffectKind.Spark, (holder.X + partner.X) / 2, Math.Min(holder.Height, partner.Height) * 0.6);
        events.Add(new(BattleEventKind.LinkCut, holder.X, holder.Id));
        return true;
    }

    // Panic (an aura holder fell): it runs straight away from the cat and does not attack.
    private bool Panic(Slime slime, double dt, double catX, double minX, double maxX)
    {
        if (slime.PanicRemaining <= 0) return false;
        int away = slime.X >= catX ? 1 : -1;
        slime.X = Math.Clamp(slime.X + away * Speed(slime) * dt, minX, maxX);
        slime.Facing = away;
        return true;
    }

    // Where a tether holder keeps: just behind its ally, on the side away from the cat (null: no rope).
    private double? TetherPost(Slime holder)
    {
        if (!Is(holder, EnemyLinks.Tether) || LinkPartner(holder) is not Slime ally) return null;
        int side = ally.X >= decisionCatX ? 1 : -1;
        // A second rope's holder keeps behind the first.
        double behind = TetherHolder(ally) is Slime first && first != holder ? first.Width + 6 : 0;
        return ally.X + side * (ally.Width / 2 + holder.Width / 2 + 6 + behind);
    }
}
