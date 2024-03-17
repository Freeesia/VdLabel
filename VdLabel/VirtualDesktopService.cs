using Kamishibai;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Windows;
using WindowsDesktop;
using WindowStartupLocation = Kamishibai.WindowStartupLocation;

namespace VdLabel;

class VirtualDesktopService(App app, IWindowService windowService, IConfigStore configStore, IVirtualDesktopCompat virtualDesktopCompat) : IHostedService
{
    private readonly App app = app;
    private readonly IWindowService windowService = windowService;
    private readonly IConfigStore configStore = configStore;
    private readonly IVirtualDesktopCompat virtualDesktopCompat = virtualDesktopCompat;
    private OpenWindowOptions options = new() { WindowStartupLocation = WindowStartupLocation.CenterScreen };
    private readonly ConcurrentDictionary<Guid, (IWindow window, OverlayViewModel vm)> windows = [];

    public async Task StartAsync(CancellationToken cancellationToken)
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
        var defaultConfig = config.DesktopConfigs.First(c => c.Id == Guid.Empty);
        for (var i = 0; i < desktops.Length; i++)
        {
            var desktop = desktops[i];
            var c = config.DesktopConfigs.FirstOrDefault(c => c.Id == desktop.Id);
            if (c is null)
            {
                c = new() { Id = desktop.Id };
                config.DesktopConfigs.Add(c);
            }
            var name = c.Name;
            if (!string.IsNullOrEmpty(name) && this.virtualDesktopCompat.IsSupportedName)
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
                name = $"Desktop {i + 1}";
            }
            await OpenOverlay(desktop, name);
        }
        VirtualDesktop.CurrentChanged += VirtualDesktop_CurrentChanged;
        VirtualDesktop.Destroyed += VirtualDesktop_Destroyed;
        VirtualDesktop.Created += VirtualDesktop_Created;
        VirtualDesktop.Renamed += VirtualDesktop_Renamed;
        VirtualDesktop.Moved += VirtualDesktop_Moved;
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

    private async Task OpenOverlay(VirtualDesktop desktop, string name)
    {
        var vm = new OverlayViewModel(desktop.Id, name, configStore);
        var window = await this.windowService.OpenWindowAsync(vm, null, this.options);
        this.windows[desktop.Id] = (window, vm);
        var w = GetWindow((WindowHandle)window);
        _ = w.Dispatcher.InvokeAsync(() => w.MoveToDesktop(desktop));
    }

    private void VirtualDesktop_CurrentChanged(object? sender, VirtualDesktopChangedEventArgs e)
    {
        foreach (var (_, vm) in this.windows.Values)
        {
            vm.Popup();
        }
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
                pair2.vm.Name = c.Name ?? $"Desktop {i}";
            }
        }
    }

    private void VirtualDesktop_Created(object? sender, VirtualDesktop e)
          => this.app.Dispatcher.Invoke(async () =>
          {
              var config = await this.configStore.Load();
              config.DesktopConfigs.Add(new() { Id = e.Id });
              await OpenOverlay(e, $"Desktop {config.DesktopConfigs.Count - 1}");
              await this.configStore.Save(config);
          });

    private void VirtualDesktop_Renamed(object? sender, VirtualDesktopRenamedEventArgs e)
        => this.app.Dispatcher.Invoke(async () =>
        {
            var config = await this.configStore.Load();
            var c = config.DesktopConfigs.First(c => c.Id == e.Desktop.Id);
            if (!string.IsNullOrEmpty(e.Name))
            {
                c.Name = e.Name;
            }
            else
            {
                c.Name = null;
            }
            if (this.windows.TryGetValue(c.Id, out var pair))
            {
                pair.vm.Name = c.Name ?? $"Desktop {config.DesktopConfigs.IndexOf(c)}";
            }
            await this.configStore.Save(config);
        });

    public Task StopAsync(CancellationToken cancellationToken)
    {
        VirtualDesktop.CurrentChanged -= VirtualDesktop_CurrentChanged;
        VirtualDesktop.Destroyed -= VirtualDesktop_Destroyed;
        VirtualDesktop.Created -= VirtualDesktop_Created;
        VirtualDesktop.Renamed -= VirtualDesktop_Renamed;
        VirtualDesktop.Moved -= VirtualDesktop_Moved;
        return Task.CompletedTask;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_window")]
    static extern ref Window GetWindow(WindowHandle window);
}

class VirtualDesktopCompat : IVirtualDesktopCompat
{
    public bool IsSupportedName { get; }

    public VirtualDesktopCompat()
    {
        //this.IsSupportedName = false;
        this.IsSupportedName = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348, 0);
    }
}
interface IVirtualDesktopCompat
{
    bool IsSupportedName { get; }
}
