namespace LivingMetalGhost.AppCore.Desktop;

public enum ProactivePresenceAction
{
    None,
    StartDailyConversation,
    StartStoryIdle
}

public sealed class ProactivePresenceScheduler
{
    private static readonly TimeSpan StoryIdleFirstDelay =
        TimeSpan.FromMinutes(3);
    private static readonly TimeSpan StoryIdleRepeatDelay =
        TimeSpan.FromMinutes(10);
    private const int StoryIdleMaxBeatsPerHour = 3;

    private readonly Func<int, int, int> _nextRandom;
    private DateTimeOffset? _nextDailyConversationAt;
    private DateTimeOffset? _nextStoryIdleAt;
    private DateTimeOffset? _storyIdleHourStart;
    private int _storyIdleBeatsThisHour;

    public ProactivePresenceScheduler()
        : this(Random.Shared.Next)
    {
    }

    internal ProactivePresenceScheduler(Func<int, int, int> nextRandom)
    {
        _nextRandom = nextRandom;
    }

    public void ResetDailySchedule()
    {
        _nextDailyConversationAt = null;
    }

    public ProactivePresenceAction Tick(
        bool enabled,
        bool advancedMode,
        bool storyMode,
        int minMinutes,
        int maxMinutes,
        DateTimeOffset now)
    {
        if (!enabled)
        {
            ResetAll();
            return ProactivePresenceAction.None;
        }

        if (advancedMode)
        {
            return ProactivePresenceAction.None;
        }

        if (storyMode)
        {
            _nextDailyConversationAt = null;
            return TickStoryIdle(now);
        }

        ResetStorySchedule();
        if (_nextDailyConversationAt is null)
        {
            ScheduleNextDaily(minMinutes, maxMinutes, now);
            return ProactivePresenceAction.None;
        }

        if (now < _nextDailyConversationAt.Value)
        {
            return ProactivePresenceAction.None;
        }

        ScheduleNextDaily(minMinutes, maxMinutes, now);
        return ProactivePresenceAction.StartDailyConversation;
    }

    private ProactivePresenceAction TickStoryIdle(DateTimeOffset now)
    {
        if (_storyIdleHourStart is null ||
            now - _storyIdleHourStart.Value >= TimeSpan.FromHours(1))
        {
            _storyIdleHourStart = now;
            _storyIdleBeatsThisHour = 0;
        }

        if (_nextStoryIdleAt is null)
        {
            _nextStoryIdleAt = now.Add(StoryIdleFirstDelay);
            return ProactivePresenceAction.None;
        }

        if (now < _nextStoryIdleAt.Value)
        {
            return ProactivePresenceAction.None;
        }

        if (_storyIdleBeatsThisHour >= StoryIdleMaxBeatsPerHour)
        {
            _nextStoryIdleAt = _storyIdleHourStart.Value.AddHours(1);
            return ProactivePresenceAction.None;
        }

        _storyIdleBeatsThisHour++;
        _nextStoryIdleAt = now.Add(StoryIdleRepeatDelay);
        return ProactivePresenceAction.StartStoryIdle;
    }

    private void ScheduleNextDaily(
        int minMinutes,
        int maxMinutes,
        DateTimeOffset now)
    {
        var delayMinutes = _nextRandom(minMinutes, maxMinutes + 1);
        _nextDailyConversationAt = now.AddMinutes(delayMinutes);
    }

    private void ResetAll()
    {
        _nextDailyConversationAt = null;
        ResetStorySchedule();
    }

    private void ResetStorySchedule()
    {
        _nextStoryIdleAt = null;
        _storyIdleHourStart = null;
        _storyIdleBeatsThisHour = 0;
    }
}
