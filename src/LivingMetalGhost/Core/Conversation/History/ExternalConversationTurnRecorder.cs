using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Conversation;

public sealed class ExternalConversationTurnRecorder
{
    private readonly ConversationHistoryStore _historyStore;

    public ExternalConversationTurnRecorder(ConversationHistoryStore historyStore)
    {
        _historyStore = historyStore;
    }

    public void Record(
        ConversationMode mode,
        string userText,
        string assistantText,
        string source)
    {
        if (string.IsNullOrWhiteSpace(userText) &&
            string.IsNullOrWhiteSpace(assistantText))
        {
            return;
        }

        var normalizedSource = string.IsNullOrWhiteSpace(source)
            ? "external"
            : source.Trim();
        var rememberedAssistantText = string.IsNullOrWhiteSpace(assistantText)
            ? $"[{normalizedSource} result]{Environment.NewLine}(no text returned)"
            : $"[{normalizedSource} result]{Environment.NewLine}{assistantText.Trim()}";

        if (!string.IsNullOrWhiteSpace(userText))
        {
            _historyStore.Add(mode, "user", userText.Trim());
        }

        _historyStore.Add(mode, "assistant", rememberedAssistantText);
    }
}
