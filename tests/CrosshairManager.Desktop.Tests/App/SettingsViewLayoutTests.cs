using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CrosshairManager.App;
using CrosshairManager.App.Localization;
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

    /// <summary>A detail line longer than the InfoBar is wide (an IOException message with a full path).</summary>
    private const string LongDetail =
        @"The process cannot access the file 'C:\Users\someone\AppData\Local\CrosshairManager\settings.json' because it is being used by another process. " +
        "Close the other program that has the file open, then open the settings window again to retry loading it.";

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

    /// <summary>
    /// The InfoBar shows the whole of its text at the minimum window size in both languages, with the notice alone and with
    /// a message plus a long detail: every TextBlock inside it gets at least the height its whole text needs at the width it
    /// was arranged with (a trimmed line would need a second line; a clipped block would need more height than it has).
    /// Renders TestResults/desktop/settings-view/infobar-{language}-{notice,message}.png.
    /// </summary>
    [Theory]
    [InlineData("ko")]
    [InlineData("en")]
    public void InfoBar_ShowsItsWholeText_AtTheMinimumWindowSize(string language)
    {
        var problems = WpfThread.Run(() =>
        {
            WpfThread.EnsureApplication();
            var previous = Loc.Instance.Culture.Name;
            Loc.Instance.SetLanguage(language);
            try
            {
                using var dir = new AppTestDir();
                var shell = new FakeAppShell(dir.Path);
                using var vm = new SettingsViewModel(shell, new FakeDialogs(), new FakeMonitors(), new ManualDebounce());
                var view = new SettingsView { DataContext = vm };
                var outDir = Path.Combine(EndToEndHarness.RepositoryRoot(), "TestResults", "desktop", "settings-view");
                Directory.CreateDirectory(outDir);
                var problems = new List<string>();
                foreach (var (state, pending, expectedIds) in new[]
                         {
                             ("notice", (PendingMessage?)null, new[] { "InfoBarNotice" }),
                             ("message", new PendingMessage("Settings.LoadFailed", LongDetail), new[] { "InfoBarNotice", "InfoBarMessage", "InfoBarDetail" }),
                         })
                {
                    if (pending is not null)
                    {
                        shell.Pending.Add(pending);
                        shell.Raise();
                    }

                    Layout(view);
                    var bar = Descendants(view).Single(e => AutomationProperties.GetAutomationId(e) == "InfoBar");
                    var blocks = Descendants(bar).OfType<TextBlock>().Where(b => IsShown(b, view)).ToList();
                    problems.AddRange(expectedIds.Except(blocks.Select(AutomationProperties.GetAutomationId)).Select(id => $"[{language}/{state}] {id} is not shown"));
                    foreach (var block in blocks)
                    {
                        var needed = HeightOfWholeText(block);
                        output.WriteLine($"[{language}/{state}] {Describe(block)}: {block.ActualWidth:0.#}x{block.ActualHeight:0.#}, whole text needs {needed:0.#} px, one line would be {WidthOfOneLine(block):0.#} px wide");
                        if (needed > block.ActualHeight + 0.5)
                        {
                            problems.Add($"[{language}/{state}] {Describe(block)} is {block.ActualHeight:0.#} px tall but its whole text needs {needed:0.#} px at {block.ActualWidth:0.#} px wide: '{block.Text}'");
                        }
                    }

                    var png = Path.Combine(outDir, $"infobar-{language}-{state}.png");
                    Save(view, png);
                    output.WriteLine("rendered " + png);
                }

                return problems;
            }
            finally
            {
                Loc.Instance.SetLanguage(previous);
            }
        });

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// A message with a long detail makes the InfoBar taller and the tabs shorter. Every always-present control inside the
    /// tabs must stay reachable at the minimum window size in both languages: drawn in full (nothing above it clips it
    /// away), or inside a ScrollViewer that scrolls it into its viewport (BringIntoView, then drawn in full). Renders
    /// TestResults/desktop/settings-view/tabs-{language}-{simple,advanced}-message.png with the lowest control scrolled into view.
    /// </summary>
    [Theory]
    [InlineData("ko")]
    [InlineData("en")]
    public void TabControls_StayReachable_WhenTheInfoBarShowsALongMessage(string language)
    {
        var (problems, advancedIds) = WpfThread.Run(() =>
        {
            WpfThread.EnsureApplication();
            var previous = Loc.Instance.Culture.Name;
            Loc.Instance.SetLanguage(language);
            try
            {
                using var dir = new AppTestDir();
                var shell = new FakeAppShell(dir.Path);
                using var vm = new SettingsViewModel(shell, new FakeDialogs(), new FakeMonitors(), new ManualDebounce());
                var view = new SettingsView { DataContext = vm };
                shell.Pending.Add(new PendingMessage("Settings.LoadFailed", LongDetail));
                shell.Raise();
                var outDir = Path.Combine(EndToEndHarness.RepositoryRoot(), "TestResults", "desktop", "settings-view");
                Directory.CreateDirectory(outDir);
                var wanted = AlwaysVisibleIds.ToHashSet(StringComparer.Ordinal);
                var problems = new List<string>();
                var advancedIds = new List<string>();
                foreach (var (index, name) in new[] { (0, "simple"), (1, "advanced") })
                {
                    view.Tabs.SelectedIndex = index;
                    Layout(view);
                    var controls = Descendants(view.Tabs)
                        .Where(e => IsShown(e, view) && wanted.Contains(AutomationProperties.GetAutomationId(e)))
                        .Select(e => (Element: e, Rect: Bounds(e, view)))
                        .ToList();
                    var scrolled = 0;
                    foreach (var (control, _) in controls)
                    {
                        var id = AutomationProperties.GetAutomationId(control);
                        if (index == 1)
                        {
                            advancedIds.Add(id);
                        }

                        if (IsDrawnInFull(control, view))
                        {
                            continue;
                        }

                        var scroller = Ancestors(control, view.Tabs).OfType<ScrollViewer>().FirstOrDefault();
                        if (scroller is null)
                        {
                            problems.Add($"[{language}/{name}] {id} {Bounds(control, view)} is clipped to {VisibleBounds(control, view)} and nothing scrolls it into view");
                            continue;
                        }

                        scrolled++;
                        control.BringIntoView(new Rect(0, 0, control.ActualWidth, control.ActualHeight));
                        view.UpdateLayout();
                        if (!IsDrawnInFull(control, view))
                        {
                            problems.Add($"[{language}/{name}] {id} {Bounds(control, view)} is still clipped to {VisibleBounds(control, view)} after BringIntoView (extent {scroller.ExtentHeight:0.#}, viewport {scroller.ViewportHeight:0.#}, offset {scroller.VerticalOffset:0.#})");
                        }
                    }

                    var lowest = controls.MaxBy(c => c.Rect.Bottom).Element;
                    lowest.BringIntoView(new Rect(0, 0, lowest.ActualWidth, lowest.ActualHeight));
                    view.UpdateLayout();
                    var viewer = Ancestors(lowest, view.Tabs).OfType<ScrollViewer>().FirstOrDefault();
                    output.WriteLine($"[{language}/{name}] {controls.Count} controls, {scrolled} needed scrolling; tabs {Bounds(view.Tabs, view)}; " +
                                     (viewer is null ? "no ScrollViewer" : $"ScrollViewer extent {viewer.ExtentHeight:0.#}, viewport {viewer.ViewportHeight:0.#}, scrollable {viewer.ScrollableHeight:0.#}, offset {viewer.VerticalOffset:0.#}"));
                    var png = Path.Combine(outDir, $"tabs-{language}-{name}-message.png");
                    Save(view, png);
                    output.WriteLine("rendered " + png);
                }

                return (problems, advancedIds);
            }
            finally
            {
                Loc.Instance.SetLanguage(previous);
            }
        });

        Assert.Contains("MonitorSelect", advancedIds);
        Assert.Contains("Language", advancedIds);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
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

    /// <summary>
    /// The part of the element that is actually drawn, in root coordinates: its bounds cut by every clip on it and its
    /// ancestors (a layout-clipped element keeps its full ActualHeight, so ActualHeight alone cannot tell).
    /// </summary>
    private static Rect VisibleBounds(FrameworkElement element, Visual root)
    {
        var rect = Bounds(element, root);
        for (DependencyObject? node = element; node is not null && node != root; node = VisualTreeHelper.GetParent(node))
        {
            if (node is Visual visual && VisualTreeHelper.GetClip(visual) is { } clip)
            {
                rect.Intersect(visual.TransformToAncestor(root).TransformBounds(clip.Bounds));
            }
        }

        return rect;
    }

    private static bool IsDrawnInFull(FrameworkElement element, Visual root) => Inside(Bounds(element, root), VisibleBounds(element, root));

    /// <summary>Rect containment with half a pixel of tolerance for layout rounding; an empty outer rect contains nothing.</summary>
    private static bool Inside(Rect inner, Rect outer) =>
        !outer.IsEmpty && inner.Left >= outer.Left - 0.5 && inner.Top >= outer.Top - 0.5 && inner.Right <= outer.Right + 0.5 && inner.Bottom <= outer.Bottom + 0.5;

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject element, DependencyObject stopAt)
    {
        for (var node = VisualTreeHelper.GetParent(element); node is not null && node != stopAt; node = VisualTreeHelper.GetParent(node))
        {
            yield return node;
        }
    }

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

    /// <summary>Height the block needs to show all of its text at its arranged width: a probe with the same text properties, wrapped and untrimmed, measured with unlimited height.</summary>
    private static double HeightOfWholeText(TextBlock block)
    {
        var probe = Probe(block, TextWrapping.Wrap);
        probe.Measure(new Size(block.ActualWidth, double.PositiveInfinity));
        return probe.DesiredSize.Height;
    }

    /// <summary>Width the block's text takes on a single unwrapped line (how far a translation is from fitting).</summary>
    private static double WidthOfOneLine(TextBlock block)
    {
        var probe = Probe(block, TextWrapping.NoWrap);
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize.Width;
    }

    private static TextBlock Probe(TextBlock block, TextWrapping wrapping) =>
        new()
        {
            Text = block.Text,
            FontFamily = block.FontFamily,
            FontSize = block.FontSize,
            FontStyle = block.FontStyle,
            FontWeight = block.FontWeight,
            FontStretch = block.FontStretch,
            Padding = block.Padding,
            LineHeight = block.LineHeight,
            LineStackingStrategy = block.LineStackingStrategy,
            FlowDirection = block.FlowDirection,
            UseLayoutRounding = block.UseLayoutRounding,
            TextWrapping = wrapping,
            TextTrimming = TextTrimming.None,
        };

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
