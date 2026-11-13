using System.IO;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Xunit.Abstractions;

namespace CrosshairManager.Ui.Tests;

[Trait("Category", "Ui")]
public sealed class LocalizationTests(ITestOutputHelper output)
{
    /// <summary>Resource folder; env CHM_RESX_DIR (failure QA with a scratch copy) overrides the repository one.</summary>
    private static string ResxDir()
    {
        var dir = Environment.GetEnvironmentVariable("CHM_RESX_DIR");
        return !string.IsNullOrEmpty(dir) ? dir : Path.Combine(AppFixture.RepositoryRoot, "src", "CrosshairManager.App", "Resources");
    }

    private static Dictionary<string, string> Load(string path) =>
        XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")!.Value, StringComparer.Ordinal);

    /// <summary>l1: both resx files hold the same keys, no empty values and the same format placeholders. Reads XML only; creates no window.</summary>
    [Fact]
    public void l1_EnglishAndKoreanResxHaveTheSameKeys_NoEmptyValues_SamePlaceholders()
    {
        var en = Load(Path.Combine(ResxDir(), "Strings.resx"));
        var ko = Load(Path.Combine(ResxDir(), "Strings.ko.resx"));
        var problems = new List<string>();
        problems.AddRange(en.Keys.Except(ko.Keys).Select(k => "missing in Strings.ko.resx: " + k));
        problems.AddRange(ko.Keys.Except(en.Keys).Select(k => "missing in Strings.resx: " + k));
        foreach (var (name, map) in new[] { ("Strings.resx", en), ("Strings.ko.resx", ko) })
        {
            problems.AddRange(map.Where(p => string.IsNullOrWhiteSpace(p.Value)).Select(p => $"empty value in {name}: {p.Key}"));
        }

        foreach (var key in en.Keys.Intersect(ko.Keys))
        {
            var a = Placeholders(en[key]);
            var b = Placeholders(ko[key]);
            if (!a.SequenceEqual(b))
            {
                problems.Add($"placeholders differ for {key}: [{string.Join(',', a)}] vs [{string.Join(',', b)}]");
            }
        }

        output.WriteLine($"keys en={en.Count} ko={ko.Count} dir={ResxDir()}");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>l2: the Language combo switches the UI language live (TabSimple name = resx value) and persists settings.json "language".</summary>
    [Fact]
    public void l2_LanguageCombo_SwitchesLiveAndPersists()
    {
        var repo = Path.Combine(AppFixture.RepositoryRoot, "src", "CrosshairManager.App", "Resources");
        var enFile = Path.Combine(repo, "Strings.resx");
        var koFile = Path.Combine(repo, "Strings.ko.resx");
        using var app = new AppFixture();
        var window = app.SettingsWindow();
        var combo = AppFixture.SelectTab(window, "TabAdvanced", "Language").AsComboBox();

        foreach (var (code, nameKey, file) in new[] { ("ko", "Language.Korean", koFile), ("en", "Language.English", enFile) })
        {
            var item = Retry.WhileNull(() => combo.Items.FirstOrDefault(i => i.Name == Resx.Value(file, nameKey)) ?? combo.Items.FirstOrDefault(i => i.Name == Resx.Value(enFile, nameKey)), AppFixture.UiTimeout, throwOnTimeout: true).Result!;
            item.Select();
            var expected = Resx.Value(file, "Tab.Simple");
            Retry.WhileFalse(() => window.FindFirstDescendant(cf => cf.ByAutomationId("TabSimple"))?.Name == expected,
                AppFixture.UiTimeout, throwOnTimeout: true, timeoutMessage: $"TabSimple did not become '{expected}' for {code}");
            Retry.WhileFalse(() => (string?)JsonNode.Parse(File.ReadAllText(app.SettingsPath))?["language"] == code,
                AppFixture.UiTimeout, throwOnTimeout: true, timeoutMessage: "settings.json language is not " + code);
            var png = Path.Combine(AppFixture.EvidenceDir, $"task-17-crosshair-overlay-{code}.png");
            FlaUI.Core.Capturing.Capture.Element(window).ToFile(png);
            output.WriteLine($"{code}: TabSimple='{expected}' screenshot {png}");
        }
    }

    private static string[] Placeholders(string value) =>
        System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+\}").Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal).ToArray();
}
