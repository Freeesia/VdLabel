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
    ValueTask Save(Config? config = null);
}

class ConfigStore : IConfigStore
{
    private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VdLabel", "config.json");
    private static readonly JsonSerializerOptions options = new()
    {
        Converters = { new ColorJsonConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private Config? config;

    public event EventHandler? Saved;

    public ValueTask<Config> Load()
    {
        if (File.Exists(this.path))
        {
            using var fs = File.OpenRead(this.path);
            this.config = JsonSerializer.Deserialize<Config>(fs, options);
        }
        return new(this.config ??= new Config()
        {
            DesktopConfigs = { new() { Id = Guid.Empty } }
        });
    }

    public ValueTask Save(Config? config = null)
    {
        if (config is not null)
        {
            this.config = config;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
        using (var fs = File.Create(this.path))
        {
            JsonSerializer.Serialize(fs, this.config, options);
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
