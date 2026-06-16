using System.IO;
using System.Text;
using System.Text.Json;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// 롤플레잉 상태를 로컬 JSON으로 저장한다. project/user memory와 의도적으로 분리한다.
/// </summary>
public sealed class StoryStateStore
{
    private const int DefaultAffinity = 50;
    private readonly string _storyRoot;
    private readonly string _stateFile;
    private readonly string _memoryFile;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private readonly JsonSerializerOptions _jsonLineOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public StoryStateStore(AppPaths paths)
    {
        _storyRoot = Path.Combine(paths.Root, "Stories", "default");
        _stateFile = Path.Combine(_storyRoot, "story_state.json");
        _memoryFile = Path.Combine(_storyRoot, "memory.jsonl");
    }

    public string StoryRoot => _storyRoot;
    public string StateFile => _stateFile;
    public string MemoryFile => _memoryFile;

    public StoryState Load()
    {
        if (!File.Exists(_stateFile))
        {
            return CreateDefaultState();
        }

        try
        {
            var json = File.ReadAllText(_stateFile);
            var state = JsonSerializer.Deserialize<StoryState>(json, _jsonOptions) ?? CreateDefaultState();
            state.Affinity = NormalizeAffinity(state.Affinity);
            return state;
        }
        catch (JsonException)
        {
            return CreateDefaultState();
        }
        catch (IOException)
        {
            return CreateDefaultState();
        }
        catch (UnauthorizedAccessException)
        {
            return CreateDefaultState();
        }
    }

    public StoryState SetEnabled(bool enabled, string? storyTemplate)
    {
        var state = Load();
        state.Enabled = enabled;
        if (enabled && IsEmptyStory(state))
        {
            ApplyTemplate(state, storyTemplate);
        }

        state.UpdatedAt = DateTimeOffset.Now;
        Save(state);
        return state;
    }

    public StoryState Reset(bool keepEnabled, string? storyTemplate)
    {
        var resetState = CreateDefaultState();
        resetState.Enabled = keepEnabled;
        ApplyTemplate(resetState, storyTemplate);
        Save(resetState);

        try
        {
            if (File.Exists(_memoryFile))
            {
                File.Delete(_memoryFile);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return resetState;
    }

    public int CountMemoryEntries()
    {
        try
        {
            return File.Exists(_memoryFile)
                ? File.ReadLines(_memoryFile).Count(line => !string.IsNullOrWhiteSpace(line))
                : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    public void Save(StoryState state)
    {
        Directory.CreateDirectory(_storyRoot);
        state.UpdatedAt = DateTimeOffset.Now;
        state.Affinity = NormalizeAffinity(state.Affinity);
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        File.WriteAllText(_stateFile, json);
    }

    public void AppendMemory(RoleplayMemoryEntry entry)
    {
        Directory.CreateDirectory(_storyRoot);
        var json = JsonSerializer.Serialize(entry, _jsonLineOptions);
        File.AppendAllText(_memoryFile, json + Environment.NewLine, Encoding.UTF8);
    }

    public static string BuildOpeningText(StoryState state)
    {
        var scene = string.IsNullOrWhiteSpace(state.Scene)
            ? "아직 시작 장면이 정해지지 않았다."
            : state.Scene.Trim();
        var objective = string.IsNullOrWhiteSpace(state.Summary)
            ? "첫 목표: 장면을 정하고 이야기를 시작한다."
            : state.Summary.Trim();

        return $"""
            {scene}

            {objective}

            상태: 긴장도 {Math.Clamp(state.Tension, 0, 5)}/5 · Affinity {NormalizeAffinity(state.Affinity)}/100

            입력 규칙:
            그냥 쓰면 대사로 처리됩니다.
            **이렇게 쓰면 행동이나 상황 설명입니다.**
            *이렇게 쓰면 일반 이탤릭 강조입니다.*
            (이렇게 쓰면 속마음입니다.)
            """.Trim();
    }

    private static StoryState CreateDefaultState()
    {
        return new StoryState
        {
            Enabled = false,
            Title = "default",
            Scene = string.Empty,
            Summary = string.Empty,
            PlayerRole = "주인공",
            Mood = "daily",
            Tension = 0,
            Affinity = DefaultAffinity,
            UpdatedAt = DateTimeOffset.Now
        };
    }

    private static bool IsEmptyStory(StoryState state) =>
        string.IsNullOrWhiteSpace(state.Scene) &&
        string.IsNullOrWhiteSpace(state.Summary);

    private static void ApplyTemplate(StoryState state, string? storyTemplate)
    {
        var template = ParseTemplate(storyTemplate);
        state.Title = string.IsNullOrWhiteSpace(template.Title) ? "새 이야기" : template.Title;
        state.PlayerRole = string.IsNullOrWhiteSpace(template.PlayerRole) ? "주인공" : template.PlayerRole;
        state.Scene = template.Scene;
        state.Summary = template.Summary;
        state.Mood = string.IsNullOrWhiteSpace(template.Mood) ? "quiet" : template.Mood;
        state.Tension = Math.Clamp(template.Tension ?? 0, 0, 5);
        state.Affinity = NormalizeAffinity(template.Affinity ?? DefaultAffinity);
    }

    private static StoryTemplate ParseTemplate(string? storyTemplate)
    {
        if (string.IsNullOrWhiteSpace(storyTemplate))
        {
            return new StoryTemplate();
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var freeform = new List<string>();
        foreach (var rawLine in storyTemplate.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex > 0)
            {
                var key = NormalizeKey(line[..separatorIndex]);
                var value = line[(separatorIndex + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    values[key] = value;
                    continue;
                }
            }

            freeform.Add(line);
        }

        var scene = Get(values, "scene");
        if (string.IsNullOrWhiteSpace(scene) && freeform.Count > 0)
        {
            scene = string.Join(" ", freeform);
        }

        return new StoryTemplate(
            Get(values, "title"),
            Get(values, "playerrole"),
            scene,
            Get(values, "summary"),
            Get(values, "mood"),
            TryParseInt(Get(values, "tension")),
            TryParseInt(Get(values, "affinity")));
    }

    private static string NormalizeKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
        return normalized switch
        {
            "title" or "제목" => "title",
            "player" or "playerrole" or "role" or "주인공" or "플레이어" or "플레이어역할" => "playerrole",
            "scene" or "opening" or "장면" or "시작장면" => "scene",
            "summary" or "objective" or "goal" or "목표" or "요약" => "summary",
            "mood" or "분위기" => "mood",
            "tension" or "긴장도" => "tension",
            "affinity" or "rapport" or "relationship" => "affinity",
            _ => string.Empty
        };
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static int? TryParseInt(string value) =>
        int.TryParse(value, out var parsed) ? parsed : null;

    private static int NormalizeAffinity(int affinity) =>
        affinity <= 0 ? DefaultAffinity : Math.Clamp(affinity, 0, 100);

    private sealed record StoryTemplate(
        string Title = "",
        string PlayerRole = "",
        string Scene = "",
        string Summary = "",
        string Mood = "",
        int? Tension = null,
        int? Affinity = null);
}
