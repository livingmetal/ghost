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
    public bool SupportsImageInput(LlmOptions options) =>
        LlmCapabilityPolicy.SupportsOpenAiCompatibleImageInput(options);

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken ct)
    {
        // Provider 는 호출자가 넘긴 옵션을 기준으로 동작한다.
        // 옵션이 없으면(레거시 호출) 안전하게 전역 기본 설정으로 폴백한다.
        var options = request.Options ?? LlmOptions.FromSettings(_configLoader.Load().Llm);

        var apiKey = _secretStore.LoadApiKey(options.ApiKeySource);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("설정에서 현재 모드의 API Key를 저장해 주세요.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("Base URL 형식이 올바르지 않습니다.");
        }

        using var httpClient = new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 15, 180))
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
        messages.Add(new
        {
            role = "user",
            content = BuildUserContent(request)
        });

        var output = new StringBuilder();
        var continuedAutomatically = false;
        var maxOutputTokens = Math.Clamp(options.MaxOutputTokens, 1024, 8192);
        var model = request.ResolveModel();

        for (var attempt = 0; attempt <= MaximumContinuationRequests; attempt++)
        {
            var result = await SendRequestAsync(
                httpClient,
                model,
                messages,
                options.Temperature,
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

    internal static object BuildUserContent(LlmRequest request)
    {
        if (request.Image is null)
        {
            return request.UserText;
        }

        return new object[]
        {
            new { type = "text", text = request.UserText },
            new
            {
                type = "image_url",
                image_url = new { url = request.Image.DataUrl }
            }
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
        var usesReasoningStyleParameters = UsesOpenAiReasoningStyleParameters(model);
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = messages,
            ["stream"] = false
        };

        if (usesReasoningStyleParameters)
        {
            payload["max_completion_tokens"] = maxOutputTokens;
        }
        else
        {
            payload["temperature"] = temperature;
            payload["max_tokens"] = maxOutputTokens;
        }

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
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            var preview = responseBody.Length > 200 ? responseBody[..200] : responseBody;
            throw new InvalidOperationException(
                $"API 응답 형식이 올바르지 않습니다 (루트가 객체가 아닌 {root.ValueKind}). 응답 앞부분: {preview}");
        }

        var choice = root.GetProperty("choices")[0];
        var messageElement = choice.GetProperty("message");
        var contentElement = messageElement.GetProperty("content");

        // content가 문자열(표준 OpenAI)이면 그대로, 배열(멀티모달/Anthropic)이면 첫 text 블록을 추출
        var text = contentElement.ValueKind == JsonValueKind.Array
            ? ExtractTextFromContentArray(contentElement)
            : contentElement.GetString();

        var finishReason = choice.TryGetProperty("finish_reason", out var finishReasonElement)
            ? finishReasonElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("API가 빈 응답을 반환했습니다.");
        }

        return new CompletionResult(text.Trim(), finishReason);
    }

    private static bool UsesOpenAiReasoningStyleParameters(string model)
    {
        return model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase) ||
               model.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
               model.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
               model.StartsWith("o4", StringComparison.OrdinalIgnoreCase);
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

    private static string? ExtractTextFromContentArray(JsonElement contentArray)
    {
        foreach (var block in contentArray.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var typeEl) &&
                string.Equals(typeEl.GetString(), "text", StringComparison.OrdinalIgnoreCase) &&
                block.TryGetProperty("text", out var textEl))
            {
                return textEl.GetString();
            }
        }

        return null;
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
