using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Security;

namespace LivingMetalGhost.Providers.Llm;

public sealed class OpenAiCompatibleProvider : ILlmProvider
{
    private const int MaximumContinuationRequests = 3;
    private readonly AppConfigLoader _configLoader;
    private readonly DpapiSecretStore _secretStore;

    public OpenAiCompatibleProvider(AppConfigLoader configLoader, DpapiSecretStore secretStore)
    {
        _configLoader = configLoader;
        _secretStore = secretStore;
    }

    public string Name => "OpenAI-Compatible";

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken ct)
    {
        var config = _configLoader.Load();
        var apiKey = _secretStore.LoadApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("설정에서 API Key를 저장해 주세요.");
        }

        if (!Uri.TryCreate(config.Llm.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("Base URL 형식이 올바르지 않습니다.");
        }

        using var httpClient = new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromSeconds(Math.Clamp(config.Llm.TimeoutSeconds, 15, 180))
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var messages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt }
        };
        messages.AddRange(request.History.Select(message => (object)new
        {
            role = message.Role,
            content = message.Content
        }));
        messages.Add(new { role = "user", content = request.UserText });

        var output = new StringBuilder();
        var continuedAutomatically = false;
        var maxOutputTokens = Math.Clamp(config.Llm.MaxOutputTokens, 1024, 8192);

        for (var attempt = 0; attempt <= MaximumContinuationRequests; attempt++)
        {
            var result = await SendRequestAsync(
                httpClient,
                request.Model,
                messages,
                config.Llm.Temperature,
                maxOutputTokens,
                ct);

            if (output.Length > 0 &&
                !char.IsWhiteSpace(output[^1]) &&
                !string.IsNullOrWhiteSpace(result.Text))
            {
                output.Append(' ');
            }

            output.Append(result.Text);
            if (!string.Equals(result.FinishReason, "length", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (attempt == MaximumContinuationRequests)
            {
                output.AppendLine();
                output.Append("[응답이 최대 길이에 도달했습니다.]");
                break;
            }

            continuedAutomatically = true;
            messages.Add(new { role = "assistant", content = result.Text });
            messages.Add(new
            {
                role = "user",
                content = "방금 답변이 길이 제한으로 끊겼습니다. 중복 없이 끊긴 지점부터 자연스럽게 이어서 완성하세요."
            });
        }

        if (output.Length == 0)
        {
            throw new InvalidOperationException("API가 빈 응답을 반환했습니다.");
        }

        return new LlmResponse
        {
            Text = output.ToString().Trim(),
            FromFallback = false,
            ContinuedAutomatically = continuedAutomatically
        };
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var response = await GenerateAsync(request, ct);
        yield return new LlmStreamChunk { Text = response.Text, IsCompleted = true };
    }

    private static async Task<CompletionResult> SendRequestAsync(
        HttpClient httpClient,
        string model,
        IReadOnlyList<object> messages,
        double temperature,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            model,
            messages,
            temperature,
            max_tokens = maxOutputTokens,
            stream = false
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
        using var response = await httpClient.PostAsync("chat/completions", content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, responseBody);
        }

        using var document = JsonDocument.Parse(responseBody);
        var choice = document.RootElement.GetProperty("choices")[0];
        var text = choice.GetProperty("message").GetProperty("content").GetString();
        var finishReason = choice.TryGetProperty("finish_reason", out var finishReasonElement)
            ? finishReasonElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("API가 빈 응답을 반환했습니다.");
        }

        return new CompletionResult(text.Trim(), finishReason);
    }

    private static Exception CreateApiException(HttpStatusCode statusCode, string responseBody)
    {
        var detail = TryReadErrorMessage(responseBody);
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new InvalidOperationException($"API Key 인증에 실패했습니다. {detail}"),
            HttpStatusCode.TooManyRequests =>
                new InvalidOperationException($"무료 사용량 또는 요청 제한을 초과했습니다. {detail}"),
            _ => new HttpRequestException(
                $"AI API 요청이 실패했습니다. HTTP {(int)statusCode}: {detail}")
        };
    }

    private static string TryReadErrorMessage(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
        }

        return responseBody.Length > 300 ? responseBody[..300] : responseBody;
    }

    private sealed record CompletionResult(string Text, string? FinishReason);
}
