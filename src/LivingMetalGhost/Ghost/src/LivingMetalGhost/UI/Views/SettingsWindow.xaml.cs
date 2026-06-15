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
}
