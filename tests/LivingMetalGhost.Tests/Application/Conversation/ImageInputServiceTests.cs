using LivingMetalGhost.AppCore.Conversation;
using LivingMetalGhost.Core.Models;
using Xunit;

namespace LivingMetalGhost.Tests.Application.Conversation;

public sealed class ImageInputServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "LivingMetalGhost.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_CreatesGeminiCompatibleAttachment()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "sample.png");
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47]);

        var image = ImageInputService.Load(path);

        Assert.Equal("sample.png", image.FileName);
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal("iVBORw==", image.Base64Data);
        Assert.StartsWith("data:image/png;base64,", image.DataUrl);
    }

    [Fact]
    public void BuildTexts_AllowsImageOnlyInput()
    {
        var image = new LlmImageAttachment(
            "sample.webp",
            "image/webp",
            "AA==",
            "sample.webp");

        Assert.Equal(
            "이 이미지를 보고 설명해줘.",
            ImageInputService.BuildPromptText("", image));
        Assert.Equal(
            "[이미지: sample.webp]",
            ImageInputService.BuildDisplayText("", image));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
