using System;

namespace DesktopPet;

// 1.5 M11, Q6 the bomb slime (EnemyType.Fuse): it rides a quick slime up (a "ride" link) and hops off
// near the cat, walks up and lights its fuse next to a cat on the floor; it stands still while the fuse
// burns, a red ring showing the blast all along. The blast hurts and stuns a cat on the floor in the ring
// (the Shield, the Turtle shell, landing protection and a lifted cat dodge it like any hit) and every
// other enemy in it (never one in the air); the bomb is gone then, not the cat's kill. Defeated before it
// is lit it fizzles; defeated while lit it blows up on the spot; knocked off its mount it lights a short
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
        if (catHeld || Grace > 0 || slime.Riding || slime.Handled || Math.Abs(catX - slime.X) > fuse.Range) return false;
        if (Math.Abs(catX - slime.X) > 1) slime.Facing = catX < slime.X ? -1 : 1;
        Light(slime, slime.Elite != null ? fuse.EliteSeconds : fuse.Seconds);
        return true;
    }

    // Every frame while lit, whatever it is doing. True: it blew up.
    private bool BurnFuse(Slime slime, double dt)
    {
        slime.FuseLeft -= dt;
        if (slime.FuseLeft > 0) return false;
        Blast(slime, true);
        return true;
    }

    // The blast at the bomb's spot. own: by its fuse or a Grabber's throw (the bomb leaves the field, not
    // counted as defeated); otherwise it was just defeated while lit.
    private void Blast(Slime bomb, bool own)
    {
        var fuse = bomb.Type.Fuse!;
        bomb.FuseLeft = 0;
        double x = bomb.X, radius = FuseRadius(bomb);
        if (own && bomb.State != SlimeState.Defeated)
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
        bool hit = !CatLifted && Grace <= 0 && Math.Abs(lastCatX - x) <= radius;
        events.Add(new(BattleEventKind.Blasted, x, hit ? 1 : 0));
        if (hit && bomb.Damage > 0)
        {
            BlastHitsOnCat++;
            HurtCat(bomb, lastCatX, fuse.Stun > 0, 1, fuse.Stun);
            if (Over) return;
        }
        if (fuse.EnemyDamage <= 0) return;
        // One blast hits everything in it at once (a lit bomb in it blows up in turn).
        bool was = blasting;
        blasting = true;
        foreach (var other in slimes.ToArray())
            if (other != bomb && other.Targetable && !other.Airborne && Math.Abs(other.X - x) <= radius + other.HitWidth / 2)
            {
                Damage(other, fuse.EnemyDamage, DamageSource.Bomb);
                if (Over) break;
            }
        blasting = was;
        if (!was) RefreshGuards();
    }
}
