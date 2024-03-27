using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PInvoke;
using System.Management;
using System.Text.RegularExpressions;
using WindowsDesktop;

namespace VdLabel;
class WindowMonitor(ILogger<WindowMonitor> logger, IConfigStore configStore) : BackgroundService
{
    private readonly ILogger<WindowMonitor> logger = logger;
    private readonly IConfigStore configStore = configStore;
    private readonly HashSet<IntPtr> checkedWindows = [];
    private bool needReload = true;
    private TargetWindow[] targetWindows = [];

    private record TargetWindow(Guid DesktopId, WindowMatchType MatchType, Regex Regex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("ウィンドウ監視開始");
        await ReloadTargetProcess().ConfigureAwait(false);
        this.configStore.Saved += ConfigStore_Saved;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (this.needReload)
            {
                this.needReload = false;
                await ReloadTargetProcess().ConfigureAwait(false);
            }
            CheckProcesses();
            stoppingToken.ThrowIfCancellationRequested();
            await Task.Delay(500, stoppingToken);
            stoppingToken.ThrowIfCancellationRequested();
        }
        this.logger.LogInformation("ウィンドウ監視終了");
    }

    private async ValueTask ReloadTargetProcess()
    {
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.Singleline;
        static Regex ToRegex(WindowPatternType patternType, string pattern)
            => patternType switch
            {
                WindowPatternType.Wildcard => new("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", options),
                WindowPatternType.Regex => new(pattern, options),
                _ => throw new NotImplementedException(),
            };

        var config = await this.configStore.Load().ConfigureAwait(false);
        this.targetWindows = config.DesktopConfigs
            .SelectMany(c => c.TargetWindows.Select(p => (c.Id, p)))
            .Select(c => new TargetWindow(c.Id, c.p.MatchType, ToRegex(c.p.PatternType, c.p.Pattern)))
            .ToArray();
        this.checkedWindows.Clear();
        this.logger.LogDebug("ターゲットプロセス再読み込み");
    }

    private void ConfigStore_Saved(object? sender, EventArgs e)
        => this.needReload = true;

    private void CheckProcesses()
    {
        this.logger.LogDebug("プロセスチェック開始");
        var windows = new HashSet<IntPtr>();
        if (this.targetWindows.Length == 0)
        {
            return;
        }
        User32.EnumWindows((hWnd, lParam) =>
        {
            // ウィンドウが表示されていない場合は今後再チェックする
            if (!User32.IsWindowVisible(hWnd))
            {
                return true;
            }
            windows.Add(hWnd);

            // すでにチェック済みのウィンドウはスキップ
            if (!this.checkedWindows.Add(hWnd))
            {
                return true;
            }

            _ = User32.GetWindowThreadProcessId(hWnd, out var processId);
            // 自分自身のプロセスは無視
            if (processId == Environment.ProcessId)
            {
                return true;
            }
            var commandLine = GetCommandLine(processId);
            string windowTitle;
            try
            {
                windowTitle = User32.GetWindowText(hWnd);
            }
            catch (Win32Exception)
            {
                // 仮想デスクトップを切り替えるタイミングで例外が発生することがある
                windows.Remove(hWnd);
                return true;
            }
            if (string.IsNullOrEmpty(commandLine) || string.IsNullOrEmpty(windowTitle))
            {
                return true;
            }
            if (this.targetWindows.FirstOrDefault(t => t.Regex.IsMatch(GetCheckText(t.MatchType, commandLine, windowTitle))) is not { } target)
            {
                return true;
            }
            try
            {
                if (target.DesktopId == Guid.Empty)
                {
                    if (!VirtualDesktop.IsPinnedWindow(hWnd))
                    {
                        this.logger.LogDebug($"ウィンドウ検出: {windowTitle} 固定");
                        VirtualDesktop.PinWindow(hWnd);
                    }
                }
                else if (VirtualDesktop.FromId(target.DesktopId) is { } desktop)
                {
                    if (VirtualDesktop.FromHwnd(hWnd)?.Id != desktop.Id)
                    {
                        this.logger.LogDebug($"ウィンドウ検出: {windowTitle} to {desktop.Name}");
                        VirtualDesktop.MoveToDesktop(hWnd, desktop);
                    }
                }
            }
            catch (Exception)
            {
                // 移動するタイミングですでにウィンドウが閉じられていることがある
                this.logger.LogWarning($"ウィンドウ移動失敗: {windowTitle}, {commandLine}");
                windows.Remove(hWnd);
            }
            return true;
        }, nint.Zero);
        this.checkedWindows.IntersectWith(windows);
        this.logger.LogDebug("プロセスチェック終了");
    }

    private static string GetCheckText(WindowMatchType type, string commandLine, string windowTitle)
        => type switch
        {
            WindowMatchType.CommandLine => commandLine,
            WindowMatchType.Title => windowTitle,
            _ => string.Empty,
        };

    private static string? GetCommandLine(int processId)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = '{processId}'");
        using var mo = searcher.Get().Cast<ManagementBaseObject>().SingleOrDefault();
        return mo?["CommandLine"] as string;
    }
}
