using CrosshairManager.App.Hotkeys;
using CrosshairManager.Core.Settings;

namespace CrosshairManager.Desktop.Tests.App;

[Trait("Category", "Desktop")]
public class HotkeyBindingsTests
{
    [Fact]
    public void Defaults_MapToStableIds()
    {
        var d = HotkeySettings.Default;
        Assert.Equal("Ctrl+Alt+Shift+X", HotkeyBindings.Get(d, HotkeyBindings.ToggleOverlay)?.ToString());
        Assert.Equal("Ctrl+Alt+Shift+Right", HotkeyBindings.Get(d, HotkeyBindings.NextPreset)?.ToString());
        Assert.Equal("Ctrl+Alt+Shift+Left", HotkeyBindings.Get(d, HotkeyBindings.PreviousPreset)?.ToString());
        Assert.Null(HotkeyBindings.Get(d, HotkeyBindings.OpenSettings));
    }

    [Fact]
    public void With_ReplacesOnlyThatId()
    {
        Assert.True(HotkeyGesture.TryParse("Ctrl+Shift+F9", out var g));
        foreach (var id in HotkeyBindings.Ids)
        {
            var updated = HotkeyBindings.With(HotkeySettings.Default, id, g);
            Assert.Equal(g, HotkeyBindings.Get(updated, id));
            foreach (var other in HotkeyBindings.Ids.Where(o => o != id))
            {
                Assert.Equal(HotkeyBindings.Get(HotkeySettings.Default, other), HotkeyBindings.Get(updated, other));
            }
        }

        Assert.Throws<ArgumentException>(() => HotkeyBindings.Get(HotkeySettings.Default, "nope"));
    }
}
