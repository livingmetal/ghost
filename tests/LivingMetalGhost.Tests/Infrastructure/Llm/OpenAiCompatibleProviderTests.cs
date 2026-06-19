using System.Text.Json;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Providers.Llm;
using Xunit;

namespace LivingMetalGhost.Tests.Infrastructure.Llm;

public sealed class OpenAiCompatibleProviderTests
{
    [Theory]
    [InlineData(
        "Gemini",
        "https://generativelanguage.googleapis.com/v1beta/openai/",
        "gemini-3.1-flash-lite",
        true)]
    [InlineData(
        "OpenAI",
        "https://api.openai.com/v1/",
        "gpt-5.4-mini",
        true)]
    [InlineData(
        "openai-compatible",
        "http://localhost:1234/v1/",
        "text-only-model",
        false)]
    [InlineData(
        "openai-compatible",
        "http://localhost:1234/v1/",
        "gemini-3.1-flash-lite",
        true)]
    public void CapabilityPolicy_RecognizesKnownImageEndpointsAndModels(
        string provider,
        string baseUrl,
        string model,
        bool expected)
    {
        var options = new LlmOptions
        {
            Provider = provider,
            BaseUrl = baseUrl,
            Model = model
        };

        Assert.Equal(
            expected,
            LlmCapabilityPolicy.SupportsOpenAiCompatibleImageInput(options));
    }

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
