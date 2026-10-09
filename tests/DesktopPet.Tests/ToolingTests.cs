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
    }
}
