using System.Diagnostics.CodeAnalysis;

namespace CrosshairManager.Core.Settings;

/// <summary>A global hotkey chord: modifier set plus one virtual key. Values match Win32 MOD_* and VK_*.</summary>
public sealed record HotkeyGesture
{
    public const uint ModAlt = 0x1;
    public const uint ModControl = 0x2;
    public const uint ModShift = 0x4;
    public const uint ModWin = 0x8;

    private static readonly Dictionary<string, uint> Keys = BuildKeys();

    private HotkeyGesture(uint modifiers, uint virtualKey, string keyName)
    {
        Modifiers = modifiers;
        VirtualKey = virtualKey;
        KeyName = keyName;
    }

    /// <summary>Win32 MOD_* flags (without MOD_NOREPEAT).</summary>
    public uint Modifiers { get; }

    /// <summary>Win32 virtual-key code.</summary>
    public uint VirtualKey { get; }

    private string KeyName { get; }

    public static bool TryParse(string? text, [NotNullWhen(true)] out HotkeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        uint modifiers = 0;
        string? keyName = null;
        uint vk = 0;
        foreach (var raw in text.Split('+'))
        {
            var part = raw.Trim();
            if (part.Length == 0)
            {
                return false;
            }

            var flag = part.ToUpperInvariant() switch
            {
                "CTRL" => ModControl,
                "ALT" => ModAlt,
                "SHIFT" => ModShift,
                "WIN" => ModWin,
                _ => 0u,
            };
            if (flag != 0)
            {
                if ((modifiers & flag) != 0 || keyName is not null)
                {
                    return false;
                }

                modifiers |= flag;
                continue;
            }

            if (keyName is not null || !Keys.TryGetValue(part, out vk))
            {
                return false;
            }

            keyName = Canonical(part);
        }

        if (keyName is null)
        {
            return false;
        }

        var isF13Plus = vk >= 0x7C && vk <= 0x87;
        if (modifiers == 0 && !isF13Plus)
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, vk, keyName);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>(5);
        if ((Modifiers & ModControl) != 0) parts.Add("Ctrl");
        if ((Modifiers & ModAlt) != 0) parts.Add("Alt");
        if ((Modifiers & ModShift) != 0) parts.Add("Shift");
        if ((Modifiers & ModWin) != 0) parts.Add("Win");
        parts.Add(KeyName);
        return string.Join('+', parts);
    }

    private static string Canonical(string key)
    {
        foreach (var name in CanonicalNames)
        {
            if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return key.ToUpperInvariant();
    }

    private static readonly string[] CanonicalNames =
    [
        "Left", "Right", "Up", "Down", "Home", "End", "PageUp", "PageDown", "Insert", "Delete",
        "NumPad0", "NumPad1", "NumPad2", "NumPad3", "NumPad4", "NumPad5", "NumPad6", "NumPad7", "NumPad8", "NumPad9",
    ];

    private static Dictionary<string, uint> BuildKeys()
    {
        var map = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28,
            ["PageUp"] = 0x21, ["PageDown"] = 0x22, ["End"] = 0x23, ["Home"] = 0x24,
            ["Insert"] = 0x2D, ["Delete"] = 0x2E,
        };
        for (var c = 'A'; c <= 'Z'; c++) map[c.ToString()] = c;
        for (var c = '0'; c <= '9'; c++) map[c.ToString()] = c;
        for (var i = 1; i <= 24; i++) map["F" + i] = (uint)(0x70 + i - 1);
        for (var i = 0; i <= 9; i++) map["NumPad" + i] = (uint)(0x60 + i);
        return map;
    }
}
