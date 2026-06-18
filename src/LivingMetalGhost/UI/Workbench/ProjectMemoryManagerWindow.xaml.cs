using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LivingMetalGhost.UI.Views;

public partial class ProjectMemoryManagerWindow : Window
{
    private readonly ProjectMemoryStore _memoryStore;
    private readonly ObservableCollection<ProjectMemoryEntry> _visibleEntries = [];
    private IReadOnlyList<ProjectMemoryEntry> _allEntries = [];
    private ProjectMemoryEntry? _selectedEntry;

    public ProjectMemoryManagerWindow()
    {
        InitializeComponent();
        _memoryStore = global::LivingMetalGhost.App.Services.GetRequiredService<ProjectMemoryStore>();
        MemoryGrid.ItemsSource = _visibleEntries;
        Loaded += (_, _) => RefreshEntries();
    }

    public bool HasChanges { get; private set; }

    private void RefreshEntries()
    {
        _allEntries = _memoryStore.ReadAll();
        ApplyFilter();
        RefreshStatus();
    }

    private void ApplyFilter()
    {
        var filter = GetSelectedFilter();
        var filtered = _allEntries.AsEnumerable();
        filtered = filter switch
        {
            "enabled" => filtered.Where(entry => entry.IsEnabled),
            "disabled" => filtered.Where(entry => !entry.IsEnabled),
            "decision" or "fact" or "warning" or "todo" or "preference" =>
                filtered.Where(entry => string.Equals(entry.Type, filter, StringComparison.OrdinalIgnoreCase)),
            _ => filtered
        };

        _visibleEntries.Clear();
        foreach (var entry in filtered)
        {
            _visibleEntries.Add(entry);
        }
    }

    private void RefreshStatus()
    {
        var enabledCount = _allEntries.Count(entry => entry.IsEnabled);
        MemoryStatusText.Text = $"{_allEntries.Count} memories / {enabledCount} enabled / {_memoryStore.MemoryFile}";
    }

    private void LoadSelectedEntry(ProjectMemoryEntry? entry)
    {
        _selectedEntry = entry;
        if (entry is null)
        {
            SelectedIdText.Text = "선택 없음";
            EnabledCheckBox.IsChecked = false;
            MemoryTypeComboBox.SelectedIndex = 0;
            ContentTextBox.Text = string.Empty;
            return;
        }

        SelectedIdText.Text = $"id: {entry.Id}\nsession: {entry.SourceSessionId}";
        EnabledCheckBox.IsChecked = entry.IsEnabled;
        SetTypeComboBox(entry.Type);
        ContentTextBox.Text = entry.Content;
    }

    private string GetSelectedFilter()
    {
        return FilterComboBox.SelectedItem is ComboBoxItem item && item.Content is string value
            ? value
            : "all";
    }

    private string GetSelectedType()
    {
        return MemoryTypeComboBox.SelectedItem is ComboBoxItem item && item.Content is string value
            ? value
            : "decision";
    }

    private void SetTypeComboBox(string type)
    {
        for (var index = 0; index < MemoryTypeComboBox.Items.Count; index++)
        {
            if (MemoryTypeComboBox.Items[index] is ComboBoxItem item &&
                item.Content is string value &&
                string.Equals(value, type, StringComparison.OrdinalIgnoreCase))
            {
                MemoryTypeComboBox.SelectedIndex = index;
                return;
            }
        }

        MemoryTypeComboBox.SelectedIndex = 0;
    }

    private void FilterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyFilter();
        RefreshStatus();
    }

    private void MemoryGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadSelectedEntry(MemoryGrid.SelectedItem as ProjectMemoryEntry);
    }

    private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshEntries();
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is null)
        {
            MessageBox.Show("수정할 기억을 선택하세요.", "프로젝트 기억", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var content = ContentTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            MessageBox.Show("기억 내용은 비워둘 수 없습니다.", "프로젝트 기억", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _selectedEntry.IsEnabled = EnabledCheckBox.IsChecked == true;
        _selectedEntry.Type = GetSelectedType();
        _selectedEntry.Content = content;

        var updated = await _memoryStore.UpdateAsync(_selectedEntry, CancellationToken.None);
        if (!updated)
        {
            MessageBox.Show("기억을 저장하지 못했어요.", "프로젝트 기억", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        HasChanges = true;
        RefreshEntries();
        MemoryGrid.SelectedItem = _visibleEntries.FirstOrDefault(entry => entry.Id == _selectedEntry.Id);
    }

    private async void ToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is null)
        {
            MessageBox.Show("활성/비활성할 기억을 선택하세요.", "프로젝트 기억", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _selectedEntry.IsEnabled = !_selectedEntry.IsEnabled;
        EnabledCheckBox.IsChecked = _selectedEntry.IsEnabled;
        var updated = await _memoryStore.UpdateAsync(_selectedEntry, CancellationToken.None);
        if (!updated)
        {
            MessageBox.Show("기억 상태를 바꾸지 못했어요.", "프로젝트 기억", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        HasChanges = true;
        RefreshEntries();
        MemoryGrid.SelectedItem = _visibleEntries.FirstOrDefault(entry => entry.Id == _selectedEntry.Id);
    }

    private async void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is null)
        {
            MessageBox.Show("삭제할 기억을 선택하세요.", "프로젝트 기억", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            "선택한 프로젝트 기억을 삭제할까요?\n이 작업은 project_memory.jsonl에서 해당 항목을 제거합니다.",
            "프로젝트 기억 삭제",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var deleted = await _memoryStore.DeleteAsync(_selectedEntry.Id, CancellationToken.None);
        if (!deleted)
        {
            MessageBox.Show("기억을 삭제하지 못했어요.", "프로젝트 기억", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        HasChanges = true;
        LoadSelectedEntry(null);
        RefreshEntries();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = HasChanges;
        Close();
    }
}
