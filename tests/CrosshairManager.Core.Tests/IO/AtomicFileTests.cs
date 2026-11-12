using CrosshairManager.Core.IO;

namespace CrosshairManager.Core.Tests.IO;

public class AtomicFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "chm-atomic-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Second_write_replaces_target_and_keeps_previous_version_as_bak()
    {
        string path = Path.Combine(_dir, "preset.json");
        AtomicFile.WriteAllText(path, "v1");
        AtomicFile.WriteAllText(path, "v2");

        Assert.Equal("v2", File.ReadAllText(path));
        Assert.Equal("v1", File.ReadAllText(path + ".bak"));
    }

    [Fact]
    public void Temp_file_without_replace_leaves_target_at_previous_version()
    {
        string path = Path.Combine(_dir, "preset.json");
        AtomicFile.WriteAllText(path, "v1");
        File.WriteAllText(path + ".tmp", "v2");

        Assert.Equal("v1", File.ReadAllText(path));
    }

    [Fact]
    public void Write_creates_missing_folders()
    {
        string path = Path.Combine(_dir, "a", "b", "data.bin");
        AtomicFile.WriteAllBytes(path, new byte[] { 1, 2, 3 });

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void Folder_that_cannot_be_created_throws_IOException()
    {
        Directory.CreateDirectory(_dir);
        string blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "not a folder");

        Assert.ThrowsAny<IOException>(() => AtomicFile.WriteAllText(Path.Combine(blocker, "x.json"), "v"));
    }
}
