using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Models;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Conversation;

public sealed class ExternalConversationTurnRecorderTests
{
    [Fact]
    public void Record_AddsNormalizedUserAndAssistantMessages()
    {
        var history = new ConversationHistoryStore();
        var recorder = new ExternalConversationTurnRecorder(history);

        recorder.Record(
            ConversationMode.Advanced,
            "  inspect repository  ",
            "  completed  ",
            "  codex  ");

        var snapshot = history.GetSnapshot(ConversationMode.Daily);
        Assert.Collection(
            snapshot,
            message =>
            {
                Assert.Equal("user", message.Role);
                Assert.Equal("inspect repository", message.Content);
            },
            message =>
            {
                Assert.Equal("assistant", message.Role);
                Assert.Equal(
                    $"[codex result]{Environment.NewLine}completed",
                    message.Content);
            });
    }

    [Fact]
    public void Record_UsesFallbackSourceAndEmptyResultMarker()
    {
        var history = new ConversationHistoryStore();
        var recorder = new ExternalConversationTurnRecorder(history);

        recorder.Record(
            ConversationMode.Daily,
            "request",
            string.Empty,
            string.Empty);

        var assistant = Assert.Single(
            history.GetSnapshot(ConversationMode.Daily),
            message => message.Role == "assistant");
        Assert.Equal(
            $"[external result]{Environment.NewLine}(no text returned)",
            assistant.Content);
    }

    [Fact]
    public void Record_IgnoresCompletelyEmptyTurn()
    {
        var history = new ConversationHistoryStore();
        var recorder = new ExternalConversationTurnRecorder(history);

        recorder.Record(
            ConversationMode.Daily,
            string.Empty,
            string.Empty,
            "tool");

        Assert.Empty(history.GetSnapshot(ConversationMode.Daily));
    }
}
