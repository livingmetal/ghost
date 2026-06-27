using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Roleplay;

public sealed class StoryFactMergerTests
{
    [Fact]
    public void Merge_PreservesProtectedFactsMissingFromCandidates()
    {
        var existing = new[]
        {
            new StoryMemoryFact { Kind = "premise", Text = "세계는 반복되고 있다.", Weight = 5 },
            new StoryMemoryFact { Kind = "question", Text = "문은 누가 열었나?", Weight = 2 }
        };
        var candidates = new[]
        {
            new StoryMemoryFact { Kind = "relationship", Text = "서로를 조금 신뢰한다.", Weight = 3 }
        };

        var merged = StoryFactMerger.Merge(existing, candidates);

        Assert.Contains(merged, fact => fact.Text == "세계는 반복되고 있다.");
        Assert.Contains(merged, fact => fact.Text == "서로를 조금 신뢰한다.");
        Assert.DoesNotContain(merged, fact => fact.Text == "문은 누가 열었나?");
    }

    [Fact]
    public void Merge_PrefersProtectedOrHigherWeightDuplicate()
    {
        var merged = StoryFactMerger.Merge(
            [new StoryMemoryFact { Kind = "premise", Text = "같은 사실", Weight = 2 }],
            [new StoryMemoryFact { Kind = "question", Text = "같은 사실", Weight = 5 }]);

        var fact = Assert.Single(merged);
        Assert.Equal("premise", fact.Kind);
    }

    [Fact]
    public void Parser_AcceptsContinuityKindsAndUsesMergerLimit()
    {
        var json = "[" + string.Join(",", Enumerable.Range(0, 20).Select(index =>
            $"{{\"kind\":\"promise\",\"text\":\"약속 {index}\",\"weight\":5}}")) + "]";

        var facts = StoryMemoryDigestParser.Parse(json);

        Assert.Equal(StoryFactMerger.MaxFacts, facts.Count);
        Assert.All(facts, fact => Assert.Equal("promise", fact.Kind));
    }
}
