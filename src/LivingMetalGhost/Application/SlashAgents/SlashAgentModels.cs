namespace LivingMetalGhost.AppCore.SlashAgents;

public static class SlashCapabilities
{
    public const string Time = "time";
    public const string Date = "date";
    public const string Meal = "meal";
    public const string Weather = "weather";
    public const string Reminder = "reminder";
    public const string Help = "help";
    public const string Unknown = "unknown";
}

public static class WeatherForecastDays
{
    public const string Current = "current";
    public const string Tomorrow = "tomorrow";
}

public sealed record SlashIntentPlan(
    string Capability,
    string OriginalText,
    string Location = "",
    string MealSlot = "",
    bool UsedLlm = false,
    string WeatherDay = WeatherForecastDays.Current);

public sealed record SlashCapabilityResult(
    string Capability,
    string Facts,
    bool Success = true);

public interface ISlashCapabilityHandler
{
    string Capability { get; }

    Task<SlashCapabilityResult> ExecuteAsync(
        SlashIntentPlan plan,
        CancellationToken cancellationToken);
}
