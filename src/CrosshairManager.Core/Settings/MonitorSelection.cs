namespace CrosshairManager.Core.Settings;

/// <summary>
/// Which monitor the overlay uses. A device is identified by its GDI device name (for example \\.\DISPLAY1),
/// never by its position in an enumeration.
/// </summary>
public sealed record MonitorSelection(MonitorMode Mode, string? DeviceName)
{
    public static MonitorSelection Primary { get; } = new(MonitorMode.Primary, null);
}

public enum MonitorMode
{
    Primary,
    Device,
}
