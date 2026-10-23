using DesktopPet;

static class ToolingTests
{
    public static void Run(Action<bool, string> check)
    {
        // Build identity shown in About and the log (scripts/publish.ps1 adds the commit and ".dirty").
        const string hash = "18cb2270e9e72c4709c41fafd1f211413b876673";
        check(PetLog.VersionLabel("1.0.0", "en") == "1.0.0" && PetLog.VersionLabel($"1.0.0+{hash}", "en") == "1.0.0 (18cb227)" &&
              PetLog.VersionLabel($"1.0.0+{hash}.dirty", "en") == "1.0.0 (18cb227, uncommitted changes)" &&
              PetLog.VersionLabel($"1.0.0+{hash}.dirty", "vi") == "1.0.0 (18cb227, có thay đổi chưa commit)" &&
              PetLog.VersionLabel("1.0.0+", "en") == "1.0.0" && PetLog.VersionLabel(null, "en") == "unknown",
            "The build label names the version, the short commit and uncommitted changes");
        string folder = Path.Combine(Path.GetTempPath(), "pet-log-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var log = new PetLogListener(Path.Combine(folder, "logs", "desktoppet.log"), 2048);
            log.WriteLine("first entry");
            check(File.Exists(log.FilePath) && File.ReadAllText(log.FilePath).Contains("first entry"),
                "Log listener creates its folder and writes timestamped lines");
            log.Write("partial ");
            log.WriteLine("line");
            check(File.ReadAllText(log.FilePath).Contains("partial line"), "Log listener joins Write and WriteLine fragments");
            for (int i = 0; i < 100; i++) log.WriteLine("filler entry " + i);
            check(File.Exists(log.PreviousPath) && new FileInfo(log.FilePath).Length <= 2048 && new FileInfo(log.PreviousPath).Length <= 2048,
                "Log rotates to one previous file and stays bounded");
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }

        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string service = File.ReadAllText(Path.Combine(root, "ai", "service.py"));
        var match = System.Text.RegularExpressions.Regex.Match(service, @"BUSY_STATES = frozenset\(\{([^}]*)\}\)");
        var python = match.Groups[1].Value.Split(',').Select(s => s.Trim().Trim('"')).Where(s => s.Length > 0).OrderBy(s => s);
        var csharp = Enum.GetValues<PetAction>().Where(PetActionRules.RejectsDecisions).Select(a => a.ToString()).OrderBy(s => s);
        check(match.Success && python.SequenceEqual(csharp), "Python BUSY_STATES matches C# actions that reject AI decisions");
        string json = System.Text.Json.JsonSerializer.Serialize(
            new DesktopPet.AI.PetObservation("Idle", true, 3, 1, "test", "LOCAL", LocalHour: 23.5), DesktopPet.AI.AiProtocol.Json);
        check(json.Contains("\"local_hour\":23.5"), "Observation sends the local hour for Python's day rhythm");

        // Uses a throwaway HKCU key, never the real Windows Run key.
        string testKey = @"Software\DesktopPetTest-" + Guid.NewGuid().ToString("N");
        if (OperatingSystem.IsWindows()) try
        {
            var startup = new StartupRegistration(testKey);
            string exe = Path.Combine(Path.GetTempPath(), "Pet Folder", "DesktopPet.exe");
            check(!startup.IsEnabled(exe), "Windows startup is off by default");
            check(startup.SetEnabled(true, exe) && startup.IsEnabled(exe) &&
                  StartupRegistration.Command(exe) == "\"" + exe + "\"", "Enabling startup stores the quoted executable path");
            check(!startup.IsEnabled(Path.Combine(Path.GetTempPath(), "Other", "DesktopPet.exe")),
                "A startup entry for a different copy is not reported as this one");
            check(startup.SetEnabled(false, exe) && !startup.IsEnabled(exe) && startup.SetEnabled(false, exe),
                "Disabling startup removes the entry and is idempotent");
            string other = Path.Combine(Path.GetTempPath(), "Other", "DesktopPet.exe");
            check(startup.SetEnabled(true, other) && startup.RemoveIfThis(exe) && startup.IsEnabled(other),
                "Uninstalling keeps a startup entry that starts another copy");
            check(startup.SetEnabled(true, exe) && startup.RemoveIfThis(exe) && !startup.IsEnabled(exe) && startup.RemoveIfThis(exe),
                "Uninstalling removes this copy's startup entry");
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testKey, false); }

        check(Enum.GetValues<PetAction>().Where(PetActionRules.IsCancellableLocal)
                .SequenceEqual(new[] { PetAction.Grooming, PetAction.Stretching, PetAction.PreparingJump,
                    PetAction.Kneading, PetAction.RollingOver, PetAction.ChasingTail, PetAction.Scratching, PetAction.Sneezing }),
            "Only local self-care, gestures and jump preparation are cancelled straight to Idle");
    }
}
