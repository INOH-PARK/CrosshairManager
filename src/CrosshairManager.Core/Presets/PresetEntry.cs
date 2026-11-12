namespace CrosshairManager.Core.Presets;

public sealed record PresetEntry(string Id, string? FilePath, PresetLoadResult Result)
{
    public Preset? Preset => (Result as PresetLoadResult.Ok)?.Preset;
}
