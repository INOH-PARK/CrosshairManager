using CrosshairManager.Core.Settings;

namespace CrosshairManager.Core.Tests.Settings;

public class HotkeyGestureTests
{
    [Theory]
    [InlineData("shift+ctrl+x", "Ctrl+Shift+X")]
    [InlineData("CTRL+ALT+SHIFT+WIN+F11", "Ctrl+Alt+Shift+Win+F11")]
    [InlineData("win+alt+0", "Alt+Win+0")]
    [InlineData(" ctrl + numpad5 ", "Ctrl+NumPad5")]
    [InlineData("alt+pageup", "Alt+PageUp")]
    [InlineData("Ctrl+left", "Ctrl+Left")]
    [InlineData("F13", "F13")]
    [InlineData("f24", "F24")]
    [InlineData("Ctrl+F1", "Ctrl+F1")]
    [InlineData("Shift+Delete", "Shift+Delete")]
    public void TryParse_accepts_and_canonicalizes(string input, string canonical)
    {
        Assert.True(HotkeyGesture.TryParse(input, out var g));
        Assert.Equal(canonical, g!.ToString());
        Assert.True(HotkeyGesture.TryParse(g.ToString(), out var again));
        Assert.Equal(g, again);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("F1")]
    [InlineData("F12")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Foo")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+Shift")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    [InlineData("Ctrl+Ctrl+X")]
    [InlineData("Ctrl+X+Y")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+NumPad10")]
    [InlineData("+X")]
    [InlineData("Ctrl++X")]
    public void TryParse_rejects(string? input)
    {
        Assert.False(HotkeyGesture.TryParse(input, out var g));
        Assert.Null(g);
    }

    [Fact]
    public void Modifiers_and_virtual_key_use_win32_values()
    {
        Assert.True(HotkeyGesture.TryParse("Ctrl+Alt+Shift+Win+F11", out var g));
        Assert.Equal(0x0Fu, g!.Modifiers);
        Assert.Equal(0x7Au, g.VirtualKey);
        Assert.True(HotkeyGesture.TryParse("shift+ctrl+x", out var h));
        Assert.Equal(0x06u, h!.Modifiers);
        Assert.Equal(0x58u, h.VirtualKey);
        Assert.True(HotkeyGesture.TryParse("F13", out var f));
        Assert.Equal(0u, f!.Modifiers);
        Assert.Equal(0x7Cu, f.VirtualKey);
        Assert.True(HotkeyGesture.TryParse("Alt+NumPad0", out var n));
        Assert.Equal(0x60u, n!.VirtualKey);
        Assert.True(HotkeyGesture.TryParse("Alt+9", out var d));
        Assert.Equal(0x39u, d!.VirtualKey);
        Assert.True(HotkeyGesture.TryParse("Alt+PageDown", out var p));
        Assert.Equal(0x22u, p!.VirtualKey);
    }
}
