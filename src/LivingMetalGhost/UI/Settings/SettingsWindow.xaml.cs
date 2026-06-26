using System.Windows;
using LivingMetalGhost.UI.ViewModels;

namespace LivingMetalGhost.UI.Views;

public partial class SettingsWindow : Window
{
    private bool _isLoading;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        _isLoading = true;
        viewModel.Reload();
        ApiKeyPasswordBox.Clear();
        AdvancedApiKeyPasswordBox.Clear();
        RoleplayCharacterApiKeyPasswordBox.Clear();
        RoleplayDirectorApiKeyPasswordBox.Clear();
        RoleplayMemoryApiKeyPasswordBox.Clear();
        _isLoading = false;
    }

    private void ApiKeyPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoading && DataContext is SettingsViewModel viewModel)
        {
            viewModel.ApiKeyInput = ApiKeyPasswordBox.Password;
        }
    }

    private void AdvancedApiKeyPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoading && DataContext is SettingsViewModel viewModel)
        {
            viewModel.AdvancedApiKeyInput = AdvancedApiKeyPasswordBox.Password;
        }
    }

    private void RoleplayCharacterApiKeyPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoading && DataContext is SettingsViewModel viewModel)
        {
            viewModel.RoleplayCharacterKeyInput = RoleplayCharacterApiKeyPasswordBox.Password;
        }
    }

    private void RoleplayDirectorApiKeyPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoading && DataContext is SettingsViewModel viewModel)
        {
            viewModel.RoleplayDirectorKeyInput = RoleplayDirectorApiKeyPasswordBox.Password;
        }
    }

    private void RoleplayMemoryApiKeyPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoading && DataContext is SettingsViewModel viewModel)
        {
            viewModel.RoleplayMemoryKeyInput = RoleplayMemoryApiKeyPasswordBox.Password;
        }
    }
}
