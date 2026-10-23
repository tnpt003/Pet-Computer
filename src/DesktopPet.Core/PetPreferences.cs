using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DesktopPet.AI;

namespace DesktopPet;

public sealed class PetPreferences
{
    // The layout this file was written in. Files without it are layout 1. A future layout change
    // raises CurrentVersion and migrates older files in Load; Save always writes CurrentVersion.
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public string Name { get; set; } = "Mochi";
    public string Personality { get; set; } = PetPersonality.Balanced;
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
    // Settings order comes from Assets/coats.json.
    public static readonly string[] Coats = PetCoats.All.Select(coat => coat.Id).ToArray();
    // "None" first, then Assets/accessories.json in settings order.
    public static readonly string[] Accessories = new[] { PetAccessories.None }.Concat(PetAccessories.All.Select(item => item.Id)).ToArray();
    public static string NormalizeCoat(string? coat) => Array.IndexOf(Coats, coat) >= 0 ? coat! : Coats[0];
    public static string NormalizeAccessory(string? accessory) => Array.IndexOf(Accessories, accessory) >= 0 ? accessory! : "None";
    public string Coat { get; set; } = "Orange";
    // The user's birthday as "MM-dd" (no year), or "" when not set; Python greets on that day.
    public string Birthday { get; set; } = "";
    public static string NormalizeBirthday(string? value) =>
        value is { Length: 5 } && value[2] == '-' && int.TryParse(value[..2], out int month) && int.TryParse(value[3..], out int day) &&
        month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(2000, month) ? value : "";
    public string Accessory { get; set; } = "None";
    // On by default: on a special day the pet wears the accessory made for it (accessories.json "event").
    public bool HolidayAccessories { get; set; } = true;
    // Off by default: the pet reminds the user to take a break after this many active minutes.
    public bool BreakReminders { get; set; }
    public int BreakMinutes { get; set; } = 45;
    public static readonly int[] BreakChoices = { 30, 45, 60 };
    // Installed app only (PetUpdates): on by default, new versions are fetched in the background;
    // off, only Settings → General / the tray's "Check for updates" asks.
    public bool AutoUpdate { get; set; } = true;
    // The installed version that ran last (null: never recorded), for "Updated to x" after an update.
    public string? LastVersion { get; set; }
    // On by default: the pet may stand on, jump onto and climb other apps' windows.
    public bool ClimbWindows { get; set; } = true;
    // On by default: the Expedition's keepsakes stand in the display corner once one is earned.
    public bool ShowTrophies { get; set; } = true;
    // Where the user or the cat left the keepsake: its monitor and the art's left edge as a fraction of
    // the free width (null: not moved yet, 0.75 across the cat's monitor).
    public string? TrophyMonitor { get; set; }
    // The one keepsake on display (Settings → Pet); null or not owned: the best one (gold → silver → bronze → cushion).
    public string? TrophyShown { get; set; }
    public double? TrophyFractionX { get; set; }
    // Where the user dragged the battle HUD, per monitor (device name): its left and top as fractions
    // of the room left in that monitor's work area. A monitor not listed: centred at the top.
    public Dictionary<string, double[]>? BattleHudSpots { get; set; }
    // Battle Mode difficulty chosen in the menu: "Easy", "Normal" or "Hard".
    public string BattleDifficulty { get; set; } = "Normal";
    public static string NormalizeDifficulty(string? value) => value is "Easy" or "Normal" or "Hard" ? value : "Normal";
    // The first-battle guide was gone through or skipped (older files: false, so it shows once).
    public bool BattleTutorialSeen { get; set; }
    // The battle lessons on the spot already shown (PetWindow.BattleCoach.cs; null or empty: none yet).
    public List<string>? BattleLessonsSeen { get; set; }
    // Battle Mode weapon chosen in the menu: "Sword" or "Hammer" (anything else reads as Sword).
    public string BattleWeapon { get; set; } = "Sword";
    // The app icons above the battlefield (Adventure, Expedition, Infinity), chosen in the setup
    // panel. Off: no icons and no viruses; such runs keep their records apart.
    public bool BattleApps { get; set; } = true;
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
        var p = JsonStore.Read<PetPreferences>(path ?? FilePath, "Preferences") ?? new();
        p.Version = CurrentVersion;
        p.Name = string.IsNullOrWhiteSpace(p.Name) ? "Mochi" : p.Name.Trim()[..Math.Min(p.Name.Trim().Length, 24)];
        p.Personality = PetPersonality.Normalize(p.Personality);
        p.Tuning = p.Tuning?.Normalized();
        p.Volume = double.IsFinite(p.Volume) ? Math.Clamp(p.Volume, 0, 1) : 0.7;
        p.Language = PetLanguage.Normalize(p.Language);
        p.Chattiness = Math.Clamp(p.Chattiness, 0, 2);
        p.Coat = NormalizeCoat(p.Coat);
        p.Accessory = NormalizeAccessory(p.Accessory);
        p.Birthday = NormalizeBirthday(p.Birthday);
        p.BattleDifficulty = NormalizeDifficulty(p.BattleDifficulty);
        if (Array.IndexOf(BreakChoices, p.BreakMinutes) < 0) p.BreakMinutes = 45;
        if (p.LearningReset == null || p.LearningReset.Length > 64) p.LearningReset = "";
        p.PositionX = double.IsFinite(p.PositionX) ? Math.Clamp(p.PositionX, 0, 1) : 0.5;
        p.PositionY = double.IsFinite(p.PositionY) ? Math.Clamp(p.PositionY, 0, 1) : 1;
        p.RestFractionX = double.IsFinite(p.RestFractionX) ? Math.Clamp(p.RestFractionX, 0, 1) : 0.5;
        p.BattleHudSpots = p.BattleHudSpots?.Where(s => !string.IsNullOrWhiteSpace(s.Key) && s.Value is { Length: 2 } && s.Value.All(double.IsFinite)).Take(16)
            .ToDictionary(s => s.Key, s => s.Value.Select(v => Math.Clamp(v, 0, 1)).ToArray());
        if (p.BattleHudSpots is { Count: 0 }) p.BattleHudSpots = null;
        if (p.TrophyFractionX is double tx) p.TrophyFractionX = double.IsFinite(tx) ? Math.Clamp(tx, 0, 1) : null;
        if (!p.RestX.HasValue || !p.RestY.HasValue || !double.IsFinite(p.RestX.Value) || !double.IsFinite(p.RestY.Value)) p.RestX = p.RestY = null;
        return p;
    }
    public bool Save(string? path = null) => JsonStore.Write(path ?? FilePath, this, "Preferences");
}
