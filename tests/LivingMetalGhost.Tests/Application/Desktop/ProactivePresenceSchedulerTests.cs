using LivingMetalGhost.AppCore.Desktop;
using Xunit;

namespace LivingMetalGhost.Tests.Application.Desktop;

public sealed class ProactivePresenceSchedulerTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 6, 19, 12, 0, 0, TimeSpan.FromHours(9));

    [Fact]
    public void DailyConversation_RunsAfterScheduledDelay()
    {
        var scheduler = new ProactivePresenceScheduler((min, max) => min);

        Assert.Equal(
            ProactivePresenceAction.None,
            scheduler.Tick(true, false, false, 20, 45, Start));
        Assert.Equal(
            ProactivePresenceAction.None,
            scheduler.Tick(true, false, false, 20, 45, Start.AddMinutes(19)));
        Assert.Equal(
            ProactivePresenceAction.StartDailyConversation,
            scheduler.Tick(true, false, false, 20, 45, Start.AddMinutes(20)));
    }

    [Fact]
    public void StoryIdle_UsesFirstAndRepeatDelays()
    {
        var scheduler = new ProactivePresenceScheduler((min, max) => min);

        Assert.Equal(
            ProactivePresenceAction.None,
            scheduler.Tick(true, false, true, 20, 45, Start));
        Assert.Equal(
            ProactivePresenceAction.StartStoryIdle,
            scheduler.Tick(true, false, true, 20, 45, Start.AddMinutes(3)));
        Assert.Equal(
            ProactivePresenceAction.None,
            scheduler.Tick(true, false, true, 20, 45, Start.AddMinutes(12)));
        Assert.Equal(
            ProactivePresenceAction.StartStoryIdle,
            scheduler.Tick(true, false, true, 20, 45, Start.AddMinutes(13)));
    }

    [Fact]
    public void StoryIdle_IsLimitedToThreeBeatsPerHour()
    {
        var scheduler = new ProactivePresenceScheduler((min, max) => min);
        scheduler.Tick(true, false, true, 20, 45, Start);

        Assert.Equal(
            ProactivePresenceAction.StartStoryIdle,
            scheduler.Tick(true, false, true, 20, 45, Start.AddMinutes(3)));
        Assert.Equal(
            ProactivePresenceAction.StartStoryIdle,
            scheduler.Tick(true, false, true, 20, 45, Start.AddMinutes(13)));
        Assert.Equal(
            ProactivePresenceAction.StartStoryIdle,
            scheduler.Tick(true, false, true, 20, 45, Start.AddMinutes(23)));
        Assert.Equal(
            ProactivePresenceAction.None,
            scheduler.Tick(true, false, true, 20, 45, Start.AddMinutes(33)));
        Assert.Equal(
            ProactivePresenceAction.StartStoryIdle,
            scheduler.Tick(true, false, true, 20, 45, Start.AddHours(1)));
    }

    [Fact]
    public void DisabledMode_ClearsExistingSchedules()
    {
        var scheduler = new ProactivePresenceScheduler((min, max) => min);
        scheduler.Tick(true, false, false, 20, 45, Start);
        scheduler.Tick(false, false, false, 20, 45, Start.AddMinutes(10));

        Assert.Equal(
            ProactivePresenceAction.None,
            scheduler.Tick(true, false, false, 20, 45, Start.AddMinutes(20)));
    }

    [Fact]
    public void AdvancedMode_PausesWithoutConsumingSchedule()
    {
        var scheduler = new ProactivePresenceScheduler((min, max) => min);
        scheduler.Tick(true, false, false, 20, 45, Start);

        Assert.Equal(
            ProactivePresenceAction.None,
            scheduler.Tick(true, true, false, 20, 45, Start.AddMinutes(30)));
        Assert.Equal(
            ProactivePresenceAction.StartDailyConversation,
            scheduler.Tick(true, false, false, 20, 45, Start.AddMinutes(31)));
    }
}
