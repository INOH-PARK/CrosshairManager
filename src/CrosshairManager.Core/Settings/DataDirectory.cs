using System.Collections;

namespace CrosshairManager.Core.Settings;

/// <summary>Chooses the data folder: <c>--data-dir</c> argument, then <c>CROSSHAIRMANAGER_DATA_DIR</c>, then <c>%LocalAppData%\CrosshairManager</c>.</summary>
public static class DataDirectory
{
    private const string ArgName = "--data-dir";
    private const string EnvName = "CROSSHAIRMANAGER_DATA_DIR";

    /// <exception cref="ArgumentException"><c>--data-dir</c> is given without a path.</exception>
    public static string Resolve(string[] args, IDictionary env)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(env);

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == ArgName)
            {
                if (i + 1 >= args.Length || args[i + 1].Length == 0)
                {
                    throw new ArgumentException($"{ArgName} requires a path.", nameof(args));
                }

                return Path.GetFullPath(args[i + 1]);
            }

            if (arg.StartsWith(ArgName + "=", StringComparison.Ordinal))
            {
                string value = arg[(ArgName.Length + 1)..];
                if (value.Length == 0)
                {
                    throw new ArgumentException($"{ArgName} requires a path.", nameof(args));
                }

                return Path.GetFullPath(value);
            }
        }

        if (env[EnvName] is string fromEnv && fromEnv.Length > 0)
        {
            return Path.GetFullPath(fromEnv);
        }

        string localAppData = env["LOCALAPPDATA"] as string ?? throw new InvalidOperationException("LOCALAPPDATA is not set.");
        return Path.Combine(localAppData, "CrosshairManager");
    }

    /// <summary>Canonical form used for single-instance keys: full path, no trailing separator, upper case.</summary>
    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).ToUpperInvariant();
}
