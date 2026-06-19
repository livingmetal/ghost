using System.IO;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.AppCore.Conversation;

public static class ImageInputService
{
    public const long MaximumFileBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> MimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".heic"] = "image/heic",
            [".heif"] = "image/heif"
        };

    public static LlmImageAttachment Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("선택한 이미지 파일을 찾을 수 없습니다.", filePath);
        }

        var extension = Path.GetExtension(filePath);
        if (!MimeTypes.TryGetValue(extension, out var mimeType))
        {
            throw new InvalidOperationException(
                "PNG, JPEG, WEBP, HEIC, HEIF 이미지만 첨부할 수 있습니다.");
        }

        var info = new FileInfo(filePath);
        if (info.Length <= 0)
        {
            throw new InvalidOperationException("빈 이미지 파일은 첨부할 수 없습니다.");
        }

        if (info.Length > MaximumFileBytes)
        {
            throw new InvalidOperationException("이미지 파일은 10MB 이하여야 합니다.");
        }

        var bytes = File.ReadAllBytes(filePath);
        return new LlmImageAttachment(
            info.Name,
            mimeType,
            Convert.ToBase64String(bytes),
            info.FullName);
    }

    public static string BuildDisplayText(
        string text,
        LlmImageAttachment? image)
    {
        var trimmed = text.Trim();
        if (image is null)
        {
            return trimmed;
        }

        return string.IsNullOrWhiteSpace(trimmed)
            ? $"[이미지: {image.FileName}]"
            : $"{trimmed}\n[이미지: {image.FileName}]";
    }

    public static string BuildPromptText(string text, LlmImageAttachment? image)
    {
        var trimmed = text.Trim();
        return image is not null && string.IsNullOrWhiteSpace(trimmed)
            ? "이 이미지를 보고 설명해줘."
            : trimmed;
    }
}
