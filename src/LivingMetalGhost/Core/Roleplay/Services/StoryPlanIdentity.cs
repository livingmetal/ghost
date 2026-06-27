using System.Security.Cryptography;
using System.Text;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

public static class StoryPlanIdentity
{
    public const int CurrentSchemaVersion = 1;

    public static bool Matches(
        StoryPlan plan,
        string characterId,
        StoryWriterSettings settings)
    {
        return plan.HasContent() &&
               plan.SchemaVersion == CurrentSchemaVersion &&
               string.Equals(plan.CharacterId, characterId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   plan.WriterSettingsFingerprint,
                   ComputeWriterSettingsFingerprint(settings),
                   StringComparison.OrdinalIgnoreCase);
    }

    public static string ComputeWriterSettingsFingerprint(StoryWriterSettings settings)
    {
        var canonical = string.Join(
            '\u001f',
            Normalize(settings.Genre),
            Normalize(settings.StoryLength),
            Math.Clamp(settings.RomanceLevel, 0, 5),
            Math.Clamp(settings.MysteryLevel, 0, 5),
            Math.Clamp(settings.ConflictLevel, 0, 5),
            Math.Clamp(settings.HorrorLevel, 0, 5),
            Math.Clamp(settings.ComedyLevel, 0, 5),
            Normalize(settings.RequiredElements),
            Normalize(settings.ForbiddenElements));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static string Normalize(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;
}
