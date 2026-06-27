using System.IO;
using System.Security.Cryptography;
using System.Text;
using LivingMetalGhost.Core.Config;

namespace LivingMetalGhost.Core.Security;

public sealed class DpapiSecretStore
{
    private const string BasicApiKeySource = "dpapi:basic";
    private const string AdvancedApiKeySource = "dpapi:advanced";
    private const string RoleplayWriterApiKeySource = "dpapi:roleplay-writer";
    private const string RoleplayCharacterApiKeySource = "dpapi:roleplay-character";
    private const string RoleplayDirectorApiKeySource = "dpapi:roleplay-director";
    private const string RoleplayMemoryApiKeySource = "dpapi:roleplay-memory";
    private readonly AppPaths _paths;

    public DpapiSecretStore(AppPaths paths)
    {
        _paths = paths;
    }

    public bool HasApiKey => File.Exists(_paths.ApiKeyFile);
    public bool HasBasicApiKey => HasApiKeyForSource(BasicApiKeySource);
    public bool HasAdvancedApiKey => HasApiKeyForSource(AdvancedApiKeySource);
    public bool HasRoleplayWriterApiKey => HasApiKeyForSource(RoleplayWriterApiKeySource);
    public bool HasRoleplayCharacterApiKey => HasApiKeyForSource(RoleplayCharacterApiKeySource);
    public bool HasRoleplayDirectorApiKey => HasApiKeyForSource(RoleplayDirectorApiKeySource);
    public bool HasRoleplayMemoryApiKey => HasApiKeyForSource(RoleplayMemoryApiKeySource);

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

    public void SaveRoleplayWriterApiKey(string apiKey)
    {
        SaveApiKey(RoleplayWriterApiKeySource, apiKey);
    }

    public void SaveRoleplayCharacterApiKey(string apiKey)
    {
        SaveApiKey(RoleplayCharacterApiKeySource, apiKey);
    }

    public void SaveRoleplayDirectorApiKey(string apiKey)
    {
        SaveApiKey(RoleplayDirectorApiKeySource, apiKey);
    }

    public void SaveRoleplayMemoryApiKey(string apiKey)
    {
        SaveApiKey(RoleplayMemoryApiKeySource, apiKey);
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

    public bool HasApiKeyForSource(string? apiKeySource)
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
            RoleplayWriterApiKeySource => Path.Combine(_paths.Root, "roleplay-writer-api-key.dat"),
            RoleplayCharacterApiKeySource => Path.Combine(_paths.Root, "roleplay-character-api-key.dat"),
            RoleplayDirectorApiKeySource => Path.Combine(_paths.Root, "roleplay-director-api-key.dat"),
            RoleplayMemoryApiKeySource => Path.Combine(_paths.Root, "roleplay-memory-api-key.dat"),
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
            "dpapi:roleplay-writer" or "roleplay-writer" or "story-writer" => RoleplayWriterApiKeySource,
            "dpapi:roleplay-character" or "roleplay-character" or "story-character" => RoleplayCharacterApiKeySource,
            "dpapi:roleplay-director" or "roleplay-director" or "story-director" => RoleplayDirectorApiKeySource,
            "dpapi:roleplay-memory" or "roleplay-memory" or "story-memory" => RoleplayMemoryApiKeySource,
            "dpapi:basic" or "basic" or "default" or "dpapi" => BasicApiKeySource,
            _ => BasicApiKeySource
        };
    }

    public static string BasicSource => BasicApiKeySource;
    public static string AdvancedSource => AdvancedApiKeySource;
    public static string RoleplayWriterSource => RoleplayWriterApiKeySource;
    public static string RoleplayCharacterSource => RoleplayCharacterApiKeySource;
    public static string RoleplayDirectorSource => RoleplayDirectorApiKeySource;
    public static string RoleplayMemorySource => RoleplayMemoryApiKeySource;
}
