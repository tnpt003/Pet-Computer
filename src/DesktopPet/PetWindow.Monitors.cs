using System;
using System.Linq;
using System.Windows;
using Forms = System.Windows.Forms;

namespace DesktopPet;

public sealed partial class PetWindow
{
    private Forms.Screen CurrentMonitor() => Forms.Screen.FromPoint(new System.Drawing.Point(
        (int)((pet.X + settings.Width / 2) * scale), (int)((pet.Y + settings.Height / 2) * scale)));

    // A monitor's own DPI scale (PerMonitorV2), read without moving any window; the pet's scale
    // if Windows cannot say.
    private double MonitorScale(Forms.Screen screen)
    {
        var area = screen.Bounds;
        var handle = MonitorFromPoint(new NativePoint { X = area.Left + area.Width / 2, Y = area.Top + area.Height / 2 }, 2);
        try { if (handle != IntPtr.Zero && GetDpiForMonitor(handle, 0, out uint dpi, out _) == 0 && dpi > 0) return dpi / 96.0; }
        catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
        return scale;
    }

    [System.Runtime.InteropServices.DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);

    public void ShowControls()
    {
        if (ContextMenu == null) return;
        ContextMenu.PlacementTarget = this;
        ContextMenu.IsOpen = true;
    }

    private void ChangeMonitorScale(double newScale)
    {
        if (!double.IsFinite(newScale) || newScale <= 0 || Math.Abs(scale - newScale) < 0.001) return;
        double ratio = scale / newScale;
        pet.X *= ratio; pet.Y *= ratio; pet.VX *= ratio; pet.VY *= ratio;
        pet.SmoothedWalkVelocity *= ratio;
        dragOffset = new Point(dragOffset.X * ratio, dragOffset.Y * ratio);
        pressPosition = new Point(pressPosition.X * ratio, pressPosition.Y * ratio);
        tracker.Clear(); // Samples from two DPI coordinate systems cannot form a throw.
        scale = newScale;
        pet.Motion.Reset(pet);
        CancelFeatureActivity();
        // Window edges were measured at the old scale: back to the floor, then rescan.
        ledges.Clear(); ledgeScan = 0; pet.LedgeId = 0;
        SelectMonitor(); physics.Clamp(pet, bounds);
        if (pet.Grounded) pet.Y = bounds.Bottom - settings.Height;
        SnapPresentation();
    }

    private void RestorePosition()
    {
        if (preferences.PositionMonitor == null) return;
        var monitor = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == preferences.PositionMonitor)
            ?? Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
        bounds = WorkArea(monitor);
        pet.X = bounds.Left + preferences.PositionX * Math.Max(0, bounds.Right - bounds.Left - settings.Width);
        pet.Y = bounds.Top + preferences.PositionY * Math.Max(0, bounds.Bottom - bounds.Top - settings.Height);
        pet.Grounded = Math.Abs(pet.Y - (bounds.Bottom - settings.Height)) < 1;
        if (!pet.Grounded) pet.Action = PetAction.Thrown;
        RefreshRestingSpot(); Present();
    }

    private void SavePosition()
    {
        preferences.PositionMonitor = CurrentMonitor().DeviceName;
        var area = WorkArea(CurrentMonitor());
        preferences.PositionX = Math.Clamp((pet.X - area.Left) / Math.Max(1, area.Right - area.Left - settings.Width), 0, 1);
        preferences.PositionY = Math.Clamp((pet.Y - area.Top) / Math.Max(1, area.Bottom - area.Top - settings.Height), 0, 1);
        if (persistPreferences) preferences.Save();
    }

    private void RefreshRestingSpot()
    {
        if (preferences.RestMonitor == null) return; // Existing coordinate-based preferences remain supported.
        var monitor = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == preferences.RestMonitor);
        if (monitor == null) monitor = Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
        var area = WorkArea(monitor);
        preferences.RestX = area.Left + preferences.RestFractionX * Math.Max(0, area.Right - area.Left - settings.Width);
        preferences.RestY = area.Bottom - settings.Height;
    }
}
