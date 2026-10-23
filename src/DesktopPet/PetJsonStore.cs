using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DesktopPet;

// Reading and writing the pet's JSON data files (preferences, records, progress). A write goes to
// "<file>.tmp" first and is then swapped in, so a crash mid-write never leaves half a file;
// with backup the previous file is kept as "<file>.bak" (ReadOrBackup falls back to it).
public static class JsonStore
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    // The file's contents, or null when it is missing, unreadable, not valid JSON or "null"
    // (logged, except a missing file). fixup sees the raw root, e.g. to tell an old layout apart.
    public static T? Read<T>(string path, string what, Action<T, JsonElement>? fixup = null) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var value = document.RootElement.Deserialize<T>() ?? throw new JsonException("empty file");
            fixup?.Invoke(value, document.RootElement);
            return value;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.WriteLine($"{what} {path} could not be read: {ex.Message}");
            return null;
        }
    }

    // Read, or the backup Write(backup: true) left when the main file cannot be read.
    public static T? ReadOrBackup<T>(string path, string what, Action<T, JsonElement>? fixup = null) where T : class
    {
        if (Read(path, what, fixup) is T value) return value;
        if (Read(path + ".bak", what, fixup) is not T backup) return null;
        Trace.WriteLine($"{what} was read from its backup.");
        return backup;
    }

    public static bool Write<T>(string path, T value, string what, bool indented = false, bool backup = false)
    {
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, indented ? JsonSerializer.Serialize(value, Indented) : JsonSerializer.Serialize(value));
            if (backup && File.Exists(path)) File.Replace(temp, path, path + ".bak", true);
            else File.Move(temp, path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"{what} could not be saved: {ex.Message}");
            return false;
        }
    }
}
