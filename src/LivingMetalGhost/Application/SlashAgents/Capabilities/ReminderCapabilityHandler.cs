using LivingMetalGhost.Core.Reminders;

namespace LivingMetalGhost.AppCore.SlashAgents.Capabilities;

public sealed class ReminderCapabilityHandler : ISlashCapabilityHandler
{
    private readonly ReminderService _reminderService;

    public ReminderCapabilityHandler(ReminderService reminderService)
    {
        _reminderService = reminderService;
    }

    public string Capability => SlashCapabilities.Reminder;

    public async Task<SlashCapabilityResult> ExecuteAsync(
        SlashIntentPlan plan,
        CancellationToken cancellationToken)
    {
        var facts = await _reminderService.CreateFromTextAsync(
            plan.OriginalText,
            cancellationToken);
        return new SlashCapabilityResult(Capability, facts);
    }
}
