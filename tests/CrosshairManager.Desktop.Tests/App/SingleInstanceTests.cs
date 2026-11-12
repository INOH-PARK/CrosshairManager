using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using CrosshairManager.App;

namespace CrosshairManager.Desktop.Tests.App;

[Trait("Category", "Desktop")]
public class SingleInstanceTests
{
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
        using var owner = SingleInstance.TryAcquire(dir.Path, out var pidError);
        Assert.NotNull(owner);
        Assert.Null(pidError);
        Assert.Equal(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            File.ReadAllText(dir.Combine(SingleInstance.PidFileName)));

        using var signaled = new ManualResetEventSlim();
        owner.Listen(signaled.Set);
        Assert.Null(SingleInstance.TryAcquire(dir.Path.ToLowerInvariant() + "\\", out _));
        Assert.True(signaled.Wait(TimeSpan.FromSeconds(5)), "owner was not signaled");
    }

    [Fact]
    public void Owner_CreatesMissingDataDirForThePidFile()
    {
        using var dir = new AppTestDir();
        var dataDir = dir.Combine("new", "data");
        using var owner = SingleInstance.TryAcquire(dataDir, out var pidError);
        Assert.NotNull(owner);
        Assert.Null(pidError);
        Assert.True(File.Exists(Path.Combine(dataDir, SingleInstance.PidFileName)));
    }

    [Fact]
    public void Dispose_RemovesOwnPidFileAndReleasesTheName()
    {
        using var dir = new AppTestDir();
        var owner = SingleInstance.TryAcquire(dir.Path, out _);
        Assert.NotNull(owner);
        owner.Dispose();
        Assert.False(File.Exists(dir.Combine(SingleInstance.PidFileName)));
        using var again = SingleInstance.TryAcquire(dir.Path, out _);
        Assert.NotNull(again);
    }
}
