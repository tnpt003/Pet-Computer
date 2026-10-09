using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DesktopPet;

// Cartoon-styled cat recordings, embedded for offline playback. See Assets/Sounds/CREDITS.md.
public static class PetSounds
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<byte[]>> Prepared = new();
    public static byte[] CreateWave(string kind, int? seed = null)
    {
        if (seed.HasValue) return BuildWave(kind, seed);
        kind = kind is "purr" or "sleep" or "excited" or "surprised" or "annoyed" or "ball_tap" or "satisfied" or "wake" ? kind : "meow";
        int variant = Random.Shared.Next(3);
        lock (Gate)
        {
            if (LastClip.TryGetValue(kind, out int previous) && previous == variant) variant = (variant + 1) % 3;
            LastClip[kind] = variant;
        }
        int fixedSeed = 100 + variant;
        byte[] cached = Prepared.GetOrAdd(kind + variant, _ => new Lazy<byte[]>(() => BuildWave(kind, fixedSeed))).Value;
        return (byte[])cached.Clone(); // Callers may scale volume without modifying the cache.
    }
    private static readonly Dictionary<string, int> LastClip = new();
    private static readonly object Gate = new();

    private static byte[] BuildWave(string kind, int? seed = null)
    {
        string[] clips = kind switch
        {
            "ball_tap" => new[] { "purr-active-a", "purr-active-b" },
            "satisfied" => new[] { "mew-purr", "mew-purr2" },
            "wake" => new[] { "mew-soft", "mew-purr" },
            "purr" => new[] { "purr-active-a", "purr-active-b" },
            "sleep" => new[] { "purr-sleep-a", "purr-sleep-b" },
            "excited" => new[] { "mew-purr", "mew-purr2" },
            "surprised" => new[] { "mew-food", "mew-purr" },
            "annoyed" => new[] { "mew-food", "mew-purr2" },
            _ => new[] { "mew-soft", "mew-purr", "mew-food" }
        };
        var random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        int index = seed.HasValue ? (int)((uint)seed.Value % (uint)clips.Length) : random.Next(clips.Length);
        if (!seed.HasValue)
        {
            lock (Gate)
            {
                if (LastClip.TryGetValue(kind, out int previous) && index == previous)
                    index = (index + 1) % clips.Length;
                LastClip[kind] = index;
            }
        }
        using var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopPet.Sounds." + clips[index] + ".wav")
            ?? throw new InvalidDataException("Missing recorded pet sound: " + clips[index]);
        using var copy = new MemoryStream(); input.CopyTo(copy);
        byte[] wave = copy.ToArray();
        if (wave.Length < 44 || BitConverter.ToInt32(wave, 40) != wave.Length - 44)
            throw new InvalidDataException("Invalid recorded pet sound");
        return Cartoonize(wave, kind, random);
    }

    // Keep a feline voice underneath the exaggeration instead of making UI beeps.
    // All cues share rounded dynamics, a short envelope, and expressive pitch arcs.
    private static byte[] Cartoonize(byte[] wave, string kind, Random random)
    {
        const int rate = 22050;
        int sourceCount = (wave.Length - 44) / 2;
        var source = new double[sourceCount];
        for (int i = 0; i < sourceCount; i++) source[i] = BitConverter.ToInt16(wave, 44 + i * 2) / 32768.0;
        double duration = kind switch { "ball_tap" => 0.13, "satisfied" => 0.55, "wake" => 0.65, "purr" => 1.0, "sleep" => 1.15, "excited" => 0.7, "surprised" => 0.42, "annoyed" => 0.65, _ => 0.85 };
        double pitch = kind switch { "ball_tap" => 0.8, "satisfied" => 1.38, "wake" => 1.05, "purr" => 1.12, "sleep" => 0.95, "excited" => 1.45, "surprised" => 1.55, "annoyed" => 1.08, _ => 1.3 };
        pitch *= 0.97 + random.NextDouble() * 0.06;
        int count = Math.Min((int)(duration * rate), (int)((sourceCount - 2) / (pitch * 1.16)));
        int span = Math.Min(sourceCount - 1, (int)(count * pitch * 1.16));
        // Find a voiced excerpt rather than speeding up the entire long recording.
        var energy = new double[sourceCount + 1];
        for (int i = 0; i < sourceCount; i++) energy[i + 1] = energy[i] + source[i] * source[i];
        int start = 0; double best = -1;
        for (int i = 0; i + span < sourceCount; i += 220)
        {
            double value = energy[i + span] - energy[i];
            if (value > best) { best = value; start = i; }
        }
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, true);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
        writer.Write((short)2); writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
        double rms = Math.Sqrt((energy[Math.Min(sourceCount, start + span)] - energy[start]) / Math.Max(1, span));
        double gain = Math.Clamp(0.12 / Math.Max(0.001, rms), 0.5, 2.5);
        double position = start, smooth = 0;
        double volume = (kind == "ball_tap" ? 0.5 : kind is "sleep" or "wake" ? 0.65 : 0.9) * (0.92 + random.NextDouble() * 0.08);
        for (int i = 0; i < count; i++)
        {
            double u = (double)i / Math.Max(1, count - 1);
            int at = Math.Min(sourceCount - 2, (int)position);
            double sample = source[at] + (source[at + 1] - source[at]) * (position - at);
            smooth += (sample - smooth) * 0.65;
            double arc = kind switch
            {
                "ball_tap" => 1.1 - 0.4 * u,
                "satisfied" => 1 + 0.1 * Math.Sin(u * Math.PI * 2),
                "wake" => 0.95 + 0.12 * Math.Sin(u * Math.PI),
                "surprised" => 0.9 + 0.2 * u,
                "excited" => 1 + 0.10 * Math.Sin(u * Math.PI * 3),
                "annoyed" => 1.05 - 0.18 * u,
                "purr" or "sleep" => 1 + 0.02 * Math.Sin(u * Math.PI * 2),
                _ => 1.1 - 0.22 * u
            };
            position = Math.Min(sourceCount - 2, position + pitch * arc);
            double envelope = Math.Pow(Math.Sin(Math.PI * u), 0.35);
            if (kind == "ball_tap") envelope *= Math.Exp(-u * 5);
            double bounce = kind == "excited" ? 0.7 + 0.3 * Math.Pow(Math.Cos(u * Math.PI * 2), 2) : 1;
            double rounded = Math.Tanh(smooth * gain * 2) * 0.48;
            writer.Write((short)(rounded * envelope * bounce * volume * 32767));
        }
        return output.ToArray();
    }
}
