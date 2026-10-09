using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DesktopPet;

// Persists existing Trace diagnostics (AI fallback, sound errors, crashes) so a
// published copy can be investigated. One bounded file plus one previous copy.
public sealed class PetLogListener : TraceListener
{
    private readonly object sync = new();
    private readonly string path;
    private readonly long maxBytes;
    private readonly StringBuilder pending = new();

    public PetLogListener(string path, long maxBytes = 1024 * 1024)
    {
        this.path = path;
        this.maxBytes = Math.Max(1024, maxBytes);
        Name = "DesktopPetFile";
    }

    public string FilePath => path;
    public string PreviousPath => Path.ChangeExtension(path, ".old.log");

    public override void Write(string? message)
    {
        lock (sync) pending.Append(message);
    }

    public override void WriteLine(string? message)
    {
        string line;
        lock (sync)
        {
            pending.Append(message);
            line = pending.ToString();
            pending.Clear();
        }
        Append(line);
    }

    private void Append(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        string text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message.TrimEnd()}{Environment.NewLine}";
        lock (sync)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var info = new FileInfo(path);
                if (info.Exists && info.Length + text.Length > maxBytes) File.Move(path, PreviousPath, true);
                File.AppendAllText(path, text, Encoding.UTF8);
            }
            // Logging must never break the pet.
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}

public static class PetLog
{
    public static PetLogListener? Install()
    {
        try
        {
            var listener = new PetLogListener(Path.Combine(PetPreferences.DataDirectory, "logs", "desktoppet.log"));
            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;
            Trace.WriteLine($"Desktop Pet {typeof(PetLog).Assembly.GetName().Version} started");
            return listener;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }
}
