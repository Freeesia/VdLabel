using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using Octokit;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using Windows.UI.Notifications;

namespace VdLabel;

internal class UpdateChecker : BackgroundService, IUpdateChecker
{
    private const string owner = "Freeesia";
    private readonly GitHubClient client;
    private readonly string name;
    private readonly Version version;
    private readonly ILogger<UpdateChecker> logger;
    private readonly IConfigStore configStore;
    private readonly App app;

    private bool hasUpdate;

    public event EventHandler? UpdateAvailable;

    public bool HasUpdate
    {
        get => this.hasUpdate;
        private set
        {
            if (this.hasUpdate != value)
            {
                this.hasUpdate = value;
                this.UpdateAvailable?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsUpdatable { get; }

    public UpdateChecker(ILogger<UpdateChecker> logger, IConfigStore configStore, App app)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetName();
        this.name = name.Name ?? throw new InvalidOperationException();
        this.version = name.Version ?? throw new InvalidOperationException();
        this.client = new(new ProductHeaderValue(this.name, this.version.ToString()));
        this.logger = logger;
        this.configStore = configStore;
        this.app = app;
        this.IsUpdatable = IsInstalled();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.IsUpdatable)
        {
            this.logger.LogInformation("インストールされていないアプリなのでチェックしない");
            return;
        }

        await this.app.WaitForStartupAsync();
        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckAndDownload(stoppingToken);
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }
        finally
        {
            ToastNotificationManagerCompat.History.Clear();
            ToastNotificationManagerCompat.Uninstall();
        }
    }

    private async ValueTask CheckAndDownload(CancellationToken stoppingToken)
    {
        var updateInfo = await this.configStore.LoadUpdateInfo();

        // 更新情報がない場合は最新のリリースを取得
        // 1日以上経過していたら最新のリリースを取得
        if (updateInfo is null || updateInfo.CheckedAt < DateTime.UtcNow.AddDays(-1))
        {
            var release = await this.client.Repository.Release.GetLatest(owner, this.name);
            stoppingToken.ThrowIfCancellationRequested();

            if (new Version(release.Name) <= this.version)
            {
                this.logger.LogInformation("アプリケーションは最新のバージョンです。");
                await this.configStore.SaveUpdateInfo(new(release.Name, release.HtmlUrl, null, DateTime.UtcNow, false)).ConfigureAwait(false);
                return;
            }
            this.logger.LogInformation($"新しいバージョン {release.Name} が利用可能です。");
            var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".msi"));
            if (asset is null)
            {
                this.logger.LogWarning("インストーラーが見つかりませんでした。");
                return;
            }
            string installerUrl = asset.BrowserDownloadUrl;

            // インストーラーをダウンロードして実行
            var dir = Path.Combine(Path.GetTempPath(), this.name);
            string installerPath = Path.Combine(dir, asset.Name);
            if (File.Exists(installerPath))
            {
                this.logger.LogInformation("インストーラーはすでにダウンロードされています。");
            }
            else
            {
                Directory.CreateDirectory(dir);
                using var downloader = new HttpClient();
                using var fs = File.Create(installerPath);
                using var stream = await downloader.GetStreamAsync(installerUrl, stoppingToken);
                await stream.CopyToAsync(fs, stoppingToken);
                this.logger.LogInformation("インストーラーをダウンロードしました。");
            }
            await this.configStore.SaveUpdateInfo(new(release.Name, release.HtmlUrl, installerPath, DateTime.UtcNow, false)).ConfigureAwait(false);
            ShowUpdateNotification(release.Name, release.HtmlUrl, installerPath, false);
            this.HasUpdate = true;
        }
        // バージョンが新しい場合は通知
        else if (new Version(updateInfo.Version) > this.version && !updateInfo.Skip && updateInfo.Path is not null && File.Exists(updateInfo.Path))
        {
            ShowUpdateNotification(updateInfo.Version, updateInfo.Url, updateInfo.Path, false);
            this.HasUpdate = true;
        }
    }

    private static void ShowUpdateNotification(string version, string url, string path, bool supress)
    {
        var builder = new ToastContentBuilder()
            .AddText(string.Format(Properties.Resources.NewVersionReleased, version), AdaptiveTextStyle.Title)
            .AddText(Properties.Resources.InstallUpdatePrompt)
            .AddArgument(nameof(url), url)
            .AddArgument(nameof(path), path)
            .AddArgument(nameof(version), version)
            .AddButton(new ToastButton()
                .AddArgument("action", ToastActions.Install)
                .SetContent(Properties.Resources.Install))
            .AddButton(new ToastButton()
                .SetContent(Properties.Resources.CheckReleaseNotes)
                .AddArgument("action", ToastActions.OpenBrowser)
                .SetBackgroundActivation());

        {
            var args = new ToastArguments();
            args.Add("action", ToastActions.Skip);
            args.Add(nameof(url), url);
            args.Add(nameof(path), path);
            args.Add(nameof(version), version);
            builder.Content.Actions.ContextMenuItems.Add(new(Properties.Resources.SkipThisVersion, args.ToString()));
        }

        builder.Show(t =>
        {
            t.ExpiresOnReboot = true;
            t.NotificationMirroring = NotificationMirroring.Disabled;
            t.SuppressPopup = supress;
        });
    }

    private async void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = ToastArguments.Parse(e.Argument);
        if (!args.TryGetValue<ToastActions>("action", out var action))
        {
            this.app.Dispatcher.Invoke(() => this.app.MainWindow.Show());
            return;
        }
        switch (action)
        {
            case ToastActions.Install:
                Process.Start("msiexec", $"/i {args.Get("path")}");
                break;
            case ToastActions.Skip:
                await this.configStore.SaveUpdateInfo(new(args.Get("version"), args.Get("url"), args.Get("path"), DateTime.UtcNow, true)).ConfigureAwait(false);
                break;
            case ToastActions.OpenBrowser:
                Process.Start(new ProcessStartInfo(args.Get("url")) { UseShellExecute = true });
                ShowUpdateNotification(args.Get("version"), args.Get("url"), args.Get("path"), true);
                break;
            default:
                break;
        }
    }

    public async Task Check(CancellationToken token)
    {
        var updateInfo = await this.configStore.LoadUpdateInfo();
        if (updateInfo is null)
        {
            await CheckAndDownload(token);
        }
        else if (new Version(updateInfo.Version) > this.version && !updateInfo.Skip && updateInfo.Path is not null && File.Exists(updateInfo.Path))
        {
            ShowUpdateNotification(updateInfo.Version, updateInfo.Url, updateInfo.Path, false);
            this.HasUpdate = true;
        }
        else
        {
            await this.configStore.SaveUpdateInfo(updateInfo with { CheckedAt = DateTime.MinValue, Skip = false }).ConfigureAwait(false);
            await CheckAndDownload(token);
        }
    }

    private static bool IsInstalled()
        => Path.GetDirectoryName(Environment.ProcessPath)
        == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StudioFreesia", "VdLabel");

    private enum ToastActions
    {
        Install,
        Skip,
        OpenBrowser
    }
}

interface IUpdateChecker
{
    bool IsUpdatable { get; }

    bool HasUpdate { get; }

    event EventHandler? UpdateAvailable;

    Task Check(CancellationToken token);
}