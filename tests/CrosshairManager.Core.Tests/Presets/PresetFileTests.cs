using System.Text.Json.Nodes;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using static CrosshairManager.Core.Tests.Presets.PresetTestData;

namespace CrosshairManager.Core.Tests.Presets;

public class PresetFileTests
{
    [Fact]
    public void Built_in_presets_round_trip_through_serialize_and_load()
    {
        foreach (var preset in BuiltInPresets.All)
        {
            var ok = AssertOk(PresetFile.Load(PresetFile.Serialize(preset)));
            Assert.Empty(ok.Warnings);
            Assert.Equal(preset with { IsBuiltIn = false }, ok.Preset);
        }
    }

    [Fact]
    public void Fifty_random_presets_round_trip_through_serialize_and_load()
    {
        var r = new Random(8);
        for (int i = 0; i < 50; i++)
        {
            var preset = RandomPreset(r, i);
            var ok = AssertOk(PresetFile.Load(PresetFile.Serialize(preset)));
            Assert.Empty(ok.Warnings);
            Assert.Equal(preset, ok.Preset);
        }
    }

    [Fact]
    public void Fixture_v1_loads_with_every_field()
    {
        var ok = AssertOk(PresetFile.Load(File.ReadAllText(FixturePath("presets", "v1.json"))));

        Assert.Empty(ok.Warnings);
        var expected = new Preset(
            "3f2b8c1e-5a7d-4e2f-9b1c-0d6e8a4f7c21",
            "Fixture v1",
            new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 2, 8, 30, 0, DateTimeKind.Utc),
            new CrosshairSpec
            {
                Color = Rgb.Parse("#FF8800"),
                Opacity = 90,
                ScalePercent = 150,
                RotationDeg = 90,
                OffsetX = -3,
                OffsetY = 5,
                Dot = new DotSpec(false, 3, 80),
                Top = new LineSpec(false, 8, 2, 4),
                Bottom = new LineSpec(true, 8, 2, 4),
                Left = new LineSpec(true, 10, 1, -2),
                Right = new LineSpec(true, 10, 1, -2),
                Outline = new OutlineSpec(true, 2, Rgb.Parse("#101010"), 60),
            });
        Assert.Equal(expected, ok.Preset);
        Assert.Equal(DateTimeKind.Utc, ok.Preset.CreatedUtc.Kind);
        Assert.False(ok.Preset.IsBuiltIn);
    }

    [Fact]
    public void Serialize_writes_camelCase_schema_version_and_hex_colors()
    {
        var root = JsonNode.Parse(PresetFile.Serialize(BuiltInPresets.All[0]))!.AsObject();

        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("builtin.default", root["id"]!.GetValue<string>());
        Assert.Equal("#00FF00", root["crosshair"]!["color"]!.GetValue<string>());
        Assert.Equal("#000000", root["crosshair"]!["outline"]!["color"]!.GetValue<string>());
        Assert.Equal(100, root["crosshair"]!["scalePercent"]!.GetValue<int>());
        Assert.True(root["crosshair"]!["dot"]!["enabled"]!.GetValue<bool>());
        Assert.False(root.ContainsKey("isBuiltIn"));
        Assert.False(root.ContainsKey("SchemaVersion"));
    }

    [Fact]
    public void Schema_version_2_is_NewerVersion()
    {
        var result = PresetFile.Load("""{"schemaVersion":2,"id":"x","crosshair":{"someFutureField":1}}""");

        Assert.Equal(new PresetLoadResult.NewerVersion(2), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("""{"crosshair":{}}""")]
    [InlineData("""{"schemaVersion":"1","crosshair":{}}""")]
    [InlineData("""{"schemaVersion":1.5,"crosshair":{}}""")]
    [InlineData("""{"schemaVersion":0,"crosshair":{}}""")]
    [InlineData("""{"schemaVersion":-3,"crosshair":{}}""")]
    [InlineData("""{"schemaVersion":1}""")]
    [InlineData("""{"schemaVersion":1,"crosshair":null}""")]
    [InlineData("""{"schemaVersion":1,"crosshair":[]}""")]
    [InlineData("""{"schemaVersion":1,"crosshair":{"color":"#GG0000"}}""")]
    [InlineData("""{"schemaVersion":1,"crosshair":{"color":5}}""")]
    [InlineData("""{"schemaVersion":1,"crosshair":{"color":null}}""")]
    [InlineData("""{"schemaVersion":1,"crosshair":{"dot":null}}""")]
    [InlineData("""{"schemaVersion":1,"crosshair":{"opacity":"x"}}""")]
    [InlineData("""{"schemaVersion":1,"crosshair":{"opacity":1.5}}""")]
    [InlineData("""{"schemaVersion":1,"crosshair":{"opacity":99999999999}}""")]
    [InlineData("""{"schemaVersion":1,"name":7,"crosshair":{}}""")]
    [InlineData("""{"schemaVersion":1,"name":null,"crosshair":{}}""")]
    [InlineData("""{"schemaVersion":1,"createdUtc":"yesterday","crosshair":{}}""")]
    [InlineData("""{"schemaVersion":1,"schemaVersion":1,"crosshair":{}}""")]
    public void Malformed_files_are_Broken_with_a_reason(string json)
    {
        var broken = Assert.IsType<PresetLoadResult.Broken>(PresetFile.Load(json));

        Assert.False(string.IsNullOrWhiteSpace(broken.Reason));
    }

    [Fact]
    public void Out_of_range_values_load_Ok_clamped_with_warnings()
    {
        var ok = AssertOk(PresetFile.Load("""
            {"schemaVersion":1,"crosshair":{"opacity":150,"rotationDeg":30,"dot":{"size":0},"left":{"gap":-100},"outline":{"thickness":99}}}
            """));

        Assert.Equal(100, ok.Preset.Crosshair.Opacity);
        Assert.Equal(45, ok.Preset.Crosshair.RotationDeg);
        Assert.Equal(1, ok.Preset.Crosshair.Dot.Size);
        Assert.Equal(-32, ok.Preset.Crosshair.Left.Gap);
        Assert.Equal(16, ok.Preset.Crosshair.Outline.Thickness);
        Assert.Equal(
            new[]
            {
                "Opacity: 150 clamped to 100",
                "RotationDeg: 30 clamped to 45",
                "Dot.Size: 0 clamped to 1",
                "Left.Gap: -100 clamped to -32",
                "Outline.Thickness: 99 clamped to 16",
            }.Order(StringComparer.Ordinal),
            ok.Warnings.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Missing_optional_fields_take_defaults()
    {
        var ok = AssertOk(PresetFile.Load("""{"schemaVersion":1,"crosshair":{}}"""));

        Assert.Equal(CrosshairSpec.Default, ok.Preset.Crosshair);
        Assert.Equal("", ok.Preset.Id);
        Assert.Equal("", ok.Preset.Name);
        Assert.Empty(ok.Warnings);
    }

    [Fact]
    public void Missing_nested_fields_take_defaults()
    {
        var ok = AssertOk(PresetFile.Load("""
            {"schemaVersion":1,"crosshair":{"top":{"enabled":false},"dot":{"size":5},"outline":{"thickness":2}}}
            """));

        Assert.Equal(CrosshairSpec.Default with
        {
            Top = new LineSpec(Enabled: false),
            Dot = new DotSpec(Size: 5),
            Outline = new OutlineSpec(Thickness: 2),
        }, ok.Preset.Crosshair);
        Assert.Equal(Rgb.Parse("#000000"), ok.Preset.Crosshair.Outline.Color);
    }

    [Fact]
    public void Unknown_fields_are_ignored()
    {
        var ok = AssertOk(PresetFile.Load("""{"schemaVersion":1,"extra":[1,2],"crosshair":{"glow":true}}"""));

        Assert.Equal(CrosshairSpec.Default, ok.Preset.Crosshair);
    }

    [Fact]
    public void Timestamps_without_offset_are_read_as_utc()
    {
        var ok = AssertOk(PresetFile.Load("""{"schemaVersion":1,"createdUtc":"2026-05-01T10:00:00","crosshair":{}}"""));

        Assert.Equal(new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc), ok.Preset.CreatedUtc);
        Assert.Equal(DateTimeKind.Utc, ok.Preset.CreatedUtc.Kind);
    }
}
