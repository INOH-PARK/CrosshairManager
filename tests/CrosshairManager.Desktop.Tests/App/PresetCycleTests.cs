using CrosshairManager.App.Presets;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;

namespace CrosshairManager.Desktop.Tests.App;

[Trait("Category", "Desktop")]
public class PresetCycleTests
{
    private static readonly IReadOnlyList<PresetEntry> Entries =
    [
        .. BuiltInPresets.All.Select(p => new PresetEntry(p.Id, null, new PresetLoadResult.Ok(p, []))),
        new PresetEntry("broken", "broken.json", new PresetLoadResult.Broken("bad")),
        new PresetEntry("user", "user.json", new PresetLoadResult.Ok(new Preset("user", "Mine", default, default, CrosshairSpec.Default), [])),
    ];

    [Fact]
    public void Next_FollowsListOrder_DefaultToDot() =>
        Assert.Equal("builtin.dot", PresetCycle.Step(Entries, "builtin.default", 1));

    [Fact]
    public void Cycle_SkipsUnloadableAndWraps()
    {
        Assert.Equal("user", PresetCycle.Step(Entries, "builtin.classic1px", 1));
        Assert.Equal("builtin.default", PresetCycle.Step(Entries, "user", 1));
        Assert.Equal("user", PresetCycle.Step(Entries, "builtin.default", -1));
    }

    [Fact]
    public void UnknownCurrent_StartsAtEitherEnd()
    {
        Assert.Equal("builtin.default", PresetCycle.Step(Entries, "gone", 1));
        Assert.Equal("user", PresetCycle.Step(Entries, "gone", -1));
        Assert.Null(PresetCycle.Step([], "x", 1));
    }

    [Fact]
    public void Resolve_FallsBackToDefaultForMissingOrBroken()
    {
        Assert.Equal("builtin.dot", PresetCycle.Resolve(Entries, "builtin.dot").Id);
        Assert.Equal("builtin.default", PresetCycle.Resolve(Entries, "broken").Id);
        Assert.Equal("builtin.default", PresetCycle.Resolve(Entries, "gone").Id);
    }
}
