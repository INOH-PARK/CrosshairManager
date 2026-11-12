using System.Collections;
using CrosshairManager.Core.Settings;

namespace CrosshairManager.App;

/// <summary>Command line: <c>--data-dir &lt;path&gt;</c>, <c>--settings</c>, <c>--hidden</c>. Unknown arguments are ignored.</summary>
public sealed record AppArguments(string DataDir, bool OpenSettings, bool Hidden)
{
    /// <exception cref="ArgumentException"><c>--data-dir</c> has no value or is followed by another option.</exception>
    public static AppArguments Parse(string[] args, IDictionary env)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(env);
        var dataDirIndex = Array.IndexOf(args, "--data-dir");
        if (dataDirIndex >= 0 && dataDirIndex + 1 < args.Length && args[dataDirIndex + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"--data-dir requires a path, got option '{args[dataDirIndex + 1]}'.", nameof(args));
        }

        var dataDir = DataDirectory.Resolve(args, env);
        var flags = args.Where((a, i) => i != dataDirIndex + 1 || dataDirIndex < 0).ToArray();
        return new AppArguments(
            dataDir,
            flags.Contains("--settings", StringComparer.Ordinal),
            flags.Contains("--hidden", StringComparer.Ordinal));
    }

    /// <summary>Startup visibility precedence: <c>--hidden</c> &gt; saved <c>overlayVisible</c> (default true).</summary>
    public bool InitialOverlayVisible(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return !Hidden && settings.OverlayVisible;
    }

    /// <summary>The settings window opens at startup only for <c>--settings</c> or on first run (disclaimer not accepted).</summary>
    public bool OpenSettingsAtStartup(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return OpenSettings || !settings.DisclaimerAccepted;
    }
}
