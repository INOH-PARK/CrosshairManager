using System.Diagnostics;

namespace CrosshairManager.Platform;

/// <summary>
/// Runs subscriber code inside a native callback (window procedure, WinEvent hook, timer procedure) so an exception never
/// unwinds into user32. A caught exception goes to <c>report</c>, which returns false when nobody observes it; unobserved
/// exceptions, and exceptions thrown while reporting, are written to <see cref="Trace"/>.
/// </summary>
internal static class CallbackGuard
{
    public static void Run(Action body, Func<Exception, bool> report, string source)
    {
        try
        {
            body();
        }
        catch (Exception ex)
        {
            try
            {
                if (!report(ex))
                {
                    Trace.TraceError("CrosshairManager: unobserved " + source + " handler exception: " + ex);
                }
            }
            catch (Exception reportError)
            {
                Trace.TraceError("CrosshairManager: " + source + " HandlerFailed subscriber threw " + reportError + " while reporting " + ex);
            }
        }
    }
}
