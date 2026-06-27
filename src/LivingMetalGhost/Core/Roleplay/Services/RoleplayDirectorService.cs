using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.Core.Services;

/// <summary>Director API에서 검증 전 상태 패치를 받아오는 best-effort 서비스다.</summary>
public sealed class RoleplayDirectorService
{
    private readonly StoryPlanStore _storyPlanStore;
    private readonly StoryCharacterStore _storyCharacterStore;
    private readonly ILlmProviderFactory _providerFactory;

    public RoleplayDirectorService(
        StoryPlanStore storyPlanStore,
        StoryCharacterStore storyCharacterStore,
        ILlmProviderFactory providerFactory)
    {
        _storyPlanStore = storyPlanStore;
        _storyCharacterStore = storyCharacterStore;
        _providerFactory = providerFactory;
    }

    public async Task<RoleplayDirectorUpdate?> CreateUpdateAsync(
        AppConfig config,
        CharacterProfile character,
        StoryState state,
        string userText,
        string assistantText,
        string mood,
        CancellationToken cancellationToken)
    {
        if (!config.RoleplayLlm.EnableDirectorStateUpdate)
        {
            return null;
        }

        try
        {
            var options = LlmOptions.FromSettings(config.RoleplayLlm.Director);
            var provider = _providerFactory.Create(options.Provider);
            var characterState = _storyCharacterStore.LoadOrCreateState(character.Id);
            var response = await provider.GenerateAsync(new LlmRequest
            {
                Model = options.Model,
                Options = options,
                UserTitle = config.App.UserTitle,
                History = [],
                SystemPrompt = BuildSystemPrompt(),
                UserText = BuildUserText(
                    state,
                    _storyPlanStore.Load(),
                    characterState,
                    userText,
                    assistantText,
                    mood)
            }, cancellationToken);

            return RoleplayDirectorUpdateParser.Parse(response.Text);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Director가 실패하면 기존 결정론적 상태 갱신기가 폴백으로 동작한다.
            return null;
        }
    }

    private static string BuildSystemPrompt() =>
        """
        You are the Director API for a fictional roleplay engine. You never speak as a character.
        Observe the completed turn and propose a conservative structured state update.
        Never invent an action, emotion, memory, or line for the player. Do not advance the plot beyond what occurred.
        Keep affection, trust, tension, and personality changes gradual.
        Output one JSON object only, without markdown or commentary. Omit fields that should not change.
        Allowed shape:
        {
          "scene": "...", "summary": "...", "story_date": "...", "story_time": "...",
          "location": "...", "mood": "...", "tension": 0, "affection": 0, "status_text": "...",
          "current_appearance": "...", "current_goal": "...",
          "current_emotion": {"anger": 0, "fear": 0, "confusion": 0, "affection": 0, "trust": 0},
          "relationship_metrics": {"affection": 0, "trust": 0, "tension": 0},
          "personality_drift": {"defensiveness": 0, "openness": 0, "dependency": 0, "honesty": 0}
        }
        tension is 0-5, affection and relationship metrics are -100 to 100,
        current emotion is 0-100, and personality drift is 0-10. Write prose values in Korean.
        """;

    private static string BuildUserText(
        StoryState state,
        StoryPlan plan,
        StoryCharacterState characterState,
        string userText,
        string assistantText,
        string mood)
    {
        return $"""
            Current state:
            - Turn: {state.TurnNumber}
            - Scene: {state.Scene}
            - Summary: {state.Summary}
            - Date/time/location: {state.StoryDate} / {state.StoryTime} / {state.Location}
            - Mood/tension/affection: {state.Mood} / {state.Tension} / {state.Affection}
            - Status: {state.StatusText}

            Writer plan (guidance only; do not force it):
            - Title: {plan.Title}
            - Premise: {plan.Premise}
            - Genre: {plan.Genre}

            Character runtime state:
            - Appearance: {characterState.CurrentAppearance}
            - Goal: {characterState.CurrentGoal}
            - Emotion: {FormatMetrics(characterState.CurrentEmotion)}
            - Relationship: {FormatMetrics(characterState.RelationshipMetrics)}
            - Drift: {FormatMetrics(characterState.PersonalityDrift)}

            Completed turn:
            Player input: {userText}
            Character response: {assistantText}
            Rendered mood: {mood}
            """;
    }

    private static string FormatMetrics(IReadOnlyDictionary<string, int> metrics) =>
        metrics.Count == 0
            ? "없음"
            : string.Join(", ", metrics.Select(pair => $"{pair.Key}={pair.Value}"));
}
