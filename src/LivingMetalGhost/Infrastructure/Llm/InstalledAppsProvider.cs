using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Providers.Llm;

/// <summary>
/// Windows에 설치된 ChatGPT 또는 Claude 앱을 자동 감지하여 고급 대화를 처리하는 프로바이더.
///
/// 라우팅 우선순위:
/// - preferred_app = "claude"  → Claude CLI(claude -p) 사용
/// - preferred_app = "chatgpt" → chatgpt CLI 또는 OpenAI 호환 API 사용
/// - preferred_app = "auto"    → Claude CLI 감지 우선, 없으면 ChatGPT 순으로 자동 선택
///
/// ChatGPT 데스크탑 앱이 설치됐지만 CLI가 없는 경우, OpenAI 호환 API(설정의 base_url/key)로 폴백한다.
/// </summary>
public sealed class InstalledAppsProvider : ILlmProvider
{
    private readonly AppConfigLoader _configLoader;
    private readonly OpenAiCompatibleProvider _openAiProvider;
    private readonly LmBotProvider _lmBotProvider;

    public InstalledAppsProvider(
        AppConfigLoader configLoader,
        OpenAiCompatibleProvider openAiProvider,
        LmBotProvider lmBotProvider)
    {
        _configLoader = configLoader;
        _openAiProvider = openAiProvider;
        _lmBotProvider = lmBotProvider;
    }

    public string Name => "InstalledApps";

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken ct)
    {
        var config = _configLoader.Load();
        var preferred = (config.AdvancedLlm.InstalledApps?.PreferredApp ?? "auto")
            .Trim().ToLowerInvariant();
        var info = await InstalledAppDetector.DetectAsync();

        // HasClaude = CLI 또는 데스크탑 앱 중 하나라도 있으면 true.
        // CLI가 없더라도 데스크탑 앱이 있으면 Claude로 라우팅한다(LmBotProvider가 CLI를 다시 확인).
        var useClaude = preferred == "claude" ||
                        (preferred == "auto" && info.HasClaude);
        var useChatGpt = preferred == "chatgpt" ||
                         (preferred == "auto" && !useClaude && info.HasChatGpt);

        if (useClaude)
        {
            return await _lmBotProvider.GenerateAsync(request, ct);
        }

        if (useChatGpt)
        {
            return await RunChatGptAsync(request, ct);
        }

        throw new InvalidOperationException(
            "설치된 AI 앱(ChatGPT 또는 Claude)을 찾을 수 없습니다.\n" +
            "ChatGPT 또는 Claude 데스크탑 앱을 설치하거나 CLI를 PATH에 추가해 주세요.\n" +
            "설정에서 advanced_llm.installed_apps.preferred_app을 'chatgpt' 또는 'claude'로 지정할 수도 있습니다.");
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var response = await GenerateAsync(request, ct);
        yield return new LlmStreamChunk { Text = response.Text, IsCompleted = true };
    }

    // ChatGPT는 OpenAI API 경유로만 지원한다.
    // stdin 기반 chatgpt CLI 도구는 규격이 없어 신뢰성이 낮으므로 사용하지 않는다.
    private Task<LlmResponse> RunChatGptAsync(LlmRequest request, CancellationToken ct) =>
        _openAiProvider.GenerateAsync(request, ct);
}
