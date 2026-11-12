using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrosshairManager.Core.IO;

namespace CrosshairManager.Core.Settings;

public sealed record SettingsLoadResult(AppSettings Settings, SettingsLoadStatus Status, IReadOnlyList<string> Warnings);

/// <summary><c>{dataDir}/settings.json</c>. Corrupt files are moved aside; newer-version files are never rewritten.</summary>
public sealed class SettingsStore(string dataDir, TimeProvider? time = null)
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly string _dir = Path.GetFullPath(dataDir);

    private string FilePath => Path.Combine(_dir, "settings.json");

    public SettingsLoadResult Load()
    {
        if (!File.Exists(FilePath))
        {
            Save(AppSettings.Default);
            return new SettingsLoadResult(AppSettings.Default, new SettingsLoadStatus.CreatedDefault(), []);
        }

        var warnings = new List<string>();
        AppSettings settings;
        try
        {
            using var stream = File.OpenRead(FilePath);
            var root = JsonNode.Parse(stream) as JsonObject ?? throw new FormatException("settings.json must hold a JSON object.");
            int version = root["schemaVersion"]?.GetValue<int>() ?? 1;
            if (version > CurrentVersion)
            {
                return new SettingsLoadResult(AppSettings.Default, new SettingsLoadStatus.NewerVersion(version), []);
            }

            settings = Map(root, warnings);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            string backup = FilePath + ".corrupt-" + _time.GetLocalNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            File.Move(FilePath, backup);
            return new SettingsLoadResult(AppSettings.Default, new SettingsLoadStatus.RecoveredFromCorrupt(backup), []);
        }

        return new SettingsLoadResult(settings, new SettingsLoadStatus.Ok(), warnings);
    }

    /// <exception cref="IOException">The data folder or file cannot be written.</exception>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(_dir);
        AtomicFile.WriteAllText(FilePath, ToJson(settings).ToJsonString(WriteOptions));
    }

    private static AppSettings Map(JsonObject root, List<string> warnings)
    {
        var d = AppSettings.Default;
        var monitor = root["monitor"] as JsonObject;
        var hotkeys = root["hotkeys"] as JsonObject;
        var placement = root["settingsWindowPlacement"] as JsonObject;
        return new AppSettings
        {
            ActivePresetId = root["activePresetId"]?.GetValue<string>() ?? d.ActivePresetId,
            OverlayVisible = root["overlayVisible"]?.GetValue<bool>() ?? d.OverlayVisible,
            Monitor = monitor is null ? d.Monitor : ReadMonitor(monitor),
            Hotkeys = hotkeys is null ? d.Hotkeys : ReadHotkeys(hotkeys, warnings),
            Language = ReadLanguage(root["language"]?.GetValue<string>(), warnings),
            StartWithWindows = root["startWithWindows"]?.GetValue<bool>() ?? d.StartWithWindows,
            DisclaimerAccepted = root["disclaimerAccepted"]?.GetValue<bool>() ?? d.DisclaimerAccepted,
            SettingsWindowPlacement = placement is null ? null : ReadPlacement(placement),
        };
    }

    private static MonitorSelection ReadMonitor(JsonObject monitor)
    {
        string? mode = monitor["mode"]?.GetValue<string>();
        string? deviceName = monitor["deviceName"]?.GetValue<string>();
        return mode == "device" && deviceName is not null
            ? new MonitorSelection(MonitorMode.Device, deviceName)
            : MonitorSelection.Primary;
    }

    private static HotkeySettings ReadHotkeys(JsonObject hotkeys, List<string> warnings)
    {
        var d = HotkeySettings.Default;
        return new HotkeySettings
        {
            ToggleOverlay = ReadGesture(hotkeys, "toggleOverlay", d.ToggleOverlay, warnings),
            NextPreset = ReadGesture(hotkeys, "nextPreset", d.NextPreset, warnings),
            PreviousPreset = ReadGesture(hotkeys, "previousPreset", d.PreviousPreset, warnings),
            OpenSettings = ReadGesture(hotkeys, "openSettings", d.OpenSettings, warnings),
        };
    }

    private static HotkeyGesture? ReadGesture(JsonObject hotkeys, string name, HotkeyGesture? fallback, List<string> warnings)
    {
        if (!hotkeys.ContainsKey(name))
        {
            return fallback;
        }

        string? text = hotkeys[name]?.GetValue<string>();
        if (text is null)
        {
            return null;
        }

        if (HotkeyGesture.TryParse(text, out var gesture))
        {
            return gesture;
        }

        warnings.Add($"hotkeys.{name}: '{text}' is not a valid hotkey; it is left unbound.");
        return null;
    }

    private static string ReadLanguage(string? language, List<string> warnings)
    {
        if (language is null or "system" or "ko" or "en")
        {
            return language ?? "system";
        }

        warnings.Add($"language: '{language}' is not supported; using 'system'.");
        return "system";
    }

    private static WindowPlacement? ReadPlacement(JsonObject p) =>
        p["left"]?.GetValue<int>() is int left
        && p["top"]?.GetValue<int>() is int top
        && p["width"]?.GetValue<int>() is int width
        && p["height"]?.GetValue<int>() is int height
            ? new WindowPlacement(left, top, width, height)
            : null;

    private static JsonObject ToJson(AppSettings s) => new()
    {
        ["schemaVersion"] = CurrentVersion,
        ["activePresetId"] = s.ActivePresetId,
        ["overlayVisible"] = s.OverlayVisible,
        ["monitor"] = new JsonObject
        {
            ["mode"] = s.Monitor.Mode == MonitorMode.Device ? "device" : "primary",
            ["deviceName"] = s.Monitor.DeviceName,
        },
        ["hotkeys"] = new JsonObject
        {
            ["toggleOverlay"] = s.Hotkeys.ToggleOverlay?.ToString(),
            ["nextPreset"] = s.Hotkeys.NextPreset?.ToString(),
            ["previousPreset"] = s.Hotkeys.PreviousPreset?.ToString(),
            ["openSettings"] = s.Hotkeys.OpenSettings?.ToString(),
        },
        ["language"] = s.Language,
        ["startWithWindows"] = s.StartWithWindows,
        ["disclaimerAccepted"] = s.DisclaimerAccepted,
        ["settingsWindowPlacement"] = s.SettingsWindowPlacement is { } p
            ? new JsonObject
            {
                ["left"] = p.Left,
                ["top"] = p.Top,
                ["width"] = p.Width,
                ["height"] = p.Height,
            }
            : null,
    };
}
