using DesktopPet;

// The shared read/write of data files (PetJsonStore.cs) and the preferences layout version.
static class JsonStoreTests
{
    public static void Run(Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "pet-store-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            string file = Path.Combine(folder, "nested", "data.json");
            check(JsonStore.Read<PetPreferences>(file, "Test") == null, "A missing file reads as nothing");
            check(JsonStore.Write(file, new PetPreferences { Name = "Bơ" }, "Test") && !File.Exists(file + ".tmp") && !File.Exists(file + ".bak"),
                "Writing creates the folder, leaves no temporary file and, without backup, no .bak");
            check(JsonStore.Read<PetPreferences>(file, "Test")?.Name == "Bơ", "What was written reads back");
            File.WriteAllText(file, "null");
            check(JsonStore.Read<PetPreferences>(file, "Test") == null, "A file holding null reads as nothing");
            File.WriteAllText(file, "{ broken");
            check(JsonStore.Read<PetPreferences>(file, "Test") == null, "A broken file reads as nothing instead of throwing");

            string backed = Path.Combine(folder, "backed.json");
            JsonStore.Write(backed, new PetPreferences { Name = "One" }, "Test", indented: true, backup: true);
            JsonStore.Write(backed, new PetPreferences { Name = "Two" }, "Test", indented: true, backup: true);
            check(File.Exists(backed + ".bak") && File.ReadAllText(backed).Contains('\n'), "With backup the previous file is kept as .bak; indented output");
            File.WriteAllText(backed, "{ broken");
            check(JsonStore.ReadOrBackup<PetPreferences>(backed, "Test")?.Name == "One", "A broken file falls back to its backup");

            // Preferences: files from before the layout version read as layout 1 and are saved with it.
            string preferences = Path.Combine(folder, "preferences.json");
            File.WriteAllText(preferences, "{\"Name\":\"Mochi\",\"Quiet\":true}");
            var old = PetPreferences.Load(preferences);
            check(old.Version == PetPreferences.CurrentVersion && old.Quiet, "Preferences without a Version load as the current layout, keeping their settings");
            check(old.Save(preferences) && File.ReadAllText(preferences).Contains($"\"Version\":{PetPreferences.CurrentVersion}"),
                "Saved preferences record their layout version");
            File.WriteAllText(preferences, "null");
            check(PetPreferences.Load(preferences).Name == "Mochi", "Preferences holding null load the defaults");
            File.WriteAllText(Path.Combine(folder, "records.json"), "null");
            check(BattleRecords.Load(Path.Combine(folder, "records.json")).Version == BattleRecords.CurrentVersion,
                "Battle records holding null start over instead of failing");
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }
}
