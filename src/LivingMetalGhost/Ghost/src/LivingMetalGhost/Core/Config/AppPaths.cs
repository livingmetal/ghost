using System.IO;

namespace LivingMetalGhost.Core.Config;

public sealed class AppPaths
{
    public AppPaths(string root)
    {
        Root = root;
        Logs = Path.Combine(root, "logs");
        Cache = Path.Combine(root, "cache");
        ConfigFile = Path.Combine(root, "config.json");
        ApiKeyFile = Path.Combine(root, "api-key.dat");
    }

    public string Root { get; }
    public string Logs { get; }
    public string Cache { get; }
    public string ConfigFile { get; }
    public string ApiKeyFile { get; }
}
