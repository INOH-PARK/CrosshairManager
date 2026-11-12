using System.Collections;
using System.IO;
using CrosshairManager.App.Logging;

namespace CrosshairManager.Desktop.Tests.App;

[Trait("Category", "Desktop")]
public class FileLogTests
{
    [Fact]
    public void Info_AppendsTimestampedLinesToAppLog()
    {
        using var dir = new AppTestDir();
        var log = new FileLog(dir.Path, dir.Combine("fallback.log"));
        log.Info("overlay shown 1,2,3,4");
        log.Info("overlay hidden");
        var lines = File.ReadAllLines(dir.Combine("logs", "app.log"));
        Assert.Equal(2, lines.Length);
        Assert.EndsWith("[INFO] overlay shown 1,2,3,4", lines[0]);
        Assert.EndsWith("[INFO] overlay hidden", lines[1]);
        Assert.False(File.Exists(dir.Combine("fallback.log")));
    }

    [Fact]
    public void RollsToApp1At1MB()
    {
        using var dir = new AppTestDir();
        Directory.CreateDirectory(dir.Combine("logs"));
        File.WriteAllBytes(dir.Combine("logs", "app.log"), new byte[FileLog.MaxBytes]);
        new FileLog(dir.Path, dir.Combine("fallback.log")).Info("after roll");
        Assert.Equal(FileLog.MaxBytes, new FileInfo(dir.Combine("logs", "app.1.log")).Length);
        Assert.Single(File.ReadAllLines(dir.Combine("logs", "app.log")));
    }

    [Fact]
    public void SwitchToFallback_WritesReadOnlyMarkerAndAllLaterLinesThere()
    {
        using var dir = new AppTestDir();
        var log = new FileLog(dir.Path, dir.Combine("fallback.log"));
        log.SwitchToFallback("test");
        log.Info("overlay shown 5,6,7,8");
        var text = File.ReadAllText(dir.Combine("fallback.log"));
        Assert.Contains("data dir read-only", text);
        Assert.Contains("overlay shown 5,6,7,8", text);
        Assert.True(log.UsingFallback);
        Assert.False(File.Exists(dir.Combine("logs", "app.log")));
    }

    [Fact]
    public void UnwritableLogFolder_LineStillLandsInFallback()
    {
        using var dir = new AppTestDir();
        File.WriteAllText(dir.Combine("logs"), "a file where the logs folder should be");
        var log = new FileLog(dir.Path, dir.Combine("fallback.log"));
        log.Info("overlay hidden");
        Assert.Contains("overlay hidden", File.ReadAllText(dir.Combine("fallback.log")));
    }

    [Fact]
    public void ConcurrentReaderWithSharedAccessDoesNotDivertTheLog()
    {
        using var dir = new AppTestDir();
        var log = new FileLog(dir.Path, dir.Combine("fallback.log"));
        log.Info("first");
        using (new FileStream(dir.Combine("logs", "app.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            log.Info("second");
        }

        Assert.Equal(2, File.ReadAllLines(dir.Combine("logs", "app.log")).Length);
        Assert.False(log.UsingFallback);
    }
}
