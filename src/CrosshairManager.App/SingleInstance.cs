using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CrosshairManager.Core.Settings;
using CrosshairManager.Platform.Windowing;

namespace CrosshairManager.App;

/// <summary>
/// One instance per normalized data folder: mutex <c>Local\CrosshairManager-&lt;16 hex&gt;</c> and auto-reset event
/// <c>...-open</c>. The owner writes its PID to <c>{dataDir}\instance.pid</c>; a second launch grants it the foreground
/// (AllowSetForegroundWindow) and signals the event.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    public const string PidFileName = "instance.pid";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _open;
    private readonly string _pidFile;
    private RegisteredWaitHandle? _wait;
    private bool _disposed;

    private SingleInstance(Mutex mutex, EventWaitHandle open, string pidFile)
    {
        _mutex = mutex;
        _open = open;
        _pidFile = pidFile;
    }

    public static (string Mutex, string OpenEvent) Names(string dataDir)
    {
        var normalized = DataDirectory.Normalize(dataDir);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
        var name = @"Local\CrosshairManager-" + hash;
        return (name, name + "-open");
    }

    /// <summary>
    /// Returns the owner handle, or null after signaling the running owner (the caller then exits 0).
    /// <paramref name="pidWriteError"/> is set when the owner could not write <c>instance.pid</c>.
    /// </summary>
    public static SingleInstance? TryAcquire(string dataDir, out Exception? pidWriteError)
    {
        pidWriteError = null;
        var (mutexName, eventName) = Names(dataDir);
        var open = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        var mutex = new Mutex(true, mutexName, out var createdNew);
        var pidFile = Path.Combine(Path.GetFullPath(dataDir), PidFileName);
        if (!createdNew)
        {
            mutex.Dispose();
            Foreground.AllowFor(ReadPid(pidFile) ?? Foreground.AnyProcess);
            open.Set();
            open.Dispose();
            return null;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pidFile)!);
            File.WriteAllText(pidFile, Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            pidWriteError = ex;
        }

        return new SingleInstance(mutex, open, pidFile);
    }

    /// <summary>Invokes <paramref name="onOpenRequested"/> on a thread-pool thread each time a second launch signals.</summary>
    public void Listen(Action onOpenRequested)
    {
        ArgumentNullException.ThrowIfNull(onOpenRequested);
        _wait = ThreadPool.RegisterWaitForSingleObject(_open, (_, _) => onOpenRequested(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>Call on the thread that created the instance (it owns the mutex).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _wait?.Unregister(null);
        if (ReadPid(_pidFile) == Environment.ProcessId)
        {
            try
            {
                File.Delete(_pidFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.WriteLine("CrosshairManager: could not delete " + _pidFile + ": " + ex.Message);
            }
        }

        _mutex.ReleaseMutex();
        _mutex.Dispose();
        _open.Dispose();
    }

    private static int? ReadPid(string pidFile)
    {
        try
        {
            return int.TryParse(File.ReadAllText(pidFile).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var pid) ? pid : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
