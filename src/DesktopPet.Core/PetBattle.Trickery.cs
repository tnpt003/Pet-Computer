using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// 1.5 M25 (docs/UPDATE-1.5.md J1; docs/BATTLE-DATA.md → Nhóm Mưu mẹo): the Trickery group's cards and
// synergies. Its cards make the skills, the apps and the drops better; its rare cards:
// - Ground quake (landingQuake): the cat put down after a real lift (RescueGraceMinLiftSeconds, a throw
//   too) shakes the floor. 2026-11-04 (docs/GOPY-C-2026-11-04.md D2): the cat lands without bouncing
//   (QuakeReady tells the window) and stands still QuakeStand s; two waves of cracks run out to both sides
//   (QuakeReach DIPs in QuakeTravel s): every enemy on the ground they reach takes Amount damage (a boss
//   half, like a tool's hit), is knocked away from the cat and dazed QuakeStun s. Not an enemy in the air,
//   on a wall, dug in or in the Grabber. Once per QuakeCooldown s.
// - Sleight of paw (skillRefund): a skill used costs only half its cooldown with chance Amount.
// Synergies: Teamwork (skillRush: a skill used gives SkillRushSeconds of Amount more swing damage),
// Quick reload (killRecharge: each enemy defeated takes Amount s off every carried skill's cooldown),
// Skill feast (skillFeast: every Amount s of fighting the carried skill with the most cooldown left is ready).
public sealed partial class BattleSim
{
    public const double QuakeRadius = 110, QuakeCooldown = 6, QuakeKnock = 260, SkillRushSeconds = 3;
    public const double QuakeReach = 170, QuakeTravel = 0.35, QuakeStun = 0.8, QuakeStand = 0.6;
    // The waves running out (-1: none), from where, and who they already hit; the cat's stand after landing.
    private double quakeAge = -1, quakeX;
    private readonly HashSet<int> quakeHit = new();
    public double QuakeStandRemaining { get; private set; }
    // The card is carried and ready: a landing now would shake the floor (the window lands the cat flat).
    public bool QuakeCarried => quakeDamage > 0;
    public bool QuakeReady => quakeDamage > 0 && QuakeRemaining <= 0 && !Over && !Paused && !MutantBumpMech && CatLifted && liftedFor >= Settings.RescueGraceMinLiftSeconds;
    public double QuakeWaveFront => quakeAge < 0 ? -1 : QuakeReach * Math.Min(1, quakeAge / QuakeTravel);
    private readonly Random trickRandom;
    private double laserBonus, fanBonus, grabberBonus, scissorsBonus, sprayBonus, dropBonus, fishCardHeal, quakeDamage, refundChance;
    private double skillRushBonus, feastClock;
    // The quake's cooldown left; the Teamwork rush's seconds left.
    public double QuakeRemaining { get; private set; }
    public double SkillRushRemaining { get; private set; }
    public int Quakes { get; private set; }
    public int QuakeHits { get; private set; }
    public int Refunds { get; private set; }
    public int Feasts { get; private set; }

    // A swing's damage with Teamwork's rush on.
    private double SkillRushFactor => SkillRushRemaining > 0 ? 1 + skillRushBonus : 1;

    // The Trickery cards' effects (Take); false: not one of them.
    private bool TakeTrick(UpgradeSpec card)
    {
        switch (card.Effect)
        {
            case "laserTime": laserBonus += card.Amount; break;
            case "fanPower": fanBonus += card.Amount; break;
            case "grabberThrow": grabberBonus += card.Amount; break;
            case "scissorsDamage": scissorsBonus += card.Amount; break;
            case "sprayTime": sprayBonus += card.Amount; break;
            case "dropChance": dropBonus += card.Amount; break;
            case "fishHeal": fishCardHeal += card.Amount; break;
            case "landingQuake": quakeDamage = Math.Max(quakeDamage, card.Amount); break;
            case "skillRefund": refundChance = Math.Min(1, refundChance + card.Amount); break;
            default: return false;
        }
        return true;
    }

    // The skills' numbers with the Trickery cards.
    public double LaserSeconds => Settings.LaserSeconds + laserBonus;
    private double GrabberHitDamage => Settings.GrabberHitDamage * (1 + grabberBonus);
    private double GrabberSlamDamage => Settings.GrabberSlamDamage * (1 + grabberBonus);
    private double GrabberStun => Settings.GrabberStun * (1 + grabberBonus);
    private double ScissorsDamage => Settings.ScissorsDamage * (1 + scissorsBonus);
    private double SprayTankSeconds => Settings.SprayTankSeconds + sprayBonus;

    // A skill used by the user (every kind, at the moment it acts): Teamwork's rush.
    private void SkillUsed()
    {
        if (Active("skillRush") is SynergySpec teamwork) { SkillRushRemaining = SkillRushSeconds; skillRushBonus = teamwork.Amount; }
    }

    // A skill spent: its cooldown starts, half of it now and then with Sleight of paw (rolled on its own
    // stream so the other draws stay what they would have been).
    private void Recharge(BattleSkill skill)
    {
        double cooldown = SkillCooldown(skill);
        if (refundChance > 0 && trickRandom.NextDouble() < refundChance)
        {
            cooldown /= 2;
            Refunds++;
            events.Add(new(BattleEventKind.SkillRefunded, lastCatX, (int)skill));
        }
        cooldowns[(int)skill] = cooldown;
    }

    // Every frame (not paused, not frozen): the rushes and clocks of this group.
    private void StepTrickery(double dt, bool catHeld)
    {
        QuakeRemaining = Math.Max(0, QuakeRemaining - dt);
        QuakeStandRemaining = Math.Max(0, QuakeStandRemaining - dt);
        StepQuake(dt);
        SkillRushRemaining = Math.Max(0, SkillRushRemaining - dt);
        if (Active("skillFeast") is not SynergySpec feast || Phase != BattlePhase.Fighting || catHeld) return;
        feastClock += dt;
        if (feastClock < feast.Amount) return;
        // The carried skill with the most cooldown left (none cooling down: the clock waits, full).
        int longest = -1;
        foreach (var skill in carried)
            if (cooldowns[(int)skill] > 0 && (longest < 0 || cooldowns[(int)skill] > cooldowns[longest])) longest = (int)skill;
        if (longest < 0) { feastClock = feast.Amount; return; }
        cooldowns[longest] = 0;
        feastClock = 0;
        Feasts++;
        events.Add(new(BattleEventKind.SkillFeast, lastCatX, longest));
    }

    // Quick reload: an enemy defeated brings every carried skill's cooldown nearer.
    private void DefeatRecharge()
    {
        if (Active("killRecharge") is not SynergySpec reload) return;
        foreach (var skill in carried) cooldowns[(int)skill] = Math.Max(0, cooldowns[(int)skill] - reload.Amount);
    }

    // The cat put down after a real lift (Step checks the lift's time): Ground quake. The cat stands still
    // a moment; the waves start running out.
    private void Quake(double x)
    {
        if (quakeDamage <= 0 || QuakeRemaining > 0 || Over) return;
        QuakeRemaining = QuakeCooldown;
        QuakeStandRemaining = QuakeStand;
        Quakes++;
        quakeAge = 0;
        quakeX = x;
        quakeHit.Clear();
        AddEffect(BattleEffectKind.Quake, x, 0, QuakeReach);
        events.Add(new(BattleEventKind.Quaked, x));
        StepQuake(0);
    }

    // The two waves run out along the floor; each enemy on the ground they reach is hit once.
    private void StepQuake(double dt)
    {
        if (quakeAge < 0 || Over) return;
        quakeAge += dt;
        double front = QuakeWaveFront;
        foreach (var s in slimes.ToArray())
        {
            if (quakeHit.Contains(s.Id) || !s.Targetable || s.Airborne || s.OnWall || s.Burrowed || s.Handled || s.LeapHeight > 0 || s.Lift > 0) continue;
            if (Math.Abs(s.X - quakeX) > front + s.HitWidth / 2) continue;
            quakeHit.Add(s.Id);
            QuakeHits++;
            if (!s.IsBoss && s.Elite is not { PushImmune: true })
            {
                int away = Math.Abs(s.X - quakeX) >= 1 ? Math.Sign(s.X - quakeX) : 1;
                double push = QuakeKnock * s.Type.PushFactor;
                if (s.KnockVelocity * away < push) s.KnockVelocity = away * push;
            }
            AddEffect(BattleEffectKind.Thud, s.X, s.Height / 2);
            ToolHit(s, quakeDamage);
            if (Over) return;
            if (s.Targetable && !s.IsBoss) Daze(s, QuakeStun);
        }
        if (quakeAge >= QuakeTravel) quakeAge = -1;
    }
}
