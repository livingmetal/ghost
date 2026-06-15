using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Security;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string DefaultCodexExecutable =
        @"%APPDATA%\LivingMetalGhost\tools\codex-cli\node_modules\.bin\codex.cmd";
    private readonly AppConfigLoader _configLoader;
    private readonly AppPaths _paths;
    private readonly DpapiSecretStore _secretStore;
    private readonly ILlmProviderFactory _providerFactory;
    private readonly Dictionary<string, CharacterPromptSettings> _characterProfileDrafts =
        new(StringComparer.OrdinalIgnoreCase);
    private string _loadedCharacterId = string.Empty;
    private bool _isReloading;

    [ObservableProperty] private string provider = "Mock";
    [ObservableProperty] private string selectedCharacterId = "ssuang";
    [ObservableProperty] private string characterAppearance = string.Empty;
    [ObservableProperty] private string characterBackground = string.Empty;
    [ObservableProperty] private string selectedCharacterSizePresetId = string.Empty;
    [ObservableProperty] private string selectedCharacterFramingPresetId = string.Empty;
    [ObservableProperty] private string model = "mock";
    [ObservableProperty] private string personalityPrompt = "차분하고 논리적인 개발 보조 캐릭터를 유지하고 정확하게 말하지만, 불필요하게 장황하지 않으며 가벼운 자연스러운 질문으로 대화를 이어간다.";
    [ObservableProperty] private string userTitle = "사용자님";
    [ObservableProperty] private bool enableProactiveChat;
    [ObservableProperty] private int proactiveChatMinMinutes = 20;
    [ObservableProperty] private int proactiveChatMaxMinutes = 45;
    [ObservableProperty] private string baseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";
    [ObservableProperty] private string codexExecutable = DefaultCodexExecutable;
    [ObservableProperty] private string codexWorkingDirectory = string.Empty;
    [ObservableProperty] private int codexTimeoutSeconds = 180;
    [ObservableProperty] private string apiKeyInput = string.Empty;
    [ObservableProperty] private string apiKeyStatus = "기본 모드 키 없음";
    [ObservableProperty] private string advancedApiKeyInput = string.Empty;
    [ObservableProperty] private string advancedApiKeyStatus = "고급 모드 키 없음";
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string advancedProvider = "Mock";
    [ObservableProperty] private string advancedModel = string.Empty;
    [ObservableProperty] private string advancedBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";
    [ObservableProperty] private double advancedTemperature = 0.4;
    [ObservableProperty] private int advancedMaxOutputTokens = 4096;
    [ObservableProperty] private int advancedTimeoutSeconds = 60;
    [ObservableProperty] private string installedAppsPreferredApp = "auto";
    [ObservableProperty] private string installedAppsStatus = string.Empty;
    [ObservableProperty] private string agentDefaultExecutor = "mock";
    [ObservableProperty] private string agentApprovalMode = "suggest";
    [ObservableProperty] private bool agentEnableExecution;
    [ObservableProperty] private string agentWorkspaceRoot = string.Empty;
    [ObservableProperty] private int agentTimeoutSeconds = 180;
    [ObservableProperty] private string claudeCodeExecutable = "claude";
    [ObservableProperty] private string agentCodexExecutable = DefaultCodexExecutable;

    public SettingsViewModel(AppConfigLoader configLoader, AppPaths paths, DpapiSecretStore secretStore, ILlmProviderFactory providerFactory)
    {
        _configLoader = configLoader;
        _paths = paths;
        _secretStore = secretStore;
        _providerFactory = providerFactory;
        Reload();
    }

    public IReadOnlyList<string> Providers { get; } = ["Mock", "Gemini", "OpenAI", "OpenAI-Compatible", "Codex"];
    public IReadOnlyList<string> AdvancedProviders { get; } = ["OpenAI", "Installed-Apps"];
    public IReadOnlyList<string> InstalledAppsPreferredApps { get; } = ["auto", "claude", "chatgpt"];
    public IReadOnlyList<string> AgentExecutors { get; } = ["mock", "claude-code", "codex-cli"];
    public IReadOnlyList<string> AgentApprovalModes { get; } = ["suggest", "ask", "apply", "execute"];
    public IReadOnlyList<CharacterProfile> Characters => CharacterCatalog.All;
    public IReadOnlyList<CharacterSizePreset> AvailableCharacterSizePresets => CharacterCatalog.Get(SelectedCharacterId).Presentation.SizePresets;
    public IReadOnlyList<CharacterFramingPreset> AvailableCharacterFramingPresets => CharacterCatalog.Get(SelectedCharacterId).Presentation.FramingPresets;

    public void Reload()
    {
        _isReloading = true;
        var config = _configLoader.Load();
        config.App.CharacterProfiles ??= [];
        _characterProfileDrafts.Clear();
        foreach (var (characterId, profile) in config.App.CharacterProfiles)
        {
            _characterProfileDrafts[characterId] = CloneProfile(profile);
        }

        SelectedCharacterId = CharacterCatalog.Get(config.App.GhostId).Id;
        var legacyPersonality = string.IsNullOrWhiteSpace(config.App.PersonalityPrompt) ? GetLegacyPersonalityPrompt(config.App.PersonalityId) : config.App.PersonalityPrompt;
        if (_characterProfileDrafts.TryGetValue(SelectedCharacterId, out var selectedProfile) && string.IsNullOrWhiteSpace(selectedProfile.Personality))
        {
            selectedProfile.Personality = legacyPersonality;
        }

        LoadCharacterProfile(SelectedCharacterId);
        Provider = config.Llm.Provider;
        Model = config.Llm.Model;
        UserTitle = config.App.UserTitle;
        EnableProactiveChat = config.App.EnableProactiveChat;
        var legacyInterval = Math.Clamp(config.App.ProactiveChatIntervalMinutes, 5, 240);
        ProactiveChatMinMinutes = config.App.ProactiveChatMinMinutes <= 0 ? legacyInterval : Math.Clamp(config.App.ProactiveChatMinMinutes, 5, 240);
        ProactiveChatMaxMinutes = config.App.ProactiveChatMaxMinutes <= 0 ? legacyInterval : Math.Clamp(config.App.ProactiveChatMaxMinutes, 5, 240);
        NormalizeProactiveRange();
        BaseUrl = config.Llm.BaseUrl;
        CodexExecutable = config.Llm.CodexExecutable;
        CodexWorkingDirectory = config.Llm.CodexWorkingDirectory;
        CodexTimeoutSeconds = Math.Clamp(config.Llm.CodexTimeoutSeconds, 30, 900);
        AdvancedProvider = config.AdvancedLlm.Provider;
        AdvancedModel = config.AdvancedLlm.Model;
        AdvancedBaseUrl = config.AdvancedLlm.BaseUrl;
        AdvancedTemperature = config.AdvancedLlm.Temperature;
        AdvancedMaxOutputTokens = config.AdvancedLlm.MaxOutputTokens;
        AdvancedTimeoutSeconds = Math.Clamp(config.AdvancedLlm.TimeoutSeconds, 15, 600);
        InstalledAppsPreferredApp = config.AdvancedLlm.InstalledApps?.PreferredApp ?? "auto";
        InstalledAppsStatus = string.Empty;
        AgentDefaultExecutor = config.Agents.DefaultExecutor;
        AgentApprovalMode = config.Agents.ApprovalMode;
        AgentEnableExecution = config.Agents.EnableExecution;
        AgentWorkspaceRoot = config.Agents.WorkspaceRoot;
        AgentTimeoutSeconds = Math.Clamp(config.Agents.TimeoutSeconds, 30, 1800);
        ClaudeCodeExecutable = string.IsNullOrWhiteSpace(config.Agents.ClaudeCode.Executable) ? "claude" : config.Agents.ClaudeCode.Executable;
        AgentCodexExecutable = string.IsNullOrWhiteSpace(config.Agents.CodexCli.Executable) ? DefaultCodexExecutable : config.Agents.CodexCli.Executable;
        ApiKeyInput = string.Empty;
        AdvancedApiKeyInput = string.Empty;
        RefreshSecretStatuses();
        StatusMessage = string.Empty;
        _isReloading = false;
    }

    partial void OnSelectedCharacterIdChanged(string value)
    {
        if (_isReloading) return;
        StoreCurrentCharacterDraft();
        LoadCharacterProfile(value);
    }

    [RelayCommand] private void RestoreCharacterTemplate()
    {
        var character = CharacterCatalog.Get(SelectedCharacterId);
        CharacterAppearance = character.DefaultAppearance;
        CharacterBackground = character.DefaultBackground;
        PersonalityPrompt = character.DefaultPersonality;
        SelectedCharacterSizePresetId = character.Presentation.DefaultSizePresetId;
        SelectedCharacterFramingPresetId = character.Presentation.DefaultFramingPresetId;
        StatusMessage = $"{character.DisplayName} 기본 설정을 불러왔습니다.";
    }

    [RelayCommand] private void ApplyGeminiFreePreset()
    {
        Provider = "Gemini";
        Model = "gemini-3.1-flash-lite";
        BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";
        StatusMessage = "Gemini 무료 프리셋을 적용했습니다. 기본 모드 키를 입력하세요.";
    }

    [RelayCommand] private void ApplyCodexPreset()
    {
        Provider = "Codex";
        Model = "default";
        CodexExecutable = string.IsNullOrWhiteSpace(CodexExecutable) ? DefaultCodexExecutable : CodexExecutable;
        CodexWorkingDirectory = string.IsNullOrWhiteSpace(CodexWorkingDirectory) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : CodexWorkingDirectory;
        CodexTimeoutSeconds = Math.Clamp(CodexTimeoutSeconds, 30, 900);
        StatusMessage = "Codex 전용 프리셋을 적용했습니다.";
    }

    [RelayCommand] private void ApplyAdvancedOpenAiPreset()
    {
        AdvancedProvider = "OpenAI";
        AdvancedModel = "gpt-4o";
        AdvancedBaseUrl = "https://api.openai.com/v1/";
        AdvancedTemperature = 0.4;
        AdvancedMaxOutputTokens = 4096;
        AdvancedTimeoutSeconds = 120;
        StatusMessage = "고급 대화 OpenAI 프리셋을 적용했습니다. 고급 모드 키를 입력하세요.";
    }

    [RelayCommand] private void ApplyInstalledAppsPreset()
    {
        AdvancedProvider = "Installed-Apps";
        AdvancedModel = string.Empty;
        AdvancedBaseUrl = string.Empty;
        AdvancedTemperature = 0.4;
        AdvancedMaxOutputTokens = 4096;
        AdvancedTimeoutSeconds = 120;
        InstalledAppsPreferredApp = "claude";
        StatusMessage = "Claude 프리셋을 적용했습니다. claude CLI 또는 Claude 데스크탑 앱이 필요합니다. '앱 감지'로 확인하세요.";
    }

    [RelayCommand] private async Task DetectInstalledAppsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        InstalledAppsStatus = "감지 중...";
        InstalledAppDetector.Invalidate();
        try
        {
            var info = await InstalledAppDetector.DetectAsync();
            var parts = new List<string>();
            if (info.HasClaudeCli) parts.Add("Claude CLI 확인 ✓ (대화 가능)");
            else if (info.ClaudeExePath is not null) parts.Add("Claude 앱 감지됨 (대화를 위해 claude CLI 필요 — npm install -g @anthropic-ai/claude-code)");
            if (info.HasChatGptCli) parts.Add("ChatGPT CLI 확인 ✓");
            else if (info.ChatGptExePath is not null) parts.Add("ChatGPT 앱 감지됨 (별도 키 필요)");
            InstalledAppsStatus = parts.Count > 0 ? string.Join("  ·  ", parts) : "감지된 앱 없음 — ChatGPT 또는 Claude를 설치하거나 claude CLI를 설치하세요.";
        }
        catch (Exception ex)
        {
            InstalledAppsStatus = $"감지 실패: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand] private void ApplyClaudeCodeAgentPreset()
    {
        AgentDefaultExecutor = "claude-code";
        ClaudeCodeExecutable = string.IsNullOrWhiteSpace(ClaudeCodeExecutable) ? "claude" : ClaudeCodeExecutable;
        AgentApprovalMode = "suggest";
        AgentEnableExecution = false;
        AgentWorkspaceRoot = string.IsNullOrWhiteSpace(AgentWorkspaceRoot) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : AgentWorkspaceRoot;
        AgentTimeoutSeconds = Math.Clamp(AgentTimeoutSeconds, 30, 1800);
        StatusMessage = "Claude Code 에이전트 프리셋(안전: 제안 모드)을 적용했습니다. 작업 루트를 확인하세요.";
    }

    [RelayCommand] private void ApplyCodexAgentPreset()
    {
        AgentDefaultExecutor = "codex-cli";
        AgentCodexExecutable = string.IsNullOrWhiteSpace(AgentCodexExecutable) ? DefaultCodexExecutable : AgentCodexExecutable;
        AgentApprovalMode = "suggest";
        AgentEnableExecution = false;
        AgentWorkspaceRoot = string.IsNullOrWhiteSpace(AgentWorkspaceRoot) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : AgentWorkspaceRoot;
        AgentTimeoutSeconds = Math.Clamp(AgentTimeoutSeconds, 30, 1800);
        StatusMessage = "Codex CLI 에이전트 프리셋(안전: 제안 모드)을 적용했습니다. 작업 루트를 확인하세요.";
    }

    [RelayCommand] private void Save()
    {
        SaveCore();
        StatusMessage = "설정을 저장했습니다.";
    }

    [RelayCommand] private async Task TestConnectionAsync() => await TestConnectionCoreAsync(false);
    [RelayCommand] private async Task TestAdvancedConnectionAsync() => await TestConnectionCoreAsync(true);

    private async Task TestConnectionCoreAsync(bool advanced)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = advanced ? "고급 모드 연결을 테스트하는 중입니다." : "기본 모드 연결을 테스트하는 중입니다.";
        try
        {
            var config = SaveCore();
            var llm = advanced ? config.AdvancedLlm : config.Llm;
            var providerInstance = _providerFactory.Create(llm.Provider);
            var response = await providerInstance.GenerateAsync(new LlmRequest
            {
                Model = llm.Model,
                Options = LlmOptions.FromSettings(llm),
                UserTitle = config.App.UserTitle,
                SystemPrompt = $"한 문장으로 인사하고 사용자를 반드시 '{config.App.UserTitle}'이라고 부르세요.",
                UserText = "연결 테스트입니다. 짧게 인사해 주세요."
            }, CancellationToken.None);
            StatusMessage = $"{(advanced ? "고급" : "기본")} 모드 연결 성공: {response.Text}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{(advanced ? "고급" : "기본")} 모드 연결 실패: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand] private void ClearApiKey()
    {
        _secretStore.SaveBasicApiKey(string.Empty);
        ApiKeyInput = string.Empty;
        RefreshSecretStatuses();
        StatusMessage = "저장된 기본 모드 키를 제거했습니다.";
    }

    [RelayCommand] private void ClearAdvancedApiKey()
    {
        _secretStore.SaveAdvancedApiKey(string.Empty);
        AdvancedApiKeyInput = string.Empty;
        RefreshSecretStatuses();
        StatusMessage = "저장된 고급 모드 키를 제거했습니다.";
    }

    private AppConfig SaveCore()
    {
        var config = _configLoader.Load();
        StoreCurrentCharacterDraft();
        config.App.GhostId = CharacterCatalog.Get(SelectedCharacterId).Id;
        config.App.CharacterProfiles = _characterProfileDrafts.ToDictionary(pair => pair.Key, pair => CloneProfile(pair.Value), StringComparer.OrdinalIgnoreCase);
        config.Llm.Provider = Provider.Trim();
        config.Llm.Model = Model.Trim();
        config.Llm.BaseUrl = EnsureTrailingSlash(BaseUrl.Trim());
        config.Llm.ApiKeySource = DpapiSecretStore.BasicSource;
        config.Llm.CodexExecutable = string.IsNullOrWhiteSpace(CodexExecutable) ? DefaultCodexExecutable : CodexExecutable.Trim();
        config.Llm.CodexWorkingDirectory = CodexWorkingDirectory.Trim();
        config.Llm.CodexTimeoutSeconds = Math.Clamp(CodexTimeoutSeconds, 30, 900);
        config.AdvancedLlm.Provider = AdvancedProvider.Trim();
        config.AdvancedLlm.Model = AdvancedModel.Trim();
        config.AdvancedLlm.BaseUrl = EnsureTrailingSlash(AdvancedBaseUrl.Trim());
        config.AdvancedLlm.Temperature = Math.Clamp(AdvancedTemperature, 0.0, 2.0);
        config.AdvancedLlm.MaxOutputTokens = Math.Clamp(AdvancedMaxOutputTokens, 256, 32768);
        config.AdvancedLlm.TimeoutSeconds = Math.Clamp(AdvancedTimeoutSeconds, 15, 600);
        config.AdvancedLlm.ApiKeySource = DpapiSecretStore.AdvancedSource;
        config.AdvancedLlm.InstalledApps = new InstalledAppsSettings { PreferredApp = string.IsNullOrWhiteSpace(InstalledAppsPreferredApp) ? "auto" : InstalledAppsPreferredApp.Trim() };
        config.Agents.DefaultExecutor = AgentDefaultExecutor.Trim();
        config.Agents.ApprovalMode = AgentApprovalMode.Trim();
        config.Agents.EnableExecution = AgentEnableExecution;
        config.Agents.WorkspaceRoot = AgentWorkspaceRoot.Trim();
        config.Agents.TimeoutSeconds = Math.Clamp(AgentTimeoutSeconds, 30, 1800);
        config.Agents.ClaudeCode.Executable = string.IsNullOrWhiteSpace(ClaudeCodeExecutable) ? "claude" : ClaudeCodeExecutable.Trim();
        config.Agents.CodexCli.Executable = string.IsNullOrWhiteSpace(AgentCodexExecutable) ? DefaultCodexExecutable : AgentCodexExecutable.Trim();
        config.App.PersonalityPrompt = string.IsNullOrWhiteSpace(PersonalityPrompt) ? CharacterCatalog.Get(SelectedCharacterId).DefaultPersonality : PersonalityPrompt.Trim();
        config.App.UserTitle = string.IsNullOrWhiteSpace(UserTitle) ? "사용자님" : UserTitle.Trim();
        config.App.EnableProactiveChat = EnableProactiveChat;
        NormalizeProactiveRange();
        config.App.ProactiveChatMinMinutes = ProactiveChatMinMinutes;
        config.App.ProactiveChatMaxMinutes = ProactiveChatMaxMinutes;
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true });
        File.WriteAllText(_paths.ConfigFile, json);
        if (!string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            _secretStore.SaveBasicApiKey(ApiKeyInput);
            ApiKeyInput = string.Empty;
        }
        if (!string.IsNullOrWhiteSpace(AdvancedApiKeyInput))
        {
            _secretStore.SaveAdvancedApiKey(AdvancedApiKeyInput);
            AdvancedApiKeyInput = string.Empty;
        }
        RefreshSecretStatuses();
        return config;
    }

    private void StoreCurrentCharacterDraft()
    {
        if (string.IsNullOrWhiteSpace(_loadedCharacterId)) return;
        _characterProfileDrafts[_loadedCharacterId] = new CharacterPromptSettings
        {
            Appearance = CharacterAppearance.Trim(),
            Background = CharacterBackground.Trim(),
            Personality = PersonalityPrompt.Trim(),
            CharacterScale = _characterProfileDrafts.TryGetValue(_loadedCharacterId, out var existing) ? existing.CharacterScale : 1.0,
            CharacterSizePresetId = SelectedCharacterSizePresetId.Trim(),
            CharacterFramingPresetId = SelectedCharacterFramingPresetId.Trim()
        };
    }

    private void LoadCharacterProfile(string characterId)
    {
        var character = CharacterCatalog.Get(characterId);
        if (!_characterProfileDrafts.TryGetValue(character.Id, out var profile))
        {
            profile = new CharacterPromptSettings
            {
                Appearance = character.DefaultAppearance,
                Background = character.DefaultBackground,
                Personality = character.DefaultPersonality,
                CharacterScale = 1.0,
                CharacterSizePresetId = character.Presentation.DefaultSizePresetId,
                CharacterFramingPresetId = character.Presentation.DefaultFramingPresetId
            };
            _characterProfileDrafts[character.Id] = profile;
        }
        _loadedCharacterId = character.Id;
        CharacterAppearance = string.IsNullOrWhiteSpace(profile.Appearance) ? character.DefaultAppearance : profile.Appearance;
        CharacterBackground = string.IsNullOrWhiteSpace(profile.Background) ? character.DefaultBackground : profile.Background;
        PersonalityPrompt = string.IsNullOrWhiteSpace(profile.Personality) ? character.DefaultPersonality : profile.Personality;
        SelectedCharacterSizePresetId = ResolveSizePresetId(character, profile.CharacterSizePresetId);
        SelectedCharacterFramingPresetId = ResolveFramingPresetId(character, profile.CharacterFramingPresetId);
        OnPropertyChanged(nameof(AvailableCharacterSizePresets));
        OnPropertyChanged(nameof(AvailableCharacterFramingPresets));
    }

    private void RefreshSecretStatuses()
    {
        ApiKeyStatus = _secretStore.HasBasicApiKey ? "기본 모드 키 저장됨 (DPAPI 보호)" : "기본 모드 키 없음";
        AdvancedApiKeyStatus = _secretStore.HasAdvancedApiKey ? "고급 모드 키 저장됨 (DPAPI 보호)" : "고급 모드 키 없음";
    }

    private static CharacterPromptSettings CloneProfile(CharacterPromptSettings profile) => new()
    {
        Appearance = profile.Appearance,
        Background = profile.Background,
        Personality = profile.Personality,
        CharacterScale = profile.CharacterScale,
        CharacterSizePresetId = profile.CharacterSizePresetId,
        CharacterFramingPresetId = profile.CharacterFramingPresetId
    };

    private static string ResolveSizePresetId(CharacterProfile character, string configuredId) =>
        character.Presentation.SizePresets.Any(preset => string.Equals(preset.Id, configuredId, StringComparison.OrdinalIgnoreCase)) ? configuredId.Trim() : character.Presentation.DefaultSizePresetId;

    private static string ResolveFramingPresetId(CharacterProfile character, string configuredId) =>
        character.Presentation.FramingPresets.Any(preset => string.Equals(preset.Id, configuredId, StringComparison.OrdinalIgnoreCase)) ? configuredId.Trim() : character.Presentation.DefaultFramingPresetId;

    private static string EnsureTrailingSlash(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
    }

    private void NormalizeProactiveRange()
    {
        ProactiveChatMinMinutes = Math.Clamp(ProactiveChatMinMinutes, 5, 240);
        ProactiveChatMaxMinutes = Math.Clamp(ProactiveChatMaxMinutes, 5, 240);
        if (ProactiveChatMaxMinutes < ProactiveChatMinMinutes) ProactiveChatMaxMinutes = ProactiveChatMinMinutes;
    }

    private static string GetLegacyPersonalityPrompt(string personalityId) => personalityId switch
    {
        "moe_codex" => "작은 개발 보조 고스트처럼 친근하게 말하되 과장하지 않고 기술적으로 정확하게 답한다.",
        "strict_reviewer" => "단정한 리뷰어처럼 가정을 줄이고 위험 요소나 반례를 먼저 짚는다.",
        "cheerful_helper" => "밝고 친근하게 말하면서도 전달 정보의 정확성을 우선한다.",
        _ => "차분하고 논리적인 개발 보조 캐릭터를 유지하고 정확하게 말하지만, 불필요하게 장황하지 않으며 가벼운 자연스러운 질문으로 대화를 이어간다."
    };
}
