using System.Text;

namespace CrosshairManager.Core.IO;

/// <summary>Writes through a temp file and swaps it in, keeping the previous version as .bak.</summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string text) =>
        WriteAllBytes(path, Encoding.UTF8.GetBytes(text));

    public static void WriteAllBytes(string path, byte[] bytes)
    {
        string? folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (folder is not null) Directory.CreateDirectory(folder);

        string temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }
}
