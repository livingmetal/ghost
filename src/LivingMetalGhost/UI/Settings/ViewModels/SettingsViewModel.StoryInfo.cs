using CommunityToolkit.Mvvm.Input;

namespace LivingMetalGhost.UI.ViewModels;

public partial class SettingsViewModel
{
    private bool _storyInfoLoaded;
    private int _storyInfoTurnNumber;
    private string _storyInfoDate = "03월 05일";
    private string _storyInfoTime = "AM 10:13";
    private string _storyInfoLocation = "명주고등학교 0층실";
    private int _storyInfoAffection = -188;
    private string _storyInfoStatusText = "현재 장면 상태를 입력하세요.";

    public int StoryInfoTurnNumber { get { EnsureStoryInfoLoaded(); return _storyInfoTurnNumber; } set => SetProperty(ref _storyInfoTurnNumber, value); }
    public string StoryInfoDate { get { EnsureStoryInfoLoaded(); return _storyInfoDate; } set => SetProperty(ref _storyInfoDate, value); }
    public string StoryInfoTime { get { EnsureStoryInfoLoaded(); return _storyInfoTime; } set => SetProperty(ref _storyInfoTime, value); }
    public string StoryInfoLocation { get { EnsureStoryInfoLoaded(); return _storyInfoLocation; } set => SetProperty(ref _storyInfoLocation, value); }
    public int StoryInfoAffection { get { EnsureStoryInfoLoaded(); return _storyInfoAffection; } set => SetProperty(ref _storyInfoAffection, value); }
    public string StoryInfoStatusText { get { EnsureStoryInfoLoaded(); return _storyInfoStatusText; } set => SetProperty(ref _storyInfoStatusText, value); }

    [RelayCommand]
    private void SaveStoryInfo()
    {
        EnsureStoryInfoLoaded();
        var state = _storyStateStore.Load();
        state.TurnNumber = Math.Max(0, StoryInfoTurnNumber);
        state.StoryDate = string.IsNullOrWhiteSpace(StoryInfoDate) ? "03월 05일" : StoryInfoDate.Trim();
        state.StoryTime = string.IsNullOrWhiteSpace(StoryInfoTime) ? "AM 10:13" : StoryInfoTime.Trim();
        state.Location = string.IsNullOrWhiteSpace(StoryInfoLocation) ? "장소 미정" : StoryInfoLocation.Trim();
        state.Affection = Math.Clamp(StoryInfoAffection, -999, 999);
        state.StatusText = string.IsNullOrWhiteSpace(StoryInfoStatusText) ? "현재 장면 상태를 입력하세요." : StoryInfoStatusText.Trim();
        _storyStateStore.Save(state);
        StatusMessage = "스토리 Info 값을 저장했습니다.";
    }

    [RelayCommand]
    private void ReloadStoryInfo()
    {
        _storyInfoLoaded = false;
        EnsureStoryInfoLoaded();
        OnPropertyChanged(nameof(StoryInfoTurnNumber));
        OnPropertyChanged(nameof(StoryInfoDate));
        OnPropertyChanged(nameof(StoryInfoTime));
        OnPropertyChanged(nameof(StoryInfoLocation));
        OnPropertyChanged(nameof(StoryInfoAffection));
        OnPropertyChanged(nameof(StoryInfoStatusText));
        StatusMessage = "스토리 Info 값을 다시 불러왔습니다.";
    }

    private void EnsureStoryInfoLoaded()
    {
        if (_storyInfoLoaded) return;
        var state = _storyStateStore.Load();
        _storyInfoTurnNumber = state.TurnNumber;
        _storyInfoDate = string.IsNullOrWhiteSpace(state.StoryDate) ? "03월 05일" : state.StoryDate;
        _storyInfoTime = string.IsNullOrWhiteSpace(state.StoryTime) ? "AM 10:13" : state.StoryTime;
        _storyInfoLocation = string.IsNullOrWhiteSpace(state.Location) ? "명주고등학교 0층실" : state.Location;
        _storyInfoAffection = state.Affection;
        _storyInfoStatusText = string.IsNullOrWhiteSpace(state.StatusText) ? "현재 장면 상태를 입력하세요." : state.StatusText;
        _storyInfoLoaded = true;
    }
}
