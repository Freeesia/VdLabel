using System.Drawing;

namespace VdLabel;

/// <summary>
/// Runtime-resolved badge values (from command output or static config).
/// Used for display in catalog and overlay.
/// </summary>
record ResolvedBadge(string Label, Color Color);
