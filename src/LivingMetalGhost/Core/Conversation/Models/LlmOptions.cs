using LivingMetalGhost.Core.Config;

namespace LivingMetalGhost.Core.Models;

/// <summary>
/// 한 번의 LLM 호출에 필요한 연결/생성 파라미터 묶음.
/// Provider 는 전역 설정(config.Llm)을 직접 읽지 않고, 호출자가 선택해 넘긴
/// 이 옵션을 기준으로 동작한다. 이렇게 해야 기본(llm)/고급(advanced_llm) 설정을
/// 같은 Provider 로 구분 없이 처리할 수 있다.
/// </summary>
public sealed class LlmOptions
{
    public string Provider { get; init; } = "mock";
    public string BaseUrl { get; init; } = "";
    public string Model { get; init; } = "";
    public string ApiKeySource { get; init; } = "dpapi";
    public double Temperature { get; init; }
    public int MaxOutputTokens { get; init; }
    public int TimeoutSeconds { get; init; }
    public bool Streaming { get; init; }

    /// <summary>설정(LlmSettings)으로부터 호출 옵션을 생성한다.</summary>
    public static LlmOptions FromSettings(LlmSettings settings) => new()
    {
        Provider = settings.Provider,
        BaseUrl = settings.BaseUrl,
        Model = settings.Model,
        ApiKeySource = settings.ApiKeySource,
        Temperature = settings.Temperature,
        MaxOutputTokens = settings.MaxOutputTokens,
        TimeoutSeconds = settings.TimeoutSeconds,
        Streaming = settings.Streaming
    };
}
