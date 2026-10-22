using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace DesktopPet;

// Opt-in per-user autostart via HKCU\...\Run. The registry value itself is the
// source of truth, so removing it elsewhere (Task Manager > Startup) is respected.
[SupportedOSPlatform("windows")]
public sealed class StartupRegistration(string keyPath = StartupRegistration.RunKey, string valueName = "DesktopPet")
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static string Command(string executable) => "\"" + Path.GetFullPath(executable) + "\"";

    public bool IsEnabled(string executable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName) is string value &&
                string.Equals(value.Trim(), Command(executable), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }

    public bool SetEnabled(bool enabled, string executable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath, true);
            if (enabled) key.SetValue(valueName, Command(executable));
            else if (key.GetValue(valueName) != null) key.DeleteValue(valueName);
            return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }

    // Uninstalling: removes the entry only when it starts this copy (a separate copied folder keeps its own).
    public bool RemoveIfThis(string executable) => !IsEnabled(executable) || SetEnabled(false, executable);
}
