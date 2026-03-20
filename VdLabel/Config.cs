using System.Drawing;

namespace VdLabel;

class Config
{
    public OverlayPosition Position { get; set; } = OverlayPosition.TopLeft;
    public double FontSize { get; set; } = 48;
    public double OverlaySize { get; set; } = 800;
    public Color Foreground { get; set; } = Color.WhiteSmoke;
    public Color Background { get; set; } = Color.FromArgb(0x0d1117);
    public double Duration { get; set; } = 2.5;
    public NamePosition NamePosition { get; set; } = NamePosition.Bottom;
    public double CommandInterval { get; set; } = 30;

    public List<BadgeConfig> Badges { get; init; } = [];
    public List<DesktopConfig> DesktopConfigs { get; init; } = [];
}

record BadgeConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;
    public Color Color { get; set; } = Color.FromArgb(255, 0, 120, 212);
    public string? Command { get; set; }
    public bool Utf8Command { get; set; }
}

/// <summary>
/// JSON output format for badge commands.
/// </summary>
record BadgeCommandResult
{
    public string Label { get; init; } = string.Empty;

    /// <summary>HTML color string, e.g. "#ff6600".</summary>
    public string? Color { get; init; }
}

/// <summary>
/// Runtime-resolved badge values (from command output or static config).
/// Used for display in catalog and overlay.
/// </summary>
record ResolvedBadge(string Label, Color Color);

enum OverlayPosition
{
    Center,
    TopLeft,
}

record DesktopConfig
{
    public Guid Id { get; set; }
    public bool IsVisibleName { get; set; } = true;
    public string? Name { get; set; }
    public string? Command { get; set; }
    public bool Utf8Command { get; set; }
    public string? ImagePath { get; set; }
    public IReadOnlyList<WindowConfig> TargetWindows { get; init; } = [];
    public IReadOnlyList<Guid> BadgeIds { get; init; } = [];
}

record WindowConfig(WindowMatchType MatchType, WindowPatternType PatternType, string Pattern);

enum WindowMatchType
{
    CommandLine,
    Title,
    Path,
}

enum WindowPatternType
{
    Wildcard,
    Regex,
}

enum NamePosition
{
    Top,
    Bottom,
}