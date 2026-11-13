using System.Drawing;
using System.IO;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using Xunit.Abstractions;

namespace CrosshairManager.Ui.Tests;

/// <summary>u1: every always-visible fixed AutomationId exists on both tabs, is on screen inside the window, and leaf siblings do not overlap.</summary>
[Trait("Category", "Ui")]
public sealed class SettingsWindowTests(ITestOutputHelper output) : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public void u1_AlwaysVisibleAutomationIds_AreOnScreenInsideTheWindow_AndLeafSiblingsDoNotOverlap()
    {
        var window = _app.SettingsWindow();
        var expected = FixedIds.AlwaysVisible.ToHashSet(StringComparer.Ordinal);
        var found = new HashSet<string>(StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var (tabId, probeId, name) in new[] { ("TabSimple", "Swatch0", "simple"), ("TabAdvanced", "ColorHex", "advanced") })
        {
            AppFixture.SelectTab(window, tabId, probeId);
            var frame = window.BoundingRectangle;
            frame.Inflate(1, 1);
            foreach (var element in window.FindAllDescendants())
            {
                var id = element.Properties.AutomationId.ValueOrDefault;
                if (string.IsNullOrEmpty(id) || !expected.Contains(id))
                {
                    continue;
                }

                found.Add(id);
                var rect = element.Properties.BoundingRectangle.ValueOrDefault;
                if (element.Properties.IsOffscreen.ValueOrDefault)
                {
                    problems.Add($"[{name}] {id} IsOffscreen");
                }

                if (rect.Width <= 0 || rect.Height <= 0 || !frame.Contains(rect))
                {
                    problems.Add($"[{name}] {id} bounds {rect} are not inside the window {frame}");
                }
            }

            problems.AddRange(LeafSiblingOverlaps(window, name));
            var png = Path.Combine(AppFixture.EvidenceDir, $"task-16-crosshair-overlay-{name}.png");
            Capture.Element(window).ToFile(png);
            output.WriteLine("screenshot " + png);
        }

        problems.AddRange(expected.Except(found).Select(id => "missing AutomationId " + id));
        output.WriteLine($"ids found: {found.Count}/{expected.Count}");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>UIA control view: leaf elements (no children) that share a parent must not overlap (1 px shrink absorbs layout rounding).</summary>
    private static IEnumerable<string> LeafSiblingOverlaps(AutomationElement root, string tab)
    {
        var pending = new Stack<AutomationElement>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var parent = pending.Pop();
            var children = parent.FindAllChildren();
            var leaves = new List<(AutomationElement Element, Rectangle Rect)>();
            foreach (var child in children)
            {
                if (child.FindAllChildren().Length > 0)
                {
                    pending.Push(child);
                    continue;
                }

                var rect = child.Properties.BoundingRectangle.ValueOrDefault;
                if (!child.Properties.IsOffscreen.ValueOrDefault && rect.Width > 2 && rect.Height > 2)
                {
                    rect.Inflate(-1, -1);
                    leaves.Add((child, rect));
                }
            }

            for (var i = 0; i < leaves.Count; i++)
            {
                for (var j = i + 1; j < leaves.Count; j++)
                {
                    if (leaves[i].Rect.IntersectsWith(leaves[j].Rect))
                    {
                        yield return $"[{tab}] {Describe(leaves[i].Element)} overlaps {Describe(leaves[j].Element)} under {Describe(parent)}";
                    }
                }
            }
        }
    }

    private static string Describe(AutomationElement e)
    {
        var id = e.Properties.AutomationId.ValueOrDefault;
        var name = e.Properties.Name.ValueOrDefault;
        return $"{e.Properties.ControlType.ValueOrDefault}({(string.IsNullOrEmpty(id) ? name : id)})";
    }
}
