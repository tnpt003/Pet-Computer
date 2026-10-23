using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopPet;

public enum AccessoryLayer { Neck, Head }
public enum ShapeKind { Path, Ellipse, Line }
public enum StrokeKind { None, Outline, Accent, Custom }

// A stroke: the coat's outline/accent pen, or a custom #color pen.
public sealed record StrokeSpec(StrokeKind Kind, string? Color = null, double Width = 0, bool Round = false, bool RoundJoin = false);

// One shape of an accessory in the cat's head space: path data, or ellipse [cx, cy, rx, ry], or line [x1, y1, x2, y2].
public sealed record AccessoryShape(ShapeKind Kind, string? Path, double[] Values, string? Fill, StrokeSpec Stroke);

// One accessory from Assets/accessories.json. Event names a special day (ai/calendar.json) it is worn on.
// Reward names what earns it (an Adventure chapter): until then it is not offered in Settings.
public sealed record AccessorySpec(string Id, string NameVi, string NameEn, AccessoryLayer Layer, string? Event, AccessoryShape[] Shapes, string? Reward = null);

// Plain data (no WPF) so preferences and tests can use it; PetRenderer turns it into frozen drawing resources.
public static class PetAccessories
{
    public static string Name(this AccessorySpec item, string language) => PetText.Data(language, item.NameVi, item.NameEn);
    public const string None = "None";
    private static readonly Lazy<AccessorySpec[]> Loaded = new(Load);

    // Settings order after the built-in "None".
    public static IReadOnlyList<AccessorySpec> All => Loaded.Value;

    public static AccessorySpec? Find(string? id) => All.FirstOrDefault(item => item.Id == id);

    // The accessory worn on a special day, if any.
    public static AccessorySpec? ForEvent(string? specialDay) =>
        specialDay == null ? null : All.FirstOrDefault(item => item.Event == specialDay);

    private static AccessorySpec[] Load()
    {
        try
        {
            using var stream = typeof(PetAccessories).Assembly.GetManifestResourceStream("DesktopPet.accessories.json")
                ?? throw new InvalidDataException("Missing embedded accessories.json");
            return Parse(stream);
        }
        // A broken file must never stop the pet: log it and offer no accessories.
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            Trace.WriteLine($"Accessories could not be loaded: {ex.Message}");
            return Array.Empty<AccessorySpec>();
        }
    }

    private static readonly Regex Color = new("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");
    private static readonly Regex Id = new("^[A-Za-z][A-Za-z0-9]{0,31}$");
    private static readonly Regex PathData = new("^[MLHVCSQTAZmlhvcsqtaz0-9 ,.+\\-eE]{1,2000}$");

    // Throws InvalidDataException/JsonException on any mistake, so a half-valid file is never used.
    // Path data is only checked for its characters here; PetRenderer skips an accessory whose path WPF rejects.
    public static AccessorySpec[] Parse(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("accessories", out var items) || items.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("accessories.json needs an \"accessories\" list");
        var list = new List<AccessorySpec>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Each accessory must be an object");
            string id = Text(item, "id") is string found && Id.IsMatch(found) && found != None ? found : throw new InvalidDataException("An accessory has a bad id");
            if (list.Any(existing => existing.Id == id)) throw new InvalidDataException("Duplicate accessory id " + id);
            var layer = Text(item, "layer") switch
            {
                "neck" => AccessoryLayer.Neck,
                "head" => AccessoryLayer.Head,
                _ => throw new InvalidDataException(id + " needs layer neck or head")
            };
            string? specialDay = Text(item, "event");
            if (specialDay != null && !Regex.IsMatch(specialDay, "^[a-z][a-z0-9_]{0,31}$")) throw new InvalidDataException(id + " has a bad event");
            string? reward = Text(item, "reward");
            if (reward != null && !Regex.IsMatch(reward, "^[a-z0-9_-]{1,32}$")) throw new InvalidDataException(id + " has a bad reward");
            if (!item.TryGetProperty("shapes", out var shapes) || shapes.ValueKind != JsonValueKind.Array || shapes.GetArrayLength() is 0 or > 64)
                throw new InvalidDataException(id + " needs 1-64 shapes");
            var parsed = shapes.EnumerateArray().Select(shape => ParseShape(id, shape)).ToArray();
            string vi = id, en = id;
            if (item.TryGetProperty("name", out var names) && names.ValueKind == JsonValueKind.Object)
            {
                vi = Text(names, "vi") ?? id;
                en = Text(names, "en") ?? id;
            }
            list.Add(new AccessorySpec(id, vi, en, layer, specialDay, parsed, reward));
        }
        return list.ToArray();
    }

    private static AccessoryShape ParseShape(string id, JsonElement shape)
    {
        if (shape.ValueKind != JsonValueKind.Object) throw new InvalidDataException(id + ": each shape must be an object");
        string? fill = Text(shape, "fill");
        if (fill != null && !Color.IsMatch(fill)) throw new InvalidDataException($"{id}: fill {fill} is not #RRGGBB or #AARRGGBB");
        var stroke = ParseStroke(id, shape);
        if (Text(shape, "path") is string path)
        {
            if (!PathData.IsMatch(path)) throw new InvalidDataException($"{id}: bad path data");
            return Checked(new AccessoryShape(ShapeKind.Path, path, Array.Empty<double>(), fill, stroke));
        }
        foreach (var (name, kind) in new[] { ("ellipse", ShapeKind.Ellipse), ("line", ShapeKind.Line) })
        {
            if (!shape.TryGetProperty(name, out var array)) continue;
            if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() != 4 || array.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.Number))
                throw new InvalidDataException($"{id}: {name} needs four numbers");
            var values = array.EnumerateArray().Select(v => v.GetDouble()).ToArray();
            if (values.Any(v => !double.IsFinite(v) || v < -60 || v > 170) || kind == ShapeKind.Ellipse && (values[2] <= 0 || values[3] <= 0))
                throw new InvalidDataException($"{id}: {name} is out of range");
            return Checked(new AccessoryShape(kind, null, values, fill, stroke));
        }
        throw new InvalidDataException(id + ": a shape needs path, ellipse or line");

        AccessoryShape Checked(AccessoryShape result) =>
            result.Fill == null && result.Stroke.Kind == StrokeKind.None ? throw new InvalidDataException(id + ": a shape needs a fill or a stroke")
            : result.Kind == ShapeKind.Line && result.Stroke.Kind == StrokeKind.None ? throw new InvalidDataException(id + ": a line needs a stroke")
            : result;
    }

    private static StrokeSpec ParseStroke(string id, JsonElement shape)
    {
        if (!shape.TryGetProperty("stroke", out var stroke) || stroke.ValueKind == JsonValueKind.Null) return new StrokeSpec(StrokeKind.None);
        if (stroke.ValueKind == JsonValueKind.String)
            return stroke.GetString() switch
            {
                "outline" => new StrokeSpec(StrokeKind.Outline),
                "accent" => new StrokeSpec(StrokeKind.Accent),
                var other => throw new InvalidDataException($"{id}: unknown stroke {other}")
            };
        if (stroke.ValueKind != JsonValueKind.Object) throw new InvalidDataException(id + ": bad stroke");
        string color = Text(stroke, "color") is string c && Color.IsMatch(c) ? c : throw new InvalidDataException(id + ": stroke needs a #color");
        double width = stroke.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetDouble() : double.NaN;
        if (!(width > 0 && width <= 20)) throw new InvalidDataException(id + ": stroke width must be 0-20");
        bool round = stroke.TryGetProperty("round", out var r) && r.ValueKind == JsonValueKind.True;
        bool roundJoin = Text(stroke, "join") switch
        {
            null or "miter" => false,
            "round" => true,
            _ => throw new InvalidDataException(id + ": join must be miter or round")
        };
        return new StrokeSpec(StrokeKind.Custom, color, width, round, roundJoin);
    }

    private static string? Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
