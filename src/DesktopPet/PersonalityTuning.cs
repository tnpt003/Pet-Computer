using System;
using System.IO;
using System.Text.Json;

namespace DesktopPet;

// The packaged JSON supplies defaults; user overrides live only in preferences.
public sealed class PersonalityTuning
{
    public double Playfulness { get; set; } = 0.7;
    public double Curiosity { get; set; } = 0.6;
    public double Sleepiness { get; set; } = 0.25;
    public double AttentionAfterSeconds { get; set; } = 300;
    public double WalkingEnergyPerSecond { get; set; } = 0.5;
    public double PlayingEnergyPerSecond { get; set; } = 1.5;
    public double SleepEnergyPerSecond { get; set; } = 5;
    public double RestEnergyPerSecond { get; set; } = 0.15;
    private static double Safe(double value, double fallback, double low, double high)
        => double.IsFinite(value) ? Math.Clamp(value, low, high) : fallback;
    public PersonalityTuning Normalized() => new()
    {
        Playfulness = Safe(Playfulness, 0.7, 0, 1), Curiosity = Safe(Curiosity, 0.6, 0, 1),
        Sleepiness = Safe(Sleepiness, 0.25, 0, 1), AttentionAfterSeconds = Safe(AttentionAfterSeconds, 300, 10, 86400),
        WalkingEnergyPerSecond = Safe(WalkingEnergyPerSecond, 0.5, 0, 10),
        PlayingEnergyPerSecond = Safe(PlayingEnergyPerSecond, 1.5, 0, 10),
        SleepEnergyPerSecond = Safe(SleepEnergyPerSecond, 5, 0, 20),
        RestEnergyPerSecond = Safe(RestEnergyPerSecond, 0.15, 0, 5)
    };
    public static PersonalityTuning LoadDefaults(string? path = null)
    {
        try
        {
            return (JsonSerializer.Deserialize<PersonalityTuning>(File.ReadAllText(path ?? Path.Combine(AppContext.BaseDirectory, "ai", "personality.json")),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }) ?? new()).Normalized();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
}
