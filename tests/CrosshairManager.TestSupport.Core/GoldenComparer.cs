namespace CrosshairManager.TestSupport.Core;

/// <summary>First mismatch between two ASCII grids; <see cref="Row"/>/<see cref="Column"/> are 0-based.</summary>
public sealed record GoldenDiff(int Row, int Column, string Expected, string Actual, string ExpectedText, string ActualText)
{
    public override string ToString() =>
        $"first difference at row {Row}, column {Column}: expected {Expected}, actual {Actual}\n"
        + $"--- expected ---\n{ExpectedText}\n--- actual ---\n{ActualText}";
}

/// <summary>Compares ASCII grids line by line; line endings and a trailing newline are ignored.</summary>
public static class GoldenComparer
{
    /// <summary>Returns null when equal, otherwise the first differing row/column.</summary>
    public static GoldenDiff? Compare(string expected, string actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        var e = Normalize(expected);
        var a = Normalize(actual);
        var eRows = e.Split('\n');
        var aRows = a.Split('\n');
        for (int r = 0; r < Math.Max(eRows.Length, aRows.Length); r++)
        {
            var er = r < eRows.Length ? eRows[r] : null;
            var ar = r < aRows.Length ? aRows[r] : null;
            if (er is null || ar is null)
            {
                return new GoldenDiff(r, 0, er is null ? "<no row>" : Describe(er, 0), ar is null ? "<no row>" : Describe(ar, 0), e, a);
            }

            for (int c = 0; c < Math.Max(er.Length, ar.Length); c++)
            {
                if (c >= er.Length || c >= ar.Length || er[c] != ar[c])
                {
                    return new GoldenDiff(r, c, Describe(er, c), Describe(ar, c), e, a);
                }
            }
        }

        return null;
    }

    /// <summary>Loads a golden file, dropping comment lines that start with <c>#</c>.</summary>
    public static string LoadGolden(string path) =>
        string.Join("\n", Normalize(File.ReadAllText(path)).Split('\n').Where(l => !l.StartsWith('#')));

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    private static string Describe(string row, int column) => column < row.Length ? $"'{row[column]}'" : "<end of row>";
}
