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
            return JsonSerializer.Deserialize<StoryState>(json, _jsonOptions) ?? CreateDefaultState();
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

    public StoryState SetEnabled(bool enabled)
    {
        var state = Load();
        state.Enabled = enabled;
        if (enabled && string.IsNullOrWhiteSpace(state.Scene))
        {
            ApplyOpeningScene(state);
        }

        state.UpdatedAt = DateTimeOffset.Now;
        Save(state);
        return state;
    }

    public StoryState Reset(bool keepEnabled)
    {
        var resetState = CreateDefaultState();
        resetState.Enabled = keepEnabled;
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
            ? "늦은 밤, 조용한 데이터센터 한가운데에 닫힌 콘솔 하나가 희미하게 깜박인다."
            : state.Scene.Trim();
        var objective = string.IsNullOrWhiteSpace(state.Summary)
            ? "첫 목표: 콘솔이 왜 깨어났는지 알아낸다."
            : state.Summary.Trim();

        return $"""
            {scene}

            {objective}

            입력 규칙:
            그냥 쓰면 대사로 처리됩니다.
            *이렇게 쓰면 행동이나 상황 설명입니다.*
            (이렇게 쓰면 속마음입니다.)
            """.Trim();
    }

    private static StoryState CreateDefaultState()
    {
        var state = new StoryState
        {
            Enabled = false,
            Title = "밤의 데이터센터",
            Scene = string.Empty,
            Summary = string.Empty,
            PlayerRole = "아키텍쳐",
            Mood = "quiet_tension",
            Tension = 1,
            UpdatedAt = DateTimeOffset.Now
        };
        ApplyOpeningScene(state);
        return state;
    }

    private static void ApplyOpeningScene(StoryState state)
    {
        state.Title = string.IsNullOrWhiteSpace(state.Title) || string.Equals(state.Title, "default", StringComparison.OrdinalIgnoreCase)
            ? "밤의 데이터센터"
            : state.Title;
        state.PlayerRole = string.IsNullOrWhiteSpace(state.PlayerRole) ? "아키텍쳐" : state.PlayerRole;
        state.Scene = "늦은 밤의 폐쇄망 데이터센터. 팬 소리는 낮게 깔리고, 사용되지 않아야 할 콘솔 하나가 푸른빛으로 깨어나 있다.";
        state.Summary = "첫 목표: 콘솔이 왜 깨어났는지 확인하고, 오르키아와 함께 안전하게 접근한다.";
        state.Mood = "quiet_tension";
        state.Tension = Math.Clamp(state.Tension <= 0 ? 1 : state.Tension, 0, 5);
    }
}
