using DesktopPet;

static class ToolingTests
{
    public static void Run(Action<bool, string> check)
    {
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
        check(Enum.GetValues<PetAction>().Where(PetActionRules.IsCancellableLocal)
                .SequenceEqual(new[] { PetAction.Grooming, PetAction.Stretching, PetAction.PreparingJump }),
            "Only local self-care and jump preparation are cancelled straight to Idle");
    }
}
