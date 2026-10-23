using System.Diagnostics;
using System.Text.Json;
using DesktopPet;
using DesktopPet.AI;

internal static class AiTests
{
    public static async Task Run(Action<bool, string> check)
    {
        var settings = new PetSettings();
        var behavior = new PetBehavior(settings);
        var pet = new PetModel { X = 100, Y = 928, Grounded = true };
        foreach (var (command, action) in new[] {
            ("IDLE", PetAction.Idle), ("WALK_LEFT", PetAction.Walking), ("WALK_RIGHT", PetAction.Walking),
            ("SIT", PetAction.Sitting), ("SLEEP", PetAction.Sleeping), ("FOLLOW_MOUSE", PetAction.Walking),
            ("JUMP", PetAction.PreparingJump), ("SEEK_ATTENTION", PetAction.SeekingAttention) })
        {
            pet.Grounded = true;
            pet.Action = PetAction.Idle;
            pet.Emotions.Reset();
            check(behavior.TryApply(pet, new(command, 2, "content")) && pet.Action == action,
                $"AI {command} maps to the existing C# action");
            if (command == "JUMP")
            {
                check(pet.Grounded && pet.VY == 0, "Autonomous jump prepares while planted");
                behavior.Step(pet, PetSettings.JumpPreparationDuration + 0.001);
                check(pet.VY == -settings.JumpSpeed && !pet.Grounded, "C# executes the unchanged jump impulse after preparation");
            }
        }
        pet.Grounded = true;
        pet.Action = PetAction.Idle;
        behavior.TryApply(pet, new("FOLLOW_MOUSE", 2, "content"));
        behavior.Step(pet, 0.1, 0);
        check(pet.VX == -settings.WalkSpeed, "C# follows a cursor to the left");
        behavior.Step(pet, 0.1, pet.X + settings.Width / 2);
        check(pet.VX == 0 && pet.Action == PetAction.Idle, "Follow mouse stops in the cursor dead zone");
        behavior.Step(pet, 0.1, 1000);
        check(pet.VX == settings.WalkSpeed, "C# follows a cursor to the right");
        check(!behavior.TryApply(pet, new("TELEPORT", 2, "content")) &&
            !behavior.TryApply(pet, new("IDLE", double.NaN, "content")), "Unknown and nonfinite commands rejected");
        foreach (var state in new[] { PetAction.Dragging, PetAction.Thrown, PetAction.Jumping })
        {
            pet.Action = state;
            pet.VX = 321;
            check(!behavior.TryApply(pet, new("SLEEP", 2, "content")) && pet.VX == 321,
                $"AI cannot override {state}");
        }
        pet.Action = PetAction.Idle;
        check(!DecisionGuard.CanApply(1, 2, 0.1, false, pet), "Stale response discarded after user interaction");
        check(!DecisionGuard.CanApply(1, 1, 0.1, true, pet), "AI cannot override pause");
        check(!DecisionGuard.CanApply(1, 1, 3, false, pet), "Expired response discarded");
        pet.Grounded = false;
        check(!DecisionGuard.CanApply(1, 1, 0.1, false, pet), "Airborne pet rejects AI decisions");
        pet.Grounded = true;
        int requests = 0;
        behavior.Reset();
        behavior.DecisionRequested += () => requests++;
        behavior.Step(pet, 3);
        check(requests == 1 && behavior.LastAppliedCommand == "LOCAL", "Local behavior continues without an AI response");

        bool Invalid(string json)
        {
            try { AiProtocol.Parse(json, 1); return false; }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return true; }
        }
        check(Invalid("{\"version\":1,\"id\":2,\"type\":\"decision\"}"), "Mismatched response ID rejected");
        check(Invalid("{\"version\":99,\"id\":1,\"type\":\"decision\"}"), "Unsupported protocol rejected");

        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string script = Path.Combine(root, "ai", "service.py");
        using (var missing = new PythonDecisionSource(script, Path.Combine(root, "missing-python.exe")))
            check(await missing.DecideAsync(Observation()) == null && missing.Status.StartsWith("Local behavior"),
                "Missing Python fails safely to local behavior");

        string? python = Environment.GetEnvironmentVariable("PET_TEST_PYTHON");
        if (string.IsNullOrEmpty(python))
        {
            Console.WriteLine("SKIP: Python integration tests. Set PET_TEST_PYTHON to a real python.exe to enable.");
            return;
        }
        // Names both sides must agree on (AiNames.cs ↔ ai/service.py; BUSY_STATES: ToolingTests).
        var names = PythonNames(python, root);
        static bool Same(IEnumerable<string> a, IEnumerable<string> b) => a.Order().SequenceEqual(b.Order());
        check(names != null && Same(names["commands"], AiCommand.All), "C# and Python know the same AI commands");
        check(names != null && Same(names["moods"], AiMood.All), "C# and Python know the same moods");
        check(names != null && Same(names["personalities"], PetPersonality.All), "C# and Python know the same personalities");
        string temporary = Path.Combine(root, ".tools", "ai-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        var source = new PythonDecisionSource(script, python, Path.Combine(temporary, "memory.json"),
            retryDelay: TimeSpan.FromMilliseconds(80));
        try
        {
            check(await source.DecideAsync(Observation()) != null, "Real Python service returns a valid decision");
            int pid = source.ProcessId!.Value;
            using (var child = Process.GetProcessById(pid)) { child.Kill(); await child.WaitForExitAsync(); }
            check(await source.DecideAsync(Observation()) != null, "Bridge restarts an exited Python process");
            pid = source.ProcessId!.Value;
            using var restarted = Process.GetProcessById(pid);
            source.Dispose();
            await restarted.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
            check(restarted.HasExited, "Stopping bridge stops its owned Python process");
            check(await source.DecideAsync(Observation()) == null, "Disposed bridge cannot restart Python");
        }
        finally { source.Dispose(); }

        string fixture = Path.Combine(root, "tests", "ai", "fault_service.py");
        foreach (string mode in new[] { "hang", "malformed", "wrong_id", "oversize", "exit" })
        {
            using var failing = new PythonDecisionSource(fixture, python, Path.Combine(temporary, mode),
                requestTimeout: TimeSpan.FromSeconds(1), retryDelay: TimeSpan.FromMilliseconds(80));
            var watch = Stopwatch.StartNew();
            check(await failing.DecideAsync(Observation()) == null && failing.ProcessId == null,
                $"{mode}: fallback and failed-child cleanup");
            check(watch.Elapsed < TimeSpan.FromSeconds(3), $"{mode}: response failure is bounded");
            check(await failing.DecideAsync(Observation()) == null && failing.ProcessId == null,
                $"{mode}: backoff prevents immediate restart");
        }
        using (var noisy = new PythonDecisionSource(fixture, python, Path.Combine(temporary, "stderr")))
            check(await noisy.DecideAsync(Observation()) != null, "Large stderr output cannot deadlock IPC");
        using (var recovering = new PythonDecisionSource(fixture, python, Path.Combine(temporary, "recover"),
            retryDelay: TimeSpan.FromMilliseconds(80)))
        {
            check(await recovering.DecideAsync(Observation()) == null, "First failed response falls back");
            await Task.Delay(100);
            check(await recovering.DecideAsync(Observation()) != null, "Service recovers automatically after backoff");
        }
        Console.WriteLine("AI behavior and integration checks passed.");
    }

    private static PetObservation Observation() => new("Idle", true, 10, 0, "started", "LOCAL");

    // Python's shared name lists, read by importing ai/service.py; null if that fails.
    private static Dictionary<string, string[]>? PythonNames(string python, string root)
    {
        const string code = "import json, sys; sys.path.insert(0, sys.argv[1]); import service as s; "
            + "print(json.dumps({'commands': sorted(s.COMMANDS), 'moods': list(s.MOODS), 'personalities': list(s.PERSONALITIES)}))";
        var start = new ProcessStartInfo(python) { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (string arg in new[] { "-I", "-c", code, Path.Combine(root, "ai") }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start);
        if (process == null) return null;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? JsonSerializer.Deserialize<Dictionary<string, string[]>>(output) : null;
    }
}
