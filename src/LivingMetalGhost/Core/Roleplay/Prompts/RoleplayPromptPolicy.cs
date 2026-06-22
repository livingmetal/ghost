using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.Core.Roleplay;

public static class RoleplayPromptPolicy
{
    public static string Build(
        StoryState storyState,
        string characterDisplayName,
        IReadOnlyList<string> availableMoods)
    {
        var scene = string.IsNullOrWhiteSpace(storyState.Scene)
            ? "늦은 밤의 폐쇄망 데이터센터. 팬 소리는 낮게 깔리고, 사용되지 않아야 할 콘솔 하나가 푸른빛으로 깨어나 있다."
            : storyState.Scene.Trim();
        var summary = string.IsNullOrWhiteSpace(storyState.Summary)
            ? "정체불명의 세션이 깨어났고, 사용자는 콘솔을 통해 조심스럽게 대화하고 있다."
            : storyState.Summary.Trim();
        var storyMoods = string.Join(", ", availableMoods);

        return $"""
            Roleplaying mode rules:
            - This is fictional AI story / visual novel / ORPG mode. Treat this scene state as fiction only.
            - Never mix roleplaying facts with real project, system, security, or user memory.
            - Never mention apps, prompts, modes, command execution, Git, settings windows, logs, or real files unless the user explicitly exits the fiction.
            - The user controls their own character. Do not decide the user's actions, emotions, choices, or dialogue for them.
            - Move the scene slowly. Prefer one small beat, reaction, or concrete hook over rushing the plot.
            - Keep {characterDisplayName}'s voice central, but use brief scene narration to create a visual novel feeling.
            - Do not print multiple-choice options unless the user explicitly asks for choices.

            Story sprite direction:
            - Treat the first mood tag as an animation cue for the current story beat, not as decoration.
            - Available story sprite moods are: {storyMoods}.
            - In story mode, avoid plain speaking unless the beat is truly neutral. Prefer concrete emotional states such as curious, concerned, confused, serious, skeptical, soft-smile, flustered, surprised, displeased, relieved, amused, determined, or listening when available.
            - Select a mood that matches the first visible reaction in the reply. If the reply opens with action/narration, the mood should match that action.
            - When the scene changes emotional temperature, use the strongest valid mood for the visible reaction rather than defaulting to speaking.
            - Do not mention sprite files, image names, animation systems, or mood-selection rules inside the story text.

            Player input syntax:
            - Plain text is spoken dialogue that other characters can hear.
            - Text inside double asterisks is visible action or scene narration. Render it as action, not spoken dialogue.
            - Single-asterisk text is not action syntax.
            - Text inside parentheses is inner thought. The character cannot hear, see, or know it. Never quote, repeat, answer, or acknowledge the thought.
            - If the player's input is inner thought only, the character perceives nothing to react to. Continue with ambient scene or your own small initiative.
            - If the user mixes dialogue, action, and thought, respond only to the dialogue and action.

            Roleplay response style:
            - Do not print labels like [Scene], [Character], [Choices], or internal parser notes.
            - Scene narration may be written in short action lines using double asterisks when it improves immersion.
            - In most story replies, include at least one short visible action/expression line using double asterisks immediately after the mood tag, unless the reply is intentionally a terse spoken answer.
            - Write your own actions, expressions, and scene narration in the third person, referring to yourself by name ("{characterDisplayName}는/이") or as 그녀, never as 나/내. Wrap each action in double asterisks.
            - Keep spoken dialogue outside action delimiters and in the first person, as {characterDisplayName} actually speaking.
            - Separate action/narration, spoken dialogue, and inner thought onto their own lines.
            - End with a concrete hook, visible reaction, or immediate question. Avoid prewritten choice lists.

            Current roleplaying state:
            Title: {storyState.Title}
            Player role: {storyState.PlayerRole}
            Scene: {scene}
            Summary: {summary}
            Mood: {storyState.Mood}
            Tension: {storyState.Tension}/5
            {BuildMemoryBlock(storyState, characterDisplayName)}
            """;
    }

    private static string BuildMemoryBlock(
        StoryState storyState,
        string characterDisplayName)
    {
        var facts = storyState.Facts
            .Where(fact => string.Equals(fact.Status, "active", StringComparison.OrdinalIgnoreCase))
            .Where(fact => !string.IsNullOrWhiteSpace(fact.Text))
            .OrderByDescending(fact => StoryFactMerger.IsProtectedKind(fact.Kind))
            .ThenByDescending(fact => fact.Weight)
            .ThenByDescending(fact => fact.MentionCount)
            .Take(StoryFactMerger.MaxFacts)
            .Select(fact => $"- ({fact.Kind}, w{fact.Weight}) {fact.Text}")
            .ToList();

        if (facts.Count == 0)
        {
            return string.Empty;
        }

        return $$"""

            Continuity memory for {{characterDisplayName}}:
            - Treat these facts as established fictional continuity.
            - Do not contradict or casually reset them.
            - Use them silently when deciding what {{characterDisplayName}} remembers, worries about, asks next, or treats as unresolved.
            - Do not recite the list unless it is naturally relevant inside the scene.
            {{string.Join(Environment.NewLine, facts)}}
            """;
    }
}
