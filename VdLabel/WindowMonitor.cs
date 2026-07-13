using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PInvoke;
using System.Text.RegularExpressions;
using WindowsDesktop;
using static VdLabel.ProcessUtility;

namespace VdLabel;

class WindowMonitor(ILogger<WindowMonitor> logger, IConfigStore configStore, App app) : BackgroundService, IWindowMonitor
{
    private readonly ILogger<WindowMonitor> logger = logger;
    private readonly IConfigStore configStore = configStore;
    private readonly App app = app;
    private readonly Dictionary<IntPtr, string> checkedWindows = [];
    private bool needReload = true;
    private TargetWindow[] targetWindows = [];
    private Dictionary<Guid, List<string>> desktopWindows = [];

    private record TargetWindow(Guid DesktopId, WindowMatchType MatchType, Regex Regex);

    public IReadOnlyList<string> GetDesktopWindows(Guid desktopId)
        => this.desktopWindows.TryGetValue(desktopId, out var windows) ? windows : [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        this.logger.LogInformation("ウィンドウ監視開始");
        await ReloadTargetProcess().ConfigureAwait(false);
        this.configStore.Saved += ConfigStore_Saved;
        await this.app.WaitForStartupAsync().ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            if (this.needReload)
            {
                this.needReload = false;
                await ReloadTargetProcess().ConfigureAwait(false);
            }
            CheckWindows();
            stoppingToken.ThrowIfCancellationRequested();
            await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
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

    private void CheckWindows()
    {
        var now = DateTime.Now;
        this.logger.LogDebug("ウィンドウチェック開始");
        var windows = new HashSet<nint>();
        var desktopWindows = new Dictionary<Guid, List<string>>();
        User32.EnumWindows((hWnd, lParam) =>
        {
            windows.Add(hWnd);
            // ウィンドウが表示されていない場合は今後再チェックする
            if (!User32.IsWindowVisible(hWnd))
            {
                return true;
            }
            // ツールチップやコンテキストメニューは無視
            var className = User32.GetClassName(hWnd);
            if (className is "tooltips_class32" or "#32768")
            {
                return true;
            }
            _ = User32.GetWindowThreadProcessId(hWnd, out var processId);
            // 自分自身のプロセスは無視
            if (processId == Environment.ProcessId)
            {
                return true;
            }

            if (GetProcessPath(processId) is not { } path)
            {
                return true;
            }

            if (VirtualDesktop.FromHwnd(hWnd) is not { } desktop)
            {
                return true;
            }
            if (!desktopWindows.TryGetValue(desktop.Id, out var processPaths))
            {
                processPaths = [];
                desktopWindows.Add(desktop.Id, processPaths);
            }
            processPaths.Add(path);

            if (this.targetWindows.Length == 0)
            {
                return true;
            }

            // ウィンドウタイトルが取得できない場合はスキップ
            if (GetWindowTitle(hWnd) is not { } windowTitle)
            {
                return true;
            }

            // すでにチェック済みかつタイトルが変わっていない場合はスキップ
            if (this.checkedWindows.TryGetValue(hWnd, out var exist) && exist == windowTitle)
            {
                return true;
            }

            // ウィンドウが所属するプロセスのパスが取得できない場合はスキップ
            // ウィンドウが所属するプロセスのコマンドラインが取得できない場合はスキップ
            if (GetCommandLine(processId) is not { } commandLine)
            {
                this.checkedWindows[hWnd] = windowTitle;
                return true;
            }

            if (this.targetWindows.FirstOrDefault(t => t.Regex.IsMatch(GetCheckText(t.MatchType, path, commandLine, windowTitle))) is not { } target)
            {
                // ウィンドウがターゲットでない場合はチェック済みとする
                this.checkedWindows[hWnd] = windowTitle;
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
                else if (VirtualDesktop.FromId(target.DesktopId) is { } targetDesktop)
                {
                    if (VirtualDesktop.FromHwnd(hWnd)?.Id != targetDesktop.Id)
                    {
                        this.logger.LogDebug($"ウィンドウ検出: {windowTitle} to {targetDesktop.Name}");
                        VirtualDesktop.MoveToDesktop(hWnd, targetDesktop);
                    }
                }

                // 固定 or 移動したウィンドウはチェック済みとする
                this.checkedWindows[hWnd] = windowTitle;
            }
            catch (Exception e)
            {
                // 移動するタイミングですでにウィンドウが閉じられていることがある
                this.logger.LogWarning(e, $"ウィンドウ移動失敗: title:{windowTitle}, class:{className}, comamnd:{commandLine}");
            }
            return true;
        }, 0);
        // 存在しないウィンドウは削除
        foreach (var hWnd in this.checkedWindows.Keys.Except(windows).ToArray())
        {
            this.checkedWindows.Remove(hWnd);
        }
        this.desktopWindows = desktopWindows;
        this.logger.LogDebug($"ウィンドウチェック終了: {DateTime.Now - now}");
    }

    private static string GetCheckText(WindowMatchType type, string path, string commandLine, string windowTitle)
        => type switch
        {
            WindowMatchType.CommandLine => commandLine,
            WindowMatchType.Title => windowTitle,
            WindowMatchType.Path => path,
            _ => string.Empty,
        };
}

interface IWindowMonitor
{
    IReadOnlyList<string> GetDesktopWindows(Guid desktopId);
}
