using System.Windows;
using System.Windows.Controls;
using LivingMetalGhost.UI.ViewModels;

namespace LivingMetalGhost.UI.Views;

public partial class ConversationLogWindow : Window
{
    public ConversationLogWindow()
    {
        InitializeComponent();
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ConversationLogViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }

    private async void DateList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ConversationLogViewModel viewModel &&
            viewModel.SelectedDate is DateTime date)
        {
            await viewModel.LoadDateAsync(date);
        }
    }
}
