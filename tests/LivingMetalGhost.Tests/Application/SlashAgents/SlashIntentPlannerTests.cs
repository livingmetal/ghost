using LivingMetalGhost.AppCore.SlashAgents;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Providers.Llm;
using Xunit;

namespace LivingMetalGhost.Tests.Application.SlashAgents;

public sealed class SlashIntentPlannerTests
{
    [Theory]
    [InlineData("서울 날씨", "서울")]
    [InlineData("서울의 날씨 알려줘", "서울")]
    [InlineData("지역: 부산 날씨 확인해줘", "부산")]
    [InlineData("날씨", "대전")]
    public async Task PlanAsync_RoutesExplicitWeatherWithoutCallingLlm(
        string text,
        string expectedLocation)
    {
        var planner = new SlashIntentPlanner(
            null!,
            new ThrowingProviderFactory());

        var plan = await planner.PlanAsync(text, CancellationToken.None);

        Assert.Equal(SlashCapabilities.Weather, plan.Capability);
        Assert.Equal(expectedLocation, plan.Location);
        Assert.False(plan.UsedLlm);
    }

    [Theory]
    [InlineData("서울 날씨", WeatherForecastDays.Current, "서울")]
    [InlineData("내일 서울 날씨", WeatherForecastDays.Tomorrow, "서울")]
    [InlineData("Busan weather tomorrow", WeatherForecastDays.Tomorrow, "Busan")]
    public void TryBuildDirectWeatherPlan_ExtractsForecastDay(
        string text,
        string expectedDay,
        string expectedLocation)
    {
        var parsed = SlashIntentPlanner.TryBuildDirectWeatherPlan(
            text,
            out var plan);

        Assert.True(parsed);
        Assert.Equal(expectedDay, plan.WeatherDay);
        Assert.Equal(expectedLocation, plan.Location);
    }

    [Fact]
    public async Task PlanAsync_UsesLlmSelectedCapabilityAndArguments()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "LivingMetalGhost.Tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            var loader = new AppConfigLoader(paths);
            var config = loader.Load();
            config.Llm.Provider = "stub";
            loader.Save(config);
            var planner = new SlashIntentPlanner(
                loader,
                new StubProviderFactory(new StubProvider(
                    """{"capability":"weather","location":"부산","meal_slot":"all"}""")));

            var plan = await planner.PlanAsync(
                "부산 비 오는지 확인",
                CancellationToken.None);

            Assert.Equal(SlashCapabilities.Weather, plan.Capability);
            Assert.Equal("부산", plan.Location);
            Assert.True(plan.UsedLlm);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void TryParsePlan_ParsesWeatherLocationFromJson()
    {
        var parsed = SlashIntentPlanner.TryParsePlan(
            "```json\n" +
            "{\"capability\":\"weather\",\"location\":\"서울\",\"meal_slot\":\"all\",\"weather_day\":\"tomorrow\"}\n" +
            "```",
            "서울 날씨",
            out var plan);

        Assert.True(parsed);
        Assert.Equal(SlashCapabilities.Weather, plan.Capability);
        Assert.Equal("서울", plan.Location);
        Assert.Equal(WeatherForecastDays.Tomorrow, plan.WeatherDay);
    }

    [Theory]
    [InlineData("문지캠퍼스 점심 식사", SlashCapabilities.Meal, "lunch")]
    [InlineData("오늘 날짜", SlashCapabilities.Date, "")]
    [InlineData("지금 시간", SlashCapabilities.Time, "")]
    [InlineData("부산 날씨", SlashCapabilities.Weather, "")]
    public void BuildFallbackPlan_RecognizesCoreCapabilities(
        string text,
        string capability,
        string mealSlot)
    {
        var plan = SlashIntentPlanner.BuildFallbackPlan(text);

        Assert.Equal(capability, plan.Capability);
        if (!string.IsNullOrEmpty(mealSlot))
        {
            Assert.Equal(mealSlot, plan.MealSlot);
        }
    }

    [Fact]
    public void BuildFallbackPlan_UsesDaejeonWhenWeatherLocationIsMissing()
    {
        var plan = SlashIntentPlanner.BuildFallbackPlan("날씨");

        Assert.Equal(SlashCapabilities.Weather, plan.Capability);
        Assert.Equal("대전", plan.Location);
    }

    private sealed class StubProviderFactory : ILlmProviderFactory
    {
        private readonly ILlmProvider _provider;

        public StubProviderFactory(ILlmProvider provider)
        {
            _provider = provider;
        }

        public ILlmProvider Create(string providerName) => _provider;
    }

    private sealed class ThrowingProviderFactory : ILlmProviderFactory
    {
        public ILlmProvider Create(string providerName) =>
            throw new InvalidOperationException("The LLM should not be called.");
    }

    private sealed class StubProvider : ILlmProvider
    {
        private readonly string _response;

        public StubProvider(string response)
        {
            _response = response;
        }

        public string Name => "stub";

        public Task<LlmResponse> GenerateAsync(
            LlmRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new LlmResponse { Text = _response });
        }

        public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
