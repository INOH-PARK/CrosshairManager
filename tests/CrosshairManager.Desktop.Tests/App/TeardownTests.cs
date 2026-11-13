using System.IO;
using CrosshairManager.App.Logging;

namespace CrosshairManager.Desktop.Tests.App;

[Trait("Category", "Desktop")]
public class TeardownTests
{
    [Fact]
    public void A_failing_step_is_logged_and_the_following_steps_still_run()
    {
        using var dir = new AppTestDir();
        var log = new FileLog(dir.Path, dir.Combine("fallback.log"));
        var ran = new List<string>();

        var first = Teardown.Step(log, "hotkeys", () => throw new TimeoutException("Hotkey thread did not exit within 5 seconds."));
        var second = Teardown.Step(log, "overlay", () => ran.Add("overlay"));

        Assert.False(first);
        Assert.True(second);
        Assert.Equal(["overlay"], ran);
        var text = File.ReadAllText(log.LogPath);
        Assert.Contains("[ERROR] could not dispose hotkeys", text, StringComparison.Ordinal);
        Assert.Contains("Hotkey thread did not exit", text, StringComparison.Ordinal);
    }
}
