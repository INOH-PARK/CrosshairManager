using Xunit.Abstractions;
using PlatformDpi = CrosshairManager.Platform.Dpi.DpiAwareness;

namespace CrosshairManager.Desktop.Tests;

/// <summary>
/// First Desktop test (Must have): the module initializer made the process Per-Monitor-V2 aware,
/// so every coordinate the other tests compare is in physical pixels. No windows are created.
/// </summary>
[Trait("Category", "Desktop")]
public class DpiAwarenessTests
{
    private readonly ITestOutputHelper _output;

    public DpiAwarenessTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Thread_dpi_awareness_context_is_per_monitor_v2()
    {
        // AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), PER_MONITOR_AWARE_V2)
        Assert.True(PlatformDpi.IsThreadPerMonitorV2());

        var dpi = PlatformDpi.GetEffectiveDpi(0, 0);
        _output.WriteLine($"Primary monitor (0,0) effective DPI: {dpi} ({dpi * 100 / 96}%)");
        Assert.True(dpi >= 96, $"unexpected DPI {dpi}");
    }

    [Fact]
    public void EnsurePerMonitorV2_is_idempotent_when_already_aware()
    {
        Assert.True(PlatformDpi.EnsurePerMonitorV2());
        Assert.True(PlatformDpi.IsThreadPerMonitorV2());
    }
}
