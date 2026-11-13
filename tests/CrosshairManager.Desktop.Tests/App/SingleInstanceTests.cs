using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using CrosshairManager.App;
using CrosshairManager.App.Logging;

namespace CrosshairManager.Desktop.Tests.App;

[Trait("Category", "Desktop")]
public class SingleInstanceTests
{
    private static FileLog Log(AppTestDir dir) => new(dir.Path, dir.Combine("fallback.log"));

    [Fact]
    public void UnreadablePidFile_IsLogged_AndSecondAcquireStillSignals()
    {
        using var dir = new AppTestDir();
        var log = Log(dir);
        using var owner = SingleInstance.TryAcquire(dir.Path, log, out _);
        Assert.NotNull(owner);

        using (new FileStream(dir.Combine(SingleInstance.PidFileName), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Null(SingleInstance.TryAcquire(dir.Path, log, out _));
        }

        Assert.Contains(SingleInstance.PidFileName, File.ReadAllText(log.LogPath));
    }

    [Fact]
    public void Names_AreStableAcrossCaseAndTrailingSeparator()
    {
        var a = SingleInstance.Names(@"C:\Data\CHM");
        var b = SingleInstance.Names(@"c:\data\chm\");
        Assert.Equal(a, b);
        Assert.Matches(new Regex(@"^Local\\CrosshairManager-[0-9A-F]{16}$"), a.Mutex);
        Assert.Equal(a.Mutex + "-open", a.OpenEvent);
        Assert.NotEqual(a, SingleInstance.Names(@"C:\Data\Other"));
    }

    [Fact]
    public void SecondAcquire_SignalsOwnerAndReturnsNull()
    {
        using var dir = new AppTestDir();
        using var owner = SingleInstance.TryAcquire(dir.Path, Log(dir), out var pidError);
        Assert.NotNull(owner);
        Assert.Null(pidError);
        Assert.Equal(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            File.ReadAllText(dir.Combine(SingleInstance.PidFileName)));

        using var signaled = new ManualResetEventSlim();
        owner.Listen(signaled.Set);
        Assert.Null(SingleInstance.TryAcquire(dir.Path.ToLowerInvariant() + "\\", Log(dir), out _));
        Assert.True(signaled.Wait(TimeSpan.FromSeconds(5)), "owner was not signaled");
    }

    [Fact]
    public void Owner_CreatesMissingDataDirForThePidFile()
    {
        using var dir = new AppTestDir();
        var dataDir = dir.Combine("new", "data");
        using var owner = SingleInstance.TryAcquire(dataDir, Log(dir), out var pidError);
        Assert.NotNull(owner);
        Assert.Null(pidError);
        Assert.True(File.Exists(Path.Combine(dataDir, SingleInstance.PidFileName)));
    }

    [Fact]
    public void Dispose_RemovesOwnPidFileAndReleasesTheName()
    {
        using var dir = new AppTestDir();
        var owner = SingleInstance.TryAcquire(dir.Path, Log(dir), out _);
        Assert.NotNull(owner);
        owner.Dispose();
        Assert.False(File.Exists(dir.Combine(SingleInstance.PidFileName)));
        using var again = SingleInstance.TryAcquire(dir.Path, Log(dir), out _);
        Assert.NotNull(again);
    }
}
