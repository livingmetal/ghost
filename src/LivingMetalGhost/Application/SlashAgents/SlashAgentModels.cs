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

public sealed record SlashIntentPlan(
    string Capability,
    string OriginalText,
    string Location = "",
    string MealSlot = "",
    bool UsedLlm = false);

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
