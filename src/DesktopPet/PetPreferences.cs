using System;
using System.IO;
using System.Text.Json;

namespace DesktopPet;

public sealed class PetPreferences
{
    public string Name { get; set; } = "Mochi";
    public string Personality { get; set; } = "Balanced";
    public PersonalityTuning? Tuning { get; set; }
    public bool Quiet { get; set; }
    public bool Sounds { get; set; }
    public double Volume { get; set; } = 0.7;
    public bool InteractionSounds { get; set; } = true;
    public bool EmotionSounds { get; set; } = true;
    public bool SleepSounds { get; set; } = true;
    // Speech bubbles (lines chosen by Python AI). Chattiness: 0 rarely, 1 sometimes, 2 often.
    public bool Speech { get; set; } = true;
    // One language for the whole app (menus, windows, diary, speech): "en" or "vi".
    public string Language { get; set; } = "en";
    public int Chattiness { get; set; } = 1;
    // A new value asks Python to forget what it learned; Python applies each value once.
    public string LearningReset { get; set; } = "";
    // Appearance; drawn by CoatPalette and PetRenderer.
    public static readonly string[] Coats = { "Orange", "Gray", "Black", "White", "Calico", "Tabby", "GingerTabby", "Tuxedo", "RussianBlue", "Siamese" };
    public static readonly string[] Accessories = { "None", "Bell", "Bow", "Hat", "Scarf", "Glasses", "Flower", "Crown" };
    public static string NormalizeCoat(string? coat) => Array.IndexOf(Coats, coat) >= 0 ? coat! : "Orange";
    public static string NormalizeAccessory(string? accessory) => Array.IndexOf(Accessories, accessory) >= 0 ? accessory! : "None";
    public string Coat { get; set; } = "Orange";
    public string Accessory { get; set; } = "None";
    // Off by default: the pet reminds the user to take a break after this many active minutes.
    public bool BreakReminders { get; set; }
    public int BreakMinutes { get; set; } = 45;
    public static readonly int[] BreakChoices = { 30, 45, 60 };
    // On by default: the pet may stand on, jump onto and climb other apps' windows.
    public bool ClimbWindows { get; set; } = true;
    public string? PositionMonitor { get; set; }
    public double PositionX { get; set; } = 0.5;
    public double PositionY { get; set; } = 1;
    public string? RestMonitor { get; set; }
    public double RestFractionX { get; set; } = 0.5;
    public double? RestX { get; set; }
    public double? RestY { get; set; }
    public static string DataDirectory => Environment.GetEnvironmentVariable("PET_DATA_DIRECTORY") is string path && Path.IsPathFullyQualified(path)
        ? path : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet");
    private static string FilePath => Path.Combine(DataDirectory, "preferences.json");
    public static PetPreferences Load(string? path = null)
    {
        try
        {
            var p = JsonSerializer.Deserialize<PetPreferences>(File.ReadAllText(path ?? FilePath)) ?? new();
            p.Name = string.IsNullOrWhiteSpace(p.Name) ? "Mochi" : p.Name.Trim()[..Math.Min(p.Name.Trim().Length, 24)];
            if (p.Personality is not ("Balanced" or "Playful" or "Curious" or "Calm")) p.Personality = "Balanced";
            p.Tuning = p.Tuning?.Normalized();
            p.Volume = double.IsFinite(p.Volume) ? Math.Clamp(p.Volume, 0, 1) : 0.7;
            if (p.Language is not ("vi" or "en")) p.Language = "en";
            p.Chattiness = Math.Clamp(p.Chattiness, 0, 2);
            p.Coat = NormalizeCoat(p.Coat);
            p.Accessory = NormalizeAccessory(p.Accessory);
            if (Array.IndexOf(BreakChoices, p.BreakMinutes) < 0) p.BreakMinutes = 45;
            if (p.LearningReset == null || p.LearningReset.Length > 64) p.LearningReset = "";
            p.PositionX = double.IsFinite(p.PositionX) ? Math.Clamp(p.PositionX, 0, 1) : 0.5;
            p.PositionY = double.IsFinite(p.PositionY) ? Math.Clamp(p.PositionY, 0, 1) : 1;
            p.RestFractionX = double.IsFinite(p.RestFractionX) ? Math.Clamp(p.RestFractionX, 0, 1) : 0.5;
            if (!p.RestX.HasValue || !p.RestY.HasValue || !double.IsFinite(p.RestX.Value) || !double.IsFinite(p.RestY.Value)) p.RestX = p.RestY = null;
            return p;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public bool Save(string? path = null)
    {
        path ??= FilePath;
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, JsonSerializer.Serialize(this));
            File.Move(temp, path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
