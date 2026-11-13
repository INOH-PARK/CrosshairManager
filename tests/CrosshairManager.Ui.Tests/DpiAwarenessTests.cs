using CrosshairManager.TestSupport;

namespace CrosshairManager.Ui.Tests;

/// <summary>First Ui test (Must have): the module initializer made the test process Per-Monitor-V2 aware. No window is created.</summary>
[Trait("Category", "Ui")]
public class DpiAwarenessTests
{
    [Fact]
    public void Thread_dpi_awareness_context_is_per_monitor_v2()
    {
        // AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), PER_MONITOR_AWARE_V2)
        Assert.True(DpiAwareness.IsThreadPerMonitorV2());
    }
}
