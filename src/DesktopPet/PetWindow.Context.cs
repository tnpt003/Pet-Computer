using System;
using System.Runtime.InteropServices;
using System.Text;
using DesktopPet.AI;

namespace DesktopPet;

// Computer context and break reminders. Reads only the foreground window's
// rectangle/class and the time of the last system input, never screen content.
public sealed partial class PetWindow
{
    private readonly PetContext context = new();
    private readonly PetBreaks breaks = new();
    private double contextTimer;
    // Replaceable for UI checks; the default asks Windows.
    private Func<(bool Fullscreen, double IdleSeconds)> contextProbe = null!;

    // A fullscreen app on the pet's monitor behaves like Quiet mode without changing the saved setting.
    private bool EffectiveQuiet => preferences.Quiet || context.Fullscreen;

    // The battle setup panel also keeps the cat still (its free time belongs to the setup).
    private void ApplyQuiet() => behavior.Quiet = EffectiveQuiet || battlePreparing;

    private bool CanDeliverBreak => !paused && !dragging && !BattleOwnsPet && !pressPending && !context.Fullscreen && !context.Away &&
        !pet.Speech.Visible && PetSpeech.CanShow(pet);

    private void InitializeContext()
    {
        // PET_CONTEXT_PROBE=off (smoke tests): never fullscreen or away, so a fullscreen app
        // or an idle machine during the test does not quiet the pet.
        contextProbe = Environment.GetEnvironmentVariable("PET_CONTEXT_PROBE") == "off"
            ? () => (false, 0.0) : () => (ForegroundIsFullscreen(), SystemIdleSeconds());
        breaks.Configure(preferences.BreakReminders, preferences.BreakMinutes);
    }

    // Once per second while not paused (Pause freezes the away nap like any sleep).
    private void TickContext(double dt)
    {
        contextTimer += dt;
        if (contextTimer < 1) return;
        double elapsed = contextTimer;
        contextTimer = 0;
        var (fullscreen, idle) = contextProbe();
        var change = context.Update(fullscreen, idle);
        if (change.FullscreenChanged)
        {
            ApplyQuiet();
            System.Diagnostics.Trace.WriteLine(context.Fullscreen ? "Fullscreen app detected: pet is quiet." : "Fullscreen app closed.");
            if (context.Fullscreen)
            {
                if (toyWindow != null || returningHome) CancelFeatureActivity();
                pet.Speech.Hide(); pet.Thought.Clear(); sound?.Stop();
                CloseBattleSummary(); CloseBattleNotice(); CloseBattleTutorial();
            }
        }
        if (change.CameBack) { behavior.EndAwayNap(pet); pet.Emotions.UserReturned(); }
        if (context.Away && !BattleOwnsPet && !behavior.HoldSleep && !dragging && !pressPending && pet.Grounded && !PetActionRules.IsInFlight(pet.Action))
        {
            CancelFeatureActivity();
            behavior.StartAwayNap(pet);
        }
        breaks.Step(elapsed, context.IdleSeconds, CanDeliverBreak);
        if (breaks.NeedsLocalLine)
        {
            var (id, text) = PetBreaks.LocalLine(preferences.Language, preferences.Name, featureRandom);
            ShowSpeech(new PetLine(id, text));
        }
    }

    private ContextReport ContextSnapshot() => new(context.Fullscreen, context.Away, context.TakeReturned());

    private static double SystemIdleSeconds()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return 0;
        return unchecked((uint)Environment.TickCount - info.Time) / 1000.0;
    }

    private bool ForegroundIsFullscreen()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero || !IsWindowVisible(window) || IsIconic(window)) return false;
        GetWindowThreadProcessId(window, out uint process);
        if (process == (uint)Environment.ProcessId) return false;
        var name = new StringBuilder(64);
        GetClassName(window, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        // Ordinary (even maximized) windows keep a caption; fullscreen apps drop it.
        const long Caption = 0x00C00000;
        if ((GetWindowLongPtr(window, -16).ToInt64() & Caption) == Caption) return false;
        if (!GetWindowRect(window, out var rect)) return false;
        IntPtr monitor = MonitorFromWindow(window, 2);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return false;
        var area = info.Monitor;
        if (rect.Left > area.Left || rect.Top > area.Top || rect.Right < area.Right || rect.Bottom < area.Bottom) return false;
        // Only the monitor the pet is on matters.
        var center = new NativePoint { X = (int)((pet.X + settings.Width / 2) * scale), Y = (int)((pet.Y + settings.Height / 2) * scale) };
        return MonitorFromPoint(center, 2) == monitor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo { public uint Size, Time; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public uint Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
