using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.UI.ViewModels;

public partial class ConversationLogViewModel : ObservableObject
{
    private const string AllModes = "전체";
    private readonly ConversationLogService _logService;
    private readonly List<ConversationLogEntry> _loadedEntries = [];

    [ObservableProperty]
    private DateTime? selectedDate;

    [ObservableProperty]
    private string statusMessage = "저장된 대화가 없습니다.";

    [ObservableProperty]
    private string selectedModeFilter = AllModes;

    public ConversationLogViewModel(ConversationLogService logService)
    {
        _logService = logService;
    }

    public ObservableCollection<DateTime> Dates { get; } = [];
    public ObservableCollection<ConversationLogEntry> Entries { get; } = [];
    public IReadOnlyList<string> ModeFilters { get; } = [AllModes, "일상", "스토리", "고급"];

    partial void OnSelectedModeFilterChanged(string value) => ApplyFilter();

    public async Task LoadAsync()
    {
        Dates.Clear();
        foreach (var date in _logService.GetAvailableDates())
        {
            Dates.Add(date);
        }

        if (Dates.Count == 0)
        {
            Entries.Clear();
            StatusMessage = "저장된 대화가 없습니다.";
            return;
        }

        SelectedDate = Dates[0];
        await LoadDateAsync(Dates[0]);
    }

    public async Task LoadDateAsync(DateTime date)
    {
        _loadedEntries.Clear();
        var entries = await _logService.ReadAsync(date, CancellationToken.None);
        _loadedEntries.AddRange(entries);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Entries.Clear();
        var filtered = _loadedEntries.Where(MatchesFilter).ToList();
        foreach (var entry in filtered)
        {
            Entries.Add(entry);
        }

        if (_loadedEntries.Count == 0)
        {
            StatusMessage = "이 날짜에는 대화가 없습니다.";
        }
        else if (SelectedModeFilter == AllModes)
        {
            StatusMessage = $"{_loadedEntries.Count}개의 대화";
        }
        else
        {
            StatusMessage = $"{SelectedModeFilter} {filtered.Count}개 / 전체 {_loadedEntries.Count}개";
        }
    }

    private bool MatchesFilter(ConversationLogEntry entry)
    {
        return SelectedModeFilter == AllModes || entry.ModeLabel == SelectedModeFilter;
    }
}
