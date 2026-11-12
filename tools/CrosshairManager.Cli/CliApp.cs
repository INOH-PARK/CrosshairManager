using System.Collections;
using System.ComponentModel;

namespace CrosshairManager.Cli;

/// <summary>Entry point of the <c>crosshair</c> tool; testable without a process (all I/O goes through the writers).</summary>
public static class CliApp
{
    internal const string Usage = """
        usage:
          crosshair render <presetIdOrJsonPath> <out.png> [--zoom 1..32 (8)] [--bg #RRGGBB] [--odd] [--data-dir <dir>]
          crosshair validate <jsonPath>
          crosshair capture <out.png> [--monitor <device>] [--size N (64)]
          crosshair compare <expected.png> <actual.png> [--tolerance N (0)]
          crosshair verify-overlay [--data-dir <dir>] [--preset <id>] [--monitor <device>] [--expect-hidden] [--timeout <s> (5)] [--out <dir>]
          crosshair ico <out.ico>
        exit codes: 0 ok, 1 mismatch/failed check, 2 usage error or unusable input, 3 newer preset version (validate)
        """;

    public static int Run(string[] args, TextWriter output, TextWriter error, IDictionary env)
    {
        ArgumentNullException.ThrowIfNull(args);
        try
        {
            if (args.Length == 0)
            {
                throw new UsageException(null);
            }

            return args[0] switch
            {
                "render" => RenderCommand.Run(Options.Parse(args, ["--zoom", "--bg", "--data-dir"], ["--odd"]), output, env),
                "validate" => ValidateCommand.Run(Options.Parse(args, [], []), output),
                "capture" => CaptureCommand.Run(Options.Parse(args, ["--monitor", "--size"], []), output),
                "compare" => CompareCommand.Run(Options.Parse(args, ["--tolerance"], []), output),
                "verify-overlay" => VerifyOverlayCommand.Run(
                    Options.Parse(args, ["--data-dir", "--preset", "--monitor", "--timeout", "--out"], ["--expect-hidden"]), output, env),
                "ico" => IcoCommand.Run(Options.Parse(args, [], []), output),
                _ => throw new UsageException($"unknown command '{args[0]}'"),
            };
        }
        catch (UsageException ex)
        {
            if (ex.Message.Length > 0)
            {
                error.WriteLine("error: " + ex.Message);
            }

            if (ex.ShowUsage)
            {
                error.WriteLine(Usage);
            }

            return 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            error.WriteLine("error: " + ex.Message);
            return 2;
        }
        catch (Exception ex) when (ex is Win32Exception or TimeoutException or InvalidOperationException)
        {
            error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }
}

internal sealed class UsageException(string? message, bool showUsage = true) : Exception(message ?? "")
{
    public bool ShowUsage { get; } = showUsage;
}
