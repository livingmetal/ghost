using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Skills;

public sealed class TranslateSkill : IGhostSkill
{
    public string Name => "Translate";
    public string Description => "Simple translation intent placeholder.";
    public IReadOnlyList<string> Examples => ["이 문장 번역해줘"];

    public bool CanHandle(UserRequest request)
        => request.RawText.Contains("번역", StringComparison.OrdinalIgnoreCase);

    public Task<SkillResult> HandleAsync(UserRequest request, CancellationToken ct)
    {
        return Task.FromResult(new SkillResult
        {
            BubbleText = $"번역 요청으로 인식했어요: {request.RawText}",
            Mood = "thinking",
            Action = "translate",
            UsedLlm = false
        });
    }
}

