namespace LivingMetalGhost.Core.Reminders;

public sealed record ReminderEntry(
    string Id,
    string Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset DueAt,
    string Status);
