using System.Collections;
using System.IO;
using CrosshairManager.App;
using CrosshairManager.Core.Settings;

namespace CrosshairManager.Desktop.Tests.App;

[Trait("Category", "Desktop")]
public class AppArgumentsTests
{
    private static readonly IDictionary Env = new Hashtable { ["LOCALAPPDATA"] = @"C:\Users\x\AppData\Local" };

    [Fact]
    public void Defaults_UseLocalAppDataAndNoFlags()
    {
        var a = AppArguments.Parse([], Env);
        Assert.Equal(@"C:\Users\x\AppData\Local\CrosshairManager", a.DataDir);
        Assert.False(a.OpenSettings);
        Assert.False(a.Hidden);
    }

    [Fact]
    public void ParsesDataDirSettingsAndHidden()
    {
        var a = AppArguments.Parse(["--hidden", "--data-dir", @"D:\chm", "--settings"], Env);
        Assert.Equal(@"D:\chm", a.DataDir);
        Assert.True(a.OpenSettings);
        Assert.True(a.Hidden);
    }

    [Fact]
    public void DataDirValueNamedLikeAFlagIsNotTreatedAsTheFlag()
    {
        var a = AppArguments.Parse(["--data-dir", @"D:\x"], Env);
        Assert.False(a.Hidden);
        Assert.Throws<ArgumentException>(() => AppArguments.Parse(["--data-dir", "--settings"], Env));
        Assert.Throws<ArgumentException>(() => AppArguments.Parse(["--data-dir"], Env));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    public void Visibility_HiddenBeatsSavedOverlayVisible(bool hidden, bool saved, bool expected)
    {
        var a = new AppArguments(@"C:\d", OpenSettings: false, Hidden: hidden);
        Assert.Equal(expected, a.InitialOverlayVisible(AppSettings.Default with { OverlayVisible = saved }));
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    public void SettingsWindowOpensForFlagOrFirstRun(bool flag, bool accepted, bool expected)
    {
        var a = new AppArguments(@"C:\d", OpenSettings: flag, Hidden: false);
        Assert.Equal(expected, a.OpenSettingsAtStartup(AppSettings.Default with { DisclaimerAccepted = accepted }));
    }
}
