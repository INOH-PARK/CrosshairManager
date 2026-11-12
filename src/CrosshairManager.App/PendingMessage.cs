namespace CrosshairManager.App;

/// <summary>An InfoBar message waiting to be shown: a resx key plus optional untranslated detail (path, error text).</summary>
public sealed record PendingMessage(string Key, string? Detail = null);
