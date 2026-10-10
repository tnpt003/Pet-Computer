using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DesktopPet.AI;

public sealed record PetObservation(
    string State, bool Grounded, double SecondsSinceInteraction,
    long InteractionCount, string LastEvent, string LastAppliedCommand, string Emotion = "Neutral",
    PetActivity? Activity = null, string Personality = "Balanced", string SessionId = "", PersonalityTuning? Tuning = null,
    double? LocalHour = null, PetInteractions? Interactions = null,
    bool SpeechEnabled = false, string Language = "vi", int Chattiness = 1, string PetName = "", bool Quiet = false,
    SpeechReport? LastSpeech = null, string LearningReset = "");

// The line actually displayed and how long ago, so Python can credit reactions to it.
public sealed record SpeechReport(string Id, double Age);

public sealed record PetNeeds(double Hunger, double Boredom, double Affection, double Bond);

public sealed record PetLine(string Id, string Text);

public sealed record PetLearned(string[] Likes, string[] Dislikes);

public sealed record AiDecision(string Command, double DurationSeconds, string Mood, string? Emotion = null, double? Energy = null,
    string? Thought = null, PetNeeds? Needs = null, PetLine? Speech = null, PetLearned? Learned = null);

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
        // Optional extras: an invalid value is dropped instead of rejecting the decision.
        string? thought = root.TryGetProperty("thought", out var idea) && idea.ValueKind == JsonValueKind.String &&
            PetThought.Parse(idea.GetString()) != ThoughtKind.None ? idea.GetString() : null;
        return new AiDecision(command!, duration, mood!, emotion, energy, thought, ParseNeeds(root), ParseLine(root), ParseLearned(root));
    }

    private static string? Text(JsonElement element, int maxLength)
    {
        if (element.ValueKind != JsonValueKind.String) return null;
        string? text = element.GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Length > maxLength) return null;
        // Display text only: drop control characters such as newlines or escapes.
        var clean = new System.Text.StringBuilder(text.Length);
        foreach (char c in text) if (!char.IsControl(c)) clean.Append(c);
        return clean.Length == 0 ? null : clean.ToString().Trim();
    }

    private static PetLine? ParseLine(JsonElement root)
    {
        if (!root.TryGetProperty("speech", out var speech) || speech.ValueKind != JsonValueKind.Object) return null;
        string? id = speech.TryGetProperty("id", out var idValue) ? Text(idValue, 80) : null;
        string? text = speech.TryGetProperty("text", out var textValue) ? Text(textValue, 160) : null;
        return id != null && text != null ? new PetLine(id, text) : null;
    }

    private static PetLearned? ParseLearned(JsonElement root)
    {
        if (!root.TryGetProperty("learned", out var learned) || learned.ValueKind != JsonValueKind.Object) return null;
        string[] List(string name) => learned.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Select(item => Text(item, 60)).OfType<string>().Take(5).ToArray()
            : Array.Empty<string>();
        return new PetLearned(List("likes"), List("dislikes"));
    }

    private static PetNeeds? ParseNeeds(JsonElement root)
    {
        if (!root.TryGetProperty("needs", out var needs) || needs.ValueKind != JsonValueKind.Object) return null;
        double? Read(string name) => needs.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
            value.TryGetDouble(out double parsed) && double.IsFinite(parsed) && parsed >= 0 && parsed <= 100 ? parsed : null;
        var (hunger, boredom, affection, bond) = (Read("hunger"), Read("boredom"), Read("affection"), Read("bond"));
        return hunger.HasValue && boredom.HasValue && affection.HasValue && bond.HasValue
            ? new PetNeeds(hunger.Value, boredom.Value, affection.Value, bond.Value) : null;
    }
}
