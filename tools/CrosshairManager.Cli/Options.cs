using System.Globalization;
using CrosshairManager.Core.Model;

namespace CrosshairManager.Cli;

/// <summary>Tiny argument reader: positionals, <c>--name value</c> options and <c>--flag</c> switches (no external parser).</summary>
internal sealed class Options
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);

    public string Command { get; private init; } = "";

    public List<string> Positional { get; } = [];

    /// <summary>Parses <c>args[1..]</c>; unknown options, missing values and duplicates are usage errors.</summary>
    public static Options Parse(string[] args, string[] valueOptions, string[] flagOptions)
    {
        var o = new Options { Command = args[0] };
        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (flagOptions.Contains(arg))
                {
                    if (!o._flags.Add(arg))
                    {
                        throw new UsageException($"{arg} given twice");
                    }
                }
                else if (valueOptions.Contains(arg))
                {
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new UsageException($"{arg} requires a value");
                    }

                    if (!o._values.TryAdd(arg, args[++i]))
                    {
                        throw new UsageException($"{arg} given twice");
                    }
                }
                else
                {
                    throw new UsageException($"unknown option '{arg}'");
                }
            }
            else
            {
                o.Positional.Add(arg);
            }
        }

        return o;
    }

    public void ExpectPositionals(int count)
    {
        if (Positional.Count != count)
        {
            throw new UsageException($"'{Command}' expects {count} argument(s) but got {Positional.Count}");
        }
    }

    public bool Flag(string name) => _flags.Contains(name);

    public string? Value(string name) => _values.GetValueOrDefault(name);

    public int Int(string name, int fallback, int min, int max)
    {
        var text = Value(name);
        if (text is null)
        {
            return fallback;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < min || n > max)
        {
            throw new UsageException($"{name} must be an integer {min}..{max}, got '{text}'");
        }

        return n;
    }

    public Rgb? Color(string name)
    {
        var text = Value(name);
        if (text is null)
        {
            return null;
        }

        return Rgb.TryParse(text, out var rgb) ? rgb : throw new UsageException($"{name} must be #RRGGBB, got '{text}'");
    }
}
