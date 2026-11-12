using System.Collections;
using CrosshairManager.Core.Settings;

namespace CrosshairManager.Core.Tests.Settings;

public class DataDirectoryTests
{
    private const string LocalAppData = @"C:\Users\Test\AppData\Local";

    private static Hashtable Env(string? dataDirVariable = null)
    {
        var env = new Hashtable { ["LOCALAPPDATA"] = LocalAppData };
        if (dataDirVariable is not null)
        {
            env["CROSSHAIRMANAGER_DATA_DIR"] = dataDirVariable;
        }

        return env;
    }

    [Fact]
    public void Argument_wins_over_environment_variable()
    {
        string resolved = DataDirectory.Resolve(["--data-dir", @"D:\arg"], Env(@"E:\env"));

        Assert.Equal(Path.GetFullPath(@"D:\arg"), resolved);
    }

    [Fact]
    public void Environment_variable_wins_over_default()
    {
        string resolved = DataDirectory.Resolve([], Env(@"E:\env"));

        Assert.Equal(Path.GetFullPath(@"E:\env"), resolved);
    }

    [Fact]
    public void Default_is_CrosshairManager_under_LocalAppData()
    {
        string resolved = DataDirectory.Resolve([], Env());

        Assert.Equal(Path.Combine(LocalAppData, "CrosshairManager"), resolved);
    }

    [Fact]
    public void Equals_form_of_the_argument_is_accepted()
    {
        string resolved = DataDirectory.Resolve([@"--data-dir=D:\arg"], Env());

        Assert.Equal(Path.GetFullPath(@"D:\arg"), resolved);
    }

    [Fact]
    public void Argument_without_a_value_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => DataDirectory.Resolve(["--data-dir"], Env()));
    }

    [Fact]
    public void Normalize_uppercases_and_drops_the_trailing_separator()
    {
        Assert.Equal(@"C:\A\B", DataDirectory.Normalize(@"C:\a\B\"));
        Assert.Equal(@"C:\A\B", DataDirectory.Normalize("c:/A/b"));
    }
}
