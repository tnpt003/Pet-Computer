using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopPet;

// UI language ids: stored in preferences and sent to Python (ai/speech.py LANGUAGES).
public static class PetLanguage
{
    public const string Vietnamese = "vi", English = "en", Japanese = "ja";
    public static readonly string[] All = { Vietnamese, English, Japanese };
    // What the welcome and Settings offer, in that order. Japanese joins once it is translated.
    public static readonly string[] Offered = { English, Vietnamese };
    public static string Normalize(string? value) => Array.IndexOf(All, value) >= 0 ? value! : English;
    // Every language but Vietnamese starts from the English text, names and layouts.
    public static bool UsesEnglish(string? language) => language != Vietnamese;
    public static string DisplayName(string language) => language switch
    {
        Vietnamese => "Tiếng Việt",
        Japanese => "日本語",
        _ => "English"
    };
    // The offered languages, plus the current one if it is not offered (set by hand or by a check).
    public static string[] Choices(string current) =>
        Array.IndexOf(Offered, current) >= 0 ? Offered : Offered.Append(current).ToArray();
}

// Text in more languages than Vietnamese and English. The code keeps both: L("vi", "en").
// Other languages are tables keyed by the English text (Assets/i18n/<language>.json); interpolated
// text is keyed by its format, "Wave {0} cleared!". Missing text shows in English and is recorded.
public static class PetText
{
    private static readonly Dictionary<string, Lazy<IReadOnlyDictionary<string, string>>> Tables = new()
    {
        [PetLanguage.Japanese] = new(() => Load(PetLanguage.Japanese))
    };
    private static readonly ConcurrentDictionary<string, byte> missing = new();

    // "ja: Wave {0} cleared!" for every key asked for but not translated, since the app started.
    public static IReadOnlyCollection<string> Missing => missing.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
    public static void ClearMissing() => missing.Clear();

    public static string Pick(string language, string vietnamese, string english) => language switch
    {
        PetLanguage.Vietnamese => vietnamese,
        PetLanguage.English => english,
        _ => Translate(language, english) ?? english
    };

    // Data text (battle.json, coats.json, accessories.json, keyed by its English like the code's):
    // empty text stays empty instead of being looked up.
    public static string Data(string language, string vietnamese, string english) =>
        english.Length == 0 && language != PetLanguage.Vietnamese ? english : Pick(language, vietnamese, english);

    public static string PickFormat(string language, FormattableString vietnamese, FormattableString english)
    {
        if (language == PetLanguage.Vietnamese) return vietnamese.ToString(CultureInfo.CurrentCulture);
        if (language != PetLanguage.English && Translate(language, english.Format) is string format)
        {
            try { return string.Format(CultureInfo.CurrentCulture, format, english.GetArguments()); }
            catch (FormatException) { Trace.WriteLine($"Translation ({language}) has a broken format: {format}"); }
        }
        return english.ToString(CultureInfo.CurrentCulture);
    }

    private static string? Translate(string language, string english)
    {
        if (Tables.TryGetValue(language, out var table) && table.Value.TryGetValue(english, out var text)) return text;
        if (missing.TryAdd($"{language}: {english}", 0)) Trace.WriteLine($"No {language} text for: {english}");
        return null;
    }

    public static IReadOnlyDictionary<string, string> Table(string language) =>
        Tables.TryGetValue(language, out var table) ? table.Value : new Dictionary<string, string>();

    private static IReadOnlyDictionary<string, string> Load(string language)
    {
        try
        {
            using var stream = typeof(PetText).Assembly.GetManifestResourceStream($"DesktopPet.i18n.{language}.json")
                ?? throw new InvalidDataException($"Missing embedded i18n/{language}.json");
            return Parse(stream);
        }
        // A broken table must never stop the pet: log it and show English.
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            Trace.WriteLine($"Translations ({language}) could not be loaded, showing English: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }

    // {"English": "translation"}; keys starting with "_" are comments. Throws on anything else.
    public static Dictionary<string, string> Parse(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("A translation table is a JSON object");
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in document.RootElement.EnumerateObject())
        {
            if (entry.Name.StartsWith('_')) continue;
            if (entry.Value.ValueKind != JsonValueKind.String || entry.Value.GetString() is not { Length: > 0 } text)
                throw new InvalidDataException($"No text for: {entry.Name}");
            table[entry.Name] = text;
        }
        return table;
    }

    private static readonly Regex Hole = new(@"\{(\d+)[^{}]*\}");
    // The argument numbers a format uses ("{0:F0} of {1}" → 0, 1); a translation uses no others.
    public static int[] Holes(string format) =>
        Hole.Matches(format.Replace("{{", "").Replace("}}", "")).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct().Order().ToArray();
}
