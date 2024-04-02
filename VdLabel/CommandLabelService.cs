using Cysharp.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace VdLabel;

partial class CommandLabelService(IConfigStore configStore, IVirualDesktopService virualDesktopService, ILogger<CommandLabelService> logger) : BackgroundService, ICommandLabelService
{
    private readonly IConfigStore configStore = configStore;
    private readonly IVirualDesktopService virualDesktopService = virualDesktopService;
    private readonly ILogger<CommandLabelService> logger = logger;

    [GeneratedRegex(@"^(""[^""]+""|\S+)", RegexOptions.Compiled)]
    private static partial Regex FilePathRegex();

    public async ValueTask<string> ExecuteCommand(string command, bool utf8, CancellationToken token = default)
    {
        // 最初のスペースで区切られた部分または全体をファイルとする。"で囲まれているときはスペースを無視する
        // それ以降は引数として渡す
        var match = FilePathRegex().Match(command);
        if (!match.Success)
        {
            throw new InvalidOperationException("ファイルパスの解析に失敗しました");
        }
        var fileName = match.Value;
        var args = command[match.Length..].Trim();
        var lines = await ProcessX.StartAsync(fileName: fileName, args, encoding: utf8 ? Encoding.UTF8 : null).ToTask(token).ConfigureAwait(false);
        return string.Join(Environment.NewLine, lines);
    }

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
                try
                {
                    var result = await ExecuteCommand(desktopConfig.Command, desktopConfig.Utf8Command, stoppingToken).ConfigureAwait(false);
                    this.virualDesktopService.SetName(desktopConfig.Id, result);
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


interface ICommandLabelService : IHostedService
{
    ValueTask<string> ExecuteCommand(string command, bool utf8, CancellationToken token = default);
}