using System.Drawing;

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
}