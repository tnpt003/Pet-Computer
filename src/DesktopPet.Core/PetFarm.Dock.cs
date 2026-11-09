using System;

namespace DesktopPet;

public enum FarmLandSlot { Field, Hearth, Pond, Coop }

// One piece of land on the farm sign (docs/FARM-GAME.md, Biển nông trại). Folded is what the user
// chose and is saved; Borrowed is a land opened only for the job the user just asked for.
public sealed class FarmLandFold
{
    public const double OpenSeconds = 0.9, FoldSeconds = 0.6, StaggerSeconds = 0.12, BorrowGraceSeconds = 1.5;
    private const double LandStart = 0.2, LandSpan = 0.5, Overshoot = 1.7;

    private double idle, wait;

    public bool Folded { get; private set; }
    public bool Borrowed { get; private set; }
    // 1 standing on the floor, 0 out of sight.
    public double Amount { get; private set; } = 1;
    public bool Showing => !Folded || Borrowed;
    // The spell is under way.
    public bool Moving => Amount > 0 && Amount < 1;
    private double LandProgress => Math.Clamp((Amount - LandStart) / LandSpan, 0, 1);
    // How much of the land stands, 0 to 1, and its height, which goes a little past 1 before it settles.
    public double Land => LandProgress * LandProgress * (3 - 2 * LandProgress);
    public double LandHeight => 1 + (Overshoot + 1) * Math.Pow(LandProgress - 1, 3) + Overshoot * Math.Pow(LandProgress - 1, 2);

    public void Restore(bool folded)
    {
        Folded = folded;
        Borrowed = false;
        idle = wait = 0;
        Amount = folded ? 0 : 1;
    }

    public void Fold(double delay = 0)
    {
        Folded = true;
        Borrowed = false;
        wait = delay;
    }

    public void Open(double delay = 0)
    {
        Folded = false;
        Borrowed = false;
        wait = delay;
    }

    public bool Borrow()
    {
        if (Showing) return false;
        Borrowed = true;
        idle = wait = 0;
        return true;
    }

    // Every frame while borrowed. True when the land has just been given back.
    public bool WatchBorrow(bool inUse, double dt)
    {
        if (!Borrowed) return false;
        idle = inUse ? 0 : idle + dt;
        if (idle < BorrowGraceSeconds) return false;
        Borrowed = false;
        return true;
    }

    // True when Amount moved.
    public bool Step(double dt)
    {
        double target = Showing ? 1 : 0;
        if (Amount == target)
        {
            wait = 0;
            return false;
        }
        if (wait > 0)
        {
            wait = Math.Max(0, wait - dt);
            return false;
        }
        double way = dt / (Showing ? OpenSeconds : FoldSeconds);
        Amount = target > Amount ? Math.Min(target, Amount + way) : Math.Max(target, Amount - way);
        return true;
    }
}

public sealed partial class PetFarm
{
    public const string TipDock = "dock";

    public bool FieldFolded { get; set; }
    public bool HearthFolded { get; set; }
    public bool PondFolded { get; set; }
    public bool CoopFolded { get; set; }
    public double? DockFractionX { get; set; }
    public string? DockMonitor { get; set; }

    public bool IsFolded(FarmLandSlot slot) => slot switch
    {
        FarmLandSlot.Field => FieldFolded,
        FarmLandSlot.Hearth => HearthFolded,
        FarmLandSlot.Pond => PondFolded,
        _ => CoopFolded
    };

    public void SetFolded(FarmLandSlot slot, bool folded)
    {
        switch (slot)
        {
            case FarmLandSlot.Field: FieldFolded = folded; break;
            case FarmLandSlot.Hearth: HearthFolded = folded; break;
            case FarmLandSlot.Pond: PondFolded = folded; break;
            default: CoopFolded = folded; break;
        }
    }

    // What is waiting for you on this land, for the mark on the sign. Bare ground waits for nothing.
    public int LandWork(FarmLandSlot slot) => slot switch
    {
        FarmLandSlot.Field => TilesAt(FarmStep.Water).Count + TilesAt(FarmStep.Harvest).Count,
        FarmLandSlot.Hearth => PotDish != null ? 1 : 0,
        FarmLandSlot.Coop => EggsInCoop,
        _ => 0
    };

    private void NormalizeDock()
    {
        if (!HasCoop) CoopFolded = false;
        if (DockFractionX is double x) DockFractionX = double.IsFinite(x) ? Math.Clamp(x, 0, 1) : null;
        if (string.IsNullOrWhiteSpace(DockMonitor)) DockMonitor = null;
    }
}
