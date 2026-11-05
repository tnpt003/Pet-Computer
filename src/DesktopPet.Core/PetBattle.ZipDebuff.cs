using System;

namespace DesktopPet;

// 2026-11-06, the Zip Slime's two debuffs (docs/ZIP-SLIME.md), outside its bag of moves and with no warning, one
// at a time, drawn at random ZipDebuffSpec.GapMin–GapMax s after the last one landed:
// - Squeezed: the cat is drawn smaller (its window and clicks stay the same), its weapon hits ×Damage and it runs
//   ×Speed for Seconds s. Skills and apps are not touched.
// - Locked: shut in a ZIP shell the cat cannot be lifted; it still walks and fights. LockClicks clicks on it, or
//   LockSwings of its swings (hit or miss), or LockSeconds s open it. Only on a cat on the floor, outside the
//   riddles and with no announced blow on the way; while it lasts and for Guard s more the boss starts no move,
//   since the hand is the cat's way of dodging.
// The Shield stops either from taking hold; Rage clears both and keeps them off.
public enum ZipDebuff { None, Squeezed, Locked }

public sealed partial class BattleSim
{
    public ZipDebuff CatZipped { get; private set; }
    public double ZipDebuffLeft { get; private set; }
    public double ZipDebuffSeconds { get; private set; }
    public double ZipGuardLeft { get; private set; }
    public int ZipLockClicks { get; private set; }
    public int ZipLockSwings { get; private set; }
    public int ZipDebuffsTaken { get; private set; }
    public int ZipDebuffsBlocked { get; private set; }
    private ZipDebuffSpec? zipDebuffRules;
    public ZipDebuffSpec? ZipDebuffRules => zipDebuffRules;
    public bool CatSqueezed => CatZipped == ZipDebuff.Squeezed && !Over;
    public bool CatLocked => CatZipped == ZipDebuff.Locked && !Over;
    // How far it has taken hold (0–1, over its first moments).
    public double ZipDebuffLook => CatZipped == ZipDebuff.None || Over ? 0 : Math.Clamp((ZipDebuffSeconds - ZipDebuffLeft) / 0.3, 0, 1);
    public double SqueezeScale => CatSqueezed ? 1 + ((zipDebuffRules?.Scale ?? 1) - 1) * ZipDebuffLook : 1;
    private double SqueezeSpeed => CatSqueezed ? zipDebuffRules?.Speed ?? 1 : 1;
    private double SqueezeDamage => CatSqueezed ? zipDebuffRules?.Damage ?? 1 : 1;

    // The debuff's own time: it runs with the battle.
    private void StepZipDebuff(double dt)
    {
        ZipGuardLeft = Math.Max(0, ZipGuardLeft - dt);
        if (CatZipped == ZipDebuff.None) return;
        ZipDebuffLeft = Math.Max(0, ZipDebuffLeft - dt);
        if (ZipDebuffLeft <= 0) ClearZipDebuff(true);
    }

    private double NextZipGap(ZipDebuffSpec d)
    {
        zipRandom ??= BattleRandom.Stream(Seed, BattleRandom.Zip);
        return d.GapMin + zipRandom.NextDouble() * (d.GapMax - d.GapMin);
    }

    // Every step of a Zip Slime on the field: its clock, then the debuff as soon as one can land.
    private void TickZipDebuff(Slime boss, ZipSpec zip, double dt)
    {
        if (zip.Debuff is not ZipDebuffSpec d || !d.On) return;
        if (boss.ZipDebuffClock < 0) boss.ZipDebuffClock = d.First;
        if (boss.Shielded || boss.Deflating > 0) return;
        boss.ZipDebuffClock = Math.Max(0, boss.ZipDebuffClock - dt);
        if (boss.ZipDebuffClock > 0 || CatZipped != ZipDebuff.None || Raging) return;
        if (boss.ZipDebuffNext == ZipDebuff.None)
        {
            zipRandom ??= BattleRandom.Stream(Seed, BattleRandom.Zip);
            boss.ZipDebuffNext = zipRandom.Next(2) == 0 ? ZipDebuff.Squeezed : ZipDebuff.Locked;
        }
        // A lock waits for a moment nothing announced is coming; a squeeze lands whenever.
        if (boss.ZipDebuffNext == ZipDebuff.Locked && !LockFits(boss)) return;
        if (ShieldRemaining > 0)
        {
            ZipDebuffsBlocked++;
            events.Add(new(BattleEventKind.Blocked, lastCatX));
        }
        else ApplyZipDebuff(boss.ZipDebuffNext, d);
        boss.ZipDebuffNext = ZipDebuff.None;
        boss.ZipDebuffClock = NextZipGap(d);
    }

    private bool LockFits(Slime boss)
    {
        if (CatHeld || CatStunned || Grace > 0 || boss.State is not (SlimeState.Moving or SlimeState.Recover) || boss.Rolling) return false;
        if (boss.ZipBeat is not (ZipBeat.None or ZipBeat.Recover) || zipBlows.Count > 0 || shots.Count > 0) return false;
        foreach (var s in slimes) if (s.PacketFuse && s.PacketLeft > 0 && s.State != SlimeState.Defeated) return false; // Its blast could not be dodged.
        return true;
    }

    private void ApplyZipDebuff(ZipDebuff kind, ZipDebuffSpec d)
    {
        CatZipped = kind;
        zipDebuffRules = d;
        ZipDebuffLeft = ZipDebuffSeconds = kind == ZipDebuff.Squeezed ? d.Seconds : d.LockSeconds;
        ZipLockClicks = ZipLockSwings = 0;
        ZipDebuffsTaken++;
        events.Add(new(BattleEventKind.ZipDebuffed, lastCatX, (int)kind));
    }

    // Timed out or opened (show), or gone with no show (Rage, the boss's fall).
    private void ClearZipDebuff(bool show)
    {
        if (CatZipped == ZipDebuff.None) return;
        var was = CatZipped;
        CatZipped = ZipDebuff.None;
        ZipDebuffLeft = 0;
        if (!show) return;
        if (was == ZipDebuff.Locked && zipDebuffRules is ZipDebuffSpec d) ZipGuardLeft = d.Guard;
        AddEffect(BattleEffectKind.Poof, lastCatX, CatStandHeight);
        events.Add(new(BattleEventKind.ZipDebuffEnded, lastCatX, (int)was));
    }

    // A press on the locked cat; true: it counted.
    public bool ClickZipLock()
    {
        if (!CatLocked || Paused || zipDebuffRules is not ZipDebuffSpec d) return false;
        ZipLockClicks++;
        events.Add(new(BattleEventKind.ZipLockTapped, lastCatX, ZipLockClicks));
        if (ZipLockClicks >= d.LockClicks) ClearZipDebuff(true);
        return true;
    }

    // A swing started, hit or miss.
    private void ZipSwing()
    {
        if (!CatLocked || zipDebuffRules is not ZipDebuffSpec d) return;
        ZipLockSwings++;
        if (ZipLockSwings >= d.LockSwings) ClearZipDebuff(true);
    }
}
