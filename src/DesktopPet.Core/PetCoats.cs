using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopPet;

public enum CoatMarkings { None, Calico, Tabby }

// One coat from Assets/coats.json: id stored in preferences, settings names and #RRGGBB colors.
// Plain data (no WPF) so preferences and tests can use it; CoatPalette turns it into brushes.
// Reward names what earns it ("achievement", 1.5 M28): until then it is not offered in Settings.
public sealed record CoatSpec(string Id, string NameVi, string NameEn, string Fur, string Cream, string Pink,
    string Ink = "#49372E", string? Iris = null, CoatMarkings Markings = CoatMarkings.None,
    string? PatchA = null, string? PatchB = null, string? Points = null, string? Paw = null, string? Reward = null);

public static class PetCoats
{
    // The original orange artwork, also used when coats.json is missing or broken.
    public static readonly CoatSpec Fallback = new("Orange", "Cam", "Orange", "#E9AE65", "#FFE4B5", "#E98990");
    private static readonly Lazy<CoatSpec[]> Loaded = new(Load);

    // Settings order; the first coat is the default.
    public static IReadOnlyList<CoatSpec> All => Loaded.Value;

    private static CoatSpec[] Load()
    {
        try
        {
            using var stream = typeof(PetCoats).Assembly.GetManifestResourceStream("DesktopPet.coats.json")
                ?? throw new InvalidDataException("Missing embedded coats.json");
            return Parse(stream);
        }
        // A broken file must never stop the pet: log it and keep the original orange cat.
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            Trace.WriteLine($"Coats could not be loaded, using the orange cat: {ex.Message}");
            return new[] { Fallback };
        }
    }

    private static readonly Regex Hex = new("^#[0-9A-Fa-f]{6}$");

    // Throws InvalidDataException/JsonException on any mistake, so a half-valid file is never used.
    public static CoatSpec[] Parse(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("coats", out var coats) || coats.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("coats.json needs a \"coats\" list");
        var list = new List<CoatSpec>();
        foreach (var item in coats.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Each coat must be an object");
            string? Text(JsonElement owner, string name) =>
                owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            string id = Text(item, "id") is { Length: > 0 } found ? found : throw new InvalidDataException("A coat has no id");
            if (list.Any(existing => existing.Id == id)) throw new InvalidDataException("Duplicate coat id " + id);
            string? Color(string name, bool required = false)
            {
                string? value = Text(item, name);
                if (value == null) return required ? throw new InvalidDataException($"{id} has no {name}") : null;
                return Hex.IsMatch(value) ? value : throw new InvalidDataException($"{id}.{name} is not #RRGGBB");
            }
            var markings = Text(item, "markings") switch
            {
                null => CoatMarkings.None,
                "Calico" => CoatMarkings.Calico,
                "Tabby" => CoatMarkings.Tabby,
                string other => throw new InvalidDataException($"{id} has unknown markings {other}")
            };
            var spec = new CoatSpec(id, id, id, Color("fur", true)!, Color("cream", true)!, Color("pink", true)!,
                Color("ink") ?? "#49372E", Color("iris"), markings, Color("patchA"), Color("patchB"), Color("points"), Color("paw"));
            if (markings != CoatMarkings.None && spec.PatchA == null || markings == CoatMarkings.Calico && spec.PatchB == null)
                throw new InvalidDataException($"{id} is missing patch colors for its markings");
            string? reward = Text(item, "reward");
            if (reward != null && !Regex.IsMatch(reward, "^[a-z0-9_-]{1,32}$")) throw new InvalidDataException(id + " has a bad reward");
            if (reward != null && list.Count == 0) throw new InvalidDataException("The first (default) coat cannot be a reward");
            spec = spec with { Reward = reward };
            if (item.TryGetProperty("name", out var names) && names.ValueKind == JsonValueKind.Object)
                spec = spec with { NameVi = Text(names, "vi") ?? id, NameEn = Text(names, "en") ?? id };
            list.Add(spec);
        }
        if (list.Count == 0) throw new InvalidDataException("coats.json has no coats");
        return list.ToArray();
    }
}
