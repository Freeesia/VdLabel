using Kamishibai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VdLabel;
using WindowsDesktop;
using Wpf.Ui;

var builder = KamishibaiApplication<App, MainWindow>.CreateBuilder();
builder.Services
    .AddHostedService(sp => sp.GetRequiredService<IVirualDesktopService>())
    .AddHostedService<WindowMonitor>()
    .AddSingleton<IVirualDesktopService, VirtualDesktopService>()
    .AddSingleton<IConfigStore, ConfigStore>()
    .AddSingleton<IContentDialogService, ContentDialogService>()
    .AddPresentation<MainWindow, MainViewModel>()
    .AddPresentation<OverlayWindow, OverlayViewModel>();
var app = builder.Build();
app.Startup += static (s, e) => VirtualDesktop.Configure();

using var mutex = new Mutex(false, "VdLael", out var createdNew);
if (!createdNew)
{
    new MessageDialog()
    {
        Caption = "VdLabel",
        Icon = MessageBoxImage.Error,
        Text = "すでにVdLabelが起動中です",
    }.Show();
    return;
}
await app.RunAsync();