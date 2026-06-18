namespace LivingMetalGhost.Core.Models;

public sealed record CharacterImagePromptRequest(
    string ViewId,
    string PoseId,
    string MoodId,
    string? OutfitId = null,
    string? BackgroundHint = null,
    string? LightingHint = null,
    string? CameraHint = null,
    bool UseReferenceImages = true,
    bool IncludeNegativePrompt = true);

public sealed record CharacterImagePromptPackage(
    string PositivePromptKo,
    string PositivePromptEn,
    string NegativePrompt,
    IReadOnlyList<string> ReferenceImagePaths,
    IReadOnlyDictionary<string, string> LockedAttributes,
    IReadOnlyDictionary<string, string> VariableAttributes);

public sealed record CharacterImagePromptProfile(
    IReadOnlyList<string> IdentityTokens,
    IReadOnlyList<string> StyleTokens,
    IReadOnlyList<string> NegativeTokens,
    IReadOnlyList<string> ReferenceImagePaths,
    IReadOnlyDictionary<string, string> ViewPromptOverrides,
    IReadOnlyDictionary<string, string> PosePromptOverrides,
    IReadOnlyDictionary<string, string> MoodPromptOverrides);
