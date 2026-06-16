using System.IO;
using System.Text.Json;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// 캐릭터 폴더에 보관된 story-default.json 들을 읽어 캐릭터별 시작 스토리 템플릿을 제공한다.
/// CharacterCatalog 와 같은 루트(앱 Assets + AppData\Characters)를 훑는다.
/// </summary>
public static class StoryTemplateCatalog
{
    private const string StoryFileName = "story-default.json";

    public static StoryTemplate? Get(string? characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        return LoadAll().TryGetValue(characterId.Trim(), out var template) ? template : null;
    }

    private static IReadOnlyDictionary<string, StoryTemplate> LoadAll()
    {
        var templates = new Dictionary<string, StoryTemplate>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetCharacterRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var storyPath in Directory.EnumerateFiles(root, StoryFileName, SearchOption.AllDirectories))
            {
                var template = TryLoad(storyPath);
                if (template is not null)
                {
                    templates[template.CharacterId] = template;
                }
            }
        }

        return templates;
    }

    private static IEnumerable<string> GetCharacterRoots()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "Characters");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LivingMetalGhost",
            "Characters");
    }

    private static StoryTemplate? TryLoad(string storyPath)
    {
        try
        {
            var json = File.ReadAllText(storyPath);
            var file = JsonSerializer.Deserialize<StoryTemplateFile>(json, JsonOptions);
            if (file is null || string.IsNullOrWhiteSpace(file.CharacterId) || string.IsNullOrWhiteSpace(file.Scene))
            {
                return null;
            }

            return new StoryTemplate(
                file.CharacterId.Trim(),
                string.IsNullOrWhiteSpace(file.Title) ? "이야기" : file.Title.Trim(),
                string.IsNullOrWhiteSpace(file.PlayerRole) ? "주인공" : file.PlayerRole.Trim(),
                file.Scene.Trim(),
                file.Summary?.Trim() ?? string.Empty,
                file.OpeningLine?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(file.Mood) ? "quiet_tension" : file.Mood.Trim(),
                file.Tension <= 0 ? 1 : Math.Clamp(file.Tension, 0, 5),
                BuildObjectives(file.Objectives));
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<StoryObjective> BuildObjectives(List<StoryObjectiveFile>? objectives)
    {
        if (objectives is null || objectives.Count == 0)
        {
            return [];
        }

        var result = new List<StoryObjective>();
        var autoIndex = 1;
        foreach (var item in objectives)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Text))
            {
                continue;
            }

            var id = string.IsNullOrWhiteSpace(item.Id) ? $"G{autoIndex}" : item.Id.Trim();
            result.Add(new StoryObjective { Id = id, Text = item.Text.Trim(), Done = item.Done });
            autoIndex++;
        }

        return result;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private sealed class StoryTemplateFile
    {
        public string CharacterId { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? PlayerRole { get; set; }
        public string Scene { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? OpeningLine { get; set; }
        public string? Mood { get; set; }
        public int Tension { get; set; } = 1;
        public List<StoryObjectiveFile>? Objectives { get; set; }
    }

    private sealed class StoryObjectiveFile
    {
        public string? Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool Done { get; set; }
    }
}
