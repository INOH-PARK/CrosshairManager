using System.IO;

namespace CrosshairManager.Desktop.Tests.App;

/// <summary>Unique temp folder per test, deleted on dispose.</summary>
internal sealed class AppTestDir : IDisposable
{
    public AppTestDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chm-apptest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
