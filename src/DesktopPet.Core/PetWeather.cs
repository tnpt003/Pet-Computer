using System;

namespace DesktopPet;

// Clear sky is also the answer when the wallpaper cannot be read.
public enum WeatherKind { Sunny, Rain, Snow, Sunset, Pollen, Night }

// The weather around the cat (1.5.1 M2): which kind a wallpaper's colors mean, and how strong it is
// right now. The kind changes only with the wallpaper; the strength wanders over time.
// The window reads the wallpaper and owns the art (PetWindow.Weather.cs).
public sealed class PetWeather
{
    public const double FadeSeconds = 1.2, SpellMinSeconds = 40, SpellMaxSeconds = 140, LightestRain = 0.1;
    // Slow enough that a shower builds up and dies away instead of switching.
    private const double EaseRate = 0.35;
    // A color says more about a picture than the same area of white, gray or black.
    private const double ColorWeight = 1.25;

    public WeatherKind Kind { get; private set; }
    // 0: a lull (rain has stopped for a while), 1: as heavy as it gets.
    public double Intensity { get; private set; } = 0.6;
    public double TargetIntensity { get; private set; } = 0.6;
    // 0 hidden, 1 fully shown.
    public double Visibility { get; private set; }
    // Seconds of weather so far; stands still in Pause. The art's particles are a function of it.
    public double Time { get; private set; }
    public double SpellRemaining { get; private set; } = SpellMinSeconds;
    private WeatherKind? next;

    public bool Visible => Visibility > 0;
    // What the field feels (1.5.1 M6): only the weather on show, and a lull in the rain waters nothing.
    public bool Raining => Visible && Kind == WeatherKind.Rain && Intensity >= LightestRain;
    public bool Shining => Visible && Kind == WeatherKind.Sunny;

    // Hidden weather changes at once; a showing one fades out first and comes back as the new kind.
    public void SetKind(WeatherKind kind)
    {
        if (kind == Kind) { next = null; return; }
        if (Visible) next = kind;
        else { Kind = kind; next = null; }
    }

    public void HideNow()
    {
        Visibility = 0;
        if (next is WeatherKind kind) { Kind = kind; next = null; }
    }

    // dt 0 (Pause) changes nothing.
    public void Step(double dt, bool show, Random random)
    {
        if (dt <= 0) return;
        Time += dt;
        double goal = show && next == null ? 1 : 0, step = dt / FadeSeconds;
        Visibility = goal > Visibility ? Math.Min(goal, Visibility + step) : Math.Max(goal, Visibility - step);
        if (Visibility <= 0 && next is WeatherKind kind) { Kind = kind; next = null; }

        SpellRemaining -= dt;
        if (SpellRemaining <= 0)
        {
            SpellRemaining = SpellMinSeconds + random.NextDouble() * (SpellMaxSeconds - SpellMinSeconds);
            TargetIntensity = NextIntensity(random);
        }
        Intensity += (TargetIntensity - Intensity) * (1 - Math.Exp(-dt * EaseRate));
    }

    // Heavy, moderate, light, or a lull with nothing falling at all.
    public static double NextIntensity(Random random)
    {
        double roll = random.NextDouble(), within = random.NextDouble();
        if (roll < 0.3) return 0.85 + within * 0.15;
        if (roll < 0.65) return 0.45 + within * 0.25;
        if (roll < 0.85) return 0.15 + within * 0.15;
        return 0;
    }

    public static WeatherKind ClassifyColor(byte r, byte g, byte b)
    {
        int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), range = max - min;
        double value = max / 255.0, saturation = max == 0 ? 0 : range / (double)max;
        if (value < 0.28) return WeatherKind.Night;

        double hue = 0;
        if (range > 0)
        {
            hue = max == r ? (g - b) / (double)range : max == g ? 2 + (b - r) / (double)range : 4 + (r - g) / (double)range;
            hue = (hue * 60 + 360) % 360;
        }
        bool blue = hue is >= 175 and < 255, green = hue is >= 70 and < 175, violet = hue is >= 255 and < 300;
        if ((blue || violet) && value < 0.42) return WeatherKind.Night;
        // Pale blue is the cold light of snow; washed-out warm tones count as white or gray.
        if (saturation < (blue ? 0.3 : 0.2)) return value >= 0.75 ? WeatherKind.Snow : WeatherKind.Rain;
        return blue ? WeatherKind.Sunny : green ? WeatherKind.Pollen : WeatherKind.Sunset;
    }

    // The kind that covers the most of a picture (BGRA bytes). Not the average color: the average
    // of a colorful picture is gray, which would make it rain.
    public static WeatherKind Classify(ReadOnlySpan<byte> bgra)
    {
        Span<double> votes = stackalloc double[Enum.GetValues<WeatherKind>().Length];
        for (int i = 0; i + 3 < bgra.Length; i += 4)
        {
            if (bgra[i + 3] < 128) continue;
            var kind = ClassifyColor(bgra[i + 2], bgra[i + 1], bgra[i]);
            votes[(int)kind] += kind is WeatherKind.Sunny or WeatherKind.Sunset or WeatherKind.Pollen ? ColorWeight : 1;
        }
        int best = 0;
        for (int i = 1; i < votes.Length; i++)
            if (votes[i] > votes[best]) best = i;
        return (WeatherKind)best;
    }
}
