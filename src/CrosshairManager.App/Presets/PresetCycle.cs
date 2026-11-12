using CrosshairManager.Core.Presets;

namespace CrosshairManager.App.Presets;

/// <summary>Next/previous over the loadable presets in list order, and the active-preset fallback.</summary>
public static class PresetCycle
{
    /// <summary>Returns the id <paramref name="direction"/> (+1/-1) steps from <paramref name="currentId"/>, wrapping; null when nothing is loadable.</summary>
    public static string? Step(IReadOnlyList<PresetEntry> entries, string currentId, int direction)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var usable = entries.Where(e => e.Preset is not null).Select(e => e.Id).ToList();
        if (usable.Count == 0)
        {
            return null;
        }

        var index = usable.FindIndex(id => string.Equals(id, currentId, StringComparison.Ordinal));
        if (index < 0)
        {
            return direction >= 0 ? usable[0] : usable[^1];
        }

        var count = usable.Count;
        return usable[(((index + direction) % count) + count) % count];
    }

    /// <summary>The loadable preset with <paramref name="id"/>, else <c>builtin.default</c>.</summary>
    public static Preset Resolve(IReadOnlyList<PresetEntry> entries, string id)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal))?.Preset ?? BuiltInPresets.All[0];
    }
}
