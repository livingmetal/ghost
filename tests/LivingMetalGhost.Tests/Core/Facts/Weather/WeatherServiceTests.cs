using LivingMetalGhost.Core.Facts.Weather;
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
}
