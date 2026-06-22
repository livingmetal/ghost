using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.Core.Services;

public sealed class RoleplayMemoryDigestService
{
    private const int DigestIntervalTurns = 5;
    private readonly StoryStateStore _storyStateStore;
    private readonly ILlmProviderFactory _providerFactory;

    public RoleplayMemoryDigestService(
        StoryStateStore storyStateStore,
        ILlmProviderFactory providerFactory)
    {
        _storyStateStore = storyStateStore;
        _providerFactory = providerFactory;
    }

    public async Task DigestIfDueAsync(
        LlmOptions options,
        CancellationToken cancellationToken)
    {
        var turnCount = _storyStateStore.CountMemoryEntries();
        if (!IsDigestDue(turnCount))
        {
            return;
        }

        try
        {
            var state = _storyStateStore.Load();
            var recent = _storyStateStore.ReadRecentMemory(DigestIntervalTurns);
            if (recent.Count == 0)
            {
                return;
            }

            var existingFacts = string.Join(
                Environment.NewLine,
                state.Facts
                    .Where(fact => string.Equals(fact.Status, "active", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(fact => StoryFactMerger.IsProtectedKind(fact.Kind))
                    .ThenByDescending(fact => fact.Weight)
                    .Select(fact => $"- ({fact.Kind}, w{fact.Weight}, m{fact.MentionCount}) {fact.Text}"));
            var recentTurns = string.Join(
                Environment.NewLine,
                recent.Select(entry => $"User: {entry.UserText}\nCharacter: {entry.AssistantText}"));

            var provider = _providerFactory.Create(options.Provider);
            var response = await provider.GenerateAsync(new LlmRequest
            {
                UserText = BuildUserText(existingFacts, recentTurns),
                UserTitle = string.Empty,
                Model = options.Model,
                Options = options,
                SystemPrompt = BuildSystemPrompt(),
                History = []
            }, cancellationToken);

            var digested = StoryMemoryDigestParser.Parse(response.Text);
            if (digested.Count == 0)
            {
                return;
            }

            var latest = _storyStateStore.Load();
            latest.Facts = StoryFactMerger.Merge(latest.Facts, digested).ToList();
            _storyStateStore.Save(latest);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Memory digestion is best-effort and must not fail the active turn.
        }
    }

    public static bool IsDigestDue(int turnCount)
    {
        return turnCount > 0 && turnCount % DigestIntervalTurns == 0;
    }

    private static string BuildSystemPrompt()
    {
        return """
            You maintain compact fictional-roleplay continuity memory. You are not a character; you only summarize.
            Given existing memory facts and the most recent turns, return continuity-critical candidate facts to add or update.
            Do not return decorative prose, transient emotions, or a full scene transcript.
            Output rules:
            - Output a JSON array only. No prose, no code fences.
            - Each item: {"kind": "...", "text": "...", "weight": 1-5}.
            - kind is one of: premise, self, player, relationship, promise, open_loop, preference, location, item, boundary, question.
            - Protect premise, self, relationship, promise, open_loop, and boundary facts from accidental deletion.
            - Prefer facts that affect future continuity: promises, secrets, unresolved questions, relationship shifts, player preferences, important places, and named items.
            - Merge duplicates conceptually. Keep at most 24 candidate facts. Write text in Korean, one short sentence each.
            """;
    }

    private static string BuildUserText(string existingFacts, string recentTurns)
    {
        return $"""
            Existing active facts:
            {existingFacts}

            Recent turns:
            {recentTurns}

            Return candidate memory facts that should be added or updated.
            """;
    }
}
