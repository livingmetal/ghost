using System.IO;
using System.Text.Json;

namespace LivingMetalGhost.Core.Models;

public sealed record CharacterProfile(
    string Id,
    string DisplayName,
    string Description,
    string DefaultAppearance,
    string DefaultBackground,
    string DefaultPersonality,
    IReadOnlyList<HiddenCharacterTrait> HiddenTraits,
    CharacterPresentationProfile Presentation,
    CharacterVisualProfile Visual);

public sealed record HiddenCharacterTrait(
    string Id,
    string Prompt,
    int MinReplyGap,
    int MaxReplyGap,
    int MinActiveReplies,
    int MaxActiveReplies);

public sealed record CharacterSizePreset(
    string Id,
    string DisplayName,
    double Scale);

public sealed record CharacterFramingPreset(
    string Id,
    string DisplayName,
    double Zoom,
    double FocusY);

public sealed record CharacterPresentationProfile(
    IReadOnlyList<CharacterSizePreset> SizePresets,
    string DefaultSizePresetId,
    IReadOnlyList<CharacterFramingPreset> FramingPresets,
    string DefaultFramingPresetId);

public abstract record CharacterVisualProfile(string Mode);

public sealed record ModularCharacterState(
    IReadOnlyDictionary<string, string?> LayerPaths);

public sealed record ModularCharacterVisualProfile(
    string RootDirectory,
    IReadOnlyList<string> LayerOrder,
    IReadOnlyDictionary<string, string?> DefaultLayerPaths,
    IReadOnlyDictionary<string, ModularCharacterState> States,
    IReadOnlyList<ModularCharacterState> SpeakingStates,
    IReadOnlyDictionary<string, IReadOnlyList<ModularCharacterState>> SpeakingStatesByState,
    string IdleStateName,
    string BlinkStateName,
    double Width,
    double Height,
    CharacterMotionProfile? IdleMotion,
    CharacterMotionProfile? SpeakingMotion)
    : CharacterVisualProfile("modular");

public sealed record SpriteCharacterVisualProfile(
    string RootDirectory,
    string IdleSpritePath,
    string? BlinkSpritePath,
    IReadOnlyList<string> SpeakingSpritePaths,
    IReadOnlyDictionary<string, string> MoodSpritePaths,
    IReadOnlyDictionary<string, string> MoodBlinkSpritePaths,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MoodCycleSpritePaths,
    double Width,
    double Height,
    CharacterMotionProfile? IdleMotion,
    CharacterMotionProfile? SpeakingMotion)
    : CharacterVisualProfile("sprite");

public sealed record CharacterMotionProfile(
    double FromY,
    double ToY,
    int DurationMilliseconds);

public static class CharacterCatalog
{
    public static IReadOnlyList<CharacterProfile> All => LoadAll();

    public static CharacterProfile Get(string? id)
    {
        var all = LoadAll();
        return all.FirstOrDefault(
                   character => string.Equals(character.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? all.First();
    }

    private static IReadOnlyList<CharacterProfile> LoadAll()
    {
        var characters = new Dictionary<string, CharacterProfile>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetCharacterRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var manifestPath in Directory.EnumerateFiles(root, "manifest.json", SearchOption.AllDirectories))
            {
                var profile = TryLoadProfile(manifestPath);
                if (profile is null)
                {
                    continue;
                }

                characters[profile.Id] = profile;
            }
        }

        return characters.Values
            .OrderBy(character => character.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> GetCharacterRoots()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "Characters");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LivingMetalGhost",
            "Characters");
    }

    private static CharacterProfile? TryLoadProfile(string manifestPath)
    {
        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<CharacterManifestFile>(json, JsonOptions);
            return manifest is null ? null : ToProfile(manifestPath, manifest);
        }
        catch
        {
            return null;
        }
    }

    private static CharacterProfile? ToProfile(string manifestPath, CharacterManifestFile manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id) ||
            string.IsNullOrWhiteSpace(manifest.DisplayName) ||
            manifest.Visual is null)
        {
            return null;
        }

        CharacterVisualProfile? visual = manifest.Visual.Mode.ToLowerInvariant() switch
        {
            "sprite" => BuildSpriteVisual(manifestPath, manifest.Visual),
            "modular" => BuildModularVisual(manifestPath, manifest.Visual),
            _ => null
        };

        if (visual is null)
        {
            return null;
        }

        return new CharacterProfile(
            manifest.Id.Trim(),
            manifest.DisplayName.Trim(),
            manifest.Description?.Trim() ?? string.Empty,
            manifest.DefaultAppearance?.Trim() ?? string.Empty,
            manifest.DefaultBackground?.Trim() ?? string.Empty,
            manifest.DefaultPersonality?.Trim() ?? string.Empty,
            BuildHiddenTraits(manifest.HiddenTraits),
            BuildPresentation(manifest.Visual.Presentation),
            visual);
    }

    private static IReadOnlyList<HiddenCharacterTrait> BuildHiddenTraits(
        List<CharacterHiddenTraitManifestFile>? hiddenTraits)
    {
        if (hiddenTraits is null || hiddenTraits.Count == 0)
        {
            return [];
        }

        return hiddenTraits
            .Where(trait =>
                !string.IsNullOrWhiteSpace(trait.Id) &&
                !string.IsNullOrWhiteSpace(trait.Prompt))
            .Select(trait => new HiddenCharacterTrait(
                trait.Id.Trim(),
                trait.Prompt.Trim(),
                Math.Max(1, trait.MinReplyGap),
                Math.Max(Math.Max(1, trait.MinReplyGap), trait.MaxReplyGap),
                Math.Max(1, trait.MinActiveReplies),
                Math.Max(Math.Max(1, trait.MinActiveReplies), trait.MaxActiveReplies)))
            .ToArray();
    }

    private static CharacterPresentationProfile BuildPresentation(
        CharacterPresentationManifestFile? presentation)
    {
        var defaultPresentation = CreateDefaultPresentation();
        if (presentation is null)
        {
            return defaultPresentation;
        }

        var sizePresets = (presentation.SizePresets ?? [])
            .Where(preset => !string.IsNullOrWhiteSpace(preset.Id) && !string.IsNullOrWhiteSpace(preset.DisplayName))
            .Select(preset => new CharacterSizePreset(
                preset.Id.Trim(),
                preset.DisplayName.Trim(),
                preset.Scale <= 0 ? 1.0 : preset.Scale))
            .ToArray();

        var framingPresets = (presentation.FramingPresets ?? [])
            .Where(preset => !string.IsNullOrWhiteSpace(preset.Id) && !string.IsNullOrWhiteSpace(preset.DisplayName))
            .Select(preset => new CharacterFramingPreset(
                preset.Id.Trim(),
                preset.DisplayName.Trim(),
                preset.Zoom <= 0 ? 1.0 : preset.Zoom,
                Math.Clamp(preset.FocusY, 0.0, 1.0)))
            .ToArray();

        if (sizePresets.Length == 0)
        {
            sizePresets = defaultPresentation.SizePresets.ToArray();
        }

        if (framingPresets.Length == 0)
        {
            framingPresets = defaultPresentation.FramingPresets.ToArray();
        }

        var defaultSizePresetId = sizePresets.Any(preset =>
                string.Equals(preset.Id, presentation.DefaultSizePresetId, StringComparison.OrdinalIgnoreCase))
            ? presentation.DefaultSizePresetId.Trim()
            : sizePresets[0].Id;

        var defaultFramingPresetId = framingPresets.Any(preset =>
                string.Equals(preset.Id, presentation.DefaultFramingPresetId, StringComparison.OrdinalIgnoreCase))
            ? presentation.DefaultFramingPresetId.Trim()
            : framingPresets[0].Id;

        return new CharacterPresentationProfile(
            sizePresets,
            defaultSizePresetId,
            framingPresets,
            defaultFramingPresetId);
    }

    private static CharacterPresentationProfile CreateDefaultPresentation()
    {
        return new CharacterPresentationProfile(
            [
                new CharacterSizePreset("small", "작게", 0.88),
                new CharacterSizePreset("normal", "보통", 1.0),
                new CharacterSizePreset("large", "크게", 1.14)
            ],
            "normal",
            [
                new CharacterFramingPreset("full-body", "전신", 1.0, 0.5),
                new CharacterFramingPreset("three-quarter", "3/4", 1.6, 0.0),
                new CharacterFramingPreset("upper-body", "상반신", 1.85, 0.0)
            ],
            "full-body");
    }

    private static SpriteCharacterVisualProfile BuildSpriteVisual(
        string manifestPath,
        CharacterVisualManifestFile visual)
    {
        var rootDirectory = Path.GetDirectoryName(manifestPath) ?? AppContext.BaseDirectory;
        var speakingSprites = (visual.Sprites?.Speaking ?? [])
            .Select(path => ResolvePath(rootDirectory, path))
            .Where(File.Exists)
            .ToArray();
        var moodSprites = (visual.Sprites?.Moods ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(
                pair => pair.Key,
                pair => ResolvePath(rootDirectory, pair.Value),
                StringComparer.OrdinalIgnoreCase);
        var moodBlinkSprites = (visual.Sprites?.MoodBlink ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(
                pair => pair.Key,
                pair => ResolvePath(rootDirectory, pair.Value),
                StringComparer.OrdinalIgnoreCase);
        var moodCycleSprites = (visual.Sprites?.MoodCycle ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value is { Count: > 0 })
            .ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value
                    .Select(path => ResolvePath(rootDirectory, path))
                    .Where(File.Exists)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return new SpriteCharacterVisualProfile(
            rootDirectory,
            ResolvePath(rootDirectory, visual.Sprites?.Idle),
            ResolvePath(rootDirectory, visual.Sprites?.Blink),
            speakingSprites,
            moodSprites,
            moodBlinkSprites,
            moodCycleSprites,
            visual.Width <= 0 ? 300 : visual.Width,
            visual.Height <= 0 ? 380 : visual.Height,
            ToMotionProfile(visual.IdleMotion),
            ToMotionProfile(visual.SpeakingMotion));
    }

    private static ModularCharacterVisualProfile BuildModularVisual(
        string manifestPath,
        CharacterVisualManifestFile visual)
    {
        var rootDirectory = Path.GetDirectoryName(manifestPath) ?? AppContext.BaseDirectory;
        var modular = visual.Modular ?? new CharacterModularManifestFile();

        var layerOrder = (modular.LayerOrder ?? [])
            .Where(layer => !string.IsNullOrWhiteSpace(layer))
            .Select(layer => layer.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var defaultLayers = (modular.Defaults ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => ResolveOptionalPath(rootDirectory, pair.Value),
                StringComparer.OrdinalIgnoreCase);

        var states = (modular.States ?? new Dictionary<string, Dictionary<string, string?>>(StringComparer.OrdinalIgnoreCase))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => BuildModularState(rootDirectory, pair.Value),
                StringComparer.OrdinalIgnoreCase);

        var speakingStates = (modular.Speaking ?? [])
            .Select(state => BuildModularState(rootDirectory, state))
            .ToArray();
        var speakingStatesByState =
            (modular.SpeakingByState ??
             new Dictionary<string, List<Dictionary<string, string?>>>(StringComparer.OrdinalIgnoreCase))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => (IReadOnlyList<ModularCharacterState>)pair.Value
                    .Select(state => BuildModularState(rootDirectory, state))
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return new ModularCharacterVisualProfile(
            rootDirectory,
            layerOrder,
            defaultLayers,
            states,
            speakingStates,
            speakingStatesByState,
            string.IsNullOrWhiteSpace(modular.IdleState) ? "idle" : modular.IdleState.Trim(),
            string.IsNullOrWhiteSpace(modular.BlinkState) ? "blink" : modular.BlinkState.Trim(),
            visual.Width <= 0 ? 300 : visual.Width,
            visual.Height <= 0 ? 380 : visual.Height,
            ToMotionProfile(visual.IdleMotion),
            ToMotionProfile(visual.SpeakingMotion));
    }

    private static ModularCharacterState BuildModularState(
        string rootDirectory,
        Dictionary<string, string?>? layers)
    {
        var resolved = (layers ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => ResolveOptionalPath(rootDirectory, pair.Value),
                StringComparer.OrdinalIgnoreCase);

        return new ModularCharacterState(resolved);
    }

    private static CharacterMotionProfile? ToMotionProfile(CharacterMotionManifestFile? motion)
    {
        if (motion is null || motion.DurationMilliseconds <= 0)
        {
            return null;
        }

        return new CharacterMotionProfile(
            motion.FromY,
            motion.ToY,
            motion.DurationMilliseconds);
    }

    private static string ResolvePath(string rootDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        return Path.GetFullPath(Path.Combine(rootDirectory, relativePath));
    }

    private static string? ResolveOptionalPath(string rootDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        return ResolvePath(rootDirectory, relativePath);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private sealed class CharacterManifestFile
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DefaultAppearance { get; set; }
        public string? DefaultBackground { get; set; }
        public string? DefaultPersonality { get; set; }
        public List<CharacterHiddenTraitManifestFile> HiddenTraits { get; set; } = [];
        public CharacterVisualManifestFile? Visual { get; set; }
    }

    private sealed class CharacterHiddenTraitManifestFile
    {
        public string Id { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public int MinReplyGap { get; set; } = 100;
        public int MaxReplyGap { get; set; } = 200;
        public int MinActiveReplies { get; set; } = 1;
        public int MaxActiveReplies { get; set; } = 1;
    }

    private sealed class CharacterVisualManifestFile
    {
        public string Mode { get; set; } = "sprite";
        public double Width { get; set; } = 300;
        public double Height { get; set; } = 380;
        public CharacterPresentationManifestFile? Presentation { get; set; }
        public CharacterSpritesManifestFile? Sprites { get; set; }
        public CharacterModularManifestFile? Modular { get; set; }
        public CharacterMotionManifestFile? IdleMotion { get; set; }
        public CharacterMotionManifestFile? SpeakingMotion { get; set; }
    }

    private sealed class CharacterPresentationManifestFile
    {
        public string DefaultSizePresetId { get; set; } = "normal";
        public List<CharacterSizePresetManifestFile> SizePresets { get; set; } = [];
        public string DefaultFramingPresetId { get; set; } = "full-body";
        public List<CharacterFramingPresetManifestFile> FramingPresets { get; set; } = [];
    }

    private sealed class CharacterSizePresetManifestFile
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public double Scale { get; set; } = 1.0;
    }

    private sealed class CharacterFramingPresetManifestFile
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public double Zoom { get; set; } = 1.0;
        public double FocusY { get; set; } = 0.5;
    }

    private sealed class CharacterModularManifestFile
    {
        public List<string> LayerOrder { get; set; } = [];
        public Dictionary<string, string?> Defaults { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Dictionary<string, string?>> States { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public List<Dictionary<string, string?>> Speaking { get; set; } = [];
        public Dictionary<string, List<Dictionary<string, string?>>> SpeakingByState { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public string IdleState { get; set; } = "idle";
        public string BlinkState { get; set; } = "blink";
    }

    private sealed class CharacterSpritesManifestFile
    {
        public string Idle { get; set; } = string.Empty;
        public string? Blink { get; set; }
        public List<string> Speaking { get; set; } = [];
        public Dictionary<string, string> Moods { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> MoodBlink { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> MoodCycle { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CharacterMotionManifestFile
    {
        public double FromY { get; set; }
        public double ToY { get; set; }
        public int DurationMilliseconds { get; set; }
    }
}
