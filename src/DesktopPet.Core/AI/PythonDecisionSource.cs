using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopPet.AI;

// One request in flight. All process/pipe operations run off the WPF dispatcher.
public sealed class PythonDecisionSource : IPetDecisionSource
{
    private readonly object sync = new();
    private readonly SemaphoreSlim gate = new(1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly string script;
    private readonly string? executable;
    private readonly string memoryPath;
    private readonly TimeSpan timeout;
    private readonly TimeSpan initialBackoff;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private Process? process;
    private bool disposed;
    private long requestId;
    private int failures;
    private double retryAt;
    private volatile string status = "Python AI: starting on next decision";
    public string Status => status;
    internal int? ProcessId { get { lock (sync) return process is { HasExited: false } ? process.Id : null; } }

    public PythonDecisionSource(string? scriptPath = null, string? pythonExecutable = null,
        string? memoryFile = null, TimeSpan? requestTimeout = null, TimeSpan? retryDelay = null)
    {
        script = scriptPath ?? Path.Combine(AppContext.BaseDirectory, "ai", "service.py");
        executable = pythonExecutable ?? Environment.GetEnvironmentVariable("PET_PYTHON");
        memoryPath = memoryFile ?? Path.Combine(PetPreferences.DataDirectory, "ai-memory.json");
        timeout = requestTimeout ?? TimeSpan.FromSeconds(2);
        initialBackoff = retryDelay ?? TimeSpan.FromSeconds(2);
    }

    public Task<AiDecision?> DecideAsync(PetObservation observation) => Task.Run(async () =>
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (sync)
                if (disposed || clock.Elapsed.TotalSeconds < retryAt) return null;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            deadline.CancelAfter(timeout);
            try
            {
                var child = EnsureProcess();
                long id = ++requestId;
                string request = JsonSerializer.Serialize(new { version = AiProtocol.Version, id, type = "observe", observation }, AiProtocol.Json);
                await child.StandardInput.WriteLineAsync(request.AsMemory(), deadline.Token).ConfigureAwait(false);
                await child.StandardInput.FlushAsync(deadline.Token).ConfigureAwait(false);
                string response = await ReadMessage(child.StandardOutput, deadline.Token).ConfigureAwait(false);
                var decision = AiProtocol.Parse(response, id);
                failures = 0;
                status = $"Python AI: connected ({decision.Mood})";
                return decision;
            }
            catch (Exception ex) when (ex is IOException or Win32Exception or InvalidOperationException or
                OperationCanceledException or JsonException or System.Collections.Generic.KeyNotFoundException or
                FormatException or OverflowException or UnauthorizedAccessException)
            {
                lock (sync)
                {
                    StopProcess();
                    if (!disposed)
                    {
                        double delay = Math.Min(30, initialBackoff.TotalSeconds * Math.Pow(2, Math.Min(failures++, 5)));
                        retryAt = clock.Elapsed.TotalSeconds + delay;
                        string reason = ex is OperationCanceledException ? "timeout" : ex.Message;
                        status = $"Local behavior: {reason} (retry after {delay:0}s)";
                        Trace.WriteLine(status);
                    }
                }
                return null;
            }
        }
        finally { gate.Release(); }
    });

    private Process EnsureProcess()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (process is { HasExited: false }) return process;
            StopProcess();
            if (!File.Exists(script)) throw new FileNotFoundException("Python service script is missing.", script);
            string bundled = Path.Combine(AppContext.BaseDirectory, "python", "python.exe");
            string[] candidates = !string.IsNullOrWhiteSpace(executable) ? new[] { executable } :
                File.Exists(bundled) ? new[] { bundled } : new[] { "py", "python" };
            Exception? last = null;
            foreach (string candidate in candidates)
            {
                var start = new ProcessStartInfo(candidate)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
                    WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(script))!
                };
                if (Path.GetFileNameWithoutExtension(candidate).Equals("py", StringComparison.OrdinalIgnoreCase))
                    start.ArgumentList.Add("-3");
                start.ArgumentList.Add("-u");
                start.ArgumentList.Add(Path.GetFullPath(script));
                start.ArgumentList.Add("--memory");
                start.ArgumentList.Add(memoryPath);
                start.Environment["PYTHONIOENCODING"] = "utf-8";
                var child = new Process { StartInfo = start };
                try
                {
                    child.Start();
                    process = child;
                    _ = DrainErrors(child.StandardError);
                    return child;
                }
                catch (Win32Exception ex) { last = ex; child.Dispose(); }
            }
            throw new IOException("Python was not found. Set PET_PYTHON to python.exe.", last);
        }
    }

    private static async Task<string> ReadMessage(StreamReader reader, CancellationToken token)
    {
        // Bound response size before allocating an arbitrary-length line.
        var line = new StringBuilder();
        var character = new char[1];
        while (line.Length <= AiProtocol.MaxMessageLength)
        {
            if (await reader.ReadAsync(character.AsMemory(), token).ConfigureAwait(false) == 0)
                throw new IOException("Python exited or closed its output.");
            if (character[0] == '\n') return line.ToString();
            line.Append(character[0]);
        }
        throw new IOException("Python response exceeded the message limit.");
    }

    private static async Task DrainErrors(StreamReader reader)
    {
        // Drain concurrently to prevent a full stderr pipe from blocking Python.
        var buffer = new char[1024];
        try
        {
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
                Trace.WriteLine(new string(buffer, 0, count));
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
    }

    // Caller holds sync. Only this bridge's child is ever terminated.
    private void StopProcess()
    {
        if (process == null) return;
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { Trace.WriteLine(ex.Message); }
        finally { process.Dispose(); process = null; }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            // EOF lets Python flush its small memory file. Bound shutdown even if hung.
            if (process != null)
            {
                try { process.StandardInput.Close(); process.WaitForExit(250); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
            }
            StopProcess();
            status = "Python AI: stopped";
        }
        // In-flight tasks own the cancellation/semaphore until they unwind.
    }
}
