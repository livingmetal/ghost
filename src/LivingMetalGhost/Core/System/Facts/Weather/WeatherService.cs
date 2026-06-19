using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace LivingMetalGhost.Core.Facts.Weather;

public sealed class WeatherService
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly HttpClient _httpClient;

    public WeatherService()
        : this(SharedHttpClient)
    {
    }

    public WeatherService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetCurrentWeatherTextAsync(
        string location,
        CancellationToken cancellationToken)
    {
        var normalizedLocation = string.IsNullOrWhiteSpace(location)
            ? "대전"
            : location.Trim();

        try
        {
            var place = await GeocodeAsync(
                normalizedLocation,
                cancellationToken);
            if (place is null)
            {
                return $"{normalizedLocation} 위치를 찾지 못했어.";
            }

            var current = await FetchCurrentWeatherAsync(
                place,
                cancellationToken);
            if (current is null)
            {
                return $"{place.DisplayName}의 현재 날씨 값을 불러오지 못했어.";
            }

            var weather = DescribeWeatherCode(current.WeatherCode);
            return
                $"Open-Meteo 기준 {place.DisplayName} 현재 날씨는 {weather}, " +
                $"기온 {current.Temperature:0.#}°C, 체감 {current.ApparentTemperature:0.#}°C, " +
                $"습도 {current.RelativeHumidity:0.#}%, 강수량 {current.Precipitation:0.#}mm, " +
                $"풍속 {current.WindSpeed:0.#}km/h야. 관측 시각: {current.Time}.";
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            TaskCanceledException or
            JsonException or
            InvalidOperationException)
        {
            return $"{normalizedLocation} 날씨를 불러오지 못했어. 원인: {ex.Message}";
        }
    }

    public async Task<string> GetTomorrowWeatherTextAsync(
        string location,
        CancellationToken cancellationToken)
    {
        var normalizedLocation = string.IsNullOrWhiteSpace(location)
            ? "대전"
            : location.Trim();

        try
        {
            var place = await GeocodeAsync(
                normalizedLocation,
                cancellationToken);
            if (place is null)
            {
                return $"{normalizedLocation} 위치를 찾지 못했어.";
            }

            var forecast = await FetchTomorrowWeatherAsync(
                place,
                cancellationToken);
            if (forecast is null)
            {
                return $"{place.DisplayName}의 내일 날씨 예보를 불러오지 못했어.";
            }

            var weather = DescribeWeatherCode(forecast.WeatherCode);
            return
                $"Open-Meteo 기준 {place.DisplayName} 내일({forecast.Date}) 날씨는 {weather}, " +
                $"최저 {forecast.MinimumTemperature:0.#}°C, 최고 {forecast.MaximumTemperature:0.#}°C, " +
                $"최대 강수 확률 {forecast.PrecipitationProbability:0.#}%야.";
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            TaskCanceledException or
            JsonException or
            InvalidOperationException)
        {
            return $"{normalizedLocation} 내일 날씨를 불러오지 못했어. 원인: {ex.Message}";
        }
    }

    public static string DescribeWeatherCode(int code) => code switch
    {
        0 => "맑음",
        1 => "대체로 맑음",
        2 => "부분적으로 흐림",
        3 => "흐림",
        45 or 48 => "안개",
        51 or 53 or 55 => "이슬비",
        56 or 57 => "어는 이슬비",
        61 or 63 or 65 => "비",
        66 or 67 => "어는 비",
        71 or 73 or 75 => "눈",
        77 => "싸락눈",
        80 or 81 or 82 => "소나기",
        85 or 86 => "눈 소나기",
        95 => "뇌우",
        96 or 99 => "우박을 동반한 뇌우",
        _ => $"알 수 없는 상태(WMO {code})"
    };

    public static string NormalizeGeocodingQuery(string location)
    {
        var normalized = location.Trim();
        return KoreanLocationAliases.TryGetValue(normalized, out var alias)
            ? alias
            : normalized;
    }

    private async Task<GeocodedPlace?> GeocodeAsync(
        string location,
        CancellationToken cancellationToken)
    {
        var query = NormalizeGeocodingQuery(location);
        var url =
            "https://geocoding-api.open-meteo.com/v1/search" +
            $"?name={Uri.EscapeDataString(query)}&count=10&language=ko&format=json";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
        {
            return null;
        }

        var result = results
            .EnumerateArray()
            .OrderByDescending(item =>
                string.Equals(
                    GetString(item, "country_code"),
                    "KR",
                    StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => GetDouble(item, "population"))
            .First();
        var name = GetString(result, "name");
        var admin = GetString(result, "admin1");
        var country = GetString(result, "country");
        var displayParts = new[] { name, admin, country }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return new GeocodedPlace(
            string.Join(", ", displayParts),
            result.GetProperty("latitude").GetDouble(),
            result.GetProperty("longitude").GetDouble());
    }

    private async Task<CurrentWeather?> FetchCurrentWeatherAsync(
        GeocodedPlace place,
        CancellationToken cancellationToken)
    {
        var latitude = place.Latitude.ToString(
            CultureInfo.InvariantCulture);
        var longitude = place.Longitude.ToString(
            CultureInfo.InvariantCulture);
        var url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={latitude}&longitude={longitude}" +
            "&current=temperature_2m,apparent_temperature,relative_humidity_2m,precipitation,weather_code,wind_speed_10m" +
            "&timezone=auto";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("current", out var current))
        {
            return null;
        }

        return new CurrentWeather(
            GetString(current, "time"),
            GetDouble(current, "temperature_2m"),
            GetDouble(current, "apparent_temperature"),
            GetDouble(current, "relative_humidity_2m"),
            GetDouble(current, "precipitation"),
            current.GetProperty("weather_code").GetInt32(),
            GetDouble(current, "wind_speed_10m"));
    }

    private async Task<DailyWeather?> FetchTomorrowWeatherAsync(
        GeocodedPlace place,
        CancellationToken cancellationToken)
    {
        var latitude = place.Latitude.ToString(
            CultureInfo.InvariantCulture);
        var longitude = place.Longitude.ToString(
            CultureInfo.InvariantCulture);
        var url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={latitude}&longitude={longitude}" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
            "&forecast_days=2&timezone=auto";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("daily", out var daily) ||
            !TryGetArrayValue(daily, "time", 1, out var date) ||
            !TryGetArrayValue(daily, "weather_code", 1, out var weatherCode) ||
            !TryGetArrayValue(daily, "temperature_2m_max", 1, out var maximum) ||
            !TryGetArrayValue(daily, "temperature_2m_min", 1, out var minimum) ||
            !TryGetArrayValue(
                daily,
                "precipitation_probability_max",
                1,
                out var precipitationProbability))
        {
            return null;
        }

        return new DailyWeather(
            date.GetString() ?? string.Empty,
            weatherCode.GetInt32(),
            maximum.GetDouble(),
            minimum.GetDouble(),
            precipitationProbability.GetDouble());
    }

    private static bool TryGetArrayValue(
        JsonElement element,
        string propertyName,
        int index,
        out JsonElement value)
    {
        value = default;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array ||
            property.GetArrayLength() <= index)
        {
            return false;
        }

        value = property[index];
        return value.ValueKind is JsonValueKind.String or JsonValueKind.Number;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static double GetDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number
            ? property.GetDouble()
            : 0;
    }

    private sealed record GeocodedPlace(
        string DisplayName,
        double Latitude,
        double Longitude);

    private sealed record CurrentWeather(
        string Time,
        double Temperature,
        double ApparentTemperature,
        double RelativeHumidity,
        double Precipitation,
        int WeatherCode,
        double WindSpeed);

    private sealed record DailyWeather(
        string Date,
        int WeatherCode,
        double MaximumTemperature,
        double MinimumTemperature,
        double PrecipitationProbability);

    private static readonly IReadOnlyDictionary<string, string> KoreanLocationAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["서울"] = "Seoul",
            ["서울특별시"] = "Seoul",
            ["부산"] = "Busan",
            ["부산광역시"] = "Busan",
            ["대구"] = "Daegu",
            ["대구광역시"] = "Daegu",
            ["인천"] = "Incheon",
            ["인천광역시"] = "Incheon",
            ["광주"] = "Gwangju",
            ["광주광역시"] = "Gwangju",
            ["대전"] = "Daejeon",
            ["대전광역시"] = "Daejeon",
            ["울산"] = "Ulsan",
            ["울산광역시"] = "Ulsan",
            ["세종"] = "Sejong",
            ["세종특별자치시"] = "Sejong",
            ["제주"] = "Jeju City",
            ["제주시"] = "Jeju City",
            ["수원"] = "Suwon",
            ["수원시"] = "Suwon",
            ["성남"] = "Seongnam",
            ["성남시"] = "Seongnam",
            ["춘천"] = "Chuncheon",
            ["강릉"] = "Gangneung",
            ["전주"] = "Jeonju",
            ["청주"] = "Cheongju",
            ["포항"] = "Pohang"
        };
}
