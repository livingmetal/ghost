using System.Text.Json;
using System.Drawing;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Characters;

public sealed class CharacterAssetContractTests
{
    private static readonly HashSet<string> ForbiddenDirectoryNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "_original",
            "_work",
            "References",
            "Rigging",
            "__pycache__"
        };

    private static readonly HashSet<string> ForbiddenExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".md",
            ".py",
            ".pyc",
            ".svg"
        };

    [Fact]
    public void RuntimeCharacterTree_ContainsOnlyRuntimeMaterial()
    {
        var characterRoot = GetRuntimeCharacterRoot();

        var forbiddenDirectories = Directory
            .EnumerateDirectories(characterRoot, "*", SearchOption.AllDirectories)
            .Where(path => ForbiddenDirectoryNames.Contains(Path.GetFileName(path)))
            .ToArray();
        var forbiddenFiles = Directory
            .EnumerateFiles(characterRoot, "*", SearchOption.AllDirectories)
            .Where(path => ForbiddenExtensions.Contains(Path.GetExtension(path)))
            .ToArray();

        Assert.Empty(forbiddenDirectories);
        Assert.Empty(forbiddenFiles);
    }

    [Fact]
    public void CharacterMetadata_ReferencesExistingRuntimePngFiles()
    {
        var characterRoot = GetRuntimeCharacterRoot();
        var metadataFiles = Directory
            .EnumerateFiles(characterRoot, "manifest.json", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(
                characterRoot,
                "image-prompt.json",
                SearchOption.AllDirectories))
            .ToArray();

        Assert.NotEmpty(metadataFiles);

        foreach (var metadataFile in metadataFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(metadataFile));
            var metadataRoot = Path.GetDirectoryName(metadataFile)!;
            var missingPaths = EnumerateStrings(document.RootElement)
                .Where(value => value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .Select(value => Path.GetFullPath(Path.Combine(metadataRoot, value)))
                .Where(path => !File.Exists(path))
                .ToArray();

            Assert.True(
                missingPaths.Length == 0,
                $"{Path.GetFileName(metadataFile)} has missing PNG references:{Environment.NewLine}" +
                string.Join(Environment.NewLine, missingPaths));
        }
    }

    [Fact]
    public void OrkiaAuthoring_UsesNaturalWaveForwardClaspedBaseline()
    {
        var repositoryRoot = FindRepositoryRoot();
        var runtimeRoot = Path.Combine(
            repositoryRoot,
            "src",
            "LivingMetalGhost",
            "Assets",
            "Characters",
            "Orkia");
        var authoringRoot = Path.Combine(
            repositoryRoot,
            "authoring",
            "Characters",
            "Orkia");

        using var runtimeManifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(runtimeRoot, "manifest.json")));
        var defaultAppearance = runtimeManifest.RootElement
            .GetProperty("default_appearance")
            .GetString() ?? string.Empty;
        Assert.Contains("네추럴 웨이브", defaultAppearance);
        Assert.DoesNotContain("높게 묶", defaultAppearance);

        using var rigManifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(
                authoringRoot,
                "Rigging",
                "rig-manifest.draft.json")));
        Assert.Equal(
            "natural-wave",
            rigManifest.RootElement
                .GetProperty("hair_style")
                .GetProperty("id")
                .GetString());
        Assert.Equal(
            "forward_clasped",
            rigManifest.RootElement
                .GetProperty("state_pose_map")
                .GetProperty("idle")
                .GetString());

        var masterPath = Path.Combine(
            authoringRoot,
            "Rigging",
            "masters",
            "orkia-natural-wave-forward-clasped-master-4x.png");
        using var master = Image.FromFile(masterPath);
        Assert.Equal(1200, master.Width);
        Assert.Equal(2392, master.Height);
        Assert.True(Image.IsAlphaPixelFormat(master.PixelFormat));

        Assert.True(File.Exists(Path.Combine(
            authoringRoot,
            "Rigging",
            "body-templates",
            "forward_clasped.svg")));
        Assert.True(File.Exists(Path.Combine(
            authoringRoot,
            "HairStyles",
            "natural-wave",
            "hair-style.draft.json")));
        Assert.False(Directory.Exists(Path.Combine(
            authoringRoot,
            "HairStyles",
            "high-ponytail")));
    }

    private static IEnumerable<string> EnumerateStrings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var propertyValue in EnumerateStrings(property.Value))
                    {
                        yield return propertyValue;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var itemValue in EnumerateStrings(item))
                    {
                        yield return itemValue;
                    }
                }

                break;
            case JsonValueKind.String:
                if (element.GetString() is { } stringValue)
                {
                    yield return stringValue;
                }

                break;
        }
    }

    private static string GetRuntimeCharacterRoot()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "src",
            "LivingMetalGhost",
            "Assets",
            "Characters");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "README.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "src", "LivingMetalGhost")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository root from test output directory.");
    }
}
