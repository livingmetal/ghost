using System.IO;
using System.Security.Cryptography;
using System.Text;
using LivingMetalGhost.Core.Config;

namespace LivingMetalGhost.Core.Security;

public sealed class DpapiSecretStore
{
    private readonly AppPaths _paths;

    public DpapiSecretStore(AppPaths paths)
    {
        _paths = paths;
    }

    public bool HasApiKey => File.Exists(_paths.ApiKeyFile);

    public void SaveApiKey(string apiKey)
    {
        Directory.CreateDirectory(_paths.Root);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (File.Exists(_paths.ApiKeyFile))
            {
                File.Delete(_paths.ApiKeyFile);
            }

            return;
        }

        File.WriteAllText(_paths.ApiKeyFile, Protect(apiKey.Trim()));
    }

    public string LoadApiKey()
    {
        if (!File.Exists(_paths.ApiKeyFile))
        {
            return string.Empty;
        }

        try
        {
            return Unprotect(File.ReadAllText(_paths.ApiKeyFile));
        }
        catch
        {
            return string.Empty;
        }
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
}
