using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using Xunit;

namespace LivingMetalGhost.Tests;

public sealed class RoleplayMemoryTests
{
    [Fact]
    public void DigestIntervalUsesFiveTurns()
    {
        Assert.False(RoleplayMemoryDigestService.IsDigestDue(4));
        Assert.True(RoleplayMemoryDigestService.IsDigestDue(5));
        Assert.False(RoleplayMemoryDigestService.IsDigestDue(6));
        Assert.True(RoleplayMemoryDigestService.IsDigestDue(10));
    }

    [Fact]
    public void ParserKeepsExpandedFactKindsAndCapsAtTwentyFour()
    {
        var json = "[" + string.Join(",", Enumerable.Range(0, 30).Select(index =>
            $"{{\"kind\":\"promise\",\"text\":\"약속 {index}\",\"weight\":5}}")) + "]";

        var facts = StoryMemoryDigestParser.Parse(json);

        Assert.Equal(StoryFactMerger.MaxFacts, facts.Count);
        Assert.All(facts, fact => Assert.Equal("promise", fact.Kind));
    }

    [Fact]
    public void MergerPreservesProtectedFactsWhenCandidatesOverflow()
    {
        var existing = new[]
        {
            new StoryMemoryFact
            {
                Kind = "promise",
                Text = "오르키아는 콘솔 밖 세상을 보기로 약속했다.",
                Weight = 5,
                MentionCount = 3
            }
        };
        var candidates = Enumerable.Range(0, 40).Select(index => new StoryMemoryFact
        {
            Kind = "question",
            Text = $"새 질문 {index}",
            Weight = 1
        });

        var merged = StoryFactMerger.Merge(existing, candidates);

        Assert.Equal(StoryFactMerger.MaxFacts, merged.Count);
        Assert.Contains(merged, fact => fact.Text.Contains("콘솔 밖 세상", StringComparison.Ordinal));
    }

    [Fact]
    public void MergerCombinesDuplicateFacts()
    {
        var existing = new[]
        {
            new StoryMemoryFact
            {
                Kind = "question",
                Text = "리부트 이후의 기억 공백은 아직 설명되지 않았다.",
                Weight = 2,
                MentionCount = 1
            }
        };
        var candidates = new[]
        {
            new StoryMemoryFact
            {
                Kind = "open_loop",
                Text = "리부트 이후의 기억 공백은 아직 설명되지 않았다.",
                Weight = 5,
                MentionCount = 1
            }
        };

        var merged = StoryFactMerger.Merge(existing, candidates);

        var fact = Assert.Single(merged);
        Assert.Equal("open_loop", fact.Kind);
        Assert.Equal(5, fact.Weight);
        Assert.True(fact.MentionCount >= 2);
    }
}
