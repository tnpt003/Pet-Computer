using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

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
    public static readonly TimeSpan CheckEvery = TimeSpan.FromHours(6);

    private readonly UpdateManager? manager;
    private readonly DispatcherTimer timer = new() { Interval = CheckEvery };
    private UpdateInfo? ready;
    private bool checking;

    // Raised on the UI thread when an update has been downloaded (its version), and after a check
    // the user asked for that found nothing (null).
    public event Action<string?>? Changed;

    public PetUpdates()
    {
        try
        {
            var candidate = new UpdateManager(new GithubSource(RepositoryUrl, null, false));
            if (candidate.IsInstalled) manager = candidate;
        }
        catch (Exception ex) { Trace.WriteLine($"Updates are off: {ex.Message}"); }
        timer.Tick += (_, _) => _ = Check(false);
    }

    public bool Available => manager != null;
    public string? ReadyVersion => ready?.TargetFullRelease.Version.ToString();
    public string? CurrentVersion => manager?.CurrentVersion?.ToString();

    public void Start()
    {
        if (manager == null) return;
        timer.Start();
        _ = Check(false);
    }

    public void Stop() => timer.Stop();

    // Asks GitHub; a newer release is downloaded before Changed says so. userAsked: also report
    // "nothing new" (and errors as that) so the tray can answer.
    public async Task Check(bool userAsked)
    {
        if (manager == null || checking) return;
        if (ready != null) { if (userAsked) Changed?.Invoke(ReadyVersion); return; }
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
            if (userAsked) Changed?.Invoke(null);
        }
        finally { checking = false; }
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
