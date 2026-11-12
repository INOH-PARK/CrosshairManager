using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrosshairManager.Core.Presets;

/// <summary>Reads and writes one preset document (schema v1, camelCase, colors as "#RRGGBB").</summary>
public static class PresetFile
{
    public const int CurrentVersion = 1;

    public static PresetLoadResult Load(string json) => Load(json, Migrations.Steps, CurrentVersion);

    public static PresetLoadResult Load(string json, IReadOnlyDictionary<int, Func<JsonNode, JsonNode>> migrations, int currentVersion)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(migrations);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowDuplicateProperties = false });
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return new PresetLoadResult.Broken($"invalid JSON: {ex.Message}");
        }

        if (root is not JsonObject obj)
        {
            return new PresetLoadResult.Broken("root is not a JSON object");
        }

        if (obj["schemaVersion"] is not JsonValue versionNode || !versionNode.TryGetValue(out int version))
        {
            return new PresetLoadResult.Broken("schemaVersion is missing or not an integer");
        }

        if (version > currentVersion)
        {
            return new PresetLoadResult.NewerVersion(version);
        }

        if (version < 1)
        {
            return new PresetLoadResult.Broken($"schemaVersion {version} is not valid");
        }

        JsonNode migrated;
        try
        {
            migrated = Migrations.Run(obj, version, currentVersion, migrations);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or ArgumentException or FormatException)
        {
            return new PresetLoadResult.Broken($"migration failed: {ex.Message}");
        }

        if (migrated is not JsonObject migratedObj || migratedObj["crosshair"] is not JsonObject crosshair)
        {
            return new PresetLoadResult.Broken("crosshair is missing or not an object");
        }

        string? nullPath = FindNull(migratedObj, "", ["id", "name", "createdUtc", "modifiedUtc"]) ?? FindNull(crosshair, "crosshair.", null);
        if (nullPath is not null)
        {
            return new PresetLoadResult.Broken($"{nullPath} must not be null");
        }

        Preset preset;
        try
        {
            var doc = migratedObj.Deserialize(PresetJson.Default.PresetDocument)!;
            preset = new Preset(doc.Id ?? "", doc.Name ?? "", AsUtc(doc.CreatedUtc), AsUtc(doc.ModifiedUtc), doc.Crosshair!.ToSpec());
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            return new PresetLoadResult.Broken($"invalid preset: {ex.Message}");
        }

        var (spec, warnings) = preset.Crosshair.Normalize();
        return new PresetLoadResult.Ok(preset with { Crosshair = spec }, warnings);
    }

    public static string Serialize(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var doc = new PresetDocument
        {
            SchemaVersion = CurrentVersion,
            Id = preset.Id,
            Name = preset.Name,
            CreatedUtc = AsUtc(preset.CreatedUtc),
            ModifiedUtc = AsUtc(preset.ModifiedUtc),
            Crosshair = CrosshairDocument.From(preset.Crosshair),
        };
        return JsonSerializer.Serialize(doc, PresetJson.Default.PresetDocument);
    }

    /// <summary>Explicit nulls are malformed (missing is fine); checks the given keys, or every key recursively when keys is null.</summary>
    private static string? FindNull(JsonObject obj, string prefix, string[]? keys)
    {
        foreach (var (key, value) in obj)
        {
            if (keys is not null && !keys.Contains(key))
            {
                continue;
            }

            if (value is null)
            {
                return prefix + key;
            }

            if (keys is null && value is JsonObject child && FindNull(child, prefix + key + ".", null) is { } inner)
            {
                return inner;
            }
        }

        return null;
    }

    private static DateTime AsUtc(DateTime? value) => value switch
    {
        null => DateTime.UnixEpoch,
        { Kind: DateTimeKind.Utc } v => v,
        { Kind: DateTimeKind.Local } v => v.ToUniversalTime(),
        var v => DateTime.SpecifyKind(v.Value, DateTimeKind.Utc),
    };
}
