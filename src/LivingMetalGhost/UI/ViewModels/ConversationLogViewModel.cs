using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.UI.ViewModels;

public partial class ConversationLogViewModel : ObservableObject
{
    private readonly ConversationLogService _logService;

    [ObservableProperty]
    private DateTime? selectedDate;

    [ObservableProperty]
    private string statusMessage = "저장된 대화가 없습니다.";

    public ConversationLogViewModel(ConversationLogService logService)
    {
        _logService = logService;
    }

    public ObservableCollection<DateTime> Dates { get; } = [];
    public ObservableCollection<ConversationLogEntry> Entries { get; } = [];

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
        Entries.Clear();
        var entries = await _logService.ReadAsync(date, CancellationToken.None);
        foreach (var entry in entries)
        {
            Entries.Add(entry);
        }

        StatusMessage = entries.Count == 0
            ? "이 날짜에는 대화가 없습니다."
            : $"{entries.Count}개의 대화";
    }
}
