using System;

namespace DesktopPet;

// 1.5 M08, docs/UPDATE-1.5.md V1/V2 and docs/BATTLE-DATA.md → Cuộn len, Găng đấm: what a weapon's own
// keys (WeaponSpec.Sweep, Pull, ComboHits, Launch) add to the cat's swing.
// - Sweep (the yarn): every landed swing also hits every other enemy in reach (Sweep in PetBattle.Rage.cs,
//   shared with the rage stance), so a combo's splash adds nothing more.
// - Pull (the yarn): a landed hit on an enemy that keeps its distance (a support kind) draws it toward the
//   cat, stopping just short of it; a boss or a firm elite stays.
// - ComboHits (the glove: 5): the chain this weapon needs for a combo.
// - Launch (the glove): a combo hit tosses its enemy up (the Grabber's flight, PetBattle.Grabber.cs, without
//   its hits); it is out of the fight while it flies and dazed LaunchStun s on landing.
public sealed partial class BattleSim
{
    public int ComboHits => arms.ComboHits ?? Settings.ComboHits;
    public int Pulls { get; private set; }
    public int WeaponLaunches { get; private set; }
    public int SweepHits { get; private set; }

    private void PullIn(Slime slime, double catX)
    {
        if (arms.Pull <= 0 || !slime.Type.Support || slime.IsBoss || !slime.Targetable || slime.Handled || slime.Stuck || slime.Elite is { PushImmune: true }) return;
        double dx = slime.X - catX;
        // Knock back fades as exp(-6 t): a speed of 6 × the distance covers it, so it stops beside the cat.
        double gap = Math.Max(0, Math.Abs(dx) - CatHalfWidth - slime.HitWidth / 2 - 8);
        double speed = Math.Min(arms.Pull, gap * 6) * slime.Type.PushFactor;
        if (speed < 5) return;
        slime.KnockVelocity = -Math.Sign(dx) * speed;
        Pulls++;
    }

    public bool Launchable(Slime s) => s.Targetable && !s.IsBoss && !s.Type.Blocker && s.Type.Scale <= Settings.GrabberMaxScale + 1e-9 && s.Elite is not { PushImmune: true }
        && !s.Handled && !s.Airborne && !s.Riding && !s.Stuck && s.LeapHeight <= 0 && !s.Shielded;

    private void LaunchUp(Slime slime, double catX)
    {
        if (arms.Launch <= 0 || !Launchable(slime)) return;
        foreach (var s in slimes)
            if (s.Riding && s.LinkedTo == slime.Id) Fall(s, 0);
        // Its windup, heal or dash is lost in the air.
        if (slime.State is not (SlimeState.Moving or SlimeState.Stunned)) slime.Set(SlimeState.Moving);
        if (slime.MendingId != 0 && Find(slime.MendingId) is Slime mended) mended.Mended = false;
        slime.MendingId = 0;
        slime.KnockVelocity = 0; slime.Blown = 0; slime.Lured = false;
        slime.Tossed = slime.Launched = true;
        slime.Thrown = false;
        slime.TossVX = (slime.X < catX ? -1 : 1) * 90;
        slime.TossVY = arms.Launch;
        WeaponLaunches++;
        events.Add(new(BattleEventKind.Launched, slime.X, slime.Id));
    }
}
