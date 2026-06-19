using LivingMetalGhost.AppCore.Desktop;
using LivingMetalGhost.Core.Config;
using Xunit;

namespace LivingMetalGhost.Tests.Application.Desktop;

public sealed class DesktopRuntimeSettingsServiceTests : IDisposable
{
    private readonly string _root;
    private readonly AppConfigLoader _configLoader;
    private readonly DesktopRuntimeSettingsService _service;

    public DesktopRuntimeSettingsServiceTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "LivingMetalGhost.Tests",
            Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(_root);
        _configLoader = new AppConfigLoader(paths);
        _service = new DesktopRuntimeSettingsService(_configLoader);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void GetProactiveChatSettings_UsesLegacyIntervalForUnsetBounds()
    {
        var config = _configLoader.Load();
        config.App.EnableProactiveChat = true;
        config.App.ProactiveChatIntervalMinutes = 35;
        config.App.ProactiveChatMinMinutes = 0;
        config.App.ProactiveChatMaxMinutes = 0;
        _configLoader.Save(config);

        var settings = _service.GetProactiveChatSettings();

        Assert.True(settings.Enabled);
        Assert.Equal(35, settings.MinMinutes);
        Assert.Equal(35, settings.MaxMinutes);
    }

    [Fact]
    public void GetProactiveChatSettings_ClampsAndOrdersBounds()
    {
        var config = _configLoader.Load();
        config.App.ProactiveChatMinMinutes = 300;
        config.App.ProactiveChatMaxMinutes = 1;
        _configLoader.Save(config);

        var settings = _service.GetProactiveChatSettings();

        Assert.Equal(5, settings.MinMinutes);
        Assert.Equal(240, settings.MaxMinutes);
    }
}
