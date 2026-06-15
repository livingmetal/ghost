using System.IO;
using System.Security.Cryptography;
using System.Text;
using LivingMetalGhost.Core.Config;

namespace LivingMetalGhost.Core.Security;

public sealed class DpapiSecretStore
{
    private const string BasicApiKeySource = "dpapi:basic";
    private const string AdvancedApiKeySource = "dpapi:advanced";
    private readonly AppPaths _paths;

    public DpapiSecretStore(AppPaths paths)
    {
        _paths = paths;
    }

    public bool HasApiKey => File.Exists(_paths.ApiKeyFile);
    public bool HasBasicApiKey => HasApiKey(BasicApiKeySource);
    public bool HasAdvancedApiKey => HasApiKey(AdvancedApiKeySource);

    public void SaveApiKey(string apiKey)
    {
        SaveApiKey(BasicApiKeySource, apiKey);
    }

    public void SaveBasicApiKey(string apiKey)
    {
        SaveApiKey(BasicApiKeySource, apiKey);
    }

    public void SaveAdvancedApiKey(string apiKey)
    {
        SaveApiKey(AdvancedApiKeySource, apiKey);
    }

    public void SaveApiKey(string apiKeySource, string apiKey)
    {
        var path = ResolveApiKeyPath(apiKeySource);
        Directory.CreateDirectory(_paths.Root);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        File.WriteAllText(path, Protect(apiKey.Trim()));
    }

    public string LoadApiKey()
    {
        return LoadApiKey(BasicApiKeySource);
    }

    public string LoadApiKey(string? apiKeySource)
    {
        var path = ResolveApiKeyPath(apiKeySource);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            return Unprotect(File.ReadAllText(path));
        }
        catch
        {
            return string.Empty;
        }
    }

    public bool HasApiKey(string? apiKeySource)
    {
        return File.Exists(ResolveApiKeyPath(apiKeySource));
    }

    public string Protect(string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret);
        return Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
    }

    public string Unprotect(string protectedSecret)
    {
        var bytes = Convert.FromBase64String(protectedSecret);
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
    }

    private string ResolveApiKeyPath(string? apiKeySource)
    {
        return NormalizeApiKeySource(apiKeySource) switch
        {
            AdvancedApiKeySource => Path.Combine(_paths.Root, "advanced-api-key.dat"),
            _ => _paths.ApiKeyFile
        };
    }

    public static string NormalizeApiKeySource(string? apiKeySource)
    {
        if (string.IsNullOrWhiteSpace(apiKeySource))
        {
            return BasicApiKeySource;
        }

        return apiKeySource.Trim().ToLowerInvariant() switch
        {
            "dpapi:advanced" or "advanced" or "advanced-dpapi" => AdvancedApiKeySource,
            "dpapi:basic" or "basic" or "default" or "dpapi" => BasicApiKeySource,
            _ => BasicApiKeySource
        };
    }

    public static string BasicSource => BasicApiKeySource;
    public static string AdvancedSource => AdvancedApiKeySource;
}
