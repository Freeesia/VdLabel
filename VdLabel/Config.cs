﻿using System.Drawing;

namespace VdLabel;

class Config
{
    public OverlayPosition Position { get; set; } = OverlayPosition.TopLeft;
    public double FontSize { get; set; } = 48;
    public Color Foreground { get; set; } = Color.WhiteSmoke;
    public Color Background { get; set; } = Color.FromArgb(0x0d1117);
    public double Duration { get; set; } = 2.5;
    public List<DesktopConfig> DesktopConfigs { get; init; } = [];
}

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
    public string? ImagePath { get; set; }
    public IReadOnlyList<WindowConfig> TargetWindows { get; init; } = [];
}

record WindowConfig
{
    public WindowMatchType MatchType { get; set; }
    public WindowPatternType PatternType { get; set; }
    public string Pattern { get; set; } = string.Empty;
}

enum WindowMatchType
{
    CommandLine,
    Title,
}

enum WindowPatternType
{
    Wildcard,
    Regex,
}