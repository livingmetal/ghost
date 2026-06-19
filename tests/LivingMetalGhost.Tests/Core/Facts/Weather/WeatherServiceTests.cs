using LivingMetalGhost.Core.Facts.Weather;
using System.Net;
using System.Text;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Facts.Weather;

public sealed class WeatherServiceTests
{
    [Theory]
    [InlineData(0, "맑음")]
    [InlineData(3, "흐림")]
    [InlineData(63, "비")]
    [InlineData(73, "눈")]
    [InlineData(95, "뇌우")]
    public void DescribeWeatherCode_UsesWmoDescriptions(
        int code,
        string expected)
    {
        Assert.Equal(expected, WeatherService.DescribeWeatherCode(code));
    }

    [Theory]
    [InlineData("대전", "Daejeon")]
    [InlineData("서울", "Seoul")]
    [InlineData("부산광역시", "Busan")]
    [InlineData("Tokyo", "Tokyo")]
    public void NormalizeGeocodingQuery_MapsCommonKoreanLocations(
        string location,
        string expected)
    {
        Assert.Equal(
            expected,
            WeatherService.NormalizeGeocodingQuery(location));
    }

    [Fact]
    public async Task GetTomorrowWeatherTextAsync_UsesSecondDailyForecast()
    {
        var handler = new SequenceHttpMessageHandler(
            """
            {
              "results": [{
                "name": "서울",
                "admin1": "서울특별시",
                "country": "대한민국",
                "country_code": "KR",
                "latitude": 37.566,
                "longitude": 126.978,
                "population": 9000000
              }]
            }
            """,
            """
            {
              "daily": {
                "time": ["2026-06-19", "2026-06-20"],
                "weather_code": [3, 63],
                "temperature_2m_max": [25.0, 22.8],
                "temperature_2m_min": [20.0, 19.3],
                "precipitation_probability_max": [40, 100]
              }
            }
            """);
        var service = new WeatherService(new HttpClient(handler));

        var result = await service.GetTomorrowWeatherTextAsync(
            "서울",
            CancellationToken.None);

        Assert.Contains("내일(2026-06-20)", result);
        Assert.Contains("비", result);
        Assert.Contains("최저 19.3°C", result);
        Assert.Contains("최고 22.8°C", result);
        Assert.Contains("최대 강수 확률 100%", result);
        Assert.Contains(
            "daily=weather_code",
            handler.RequestUris[1].Query);
    }

    private sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public SequenceHttpMessageHandler(params string[] responses)
        {
            _responses = new Queue<string>(responses);
        }

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responses.Dequeue(),
                    Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
