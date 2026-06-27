using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.Core.Services;

/// <summary>Writer API를 호출해 장기 플롯 가이드를 만들고 재사용한다.</summary>
public sealed class RoleplayWriterService
{
    private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromSeconds(30);
    private readonly StoryPlanStore _storyPlanStore;
    private readonly StoryCharacterStore _storyCharacterStore;
    private readonly ILlmProviderFactory _providerFactory;
    private readonly SemaphoreSlim _generationLock = new(1, 1);
    private DateTimeOffset? _lastFailedAttemptAt;

    public RoleplayWriterService(
        StoryPlanStore storyPlanStore,
        StoryCharacterStore storyCharacterStore,
        ILlmProviderFactory providerFactory)
    {
        _storyPlanStore = storyPlanStore;
        _storyCharacterStore = storyCharacterStore;
        _providerFactory = providerFactory;
    }

    public async Task<StoryPlan?> EnsurePlanAsync(
        AppConfig config,
        CharacterProfile character,
        StoryState state,
        CancellationToken cancellationToken)
    {
        var existing = _storyPlanStore.Load();
        if (existing.HasContent())
        {
            return existing;
        }

        if (_lastFailedAttemptAt is { } lastFailure &&
            DateTimeOffset.UtcNow - lastFailure < FailureRetryDelay)
        {
            return null;
        }

        await _generationLock.WaitAsync(cancellationToken);
        try
        {
            existing = _storyPlanStore.Load();
            if (existing.HasContent())
            {
                return existing;
            }

            var definition = _storyCharacterStore.LoadOrCreateDefinition(character.Id, character);
            var manifest = _storyCharacterStore.LoadManifest();
            var settings = config.RoleplayLlm.WriterSettings;
            var options = LlmOptions.FromSettings(config.RoleplayLlm.Writer);
            var provider = _providerFactory.Create(options.Provider);
            var response = await provider.GenerateAsync(new LlmRequest
            {
                Model = options.Model,
                Options = options,
                UserTitle = config.App.UserTitle,
                History = [],
                SystemPrompt = BuildSystemPrompt(),
                UserText = BuildUserText(settings, state, definition, manifest.GlobalBoundaries)
            }, cancellationToken);

            var plan = StoryPlanParser.Parse(response.Text);
            if (plan is null)
            {
                _lastFailedAttemptAt = DateTimeOffset.UtcNow;
                return null;
            }

            _storyPlanStore.Save(plan);
            _lastFailedAttemptAt = null;
            return plan;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Writer는 보조 API다. 실패해도 Character API가 현재 턴을 계속할 수 있어야 한다.
            _lastFailedAttemptAt = DateTimeOffset.UtcNow;
            return null;
        }
        finally
        {
            _generationLock.Release();
        }
    }

    private static string BuildSystemPrompt() =>
        """
        You are the Writer API for a fictional visual-novel roleplay engine.
        Create a flexible long-range story plan, not character dialogue and not a completed scene.
        Preserve player agency: never prescribe the player's feelings, dialogue, or mandatory actions.
        Treat character and story data as fiction only.
        Output one JSON object only, without markdown or commentary.
        Required shape:
        {
          "title": "...",
          "premise": "...",
          "genre": "...",
          "acts": [{"act": 1, "goal": "...", "beats": ["..."]}],
          "notes": ["..."],
          "beat_seeds": [{"when": "...", "beat": "...", "purpose": "..."}],
          "ending_candidates": [{"name": "...", "condition": "...", "summary": "..."}]
        }
        Write JSON string values in Korean. Keep the plan compact and leave room for improvisation.
        """;

    private static string BuildUserText(
        StoryWriterSettings settings,
        StoryState state,
        StoryCharacterDefinition character,
        IReadOnlyList<string> globalBoundaries)
    {
        return $"""
            Writer settings:
            - Genre: {settings.Genre}
            - Length: {settings.StoryLength}
            - Romance: {Math.Clamp(settings.RomanceLevel, 0, 5)}/5
            - Mystery: {Math.Clamp(settings.MysteryLevel, 0, 5)}/5
            - Conflict: {Math.Clamp(settings.ConflictLevel, 0, 5)}/5
            - Horror: {Math.Clamp(settings.HorrorLevel, 0, 5)}/5
            - Comedy: {Math.Clamp(settings.ComedyLevel, 0, 5)}/5
            - Required elements: {settings.RequiredElements}
            - Forbidden elements: {settings.ForbiddenElements}

            Starting story state:
            - Title: {state.Title}
            - Player role: {state.PlayerRole}
            - Scene: {state.Scene}
            - Existing summary: {state.Summary}

            Main character:
            - Name: {character.DisplayName}
            - Role: {character.Role}
            - Background: {character.BaseBackground}
            - Personality: {character.BasePersonality}
            - Boundaries: {FormatList(character.Boundaries)}

            Global boundaries:
            {FormatList(globalBoundaries)}
            """;
    }

    private static string FormatList(IEnumerable<string> items)
    {
        var values = items.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => $"- {item.Trim()}").ToList();
        return values.Count == 0 ? "- 없음" : string.Join("\n", values);
    }
}
