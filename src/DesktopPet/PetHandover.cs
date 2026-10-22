using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopPet;

// First run after installing only: another copy (a copied folder, any version) still running would
// keep the single-instance lock, and the new install would just open that copy's controls and quit.
// Instead the other copy is closed the normal way (WM_CLOSE to its pet window, as its Exit does),
// so it saves and stops its Python service, and the installed pet takes over.
public static class PetHandover
{
    // True when every other pet in this session closed within the wait.
    public static bool CloseOtherCopies(TimeSpan wait)
    {
        var own = Process.GetCurrentProcess();
        var others = Process.GetProcessesByName("DesktopPet").Where(p => p.Id != own.Id && p.SessionId == own.SessionId).ToList();
        if (others.Count == 0) return false;
        foreach (var other in others)
        {
            IntPtr window = PetWindowOf(other.Id);
            Trace.WriteLine($"First run: closing the other running pet ({other.Id}).");
            if (window != IntPtr.Zero) PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
        }
        var deadline = DateTime.UtcNow + wait;
        foreach (var other in others)
        {
            int left = Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
            try { if (!other.WaitForExit(left)) return false; }
            catch (InvalidOperationException) { }
        }
        return true;
    }

    // The pet's own window: its title ends with "Desktop Pet" (speech bubbles and menus have none).
    private static IntPtr PetWindowOf(int processId)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint process);
            if (process != (uint)processId || !IsWindowVisible(window)) return true;
            var title = new StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            if (!title.ToString().EndsWith("Desktop Pet", StringComparison.Ordinal)) return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
