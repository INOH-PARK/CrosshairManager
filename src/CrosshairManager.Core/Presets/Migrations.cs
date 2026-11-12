using System.Text.Json.Nodes;

namespace CrosshairManager.Core.Presets;

/// <summary>Upgrades older preset documents step by step; key = the version a step upgrades from.</summary>
public static class Migrations
{
    public static IReadOnlyDictionary<int, Func<JsonNode, JsonNode>> Steps { get; } =
        new Dictionary<int, Func<JsonNode, JsonNode>>();

    public static JsonNode Run(JsonNode node, int fromVersion, int toVersion, IReadOnlyDictionary<int, Func<JsonNode, JsonNode>> steps)
    {
        for (int version = fromVersion; version < toVersion; version++)
        {
            if (!steps.TryGetValue(version, out var step))
            {
                throw new InvalidOperationException($"no migration from schema version {version}");
            }

            node = step(node);
        }

        node["schemaVersion"] = toVersion;
        return node;
    }
}
