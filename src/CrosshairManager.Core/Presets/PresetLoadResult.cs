namespace CrosshairManager.Core.Presets;

public abstract record PresetLoadResult
{
    private PresetLoadResult()
    {
    }

    public sealed record Ok(Preset Preset, IReadOnlyList<string> Warnings) : PresetLoadResult;

    public sealed record NewerVersion(int Version) : PresetLoadResult;

    public sealed record Broken(string Reason) : PresetLoadResult;
}
