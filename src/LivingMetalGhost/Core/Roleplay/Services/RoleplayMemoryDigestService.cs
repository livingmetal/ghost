using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.Core.Services;

public sealed class RoleplayMemoryDigestService
{
    private const int DigestIntervalTurns = 6;
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
        var state = _storyStateStore.Load();
        if (!IsDigestDue(turnCount, state.LastMemoryDigestTurn))
        {
            return;
        }

        try
        {
            var recent = _storyStateStore.ReadRecentMemory(DigestIntervalTurns);
            if (recent.Count == 0)
            {
                return;
            }

            var existingFacts = string.Join(
                Environment.NewLine,
                state.Facts.Select(fact => $"- ({fact.Kind}, w{fact.Weight}) {fact.Text}"));
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
            latest.Facts = digested
                .Select(fact => new StoryMemoryFact
                {
                    Kind = fact.Kind,
                    Text = fact.Text,
                    Weight = fact.Weight
                })
                .ToList();
            latest.LastMemoryDigestTurn = turnCount;
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

    public static bool IsDigestDue(int turnCount, int lastSuccessfulDigestTurn)
    {
        return turnCount > 0 &&
               turnCount - Math.Max(0, lastSuccessfulDigestTurn) >= DigestIntervalTurns;
    }

    private static string BuildSystemPrompt()
    {
        return """
            You maintain a compact fictional-roleplay memory. You are not a character; you only summarize.
            Given existing memory facts and the most recent turns, return an UPDATED fact list.
            Output rules:
            - Output a JSON array only. No prose, no code fences.
            - Each item: {"kind": "...", "text": "...", "weight": 1-5}.
            - kind is one of: premise, self, relationship, question.
            - Keep premise and self facts stable. Update relationship texture and open questions from recent events.
            - Merge duplicates. Keep at most 8 facts. Write text in Korean, one short sentence each.
            """;
    }

    private static string BuildUserText(string existingFacts, string recentTurns)
    {
        return $"""
            Existing facts:
            {existingFacts}

            Recent turns:
            {recentTurns}

            Return the updated JSON fact array.
            """;
    }
}
