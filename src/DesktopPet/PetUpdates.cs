using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using Velopack;

namespace DesktopPet;

// Self-update (2026-10-22) for the installed app (scripts/pack.ps1 makes the installer and the
// update packages, GitHub Actions puts them on the repository's Releases). On start and every few
// hours it asks GitHub for a newer release and downloads it in the background; Ready then says
// which version waits. The user applies it now (the app closes, updates and opens again) or later:
// a downloaded update is applied the next time the app starts. A copied folder (not installed),
// no network or any error: nothing happens beyond a log line. User data lives elsewhere
// (%LOCALAPPDATA%\DesktopPet), so updating or uninstalling never touches it.
public sealed class PetUpdates
{
    public const string RepositoryUrl = "https://github.com/tnpt003/Pet-Computer";
    // The latest release's files, read as plain downloads (1.4.3): GitHub's API (GithubSource) allows
    // 60 requests an hour per IP without a sign-in, and on a shared IP that ran out before the pet asked.
    public const string FeedUrl = RepositoryUrl + "/releases/latest/download/";
    public static readonly TimeSpan CheckEvery = TimeSpan.FromHours(6);
    // After a failed check, a check the user asks for within this time only says to try later.
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(10);

    private readonly UpdateManager? manager;
    private readonly DispatcherTimer timer = new() { Interval = CheckEvery };
    private UpdateInfo? ready;
    private bool checking;
    private DateTime lastFailure = DateTime.MinValue;

    // Raised on the UI thread when an update has been downloaded (its version), and after a check
    // the user asked for that found nothing (null).
    public event Action<string?>? Changed;
    // Raised after a check the user asked for that could not reach the updates (or was too soon after one that could not).
    public event Action? Failed;

    public static bool TooSoonAfterFailure(DateTime lastFailure, DateTime now) => now - lastFailure < RetryAfterFailure;

    public PetUpdates()
    {
        try
        {
            var candidate = new UpdateManager(FeedUrl);
            if (candidate.IsInstalled) manager = candidate;
        }
        catch (Exception ex) { Trace.WriteLine($"Updates are off: {ex.Message}"); }
        timer.Tick += (_, _) => _ = Check(false);
    }

    public bool Available => manager != null;
    public string? ReadyVersion => ready?.TargetFullRelease.Version.ToString();
    public string? CurrentVersion => manager?.CurrentVersion?.ToString();
    public bool Checking => checking;

    public static string ReleaseUrl(string version) => $"{RepositoryUrl}/releases/tag/v{version}";

    // Automatic (Settings → General): checks on start and every few hours; off, only when the user asks.
    public void Start(bool automatic)
    {
        if (manager == null) return;
        SetAutomatic(automatic);
    }

    public void SetAutomatic(bool automatic)
    {
        if (manager == null) return;
        if (!automatic) { timer.Stop(); return; }
        if (timer.IsEnabled) return;
        timer.Start();
        _ = Check(false);
    }

    public void Stop() => timer.Stop();

    // Asks GitHub; a newer release is downloaded before Changed says so. userAsked: also report
    // "nothing new" (Changed with null) and errors (Failed) so the tray can answer.
    public async Task Check(bool userAsked)
    {
        if (manager == null || checking) return;
        if (ready != null) { if (userAsked) Changed?.Invoke(ReadyVersion); return; }
        if (userAsked && TooSoonAfterFailure(lastFailure, DateTime.UtcNow))
        {
            Trace.WriteLine("Update check skipped: the last one failed moments ago.");
            Failed?.Invoke();
            return;
        }
        checking = true;
        try
        {
            var info = await manager.CheckForUpdatesAsync();
            if (info == null)
            {
                Trace.WriteLine("Update check: up to date.");
                if (userAsked) Changed?.Invoke(null);
                return;
            }
            Trace.WriteLine($"Update check: {info.TargetFullRelease.Version} found, downloading.");
            await manager.DownloadUpdatesAsync(info);
            ready = info;
            Trace.WriteLine($"Update {ReadyVersion} downloaded; applied on restart.");
            Changed?.Invoke(ReadyVersion);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Update check failed: {ex.Message}");
            lastFailure = DateTime.UtcNow;
            if (userAsked) Failed?.Invoke();
        }
        finally { checking = false; }
    }

    // Uninstall from Settings → General (installed app only): runs Velopack's uninstaller, the same as
    // Windows Settings → Apps; the caller then closes the app the normal way. removeData leaves a marker
    // in the install folder that the uninstall hook (OnUninstall) reads to delete the user's data too;
    // any normal start clears a marker left by an uninstall that did not happen.
    public const string RemoveDataMarker = "remove-data-on-uninstall";
    private static string InstallRoot => Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!;

    // Prepares the uninstall; RunPendingUninstall (the app's very last step on exit) starts it, so the
    // pet has closed its tray, Python and log first instead of being stopped by the uninstaller.
    public bool StartUninstall(bool removeData)
    {
        if (manager == null) return false;
        try
        {
            string uninstaller = Path.Combine(InstallRoot, "Update.exe");
            if (!File.Exists(uninstaller)) { Trace.WriteLine("Uninstall: Update.exe not found."); return false; }
            string marker = Path.Combine(InstallRoot, RemoveDataMarker);
            if (removeData) File.WriteAllText(marker, ""); else File.Delete(marker);
            pendingUninstall = uninstaller;
            Trace.WriteLine($"Uninstall requested (remove data: {removeData}); starts when the pet has closed.");
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Uninstall could not start: {ex.Message}");
            return false;
        }
    }

    private string? pendingUninstall;

    public void RunPendingUninstall()
    {
        if (pendingUninstall == null) return;
        try { Process.Start(new ProcessStartInfo(pendingUninstall, "--uninstall") { UseShellExecute = false }); }
        catch (Exception ex) { Trace.WriteLine($"Uninstall could not start: {ex.Message}"); }
    }

    public void ForgetUninstallChoice()
    {
        if (manager == null) return;
        try { File.Delete(Path.Combine(InstallRoot, RemoveDataMarker)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    // Velopack's before-uninstall hook (its own short-lived process): drop this copy's Windows startup
    // entry, and delete the user's data only when the in-app uninstall asked for it.
    public static void OnUninstall() => OnUninstall(InstallRoot, PetPreferences.DataDirectory, Environment.ProcessPath);

    public static bool OnUninstall(string installRoot, string dataDirectory, string? executable)
    {
        if (executable != null) new StartupRegistration().RemoveIfThis(executable);
        string marker = Path.Combine(installRoot, RemoveDataMarker);
        if (!File.Exists(marker)) return false;
        File.Delete(marker);
        string data = Path.GetFullPath(dataDirectory);
        // Never a drive root or the install folder itself.
        if (Path.GetPathRoot(data) == data || string.Equals(data.TrimEnd('\\'), Path.GetFullPath(installRoot).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return false;
        // The closing pet may still hold its log for a moment: retry briefly (the hook has 30 s).
        for (int attempt = 0; attempt < 10 && Directory.Exists(data); attempt++)
        {
            try { Directory.Delete(data, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { System.Threading.Thread.Sleep(500); }
        }
        return !Directory.Exists(data);
    }

    // Starts the updater, which waits for this app to exit, applies the update and opens the new
    // version. The caller then closes the app the normal way (Python and the tray included).
    public bool ApplyAndRestart()
    {
        if (manager == null || ready == null) return false;
        try
        {
            manager.WaitExitThenApplyUpdates(ready.TargetFullRelease, silent: true, restart: true);
            Trace.WriteLine($"Updating to {ReadyVersion}: closing for the updater.");
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Update could not start: {ex.Message}");
            return false;
        }
    }
}
