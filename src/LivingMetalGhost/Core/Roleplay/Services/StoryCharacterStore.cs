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
            definition = CreateDefaultDefinition(characterId, profile);
            definitions[characterId] = definition;
            SaveDefinitions(definitions);
        }
        else
        {
            NormalizeDefinition(definition, profile);
        }

        return definition;
    }

    public StoryCharacterDefinition ResetDefinition(string characterId, CharacterProfile profile)
    {
        var definitions = LoadDefinitions();
        var definition = CreateDefaultDefinition(characterId, profile);
        definitions[characterId] = definition;
        SaveDefinitions(definitions);
        return definition;
    }

    public StoryCharacterState LoadOrCreateState(string characterId)
    {
        var states = LoadStates();
        if (!states.TryGetValue(characterId, out var state))
        {
            state = CreateDefaultState(characterId);
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

    public void ResetState(string characterId)
    {
        var states = LoadStates();
        states[characterId] = CreateDefaultState(characterId);
        SaveStates(states);
    }

    public void DeleteDefinitionFile()
    {
        if (File.Exists(DefinitionsFile)) File.Delete(DefinitionsFile);
    }

    public void DeleteStateFile()
    {
        if (File.Exists(StatesFile)) File.Delete(StatesFile);
    }

    private static StoryCharacterDefinition CreateDefaultDefinition(string characterId, CharacterProfile profile) => new()
    {
        Id = characterId,
        DisplayName = profile.DisplayName,
        Role = "주요 등장인물",
        BaseAppearance = profile.DefaultAppearance,
        BaseBackground = profile.DefaultBackground,
        BasePersonality = profile.DefaultPersonality,
        SpeechStyle = "장면과 감정 상태에 맞춰 말하되, 기본 성격을 갑자기 뒤집지 않는다.",
        Boundaries =
        [
            "기본 성격은 한두 턴만에 뒤집히지 않는다.",
            "호감과 신뢰는 사건과 행동의 누적으로 천천히 변한다.",
            "플레이어의 행동이나 감정을 대신 결정하지 않는다.",
            "사용자가 지정하지 않은 학교, 병원, 교실, 양호실, 보건실 배경을 새로 만들지 않는다."
        ]
    };

    private static StoryCharacterState CreateDefaultState(string characterId) => new()
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

    private static void NormalizeDefinition(StoryCharacterDefinition definition, CharacterProfile profile)
    {
        if (string.IsNullOrWhiteSpace(definition.Id)) definition.Id = profile.Id;
        if (string.IsNullOrWhiteSpace(definition.DisplayName)) definition.DisplayName = profile.DisplayName;
        if (string.IsNullOrWhiteSpace(definition.Role)) definition.Role = "주요 등장인물";
        if (string.IsNullOrWhiteSpace(definition.BaseAppearance)) definition.BaseAppearance = profile.DefaultAppearance;
        if (string.IsNullOrWhiteSpace(definition.BaseBackground)) definition.BaseBackground = profile.DefaultBackground;
        if (string.IsNullOrWhiteSpace(definition.BasePersonality)) definition.BasePersonality = profile.DefaultPersonality;
        if (string.IsNullOrWhiteSpace(definition.SpeechStyle)) definition.SpeechStyle = "장면과 감정 상태에 맞춰 말하되, 기본 성격을 갑자기 뒤집지 않는다.";
        definition.Boundaries ??= [];
        definition.Secrets ??= [];
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
