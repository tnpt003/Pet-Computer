using System;

namespace DesktopPet;

// 1.5 M09, Q2 the spring slime (EnemyType.Spring): a trap lying flat on the floor with a gold ring that
// always shows. The cat on the floor stepping in sets it off: it squeezes (the ring turns red), then
// springs; the cat still in the ring and on the floor is hurt and stunned (the Shield, the Turtle shell,
// rage and stun immunity work as for any hit). Then it rests. Lifting the cat out while it squeezes
// dodges it; a lifted cat never sets it off. It never attacks otherwise. Hits from the cat's weapon or
// a bomb flatten it for a while; a push or a stun while it squeezes spends the spring (it rests).
// With the cat far away it slides up slowly. The cat's physics are never touched.
public sealed partial class BattleSim
{
    public int SpringHitsOnCat { get; private set; }
    public int SpringsFlattened { get; private set; }

    public static double SpringRadius(Slime slime) =>
        slime.Type.Spring is SpringSpec spring ? spring.Radius * (slime.Elite != null ? spring.EliteRadius : 1) : 0;

    private static int FlattenHits(Slime slime) =>
        slime.Type.Spring is SpringSpec spring ? slime.Elite != null ? spring.EliteHits : spring.FlattenHits : 0;

    // Every frame, whatever the trap is doing (before its state's own step).
    private void StepSpring(Slime slime, double dt, double catX, bool catHeld)
    {
        var spring = slime.Type.Spring!;
        bool free = slime.State == SlimeState.Moving && !slime.Handled;
        switch (slime.Spring)
        {
            case SpringState.Flat:
            case SpringState.Resting:
                slime.SpringClock -= dt;
                if (slime.SpringClock <= 0) { slime.Spring = SpringState.Ready; slime.SpringHits = 0; }
                break;
            case SpringState.Squeezing:
                // Pushed, stunned, grabbed or tossed: the squeeze is spent.
                if (!free) { Rest(slime, spring); break; }
                slime.SpringClock -= dt * slime.ChillFactor;
                if (slime.SpringClock <= 0) Bounce(slime, spring);
                break;
            case SpringState.Ready:
                if (free && !catHeld && Grace <= 0 && Math.Abs(catX - slime.X) <= SpringRadius(slime))
                {
                    slime.Spring = SpringState.Squeezing;
                    slime.SpringClock = spring.Compress;
                    events.Add(new(BattleEventKind.SpringSqueezed, slime.X, slime.Id));
                }
                break;
        }
    }

    private static void Rest(Slime slime, SpringSpec spring)
    {
        slime.Spring = SpringState.Resting;
        slime.SpringClock = slime.Elite != null ? spring.EliteRest : spring.Rest;
    }

    private void Bounce(Slime slime, SpringSpec spring)
    {
        Rest(slime, spring);
        double radius = SpringRadius(slime);
        AddEffect(BattleEffectKind.Slam, slime.X, 0, radius);
        bool hit = !CatLifted && Grace <= 0 && Math.Abs(lastCatX - slime.X) <= radius;
        events.Add(new(BattleEventKind.SpringBounced, slime.X, hit ? 1 : 0));
        if (!hit) return;
        SpringHitsOnCat++;
        HurtCat(slime, lastCatX, true, 1, spring.Stun, crush: true);
    }

    // A hit from the cat's weapon or a bomb (PetBattle.cs Damage): enough of them flatten it.
    private void SpringStruck(Slime slime, DamageSource source)
    {
        if (slime.Type.Spring is not SpringSpec spring || slime.Spring == SpringState.Flat) return;
        if (source is not (DamageSource.Weapon or DamageSource.Combo or DamageSource.Splash or DamageSource.Echo or DamageSource.Bomb)) return;
        if (++slime.SpringHits < FlattenHits(slime)) return;
        slime.Spring = SpringState.Flat;
        slime.SpringClock = spring.FlatSeconds;
        slime.SpringHits = 0;
        SpringsFlattened++;
        events.Add(new(BattleEventKind.SpringFlattened, slime.X, slime.Id));
    }

    // With the cat further than CreepRange it slides towards it (never while squeezing or flat).
    private void Creep(Slime slime, double dt, double catX, double minX, double maxX)
    {
        var spring = slime.Type.Spring!;
        double gap = catX - slime.X;
        if (Math.Abs(gap) > 1) slime.Facing = gap < 0 ? -1 : 1;
        if (spring.CreepRange <= 0 || slime.Spring is SpringState.Squeezing or SpringState.Flat || Math.Abs(gap) <= spring.CreepRange) return;
        slime.X = Math.Clamp(slime.X + Math.Sign(gap) * Math.Min(Math.Abs(gap) - spring.CreepRange, Speed(slime) * dt), minX, maxX);
    }
}
