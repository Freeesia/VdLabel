using Microsoft.Extensions.Logging;
using System.Drawing;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VdLabel;

interface IConfigStore
{
    event EventHandler? Saved;

    ValueTask<Config> Load();
    ValueTask Save(Config config);

    ValueTask<UpdateInfo?> LoadUpdateInfo();
    ValueTask SaveUpdateInfo(UpdateInfo updateInfo);
}

class ConfigStore(ILogger<ConfigStore> logger) : IConfigStore
{
    private static readonly string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VdLabel");
    private static readonly string configPath = Path.Combine(baseDir, "config.json");
    private static readonly string updateInfoPath = Path.Combine(baseDir, "update.json");

    private static readonly JsonSerializerOptions options = new()
    {
        Converters = { new ColorJsonConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private readonly ILogger<ConfigStore> logger = logger;

    public event EventHandler? Saved;

    public async ValueTask<Config> Load()
    {
        Config? config = null;
        try
        {
            if (File.Exists(configPath))
            {
                using var fs = File.OpenRead(configPath);
                config = await JsonSerializer.DeserializeAsync<Config>(fs, options).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "設定の読み込みに失敗しました");
        }
        return config ?? new Config() { DesktopConfigs = { new() { Id = Guid.Empty } } };
    }

    public async ValueTask<UpdateInfo?> LoadUpdateInfo()
    {
        try
        {
            if (File.Exists(updateInfoPath))
            {
                using var fs = File.OpenRead(updateInfoPath);
                return await JsonSerializer.DeserializeAsync<UpdateInfo>(fs, options);
            }
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "更新情報の読み込みに失敗しました");
        }
        return null;
    }

    public async ValueTask Save(Config config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        using (var fs = File.Create(configPath))
        {
            await JsonSerializer.SerializeAsync(fs, config, options);
        }
        this.Saved?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask SaveUpdateInfo(UpdateInfo updateInfo)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(updateInfoPath)!);
            using var fs = File.Create(updateInfoPath);
            await JsonSerializer.SerializeAsync(fs, updateInfo, options);
        }
        catch (Exception)
        {
            this.logger.LogError("更新情報の保存に失敗しました");
        }
    }

    private class ColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => ColorTranslator.FromHtml(reader.GetString()!);
        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
            => writer.WriteStringValue(ColorTranslator.ToHtml(value));
    }
}

record UpdateInfo(string Version, string Url, string? Path, DateTime CheckedAt, bool Skip);
