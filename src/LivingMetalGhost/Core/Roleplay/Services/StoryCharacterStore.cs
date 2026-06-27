using System.IO;
using System.Text.Json;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

public sealed class StoryCharacterStore
{
    private readonly AppPaths _paths;

    public StoryCharacterStore(AppPaths paths)
    {
        _paths = paths;
    }

    public string StoryRoot => Path.Combine(_paths.Root, "story");
    public string DefinitionsFile => Path.Combine(StoryRoot, "story_characters.json");
    public string StatesFile => Path.Combine(StoryRoot, "character_state.json");

    public StoryCharacterDefinition LoadOrCreateDefinition(string characterId, CharacterProfile profile)
    {
        var definitions = LoadDefinitions();
        if (!definitions.TryGetValue(characterId, out var definition))
        {
            definition = new StoryCharacterDefinition
            {
                Id = characterId,
                DisplayName = profile.DisplayName,
                Role = "주요 등장인물",
                BaseAppearance = profile.DefaultAppearance,
                BaseBackground = profile.DefaultBackground,
                BasePersonality = profile.DefaultPersonality,
                Boundaries =
                [
                    "기본 성격은 한두 턴만에 뒤집히지 않는다.",
                    "호감과 신뢰는 사건과 행동의 누적으로 천천히 변한다.",
                    "플레이어의 행동이나 감정을 대신 결정하지 않는다."
                ]
            };
            definitions[characterId] = definition;
            SaveDefinitions(definitions);
        }

        return definition;
    }

    public StoryCharacterState LoadOrCreateState(string characterId)
    {
        var states = LoadStates();
        if (!states.TryGetValue(characterId, out var state))
        {
            state = new StoryCharacterState
            {
                CharacterId = characterId,
                CurrentAppearance = "기본 외형을 유지한다.",
                CurrentEmotion = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["anger"] = 0,
                    ["fear"] = 0,
                    ["confusion"] = 0,
                    ["affection"] = 0,
                    ["trust"] = 0
                },
                RelationshipMetrics = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["affection"] = 0,
                    ["trust"] = 0,
                    ["tension"] = 0
                },
                PersonalityDrift = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["defensiveness"] = 0,
                    ["openness"] = 0,
                    ["dependency"] = 0,
                    ["honesty"] = 0
                },
                CurrentGoal = "현재 장면에서 자신의 태도와 목표를 유지한다."
            };
            states[characterId] = state;
            SaveStates(states);
        }

        return state;
    }

    public void SaveDefinition(StoryCharacterDefinition definition)
    {
        var definitions = LoadDefinitions();
        definitions[definition.Id] = definition;
        SaveDefinitions(definitions);
    }

    public void SaveState(StoryCharacterState state)
    {
        var states = LoadStates();
        state.UpdatedAt = DateTimeOffset.Now;
        states[state.CharacterId] = state;
        SaveStates(states);
    }

    private Dictionary<string, StoryCharacterDefinition> LoadDefinitions()
    {
        Directory.CreateDirectory(StoryRoot);
        if (!File.Exists(DefinitionsFile))
        {
            return new Dictionary<string, StoryCharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, StoryCharacterDefinition>>(
                       File.ReadAllText(DefinitionsFile),
                       JsonOptions) ??
                   new Dictionary<string, StoryCharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, StoryCharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private Dictionary<string, StoryCharacterState> LoadStates()
    {
        Directory.CreateDirectory(StoryRoot);
        if (!File.Exists(StatesFile))
        {
            return new Dictionary<string, StoryCharacterState>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, StoryCharacterState>>(
                       File.ReadAllText(StatesFile),
                       JsonOptions) ??
                   new Dictionary<string, StoryCharacterState>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, StoryCharacterState>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveDefinitions(Dictionary<string, StoryCharacterDefinition> definitions)
    {
        Directory.CreateDirectory(StoryRoot);
        File.WriteAllText(DefinitionsFile, JsonSerializer.Serialize(definitions, JsonOptions));
    }

    private void SaveStates(Dictionary<string, StoryCharacterState> states)
    {
        Directory.CreateDirectory(StoryRoot);
        File.WriteAllText(StatesFile, JsonSerializer.Serialize(states, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };
}
