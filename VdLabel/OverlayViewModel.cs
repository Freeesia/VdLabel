using CommunityToolkit.Mvvm.ComponentModel;
using System.Drawing;
using System.Windows.Controls;

namespace VdLabel;

partial class OverlayViewModel : ObservableObject, IDisposable
{
    private readonly Guid id;
    private readonly IConfigStore configStore;
    private DateTime requestTime;
    private double duration;
    private bool isVisibleName;

    [ObservableProperty]
    private bool visible;

    [ObservableProperty]
    private Dock position;

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private double fontSize;

    [ObservableProperty]
    private double overlaySize;

    [ObservableProperty]
    private Color foreground;

    [ObservableProperty]
    private Color background;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisibleImage))]
    [NotifyPropertyChangedFor(nameof(IsVisibleName))]
    private string? imagePath;

    [ObservableProperty]
    private IReadOnlyList<BadgeConfig> badges = [];

    public bool IsVisibleImage => this.ImagePath is not null;

    public bool IsVisibleName => this.ImagePath is null || this.isVisibleName;

    public OverlayViewModel(Guid id, string name, IConfigStore configStore)
    {
        this.id = id;
        this.configStore = configStore;
        this.name = name;
        var config = this.configStore.Load().AsTask().Result;
        this.fontSize = config.FontSize;
        this.overlaySize = config.OverlaySize;
        this.foreground = config.Foreground;
        this.background = config.Background;
        this.duration = config.Duration;
        this.position = config.NamePosition switch
        {
            NamePosition.Top => Dock.Top,
            NamePosition.Bottom => Dock.Bottom,
            _ => throw new NotImplementedException(),
        };
        var c = config.DesktopConfigs.FirstOrDefault(c => c.Id == this.id);
        this.imagePath = c?.ImagePath;
        this.isVisibleName = c?.IsVisibleName ?? true;
        this.badges = ResolveBadges(config, c);
        this.configStore.Saved += ConfigStore_Saved;
    }

    private static IReadOnlyList<BadgeConfig> ResolveBadges(Config config, DesktopConfig? desktopConfig)
    {
        if (desktopConfig is null || desktopConfig.BadgeIds.Count == 0)
        {
            return [];
        }
        return config.Badges
            .Where(b => desktopConfig.BadgeIds.Contains(b.Id))
            .ToArray();
    }

    private async void ConfigStore_Saved(object? sender, EventArgs e)
    {
        var config = await this.configStore.Load();
        this.FontSize = config.FontSize;
        this.OverlaySize = config.OverlaySize;
        this.Foreground = config.Foreground;
        this.Background = config.Background;
        this.duration = config.Duration;
        this.Position = config.NamePosition switch
        {
            NamePosition.Top => Dock.Top,
            NamePosition.Bottom => Dock.Bottom,
            _ => throw new NotImplementedException(),
        };
        var c = config.DesktopConfigs.FirstOrDefault(c => c.Id == this.id);
        if (c?.Name is not null)
        {
            this.Name = c.Name;
        }
        this.isVisibleName = c?.IsVisibleName ?? true;
        this.ImagePath = c?.ImagePath;
        this.Badges = ResolveBadges(config, c);
    }

    public void Dispose()
    {
        this.configStore.Saved -= ConfigStore_Saved;
    }


    public async void Popup()
    {
        this.Visible = true;
        var time = this.requestTime = DateTime.Now;
        await Task.Delay(TimeSpan.FromSeconds(this.duration));
        if (time != this.requestTime)
        {
            return;
        }
        this.Visible = false;
    }
}
