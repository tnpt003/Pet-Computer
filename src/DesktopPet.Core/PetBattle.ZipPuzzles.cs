using System;
using System.Linq;

namespace DesktopPet;

// ZIP puzzle props use the existing floor/air targeting and swing clocks, never damage HP.
public enum ZipProp { None, Belt, File, Tab }

public sealed partial class BattleSim
{
    public double CatXForZip => lastCatX;
    public double ZipFileGoal(Slime file) => Left + (Right - Left) * (0.25 + file.ZipSymbol * 0.25);
    public bool ZipPropActive(Slime prop) => prop.ZipProp != ZipProp.Belt || Find(prop.SummonedBy) is Slime boss && prop.ZipSymbol == (boss.Pins / 3) % 2;

    private Slime AddZipProp(Slime boss, ZipProp role, double x, int symbol, double height = 0)
    {
        var prop = AddSlime(x, Settings.Enemy("zip_packet"));
        prop.SummonedBy = boss.Id;
        prop.ZipProp = role;
        prop.ZipSymbol = symbol;
        prop.Altitude = height;
        prop.Set(SlimeState.Moving);
        return prop;
    }

    private void ClearZipProps(Slime boss)
    {
        foreach (var prop in slimes.Where(s => s.SummonedBy == boss.Id && s.ZipProp != ZipProp.None).ToArray()) Unpacked(prop);
    }

    private void BeginZipFiles(Slime boss)
    {
        boss.Retreating = false;
        boss.PendingCalls = null;
        boss.ZipPuzzleClock = 3.5;
        // Each lane leaves room on BOTH sides, even after repeated hits.
        for (int i = 0; i < 3; i++) AddZipProp(boss, ZipProp.File, Left + (Right - Left) * (0.18 + i * 0.25), i);
    }

    private HitResult HitZipProp(Slime prop, DamageSource source)
    {
        if (source is not (DamageSource.Weapon or DamageSource.Combo) || !ZipPropActive(prop)) return HitResult.Immune;
        if (Find(prop.SummonedBy) is not Slime boss || !boss.Shielded) return HitResult.Immune;
        if (prop.ZipProp == ZipProp.Belt)
        {
            boss.Pins++; ZipPins++;
            if (boss.Pins % 3 == 0)
            {
                boss.ZipPuzzleClock = 2.8;
                zipBlows.RemoveAll(b => b.OwnerId == boss.Id);
            }
            events.Add(new(BattleEventKind.ZipPinned, prop.X, boss.Pins));
            if (boss.Pins >= 12) { ClearZipProps(boss); EndPress(boss, boss.Type.Zip!.Press, true); }
        }
        else if (prop.ZipProp == ZipProp.File)
        {
            double margin = Math.Min(120, (Right - Left) * 0.15);
            prop.X = Math.Clamp(prop.X + (lastCatX <= prop.X ? 1 : -1) * 36, Left + margin, Right - margin);
            if (Math.Abs(prop.X - ZipFileGoal(prop)) <= 40)
            {
                Unpacked(prop);
                if (!slimes.Any(s => s.SummonedBy == boss.Id && s.ZipProp == ZipProp.File && s.Targetable))
                {
                    boss.Shielded = false; boss.ShieldTag = 0;
                    zipBlows.RemoveAll(b => b.OwnerId == boss.Id);
                    ZipGrow(boss);
                    boss.StunRemaining = 2.2; boss.Set(SlimeState.Stunned);
                    events.Add(new(BattleEventKind.BossShieldDown, boss.X));
                }
            }
        }
        else if (prop.ZipProp == ZipProp.Tab)
        {
            Unpacked(prop); boss.BombPulls++;
            events.Add(new(BattleEventKind.ZipTabPulled, prop.X, boss.BombPulls));
            if (!slimes.Any(s => s.SummonedBy == boss.Id && s.ZipProp == ZipProp.Tab && s.Targetable))
            {
                boss.BombClock = 0; boss.BombDone = true;
                boss.Deflating = boss.Type.Zip!.Bomb.Flight;
                boss.DeflateVelocity = DeflateSpeed;
                events.Add(new(BattleEventKind.ZipDeflated, boss.X));
            }
        }
        AddEffect(BattleEffectKind.Spark, prop.X, prop.Altitude + 20);
        return HitResult.Landed;
    }

    private void StepZipPuzzle(Slime boss, double dt, BossPhase phase)
    {
        if (phase.Press && boss.Retreating)
        {
            if (!StepLeap(boss, dt, (Left + Right) / 2)) return;
            boss.Retreating = false;
            AddZipProp(boss, ZipProp.Belt, Left + 65, 0);
            AddZipProp(boss, ZipProp.Belt, Right - 65, 1);
            boss.ZipPuzzleClock = 2.8;
        }
        boss.ZipPuzzleClock -= dt;
        if (boss.ZipPuzzleClock > 0) return;
        if (phase.Press)
        {
            boss.ZipPuzzleClock += 2.8;
            int side = (boss.Pins / 3) % 2 == 0 ? -1 : 1;
            double edge = side < 0 ? Left : Right;
            double radius = Math.Abs(edge - boss.X) / 2;
            var sweep = new ZipMoveSpec { Damage = 8, Stun = 0.5 };
            AddZipBlow(boss, (edge + boss.X) / 2, radius, 0, 65, 0.8, sweep, ZipBlowKind.Mark, 1);
        }
        else
        {
            boss.ZipPuzzleClock += 3.5;
            var tree = new ZipMoveSpec { Damage = 12, Stun = 0.6 };
            AddZipBlow(boss, Math.Clamp(lastCatX, Left + 35, Right - 35), 35, 0, 120, 1, tree, ZipBlowKind.Branch, 1);
        }
    }

    private void StepZipRepulse(Slime boss, double dt)
    {
        if (boss.ZipStage < 1 || boss.Shielded || boss.State == SlimeState.Stunned || CatLocked || ZipGuardLeft > 0) return;
        if (boss.ZipRepulseWarn > 0)
        {
            boss.ZipRepulseWarn = Math.Max(0, boss.ZipRepulseWarn - dt);
            if (boss.ZipRepulseWarn > 0) return;
            boss.ZipRepulseClock = 22;
            if (CatOffFloor || Raging || CrushProof || ShieldRemaining > 0 || Grace > 0) return;
            int side = lastCatX < boss.X ? -1 : 1;
            double goal = side < 0 ? Left + CatHalfWidth : Right - CatHalfWidth;
            double distance = Math.Abs(goal - lastCatX);
            if (distance < 1) CatStunRemaining = Math.Max(CatStunRemaining, StunFor(1.2));
            else
            {
                Shove(side, distance, 0.8);
                zipShoveStun = true;
            }
            boss.Cooldown = Math.Max(boss.Cooldown, 2.8);
            return;
        }
        boss.ZipRepulseClock -= dt;
        if (boss.ZipRepulseClock <= 0 && boss.ZipAct == ZipAct.None && boss.State == SlimeState.Moving)
        {
            boss.ZipRepulseWarn = 0.9;
            boss.Cooldown = Math.Max(boss.Cooldown, 3.7);
        }
    }
    private bool zipShoveStun;
}
