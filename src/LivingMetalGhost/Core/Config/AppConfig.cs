namespace LivingMetalGhost.Core.Config;

public sealed class AppConfig
{
    public AppSettings App { get; set; } = new();
    public LlmSettings Llm { get; set; } = new();
    public LlmSettings AdvancedLlm { get; set; } = new();
}

public sealed class AppSettings
{
    public string GhostId { get; set; } = "orkia";
    public string PersonalityId { get; set; } = "calm_architect";
    public string PersonalityPrompt { get; set; } =
        "차분하고 논리적인 개발 보조 캐릭터. 짧고 정확하게 말하지만 너무 딱딱하지 않으며, 가끔 자연스러운 질문으로 대화를 이어간다.";
    public string UserTitle { get; set; } = "사용자님";
    public string Language { get; set; } = "ko-KR";
    public bool EnableLogging { get; set; } = true;
    public bool EnableChatHistory { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public bool EnableProactiveChat { get; set; }
    public int ProactiveChatIntervalMinutes { get; set; } = 30;
    public int ProactiveChatMinMinutes { get; set; } = 20;
    public int ProactiveChatMaxMinutes { get; set; } = 45;
    public Dictionary<string, CharacterPromptSettings> CharacterProfiles { get; set; } = [];
}

public sealed class CharacterPromptSettings
{
    public string Appearance { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public double CharacterScale { get; set; } = 1.0;
    public string CharacterSizePresetId { get; set; } = string.Empty;
    public string CharacterFramingPresetId { get; set; } = string.Empty;
}

public sealed class LlmSettings
{
    public string Provider { get; set; } = "Mock";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";
    public string Model { get; set; } = "mock";
    public string ApiKeySource { get; set; } = "dpapi";
    public double Temperature { get; set; } = 0.7;
    public int MaxOutputTokens { get; set; } = 2048;
    public bool Streaming { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    public string CodexExecutable { get; set; } =
        @"%APPDATA%\LivingMetalGhost\tools\codex-cli\node_modules\.bin\codex.cmd";
    public string CodexWorkingDirectory { get; set; } = string.Empty;
    public int CodexTimeoutSeconds { get; set; } = 180;
}
