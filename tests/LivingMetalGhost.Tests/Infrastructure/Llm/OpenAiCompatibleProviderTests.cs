using System.Text.Json;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Providers.Llm;
using Xunit;

namespace LivingMetalGhost.Tests.Infrastructure.Llm;

public sealed class OpenAiCompatibleProviderTests
{
    [Fact]
    public void BuildUserContent_UsesImageUrlDataBlock()
    {
        var request = new LlmRequest
        {
            UserText = "이 이미지를 설명해줘.",
            Image = new LlmImageAttachment(
                "sample.png",
                "image/png",
                "iVBORw==",
                "sample.png")
        };

        var json = JsonSerializer.Serialize(
            OpenAiCompatibleProvider.BuildUserContent(request));

        Assert.Contains("\"type\":\"text\"", json);
        Assert.Contains("\"type\":\"image_url\"", json);
        Assert.Contains(
            "\"url\":\"data:image/png;base64,iVBORw==\"",
            json);
    }

    [Fact]
    public void BuildUserContent_LeavesTextOnlyRequestsAsString()
    {
        var content = OpenAiCompatibleProvider.BuildUserContent(
            new LlmRequest { UserText = "hello" });

        Assert.Equal("hello", content);
    }
}
