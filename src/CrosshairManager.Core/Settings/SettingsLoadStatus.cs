namespace CrosshairManager.Core.Settings;

/// <summary>What happened when <c>settings.json</c> was loaded.</summary>
public abstract record SettingsLoadStatus
{
    /// <summary>The file was read and applied.</summary>
    public sealed record Ok : SettingsLoadStatus;

    /// <summary>No file existed; defaults were written.</summary>
    public sealed record CreatedDefault : SettingsLoadStatus;

    /// <summary>The file was unreadable and was moved to <paramref name="BackupPath"/>; defaults are used.</summary>
    public sealed record RecoveredFromCorrupt(string BackupPath) : SettingsLoadStatus;

    /// <summary>The file was written by a newer version; defaults are used in memory and the file is untouched.</summary>
    public sealed record NewerVersion(int Version) : SettingsLoadStatus;
}
