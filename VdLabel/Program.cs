using Kamishibai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VdLabel;
using WindowsDesktop;
using Wpf.Ui;

var builder = KamishibaiApplication<App, MainWindow>.CreateBuilder();
builder.Services.AddHostedService<VirtualDesktopService>()
    .AddSingleton<IConfigStore, ConfigStore>()
    .AddSingleton<IContentDialogService, ContentDialogService>()
    .AddPresentation<MainWindow, MainViewModel>()
    .AddPresentation<OverlayWindow, OverlayViewModel>();
var app = builder.Build();
app.Startup += (s, e) => VirtualDesktop.Configure();
await app.RunAsync();