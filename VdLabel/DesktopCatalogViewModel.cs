using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

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
            .Select((c, i) => new DesktopViewModel(i + 1, c, this.commandLabelService.GetCacheResult(c.Id), this.virualDesktopService.GetWallpaperPath(c.Id), pos))
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
        this.Height = Math.Min(SystemParameters.PrimaryScreenHeight * 0.8, 280 * rows);
    }

    partial void OnWidthChanged(double value)
        => this.Width = this.Columns * 280;
    partial void OnTopChanged(double value)
        => this.Top = (SystemParameters.PrimaryScreenHeight - this.Height) / 2;
    partial void OnLeftChanged(double value)
        => this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
}

internal class DesktopViewModel(int index, DesktopConfig desktopConfig, string? commandLabel, string? wallpaperPath, Dock pos)
{
    private readonly int index = index;
    private DesktopConfig desktopConfig = desktopConfig;
    private readonly string? commandLabel = commandLabel;
    private readonly string? wallpaperPath = wallpaperPath;

    public Guid Id => this.desktopConfig.Id;

    public Dock Position { get; } = pos;

    public string Label => this.commandLabel ?? this.desktopConfig.Name ?? $"Desktop {this.index}";

    public string? ImagePath => this.desktopConfig.ImagePath ?? this.wallpaperPath;
}