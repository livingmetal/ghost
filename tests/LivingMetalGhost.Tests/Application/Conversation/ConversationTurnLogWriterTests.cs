using LivingMetalGhost.AppCore.Conversation;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using Xunit;

namespace LivingMetalGhost.Tests.Application.Conversation;

public sealed class ConversationTurnLogWriterTests : IDisposable
{
    private readonly string _root;
    private readonly AppConfigLoader _configLoader;
    private readonly ConversationLogService _logService;
    private readonly ConversationTurnLogWriter _writer;

    public ConversationTurnLogWriterTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "LivingMetalGhost.Tests",
            Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(_root);
        _configLoader = new AppConfigLoader(paths);
        _logService = new ConversationLogService(paths);
        _writer = new ConversationTurnLogWriter(_configLoader, _logService);
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
    public void GetProviderLabel_UsesAdvancedSettingsOnlyForAdvancedMode()
    {
        var config = _configLoader.Load();
        config.Llm.Provider = "daily-provider";
        config.Llm.Model = "daily-model";
        config.AdvancedLlm.Provider = "advanced-provider";
        config.AdvancedLlm.Model = "advanced-model";
        _configLoader.Save(config);

        Assert.Equal(
            "DAILY: daily-provider / daily-model",
            _writer.GetProviderLabel(ConversationMode.Daily));
        Assert.Equal(
            "STORY: daily-provider / daily-model",
            _writer.GetProviderLabel(ConversationMode.Story));
        Assert.Equal(
            "ADVANCED: advanced-provider / advanced-model",
            _writer.GetProviderLabel(ConversationMode.Advanced));
    }

    [Fact]
    public async Task WriteAsync_PersistsCompleteTurnMetadata()
    {
        var config = _configLoader.Load();
        config.Llm.Provider = "test-provider";
        config.Llm.Model = "test-model";
        _configLoader.Save(config);

        await _writer.WriteAsync(new ConversationTurnLogContext(
            "hello",
            "welcome",
            IsProactive: false,
            "idle",
            ConversationMode.Daily,
            "character-id",
            "Character Name"), CancellationToken.None);

        var date = Assert.Single(_logService.GetAvailableDates());
        var entry = Assert.Single(await _logService.ReadAsync(date, CancellationToken.None));
        Assert.Equal("hello", entry.UserText);
        Assert.Equal("welcome", entry.AssistantText);
        Assert.Equal("test-provider", entry.Provider);
        Assert.Equal("test-model", entry.Model);
        Assert.Equal("DAILY: test-provider / test-model", entry.ProviderLabel);
        Assert.Equal("character-id", entry.CharacterId);
        Assert.Equal("Character Name", entry.CharacterName);
        Assert.Equal("idle", entry.Mood);
        Assert.Equal("Daily", entry.Mode);
    }
}
