using System;

namespace DesktopPet;

// 1.5 M11, Q6 the bomb slime (EnemyType.Fuse): it rides a quick slime up (a "ride" link; 2026-10-31: the
// quick slime throws it at the cat from link.throwRange, PetBattle.Links.cs, and it blows up where it lands) and hops off
// near the cat, walks up and lights its fuse next to a cat on the floor; it stands still while the fuse
// burns, a red ring showing the blast all along. The blast hurts and stuns a cat on the floor in the ring
// (the Shield, the Turtle shell, landing protection and a lifted cat dodge it like any hit) and every
// other enemy in it (never one in the air); the bomb is gone then, not the cat's kill. Defeated before it
// is lit it fizzles; defeated while lit it fizzles too (2026-10-31, a dud: no blast); knocked off its mount it lights a short
// fuse where it falls; thrown by the Grabber it blows up where it hits or lands. A push moves a lit bomb
// (it blows up where it ends up). Pause freezes the fuse (time runs only in Step).
public sealed partial class BattleSim
{
    public int Blasts { get; private set; }
    public int BlastHitsOnCat { get; private set; }

    public static double FuseRadius(Slime slime) =>
        slime.Type.Fuse is FuseSpec fuse ? fuse.Radius * (slime.Elite != null ? fuse.EliteRadius : 1) : 0;

    private void Light(Slime slime, double seconds)
    {
        if (slime.Type.Fuse == null || slime.Lit || !slime.Targetable) return;
        slime.FuseLeft = slime.FuseTotal = seconds;
        events.Add(new(BattleEventKind.FuseLit, slime.X, slime.Id));
    }

    // On the move: next to the cat on the floor (never while the user has it or under landing protection).
    private bool StartFuse(Slime slime, double catX, bool catHeld)
    {
        var fuse = slime.Type.Fuse!;
        if (catHeld || Grace > 0 && !slime.OnBasket || slime.Riding || slime.Handled || Math.Abs(catX - slime.X) > fuse.Range) return false;
        if (Math.Abs(catX - slime.X) > 1) slime.Facing = catX < slime.X ? -1 : 1;
        Light(slime, slime.Elite != null ? fuse.EliteSeconds : fuse.Seconds);
        return true;
    }

    public int Fizzles { get; private set; }

    // Defeated while lit (2026-10-31): a dud. The fuse goes out in a puff, nothing is hurt.
    private void Fizzle(Slime bomb)
    {
        bomb.FuseLeft = 0;
        Fizzles++;
        AddEffect(BattleEffectKind.Poof, bomb.X, bomb.Height / 2, 1);
        effects[^1].Tint = "#8A8F99";
        events.Add(new(BattleEventKind.BombFizzled, bomb.X, bomb.Id));
    }

    // Every frame while lit, whatever it is doing. True: it blew up.
    private bool BurnFuse(Slime slime, double dt)
    {
        slime.FuseLeft -= dt;
        if (slime.FuseLeft > 0) return false;
        Blast(slime, true);
        return true;
    }

    // ── 2026-11-04 (docs/GOPY-C-2026-11-04.md D5): the herb slime and the bomb slime ──

    public const double RecoilSeconds = 0.6, RecoilHeight = 90, RecoilFallStun = 1;
    public int LeafShields { get; private set; }
    public int BombsCaught { get; private set; }

    // Each herb slime (wherever it is) shields one bomb slime that never had a shield: no damage until the
    // bomb's first blast. Never healed: a bomb slime is not a herb's patient.
    private void PairLeaves()
    {
        foreach (var herb in slimes)
        {
            if (herb.Type.HealAmount <= 0 || !herb.Targetable || herb.Decoy) continue;
            bool busy = false;
            foreach (var s in slimes) if (s.LeafShield && s.LeafBy == herb.Id) { busy = true; break; }
            if (busy) continue;
            foreach (var bomb in slimes)
            {
                if (bomb.Type.Fuse == null || !bomb.Targetable || bomb.LeafUsed) continue;
                bomb.LeafShield = bomb.LeafUsed = true;
                bomb.LeafBy = herb.Id;
                bomb.LeafCastAt = Time;
                LeafShields++;
                events.Add(new(BattleEventKind.LeafShielded, bomb.X, bomb.Id));
                break;
            }
        }
    }

    // A herb slime gone: its leaf shield falls.
    private void LeafFallen(Slime herb)
    {
        foreach (var s in slimes)
            if (s.LeafShield && s.LeafBy == herb.Id) { s.LeafShield = false; events.Add(new(BattleEventKind.LeafLost, s.X, s.Id)); }
    }

    // A cut or the like takes the shield off.
    private void LeafCut(Slime bomb)
    {
        if (!bomb.LeafShield) return;
        bomb.LeafShield = false;
        events.Add(new(BattleEventKind.LeafLost, bomb.X, bomb.Id));
    }

    // The shield took the blast: leaves burst, the bomb, burnt, is thrown back at the quick slime that threw it
    // (none: at its herb slime, or it just lands where it is).
    private void LeafBurst(Slime bomb)
    {
        bomb.LeafShield = false;
        bomb.Charred = true;
        bomb.KnockVelocity = 0;
        double to = Find(bomb.ThrownBy) is { Targetable: true, Handled: false } mount ? mount.X : Find(bomb.LeafBy) is { Targetable: true } herb ? herb.X : bomb.X;
        var (minX, maxX) = Bounds(bomb);
        bomb.RecoilFrom = bomb.X;
        bomb.RecoilTo = Math.Clamp(to, minX, maxX);
        bomb.RecoilLeft = bomb.RecoilTotal = RecoilSeconds;
        AddEffect(BattleEffectKind.Mend, bomb.X, bomb.Height);
        events.Add(new(BattleEventKind.LeafBurst, bomb.X, bomb.Id));
    }

    // Every frame of the flight back: its mount, still free and on its feet, catches it (and soon throws it
    // again); otherwise it lands dazed.
    private void FlyRecoil(Slime bomb, double dt)
    {
        bomb.RecoilLeft = Math.Max(0, bomb.RecoilLeft - dt);
        double k = 1 - bomb.RecoilLeft / bomb.RecoilTotal;
        bomb.X = bomb.RecoilFrom + (bomb.RecoilTo - bomb.RecoilFrom) * k;
        bomb.LeapHeight = 4 * RecoilHeight * k * (1 - k);
        bomb.KnockVelocity = 0;
        if (bomb.RecoilLeft > 0) return;
        bomb.LeapHeight = 0;
        if (Find(bomb.ThrownBy) is { Targetable: true, Handled: false, State: SlimeState.Moving } mount && !Ridden(mount) && bomb.Link is { Kind: EnemyLinks.Ride })
        {
            bomb.LinkedTo = mount.Id;
            bomb.X = mount.X;
            bomb.RideHeight = mount.Height * 0.8;
            mount.ThrowHold = Math.Min(mount.ThrowHold, 0.5);
            BombsCaught++;
            events.Add(new(BattleEventKind.BombCaught, bomb.X, bomb.Id));
            return;
        }
        Daze(bomb, RecoilFallStun);
    }

    // The mount standing by for its bomb to come back (the view's "!").
    public bool AwaitingBomb(Slime mount)
    {
        foreach (var s in slimes)
            if (s.ThrownBy == mount.Id && s.Targetable && (s.RecoilLeft > 0 || s.BombFlight > 0 && s.LeafShield)) return true;
        return false;
    }

    // The blast at the bomb's spot. own: by its fuse or a Grabber's throw (the bomb leaves the field, not
    // counted as defeated); otherwise it was just defeated while lit.
    private void Blast(Slime bomb, bool own)
    {
        var fuse = bomb.Type.Fuse!;
        // A leaf shield takes the blast for it (2026-11-04): the bomb lives, hurts no friend, flies back.
        bool leaf = own && bomb.LeafShield && !bomb.ByUser;
        bomb.FuseLeft = 0;
        double x = bomb.X, radius = FuseRadius(bomb);
        if (own && bomb.State != SlimeState.Defeated && !leaf)
        {
            bomb.Set(SlimeState.Defeated);
            bomb.Exploded = true;
            bomb.KnockVelocity = 0;
            ClearStatus(bomb);
            if (SelectedId == bomb.Id) SelectedId = null;
            LinkFallen(bomb, false);
        }
        Blasts++;
        AddEffect(BattleEffectKind.Explosion, x, 0, radius);
        bool hit = !bomb.ByUser && !CatLifted && Grace <= 0 && Math.Abs(lastCatX - x) <= radius;
        events.Add(new(BattleEventKind.Blasted, x, hit ? 1 : 0));
        if (hit && bomb.Damage > 0)
        {
            BlastHitsOnCat++;
            HurtCat(bomb, lastCatX, fuse.Stun > 0, 1, fuse.Stun);
            if (Over) return;
        }
        // 1.5 M20 defence: the fish basket in the ring is hit too (never by a bomb the user threw).
        if (!bomb.ByUser && bomb.Damage > 0 && double.IsFinite(BasketX) && Math.Abs(BasketX - x) <= radius + BasketHalfWidth)
        {
            HurtBasket(bomb);
            if (Over) return;
        }
        if (leaf) { LeafBurst(bomb); return; }
        if (fuse.EnemyDamage <= 0) return;
        // One blast hits everything in it at once (a lit bomb in it blows up in turn).
        bool was = blasting;
        blasting = true;
        foreach (var other in slimes.ToArray())
            if (other != bomb && other.Targetable && !other.Airborne && !other.Burrowed && Math.Abs(other.X - x) <= radius + other.HitWidth / 2)
            {
                Damage(other, fuse.EnemyDamage, DamageSource.Bomb);
                if (Over) break;
            }
        blasting = was;
        if (!was) RefreshGuards();
    }
}
