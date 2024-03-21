using Cysharp.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace VdLabel;

partial class NameCommandService(IConfigStore configStore, IVirualDesktopService virualDesktopService, ILogger<NameCommandService> logger) : BackgroundService
{
    private readonly IConfigStore configStore = configStore;
    private readonly IVirualDesktopService virualDesktopService = virualDesktopService;
    private readonly ILogger<NameCommandService> logger = logger;

    [GeneratedRegex(@"^(""[^""]+""|\S+)", RegexOptions.Compiled)]
    private static partial Regex FilePathRegex();

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
                // 最初のスペースで区切られた部分または全体をファイルとする。"で囲まれているときはスペースを無視する
                // それ以降は引数として渡す
                var match = FilePathRegex().Match(desktopConfig.Command);
                if (!match.Success)
                {
                    this.logger.LogWarning("コマンドが見つかりませんでした");
                    continue;
                }
                var command = match.Value;
                var args = desktopConfig.Command[match.Length..].Trim();
                try
                {
                    var lines = await ProcessX.StartAsync(fileName: command, args).ToTask(stoppingToken);
                    this.virualDesktopService.SetName(desktopConfig.Id, string.Join(Environment.NewLine, lines));
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, "コマンド実行エラー");
                }
                stoppingToken.ThrowIfCancellationRequested();
            }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
        timer?.Dispose();
    }
}
