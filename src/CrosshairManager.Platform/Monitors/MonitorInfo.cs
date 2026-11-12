using CrosshairManager.Core.Geometry;

namespace CrosshairManager.Platform.Monitors;

/// <summary>A display as Windows reports it. Bounds and WorkArea are physical pixels in virtual-screen space.</summary>
public sealed record MonitorInfo(string DeviceName, IntRect Bounds, IntRect WorkArea, bool IsPrimary, uint DpiX, uint DpiY);
