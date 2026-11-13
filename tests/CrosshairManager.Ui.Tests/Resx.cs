using System.IO;
using System.Xml.Linq;

namespace CrosshairManager.Ui.Tests;

/// <summary>Reads values from the app's resx files so tests never hard-code translated text.</summary>
internal static class Resx
{
    public static string English(string key) => Value(Path.Combine(AppFixture.RepositoryRoot, "src", "CrosshairManager.App", "Resources", "Strings.resx"), key);

    public static string Value(string resxPath, string key)
    {
        var data = XDocument.Load(resxPath).Root!
            .Elements("data")
            .FirstOrDefault(d => (string?)d.Attribute("name") == key)
            ?? throw new KeyNotFoundException($"{key} is not in {resxPath}");
        return data.Element("value")!.Value;
    }
}
