using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Presentation;

public sealed class CharacterMoodResolver
{
    public string Resolve(
        ConversationMode mode,
        string? requestedMood,
        CharacterVisualProfile visual)
    {
        var normalizedMood = Normalize(requestedMood, visual);
        return CharacterExpressionPolicy.ResolveResponseState(normalizedMood, mode);
    }

    public IReadOnlyList<string> GetAvailableMoods(CharacterVisualProfile visual)
    {
        var moods = new List<string>();

        if (visual is ModularCharacterVisualProfile modular)
        {
            moods.AddRange(modular.States.Keys
                .Where(mood => !string.IsNullOrWhiteSpace(mood))
                .Where(mood => !string.Equals(
                    mood,
                    modular.BlinkStateName,
                    StringComparison.OrdinalIgnoreCase)));

            if (modular.SpeakingStates.Count > 0 ||
                modular.SpeakingStatesByState.Count > 0)
            {
                moods.Add("speaking");
            }
        }
        else if (visual is SpriteCharacterVisualProfile sprite)
        {
            if (!string.IsNullOrWhiteSpace(sprite.IdleSpritePath))
            {
                moods.Add("idle");
            }

            moods.AddRange(sprite.MoodSpritePaths.Keys);
            moods.AddRange(sprite.MoodBlinkSpritePaths.Keys);
            moods.AddRange(sprite.MoodCycleSpritePaths.Keys);

            if (sprite.SpeakingSpritePaths.Count > 0)
            {
                moods.Add("speaking");
            }
        }

        if (moods.Count == 0)
        {
            moods.Add("speaking");
        }

        return moods
            .Where(mood => !string.IsNullOrWhiteSpace(mood))
            .Select(mood => mood.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(mood => mood, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string? Normalize(
        string? mood,
        CharacterVisualProfile visual)
    {
        if (string.IsNullOrWhiteSpace(mood))
        {
            return null;
        }

        var normalized = mood.Trim().ToLowerInvariant();
        return GetAvailableMoods(visual).Contains(
            normalized,
            StringComparer.OrdinalIgnoreCase)
            ? normalized
            : null;
    }
}
