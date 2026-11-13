using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CrosshairManager.Core.Settings;

namespace CrosshairManager.App.Controls;

/// <summary>
/// Read-only TextBox that records a key chord on KeyDown. <c>Key.ImeProcessed</c> (Korean IME on) uses <c>ImeProcessedKey</c>,
/// <c>Key.System</c> (Alt / F10 chords) uses <c>SystemKey</c>. Backspace clears the gesture; Escape leaves it unchanged.
/// </summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly DependencyProperty GestureProperty = DependencyProperty.Register(
        nameof(Gesture), typeof(HotkeyGesture), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((HotkeyBox)d).Refresh()));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(HotkeyBox), new PropertyMetadata(string.Empty, (d, _) => ((HotkeyBox)d).Refresh()));

    public static readonly DependencyProperty IsFailedProperty = DependencyProperty.Register(
        nameof(IsFailed), typeof(bool), typeof(HotkeyBox), new PropertyMetadata(false));

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;
        Refresh();
    }

    public HotkeyGesture? Gesture
    {
        get => (HotkeyGesture?)GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    /// <summary>The last registration of this gesture failed (HotkeyError).</summary>
    public bool IsFailed
    {
        get => (bool)GetValue(IsFailedProperty);
        set => SetValue(IsFailedProperty, value);
    }

    /// <summary>Gesture text for one KeyDown, or null when the chord is incomplete or not a hotkey key. Pure, for tests.</summary>
    public static string? ChordText(Key key, ModifierKeys modifiers)
    {
        var name = KeyName(key);
        if (name is null)
        {
            return null;
        }

        var parts = new List<string>(5);
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(name);
        return string.Join('+', parts);
    }

    /// <summary>Resolves the real key behind IME / Alt processing.</summary>
    public static Key RealKey(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return e.Key switch
        {
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.System => e.SystemKey,
            _ => e.Key,
        };
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = RealKey(e);
        if (key == Key.Tab)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        if (key == Key.Back)
        {
            Gesture = null;
            return;
        }

        if (key == Key.Escape)
        {
            return;
        }

        var text = ChordText(key, Keyboard.Modifiers);
        if (text is not null && HotkeyGesture.TryParse(text, out var gesture))
        {
            Gesture = gesture;
        }
    }

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        e.Handled = true;
    }

    private static string? KeyName(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return ((char)('A' + (key - Key.A))).ToString();
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return (key - Key.D0).ToString(CultureInfo.InvariantCulture);
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            return "F" + (key - Key.F1 + 1).ToString(CultureInfo.InvariantCulture);
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return "NumPad" + (key - Key.NumPad0).ToString(CultureInfo.InvariantCulture);
        }

        return key switch
        {
            Key.Left => "Left",
            Key.Up => "Up",
            Key.Right => "Right",
            Key.Down => "Down",
            Key.Prior => "PageUp",
            Key.Next => "PageDown",
            Key.End => "End",
            Key.Home => "Home",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            _ => null,
        };
    }

    private void Refresh() => Text = Gesture?.ToString() ?? EmptyText;
}
