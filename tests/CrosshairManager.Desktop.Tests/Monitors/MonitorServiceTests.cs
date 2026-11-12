using CrosshairManager.Core.Settings;
using CrosshairManager.Platform.Monitors;
using CrosshairManager.TestSupport;
using Xunit.Abstractions;

namespace CrosshairManager.Desktop.Tests.Monitors;

[Trait("Category", "Desktop")]
public class MonitorServiceTests
{
    private readonly ITestOutputHelper _output;

    public MonitorServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Test_thread_is_per_monitor_v2_aware()
    {
        Assert.True(DpiAwareness.IsThreadPerMonitorV2());
    }

    [Fact]
    public void Lists_devices_bounds_and_dpi_with_exactly_one_primary_monitor_at_origin()
    {
        using var service = new MonitorService();
        var monitors = service.GetMonitors();

        foreach (var m in monitors)
        {
            _output.WriteLine($"{m.DeviceName} bounds={m.Bounds} work={m.WorkArea} primary={m.IsPrimary} dpi={m.DpiX}x{m.DpiY}");
        }

        Assert.NotEmpty(monitors);
        var origin = Assert.Single(monitors, m => m.Bounds.X == 0 && m.Bounds.Y == 0);
        Assert.True(origin.IsPrimary);
        Assert.Single(monitors, m => m.IsPrimary);
        Assert.All(monitors, m =>
        {
            Assert.True(m.DpiX > 0);
            Assert.True(m.DpiY > 0);
        });
    }

    [Fact]
    public void Missing_device_resolves_to_primary_and_reports_fallback()
    {
        using var service = new MonitorService();

        var resolved = service.Resolve(new MonitorSelection(MonitorMode.Device, @"\\.\DISPLAY_NOPE"), out var fellBack);

        Assert.True(resolved.IsPrimary);
        Assert.True(fellBack);
    }

    [Fact]
    public void First_non_primary_device_resolves_without_fallback()
    {
        using var service = new MonitorService();
        var target = service.GetMonitors().FirstOrDefault(m => !m.IsPrimary);
        Assert.NotNull(target); // this machine must have a second display for this check

        var resolved = service.Resolve(new MonitorSelection(MonitorMode.Device, target.DeviceName), out var fellBack);

        Assert.Equal(target.DeviceName, resolved.DeviceName);
        Assert.False(fellBack);
    }
}
