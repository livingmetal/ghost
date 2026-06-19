namespace LivingMetalGhost.AppCore.SlashAgents;

public static class KoreaTime
{
    public static DateTimeOffset GetNow()
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9));
        }
        catch (InvalidTimeZoneException)
        {
            return DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9));
        }
    }
}
