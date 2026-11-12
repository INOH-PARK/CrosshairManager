using System.Text.Json.Nodes;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using static CrosshairManager.Core.Tests.Presets.PresetTestData;

namespace CrosshairManager.Core.Tests.Presets;

public class MigrationsTests
{
    [Fact]
    public void Version_1_has_no_migration_steps()
    {
        Assert.Equal(1, PresetFile.CurrentVersion);
        Assert.Empty(Migrations.Steps);
    }

    [Fact]
    public void Run_applies_steps_in_order_and_stamps_target_version()
    {
        var steps = new Dictionary<int, Func<JsonNode, JsonNode>>
        {
            [1] = n => { n["trail"] = n["trail"]!.GetValue<string>() + "1"; return n; },
            [2] = n => { n["trail"] = n["trail"]!.GetValue<string>() + "2"; return n; },
        };

        var node = Migrations.Run(JsonNode.Parse("""{"schemaVersion":1,"trail":""}""")!, 1, 3, steps);

        Assert.Equal("12", node["trail"]!.GetValue<string>());
        Assert.Equal(3, node["schemaVersion"]!.GetValue<int>());
    }

    [Fact]
    public void Run_with_missing_step_throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Migrations.Run(JsonNode.Parse("""{"schemaVersion":1}""")!, 1, 3, new Dictionary<int, Func<JsonNode, JsonNode>>
            {
                [1] = n => n,
            }));
    }

    [Fact]
    public void Load_older_file_runs_migration_chain_before_reading()
    {
        var steps = new Dictionary<int, Func<JsonNode, JsonNode>>
        {
            [1] = n =>
            {
                var crosshair = n["crosshair"]!.AsObject();
                var colour = crosshair["colour"]!.GetValue<string>();
                crosshair.Remove("colour");
                crosshair["color"] = colour;
                return n;
            },
        };

        var ok = AssertOk(PresetFile.Load("""{"schemaVersion":1,"crosshair":{"colour":"#123456"}}""", steps, currentVersion: 2));

        Assert.Equal(Rgb.Parse("#123456"), ok.Preset.Crosshair.Color);
    }

    [Fact]
    public void Load_with_missing_migration_step_is_Broken()
    {
        Assert.IsType<PresetLoadResult.Broken>(
            PresetFile.Load("""{"schemaVersion":1,"crosshair":{}}""", new Dictionary<int, Func<JsonNode, JsonNode>>(), currentVersion: 2));
    }

    [Fact]
    public void Load_with_throwing_migration_step_is_Broken()
    {
        var steps = new Dictionary<int, Func<JsonNode, JsonNode>> { [1] = _ => throw new InvalidOperationException("boom") };

        var broken = Assert.IsType<PresetLoadResult.Broken>(PresetFile.Load("""{"schemaVersion":1,"crosshair":{}}""", steps, currentVersion: 2));
        Assert.Contains("boom", broken.Reason);
    }
}
