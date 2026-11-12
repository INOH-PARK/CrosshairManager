namespace CrosshairManager.Core.Geometry;

/// <summary>Integer rectangle in physical pixels; half-open on the right and bottom edges.</summary>
public readonly record struct IntRect(int X, int Y, int W, int H)
{
    public bool IsEmpty => W <= 0 || H <= 0;
}
