using CrosshairManager.Core.Model;

namespace CrosshairManager.Core.Geometry;

/// <summary>
/// Pixel-exact crosshair layout in canvas coordinates. <see cref="PivotX"/>/<see cref="PivotY"/> is the
/// crosshair center P inside the canvas (integer or .5); <see cref="AnchorX"/>/<see cref="AnchorY"/> is the
/// canvas pixel that lands on monitor pixel (Left + W/2 + OffsetX, Top + H/2 + OffsetY) with integer division.
/// </summary>
public sealed record CrosshairGeometry(
    int CanvasW,
    int CanvasH,
    double PivotX,
    double PivotY,
    int AnchorX,
    int AnchorY,
    IReadOnlyList<GeomRect> Fills,
    int OutlineThickness,
    double RotationDeg,
    Rgb Color,
    Rgb OutlineColor,
    byte DotAlpha,
    byte LineAlpha,
    byte OutlineAlpha);
