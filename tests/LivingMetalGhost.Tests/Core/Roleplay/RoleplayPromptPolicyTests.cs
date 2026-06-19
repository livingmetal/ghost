using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Roleplay;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Roleplay;

public sealed class RoleplayPromptPolicyTests
{
    [Fact]
    public void Build_IncludesRoleplayIsolationAndPlayerAgencyRules()
    {
        var prompt = RoleplayPromptPolicy.Build(
            new StoryState(),
            "Orkia",
            ["idle", "thinking"]);

        Assert.Contains("Never mix roleplaying facts with real project", prompt);
        Assert.Contains("The user controls their own character", prompt);
        Assert.Contains("Available story sprite moods are: idle, thinking.", prompt);
    }

    [Fact]
    public void Build_UsesFallbackSceneAndSummaryWhenStateIsEmpty()
    {
        var prompt = RoleplayPromptPolicy.Build(
            new StoryState { Scene = " ", Summary = " " },
            "Orkia",
            []);

        Assert.Contains("늦은 밤의 폐쇄망 데이터센터", prompt);
        Assert.Contains("정체불명의 세션이 깨어났고", prompt);
    }

    [Fact]
    public void Build_IncludesFactsByDescendingWeight()
    {
        var state = new StoryState
        {
            Facts =
            [
                new StoryMemoryFact { Kind = "question", Text = "low", Weight = 1 },
                new StoryMemoryFact { Kind = "relationship", Text = "high", Weight = 5 }
            ]
        };

        var prompt = RoleplayPromptPolicy.Build(state, "Orkia", ["idle"]);

        Assert.True(
            prompt.IndexOf("- (relationship) high", StringComparison.Ordinal) <
            prompt.IndexOf("- (question) low", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_OmitsMemoryBlockWhenNoFactsExist()
    {
        var prompt = RoleplayPromptPolicy.Build(
            new StoryState(),
            "Orkia",
            ["idle"]);

        Assert.DoesNotContain("carries into this scene", prompt);
    }
}
