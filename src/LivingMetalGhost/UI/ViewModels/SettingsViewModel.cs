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

    [ObservableProperty]
    private string provider = "Mock";

    [ObservableProperty]
    private string selectedCharacterId = "codex-tan";

    [ObservableProperty]
    private string characterAppearance = string.Empty;

    [ObservableProperty]
    private string characterBackground = string.Empty;

    [ObservableProperty]
    private string selectedCharacterSizePresetId = string.Empty;

    [ObservableProperty]
    private string selectedCharacterFramingPresetId = string.Empty;

    [ObservableProperty]
    private string model = "mock-codex-tan";

    [ObservableProperty]
    private string personalityPrompt =
        "차분하고 논리적인 개발 보조 캐릭터를 유지하고 정확하게 말하지만, 불필요하게 장황하지 않으며 가벼운 자연스러운 질문으로 대화를 이어간다.";

    [ObservableProperty]
    private string userTitle = "사용자님";

    [ObservableProperty]
    private bool enableProactiveChat;

    [ObservableProperty]
    private int proactiveChatMinMinutes = 20;

    [ObservableProperty]
    private int proactiveChatMaxMinutes = 45;

    [ObservableProperty]
    private string baseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";

    [ObservableProperty]
    private string codexExecutable = DefaultCodexExecutable;

    [ObservableProperty]
    private string codexWorkingDirectory = string.Empty;

    [ObservableProperty]
    private int codexTimeoutSeconds = 180;

    [ObservableProperty]
    private string apiKeyInput = string.Empty;

    [ObservableProperty]
    private string apiKeyStatus = "API Key 없음";

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public SettingsViewModel(
        AppConfigLoader configLoader,
        AppPaths paths,
        DpapiSecretStore secretStore,
        ILlmProviderFactory providerFactory)
    {
        _configLoader = configLoader;
        _paths = paths;
        _secretStore = secretStore;
        _providerFactory = providerFactory;
        Reload();
    }

    public IReadOnlyList<string> Providers { get; } =
        ["Mock", "Gemini", "OpenAI-Compatible", "Codex"];

    public IReadOnlyList<CharacterProfile> Characters => CharacterCatalog.All;

    public IReadOnlyList<CharacterSizePreset> AvailableCharacterSizePresets =>
        CharacterCatalog.Get(SelectedCharacterId).Presentation.SizePresets;

    public IReadOnlyList<CharacterFramingPreset> AvailableCharacterFramingPresets =>
        CharacterCatalog.Get(SelectedCharacterId).Presentation.FramingPresets;

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
        var legacyPersonality = string.IsNullOrWhiteSpace(config.App.PersonalityPrompt)
            ? GetLegacyPersonalityPrompt(config.App.PersonalityId)
            : config.App.PersonalityPrompt;
        if (_characterProfileDrafts.TryGetValue(SelectedCharacterId, out var selectedProfile) &&
            string.IsNullOrWhiteSpace(selectedProfile.Personality))
        {
            selectedProfile.Personality = legacyPersonality;
        }

        LoadCharacterProfile(SelectedCharacterId);
        Provider = config.Llm.Provider;
        Model = config.Llm.Model;
        UserTitle = config.App.UserTitle;
        EnableProactiveChat = config.App.EnableProactiveChat;
        var legacyInterval = Math.Clamp(config.App.ProactiveChatIntervalMinutes, 5, 240);
        ProactiveChatMinMinutes = config.App.ProactiveChatMinMinutes <= 0
            ? legacyInterval
            : Math.Clamp(config.App.ProactiveChatMinMinutes, 5, 240);
        ProactiveChatMaxMinutes = config.App.ProactiveChatMaxMinutes <= 0
            ? legacyInterval
            : Math.Clamp(config.App.ProactiveChatMaxMinutes, 5, 240);
        NormalizeProactiveRange();
        BaseUrl = config.Llm.BaseUrl;
        CodexExecutable = config.Llm.CodexExecutable;
        CodexWorkingDirectory = config.Llm.CodexWorkingDirectory;
        CodexTimeoutSeconds = Math.Clamp(config.Llm.CodexTimeoutSeconds, 30, 900);
        ApiKeyInput = string.Empty;
        ApiKeyStatus = _secretStore.HasApiKey ? "API Key 저장됨 (DPAPI 보호)" : "API Key 없음";
        StatusMessage = string.Empty;
        _isReloading = false;
    }

    partial void OnSelectedCharacterIdChanged(string value)
    {
        if (_isReloading)
        {
            return;
        }

        StoreCurrentCharacterDraft();
        LoadCharacterProfile(value);
    }

    [RelayCommand]
    private void RestoreCharacterTemplate()
    {
        var character = CharacterCatalog.Get(SelectedCharacterId);
        CharacterAppearance = character.DefaultAppearance;
        CharacterBackground = character.DefaultBackground;
        PersonalityPrompt = character.DefaultPersonality;
        SelectedCharacterSizePresetId = character.Presentation.DefaultSizePresetId;
        SelectedCharacterFramingPresetId = character.Presentation.DefaultFramingPresetId;
        StatusMessage = $"{character.DisplayName} 기본 설정을 불러왔습니다.";
    }

    [RelayCommand]
    private void ApplyGeminiFreePreset()
    {
        Provider = "Gemini";
        Model = "gemini-3.1-flash-lite";
        BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";
        StatusMessage = "Gemini 무료 프리셋을 적용했습니다. API Key를 입력하세요.";
    }

    [RelayCommand]
    private void ApplyCodexPreset()
    {
        Provider = "Codex";
        Model = "default";
        CodexExecutable = string.IsNullOrWhiteSpace(CodexExecutable)
            ? DefaultCodexExecutable
            : CodexExecutable;
        CodexWorkingDirectory = string.IsNullOrWhiteSpace(CodexWorkingDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : CodexWorkingDirectory;
        CodexTimeoutSeconds = Math.Clamp(CodexTimeoutSeconds, 30, 900);
        StatusMessage = "Codex 전용 프리셋을 적용했습니다.";
    }

    [RelayCommand]
    private void Save()
    {
        SaveCore();
        StatusMessage = "설정을 저장했습니다.";
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "연결을 테스트하는 중입니다.";

        try
        {
            var config = SaveCore();
            var providerInstance = _providerFactory.Create(config.Llm.Provider);
            var response = await providerInstance.GenerateAsync(new LlmRequest
            {
                Model = config.Llm.Model,
                UserTitle = config.App.UserTitle,
                SystemPrompt = $"한 문장으로 인사하고 사용자를 반드시 '{config.App.UserTitle}'이라고 부르세요.",
                UserText = "연결 테스트입니다. 짧게 인사해 주세요."
            }, CancellationToken.None);

            StatusMessage = $"연결 성공: {response.Text}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"연결 실패: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearApiKey()
    {
        _secretStore.SaveApiKey(string.Empty);
        ApiKeyInput = string.Empty;
        ApiKeyStatus = "API Key 없음";
        StatusMessage = "저장된 API Key를 제거했습니다.";
    }

    private AppConfig SaveCore()
    {
        var config = _configLoader.Load();
        StoreCurrentCharacterDraft();
        config.App.GhostId = CharacterCatalog.Get(SelectedCharacterId).Id;
        config.App.CharacterProfiles = _characterProfileDrafts.ToDictionary(
            pair => pair.Key,
            pair => CloneProfile(pair.Value),
            StringComparer.OrdinalIgnoreCase);
        config.Llm.Provider = Provider.Trim();
        config.Llm.Model = Model.Trim();
        config.Llm.BaseUrl = EnsureTrailingSlash(BaseUrl.Trim());
        config.Llm.CodexExecutable = string.IsNullOrWhiteSpace(CodexExecutable)
            ? DefaultCodexExecutable
            : CodexExecutable.Trim();
        config.Llm.CodexWorkingDirectory = CodexWorkingDirectory.Trim();
        config.Llm.CodexTimeoutSeconds = Math.Clamp(CodexTimeoutSeconds, 30, 900);
        config.App.PersonalityPrompt = string.IsNullOrWhiteSpace(PersonalityPrompt)
            ? CharacterCatalog.Get(SelectedCharacterId).DefaultPersonality
            : PersonalityPrompt.Trim();
        config.App.UserTitle = string.IsNullOrWhiteSpace(UserTitle) ? "사용자님" : UserTitle.Trim();
        config.App.EnableProactiveChat = EnableProactiveChat;
        NormalizeProactiveRange();
        config.App.ProactiveChatMinMinutes = ProactiveChatMinMinutes;
        config.App.ProactiveChatMaxMinutes = ProactiveChatMaxMinutes;

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        });
        File.WriteAllText(_paths.ConfigFile, json);

        if (!string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            _secretStore.SaveApiKey(ApiKeyInput);
            ApiKeyInput = string.Empty;
            ApiKeyStatus = "API Key 저장됨 (DPAPI 보호)";
        }

        return config;
    }

    private void StoreCurrentCharacterDraft()
    {
        if (string.IsNullOrWhiteSpace(_loadedCharacterId))
        {
            return;
        }

        _characterProfileDrafts[_loadedCharacterId] = new CharacterPromptSettings
        {
            Appearance = CharacterAppearance.Trim(),
            Background = CharacterBackground.Trim(),
            Personality = PersonalityPrompt.Trim(),
            CharacterScale = _characterProfileDrafts.TryGetValue(_loadedCharacterId, out var existing)
                ? existing.CharacterScale
                : 1.0,
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
        CharacterAppearance = string.IsNullOrWhiteSpace(profile.Appearance)
            ? character.DefaultAppearance
            : profile.Appearance;
        CharacterBackground = string.IsNullOrWhiteSpace(profile.Background)
            ? character.DefaultBackground
            : profile.Background;
        PersonalityPrompt = string.IsNullOrWhiteSpace(profile.Personality)
            ? character.DefaultPersonality
            : profile.Personality;
        SelectedCharacterSizePresetId = ResolveSizePresetId(character, profile.CharacterSizePresetId);
        SelectedCharacterFramingPresetId = ResolveFramingPresetId(character, profile.CharacterFramingPresetId);
        OnPropertyChanged(nameof(AvailableCharacterSizePresets));
        OnPropertyChanged(nameof(AvailableCharacterFramingPresets));
    }

    private static CharacterPromptSettings CloneProfile(CharacterPromptSettings profile)
    {
        return new CharacterPromptSettings
        {
            Appearance = profile.Appearance,
            Background = profile.Background,
            Personality = profile.Personality,
            CharacterScale = profile.CharacterScale,
            CharacterSizePresetId = profile.CharacterSizePresetId,
            CharacterFramingPresetId = profile.CharacterFramingPresetId
        };
    }

    private static string ResolveSizePresetId(CharacterProfile character, string configuredId)
    {
        return character.Presentation.SizePresets.Any(preset =>
            string.Equals(preset.Id, configuredId, StringComparison.OrdinalIgnoreCase))
            ? configuredId.Trim()
            : character.Presentation.DefaultSizePresetId;
    }

    private static string ResolveFramingPresetId(CharacterProfile character, string configuredId)
    {
        return character.Presentation.FramingPresets.Any(preset =>
            string.Equals(preset.Id, configuredId, StringComparison.OrdinalIgnoreCase))
            ? configuredId.Trim()
            : character.Presentation.DefaultFramingPresetId;
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
    }

    private void NormalizeProactiveRange()
    {
        ProactiveChatMinMinutes = Math.Clamp(ProactiveChatMinMinutes, 5, 240);
        ProactiveChatMaxMinutes = Math.Clamp(ProactiveChatMaxMinutes, 5, 240);
        if (ProactiveChatMaxMinutes < ProactiveChatMinMinutes)
        {
            ProactiveChatMaxMinutes = ProactiveChatMinMinutes;
        }
    }

    private static string GetLegacyPersonalityPrompt(string personalityId)
    {
        return personalityId switch
        {
            "moe_codex" => "작은 개발 보조 고스트처럼 친근하게 말하되 과장하지 않고 기술적으로 정확하게 답한다.",
            "strict_reviewer" => "단정한 리뷰어처럼 가정을 줄이고 위험 요소나 반례를 먼저 짚는다.",
            "cheerful_helper" => "밝고 친근하게 말하면서도 전달 정보의 정확성을 우선한다.",
            _ => "차분하고 논리적인 개발 보조 캐릭터를 유지하고 정확하게 말하지만, 불필요하게 장황하지 않으며 가벼운 자연스러운 질문으로 대화를 이어간다."
        };
    }
}
