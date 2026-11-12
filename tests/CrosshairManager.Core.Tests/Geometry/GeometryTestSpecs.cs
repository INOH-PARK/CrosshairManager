using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;

namespace CrosshairManager.Core.Tests.Geometry;

internal static class GeometryTestSpecs
{
    public static LineSpec Line(int length, int thickness, int gap) => new(true, length, thickness, gap);

    public static CrosshairSpec AllLines(CrosshairSpec spec, LineSpec line) =>
        spec with { Top = line, Bottom = line, Left = line, Right = line };

    /// <summary>builtin.cross: dot off, lines 6/2/3.</summary>
    public static CrosshairSpec Cross { get; } = AllLines(CrosshairSpec.Default, Line(6, 2, 3)) with
    {
        Dot = new DotSpec(Enabled: false),
    };

    /// <summary>builtin.classic1px: dot off, lines L4 T1 G2, outline off.</summary>
    public static CrosshairSpec Classic1px { get; } = AllLines(CrosshairSpec.Default, Line(4, 1, 2)) with
    {
        Dot = new DotSpec(Enabled: false),
        Outline = new OutlineSpec(Enabled: false, Color: Rgb.Parse("#000000")),
    };

    public static CrosshairSpec Prep(CrosshairSpec spec) => spec.Normalize().Spec.ApplyScale();

    public static CrosshairGeometry Layout(CrosshairSpec spec, bool oddW, bool oddH) =>
        CrosshairLayout.Compute(Prep(spec), oddW, oddH);

    public static GeomRect R(ShapeRole role, int x, int y, int w, int h) => new(role, new IntRect(x, y, w, h));

    public static IntRect Of(CrosshairGeometry g, ShapeRole role) => g.Fills.Single(f => f.Role == role).Rect;

    /// <summary>Empty pixels between the cross center band and each line: (top, bottom, left, right).</summary>
    public static (int Top, int Bottom, int Left, int Right) Gaps(CrosshairGeometry g)
    {
        var top = Of(g, ShapeRole.Top);
        var bottom = Of(g, ShapeRole.Bottom);
        var left = Of(g, ShapeRole.Left);
        var right = Of(g, ShapeRole.Right);

        // Horizontal band rows = rows of the Left/Right lines; vertical band columns = columns of Top/Bottom.
        return (
            left.Y - (top.Y + top.H),
            bottom.Y - (left.Y + left.H),
            top.X - (left.X + left.W),
            right.X - (top.X + top.W));
    }
}
