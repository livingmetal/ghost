namespace LivingMetalGhost.Core.Models;

public sealed class LlmRequest
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserText { get; set; } = string.Empty;
    public string UserTitle { get; set; } = "사용자님";

    /// <summary>
    /// 모델명. 하위 호환을 위해 유지한다. <see cref="Options"/> 가 지정되면 그쪽 Model 을 우선한다.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 이번 호출에 사용할 연결/생성 옵션. ConversationService 가 기본/고급 설정을 골라 채운다.
    /// null 이면 Provider 는 안전을 위해 전역 기본 설정으로 폴백한다.
    /// </summary>
    public LlmOptions? Options { get; set; }

    public IReadOnlyList<LlmHistoryMessage> History { get; set; } = [];
    public LlmImageAttachment? Image { get; set; }

    /// <summary>Options 의 Model 을 우선 사용하고, 없으면 Model 필드로 폴백한다.</summary>
    public string ResolveModel() =>
        !string.IsNullOrWhiteSpace(Options?.Model) ? Options!.Model : Model;
}

public sealed record LlmImageAttachment(
    string FileName,
    string MimeType,
    string Base64Data,
    string SourcePath)
{
    public string DataUrl => $"data:{MimeType};base64,{Base64Data}";
}

public sealed class LlmHistoryMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class LlmResponse
{
    public string Text { get; set; } = string.Empty;
    public bool FromFallback { get; set; }
    public bool ContinuedAutomatically { get; set; }
}

public sealed class LlmStreamChunk
{
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public sealed class UserRequest
{
    public string RawText { get; set; } = string.Empty;
    public bool UseAdvancedModel { get; set; }
    public LlmImageAttachment? Image { get; set; }
}

public sealed class SkillResult
{
    public string BubbleText { get; set; } = string.Empty;
    public string Mood { get; set; } = "idle";
    public string Action { get; set; } = "none";
    public bool UsedLlm { get; set; }
    public object? RawData { get; set; }
}
