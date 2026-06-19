using System.Globalization;

namespace LivingMetalGhost.AppCore.SlashAgents.Capabilities;

public sealed class DateCapabilityHandler : ISlashCapabilityHandler
{
    public string Capability => SlashCapabilities.Date;

    public Task<SlashCapabilityResult> ExecuteAsync(
        SlashIntentPlan plan,
        CancellationToken cancellationToken)
    {
        var now = KoreaTime.GetNow();
        var culture = CultureInfo.GetCultureInfo("ko-KR");
        var facts =
            $"현재 한국 날짜는 {now.ToString("yyyy년 M월 d일 dddd", culture)}이며 ISO 날짜는 {now:yyyy-MM-dd}야.";
        return Task.FromResult(new SlashCapabilityResult(Capability, facts));
    }
}
