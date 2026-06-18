using System.IO;
using System.Text.Json;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// Builds reusable character asset prompt packages.
/// This class does not call any generation API.
/// </summary>
public sealed class CharacterImagePromptBuilder
{
    public CharacterImagePromptPackage Build(CharacterProfile character, CharacterImagePromptRequest request)
    {
        var rootDirectory = GetRootDirectory(character.Visual);
        var profile = LoadProfile(rootDirectory);
        var viewId = string.IsNullOrWhiteSpace(request.ViewId)
            ? character.Presentation.DefaultFramingPresetId
            : request.ViewId.Trim();
        var poseId = string.IsNullOrWhiteSpace(request.PoseId)
            ? "neutral_stand"
            : request.PoseId.Trim();
        var moodId = string.IsNullOrWhiteSpace(request.MoodId)
            ? "neutral"
            : request.MoodId.Trim();

        var identityBlock = JoinDistinct(
            new[] { character.DefaultAppearance },
            profile.IdentityTokens,
            profile.StyleTokens);
        var variationBlock = JoinDistinct(
            [
                Lookup(profile.ViewPromptOverrides, viewId),
                Lookup(profile.PosePromptOverrides, poseId),
                Lookup(profile.MoodPromptOverrides, moodId),
                request.OutfitId,
                request.CameraHint,
                request.LightingHint,
                request.BackgroundHint
            ]);
        var negativePrompt = request.IncludeNegativePrompt
            ? JoinDistinct(profile.NegativeTokens)
            : string.Empty;
        var referenceImagePaths = request.UseReferenceImages
            ? profile.ReferenceImagePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Path.GetFullPath(Path.Combine(rootDirectory, path)))
                .ToArray()
            : [];

        var lockedAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["character_id"] = character.Id,
            ["display_name"] = character.DisplayName,
            ["appearance"] = character.DefaultAppearance,
            ["background_origin"] = character.DefaultBackground
        };
        var variableAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["view"] = viewId,
            ["pose"] = poseId,
            ["mood"] = moodId,
            ["outfit"] = request.OutfitId ?? string.Empty,
            ["camera"] = request.CameraHint ?? string.Empty,
            ["lighting"] = request.LightingHint ?? string.Empty,
            ["background_hint"] = request.BackgroundHint ?? string.Empty
        };

        var positiveKo = $"캐릭터 정체성 고정: {identityBlock}. 장면 변주: {variationBlock}. " +
                         "허용된 범위 안에서만 포즈, 표정, 프레이밍, 조명, 배경을 바꾸고 얼굴 구조와 핵심 복장 요소는 유지한다.";
        var positiveEn = $"Lock character identity: {identityBlock}. Scene variation: {variationBlock}. " +
                         "Vary only pose, expression, framing, lighting, and background within the approved range; preserve facial structure and key outfit elements.";

        return new CharacterImagePromptPackage(
            positiveKo,
            positiveEn,
            negativePrompt,
            referenceImagePaths,
            lockedAttributes,
            variableAttributes);
    }

    private static CharacterImagePromptProfile LoadProfile(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return EmptyProfile();
        }

        var sidecarPath = Path.Combine(rootDirectory, "image-prompt.json");
        var sidecarProfile = LoadSidecarProfile(sidecarPath);
        if (sidecarProfile is not null)
        {
            return sidecarProfile;
        }

        var manifestPath = Path.Combine(rootDirectory, "manifest.json");
        return LoadManifestProfile(manifestPath) ?? EmptyProfile();
    }

    private static CharacterImagePromptProfile? LoadSidecarProfile(string sidecarPath)
    {
        if (!File.Exists(sidecarPath))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<CharacterImagePromptManifestFile>(
                File.ReadAllText(sidecarPath),
                JsonOptions);
            return manifest is null ? null : ToProfile(manifest);
        }
        catch
        {
            return null;
        }
    }

    private static CharacterImagePromptProfile? LoadManifestProfile(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!document.RootElement.TryGetProperty("image_prompt", out var imagePrompt))
            {
                return null;
            }

            var manifest = JsonSerializer.Deserialize<CharacterImagePromptManifestFile>(
                imagePrompt.GetRawText(),
                JsonOptions);
            return manifest is null ? null : ToProfile(manifest);
        }
        catch
        {
            return null;
        }
    }

    private static CharacterImagePromptProfile ToProfile(CharacterImagePromptManifestFile manifest)
    {
        return new CharacterImagePromptProfile(
            CleanList(manifest.IdentityTokens),
            CleanList(manifest.StyleTokens),
            CleanList(manifest.NegativeTokens),
            CleanList(manifest.ReferenceImages),
            CleanDictionary(manifest.ViewPromptOverrides),
            CleanDictionary(manifest.PosePromptOverrides),
            CleanDictionary(manifest.MoodPromptOverrides));
    }

    private static CharacterImagePromptProfile EmptyProfile()
    {
        return new CharacterImagePromptProfile(
            [],
            [],
            [],
            [],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static string GetRootDirectory(CharacterVisualProfile visual)
    {
        return visual switch
        {
            ModularCharacterVisualProfile modular => modular.RootDirectory,
            SpriteCharacterVisualProfile sprite => sprite.RootDirectory,
            _ => AppContext.BaseDirectory
        };
    }

    private static string Lookup(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static IReadOnlyList<string> CleanList(List<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> CleanDictionary(Dictionary<string, string>? values)
    {
        return (values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string JoinDistinct(params IEnumerable<string?>[] valueGroups)
    {
        return string.Join(", ", valueGroups
            .SelectMany(values => values)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private sealed class CharacterImagePromptManifestFile
    {
        public List<string> IdentityTokens { get; set; } = [];
        public List<string> StyleTokens { get; set; } = [];
        public List<string> NegativeTokens { get; set; } = [];
        public List<string> ReferenceImages { get; set; } = [];
        public Dictionary<string, string> ViewPromptOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> PosePromptOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> MoodPromptOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
