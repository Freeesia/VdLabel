using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octokit;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;

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

        while (!stoppingToken.IsCancellationRequested)
        {
            var path = await CheckAndDownload(stoppingToken);
            if (path is not null)
            {
                // TODO: 新しいバージョンを通知して実行できるようにする
                Process.Start("msiexec", $"/i {path}");
                return;
            }
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task<string?> CheckAndDownload(CancellationToken stoppingToken)
    {
        var release = await this.client.Repository.Release.GetLatest(owner, this.name);
        stoppingToken.ThrowIfCancellationRequested();

        if (new Version(release.Name) > this.version)
        {
            this.logger.LogInformation($"新しいバージョン {release.Name} が利用可能です。");
            var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".msi"));
            if (asset is null)
            {
                this.logger.LogWarning("インストーラーが見つかりませんでした。");
                return null;
            }
            string installerUrl = asset.BrowserDownloadUrl;

            // インストーラーをダウンロードして実行
            var dir = Path.Combine(Path.GetTempPath(), this.name);
            string installerPath = Path.Combine(dir, asset.Name);
            if (File.Exists(installerPath))
            {
                this.logger.LogInformation("インストーラーはすでにダウンロードされています。");
                return installerPath;
            }
            Directory.CreateDirectory(dir);
            using var downloader = new HttpClient();
            using var fs = File.Create(installerPath);
            using var stream = await downloader.GetStreamAsync(installerUrl, stoppingToken);
            await stream.CopyToAsync(fs, stoppingToken);
            return installerPath;
        }
        else
        {
            this.logger.LogInformation("アプリケーションは最新のバージョンです。");
            return null;
        }
    }
}
