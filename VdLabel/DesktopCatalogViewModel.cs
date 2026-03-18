using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VdLabel;

internal sealed partial class DesktopCatalogViewModel : ObservableObject, IDisposable
{
    private readonly IVirualDesktopService virualDesktopService;
    private readonly ICommandLabelService commandLabelService;
    private readonly IConfigStore configStore;
    private readonly int maxColumns;
    [ObservableProperty]
    private IReadOnlyList<DesktopViewModel> desktops = [];
    [ObservableProperty]
    private DesktopViewModel? selectedDesktop;

    [ObservableProperty]
    private int columns = 0;

    [ObservableProperty]
    private double top;
    [ObservableProperty]
    private double left;
    [ObservableProperty]
    private double width;
    [ObservableProperty]
    private double height;

    public DesktopCatalogViewModel(IVirualDesktopService virualDesktopService, ICommandLabelService commandLabelService, IConfigStore configStore)
    {
        this.virualDesktopService = virualDesktopService;
        this.commandLabelService = commandLabelService;
        this.configStore = configStore;
        this.configStore.Saved += ConfigStore_Saved;
        this.maxColumns = (int)(SystemParameters.PrimaryScreenWidth * 0.8 / 280);
        Setup();
    }

    private async void Setup()
    {
        var config = await this.configStore.Load().ConfigureAwait(false);
        var pos = config.NamePosition switch
        {
            NamePosition.Top => Dock.Top,
            NamePosition.Bottom => Dock.Bottom,
            _ => throw new NotImplementedException(),
        };
        this.Desktops = config.DesktopConfigs
            .Where(c => c.Id != Guid.Empty)
            .Select((c, i) => new DesktopViewModel(i + 1, c, this.commandLabelService.GetCacheResult(c.Id), this.virualDesktopService.GetWallpaperPath(c.Id), pos, config.Badges, ToggleBadgeAsync))
            .ToArray();
        var currentDesktop = this.virualDesktopService.GetCurrent();
        this.SelectedDesktop = this.Desktops.FirstOrDefault(d => d.Id == currentDesktop);
        this.Columns = Math.Min(this.maxColumns, this.Desktops.Count);
        this.Width = this.Columns * 280;
        var rows = (this.Desktops.Count / this.Columns) + (this.Desktops.Count % this.Columns == 0 ? 0 : 1);
        this.Height = Math.Min(SystemParameters.PrimaryScreenHeight * 0.8, 280 * rows);
        this.Top = (SystemParameters.PrimaryScreenHeight - this.Height) / 2;
        this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
    }

    private async Task ToggleBadgeAsync(Guid desktopId, Guid badgeId)
    {
        var config = await this.configStore.Load().ConfigureAwait(false);
        var desktopConfig = config.DesktopConfigs.FirstOrDefault(c => c.Id == desktopId);
        if (desktopConfig is null)
        {
            return;
        }
        var newBadgeIds = desktopConfig.BadgeIds.Contains(badgeId)
            ? desktopConfig.BadgeIds.Where(id => id != badgeId).ToArray()
            : [.. desktopConfig.BadgeIds, badgeId];
        var idx = config.DesktopConfigs.IndexOf(desktopConfig);
        config.DesktopConfigs[idx] = desktopConfig with { BadgeIds = newBadgeIds };
        await this.configStore.Save(config).ConfigureAwait(false);
    }

    public void Loaded() => Setup();

    private void ConfigStore_Saved(object? sender, EventArgs e) => Setup();

    public void Dispose() => this.configStore.Saved -= ConfigStore_Saved;

    partial void OnSelectedDesktopChanged(DesktopViewModel? value)
    {
        if (value is { Id: var id } && this.virualDesktopService.GetCurrent() != id)
        {
            this.virualDesktopService.Switch(id);
        }
    }

    partial void OnHeightChanged(double value)
    {
        var rows = (this.Desktops.Count / this.Columns) + (this.Desktops.Count % this.Columns == 0 ? 0 : 1);
        this.Height = Math.Min(SystemParameters.PrimaryScreenHeight * 0.8, 280 * rows) + 2;
    }

    partial void OnWidthChanged(double value)
        => this.Width = this.Columns * 280 + 2;
    partial void OnTopChanged(double value)
        => this.Top = (SystemParameters.PrimaryScreenHeight - this.Height) / 2;
    partial void OnLeftChanged(double value)
        => this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
}

internal class DesktopViewModel(int index, DesktopConfig desktopConfig, string? commandLabel, string? wallpaperPath, Dock pos, IReadOnlyList<BadgeConfig> allBadges, Func<Guid, Guid, Task> toggleBadge)
{
    private readonly int index = index;
    private DesktopConfig desktopConfig = desktopConfig;
    private readonly string? commandLabel = commandLabel;
    private readonly string? wallpaperPath = wallpaperPath;

    public Guid Id => this.desktopConfig.Id;

    public Dock Position { get; } = pos;

    public string Label => this.commandLabel ?? this.desktopConfig.Name ?? string.Format(VdLabel.Properties.Resources.Desktop, this.index);

    public string? ImagePath => this.desktopConfig.ImagePath ?? this.wallpaperPath;

    public IReadOnlyList<BadgeConfig> AssignedBadges { get; } = allBadges
        .Where(b => desktopConfig.BadgeIds.Contains(b.Id))
        .ToArray();

    public IReadOnlyList<BadgeMenuItem> BadgeMenuItems { get; } = allBadges
        .Select(b => new BadgeMenuItem(b, desktopConfig.Id, desktopConfig.BadgeIds.Contains(b.Id), toggleBadge))
        .ToArray();
}

internal class BadgeMenuItem
{
    private readonly BadgeConfig badge;
    private readonly Guid desktopId;
    private readonly Func<Guid, Guid, Task> toggleAction;

    public BadgeMenuItem(BadgeConfig badge, Guid desktopId, bool isAssigned, Func<Guid, Guid, Task> toggleAction)
    {
        this.badge = badge;
        this.desktopId = desktopId;
        IsAssigned = isAssigned;
        this.toggleAction = toggleAction;
        this.ToggleCommand = new AsyncRelayCommand(Toggle);
    }

    public string Label => this.badge.Label;
    public System.Drawing.Color Color => this.badge.Color;

    public bool IsAssigned { get; }

    public AsyncRelayCommand ToggleCommand { get; }

    private Task Toggle()
        => this.toggleAction(this.desktopId, this.badge.Id);
}