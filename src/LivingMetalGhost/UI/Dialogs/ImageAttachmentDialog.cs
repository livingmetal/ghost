using System.Windows;
using LivingMetalGhost.AppCore.Conversation;
using LivingMetalGhost.Core.Models;
using Microsoft.Win32;

namespace LivingMetalGhost.UI.Dialogs;

public static class ImageAttachmentDialog
{
    private const string ImageFilter =
        "지원 이미지|*.png;*.jpg;*.jpeg;*.webp;*.heic;*.heif|모든 파일|*.*";

    public static LlmImageAttachment? Select(Window owner, string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = ImageFilter
        };
        if (dialog.ShowDialog(owner) != true)
        {
            return null;
        }

        try
        {
            return ImageInputService.Load(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                owner,
                ex.Message,
                "이미지 첨부",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return null;
        }
    }
}
