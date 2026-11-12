using System.Diagnostics;
using Windows.Win32;

namespace CrosshairManager.TestSupport;

/// <summary>
/// Capture rule: every attempt is preceded by DwmFlush (blocks until the next composition), so retries are paced
/// by the compositor instead of sleeps. Bounded by <c>timeout</c>; at least one attempt is always made.
/// </summary>
public static class Eventually
{
    public static bool True(Func<bool> predicate, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var watch = Stopwatch.StartNew();
        do
        {
            PInvoke.DwmFlush();
            if (predicate())
            {
                return true;
            }
        }
        while (watch.Elapsed < timeout);

        return false;
    }
}
