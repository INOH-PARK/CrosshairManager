using CrosshairManager.Core.Settings;

namespace CrosshairManager.Core.Tests.Settings;

public class MonitorSelectionTests
{
    [Fact]
    public void Primary_selection_has_primary_mode_and_no_device_name()
    {
        var selection = MonitorSelection.Primary;

        Assert.Equal(MonitorMode.Primary, selection.Mode);
        Assert.Null(selection.DeviceName);
    }

    [Fact]
    public void Device_selection_equality_uses_device_name_value()
    {
        var a = new MonitorSelection(MonitorMode.Device, @"\\.\DISPLAY2");

        Assert.Equal(new MonitorSelection(MonitorMode.Device, @"\\.\DISPLAY2"), a);
        Assert.NotEqual(MonitorSelection.Primary, a);
    }
}
