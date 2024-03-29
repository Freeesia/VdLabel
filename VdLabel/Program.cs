﻿using Kamishibai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VdLabel;
using WindowsDesktop;
using Wpf.Ui;

var builder = KamishibaiApplication<App, MainWindow>.CreateBuilder();
builder.Services
    .Configure<HostOptions>(op => op.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore)
    .AddHostedService(sp => sp.GetRequiredService<IVirualDesktopService>())
    .AddHostedService<WindowMonitor>()
    .AddHostedService(sp => sp.GetRequiredService<ICommandLabelService>())
    .AddSingleton<IVirualDesktopService, VirtualDesktopService>()
    .AddSingleton<ICommandLabelService, CommandLabelService>()
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