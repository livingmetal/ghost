using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Conversation;

public sealed class HiddenTraitScheduler
{
    private readonly ConversationHistoryStore _historyStore;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, HiddenTraitRuntimeState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public HiddenTraitScheduler(ConversationHistoryStore historyStore)
    {
        _historyStore = historyStore;
    }

    public string BuildDirective(CharacterProfile character, ConversationMode mode)
    {
        if (character.HiddenTraits.Count == 0)
        {
            return string.Empty;
        }

        var upcomingReplyIndex = _historyStore.CountByRole(mode, "assistant") + 1;
        var activeTraits = GetActiveTraits(character, mode, upcomingReplyIndex);
        if (activeTraits.Count == 0)
        {
            return "You may have hidden sides to your personality, but they should stay dormant unless they naturally surface.";
        }

        var prompts = string.Join(
            Environment.NewLine,
            activeTraits.Select(trait => $"- {trait.Prompt}"));

        return $$"""
            A rare hidden side of your personality is surfacing for this reply.
            Let it show subtly and naturally without naming it as a mode switch or hidden trait.
            Keep the character recognizable and avoid breaking safety, coherence, or the established relationship.
            Hidden side guidance:
            {{prompts}}
            """;
    }

    private IReadOnlyList<HiddenCharacterTrait> GetActiveTraits(
        CharacterProfile character,
        ConversationMode mode,
        int upcomingReplyIndex)
    {
        var activeTraits = new List<HiddenCharacterTrait>();

        lock (_lock)
        {
            foreach (var trait in character.HiddenTraits)
            {
                var historyChannel = ConversationModePolicy.GetHistoryChannel(mode);
                var key = $"{historyChannel}:{character.Id}:{trait.Id}";
                if (!_states.TryGetValue(key, out var state))
                {
                    state = new HiddenTraitRuntimeState();
                    state.ScheduleNext(upcomingReplyIndex, trait);
                    _states[key] = state;
                }

                if (state.RemainingActiveReplies <= 0 &&
                    upcomingReplyIndex >= state.NextActivationReplyIndex)
                {
                    state.RemainingActiveReplies = Random.Shared.Next(
                        trait.MinActiveReplies,
                        trait.MaxActiveReplies + 1);
                }

                if (state.RemainingActiveReplies <= 0)
                {
                    continue;
                }

                activeTraits.Add(trait);
                state.RemainingActiveReplies--;

                if (state.RemainingActiveReplies <= 0)
                {
                    state.ScheduleNext(upcomingReplyIndex, trait);
                }
            }
        }

        return activeTraits;
    }

    private sealed class HiddenTraitRuntimeState
    {
        public int NextActivationReplyIndex { get; set; }
        public int RemainingActiveReplies { get; set; }

        public void ScheduleNext(int currentReplyIndex, HiddenCharacterTrait trait)
        {
            NextActivationReplyIndex = currentReplyIndex +
                                       Random.Shared.Next(
                                           trait.MinReplyGap,
                                           trait.MaxReplyGap + 1);
        }
    }
}
