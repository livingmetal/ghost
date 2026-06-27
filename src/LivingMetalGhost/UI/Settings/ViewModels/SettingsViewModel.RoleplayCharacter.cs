using System.IO;
using CommunityToolkit.Mvvm.Input;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.UI.ViewModels;

public partial class SettingsViewModel
{
    private string _loadedRoleplayCharacterId = string.Empty;
    private string _roleplayCharacterDisplayName = string.Empty;
    private string _roleplayCharacterRole = string.Empty;
    private string _roleplayCharacterAppearance = string.Empty;
    private string _roleplayCharacterBackground = string.Empty;
    private string _roleplayCharacterPersonality = string.Empty;
    private string _roleplayCharacterSpeechStyle = string.Empty;
    private string _roleplayCharacterBoundaries = string.Empty;
    private string _roleplayCharacterSecrets = string.Empty;

    public string RoleplayCharacterDisplayName
    {
        get { EnsureRoleplayCharacterDefinitionLoaded(); return _roleplayCharacterDisplayName; }
        set => SetProperty(ref _roleplayCharacterDisplayName, value);
    }

    public string RoleplayCharacterRole
    {
        get { EnsureRoleplayCharacterDefinitionLoaded(); return _roleplayCharacterRole; }
        set => SetProperty(ref _roleplayCharacterRole, value);
    }

    public string RoleplayCharacterAppearance
    {
        get { EnsureRoleplayCharacterDefinitionLoaded(); return _roleplayCharacterAppearance; }
        set => SetProperty(ref _roleplayCharacterAppearance, value);
    }

    public string RoleplayCharacterBackground
    {
        get { EnsureRoleplayCharacterDefinitionLoaded(); return _roleplayCharacterBackground; }
        set => SetProperty(ref _roleplayCharacterBackground, value);
    }

    public string RoleplayCharacterPersonality
    {
        get { EnsureRoleplayCharacterDefinitionLoaded(); return _roleplayCharacterPersonality; }
        set => SetProperty(ref _roleplayCharacterPersonality, value);
    }

    public string RoleplayCharacterSpeechStyle
    {
        get { EnsureRoleplayCharacterDefinitionLoaded(); return _roleplayCharacterSpeechStyle; }
        set => SetProperty(ref _roleplayCharacterSpeechStyle, value);
    }

    public string RoleplayCharacterBoundaries
    {
        get { EnsureRoleplayCharacterDefinitionLoaded(); return _roleplayCharacterBoundaries; }
        set => SetProperty(ref _roleplayCharacterBoundaries, value);
    }

    public string RoleplayCharacterSecrets
    {
        get { EnsureRoleplayCharacterDefinitionLoaded(); return _roleplayCharacterSecrets; }
        set => SetProperty(ref _roleplayCharacterSecrets, value);
    }

    [RelayCommand]
    private void ReloadRoleplayCharacterDefinition()
    {
        _loadedRoleplayCharacterId = string.Empty;
        EnsureRoleplayCharacterDefinitionLoaded();
        RaiseRoleplayCharacterDefinitionProperties();
        StatusMessage = "롤플레잉 캐릭터 정의를 다시 불러왔습니다.";
    }

    [RelayCommand]
    private void SaveRoleplayCharacterDefinition()
    {
        SaveRoleplayCharacterDefinitionCore();
        StatusMessage = "롤플레잉 캐릭터 정의를 저장했습니다.";
    }

    [RelayCommand]
    private void RestoreRoleplayCharacterTemplate()
    {
        var character = CharacterCatalog.Get(SelectedCharacterId);
        var store = new StoryCharacterStore(_paths);
        var definition = store.ResetDefinition(character.Id, character);
        LoadRoleplayCharacterDefinitionIntoFields(definition);
        RaiseRoleplayCharacterDefinitionProperties();
        StatusMessage = "롤플레잉 캐릭터 정의를 기본 템플릿으로 복원했습니다.";
    }

    [RelayCommand]
    private void ClearRoleplaySceneMemory()
    {
        DeleteStoryFile("story_state.json");
        DeleteStoryFile("memory.jsonl");
        LoadStoryStateDraft();
        StatusMessage = "롤플레잉 장면 상태와 대화 기억을 초기화했습니다. 캐릭터 정의는 유지했습니다.";
    }

    [RelayCommand]
    private void ClearRoleplayPlan()
    {
        DeleteStoryFile("story_plan.json");
        StatusMessage = "Writer가 만든 story_plan.json을 초기화했습니다.";
    }

    [RelayCommand]
    private void ClearRoleplayCharacterRuntimeState()
    {
        var character = CharacterCatalog.Get(SelectedCharacterId);
        var store = new StoryCharacterStore(_paths);
        store.ResetState(character.Id);
        StatusMessage = "현재 롤플레잉 캐릭터의 감정/관계/현재 외형 상태를 초기화했습니다.";
    }

    [RelayCommand]
    private void ClearAllRoleplayRuntimeData()
    {
        DeleteStoryFile("story_state.json");
        DeleteStoryFile("memory.jsonl");
        DeleteStoryFile("story_plan.json");
        DeleteStoryFile("character_state.json");
        LoadStoryStateDraft();
        StatusMessage = "롤플레잉 런타임 데이터를 초기화했습니다. story_characters.json은 유지했습니다.";
    }

    private void EnsureRoleplayCharacterDefinitionLoaded()
    {
        var character = CharacterCatalog.Get(SelectedCharacterId);
        if (string.Equals(_loadedRoleplayCharacterId, character.Id, StringComparison.OrdinalIgnoreCase)) return;

        var store = new StoryCharacterStore(_paths);
        var definition = store.LoadOrCreateDefinition(character.Id, character);
        LoadRoleplayCharacterDefinitionIntoFields(definition);
    }

    private void LoadRoleplayCharacterDefinitionIntoFields(StoryCharacterDefinition definition)
    {
        _loadedRoleplayCharacterId = definition.Id;
        _roleplayCharacterDisplayName = definition.DisplayName;
        _roleplayCharacterRole = definition.Role;
        _roleplayCharacterAppearance = definition.BaseAppearance;
        _roleplayCharacterBackground = definition.BaseBackground;
        _roleplayCharacterPersonality = definition.BasePersonality;
        _roleplayCharacterSpeechStyle = definition.SpeechStyle;
        _roleplayCharacterBoundaries = string.Join(Environment.NewLine, definition.Boundaries ?? []);
        _roleplayCharacterSecrets = string.Join(Environment.NewLine, definition.Secrets ?? []);
    }

    private void SaveRoleplayCharacterDefinitionCore()
    {
        EnsureRoleplayCharacterDefinitionLoaded();
        var character = CharacterCatalog.Get(SelectedCharacterId);
        var store = new StoryCharacterStore(_paths);
        store.SaveDefinition(new StoryCharacterDefinition
        {
            Id = character.Id,
            DisplayName = string.IsNullOrWhiteSpace(RoleplayCharacterDisplayName) ? character.DisplayName : RoleplayCharacterDisplayName.Trim(),
            Role = string.IsNullOrWhiteSpace(RoleplayCharacterRole) ? "주요 등장인물" : RoleplayCharacterRole.Trim(),
            BaseAppearance = string.IsNullOrWhiteSpace(RoleplayCharacterAppearance) ? character.DefaultAppearance : RoleplayCharacterAppearance.Trim(),
            BaseBackground = string.IsNullOrWhiteSpace(RoleplayCharacterBackground) ? character.DefaultBackground : RoleplayCharacterBackground.Trim(),
            BasePersonality = string.IsNullOrWhiteSpace(RoleplayCharacterPersonality) ? character.DefaultPersonality : RoleplayCharacterPersonality.Trim(),
            SpeechStyle = string.IsNullOrWhiteSpace(RoleplayCharacterSpeechStyle) ? "장면과 감정 상태에 맞춰 말하되, 기본 성격을 갑자기 뒤집지 않는다." : RoleplayCharacterSpeechStyle.Trim(),
            Boundaries = SplitLines(RoleplayCharacterBoundaries),
            Secrets = SplitLines(RoleplayCharacterSecrets)
        });
    }

    private void RaiseRoleplayCharacterDefinitionProperties()
    {
        OnPropertyChanged(nameof(RoleplayCharacterDisplayName));
        OnPropertyChanged(nameof(RoleplayCharacterRole));
        OnPropertyChanged(nameof(RoleplayCharacterAppearance));
        OnPropertyChanged(nameof(RoleplayCharacterBackground));
        OnPropertyChanged(nameof(RoleplayCharacterPersonality));
        OnPropertyChanged(nameof(RoleplayCharacterSpeechStyle));
        OnPropertyChanged(nameof(RoleplayCharacterBoundaries));
        OnPropertyChanged(nameof(RoleplayCharacterSecrets));
    }

    private void DeleteStoryFile(string fileName)
    {
        var path = Path.Combine(_paths.Root, "story", fileName);
        if (File.Exists(path)) File.Delete(path);
    }

    private static List<string> SplitLines(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
