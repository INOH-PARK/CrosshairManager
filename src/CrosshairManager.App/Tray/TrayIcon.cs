using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using CrosshairManager.App.Localization;
using CrosshairManager.Core.Presets;
using H.NotifyIcon;

namespace CrosshairManager.App.Tray;

/// <summary>
/// Tray icon (H.NotifyIcon.Wpf), tooltip <c>CrosshairManager</c>, menu: TrayOpenSettings, TrayToggleOverlay (checked when visible),
/// TrayPresets (radio-style submenu, one checked), TrayExit. Double-click opens settings. Create and use on the UI thread.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    public const string ToolTip = "CrosshairManager";

    private readonly TaskbarIcon _icon;
    private readonly MenuItem _presets;
    private readonly MenuItem _toggle;

    public TrayIcon()
    {
        var open = Item("TrayOpenSettings", "Tray.OpenSettings");
        open.Click += (_, _) => OpenSettingsClicked?.Invoke(this, EventArgs.Empty);
        _toggle = Item("TrayToggleOverlay", "Tray.ToggleOverlay");
        _toggle.IsCheckable = false;
        _toggle.Click += (_, _) => ToggleOverlayClicked?.Invoke(this, EventArgs.Empty);
        _presets = Item("TrayPresets", "Tray.Presets");
        var exit = Item("TrayExit", "Tray.Exit");
        exit.Click += (_, _) => ExitClicked?.Invoke(this, EventArgs.Empty);

        var menu = new ContextMenu();
        menu.Items.Add(open);
        menu.Items.Add(_toggle);
        menu.Items.Add(_presets);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);

        _icon = new TaskbarIcon
        {
            ToolTipText = ToolTip,
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute)),
            ContextMenu = menu,
        };
        _icon.TrayMouseDoubleClick += (_, _) => OpenSettingsClicked?.Invoke(this, EventArgs.Empty);
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    public event EventHandler? OpenSettingsClicked;

    public event EventHandler? ToggleOverlayClicked;

    public event EventHandler? ExitClicked;

    public event EventHandler<string>? PresetClicked;

    /// <summary>Rebuilds the preset submenu and the toggle check mark.</summary>
    public void Update(IReadOnlyList<PresetEntry> presets, string activeId, bool overlayVisible)
    {
        ArgumentNullException.ThrowIfNull(presets);
        _toggle.IsChecked = overlayVisible;
        _presets.Items.Clear();
        foreach (var entry in presets)
        {
            var preset = entry.Preset;
            var item = new MenuItem
            {
                Header = preset is null ? entry.Id : DisplayName(preset),
                IsCheckable = true,
                IsChecked = string.Equals(entry.Id, activeId, StringComparison.Ordinal),
                IsEnabled = preset is not null,
            };
            AutomationProperties.SetAutomationId(item, "TrayPreset." + entry.Id);
            var id = entry.Id;
            item.Click += (_, _) => PresetClicked?.Invoke(this, id);
            _presets.Items.Add(item);
        }
    }

    public void Dispose() => _icon.Dispose();

    /// <summary>Built-in names are resx keys (<c>Preset.builtin.*</c>); user names are shown as typed.</summary>
    public static string DisplayName(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return preset.IsBuiltIn ? Loc.Instance[preset.Name] : preset.Name;
    }

    private static MenuItem Item(string automationId, string key)
    {
        var item = new MenuItem();
        item.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding("[" + key + "]") { Source = Loc.Instance });
        AutomationProperties.SetAutomationId(item, automationId);
        return item;
    }
}
