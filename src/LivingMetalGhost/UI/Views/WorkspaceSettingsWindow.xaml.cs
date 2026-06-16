using System.Windows;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LivingMetalGhost.UI.Views;

public partial class WorkspaceSettingsWindow : Window
{
    private readonly WorkspaceStore _workspaceStore;

    public WorkspaceSettingsWindow()
    {
        InitializeComponent();
        _workspaceStore = global::LivingMetalGhost.App.Services.GetRequiredService<WorkspaceStore>();
        Loaded += (_, _) => LoadSettings();
    }

    public bool HasChanges { get; private set; }

    private void LoadSettings()
    {
        var settings = _workspaceStore.Load();
        SettingsFileText.Text = _workspaceStore.SettingsFile;
        WorkspaceIdTextBox.Text = settings.WorkspaceId;
        DisplayNameTextBox.Text = settings.DisplayName;
        RootPathTextBox.Text = settings.RootPath;
        AllowedReadPathsTextBox.Text = string.Join(Environment.NewLine, settings.AllowedReadPaths);
        AllowedWritePathsTextBox.Text = string.Join(Environment.NewLine, settings.AllowedWritePaths);
        AllowedCommandsTextBox.Text = string.Join(Environment.NewLine, settings.AllowedCommands);
        RequireWriteApprovalCheckBox.IsChecked = settings.RequireApprovalForWrite;
        RequireExecuteApprovalCheckBox.IsChecked = settings.RequireApprovalForExecute;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var settings = new WorkspaceSettings
        {
            WorkspaceId = WorkspaceIdTextBox.Text.Trim(),
            DisplayName = DisplayNameTextBox.Text.Trim(),
            RootPath = RootPathTextBox.Text.Trim(),
            AllowedReadPaths = SplitLines(AllowedReadPathsTextBox.Text),
            AllowedWritePaths = SplitLines(AllowedWritePathsTextBox.Text),
            AllowedCommands = SplitLines(AllowedCommandsTextBox.Text),
            RequireApprovalForWrite = RequireWriteApprovalCheckBox.IsChecked == true,
            RequireApprovalForExecute = RequireExecuteApprovalCheckBox.IsChecked == true,
            UpdatedAt = DateTimeOffset.Now
        };

        _workspaceStore.Save(settings);
        HasChanges = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static IReadOnlyList<string> SplitLines(string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }
}
