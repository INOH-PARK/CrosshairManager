using System.ComponentModel;
using System.Runtime.InteropServices;
using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Settings;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace CrosshairManager.Platform.Monitors;

/// <summary>Enumerates monitors with physical-pixel bounds and effective DPI, and resolves a <see cref="MonitorSelection"/>.</summary>
public sealed unsafe class MonitorService : IDisposable
{
    /// <summary>MONITORINFOF_PRIMARY: set on the monitor at virtual-screen origin (0,0).</summary>
    private const uint MonitorInfoFPrimary = 1;

    public MonitorService()
    {
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>Raised when the display configuration changes (on the SystemEvents thread).</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var handles = new List<HMONITOR>();
        if (!PInvoke.EnumDisplayMonitors(HDC.Null, null, (hmon, _, _, _) => { handles.Add(hmon); return true; }, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var monitors = new List<MonitorInfo>(handles.Count);
        foreach (var hmon in handles)
        {
            monitors.Add(Describe(hmon));
        }

        return monitors;
    }

    /// <summary>
    /// Returns the selected monitor. A device name that is not connected falls back to the primary monitor
    /// and sets <paramref name="fellBack"/>; the selection itself is never rewritten here.
    /// </summary>
    public MonitorInfo Resolve(MonitorSelection selection, out bool fellBack)
    {
        var monitors = GetMonitors();
        if (selection.Mode == MonitorMode.Device && selection.DeviceName is not null)
        {
            foreach (var monitor in monitors)
            {
                if (string.Equals(monitor.DeviceName, selection.DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    fellBack = false;
                    return monitor;
                }
            }
        }

        fellBack = selection.Mode == MonitorMode.Device;
        return monitors.First(m => m.IsPrimary);
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    private static MonitorInfo Describe(HMONITOR hmon)
    {
        var info = new MONITORINFOEXW { monitorInfo = { cbSize = (uint)sizeof(MONITORINFOEXW) } };
        if (!PInvoke.GetMonitorInfo(hmon, (MONITORINFO*)&info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        PInvoke.GetDpiForMonitor(hmon, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY).ThrowOnFailure();

        var m = info.monitorInfo.rcMonitor;
        var w = info.monitorInfo.rcWork;
        return new MonitorInfo(
            new string((char*)&info.szDevice),
            new IntRect(m.left, m.top, m.right - m.left, m.bottom - m.top),
            new IntRect(w.left, w.top, w.right - w.left, w.bottom - w.top),
            (info.monitorInfo.dwFlags & MonitorInfoFPrimary) != 0,
            dpiX,
            dpiY);
    }
}
