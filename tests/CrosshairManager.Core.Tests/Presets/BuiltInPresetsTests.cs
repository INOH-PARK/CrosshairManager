using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;

namespace CrosshairManager.Core.Tests.Presets;

public class BuiltInPresetsTests
{
    [Fact]
    public void Six_built_ins_in_fixed_order_with_resx_name_keys()
    {
        string[] suffixes = ["default", "dot", "cross", "t", "x45", "classic1px"];

        Assert.Equal(suffixes.Select(s => "builtin." + s), BuiltInPresets.All.Select(p => p.Id));
        Assert.Equal(suffixes.Select(s => "Preset.builtin." + s), BuiltInPresets.All.Select(p => p.Name));
        Assert.All(BuiltInPresets.All, p => Assert.True(p.IsBuiltIn));
    }

    [Fact]
    public void Built_in_specs_match_the_definitions()
    {
        var d = CrosshairSpec.Default;
        var off = new LineSpec(Enabled: false);
        var cross = d with { Dot = d.Dot with { Enabled = false } };
        var classic = new LineSpec(true, 4, 1, 2);
        var expected = new Dictionary<string, CrosshairSpec>
        {
            ["builtin.default"] = d,
            ["builtin.dot"] = d with { Dot = new DotSpec(true, 4, 100), Top = off, Bottom = off, Left = off, Right = off, Outline = new OutlineSpec(true, 1) },
            ["builtin.cross"] = cross,
            ["builtin.t"] = d with { Top = d.Top with { Enabled = false } },
            ["builtin.x45"] = cross with { RotationDeg = 45 },
            ["builtin.classic1px"] = d with
            {
                Dot = d.Dot with { Enabled = false },
                Top = classic,
                Bottom = classic,
                Left = classic,
                Right = classic,
                Outline = d.Outline with { Enabled = false },
            },
        };

        Assert.All(BuiltInPresets.All, p => Assert.Equal(expected[p.Id], p.Crosshair));
    }

    [Theory]
    [InlineData("builtin.default", true)]
    [InlineData("builtin.x45", true)]
    [InlineData("BUILTIN.dot", true)]
    [InlineData("builtin.future", true)]
    [InlineData("3f2b8c1e-5a7d-4e2f-9b1c-0d6e8a4f7c21", false)]
    public void Built_in_id_prefix_is_reserved(string id, bool reserved)
    {
        Assert.Equal(reserved, BuiltInPresets.IsBuiltInId(id));
    }
}
