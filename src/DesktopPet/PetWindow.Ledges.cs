using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using DesktopPet.AI;

namespace DesktopPet;

// Walking on windows. Reads only other windows' rectangles, styles and z-order (never
// their content), on the pet's monitor. Physics stays in PetPhysics; this file keeps the
// ledge list fresh, carries the pet along with a moved window, and starts the
// autonomous trips up (leap or climb) and down.
public sealed partial class PetWindow
{
    private List<Ledge> ledges = new();
    private double ledgeScan, ledgeIdea = 20, ledgeStay, ledgeLeaveAfter = 90, ledgeExitTime;
    // Walking to the base of a window side before climbing it.
    private (long Id, int Side, double StartX)? climbPlan;
    // Walking off the current ledge (-1 left, +1 right; 0 none).
    private int ledgeExit;
    // Replaceable for UI checks; the defaults ask Windows.
    private Func<IReadOnlyList<WindowSnapshot>> windowProbe = null!;
    private Func<long, WindowSnapshot?> windowRectProbe = null!;
    private const double LedgeScanSeconds = 0.3;

    // Battle Mode fights on the floor of one monitor, so windows are not ground meanwhile.
    private bool LedgesEnabled => preferences.ClimbWindows && !context.Fullscreen && !BattleActive;
    private bool OnLedge => pet.LedgeId != 0 && pet.Grounded;

    private void InitializeLedges()
    {
        windowProbe = ProbeWindows;
        windowRectProbe = ProbeWindow;
    }

    // Every unpaused frame, before physics.
    private void TickLedges(double dt)
    {
        if (!LedgesEnabled)
        {
            if (ledges.Count > 0 || climbPlan != null || ledgeExit != 0 || pet.Action == PetAction.Climbing) DropLedges();
            return;
        }
        FollowTrackedWindow();
        ledgeScan -= dt;
        if (ledgeScan <= 0)
        {
            ledgeScan = LedgeScanSeconds;
            ledges = PetLedges.Build(windowProbe(), bounds, settings.Width, settings.Height);
        }
        ledgeStay = OnLedge ? ledgeStay + dt : 0;
        TickLedgePlans(dt);
    }

    // The window under (or beside) the pet moved: move the pet with it in the same frame.
    private void FollowTrackedWindow()
    {
        long id = OnLedge ? pet.LedgeId : pet.Action == PetAction.Climbing ? pet.ClimbId : 0;
        if (id == 0) return;
        int index = ledges.FindIndex(l => l.Id == id);
        if (index < 0) return;
        if (windowRectProbe(id) is not WindowSnapshot now)
        {
            ledges.RemoveAll(l => l.Id == id); // Closed, minimized or maximized: physics drops the pet.
            return;
        }
        double dx = now.Left - ledges[index].WindowLeft, dy = now.Top - ledges[index].Top;
        if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01) return;
        for (int i = 0; i < ledges.Count; i++) if (ledges[i].Id == id) ledges[i] = ledges[i].Shifted(dx, dy);
        pet.X += dx; pet.Y += dy;
        previousX += dx; previousY += dy; // Keep the drawn position smooth.
    }

    private void TickLedgePlans(double dt)
    {
        bool free = CanPlay && !EffectiveQuiet && queuedCommand == null && toyWindow == null && laserWindow == null &&
            !returningHome && !admiring && keepsakeShove == KeepsakeShove.None && cushionNap == CushionNap.None && pet.TreatRemaining <= 0 && treatSatisfiedRemaining <= 0;
        if (!free) { climbPlan = null; ledgeExit = 0; return; }
        if (climbPlan is var (id, side, startX))
        {
            if (PetLedges.Wall(ledges, id, side) == null || OnLedge) { climbPlan = null; return; }
            if (Math.Abs(pet.X - startX) > 3) { WalkToward(startX); return; }
            pet.X = startX; pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
            pet.Action = PetAction.Climbing; pet.Grounded = false;
            pet.ClimbId = id; pet.ClimbSide = side; pet.Facing = -side;
            climbPlan = null;
            return;
        }
        if (ledgeExit != 0)
        {
            ledgeExitTime += dt;
            if (!OnLedge || ledgeExitTime > 10) { ledgeExit = 0; return; }
            WalkToward(pet.X + ledgeExit * 200); // Physics lets this walk step off the end.
            return;
        }
        ledgeIdea -= dt;
        if (ledgeIdea > 0 || pet.Action is not (PetAction.Idle or PetAction.Walking or PetAction.Sitting or PetAction.Looking)) return;
        double pace = preferences.Personality == PetPersonality.Playful ? 0.6 : preferences.Personality == PetPersonality.Calm ? 1.6 : 1;
        ledgeIdea = (30 + featureRandom.NextDouble() * 30) * pace;
        if (OnLedge && ledgeStay >= ledgeLeaveAfter)
        {
            // Down again: walk off the nearer end.
            var ledge = PetLedges.Support(ledges, pet.LedgeId, pet.X + settings.Width / 2);
            if (ledge is not Ledge here) return;
            double center = pet.X + settings.Width / 2;
            ledgeExit = center - here.Left < here.Right - center ? -1 : 1;
            ledgeExitTime = 0;
            ledgeLeaveAfter = (60 + featureRandom.NextDouble() * 60) * pace;
            return;
        }
        if (PetLedges.PlanJump(ledges, pet, bounds, settings) is var (_, vx, vy))
        {
            if (behavior.StartLeap(pet, vx, vy)) ledgeLeaveAfter = (60 + featureRandom.NextDouble() * 60) * pace;
            return;
        }
        if (!OnLedge && PetLedges.PlanClimb(ledges, pet, settings) is var (target, climbSide, climbStart))
        {
            climbPlan = (target.Id, climbSide, climbStart);
            ledgeLeaveAfter = (60 + featureRandom.NextDouble() * 60) * pace;
        }
    }

    // Walks that physics may carry past a ledge's end: games, the resting spot, leaving.
    private bool MayLeaveLedge => FeatureOwnsMovement() || ledgeExit != 0;

    // Feature off or a fullscreen app: no ledges. A pet on one falls; a climbing one lets go.
    private void DropLedges()
    {
        ledges.Clear();
        climbPlan = null; ledgeExit = 0;
        PetPhysics.LetGo(pet);
    }

    private IReadOnlyList<WindowSnapshot> ProbeWindows()
    {
        var list = new List<WindowSnapshot>();
        var center = new NativePoint { X = (int)((pet.X + settings.Width / 2) * scale), Y = (int)((pet.Y + settings.Height / 2) * scale) };
        IntPtr petMonitor = MonitorFromPoint(center, 2);
        uint own = (uint)Environment.ProcessId;
        var name = new StringBuilder(64);
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || IsIconic(window)) return true;
            GetWindowThreadProcessId(window, out uint process);
            if (process == own || IsCloaked(window)) return true;
            long exStyle = GetWindowLongPtr(window, -20).ToInt64();
            if ((exStyle & 0x20) != 0) return true; // WS_EX_TRANSPARENT: click-through overlays cover nothing.
            name.Clear(); GetClassName(window, name, name.Capacity);
            string className = name.ToString();
            if (className is "Progman" or "WorkerW") return true; // The desktop itself.
            if (!TryFrame(window, out var rect) || rect.Right <= rect.Left || rect.Bottom <= rect.Top) return true;
            // Ordinary app windows (as in Alt+Tab): not tool windows, unowned (or app windows), titled, not maximized.
            bool candidate = (exStyle & 0x80) == 0 && (GetWindow(window, 4) == IntPtr.Zero || (exStyle & 0x40000) != 0) &&
                GetWindowTextLength(window) > 0 && !IsZoomed(window) &&
                className is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd") && MonitorFromWindow(window, 2) == petMonitor;
            list.Add(new WindowSnapshot(window.ToInt64(), rect.Left / scale, rect.Top / scale, rect.Right / scale, rect.Bottom / scale, candidate));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    // The one window the pet stands on, every frame; null once it cannot hold the pet.
    private WindowSnapshot? ProbeWindow(long id)
    {
        var window = new IntPtr(id);
        if (!IsWindow(window) || !IsWindowVisible(window) || IsIconic(window) || IsZoomed(window) || IsCloaked(window)) return null;
        if (!TryFrame(window, out var rect)) return null;
        return new WindowSnapshot(id, rect.Left / scale, rect.Top / scale, rect.Right / scale, rect.Bottom / scale, true);
    }

    // The visible frame (DWM), without the invisible resize borders of Windows 10/11.
    private static bool TryFrame(IntPtr window, out NativeRect rect)
    {
        if (DwmGetWindowAttribute(window, 9, out rect, Marshal.SizeOf<NativeRect>()) == 0) return true;
        return GetWindowRect(window, out rect);
    }

    private static bool IsCloaked(IntPtr window) =>
        DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr window);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
}
