using Kamishibai;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Windows;
using WindowsDesktop;
using WindowStartupLocation = Kamishibai.WindowStartupLocation;

namespace VdLabel;

class VirtualDesktopService(App app, IWindowService windowService, IConfigStore configStore) : IHostedService, IVirualDesktopService
{
    private readonly App app = app;
    private readonly IWindowService windowService = windowService;
    private readonly IConfigStore configStore = configStore;
    private readonly ConcurrentDictionary<Guid, (IWindow window, OverlayViewModel vm)> windows = [];
    private OpenWindowOptions options = new() { WindowStartupLocation = WindowStartupLocation.CenterScreen };

    public bool IsEnableOverlay { get; set; } = true;

    public event EventHandler<DesktopChangedEventArgs>? DesktopChanged;

    public bool IsSupportedName { get; } = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348, 0);

    public bool IsSupportedMoveDesktop { get; } = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000, 0);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.app.Startup += async (_, _) =>
        {
            await ReloadDesktops();
            VirtualDesktop.CurrentChanged += VirtualDesktop_CurrentChanged;
            VirtualDesktop.Destroyed += VirtualDesktop_Destroyed;
            VirtualDesktop.Created += VirtualDesktop_Created;
            VirtualDesktop.Moved += VirtualDesktop_Moved;
            VirtualDesktop.Renamed += VirtualDesktop_Renamed;
        };
        return Task.CompletedTask;
    }

    private async void VirtualDesktop_Renamed(object? sender, VirtualDesktopRenamedEventArgs e)
    {
        if (!this.windows.TryGetValue(e.Desktop.Id, out var pair) || pair.vm.Name != e.Name)
        {
            return;
        }
        pair.vm.Name = e.Name;
        var config = await this.configStore.Load();
        var c = config.DesktopConfigs.FirstOrDefault(c => c.Id == e.Desktop.Id);
        if (c is null)
        {
            return;
        }
        c.Name = e.Name;
        await this.configStore.Save(config);
    }

    private async void VirtualDesktop_Moved(object? sender, VirtualDesktopMovedEventArgs e)
    {
        var config = await this.configStore.Load();
        var c = config.DesktopConfigs[e.OldIndex + 1];
        config.DesktopConfigs.RemoveAt(e.OldIndex + 1);
        config.DesktopConfigs.Insert(e.NewIndex + 1, c);
        Rename(config);
        await this.configStore.Save(config);
    }

    private void OpenOverlay(VirtualDesktop desktop, string name)
        => this.app.Dispatcher.Invoke(async () =>
        {
            var vm = new OverlayViewModel(desktop.Id, name, configStore);
            var window = await this.windowService.OpenWindowAsync(vm, null, this.options);
            this.windows[desktop.Id] = (window, vm);
            var w = GetWindow((WindowHandle)window);
            w.MoveToDesktop(desktop);
        });


    private void VirtualDesktop_CurrentChanged(object? sender, VirtualDesktopChangedEventArgs e)
    {
        PopupOverlay();
        this.DesktopChanged?.Invoke(this, new(e.NewDesktop.Id));
    }

    private void VirtualDesktop_Destroyed(object? sender, VirtualDesktopDestroyEventArgs e)
        => this.app.Dispatcher.Invoke(async () =>
        {
            if (this.windows.Remove(e.Destroyed.Id, out var pair))
            {
                pair.window.Close();
            }
            var config = await this.configStore.Load();
            config.DesktopConfigs.RemoveAll(c => c.Id == e.Destroyed.Id);
            Rename(config);
            await this.configStore.Save(config);
        });

    private void Rename(Config config)
    {
        for (var i = 1; i < config.DesktopConfigs.Count; i++)
        {
            var c = config.DesktopConfigs[i];
            if (this.windows.TryGetValue(c.Id, out var pair2))
            {
                pair2.vm.Name = c.Name ?? string.Format(Properties.Resources.Desktop, i);
            }
        }
    }

    private void VirtualDesktop_Created(object? sender, VirtualDesktop e)
          => this.app.Dispatcher.Invoke(async () =>
          {
              var config = await this.configStore.Load();
              config.DesktopConfigs.Add(new() { Id = e.Id });
              await this.configStore.Save(config);
              OpenOverlay(e, string.Format(Properties.Resources.Desktop, config.DesktopConfigs.Count - 1));
          });

    public Task StopAsync(CancellationToken cancellationToken)
    {
        VirtualDesktop.CurrentChanged -= VirtualDesktop_CurrentChanged;
        VirtualDesktop.Destroyed -= VirtualDesktop_Destroyed;
        VirtualDesktop.Created -= VirtualDesktop_Created;
        VirtualDesktop.Moved -= VirtualDesktop_Moved;
        VirtualDesktop.Renamed += VirtualDesktop_Renamed;
        return Task.CompletedTask;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_window")]
    static extern ref Window GetWindow(WindowHandle window);

    public void Pin(Window window)
        => window.Pin();

    public async ValueTask ReloadDesktops()
    {
        var desktops = VirtualDesktop.GetDesktops();
        var config = await this.configStore.Load();
        this.options = new()
        {
            WindowStartupLocation = config.Position switch
            {
                OverlayPosition.Center => WindowStartupLocation.CenterScreen,
                OverlayPosition.TopLeft => WindowStartupLocation.Manual,
                _ => throw new NotImplementedException(),
            }
        };
        var desktopConfigs = config.DesktopConfigs.ToDictionary(c => c.Id);
        config.DesktopConfigs.Clear();
        var defaultConfig = desktopConfigs[Guid.Empty];
        config.DesktopConfigs.Add(defaultConfig);
        for (var i = 0; i < desktops.Length; i++)
        {
            var desktop = desktops[i];
            if (!desktopConfigs.TryGetValue(desktop.Id, out var c))
            {
                c = new() { Id = desktop.Id };
            }
            config.DesktopConfigs.Add(c);
            if (this.windows.ContainsKey(desktop.Id))
            {
                continue;
            }
            var name = c.Name;
            if (!string.IsNullOrEmpty(name) && this.IsSupportedName)
            {
                desktop.Name = name;
            }
            else if (!string.IsNullOrEmpty(desktop.Name))
            {
                name = desktop.Name;
            }
            c.Name = name;
            if (string.IsNullOrEmpty(name))
            {
                name = string.Format(Properties.Resources.Desktop, i + 1);
            }
            OpenOverlay(desktop, name);
        }
        await this.configStore.Save(config);
    }

    public void SetName(Guid id, string v)
    {
        if (this.windows.TryGetValue(id, out var pair))
        {
            pair.vm.Name = v;
        }
        if (this.IsSupportedName && VirtualDesktop.FromId(id) is { } desktop)
        {
            desktop.Name = v.ReplaceLineEndings(string.Empty);
        }
    }

    public void PopupOverlay()
    {
        if (!this.IsEnableOverlay)
        {
            return;
        }
        foreach (var (_, vm) in this.windows.Values)
        {
            vm.Popup();
        }
    }

    public void Swtich(int index)
    {
        var desktops = VirtualDesktop.GetDesktops();
        if (index < 0 || index >= desktops.Length)
        {
            return;
        }
        desktops[index].Switch();
        PopupOverlay();
    }

    public void Switch(Guid id)
    {
        var desktop = VirtualDesktop.FromId(id);
        desktop?.Switch();
        PopupOverlay();
    }

    public string? GetWallpaperPath(Guid id)
    {
        var vd = VirtualDesktop.FromId(id) ?? throw new InvalidOperationException();
        return vd.WallpaperPath is { Length: > 0 } path ? path : null;
    }

    public Guid GetCurrent()
        => VirtualDesktop.Current.Id;

    public void CreateDesktop()
        => VirtualDesktop.Create();

    public void RemoveDesktop(Guid id)
    {
        var desktop = VirtualDesktop.FromId(id);
        desktop?.Remove();
    }

    public void MoveDesktop(Guid id, int index)
    {
        if (!this.IsSupportedMoveDesktop)
        {
            return;
        }
        var desktop = VirtualDesktop.FromId(id);
        desktop?.Move(index);
    }
}

public interface IVirualDesktopService
{
    event EventHandler<DesktopChangedEventArgs>? DesktopChanged;
    bool IsSupportedName { get; }
    bool IsSupportedMoveDesktop { get; }
    bool IsEnableOverlay { get; set; }

    void Pin(Window window);
    ValueTask ReloadDesktops();
    void SetName(Guid id, string v);
    void PopupOverlay();
    void Swtich(int index);
    void Switch(Guid id);
    string? GetWallpaperPath(Guid id);
    Guid GetCurrent();
    void CreateDesktop();
    void RemoveDesktop(Guid id);
    void MoveDesktop(Guid id, int index);
}

public class DesktopChangedEventArgs(Guid desktopId) : EventArgs
{
    public Guid DesktopId { get; } = desktopId;
}