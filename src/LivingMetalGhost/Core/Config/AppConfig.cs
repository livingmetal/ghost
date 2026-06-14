namespace LivingMetalGhost.Core.Config;

public sealed class AppConfig
{
    public AppSettings App { get; set; } = new();

    /// <summary>기본(가벼운) 대화용 LLM 설정. 기본값은 Gemini(OpenAI 호환 API).</summary>
    public LlmSettings Llm { get; set; } = new()
    {
        Provider = "Gemini",
        BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/",
        Model = "gemini-2.0-flash",
        Temperature = 0.7,
        MaxOutputTokens = 2048,
        TimeoutSeconds = 30
    };

    /// <summary>고급 대화/분석용 LLM 설정. 기본값은 로컬 claude/codex CLI(lmbot).</summary>
    public LlmSettings AdvancedLlm { get; set; } = new() { Provider = "lmbot", TimeoutSeconds = 180 };

    /// <summary>외부 작업 에이전트(Claude Code / Codex CLI 등) 설정.</summary>
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
    public string Provider { get; set; } = "Gemini";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";
    public string Model { get; set; } = "gemini-2.0-flash";
    public string ApiKeySource { get; set; } = "dpapi";
    public double Temperature { get; set; } = 0.7;
    public int MaxOutputTokens { get; set; } = 2048;
    public bool Streaming { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;

    // --- Legacy: codex-as-chat-provider 설정 ---
    // 새 외부 작업 에이전트 설정은 AppConfig.Agents 로 이동했다.
    // 기존 CodexCliProvider(채팅 경로)와 SettingsViewModel 호환을 위해 유지한다.
    // TODO: Agents 계층으로 완전히 통합되면 제거 검토.
    public string CodexExecutable { get; set; } =
        @"%APPDATA%\LivingMetalGhost\tools\codex-cli\node_modules\.bin\codex.cmd";
    public string CodexWorkingDirectory { get; set; } = string.Empty;
    public int CodexTimeoutSeconds { get; set; } = 180;

    /// <summary>installed-apps 프로바이더 전용 설정. provider = "installed-apps" 일 때만 사용한다.</summary>
    public InstalledAppsSettings? InstalledApps { get; set; }
}

/// <summary>
/// 외부 작업 에이전트 계층 설정. Provider(텍스트 응답)와 분리된, 파일/명령/빌드/테스트
/// 같은 무거운 작업을 다루는 Agent Executor 의 동작을 정의한다.
/// </summary>
public sealed class AgentsSettings
{
    /// <summary>기본 사용 에이전트: mock | claude-code | codex-cli.</summary>
    public string DefaultExecutor { get; set; } = "mock";

    /// <summary>기본 승인 모드: ask | suggest | apply | execute. 안전을 위해 기본 suggest.</summary>
    public string ApprovalMode { get; set; } = "suggest";

    /// <summary>
    /// 실제 명령/파일 수정 실행을 허용하는 마스터 스위치. false 이면 apply/execute 요청도
    /// suggest 로 강등된다(이중 안전장치).
    /// </summary>
    public bool EnableExecution { get; set; }

    /// <summary>에이전트가 작업할 수 있는 루트 경로. 이 밖의 파일은 수정할 수 없다.</summary>
    public string WorkspaceRoot { get; set; } = string.Empty;

    /// <summary>에이전트 실행 타임아웃(초).</summary>
    public int TimeoutSeconds { get; set; } = 180;

    public AgentToolSettings ClaudeCode { get; set; } = new();
    public AgentToolSettings CodexCli { get; set; } = new();
}

/// <summary>개별 외부 CLI 도구 설정. 모델명이 아닌 실행 경로/인자만 다룬다.</summary>
public sealed class AgentToolSettings
{
    public string Executable { get; set; } = string.Empty;
    public string ExtraArgs { get; set; } = string.Empty;
}

/// <summary>
/// installed-apps 프로바이더 전용 설정.
/// 설치된 ChatGPT 또는 Claude 데스크탑 앱 중 어느 쪽을 우선 사용할지 지정한다.
/// </summary>
public sealed class InstalledAppsSettings
{
    /// <summary>우선 사용할 앱: chatgpt | claude | auto (기본값: auto)</summary>
    public string PreferredApp { get; set; } = "auto";
}
