namespace CrosshairManager.Core.Model;

public sealed record OutlineSpec(bool Enabled = true, int Thickness = 1, Rgb Color = default, int Opacity = 100);
