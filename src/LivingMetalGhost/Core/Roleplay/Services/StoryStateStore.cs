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

    private readonly AppConfigLoader _configLoader;

    public StoryStateStore(AppPaths paths, AppConfigLoader configLoader)
    {
        _configLoader = configLoader;
        _storyRoot = Path.Combine(paths.Root, "story");
        _stateFile = Path.Combine(_storyRoot, "story_state.json");
        _memoryFile = Path.Combine(_storyRoot, "memory.jsonl");
        MigrateLegacyData(paths.Root, _storyRoot);
    }

    public string StoryRoot => _storyRoot;
    public string StateFile => _stateFile;
    public string MemoryFile => _memoryFile;

    public StoryState Load()
    {
        if (!File.Exists(_stateFile))
        {
            return CreateSeededDefault();
        }

        try
        {
            var json = File.ReadAllText(_stateFile);
            var state = JsonSerializer.Deserialize<StoryState>(json, _jsonOptions) ?? CreateDefaultState();
            NormalizeLoadedState(state);
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

    public StoryState SetEnabled(bool enabled)
    {
        var state = Load();
        state.Enabled = enabled;
        state.ShowOpeningOnActivation = enabled && !state.OpeningShown;
        if (enabled && string.IsNullOrWhiteSpace(state.Scene))
        {
            ApplyTemplate(state);
            if (string.IsNullOrWhiteSpace(state.Scene))
            {
                ApplyOpeningScene(state);
            }
        }

        if (state.ShowOpeningOnActivation)
        {
            state.OpeningShown = true;
        }

        state.UpdatedAt = DateTimeOffset.Now;
        Save(state);
        return state;
    }

    public StoryState Reset(bool keepEnabled)
    {
        var previousState = Load();
        var resetState = CreateDefaultState();
        PreserveStoryboard(resetState, previousState);
        resetState.Enabled = keepEnabled;
        resetState.OpeningShown = false;
        resetState.ShowOpeningOnActivation = keepEnabled;
        resetState.Summary = StripRuntimeBeats(previousState.Summary);

        // 리셋은 런타임 진행만 비우고, 캐릭터 전제 같은 시드 기억 텍스처는 템플릿에서 되살린다.
        var template = ResolveActiveTemplate();
        resetState.Facts = template is null ? [] : CloneFacts(template.Facts);
        if (keepEnabled)
        {
            resetState.OpeningShown = true;
        }

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

    public IReadOnlyList<RoleplayMemoryEntry> ReadRecentMemory(int count)
    {
        if (count <= 0 || !File.Exists(_memoryFile))
        {
            return [];
        }

        try
        {
            var entries = new List<RoleplayMemoryEntry>();
            foreach (var line in File.ReadLines(_memoryFile))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize<RoleplayMemoryEntry>(line, _jsonLineOptions);
                    if (entry is not null)
                    {
                        entries.Add(entry);
                    }
                }
                catch (JsonException)
                {
                    // 손상된 줄은 건너뛴다.
                }
            }

            return entries.Count <= count ? entries : entries.GetRange(entries.Count - count, count);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
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
        var summary = state.Summary?.Trim() ?? string.Empty;
        var openingLine = state.OpeningLine?.Trim() ?? string.Empty;

        var builder = new StringBuilder();
        builder.Append(scene);
        if (!string.IsNullOrWhiteSpace(openingLine))
        {
            builder.Append("\n\n").Append(openingLine);
        }

        if (!string.IsNullOrWhiteSpace(summary))
        {
            builder.Append("\n\n").Append(summary);
        }

        builder.Append(
            "\n\n입력 규칙:\n그냥 쓰면 대사로 처리됩니다.\n**이렇게 쓰면 행동이나 상황 설명입니다.**\n*이렇게 쓰면 일반 이탤릭 강조입니다.*\n(이렇게 쓰면 속마음입니다.)");
        return builder.ToString().Trim();
    }

    private StoryState CreateSeededDefault()
    {
        var state = CreateDefaultState();
        ApplyTemplate(state);
        return state;
    }

    /// <summary>활성 캐릭터의 시작 스토리 템플릿이 있으면 StoryState 기본값을 덮어쓴다(없으면 그대로 둔다).</summary>
    private void ApplyTemplate(StoryState state)
    {
        var template = ResolveActiveTemplate();
        if (template is null)
        {
            return;
        }

        state.Title = template.Title;
        state.PlayerRole = template.PlayerRole;
        state.Scene = template.Scene;
        state.Summary = template.Summary;
        state.OpeningLine = template.OpeningLine;
        state.Mood = template.Mood;
        state.Tension = template.Tension;
        state.Facts = CloneFacts(template.Facts);
    }

    private static List<StoryMemoryFact> CloneFacts(IReadOnlyList<StoryMemoryFact> facts)
    {
        return facts
            .Select(fact => new StoryMemoryFact { Kind = fact.Kind, Text = fact.Text, Weight = fact.Weight })
            .ToList();
    }

    private StoryTemplate? ResolveActiveTemplate()
    {
        try
        {
            return StoryTemplateCatalog.Get(_configLoader.Load().App.GhostId);
        }
        catch
        {
            return null;
        }
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
            Location = string.Empty,
            Affection = 0,
            StatusText = "장소와 상황은 아직 고정되지 않았다.",
            OpeningShown = false,
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
        state.Summary = "정체불명의 서비스가 깨어났고, 오르키아와 함께 조심스럽게 상황을 확인한다.";
        state.Mood = "quiet_tension";
        state.Tension = Math.Clamp(state.Tension <= 0 ? 1 : state.Tension, 0, 5);
        if (string.IsNullOrWhiteSpace(state.Location))
        {
            state.Location = "장소 미정";
        }
        if (string.IsNullOrWhiteSpace(state.StatusText))
        {
            state.StatusText = "장소와 상황은 아직 고정되지 않았다.";
        }
    }

    private static void PreserveStoryboard(StoryState target, StoryState source)
    {
        target.Title = string.IsNullOrWhiteSpace(source.Title) ? target.Title : source.Title.Trim();
        target.PlayerRole = string.IsNullOrWhiteSpace(source.PlayerRole) ? target.PlayerRole : source.PlayerRole.Trim();
        target.Scene = string.IsNullOrWhiteSpace(source.Scene) ? target.Scene : source.Scene.Trim();
        target.OpeningLine = string.IsNullOrWhiteSpace(source.OpeningLine) ? target.OpeningLine : source.OpeningLine.Trim();
        target.Mood = string.IsNullOrWhiteSpace(source.Mood) ? target.Mood : source.Mood.Trim();
        target.Tension = Math.Clamp(source.Tension <= 0 ? target.Tension : source.Tension, 0, 5);
    }

    private static void NormalizeLoadedState(StoryState state)
    {
        if (string.Equals(state.Location, "명주고등학교 0층실", StringComparison.OrdinalIgnoreCase))
        {
            state.Location = string.Empty;
        }

        if (state.TurnNumber > 0 || !string.IsNullOrWhiteSpace(state.Summary))
        {
            state.OpeningShown = true;
        }
    }

    private static string StripRuntimeBeats(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return string.Empty;
        }

        var keptLines = new List<string>();
        var skipPossibleCharacterLine = false;
        foreach (var line in summary.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- 사용자:", StringComparison.Ordinal))
            {
                skipPossibleCharacterLine = true;
                continue;
            }

            if (skipPossibleCharacterLine && trimmed.StartsWith("캐릭터:", StringComparison.Ordinal))
            {
                skipPossibleCharacterLine = false;
                continue;
            }

            skipPossibleCharacterLine = false;
            keptLines.Add(line);
        }

        return string.Join(Environment.NewLine, keptLines).Trim();
    }

    private static void MigrateLegacyData(string appRoot, string storyRoot)
    {
        var migrationMarker = Path.Combine(storyRoot, ".legacy-migration-v1");
        if (File.Exists(migrationMarker))
        {
            return;
        }

        var legacyRoot = Path.Combine(appRoot, "Stories", "default");
        try
        {
            Directory.CreateDirectory(storyRoot);
            if (Directory.Exists(legacyRoot))
            {
                CopyIfMissing(
                    Path.Combine(legacyRoot, "story_state.json"),
                    Path.Combine(storyRoot, "story_state.json"));
                CopyIfMissing(
                    Path.Combine(legacyRoot, "memory.jsonl"),
                    Path.Combine(storyRoot, "memory.jsonl"));
            }

            File.WriteAllText(
                migrationMarker,
                $"Legacy migration checked at {DateTimeOffset.Now:O}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void CopyIfMissing(string source, string destination)
    {
        if (File.Exists(source) && !File.Exists(destination))
        {
            File.Copy(source, destination);
        }
    }
}
