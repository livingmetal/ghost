using System.IO;
using System.Reflection;
using System.Text.Json;

namespace LivingMetalGhost.Core.Config;

public sealed class AppConfigLoader
{
    private readonly AppPaths _paths;

    public AppConfigLoader(AppPaths paths)
    {
        _paths = paths;
    }

    public AppConfig Load()
    {
        Directory.CreateDirectory(_paths.Root);
        var configPath = _paths.ConfigFile;

        if (!File.Exists(configPath))
        {
            var content = LoadEmbeddedTemplate();
            File.WriteAllText(configPath, content);
        }

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch
        {
            var fallback = new AppConfig();
            File.WriteAllText(configPath, JsonSerializer.Serialize(fallback, JsonOptions));
            return fallback;
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(_paths.Root);
        File.WriteAllText(_paths.ConfigFile, JsonSerializer.Serialize(config, JsonOptions));
    }

    private static string LoadEmbeddedTemplate()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("LivingMetalGhost.config.template.json");
        if (stream is null)
        {
            return JsonSerializer.Serialize(new AppConfig(), JsonOptions);
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };
}
