using CommunityToolkit.Mvvm.Input;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Security;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.UI.ViewModels;

public partial class SettingsViewModel
{
    private bool _roleplayRoutingLoaded;
    private string _roleplayCharacterProvider = "Gemini";
    private string _roleplayCharacterModel = "gemini-3.1-flash-lite";
    private string _roleplayCharacterBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";
    private string _roleplayCharacterKeyInput = string.Empty;
    private string _roleplayCharacterKeyStatus = string.Empty;
    private string _roleplayDirectorProvider = "Gemini";
    private string _roleplayDirectorModel = "gemini-3.1-flash-lite";
    private string _roleplayDirectorBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";
    private string _roleplayDirectorKeyInput = string.Empty;
    private string _roleplayDirectorKeyStatus = string.Empty;
    private string _roleplayMemoryProvider = "Gemini";
    private string _roleplayMemoryModel = "gemini-3.1-flash-lite";
    private string _roleplayMemoryBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";
    private string _roleplayMemoryKeyInput = string.Empty;
    private string _roleplayMemoryKeyStatus = string.Empty;
    private bool _roleplayEnableStatePanel = true;
    private bool _roleplayEnableDirectorStateUpdate = true;
    private string _roleplayStatePanelMetrics = "affection,trust,tension";

    public IReadOnlyList<string> RoleplayProviders => Providers;

    public string RoleplayCharacterProvider { get { EnsureRoleplayRoutingLoaded(); return _roleplayCharacterProvider; } set => SetProperty(ref _roleplayCharacterProvider, value); }
    public string RoleplayCharacterModel { get { EnsureRoleplayRoutingLoaded(); return _roleplayCharacterModel; } set => SetProperty(ref _roleplayCharacterModel, value); }
    public string RoleplayCharacterBaseUrl { get { EnsureRoleplayRoutingLoaded(); return _roleplayCharacterBaseUrl; } set => SetProperty(ref _roleplayCharacterBaseUrl, value); }
    public string RoleplayCharacterKeyInput { get => _roleplayCharacterKeyInput; set => SetProperty(ref _roleplayCharacterKeyInput, value); }
    public string RoleplayCharacterKeyStatus { get { EnsureRoleplayRoutingLoaded(); return _roleplayCharacterKeyStatus; } set => SetProperty(ref _roleplayCharacterKeyStatus, value); }

    public string RoleplayDirectorProvider { get { EnsureRoleplayRoutingLoaded(); return _roleplayDirectorProvider; } set => SetProperty(ref _roleplayDirectorProvider, value); }
    public string RoleplayDirectorModel { get { EnsureRoleplayRoutingLoaded(); return _roleplayDirectorModel; } set => SetProperty(ref _roleplayDirectorModel, value); }
    public string RoleplayDirectorBaseUrl { get { EnsureRoleplayRoutingLoaded(); return _roleplayDirectorBaseUrl; } set => SetProperty(ref _roleplayDirectorBaseUrl, value); }
    public string RoleplayDirectorKeyInput { get => _roleplayDirectorKeyInput; set => SetProperty(ref _roleplayDirectorKeyInput, value); }
    public string RoleplayDirectorKeyStatus { get { EnsureRoleplayRoutingLoaded(); return _roleplayDirectorKeyStatus; } set => SetProperty(ref _roleplayDirectorKeyStatus, value); }

    public string RoleplayMemoryProvider { get { EnsureRoleplayRoutingLoaded(); return _roleplayMemoryProvider; } set => SetProperty(ref _roleplayMemoryProvider, value); }
    public string RoleplayMemoryModel { get { EnsureRoleplayRoutingLoaded(); return _roleplayMemoryModel; } set => SetProperty(ref _roleplayMemoryModel, value); }
    public string RoleplayMemoryBaseUrl { get { EnsureRoleplayRoutingLoaded(); return _roleplayMemoryBaseUrl; } set => SetProperty(ref _roleplayMemoryBaseUrl, value); }
    public string RoleplayMemoryKeyInput { get => _roleplayMemoryKeyInput; set => SetProperty(ref _roleplayMemoryKeyInput, value); }
    public string RoleplayMemoryKeyStatus { get { EnsureRoleplayRoutingLoaded(); return _roleplayMemoryKeyStatus; } set => SetProperty(ref _roleplayMemoryKeyStatus, value); }

    public bool RoleplayEnableStatePanel { get { EnsureRoleplayRoutingLoaded(); return _roleplayEnableStatePanel; } set => SetProperty(ref _roleplayEnableStatePanel, value); }
    public bool RoleplayEnableDirectorStateUpdate { get { EnsureRoleplayRoutingLoaded(); return _roleplayEnableDirectorStateUpdate; } set => SetProperty(ref _roleplayEnableDirectorStateUpdate, value); }
    public string RoleplayStatePanelMetrics { get { EnsureRoleplayRoutingLoaded(); return _roleplayStatePanelMetrics; } set => SetProperty(ref _roleplayStatePanelMetrics, value); }

    [RelayCommand]
    private void SaveRoleplayRouting()
    {
        var config = SaveRoleplayRoutingCore();
        StatusMessage = "롤플레잉 라우팅 설정을 저장했습니다.";
    }

    [RelayCommand]
    private async Task TestRoleplayCharacterConnectionAsync() => await TestRoleplayConnectionCoreAsync("캐릭터", SaveRoleplayRoutingCore().RoleplayLlm.Character);
    [RelayCommand]
    private async Task TestRoleplayDirectorConnectionAsync() => await TestRoleplayConnectionCoreAsync("디렉터", SaveRoleplayRoutingCore().RoleplayLlm.Director);
    [RelayCommand]
    private async Task TestRoleplayMemoryConnectionAsync() => await TestRoleplayConnectionCoreAsync("메모리", SaveRoleplayRoutingCore().RoleplayLlm.Memory);

    [RelayCommand]
    private void ClearRoleplayCharacterKey() { _secretStore.SaveRoleplayCharacterApiKey(string.Empty); RoleplayCharacterKeyInput = string.Empty; RefreshRoleplaySecretStatuses(); }
    [RelayCommand]
    private void ClearRoleplayDirectorKey() { _secretStore.SaveRoleplayDirectorApiKey(string.Empty); RoleplayDirectorKeyInput = string.Empty; RefreshRoleplaySecretStatuses(); }
    [RelayCommand]
    private void ClearRoleplayMemoryKey() { _secretStore.SaveRoleplayMemoryApiKey(string.Empty); RoleplayMemoryKeyInput = string.Empty; RefreshRoleplaySecretStatuses(); }

    private void EnsureRoleplayRoutingLoaded()
    {
        if (_roleplayRoutingLoaded) return;
        var config = _configLoader.Load();
        LoadRoleplayEndpoint(config.RoleplayLlm.Character, ref _roleplayCharacterProvider, ref _roleplayCharacterModel, ref _roleplayCharacterBaseUrl);
        LoadRoleplayEndpoint(config.RoleplayLlm.Director, ref _roleplayDirectorProvider, ref _roleplayDirectorModel, ref _roleplayDirectorBaseUrl);
        LoadRoleplayEndpoint(config.RoleplayLlm.Memory, ref _roleplayMemoryProvider, ref _roleplayMemoryModel, ref _roleplayMemoryBaseUrl);
        _roleplayEnableStatePanel = config.RoleplayLlm.EnableStatePanel;
        _roleplayEnableDirectorStateUpdate = config.RoleplayLlm.EnableDirectorStateUpdate;
        _roleplayStatePanelMetrics = config.RoleplayLlm.StatePanelMetrics;
        RefreshRoleplaySecretStatuses();
        _roleplayRoutingLoaded = true;
    }

    private AppConfig SaveRoleplayRoutingCore()
    {
        EnsureRoleplayRoutingLoaded();
        var config = _configLoader.Load();
        ApplyRoleplayEndpoint(config.RoleplayLlm.Character, RoleplayCharacterProvider, RoleplayCharacterModel, RoleplayCharacterBaseUrl, DpapiSecretStore.RoleplayCharacterSource, 0.9, 2048);
        ApplyRoleplayEndpoint(config.RoleplayLlm.Director, RoleplayDirectorProvider, RoleplayDirectorModel, RoleplayDirectorBaseUrl, DpapiSecretStore.RoleplayDirectorSource, 0.25, 1024);
        ApplyRoleplayEndpoint(config.RoleplayLlm.Memory, RoleplayMemoryProvider, RoleplayMemoryModel, RoleplayMemoryBaseUrl, DpapiSecretStore.RoleplayMemorySource, 0.2, 1024);
        config.RoleplayLlm.EnableStatePanel = RoleplayEnableStatePanel;
        config.RoleplayLlm.EnableDirectorStateUpdate = RoleplayEnableDirectorStateUpdate;
        config.RoleplayLlm.StatePanelMetrics = string.IsNullOrWhiteSpace(RoleplayStatePanelMetrics) ? "affection,trust,tension" : RoleplayStatePanelMetrics.Trim();
        _configLoader.Save(config);
        if (!string.IsNullOrWhiteSpace(RoleplayCharacterKeyInput)) { _secretStore.SaveRoleplayCharacterApiKey(RoleplayCharacterKeyInput); RoleplayCharacterKeyInput = string.Empty; }
        if (!string.IsNullOrWhiteSpace(RoleplayDirectorKeyInput)) { _secretStore.SaveRoleplayDirectorApiKey(RoleplayDirectorKeyInput); RoleplayDirectorKeyInput = string.Empty; }
        if (!string.IsNullOrWhiteSpace(RoleplayMemoryKeyInput)) { _secretStore.SaveRoleplayMemoryApiKey(RoleplayMemoryKeyInput); RoleplayMemoryKeyInput = string.Empty; }
        RefreshRoleplaySecretStatuses();
        return config;
    }

    private async Task TestRoleplayConnectionCoreAsync(string label, LlmSettings settings)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = $"롤플레잉 {label} 연결을 테스트하는 중입니다.";
        try
        {
            var providerInstance = _providerFactory.Create(settings.Provider);
            var response = await providerInstance.GenerateAsync(new LlmRequest
            {
                Model = settings.Model,
                Options = LlmOptions.FromSettings(settings),
                UserTitle = UserTitle,
                SystemPrompt = "Answer with one short Korean sentence.",
                UserText = "connection test"
            }, CancellationToken.None);
            StatusMessage = $"롤플레잉 {label} 연결 성공: {response.Text}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"롤플레잉 {label} 연결 실패: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshRoleplaySecretStatuses()
    {
        _roleplayCharacterKeyStatus = _secretStore.HasRoleplayCharacterApiKey ? "캐릭터 키 저장됨" : "캐릭터 키 없음";
        _roleplayDirectorKeyStatus = _secretStore.HasRoleplayDirectorApiKey ? "디렉터 키 저장됨" : "디렉터 키 없음";
        _roleplayMemoryKeyStatus = _secretStore.HasRoleplayMemoryApiKey ? "메모리 키 저장됨" : "메모리 키 없음";
        OnPropertyChanged(nameof(RoleplayCharacterKeyStatus));
        OnPropertyChanged(nameof(RoleplayDirectorKeyStatus));
        OnPropertyChanged(nameof(RoleplayMemoryKeyStatus));
    }

    private static void LoadRoleplayEndpoint(LlmSettings settings, ref string provider, ref string model, ref string baseUrl)
    {
        provider = settings.Provider;
        model = settings.Model;
        baseUrl = settings.BaseUrl;
    }

    private static void ApplyRoleplayEndpoint(LlmSettings settings, string provider, string model, string baseUrl, string source, double temperature, int maxTokens)
    {
        settings.Provider = string.IsNullOrWhiteSpace(provider) ? "Gemini" : provider.Trim();
        settings.Model = string.IsNullOrWhiteSpace(model) ? "gemini-3.1-flash-lite" : model.Trim();
        settings.BaseUrl = EnsureTrailingSlash(baseUrl.Trim());
        settings.ApiKeySource = source;
        settings.Temperature = temperature;
        settings.MaxOutputTokens = maxTokens;
        settings.TimeoutSeconds = Math.Clamp(settings.TimeoutSeconds <= 0 ? 45 : settings.TimeoutSeconds, 15, 600);
    }
}
