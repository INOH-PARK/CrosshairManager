namespace CrosshairManager.Core.Geometry;

/// <summary>A filled crosshair part in canvas pixel coordinates.</summary>
public readonly record struct GeomRect(ShapeRole Role, IntRect Rect);
