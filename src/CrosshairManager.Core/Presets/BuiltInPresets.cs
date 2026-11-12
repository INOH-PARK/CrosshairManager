using System.Reflection;

namespace CrosshairManager.Core.Presets;

/// <summary>Read-only presets embedded as JSON; Name holds the resx key.</summary>
public static class BuiltInPresets
{
    public const string IdPrefix = "builtin.";

    private static readonly string[] Suffixes = ["default", "dot", "cross", "t", "x45", "classic1px"];

    public static IReadOnlyList<Preset> All { get; } = Suffixes.Select(Load).ToArray();

    public static bool IsBuiltInId(string id) => id.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase);

    private static Preset Load(string suffix)
    {
        string resource = $"CrosshairManager.Core.Presets.BuiltIn.{suffix}.json";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded preset {resource} is missing.");
        using var reader = new StreamReader(stream);
        var result = PresetFile.Load(reader.ReadToEnd());
        if (result is not PresetLoadResult.Ok { Warnings.Count: 0 } ok || ok.Preset.Id != IdPrefix + suffix)
        {
            throw new InvalidOperationException($"Embedded preset {resource} is invalid: {result}");
        }

        return ok.Preset with { IsBuiltIn = true };
    }
}
