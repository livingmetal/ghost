using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Conversation;

public sealed class ConversationHistoryStore
{
    private const int MaximumHistoryMessages = 20;
    private const int MaximumHistoryCharacters = 24000;
    private readonly Dictionary<ConversationHistoryChannel, List<LlmHistoryMessage>> _histories = new();
    private readonly Lock _lock = new();

    public IReadOnlyList<LlmHistoryMessage> GetSnapshot(ConversationMode mode)
    {
        lock (_lock)
        {
            return GetHistory(mode)
                .Select(message => new LlmHistoryMessage
                {
                    Role = message.Role,
                    Content = message.Content
                })
                .ToArray();
        }
    }

    public void Add(ConversationMode mode, string role, string content)
    {
        lock (_lock)
        {
            var history = GetHistory(mode);
            history.Add(new LlmHistoryMessage
            {
                Role = role,
                Content = content
            });

            while (history.Count > MaximumHistoryMessages ||
                   history.Sum(message => message.Content.Length) > MaximumHistoryCharacters)
            {
                history.RemoveAt(0);
            }
        }
    }

    public int CountByRole(ConversationMode mode, string role)
    {
        lock (_lock)
        {
            return GetHistory(mode).Count(
                message => string.Equals(message.Role, role, StringComparison.OrdinalIgnoreCase));
        }
    }

    private List<LlmHistoryMessage> GetHistory(ConversationMode mode)
    {
        var channel = ConversationModePolicy.GetHistoryChannel(mode);
        if (!_histories.TryGetValue(channel, out var history))
        {
            history = [];
            _histories[channel] = history;
        }

        return history;
    }
}
