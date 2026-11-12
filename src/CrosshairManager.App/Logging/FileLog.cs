using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CrosshairManager.App.Logging;

/// <summary>
/// Line log at <c>{dataDir}\logs\app.log</c>, rolled to <c>app.1.log</c> at 1 MB. Once the data folder is known to be
/// read-only (<see cref="SwitchToFallback"/>) every line goes to <c>%TEMP%\CrosshairManager-fallback.log</c> instead,
/// sentinel lines included. Files are opened with shared read/write so readers and a second process never block it.
/// </summary>
public sealed class FileLog
{
    public const long MaxBytes = 1024 * 1024;
    public const string FallbackFileName = "CrosshairManager-fallback.log";

    private const int Attempts = 3;
    private readonly object _lock = new();
    private bool _fallback;

    public FileLog(string dataDir, string? fallbackPath = null)
    {
        ArgumentNullException.ThrowIfNull(dataDir);
        LogDir = Path.Combine(Path.GetFullPath(dataDir), "logs");
        LogPath = Path.Combine(LogDir, "app.log");
        RolledPath = Path.Combine(LogDir, "app.1.log");
        FallbackPath = fallbackPath ?? Path.Combine(Path.GetTempPath(), FallbackFileName);
    }

    public string LogDir { get; }

    public string LogPath { get; }

    public string RolledPath { get; }

    public string FallbackPath { get; }

    public bool UsingFallback
    {
        get
        {
            lock (_lock)
            {
                return _fallback;
            }
        }
    }

    /// <summary>Routes all further lines to the fallback log and records <c>data dir read-only</c> there.</summary>
    public void SwitchToFallback(string reason)
    {
        lock (_lock)
        {
            _fallback = true;
            WriteFallback(Format("WARN", "data dir read-only: " + reason));
        }
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : message + ": " + ex);

    private static string Format(string level, string message) =>
        DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture) + " [" + level + "] " + message + Environment.NewLine;

    private void Write(string level, string message)
    {
        var line = Format(level, message);
        lock (_lock)
        {
            if (!_fallback)
            {
                if (TryWritePrimary(line, out var failure))
                {
                    return;
                }

                if (failure is UnauthorizedAccessException)
                {
                    _fallback = true;
                    WriteFallback(Format("WARN", "data dir read-only: cannot write " + LogPath + ": " + failure.Message));
                }
            }

            WriteFallback(line);
        }
    }

    private bool TryWritePrimary(string line, out Exception? failure)
    {
        failure = null;
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length >= MaxBytes)
                {
                    File.Move(LogPath, RolledPath, overwrite: true);
                }

                Append(LogPath, line);
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                failure = ex;
                return false;
            }
            catch (IOException ex)
            {
                // Sharing violations from a concurrent reader are retried; anything persistent goes to the fallback.
                failure = ex;
            }
        }

        return false;
    }

    private void WriteFallback(string line)
    {
        try
        {
            Append(FallbackPath, line);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine("CrosshairManager: fallback log unavailable (" + ex.Message + "): " + line);
        }
    }

    private static void Append(string path, string line)
    {
        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        var bytes = Encoding.UTF8.GetBytes(line);
        stream.Write(bytes, 0, bytes.Length);
    }
}
