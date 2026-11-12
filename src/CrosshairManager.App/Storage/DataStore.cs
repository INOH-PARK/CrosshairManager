using System.IO;
using CrosshairManager.App.Logging;
using CrosshairManager.Core.Presets;
using CrosshairManager.Core.Settings;

namespace CrosshairManager.App.Storage;

/// <summary>
/// Settings and presets behind the "unwritable data folder" rule: failures to read or write never stop the app; it keeps
/// running in memory, logs to the fallback log and queues <c>Error.DataDirReadOnly</c>.
/// </summary>
public sealed class DataStore(string dataDir, FileLog log, TimeProvider? time = null)
{
    private readonly SettingsStore _settings = new(dataDir, time);
    private readonly List<PendingMessage> _messages = [];

    public string DataDir { get; } = Path.GetFullPath(dataDir);

    public PresetStore Presets { get; } = new(dataDir, time);

    public bool ReadOnly { get; private set; }

    public IReadOnlyList<PendingMessage> Messages => _messages;

    /// <summary>Creates the folder and a probe file; on failure switches to read-only mode.</summary>
    public bool ProbeWritable()
    {
        var probe = Path.Combine(DataDir, ".write-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MarkReadOnly(ex);
            return false;
        }
    }

    public AppSettings LoadSettings()
    {
        SettingsLoadResult result;
        try
        {
            result = _settings.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unwritable folder (Load writes defaults when the file is missing) or a corrupt-file backup name collision.
            log.Error("settings load failed; using defaults in memory", ex);
            if (!ReadOnly)
            {
                _messages.Add(new PendingMessage("Settings.LoadFailed", ex.Message));
            }

            return AppSettings.Default;
        }

        switch (result.Status)
        {
            case SettingsLoadStatus.RecoveredFromCorrupt corrupt:
                log.Info("settings.json was corrupt; moved to " + corrupt.BackupPath);
                _messages.Add(new PendingMessage("Settings.RecoveredFromCorrupt", corrupt.BackupPath));
                break;
            case SettingsLoadStatus.NewerVersion newer:
                log.Info("settings.json has newer schema version " + newer.Version + "; using defaults in memory");
                _messages.Add(new PendingMessage("Settings.NewerVersion", newer.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                break;
        }

        foreach (var warning in result.Warnings)
        {
            log.Info("settings: " + warning);
            _messages.Add(new PendingMessage("Settings.Warning", warning));
        }

        return result.Settings;
    }

    /// <summary>Saves unless the folder is read-only; a failed save switches to read-only mode. Returns whether it was written.</summary>
    public bool TrySaveSettings(AppSettings settings)
    {
        if (ReadOnly)
        {
            return false;
        }

        try
        {
            _settings.Save(settings);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MarkReadOnly(ex);
            return false;
        }
    }

    public IReadOnlyList<PresetEntry> LoadPresets()
    {
        try
        {
            return Presets.LoadAll();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Error("presets folder unreadable; only built-in presets are available", ex);
            _messages.Add(new PendingMessage("Presets.LoadFailed", ex.Message));
            return BuiltInPresets.All.Select(p => new PresetEntry(p.Id, null, new PresetLoadResult.Ok(p, []))).ToList();
        }
    }

    /// <summary>Records a read-only data folder once: fallback log line and the InfoBar message.</summary>
    public void MarkReadOnly(Exception reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        if (ReadOnly)
        {
            return;
        }

        ReadOnly = true;
        log.SwitchToFallback(DataDir + ": " + reason.Message);
        _messages.Add(new PendingMessage("Error.DataDirReadOnly", DataDir));
    }
}
