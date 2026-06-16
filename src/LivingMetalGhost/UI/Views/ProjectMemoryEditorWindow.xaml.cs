using System.Windows;
using System.Windows.Controls;

namespace LivingMetalGhost.UI.Views;

public partial class ProjectMemoryEditorWindow : Window
{
    public ProjectMemoryEditorWindow(string sourceSessionId, string initialContent)
    {
        InitializeComponent();
        SourceSessionId = sourceSessionId;
        SourceSessionText.Text = $"session: {sourceSessionId}";
        MemoryContentTextBox.Text = initialContent;
        MemoryContentTextBox.SelectAll();
        Loaded += (_, _) => MemoryContentTextBox.Focus();
    }

    public string SourceSessionId { get; }

    public string MemoryType
    {
        get
        {
            if (MemoryTypeComboBox.SelectedItem is ComboBoxItem item && item.Content is string value)
            {
                return value;
            }

            return "decision";
        }
    }

    public string MemoryContent => MemoryContentTextBox.Text.Trim();

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MemoryContent))
        {
            MessageBox.Show(
                "저장할 기억 내용을 입력하세요.",
                "프로젝트 기억 저장",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
