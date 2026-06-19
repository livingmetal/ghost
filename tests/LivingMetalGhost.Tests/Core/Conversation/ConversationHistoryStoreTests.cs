using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Models;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Conversation;

public sealed class ConversationHistoryStoreTests
{
    [Fact]
    public void DailyAndAdvanced_ReadAndWriteTheSameHistory()
    {
        var store = new ConversationHistoryStore();

        store.Add(ConversationMode.Daily, "user", "daily question");
        store.Add(ConversationMode.Advanced, "assistant", "advanced answer");

        var daily = store.GetSnapshot(ConversationMode.Daily);
        var advanced = store.GetSnapshot(ConversationMode.Advanced);

        Assert.Equal(2, daily.Count);
        Assert.Equal(
            daily.Select(message => (message.Role, message.Content)),
            advanced.Select(message => (message.Role, message.Content)));
    }

    [Fact]
    public void RoleplayHistory_IsIsolatedFromCompanionHistory()
    {
        var store = new ConversationHistoryStore();

        store.Add(ConversationMode.Daily, "user", "real conversation");
        store.Add(ConversationMode.Story, "user", "fictional conversation");

        var companion = Assert.Single(store.GetSnapshot(ConversationMode.Advanced));
        var roleplay = Assert.Single(store.GetSnapshot(ConversationMode.Story));

        Assert.Equal("real conversation", companion.Content);
        Assert.Equal("fictional conversation", roleplay.Content);
    }

    [Fact]
    public void Snapshot_IsDetachedFromStoredMessages()
    {
        var store = new ConversationHistoryStore();
        store.Add(ConversationMode.Daily, "assistant", "original");

        var snapshot = store.GetSnapshot(ConversationMode.Daily);
        snapshot[0].Content = "changed";

        Assert.Equal(
            "original",
            Assert.Single(store.GetSnapshot(ConversationMode.Advanced)).Content);
    }

    [Fact]
    public void History_IsTrimmedToMaximumMessageCount()
    {
        var store = new ConversationHistoryStore();

        for (var index = 0; index < 25; index++)
        {
            store.Add(ConversationMode.Daily, "user", $"message-{index}");
        }

        var snapshot = store.GetSnapshot(ConversationMode.Advanced);

        Assert.Equal(20, snapshot.Count);
        Assert.Equal("message-5", snapshot[0].Content);
        Assert.Equal("message-24", snapshot[^1].Content);
    }

    [Fact]
    public void CountByRole_UsesTheResolvedHistoryChannel()
    {
        var store = new ConversationHistoryStore();
        store.Add(ConversationMode.Daily, "assistant", "one");
        store.Add(ConversationMode.Advanced, "assistant", "two");
        store.Add(ConversationMode.Story, "assistant", "story");

        Assert.Equal(2, store.CountByRole(ConversationMode.Daily, "assistant"));
        Assert.Equal(2, store.CountByRole(ConversationMode.Advanced, "assistant"));
        Assert.Equal(1, store.CountByRole(ConversationMode.Story, "assistant"));
    }
}
