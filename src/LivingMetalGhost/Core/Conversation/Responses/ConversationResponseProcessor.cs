using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;

namespace LivingMetalGhost.Core.Conversation;

public sealed record ProcessedConversationResponse(
    string Text,
    string Mood);

public sealed class ConversationResponseProcessor
{
    private readonly CharacterMoodResolver _characterMoodResolver;

    public ConversationResponseProcessor(CharacterMoodResolver characterMoodResolver)
    {
        _characterMoodResolver = characterMoodResolver;
    }

    public ProcessedConversationResponse Process(
        string responseText,
        ConversationMode mode,
        CharacterVisualProfile visual)
    {
        var parsed = ConversationResponseParser.ParseMoodTag(responseText);
        var modeCleanText = mode == ConversationMode.Story
            ? ConversationResponseParser.StripLegacyRoleplayTags(parsed.Text)
            : parsed.Text;
        var characterText = CharacterSpeechSanitizer.Sanitize(modeCleanText);
        var characterMood = _characterMoodResolver.Resolve(
            mode,
            parsed.Mood,
            visual);

        return new ProcessedConversationResponse(
            characterText,
            characterMood);
    }
}
