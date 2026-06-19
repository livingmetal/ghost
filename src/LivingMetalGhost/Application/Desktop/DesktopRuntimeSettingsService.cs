using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.AppCore.Desktop;

public sealed record ProactiveChatSettings(
    bool Enabled,
    int MinMinutes,
    int MaxMinutes);

public sealed record AdvancedModeAvailability(
    bool IsLocalLmAvailable,
    bool IsAdvancedModeAvailable);

public sealed class DesktopRuntimeSettingsService
{
    private readonly AppConfigLoader _configLoader;

    public DesktopRuntimeSettingsService(AppConfigLoader configLoader)
    {
        _configLoader = configLoader;
    }

    public ProactiveChatSettings GetProactiveChatSettings()
    {
        var app = _configLoader.Load().App;
        var legacyInterval = Math.Clamp(
            app.ProactiveChatIntervalMinutes,
            5,
            240);
        var minMinutes = app.ProactiveChatMinMinutes <= 0
            ? legacyInterval
            : Math.Clamp(app.ProactiveChatMinMinutes, 5, 240);
        var maxMinutes = app.ProactiveChatMaxMinutes <= 0
            ? legacyInterval
            : Math.Clamp(app.ProactiveChatMaxMinutes, 5, 240);

        return new ProactiveChatSettings(
            app.EnableProactiveChat,
            Math.Min(minMinutes, maxMinutes),
            Math.Max(minMinutes, maxMinutes));
    }

    public async Task<AdvancedModeAvailability> DetectAdvancedModeAvailabilityAsync()
    {
        var provider = _configLoader.Load().AdvancedLlm.Provider;
        if (IsInstalledAppsProvider(provider))
        {
            InstalledAppDetector.Invalidate();
            var info = await InstalledAppDetector.DetectAsync();
            return new AdvancedModeAvailability(
                info.IsAnyAvailable,
                info.IsAnyAvailable);
        }

        if (string.Equals(provider, "lmbot", StringComparison.OrdinalIgnoreCase))
        {
            LocalLmDetector.Invalidate();
            var info = await LocalLmDetector.DetectAsync();
            return new AdvancedModeAvailability(
                info.IsAvailable,
                info.IsAvailable);
        }

        return new AdvancedModeAvailability(
            IsLocalLmAvailable: false,
            IsAdvancedModeAvailable: true);
    }

    private static bool IsInstalledAppsProvider(string provider) =>
        string.Equals(provider, "installed-apps", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider, "installed_apps", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider, "apps", StringComparison.OrdinalIgnoreCase);
}
