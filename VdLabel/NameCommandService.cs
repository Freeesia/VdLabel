using Cysharp.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace VdLabel;

class NameCommandService(IConfigStore configStore, IVirualDesktopService virualDesktopService) : BackgroundService
{
    private readonly IConfigStore configStore = configStore;
    private readonly IVirualDesktopService virualDesktopService = virualDesktopService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PeriodicTimer? timer = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            var config = await this.configStore.Load();
            stoppingToken.ThrowIfCancellationRequested();
            var span = TimeSpan.FromSeconds(config.CommandInterval);
            timer ??= new PeriodicTimer(span);
            if (timer.Period != span)
            {
                timer.Period = span;
            }
            foreach (var desktopConfig in config.DesktopConfigs)
            {
                if (desktopConfig.Command is not { Length: > 0 })
                {
                    continue;
                }
                var lines = await ProcessX.StartAsync(desktopConfig.Command).ToTask(stoppingToken);
                stoppingToken.ThrowIfCancellationRequested();
                this.virualDesktopService.SetName(desktopConfig.Id, string.Join(Environment.NewLine, lines));
            }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
        timer?.Dispose();
    }
}
