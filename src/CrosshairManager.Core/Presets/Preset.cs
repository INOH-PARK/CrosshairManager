using CrosshairManager.Core.Model;

namespace CrosshairManager.Core.Presets;

public sealed record Preset(string Id, string Name, DateTime CreatedUtc, DateTime ModifiedUtc, CrosshairSpec Crosshair, bool IsBuiltIn = false);
