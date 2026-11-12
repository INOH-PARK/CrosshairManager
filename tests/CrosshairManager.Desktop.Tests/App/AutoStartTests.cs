using CrosshairManager.App.Startup;

namespace CrosshairManager.Desktop.Tests.App;

[Trait("Category", "Desktop")]
public class AutoStartTests
{
    private const string Exe = @"C:\Apps\CrosshairManager.exe";

    [Fact]
    public void Enable_WritesQuotedPathOnceAndRewritesWhenPathDiffers()
    {
        var key = new FakeRunKey();
        var auto = new AutoStart(key, Exe);
        Assert.True(auto.Apply(true));
        Assert.Equal("\"" + Exe + "\"", key.Values[AutoStart.ValueName]);
        Assert.False(auto.Apply(true));

        key.Values[AutoStart.ValueName] = "\"D:\\old\\CrosshairManager.exe\"";
        Assert.True(auto.Apply(true));
        Assert.Equal(auto.Command, key.Values[AutoStart.ValueName]);
        Assert.Equal(1 + 1, key.Writes);
    }

    [Fact]
    public void Disable_RemovesValueOnlyWhenPresent()
    {
        var key = new FakeRunKey();
        var auto = new AutoStart(key, Exe);
        Assert.False(auto.Apply(false));
        key.Values[AutoStart.ValueName] = auto.Command;
        Assert.True(auto.Apply(false));
        Assert.False(key.Values.ContainsKey(AutoStart.ValueName));
    }

    private sealed class FakeRunKey : IRunKey
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int Writes { get; private set; }

        public string? Get(string name) => Values.TryGetValue(name, out var v) ? v : null;

        public void Set(string name, string value)
        {
            Writes++;
            Values[name] = value;
        }

        public void Delete(string name) => Values.Remove(name);
    }
}
