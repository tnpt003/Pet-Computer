using System;
using System.Collections.Generic;

namespace DesktopPet;

// The three statuses an enemy can carry (1.5 M04): poison (HP lost over time), chill (everything it
// does is slower) and stick (it stands where it is).
public enum SlimeStatus { Poison, Chill, Stick }

// A status a hit may leave (battle.json: a weapon's or an "onHit" card's "onHit"). Status: "poison",
// "chill" or "stick"; Chance: of each landed hit (a card's chance is its amount, so leave it out
// there); Seconds: how long; Power: poison HP a second (× the build's damage), chill how much slower
// (0.5: half speed); stick has none.
public sealed record StatusHit
{
    public string Status { get; init; } = "poison";
    public double Chance { get; init; } = 1;
    public double Seconds { get; init; } = 3;
    public double Power { get; init; }
    public static readonly string[] Kinds = { "poison", "chill", "stick" };
    public static SlimeStatus? Parse(string? id) => id switch
    {
        "poison" => SlimeStatus.Poison,
        "chill" => SlimeStatus.Chill,
        "stick" => SlimeStatus.Stick,
        _ => null
    };
    // What the battle data may hold (InvalidDataException messages name `at`).
    public static void Validate(StatusHit hit, string at)
    {
        void Within(double value, double min, double max, string name)
        {
            if (!double.IsFinite(value) || value < min || value > max)
                throw new System.IO.InvalidDataException($"{at}.{name} must be {min.ToString(System.Globalization.CultureInfo.InvariantCulture)}–{max.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
        switch (Parse(hit.Status))
        {
            case SlimeStatus.Poison: Within(hit.Seconds, 0.5, 20, "seconds"); Within(hit.Power, 2, 200, "power"); break;
            case SlimeStatus.Chill: Within(hit.Seconds, 0.5, 10, "seconds"); Within(hit.Power, 0.05, 0.9, "power"); break;
            case SlimeStatus.Stick: Within(hit.Seconds, 0.2, 5, "seconds"); Within(hit.Power, 0, 0, "power"); break;
            default: throw new System.IO.InvalidDataException($"{at}.status must be one of: {string.Join(", ", Kinds)}");
        }
        Within(hit.Chance, 0.01, 1, "chance");
    }
}

// 1.5 M04, docs/UPDATE-1.5.md (M2) and docs/BATTLE-DATA.md → Hiệu ứng trên quái:
// - Poison: up to PoisonMaxLayers layers, each its own HP a second; another one adds a layer (none
//   past the last) and the time is the longer of the two. Dealt every PoisonTick s; never blocked by a
//   rock, never knocks back, stuns or sets off a card bonus; a defeat by it counts as the cat's. A
//   shielded boss or a guarded enemy loses nothing meanwhile (its time still runs, no "IMMUNE").
// - Chill: walking, dashing, winding up, recovering and healing go slower by its power; no stacking
//   (the stronger slow and the longer time are kept).
// - Stick: no walking, dashing, knock back or being shoved by the others; it still turns and hits a
//   cat in reach. No stacking (the longer time); once it ends the enemy is free of it StickImmuneSeconds.
// - A boss takes half the poison, half the chill and half as long a stick. None on an enemy still
//   appearing, defeated, behind its shield phase, leaping or riding (a stick); defeat clears them.
// Time runs only in Step: Pause, a card offer and the frozen guide freeze every status.
public sealed partial class BattleSim
{
    public const int PoisonMaxLayers = 3;
    public const double PoisonTick = 0.5, StickImmuneSeconds = 1.5, BossStatusShare = 0.5;
    private readonly Random statusRandom;
    // Statuses that took (by kind) and HP poison took in this battle.
    private readonly int[] statusesApplied = new int[3];
    public int StatusesApplied(SlimeStatus status) => statusesApplied[(int)status];
    public double PoisonDamage { get; private set; }
    // The weapon's own hit and every onHit card taken (the same card again adds its chance).
    private readonly List<(StatusHit Hit, double Chance, string Card)> statusCards = new();

    // Puts a status on an enemy; true when it took. power: see StatusHit (poison before the build's
    // damage is applied by the caller; a boss's share is taken here).
    public bool ApplyStatus(Slime slime, SlimeStatus status, double seconds, double power = 0)
    {
        if (!slime.Targetable || slime.Shielded || slime.LeapHeight > 0 || !double.IsFinite(seconds) || seconds <= 0) return false;
        double share = slime.IsBoss ? BossStatusShare : 1;
        switch (status)
        {
            case SlimeStatus.Poison:
                if (!double.IsFinite(power) || power <= 0) return false;
                if (slime.PoisonLayers < PoisonMaxLayers) { slime.PoisonLayers++; slime.PoisonDps += power * share; }
                if (seconds >= slime.PoisonRemaining) slime.PoisonRemaining = slime.PoisonTotal = seconds;
                break;
            case SlimeStatus.Chill:
                if (!double.IsFinite(power) || power <= 0) return false;
                slime.ChillPower = Math.Max(slime.ChillRemaining > 0 ? slime.ChillPower : 0, Math.Min(0.9, power) * share);
                if (seconds >= slime.ChillRemaining) slime.ChillRemaining = slime.ChillTotal = seconds;
                break;
            case SlimeStatus.Stick:
                if (slime.StickImmune > 0 || slime.Riding) return false;
                if (seconds * share >= slime.StuckRemaining) slime.StuckRemaining = slime.StuckTotal = seconds * share;
                slime.KnockVelocity = 0;
                break;
        }
        statusesApplied[(int)status]++;
        events.Add(new(BattleEventKind.StatusApplied, slime.X, (int)status));
        return true;
    }

    // A landed hit of the cat's weapon (on the floor or held): each onHit (the weapon's, the cards')
    // rolls its chance on its own stream, so the rest of the battle's rolls are untouched.
    private void HitStatuses(Slime slime)
    {
        if (!slime.Targetable) return;
        if (arms.OnHit is StatusHit own) Roll(own, own.Chance);
        foreach (var (hit, chance, _) in statusCards) Roll(hit, chance);

        void Roll(StatusHit hit, double chance)
        {
            if (StatusHit.Parse(hit.Status) is not SlimeStatus status || statusRandom.NextDouble() >= chance || !slime.Targetable) return;
            ApplyStatus(slime, status, hit.Seconds, status == SlimeStatus.Poison ? hit.Power * DamageFactor : hit.Power);
        }
    }

    private void TakeStatusCard(UpgradeSpec card)
    {
        if (card.OnHit is not StatusHit hit) return;
        for (int i = 0; i < statusCards.Count; i++)
            if (statusCards[i].Card == card.Id) { statusCards[i] = (hit, Math.Min(1, statusCards[i].Chance + card.Amount), card.Id); return; }
        statusCards.Add((hit, Math.Min(1, card.Amount), card.Id));
    }

    // The statuses' clocks and the poison's damage for one step (the battle still going).
    private void StepStatus(Slime slime, double dt)
    {
        slime.StickImmune = Math.Max(0, slime.StickImmune - dt);
        if (slime.StuckRemaining > 0)
        {
            slime.StuckRemaining = Math.Max(0, slime.StuckRemaining - dt);
            if (slime.StuckRemaining <= 0) slime.StickImmune = StickImmuneSeconds;
        }
        if (slime.ChillRemaining > 0)
        {
            slime.ChillRemaining = Math.Max(0, slime.ChillRemaining - dt);
            if (slime.ChillRemaining <= 0) slime.ChillPower = 0;
        }
        if (slime.PoisonRemaining <= 0) return;
        double run = Math.Min(dt, slime.PoisonRemaining);
        slime.PoisonRemaining -= run;
        // Protected meanwhile: the time runs, nothing is owed.
        if (slime.Shielded || slime.Mended || slime.WardUntil > Time) slime.PoisonClock = slime.PoisonOwed = 0;
        else
        {
            slime.PoisonOwed += slime.PoisonDps * run;
            slime.PoisonClock += run;
        }
        bool ended = slime.PoisonRemaining <= 0;
        if (slime.PoisonClock >= PoisonTick - 1e-9 || (ended && slime.PoisonOwed > 0)) // (frame times add up a hair short)
        {
            double owed = slime.PoisonOwed;
            slime.PoisonClock = slime.PoisonOwed = 0;
            PoisonDamage += Math.Min(owed, slime.Hp);
            hitTone = NumberTone.Poison;
            Damage(slime, owed, DamageSource.Poison);
            hitTone = NumberTone.Plain;
        }
        if (ended) { slime.PoisonLayers = 0; slime.PoisonDps = 0; slime.PoisonClock = slime.PoisonOwed = 0; }
    }

    private static void ClearStatus(Slime slime)
    {
        slime.PoisonLayers = 0;
        slime.PoisonDps = slime.PoisonRemaining = slime.PoisonClock = slime.PoisonOwed = 0;
        slime.ChillPower = slime.ChillRemaining = 0;
        slime.StuckRemaining = slime.StickImmune = 0;
    }
}
