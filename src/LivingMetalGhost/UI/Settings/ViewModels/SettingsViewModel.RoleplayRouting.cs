using CommunityToolkit.Mvvm.Input;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Security;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.UI.ViewModels;

public partial class SettingsViewModel
{
    private bool _roleplayRoutingLoaded;
    private string _roleplayWriterProvider = "Gemini";
    private string _roleplayWriterModel = "gemini-3.1-flash-lite";
    private string _roleplayWriterBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";
    private string _roleplayWriterKeyInput = string.Empty;
    private string _roleplayWriterKeyStatus = string.Empty;
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
    private bool _roleplayEnableWriter = true;
    private bool _roleplayEnableDirectorStateUpdate = true;
    private bool _roleplayEnableMemory = true;
    private string _roleplayStatePanelMetrics = "affection,trust,tension";
    private string _writerGenre = "현대판타지, 심리극";
    private string _writerStoryLength = "medium";
    private int _writerRomanceLevel = 2;
    private int _writerMysteryLevel = 4;
    private int _writerConflictLevel = 3;
    private int _writerHorrorLevel = 1;
    private int _writerComedyLevel = 1;
    private string _writerRequiredElements = string.Empty;
    private string _writerForbiddenElements = string.Empty;

    public IReadOnlyList<string> RoleplayProviders => Providers;

    public string RoleplayWriterProvider { get { EnsureRoleplayRoutingLoaded(); return _roleplayWriterProvider; } set => SetProperty(ref _roleplayWriterProvider, value); }
    public string RoleplayWriterModel { get { EnsureRoleplayRoutingLoaded(); return _roleplayWriterModel; } set => SetProperty(ref _roleplayWriterModel, value); }
    public string RoleplayWriterBaseUrl { get { EnsureRoleplayRoutingLoaded(); return _roleplayWriterBaseUrl; } set => SetProperty(ref _roleplayWriterBaseUrl, value); }
    public string RoleplayWriterKeyInput { get => _roleplayWriterKeyInput; set => SetProperty(ref _roleplayWriterKeyInput, value); }
    public string RoleplayWriterKeyStatus { get { EnsureRoleplayRoutingLoaded(); return _roleplayWriterKeyStatus; } set => SetProperty(ref _roleplayWriterKeyStatus, value); }

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
    public bool RoleplayEnableWriter { get { EnsureRoleplayRoutingLoaded(); return _roleplayEnableWriter; } set => SetProperty(ref _roleplayEnableWriter, value); }
    public bool RoleplayEnableDirectorStateUpdate { get { EnsureRoleplayRoutingLoaded(); return _roleplayEnableDirectorStateUpdate; } set => SetProperty(ref _roleplayEnableDirectorStateUpdate, value); }
    public bool RoleplayEnableMemory { get { EnsureRoleplayRoutingLoaded(); return _roleplayEnableMemory; } set => SetProperty(ref _roleplayEnableMemory, value); }
    public string RoleplayStatePanelMetrics { get { EnsureRoleplayRoutingLoaded(); return _roleplayStatePanelMetrics; } set => SetProperty(ref _roleplayStatePanelMetrics, value); }

    public string WriterGenre { get { EnsureRoleplayRoutingLoaded(); return _writerGenre; } set => SetProperty(ref _writerGenre, value); }
    public string WriterStoryLength { get { EnsureRoleplayRoutingLoaded(); return _writerStoryLength; } set => SetProperty(ref _writerStoryLength, value); }
    public int WriterRomanceLevel { get { EnsureRoleplayRoutingLoaded(); return _writerRomanceLevel; } set => SetProperty(ref _writerRomanceLevel, value); }
    public int WriterMysteryLevel { get { EnsureRoleplayRoutingLoaded(); return _writerMysteryLevel; } set => SetProperty(ref _writerMysteryLevel, value); }
    public int WriterConflictLevel { get { EnsureRoleplayRoutingLoaded(); return _writerConflictLevel; } set => SetProperty(ref _writerConflictLevel, value); }
    public int WriterHorrorLevel { get { EnsureRoleplayRoutingLoaded(); return _writerHorrorLevel; } set => SetProperty(ref _writerHorrorLevel, value); }
    public int WriterComedyLevel { get { EnsureRoleplayRoutingLoaded(); return _writerComedyLevel; } set => SetProperty(ref _writerComedyLevel, value); }
    public string WriterRequiredElements { get { EnsureRoleplayRoutingLoaded(); return _writerRequiredElements; } set => SetProperty(ref _writerRequiredElements, value); }
    public string WriterForbiddenElements { get { EnsureRoleplayRoutingLoaded(); return _writerForbiddenElements; } set => SetProperty(ref _writerForbiddenElements, value); }

    [RelayCommand] private void SaveRoleplayRouting() { SaveRoleplayRoutingCore(); StatusMessage = "롤플레잉 라우팅 설정을 저장했습니다."; }
    [RelayCommand] private async Task TestRoleplayWriterConnectionAsync() => await TestRoleplayConnectionCoreAsync("작가", SaveRoleplayRoutingCore().RoleplayLlm.Writer);
    [RelayCommand] private async Task TestRoleplayCharacterConnectionAsync() => await TestRoleplayConnectionCoreAsync("캐릭터", SaveRoleplayRoutingCore().RoleplayLlm.Character);
    [RelayCommand] private async Task TestRoleplayDirectorConnectionAsync() => await TestRoleplayConnectionCoreAsync("디렉터", SaveRoleplayRoutingCore().RoleplayLlm.Director);
    [RelayCommand] private async Task TestRoleplayMemoryConnectionAsync() => await TestRoleplayConnectionCoreAsync("메모리", SaveRoleplayRoutingCore().RoleplayLlm.Memory);

    [RelayCommand] private void ClearRoleplayWriterKey() { _secretStore.SaveRoleplayWriterApiKey(string.Empty); RoleplayWriterKeyInput = string.Empty; RefreshRoleplaySecretStatuses(); }
    [RelayCommand] private void ClearRoleplayCharacterKey() { _secretStore.SaveRoleplayCharacterApiKey(string.Empty); RoleplayCharacterKeyInput = string.Empty; RefreshRoleplaySecretStatuses(); }
    [RelayCommand] private void ClearRoleplayDirectorKey() { _secretStore.SaveRoleplayDirectorApiKey(string.Empty); RoleplayDirectorKeyInput = string.Empty; RefreshRoleplaySecretStatuses(); }
    [RelayCommand] private void ClearRoleplayMemoryKey() { _secretStore.SaveRoleplayMemoryApiKey(string.Empty); RoleplayMemoryKeyInput = string.Empty; RefreshRoleplaySecretStatuses(); }

    private void EnsureRoleplayRoutingLoaded()
    {
        if (_roleplayRoutingLoaded) return;
        var config = _configLoader.Load();
        LoadRoleplayEndpoint(config.RoleplayLlm.Writer, ref _roleplayWriterProvider, ref _roleplayWriterModel, ref _roleplayWriterBaseUrl);
        LoadRoleplayEndpoint(config.RoleplayLlm.Character, ref _roleplayCharacterProvider, ref _roleplayCharacterModel, ref _roleplayCharacterBaseUrl);
        LoadRoleplayEndpoint(config.RoleplayLlm.Director, ref _roleplayDirectorProvider, ref _roleplayDirectorModel, ref _roleplayDirectorBaseUrl);
        LoadRoleplayEndpoint(config.RoleplayLlm.Memory, ref _roleplayMemoryProvider, ref _roleplayMemoryModel, ref _roleplayMemoryBaseUrl);
        _roleplayEnableStatePanel = config.RoleplayLlm.EnableStatePanel;
        _roleplayEnableWriter = config.RoleplayLlm.EnableWriter;
        _roleplayEnableDirectorStateUpdate = config.RoleplayLlm.EnableDirectorStateUpdate;
        _roleplayEnableMemory = config.RoleplayLlm.EnableMemory;
        _roleplayStatePanelMetrics = config.RoleplayLlm.StatePanelMetrics;
        _writerGenre = config.RoleplayLlm.WriterSettings.Genre;
        _writerStoryLength = config.RoleplayLlm.WriterSettings.StoryLength;
        _writerRomanceLevel = config.RoleplayLlm.WriterSettings.RomanceLevel;
        _writerMysteryLevel = config.RoleplayLlm.WriterSettings.MysteryLevel;
        _writerConflictLevel = config.RoleplayLlm.WriterSettings.ConflictLevel;
        _writerHorrorLevel = config.RoleplayLlm.WriterSettings.HorrorLevel;
        _writerComedyLevel = config.RoleplayLlm.WriterSettings.ComedyLevel;
        _writerRequiredElements = config.RoleplayLlm.WriterSettings.RequiredElements;
        _writerForbiddenElements = config.RoleplayLlm.WriterSettings.ForbiddenElements;
        RefreshRoleplaySecretStatuses();
        _roleplayRoutingLoaded = true;
    }

    private AppConfig SaveRoleplayRoutingCore()
    {
        EnsureRoleplayRoutingLoaded();
        var config = _configLoader.Load();
        ApplyRoleplayEndpoint(config.RoleplayLlm.Writer, RoleplayWriterProvider, RoleplayWriterModel, RoleplayWriterBaseUrl, DpapiSecretStore.RoleplayWriterSource, 0.7, 4096);
        ApplyRoleplayEndpoint(config.RoleplayLlm.Character, RoleplayCharacterProvider, RoleplayCharacterModel, RoleplayCharacterBaseUrl, DpapiSecretStore.RoleplayCharacterSource, 0.9, 2048);
        ApplyRoleplayEndpoint(config.RoleplayLlm.Director, RoleplayDirectorProvider, RoleplayDirectorModel, RoleplayDirectorBaseUrl, DpapiSecretStore.RoleplayDirectorSource, 0.25, 1024);
        ApplyRoleplayEndpoint(config.RoleplayLlm.Memory, RoleplayMemoryProvider, RoleplayMemoryModel, RoleplayMemoryBaseUrl, DpapiSecretStore.RoleplayMemorySource, 0.2, 1024);
        config.RoleplayLlm.EnableStatePanel = RoleplayEnableStatePanel;
        config.RoleplayLlm.EnableWriter = RoleplayEnableWriter;
        config.RoleplayLlm.EnableDirectorStateUpdate = RoleplayEnableDirectorStateUpdate;
        config.RoleplayLlm.EnableMemory = RoleplayEnableMemory;
        config.RoleplayLlm.StatePanelMetrics = string.IsNullOrWhiteSpace(RoleplayStatePanelMetrics) ? "affection,trust,tension" : RoleplayStatePanelMetrics.Trim();
        config.RoleplayLlm.WriterSettings.Genre = string.IsNullOrWhiteSpace(WriterGenre) ? "현대판타지, 심리극" : WriterGenre.Trim();
        config.RoleplayLlm.WriterSettings.StoryLength = string.IsNullOrWhiteSpace(WriterStoryLength) ? "medium" : WriterStoryLength.Trim();
        config.RoleplayLlm.WriterSettings.RomanceLevel = Math.Clamp(WriterRomanceLevel, 0, 5);
        config.RoleplayLlm.WriterSettings.MysteryLevel = Math.Clamp(WriterMysteryLevel, 0, 5);
        config.RoleplayLlm.WriterSettings.ConflictLevel = Math.Clamp(WriterConflictLevel, 0, 5);
        config.RoleplayLlm.WriterSettings.HorrorLevel = Math.Clamp(WriterHorrorLevel, 0, 5);
        config.RoleplayLlm.WriterSettings.ComedyLevel = Math.Clamp(WriterComedyLevel, 0, 5);
        config.RoleplayLlm.WriterSettings.RequiredElements = WriterRequiredElements.Trim();
        config.RoleplayLlm.WriterSettings.ForbiddenElements = WriterForbiddenElements.Trim();
        _configLoader.Save(config);
        if (!string.IsNullOrWhiteSpace(RoleplayWriterKeyInput)) { _secretStore.SaveRoleplayWriterApiKey(RoleplayWriterKeyInput); RoleplayWriterKeyInput = string.Empty; }
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
        _roleplayWriterKeyStatus = _secretStore.HasRoleplayWriterApiKey ? "작가 키 저장됨" : "작가 키 없음";
        _roleplayCharacterKeyStatus = _secretStore.HasRoleplayCharacterApiKey ? "캐릭터 키 저장됨" : "캐릭터 키 없음";
        _roleplayDirectorKeyStatus = _secretStore.HasRoleplayDirectorApiKey ? "디렉터 키 저장됨" : "디렉터 키 없음";
        _roleplayMemoryKeyStatus = _secretStore.HasRoleplayMemoryApiKey ? "메모리 키 저장됨" : "메모리 키 없음";
        OnPropertyChanged(nameof(RoleplayWriterKeyStatus));
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
