using System.IO;
using System.Text.Json;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// StoryMode 상태를 로컬 JSON으로 저장한다. project/user memory와 의도적으로 분리한다.
/// </summary>
public sealed class StoryStateStore
{
    private readonly string _storyRoot;
    private readonly string _stateFile;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public StoryStateStore(AppPaths paths)
    {
        _storyRoot = Path.Combine(paths.Root, "Stories", "default");
        _stateFile = Path.Combine(_storyRoot, "story_state.json");
    }

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
        state.UpdatedAt = DateTimeOffset.Now;
        Save(state);
        return state;
    }

    public void Save(StoryState state)
    {
        Directory.CreateDirectory(_storyRoot);
        state.UpdatedAt = DateTimeOffset.Now;
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        File.WriteAllText(_stateFile, json);
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
            UpdatedAt = DateTimeOffset.Now
        };
    }
}
