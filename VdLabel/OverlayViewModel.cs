using CommunityToolkit.Mvvm.ComponentModel;
using System.Drawing;
using System.Windows.Controls;

namespace VdLabel;

partial class OverlayViewModel : ObservableObject, IDisposable
{
    private readonly Guid id;
    private readonly IConfigStore configStore;
    private readonly ICommandService commandService;
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
    private IReadOnlyList<ResolvedBadge> badges = [];

    public bool IsVisibleImage => this.ImagePath is not null;

    public bool IsVisibleName => this.ImagePath is null || this.isVisibleName;

    public OverlayViewModel(Guid id, string name, IConfigStore configStore, ICommandService commandService)
    {
        this.id = id;
        this.configStore = configStore;
        this.commandService = commandService;
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
        this.badges = ResolveBadges(config, c, commandService);
        this.configStore.Saved += ConfigStore_Saved;
        this.commandService.BadgeResultsUpdated += CommandService_BadgeResultsUpdated;
    }

    private static IReadOnlyList<ResolvedBadge> ResolveBadges(Config config, DesktopConfig? desktopConfig, ICommandService commandService)
    {
        if (desktopConfig is null || desktopConfig.BadgeIds.Count == 0)
        {
            return [];
        }
        return config.Badges
            .Where(b => desktopConfig.BadgeIds.Contains(b.Id))
            .Select(b =>
            {
                var cached = commandService.GetBadgeResult(b.Id, desktopConfig.Id);
                return cached.HasValue
                    ? new ResolvedBadge(cached.Value.Label, cached.Value.Color)
                    : new ResolvedBadge(b.Label, b.Color);
            })
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
        this.Badges = ResolveBadges(config, c, this.commandService);
    }

    private async void CommandService_BadgeResultsUpdated(object? sender, EventArgs e)
    {
        var config = await this.configStore.Load().ConfigureAwait(false);
        var c = config.DesktopConfigs.FirstOrDefault(c => c.Id == this.id);
        var badges = ResolveBadges(config, c, this.commandService);
        // バックグラウンドスレッドから発火するため UI スレッドへディスパッチ
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => this.Badges = badges);
    }

    public void Dispose()
    {
        this.configStore.Saved -= ConfigStore_Saved;
        this.commandService.BadgeResultsUpdated -= CommandService_BadgeResultsUpdated;
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
