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
}

class ConfigStore(ILogger<ConfigStore> logger) : IConfigStore
{
    private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VdLabel", "config.json");
    private static readonly JsonSerializerOptions options = new()
    {
        Converters = { new ColorJsonConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private readonly ILogger<ConfigStore> logger = logger;

    public event EventHandler? Saved;

    public ValueTask<Config> Load()
    {
        Config? config = null;
        try
        {
            if (File.Exists(this.path))
            {
                using var fs = File.OpenRead(this.path);
                config = JsonSerializer.Deserialize<Config>(fs, options);
            }
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "設定の読み込みに失敗しました");
        }
        return new(config ?? new Config() { DesktopConfigs = { new() { Id = Guid.Empty } } });
    }

    public ValueTask Save(Config config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
        using (var fs = File.Create(this.path))
        {
            JsonSerializer.Serialize(fs, config, options);
        }
        this.Saved?.Invoke(this, EventArgs.Empty);
        return default;
    }

    private class ColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => ColorTranslator.FromHtml(reader.GetString()!);
        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
            => writer.WriteStringValue(ColorTranslator.ToHtml(value));
    }
}
