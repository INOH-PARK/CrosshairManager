using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CrosshairManager.App.ViewModels;
using CrosshairManager.App.Views;
using CrosshairManager.Desktop.Tests.EndToEnd;
using Xunit.Abstractions;

namespace CrosshairManager.Desktop.Tests.App;

/// <summary>
/// Headless layout check of SettingsView at the 900x620 window's client size (884x580): every always-visible fixed
/// AutomationId exists on both tabs, lies inside the view, is visible, and leaf controls that share a parent do not
/// overlap. Renders each tab to TestResults/desktop/settings-view/{simple,advanced}.png. No window is created.
/// </summary>
[Trait("Category", "Desktop")]
public sealed class SettingsViewLayoutTests(ITestOutputHelper output)
{
    public const double ClientWidth = 884;
    public const double ClientHeight = 580;

    /// <summary>Must have ids minus the conditional ones (Disclaimer*, Rename*, ConfirmDelete*, ShareCodeError, MonitorWarning, HotkeyError, Tray*).</summary>
    public static readonly string[] AlwaysVisibleIds =
    [
        "PresetList", "NewPreset", "DuplicatePreset", "RenamePreset", "DeletePreset", "ImportPresetFile", "ExportPresetFile",
        "CopyShareCode", "ShareCodeInput", "PasteShareCode", "TabSimple", "TabAdvanced",
        "Swatch0", "Swatch1", "Swatch2", "Swatch3", "Swatch4", "Swatch5", "Swatch6", "Swatch7", "SimpleScale", "SimpleOpacity", "SimpleOutline",
        "ColorHex", "ColorR", "ColorG", "ColorB", "DotEnabled", "DotSize", "DotOpacity", "LinkLines", "TStyle",
        "Top.Enabled", "Top.Length", "Top.Thickness", "Top.Gap",
        "Bottom.Enabled", "Bottom.Length", "Bottom.Thickness", "Bottom.Gap",
        "Left.Enabled", "Left.Length", "Left.Thickness", "Left.Gap",
        "Right.Enabled", "Right.Length", "Right.Thickness", "Right.Gap",
        "OutlineEnabled", "OutlineThickness", "OutlineColorHex", "OutlineOpacity", "Opacity", "Scale", "Rotation", "OffsetX", "OffsetY",
        "MonitorSelect", "HotkeyToggle", "HotkeyNext", "HotkeyPrev", "HotkeyOpenSettings", "Language", "StartWithWindows", "OverlayVisible",
        "Preview", "InfoBar",
    ];

    public static readonly string[] SimpleTabIds =
    [
        "Swatch0", "Swatch1", "Swatch2", "Swatch3", "Swatch4", "Swatch5", "Swatch6", "Swatch7", "SimpleScale", "SimpleOpacity", "SimpleOutline",
    ];

    [Fact]
    public void BothTabs_ShowEveryAlwaysVisibleId_InsideTheView_WithoutOverlappingLeafSiblings()
    {
        var problems = WpfThread.Run(() =>
        {
            WpfThread.EnsureApplication();
            using var dir = new AppTestDir();
            var shell = new FakeAppShell(dir.Path);
            using var vm = new SettingsViewModel(shell, new FakeDialogs(), new FakeMonitors(), new ManualDebounce());
            var view = new SettingsView { DataContext = vm };
            Layout(view);

            var problems = new List<string>();
            var found = new HashSet<string>(StringComparer.Ordinal);
            var outDir = Path.Combine(EndToEndHarness.RepositoryRoot(), "TestResults", "desktop", "settings-view");
            Directory.CreateDirectory(outDir);
            foreach (var (index, name) in new[] { (0, "simple"), (1, "advanced") })
            {
                view.Tabs.SelectedIndex = index;
                Layout(view);
                var shown = Descendants(view).Where(e => IsShown(e, view)).ToList();
                foreach (var element in shown)
                {
                    var id = AutomationProperties.GetAutomationId(element);
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    found.Add(id);
                    var rect = Bounds(element, view);
                    if (rect.Width < 1 || rect.Height < 1 || rect.Left < 0 || rect.Top < 0 || rect.Right > ClientWidth + 0.5 || rect.Bottom > ClientHeight + 0.5)
                    {
                        problems.Add($"[{name}] {id} is not inside the view: {rect}");
                    }
                }

                problems.AddRange(LeafSiblingOverlaps(view, name));
                var png = Path.Combine(outDir, name + ".png");
                Save(view, png);
                output.WriteLine("rendered " + png);
            }

            problems.AddRange(AlwaysVisibleIds.Except(found).Select(id => "missing AutomationId " + id));
            return problems;
        });

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void SimpleTab_HidesAdvancedIds_AndAdvancedTab_HidesSimpleIds()
    {
        var (onSimple, onAdvanced) = WpfThread.Run(() =>
        {
            WpfThread.EnsureApplication();
            using var dir = new AppTestDir();
            var shell = new FakeAppShell(dir.Path);
            using var vm = new SettingsViewModel(shell, new FakeDialogs(), new FakeMonitors(), new ManualDebounce());
            var view = new SettingsView { DataContext = vm };
            view.Tabs.SelectedIndex = 0;
            Layout(view);
            var simple = ShownIds(view);
            view.Tabs.SelectedIndex = 1;
            Layout(view);
            return (simple, ShownIds(view));
        });

        Assert.Superset(SimpleTabIds.ToHashSet(StringComparer.Ordinal), onSimple);
        Assert.DoesNotContain("ColorHex", onSimple);
        Assert.Contains("ColorHex", onAdvanced);
        Assert.DoesNotContain("Swatch0", onAdvanced);
        Assert.Contains("PresetList", onSimple);
        Assert.Contains("PresetList", onAdvanced);
    }

    /// <summary>Peers fall back to x:Name when no AutomationId is set, so a named host could shadow the preview Image in UI Automation.</summary>
    [Fact]
    public void Preview_AutomationIdBelongsOnlyToThePreviewImage()
    {
        var owners = WpfThread.Run(() =>
        {
            WpfThread.EnsureApplication();
            using var dir = new AppTestDir();
            var shell = new FakeAppShell(dir.Path);
            using var vm = new SettingsViewModel(shell, new FakeDialogs(), new FakeMonitors(), new ManualDebounce());
            var view = new SettingsView { DataContext = vm };
            Layout(view);
            return Descendants(view).OfType<UIElement>()
                .Where(e => System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(e)?.GetAutomationId() == "Preview")
                .Select(e => e.GetType().Name)
                .ToList();
        });

        Assert.Equal([nameof(Image)], owners);
    }

    [Fact]
    public void Preview_IsEightDevicePixelsPerSourcePixel()
    {
        var (imageWidth, bitmapWidth, imageHeight, bitmapHeight) = WpfThread.Run(() =>
        {
            WpfThread.EnsureApplication();
            using var dir = new AppTestDir();
            var shell = new FakeAppShell(dir.Path);
            using var vm = new SettingsViewModel(shell, new FakeDialogs(), new FakeMonitors(), new ManualDebounce());
            var view = new SettingsView { DataContext = vm };
            Layout(view);
            var image = Descendants(view).OfType<Image>().Single(i => AutomationProperties.GetAutomationId(i) == "Preview");
            var dpi = VisualTreeHelper.GetDpi(image);
            return (image.ActualWidth * dpi.DpiScaleX, view.Preview.Bitmap!.PixelWidth, image.ActualHeight * dpi.DpiScaleY, view.Preview.Bitmap.PixelHeight);
        });

        Assert.Equal(bitmapWidth * 8, imageWidth, 0.01);
        Assert.Equal(bitmapHeight * 8, imageHeight, 0.01);
        Assert.Equal(20, bitmapWidth);
    }

    private static void Layout(UIElement view)
    {
        view.Measure(new Size(ClientWidth, ClientHeight));
        view.Arrange(new Rect(0, 0, ClientWidth, ClientHeight));
        view.UpdateLayout();
    }

    private static HashSet<string> ShownIds(SettingsView view) =>
        Descendants(view).Where(e => IsShown(e, view))
            .Select(AutomationProperties.GetAutomationId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe)
            {
                yield return fe;
            }

            foreach (var inner in Descendants(child))
            {
                yield return inner;
            }
        }
    }

    private static bool IsShown(FrameworkElement element, FrameworkElement root)
    {
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return false;
        }

        for (DependencyObject? node = element; node is not null && node != root; node = VisualTreeHelper.GetParent(node))
        {
            if (node is UIElement ui && ui.Visibility != Visibility.Visible)
            {
                return false;
            }
        }

        return true;
    }

    private static Rect Bounds(FrameworkElement element, Visual root) =>
        element.TransformToAncestor(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

    /// <summary>Approximates the UIA rule: leaf controls (TextBlock, TextBox, Button, CheckBox, ComboBox, Slider, Image) with the same visual parent must not intersect.</summary>
    private static IEnumerable<string> LeafSiblingOverlaps(SettingsView view, string tab)
    {
        static bool IsLeaf(FrameworkElement e) => e is TextBlock or TextBoxBase or ButtonBase or ComboBox or Slider or Image;
        var groups = Descendants(view)
            .Where(e => IsLeaf(e) && IsShown(e, view))
            .GroupBy(e => VisualTreeHelper.GetParent(e));
        foreach (var group in groups)
        {
            var items = group.Select(e => (Element: e, Rect: Shrink(Bounds(e, view)))).ToList();
            for (var i = 0; i < items.Count; i++)
            {
                for (var j = i + 1; j < items.Count; j++)
                {
                    if (items[i].Rect.IntersectsWith(items[j].Rect))
                    {
                        yield return $"[{tab}] {Describe(items[i].Element)} overlaps {Describe(items[j].Element)}";
                    }
                }
            }
        }
    }

    private static Rect Shrink(Rect r) => r.Width > 2 && r.Height > 2 ? new Rect(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2) : Rect.Empty;

    private static string Describe(FrameworkElement e)
    {
        var id = AutomationProperties.GetAutomationId(e);
        var text = e switch { TextBlock t => t.Text, _ => string.Empty };
        return $"{e.GetType().Name}({(string.IsNullOrEmpty(id) ? text : id)})";
    }

    private static void Save(FrameworkElement view, string path)
    {
        var bitmap = new RenderTargetBitmap((int)ClientWidth, (int)ClientHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
