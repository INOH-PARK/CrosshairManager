using CrosshairManager.Core.IO;
using CrosshairManager.Core.Model;

namespace CrosshairManager.Core.Presets;

/// <summary>User presets in <c>{dataDir}/presets/{id}.json</c> plus the built-ins. Unusable files are never rewritten.</summary>
public sealed class PresetStore(string dataDir, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public string PresetsDir { get; } = Path.Combine(Path.GetFullPath(dataDir), "presets");

    public IReadOnlyList<PresetEntry> LoadAll()
    {
        var entries = BuiltInPresets.All.Select(p => new PresetEntry(p.Id, null, new PresetLoadResult.Ok(p, []))).ToList();
        if (!Directory.Exists(PresetsDir))
        {
            return entries;
        }

        var users = Directory.EnumerateFiles(PresetsDir)
            .Where(f => string.Equals(Path.GetExtension(f), ".json", StringComparison.OrdinalIgnoreCase))
            .Select(f => ReadEntry(Path.GetFileNameWithoutExtension(f), f))
            .Where(e => !BuiltInPresets.IsBuiltInId(e.Id))
            .OrderBy(e => e.Preset is null)
            .ThenBy(e => e.Preset?.CreatedUtc ?? default)
            .ThenBy(e => e.Id, StringComparer.Ordinal);
        entries.AddRange(users);
        return entries;
    }

    public Preset Create(string name, CrosshairSpec spec)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(spec);
        var now = Now();
        return Write(new Preset(NewId(), name, now, now, spec));
    }

    public Preset Save(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (preset.IsBuiltIn)
        {
            throw new InvalidOperationException("Built-in presets are read-only.");
        }

        return Write(preset with { ModifiedUtc = Now() });
    }

    public Preset Rename(string id, string newName)
    {
        ArgumentNullException.ThrowIfNull(newName);
        EnsureMutable(id);
        return Save(Get(id) with { Name = newName });
    }

    public Preset Duplicate(string id, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var source = Get(id);
        return Create(name, source.Crosshair);
    }

    public void Delete(string id)
    {
        string path = PathFor(id);
        File.Delete(path);
        File.Delete(path + ".bak");
    }

    public Preset ImportFile(string path)
    {
        var result = PresetFile.Load(File.ReadAllText(path));
        var preset = result switch
        {
            PresetLoadResult.Ok ok => ok.Preset,
            PresetLoadResult.NewerVersion newer => throw new InvalidDataException(
                $"'{path}' uses preset schema version {newer.Version}; this version supports up to {PresetFile.CurrentVersion}."),
            PresetLoadResult.Broken broken => throw new InvalidDataException($"'{path}' is not a valid preset: {broken.Reason}"),
            _ => throw new InvalidDataException($"'{path}' could not be read."),
        };

        bool keepId = Guid.TryParse(preset.Id, out var guid) && !File.Exists(Path.Combine(PresetsDir, guid.ToString("D") + ".json"));
        return Write(preset with { Id = keepId ? guid.ToString("D") : NewId() });
    }

    public void ExportFile(string id, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        AtomicFile.WriteAllText(path, PresetFile.Serialize(Get(id)));
    }

    private Preset Get(string id)
    {
        var builtIn = BuiltInPresets.All.FirstOrDefault(p => p.Id == id);
        if (builtIn is not null)
        {
            return builtIn;
        }

        string path = PathFor(id);
        if (!File.Exists(path))
        {
            throw new KeyNotFoundException($"Preset '{id}' does not exist.");
        }

        return ReadEntry(id, path) switch
        {
            { Preset: { } preset } => preset,
            var entry => throw Unusable(path, entry.Result),
        };
    }

    private Preset Write(Preset preset)
    {
        string path = PathFor(preset.Id);
        if (File.Exists(path) && ReadEntry(preset.Id, path).Result is var existing and not PresetLoadResult.Ok)
        {
            throw Unusable(path, existing);
        }

        var stored = preset with { Crosshair = preset.Crosshair.Normalize().Spec, IsBuiltIn = false };
        AtomicFile.WriteAllText(path, PresetFile.Serialize(stored));
        return stored;
    }

    private static PresetEntry ReadEntry(string id, string path)
    {
        PresetLoadResult result;
        try
        {
            result = PresetFile.Load(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            result = new PresetLoadResult.Broken($"cannot read file: {ex.Message}");
        }

        if (result is PresetLoadResult.Ok ok)
        {
            result = ok with { Preset = ok.Preset with { Id = id } };
        }

        return new PresetEntry(id, path, result);
    }

    private string PathFor(string id)
    {
        EnsureMutable(id);
        return Path.Combine(PresetsDir, id + ".json");
    }

    private static void EnsureMutable(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (BuiltInPresets.IsBuiltInId(id))
        {
            throw new InvalidOperationException($"Built-in preset '{id}' is read-only.");
        }

        if (id.Length == 0 || id is "." or ".." || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"'{id}' is not a valid preset id.", nameof(id));
        }
    }

    private static InvalidOperationException Unusable(string path, PresetLoadResult result) => new(result switch
    {
        PresetLoadResult.NewerVersion n => $"'{path}' uses newer schema version {n.Version}; it is never rewritten.",
        PresetLoadResult.Broken b => $"'{path}' is broken ({b.Reason}); it is never rewritten.",
        _ => $"'{path}' is unusable.",
    });

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;

    private static string NewId() => Guid.NewGuid().ToString("D");
}
