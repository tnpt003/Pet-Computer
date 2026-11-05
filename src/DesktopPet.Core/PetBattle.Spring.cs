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
    public int SpringLeaps { get; private set; }
    public int CatShoves { get; private set; }
    // The cat being slid by a spring: towards which side, how fast (DIPs/s), how long still (s).
    public int ShoveSide { get; private set; }
    public double ShoveSpeed { get; private set; }
    public double ShoveLeft { get; private set; }

    // From CatTurn: the cat's spot this frame while a shove lasts (null: none). Kept on the field.
    private double? ShoveCat(double dt, double catX)
    {
        if (ShoveLeft <= 0) return null;
        double step = ShoveSpeed * Math.Min(dt, ShoveLeft);
        ShoveLeft = Math.Max(0, ShoveLeft - dt);
        return Math.Clamp(catX + ShoveSide * step, Left, Right);
    }

    public static double SpringRadius(Slime slime) =>
        slime.Type.Spring is SpringSpec spring ? spring.Radius * (slime.Elite != null ? spring.EliteRadius : 1) : 0;

    private static int FlattenHits(Slime slime) =>
        slime.Type.Spring is SpringSpec spring ? slime.Elite != null ? spring.EliteHits : spring.FlattenHits : 0;

    // Every frame, whatever the trap is doing (before its state's own step).
    private void StepSpring(Slime slime, double dt, double catX, bool catHeld)
    {
        var spring = slime.Type.Spring!;
        bool free = slime.State == SlimeState.Moving && !slime.Handled;
        slime.LeapCooldown = Math.Max(0, slime.LeapCooldown - dt);
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
            case SpringState.Crouching:
                // Pushed, stunned or grabbed while it crouches: no leap.
                if (!free) { slime.Crouch = 0; Rest(slime, spring); break; }
                slime.SpringClock -= dt * slime.ChillFactor;
                slime.Crouch = Math.Clamp(1 - slime.SpringClock / spring.LeapCrouch, 0, 1);
                if (slime.SpringClock > 0) break;
                slime.Crouch = 0;
                slime.Spring = SpringState.Leaping;
                slime.SpringClock = spring.LeapSeconds;
                events.Add(new(BattleEventKind.SpringLeapt, slime.X, slime.Id));
                break;
            case SpringState.Leaping:
                // Grabbed in mid-air: it drops where it is, spent.
                if (slime.Handled) { slime.LeapHeight = 0; Rest(slime, spring); break; }
                slime.SpringClock = Math.Max(0, slime.SpringClock - dt);
                double k = 1 - slime.SpringClock / spring.LeapSeconds;
                slime.X = slime.LeapFromX + (slime.LeapToX - slime.LeapFromX) * k;
                slime.LeapHeight = 4 * spring.LeapHeight * k * (1 - k);
                slime.KnockVelocity = 0;
                if (slime.SpringClock > 0) break;
                slime.LeapHeight = 0;
                slime.X = slime.LeapToX;
                Bounce(slime, spring);
                break;
            case SpringState.Ready:
                double gap = Math.Abs(catX - slime.X);
                if (free && !catHeld && Grace <= 0 && gap <= SpringRadius(slime))
                {
                    slime.SpringWait = 0;
                    slime.Spring = SpringState.Squeezing;
                    slime.SpringClock = spring.Compress;
                    events.Add(new(BattleEventKind.SpringSqueezed, slime.X, slime.Id));
                    break;
                }
                // Just out of its ring long enough: it leaps at the cat.
                if (free && !catHeld && Grace <= 0 && spring.LeapRange > 0 && gap <= spring.LeapRange && slime.LeapCooldown <= 0)
                {
                    slime.SpringWait += dt;
                    if (slime.SpringWait >= spring.LeapWait) StartLeap(slime, spring, catX);
                }
                else slime.SpringWait = 0;
                break;
        }
    }

    // A crouch, then a leap to where the cat stands now (kept on the floor).
    private void StartLeap(Slime slime, SpringSpec spring, double catX)
    {
        double half = slime.Width / 2;
        slime.SpringWait = 0;
        slime.Spring = SpringState.Crouching;
        slime.SpringClock = spring.LeapCrouch;
        slime.LeapFromX = slime.X;
        slime.LeapToX = Math.Clamp(catX, Left + half, Math.Max(Left + half, Right - half));
        slime.LeapCooldown = spring.LeapCooldown;
        slime.Facing = catX < slime.X ? -1 : 1;
        SpringLeaps++;
        events.Add(new(BattleEventKind.SpringCrouched, slime.LeapToX, slime.Id));
    }

    // Where a crouching or leaping spring will come down (null: it is not leaping).
    public static double? SpringLanding(Slime slime) => slime.Spring is SpringState.Crouching or SpringState.Leaping ? slime.LeapToX : null;

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
        // It got through (no Shield, no Turtle shell): the cat is also slid away from it.
        if (HurtCat(slime, lastCatX, true, 1, spring.Stun, crush: true) && spring.Push > 0 && !CrushProof && !Over) Shove(lastCatX >= slime.X ? 1 : -1, spring.Push, spring.PushSeconds);
    }

    // The cat slid `distance` DIPs towards `side` over `seconds` (CatTurn moves it; a lift ends it).
    private void Shove(int side, double distance, double seconds)
    {
        if (Raging || CatLifted || Over || distance <= 0 || seconds <= 0) return;
        ShoveSide = side;
        ShoveSpeed = distance / seconds;
        ShoveLeft = seconds;
        CatShoves++;
        CancelSwing();
        events.Add(new(BattleEventKind.CatShoved, lastCatX, side));
    }

    // A hit from the cat's weapon or a bomb (PetBattle.cs Damage): enough of them flatten it. Hit from out
    // of its ring by the weapon (a long reach), it leaps at the cat instead when it may.
    private void SpringStruck(Slime slime, DamageSource source)
    {
        if (slime.Type.Spring is not SpringSpec spring || slime.Spring == SpringState.Flat) return;
        if (source is not (DamageSource.Weapon or DamageSource.Combo or DamageSource.Splash or DamageSource.Echo or DamageSource.Bomb)) return;
        if (source != DamageSource.Bomb && slime.Spring == SpringState.Ready && spring.LeapRange > 0 && slime.LeapCooldown <= 0 && slime.State == SlimeState.Moving
            && !slime.Handled && !CatHeld && Math.Abs(lastCatX - slime.X) > SpringRadius(slime)) { StartLeap(slime, spring, lastCatX); return; }
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
        if (spring.CreepRange <= 0 || slime.Spring is SpringState.Squeezing or SpringState.Flat or SpringState.Crouching or SpringState.Leaping || Math.Abs(gap) <= spring.CreepRange) return;
        slime.X = Math.Clamp(slime.X + Math.Sign(gap) * Math.Min(Math.Abs(gap) - spring.CreepRange, Speed(slime) * dt), minX, maxX);
    }
}
