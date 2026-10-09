using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace DesktopPet.AI;

public sealed record PetObservation(
    string State, bool Grounded, double SecondsSinceInteraction,
    long InteractionCount, string LastEvent, string LastAppliedCommand, string Emotion = "Neutral",
    PetActivity? Activity = null, string Personality = "Balanced", string SessionId = "", PersonalityTuning? Tuning = null,
    double? LocalHour = null);

public sealed record AiDecision(string Command, double DurationSeconds, string Mood, string? Emotion = null, double? Energy = null);

public interface IPetDecisionSource : IDisposable
{
    string Status { get; }
    Task<AiDecision?> DecideAsync(PetObservation observation);
}

public static class AiProtocol
{
    public const int Version = 1;
    public const int MaxMessageLength = 8192;
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static bool IsCommand(string? command) => command is
        "LOOK" or "IDLE" or "WALK_LEFT" or "WALK_RIGHT" or "SIT" or "JUMP" or "SLEEP" or "FOLLOW_MOUSE" or "SEEK_ATTENTION";

    public static AiDecision Parse(string line, long requestId)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != Version ||
            root.GetProperty("id").GetInt64() != requestId ||
            root.GetProperty("type").GetString() != "decision")
            throw new JsonException("Mismatched protocol version, request ID, or response type.");
        var command = root.GetProperty("command").GetString();
        double duration = root.GetProperty("duration_seconds").GetDouble();
        var mood = root.GetProperty("mood").GetString();
        if (!IsCommand(command) || !double.IsFinite(duration) || duration < 0.5 || duration > 12 ||
            mood is not ("content" or "playful" or "bored" or "sleepy"))
            throw new JsonException("Invalid command, duration, or mood.");
        string? emotion = root.TryGetProperty("emotion", out var hint) ? hint.GetString() : null;
        if (emotion is not (null or "neutral" or "sad" or "sleepy")) throw new JsonException("Invalid long-term emotion hint.");
        double? energy = root.TryGetProperty("energy", out var value) && value.TryGetDouble(out double parsed)
            && double.IsFinite(parsed) && parsed >= 0 && parsed <= 100 ? parsed : null;
        return new AiDecision(command!, duration, mood!, emotion, energy);
    }
}
