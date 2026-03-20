using Cysharp.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VdLabel;

partial class CommandService(App app, IConfigStore configStore, ILogger<CommandService> logger) : BackgroundService, ICommandService
{
    private readonly App app = app;
    private readonly IConfigStore configStore = configStore;
    private readonly ILogger<CommandService> logger = logger;
    private readonly ConcurrentDictionary<Guid, string> commandCache = new();
    private readonly ConcurrentDictionary<(Guid BadgeId, Guid? DesktopId), (string Label, Color Color)> badgeCommandCache = new();

    public event EventHandler? BadgeResultsUpdated;
    public event EventHandler<LabelResultUpdatedEventArgs>? LabelResultUpdated;

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

    public string? GetCacheResult(Guid desktopId)
        => this.commandCache.TryGetValue(desktopId, out var result) ? result : null;

    public (string Label, Color Color)? GetBadgeResult(Guid badgeId, Guid desktopId)
    {
        if (this.badgeCommandCache.TryGetValue((badgeId, desktopId), out var result))
        {
            return result;
        }
        return this.badgeCommandCache.TryGetValue((badgeId, null), out var shared) ? shared : null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PeriodicTimer? timer = null;
        await this.app.WaitForStartupAsync();
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
                    this.commandCache[desktopConfig.Id] = result;
                    LabelResultUpdated?.Invoke(this, new LabelResultUpdatedEventArgs(desktopConfig.Id, result));
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, "コマンド実行エラー");
                }
                stoppingToken.ThrowIfCancellationRequested();
            }

            // バッジキャッシュの失効処理：設定に存在しないバッジ、コマンド未設定のバッジ、
            // または割り当てが外れたデスクトップのキャッシュエントリを削除する
            foreach (var key in this.badgeCommandCache.Keys.ToList())
            {
                var badge = config.Badges.FirstOrDefault(b => b.Id == key.BadgeId);
                if (badge is null || badge.Command is not { Length: > 0 })
                {
                    // バッジが削除されたかコマンドが未設定になったためキャッシュを削除
                    this.badgeCommandCache.TryRemove(key, out _);
                }
                else if (key.DesktopId.HasValue)
                {
                    // デスクトップへのバッジ割り当てが外れた場合はキャッシュを削除
                    var desktopHasBadge = config.DesktopConfigs.Any(d => d.Id == key.DesktopId.Value && d.BadgeIds.Contains(key.BadgeId));
                    if (!desktopHasBadge)
                    {
                        this.badgeCommandCache.TryRemove(key, out _);
                    }
                }
            }

            var badgeResultsChanged = false;
            foreach (var badgeConfig in config.Badges)
            {
                if (badgeConfig.Command is not { Length: > 0 })
                {
                    continue;
                }
                var hasPlaceholder = badgeConfig.Command.Contains("{desktopId}", StringComparison.OrdinalIgnoreCase);
                if (hasPlaceholder)
                {
                    // Execute per desktop that has this badge assigned
                    foreach (var desktopConfig in config.DesktopConfigs.Where(d => d.BadgeIds.Contains(badgeConfig.Id)))
                    {
                        try
                        {
                            var command = badgeConfig.Command.Replace("{desktopId}", desktopConfig.Id.ToString(), StringComparison.OrdinalIgnoreCase);
                            var output = await ExecuteCommand(command, badgeConfig.Utf8Command, stoppingToken).ConfigureAwait(false);
                            var parsed = JsonSerializer.Deserialize<BadgeCommandResult>(output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (parsed is not null)
                            {
                                var label = string.IsNullOrEmpty(parsed.Label) ? badgeConfig.Label : parsed.Label;
                                var color = TryParseColor(parsed.Color, badgeConfig.Color);
                                this.badgeCommandCache[(badgeConfig.Id, desktopConfig.Id)] = (label, color);
                                badgeResultsChanged = true;
                            }
                        }
                        catch (Exception e)
                        {
                            this.logger.LogError(e, "バッジコマンド実行エラー (Badge ID: {BadgeId}, Desktop ID: {DesktopId}, Command: {Command})", badgeConfig.Id, desktopConfig.Id, badgeConfig.Command);
                        }
                        stoppingToken.ThrowIfCancellationRequested();
                    }
                }
                else
                {
                    // No placeholder — run once and store under null desktop ID
                    try
                    {
                        var output = await ExecuteCommand(badgeConfig.Command, badgeConfig.Utf8Command, stoppingToken).ConfigureAwait(false);
                        var parsed = JsonSerializer.Deserialize<BadgeCommandResult>(output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (parsed is not null)
                        {
                            var shared = (string.IsNullOrEmpty(parsed.Label) ? badgeConfig.Label : parsed.Label, TryParseColor(parsed.Color, badgeConfig.Color));
                            this.badgeCommandCache[(badgeConfig.Id, null)] = shared;
                            badgeResultsChanged = true;
                        }
                    }
                    catch (Exception e)
                    {
                        this.logger.LogError(e, "バッジコマンド実行エラー (Badge ID: {BadgeId}, Command: {Command})", badgeConfig.Id, badgeConfig.Command);
                    }
                    stoppingToken.ThrowIfCancellationRequested();
                }
            }

            if (badgeResultsChanged)
            {
                BadgeResultsUpdated?.Invoke(this, EventArgs.Empty);
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
        timer?.Dispose();
    }

    private static Color TryParseColor(string? htmlColor, Color fallback)
    {
        if (htmlColor is null)
        {
            return fallback;
        }
        try
        {
            return ColorTranslator.FromHtml(htmlColor);
        }
        catch
        {
            return fallback;
        }
    }
}


interface ICommandService
{
    ValueTask<string> ExecuteCommand(string command, bool utf8, CancellationToken token = default);
    string? GetCacheResult(Guid desktopId);
    (string Label, Color Color)? GetBadgeResult(Guid badgeId, Guid desktopId);
    event EventHandler? BadgeResultsUpdated;
    event EventHandler<LabelResultUpdatedEventArgs>? LabelResultUpdated;
}

class LabelResultUpdatedEventArgs(Guid desktopId, string label) : EventArgs
{
    public Guid DesktopId { get; } = desktopId;
    public string Label { get; } = label;
}

/// <summary>
/// バッジ用コマンドの JSON 出力形式を表します。
/// </summary>
record BadgeCommandResult
{
    public string Label { get; init; } = string.Empty;

    /// <summary>HTML カラー文字列（例: "#ff6600"）。</summary>
    public string? Color { get; init; }
}