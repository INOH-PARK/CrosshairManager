using System.Text;

namespace CrosshairManager.Core.IO;

/// <summary>Writes through a temp file and swaps it in, keeping the previous version as .bak.</summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string text) =>
        WriteAllBytes(path, Encoding.UTF8.GetBytes(text));

    public static void WriteAllBytes(string path, byte[] bytes)
    {
        string full = Path.GetFullPath(path);
        string? folder = Path.GetDirectoryName(full);
        string temp = full + ".tmp";
        bool done = false;
        try
        {
            if (folder is not null) Directory.CreateDirectory(folder);
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(full)) File.Replace(temp, full, full + ".bak");
            else File.Move(temp, full);
            done = true;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Cannot write '{full}': the folder is not writable.", ex);
        }
        finally
        {
            if (!done && File.Exists(temp)) File.Delete(temp);
        }
    }
}
