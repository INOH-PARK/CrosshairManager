using Microsoft.Win32;

namespace CrosshairManager.App.Startup;

/// <summary>The value store behind <see cref="AutoStart"/>: HKCU Run key in production, a dictionary in tests.</summary>
public interface IRunKey
{
    string? Get(string name);

    void Set(string name, string value);

    void Delete(string name);
}

/// <summary><c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.</summary>
public sealed class RegistryRunKey : IRunKey
{
    public const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? Get(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        return key?.GetValue(name) as string;
    }

    public void Set(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void Delete(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}

/// <summary>Start with Windows: value <c>CrosshairManager</c> = <c>"&lt;exe path&gt;"</c>; rewritten when the path differs, removed when off.</summary>
public sealed class AutoStart(IRunKey key, string exePath)
{
    public const string ValueName = "CrosshairManager";

    public string Command { get; } = "\"" + exePath + "\"";

    /// <summary>Brings the Run value in line with <paramref name="enabled"/>. Returns whether the registry was changed.</summary>
    public bool Apply(bool enabled)
    {
        var current = key.Get(ValueName);
        if (enabled)
        {
            if (string.Equals(current, Command, StringComparison.Ordinal))
            {
                return false;
            }

            key.Set(ValueName, Command);
            return true;
        }

        if (current is null)
        {
            return false;
        }

        key.Delete(ValueName);
        return true;
    }
}
