namespace LivingMetalGhost.Core.Config;

public sealed class AppConfig
{
    public AppSettings App { get; set; } = new();

    public LlmSettings Llm { get; set; } = new()
    {
        Provider = "Gemini",
        BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/",
        Model = "gemini-2.0-flash",
        ApiKeySource = "dpapi:basic",
        Temperature = 0.7,
        MaxOutputTokens = 2048,
        TimeoutSeconds = 30
    };

    public RoleplayLlmSettings RoleplayLlm { get; set; } = new();

    public LlmSettings AdvancedLlm { get; set; } = new()
    {
        Provider = "lmbot",
        ApiKeySource = "dpapi:advanced",
        TimeoutSeconds = 180
    };

    public AgentsSettings Agents { get; set; } = new();
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
    public WindowPlacementSettings DailyChatWindow { get; set; } = new();
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

public sealed class WindowPlacementSettings
{
    public bool HasPosition { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }
}

public sealed class RoleplayLlmSettings
{
    public LlmSettings Writer { get; set; } = new()
    {
        Provider = "Gemini",
        BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/",
        Model = "gemini-3.1-flash-lite",
        ApiKeySource = "dpapi:roleplay-writer",
        Temperature = 0.7,
        MaxOutputTokens = 4096,
        TimeoutSeconds = 90
    };

    public LlmSettings Character { get; set; } = new()
    {
        Provider = "Gemini",
        BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/",
        Model = "gemini-3.1-flash-lite",
        ApiKeySource = "dpapi:roleplay-character",
        Temperature = 0.9,
        MaxOutputTokens = 2048,
        TimeoutSeconds = 45
    };

    public LlmSettings Director { get; set; } = new()
    {
        Provider = "Gemini",
        BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/",
        Model = "gemini-3.1-flash-lite",
        ApiKeySource = "dpapi:roleplay-director",
        Temperature = 0.25,
        MaxOutputTokens = 1024,
        TimeoutSeconds = 45
    };

    public LlmSettings Memory { get; set; } = new()
    {
        Provider = "Gemini",
        BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/",
        Model = "gemini-3.1-flash-lite",
        ApiKeySource = "dpapi:roleplay-memory",
        Temperature = 0.2,
        MaxOutputTokens = 1024,
        TimeoutSeconds = 45
    };

    public bool EnableStatePanel { get; set; } = true;
    public bool EnableDirectorStateUpdate { get; set; } = true;
    public string StatePanelMetrics { get; set; } = "affection,trust,tension";
    public StoryInfoLabelSettings InfoLabels { get; set; } = new();
    public StoryWriterSettings WriterSettings { get; set; } = new();
}

public sealed class StoryInfoLabelSettings
{
    public string Turn { get; set; } = "No.";
    public string Date { get; set; } = "Date";
    public string Place { get; set; } = "Place";
    public string Affection { get; set; } = "Affection";
    public string Status { get; set; } = "Info";
}

public sealed class StoryWriterSettings
{
    public string Genre { get; set; } = "현대판타지, 심리극";
    public string StoryLength { get; set; } = "medium";
    public int RomanceLevel { get; set; } = 2;
    public int MysteryLevel { get; set; } = 4;
    public int ConflictLevel { get; set; } = 3;
    public int HorrorLevel { get; set; } = 1;
    public int ComedyLevel { get; set; } = 1;
    public string RequiredElements { get; set; } = string.Empty;
    public string ForbiddenElements { get; set; } = string.Empty;
}

public sealed class LlmSettings
{
    public string Provider { get; set; } = "Gemini";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";
    public string Model { get; set; } = "gemini-2.0-flash";
    public string ApiKeySource { get; set; } = "dpapi:basic";
    public double Temperature { get; set; } = 0.7;
    public int MaxOutputTokens { get; set; } = 2048;
    public bool Streaming { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;

    public string CodexExecutable { get; set; } =
        @"%APPDATA%\LivingMetalGhost\tools\codex-cli\node_modules\.bin\codex.cmd";
    public string CodexWorkingDirectory { get; set; } = string.Empty;
    public int CodexTimeoutSeconds { get; set; } = 180;

    public InstalledAppsSettings? InstalledApps { get; set; }
}

public sealed class AgentsSettings
{
    public string DefaultExecutor { get; set; } = "mock";
    public string ApprovalMode { get; set; } = "suggest";
    public bool EnableExecution { get; set; }
    public string WorkspaceRoot { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 180;
    public AgentToolSettings ClaudeCode { get; set; } = new();
    public AgentToolSettings CodexCli { get; set; } = new();
}

public sealed class AgentToolSettings
{
    public string Executable { get; set; } = string.Empty;
    public string ExtraArgs { get; set; } = string.Empty;
}

public sealed class InstalledAppsSettings
{
    public string PreferredApp { get; set; } = "auto";
}
