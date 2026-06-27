using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.Core.Workbench;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Roleplay;

public sealed class RoleplayCharacterServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "LivingMetalGhost.Tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task GenerateAsync_UsesCharacterEndpointAndWriterPlanContext()
    {
        var paths = new AppPaths(_root);
        var planStore = new StoryPlanStore(paths);
        planStore.Save(new StoryPlan
        {
            Title = "별빛 관측소",
            Premise = "오래된 신호의 정체를 추적한다.",
            Acts = [new StoryAct { Act = 1, Goal = "첫 신호를 확인한다." }]
        });
        var provider = new StubRoleplayProvider("[mood: thinking]\n**그녀가 수신기를 살핀다.**\n신호가 다시 왔어.");
        var providerFactory = new StubRoleplayProviderFactory(provider);
        var history = new ConversationHistoryStore();
        var workspaceStore = new WorkspaceStore(paths);
        var promptAssembler = new PromptAssembler(
            new AdvancedPromptPolicy(new AdvancedSessionLogService(paths, workspaceStore)),
            new CharacterMoodResolver(),
            new StoryCharacterStore(paths),
            planStore);
        var requestFactory = new ConversationRequestFactory(
            promptAssembler,
            history,
            new HiddenTraitScheduler(history));
        var service = new RoleplayCharacterService(
            providerFactory,
            requestFactory,
            new ConversationResponseProcessor(new CharacterMoodResolver()));
        var config = new AppConfig();
        config.RoleplayLlm.Character.Provider = "stub";
        config.RoleplayLlm.Character.Model = "character-model";

        var result = await service.GenerateAsync(
            config,
            RoleplayApiTestSupport.CreateCharacter(),
            new StoryState { Enabled = true },
            "안녕",
            image: null,
            CancellationToken.None);

        Assert.Equal("thinking", result.Mood);
        Assert.Contains("신호가 다시 왔어", result.Text);
        Assert.Equal("character-model", provider.LastRequest?.ResolveModel());
        Assert.Contains("Writer continuity guide", provider.LastRequest?.SystemPrompt);
        Assert.Contains("오래된 신호의 정체", provider.LastRequest?.SystemPrompt);
    }
}
