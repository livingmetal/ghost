using LivingMetalGhost.Core.Facts.Weather;

namespace LivingMetalGhost.AppCore.SlashAgents.Capabilities;

public sealed class WeatherCapabilityHandler : ISlashCapabilityHandler
{
    private readonly WeatherService _weatherService;

    public WeatherCapabilityHandler(WeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    public string Capability => SlashCapabilities.Weather;

    public async Task<SlashCapabilityResult> ExecuteAsync(
        SlashIntentPlan plan,
        CancellationToken cancellationToken)
    {
        var location = string.IsNullOrWhiteSpace(plan.Location)
            ? "대전"
            : plan.Location;
        var facts = plan.WeatherDay == WeatherForecastDays.Tomorrow
            ? await _weatherService.GetTomorrowWeatherTextAsync(
                location,
                cancellationToken)
            : await _weatherService.GetCurrentWeatherTextAsync(
                location,
                cancellationToken);
        return new SlashCapabilityResult(Capability, facts);
    }
}
