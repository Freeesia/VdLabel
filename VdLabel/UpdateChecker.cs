using Kamishibai;
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

internal class UpdateChecker : BackgroundService
{
    private const string owner = "Freeesia";
    private readonly GitHubClient client;
    private readonly string name;
    private readonly Version version;
    private readonly ILogger<UpdateChecker> logger;

    public UpdateChecker(ILogger<UpdateChecker> logger)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetName();
        this.name = name.Name ?? throw new InvalidOperationException();
        this.version = name.Version ?? throw new InvalidOperationException();
        this.client = new(new ProductHeaderValue(this.name, this.version.ToString()));
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

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

    private async Task CheckAndDownload(CancellationToken stoppingToken)
    {
        var release = await this.client.Repository.Release.GetLatest(owner, this.name);
        stoppingToken.ThrowIfCancellationRequested();

        if (new Version(release.Name) <= this.version)
        {
            this.logger.LogInformation("アプリケーションは最新のバージョンです。");
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
        Directory.CreateDirectory(dir);
        using var downloader = new HttpClient();
        using var fs = File.Create(installerPath);
        using var stream = await downloader.GetStreamAsync(installerUrl, stoppingToken);
        await stream.CopyToAsync(fs, stoppingToken);
        ShowUpdateNotification(release.Name, release.HtmlUrl, installerPath, false);
    }

    private static void ShowUpdateNotification(string version, string url, string path, bool supress)
    {
        var builder = new ToastContentBuilder()
            .AddText($"新しいバージョン {version} がリリースされました", AdaptiveTextStyle.Title)
            .AddText($"更新版をインストールしますか？")
            .AddArgument(nameof(url), url)
            .AddArgument(nameof(path), path)
            .AddArgument(nameof(version), version)
            .AddButton(new ToastButton()
                .AddArgument("action", ToastActions.Install)
                .SetContent("インストール"))
            .AddButton(new ToastButton()
                .SetContent("更新内容の確認")
                .AddArgument("action", ToastActions.OpenBrowser)
                .SetBackgroundActivation());

        {
            var args = new ToastArguments();
            args.Add("action", ToastActions.Skip);
            builder.Content.Actions.ContextMenuItems.Add(new("このバージョンをスキップ", args.ToString()));
        }

        builder.Show(t =>
        {
            t.ExpiresOnReboot = true;
            t.NotificationMirroring = NotificationMirroring.Disabled;
            t.SuppressPopup = supress;
        });
    }

    private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = ToastArguments.Parse(e.Argument);
        switch (args.GetEnum<ToastActions>("action"))
        {
            case ToastActions.Install:
                Process.Start("msiexec", $"/i {args.Get("path")}");
                break;
            case ToastActions.Skip:
                break;
            case ToastActions.OpenBrowser:
                Process.Start(new ProcessStartInfo(args.Get("url")) { UseShellExecute = true });
                ShowUpdateNotification(args.Get("version"), args.Get("url"), args.Get("path"), true);
                break;
            default:
                break;
        }
    }

    private enum ToastActions
    {
        Install,
        Skip,
        OpenBrowser
    }
}
