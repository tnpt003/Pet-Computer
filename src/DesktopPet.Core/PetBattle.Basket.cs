using System;

namespace DesktopPet;

// 1.5 M20, defence since 2026-11-13 (docs/BATTLE-DATA.md → Bảo vệ giỏ cá): a stage with a fish basket that
// has HP. It stands on the floor (in the middle by default) and the slimes coming in from both edges go for
// it rather than the cat, each with its own blow (a strike, a dash, goo, a bomb, a thrown rider, a burrower
// coming up under it, a flyer's drops). A slime the cat hits turns on the cat for Aggro s (each blow of the
// cat starts that again), then goes back for the basket. Bosses, supports, rocks and the slimes with a job
// of their own (thief, ghost, spring, wall climber, timer, drummer, file shards) keep going for the cat.
// The basket broken loses the battle whatever HP the cat has. No random stream is used; a stage without a
// basket plays as before.
public sealed class BasketSpec
{
    // The basket's HP; where it stands (0–1 across the strip); × a slime's damage on it; seconds a slime the
    // cat hit goes for the cat.
    public double Hp { get; init; } = 150;
    public double At { get; init; } = 0.5;
    public double Damage { get; init; } = 1;
    public double Aggro { get; init; } = 4;
    // The old rules' keys (fish carried off, before 2026-11-13): still read so an older custom battle.json
    // loads, and ignored.
    public double? Fish { get; init; }
    public double? Every { get; init; }
    public double? Grab { get; init; }
    public double? Carry { get; init; }
    public double? Back { get; init; }

    public static void Validate(BasketSpec b, string where)
    {
        void Within(double value, double min, double max, string name)
        {
            if (!double.IsFinite(value) || value < min || value > max)
                throw new System.IO.InvalidDataException($"{where}.basket.{name} must be {min.ToString(System.Globalization.CultureInfo.InvariantCulture)}–{max.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
        Within(b.Hp, 10, 2000, "hp"); Within(b.At, 0.15, 0.85, "at"); Within(b.Damage, 0.1, 5, "damage"); Within(b.Aggro, 0.5, 30, "aggro");
    }
}

public sealed partial class BattleSim
{
    public BasketSpec? Basket => Stage?.Basket;
    // Where the basket stands (NaN: no basket), its HP now and at most, and whether it was broken.
    public double BasketX { get; private set; } = double.NaN;
    public double BasketHp { get; private set; }
    public double BasketMaxHp => Basket?.Hp ?? 0;
    // The basket's HP left in whole percent (0–100; 0 without a basket).
    public int BasketPercent => BasketMaxHp > 0 ? (int)Math.Ceiling(Math.Round(BasketHp / BasketMaxHp * 100, 6)) : 0;
    public bool BasketBroken { get; private set; }
    public double BasketHurtTaken { get; private set; }
    public int BasketHits { get; private set; }
    public int Aggroed { get; private set; }
    // Seconds the basket flashes after a hit (for the view).
    public double BasketFlash { get; private set; }
    public const double BasketHalfWidth = 26, BasketHeight = 26;

    private void PlaceBasket()
    {
        if (Basket is not BasketSpec b) return;
        BasketX = Left + (Right - Left) * b.At;
        BasketHp = b.Hp;
    }

    // A kind that goes for the basket while the cat has not hit it: not a boss, a support (herb), a rock or a
    // still thing (file shard, flag; a spitter, kept back like a support, spits at it), nor a slime whose job is the cat (thief, ghost, spring trap, wall climber,
    // timer, drummer).
    public static bool AttacksBasket(EnemyType t) => !t.Boss && (!t.Support || t.Spit != null) && !t.Blocker && !t.Still && t.Steal == null && t.Ghost == null
        && t.Spring == null && t.Cling == null && t.Clock == null && t.Zip == null && t.Link?.Kind != EnemyLinks.Rhythm;

    // From the start of its step: this slime goes for the basket now (not one the cat hit lately).
    private bool AimsAtBasket(Slime s) => Basket != null && double.IsFinite(BasketX) && !Over && s.AggroLeft <= 0 && AttacksBasket(s.Type);

    // Every blow of the cat's own (its weapon, combo, splash, echo, the cat thrown) on a slime of a basket
    // stage: it turns on the cat for Aggro s.
    private void Provoke(Slime s, DamageSource source)
    {
        if (Basket is not BasketSpec b || !AttacksBasket(s.Type)) return;
        if (source is not (DamageSource.Weapon or DamageSource.Combo or DamageSource.Splash or DamageSource.Echo or DamageSource.Throw)) return;
        if (s.AggroLeft <= 0)
        {
            Aggroed++;
            events.Add(new(BattleEventKind.EnemyAggroed, s.X, s.Id));
        }
        s.AggroLeft = b.Aggro;
        s.OnBasket = false;
    }

    // In reach of the basket from x (a slime's strike of `range`).
    private bool BasketInReach(double x, double range) => double.IsFinite(BasketX) && Math.Abs(BasketX - x) <= range + 12;

    // Every hit on the basket. True when it took damage (a mirage's copy only pretends).
    private bool HurtBasket(Slime from, double share = 1)
    {
        if (Basket is not BasketSpec b || Over || BasketHp <= 0) return false;
        if (from.Decoy) { AddEffect(BattleEffectKind.Miss, BasketX, BasketHeight + 10); return false; }
        if (from.Rooted) share *= 1 - Settings.FanColumnCut;
        double hurt = Math.Min(BasketHp, from.Damage * share * from.Puff * b.Damage);
        if (hurt <= 0) return false;
        BasketHp = Math.Max(0, BasketHp - hurt);
        BasketHurtTaken += hurt;
        BasketHits++;
        BasketFlash = Settings.HurtFlashSeconds;
        AddEffect(BattleEffectKind.Number, BasketX, BasketHeight + 16, Math.Round(hurt));
        events.Add(new(BattleEventKind.BasketHurt, BasketX, BasketPercent));
        if (BasketHp <= 0)
        {
            BasketBroken = true;
            AddEffect(BattleEffectKind.Poof, BasketX, BasketHeight / 2, 1.4);
            events.Add(new(BattleEventKind.BasketBroken, BasketX));
            Finish(BattlePhase.Lost);
        }
        return true;
    }

    // Each Step: the basket's flash fades.
    private void StepBasket(double dt)
    {
        if (Basket == null) return;
        BasketFlash = Math.Max(0, BasketFlash - dt);
    }

    private void ClearBasket(bool won)
    {
        if (Basket == null) return;
        BasketFlash = 0;
        foreach (var s in slimes) { s.AggroLeft = 0; s.OnBasket = false; }
    }

    private void MapBasket(double left, double oldWidth, double newWidth)
    {
        if (double.IsFinite(BasketX)) BasketX = left + (BasketX - Left) / oldWidth * newWidth;
    }
}
