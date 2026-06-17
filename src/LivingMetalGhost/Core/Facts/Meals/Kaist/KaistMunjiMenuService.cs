using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using LivingMetalGhost.Core.Facts;
using LivingMetalGhost.Core.Facts.Meals;

namespace LivingMetalGhost.Core.Facts.Meals.Kaist;

public sealed class KaistMunjiMenuService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly FactStore _factStore;
    private readonly KaistMenuParser _parser;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public KaistMunjiMenuService(FactStore factStore, KaistMenuParser parser)
    {
        _factStore = factStore;
        _parser = parser;
    }

    public async Task<string> GetTodayMenuTextAsync(string requestText, CancellationToken ct)
    {
        var now = GetKoreaNow();
        var today = DateOnly.FromDateTime(now.DateTime);
        var requestedSlot = ParseRequestedSlot(requestText);
        var factKey = BuildFactKey(today);
        var cached = await _factStore.TryGetLatestAsync(factKey, ct);

        if (cached is not null && _factStore.IsFresh(cached, now))
        {
            var cachedDocument = Deserialize(cached.PayloadJson);
            if (cachedDocument is not null)
            {
                return BuildAnswer(cachedDocument, requestedSlot, "저장된 최신 식단 기준이야.");
            }
        }

        try
        {
            var sourceUrl = BuildSourceUrl(today);
            var html = await FetchAsync(sourceUrl, ct);
            var document = _parser.Parse(html, today, now, sourceUrl);
            await _factStore.UpsertAsync(new FactEntry(
                Key: factKey,
                Category: "meal",
                Source: "KAIST",
                SourceUrl: sourceUrl,
                ObservedAt: now,
                ValidUntil: new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue).AddDays(1).AddHours(2), now.Offset),
                PayloadJson: JsonSerializer.Serialize(document, _jsonOptions),
                ParserId: _parser.ParserId,
                SchemaVersion: 1), ct);

            return BuildAnswer(document, requestedSlot, "방금 KAIST 페이지에서 확인했어.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
        {
            if (cached is not null)
            {
                var cachedDocument = Deserialize(cached.PayloadJson);
                if (cachedDocument is not null)
                {
                    return BuildAnswer(cachedDocument, requestedSlot, $"새로 확인하진 못해서 마지막 저장본을 보여줄게. 원인: {ex.Message}");
                }
            }

            return $"KAIST 문지캠퍼스 식단을 아직 가져오지 못했어. 원인: {ex.Message}";
        }
    }

    private static async Task<string> FetchAsync(string sourceUrl, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("LivingMetalGhost", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        using var response = await HttpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private KaistMenuDocument? Deserialize(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<KaistMenuDocument>(payloadJson, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildAnswer(KaistMenuDocument document, MealSlot? requestedSlot, string prefix)
    {
        var dateText = document.MenuDate.ToString("yyyy-MM-dd");
        if (requestedSlot is not null)
        {
            var meal = GetMeal(document, requestedSlot.Value);
            return meal is null
                ? $"{prefix} {dateText} 문지캠퍼스 {ToLabel(requestedSlot.Value)} 메뉴는 찾지 못했어."
                : $"{prefix} {dateText} 문지캠퍼스 {FormatMeal(meal)}";
        }

        var meals = new[] { document.Breakfast, document.Lunch, document.Dinner }
            .Where(meal => meal is not null)
            .Select(meal => FormatMeal(meal!));
        return $"{prefix} {dateText} 문지캠퍼스 식단이야. " + string.Join(" / ", meals);
    }

    private static string FormatMeal(MealMenu meal)
    {
        var items = string.Join(", ", meal.Items.Select(item => item.Name));
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(meal.ServiceTime))
        {
            details.Add(meal.ServiceTime!);
        }

        if (meal.PriceWon is not null)
        {
            details.Add($"{meal.PriceWon:N0}원");
        }

        if (meal.Calories is not null)
        {
            details.Add($"{meal.Calories:N0}kcal");
        }

        var suffix = details.Count > 0 ? $" ({string.Join(", ", details)})" : string.Empty;
        return $"{meal.Label}: {items}{suffix}";
    }

    private static MealMenu? GetMeal(KaistMenuDocument document, MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => document.Breakfast,
        MealSlot.Lunch => document.Lunch,
        MealSlot.Dinner => document.Dinner,
        _ => null
    };

    private static MealSlot? ParseRequestedSlot(string text)
    {
        if (ContainsAny(text, "조식", "아침"))
        {
            return MealSlot.Breakfast;
        }

        if (ContainsAny(text, "중식", "점심", "런치"))
        {
            return MealSlot.Lunch;
        }

        if (ContainsAny(text, "석식", "저녁", "디너"))
        {
            return MealSlot.Dinner;
        }

        return null;
    }

    private static bool ContainsAny(string text, params string[] words)
    {
        return words.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToLabel(MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => "조식",
        MealSlot.Lunch => "중식",
        MealSlot.Dinner => "석식",
        _ => "식사"
    };

    private static string BuildFactKey(DateOnly date)
    {
        return $"kaist.menu.munji.{date:yyyy-MM-dd}";
    }

    private static string BuildSourceUrl(DateOnly date)
    {
        return $"https://www.kaist.ac.kr/kr/html/campus/053001.html?dvs_cd=icc&stt_dt={date:yyyy-MM-dd}";
    }

    private static DateTimeOffset GetKoreaNow()
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
