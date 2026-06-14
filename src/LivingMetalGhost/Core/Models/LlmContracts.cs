namespace LivingMetalGhost.Core.Models;

public sealed class LlmRequest
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserText { get; set; } = string.Empty;
    public string UserTitle { get; set; } = "사용자님";
    public string Model { get; set; } = string.Empty;
    public IReadOnlyList<LlmHistoryMessage> History { get; set; } = [];
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
}

public sealed class SkillResult
{
    public string BubbleText { get; set; } = string.Empty;
    public string Mood { get; set; } = "idle";
    public string Action { get; set; } = "none";
    public bool UsedLlm { get; set; }
    public object? RawData { get; set; }
}
