using Kamishibai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VdLabel;
using WindowsDesktop;
using Wpf.Ui;

var builder = KamishibaiApplication<App, MainWindow>.CreateBuilder();
builder.Services
    .Configure<HostOptions>(op => op.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore)
    .AddSingleton<VirtualDesktopService>()
    .AddSingleton<CommandLabelService>()
    .AddSingleton<UpdateChecker>()
    .AddHostedService(sp => sp.GetRequiredService<VirtualDesktopService>())
    .AddHostedService(sp => sp.GetRequiredService<CommandLabelService>())
    .AddHostedService(sp => sp.GetRequiredService<UpdateChecker>())
    .AddHostedService<WindowMonitor>()
    .AddSingleton<IVirualDesktopService>(sp => sp.GetRequiredService<VirtualDesktopService>())
    .AddSingleton<ICommandLabelService>(sp => sp.GetRequiredService<CommandLabelService>())
    .AddSingleton<IUpdateChecker>(sp => sp.GetRequiredService<UpdateChecker>())
    .AddSingleton<IConfigStore, ConfigStore>()
    .AddSingleton<IContentDialogService, ContentDialogService>()
    .AddPresentation<MainWindow, MainViewModel>()
    .AddPresentation<DesktopCatalog, DesktopCatalogViewModel>()
    .AddPresentation<OverlayWindow, OverlayViewModel>()
    .AddPresentation<TargetWindowOverlay, TargetWindowViewModel>();
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