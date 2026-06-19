using System.Text.Json;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.AppCore.SlashAgents;

public sealed class SlashIntentPlanner
{
    private readonly AppConfigLoader _configLoader;
    private readonly ILlmProviderFactory _providerFactory;

    public SlashIntentPlanner(
        AppConfigLoader configLoader,
        ILlmProviderFactory providerFactory)
    {
        _configLoader = configLoader;
        _providerFactory = providerFactory;
    }

    public async Task<SlashIntentPlan> PlanAsync(
        string intentText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(intentText))
        {
            return new SlashIntentPlan(SlashCapabilities.Help, intentText);
        }

        if (TryBuildDirectWeatherPlan(intentText, out var weatherPlan))
        {
            return weatherPlan;
        }

        var config = _configLoader.Load();
        var options = LlmOptions.FromSettings(config.Llm);
        try
        {
            var provider = _providerFactory.Create(options.Provider);
            var response = await provider.GenerateAsync(new LlmRequest
            {
                UserText = intentText,
                UserTitle = config.App.UserTitle,
                Model = options.Model,
                Options = options,
                SystemPrompt = BuildPlannerPrompt()
            }, cancellationToken);

            if (!response.FromFallback &&
                TryParsePlan(response.Text, intentText, out var plan))
            {
                return plan with { UsedLlm = true };
            }
        }
        catch
        {
            // Explicit slash capabilities should remain available if planning fails.
        }

        return BuildFallbackPlan(intentText);
    }

    public static bool TryBuildDirectWeatherPlan(
        string text,
        out SlashIntentPlan plan)
    {
        var normalized = text.Trim();
        if (!ContainsAny(normalized, "날씨", "기온", "weather"))
        {
            plan = new SlashIntentPlan(SlashCapabilities.Unknown, normalized);
            return false;
        }

        plan = new SlashIntentPlan(
            SlashCapabilities.Weather,
            normalized,
            ExtractWeatherLocation(normalized));
        return true;
    }

    public static bool TryParsePlan(
        string responseText,
        string originalText,
        out SlashIntentPlan plan)
    {
        plan = new SlashIntentPlan(SlashCapabilities.Unknown, originalText);
        var json = ExtractJsonObject(responseText);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var capability = NormalizeCapability(
                GetString(root, "capability"));
            var location = GetString(root, "location");
            var mealSlot = NormalizeMealSlot(GetString(root, "meal_slot"));

            plan = new SlashIntentPlan(
                capability,
                originalText,
                location,
                mealSlot);
            return capability != SlashCapabilities.Unknown;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static SlashIntentPlan BuildFallbackPlan(string text)
    {
        var normalized = text.Trim();
        if (ContainsAny(normalized, "도움", "도움말", "help", "기능"))
        {
            return new SlashIntentPlan(SlashCapabilities.Help, normalized);
        }

        if (ContainsAny(normalized, "날씨", "기온", "비", "weather"))
        {
            return new SlashIntentPlan(
                SlashCapabilities.Weather,
                normalized,
                ExtractWeatherLocation(normalized));
        }

        if (ContainsAny(
                normalized,
                "문지",
                "문지캠퍼스",
                "식단",
                "학식",
                "아침",
                "점심",
                "저녁"))
        {
            return new SlashIntentPlan(
                SlashCapabilities.Meal,
                normalized,
                MealSlot: InferMealSlot(normalized));
        }

        if (ContainsAny(normalized, "날짜", "오늘", "요일", "date"))
        {
            return new SlashIntentPlan(SlashCapabilities.Date, normalized);
        }

        if (ContainsAny(normalized, "시간", "몇시", "지금", "time"))
        {
            return new SlashIntentPlan(SlashCapabilities.Time, normalized);
        }

        if (ContainsAny(
                normalized,
                "알림",
                "리마인더",
                "타이머",
                "초 뒤",
                "분 뒤",
                "시간 뒤"))
        {
            return new SlashIntentPlan(SlashCapabilities.Reminder, normalized);
        }

        return new SlashIntentPlan(SlashCapabilities.Unknown, normalized);
    }

    private static string BuildPlannerPrompt()
    {
        var now = KoreaTime.GetNow();
        return $$"""
            You route an explicit slash request to exactly one safe capability.
            Current Korea date and time: {{now:yyyy-MM-dd HH:mm:ss zzz}}.

            Allowed capabilities:
            - time: current Korea time
            - date: current Korea date and weekday
            - meal: today's KAIST Munji campus menu
            - weather: current weather for a named location
            - reminder: create a relative timer/reminder
            - help: explain available slash capabilities
            - unknown: none of the allowed capabilities

            Return one JSON object only:
            {"capability":"time|date|meal|weather|reminder|help|unknown","location":"","meal_slot":"breakfast|lunch|dinner|all"}

            Rules:
            - Never invent another capability.
            - Preserve a weather location from the user's text.
            - If weather has no location, use "대전".
            - Use meal_slot lunch for 점심, breakfast for 아침/조식, dinner for 저녁/석식, otherwise all.
            - Do not answer the request. Only return JSON.
            """;
    }

    private static string ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start
            ? text[start..(end + 1)]
            : string.Empty;
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim() ?? string.Empty
            : string.Empty;
    }

    private static string NormalizeCapability(string capability) =>
        capability.Trim().ToLowerInvariant() switch
        {
            SlashCapabilities.Time => SlashCapabilities.Time,
            SlashCapabilities.Date => SlashCapabilities.Date,
            SlashCapabilities.Meal => SlashCapabilities.Meal,
            SlashCapabilities.Weather => SlashCapabilities.Weather,
            SlashCapabilities.Reminder => SlashCapabilities.Reminder,
            SlashCapabilities.Help => SlashCapabilities.Help,
            _ => SlashCapabilities.Unknown
        };

    private static string NormalizeMealSlot(string mealSlot) =>
        mealSlot.Trim().ToLowerInvariant() switch
        {
            "breakfast" => "breakfast",
            "lunch" => "lunch",
            "dinner" => "dinner",
            _ => "all"
        };

    private static string InferMealSlot(string text)
    {
        if (ContainsAny(text, "아침", "조식"))
        {
            return "breakfast";
        }

        if (ContainsAny(text, "점심", "중식"))
        {
            return "lunch";
        }

        if (ContainsAny(text, "저녁", "석식"))
        {
            return "dinner";
        }

        return "all";
    }

    private static string ExtractWeatherLocation(string text)
    {
        var location = text
            .Replace("오늘", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("현재", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("지역", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("날씨", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("기온", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("확인해줘", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("확인", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("알려줘", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("어때", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("weather", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '?', '!', '.', ',', ':');

        location = TrimTrailingLocationParticle(location);
        return string.IsNullOrWhiteSpace(location) ? "대전" : location;
    }

    private static string TrimTrailingLocationParticle(string location)
    {
        var trimmed = location.Trim();
        foreach (var particle in new[] { "에서의", "에서", "의", "은", "는" })
        {
            if (trimmed.EndsWith(particle, StringComparison.Ordinal))
            {
                return trimmed[..^particle.Length].Trim();
            }
        }

        return trimmed;
    }

    private static bool ContainsAny(string text, params string[] words) =>
        words.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
}
