namespace CrosshairManager.App.Logging;

/// <summary>Shutdown steps that must not stop each other: a failing step is logged and the caller moves on.</summary>
public static class Teardown
{
    /// <summary>Runs <paramref name="step"/>; on failure logs <c>could not dispose {name}</c> with the exception and returns false.</summary>
    public static bool Step(FileLog log, string name, Action step)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(step);
        try
        {
            step();
            return true;
        }
        catch (Exception ex)
        {
            log.Error("could not dispose " + name, ex);
            return false;
        }
    }
}
