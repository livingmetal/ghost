namespace LivingMetalGhost.AppCore.SlashAgents.Capabilities;

public sealed class TimeCapabilityHandler : ISlashCapabilityHandler
{
    public string Capability => SlashCapabilities.Time;

    public Task<SlashCapabilityResult> ExecuteAsync(
        SlashIntentPlan plan,
        CancellationToken cancellationToken)
    {
        var now = KoreaTime.GetNow();
        var facts =
            $"현재 한국 표준시(KST)는 {now:yyyy-MM-dd HH:mm:ss zzz}야.";
        return Task.FromResult(new SlashCapabilityResult(Capability, facts));
    }
}
